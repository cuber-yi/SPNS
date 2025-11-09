
namespace Project.Models
{
    public class LimitPressureValve
    {
        public required string Index { get; set; }
        public required string JunctionsA { get; set; }
        public required string JunctionsB { get; set; }
        public required int FlowDirection { get; set; }
        public required double SetP { get; set; }
        public double Pressure { get; set; }
        public double Flow { get; set; }
    }
}
