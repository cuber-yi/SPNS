
namespace Project.Models
{
    // 代表复合管道中的一类相同规格的弯头。
    public class BenderSection
    {
        // 此类规格的弯头数量。
        public required int Quantity { get; set; }

        // 弯头的角度 (单位: 度)。
        public required double AngleDegrees { get; set; }

        // 弯头的曲率半径与管道直径之比 (R/D)。
        public required double RadiusToDiameterRatio { get; set; }
    }
}
