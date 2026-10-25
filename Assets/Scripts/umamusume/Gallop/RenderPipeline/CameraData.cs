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

        public void UpdateImageEffectParameter()
        {
            // Native RVA 0x1a0da30; recomputed again before each render.
            ref var dof = ref Parameter.DofDiffuionBloomOverlay;
            dof.IsDisableDofTemporary = dof.IsEnableDofAutoDisable &&
                dof.DofFocalSize >= dof.DofMaxFocalSize;
            // Per-effect switch of the live settings panel (Bloom/Diffusion/PostFilm are masked
            // inside DecideDrawType, which reads this parameter's own flags).
            dof.DecideDrawType(
                dof.IsEnableDof && !dof.IsDisableDofTemporary && GallopImageEffect.UserDofEnabled &&
                Gallop.RenderPipeline.PostEffectSelection.IsEnabled(Gallop.RenderPipeline.PostEffectId.Dof));
        }

        private void Awake()
        {
            Initialize(false);
        }
    }
}
