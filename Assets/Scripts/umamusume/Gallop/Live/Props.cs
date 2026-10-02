using System;
using UnityEngine;

namespace Gallop.Live
{
    // Serialized contract from common prop000/001 TypeTrees (assembly: umamusume).
    // Height adjustment follows the actual game Props.SetScale (Komoe RVA 0x71daed0).
    public sealed class Props : MonoBehaviour
    {
        [Serializable]
        public sealed class AdjustmentData
        {
            public Transform Transform;
            public Vector3 OffsetRange;
            public Vector3 TransformRate;
            public int TargetNode;
            public Vector3 TargetOffset;

            public Vector3 TargetPosition { get; internal set; }
        }

        [SerializeField] private AdjustmentData[] _adjustmentDataArray;
        [SerializeField] private bool _isInfluenceOfCharaHeight;
        [SerializeField] private GameObject[] _blinkLightRootObjectArray;
        [SerializeField] private Transform[] _stageObjectTransformArray;

        internal GameObject[] BlinkLightRootObjectArray => _blinkLightRootObjectArray;
        internal Transform[] StageObjectTransformArray => _stageObjectTransformArray;

        internal void SetScale(Transform headTransform, Transform positionNode, float bodyScale,
            bool isInfluenceOfCharaHeight)
        {
            _isInfluenceOfCharaHeight = isInfluenceOfCharaHeight;
            if (_adjustmentDataArray == null || _adjustmentDataArray.Length == 0) return;

            // Native Head target: world-space Y difference only. Neither the authored
            // local position nor head X/Z participates, and the result is not clamped.
            Vector3 targetPosition = new Vector3(0f, headTransform.position.y - positionNode.position.y, 0f);
            for (int i = 0; i < _adjustmentDataArray.Length; i++)
            {
                var adjustment = _adjustmentDataArray[i];
                if (adjustment.Transform == null) continue;
                adjustment.TargetPosition = targetPosition;
                Vector3 position = _isInfluenceOfCharaHeight ? targetPosition / bodyScale : targetPosition;
                adjustment.Transform.localPosition = Vector3.Scale(
                    position + adjustment.TargetOffset - adjustment.OffsetRange, adjustment.TransformRate);
            }
        }
    }
}
