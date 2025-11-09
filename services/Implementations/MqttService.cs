using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using Project.Services.Abstractions;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Project.Services.Implementations
{
    public class MqttService : IMqttService
    {
        private readonly ILogger<MqttService> _logger;
        private readonly MqttOptions _mqttOptions;
        private IMqttClient _mqttClient;

        public event Func<string, string, Task> MessageReceivedAsync;
        public event Func<Task> ConnectedAsync;
        public event Func<Task> DisconnectedAsync;

        public bool IsConnected => _mqttClient?.IsConnected ?? false;

        public MqttService(IOptions<MqttOptions> mqttOptions, ILogger<MqttService> logger)
        {
            _logger = logger;
            _mqttOptions = mqttOptions.Value;
            InitializeMqttClient();
        }

        private void InitializeMqttClient()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            _mqttClient.ConnectedAsync += async e =>
            {
                _logger.LogInformation("成功连接到 MQTT Broker.");
                if (ConnectedAsync != null)
                {
                    await ConnectedAsync.Invoke();
                }
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("与 MQTT Broker 的连接已断开。");
                if (DisconnectedAsync != null)
                {
                    await DisconnectedAsync.Invoke();
                }

                // 可选：添加自动重连逻辑
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    _logger.LogInformation("尝试重新连接...");
                    await ConnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "重新连接失败。");
                }
            };

            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                _logger.LogInformation("接收到消息 - 主题: '{Topic}', 载荷大小: {PayloadSize} bytes", topic, payload.Length);

                if (MessageReceivedAsync != null)
                {
                    await MessageReceivedAsync.Invoke(topic, payload);
                }
            };
        }

        public async Task ConnectAsync()
        {
            if (IsConnected)
            {
                _logger.LogInformation("客户端已经连接。");
                return;
            }

            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttOptions.Server, _mqttOptions.Port)
                .WithClientId(_mqttOptions.ClientId)
                .WithCleanSession(false)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(50))
                .Build();

            try
            {
                var result = await _mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                _logger.LogInformation("MQTT 连接结果: {ResultCode}", result.ResultCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "连接 MQTT Broker 时发生错误。");
                throw;
            }
        }

        public async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("客户端未连接，无法发布消息。");
                return;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(retain)
                .Build();

            var result = await _mqttClient.PublishAsync(message, CancellationToken.None);
            if (result.IsSuccess)
            {
                _logger.LogInformation("消息已成功发布到主题: '{Topic}'", topic);
            }
            else
            {
                _logger.LogWarning("发布消息到主题 '{Topic}' 失败: {Reason}", topic, result.ReasonString);
            }
        }

        public async Task SubscribeAsync(string topic)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("客户端未连接，无法订阅主题。");
                return;
            }

            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .Build();

            var result = await _mqttClient.SubscribeAsync(topicFilter, CancellationToken.None);

            // _logger.LogInformation("订阅主题 '{Topic}' 的结果: {ResultCode}", topic, result.Items.FirstOrDefault()?.ResultCode);
        }
    }
}
