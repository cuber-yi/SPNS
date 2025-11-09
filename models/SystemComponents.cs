using Project.Models;
using System.Collections.Generic;

namespace Project.Models
{
    // 整个管网系统的根数据容器，包含所有组件的列表。
    // 这个对象在服务之间传递，以代表整个系统的状态。
    public class SystemComponents
    {
        public required SystemParameter SystemParameter { get; set; }
        public List<AirCompressionStation> AirCompressionStations { get; set; } = new();
        public List<UserSide> UserSides { get; set; } = new();
        public List<CompositePipes> CompositePipes { get; set; } = new();
        public List<Reducing> Reducers { get; set; } = new();
        public List<TeeJunction> TeeJunctions { get; set; } = new();
        public List<Valve> Valves { get; set; } = new();
        public List<LimitFlowValve> LimitFlowValves { get; set; } = new();
        public List<LimitDropPValve> LimitDropPValves { get; set; } = new();
        public List<LimitPressureValve> LimitPressureValves { get; set; } = new();
    }
}
