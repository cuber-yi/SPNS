using Newtonsoft.Json;

namespace Project.Models
{
    // 代表单个压缩机及其运行状态和配置
    public class Compressor
    {
        // --- 配置参数 (从JSON文件读取) ---

        // 压缩机的唯一索引或编号
        public required int Index { get; set; }

        // 压缩机在阀门全开时的最大输出流量
        public required double MaxFlow { get; set; }

        // 允许的最低阀门开度 (例如 0.7 表示 70%)
        public required double MinValveDegree { get; set; }


        // --- 运行结果参数 (由服务计算后填充) ---

        /// 当前计算周期中，此压缩机被设定的阀门开度
        public double CurrentValveDegree { get; set; } = 0;

        // 当前计算周期中，此压缩机的实时流量。
        public double RealTimeFlow { get; set; } = 0;

        // 当前计算周期中，此压缩机的实时功率。
        public double RealTimePower { get; set; } = 0;
    }
}
