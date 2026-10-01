using System;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineHdrBloomData
        : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "HdrBloom";

        public LiveTimelineKeyHdrBloomDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineHdrBloomData()
            : base(default_name)
        {
            keys = new LiveTimelineKeyHdrBloomDataList();
        }
    }
}