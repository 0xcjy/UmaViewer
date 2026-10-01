using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.Live
{
    // LightProjection.Initialize 0x1a72310: clone the authored CustomProjector material.
    [DisallowMultipleComponent]
    public sealed class LightProjection : MonoBehaviour
    {
        [SerializeField] private int _projectionType;
        private Material _originalMaterial;
        private Material _runtimeMaterial;
        private CustomProjector _projector;

        public int ProjectionTypeValue => _projectionType;
        public bool IsAnimationProjection => _projectionType == 1;
        public CustomProjector Projector => _projector != null ? _projector : GetComponent<CustomProjector>();

        public bool Initialize()
        {
            if (_runtimeMaterial != null) return true;
            _projector = GetComponent<CustomProjector>();
            if (_projector == null || _projector.Material == null || _projector.Material.shader == null)
                return false;
            _originalMaterial = _projector.Material;
            _runtimeMaterial = new Material(_originalMaterial)
            {
                name = _originalMaterial.name,
                hideFlags = HideFlags.DontSave
            };
            _projector.Material = _runtimeMaterial;
            return true;
        }

        public void Release()
        {
            if (_runtimeMaterial == null) return;
            if (_projector != null) _projector.Material = _originalMaterial;
            CoreUtils.Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
            _originalMaterial = null;
        }

        private void OnDestroy() { Release(); }
    }
}
