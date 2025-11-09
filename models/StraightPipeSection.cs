
namespace Project.Models
{
    // 代表复合管道中的一个直管段

    public class StraightPipeSection
    {
        // 直管段的长度 (单位: 米)
        public required double Length { get; set; }

        // 直管段的内径 (单位: 米)
        public required double Diameter { get; set; }

        // 管道的绝对粗糙度 (单位: 毫米)。
        public required double Roughness { get; set; }
    }
}


