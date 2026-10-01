using System;

namespace Gallop
{
    [Serializable]
    public class SkirtParamDataElement
    {
        public string _rootBoneName;
        public string _childBoneName;

        public bool _isCheckRightLeg;
        public bool _isCheckLeftLeg;

        public bool IsIgnoreRaceRun;

        public SkirtParamDataElement()
        {
        }
    }
}