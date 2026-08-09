using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    [DisallowMultipleComponent]
    public class CustomProjector : MonoBehaviour
    {
        public float _nearClipPlane = 0.1f;
        public float _farClipPlane = 60f;
        public float _fieldOfView = 30f;
        public float _aspectRatio = 1f;
        public Material _material;

        public Texture ProjectionTexture
        {
            get
            {
                if (_material == null) return null;
                string[] names = { "_MainTex", "_BaseMap", "_ProjectionTex" };
                foreach (string name in names)
                    if (_material.HasProperty(name) && _material.GetTexture(name) != null) return _material.GetTexture(name);
                return null;
            }
        }
    }

    [Serializable]
    public class LensFlareElement
    {
        public int ImageIndex;
        public float Position;
        public float Size = 1f;
        public Color Color = Color.white;
        public bool UseLightColor;
        public bool Rotate;
        public bool Zoom;
        public bool Fade;
    }

    public class LensFlareData : ScriptableObject
    {
        public int TextureLayout;
        public Texture2D Texture;
        public LensFlareElement[] ElementArray = Array.Empty<LensFlareElement>();
        public bool UseFog;
    }

    [DisallowMultipleComponent]
    public class CustomLensFlare : MonoBehaviour
    {
        public LensFlareData Flare;
        public float Brightness = 1f;
        public float FadeSpeed = 1f;
        public Color Color = Color.white;
        public LayerMask IgnoreLayers;
        public bool IsDirectional;

        private LensFlareComponentSRP _component;
        private LensFlareDataSRP _runtimeData;

        private void OnEnable()
        {
            BuildRuntimeFlare();
            ApplyRuntimeParameters();
        }

        private void OnDisable()
        {
            if (_component != null) _component.enabled = false;
        }

        private void OnDestroy()
        {
            if (_runtimeData != null) Destroy(_runtimeData);
        }

        public void ApplyTimeline(bool enabled, Vector3 offset, Color color, float brightness, float fadeSpeed)
        {
            if (_component == null || _runtimeData == null) BuildRuntimeFlare();
            transform.localPosition = offset;
            Color = color;
            Brightness = Mathf.Max(0f, brightness);
            FadeSpeed = Mathf.Max(0f, fadeSpeed);
            ApplyRuntimeParameters();
            if (_component != null) _component.enabled = enabled;
        }

        private void BuildRuntimeFlare()
        {
            if (Flare == null) return;
            _component = GetComponent<LensFlareComponentSRP>();
            if (_component == null) _component = gameObject.AddComponent<LensFlareComponentSRP>();

            if (_runtimeData != null) Destroy(_runtimeData);
            _runtimeData = ScriptableObject.CreateInstance<LensFlareDataSRP>();

            var source = Flare.ElementArray ?? Array.Empty<LensFlareElement>();
            var elements = new LensFlareDataElementSRP[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                LensFlareElement item = source[i] ?? new LensFlareElement();
                var element = new LensFlareDataElementSRP
                {
                    flareType = SRPLensFlareType.Image,
                    lensFlareTexture = Flare.Texture,
                    position = item.Position,
                    uniformScale = Mathf.Max(0.001f, item.Size),
                    sizeXY = Vector2.one,
                    tint = item.Color,
                    autoRotate = item.Rotate,
                    modulateByLightColor = item.UseLightColor,
                    blendMode = SRPLensFlareBlendMode.Additive,
                    preserveAspectRatio = true
                };
                elements[i] = element;
            }

            _runtimeData.elements = elements;
            _component.lensFlareData = _runtimeData;
            _component.useOcclusion = true;
            _component.allowOffScreen = false;
            _component.attenuationByLightShape = false;
        }

        private void ApplyRuntimeParameters()
        {
            if (_component == null || _runtimeData == null) return;
            _component.intensity = Mathf.Max(0f, Brightness);
            for (int i = 0; i < _runtimeData.elements.Length; i++)
            {
                LensFlareElement source = Flare != null && Flare.ElementArray != null && i < Flare.ElementArray.Length
                    ? Flare.ElementArray[i]
                    : null;
                Color sourceColor = source != null ? source.Color : UnityEngine.Color.white;
                _runtimeData.elements[i].tint = sourceColor * Color;
            }
        }
    }
}
