namespace Gallop
{
    public interface ICySpringWindParamData
    {
        bool EnableVerticalWind { get; }
        bool EnableHorizontalWind { get; }

        float CenterWindAngleSlow { get; }
        float CenterWindAngleFast { get; }

        float VerticalCycleSlow { get; }
        float HorizontalCycleSlow { get; }

        float VerticalAngleWidthSlow { get; }
        float HorizontalAngleWidthSlow { get; }

        float VerticalCycleFast { get; }
        float HorizontalCycleFast { get; }

        float VerticalAngleWidthFast { get; }
        float HorizontalAngleWidthFast { get; }
    }
}