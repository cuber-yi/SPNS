using System;
using System.Collections.Generic;

namespace Project.Models
{
    /// 存储系统级的全局参数和拓扑结构信息。
    public class SystemParameter
    {
        // --- 系统配置 ---
        public int SendAlgorithmJsonId { get; set; }
        public long DeviceId { get; set; }
        public int FluidNumber { get; set; }
        public double Temperature { get; set; } // 单位：开尔文 (K)
        public double WarnPressure { get; set; } = 700000; // 默认告警压力

        // --- 拓扑结构 (由JsonFileService解析填充) ---
        public int DotNum { get; set; }
        public int PipeNum { get; set; }
        public int[,] Matrix { get; set; }
        public List<int[]> listPipeJunction { get; set; } = new();
        public Dictionary<string, int> DotNameDict { get; set; } = new();
        public Dictionary<string, int> PipeNameDict { get; set; } = new();

        // --- 流体物理属性 (计算后填充) ---
        public double FluidDensity { get; private set; }
        public double FluidSickness { get; private set; }

        // --- 计算结果 ---
        public double TotalEfficiency { get; set; }
        public double SystemEfficiency { get; set; }
        public double Totalpower { get; set; }
        public List<double> Dot_Pressure { get; set; } = new();
        public List<double> Pipe_Flow { get; set; } = new();

        // 根据流体编号和温度计算流体的密度和动力粘度。
        // 此方法应在 FluidNumber 和 Temperature 被赋值后调用。
        public void InitializeFluidProperties()
        {
            double S;   //（萨瑟兰常数）
            if (FluidNumber == 1)
            {
                // 压缩空气
                S = 110.4;
                FluidDensity = 1.293;
                FluidSickness = 17.20 * Math.Pow(Temperature / 273.15, 1.5) * ((273.15 + S) / (Temperature + S));
            }

            if (FluidNumber == 2)
            {
                // 氮气
                S = 107.0;
                FluidDensity = 1.250;
                FluidSickness = 17.50 * Math.Pow((273.15 + Temperature) / (273.15), 1.5) * ((273.15 + S) / (273.15 + Temperature + S));
            }
            if (FluidNumber == 3)
            {
                // 氧气
                S = 125.0;
                FluidDensity = 1.429;
                FluidSickness = 19.20 * Math.Pow((273.15 + Temperature) / (273.15), 1.5) * ((273.15 + S) / (273.15 + Temperature + S));
            }
            if (FluidNumber == 4)
            {
                // 氩气用幂律公式
                FluidDensity = 1.784;
                FluidSickness = 21.0 * Math.Pow(((273.15 + Temperature) / 273.15), 0.71);
            }
            if (FluidNumber == 5)
            {
                // 天然气，20摄氏度，动力粘度10.66，密度0.75
                S = 198;
                FluidDensity = 0.75;
                FluidSickness = 13.75 * Math.Pow((273.15 + Temperature) / (273.15), 1.5) * ((273.15 + S) / (273.15 + Temperature + S));
            }
            if (FluidNumber == 6)
            {
                // 焦炉煤气
                FluidDensity = 0.46;
                FluidSickness = 11.6 * Math.Pow(((273.15 + Temperature) / 273.15), 0.70);
            }
            if (FluidNumber == 7)
            {
                // 高炉煤气
                FluidDensity = 1.35;
                FluidSickness = 15.79 * Math.Pow(((273.15 + Temperature) / 273.15), 0.80);
            }
            if (FluidNumber == 8)
            {
                // 转炉煤气
                FluidDensity = 1.20;
                FluidSickness = 18 * Math.Pow(((273.15 + Temperature) / 273.15), 0.75);
            }
        }
    }
}
