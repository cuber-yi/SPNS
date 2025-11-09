
namespace Project.Models
{
    public class Valve
    {
        public required string Index { get; set; }
        public required string JunctionsA { get; set; }
        public required string JunctionsB { get; set; }
        public required int FlowDirection { get; set; }
        public required int ValveType { get; set; }
        public required double ValveMeasure { get; set; } // ¿ª¶È
        public required double Diameter { get; set; }
        public double Pressure { get; set; }
        public double Flow { get; set; }
    }
}
