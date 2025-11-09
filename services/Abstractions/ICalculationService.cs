using Project.Models;


namespace Project.Services.Abstractions
{
    // 定义了管网水力计算服务的接口。
    public interface ICalculationService
    {
        // 执行核心的管网水力平衡计算。
        bool GasDuctCalc(ref SystemComponents components);

        // 将在 SystemParameter 中计算出的核心结果（如节点压力、管道流量）加载回具体的组件模型中，并计算各组件的派生指标。
        void LoadData(ref SystemComponents components);
    }
}