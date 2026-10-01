namespace Gallop.Live.Cutt
{
     public delegate void HdrBloomUpdateInfoDelegate(ref HdrBloomUpdateInfo updateInfo);
    public struct HdrBloomUpdateInfo
    {
        // 0x00：LiveTimelineHdrBloomData 引用
        public LiveTimelineHdrBloomData data;

        // 0x08
        public float intensity;

        // 0x0C
        public float blurSpread;

        // 0x10
        public bool enable;
    }
}