using Gallop.Live.Cutt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Gallop.Live.Cyalume;
using Gallop.RenderPipeline;

namespace Gallop.Live
{
    public class Director : MonoBehaviour
    {
        private static Director _instance = null;
        public LiveTimelineControl _liveTimelineControl; //Edited to public
        [SerializeField]
        public float _liveCurrentTime;  //Edited to public
        public bool _isLiveSetup; //Edit to pulic
        public StageController _stageController; //Edited to public
        [SerializeField]
        private GameObject[] _cameraNodes;
        private Camera[] _cameraObjects;
        private Transform[] _cameraTransforms;
        [SerializeField]
        private CameraLookAt _cameraLookAt;
        private int _activeCameraIndex  = 1;
        private readonly int[] kTimelineCameraIndices = new int[3] { 1, 2, 3 };

        public static Director instance => _instance;

        //real work start
        public LiveEntry live;
        private const string CUTT_PATH = "cutt/cutt_son{0}/cutt_son{0}";
        private const string STAGE_PATH = "3d/env/live/live{0}/pfb_env_live{0}_controller000";
        private const string SONG_PATH = "sound/l/{0}/snd_bgm_live_{0}_oke_01";
        private const string VOCAL_PATH = "sound/l/{0}/snd_bgm_live_{0}_chara_{1}_01";
        private const string RANDOM_VOCAL_PATH = "sound/l/{0}/snd_bgm_live_{0}_chara";
        private const string LIVE_PART_PATH = "live/musicscores/m{0}/m{0}_part";
        private const string EFFECT_PATH = "3d/effect/live/pfb_{0}";

        private UmaViewerBuilder Builder => UmaViewerBuilder.Instance;

        public List<Transform> charaObjs;

        public List<UmaContainerCharacter> CharaContainerScript = new List<UmaContainerCharacter>();

        public List<Animation> charaAnims;
        public List<UmaViewerAudio.CuteAudioSource> liveVocal = new List<UmaViewerAudio.CuteAudioSource>();

        // effect track: maps effectList entry -> (last key frame, active instance)
        private Dictionary<LiveTimelineEffectData, (int frame, GameObject instance)> _activeEffects
            = new Dictionary<LiveTimelineEffectData, (int, GameObject)>();

        private Dictionary<string, Vector2> _uvScrollAccum = new Dictionary<string, Vector2>();

        private Volume _postProcessVolume;
        private readonly Dictionary<int, GameObject> _liveProps = new Dictionary<int, GameObject>();
        private readonly Dictionary<LiveTimelineAdditionalLight, Light> _additionalLights = new Dictionary<LiveTimelineAdditionalLight, Light>();
        private readonly Dictionary<LiveTimelineLightProjectionData, Light> _projectionLights = new Dictionary<LiveTimelineLightProjectionData, Light>();

        public UmaViewerAudio.CuteAudioSource liveMusic = new UmaViewerAudio.CuteAudioSource();

        public PartEntry partInfo;

        public bool _syncTime = false;
        public bool _soloMode = false;

        public int characterCount = 0;
        public int allowCount = 0;

        public int liveMode = 1;

        public LiveViewerUI UI;

        public float totalTime;

        public SliderControl sliderControl;

        public bool IsRecordVMD;

        public bool RequireStage = true;

        public Transform MainCameraTransform => _mainCameraTransform;

        private Transform _mainCameraTransform;
        private bool _playbackPaused;
        private bool _freeCameraActive;
        private FreeCam _freeCamera;

        public bool PlaybackPaused => _playbackPaused;
        public bool FreeCameraActive => _freeCameraActive;

        public bool isTimelineControlled
        {
            get
            {
                if (_liveTimelineControl != null)
                {
                    return _liveTimelineControl.data != null;
                }
                return false;
            }
        }

        public float CalcFrameJustifiedMusicTime()
        {
            if (isTimelineControlled)
            {
                return Mathf.RoundToInt(musicScoreTime * 60f) / 60f;
            }
            return musicScoreTime;
        }

        public float musicScoreTime => Mathf.Clamp(smoothMusicScoreTime, 0f, 99999f);

        private float smoothMusicScoreTime => _liveCurrentTime;//temp to liveCurrentTime

        public void Initialize()
        {
            if (live != null)
            {
                _instance = this;
                if (GetComponent<SkirtCollisionRuntimeTuner>() == null)
                    gameObject.AddComponent<SkirtCollisionRuntimeTuner>();
                if (GetComponent<LivePlaybackToolbar>() == null)
                    gameObject.AddComponent<LivePlaybackToolbar>();
                Debug.Log(string.Format(CUTT_PATH, live.MusicId));
                Builder.LoadAssetPath(string.Format(CUTT_PATH, live.MusicId), transform);
                if (RequireStage)
                {
                    Debug.Log(live.BackGroundId);
                    Builder.LoadAssetPath(string.Format(STAGE_PATH, live.BackGroundId), transform);
                    _liveTimelineControl.StageObjectMap = _stageController.StageObjectMap;
                    _liveTimelineControl.StageObjectUnitMap = _stageController.StageObjectUnitMap;
                    InitializeCyalumeStage();
                    InitializeStageMonitor();
                }

                //Make CharacterObject

                var characterStandPos = _liveTimelineControl.transform.Find("CharacterStandPos");
                int counter = 0;
                var standPos = characterStandPos.GetComponentsInChildren<Transform>();
                var count = _liveTimelineControl.data.characterSettings.useHighPolygonModel.Length;
                for (int i = 0; i < count; i++)
                {
                    if (i < characterStandPos.childCount)
                    {
                        var newObj = Instantiate(standPos[i + 1], transform);
                        newObj.gameObject.name = string.Format("CharacterObject{0}", counter);
                        charaObjs.Add(newObj.transform);
                        counter++;
                    }
                    else
                    {
                        var newObj = Instantiate(standPos[i % characterStandPos.childCount + 1], transform);
                        newObj.gameObject.name = string.Format("CharacterObject{0}", counter);
                        charaObjs.Add(newObj.transform);
                        counter++;
                    }
                };


                //Get live parts info
                UmaDatabaseEntry partAsset = UmaViewerMain.Instance.AbList[string.Format(LIVE_PART_PATH, live.MusicId)];
                UmaViewerAudio.LastAudioPartIndex = -1;

                Debug.Log(partAsset.Name);

                AssetBundle bundle = UmaAssetManager.LoadAssetBundle(partAsset);
                TextAsset partData = bundle.LoadAsset<TextAsset>($"m{live.MusicId}_part");
                partInfo = new PartEntry(partData.text);

            }
        }

        public void InitializeUI()
        {
            UI = GameObject.Find("LiveUI").GetComponent<LiveViewerUI>();

            sliderControl = UI.ProgressBar.GetComponent<SliderControl>();
            LiveViewerUI.Instance.RecordingUI.SetActive(IsRecordVMD);
            LiveViewerUI.Instance.RecordingText.text = $"�� Recording...\r\n VMD will be saved in {Path.GetFullPath(Application.dataPath + UnityHumanoidVMDRecorder.FileSavePath)}";
        }

        public void InitializeTimeline(List<LiveCharacterSelect> characters, int mode)
        {
            _uvScrollAccum.Clear();
            totalTime = _liveTimelineControl.data.timeLength;

            // 每首歌开场时列一遍本曲实际带了哪些轨道、各多少关键帧。
            // 「worksheet[1..] 是否也带数据」这个问题已经有结论（1177 实测 Count==1 / MainLive，
            // 读 worksheetList[0] 不丢东西），但这份清单本身仍是接新轨道时最快的对照表，故保留。
            LiveTimelineWorksheetDiag.Dump(_liveTimelineControl.data);

            liveMode = mode;

            allowCount = characters.Count;

            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name != "")
                {
                    characterCount += 1;
                }
            }
            if (characterCount == 1)
            {
                _soloMode = true;
            }

            _liveTimelineControl.InitCharaMotionSequence(_liveTimelineControl.data.characterSettings.motionSequenceIndices);

            _liveTimelineControl.OnUpdateLipSync += OnLipSyncUpdate;
            _liveTimelineControl.OnUpdateFacial += OnFacialUpdate;
            _liveTimelineControl.OnUpdateGlobalLight += OnGlobalLightUpdate;
            _liveTimelineControl.OnUpdateBgColor1 += OnBgColor1Update;

            SetupCharacterLocator();
            InitializeCamera();
            UpdateMainCamera();
            InitializeMultiCamera(_liveTimelineControl);
            for (int i = 0; i < kTimelineCameraIndices.Length; i++)
            {
                int num = kTimelineCameraIndices[i];
                if (num < _cameraObjects.Length)
                {
                    _liveTimelineControl.SetTimelineCamera(_cameraObjects[num], i);
                }
            }

            _liveTimelineControl.OnUpdateCameraSwitcher += OnCameraSwitcherUpdate;

            _liveTimelineControl.OnUpdateBgColor2 += OnBgColor2Update;
            _liveTimelineControl.OnUpdateEffect += OnEffectUpdate;
            _liveTimelineControl.OnUpdateGlobalFog += OnGlobalFogUpdate;
            _liveTimelineControl.OnUpdateSpotlight3d += OnSpotlight3dUpdate;
            _liveTimelineControl.OnUpdateUVScrollLight += OnUVScrollLightUpdate;
            _liveTimelineControl.OnUpdateVolumeLight += OnVolumeLightUpdate;
            _liveTimelineControl.OnUpdateLightShafts += OnLightShaftsUpdate;
            _liveTimelineControl.OnUpdateParticle += OnParticleUpdate;
            _liveTimelineControl.OnUpdateParticleGroup += OnParticleGroupUpdate;
            _liveTimelineControl.OnUpdateWashLight += OnWashLightUpdate;
            _liveTimelineControl.OnUpdateLaser += OnLaserUpdate;
            _liveTimelineControl.OnUpdateBlinkLight += OnBlinkLightUpdate;
            _liveTimelineControl.OnUpdateChromaticAberration += OnChromaticAberrationUpdate;
            _liveTimelineControl.OnUpdateColorCorrection += OnColorCorrectionUpdate;
            _liveTimelineControl.OnUpdatePostFilm += OnPostFilmUpdate;
            _liveTimelineControl.OnUpdateLensFlare += OnLensFlareUpdate;
            _liveTimelineControl.OnUpdateEnvironment += OnEnvironmentUpdate;
            _liveTimelineControl.OnUpdateFacialToon += OnFacialToonUpdate;
            _liveTimelineControl.OnUpdateProps += OnPropsUpdate;
            _liveTimelineControl.OnUpdatePropsAttach += OnPropsAttachUpdate;
            _liveTimelineControl.OnUpdateCharaFootLight += OnCharaFootLightUpdate;
            _liveTimelineControl.OnUpdateAdditionalLight += OnAdditionalLightUpdate;
            _liveTimelineControl.OnUpdateLightProjection += OnLightProjectionUpdate;
            PostFilmRendererFeature.ResetLayers();

            // 获取或创建摄像机上的 Volume 组件，供后处理 handler 使用
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                _postProcessVolume = mainCam.GetComponent<Volume>();
                if (_postProcessVolume == null)
                    _postProcessVolume = mainCam.gameObject.AddComponent<Volume>();
                if (_postProcessVolume.profile == null)
                    _postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

                // 只加真的有 handler 驱动的 override。Bloom 曾在这里被加上，但驱动它的
                // HdrBloom 轨道全语料 0 keys，等于挂一个没人写、参数恒为默认值的效果。
                var profile = _postProcessVolume.profile;
                if (!profile.Has<ChromaticAberration>()) profile.Add<ChromaticAberration>(true);
                if (!profile.Has<ColorAdjustments>())   profile.Add<ColorAdjustments>(true);
                if (!profile.Has<ColorCurves>())        profile.Add<ColorCurves>(true);
            }
        }

        private void InitializeCyalumeStage()
        {
            if (_stageController == null) return;

            foreach (var holder in _stageController.GetComponentsInChildren<AssetHolder>(true))
            {
                if (holder == null || holder._assetTable == null || holder._assetTable.list == null) continue;

                bool hasDefault = false;
                bool hasRandom = false;
                foreach (var entry in holder._assetTable.list)
                {
                    if (string.Equals(entry.Key, "default", StringComparison.OrdinalIgnoreCase)) hasDefault = true;
                    if (string.Equals(entry.Key, "random", StringComparison.OrdinalIgnoreCase)) hasRandom = true;
                }

                if (!hasDefault && !hasRandom) continue;
                var controller = holder.GetComponent<CyalumeController3D>();
                if (controller == null) controller = holder.gameObject.AddComponent<CyalumeController3D>();
                controller.StartOfficialLikeSetup();
                return;
            }

            Debug.Log("[Cyalume] no audience AssetHolder was present on this stage.");
        }

        private void InitializeStageMonitor()
        {
            if (_stageController == null) return;
            if (_stageController.GetComponent<StageMonitorDriver>() == null)
                _stageController.gameObject.AddComponent<StageMonitorDriver>();
        }

        private void OnEffectUpdate(LiveTimelineEffectData effectData, LiveTimelineKeyEffectData keyData)
        {
            if (keyData == null) return;

            // Only (re)instantiate when the key frame changes
            if (_activeEffects.TryGetValue(effectData, out var current) && current.frame == keyData.frame)
            {
                // Same key — update position if following owner
                if (current.instance != null)
                    ApplyEffectTransform(current.instance.transform, keyData);
                return;
            }

            // Destroy previous instance
            if (_activeEffects.TryGetValue(effectData, out var old) && old.instance != null)
                Destroy(old.instance);

            GameObject prefab = LoadEffectPrefab(effectData.name);
            GameObject instance = null;
            if (prefab != null)
            {
                instance = Instantiate(prefab, transform);
                ApplyEffectTransform(instance.transform, keyData);
            }
            // 载不到时也要记下帧号，否则每帧都会重试一遍 bundle 载入。
            _activeEffects[effectData] = (keyData.frame, instance);
        }

        private void OnLensFlareUpdate(LiveTimelineLensFlareData data, LiveTimelineKeyLensFlareData key)
        {
            if (key == null || _stageController == null) return;
            bool enabled = key.enableParameter == 0 || key.enableFlare != 0;
            foreach (var flare in _stageController.GetComponentsInChildren<CustomLensFlare>(true))
            {
                if (flare == null) continue;
                flare.ApplyTimeline(enabled,
                    key.IsOverridePosition != 0 ? key.offset : flare.transform.localPosition,
                    key.color, key.brightness, key.fadeSpeed);
            }
        }

        private void OnEnvironmentUpdate(LiveTimelineStageEnvironmentData data, LiveTimelineKeyStageEnvironmentData key)
        {
            if (key == null || _stageController == null) return;
            foreach (var mirror in _stageController.GetComponentsInChildren<MirrorReflection>(true))
                mirror.ApplyTimeline(key.MirrorIsValid, key.MirrorEnabled, key.ReflectionRate);
        }

        private void OnFacialToonUpdate(int characterIndex, LiveTimelineKeyFacialToonData key)
        {
            if (key == null || characterIndex < 0 || characterIndex >= CharaContainerScript.Count) return;
            var container = CharaContainerScript[characterIndex];
            if (container == null) return;
            var block = PropBlock;
            foreach (var renderer in container.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (material.HasProperty("_CheekPretenseThreshold")) block.SetFloat("_CheekPretenseThreshold", key.CheekPretenseThreshold);
                    if (material.HasProperty("_NosePretenseThreshold")) block.SetFloat("_NosePretenseThreshold", key.NosePretenseThreshold);
                    if (material.HasProperty("_CylinderBlend")) block.SetFloat("_CylinderBlend", key.CylinderBlend);
                    if (material.HasProperty("_HairNormalBlend")) block.SetFloat("_HairNormalBlend", key.HairNormalBlend);
                    if (material.HasProperty("_UseOriginalDirectionalLight")) block.SetFloat("_UseOriginalDirectionalLight", key.UseOriginalDirectionalLight);
                    if (material.HasProperty("_OriginalDirectionalLightDir")) block.SetVector("_OriginalDirectionalLightDir", key.OriginalDirectionalLightDir);
                    if (material.HasProperty("_EyeToonStep")) block.SetFloat("_EyeToonStep", key.EyeToonStep);
                    if (material.HasProperty("_EyeToonFeather")) block.SetFloat("_EyeToonFeather", key.EyeToonFeather);
                    if (material.HasProperty("_EyeSaturation")) block.SetFloat("_EyeSaturation", key.EyeSaturation);
                }
                renderer.SetPropertyBlock(block);
            }
        }

        private GameObject ResolveLiveProp(int propsId)
        {
            if (_liveProps.TryGetValue(propsId, out var existing) && existing != null) return existing;
            var settings = _liveTimelineControl?.data?.propsSettings;
            if (settings?.propsDataGroup == null || propsId < 0 || propsId >= settings.propsDataGroup.Length) return null;
            var group = settings.propsDataGroup[propsId];
            if (group == null || string.IsNullOrEmpty(group.propsName) || !UmaViewerMain.Instance.AbList.TryGetValue(group.propsName, out var entry)) return null;
            var prefab = entry.Get<GameObject>();
            if (prefab == null) return null;
            var instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            _liveProps[propsId] = instance;
            return instance;
        }

        private void OnPropsUpdate(LiveTimelinePropsData data, LiveTimelineKeyPropsData key)
        {
            if (key == null) return;
            var prop = ResolveLiveProp(key.propsID);
            if (prop == null) return;
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = key.rendererEnable != 0;
                var block = PropBlock;
                renderer.GetPropertyBlock(block);
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (material.HasProperty("_Color")) block.SetColor("_Color", key.color);
                    if (material.HasProperty("_RootColor")) block.SetColor("_RootColor", key.rootColor);
                    if (material.HasProperty("_TipColor")) block.SetColor("_TipColor", key.tipColor);
                    if (material.HasProperty("_ColorPower")) block.SetFloat("_ColorPower", key.colorPower);
                    if (key.IsUpdateOutline != 0 && material.HasProperty("_OutlineWidth")) block.SetFloat("_OutlineWidth", key.OutlineWidth);
                    if (key.IsUpdateOutline != 0 && material.HasProperty("_OutlineColor")) block.SetColor("_OutlineColor", key.OutlineColor);
                    if (key.IsEmissive != 0 && material.HasProperty("_EmissiveColor")) block.SetColor("_EmissiveColor", key.EmissiveColor);
                }
                renderer.SetPropertyBlock(block);
            }
        }

        private void OnPropsAttachUpdate(LiveTimelinePropsAttachData data, LiveTimelineKeyPropsAttachData key)
        {
            if (key == null) return;
            var prop = ResolveLiveProp(key._propsId);
            if (prop == null) return;
            Transform target = null;
            for (int i = 0; i < _liveTimelineControl.liveCharactorLocators.Length && target == null; i++)
            {
                if (_liveTimelineControl.liveCharactorLocators[i] is LiveTimelineCharaLocator locator &&
                    locator.Bones != null && locator.Bones.TryGetValue(key._attachJointName, out var bone)) target = bone;
            }
            if (target == null) return;
            prop.transform.SetParent(target, false);
            prop.transform.localPosition = key._offsetPosition;
            prop.transform.localRotation = Quaternion.Euler(key.OffsetRotate);
            prop.transform.localScale = key.OffsetScale;
        }

        private void OnCharaFootLightUpdate(LiveTimelineKeyCharaFootLightData key)
        {
            if (key == null) return;
            for (int i = 0; i < CharaContainerScript.Count; i++)
            {
                if (key.positionFlag != 0 && (key.positionFlag & (1 << i)) == 0) continue;
                if (i >= key.lightColor.Length || i >= key.hightMax.Length) continue;
                var container = CharaContainerScript[i];
                if (container == null) continue;
                float blend = i < key.LightBlendModeArray.Length ? key.LightBlendModeArray[i] : 0f;
                float easing = i < key.EasingArray.Length ? key.EasingArray[i] : 0f;
                var block = PropBlock;
                foreach (var renderer in container.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    bool supported = false;
                    foreach (var material in renderer.sharedMaterials)
                        supported |= material != null && material.HasProperty("_HightLightColor") && material.HasProperty("_HightLightParam");
                    if (!supported) continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_HightLightColor", key.lightColor[i]);
                    block.SetVector("_HightLightParam", new Vector4(key.hightMax[i], blend, easing, 0f));
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        private void OnAdditionalLightUpdate(LiveTimelineAdditionalLight data, LiveTimelineKeyData_AdditionalLight key)
        {
            if (data == null || key == null) return;
            if (!_additionalLights.TryGetValue(data, out var light) || light == null)
            {
                var go = new GameObject(string.IsNullOrEmpty(data.name) ? "AdditionalLight" : data.name);
                go.transform.SetParent(_stageController != null ? _stageController.transform : transform, false);
                light = go.AddComponent<Light>();
                _additionalLights[data] = light;
            }
            light.gameObject.SetActive(key.IsEnable != 0);
            light.transform.localPosition = key.Position;
            light.transform.localRotation = Quaternion.Euler(key.Rotate);
            light.type = (LightType)Mathf.Clamp(key.Type, 0, 2);
            light.range = Mathf.Max(0f, key.Range);
            light.spotAngle = Mathf.Clamp(key.SpotAngle, 1f, 179f);
            light.intensity = Mathf.Max(0f, key.Strength);
            light.bounceIntensity = Mathf.Max(0f, key.IndirectMultiplier);
            light.shadows = (LightShadows)Mathf.Clamp(key.ShadowType, 0, 2);
            light.shadowStrength = Mathf.Clamp01(key.Strength);
            light.shadowBias = Mathf.Max(0f, key.Bias);
            light.shadowNormalBias = Mathf.Max(0f, key.NormalBias);
            light.shadowNearPlane = Mathf.Max(0.01f, key.NearPlane);
        }

        private void OnLightProjectionUpdate(LiveTimelineLightProjectionData data, LiveTimelineKeyLightProjectionData key)
        {
            if (data == null || key == null || _stageController == null) return;
            if (!_projectionLights.TryGetValue(data, out var light) || light == null)
            {
                CustomProjector authored = null;
                foreach (var candidate in _stageController.GetComponentsInChildren<CustomProjector>(true))
                {
                    if (candidate != null && string.Equals(candidate.name, data.name, StringComparison.OrdinalIgnoreCase))
                    { authored = candidate; break; }
                }
                var host = authored != null ? authored.gameObject : new GameObject(data.name ?? "LightProjection");
                if (authored == null) host.transform.SetParent(_stageController.transform, false);
                light = host.GetComponent<Light>();
                if (light == null) light = host.AddComponent<Light>();
                light.type = LightType.Spot;
                if (authored != null) light.cookie = authored.ProjectionTexture;
                _projectionLights[data] = light;
            }

            light.gameObject.SetActive(key.IsEnable != 0);
            light.transform.localPosition = key.Position;
            light.transform.localRotation = Quaternion.Euler(key.Angle);
            light.transform.localScale = key.Scale;
            light.range = Mathf.Max(0.01f, key.FarClipPlane);
            light.spotAngle = Mathf.Clamp(key.FieldOfView, 1f, 179f);
            light.color = key.Color;
            light.intensity = Mathf.Max(0f, key.ColorPower * key.BlinkLightBrightnessPower);
            light.shadowNearPlane = Mathf.Max(0.01f, key.NearClipPlane);
            if (key.OverrideIgnoreLayer != 0) light.cullingMask = key.OverrideLayerMask.value;
        }

        private static GameObject LoadEffectPrefab(string effectName)
        {
            string path = string.Format(EFFECT_PATH, effectName);
            if (!UmaViewerMain.Instance.AbList.TryGetValue(path, out var entry)) return null;

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry);
            return bundle != null ? bundle.LoadAsset<GameObject>(Path.GetFileName(path)) : null;
        }

        private void ApplyEffectTransform(Transform t, LiveTimelineKeyEffectData keyData)
        {
            Vector3 basePos = Vector3.zero;

            // owner == World (18) or out of range: world origin
            int ownerIndex = keyData.owner;
            if (ownerIndex >= 0 && ownerIndex < CharaContainerScript.Count)
            {
                var container = CharaContainerScript[ownerIndex];
                if (container != null)
                {
                    basePos = new Vector3(
                        keyData.IsLinkOwnerPositionX ? container.transform.position.x : 0f,
                        keyData.IsLinkOwnerPositionY ? container.transform.position.y : 0f,
                        keyData.IsLinkOwnerPositionZ ? container.transform.position.z : 0f
                    );
                }
            }

            t.position = basePos + keyData.offset;
            t.eulerAngles = keyData.offsetAngle;
            t.localScale = keyData.offsetScale;
        }

        private void OnLipSyncUpdate(LiveTimelineKeyIndex keyData_, float liveTime_)
        {
            var prevKey = keyData_.prevKey as LiveTimelineKeyLipSyncData;
            var curKey  = keyData_.key     as LiveTimelineKeyLipSyncData;
            var nextKey = keyData_.nextKey as LiveTimelineKeyLipSyncData;
            for (int k = 0; k < charaObjs.Count; k++)
            {
                if (k < CharaContainerScript.Count)
                    CharaContainerScript[k].FaceDrivenKeyTarget.AlterUpdateAutoLip(prevKey, curKey, liveTime_, ((int)curKey.character >> k) % 2);
            }
        }

        private void OnFacialUpdate(FacialDataUpdateInfo updateInfo_, float liveTime_, int position)
        {
            if (position < charaObjs.Count)
                CharaContainerScript[position].FaceDrivenKeyTarget.AlterUpdateFacialNew(ref updateInfo_, liveTime_);
        }

        /// <summary>
        /// 所有写 shader 属性的 handler 共用的 MaterialPropertyBlock。
        ///
        /// 用法固定为 **Get → 改 → Set**，不能省掉 Get：
        /// <c>Renderer.SetPropertyBlock()</c> 是「整块替换」而不是合并，而多条轨道在同一帧里
        /// 按 AlterLateUpdate 的顺序先后写同一批渲染器。GlobalLight → BgColor1（角色分支）就是
        /// 这样的一对：各自 new 一个空 block 再 Set 的话，后跑的 BgColor1 每帧都会把 GlobalLight
        /// 刚写进去的 13 个 rim 属性抹回材质默认值 —— 全语料 59/59 首都同时带这两条轨道的数据
        /// （歌曲 1177 是 GlobalLight×2 组 + CharaColor×2 组），于是 GlobalLight 从来没生效过。
        ///
        /// 既然每次使用都是「取回渲染器当前的块 → 改 → 写回」这一个自洽动作，块本身不携带
        /// 跨帧或跨 handler 的状态，一个实例足够；此前每个 handler 各存一个字段（chara /
        /// bgColor1 / bgColor2 / spotlight / uvScroll / blinkLight 六个）只是重复。
        /// </summary>
        private MaterialPropertyBlock _propBlock;

        private MaterialPropertyBlock PropBlock => _propBlock ??= new MaterialPropertyBlock();

        private void OnGlobalLightUpdate(ref GlobalLightUpdateInfo updateInfo)
        {
            var tmpPos = -(updateInfo.lightRotation * Vector3.forward).normalized;
            var block = PropBlock;
            foreach (var locator in _liveTimelineControl.liveCharactorLocators)
            {
                if (locator == null || !updateInfo.flags.hasFlag(locator.liveCharaStandingPosition) || locator is not LiveTimelineCharaLocator charaLocator) continue;
                var container = charaLocator.UmaContainer;
                if (!container) continue;
                foreach (var renderer in container.Renderers)
                {
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(block);
                    block.SetFloat("_RimShadowRate",     updateInfo.globalRimShadowRate);
                    block.SetColor("_RimColor",          updateInfo.rimColor);
                    block.SetFloat("_RimStep",           updateInfo.rimStep);
                    block.SetFloat("_RimFeather",        updateInfo.rimFeather);
                    block.SetFloat("_RimSpecRate",       updateInfo.rimSpecRate);
                    block.SetFloat("_RimHorizonOffset",  updateInfo.RimHorizonOffset);
                    block.SetFloat("_RimVerticalOffset", updateInfo.RimVerticalOffset);
                    block.SetFloat("_RimHorizonOffset2",  updateInfo.RimHorizonOffset2);
                    block.SetFloat("_RimVerticalOffset2", updateInfo.RimVerticalOffset2);
                    block.SetColor("_RimColor2",         updateInfo.rimColor2);
                    block.SetFloat("_RimStep2",          updateInfo.rimStep2);
                    block.SetFloat("_RimFeather2",       updateInfo.rimFeather2);
                    block.SetFloat("_RimSpecRate2",      updateInfo.rimSpecRate2);
                    block.SetFloat("_RimShadowRate2",    updateInfo.globalRimShadowRate2);
                    // 这两个原先走 renderer.materials，那个 getter 每次调用都会实例化材质副本
                    // 并新分配数组（每帧、每渲染器）。两者在 bundle 的 Gallop/3D/Chara/* 上
                    // 分别是 Float 和 Vector，MPB 能直接写；ToonEye/T 上的 [MaterialToggle]
                    // 只是编辑器 drawer，运行时 SetFloat 一样不会开关键字，所以行为不变。
                    block.SetFloat("_UseOriginalDirectionalLight", 1);
                    block.SetVector("_OriginalDirectionalLightDir", tmpPos);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        // BgColor1 在舞台物件上对应哪个 shader 属性尚未确认，按存在性依次尝试。
        // 首次解析每个轨道组时会打一条日志，说明命中了什么以及材质上有哪些候选属性。
        // 舞台 shader 的染色通道。运行时枚举 shader 属性表实测：
        //   Gallop/3D/Live/Stage/DefaultNoAmbient        -> _MulColor0
        //   Gallop/3D/Live/Stage/DefaultEnvMapNoAmbient  -> _MulColor0, _AddColor
        //   Gallop/3D/Live/Stage/LightBlinkBlend         -> _BlinkLightColor（BlinkLight 轨道的，不归 BgColor1）
        //   Gallop/3D/Live/Stage/StageTransmittedLightMask -> 无
        // 和 WashLight 的 MulColor0 / UVScrollLight 的 mulColor1 是同一套命名体系。
        // 刻意不含 _AmbientColor：那个通道归 BgColor2（OnBgColor2Update），两条轨道不该抢同一个属性。
        private static readonly string[] kStageBgColor1Props = { "_MulColor0" };

        /// <summary>解析结果：目标 Renderer + 它实际拥有的那个颜色属性。</summary>
        private struct StageBgColorTarget
        {
            public Renderer renderer;
            public string prop;
            public bool hasColorPower;
        }

        private readonly Dictionary<string, List<StageBgColorTarget>> _bgColor1StageCache =
            new Dictionary<string, List<StageBgColorTarget>>();

        private void OnBgColor1Update(ref BgColor1UpdateInfo updateInfo)
        {
            if (!string.IsNullOrEmpty(updateInfo.TimelineName) &&
                !LiveTimelineControl.CharaBgColorNames.Contains(updateInfo.TimelineName))
            {
                ApplyBgColor1ToStage(ref updateInfo);
                return;
            }

            foreach (var locator in _liveTimelineControl.liveCharactorLocators)
            {
                var EFlags = (LiveCharaPositionFlag)updateInfo.flags;
                if (locator == null || (updateInfo.flags != 0 && !EFlags.hasFlag(locator.liveCharaStandingPosition)) || locator is not LiveTimelineCharaLocator charaLocator) continue;
                var container = charaLocator.UmaContainer;
                if (!container) continue;
                // 必须 Get→改→Set，否则会把 GlobalLight 同一帧写进去的 rim 属性整块抹掉。
                // 详见 _propBlock 的注释。
                var block = PropBlock;
                foreach (var renderer in container.Renderers)
                {
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_CharaColor",      updateInfo.color);
                    block.SetColor("_ToonDarkColor",   updateInfo.toonDarkColor);
                    block.SetColor("_ToonBrightColor", updateInfo.toonBrightColor);
                    block.SetColor("_OutlineColor",    updateInfo.outlineColor);
                    block.SetFloat("_Saturation",      updateInfo.Saturation);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>
        /// BgColor1 的舞台物件分支。轨道组名可能指向 GameObject，也可能指向材质名
        /// （uvScrollLightList 用的就是材质名），所以两种都试。
        /// </summary>
        private void ApplyBgColor1ToStage(ref BgColor1UpdateInfo updateInfo)
        {
            if (_stageController == null) return;

            string key = updateInfo.TimelineName;
            if (!_bgColor1StageCache.TryGetValue(key, out var targets))
            {
                targets = ResolveStageTargets(key, kStageBgColor1Props, out var report);
                _bgColor1StageCache[key] = targets;
                LogBgColor1Resolution(key, targets, report);
            }
            if (targets.Count == 0) return;

            var block = PropBlock;

            foreach (var t in targets)
            {
                if (t.renderer == null) continue;
                t.renderer.GetPropertyBlock(block);
                block.SetColor(t.prop, updateInfo.color);

                // ⚠ 不要在这里写 _ColorPower —— 缺 ground truth。
                // 已核实的只有「这些 shader 上 _ColorPower 这个 Float 属性存在」；
                // **没有任何证据表明 BgColor1 的 power 字段映射到它**，它也可能是乘在
                // 材质原值上、或者对应完全不同的东西。2026-08-05 试写过一版，无法判断对错，
                // 遂按「缺依据即不做」撤回。t.hasColorPower 保留，供将来确认后启用。
                //
                // （注：曾长期以为「地板 plane_000 / stage_object_001 / specular_002 发白」，
                //   2026-08-05 查明白的其实是**叠在同一位置的 mirror_a** —— 它用
                //   Cygames/MirrorAndShadow/ReceiveMirror，_ReflectionRate 默认 1.0 而
                //   _ReflectionTex 从没赋值，Unity 代入白贴图 = 满强度白反射。
                //   实现 Gallop.MirrorReflection 后已解决。与 _ColorPower 无关。）

                t.renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// 枚举 shader 真正声明的 Color 属性。
        /// 注意：材质在 bundle 里的 m_SavedProperties 会保留历史属性，和当前 shader 声明的不是一回事，
        /// 判断能写什么必须问 shader，不能看材质存档表。
        /// </summary>
        private static string DescribeShaderColors(Shader sh)
        {
            if (sh == null) return "<null shader>";

            var colors = new List<string>();
            int count = sh.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (sh.GetPropertyType(i) == ShaderPropertyType.Color)
                    colors.Add(sh.GetPropertyName(i));
            }
            return $"{sh.name} 颜色属性: {(colors.Count > 0 ? string.Join(",", colors) : "<无>")}";
        }

        /// <summary>把一批渲染器用到的 shader 去重列出，供各 handler 的首次日志核对写对了通道。</summary>
        private static string DescribeRendererShaders(IEnumerable<Renderer> renderers)
        {
            var shaders = new List<string>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string info = DescribeShaderColors(mat.shader);
                    if (!shaders.Contains(info)) shaders.Add(info);
                }
            }
            return string.Join(" | ", shaders);
        }

        /// <summary>
        /// <see cref="ResolveStageTargets"/> 的诊断副产物：找到几个同名对象、它们下面有几个
        /// Renderer、这些 Renderer 用的材质和 shader。解析失败时靠它把原因拆开说清楚。
        /// 原先是四个实例字段被 resolve 当输出参数改写，日志再读回来 —— 隐式耦合，改成显式返回。
        /// </summary>
        private struct StageResolveReport
        {
            public bool foundObject;
            public int objectCount;
            public int rendererCount;
            public List<string> materials;
        }

        /// <summary>用 sharedMaterials 探测（不会实例化材质），只保留确实有候选属性的 Renderer。</summary>
        private List<StageBgColorTarget> ResolveStageTargets(string timelineName, string[] props,
                                                             out StageResolveReport report)
        {
            var result = new List<StageBgColorTarget>();
            report = new StageResolveReport { materials = new List<string>() };

            // 按 Transform 名遍历整个舞台层级。
            // 不能只查 StageObjectMap：它按名字去重，而观众群里同名对象有几十上百个
            // （mob_a000 实测 66 个），只取第一个会漏掉绝大多数。
            // StageObjectMap 里的对象本身也在这个层级里，所以这一趟已经覆盖它。
            var seen = new HashSet<Renderer>();
            var byName = new List<Renderer>();
            foreach (var tr in _stageController.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(tr.name.Replace("(Clone)", ""), timelineName, StringComparison.OrdinalIgnoreCase))
                    continue;
                report.foundObject = true;
                report.objectCount++;
                foreach (var r in tr.GetComponentsInChildren<Renderer>(true))
                    if (r != null && seen.Add(r)) byName.Add(r);
            }

            // StageObjectMap 兜底：万一有对象不在 _stageController 层级下。
            if (_stageController.StageObjectMap.TryGetValue(timelineName, out var go) && go != null)
            {
                report.foundObject = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (r != null && seen.Add(r)) byName.Add(r);
            }

            report.rendererCount = byName.Count;
            foreach (var r in byName)
            {
                if (r == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) { report.materials.Add("<null mat>"); continue; }
                    string info = $"{mat.name}[{DescribeShaderColors(mat.shader)}]";
                    if (!report.materials.Contains(info)) report.materials.Add(info);
                }
            }
            if (byName.Count > 0)
            {
                CollectTargets(byName, result, props);
                if (result.Count > 0) return result;
            }

            // 退回按材质名匹配（uvScrollLightList 用的就是材质名）
            var matched = new List<Renderer>();
            foreach (var r in _stageController.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (mat.name.IndexOf(timelineName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        report.foundObject = true;
                        matched.Add(r);
                        break;
                    }
                }
            }
            CollectTargets(matched, result, props);
            return result;
        }

        private static void CollectTargets(IEnumerable<Renderer> renderers, List<StageBgColorTarget> result,
                                           string[] props)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string hit = null;
                    foreach (string p in props)
                    {
                        if (mat.HasProperty(p)) { hit = p; break; }
                    }
                    if (hit != null)
                    {
                        result.Add(new StageBgColorTarget
                        {
                            renderer = r,
                            prop = hit,
                            hasColorPower = mat.HasProperty(kColorPowerProp),
                        });
                        break;
                    }
                }
            }
        }

        private static void LogBgColor1Resolution(string timelineName, List<StageBgColorTarget> targets,
                                                  StageResolveReport report)
        {
            if (targets.Count == 0)
            {
                if (!report.foundObject)
                    Debug.LogWarning($"[BgColor1] '{timelineName}'：整个舞台层级里找不到同名对象，材质名也匹配不上");
                else if (report.rendererCount == 0)
                    Debug.LogWarning($"[BgColor1] '{timelineName}'：找到 {report.objectCount} 个同名对象，但它们下面没有任何 Renderer");
                else
                    Debug.LogWarning($"[BgColor1] '{timelineName}'：找到 {report.objectCount} 个对象 / {report.rendererCount} 个 Renderer，" +
                                     $"但材质无候选属性。材质[shader]: {string.Join(" | ", report.materials)}");
                return;
            }

            var props = new HashSet<string>();
            foreach (var t in targets) props.Add(t.prop);

            // 成功分支也打印 shader 真实声明的颜色属性，便于核对写对了通道。
            Debug.Log($"[BgColor1] '{timelineName}' -> {targets.Count} 个 Renderer，写入 {string.Join(",", props)}；" +
                      $"{DescribeRendererShaders(targets.Select(t => t.renderer))}");
        }

        /// <summary>
        /// PostFilm (39)。三条轨道各占一层，参数写进 PostFilmRendererFeature.Layers，
        /// 由 RendererFeature 在 AfterRenderingPostProcessing 做全屏叠加。
        /// 该 Feature 必须先加进 UMAUniversalRenderPipelineAsset_Renderer.asset 才会生效。
        /// </summary>
        private void OnPostFilmUpdate(ref PostFilmUpdateInfo info)
        {
            int i = info.layerIndex;
            if (i < 0 || i >= PostFilmRendererFeature.kLayerCount) return;

            PostFilmRendererFeature.Layers[i] = new PostFilmRendererFeature.LayerState
            {
                enable = info.enable,
                filmMode = info.filmMode,
                colorType = info.colorType,
                filmPower = info.filmPower,
                color0 = info.color0,
                color1 = info.color1,
                color2 = info.color2,
                color3 = info.color3,
                filmOffset = info.filmOffsetParam,
                filmScale = info.filmScale,
                rollAngle = info.rollAngle,
                filmOption = info.filmOptionParam,
            };

            LogPostFilmOnce(i, ref info);
        }

        private readonly HashSet<int> _postFilmLogged = new HashSet<int>();

        private void LogPostFilmOnce(int layer, ref PostFilmUpdateInfo info)
        {
            if (!_postFilmLogged.Add(layer)) return;
            Debug.Log($"[PostFilm] layer{layer} filmMode={info.filmMode} colorType={info.colorType} " +
                      $"power={info.filmPower:F3} layerMode={info.layerMode} colorBlend={info.colorBlend} " +
                      $"movieResId={info.movieResId} color0={info.color0} scale={info.filmScale} " +
                      $"offset={info.filmOffsetParam} roll={info.rollAngle:F3} option={info.filmOptionParam}");
        }

        private void OnCameraSwitcherUpdate(int cameraIndex_)
        {
            if (cameraIndex_ < 0)
                _activeCameraIndex = 0;
            else if (cameraIndex_ < kTimelineCameraIndices.Length)
                _activeCameraIndex = kTimelineCameraIndices[cameraIndex_];
        }

        // BgColor2 的通道。_MulColor0 归 BgColor1，两条轨道不能抢同一个属性。
        private static readonly string[] kStageBgColor2Props = { "_AmbientColor" };

        private readonly Dictionary<string, List<StageBgColorTarget>> _bgColor2StageCache =
            new Dictionary<string, List<StageBgColorTarget>>();
        private List<StageBgColorTarget> _bgColor2AllTargets;

        /// <summary>
        /// BgColor2。原实现有三个问题，这里修掉两个半：
        ///
        /// 1. **组名被完全丢弃**（已修）。dispatcher 现在把 TimelineName 传下来了。
        /// 2. **每帧 r.materials**（已修）。那个 getter 每次调用都会实例化材质副本并新分配数组，
        ///    这里是「整个舞台的渲染器 × 每帧 × 最多 15 个组」，是全项目最重的一处。
        ///    改用 sharedMaterials 解析一次 + MaterialPropertyBlock 写入，和 BgColor1
        ///    舞台分支、BlinkLight 的做法统一。
        /// 3. **组名到底指向什么，仍然未知**（未修，缺 ground truth）。
        ///
        /// 关于 3：全语料 15 首里出现过的组名只有 BgWashA..BgWashO、LaserA..LaserC、BgColor2。
        /// 已核实这些**既不是 GameObject 名，也不是 _stageObjectUnits 的 unit 名**
        /// （已下载的 live10132/10149/10151 三个舞台的 unit 名分别是
        ///  blinklight_washlight_wall_a_vertical_000_set / neonsign / stage000_shop 这类具体名字），
        /// BlinkLight 的键里也没有回指 BgColor2 的索引字段。它和 BgColor1 的
        /// BgBL / FollowSpotColor / Shadow 属于同一类「非物件名的组」，映射关系尚未找到。
        ///
        /// 所以这里**不猜**：名字能解析到渲染器就只写那些，解析不到就退回原来的全舞台写入
        /// 并打一次警告。行为上不会比改动前更差，映射一旦查明，只需删掉 fallback 分支。
        /// </summary>
        private void OnBgColor2Update(ref BgColor2UpdateInfo updateInfo)
        {
            if (_stageController == null) return;
            Color c = Color.Lerp(updateInfo.color1, updateInfo.color2, updateInfo.value);

            string key = updateInfo.TimelineName ?? string.Empty;
            if (!_bgColor2StageCache.TryGetValue(key, out var targets))
            {
                targets = string.IsNullOrEmpty(key)
                    ? new List<StageBgColorTarget>()
                    : ResolveStageTargets(key, kStageBgColor2Props, out _);
                _bgColor2StageCache[key] = targets;
                if (targets.Count == 0)
                    Debug.LogWarning($"[BgColor2] 组名 '{key}' 解析不到任何渲染器，退回全舞台写入。" +
                                     $"该组与其它未解析的组会互相覆盖 —— 映射关系待查，见 LIVE_TRACKS.md 「已知未解问题」");
                else
                    Debug.Log($"[BgColor2] 组名 '{key}' → {targets.Count} 个渲染器");
            }

            // 名字解析成功：只写这一组。
            if (targets.Count > 0)
            {
                WriteAmbient(targets, c);
                return;
            }

            // Fallback：维持改动前的全舞台语义（缺映射依据，不改画面行为），但只解析一次。
            if (_bgColor2AllTargets == null)
            {
                _bgColor2AllTargets = new List<StageBgColorTarget>();
                CollectTargets(_stageController.GetComponentsInChildren<Renderer>(true),
                               _bgColor2AllTargets, kStageBgColor2Props);
            }
            WriteAmbient(_bgColor2AllTargets, c);
        }

        private void WriteAmbient(List<StageBgColorTarget> targets, Color c)
        {
            var block = PropBlock;
            foreach (var t in targets)
            {
                if (t.renderer == null) continue;
                t.renderer.GetPropertyBlock(block);
                block.SetColor(t.prop, c);
                t.renderer.SetPropertyBlock(block);
            }
        }

        private void OnGlobalFogUpdate(LiveTimelineGlobalFogData fogData, LiveTimelineKeyGlobalFogData keyData)
        {
            if (keyData == null) return;
            RenderSettings.fog = keyData.isDistance || keyData.isHeight || keyData.fogMode != 0;
            RenderSettings.fogColor = keyData.color;
            RenderSettings.fogMode = (FogMode)keyData.fogMode;
            RenderSettings.fogDensity = keyData.expDensity;
            RenderSettings.fogStartDistance = keyData.start;
            RenderSettings.fogEndDistance = keyData.end;
        }

        private void OnSpotlight3dUpdate(LiveTimelineSpotlight3dData spotData, LiveTimelineKeySpotlight3dData keyData)
        {
            if (keyData == null || _stageController == null) return;

            if (!_stageController.StageObjectMap.TryGetValue(keyData.assetName, out var go)) return;

            go.SetActive(keyData.isActive);
            if (!keyData.isActive) return;

            Vector3 basePos = Vector3.zero;
            if (keyData.characterIndex >= 0 && keyData.characterIndex < CharaContainerScript.Count)
                basePos = CharaContainerScript[keyData.characterIndex].transform.position;

            go.transform.position = basePos + keyData.position;
            go.transform.eulerAngles = keyData.rotation;
            go.transform.localScale = keyData.scale;

            // 之前写的是 _Color —— 枚举 shader bundle 里全部 133 个 Gallop/3D/{Live,Bg,Stage}
            // shader 后确认，Gallop/3D/Live/Stage/* 下**没有任何一个** shader 声明 _Color
            // （整个 bundle 里只有 Bg/BgShadowOnly 和 Bg/RedAlphaGreenColorShadowFogUVScroll 有，
            //  都和灯柱无关）。灯柱用的是 StageBeamLight 系列，通道是 _MulColor0/_MulColor1 +
            // _ColorPower。所以颜色写入一直是空操作，而 _ColorPower 是有效的 ——
            // 表现为「材质原色 × 关键帧亮度」：亮度跟着音乐动，颜色永远不对。
            // 这和 BlinkLight 之前那个 _Color bug 是同一份，复用它的 shader 探测逻辑。
            if (!_spotlightRenderers.TryGetValue(go, out var renderers))
            {
                renderers = go.GetComponentsInChildren<Renderer>(true);
                _spotlightRenderers[go] = renderers;
            }

            var block = PropBlock;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                BlinkTarget t = ResolveBlinkTarget(r);
                if (t.colorProp == null) continue;

                r.GetPropertyBlock(block);
                if (t.hasColorPower)
                {
                    // 亮度是独立通道，不要折进颜色里，合成交给 shader。
                    block.SetColor(t.colorProp, keyData.color);
                    block.SetFloat(kColorPowerProp, keyData.colorPower);
                }
                else
                {
                    block.SetColor(t.colorProp, keyData.color * keyData.colorPower);
                }
                r.SetPropertyBlock(block);
            }
            // TODO: localHeight / targetCameraType / targetCameraIndex 未实现。
        }

        private readonly Dictionary<GameObject, Renderer[]> _spotlightRenderers =
            new Dictionary<GameObject, Renderer[]>();

        /// <summary>按材质名解析出来的 UVScroll 目标：渲染器 + 该材质原始的 _MainTex 缩放。</summary>
        private struct UVScrollTarget
        {
            public Renderer renderer;
            public Vector2 baseScale;
        }

        private readonly Dictionary<string, List<UVScrollTarget>> _uvScrollTargets =
            new Dictionary<string, List<UVScrollTarget>>();
        private readonly HashSet<string> _uvScrollLoggedShaders = new HashSet<string>();

        private void OnUVScrollLightUpdate(LiveTimelineUVScrollLightData data, LiveTimelineKeyUVScrollLightData keyData)
        {
            if (keyData == null || _stageController == null) return;

            if (!_uvScrollAccum.ContainsKey(data.name))
                _uvScrollAccum[data.name] = Vector2.zero;
            // TODO: 这里用 Time.deltaTime 累积，拖动进度条/暂停后滚动相位会和时间轴对不上。
            // 改成按 currentLiveTime 积分才是确定性的，但「相位从歌曲起点算还是从关键帧起点算」
            // 没有依据，先不动 —— 换算错了比现在更难发现。
            _uvScrollAccum[data.name] += new Vector2(keyData.scrollSpeedX, keyData.scrollSpeedY) * Time.deltaTime;
            Vector2 totalOffset = new Vector2(keyData.scrollOffsetX, keyData.scrollOffsetY) + _uvScrollAccum[data.name];

            // 首次按材质名解析并缓存。原实现每帧遍历整个舞台的 Renderer 并访问 r.materials，
            // 那个 getter 每次都会实例化材质副本 + 新分配数组。
            if (!_uvScrollTargets.TryGetValue(data.name, out var targets))
            {
                targets = new List<UVScrollTarget>();
                foreach (var r in _stageController.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        if (mat.name.Replace(" (Instance)", "") != data.name) continue;
                        targets.Add(new UVScrollTarget
                        {
                            renderer  = r,
                            baseScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one,
                        });
                        if (!_uvScrollLoggedShaders.Add(data.name))
                            break;
                        // 把实际命中的 shader 记一次，取代那个猜测性的兜底分支：
                        // 万一将来碰上没有 _ColorPower 的材质，这条日志能直接看出来。
                        Debug.Log($"[UVScrollLight] '{data.name}' shader={mat.shader?.name} " +
                                  $"_ColorPower={mat.HasProperty(kColorPowerProp)} _MulColor1={mat.HasProperty("_MulColor1")}");
                        break;
                    }
                }
                _uvScrollTargets[data.name] = targets;
                Debug.Log($"[UVScrollLight] 材质 '{data.name}' → {targets.Count} 个渲染器");
            }
            if (targets.Count == 0) return;

            var block = PropBlock;

            foreach (var t in targets)
            {
                if (t.renderer == null) continue;
                t.renderer.GetPropertyBlock(block);

                // MPB 没有 SetTextureOffset，等价写法是 _MainTex_ST = (scaleX, scaleY, offsetX, offsetY)。
                block.SetVector("_MainTex_ST",
                    new Vector4(t.baseScale.x, t.baseScale.y, totalOffset.x, totalOffset.y));

                // 之前写的是 _Color。实测这些材质用的是
                //   Gallop/3D/Live/Stage/LightAdd1_UV
                //   Gallop/3D/Live/Stage/StageLightAdd1_UVAlphaMask_TransmittedLightMask
                // 两者的属性都是 _MulColor0/_MulColor1/_ColorPower/_ColorPowerMultiply，
                // **没有 _Color** —— 全部 499 个 shader 里带 _Color 的舞台 shader 只有
                // BgShadowOnly 和 RedAlphaGreenColorShadowFogUVScroll，都跟这里无关。
                // 所以颜色写入一直是空操作。这是 BlinkLight、Spotlight3d 之后的同一个 bug 第三例。
                // 字段名 mulColor0/mulColor1/colorPower 与属性名逐字对应，映射关系是硬的。
                // 亮度是独立通道，不折进颜色里，合成交给 shader。
                // 不做「没有 _ColorPower 就乘进颜色」的兜底：实测这条轨道的目标材质
                // （LightAdd1_UV / StageLightAdd1_UVAlphaMask_TransmittedLightMask）都有该属性，
                // 兜底分支永远跑不到，却会在真跑到时悄悄产生另一种画面。
                // MPB 往 shader 没有的属性写入本身是无害空操作，不需要守卫。
                block.SetColor("_MulColor0", keyData.mulColor0);
                block.SetColor("_MulColor1", keyData.mulColor1);
                block.SetFloat(kColorPowerProp, keyData.colorPower);

                t.renderer.SetPropertyBlock(block);
            }
            // TODO: ColorType0/1、CharacterIndex0/1、IsColorBlend0/1、ColorBlendRate0/1、
            //       AltCharaColor0/1、loopType/loopCount 未实现。
        }

        private void OnChromaticAberrationUpdate(LiveTimelineChromaticAberrationData data, LiveTimelineKeyChromaticAberrationData keyData)
        {
            if (keyData == null || _postProcessVolume == null) return;
            if (!_postProcessVolume.profile.TryGet<ChromaticAberration>(out var fx)) return;
            bool on = keyData.isEnable != 0;
            fx.active = on;
            if (on)
                fx.intensity.Override(keyData.power);
            // TODO: keyData.redOffset/greenOffset/blueOffset — per-channel displacement,
            // not expressible in URP built-in ChromaticAberration. clip, effectType unused.
        }

        // HdrBloom (38) 没有 handler，这是有意的：全语料 59 首的 hdrBloomKeys 全部为空
        // （`tools/` 全量扫描 + song 1177 的 dump 都是 0），而字段到 URP Bloom 的映射也无从核对。
        // 数据层（LiveTimelineHdrBloomData / hdrBloomKeys / OnUpdateHdrBloom）保留着，
        // 哪天真有歌带数据，接一个 handler 即可。

        private void OnColorCorrectionUpdate(LiveTimelineColorCorrectionData data, LiveTimelineKeyColorCorrectionData keyData)
        {
            if (keyData == null || _postProcessVolume == null) return;

            bool on = keyData.enable != 0;

            if (_postProcessVolume.profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.active = on;
                if (on)
                    // game: 1.0 = neutral; URP: 0 = neutral, range -100..100
                    ca.saturation.Override((keyData.saturation - 1f) * 100f);
            }

            if (_postProcessVolume.profile.TryGet<ColorCurves>(out var cc))
            {
                cc.active = on;
                if (on && keyData.redCurve != null)
                {
                    cc.red.Override(new TextureCurve(keyData.redCurve.keys, 0f, false, new Vector2(0f, 1f)));
                    cc.green.Override(new TextureCurve(keyData.greenCurve.keys, 0f, false, new Vector2(0f, 1f)));
                    cc.blue.Override(new TextureCurve(keyData.blueCurve.keys, 0f, false, new Vector2(0f, 1f)));
                }
            }
            // TODO: depthRedCurve/depthGreenCurve/depthBlueCurve — depth-based curves, no URP equivalent.
            // blendCurve, mode, selective, keyColor, targetColor unused.
        }

        private void OnBlinkLightUpdate(LiveTimelineBlinkLightData data, LiveTimelineKeyBlinkLightData keyData)
        {
            if (keyData == null || _stageController == null) return;
            if (!_stageController.StageObjectMap.TryGetValue(data.name, out var go)) return;
            go.SetActive(true);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var block = PropBlock;
            int noProp = 0;

            // color0Array/powerArray 恒为 10 项，是「调色板」而不是每盏灯一项。
            //
            // 槽位映射规则（2026-08-05 逆出来的）：**槽位号 = 渲染器自身或最近祖先的
            // `lightNNN_` 名字前缀**。live10149 全组实测，每一组里「前缀种数 N」与
            // 「槽位 0..N-1 的去重颜色数」精确相等，且槽位 N..9 无一例外是白色填充：
            //
            //   mirrorball_flarelight  N=3  粉/蓝/黄        槽3..9 全白
            //   wash_truss_a / _b      N=4  黄/绿/蓝/紫     槽4..9 全白
            //   glow_object            N=2  橙/绿           槽2..9 全白
            //   wash_ground_b          N=1  单色            槽1..9 全白
            //   audience_light         N=9  本来就单色      槽9   全白
            //
            // 此前统一取第 0 槽，于是多色组被涂成单色 —— 迪斯科灯球整个发粉红
            // （它的槽 0 是 (1.0,0.53,0.71)），truss wash 也丢掉了黄绿蓝紫四色。
            float baseElapsed = _liveTimelineControl.currentLiveTime - keyData.frame / 60f - keyData.waitTime;
            float cycle = keyData.turnOnTime + keyData.keepTime + keyData.turnOffTime + keyData.intervalTime;

            bool groupHasSlots = GroupHasSlottedRenderers(data.name, renderers);

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                BlinkTarget t = ResolveBlinkTarget(r);

                // 没有槽位前缀 = 不是调色板灯（镜面球球体、灯具外壳等），别抢别人的通道。
                if (t.slot < 0 && groupHasSlots) continue;
                int slot = t.slot < 0 ? 0 : t.slot;

                Color col = (keyData.color0Array != null && keyData.color0Array.Length > 0)
                    ? keyData.color0Array[Mathf.Clamp(slot, 0, keyData.color0Array.Length - 1)]
                    : Color.white;
                float power = (keyData.powerArray != null && keyData.powerArray.Length > 0)
                    ? keyData.powerArray[Mathf.Clamp(slot, 0, keyData.powerArray.Length - 1)]
                    : 1f;

                if (keyData.pattern != 0)
                {
                    // pattern != 0 时逐灯错开相位，做出滚动闪烁（U 闪 → M 闪 → A 闪）。
                    // pattern 的确切语义还没逆出来，这里只区分「同步」与「滚动」两种；
                    // 若方向或速度不对，调这里的相位公式即可。
                    float phase = (cycle > 0f && renderers.Length > 1)
                        ? cycle * i / renderers.Length
                        : 0f;
                    power *= ComputeBlinkIntensity(keyData, baseElapsed - phase);
                }

                // 这些灯的 shader 是 Gallop/3D/Live/Stage/LightBlinkBlend，唯一的 Color 属性是
                // _BlinkLightColor。之前写的是 _Color —— 该 shader 上没有这个属性，写入是空操作，
                // 于是 _BlinkLightColor 一直是默认值，灯全渲染成黑块。
                if (t.colorProp == null) { noProp++; continue; }

                r.GetPropertyBlock(block);
                if (t.hasColorPower)
                {
                    // shader 自带独立的亮度通道，颜色和强度分开写，合成交给 shader。
                    block.SetColor(t.colorProp, col);
                    block.SetFloat(kColorPowerProp, power);
                }
                else
                {
                    // BgMirrorBall 这类只有 _MulColor0、没有 _ColorPower，只能把强度乘进颜色。
                    block.SetColor(t.colorProp, col * power);
                }
                r.SetPropertyBlock(block);
            }
            LogBlinkLightOnce(data.name, keyData, renderers, noProp);
            // TODO: color1Array、LightBlendMode、isReverseHueArray 尚未实现。
        }

        // 舞台灯光 shader 的通道（枚举 shader 属性表实测，见 CLAUDE.md）：
        //   LightBlinkBlend               _BlinkLightColor + _ColorPower
        //   DefaultNoAmbient              _MulColor0       + _ColorPower
        //   DefaultEnvMapNoAmbient        _MulColor0       + _ColorPower (+_AddColor)
        //   DefaultTransparentNoAmbient   _MulColor0       + _ColorPower (+_AmbientColor)
        //   BgMirrorBall                  _MulColor0       （无 _ColorPower）
        //   StageMirrorBallShine / StageTransmittedLightMask  完全没有颜色属性
        private static readonly string[] kBlinkColorProps = { "_BlinkLightColor", "_MulColor0", "_Color" };
        private const string kColorPowerProp = "_ColorPower";

        private struct BlinkTarget
        {
            public string colorProp;
            public bool hasColorPower;
            /// <summary>调色板槽位 = 自身或最近祖先的 `lightNNN_` 名字前缀。</summary>
            public int slot;
        }

        /// <summary>
        /// 从 "light003_xxx" / "alpha001_xxx" 取出槽位号；不匹配返回 -1。
        /// 形状是「小写词 + 数字 + 下划线」。
        ///
        /// 一开始只认 "light" 前缀，结果镜面球 83 个渲染器里 47 个是 "alphaNNN_"（发光贴片），
        /// 全落回槽 0，整个球还是粉红的。全舞台实测只存在 light(702) 和 alpha(207) 两种前缀词，
        /// 放宽成通配后覆盖率与调色板用量吻合：
        ///   mirrorball_flarelight  槽0/1/2 各 24  ↔ 3 色
        ///   glow_ramp              槽0/1/2 20/19/19 ↔ 3 色
        ///   glow_object            槽0/1 33/34    ↔ 2 色
        ///   audience_light         槽0..8 各 10   ↔ 9 槽
        /// 剩下未命中的都在单槽组里，落回槽 0 本来就是对的。
        ///
        /// 手写而不用 Regex：每个 Renderer 只解析一次并缓存，但会走一遍祖先链，
        /// 570 个渲染器的量级没必要再加正则的分配。
        /// </summary>
        private static int ParseLightSlotPrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int i = 0;
            while (i < name.Length && name[i] >= 'a' && name[i] <= 'z') i++;
            if (i == 0) return -1;                       // 必须以小写词开头
            int value = 0, digits = 0;
            while (i < name.Length && name[i] >= '0' && name[i] <= '9')
            {
                value = value * 10 + (name[i] - '0');
                i++; digits++;
            }
            if (digits == 0 || i >= name.Length || name[i] != '_') return -1;
            return value;
        }

        /// <summary>没有前缀返回 -1 —— 那不是调色板灯，见 _blinkGroupHasSlots 的说明。</summary>
        private static int ResolveBlinkSlot(Renderer r)
        {
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                int s = ParseLightSlotPrefix(t.name);
                if (s >= 0) return s;
            }
            return -1;
        }

        /// <summary>
        /// 某个 BlinkLight 组里是否存在带槽位前缀的渲染器。
        ///
        /// 一个组里混着两种东西。以 mirrorball_flarelight 为例，83 个渲染器里既有
        /// **闪光片**（`light000_*` / `alpha001_*`，吃调色板颜色），也有**球体本身**
        /// （`mirrorball_b_000`，用 Gallop/3D/Bg/BgMirrorBall）。
        ///
        /// 球体的 `_MulColor0` 是 **BgColor1** 的通道 —— 它有 `mirrorball_b_000..004`
        /// 五个组，给球体写的是 (0.21,0.22,0.32) → (1,1,1)，也就是原版那个银白色球。
        /// 但 BlinkLight 在 AlterLateUpdate 里排在 BgColor1 之后，之前会把调色板的粉色
        /// 盖上去，于是球被涂成粉红 —— 和原版截图的银球完全不符。
        ///
        /// 所以：**组里只要有带前缀的渲染器，就只染带前缀的那些**，没前缀的留给它真正的
        /// 所有者（BgColor1）。整组都没前缀时（单色组）维持旧行为，全部按槽 0 处理。
        /// </summary>
        private readonly Dictionary<string, bool> _blinkGroupHasSlots = new Dictionary<string, bool>();

        private bool GroupHasSlottedRenderers(string groupName, Renderer[] renderers)
        {
            if (_blinkGroupHasSlots.TryGetValue(groupName, out bool cached)) return cached;
            bool any = false;
            foreach (var r in renderers)
            {
                if (r != null && ResolveBlinkTarget(r).slot >= 0) { any = true; break; }
            }
            _blinkGroupHasSlots[groupName] = any;
            return any;
        }

        private readonly Dictionary<Renderer, BlinkTarget> _blinkPropCache = new Dictionary<Renderer, BlinkTarget>();
        private readonly HashSet<string> _blinkLoggedGroups = new HashSet<string>();

        private BlinkTarget ResolveBlinkTarget(Renderer r)
        {
            if (_blinkPropCache.TryGetValue(r, out BlinkTarget cached)) return cached;

            BlinkTarget t = default;
            t.slot = ResolveBlinkSlot(r);
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                foreach (string p in kBlinkColorProps)
                {
                    if (mat.HasProperty(p)) { t.colorProp = p; break; }
                }
                if (t.colorProp != null)
                {
                    t.hasColorPower = mat.HasProperty(kColorPowerProp);
                    break;
                }
            }
            _blinkPropCache[r] = t;
            return t;
        }

        /// <summary>每个 BlinkLight 组只打一次：pattern / 数组长度 / renderer 数 / shader 属性，用于设计逐灯相位。</summary>
        private void LogBlinkLightOnce(string groupName, LiveTimelineKeyBlinkLightData k, Renderer[] renderers, int noProp)
        {
            if (!_blinkLoggedGroups.Add(groupName)) return;

            Debug.Log($"[BlinkLight] '{groupName}' renderers={renderers.Length} 无可写颜色属性={noProp} " +
                      $"pattern={k.pattern} colorType={k.colorType} blendMode={k.LightBlendMode} " +
                      $"color0Array={k.color0Array?.Length ?? -1} color1Array={k.color1Array?.Length ?? -1} " +
                      $"powerArray={k.powerArray?.Length ?? -1} reverseHue={k.isReverseHueArray?.Length ?? -1} " +
                      $"power={k.powerMin}~{k.powerMax} loop={k.loopCount} " +
                      $"wait={k.waitTime} on={k.turnOnTime} keep={k.keepTime} off={k.turnOffTime} interval={k.intervalTime}; " +
                      $"{DescribeRendererShaders(renderers)}");
        }

        private static float ComputeBlinkIntensity(LiveTimelineKeyBlinkLightData keyData, float elapsed)
        {
            if (elapsed < 0f) return keyData.powerMin;

            float cycleDuration = keyData.turnOnTime + keyData.keepTime + keyData.turnOffTime + keyData.intervalTime;
            if (cycleDuration <= 0f) return keyData.powerMax;

            if (keyData.loopCount > 0 && elapsed >= cycleDuration * keyData.loopCount)
                return keyData.powerMin;

            float t = elapsed % cycleDuration;

            if (t < keyData.turnOnTime)
                return Mathf.Lerp(keyData.powerMin, keyData.powerMax, keyData.turnOnTime > 0f ? t / keyData.turnOnTime : 1f);
            t -= keyData.turnOnTime;

            if (t < keyData.keepTime)
                return keyData.powerMax;
            t -= keyData.keepTime;

            if (t < keyData.turnOffTime)
                return Mathf.Lerp(keyData.powerMax, keyData.powerMin, keyData.turnOffTime > 0f ? t / keyData.turnOffTime : 1f);

            return keyData.powerMin; // intervalTime: off
        }

        private void OnWashLightUpdate(LiveTimelineWashLightData data, LiveTimelineKeyWashLightData keyData)
        {
            if (keyData == null || _stageController == null) return;
            if (!_stageController.StageObjectMap.TryGetValue(data.name, out var go)) return;
            go.SetActive(true);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var mat in r.materials)
                {
                    mat.SetFloat("_ProjectorColorPower", keyData.CameraProjectionColorPower);
                    // TODO: RaycastDistance, CameraProjectionSide unused.
                    // _ProjectorMulColor0 (wash color) has no corresponding field in keyData.
                }
        }

        private void OnLaserUpdate(LiveTimelineLaserData data, LiveTimelineKeyLaserData keyData)
        {
            if (keyData == null || _stageController == null) return;
            if (!_stageController.StageObjectMap.TryGetValue(data.name, out var go)) return;
            go.SetActive(true);
            go.transform.localPosition = keyData.objectPosition;
            go.transform.localEulerAngles = keyData.objectRotate;
            go.transform.localScale = keyData.objectScale;
            // TODO: incomplete — keyData.blink/blinkPeriod (SetActive flicker), degLaserPitch (beam angle),
            // RaycastDistance (beam length via scale), formation/posInterval (multi-laser layout) unused.
        }

        private void OnVolumeLightUpdate(LiveTimelineVolumeLightData data, LiveTimelineKeyVolumeLightData keyData)
        {
            // SunShafts component not present in this build — data deserialized only
        }

        private void OnLightShaftsUpdate(LiveTimelineLightShaftsData data, LiveTimelineKeyLightShaftsData keyData)
        {
            // LightShaftsController component not present in this build — data deserialized only
        }

        private void OnParticleUpdate(LiveTimelineParticleData data, LiveTimelineKeyParticleData keyData)
        {
            if (keyData == null || _stageController == null) return;
            foreach (var ps in _stageController.GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.gameObject.name != data.name) continue;
                var emission = ps.emission;
                emission.rateOverTime = keyData.emissionRate;
            }
        }

        private void OnParticleGroupUpdate(LiveTimelineParticleGroupData data, LiveTimelineKeyParticleGroupData keyData)
        {
            if (keyData == null || _stageController == null) return;
            foreach (var ps in _stageController.GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.gameObject.name != data.name) continue;
                var emission = ps.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(keyData.FlickerDarkRate, keyData.FlickerLightRate);
            }
        }

        public void InitializeCamera()
        {
            if (_cameraObjects == null)
            {
                _cameraObjects = new Camera[_cameraNodes.Length + 1];
                _cameraTransforms = new Transform[_cameraNodes.Length + 1];
                for (int i = 0; i < _cameraNodes.Length; i++)
                {
                    GameObject gameObject = _cameraNodes[i];
                    Camera camera = gameObject.GetComponent<Camera>();
                    if (camera == null)
                    {
                        camera = gameObject.GetComponentInChildren<Camera>();
                    }
                    //camera.cullingMask = num;
                    _cameraObjects[i] = camera;
                    _cameraTransforms[i] = camera.transform;
                }
            }
        }

        public void InitializeMultiCamera(LiveTimelineControl control)
        {
            var cameraCount = control.data.multiCameraSettings.cameraNum;
            MultiCamera[] cameras = new MultiCamera[cameraCount];
            var root = new GameObject("MultiCameras");
            root.transform.SetParent(control.transform);
            for (int i = 0; i < cameraCount; i++)
            {
                var camObj = new GameObject($"MultiCamera_{i}");
                camObj.transform.SetParent(root.transform);

                var cam = camObj.AddComponent<MultiCamera>();
                cam.Initialize();
                cameras[i] = cam;
                control.MultiRecordFrames.Add(new List<LiveCameraFrame>());
            }
            control.SetMultiCamera(cameras);
        }

        private void UpdateMainCamera()
        {
            if (_cameraObjects == null || _cameraNodes == null || _cameraTransforms == null) return;
            if (_freeCameraActive && _freeCamera != null)
            {
                _mainCameraTransform = _freeCamera.transform;
                return;
            }
            for (int i = 0; i < _cameraNodes.Length; i++)
            {
                if (_cameraNodes[i] == null) continue;
                bool activeSelf = _cameraNodes[i].activeSelf;
                bool flag = i == _activeCameraIndex;
                _cameraNodes[i].SetActive(flag);
                if (i == 0 && activeSelf != flag && flag && _cameraLookAt != null)
                {
                    _cameraLookAt.ActivationUpdate();
                }
            }
            if (_activeCameraIndex >= 0 && _activeCameraIndex < _cameraTransforms.Length)
                _mainCameraTransform = _cameraTransforms[_activeCameraIndex];
        }

        public void SetPlaybackPaused(bool paused)
        {
            if (_playbackPaused == paused || IsRecordVMD)
                return;

            _playbackPaused = paused;
            SetAudioPaused(liveMusic, paused);
            foreach (var vocal in liveVocal)
                SetAudioPaused(vocal, paused);
        }

        public void SeekPlayback(float time)
        {
            _liveCurrentTime = Mathf.Clamp(time, 0f, Mathf.Max(0f, totalTime - 0.001f));
            UI.ProgressBar.SetValueWithoutNotify(totalTime > 0f ? _liveCurrentTime / totalTime : 0f);
            if (liveMusic != null && liveMusic.sourceList != null)
                UmaViewerAudio.SetTime(liveMusic, _liveCurrentTime);
            foreach (var vocal in liveVocal)
                if (vocal != null && vocal.sourceList != null)
                    UmaViewerAudio.SetTime(vocal, _liveCurrentTime);
            OnTimelineUpdate(_liveCurrentTime);
            if (_liveTimelineControl != null)
                _liveTimelineControl.AlterLateUpdate();
            _syncTime = !_playbackPaused;
        }

        public void SetFreeCameraEnabled(bool enabled)
        {
            if (_freeCameraActive == enabled)
                return;

            if (_freeCamera == null)
                _freeCamera = Resources.FindObjectsOfTypeAll<FreeCam>().FirstOrDefault(cam => cam.gameObject.scene.IsValid());
            if (_freeCamera == null)
                return;

            _freeCameraActive = enabled;
            if (enabled)
            {
                Transform source = _cameraTransforms != null && _activeCameraIndex >= 0 && _activeCameraIndex < _cameraTransforms.Length
                    ? _cameraTransforms[_activeCameraIndex] : null;
                if (source != null)
                    _freeCamera.transform.SetPositionAndRotation(source.position, source.rotation);
                _freeCamera.gameObject.SetActive(true);
                if (_cameraNodes != null)
                {
                    for (int i = 0; i < _cameraNodes.Length; i++)
                        if (_cameraNodes[i] != null) _cameraNodes[i].SetActive(false);
                }
                _mainCameraTransform = _freeCamera.transform;
            }
            else
            {
                _freeCamera.gameObject.SetActive(false);
                UpdateMainCamera();
            }
        }

        private static void SetAudioPaused(UmaViewerAudio.CuteAudioSource source, bool paused)
        {
            if (source == null || source.sourceList == null)
                return;
            foreach (var audioSource in source.sourceList)
            {
                if (paused) audioSource.Pause();
                else audioSource.UnPause();
            }
        }

        private void SetupCharacterLocator()
        {
            if (!_liveTimelineControl) return;
            for (int i = 0; i < CharaContainerScript.Count; i++)
            {
                var container = CharaContainerScript[i];
                container.LiveLocator = new LiveTimelineCharaLocator(container);
                container.LiveLocator.liveCharaStandingPosition = (LiveCharaPosition)i;
                _liveTimelineControl.liveCharactorLocators[i] = container.LiveLocator;
                container.LiveLocator.liveCharaInitialPosition = container.transform.position;
            }
        }

        /// <summary>
        /// 某位角色在某首歌里的 vocal 音源。角色专属音轨不存在时（未实装的组合），
        /// 从该曲的全部 chara 音轨里随机挑一条顶上。
        /// </summary>
        private static UmaDatabaseEntry ResolveVocalEntry(int songid, int charaid)
        {
            var sounds = UmaViewerMain.Instance.AbSounds;
            var entry = sounds.FirstOrDefault(a => a.Name.Contains(string.Format(VOCAL_PATH, songid, charaid)) && a.Name.EndsWith("awb"));
            if (entry != null) return entry;

            var fallbacks = sounds.Where(a => a.Name.Contains(string.Format(RANDOM_VOCAL_PATH, songid)) && a.Name.EndsWith("awb")).ToList();
            return fallbacks.Count > 0 ? fallbacks[UnityEngine.Random.Range(0, fallbacks.Count - 1)] : null;
        }

        public void InitializeMusic(int songid, List<LiveCharacterSelect> characters)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name == "" || i >= partInfo.SingerCount) continue;

                var entry = ResolveVocalEntry(songid, characters[i].CharaEntry.Id);
                if (entry == null) continue;

                Debug.Log(entry.Name);
                liveVocal.Add(UmaViewerAudio.ApplySound(entry.Name.Split('.')[0], i));
            }

            liveMusic = UmaViewerAudio.ApplySound(string.Format(SONG_PATH, songid), -1);
        }

        public void Play()
        {

            // Play can be invoked after scene/UI setup has been rebuilt; ensure the
            // debugging overlay exists and is enabled for the whole playback.
            LivePlaybackToolbar toolbar = GetComponent<LivePlaybackToolbar>();
            if (toolbar == null)
                toolbar = gameObject.AddComponent<LivePlaybackToolbar>();
            toolbar.enabled = true;

            foreach (var vocal in liveVocal)
            {
                UmaViewerAudio.Play(vocal);
            }
            UmaViewerAudio.Play(liveMusic);

            _isLiveSetup = true;
            _liveCurrentTime = 0;

            if (IsRecordVMD)
            {
                foreach (var container in CharaContainerScript)
                {
                    var rootbone = container.transform.Find("Position");
                    var newRecorder = rootbone.gameObject.AddComponent<UnityHumanoidVMDRecorder>();
                    newRecorder.UseParentOfAll = true;
                    newRecorder.UseAbsoluteCoordinateSystem = true;
                    newRecorder.Initialize();
                    if (!newRecorder.IsRecording)
                    {
                        newRecorder.StartRecording(true);
                    }
                }
            }
        }

        private void OnTimelineUpdate(float _liveCurrentTime)
        {
            _liveTimelineControl.AlterUpdate(_liveCurrentTime);
            if (!_soloMode)
            {
                UmaViewerAudio.AlterUpdate(_liveCurrentTime, partInfo, liveVocal, sliderControl.is_Outed);
            }
        }

        bool isExit;
        void Update()
        {
            if (isExit) return;

            if (_isLiveSetup)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || _liveCurrentTime >= totalTime)
                {
                    ExitLive();
                }

                if (_syncTime == false)
                {
                    if(liveMusic.sourceList.Count == 0)
                    {
                        _syncTime = true;
                    }
                    else if (liveMusic.sourceList[0].time > 0.01)
                    {
                        _liveCurrentTime = liveMusic.sourceList[0].time;
                        _syncTime = true;
                    }
                }
                else
                {
                    if (IsRecordVMD)
                    {
                        _liveCurrentTime += (1 / 60f);
                        if (liveMusic != null)
                        {
                            UmaViewerAudio.Stop(liveMusic);
                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.Stop(vocal);
                            }
                        }

                        UI.ProgressBar.SetValueWithoutNotify(_liveCurrentTime / totalTime);
                        OnTimelineUpdate(_liveCurrentTime);
                        _liveTimelineControl.AlterLateUpdate();
                    }
                    else if (_playbackPaused)
                    {
                        // The toolbar can still seek while paused; the timeline itself remains frozen.
                    }
                    else if (sliderControl.is_Outed)
                    {
                        _liveCurrentTime = UI.ProgressBar.value * totalTime;

                        if (liveMusic != null)
                        {
                            UmaViewerAudio.SetTime(liveMusic, _liveCurrentTime);

                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.SetTime(vocal, _liveCurrentTime);
                            }

                            UmaViewerAudio.Play(liveMusic);

                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.Play(vocal);
                            }
                        }

                        OnTimelineUpdate(_liveCurrentTime);

                        sliderControl.is_Outed = false;
                        sliderControl.is_Touched = false;
                        _syncTime = false;
                    }
                    else if (sliderControl.is_Touched)
                    {
                        _liveCurrentTime = UI.ProgressBar.value * totalTime;

                        if (liveMusic != null)
                        {
                            UmaViewerAudio.Stop(liveMusic);
                            foreach (var vocal in liveVocal)
                            {
                                UmaViewerAudio.Stop(vocal);
                            }
                        }

                        OnTimelineUpdate(_liveCurrentTime);
                    }
                    else
                    {
                        _liveCurrentTime += Time.deltaTime;
                        UI.ProgressBar.SetValueWithoutNotify(_liveCurrentTime / totalTime);
                        OnTimelineUpdate(_liveCurrentTime);
                    }
                }

                UpdateMainCamera();
            }
        }

        private void LateUpdate()
        {
            if (_isLiveSetup && _syncTime && !IsRecordVMD)
            {
                _liveTimelineControl.AlterLateUpdate();
            }
        }

        private void FixedUpdate()
        {
            LiveViewerUI.Instance.UpdateLyrics(_liveCurrentTime);
        }

        DateTime ExitTime;
        private void ExitLive()
        {
            isExit = true;
            if (_liveTimelineControl.IsRecordVMD)
            {
                ExitTime = DateTime.Now;
                SaveCameraVMD();
                SaveMultiCameraVMD();
                SaveCharacterVMD();
            }
            UmaSceneController.LoadScene("Version2");
            UmaAssetManager.UnloadAllBundle(true);
        }

        private void SaveCharacterVMD()
        {
            foreach (var container in CharaContainerScript)
            {
                var rootbone = container.transform.Find("Position");
                if (rootbone.gameObject.TryGetComponent(out UnityHumanoidVMDRecorder recorder))
                {
                    if (recorder.IsRecording)
                    {
                        recorder.StopRecording();
                        recorder.SaveLiveVMD(live, ExitTime, $"Live{live.MusicId}_Pos{CharaContainerScript.IndexOf(container)}", Config.Instance.VmdKeyReductionLevel);
                    }
                }
            }
        }

        /// <summary>
        /// 标记 VMD 录制里需要保留 FOV 的帧：首帧，以及每个 fov 关键帧命中的那一帧和它周围
        /// 的 -3..+1 帧。单机位与多机位两条保存路径原先各写了一份完全相同的循环。
        /// </summary>
        private static void MarkFovFrames(List<LiveCameraFrame> frames, IEnumerable<LiveTimelineKey> fovKeys)
        {
            if (frames == null || frames.Count == 0) return;
            frames[0].FovVaild = true;
            if (fovKeys == null) return;

            foreach (var key in fovKeys)
            {
                int frame = key.frame;
                var keyframe = frames.Find(f => f.frameIndex == frame);
                if (keyframe == null) continue;

                int index = frames.IndexOf(keyframe);
                keyframe.FovVaild = true;
                if (index + 1 < frames.Count) frames[index + 1].FovVaild = true;
                if (index - 1 > 0) frames[index - 1].FovVaild = true;
                if (index - 2 > 0) frames[index - 2].FovVaild = true;
                if (index - 3 > 0) frames[index - 3].FovVaild = true;
            }
        }

        private void SaveMultiCameraVMD()
        {
            var sheet = _liveTimelineControl.data.worksheetList[0];
            for (int i = 0; i < sheet.multiCameraPosKeys.Count; i++)
            {
                var frames = _liveTimelineControl.MultiRecordFrames[i];
                MarkFovFrames(frames, sheet.multiCameraPosKeys[i].keys.thisList);
                UnityCameraVMDRecorder.SaveLiveCameraVMD(live, ExitTime, frames, i);
            }
        }

        private void SaveCameraVMD()
        {
            var frames = _liveTimelineControl.RecordFrames;
            MarkFovFrames(frames, _liveTimelineControl.data.worksheetList[0].cameraFovKeys.thisList);
            UnityCameraVMDRecorder.SaveLiveCameraVMD(live, ExitTime, frames);
        }

        public static List<UmaDatabaseEntry> GetLiveAllVoiceEntry(int songid, List<LiveCharacterSelect> characters)
        {
            List<UmaDatabaseEntry> entryList = new List <UmaDatabaseEntry>();
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name == "") continue;

                var entry = ResolveVocalEntry(songid, characters[i].CharaEntry.Id);
                if (entry != null)
                {
                    entryList.Add(entry);
                }
            }

            var bgEntry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(SONG_PATH, songid)) && a.Name.EndsWith("awb"));
            if (bgEntry != null)
            {
                entryList.Add(bgEntry);
            }
            return entryList;
        }
    }

}
