using MathNet.Numerics.LinearAlgebra;
using Project.Models;
using Project.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Project.Services.Implementations
{
    // 提供了管网水力计算的具体实现。
    public class CalculationService : ICalculationService
    {
        #region 公开接口方法

        /// 执行核心的管网水力平衡计算。
        public bool GasDuctCalc(ref SystemComponents components)
        {
            // ==========================================================================================
            // 步骤 1: 初始化和设定边界条件
            // ==========================================================================================

            // 提取流体和管网基础数据
            double fluidDensity = components.SystemParameter.FluidDensity;
            double T = components.SystemParameter.Temperature;
            double fluidSickness = components.SystemParameter.FluidSickness;

            int dotNum = components.SystemParameter.DotNum;
            int pipeNum = components.SystemParameter.PipeNum;


            // 设定压力边界：选取所有空压站中的最高压力作为基准压力(Base_Pressure)，
            // 并计算各空压站相对于该基准的压降(CompressionPress)。
            double base_Pressure;
            double[] compressionPress = components.AirCompressionStations.Select(s => s.TempPressure).ToArray();
            (base_Pressure, compressionPress) = TackleCompressionPress(compressionPress);


            // 将管网拓扑结构的整数矩阵(int[,])转换为计算所需的双精度浮点数矩阵(Matrix<double>)
            Matrix<double> A = ConvertToDoubleMatrix(components.SystemParameter.Matrix);
            Matrix<double> AT = A.Transpose(); // 计算A的转置矩阵AT

            // 创建节点压降初始向量 Ap
            double[] apInitial = new double[dotNum];
            for (int i = 0; i < compressionPress.Length; i++)
            {
                apInitial[dotNum - compressionPress.Length + i] = compressionPress[i];
            }
            Matrix<double> Ap = Matrix<double>.Build.DenseOfRowMajor(dotNum, 1, apInitial);

            // 设定流量边界：获取所有用户端的初始流量，并转换为质量流量，构建节点流出向量 q
            double[] users_q_v = components.UserSides.Select(u => u.InitialFlow).ToArray();
            double[] q_v_initial = new double[dotNum - components.AirCompressionStations.Count];
            for (int i = 0; i < users_q_v.Length; i++)
            {
                q_v_initial[q_v_initial.Length - users_q_v.Length + i] = users_q_v[i];
            }
            Matrix<double> q_v = Matrix<double>.Build.DenseOfRowMajor(dotNum - components.AirCompressionStations.Count, 1, q_v_initial);
            double[] q_m = new double[dotNum - components.AirCompressionStations.Count];
            for (int i = 0; i < q_m.Length; i++)
            {
                q_m[i] = (fluidDensity * q_v[i, 0]) / 60; // 从 m³/min 转换为 kg/s
            }
            Matrix<double> q = Matrix<double>.Build.Dense(q_m.Length, 1, q_m);

            // 初始化管道流量向量 AQ 
            Matrix<double> AQ = Matrix<double>.Build.Dense(pipeNum, 1, Enumerable.Repeat(1.0, pipeNum).ToArray());

            // 声明将在循环中使用的矩阵和向量
            Matrix<double> RMatrix, SMatrix, G, p = null, P;
            Matrix<double> Q = null;
            Matrix<double> Press = Matrix<double>.Build.Dense(pipeNum, 1);

            // ============================================== 第一次平差计算 =========================================================================
            // ============================================== 第一次平差计算 =========================================================================
            // ============================================== 第一次平差计算 =========================================================================
            // ============================================== 第一次平差计算 =========================================================================
            // ============================================== 第一次平差计算 =========================================================================

            for (int i = 0; i < pipeNum; i++)
            {
                double press1 = Ap[components.SystemParameter.listPipeJunction[i][0], 0];
                double press2 = Ap[components.SystemParameter.listPipeJunction[i][1], 0];
                Press[i, 0] = (press1 + press2) / 2;
            }

            double[] density = new double[pipeNum];
            for (int i = 0; i < pipeNum; i++)
            {
                density[i] = fluidDensity * (273.15 / T) * ((base_Pressure - Press[i, 0]) / 101325.0);
                //Console.WriteLine("{0}", density[i]);
            }

            // 2.2 - 更新流速 (V), 雷诺数 (Re), 和摩擦系数 (f)
            double[] VArray = new double[pipeNum];
            for (int i = 0; i < pipeNum; i++)
            {
                VArray[i] = CalculateV(AQ[i, 0], components.CompositePipes[i].StraightPipeSections[0].Diameter, density[i]);
                //Console.WriteLine(VArray[i]);
            }

            double[] ReArray = new double[pipeNum];
            for (int i = 0; i < pipeNum; i++)
            {
                ReArray[i] = CalculateRe(VArray[i], components.CompositePipes[i].StraightPipeSections[0].Diameter, density[i], fluidSickness);
                //Console.WriteLine(ReArray[i]);
            }

            double[] fArray = new double[pipeNum];
            for (int i = 0; i < pipeNum; i++)
            {
                fArray[i] = Calculatef(components.CompositePipes[i].StraightPipeSections[0].Diameter, ReArray[i], components.CompositePipes[i].StraightPipeSections[0].Roughness);
                //Console.WriteLine(fArray[i]);
            }

            // 2.3 - 计算每条管道的总阻力系数 R 
            double[] RArray = new double[pipeNum];
            for (int i = 0; i < pipeNum; i++)
            {
                // 计算直管段阻力
                foreach (var sp in components.CompositePipes[i].StraightPipeSections)
                {
                    //Console.WriteLine("{0}, {1}, {2}", i, sp.Length, sp.Diameter);
                    RArray[i] += CalculateStraightPipe(sp.Length, sp.Diameter, density[i], fArray[i]);
                }
                // 计算弯头段阻力
                foreach (var bender in components.CompositePipes[i].BenderSections)
                {
                    double mainDiameter = components.CompositePipes[i].StraightPipeSections.First().Diameter;
                    double benderResistance = bender.Quantity * CalculateWANTOU(mainDiameter, bender.AngleDegrees, fArray[i]);
                    RArray[i] += CalculateBender(benderResistance, mainDiameter, density[i], fArray[i]);
                }
            }

            // 附加普通阀门阻力
            foreach (var valve in components.Valves)
            {
                if (components.SystemParameter.PipeNameDict.TryGetValue(valve.JunctionsA, out int pipeIndex))
                {
                    RArray[pipeIndex] += Calculatevalve(valve.Diameter, density[pipeIndex]);
                }
            }
            // 附加变径阻力
            foreach (var reducer in components.Reducers)
            {
                if (components.SystemParameter.PipeNameDict.TryGetValue(reducer.JunctionsA, out int pipeIndex))
                {
                    RArray[pipeIndex] += CalculateReducing(reducer.DiameterA, reducer.DiameterB, reducer.AngleDegrees, AQ[pipeIndex, 0], density[pipeIndex], ReArray[pipeIndex]);
                }
            }

            // 2.4 - 构建并求解线性方程组 (Y * p = q)
            RMatrix = Matrix<double>.Build.DiagonalOfDiagonalArray(RArray);
            SMatrix = RMatrix * AQ.PointwiseAbs();
            double[] sVector = SMatrix.Column(0).ToArray();
            double[] gDiagonal = new double[sVector.Length];
            for (int i = 0; i < sVector.Length; i++)
            {
                gDiagonal[i] = (sVector[i] == 0) ? 0 : (1.0 / sVector[i]);
            }
            G = Matrix<double>.Build.DiagonalOfDiagonalArray(gDiagonal);

            Matrix<double> Y = A * G * AT;
            Matrix<double> YY = Y.SubMatrix(0, Y.RowCount - components.AirCompressionStations.Count, 0, Y.ColumnCount - components.AirCompressionStations.Count);
            Matrix<double> YT = YY.Inverse(); // 求逆

            // 补偿气源节点的已知压降对流量向量q的影响
            for (int i = 0; i < components.AirCompressionStations.Count; i++)
            {
                string compression_junction = components.AirCompressionStations[i].JunctionNodeId;
                foreach (var item in components.SystemParameter.PipeNameDict)
                {
                    if (item.Key == compression_junction)
                    {
                        // 原始代码的逻辑：找到气源连接的管道，然后找到该管道的另一个节点，
                        // 将该节点的流量 q 值设置为等于气源的流出量。
                        int pipeG_Index = item.Value;
                        int dotQ_Index = components.SystemParameter.listPipeJunction[pipeG_Index][1]; // 假设气源在A端, B端是下游节点
                        int ap_Index = dotNum - components.AirCompressionStations.Count + i;

                        if (dotQ_Index < q.RowCount)
                        {
                            q[dotQ_Index, 0] = G[pipeG_Index, pipeG_Index] * apInitial[ap_Index];
                        }
                    }
                }
            }

            p = YT * q; // 求解非气源节点的压降 p

            // 2.5 - 求解管道流量 Q
            Matrix<double> p_padded = p.Stack(Matrix<double>.Build.DenseOfColumnArrays(compressionPress)); // 将所有节点压降合并
            P = AT * p_padded;

            //for (int i = 0; i < P.RowCount; i++)
            //{
            //    Console.WriteLine(P[i, 0]);
            //}


            Q = G * P;


            // ============================================== 第一次平差计算结束 =========================================================================
            // ============================================== 第一次平差计算结束 =========================================================================
            // ============================================== 第一次平差计算结束 =========================================================================
            // ============================================== 第一次平差计算结束 =========================================================================
            // ============================================== 第一次平差计算结束 =========================================================================


            // 初始化迭代参数
            double threshold = 0.001; // 收敛阈值
            double e = double.MaxValue; // 当前最大相对误差
            int count = 0; // 迭代计数器

            // ==========================================================================================
            // 步骤 2: 核心迭代计算 (while 循环)
            // ==========================================================================================
            while (e > threshold)
            {
                count++;

                if (count > 25) break; // 防止死循环，设置最大迭代次数
                
                bool Is_Zero = false;
                for (int i = 0; i < Q.RowCount; i++)
                {
                    double element = Q[i, 0] * 60 / fluidDensity;
                    if (Math.Abs(element) < 0.2)
                    {
                        Is_Zero = true;
                    }
                }
                if (Is_Zero) { break; }

                foreach (var lpv in components.LimitPressureValves)
                {
                    // 在原始代码中，LPV阀门会直接修改节点压力作为已知条件
                    if (components.SystemParameter.DotNameDict.TryGetValue(lpv.Index, out int dotIndex))
                    {
                        p[dotIndex, 0] = base_Pressure - lpv.SetP;
                    }
                }
                // 2.1 - 更新每条管道的平均压力和流体密度
                for (int i = 0; i < pipeNum - components.AirCompressionStations.Count; i++)
                {
                    double press1 = p[components.SystemParameter.listPipeJunction[i][0], 0];
                    double press2 = p[components.SystemParameter.listPipeJunction[i][1], 0];
                    Press[i, 0] = (press1 + press2) / 2;
                }
                // p 不包含连接空压站的管段压力
                for (int i = 0; i < components.AirCompressionStations.Count; i ++)
                {
                    double press1 = base_Pressure - components.AirCompressionStations[i].TempPressure;
                    double press2 = 0;

                    string compression_junction = components.AirCompressionStations[i].JunctionNodeId;
                    foreach (var item in components.SystemParameter.PipeNameDict)
                    {
                        if (item.Key == compression_junction)
                        {
                            // 找到气源连接的管道，然后找到该管道的另一个节点，
                            int pipeG_Index = item.Value;
                            int dotQ_Index = components.SystemParameter.listPipeJunction[pipeG_Index][1];
                            press2 = p[dotQ_Index, 0];
                        }
                    }

                    Press[pipeNum - components.AirCompressionStations.Count + i, 0] = (press1 + press2) / 2;
                }

                density = new double[pipeNum];
                for (int i = 0; i < pipeNum; i++)
                {
                    //density[i] = 11.7285 * (0.618812 - Press[i, 0] / 1000000);
                    density[i] = fluidDensity * (273.15 / T) * ((base_Pressure - Press[i, 0]) / 101325.0);
                    //Console.WriteLine("{0}", density[i]);
                }

                // 2.2 - 更新流速 (V), 雷诺数 (Re), 和摩擦系数 (f)
                VArray = new double[pipeNum];
                for (int i = 0; i < pipeNum; i++)
                {
                    VArray[i] = CalculateV(AQ[i, 0], components.CompositePipes[i].StraightPipeSections[0].Diameter, density[i]);
                    //Console.WriteLine(VArray[i]);
                }

                ReArray = new double[pipeNum];
                for (int i = 0; i < pipeNum; i++)
                {
                    ReArray[i] = CalculateRe(VArray[i], components.CompositePipes[i].StraightPipeSections[0].Diameter, density[i], fluidSickness);
                    //Console.WriteLine(ReArray[i]);
                }

                fArray = new double[pipeNum];
                for (int i = 0; i < pipeNum; i++)
                {
                    fArray[i] = Calculatef(components.CompositePipes[i].StraightPipeSections[0].Diameter, ReArray[i], components.CompositePipes[i].StraightPipeSections[0].Roughness);
                    //Console.WriteLine(components.CompositePipes[i].StraightPipeSections[0].Roughness);
                }

                // 2.3 - 计算每条管道的总阻力系数 R 
                RArray = new double[pipeNum];
                for (int i = 0; i < pipeNum; i++)
                {
                    // 计算直管段阻力
                    foreach (var sp in components.CompositePipes[i].StraightPipeSections)
                    {
                        RArray[i] += CalculateStraightPipe(sp.Length, sp.Diameter, density[i], fArray[i]);
                    }
                    // 计算弯头段阻力
                    foreach (var bender in components.CompositePipes[i].BenderSections)
                    {
                        double mainDiameter = components.CompositePipes[i].StraightPipeSections.First().Diameter;
                        double benderResistance = bender.Quantity * CalculateWANTOU(mainDiameter, bender.AngleDegrees, fArray[i]);
                        RArray[i] += CalculateBender(benderResistance, mainDiameter, density[i], fArray[i]);
                    }
                }

                // 附加普通阀门阻力
                foreach (var valve in components.Valves)
                {
                    if (components.SystemParameter.PipeNameDict.TryGetValue(valve.JunctionsA, out int pipeIndex))
                    {
                        RArray[pipeIndex] += Calculatevalve(valve.Diameter, density[pipeIndex]);
                    }
                }
                // 附加变径阻力
                foreach (var reducer in components.Reducers)
                {
                    if (components.SystemParameter.PipeNameDict.TryGetValue(reducer.JunctionsA, out int pipeIndex))
                    {
                        RArray[pipeIndex] += CalculateReducing(reducer.DiameterA, reducer.DiameterB, reducer.AngleDegrees, AQ[pipeIndex, 0], density[pipeIndex], ReArray[pipeIndex]);
                    }
                }

                // 2.4 - 处理特殊阀门 (通过调整R值来强制满足约束)
                // 限制流量阀门 (LFV)
                foreach (var lfv in components.LimitFlowValves)
                {
                    if (components.SystemParameter.PipeNameDict.TryGetValue(lfv.JunctionsA, out int pipeIdx))
                    {
                        int nodeA, nodeB;
                        if (lfv.FlowDirection == 1)
                        {
                            nodeA = components.SystemParameter.listPipeJunction[pipeIdx][0];
                            nodeB = components.SystemParameter.listPipeJunction[pipeIdx][1];
                        }
                        else
                        {
                            nodeB = components.SystemParameter.listPipeJunction[pipeIdx][0];
                            nodeA = components.SystemParameter.listPipeJunction[pipeIdx][1];
                        }

                        double pressureDrop = Math.Abs(p[nodeA, 0] - p[nodeB, 0]);
                        double targetFlowSquared = Math.Pow(lfv.SetFlow / 60.0 * fluidDensity, 2);
                        //Console.WriteLine("{0}, {1}", pressureDrop, targetFlowSquared);

                        RArray[pipeIdx] = pressureDrop / targetFlowSquared;
                        //Console.WriteLine("{0}, {1}", RArray[pipeIdx], pressureDrop);
                    }
                }

                // 恒定压降阀门 (LDPV)
                foreach (var ldpv in components.LimitDropPValves)
                {
                    if (components.SystemParameter.PipeNameDict.TryGetValue(ldpv.JunctionsA, out int pipeIdx))
                    {
                        double currentFlowSquared = Q[pipeIdx, 0] * Q[pipeIdx, 0];

                        // 在原有管道阻力基础上增加一个附加阻力
                        RArray[pipeIdx] += ldpv.SetDropP / currentFlowSquared;
                    }
                }

                // 限制阀后压力阀门 (LPV)
                foreach (var lpv in components.LimitPressureValves)
                {
                    if (components.SystemParameter.PipeNameDict.TryGetValue(lpv.JunctionsA, out int pipeIdx))
                    {
                        double currentFlow = Q[pipeIdx, 0];
                        double currentFlowSquared = currentFlow * currentFlow;

                        if (currentFlowSquared > 0)
                        {
                            // -- 判断实际流向 --
                            int upstreamNode, downstreamNode;
                            int nodeA_idx = components.SystemParameter.listPipeJunction[pipeIdx][0];
                            int nodeB_idx = components.SystemParameter.listPipeJunction[pipeIdx][1];

                            bool isFlowFromAtoB = (components.CompositePipes[pipeIdx].FlowDirection == 1 && currentFlow > 0) ||
                                                (components.CompositePipes[pipeIdx].FlowDirection == -1 && currentFlow < 0);

                            if (isFlowFromAtoB)
                            {
                                upstreamNode = nodeA_idx;
                                downstreamNode = nodeB_idx;
                            }
                            else
                            {
                                upstreamNode = nodeB_idx;
                                downstreamNode = nodeA_idx;
                            }

                            double p_upstream = base_Pressure - p[upstreamNode, 0];
                            double requiredPressureDrop = p_upstream - lpv.SetP;

                            if (requiredPressureDrop > 0)
                            {
                                RArray[pipeIdx] = requiredPressureDrop / currentFlowSquared;
                            }
                        }
                    }
                }

                // 2.5 - 构建并求解线性方程组 (Y * p = q)
                RMatrix = Matrix<double>.Build.DiagonalOfDiagonalArray(RArray);
                SMatrix = RMatrix * AQ.PointwiseAbs();
                sVector = SMatrix.Column(0).ToArray();
                gDiagonal = new double[sVector.Length];
                for (int i = 0; i < sVector.Length; i++)
                {
                    gDiagonal[i] = (sVector[i] == 0) ? 0 : (1.0 / sVector[i]);
                }
                G = Matrix<double>.Build.DiagonalOfDiagonalArray(gDiagonal);

                Y = A * G * AT;
                YY = Y.SubMatrix(0, Y.RowCount - components.AirCompressionStations.Count, 0, Y.ColumnCount - components.AirCompressionStations.Count);
                YT = YY.Inverse(); // 求逆

                // 补偿气源节点的已知压降对流量向量q的影响
                for (int i = 0; i < components.AirCompressionStations.Count; i++)
                {
                    string compression_junction = components.AirCompressionStations[i].JunctionNodeId;
                    foreach (var item in components.SystemParameter.PipeNameDict)
                    {
                        if (item.Key == compression_junction)
                        {
                            // 原始代码的逻辑：找到气源连接的管道，然后找到该管道的另一个节点，
                            // 将该节点的流量 q 值设置为等于气源的流出量。
                            int pipeG_Index = item.Value;
                            int dotQ_Index = components.SystemParameter.listPipeJunction[pipeG_Index][1]; // 假设气源在A端, B端是下游节点
                            int ap_Index = dotNum - components.AirCompressionStations.Count + i;

                            if (dotQ_Index < q.RowCount)
                            {
                                q[dotQ_Index, 0] = G[pipeG_Index, pipeG_Index] * apInitial[ap_Index];
                            }
                        }
                    }
                }

                p = YT * q; // 求解非气源节点的压降 p

                // 2.6 - 求解管道流量 Q
                p_padded = p.Stack(Matrix<double>.Build.DenseOfColumnArrays(compressionPress)); // 将所有节点压降合并
                P = AT * p_padded; 
                Q = G * P;

                // 2.7 - 检查收敛性并更新流量
                Matrix<double> relativeDiff = (Q - AQ).PointwiseDivide(AQ);
                e = relativeDiff.PointwiseAbs().Enumerate().Max(v => double.IsNaN(v) ? 0 : v); // 计算最大相对误差
                AQ = (AQ + Q) / 2;

            }

            // ==========================================================================================
            // 步骤 3: 结果处理与验证
            // ==========================================================================================

            bool Is_Satisfied = true;
            components.SystemParameter.Dot_Pressure = new List<double>(new double[dotNum]);
            components.SystemParameter.Pipe_Flow = new List<double>(new double[pipeNum]);

            // 3.1 - 将结果写入SystemParameter
            for (int i = 0; i < p_padded.RowCount; i++)
            {
                components.SystemParameter.Dot_Pressure[i] = base_Pressure - p_padded[i, 0];
                if (double.IsNaN(components.SystemParameter.Dot_Pressure[i]))
                {
                    //Console.WriteLine(0);
                    Is_Satisfied = false;
                }
            }

            for (int i = 0; i < Q.RowCount; i++)
            {
                components.SystemParameter.Pipe_Flow[i] = Q[i, 0] * 60 / fluidDensity;
                //Console.WriteLine(Q[i, 0]);
            }

            // 3.2 - 验证结果
            // 验证用户端压力是否满足要求
            foreach (var user in components.UserSides)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(user.Index, out int dotIndex))
                {
                    if (components.SystemParameter.Dot_Pressure[dotIndex] < user.WarnPressure)
                    {
                        //Console.WriteLine(1);
                        Is_Satisfied = false;
                        break;
                    }
                }
            }

            // 验证空压站出口是否倒流
            foreach (var station in components.AirCompressionStations)
            {
                if (components.SystemParameter.PipeNameDict.TryGetValue(station.JunctionNodeId, out int pipeIndex))
                {
                    if (AQ[pipeIndex, 0] < 0) // 假设气源连接在管道A端，流向为A->B，流量应为正
                    {
                        //Console.WriteLine(2);
                        Is_Satisfied = false;
                        break;
                    }
                }
            }

            return Is_Satisfied;
        }


        // 将计算结果加载回组件模型
        public void LoadData(ref SystemComponents components)
        {
            double total_power = 0;

            // 更新空压站状态并计算功率
            for (int i = 0; i < components.AirCompressionStations.Count; i++)
            {
                var station = components.AirCompressionStations[i];

                // 从水力计算结果中获取站点的实时流量和压力
                if (components.SystemParameter.PipeNameDict.TryGetValue(station.JunctionNodeId, out int pipeIndex))
                {
                    station.RealTimeFlow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
                if (components.SystemParameter.DotNameDict.TryGetValue(station.Index, out int dotIndex))
                {
                    station.RealTimePressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }

                // 根据站点所需流量，计算出各压缩机的阀门开度、开启数量和因保压产生的浪费流量
                var (isSpillage, valves, openNum, realTotalFlow, wastedFlow) = CalculateValves(
                    station.Compressors.Count,
                    station.Compressors.First().MaxFlow,
                    station.Compressors.First().MinValveDegree,
                    station.RealTimeFlow
                );

                station.OpenCompressorCount = openNum;
                station.WastedFlow = wastedFlow;

                // 更新每一台压缩机的状态（阀门、流量、单机功率）
                for (int j = 0; j < station.Compressors.Count; j++)
                {
                    station.Compressors[j].CurrentValveDegree = valves[j];
                    station.Compressors[j].RealTimeFlow = valves[j] * station.Compressors[j].MaxFlow;
                    // 调用 Calculate_SinglePower 计算单台压缩机的功率并赋值
                    station.Compressors[j].RealTimePower = Calculate_SinglePower(station.Index, station.RealTimePressure, station.Compressors[j].RealTimeFlow);
                }

                // 计算包含浪费流量的站点总输出流量
                double stationGrossFlow = station.RealTimeFlow + station.WastedFlow;

                // 根据“站点总输出流量”计算“站点总功率”
                station.TotalPower = Calculate_StationPower(station.Index, station.RealTimePressure, stationGrossFlow);

                // 累加到系统总功率
                total_power += station.TotalPower;
            }
            components.SystemParameter.Totalpower = total_power;

            // 更新用户端、三通、变径、阀门的实时数据
            foreach (var user in components.UserSides)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(user.Index, out int dotIndex))
                {
                    user.RealTimePressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                if (components.SystemParameter.PipeNameDict.TryGetValue(user.JunctionNodeId, out int pipeIndex))
                {
                    user.RealTimeFlow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
            }

            foreach (var tee in components.TeeJunctions)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(tee.Index, out int dotIndex))
                {
                    tee.Pressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                if (components.SystemParameter.PipeNameDict.TryGetValue(tee.JunctionsA, out int pipeA)) tee.FlowA = components.SystemParameter.Pipe_Flow[pipeA];
                if (components.SystemParameter.PipeNameDict.TryGetValue(tee.JunctionsB, out int pipeB)) tee.FlowB = components.SystemParameter.Pipe_Flow[pipeB];
                if (components.SystemParameter.PipeNameDict.TryGetValue(tee.JunctionsC, out int pipeC)) tee.FlowC = components.SystemParameter.Pipe_Flow[pipeC];
            }

            // 更新变径的实时数据
            foreach (var reducer in components.Reducers)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(reducer.Index, out int dotIndex))
                {
                    reducer.Pressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                // 变径的流量通常等于其连接的某一条管道的流量，这里以A端为准
                if (components.SystemParameter.PipeNameDict.TryGetValue(reducer.JunctionsA, out int pipeIndex))
                {
                    reducer.Flow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
            }

            // 更新普通阀门的实时数据
            foreach (var valve in components.Valves)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(valve.Index, out int dotIndex))
                {
                    valve.Pressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                if (components.SystemParameter.PipeNameDict.TryGetValue(valve.JunctionsA, out int pipeIndex))
                {
                    valve.Flow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
            }

            // 更新特殊阀门（限流阀）的实时数据
            foreach (var lfv in components.LimitFlowValves)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(lfv.Index, out int dotIndex))
                {
                    lfv.Pressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                if (components.SystemParameter.PipeNameDict.TryGetValue(lfv.JunctionsA, out int pipeIndex))
                {
                    lfv.Flow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
            }

            // 更新特殊阀门（恒压降阀）的实时数据
            foreach (var ldpv in components.LimitDropPValves)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(ldpv.Index, out int dotIndex))
                {
                    ldpv.Pressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                if (components.SystemParameter.PipeNameDict.TryGetValue(ldpv.JunctionsA, out int pipeIndex))
                {
                    ldpv.Flow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
            }

            // 更新特殊阀门（限压阀）的实时数据
            foreach (var lpv in components.LimitPressureValves)
            {
                if (components.SystemParameter.DotNameDict.TryGetValue(lpv.Index, out int dotIndex))
                {
                    lpv.Pressure = components.SystemParameter.Dot_Pressure[dotIndex];
                }
                if (components.SystemParameter.PipeNameDict.TryGetValue(lpv.JunctionsA, out int pipeIndex))
                {
                    lpv.Flow = components.SystemParameter.Pipe_Flow[pipeIndex];
                }
            }

            // 计算管道的详细结果，如压降、效率等
            for (int i = 0; i < components.CompositePipes.Count; i++)
            {
                var pipe = components.CompositePipes[i];
                pipe.Flow = components.SystemParameter.Pipe_Flow[i];

                int indexA = components.SystemParameter.listPipeJunction[i][0];
                int indexB = components.SystemParameter.listPipeJunction[i][1];

                pipe.PressureA = components.SystemParameter.Dot_Pressure[indexA];
                pipe.PressureB = components.SystemParameter.Dot_Pressure[indexB];
                pipe.DropPressure = Math.Abs(pipe.PressureA - pipe.PressureB);
                pipe.AveragePressure = (pipe.PressureA + pipe.PressureB) / 2.0;

                // 计算单根管道的输送能效
                double e_in = Math.Abs(pipe.Flow) * Math.Log(pipe.PressureA / 101325.0);
                double e_out = Math.Abs(pipe.Flow) * Math.Log(pipe.PressureB / 101325.0);
                if (e_in > e_out)
                {
                    pipe.PipeEfficiency = e_out / e_in;
                    // Console.WriteLine(pipe.PipeEfficiency);
                }
                else
                {
                    pipe.PipeEfficiency = e_in / e_out;
                }
            }

            // 计算系统总能效
            double e_in_total = 0;
            double e_out_total = 0;
            foreach (var station in components.AirCompressionStations)
            {
                // Console.WriteLine("{0}, {1}", station.RealTimeFlow, station.WastedFlow);
                e_in_total += station.RealTimeFlow * Math.Log(station.RealTimePressure / 101325.0);
            }
            foreach (var user in components.UserSides)
            {
                e_out_total += Math.Abs(user.RealTimeFlow) * Math.Log(user.RealTimePressure / 101325.0);
            }

            if (e_in_total > 0)
            {
                components.SystemParameter.SystemEfficiency = e_out_total / e_in_total;
            }

            // 计算包含浪费流量的总能效 (TotalEfficiency)
            double e_in_gross_total = 0;
            foreach (var station in components.AirCompressionStations)
            {
                // 使用未扣除浪费的实时流量来计算总输入能量
                e_in_gross_total += (station.RealTimeFlow + station.WastedFlow) * Math.Log(station.RealTimePressure / 101325.0);
            }
            if (e_in_gross_total > 0)
            {
                components.SystemParameter.TotalEfficiency = e_out_total / e_in_gross_total;
            }
            else
            {
                components.SystemParameter.TotalEfficiency = 0;
            }


        }


        #endregion

        #region 私有辅助方法 

        // 计算压降系数R，λ不好打，用f代替表示
        private static double CalculateStraightPipe(double length, double diameter, double density, double f)
        {
            double pi = Math.PI;
            return f * 8 / (Math.Pow(pi, 2)) * (length / Math.Pow(diameter, 5)) * (1 / density);
        }

        // 计算弯管段系数
        private static double CalculateBender(double ratio, double diameter, double density, double f)
        {
            double pi = Math.PI;
            return f * 8 / (Math.Pow(pi, 2)) * (ratio / Math.Pow(diameter, 5)) * (1 / density);
        }

        // 计算雷诺数
        private static double CalculateRe(double v, double D, double density, double stickiness)
        {
            if (stickiness == 0) return double.MaxValue;
            return (v * D * density * Math.Pow(10, 6)) / stickiness;
        }

        // 流速与质量流量的关系
        private static double CalculateV(double Q, double D, double density)
        {
            double pi = Math.PI;
            return Math.Abs(4 * Q / (pi * Math.Pow(D, 2) * density));
        }

        private static double CalculateExpression(double D, double Re, double f, double roughness)
        {
            // 计算表达式
            double expression = 1.14 - 2 * Math.Log10(roughness / (1000 * D) + 9.35 / (Re * Math.Pow(f, 0.5)));
            double result = Math.Pow(expression, -2);
            return result;
        }

        private static double Calculatef(double D, double Re, double roughness)
        {
            double f = 0.01; // 初始猜测值
            // 迭代求解
            for (int i = 0; i < 1000; i++)
            {
                double leftSide = CalculateExpression(D, Re, f, roughness);
                double rightSide = f;

                // 判断是否满足精度要求
                if (Math.Abs(leftSide - rightSide) < 0.000001)
                {
                    break;
                }

                // 更新f的值
                f = leftSide;
            }
            return f;
        }

        private static double CalculateReducing(double D0, double D1, double a, double flow, double density, double Re)
        {
            double pi = Math.PI;
            double S0 = Math.Pow(pi, 1) * Math.Pow(D0, 2) / 4;
            double S1 = Math.Pow(pi, 1) * Math.Pow(D1, 2) / 4;
            double y = S1 / S0;
            double K1 = 20 * Math.Pow(y, 0.33) / Math.Pow(Math.Tan(a), 0.75);
            double K2 = 19 / (Math.Pow(y, 0.5) * Math.Pow(Math.Tan(a), 0.75));
            double result_K;
            double result_R;

            if (flow > 0)
            {
                if (S0 > S1) { result_K = K2; }
                else { result_K = K1; }
            }
            else
            {
                if (S0 > S1) { result_K = K1; }
                else { result_K = K2; }

            }

            result_R = (8 * result_K) / (Math.Pow(pi, 2) * Math.Pow(D1, 4) * density * Re);

            // 添加判别条件
            if (double.IsNaN(result_R))
            {
                result_R = 0; // 或者设置为其他合适的值
            }

            return result_R;
        }

        private static double Calculatevalve(double D, double density)
        {
            // 计算阀门阻力系数，默认为闸阀
            double pi = Math.PI;
            double result = 8 * 0.2 / (Math.Pow(pi, 2) * Math.Pow(D, 4) * density);
            return result;
        }

        private static double CalculateWANTOU(double D, double a, double f)
        {
            // 计算弯头阻力系数
            double pi = Math.PI;
            double result1;

            if (a == 90)
            {
                result1 = 14 * D;
            }
            else
            {
                result1 = (a / 90 - 1) * (0.25 * pi * f * 1.5 + 0.5 * 14 * D) + 14 * D;
            }

            return result1;
        }

        private static (double, double[]) TackleCompressionPress(double[] compressionPress)
        {
            if (compressionPress == null || compressionPress.Length == 0)
                return (0, new double[0]);

            double base_Pressure = compressionPress.Max();
            double[] relativePress = new double[compressionPress.Length];
            for (int i = 0; i < compressionPress.Length; i++)
            {
                relativePress[i] = base_Pressure - compressionPress[i];
            }
            return (base_Pressure, relativePress);
        }

        private static Matrix<double> ConvertToDoubleMatrix(int[,] intArray)
        {
            int rows = intArray.GetLength(0);
            int cols = intArray.GetLength(1);

            var doubleMatrix = Matrix<double>.Build.Dense(rows, cols);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    doubleMatrix[i, j] = (double)intArray[i, j];
                }
            }
            return doubleMatrix;
        }

        private (bool, double[], int, double, double) CalculateValves(int compressorNum, double maxFlow, double lowDegree, double objectiveFlow)
        {
            int lastValvesNum;
            double lastAverageDegree;
            double wasteTotalFlow = 0;
            int openNum;
            double realTotalFlow = 0;
            bool isSolvable = true;
            double[] valves = new double[compressorNum];

            openNum = (int)(objectiveFlow / maxFlow) + 1;
            if (objectiveFlow <= 0) openNum = 0; 

            // 如果所需数量超过拥有数量，则认为不可解
            if (openNum > compressorNum) { isSolvable = false; }

            if (isSolvable)
            {
                double averageDegree = (objectiveFlow / openNum) / maxFlow;

                if (averageDegree < lowDegree)
                {
                    // 平均开度低于下限，则所有开启的阀门都开到下限
                    for (int i = 0; i < openNum; i++)
                    {
                        valves[i] = lowDegree;
                    }
                }
                else if (averageDegree >= lowDegree && openNum == 1)
                {
                    // 只有一台，且开度满足要求
                    valves[0] = averageDegree > 1.0 ? 1.0 : averageDegree;
                }
                else
                {
                    for (int i = 1; i < openNum; i++)
                    {
                        // 尝试将 i 台压缩机设置为100%满负荷
                        for (int ii = 0; ii < i; ii++)
                        {
                            valves[ii] = 1.0;
                        }

                        lastValvesNum = openNum - i;
                        if (lastValvesNum > 0)
                        {
                            lastAverageDegree = ((objectiveFlow - i * maxFlow) / (lastValvesNum * maxFlow));
                        }
                        else // 意味着前面的满开已经超过目标流量
                        {
                            lastAverageDegree = 0;
                        }

                        // 如果剩下的一台是调节机，则将其开度设置为最低开度
                        if (i == openNum - 1)
                        {
                            valves[i] = lowDegree;
                        }

                        if (lastAverageDegree > lowDegree)
                        {
                            for (int j = 0; j < lastValvesNum; j++)
                            {
                                valves[openNum - j - 1] = lastAverageDegree > 1.0 ? 1.0 : lastAverageDegree;
                            }
                            break; // 找到方案，跳出循环
                        }
                    }
                }

                realTotalFlow = 0;
                for (int j = 0; j < openNum; j++)
                {
                    realTotalFlow += valves[j] * maxFlow;
                }
                wasteTotalFlow = realTotalFlow - objectiveFlow;

                if (Math.Abs(wasteTotalFlow) < 0.1)
                {
                    wasteTotalFlow = 0;
                }
            }

            return (isSolvable, valves, openNum, realTotalFlow, wasteTotalFlow);
        }

        private double Calculate_StationPower(string stationIndex, double pressure, double totalGrossFlow)
        {
            double power = 0;

            // 如果总流量小于或等于0，则认为功率为0，直接返回。
            if (totalGrossFlow <= 0)
            {
                return 0;
            }

            if (stationIndex == "AS-1")
            {
                power = 51.7 * pressure * 1e-5 + totalGrossFlow * 5.5833 - 312.37;
            }
            else if (stationIndex == "AS-2")
            {
                power = 51.5 * pressure * 1e-5 + totalGrossFlow * 5.6867 - 264.2;
            }
            else if (stationIndex == "AS-3")
            {
                power = 34.3 * pressure * 1e-5 + totalGrossFlow * 5.93 - 238;
            }
            else
            {
                power = 51.7 * pressure * 1e-5 + totalGrossFlow * 5.5833 - 312.37;
            }

            //power = 50 * pressure * 1e-5 + totalGrossFlow * 5.5 - 300;

            if (power <= 0)
            {
                power = 10;
            }
            return power;
        }

        private double Calculate_SinglePower(string stationIndex, double pressure, double singleFlow)
        {
            double singlePower = 0;

            if (singleFlow <= 0)
            {
                return 0;
            }

            // 根据站点的Index选择不同的计算公式
            if (stationIndex == "AS-1")
            {
                singlePower = 51.7 * pressure * 1e-5 + singleFlow * 5.5833 - 312.37;
            }
            else if (stationIndex == "AS-2")
            {
                singlePower = 51.5 * pressure * 1e-5 + singleFlow * 5.6867 - 264.2;
            }
            else if (stationIndex == "AS-3")
            {
                singlePower = 34.3 * pressure * 1e-5 + singleFlow * 5.93 - 238;
            }
            else
            {
                singlePower = 51.7 * pressure * 1e-5 + singleFlow * 5.5833 - 312.37;
            }

            //singlePower = 50 * pressure * 1e-5 + singleFlow * 5.5 - 300;

            if (singlePower <= 0)
            {
                singlePower = 10;
            }

            return singlePower;
        }

        #endregion
    }
}
