using System;
using UnityEngine;

namespace Gallop.RenderPipeline
{
    // Serialized field names are the original CustomProjector TypeTree, not UnityEngine.Projector.
    [DisallowMultipleComponent]
    public sealed class CustomProjector : MonoBehaviour
    {
        public float NearClipPlane;
        public float FarClipPlane;
        public float FieldOfView;
        public float AspectRatio;
        public bool OrthoGraphic;
        public float OrthoGraphicSize;
        public LayerMask IgnoreLayer;
        public Material Material;

        public bool OverrideIgnoreLayer { get; set; }
        public LayerMask OverrideLayerMask { get; set; }
        public Action OnPreRender { get; set; }
        public Action OnPostRender { get; set; }
        public bool IsActiveAndEnabled => isActiveAndEnabled;
        public Transform CacheTransform => transform;

        // CalculateMatrixJob.SetupProjectorMatrix 0x1a25790. Native ignores transform scale
        // when building its projector view; scale still remains on the authored Transform.
        public void CalculateMatrices(out Matrix4x4 culling, out Matrix4x4 projector,
            out Matrix4x4 projectorClip)
        {
            Matrix4x4 projection = OrthoGraphic
                ? Matrix4x4.Ortho(-OrthoGraphicSize * AspectRatio, OrthoGraphicSize * AspectRatio,
                    -OrthoGraphicSize, OrthoGraphicSize, NearClipPlane, FarClipPlane)
                : Matrix4x4.Perspective(FieldOfView, AspectRatio, NearClipPlane, FarClipPlane);
            Transform source = transform;
            Matrix4x4 view = Matrix4x4.TRS(source.position, source.rotation, Vector3.one).inverse;
            Matrix4x4 zFlip = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
            culling = projection * zFlip * view;
            projector = Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0f)) * projection * zFlip *
                Matrix4x4.Scale(new Vector3(0.5f, 0.5f, 1f)) * view;
            Matrix4x4 depth = Matrix4x4.identity;
            depth.m00 = 0f;
            depth.m01 = 0f;
            depth.m02 = 1f;
            float inverseRange = 1f / (FarClipPlane - NearClipPlane);
            projectorClip = depth * Matrix4x4.Scale(Vector3.one * inverseRange) *
                Matrix4x4.Translate(Vector3.one * -NearClipPlane) * view;
        }
    }
}
