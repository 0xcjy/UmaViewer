using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    [CreateAssetMenu(
        fileName = "CySpringParamDataAsset",
        menuName = "Gallop/CySpring/CySpring Param Data Asset"
    )]
    public class CySpringParamDataAsset : ScriptableObject, ICySpringWindParamData
    {
        public bool _enableVerticalWind;
        public bool _enableHorizontalWind;

        public float _centerWindAngleSlow;
        public float _centerWindAngleFast;

        public float _verticalCycleSlow = 1f;
        public float _horizontalCycleSlow = 1f;

        public float _verticalAngleWidthSlow;
        public float _horizontalAngleWidthSlow;

        public float _verticalCycleFast = 1f;
        public float _horizontalCycleFast = 1f;

        public float _verticalAngleWidthFast;
        public float _horizontalAngleWidthFast;

        public bool IsEnableHipMoveParam;
        public float HipMoveInfluenceDistance;
        public float HipMoveInfluenceMaxDistance;
        public bool UseCorrectScaleCalc;

        public List<CySpringParamDataElement> _elements = new List<CySpringParamDataElement>();
        public List<ConnectedBoneData> ConnectedBoneList = new List<ConnectedBoneData>();

        public bool EnableVerticalWind
        {
            get { return _enableVerticalWind; }
        }

        public bool EnableHorizontalWind
        {
            get { return _enableHorizontalWind; }
        }

        public float CenterWindAngleSlow
        {
            get { return _centerWindAngleSlow; }
        }

        public float CenterWindAngleFast
        {
            get { return _centerWindAngleFast; }
        }

        public float VerticalCycleSlow
        {
            get { return _verticalCycleSlow; }
        }

        public float HorizontalCycleSlow
        {
            get { return _horizontalCycleSlow; }
        }

        public float VerticalAngleWidthSlow
        {
            get { return _verticalAngleWidthSlow; }
        }

        public float HorizontalAngleWidthSlow
        {
            get { return _horizontalAngleWidthSlow; }
        }

        public float VerticalCycleFast
        {
            get { return _verticalCycleFast; }
        }

        public float HorizontalCycleFast
        {
            get { return _horizontalCycleFast; }
        }

        public float VerticalAngleWidthFast
        {
            get { return _verticalAngleWidthFast; }
        }

        public float HorizontalAngleWidthFast
        {
            get { return _horizontalAngleWidthFast; }
        }

        public void SetWindParam(CySpringDataContainer container)
        {
            if (container == null)
                return;

            _enableVerticalWind = container.enableVerticalWind;
            _enableHorizontalWind = container.enableHorizontalWind;

            _centerWindAngleSlow = container.centerWindAngleSlow;
            _centerWindAngleFast = container.centerWindAngleFast;

            _verticalCycleSlow = container.verticalCycleSlow;
            _horizontalCycleSlow = container.horizontalCycleSlow;

            _verticalAngleWidthSlow = container.verticalAngleWidthSlow;
            _horizontalAngleWidthSlow = container.horizontalAngleWidthSlow;

            _verticalCycleFast = container.verticalCycleFast;
            _horizontalCycleFast = container.horizontalCycleFast;

            _verticalAngleWidthFast = container.verticalAngleWidthFast;
            _horizontalAngleWidthFast = container.horizontalAngleWidthFast;
        }

        public void SetMoveSpringParam(CySpringDataContainer container)
        {
            if (container == null)
                return;

            IsEnableHipMoveParam = container.IsEnableHipMoveParam;
            HipMoveInfluenceDistance = container.HipMoveInfluenceDistance;
            HipMoveInfluenceMaxDistance = container.HipMoveInfluenceMaxDistance;

            // 只有你的 CySpringParamDataAsset 里真的有这个字段才保留
            UseCorrectScaleCalc = container.UseCorrectScaleCalc;
        }

        public void SetConnectedBoneParam(CySpringDataContainer container)
        {
            if (container == null || container.ConnectedBoneList == null)
            {
                ConnectedBoneList = new List<ConnectedBoneData>();
                return;
            }

            ConnectedBoneList = new List<ConnectedBoneData>(container.ConnectedBoneList);
        }

        public List<string> GetCollisionNameList()
        {
            return GetCollisionNameList(_elements);
        }

        public static List<string> GetCollisionNameList(List<CySpringParamDataElement> elements)
        {
            List<string> result = new List<string>();

            if (elements == null)
                return result;

            HashSet<string> set = new HashSet<string>();

            for (int i = 0; i < elements.Count; i++)
            {
                CySpringParamDataElement element = elements[i];
                if (element == null)
                    continue;

                AddCollisionNames(set, element.CollisionNameList);

                List<CySpringParamDataChildElement> childList = element.ChildElementList;
                if (childList == null)
                    continue;

                for (int j = 0; j < childList.Count; j++)
                {
                    CySpringParamDataChildElement child = childList[j];
                    if (child == null)
                        continue;

                    AddCollisionNames(set, child.CollisionNameList);
                }
            }

            result.AddRange(set);
            return result;
        }

        private static void AddCollisionNames(HashSet<string> set, List<string> names)
        {
            if (set == null || names == null)
                return;

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];

                if (string.IsNullOrEmpty(name))
                    continue;

                if (!set.Contains(name))
                    set.Add(name);
            }
        }

        public CySpringParamDataAsset()
        {
            _elements = new List<CySpringParamDataElement>();
            ConnectedBoneList = new List<ConnectedBoneData>();

            _verticalCycleSlow = 1f;
            _horizontalCycleSlow = 1f;
            _verticalCycleFast = 1f;
            _horizontalCycleFast = 1f;
        }

        private void OnValidate()
        {
            if (_elements == null)
                _elements = new List<CySpringParamDataElement>();

            if (ConnectedBoneList == null)
                ConnectedBoneList = new List<ConnectedBoneData>();

            if (_verticalCycleSlow <= 0f)
                _verticalCycleSlow = 1f;

            if (_horizontalCycleSlow <= 0f)
                _horizontalCycleSlow = 1f;

            if (_verticalCycleFast <= 0f)
                _verticalCycleFast = 1f;

            if (_horizontalCycleFast <= 0f)
                _horizontalCycleFast = 1f;
        }
    }
}
