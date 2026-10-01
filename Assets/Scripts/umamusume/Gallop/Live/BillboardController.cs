using UnityEngine;

namespace Gallop.Live
{
    // Serialized component identity and fields match the stage/spotlight prefabs.
    public class BillboardController : MonoBehaviour
    {
        [SerializeField] private Transform _targetCameraTransform;
        [SerializeField] private int _rotationType;
        [SerializeField] private bool _isInversedForward;
        private Transform _transform;
        private bool _isInitialized;

        public Transform TargetCameraTransform { get => _targetCameraTransform; set => _targetCameraTransform = value; }
        public void Initialize(Transform cameraTransform)
        {
            _transform = transform;
            _targetCameraTransform = cameraTransform;
            _isInitialized = true;
        }

        // Native 0x1a404e0; invoked explicitly by the render driver, not Unity LateUpdate.
        public void AlterLateUpdate()
        {
            if (!_isInitialized || !enabled || _targetCameraTransform == null) return;
            Vector3 direction = _targetCameraTransform.position - _transform.position;
            if (direction.sqrMagnitude < 1f) return;
            if (_rotationType == 2)
            {
                _transform.LookAt(_targetCameraTransform);
                return;
            }
            if (_rotationType != 0 && _rotationType != 1) return;
            Vector3 up = _transform.rotation * Vector3.up;
            direction -= up * Vector3.Dot(direction, up);
            if (direction.sqrMagnitude < 1e-10f) return;
            Quaternion rotation = Quaternion.LookRotation(direction, up);
            if (_rotationType == 1) rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            if (Quaternion.Angle(rotation, _transform.rotation) > 0.1f) _transform.rotation = rotation;
            // Native does not read _isInversedForward in any rotation mode.
        }
    }
}
