using System;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class ILiveTimelineGroupDataWithName : ILiveTimelineGroupData
    {
        public string name;

        private int _nameHash;

        public int nameHash
        {
            get
            {
                return _nameHash;
            }
        }

        public override void UpdateStatus()
        {
            _nameHash = FNVHash.Generate(name);
        }

        public ILiveTimelineGroupDataWithName()
        {
            name = string.Empty;
            _nameHash = 0;
        }

        public ILiveTimelineGroupDataWithName(string defaultName)
        {
            name = defaultName;
            _nameHash = FNVHash.Generate(name);
        }

        public void SetName(string timelineName)
        {
            name = timelineName;
            _nameHash = FNVHash.Generate(name);
        }
    }
}