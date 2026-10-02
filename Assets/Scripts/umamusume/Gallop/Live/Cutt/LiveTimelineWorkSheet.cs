using System;
using System.Collections.Generic;
using UnityEngine;
using static Gallop.Live.Cutt.LiveTimelineDefine;

namespace Gallop.Live.Cutt
{
    [Flags]
    public enum LiveTimelineKeyAttribute
    {
        Disable = 1,
        CameraDelayEnable = 2,
        CameraDelayInherit = 4,
        KeyCommonBitMask = 32768,
        kAttrCheek = 65536,
        kAttrTeary = 131072,
        kAttrTearful = 262144,
        kAttrTeardrop = 524288,
        kAttrMangame = 2097152,
        kAttrFaceShadow = 4194304,
        kAttrFaceShadowVisible = 8388608,
}

    public enum LiveTimelineKeyDataListAttr
    {
        Disable = 1
    }

    public enum TimelineKeyPlayMode
    {
        Always = 0,
        LightOnly = 1,
        DefaultOver = 2
    }

    public enum LiveCameraInterpolateType
    {
        None = 0,
        Linear = 1,
        Curve = 2,
        Ease = 3
    }

    public enum LiveCameraPositionType
    {
        Direct = 0,
        Character = 1
    }

    [Flags]
    public enum LiveCharaPositionFlag
    {
        Place01 = 1,
        Place02 = 2,
        Place03 = 4,
        Place04 = 8,
        Place05 = 16,
        Place06 = 32,
        Place07 = 64,
        Place08 = 128,
        Place09 = 256,
        Place10 = 512,
        Place11 = 1024,
        Place12 = 2048,
        Place13 = 4096,
        Place14 = 8192,
        Place15 = 16384,
        Place16 = 32768,
        Place17 = 65536,
        Place18 = 131072,
        Center = 1,
        Left = 2,
        Right = 4,
        Side = 6,
        Back = 262136,
        Other = 262142,
        All = 262143
    }

    public enum LiveCameraCharaParts
    {
        Face = 0,
        Waist = 1,
        LeftHandWrist = 2,
        RightHandAttach = 3,
        Chest = 4,
        Foot = 5,
        InitFaceHeight = 6,
        InitWaistHeight = 7,
        InitChestHeight = 8,
        RightHandWrist = 9,
        LeftHandAttach = 10,
        ConstFaceHeight = 11,
        ConstChestHeight = 12,
        ConstWaistHeight = 13,
        ConstFootHeight = 14,
        Position = 15,
        PositionWithoutOffset = 16,
        InitialHeightFace = 17,
        InitialHeightChest = 18,
        InitialHeightWaist = 19,
        Max = 20
    }

    public enum LiveCameraCullingLayer
    {
        None = 0,
        TransparentFX = 1,
        Background3d_NotReflect = 2,
        Background3d = 4,
        Character3d = 8,
        Character3d_0 = 16,
        Character3d_1 = 32,
        Character3d_NotReflect = 64,
        NotLayerDefault = 128,
        NotLayer3d = 256,
        Effect = 512
    }

    public enum LiveCameraBgColorType
    {
        Direct = 0,
        CharacterImageColorMain = 1,
        CharacterImageColorSub = 2,
        CharacterUIColorMain = 3,
        CharacterUIColorSub = 4
    }


    [Serializable]
    public class LiveTimelineKeyTimescaleData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Timescale;
            }
        }

        public float Timescale;
    }


    [System.Serializable]
    public class LiveTimelineKeyCameraPositionData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
            return LiveTimelineKeyDataType.CameraPos;
            }
        }
        public LiveCameraPositionType setType;
        public Vector3 position;
        public Vector3 charaPos;
        public Vector3[] bezierPoints;
        public LiveCharaPositionFlag charaRelativeBase;
        public LiveCameraCharaParts charaRelativeParts;
        public float traceSpeed;
        public float nearClip;
        public float farClip;
        public LiveCameraCullingLayer cullingLayer;
        public LiveCameraBgColorType BgColorType;
        public Color BgColor;
        public int BgColorTargetCharacterIndex;

        public Vector3 offset = Vector3.zero;

        public Vector3 posDirect = Vector3.zero;

        public bool newBezierCalcMethod;


        public float outlineZOffset = 1f;

        public CharacterLOD characterLODMask = (CharacterLOD)((uint)outlineLODMask + (uint)shaderLODMask);


        protected const CharacterLOD outlineLODMask = (CharacterLOD)0x3FF;

        protected const CharacterLOD shaderLODMask = (CharacterLOD)0x1FF8000;

        public int GetCullingMask()
        {
            // Native GetCullingMask (0x1af50a0) reads cullingLayer, not the legacy CGSS mask.
            int mask = 0;
            if ((cullingLayer & LiveCameraCullingLayer.TransparentFX) != 0) mask |= 1 << 1;
            if ((cullingLayer & LiveCameraCullingLayer.Effect) != 0) mask |= 1 << 11;
            if ((cullingLayer & LiveCameraCullingLayer.Background3d_NotReflect) != 0) mask |= 1 << 25;
            if ((cullingLayer & LiveCameraCullingLayer.Background3d) != 0) mask |= 1 << 12;
            if ((cullingLayer & LiveCameraCullingLayer.Character3d) != 0) mask |= 1 << 13;
            if ((cullingLayer & LiveCameraCullingLayer.Character3d_0) != 0) mask |= 1 << 27;
            if ((cullingLayer & LiveCameraCullingLayer.Character3d_1) != 0) mask |= 1 << 28;
            if ((cullingLayer & LiveCameraCullingLayer.Character3d_NotReflect) != 0) mask |= 1 << 26;
            if ((cullingLayer & LiveCameraCullingLayer.NotLayerDefault) == 0)
                mask |= GraphicSettings.GetCullingLayer(GraphicSettings.LayerIndex.LayerDefault);
            if ((cullingLayer & LiveCameraCullingLayer.NotLayer3d) == 0)
                mask |= GraphicSettings.GetCullingLayer(GraphicSettings.LayerIndex.Layer3D);
            return mask;
        }

        public virtual Vector3 GetValue(LiveTimelineControl timelineControl)
        {
            return GetValue(timelineControl, setType, containOffset: true);
        }

        protected virtual Vector3 GetValue(LiveTimelineControl timelineControl, LiveCameraPositionType type, bool containOffset)
        {
            Vector3 vector = position;
            switch (type)
            {
                case LiveCameraPositionType.Direct:
                    vector += posDirect;
                    break;
                case LiveCameraPositionType.Character:
                    vector += timelineControl.GetPositionWithCharacters(charaRelativeBase, charaRelativeParts, charaPos);
                    break;
            }
            if (!containOffset)
            {
                return vector;
            }
            return vector + offset;
        }

        public int GetBezierPointCount()
        {
            if (!HasBezier())
            {
                return 0;
            }
            return bezierPoints.Length;
        }

        public bool HasBezier()
        {
            if (bezierPoints != null)
            {
                return bezierPoints.Length != 0;
            }
            return false;
        }

        public bool necessaryToUseNewBezierCalcMethod
        {
            get
            {
                if (!newBezierCalcMethod)
                {
                    return GetBezierPointCount() > 3;
                }
                return true;
            }
        }

        public Vector3 GetBezierPoint(int index, LiveTimelineControl timelineControl)
        {
            if (HasBezier() && index < bezierPoints.Length)
            {
                return GetValue(timelineControl) + bezierPoints[index];
            }
            return GetValue(timelineControl) + Vector3.zero;
        }

        public void GetBezierPoints(LiveTimelineControl timelineControl, Vector3[] outPoints, int startIndex)
        {
            if (HasBezier())
            {
                for (int i = 0; i < bezierPoints.Length; i++)
                {
                    outPoints[startIndex + i] = GetValue(timelineControl) + bezierPoints[i];
                }
            }
        }
    }

    public enum CullingLayer
    {
        TransparentFX = 1,
        Background3D_NotReflect = 2,
        Background3d = 4,
        Character3d = 8,
        Character3d_0 = 0x10,
        Character3d_1 = 0x20,
        Character3d_2 = 0x40,
        Character3d_3 = 0x80,
        Character3d_4 = 0x100,
        Character3D_NotReflect = 0x200,
        Background3D_Other = 0x400
    }

    public enum CharacterLOD
    {
        Outline_0 = 1,
        Outline_1 = 2,
        Outline_2 = 4,
        Outline_3 = 8,
        Outline_4 = 0x10,
        Outline_5 = 0x20,
        Outline_6 = 0x40,
        Outline_7 = 0x80,
        Outline_8 = 0x100,
        Outline_9 = 0x200,
        Shader_0 = 0x8000,
        Shader_1 = 0x10000,
        Shader_2 = 0x20000,
        Shader_3 = 0x40000,
        Shader_4 = 0x80000,
        Shader_5 = 0x100000,
        Shader_6 = 0x200000,
        Shader_7 = 0x400000,
        Shader_8 = 0x800000,
        Shader_9 = 0x1000000
    }

    public enum TimelinePlayerMode
    {
        Light,
        Default
    }

    public delegate void CameraPosUpdateInfoDelegate(ref CameraPosUpdateInfo updateInfo);

    [Serializable]
    public class LiveTimelineKeyTimescaleDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTimescaleData>
    {
        public bool _isCheckSameFrame;
    }

    [System.Serializable]
    public class LiveTimelineKeyCameraPositionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCameraPositionData>
    {

    }

    public struct FindKeyResult
    {
        public LiveTimelineKey key;

        public int index;
    }

    public class LiveTimelineWorkSheet : ScriptableObject
    {
        public string version;
        public int targetCameraIndex;
        public bool enableAtRuntime;
        public bool enableAtEdit;
        public float TotalTimeLength;
        public bool Lyrics;
        public LiveTimelineDefine.SheetIndex SheetType;
        [SerializeField] public LiveTimelineKeyTimescaleDataList timescaleKeys;
        [SerializeField] public LiveTimelineKeyCameraPositionDataList cameraPosKeys;
        [SerializeField] public List<LiveTimelineMultiCameraPositionData> multiCameraPosKeys;
        [SerializeField] public List<LiveTimelineMultiCameraLookAtData> multiCameraLookAtKeys;
        [SerializeField] public List<LiveTimelineMultiCameraLayerData> multiCameraLayerKeys;
        [SerializeField] public List<LiveTimelineMultiCameraPostFilmData> postFilm1MultiCameraKeys;
        [SerializeField] public List<LiveTimelineMultiCameraPostFilmData> postFilm2MultiCameraKeys;
        [SerializeField] public List<LiveTimelineMultiCameraPostFilmData> postFilm3MultiCameraKeys;
        [SerializeField] public List<LiveTimelineMultiCameraPostEffectBloomDiffusionData> postEffectBloomDiffusionMultiCameraKeys;

        //[SerializeField]锟斤拷锟节革拷锟斤拷锟节憋拷慕疟锟斤拷锒拷锟斤拷时锟斤拷
        [SerializeField] public LiveTimelineKeyCameraLookAtDataList cameraLookAtKeys;
        [SerializeField] public LiveTimelineKeyCameraFovDataList cameraFovKeys;
        [SerializeField] public LiveTimelineKeyCameraRollDataList cameraRollKeys;

        [SerializeField]
        public LiveTimelineKeyPostEffectBloomDiffusionDataList postEffectBloomDiffusionKeys;
        [SerializeField] public LiveTimelineKeyPostEffectDOFDataList postEffectDOFKeys;
        [SerializeField] public LiveTimelineKeyRadialBlurDataList radialBlurKeys;
        [SerializeField] public LiveTimelineKeyTiltShiftDataList tiltShiftKeys;
        // Main-camera simple/nonselective color correction; other modes remain unsupported.
        public List<LiveTimelineColorCorrectionData> colorCorrectionDataLists;
        public LiveTimelineKeyPostFilmDataList postFilmKeys;
        [SerializeField] public LiveTimelineKeyPostFilmDataList postFilm2Keys;
        [SerializeField] public LiveTimelineKeyPostFilmDataList postFilm3Keys;
        [SerializeField]
        public List<LiveTimelineHdrBloomData> hdrBloomList;

        // 子系统 A7b：Exposure(轨道92)/ToneCurve(轨道91) 关键帧表。
        // 原生 AlterLateUpdate(0x1ac9460) 在 0x1ac9e0f/0x1ac9e24 传
        // [rdi+1A8h]/[rdi+1B0h]（sheet 内第 0x1A8/0x1B0 偏移的两个
        // LiveTimelineKeyXxxDataList）给 AlterUpdate_Exposure/ToneCurve。
        // Names are case-sensitive and match the bundle TypeTree.
        [SerializeField] public LiveTimelineKeyExposureDataList ExposureKeys;
        [SerializeField] public LiveTimelineKeyToneCurveDataList ToneCurveKeys;

        [SerializeField] public List<LiveTimelineEffectData> effectList;
        [SerializeField] public List<LiveTimelineParticleData> particleList;
        [SerializeField] public List<LiveTimelineParticleGroupData> particleGroupList;
        [SerializeField] public List<LiveTimelinePropsData> propsList;
        [SerializeField] public List<LiveTimelinePropsAttachData> propsAttachList;
        [SerializeField] public List<LiveTimelineSpotlight3dData> spotlight3dList;
        [SerializeField] public List<LiveTimelineVolumeLightData> volumeLightKeys;
        [SerializeField] public List<LiveTimelineLightShaftsData> lightShaftsKeysLine;

        [SerializeField] public List<LiveTimelineCharaMotSeqData> charaMotSeqList;
        [SerializeField] public List<LiveTimelineAnimationData> animationList = new List<LiveTimelineAnimationData>();

        [SerializeField] public LiveTimelineKeyCameraSwitcherDataList cameraSwitcherKeys;
        [SerializeField] public LiveTimelineKeyLipSyncDataList ripSyncKeys;
        [SerializeField] public LiveTimelineKeyLipSyncDataList ripSync2Keys;

        [SerializeField] public LiveTimelineFacialData facial1Set;
        [SerializeField] public LiveTimelineFacialData[] other4FacialArray;
        [SerializeField] public LiveTimelineFormationOffsetData formationOffsetSet;

        [SerializeField] public List<LiveTimelineGlobalFogData> globalFogDataLists;
        [SerializeField] public List<LiveTimelineGlobalLightData> globalLightDataLists;
        [SerializeField] public List<LiveTimelineStageEnvironmentData> environmentDataLists;
        [SerializeField] public List<LiveTimelineBgColor1Data> bgColor1List;
        [SerializeField] public List<LiveTimelineBgColor2Data> bgColor2List;
        [SerializeField] public List<LiveTimelineBlinkLightData> blinkLightList;
        [SerializeField] public List<LiveTimelineWashLightData> washLightList;
        [SerializeField] public List<LiveTimelineMonitorControlData> monitorControlList;
        [SerializeField] public List<LiveTimelineMonitorCameraLayerData> monitorCameraLayerKeys;
        [SerializeField] public List<LiveTimelineMonitorCameraPositionData> monitorCameraPosKeys;
        [SerializeField] public List<LiveTimelineMonitorCameraLookAtData> monitorCameraLookAtKeys;
        [SerializeField]public List<LiveTimelineLaserData> laserList;
        [SerializeField] public List<LiveTimelineUVScrollLightData> uvScrollLightList;
        [SerializeField] public List<LiveTimelineLensFlareData> lensFlareList;
        [SerializeField] public List<LiveTimelineLightProjectionData> lightProjectionList;

        [SerializeField] public List<LiveTimelineTransformData> transformList;
        [SerializeField] public List<LiveTimelineObjectData> objectList;
        [SerializeField] public List<LiveTimelineMobCyalumeControlData> MobControlKeys;
        [SerializeField] public List<LiveTimelineMobCyalumeControlData> CyalumeControlKeys;

        private void OnEnable()
        {
            InitializeLightProjection(null);
            InitializeProps(null);
        }

        public void InitializeLightProjection(LiveTimelineControl control)
        {
            if (lightProjectionList == null) return;
            foreach (var group in lightProjectionList)
            {
                if (group == null) continue;
                group.UpdateStatus();
                if (group.keys == null) continue;
                for (int i = 0; i < group.keys.Count; ++i)
                    group.keys[i]?.OnLoad(control);
            }
        }

        public void InitializeProps(LiveTimelineControl control)
        {
            InitializePropsGroups(propsList, control);
            InitializePropsGroups(propsAttachList, control);
        }

        private static void InitializePropsGroups<T>(IList<T> groups, LiveTimelineControl control)
            where T : ILiveTimelineGroupDataWithName
        {
            if (groups == null) return;
            for (int i = 0; i < groups.Count; ++i)
            {
                var group = groups[i];
                if (group == null) continue;
                group.UpdateStatus();
                var keys = group.GetKeyList();
                if (keys == null) continue;
                for (int keyIndex = 0; keyIndex < keys.Count; ++keyIndex)
                    keys[keyIndex]?.OnLoad(control);
            }
        }

        /*
		//锟斤拷锟节匡拷锟皆碉拷锟斤拷AB锟斤拷锟剿ｏ拷锟斤拷然锟斤拷锟芥发锟斤拷没什么锟斤拷...说锟斤拷锟斤拷什么时锟斤拷锟斤拷锟矫碉拷
		private void Start()
		{
			LoadCharaMotion();
		}

		public void LoadCharaMotion()
		{
			foreach(LiveTimelineCharaMotSeqData liveCharaData in charaMotSeqList)
			{
				foreach(LiveTimelineKeyCharaMotionData charaMotionData in liveCharaData.keys.thisList)
				{
					foreach(var motionname in UmaViewerMain.Instance.AbList.Where(a => a.Name.StartsWith("3d/motion/live/body") && a.Name.EndsWith(charaMotionData.motionName)))
					{
						//UmaViewerBuilder.Instance.LoadComponent(motionname);
					}
				}
			}
		}
		*/
    }

    public static class LiveCharaPositionFlag_Helper
    {
        public static LiveCharaPositionFlag Default5 => LiveCharaPositionFlag.Center | LiveCharaPositionFlag.Place02 | LiveCharaPositionFlag.Place03 | LiveCharaPositionFlag.Place04 | LiveCharaPositionFlag.Place05;

        public static LiveCharaPositionFlag Everyone => LiveCharaPositionFlag.All;

        public static bool hasFlag(this LiveCharaPositionFlag This, LiveCharaPosition pos)
        {
            return This.hasFlag((LiveCharaPositionFlag)(1 << (int)pos));
        }

        public static bool hasFlag(this LiveCharaPositionFlag This, int bit)
        {
            return This.hasFlag((LiveCharaPositionFlag)bit);
        }

        public static bool hasFlag(this LiveCharaPositionFlag This, LiveCharaPositionFlag bit)
        {
            return (This & bit) != 0;
        }

        public static bool hasFlag(this LiveTimelineKeyAttribute This, LiveTimelineKeyAttribute bit)
    {
            return (This & bit) != 0;
        }
    }
    
}
