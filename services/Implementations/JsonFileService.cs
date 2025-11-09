using Project.Models;
using Project.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Text;


namespace Project.Services.Implementations
{
    public class JsonFileService : IJsonFileService
    {
        private readonly ILogger<JsonFileService> _logger;

        // 通过构造函数注入日志服务
        public JsonFileService(ILogger<JsonFileService> logger)
        {
            _logger = logger;
        }

        // =================================================================================================
        // 获取并验证JSON中的必需字段
        // =================================================================================================
        private T GetRequiredValue<T>(JToken token, string propertyName, string componentIdentifier)
        {
            var value = token[propertyName];
            if (value == null || string.IsNullOrEmpty(value.ToString()))
            {
                throw new InvalidDataException($"错误: 在组件 '{componentIdentifier}' 中, 必需的属性 '{propertyName}' 缺失或为空。");
            }
            try
            {
                // 对于JValue类型，直接使用ToObject<T>()进行转换
                return value.ToObject<T>();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"错误: 在组件 '{componentIdentifier}' 的属性 '{propertyName}' 中, 值的格式不正确或无法转换为期望的类型 '{typeof(T).Name}'。", ex);
            }
        }


        // 解码 Json 文档
        public SystemComponents DecodeStructure(string filePath)
        {
            _logger.LogInformation("开始从文件解码管网结构: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("结构文件不存在: {FilePath}", filePath);
                throw new FileNotFoundException("指定的结构文件未找到！", filePath);
            }

            try
            {
                var fileContent = File.ReadAllText(filePath);
                var jsonObject = JObject.Parse(fileContent);

                // 初始化一个空的根容器
                var components = new SystemComponents
                {
                    SystemParameter = new SystemParameter(),
                };

                // 1. 解析系统级参数
                var systemProperties = jsonObject["systemProperties"]?.FirstOrDefault();
                if (systemProperties == null)
                {
                    throw new InvalidDataException("错误: 'systemProperties' 字段在JSON文件中缺失或为空。");
                }

                components.SystemParameter.FluidNumber = GetRequiredValue<int>(systemProperties, "fluidNumber", "systemProperties");
                components.SystemParameter.Temperature = GetRequiredValue<double>(systemProperties, "temperature", "systemProperties") + 273.15; // 转为开尔文
                components.SystemParameter.SendAlgorithmJsonId = GetRequiredValue<int>(jsonObject, "sendAlgorithmJsonId", "root");
                components.SystemParameter.DeviceId = GetRequiredValue<long>(jsonObject, "deviceId", "root");

                // 根据解析出的参数，计算流体物理属性
                components.SystemParameter.InitializeFluidProperties();
                _logger.LogDebug("流体属性已计算: 密度={Density:F3}, 粘度={Sickness:F3}",
                    components.SystemParameter.FluidDensity, components.SystemParameter.FluidSickness);

                // 2. 提取所有组件的JArray
                var airCompressionStationsJson = (JArray)jsonObject["airCompressionStation"];
                var userSidesJson = (JArray)jsonObject["userSide"];
                var compositePipesJson = (JArray)jsonObject["compositePipes"];
                var reducingJson = (JArray)jsonObject["reducing"];
                var teeJunctionJson = (JArray)jsonObject["teeJunction"];
                var valveJson = (JArray)jsonObject["valve"];
                var lfvJson = (JArray)jsonObject["limitFlowValve"];
                var ldpvJson = (JArray)jsonObject["limitDropPValve"];
                var lpvJson = (JArray)jsonObject["limitPressureValve"];

                // 3. 构建节点字典 (DotNameDict) 并填充组件列表
                int dotIndexCounter = 0;

                foreach (var property in teeJunctionJson)
                {
                    string teeIndex = property["teeJunctionNumber"]?.ToString();
                    if (string.IsNullOrEmpty(teeIndex)) throw new InvalidDataException("错误: 发现一个三通缺少 'teeJunctionNumber' 字段。");

                    var tee = new TeeJunction
                    {
                        Index = teeIndex,
                        JunctionsA = GetRequiredValue<string>(property, "junctionsA", $"TeeJunction: {teeIndex}"),
                        JunctionsB = GetRequiredValue<string>(property, "junctionsB", $"TeeJunction: {teeIndex}"),
                        JunctionsC = GetRequiredValue<string>(property, "junctionsC", $"TeeJunction: {teeIndex}")
                    };
                    components.TeeJunctions.Add(tee);
                    components.SystemParameter.DotNameDict[tee.Index] = dotIndexCounter++;
                }

                foreach (var property in valveJson)
                {
                    string valveIndex = property["valveNumber"]?.ToString();
                    if (string.IsNullOrEmpty(valveIndex)) throw new InvalidDataException("错误: 发现一个阀门缺少 'valveNumber' 字段。");

                    var valve = new Valve
                    {
                        Index = valveIndex,
                        JunctionsA = GetRequiredValue<string>(property, "junctionsA", $"Valve: {valveIndex}"),
                        JunctionsB = GetRequiredValue<string>(property, "junctionsB", $"Valve: {valveIndex}"),
                        FlowDirection = GetRequiredValue<int>(property, "flowDirection", $"Valve: {valveIndex}"),
                        ValveType = GetRequiredValue<int>(property, "valveType", $"Valve: {valveIndex}"),
                        ValveMeasure = GetRequiredValue<double>(property, "valveMeasure", $"Valve: {valveIndex}") * 100,
                        Diameter = GetRequiredValue<double>(property, "diameter", $"Valve: {valveIndex}") / 1000
                    };
                    components.Valves.Add(valve);
                    components.SystemParameter.DotNameDict[valve.Index] = dotIndexCounter++;
                }

                foreach (var property in reducingJson)
                {
                    string reducerIndex = property["reducingNumber"]?.ToString();
                    if (string.IsNullOrEmpty(reducerIndex)) throw new InvalidDataException("错误: 发现一个变径缺少 'reducingNumber' 字段。");

                    var reducer = new Reducing
                    {
                        Index = reducerIndex,
                        JunctionsA = GetRequiredValue<string>(property, "junctionsA", $"Reducing: {reducerIndex}"),
                        JunctionsB = GetRequiredValue<string>(property, "junctionsB", $"Reducing: {reducerIndex}"),
                        DiameterA = GetRequiredValue<double>(property, "diameterA", $"Reducing: {reducerIndex}") / 1000,
                        DiameterB = GetRequiredValue<double>(property, "diameterB", $"Reducing: {reducerIndex}") / 1000,
                        AngleDegrees = GetRequiredValue<double>(property, "angleDegrees", $"Reducing: {reducerIndex}")
                    };
                    components.Reducers.Add(reducer);
                    components.SystemParameter.DotNameDict[reducer.Index] = dotIndexCounter++;
                }

                foreach (var property in lfvJson)
                {
                    string lfvIndex = property["LFVNumber"]?.ToString();
                    if (string.IsNullOrEmpty(lfvIndex)) throw new InvalidDataException("错误: 发现一个限流阀缺少 'LFVNumber' 字段。");

                    var lfv = new LimitFlowValve
                    {
                        Index = lfvIndex,
                        JunctionsA = GetRequiredValue<string>(property, "junctionsA", $"LimitFlowValve: {lfvIndex}"),
                        JunctionsB = GetRequiredValue<string>(property, "junctionsB", $"LimitFlowValve: {lfvIndex}"),
                        FlowDirection = GetRequiredValue<int>(property, "flowDirection", $"LimitFlowValve: {lfvIndex}"),
                        SetFlow = GetRequiredValue<double>(property, "setFlow", $"LimitFlowValve: {lfvIndex}")
                    };
                    components.LimitFlowValves.Add(lfv);
                    components.SystemParameter.DotNameDict[lfv.Index] = dotIndexCounter++;
                }

                foreach (var property in ldpvJson)
                {
                    string ldpvIndex = property["LDPVNumber"]?.ToString();
                    if (string.IsNullOrEmpty(ldpvIndex)) throw new InvalidDataException("错误: 发现一个限压降阀缺少 'LDPVNumber' 字段。");

                    var ldpv = new LimitDropPValve
                    {
                        Index = ldpvIndex,
                        JunctionsA = GetRequiredValue<string>(property, "junctionsA", $"LimitDropPValve: {ldpvIndex}"),
                        JunctionsB = GetRequiredValue<string>(property, "junctionsB", $"LimitDropPValve: {ldpvIndex}"),
                        FlowDirection = GetRequiredValue<int>(property, "flowDirection", $"LimitFlowValve: {ldpvIndex}"),
                        SetDropP = GetRequiredValue<double>(property, "setDropP", $"LimitDropPValve: {ldpvIndex}")
                    };
                    components.LimitDropPValves.Add(ldpv);
                    components.SystemParameter.DotNameDict[ldpv.Index] = dotIndexCounter++;
                }

                foreach (var property in lpvJson)
                {
                    string lpvIndex = property["LPVNumber"]?.ToString();
                    if (string.IsNullOrEmpty(lpvIndex)) throw new InvalidDataException("错误: 发现一个限压阀缺少 'LPVNumber' 字段。");

                    var lpv = new LimitPressureValve
                    {
                        Index = lpvIndex,
                        JunctionsA = GetRequiredValue<string>(property, "junctionsA", $"LimitPressureValve: {lpvIndex}"),
                        JunctionsB = GetRequiredValue<string>(property, "junctionsB", $"LimitPressureValve: {lpvIndex}"),
                        FlowDirection = GetRequiredValue<int>(property, "flowDirection", $"LimitFlowValve: {lpvIndex}"),
                        SetP = GetRequiredValue<double>(property, "setP", $"LimitPressureValve: {lpvIndex}")
                    };
                    components.LimitPressureValves.Add(lpv);
                    components.SystemParameter.DotNameDict[lpv.Index] = dotIndexCounter++;
                }

                foreach (var property in userSidesJson)
                {
                    string userIndex = property["userSideNumber"]?.ToString();
                    if (string.IsNullOrEmpty(userIndex)) throw new InvalidDataException("错误: 发现一个用户端缺少 'userSideNumber' 字段。");

                    var user = new UserSide
                    {
                        Name = GetRequiredValue<string>(property, "usersName", $"UserSide: {userIndex}"),
                        Index = userIndex,
                        InitialFlow = GetRequiredValue<double>(property, "initialFlow", $"UserSide: {userIndex}"),
                        JunctionNodeId = GetRequiredValue<string>(property, "junctions", $"UserSide: {userIndex}"),
                        WarnPressure = 1000 * GetRequiredValue<double>(property, "warmPressure", $"UserSide: {userIndex}")
                    };
                    components.UserSides.Add(user);
                    components.SystemParameter.DotNameDict[user.Index] = dotIndexCounter++;
                }

                List<string> airCompressionPipeNames = new List<string>();
                foreach (var property in airCompressionStationsJson)
                {
                    string stationIndex = property["airCompressionStationNumber"]?.ToString();
                    if (string.IsNullOrEmpty(stationIndex)) throw new InvalidDataException("错误: 发现一个空压站缺少 'airCompressionStationNumber' 字段。");

                    var station = new AirCompressionStation
                    {
                        Name = GetRequiredValue<string>(property, "name", $"AirCompressionStation: {stationIndex}"),
                        Index = stationIndex,
                        InitialPressure = GetRequiredValue<double>(property, "initialPressure", $"AirCompressionStation: {stationIndex}") * 1000,
                        JunctionNodeId = GetRequiredValue<string>(property, "junctions", $"AirCompressionStation: {stationIndex}")
                    };

                    var compressorsJson = property["compressor"] as JArray;
                    if (compressorsJson == null || !compressorsJson.Any())
                    {
                        throw new InvalidDataException($"错误: 空压站 '{stationIndex}' 中必需的 'compressor' 数组缺失或为空。");
                    }

                    foreach (var compressorJson in compressorsJson)
                    {
                        string compIndex = compressorJson["index"]?.ToString();
                        if (string.IsNullOrEmpty(compIndex)) throw new InvalidDataException($"错误: 空压站 '{stationIndex}' 的一个压缩机缺少 'index' 字段。");

                        station.Compressors.Add(new Compressor
                        {
                            Index = GetRequiredValue<int>(compressorJson, "index", $"Compressor in Station: {stationIndex}"),
                            MaxFlow = GetRequiredValue<double>(compressorJson, "maxCompressorFlow", $"Compressor '{compIndex}' in Station: {stationIndex}"),
                            MinValveDegree = GetRequiredValue<double>(compressorJson, "lowDegree", $"Compressor '{compIndex}' in Station: {stationIndex}") / 100
                        });
                    }
                    components.AirCompressionStations.Add(station);
                    airCompressionPipeNames.Add(station.JunctionNodeId);
                    components.SystemParameter.DotNameDict[station.Index] = dotIndexCounter++;
                }

                // 4. 构建管道字典和管道列表
                int pipeIndexCounter = 0;
                foreach (var property in compositePipesJson)
                {
                    string pipeIndex = property["compositePipesNumber"]?.ToString();
                    if (string.IsNullOrEmpty(pipeIndex)) throw new InvalidDataException("错误: 发现一个管道缺少 'compositePipesNumber' 字段。");

                    var pipe = new CompositePipes
                    {
                        Index = pipeIndex,
                        JunctionA = GetRequiredValue<string>(property, "junctionsA", $"CompositePipe: {pipeIndex}"),
                        JunctionB = GetRequiredValue<string>(property, "junctionsB", $"CompositePipe: {pipeIndex}"),
                        FlowDirection = GetRequiredValue<int>(property, "flowDirection", $"CompositePipe: {pipeIndex}")
                    };

                    var straightPipesJson = property["fusionStraightPipe"] as JArray;
                    if (straightPipesJson == null || !straightPipesJson.Any())
                    {
                        throw new InvalidDataException($"错误: 管道 '{pipeIndex}' 中必需的 'fusionStraightPipe' 数组缺失或为空。");
                    }

                    foreach (var spJson in straightPipesJson)
                    {
                        pipe.StraightPipeSections.Add(new StraightPipeSection
                        {
                            Length = GetRequiredValue<double>(spJson, "length", $"StraightPipe in Pipe: {pipeIndex}"),
                            Diameter = GetRequiredValue<double>(spJson, "diameter", $"StraightPipe in Pipe: {pipeIndex}") / 1000,
                            Roughness = spJson["roughness"]?.ToObject<double>() ?? 0.1 
                        });
                    }

                    var bendersJson = property["fusionBend"] as JArray;
                    if (bendersJson != null)
                    {
                        foreach (var bJson in bendersJson)
                        {
                            pipe.BenderSections.Add(new BenderSection
                            {
                                Quantity = GetRequiredValue<int>(bJson, "quantities", $"Bender in Pipe: {pipeIndex}"),
                                AngleDegrees = GetRequiredValue<double>(bJson, "angleDegrees", $"Bender in Pipe: {pipeIndex}"),
                                RadiusToDiameterRatio = 1.5 // JSON中没有，使用一个默认值
                            });
                        }
                    }

                    components.CompositePipes.Add(pipe);
                    components.SystemParameter.PipeNameDict[pipe.Index] = pipeIndexCounter++;
                }

                _logger.LogInformation("JSON组件解析完成，共解析 {DotCount} 个节点, {PipeCount} 条管道。", dotIndexCounter, pipeIndexCounter);

                // 5. 构建和重排序关联矩阵
                int dotNum = dotIndexCounter;
                int pipeNum = pipeIndexCounter;
                components.SystemParameter.DotNum = dotNum;
                components.SystemParameter.PipeNum = pipeNum;
                var graph = new Graph(dotNum, pipeNum);

                for (int i = 0; i < pipeNum; i++)
                {
                    var pipe = components.CompositePipes[i];
                    int indexA = components.SystemParameter.DotNameDict[pipe.JunctionA];
                    int indexB = components.SystemParameter.DotNameDict[pipe.JunctionB];
                    int[] item = new int[2];

                    if (pipe.FlowDirection == 1)
                    {
                        graph.AddMatrixNum(indexA, i, -1);
                        graph.AddMatrixNum(indexB, i, 1);
                        item[0] = indexA; item[1] = indexB;
                    }
                    else
                    {
                        graph.AddMatrixNum(indexA, i, 1);
                        graph.AddMatrixNum(indexB, i, -1);
                        item[0] = indexB; item[1] = indexA;
                    }
                    components.SystemParameter.listPipeJunction.Add(item);
                }

                // 重排序矩阵和列表，将与空压站相连的管道相关项移到末尾
                int[] airCompressionPipeIndices = airCompressionPipeNames
                    .Select(name => components.SystemParameter.PipeNameDict[name])
                    .ToArray();

                components.SystemParameter.Matrix = MoveColumnsToEnd(graph.Matrix, airCompressionPipeIndices);

                List<string> pipeKeys = components.SystemParameter.PipeNameDict.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                MoveElementsToEnd(components.SystemParameter.listPipeJunction, pipeKeys, components.CompositePipes, airCompressionPipeIndices);

                // 重建 PipeNameDict 以反映新的顺序
                components.SystemParameter.PipeNameDict.Clear();
                for (int i = 0; i < pipeKeys.Count; i++)
                {
                    components.SystemParameter.PipeNameDict.Add(pipeKeys[i], i);
                }

                _logger.LogInformation("管网拓扑结构和关联矩阵构建成功。");
                return components;
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(ex, "解码结构文件 {FilePath} 时发现数据缺失或格式错误。", filePath);
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解码结构文件 {FilePath} 时发生严重错误！", filePath);
                throw new InvalidDataException($"解析文件失败，请检查JSON文件格式: {filePath}", ex);
            }
        }


        public void GenerateOutputFile(string filePath, SystemComponents components)
        {
            _logger.LogInformation("正在将计算结果写入到JSON文件: {FilePath}", filePath);

            try
            {
                var jsonObject = BuildJsonObject(components);
                var jsonString = jsonObject.ToString();
                File.WriteAllText(filePath, jsonString, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入JSON文件时发生错误。");
                throw;
            }
        }


        public SystemComponents ReadUsersFlowChange(string filePath, SystemComponents currentComponents)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    _logger.LogInformation("正在读取用户流量变化文件: {FilePath}", filePath);
                    string usersFlowContent = File.ReadAllText(filePath);
                    var usersFlowJson = JObject.Parse(usersFlowContent);
                    var usersChange = (JArray)usersFlowJson["usersChange"];

                    if (usersChange != null)
                    {
                        foreach (var userChange in usersChange)
                        {
                            string userSideNumber = userChange["usersNumber"].ToString();
                            double newFlow = (double)userChange["fluid"];

                            var userToUpdate = currentComponents.UserSides.FirstOrDefault(u => u.Index == userSideNumber);
                            if (userToUpdate != null)
                            {
                                _logger.LogInformation("正在更新用户 {UserIndex} 的流量从 {OldFlow} 到 {NewFlow}", userToUpdate.Index, userToUpdate.InitialFlow, newFlow);
                                userToUpdate.InitialFlow = newFlow;
                            }
                            else
                            {
                                _logger.LogWarning("在流量变化文件中找到用户 {UserIndex}，但在管网结构中未找到该用户。", userSideNumber);
                            }
                        }
                        _logger.LogInformation("用户流量更新完毕。");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取或应用用户流量变化时出错。将继续使用原始流量。");
                }
            }
            else
            {
                _logger.LogWarning("未找到用户流量变化文件: {FilePath}。将使用 structure.json 中的原始流量进行计算。", filePath);
            }
            return currentComponents;
        }

        #region 

        private class Graph
        {
            public int[,] Matrix;
            public Graph(int rowCount, int colCount) { Matrix = new int[rowCount, colCount]; }
            public void AddMatrixNum(int row, int col, int weight)
            {
                if (row >= 0 && row < Matrix.GetLength(0) && col >= 0 && col < Matrix.GetLength(1))
                {
                    Matrix[row, col] = weight;
                }
            }
        }

        private static int[,] MoveColumnsToEnd(int[,] array, int[] columnsToMove)
        {
            int rowCount = array.GetLength(0);
            int colCount = array.GetLength(1);
            int[,] newArray = new int[rowCount, colCount];
            int newColIdx = 0;

            // 复制非移动列
            for (int col = 0; col < colCount; col++)
            {
                if (Array.IndexOf(columnsToMove, col) == -1)
                {
                    for (int row = 0; row < rowCount; row++)
                    {
                        newArray[row, newColIdx] = array[row, col];
                    }
                    newColIdx++;
                }
            }
            // 复制移动列到末尾
            foreach (var col in columnsToMove)
            {
                for (int row = 0; row < rowCount; row++)
                {
                    newArray[row, newColIdx] = array[row, col];
                }
                newColIdx++;
            }
            return newArray;
        }

        private static void MoveElementsToEnd<T1, T2, T3>(List<T1> list1, List<T2> list2, List<T3> list3, int[] positions)
        {

            var elementsToMove1 = positions.Select(p => list1[p]).ToList();
            var elementsToMove2 = positions.Select(p => list2[p]).ToList();
            var elementsToMove3 = positions.Select(p => list3[p]).ToList();

            var sortedPositionsForRemoval = positions.OrderByDescending(p => p).ToArray();

            foreach (var pos in sortedPositionsForRemoval)
            {
                list1.RemoveAt(pos);
                list2.RemoveAt(pos);
                list3.RemoveAt(pos);
            }

            list1.AddRange(elementsToMove1);
            list2.AddRange(elementsToMove2);
            list3.AddRange(elementsToMove3);
        }

        // 构建完整的JSON对象
        private JObject BuildJsonObject(SystemComponents components)
        {
            return new JObject
            {
                ["airCompressionStation"] = BuildAirCompressionStationArray(components),
                ["userSide"] = BuildUserSideArray(components),
                ["reducing"] = BuildReducingArray(components),
                ["teeJunction"] = BuildTeeJunctionArray(components),
                ["valve"] = BuildValveArray(components),
                ["limitFlowValve"] = BuildLimitFlowValveArray(components),
                ["limitPressureValve"] = BuildLimitPressureValveArray(components),
                ["limitDropPValve"] = BuildLimitDropPValveArray(components),
                ["compositePipes"] = BuildPipeArray(components),
                ["systemProperties"] = BuildSystemPropertiesArray(components),
                ["sendAlgorithmJsonId"] = components.SystemParameter.SendAlgorithmJsonId,
                ["deviceId"] = components.SystemParameter.DeviceId
            };
        }

        // 构建空压站数据数组
        private JArray BuildAirCompressionStationArray(SystemComponents components)
        {
            var array = new JArray();

            foreach (var compression in components.AirCompressionStations)
            {
                var stationObject = new JObject
                {
                    ["airCompressionStationNumber"] = compression.Index,
                    ["name"] = compression.Name,
                    ["realTimePressure"] = (compression.RealTimePressure / 1000).ToString(),
                    ["realTimeFlow"] = compression.RealTimeFlow.ToString(),
                    ["totalPower"] = compression.TotalPower.ToString(),
                    ["wasteFlow"] = compression.WastedFlow.ToString(),
                    ["compressor"] = BuildCompressorArray(compression)
                };

                array.Add(stationObject);
            }

            return array;
        }

        // 构建压缩机数组
        private JArray BuildCompressorArray(AirCompressionStation compression)
        {
            var compressorArray = new JArray();

            for (int i = 0; i < compression.Compressors.Count; i++)
            {
                var compressorObject = new JObject
                {
                    ["Index"] = compression.Compressors[i].Index.ToString(),
                    ["power"] = compression.Compressors[i].RealTimePower.ToString(),
                    ["flow"] = compression.Compressors[i].RealTimeFlow.ToString(),
                    ["Valve"] = compression.Compressors[i].CurrentValveDegree.ToString(),
                };

                compressorArray.Add(compressorObject);
            }

            return compressorArray;
        }

        // 通用方法：构建简单对象数组
        private JArray BuildSimpleArray<T>(IEnumerable<T> items, Func<T, JObject> objectBuilder)
        {
            var array = new JArray();

            foreach (var item in items)
            {
                array.Add(objectBuilder(item));
            }

            return array;
        }

        // 构建用户端数据数组
        private JArray BuildUserSideArray(SystemComponents components)
        {
            return BuildSimpleArray(components.UserSides, user => new JObject
            {
                ["userSideNumber"] = user.Index.ToString(),
                ["name"] = user.Name.ToString(),
                ["realTimePressure"] = (user.RealTimePressure / 1000).ToString(),
                ["realTimeFlow"] = user.RealTimeFlow.ToString(),
            });
        }

        // 构建变径数据数组
        private JArray BuildReducingArray(SystemComponents components)
        {
            return BuildSimpleArray(components.Reducers, reducing => new JObject
            {
                ["reducingNumber"] = reducing.Index.ToString(),
                ["pressure"] = (reducing.Pressure / 1000).ToString(),
                ["flow"] = reducing.Flow.ToString()
            });
        }

        // 构建三通数据数组
        private JArray BuildTeeJunctionArray(SystemComponents components)
        {
            return BuildSimpleArray(components.TeeJunctions, tee => new JObject
            {
                ["teeJunctionNumber"] = tee.Index.ToString(),
                ["pressure"] = (tee.Pressure / 1000).ToString(),
                ["flowA"] = tee.FlowA.ToString(),
                ["flowB"] = tee.FlowB.ToString(),
                ["flowC"] = tee.FlowC.ToString()
            });
        }

        // 构建阀门数据数组
        private JArray BuildValveArray(SystemComponents components)
        {
            return BuildSimpleArray(components.Valves, valve => new JObject
            {
                ["valveNumber"] = valve.Index.ToString(),
                ["pressure"] = (valve.Pressure / 1000).ToString(),
                ["massFlow"] = valve.Flow.ToString()
            });
        }

        // 构建限流阀门数据数组
        private JArray BuildLimitFlowValveArray(SystemComponents components)
        {
            return BuildSimpleArray(components.LimitFlowValves, lfv => new JObject
            {
                ["LFVNumber"] = lfv.Index.ToString(),
                ["massFlow"] = lfv.Flow.ToString(),
                ["pressure"] = (lfv.Pressure / 1000).ToString()
            });
        }

        // 构建限压阀门数据数组
        private JArray BuildLimitPressureValveArray(SystemComponents components)
        {
            return BuildSimpleArray(components.LimitPressureValves, lpv => new JObject
            {
                ["LPVNumber"] = lpv.Index.ToString(),
                ["massFlow"] = lpv.Flow.ToString(),
                ["pressure"] = (lpv.Pressure / 1000).ToString()
            });
        }

        // 构建限压降阀门数据数组
        private JArray BuildLimitDropPValveArray(SystemComponents components)
        {
            return BuildSimpleArray(components.LimitDropPValves, ldpv => new JObject
            {
                ["LDPVNumber"] = ldpv.Index.ToString(),
                ["massFlow"] = ldpv.Flow.ToString(),
                ["pressure"] = (ldpv.Pressure / 1000).ToString()
            });
        }

        // 构建管道数据数组
        private JArray BuildPipeArray(SystemComponents components)
        {
            return BuildSimpleArray(components.CompositePipes, pipe => new JObject
            {
                ["compositePipesNumber"] = pipe.Index.ToString(),
                ["pressure"] = (pipe.AveragePressure / 1000).ToString(),
                ["pressureA"] = (pipe.PressureA / 1000).ToString(),
                ["pressureB"] = (pipe.PressureB / 1000).ToString(),
                ["dropPressure"] = (pipe.DropPressure / 1000).ToString(),
                ["flow"] = pipe.Flow.ToString(),
                ["pipeEfficiency"] = pipe.PipeEfficiency.ToString()
            });
        }

        // 构建系统参数数据数组
        private JArray BuildSystemPropertiesArray(SystemComponents components)
        {
            var systemProps = new JObject
            {
                ["efficiency"] = components.SystemParameter.TotalEfficiency * 100,
                ["systemEfficiency"] = components.SystemParameter.SystemEfficiency * 100,
                ["totalPower"] = components.SystemParameter.Totalpower
            };

            return new JArray { systemProps };
        }

        #endregion

    }
}