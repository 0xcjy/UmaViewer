using UnityEngine;

namespace Gallop.Live
{
    // Serialized identity and fields come from the authored common spotlight prefab.
    // This is a ground-following mesh, not Spotlight3d or a generated Light cookie.
    [DisallowMultipleComponent]
    public sealed class FollowSpotLightController : MonoBehaviour
    {
        [SerializeField] private Transform _rootTransform;
        [SerializeField] private Transform _locationTransform;
        private static readonly int ColorId = Shader.PropertyToID("_MulColor0");
        private static readonly int PowerId = Shader.PropertyToID("_ColorPower");
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Vector3 _authoredScale;
        private bool _illuminated;

        public void Bind(Transform root, Transform location)
        {
            _rootTransform = root;
            _locationTransform = location;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _authoredScale = transform.localScale;
            ApplyLight(Color.white, 0f, 1f);
        }

        public void ApplyLight(Color color, float power, float scale)
        {
            _illuminated = power != 0f;
            transform.localScale = _authoredScale * scale;
            foreach (var renderer in _renderers)
            {
                renderer.enabled = _illuminated;
                renderer.GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                _block.SetFloat(PowerId, power);
                renderer.SetPropertyBlock(_block);
            }
        }

        public void SetCharacterVisible(bool visible)
        {
            foreach (var renderer in _renderers)
                renderer.enabled = visible && _illuminated;
        }

        public void AlterLateUpdate(Vector3 groundPosition)
        {
            // The caller supplies the evaluated formation position, not an animated hip/foot.
            // Keep the authored child mesh offset, rotation, scale and material intact.
            transform.position = groundPosition;
        }
    }
}
