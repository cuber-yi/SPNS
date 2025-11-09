using Project.Models;
using System.Collections.Generic;

namespace Project.Models
{
    public class CompositePipes
    {
        // --- 核心标识与配置 ---

        // 复合管道的唯一编号或ID。
        public required string Index { get; set; }

        // 管道A端的连接点ID。
        public required string JunctionA { get; set; }

        // 管道B端的连接点ID。
        public required string JunctionB { get; set; }

        // 定义的默认流向 (1 表示 A->B, -1 表示 B->A)。
        public required int FlowDirection { get; set; }

        // 包含此管道所有的直管段部分。
        public List<StraightPipeSection> StraightPipeSections { get; set; } = new();

        // 包含此管道所有的弯头部分。
        public List<BenderSection> BenderSections { get; set; } = new();


        // --- 最终计算结果 ---

        // 管道的平均压力 (单位: Pa)。
        public double AveragePressure { get; set; }

        // A端压力 (单位: Pa)。
        public double PressureA { get; set; }

        // B端压力 (单位: Pa)。
        public double PressureB { get; set; }

        // 管道两端的压降 (单位: Pa)，始终为正值。
        public double DropPressure { get; set; }

        // 管道中的质量流量 (单位: kg/s)。
        public double Flow { get; set; }

        // 单根管道的输送能效。
        public double PipeEfficiency { get; set; }
    }
}
