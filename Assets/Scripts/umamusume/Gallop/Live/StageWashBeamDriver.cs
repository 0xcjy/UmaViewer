using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.Live
{
    // master5 StageController.BuildWashLightBeamVolumes / WashBeamAxialBillboard:
    // one original-mesh sibling billboard, not radial slices or screen-pixel scaling.
    public sealed class StageWashBeamDriver : MonoBehaviour
    {
        private struct Beam
        {
            public MeshRenderer Source, Renderer;
            public Vector3 Axis, Normal;
            public bool SourceWasForcedOff;
        }
        private readonly List<Beam> _beams = new List<Beam>();
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize()
        {
            ReleaseBeams();
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.IndexOf("_billboard_ground_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var material = renderer.sharedMaterial;
                if (material == null || material.shader == null ||
                    material.shader.name.IndexOf("/LightBlink", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool wash = false;
                for (var parent = renderer.transform.parent; parent != null; parent = parent.parent)
                    if (parent.name.IndexOf("blinklight_wash_", StringComparison.OrdinalIgnoreCase) >= 0) { wash = true; break; }
                if (!wash) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.sharedMaterials.Length != 1) continue;
                Vector3 size = filter.sharedMesh.bounds.size;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float thinnest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (longest <= 0f || thinnest / longest >= .02f) continue;
                var sourceTransform = renderer.transform;
                var copyObject = new GameObject(renderer.name + "__axial_billboard");
                copyObject.layer = renderer.gameObject.layer;
                copyObject.transform.SetParent(sourceTransform.parent, false);
                copyObject.transform.localPosition = sourceTransform.localPosition;
                copyObject.transform.localRotation = sourceTransform.localRotation;
                copyObject.transform.localScale = sourceTransform.localScale;
                copyObject.SetActive(sourceTransform.gameObject.activeSelf);
                copyObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copyRenderer = copyObject.AddComponent<MeshRenderer>();
                CopyRendererState(renderer, copyRenderer);
                var beam = new Beam {
                    Source = renderer,
                    Renderer = copyRenderer,
                    Axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward,
                    Normal = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward,
                    SourceWasForcedOff = renderer.forceRenderingOff
                };
                _beams.Add(beam);
                // Keep enabled/active/TRS under the authored timeline's ownership.
                // Only the duplicate draw is suppressed; Apply forwards its state.
                renderer.forceRenderingOff = true;
            }
        }

        // Explicitly called after timeline camera/parent pose updates, including paused seeks.
        public void Apply(Camera camera)
        {
            if (camera == null || !isActiveAndEnabled) return;
            foreach (var beam in _beams)
            {
                var source = beam.Source;
                var copy = beam.Renderer;
                if (source == null || copy == null) continue;
                source.forceRenderingOff = true;
                var node = source.transform;
                var billboard = copy.transform;
                if (billboard.parent != node.parent) billboard.SetParent(node.parent, false);
                billboard.localPosition = node.localPosition;
                billboard.localScale = node.localScale;
                Quaternion baseRotation = node.localRotation;
                billboard.localRotation = baseRotation;
                copy.gameObject.layer = source.gameObject.layer;
                copy.gameObject.SetActive(node.gameObject.activeSelf);
                copy.enabled = source.enabled;
                copy.forceRenderingOff = beam.SourceWasForcedOff;
                if (!node.gameObject.activeInHierarchy || !source.enabled) continue;
                if (copy.sharedMaterial != source.sharedMaterial) copy.sharedMaterial = source.sharedMaterial;
                source.GetPropertyBlock(_propertyBlock);
                copy.SetPropertyBlock(_propertyBlock.isEmpty ? null : _propertyBlock);
                source.GetPropertyBlock(_propertyBlock, 0);
                copy.SetPropertyBlock(_propertyBlock.isEmpty ? null : _propertyBlock, 0);
                // Source nodes never rotate: nested authored billboards therefore do
                // not inherit a camera-dependent rotation from another billboard.
                Vector3 axis = node.parent != null
                    ? node.parent.TransformDirection(baseRotation * beam.Axis).normalized
                    : (baseRotation * beam.Axis).normalized;
                Vector3 normal = node.parent != null
                    ? node.parent.TransformDirection(baseRotation * beam.Normal)
                    : baseRotation * beam.Normal;
                normal = Vector3.ProjectOnPlane(normal, axis);
                Vector3 view = Vector3.ProjectOnPlane(camera.transform.position - node.position, axis);
                if (view.sqrMagnitude < 1e-8f || normal.sqrMagnitude < 1e-8f) continue;
                billboard.localRotation = baseRotation * Quaternion.AngleAxis(Vector3.SignedAngle(normal, view, axis), beam.Axis);
            }
        }

        private void CopyRendererState(MeshRenderer source, MeshRenderer target)
        {
            target.sharedMaterials = source.sharedMaterials;
            target.enabled = source.enabled;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.renderingLayerMask = source.renderingLayerMask;
            target.rendererPriority = source.rendererPriority;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            source.GetPropertyBlock(_propertyBlock);
            target.SetPropertyBlock(_propertyBlock.isEmpty ? null : _propertyBlock);
            source.GetPropertyBlock(_propertyBlock, 0);
            target.SetPropertyBlock(_propertyBlock.isEmpty ? null : _propertyBlock, 0);
        }

        private void ReleaseBeams()
        {
            foreach (var beam in _beams)
            {
                if (beam.Source != null) beam.Source.forceRenderingOff = beam.SourceWasForcedOff;
                if (beam.Renderer == null) continue;
                // Destroy is deferred in play mode. Detach before another discovery
                // pass so that the previous proxies cannot be registered as sources.
                beam.Renderer.enabled = false;
                beam.Renderer.transform.SetParent(null, false);
                Destroy(beam.Renderer.gameObject);
            }
            _beams.Clear();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ApplyForCamera;
            foreach (var beam in _beams)
            {
                if (beam.Source != null) beam.Source.forceRenderingOff = beam.SourceWasForcedOff;
                if (beam.Renderer != null) beam.Renderer.enabled = false;
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += ApplyForCamera;
            foreach (var beam in _beams)
                if (beam.Source != null) beam.Source.forceRenderingOff = true;
        }

        private void ApplyForCamera(ScriptableRenderContext context, Camera camera)
        {
            Apply(camera);
        }

        private void OnDestroy()
        {
            ReleaseBeams();
        }
    }
}
