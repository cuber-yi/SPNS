
namespace Project.Models
{
    // 代表一个用户用气端。
    public class UserSide
    {
        // --- 核心标识与配置 ---

        // 用户端的唯一编号或ID。
        public required string Index { get; set; }

        // 用户端的名称。
        public required string Name { get; set; }

        // 用户端在管网中的连接点ID。
        public required string JunctionNodeId { get; set; }

        // 用户端的基础或初始设定流量。
        public required double InitialFlow { get; set; }

        // 满足用户需求的最低告警压力 (单位: Pa)。
        public required double WarnPressure { get; set; }


        // --- 最终计算结果 ---

        // 最终计算出的用户端实时压力 (单位: Pa)
        public double RealTimePressure { get; set; }

        // 最终计算出的用户端实时流量
        public double RealTimeFlow { get; set; }
    }
}