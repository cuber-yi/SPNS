using Project.Models;


namespace Project.Services.Abstractions
{
    /// 定义了管网优化服务的接口。
    public interface IOptimizationService
    {
        // 基于初始管网状态，寻找最优的空压站压力设置，并返回计算完成后的系统状态。
        SystemComponents FindOptimalPressures(SystemComponents initialComponents);
    }
}
