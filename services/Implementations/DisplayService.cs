using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Project.Models;
using Project.Services.Abstractions;


namespace Project.Services.Implementations
{
    public class DisplayService : IDisplayService
    {
        private readonly ILogger<DisplayService> _logger;

        public DisplayService(ILogger<DisplayService> logger)
        {
            _logger = logger;
        }

        public void DisplayStructure(SystemComponents components)
        {
            Console.WriteLine("\n==================================================");
            Console.WriteLine("          管网系统全量数据展示");
            Console.WriteLine("==================================================");

            PrintSystemParameters(components.SystemParameter);
            PrintAirCompressionStations(components.AirCompressionStations);
            PrintUserSides(components.UserSides);
            PrintCompositePipes(components.CompositePipes);
            PrintTeeJunctions(components.TeeJunctions);
            PrintReducers(components.Reducers);
            PrintValves(components.Valves);
            PrintLimitValves(components.LimitFlowValves, components.LimitDropPValves, components.LimitPressureValves);

            Console.WriteLine("\n==================================================");
            Console.WriteLine("                  数据展示完毕");
            Console.WriteLine("==================================================");
            _logger.LogInformation("系统全量数据已在控制台展示完毕。");
        }


        public void DisplayResults(SystemComponents components)
        {
            _logger.LogInformation("正在显示管网计算结果...");

            Console.WriteLine();

            // 显示系统总体结果
            Console.WriteLine("\n--- 1. 系统总体结果 (System-wide Results) ---\n");
            Console.WriteLine($"  -> 系统总消耗功率 (Total Power Consumption): {components.SystemParameter.Totalpower:F2} kW");
            Console.WriteLine($"  -> 系统有效能效 (System Efficiency):     {components.SystemParameter.SystemEfficiency:P2}");
            Console.WriteLine($"  -> 总能效 (TotalEfficiency): {components.SystemParameter.TotalEfficiency:P2}");

            // 显示空压站运行状态
            Console.WriteLine("\n--- 2. 空压站运行状态 (Air Compression Stations Status) ---\n");
            foreach (var station in components.AirCompressionStations)
            {
                Console.WriteLine($"  [空压站: {station.Index}]");
                Console.WriteLine($"    - 实时出口压力: {station.RealTimePressure / 1000:F2} kPa");
                Console.WriteLine($"    - 实时总流量:   {station.RealTimeFlow:F2} m³/min");
                Console.WriteLine($"    - 开启压缩机数: {station.OpenCompressorCount} / {station.Compressors.Count}");
                Console.WriteLine($"    - 本站总功率:   {station.TotalPower:F2} kW");
                Console.WriteLine($"    - 溢出/浪费流量:{station.WastedFlow:F2} m³/min");
                Console.WriteLine("    - 下属压缩机状态:");
                Console.WriteLine("      -------------------------------------------------");
                Console.WriteLine("      |  编号  |  阀门开度  |  实时流量(m³/min)  |");
                Console.WriteLine("      -------------------------------------------------");
                foreach (var compressor in station.Compressors)
                {
                    Console.WriteLine($"      |  {compressor.Index,-5} |  {compressor.CurrentValveDegree,-9:P1} |  {compressor.RealTimeFlow,-17:F2} |");
                }
                Console.WriteLine("      -------------------------------------------------\n");
            }

            // 显示用户端状态
            Console.WriteLine("\n--- 3. 用户端状态 (User-side Status) ---\n");
            Console.WriteLine("  -------------------------------------------------------------");
            Console.WriteLine("  |  用户编号  |  实时压力(kPa)  |  实时耗气量(m³/min)  |");
            Console.WriteLine("  -------------------------------------------------------------");
            foreach (var user in components.UserSides)
            {
                // 耗气量为负值，显示时取绝对值
                Console.WriteLine($"  |  {user.Index,-9} |  {user.RealTimePressure / 1000,-14:F2} |  {Math.Abs(user.RealTimeFlow),-19:F2} |");
            }
            Console.WriteLine("  -------------------------------------------------------------\n");

            // 显示管道状态
            Console.WriteLine("\n--- 4. 管道状态 (Main Pipes Status) ---\n");
            Console.WriteLine("  --------------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("  |  管道编号  |  流量(m³/min)    |  起点压力(kPa)  |  终点压力(kPa)  |  压降(kPa)  |  平均压力(kPa)  |  管道能效  |");
            Console.WriteLine("  --------------------------------------------------------------------------------------------------------------------");
            foreach (var pipe in components.CompositePipes)
            {
                Console.WriteLine($"  |  {pipe.Index,-9} |  {pipe.Flow,-15:F3} |  {pipe.PressureA / 1000,-14:F2} |  {pipe.PressureB / 1000,-14:F2} |  " +
                    $"{pipe.DropPressure / 1000,-10:F2} |  {pipe.AveragePressure / 1000,-14:F2} |  {pipe.PipeEfficiency,-9:P2} |");
            }
            Console.WriteLine("  --------------------------------------------------------------------------------------------------------------------");

            Console.WriteLine("\n--- 5. 三通节点状态 (Tee Junctions Status) ---\n");
            Console.WriteLine("  --------------------------------------------------------------------------------------");
            Console.WriteLine("  |  三通编号  |  节点压力(kPa)  |  流量A(m³/min)  |  流量B(m³/min)  |  流量C(m³/min)  |");
            Console.WriteLine("  --------------------------------------------------------------------------------------");
            foreach (var tee in components.TeeJunctions)
            {
                Console.WriteLine($"  |  {tee.Index,-9} |  {tee.Pressure / 1000,-14:F2} |  {tee.FlowA,-14:F2} |  {tee.FlowB,-14:F2} |  {tee.FlowC,-14:F2} |");
            }
            Console.WriteLine("  --------------------------------------------------------------------------------------\n");

            // 显示变径节点状态
            Console.WriteLine("\n--- 6. 变径节点状态 (Reducer Joints Status) ---\n");
            Console.WriteLine("  ---------------------------------------------------------");
            Console.WriteLine("  |  变径编号  |  节点压力(kPa)  |  流量(m³/min)  |");
            Console.WriteLine("  ---------------------------------------------------------");
            foreach (var reducer in components.Reducers)
            {
                Console.WriteLine($"  |  {reducer.Index,-9} |  {reducer.Pressure / 1000,-14:F2} |  {reducer.Flow,-13:F2} |");
            }
            Console.WriteLine("  ---------------------------------------------------------\n");

            // 显示阀门状态
            Console.WriteLine("\n--- 7. 阀门状态 (Valves Status) ---\n");
            Console.WriteLine("  ------------------------------------------------------------------");
            Console.WriteLine("  |  阀门编号  |  类型        |  节点压力(kPa)  |  流量(m³/min)  |");
            Console.WriteLine("  ------------------------------------------------------------------");
            foreach (var valve in components.Valves)
            {
                Console.WriteLine($"  |  {valve.Index,-9} |  普通阀门    |  {valve.Pressure / 1000,-14:F2} |  {valve.Flow,-15:F3} |");
            }
            foreach (var lfv in components.LimitFlowValves)
            {
                Console.WriteLine($"  |  {lfv.Index,-9} |  限流阀      |  {lfv.Pressure / 1000,-14:F2} |  {lfv.Flow,-15:F3} |");
            }
            foreach (var ldpv in components.LimitDropPValves)
            {
                Console.WriteLine($"  |  {ldpv.Index,-9} |  恒压降阀    |  {ldpv.Pressure / 1000,-14:F2} |  {ldpv.Flow,-15:F3} |");
            }
            foreach (var lpv in components.LimitPressureValves)
            {
                Console.WriteLine($"  |  {lpv.Index,-9} |  限压阀      |  {lpv.Pressure / 1000,-14:F2} |  {lpv.Flow,-15:F3} |");
            }
            Console.WriteLine("  ------------------------------------------------------------------\n");


            Console.WriteLine("\n\n计算结果显示完毕。");
        }


        private void PrintSystemParameters(SystemParameter param)
        {
            Console.WriteLine("\n---------------- 1. 系统全局参数 -----------------");
            // ... (此处省略与上一版本相同的代码)
            Console.WriteLine($"  - 发送算法JSON ID: {param.SendAlgorithmJsonId}");
            Console.WriteLine($"  - 设备ID: {param.DeviceId}");
            Console.WriteLine($"  - 流体编号: {param.FluidNumber}");
            Console.WriteLine($"  - 系统温度 (K): {param.Temperature:F2}");
            Console.WriteLine($"  - 流体密度 (kg/m³, 0℃): {param.FluidDensity:F3}");
            Console.WriteLine($"  - 计算出的流体动力粘度: {param.FluidSickness:F5}");
            Console.WriteLine($"  - 拓扑结构: {param.DotNum} 个节点, {param.PipeNum} 条管道");

            PrintDotNameDict(param.DotNameDict);
            PrintPipeNameDict(param.PipeNameDict);
            PrintListPipeJunction(param.listPipeJunction, param.PipeNameDict);
            PrintMatrix(param.Matrix);
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintDotNameDict(Dictionary<string, int> dict)
        {
            Console.WriteLine("\n  --- 节点名称字典 (DotNameDict) ---");
            if (dict == null || !dict.Any())
            {
                Console.WriteLine("    节点字典数据为空。");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"    共 {dict.Count} 个节点映射:");
            foreach (var kvp in dict.OrderBy(x => x.Value)) // 按整数索引排序显示
            {
                sb.AppendLine($"    - 节点 '{kvp.Key,-8}' -> 索引: {kvp.Value}");
            }
            Console.Write(sb.ToString());
        }

        private void PrintPipeNameDict(Dictionary<string, int> dict)
        {
            Console.WriteLine("\n  --- 管道名称字典 (PipeNameDict) ---");
            if (dict == null || !dict.Any())
            {
                Console.WriteLine("    管道字典数据为空。");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"    共 {dict.Count} 个管道映射:");
            foreach (var kvp in dict.OrderBy(x => x.Value)) // 按整数索引排序显示
            {
                sb.AppendLine($"    - 管道 '{kvp.Key,-8}' -> 索引: {kvp.Value}");
            }
            Console.Write(sb.ToString());
        }

        private void PrintListPipeJunction(List<int[]> list, Dictionary<string, int> pipeNameDict)
        {
            Console.WriteLine("\n  --- 管道连接点列表 (listPipeJunction) ---");
            if (list == null || !list.Any())
            {
                Console.WriteLine("    管道连接列表数据为空。");
                return;
            }

            // 为了方便查看，我们创建一个反向的管道字典
            var reversePipeDict = pipeNameDict.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"    共 {list.Count} 条管道连接信息:");
            for (int i = 0; i < list.Count; i++)
            {
                // 尝试从反向字典中获取管道名称
                string pipeName = reversePipeDict.ContainsKey(i) ? reversePipeDict[i] : "Unknown";
                sb.AppendLine($"    - 管道 {i} ('{pipeName,-8}'): 连接节点 [ {list[i][0]}, {list[i][1]} ]");
            }
            Console.Write(sb.ToString());
        }

        private void PrintMatrix(int[,] matrix)
        {
            Console.WriteLine("\n  --- 关联矩阵 (Matrix) ---");
            if (matrix == null)
            {
                Console.WriteLine("    矩阵数据为空。");
                return;
            }

            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            Console.WriteLine($"    矩阵维度: {rows} x {cols}");

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < rows; i++)
            {
                sb.Append("    [ ");
                for (int j = 0; j < cols; j++)
                {
                    // 格式化字符串，确保对齐
                    sb.Append(string.Format("{0,2}", Convert.ToInt32(matrix[i, j])));
                    if (j < cols - 1) sb.Append(", ");
                }
                sb.AppendLine(" ]");
            }
            Console.Write(sb.ToString());
        }

        private void PrintAirCompressionStations(List<AirCompressionStation> stations)
        {
            Console.WriteLine($"\n---------------- 2. 空压站 ({stations.Count}个) -----------------");
            foreach (var station in stations)
            {
                Console.WriteLine($"  - 空压站ID: {station.Index} | 名称: {station.Name}");
                Console.WriteLine($"    - 连接点: {station.JunctionNodeId}");
                Console.WriteLine($"    - 初始压力 (Pa): {station.InitialPressure}");
                Console.WriteLine($"    - 包含的压缩机 ({station.Compressors.Count}台):");
                foreach (var compressor in station.Compressors)
                {
                    Console.WriteLine($"      - 压缩机#{compressor.Index}: 最大流量={compressor.MaxFlow}, 最低阀门开度={compressor.MinValveDegree}");
                }
            }
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintUserSides(List<UserSide> userSides)
        {
            Console.WriteLine($"\n---------------- 3. 用户端 ({userSides.Count}个) -------------------");
            foreach (var user in userSides)
            {
                Console.WriteLine($"  - 用户端ID: {user.Index} | 名称: {user.Name}");
                Console.WriteLine($"    - 连接点: {user.JunctionNodeId}");
                Console.WriteLine($"    - 初始流量: {user.InitialFlow}");
                Console.WriteLine($"    - 告警压力 (Pa): {user.WarnPressure}");
            }
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintCompositePipes(List<CompositePipes> pipes)
        {
            Console.WriteLine($"\n---------------- 4. 复合管道 ({pipes.Count}条) -----------------");
            foreach (var pipe in pipes)
            {
                Console.WriteLine($"  - 管道ID: {pipe.Index} | 连接: {pipe.JunctionA} <--> {pipe.JunctionB}");
                Console.WriteLine($"    - 默认流向: {pipe.FlowDirection}");
                if (pipe.StraightPipeSections.Any())
                {
                    Console.WriteLine($"    - 直管段 ({pipe.StraightPipeSections.Count}段):");
                    foreach (var sp in pipe.StraightPipeSections)
                    {
                        Console.WriteLine($"      - 长度={sp.Length}m, 直径={sp.Diameter * 1000}mm, 粗糙度={sp.Roughness}");
                    }
                }
                if (pipe.BenderSections.Any())
                {
                    Console.WriteLine($"    - 弯头 ({pipe.BenderSections.Count}类):");
                    foreach (var bender in pipe.BenderSections)
                    {
                        Console.WriteLine($"      - 数量={bender.Quantity}, 角度={bender.AngleDegrees}°, R/D比={bender.RadiusToDiameterRatio}");
                    }
                }
            }
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintTeeJunctions(List<TeeJunction> junctions)
        {
            Console.WriteLine($"\n---------------- 5. 三通 ({junctions.Count}个) --------------------");
            foreach (var tee in junctions)
            {
                Console.WriteLine($"  - 三通ID: {tee.Index}");
                Console.WriteLine($"    - 连接点A: {tee.JunctionsA}");
                Console.WriteLine($"    - 连接点B: {tee.JunctionsB}");
                Console.WriteLine($"    - 连接点C: {tee.JunctionsC}");
            }
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintReducers(List<Reducing> reducers)
        {
            Console.WriteLine($"\n---------------- 6. 变径 ({reducers.Count}个) ----------------------");
            foreach (var reducer in reducers)
            {
                Console.WriteLine($"  - 变径ID: {reducer.Index}");
                Console.WriteLine($"    - 连接: {reducer.JunctionsA} <--> {reducer.JunctionsB}");
                Console.WriteLine($"    - 尺寸: 直径A={reducer.DiameterA * 1000}mm, 直径B={reducer.DiameterB * 1000}mm, 角度={reducer.AngleDegrees}°");
            }
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintValves(List<Valve> valves)
        {
            Console.WriteLine($"\n---------------- 7. 阀门 ({valves.Count}个) ----------------------");
            foreach (var valve in valves)
            {
                Console.WriteLine($"  - 阀门ID: {valve.Index}");
                Console.WriteLine($"    - 连接: {valve.JunctionsA} <--> {valve.JunctionsB}");
                Console.WriteLine($"    - 直径: {valve.Diameter * 1000}mm, 类型: {valve.ValveType}, 开度: {valve.ValveMeasure}, 流向: {valve.FlowDirection}");
            }
            Console.WriteLine("--------------------------------------------------");
        }

        private void PrintLimitValves(List<LimitFlowValve> lfvs, List<LimitDropPValve> ldpvs, List<LimitPressureValve> lpvs)
        {
            Console.WriteLine($"\n---------------- 8. 特殊阀门 ---------------------");
            if (!lfvs.Any() && !ldpvs.Any() && !lpvs.Any())
            {
                Console.WriteLine("  - 未发现任何特殊限制阀门。");
            }
            else
            {
                // 打印限流阀 (LimitFlowValve)
                if (lfvs.Any())
                {
                    Console.WriteLine($"\n  --- 限流阀 ({lfvs.Count}个) ---");
                    foreach (var lfv in lfvs)
                    {
                        Console.WriteLine($"    - 阀门ID: {lfv.Index}");
                        Console.WriteLine($"      - 连接: {lfv.JunctionsA} <--> {lfv.JunctionsB}");
                        Console.WriteLine($"      - 流向: {lfv.FlowDirection}, 设定流量: {lfv.SetFlow}");
                    }
                }

                // 打印恒压降阀 (LimitDropPValve)
                if (ldpvs.Any())
                {
                    Console.WriteLine($"\n  --- 恒压降阀 ({ldpvs.Count}个) ---");
                    foreach (var ldpv in ldpvs)
                    {
                        Console.WriteLine($"    - 阀门ID: {ldpv.Index}");
                        Console.WriteLine($"      - 连接: {ldpv.JunctionsA} <--> {ldpv.JunctionsB}");
                        Console.WriteLine($"      - 流向: {ldpv.FlowDirection}, 设定压降: {ldpv.SetDropP}");
                    }
                }

                // 打印限压阀 (LimitPressureValve)
                if (lpvs.Any())
                {
                    Console.WriteLine($"\n  --- 限压阀 ({lpvs.Count}个) ---");
                    foreach (var lpv in lpvs)
                    {
                        Console.WriteLine($"    - 阀门ID: {lpv.Index}");
                        Console.WriteLine($"      - 连接: {lpv.JunctionsA} <--> {lpv.JunctionsB}");
                        Console.WriteLine($"      - 流向: {lpv.FlowDirection}, 设定压力: {lpv.SetP}");
                    }
                }
            }
            Console.WriteLine("--------------------------------------------------");
        }
    }
}