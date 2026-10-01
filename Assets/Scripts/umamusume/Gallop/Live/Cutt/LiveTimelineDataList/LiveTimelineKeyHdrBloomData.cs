using System;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyHdrBloomData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                
                return (LiveTimelineKeyDataType)39;
            }
        }

        public float intensity;
        public float blurSpread;
        public bool enable;

        public LiveTimelineKeyHdrBloomData()
        {
            intensity = 0.3f;
            blurSpread = 2.5f;
            enable = true;
        }
    }
}