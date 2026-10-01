using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    public class SkirtParamDataAsset : ScriptableObject
    {
        public bool _isLongSkirt;

        public string _centerBoneName;
        public string _kneeLBoneName;
        public string _kneeRBoneName;
        public string _ankleLBoneName;
        public string _ankleRBoneName;

        public float _kneeColliderRadius;
        public float _ankleColliderRadius;
        public float _influenceAngle;
        public float _influenceMaxAngle;

        public bool IsOffsetAngleTargetEndNode;
        public bool IsScaleCollision;

        public List<SkirtParamDataOverrideCollision> OverrideCollisionList;
        public List<SkirtParamDataElement> _skirtBone;

        public SkirtParamDataOverrideCollision GetOverrideData(int charaId)
        {
            if (OverrideCollisionList == null)
                return null;

            for (int i = 0; i < OverrideCollisionList.Count; i++)
            {
                SkirtParamDataOverrideCollision data = OverrideCollisionList[i];

                if (data == null)
                    continue;

                if (data.CharaId == charaId)
                    return data;
            }

            return null;
        }

        public SkirtParamDataAsset()
        {
        }
    }
}