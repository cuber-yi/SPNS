using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Project.Models;
using Project.Services.Abstractions;


namespace Project.Services.Implementations
{
    public class OptimizationService : IOptimizationService
    {
        private readonly ICalculationService _calculationService;
        private readonly ILogger<OptimizationService> _logger;

        // 算法参数
        private readonly int _populationSize;
        private readonly int _maxIterations;
        private readonly double _stepSize;
        private double[] _individualLowerBounds;
        private double[] _individualUpperBounds;

        // 问题参数
        private int _dimension;
        private double[] _basePressures;
        private double[] _initialSolution;  // 保存初始解
        private double _initialFitness;     // 保存初始解的适应度


        public OptimizationService(ICalculationService calculationService, IOptions<OptimizationOptions> options, ILogger<OptimizationService> logger)
        {
            _calculationService = calculationService;
            _logger = logger;

            var optimizationOptions = options.Value;
            _populationSize = optimizationOptions.PopulationSize;
            _maxIterations = optimizationOptions.MaxIterations;
            _stepSize = optimizationOptions.StepSize;
        }

        public SystemComponents FindOptimalPressures(SystemComponents initialComponents)
        {
            _dimension = initialComponents.AirCompressionStations.Count;
            if (_dimension == 0)
            {
                _logger.LogWarning("系统中没有配置空压站，跳过优化。");
                return initialComponents;
            }

            // 初始化算法的边界
            InitializeBounds(initialComponents);

            // 保存并评估初始解
            _initialSolution = ExtractCurrentSolution(initialComponents);
            _initialFitness = FitnessFunction(_initialSolution, initialComponents);

            _logger.LogInformation("初始解适应度: {InitialFitness}", _initialFitness);
            _logger.LogInformation("开始优化算法启动，种群大小: {PopulationSize}, 最大迭代次数: {MaxIterations}", _populationSize, _maxIterations);

            // 运行算法寻找最优解
            double[] bestPosition = RunOptimization(initialComponents);

            // 确保找到的解比初始解更优
            double bestFitness = FitnessFunction(bestPosition, initialComponents);
            if (bestFitness >= _initialFitness)
            {
                _logger.LogWarning("优化算法未找到比初始解更优的组合，保持原始配置。初始适应度: {InitialFitness}, 最优适应度: {BestFitness}",
                    _initialFitness, bestFitness);
                return initialComponents; // 返回原始配置
            }

            // 应用找到的更优解
            for (int i = 0; i < bestPosition.Length; i++)
            {
                bestPosition[i] = RoundToStepSize(bestPosition[i]);
            }

            _logger.LogInformation("找到更优解，准备应用最优压力进行最终计算...");
            _logger.LogInformation("改进程度: {Improvement:N2}", _initialFitness - bestFitness);

            // 应用最优解
            for (int i = 0; i < _dimension; i++)
            {
                initialComponents.AirCompressionStations[i].TempPressure = Math.Round(bestPosition[i]);
            }

            // 执行最终计算
            if (_calculationService.GasDuctCalc(ref initialComponents))
            {
                _calculationService.LoadData(ref initialComponents);
                _logger.LogInformation("已成功应用最优压力并完成最终计算。");
            }
            else
            {
                _logger.LogWarning("在应用最优压力后，管网计算结果无法满足约束条件。返回非优化状态。");
                return RestoreInitialSolution(initialComponents);
            }

            return initialComponents;
        }


        // 提取当前解（压力增量）
        private double[] ExtractCurrentSolution(SystemComponents components)
        {
            var solution = new double[_dimension];
            for (int i = 0; i < _dimension; i++)
            {
                // 当前压力减去基础压力得到增量
                solution[i] = components.AirCompressionStations[i].TempPressure;
            }
            return solution;
        }

        // 恢复初始解
        private SystemComponents RestoreInitialSolution(SystemComponents components)
        {
            for (int i = 0; i < _dimension; i++)
            {
                components.AirCompressionStations[i].TempPressure = _initialSolution[i];
            }

            if (_calculationService.GasDuctCalc(ref components))
            {
                _calculationService.LoadData(ref components);
            }

            return components;
        }

        private void InitializeBounds(SystemComponents components)
        {
            _individualLowerBounds = new double[_dimension];
            _individualUpperBounds = new double[_dimension];
            _basePressures = new double[_dimension];

            for (int i = 0; i < _dimension; i++)
            {
                double currentPressure = components.AirCompressionStations[i].TempPressure;

                // 每个站点的基础压力就是当前压力
                _basePressures[i] = currentPressure;

                // 以当前压力为中心，下限-50000，上限+10000
                _individualLowerBounds[i] = currentPressure - 50000;
                _individualUpperBounds[i] = currentPressure + 10000;
            }
        }


        private double FitnessFunction(double[] position, SystemComponents components)
        {
            for (int i = 0; i < _dimension; i++)
            {
                components.AirCompressionStations[i].TempPressure = Math.Round(position[i]);
                //Console.WriteLine(components.AirCompressionStations[i].TempPressure);
            }

            bool isSatisfied = _calculationService.GasDuctCalc(ref components);
            Console.ReadKey();
            if (!isSatisfied)
            {
                return double.MaxValue;
            }

            _calculationService.LoadData(ref components);

            double totalPower = components.SystemParameter.Totalpower;
            double totalEfficiency = components.SystemParameter.TotalEfficiency;

            return 0.7 * totalPower - totalEfficiency * 200;
        }

        #region 算法核心逻辑 - 改进版

        private double[] RunOptimization(SystemComponents components)
        {
            Random random = new Random();
            var curve = new List<double>();

            // 初始化种群 - 确保初始解包含在种群中
            var population = new double[_populationSize][];
            var fitness = new double[_populationSize];

            // 第一个个体设为初始解
            population[0] = (double[])_initialSolution.Clone();
            fitness[0] = _initialFitness;

            // 生成其他个体，并在初始解附近生成一些个体
            for (int i = 1; i < _populationSize; i++)
            {
                if (i < _populationSize / 2)
                {
                    // 一半个体在初始解附近生成
                    population[i] = GenerateNearInitialSolution(random);
                }
                else
                {
                    // 另一半随机生成
                    population[i] = GenerateRandomSolution(random);
                }
                fitness[i] = FitnessFunction(population[i], components);
            }

            // 初始化全局最优解
            double[] globalBest = (double[])population[0].Clone();
            double globalBestFitness = fitness[0];

            for (int i = 1; i < _populationSize; i++)
            {
                if (fitness[i] < globalBestFitness)
                {
                    globalBest = (double[])population[i].Clone();
                    globalBestFitness = fitness[i];
                }
            }

            int noImprovementCount = 0;
            const int maxNoImprovement = 200; // 连续**代无改进则提前停止

            // 主循环
            for (int iteration = 0; iteration < _maxIterations; iteration++)
            {
                double previousBest = globalBestFitness;

                // Phase 1: 搜索行为
                for (int i = 0; i < _populationSize; i++)
                {
                    var newSolution = new double[_dimension];
                    for (int d = 0; d < _dimension; d++)
                    {
                        int I = random.Next(2);
                        // 添加一些随机扰动
                        double perturbation = (random.NextDouble() - 0.5) * _stepSize;
                        newSolution[d] = population[i][d] + random.NextDouble() * (globalBest[d] - I * population[i][d]) + perturbation;
                        newSolution[d] = RoundToStepSize(newSolution[d]);
                    }
                    newSolution = Clamp(newSolution);

                    double newFitness = FitnessFunction(newSolution, components);
                    if (newFitness < fitness[i])
                    {
                        population[i] = newSolution;
                        fitness[i] = newFitness;
                    }
                }

                // Phase 2: 攻击反应策略
                double Ps = random.NextDouble();
                int randomIndex = random.Next(_populationSize);
                double[] attackedZebra = population[randomIndex];

                for (int i = 0; i < _populationSize; i++)
                {
                    var newSolution = new double[_dimension];
                    if (Ps < 0.5)
                    {
                        int I = random.Next(2);
                        for (int d = 0; d < _dimension; d++)
                        {
                            newSolution[d] = population[i][d] + random.NextDouble() * (attackedZebra[d] - I * population[i][d]);
                            newSolution[d] = RoundToStepSize(newSolution[d]);
                        }
                    }
                    else
                    {
                        int I = random.Next(2);
                        for (int d = 0; d < _dimension; d++)
                        {
                            newSolution[d] = population[i][d] + random.NextDouble() * (attackedZebra[d] - I * population[i][d]);
                            newSolution[d] = RoundToStepSize(newSolution[d]);
                        }
                    }

                    newSolution = Clamp(newSolution);

                    double newFitness = FitnessFunction(newSolution, components);
                    if (newFitness < fitness[i])
                    {
                        population[i] = newSolution;
                        fitness[i] = newFitness;
                    }
                }

                // 更新全局最优解
                for (int i = 0; i < _populationSize; i++)
                {
                    if (fitness[i] < globalBestFitness)
                    {
                        globalBest = (double[])population[i].Clone();
                        globalBestFitness = fitness[i];
                    }
                }

                // 检查是否有改进
                if (globalBestFitness < previousBest - 1e-6) 
                {
                    noImprovementCount = 0;
                }
                else
                {
                    noImprovementCount++;
                }

                curve.Add(globalBestFitness);

                if ((iteration + 1) % 20 == 0)
                {
                    _logger.LogInformation("第 {Iteration} 代, 当前最优适应度: {BestFitness}, 相比初始解改进: {Improvement:N2}",
                        iteration + 1, globalBestFitness, _initialFitness - globalBestFitness);
                }

                // 提前停止条件
                if (noImprovementCount >= maxNoImprovement)
                {
                    _logger.LogInformation("连续 {Count} 代无改进，提前终止优化。", maxNoImprovement);
                    break;
                }
            }

            return globalBest;
        }

        // 在初始解附近生成解
        private double[] GenerateNearInitialSolution(Random random)
        {
            var solution = new double[_dimension];
            for (int i = 0; i < _dimension; i++)
            {
                double range = (_individualUpperBounds[i] - _individualLowerBounds[i]) * 0.2;
                double offset = (random.NextDouble() - 0.5) * 2 * range;
                solution[i] = _basePressures[i] + offset;
                solution[i] = Clamp(new[] { solution[i] })[0]; // 保证在合法范围内
            }
            return solution;
        }

        #endregion

        // 随机生成解
        private double[] GenerateRandomSolution(Random random)
        {
            var solution = new double[_dimension];
            for (int i = 0; i < _dimension; i++)
            {
                solution[i] = _individualLowerBounds[i] + random.NextDouble() * (_individualUpperBounds[i] - _individualLowerBounds[i]);
                solution[i] = RoundToStepSize(solution[i]);
            }
            return solution;
        }

        private double[] Clamp(double[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Math.Max(_individualLowerBounds[i], Math.Min(values[i], _individualUpperBounds[i]));
                values[i] = RoundToStepSize(values[i]);
            }
            return values;
        }

        private double RoundToStepSize(double value)
        {
            return Math.Round(value / _stepSize) * _stepSize;
        }

        private T DeepClone<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}