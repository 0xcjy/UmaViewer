using UnityEngine;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Runtime image-effect state attached to a render camera.
    /// The recovered Gallop feature reads this parameter from CameraData.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraData : MonoBehaviour
    {
        [SerializeField]
        private PostImageEffectFeature.Parameter _parameter;

        public PostImageEffectFeature.Parameter Parameter
        {
            get
            {
                if (_parameter == null)
                    _parameter = new PostImageEffectFeature.Parameter();
                return _parameter;
            }
            set => _parameter = value;
        }

        public void Initialize(bool _)
        {
            _ = false;
            if (_parameter == null)
                _parameter = new PostImageEffectFeature.Parameter();
        }

        private void Awake()
        {
            Initialize(false);
        }
    }
}
