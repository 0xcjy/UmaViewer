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
        private float _power;
        private Cutt.LiveTimelineDefine.LightBlendMode _blendMode = Cutt.LiveTimelineDefine.LightBlendMode.Addition;
        // La sombra solo se dibuja cuando el foco esta encendido (no en el arranque de la rampa):
        // en los fogonazos el exceso de luz la tapa y el juego no la muestra. Con 0,5 las claves de
        // intensidad 0,5 (FollowSpotCenter/Left) siguen mostrando sombra y la rampa 0->1 de f1175 no.
        private const float ShadowPowerThreshold = 0.5f;

        public void Bind(Transform root, Transform location)
        {
            _rootTransform = root;
            _locationTransform = location;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _authoredScale = transform.localScale;
            ApplyLight(Color.white, 0f, 1f);
        }

        // La pista BgColor1 marca con LightBlendMode.Multiply las ventanas en las que el juego no
        // dibuja el charco de luz sino la sombra del personaje (sistema nativo aparte).
        // Mientras esa sombra no esté portada, el quad no se pinta en ese modo: si se pintara
        // saldría el charco blanco, que no existe en el original.
        // Ver PENDIENTE_SOMBRA_PERSONAJE.md.
        public void SetBlendMode(Cutt.LiveTimelineDefine.LightBlendMode mode)
        {
            _blendMode = mode;
        }

        private bool DrawsLight =>
            _illuminated && _blendMode != Cutt.LiveTimelineDefine.LightBlendMode.Multiply;

        // Verdadero cuando el foco esta encendido de verdad (no en la rampa de subida) y la pista
        // pide LightBlendMode.Multiply: es cuando el juego muestra la sombra del personaje.
        public bool IsShadowMode =>
            _power >= ShadowPowerThreshold && _blendMode == Cutt.LiveTimelineDefine.LightBlendMode.Multiply;

        public void ApplyLight(Color color, float power, float scale)
        {
            _illuminated = power != 0f;
            _power = power;
            transform.localScale = _authoredScale * scale;
            foreach (var target in _renderers)
            {
                target.enabled = DrawsLight;
                target.GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                _block.SetFloat(PowerId, power);
                target.SetPropertyBlock(_block);
            }
        }

        public void SetCharacterVisible(bool visible)
        {
            foreach (var target in _renderers)
                target.enabled = visible && DrawsLight;
        }

        public void AlterLateUpdate(Vector3 groundPosition)
        {
            // The caller supplies the evaluated formation position, not an animated hip/foot.
            // Keep the authored child mesh offset, rotation and scale intact.
            transform.position = groundPosition;
        }
    }
}
