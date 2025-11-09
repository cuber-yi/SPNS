using Project.Models;

namespace Project.Services.Abstractions
{
    /// 定义了一个用于在控制台显示系统组件信息服务的接口。
    public interface IDisplayService
    {
        // 显示所有从文件中解析出的组件信息
        void DisplayStructure(SystemComponents components);


        // 显示管网水力计算后的动态结果
        void DisplayResults(SystemComponents components);
    }
}
