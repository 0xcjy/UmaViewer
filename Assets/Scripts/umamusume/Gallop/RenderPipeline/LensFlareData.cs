using System;
using UnityEngine;

namespace Gallop.RenderPipeline
{
    // This must live in LensFlareData.cs so Unity creates the MonoScript asset
    // used by the serialized ScriptableObject references in stage bundles.
    public class LensFlareData : ScriptableObject
    {
        public int TextureLayout;
        public Texture2D Texture;
        public LensFlareElement[] ElementArray = Array.Empty<LensFlareElement>();
        public bool UseFog;
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
}
