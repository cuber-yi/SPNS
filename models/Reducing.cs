
namespace Project.Models
{
    // 代表一个变径接头。
    public class Reducing
    {
        // --- 核心标识与配置 ---

        // 变径的唯一编号或ID，通常也是其在管网中的节点ID。
        public required string Index { get; set; }

        // A端的连接管道ID。
        public required string JunctionsA { get; set; }

        // B端的连接管道ID。
        public required string JunctionsB { get; set; }

        // A端的直径 (单位: 米)。
        public required double DiameterA { get; set; }

        // B端的直径 (单位: 米)。
        public required double DiameterB { get; set; }

        // 变径角度 (单位: 度)。
        public required double AngleDegrees { get; set; }


        // --- 最终计算结果 ---

        // 节点的实时压力 (单位: Pa)。
        public double Pressure { get; set; }

        // 流过节点的实时流量。
        public double Flow { get; set; }
    }
}
