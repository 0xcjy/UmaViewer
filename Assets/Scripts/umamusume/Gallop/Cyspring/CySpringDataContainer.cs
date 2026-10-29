using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    [Serializable]
    public class CySpringDataContainer : MonoBehaviour
    {
        public const int CATEGORY_NUM = 5;

        public List<CySpringCollisionData> collisionParam = new List<CySpringCollisionData>();
        public List<CySpringParamDataElement> springParam = new List<CySpringParamDataElement>();
        public List<ConnectedBoneData> ConnectedBoneList = new List<ConnectedBoneData>();

        public bool enableVerticalWind;
        public bool enableHorizontalWind;

        public float centerWindAngleSlow;
        public float centerWindAngleFast;

        public float verticalCycleSlow = 1f;
        public float horizontalCycleSlow = 1f;

        public float verticalAngleWidthSlow;
        public float horizontalAngleWidthSlow;

        public float verticalCycleFast = 1f;
        public float horizontalCycleFast = 1f;

        public float verticalAngleWidthFast;
        public float horizontalAngleWidthFast;

        public bool IsEnableHipMoveParam;
        public float HipMoveInfluenceDistance;
        public float HipMoveInfluenceMaxDistance;

        public bool UseCorrectScaleCalc;

        public void SetWindParam(CySpringParamDataAsset asset)
        {
            if (asset == null)
                return;

            enableVerticalWind = asset.EnableVerticalWind;
            enableHorizontalWind = asset.EnableHorizontalWind;

            centerWindAngleSlow = asset.CenterWindAngleSlow;
            centerWindAngleFast = asset.CenterWindAngleFast;

            verticalCycleSlow = asset.VerticalCycleSlow;
            horizontalCycleSlow = asset.HorizontalCycleSlow;

            verticalAngleWidthSlow = asset.VerticalAngleWidthSlow;
            horizontalAngleWidthSlow = asset.HorizontalAngleWidthSlow;

            verticalCycleFast = asset.VerticalCycleFast;
            horizontalCycleFast = asset.HorizontalCycleFast;

            verticalAngleWidthFast = asset.VerticalAngleWidthFast;
            horizontalAngleWidthFast = asset.HorizontalAngleWidthFast;
        }

        public void SetMoveSpringParam(CySpringParamDataAsset asset)
        {
            if (asset == null)
                return;

            IsEnableHipMoveParam = asset.IsEnableHipMoveParam;
            HipMoveInfluenceDistance = asset.HipMoveInfluenceDistance;
            HipMoveInfluenceMaxDistance = asset.HipMoveInfluenceMaxDistance;
            UseCorrectScaleCalc = asset.UseCorrectScaleCalc;
        }

        public void SetConnectedBonePara(CySpringParamDataAsset asset)
        {
            if (asset == null || asset.ConnectedBoneList == null)
            {
                ConnectedBoneList = new List<ConnectedBoneData>();
                return;
            }

            ConnectedBoneList = new List<ConnectedBoneData>(asset.ConnectedBoneList);
        }

        public CySpringDataContainer()
        {
            collisionParam = new List<CySpringCollisionData>();
            springParam = new List<CySpringParamDataElement>();
            ConnectedBoneList = new List<ConnectedBoneData>();

            verticalCycleSlow = 1f;
            horizontalCycleSlow = 1f;
            verticalCycleFast = 1f;
            horizontalCycleFast = 1f;
        }

        private void OnValidate()
        {
            if (collisionParam == null)
                collisionParam = new List<CySpringCollisionData>();

            if (springParam == null)
                springParam = new List<CySpringParamDataElement>();

            if (ConnectedBoneList == null)
                ConnectedBoneList = new List<ConnectedBoneData>();

            if (verticalCycleSlow <= 0f)
                verticalCycleSlow = 1f;

            if (horizontalCycleSlow <= 0f)
                horizontalCycleSlow = 1f;

            if (verticalCycleFast <= 0f)
                verticalCycleFast = 1f;

            if (horizontalCycleFast <= 0f)
                horizontalCycleFast = 1f;
        }

        public enum Category
        {
            Invalid = -1,
            Live = 0,
            Race = 1,
            Story = 2,
            Home = 3,
            Training = 4
        }
    }
}