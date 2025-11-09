
using Project.Models;
using System.Collections.Generic;

namespace Project.Services.Abstractions
{
    // 定义了所有与JSON文件读写相关的服务契约。
    // 任何实现此接口的类都必须提供这些方法。
    public interface IJsonFileService
    {
        // 从指定的结构文件中解码管网系统数据。
        SystemComponents DecodeStructure(string filePath);

        // 将计算结果序列化为JSON并写入指定文件。
        void GenerateOutputFile(string filePath, SystemComponents components);

        // 读取用户流量变化文件，并更新现有的 SystemComponents 对象。
        SystemComponents ReadUsersFlowChange(string filePath, SystemComponents currentComponents);
    }
}
