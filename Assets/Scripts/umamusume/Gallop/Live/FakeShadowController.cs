using UnityEngine;

namespace Gallop.Live
{
    // Field spelling/defaults are the shadow000 prefab TypeTree. The authored cross mesh
    // is already enabled; its optional CustomProjector is authored disabled.
    [DisallowMultipleComponent]
    public sealed class FakeShadowController : MonoBehaviour
    {
        [SerializeField] private float _maxAlpha = 1f;
        [SerializeField] private float _shadowPower = 0.75f;
        [SerializeField] private Color _color = Color.black;
        [SerializeField] private Transform _rootTransform;
        [SerializeField] private Transform _locationTransform;
        [SerializeField] private float _crossHeight = 2f;

        public void Bind(Transform root, Transform location)
        {
            _rootTransform = root;
            _locationTransform = location;
        }

        public void AlterLateUpdate(Vector3 groundPosition)
        {
            if (_locationTransform == null) return;
            Vector3 footPosition = _locationTransform.position;
            footPosition.y = groundPosition.y;
            transform.position = footPosition;
            // Native height/alpha evaluation is not present in the viewer binary. Do not
            // invent jump attenuation or rewrite the prefab's shadow material/intensity.
        }
    }
}
