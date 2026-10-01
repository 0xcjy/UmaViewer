using UnityEngine;

namespace Gallop.Live.Cutt
{
    public interface ILiveTimelineCharactorLocator
    {
        bool liveCharaVisible { get; set; }
        LiveCharaPosition liveCharaStandingPosition { get; set; }
        Vector3 liveCharaInitialPosition { get; set; }
        Vector3 liveCharaPosition { get; }
        Quaternion liveCharaPositionLocalRotation { get; set; }

        Vector3 liveCharaHeadPosition { get; }
        Vector3 liveCharaWaistPosition { get; }
        Vector3 liveCharaLeftHandWristPosition { get; }
        Vector3 liveCharaLeftHandAttachPosition { get; }
        Vector3 liveCharaRightHandAttachPosition { get; }
        Vector3 liveCharaRightHandWristPosition { get; }
        Vector3 liveCharaChestPosition { get; }
        Vector3 liveCharaFootPosition { get; }

        Vector3 liveCharaConstHeightHeadPosition { get; }
        Vector3 liveCharaConstHeightWaistPosition { get; }
        Vector3 liveCharaConstHeightChestPosition { get; }

        Vector3 liveCharaInitialHeightHeadPosition { get; }
        Vector3 liveCharaInitialHeightWaistPosition { get; }
        Vector3 liveCharaInitialHeightChestPosition { get; }

        Vector3 liveCharaScale { get; }

        Transform liveParentDefaultTransform { get; set; }
        Transform liveParentTransform { get; set; }
        Transform liveRootTransform { get; }

        int liveCharaHeightLevel { get; set; }
        float liveCharaHeightValue { get; }
        float liveCharaHeightRatioBase { get; set; }
        float liveCharaHeightRatio { get; set; }
        Vector3 liveCharaFormationHeightRateOffset { get; set; }

        bool IsPositionNodePositionAddParent { get; set; }
        bool IsCastShadow { get; set; }
        float CySpringRate { get; set; }

        int LayerIndex { set; }
        Color EmissiveColor { set; }

        void SetEmissiveScale(float speed, float energyScale);
        void SetEmissiveIntensityParam(float emissiveIntensity);
        void SetEmissiveSoftEdgeParam(float emissiveRimPower, float emissiveRimIntensity, bool isemissiveCenter);
        void SetScaleFactor(float scale);
    }
}