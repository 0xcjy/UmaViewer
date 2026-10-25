using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Pista "FacialToon" del timeline del live. Controla, por posicion de personaje, los
    /// umbrales del sombreado de la cara (incluida la sombra falsa de la nariz) y los blends
    /// de la iluminacion facial. Los nombres de los campos coinciden con el TypeTree del
    /// bundle del juego (son case-sensitive).
    /// </summary>
    [Serializable]
    public class LiveTimelineKeyFacialToonData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.FacialToon; }
        }

        public float CheekPretenseThreshold;
        public float NosePretenseThreshold;
        public float CylinderBlend;
        public float HairNormalBlend;
        public LiveTimelineDefine.FacialToonLightType UseOriginalDirectionalLight;
        public Vector3 OriginalDirectionalLightDir;
        public float EyeToonStep;
        public float EyeToonFeather;
    }

    [Serializable]
    public class LiveTimelineKeyFacialToonDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFacialToonData>
    {
    }

    /// <summary>
    /// Conjunto de listas de la pista FacialToon: una por posicion de personaje
    /// (center, left1, right1, left2, right2 y motion5..motion19).
    /// </summary>
    [Serializable]
    public class LiveTimelineFacialToonData
    {
        public const int DATA_LIST_SIZE = 20;

        public LiveTimelineKeyFacialToonDataList centerKeys;
        public LiveTimelineKeyFacialToonDataList left1Keys;
        public LiveTimelineKeyFacialToonDataList right1Keys;
        public LiveTimelineKeyFacialToonDataList left2Keys;
        public LiveTimelineKeyFacialToonDataList right2Keys;
        public LiveTimelineKeyFacialToonDataList motion5Keys;
        public LiveTimelineKeyFacialToonDataList motion6Keys;
        public LiveTimelineKeyFacialToonDataList motion7Keys;
        public LiveTimelineKeyFacialToonDataList motion8Keys;
        public LiveTimelineKeyFacialToonDataList motion9Keys;
        public LiveTimelineKeyFacialToonDataList motion10Keys;
        public LiveTimelineKeyFacialToonDataList motion11Keys;
        public LiveTimelineKeyFacialToonDataList motion12Keys;
        public LiveTimelineKeyFacialToonDataList motion13Keys;
        public LiveTimelineKeyFacialToonDataList motion14Keys;
        public LiveTimelineKeyFacialToonDataList motion15Keys;
        public LiveTimelineKeyFacialToonDataList motion16Keys;
        public LiveTimelineKeyFacialToonDataList motion17Keys;
        public LiveTimelineKeyFacialToonDataList motion18Keys;
        public LiveTimelineKeyFacialToonDataList motion19Keys;

        private ILiveTimelineKeyDataList[] _cacheDataList;

        /// <summary>
        /// Devuelve la lista de claves de la posicion <paramref name="index"/> (0..19),
        /// en el mismo orden que el cliente original.
        /// </summary>
        public ILiveTimelineKeyDataList GetKeyList(int index)
        {
            if (_cacheDataList == null)
            {
                _cacheDataList = new ILiveTimelineKeyDataList[DATA_LIST_SIZE];
                _cacheDataList[0] = centerKeys;
                _cacheDataList[1] = left1Keys;
                _cacheDataList[2] = right1Keys;
                _cacheDataList[3] = left2Keys;
                _cacheDataList[4] = right2Keys;
                _cacheDataList[5] = motion5Keys;
                _cacheDataList[6] = motion6Keys;
                _cacheDataList[7] = motion7Keys;
                _cacheDataList[8] = motion8Keys;
                _cacheDataList[9] = motion9Keys;
                _cacheDataList[10] = motion10Keys;
                _cacheDataList[11] = motion11Keys;
                _cacheDataList[12] = motion12Keys;
                _cacheDataList[13] = motion13Keys;
                _cacheDataList[14] = motion14Keys;
                _cacheDataList[15] = motion15Keys;
                _cacheDataList[16] = motion16Keys;
                _cacheDataList[17] = motion17Keys;
                _cacheDataList[18] = motion18Keys;
                _cacheDataList[19] = motion19Keys;
            }

            if (index < 0 || index >= DATA_LIST_SIZE)
            {
                return null;
            }

            return _cacheDataList[index];
        }

        public ILiveTimelineKeyDataList[] GetKeyListArray()
        {
            if (_cacheDataList == null)
            {
                // Fuerza la construccion del array cache.
                GetKeyList(0);
            }

            return _cacheDataList;
        }
    }
}
