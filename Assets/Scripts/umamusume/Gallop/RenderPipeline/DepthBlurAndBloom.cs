namespace Gallop
{
    /// <summary>
    /// Depth of Field / Bloom 相关枚举定义
    /// 该类型本身不负责执行后处理。
    /// </summary>
    public class DepthBlurAndBloom
    {
        public DepthBlurAndBloom()
        {
        }
        /// 景深焦点的计算方式
        public enum DofFocalType
        {
            Transform = 0,
            Position = 1,
            Point = 2
        }
        /// 景深模糊模式
        public enum DofBlur
        {
            Horizon = 0,
            Mixed = 1,
            Disc = 2,
            BallBlur = 3
        }
        /// 景深质量模式
        public enum DofQuality
        {
            OnlyBackground = 1,
            BackgroundAndForeground = 5
        }
        /// Transform 类型焦点所跟踪的角色
        public enum DofTransformTarget
        {
            Character0 = 0,
            Character1 = 1,
            Character2 = 2,
            Character3 = 3,
            Character4 = 4,
            Character5 = 5,
            Character6 = 6,
            Character7 = 7,
            Character8 = 8,
            Character9 = 9,
            Character10 = 10,
            Character11 = 11,
            Character12 = 12,
            Character13 = 13,
            Character14 = 14,
            Character15 = 15,
            Character16 = 16,
            Character17 = 17,
            Character18 = 18
        }
    }
}