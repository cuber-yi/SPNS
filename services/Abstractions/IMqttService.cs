using System;
using System.Threading.Tasks;

namespace Project.Services.Abstractions
{
    // 定义了MQTT通信服务的接口。
    public interface IMqttService
    {
        // 当接收到消息时触发的事件。
        event Func<string, string, Task> MessageReceivedAsync;

        // 当与服务器成功连接时触发的事件。
        event Func<Task> ConnectedAsync;

        // 当与服务器断开连接时触发的事件。
        event Func<Task> DisconnectedAsync;

        // 连接到MQTT代理。
        Task ConnectAsync();

        // 发布消息到指定的主题。
        Task PublishAsync(string topic, string payload, bool retain = false);

        // 订阅一个主题。
        Task SubscribeAsync(string topic);

        // 检查客户端当前是否已连接。
        bool IsConnected { get; }
    }
}
