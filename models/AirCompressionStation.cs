using Project.Models;
using System.Collections.Generic;

namespace Project.Models
{
    /// 代表一个空压站，它包含多个压缩机以及整个站点的状态。
    public class AirCompressionStation
    {
        // --- 核心标识与配置 ---

        // 空压站的唯一编号或ID。
        public required string Index { get; set; }

        /// 空压站的名称。
        public required string Name { get; set; }

        // 空压站在管网中的连接点编号。
        public required string JunctionNodeId { get; set; }

        // 包含此站点下所有的压缩机对象。
        public List<Compressor> Compressors { get; set; } = new();

        // 用于功率计算的参数
        public double a { get; set; }
        public double b { get; set; }
        public double c { get; set; }


        // --- 寻优与计算过程中的状态变量 ---

        // 初始或上一周期的压力，用于动态寻优时的比较基准。
        public double InitialPressure { get; set; }

        /// 在当前优化迭代中，用于计算的临时压力设定值。
        public double TempPressure { get; set; }


        // --- 最终计算结果 ---

        // 最终确定的实时出口压力
        public double RealTimePressure { get; set; }

        // 最终计算出的站点总实时流量
        public double RealTimeFlow { get; set; }

        // 最终计算出的站点总功率。
        public double TotalPower { get; set; }

        // 因阀门开度控制而产生的“浪费”流量
        public double WastedFlow { get; set; }

        /// 实际运行的压缩机数量。
        public int OpenCompressorCount { get; set; }
    }
}
