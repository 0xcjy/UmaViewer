using UnityEngine;

namespace Gallop.Cyalume
{
    [AddComponentMenu("")]
    public class MobShadowController : MonoBehaviour
    {
        private static readonly int MobGroupMatrixPropertyId = Shader.PropertyToID("_MobGroupMatrix");

        [SerializeField] private string _mobColorPropertyName = "_MobColor";
        [SerializeField] private string _maskPosArrayPropertyName = "_MaskPosArray";
        [SerializeField] private string _groupMatrixArrayPropertyName = "_MobGroupMatrix";

        private GameObject _targetObject;
        private Transform _targetObjectTransform;
        private Vector3 _targetObjectInitPosition;
        private bool _isInitialized;
        private Renderer[] _renderers;
        private Color _mobColor;
        private Color _ambientColor;
        private int _mobColorPropertyId;
        private int _maskPosArrayPropertyId;
        private int _groupMatrixArrayPropertyId;
        private Vector4[] _maskPosArray;
        private Matrix4x4[] _groupMatrixArray;
        private bool _isUpdateGroupMatrix;

        public Vector4[] MaskPosArray
        {
            set
            {
                _maskPosArray = value;
            }
        }

        public MobShadowController()
        {
            _mobColor = Color.white;
            _ambientColor = Color.white;
            _mobColorPropertyId = -1;
            _maskPosArrayPropertyId = -1;
            _groupMatrixArrayPropertyId = -1;
            _groupMatrixArray = new Matrix4x4[11];
        }

        public void Initialize(GameObject targetObject)
        {
            _mobColorPropertyId = string.IsNullOrEmpty(_mobColorPropertyName) ? -1 : Shader.PropertyToID(_mobColorPropertyName);
            _maskPosArrayPropertyId = string.IsNullOrEmpty(_maskPosArrayPropertyName) ? -1 : Shader.PropertyToID(_maskPosArrayPropertyName);
            _groupMatrixArrayPropertyId = string.IsNullOrEmpty(_groupMatrixArrayPropertyName) ? -1 : Shader.PropertyToID(_groupMatrixArrayPropertyName);

            _targetObject = targetObject;
            if (_targetObject == null)
                return;

            _targetObjectTransform = _targetObject.transform;
            if (_targetObjectTransform == null)
                return;

            _targetObjectInitPosition = _targetObjectTransform.position;
            _renderers = _targetObject.GetComponentsInChildren<Renderer>();

            if (_groupMatrixArray == null)
                _groupMatrixArray = new Matrix4x4[11];

            for (int i = 0; i < _groupMatrixArray.Length; i++)
            {
                _groupMatrixArray[i] = Matrix4x4.identity;
            }

            _isUpdateGroupMatrix = true;
            _isInitialized = true;
        }

        public void SetMobColor(Color color)
        {
            _mobColor = color;
            _UpdateShaderParamColor();
        }

        public void SetAmbientColor(Color color)
        {
            _ambientColor = color;
            _UpdateShaderParamColor();
        }

        public void SetGroupTRS(int index, ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            if (!_isInitialized)
                return;

            if (_groupMatrixArray == null)
                return;

            if ((uint)index >= (uint)_groupMatrixArray.Length)
                return;

            _groupMatrixArray[index].SetTRS(position, rotation, scale);
            _isUpdateGroupMatrix = true;
        }

        public void FlushGroupMatrix()
        {
            if (!_isInitialized || !_isUpdateGroupMatrix || _groupMatrixArray == null)
                return;

            _isUpdateGroupMatrix = false;

            int propertyId = _groupMatrixArrayPropertyId >= 0
                ? _groupMatrixArrayPropertyId
                : MobGroupMatrixPropertyId;

            Shader.SetGlobalMatrixArray(propertyId, _groupMatrixArray);
        }

        private void _UpdateShaderParamColor()
        {
            if (_renderers == null)
                return;

            Color finalColor = new Color(
                _ambientColor.r * _mobColor.r,
                _ambientColor.g * _mobColor.g,
                _ambientColor.b * _mobColor.b,
                _ambientColor.a * _mobColor.a
            );

            if (_mobColorPropertyId >= 0)
                Shader.SetGlobalColor(_mobColorPropertyId, finalColor);

            if (_maskPosArray != null && _maskPosArrayPropertyId >= 0)
                Shader.SetGlobalVectorArray(_maskPosArrayPropertyId, _maskPosArray);
        }

        private void OnDestroy()
        {
        }
    }
}
