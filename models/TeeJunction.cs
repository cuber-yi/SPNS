
namespace Project.Models
{
    public class TeeJunction
    {
        public required string Index { get; set; }
        public string JunctionsA { get; set; }
        public string JunctionsB { get; set; }
        public string JunctionsC { get; set; }
        public double Pressure { get; set; }
        public double FlowA { get; set; }
        public double FlowB { get; set; }
        public double FlowC { get; set; }
    }
}
