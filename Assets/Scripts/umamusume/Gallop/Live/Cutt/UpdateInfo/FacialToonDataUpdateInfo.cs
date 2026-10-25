using UnityEngine;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Valores muestreados de la pista FacialToon para una posicion de personaje.
    /// </summary>
    public struct FacialToonDataUpdateInfo
    {
        public int CharacterPos;
        public float CheekPretenseThreshold;
        public float NosePretenseThreshold;
        public float CylinderBlend;
        public float HairNormalBlend;
        public LiveTimelineDefine.FacialToonLightType UseOriginalDirectionalLight;
        public Vector3 OriginalDirectionalLightDir;
        public float EyeToonStep;
        public float EyeToonFeather;
    }

    public delegate void FacialToonUpdateInfoDelegate(ref FacialToonDataUpdateInfo updateInfo);
}
