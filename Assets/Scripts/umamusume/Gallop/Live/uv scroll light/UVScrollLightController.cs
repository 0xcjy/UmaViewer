using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// UV 滚动灯光控制器
    /// 
    ///接收已经展平的 UVScrollLightUpdateInfo；
    ///将颜色、强度、偏移、速度、经过时间和纹理写入材质；
    public class UVScrollLightController
    {
        private readonly Material[] _materialArray;
        private readonly bool _isInstanceMaterial;

        private readonly string _mainTexProperty;
        private readonly string _mulColor0Property;
        private readonly string _mulColor1Property;
        private readonly string _colorPowerProperty;
        private readonly string _scrollOffsetXProperty;
        private readonly string _scrollOffsetYProperty;
        private readonly string _scrollSpeedXProperty;
        private readonly string _scrollSpeedYProperty;
        private readonly string _elapsedTimeProperty;

        private Color _mulColor0;
        private Color _mulColor1;
        private float _colorPower;
        private float _scrollOffsetX;
        private float _scrollOffsetY;
        private float _scrollSpeedX;
        private float _scrollSpeedY;
        private float _elapsedTime;
        private Texture2D _texture;
        private bool _isEnabledTexture;

        public UVScrollLightController()
        {
            _materialArray = Array.Empty<Material>();
            _isInstanceMaterial = false;
        }

        public UVScrollLightController(Renderer[] rendererArray)
        {
            _materialArray = CollectInstanceMaterials(rendererArray);
            _isInstanceMaterial = true;
        }

        public UVScrollLightController(
            Material[] materialArray,
            bool isInstanceMaterial,
            string mainTexProperty,
            string mulColor0Property,
            string mulColor1Property,
            string colorPowerProperty,
            string scrollOffsetXProperty,
            string scrollOffsetYProperty,
            string scrollSpeedXProperty,
            string scrollSpeedYProperty,
            string elapsedTimeProperty)
        {
            _materialArray = materialArray ?? Array.Empty<Material>();
            _isInstanceMaterial = isInstanceMaterial;

            _mainTexProperty = mainTexProperty;
            _mulColor0Property = mulColor0Property;
            _mulColor1Property = mulColor1Property;
            _colorPowerProperty = colorPowerProperty;
            _scrollOffsetXProperty = scrollOffsetXProperty;
            _scrollOffsetYProperty = scrollOffsetYProperty;
            _scrollSpeedXProperty = scrollSpeedXProperty;
            _scrollSpeedYProperty = scrollSpeedYProperty;
            _elapsedTimeProperty = elapsedTimeProperty;
        }

        public void UpdateInfo(ref UVScrollLightUpdateInfo updateInfo)
        {
            _mulColor0 = updateInfo.mulColor0;
            _mulColor1 = updateInfo.mulColor1;
            _colorPower = updateInfo.colorPower;
            _scrollOffsetX = updateInfo.scrollOffsetX;
            _scrollOffsetY = updateInfo.scrollOffsetY;
            _scrollSpeedX = updateInfo.scrollSpeedX;
            _scrollSpeedY = updateInfo.scrollSpeedY;
            _elapsedTime = updateInfo.elapsedTime;
            _texture = updateInfo.texture;
            _isEnabledTexture = updateInfo.isEnabledTexture;
        }

        public void Update()
        {
            if (_materialArray == null || _materialArray.Length == 0)
                return;

            float offsetX = _scrollOffsetX + _scrollSpeedX * _elapsedTime;
            float offsetY = _scrollOffsetY + _scrollSpeedY * _elapsedTime;

            for (int i = 0; i < _materialArray.Length; i++)
            {
                var mat = _materialArray[i];
                if (mat == null)
                    continue;

                if (mat.HasProperty("_MulColor0"))
                    mat.SetColor("_MulColor0", _mulColor0);

                if (mat.HasProperty("_MulColor1"))
                    mat.SetColor("_MulColor1", _mulColor1);

                if (mat.HasProperty("_ColorPower"))
                    mat.SetFloat("_ColorPower", _colorPower);

                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTextureOffset("_MainTex", new Vector2(offsetX, offsetY));
                }

                if (_isEnabledTexture && _texture != null && mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", _texture);
            }
        }

        public void Release()
        {
            if (_isInstanceMaterial && _materialArray != null)
            {
                for (int i = 0; i < _materialArray.Length; i++)
                {
                    Material mat = _materialArray[i];
                    if (mat == null)
                        continue;

#if UNITY_EDITOR
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(mat);
                    else
                        UnityEngine.Object.DestroyImmediate(mat);
#else
                    UnityEngine.Object.Destroy(mat);
#endif
                }
            }
        }

        public static Material[] CollectInstanceMaterials(Renderer[] rendererArray)
        {
            if (rendererArray == null || rendererArray.Length == 0)
                return Array.Empty<Material>();

            List<Material> list = new List<Material>(16);

            for (int i = 0; i < rendererArray.Length; i++)
            {
                Renderer r = rendererArray[i];
                if (r == null)
                    continue;

                Material[] mats;
                try
                {
                    mats = r.materials;
                }
                catch
                {
                    continue;
                }

                if (mats == null || mats.Length == 0)
                    continue;

                for (int j = 0; j < mats.Length; j++)
                {
                    if (mats[j] != null)
                        list.Add(mats[j]);
                }
            }

            return list.ToArray();
        }
    }
}
