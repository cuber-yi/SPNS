using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Project.Models;
using Project.Services.Abstractions;
using Project.Services.Implementations;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Project.Workers
{
    public class MainWorker : BackgroundService
    {
        private readonly ILogger<MainWorker> _logger;
        private readonly IMqttService _mqttService;
        private readonly IJsonFileService _jsonFileService;
        private readonly IDisplayService _displayService;
        private readonly IOptimizationService _optimizationService;
        private readonly ICalculationService _calculationService;
        private readonly FilePathsOptions _filePaths;
        private readonly MqttOptions _mqttOptions;

        // 状态管理字段
        private readonly ConcurrentDictionary<string, string> _pendingRequests;
        private bool _calculationResultSent;
        private bool _feedbackReceived;

        public MainWorker(
            ILogger<MainWorker> logger,
            IMqttService mqttService,
            IJsonFileService jsonFileService,
            IDisplayService displayService,
            IOptimizationService optimizationService,
            ICalculationService calculationService,
            IOptions<FilePathsOptions> filePathsOptions,
            IOptions<MqttOptions> mqttOptions)
        {
            _logger = logger;
            _mqttService = mqttService;
            _jsonFileService = jsonFileService;
            _displayService = displayService;
            _optimizationService = optimizationService;
            _calculationService = calculationService;
            _filePaths = filePathsOptions.Value;
            _mqttOptions = mqttOptions.Value;

            // 初始化状态
            _pendingRequests = new ConcurrentDictionary<string, string>();
            _calculationResultSent = false;
            _feedbackReceived = false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MainWorker 服务已启动。");

            // --- 订阅MQTT事件 ---
            _mqttService.ConnectedAsync += OnConnectedAsync;
            _mqttService.MessageReceivedAsync += OnMessageReceivedAsync;
            _mqttService.DisconnectedAsync += OnDisconnectedAsync;

            // --- 启动MQTT连接 ---
            await _mqttService.ConnectAsync();

            // --- 主应用循环 ---
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingRequests();
                    await PublishResultsAndWaitForFeedback(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "主循环出错。");
                }

                await Task.Delay(500, stoppingToken); // 等待，避免CPU占用过高
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MainWorker 服务正在停止。");

            // 取消事件订阅，防止内存泄漏
            _mqttService.ConnectedAsync -= OnConnectedAsync;
            _mqttService.MessageReceivedAsync -= OnMessageReceivedAsync;
            _mqttService.DisconnectedAsync -= OnDisconnectedAsync;

            return base.StopAsync(cancellationToken);
        }

        private async Task ProcessPendingRequests()
        {
            if (_pendingRequests.IsEmpty) return;

            // 一次只处理一个请求
            var (topic, payload) = _pendingRequests.First();
            if (_pendingRequests.TryRemove(topic, out payload))
            {
                SystemComponents finalComponents = null;

                if (topic == _mqttOptions.StructureTopic)
                {
                    _logger.LogInformation("开始处理 'Structure' 请求，将执行基础水力计算...");
                    await File.WriteAllTextAsync(_filePaths.Structure, payload);
                    var components = _jsonFileService.DecodeStructure(_filePaths.Structure);
                    _displayService.DisplayStructure(components);
                    _logger.LogInformation("正在使用JSON文件中的初始压力进行基础水力计算...");

                    // 对于基础计算，我们直接使用文件中的初始压力
                    foreach (var station in components.AirCompressionStations)
                    {
                        station.TempPressure = station.InitialPressure;
                    }
                    if (_calculationService.GasDuctCalc(ref components))
                    {
                        _calculationService.LoadData(ref components);
                        _logger.LogInformation("基础水力计算成功。");
                    }
                    else
                    {
                        _logger.LogWarning("基础水力计算未能满足约束条件.");
                        _calculationService.LoadData(ref components);
                    }

                    finalComponents = components;
                    _jsonFileService.GenerateOutputFile(_filePaths.Output, finalComponents);
                    _logger.LogInformation("'Structure' 请求处理及计算完成。");
                }
                else if (topic == _mqttOptions.StaticCalculateTopic)
                {
                    _logger.LogInformation("开始处理 'StaticCalculate' 静态寻优请求...");
                    await File.WriteAllTextAsync(_filePaths.StaticCalculate, payload);
                    var components = _jsonFileService.DecodeStructure(_filePaths.StaticCalculate);
                    foreach (var station in components.AirCompressionStations)
                    {
                        station.TempPressure = station.InitialPressure;
                    }
                    finalComponents = _optimizationService.FindOptimalPressures(components);

                    _jsonFileService.GenerateOutputFile(_filePaths.Output, finalComponents);
                    _logger.LogInformation("静态寻优计算完成。");
                }
                else if (topic == _mqttOptions.DynamicCalculateTopic)
                {
                    _logger.LogInformation("开始处理 'DynamicCalculate' 动态寻优请求...");
                    await File.WriteAllTextAsync(_filePaths.DynamicCalculate, payload);
                    var baseComponents = _jsonFileService.DecodeStructure(_filePaths.Structure);
                    foreach (var station in baseComponents.AirCompressionStations)
                    {
                        station.TempPressure = station.InitialPressure;
                    }
                    var componentsWithFlowChange = _jsonFileService.ReadUsersFlowChange(_filePaths.DynamicCalculate, baseComponents);
                    finalComponents = _optimizationService.FindOptimalPressures(componentsWithFlowChange);
                    _jsonFileService.GenerateOutputFile(_filePaths.Output, finalComponents);
                    _logger.LogInformation("动态寻优计算完成。");
                }

                if (finalComponents != null)
                {
                    _displayService.DisplayResults(finalComponents);
                    _jsonFileService.GenerateOutputFile(_filePaths.Output, finalComponents);
                    _calculationResultSent = true;
                    _feedbackReceived = false; // 重置反馈标志
                }
            }
        }

        private async Task PublishResultsAndWaitForFeedback(CancellationToken stoppingToken)
        {
            // 只有当有结果需要发送时，才执行此逻辑
            if (!_calculationResultSent)
            {
                return;
            }

            // 这个try-finally结构确保无论成功或失败，状态都会被重置
            try
            {
                // 1. 发送一次结果
                _logger.LogInformation("正在发送计算结果至主题 '{Topic}'...", _mqttOptions.ResultTopic);
                string resultPayload = await File.ReadAllTextAsync(_filePaths.Output, stoppingToken);
                await _mqttService.PublishAsync(_mqttOptions.ResultTopic, resultPayload);

                _logger.LogInformation("结果已发送，将在10秒内等待前端确认...");

                // 2. 创建一个10秒的超时CancellationToken
                using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    // 3. 将服务停止token和超时token关联起来
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token))
                    {
                        // 4. 循环等待，直到收到反馈或超时
                        while (!_feedbackReceived)
                        {
                            // 如果token被取消（超时或服务停止），将抛出OperationCanceledException
                            linkedCts.Token.ThrowIfCancellationRequested();
                            await Task.Delay(100, linkedCts.Token); // 短暂等待，避免空耗CPU
                        }
                    }
                }

                // 5. 如果代码能执行到这里，说明是在超时前收到了反馈
                _logger.LogInformation("在超时前收到前端确认，发送成功！");
                Console.WriteLine("============================================\n");
            }
            catch (OperationCanceledException)
            {
                // 6. 如果等待被取消，检查是否是由于超时（即仍然没有收到反馈）
                if (!_feedbackReceived)
                {
                    _logger.LogWarning("未在10秒内收到前端确认，发送失败。将进入下一个周期。");
                    Console.WriteLine("============================================\n");
                }
            }
            finally
            {
                // 7. 无论成功还是失败，都重置状态标志，以便程序可以处理下一个请求
                _calculationResultSent = false;
                _feedbackReceived = false;
            }
        }

        // --- MQTT 事件处理器 ---
        private Task OnConnectedAsync()
        {
            _logger.LogInformation("连接成功，开始订阅主题...");
            return Task.WhenAll(
                _mqttService.SubscribeAsync(_mqttOptions.StructureTopic),
                _mqttService.SubscribeAsync(_mqttOptions.StaticCalculateTopic),
                _mqttService.SubscribeAsync(_mqttOptions.DynamicCalculateTopic),
                _mqttService.SubscribeAsync(_mqttOptions.FeedbackTopic)
            );
        }

        private Task OnMessageReceivedAsync(string topic, string payload)
        {
            if (topic == _mqttOptions.StructureTopic ||
                topic == _mqttOptions.StaticCalculateTopic ||
                topic == _mqttOptions.DynamicCalculateTopic)
            {
                _pendingRequests[topic] = payload;
                _logger.LogInformation("已将来自主题 '{Topic}' 的请求加入处理队列。", topic);
            }
            else if (topic == _mqttOptions.FeedbackTopic)
            {
                _logger.LogInformation("前端已确认接收到计算结果。");
                _feedbackReceived = true;
            }
            return Task.CompletedTask;
        }

        private Task OnDisconnectedAsync()
        {
            _logger.LogWarning("与 MQTT Broker 的连接已断开。 MqttService 内部将处理重连。");
            return Task.CompletedTask;
        }
    }
}
