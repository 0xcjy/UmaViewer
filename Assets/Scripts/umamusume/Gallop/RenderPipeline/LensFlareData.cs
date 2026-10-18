using System;
using UnityEngine;

namespace Gallop.RenderPipeline
{
    // This must live in LensFlareData.cs so Unity creates the MonoScript asset
    // used by the serialized references in stage bundles.
    //
    // The asset is the in-house copy of Unity's legacy Flare: one texture atlas shared by every
    // element plus the list of elements strung out along the line that runs from the flare object
    // through the screen centre (Position 0 = flare object, 1 = screen centre; values above 1 go
    // past the centre). URP does not render UnityEngine.LensFlare, so CustomLensFlare draws the
    // elements itself and needs to know which slice of the atlas each ImageIndex means.
    public class LensFlareData : ScriptableObject
    {
        // Mirror of Unity's internal FlareLayout enum (UnityCsReference, GraphicsComponents).
        public const int LayoutLargeRestSmall = 0;
        public const int LayoutMixed = 1;
        public const int Layout1x1 = 2;
        public const int Layout2x2 = 3;
        public const int Layout3x3 = 4;
        public const int Layout4x4 = 5;

        public int TextureLayout;
        public Texture2D Texture;
        public LensFlareElement[] ElementArray = Array.Empty<LensFlareElement>();
        public bool UseFog;

        public int ElementCount => ElementArray != null ? ElementArray.Length : 0;
        public bool HasTexture => Texture != null;

        /// <summary>
        /// UV rect of the atlas that <paramref name="imageIndex"/> points at, for the layout of
        /// this asset. UV origin is bottom-left; image indices run left to right / top to bottom,
        /// which is the convention the Flare inspector shows over the texture.
        /// Returns false (and falls back to the whole texture) when the index is outside the
        /// layout, which keeps an out-of-range authored element visible instead of black.
        /// </summary>
        public bool TryGetImageUvRect(int imageIndex, out Rect rect)
        {
            rect = new Rect(0f, 0f, 1f, 1f);
            int index = Mathf.Max(0, imageIndex);

            switch (TextureLayout)
            {
                case LayoutLargeRestSmall:
                    return TryGetLargeRestSmall(index, out rect);

                case LayoutMixed:
                    return TryGetMixed(index, out rect);

                case Layout2x2:
                    return TryGetGrid(index, 2, 2, out rect);

                case Layout3x3:
                    return TryGetGrid(index, 3, 3, out rect);

                case Layout4x4:
                    return TryGetGrid(index, 4, 4, out rect);

                case Layout1x1:
                default:
                    // 1x1 (and unknown layouts): the whole texture is a single image.
                    rect = new Rect(0f, 0f, 1f, 1f);
                    return true;
            }
        }

        // "1 Large 4 Small": image 0 is the top half (twice as high as wide texture); images 1..4
        // are a 2x2 grid inside the bottom half, upper row first.
        private static bool TryGetLargeRestSmall(int index, out Rect rect)
        {
            if (index == 0)
            {
                rect = new Rect(0f, 0.5f, 1f, 0.5f);
                return true;
            }

            int i = index - 1;
            if (i >= 4)
            {
                rect = new Rect(0f, 0.5f, 1f, 0.5f);
                return false;
            }

            int column = i % 2;
            int row = i / 2;
            rect = new Rect(column * 0.5f, row == 0 ? 0.25f : 0f, 0.5f, 0.5f);
            return true;
        }

        // "1 Large 2 Medium 8 Small" (the classic 50mm zoom flare): image 0 is the top half; images
        // 1..2 are the two medium ones down the left edge of the bottom half; images 3..10 are a
        // 2x4 grid in the right half of the bottom half. Best effort: no live stage uses it.
        private static bool TryGetMixed(int index, out Rect rect)
        {
            if (index == 0)
            {
                rect = new Rect(0f, 0.5f, 1f, 0.5f);
                return true;
            }

            if (index <= 2)
            {
                rect = new Rect(0f, index == 1 ? 0.25f : 0f, 0.5f, 0.25f);
                return true;
            }

            int i = index - 3;
            if (i >= 8)
            {
                rect = new Rect(0f, 0f, 1f, 1f);
                return false;
            }

            int column = i % 2;
            int row = i / 2;
            rect = new Rect(0.5f + column * 0.25f, 0.5f - (row + 1) * 0.25f, 0.25f, 0.25f);
            return true;
        }

        private static bool TryGetGrid(int index, int columns, int rows, out Rect rect)
        {
            if (index < 0 || index >= columns * rows)
            {
                rect = new Rect(0f, 0f, 1f, 1f);
                return false;
            }

            int column = index % columns;
            int row = index / columns;
            float width = 1f / columns;
            float height = 1f / rows;
            rect = new Rect(column * width, 1f - (row + 1) * height, width, height);
            return true;
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
}
