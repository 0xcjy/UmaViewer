using System.Runtime.InteropServices;

namespace Gallop.Live.Cutt
{
    [StructLayout(LayoutKind.Explicit)]
    public struct WashLightUpdateInfo
    {
        [FieldOffset(0x00)] public int NameHash;
        [FieldOffset(0x04)] public bool IsEnabledRaycast;
        [FieldOffset(0x08)] public float RaycastDistance;
        [FieldOffset(0x0C)] public bool IsAllSettings;
        [FieldOffset(0x10)] public float CameraProjectionSide;
        [FieldOffset(0x14)] public float CameraProjectionColorPower;
    }

    public delegate void WashLightUpdateInfoDelegate(ref WashLightUpdateInfo updateInfo);
}