using Gallop.Live.Cutt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Gallop.ImageEffect;

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
        [SerializeField] private bool _enableMirrorReflection = true;
        [SerializeField] private List<MirrorReflection> _mirrorReflections = new List<MirrorReflection>();
        [SerializeField] private bool _mirrorRenderInLateUpdate = true;

        [SerializeField]
        private GallopImageEffect _mainGallopImageEffect;
        public MultiCameraFinalComposite MultiCameraFinalComposite { get; private set; }
        private MultiCamera[] _multiCameras;
        private LiveVolumeLightController _volumeLightController;
        private LiveStageParticleController _stageParticleController;
        private LiveEffectController _effectController;
        private readonly Dictionary<int, Transform> _effectStageObjects = new Dictionary<int, Transform>();
        private float _effectLastTimelineTime = float.NaN;
        public const string Spotlight3dControllerPath = "3d/env/live/common/spotlight3d/pfb_env_live_cmn_spotlight3d_controller000";
        private Spotlight3dRuntime _spotlightRuntime;
        private MaterialPropertyBlock _characterLightingBlock;
        private Gallop.RenderPipeline.RecoveredLensFlareController _stageFlares;
        private LightProjectionRuntimeController _lightProjectionController;
        private FootLightRuntime _footLightRuntime;
        private LivePropsController _livePropsController;
        private readonly Dictionary<int, Texture> _lightProjectionTextures = new Dictionary<int, Texture>();
        private readonly Dictionary<int, Texture> _mirrorBallProjectionTextures = new Dictionary<int, Texture>();
        public IReadOnlyList<Gallop.RenderPipeline.CustomProjector> LightProjectors => _lightProjectionController?.Projectors;
        public IReadOnlyList<Gallop.RenderPipeline.MirrorBallProjector> MirrorBallProjectors => _lightProjectionController?.MirrorBallProjectors;

        public static string GetLightProjectionTexturePath(int textureId) => string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "3d/env/live/common/projector/tex_env_live_cmn_projector{0:000}", textureId);

        public static string GetMirrorBallProjectionTexturePath(int textureId) => string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "3d/env/live/common/projectormirrorball/tex_env_live_cmn_projector_mirrorball{0:000}", textureId);

        private Texture ResolveMirrorBallProjectionTexture(int textureId)
        {
            if (textureId < 0) return null;
            if (_mirrorBallProjectionTextures.TryGetValue(textureId, out var texture)) return texture;
            texture = LiveFlashResourceUtility.LoadOnView<Texture>(GetMirrorBallProjectionTexturePath(textureId));
            _mirrorBallProjectionTextures.Add(textureId, texture);
            return texture;
        }

        private Texture ResolveLightProjectionTexture(int textureId)
        {
            if (textureId < 0) return null;
            if (_lightProjectionTextures.TryGetValue(textureId, out var texture)) return texture;
            texture = LiveFlashResourceUtility.LoadOnView<Texture>(GetLightProjectionTexturePath(textureId));
            _lightProjectionTextures.Add(textureId, texture);
            return texture;
        }

        private Transform ResolveEffectCharacter(int index)
        {
            var locators = _liveTimelineControl.liveCharactorLocators;
            return index >= 0 && index < locators.Length ? locators[index]?.liveRootTransform : null;
        }

        private Transform ResolveEffectStageObject(string name, int hash)
        {
            return _effectStageObjects.TryGetValue(hash, out var owner) ? owner : null;
        }

        private void InitializeLiveEffects(int normalLayer, int excludedLayer, int transparentLayer)
        {
            if (_effectController != null)
            {
                _liveTimelineControl.OnUpdateEffect -= _effectController.Update;
                _liveTimelineControl.OnUpdateEffectScale -= _effectController.UpdateScale;
                _effectController.Dispose();
            }
            _effectStageObjects.Clear();
            foreach (var item in _liveTimelineControl.StageObjectMap)
                if (item.Value != null) _effectStageObjects[FNVHash.Generate(item.Key)] = item.Value.transform;
            _effectController = new LiveEffectController(transform, transform,
                ResolveEffectCharacter, ResolveEffectStageObject, normalLayer, excludedLayer, transparentLayer);
            var sheet = _liveTimelineControl.GetWorkSheetBySheetIndex(LiveTimelineDefine.SheetIndex.MainLive);
            if (sheet?.effectList != null)
                foreach (var group in sheet.effectList)
                    if (group?.keys != null)
                        for (int i = 0; i < group.keys.Count; i++) group.keys[i]?.OnLoad(_liveTimelineControl);
            _effectController.Load(sheet, TimelinePlayerMode.Default, variation => variation == 0);
            _liveTimelineControl.OnUpdateEffect += _effectController.Update;
            _liveTimelineControl.OnUpdateEffectScale += _effectController.UpdateScale;
            _liveTimelineControl.ResetEffectTimelineForSeek();
            _effectLastTimelineTime = float.NaN;
        }

        private void InitializeLiveSpotlights()
        {
            if (_spotlightRuntime != null)
            {
                _liveTimelineControl.OnUpdateSpotlight3d -= _spotlightRuntime.Update;
                _spotlightRuntime.Dispose();
                _spotlightRuntime = null;
            }
            if (_stageController == null) return;
            List<LiveTimelineSpotlight3dData> groups = null;
            foreach (var sheet in _liveTimelineControl.data.worksheetList)
                if (sheet?.spotlight3dList != null && sheet.spotlight3dList.Count != 0)
                {
                    groups = sheet.spotlight3dList;
                    break;
                }
            if (groups == null) return;
            var prefab = LiveFlashResourceUtility.LoadOnView<GameObject>(Spotlight3dControllerPath);
            var holder = prefab != null ? prefab.GetComponent<AssetHolder>() : null;
            if (holder == null)
            {
                Debug.LogError("[Spotlight3d] Missing native common controller AssetHolder");
                return;
            }
            var roots = new List<Transform>(CharaContainerScript.Count);
            for (int i = 0; i < CharaContainerScript.Count; ++i) roots.Add(ResolveEffectCharacter(i));
            _spotlightRuntime = new Spotlight3dRuntime(roots, MainCameraTransform,
                GraphicSettings.GetLayer(GraphicSettings.LayerIndex.LayerBG));
            _spotlightRuntime.CreateControllers(groups, TimelinePlayerMode.Default,
                id => holder.Get<GameObject>(LiveTimelineSpotlight3dData.GetAssetName(id)));
            _liveTimelineControl.OnUpdateSpotlight3d += _spotlightRuntime.Update;
        }

        public static Director instance => _instance;

        //real work start
        public LiveEntry live;
        private const string CUTT_PATH = "cutt/cutt_son{0}/cutt_son{0}";
        private const string STAGE_PATH = "3d/env/live/live{0}/pfb_env_live{0}_controller000";
        private const string SONG_PATH = "sound/l/{0}/snd_bgm_live_{0}_oke_01";
        private const string VOCAL_PATH = "sound/l/{0}/snd_bgm_live_{0}_chara_{1}_01";
        private const string RANDOM_VOCAL_PATH = "sound/l/{0}/snd_bgm_live_{0}_chara";
        private const string LIVE_PART_PATH = "live/musicscores/m{0}/m{0}_part";
        private const string CYALUME_SCORE_PATH = "live/musicscores/m{0:0000}/m{0:0000}_cyalume";

        private UmaViewerBuilder Builder => UmaViewerBuilder.Instance;

        public List<Transform> charaObjs;

        public List<UmaContainerCharacter> CharaContainerScript = new List<UmaContainerCharacter>();

        public List<Animation> charaAnims;
        public List<UmaViewerAudio.CuteAudioSource> liveVocal = new List<UmaViewerAudio.CuteAudioSource>();
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

        private bool _lateTimelineAppliedThisFrame;

        public Transform MainCameraTransform => _mainCameraTransform;

        public Camera MainRenderCamera
        {
            get
            {
                if (_mainCameraTransform != null)
                {
                    var cam = _mainCameraTransform.GetComponent<Camera>();
                    if (cam == null) cam = _mainCameraTransform.GetComponentInChildren<Camera>(true);
                    if (cam != null) return cam;
                }
                if (_cameraObjects != null &&
                    _activeCameraIndex >= 0 &&
                    _activeCameraIndex < _cameraObjects.Length)
                    return _cameraObjects[_activeCameraIndex];
                return Camera.main;
            }
        }

        // [REPORT §10.3-B1/§4.2] 原生 RegisterPass 按相机组逐台注册后处理；
        // 演示相机组 = cutt prefab 的时间轴相机（_cameraObjects，InitializeCamera
        // 收集，CameraSwitcher 逐台 SetActive 激活）。成员判定取代 master7 原
        // "camera == MainRenderCamera 引用相等"门控，使切换帧新旧相机同帧都
        // 持有完整后处理链。多相机（MultiCamera）/镜面反射/监视器捕获相机
        // 不在组内，继续走各自链路。
        public bool IsPresentationCamera(Camera camera)
        {
            if (camera == null || _cameraObjects == null)
                return false;
            for (int i = 0; i < _cameraObjects.Length; i++)
                if (ReferenceEquals(_cameraObjects[i], camera))
                    return true;
            return false;
        }

        private Transform _mainCameraTransform;

        private static readonly Dictionary<string, UmaDatabaseEntry> _laserBundleCache
            = new Dictionary<string, UmaDatabaseEntry>();

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
                LiveRuntimeDiagnostics.BeginDirector(this);
                Gallop.RenderPipeline.PostImageEffectFeature.ResetRuntimeParameter();
                Debug.Log(string.Format(CUTT_PATH, live.MusicId));
                Builder.LoadAssetPath(string.Format(CUTT_PATH, live.MusicId), transform);
                // Character eye tracking can run while subsequent asset preloads yield.
                // Establish Camera.main before constructing/enabling those characters.
                InitializeCamera();
                UpdateMainCamera();
                if (RequireStage)
                {
                    Debug.Log(live.BackGroundId);

                    string stagePath = string.Format(STAGE_PATH, live.BackGroundId);
                    if (UmaViewerMain.Instance.AbList.TryGetValue(stagePath, out var stageEntry) &&
                        !UmaAssetManager.Exist(stageEntry))
                    {
                        // 正常从 LoadLive 进入时已经异步预载；这里仅作为其他入口的同步兜底。
                        PreloadStageBundlesBeforeInstantiate(live.BackGroundId);
                    }

                    Builder.LoadAssetPath(stagePath, transform);
                    

                    _liveTimelineControl.StageObjectMap = _stageController.StageObjectMap;
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
                if (LiveRuntimeDiagnostics.Enabled)
                    LiveRuntimeDiagnostics.RecordAssetLoad(partAsset.Name, "LoadAsset", typeof(TextAsset),
                        new UnityEngine.Object[] { partData }, partData);
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

        public void InitializeTimeline(List<LiveCharacterLoadData> characters, int mode)
        {
            totalTime = _liveTimelineControl.data.timeLength;

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

            _liveTimelineControl.OnUpdateLipSync += delegate (LiveTimelineKeyIndex keyData_, float liveTime_)
            {
                var prevKey = keyData_.prevKey as LiveTimelineKeyLipSyncData;
                var curKey = keyData_.key as LiveTimelineKeyLipSyncData;
                var nextKey = keyData_.nextKey as LiveTimelineKeyLipSyncData;
                for (int k = 0; k < charaObjs.Count; k++)
                {
                    if (k < CharaContainerScript.Count)
                    {
                        var container = CharaContainerScript[k];
                        container.FaceDrivenKeyTarget.AlterUpdateAutoLip(prevKey, curKey, liveTime_, ((int)curKey.character >> k) % 2);
                    }
                }
            };

            _liveTimelineControl.OnUpdateFacial += delegate (FacialDataUpdateInfo updateInfo_, float liveTime_, int position)
            {
                if (position < charaObjs.Count)
                {
                    var container = CharaContainerScript[position];
                    container.FaceDrivenKeyTarget.AlterUpdateFacialNew(ref updateInfo_, liveTime_);
                }
            };

            _liveTimelineControl.OnUpdateGlobalLight += delegate (ref GlobalLightUpdateInfo updateInfo)
            {
                var tmpPos = -(updateInfo.lightRotation * Vector3.forward).normalized;
                foreach (var locator in _liveTimelineControl.liveCharactorLocators)
                {
                    if (locator != null && updateInfo.flags.hasFlag(locator.liveCharaStandingPosition) && locator is LiveTimelineCharaLocator charaLocator)
                    {
                        var container = charaLocator.UmaContainer;
                        if (container)
                        {
                            var propertyBlock = _characterLightingBlock ?? (_characterLightingBlock = new MaterialPropertyBlock());
                            foreach (var renderer in container.Renderers)
                            {
                                renderer.GetPropertyBlock(propertyBlock);
                            propertyBlock.SetFloat("_RimShadowRate", updateInfo.globalRimShadowRate);
                            propertyBlock.SetColor("_RimColor", updateInfo.rimColor);
                            propertyBlock.SetFloat("_RimStep", updateInfo.rimStep);
                            propertyBlock.SetFloat("_RimFeather", updateInfo.rimFeather);
                            propertyBlock.SetFloat("_RimSpecRate", updateInfo.rimSpecRate);
                            propertyBlock.SetFloat("_RimHorizonOffset", updateInfo.RimHorizonOffset);
                            propertyBlock.SetFloat("_RimVerticalOffset", updateInfo.RimVerticalOffset);
                            propertyBlock.SetFloat("_RimHorizonOffset2", updateInfo.RimHorizonOffset2);
                            propertyBlock.SetFloat("_RimVerticalOffset2", updateInfo.RimVerticalOffset2);
                            propertyBlock.SetColor("_RimColor2", updateInfo.rimColor2);
                            propertyBlock.SetFloat("_RimStep2", updateInfo.rimStep2);
                            propertyBlock.SetFloat("_RimFeather2", updateInfo.rimFeather2);
                            propertyBlock.SetFloat("_RimSpecRate2", updateInfo.rimSpecRate2);
                            propertyBlock.SetFloat("_RimShadowRate2", updateInfo.globalRimShadowRate2);
                                propertyBlock.SetFloat("_UseOriginalDirectionalLight", 1f);
                                propertyBlock.SetVector("_OriginalDirectionalLightDir", tmpPos);
                                renderer.SetPropertyBlock(propertyBlock);
                            }
                        }
                    }
                }
            };

            _liveTimelineControl.OnUpdateBgColor1 += delegate (ref BgColor1UpdateInfo updateInfo)
            {
                if (updateInfo.TimelineName != "CharaCenter" && updateInfo.TimelineName != "CharaLeft" &&
                    updateInfo.TimelineName != "CharaRight" && updateInfo.TimelineName != "CharaColor") return;
                foreach (var locator in _liveTimelineControl.liveCharactorLocators)
                {
                    var EFlags = (LiveCharaPositionFlag)updateInfo.flags;
                    if (locator != null && (updateInfo.flags == 0 || EFlags.hasFlag(locator.liveCharaStandingPosition)) && locator is LiveTimelineCharaLocator charaLocator)
                    {
                        var container = charaLocator.UmaContainer;
                        if (container)
                        {
                            var propertyBlock = _characterLightingBlock ?? (_characterLightingBlock = new MaterialPropertyBlock());
                            foreach (var renderer in container.Renderers)
                            {
                                renderer.GetPropertyBlock(propertyBlock);
                            propertyBlock.SetColor("_CharaColor", updateInfo.color * updateInfo.colorPower);
                            propertyBlock.SetColor("_ToonDarkColor", updateInfo.toonDarkColor);
                            propertyBlock.SetColor("_ToonBrightColor", updateInfo.toonBrightColor);
                            propertyBlock.SetColor("_OutlineColor", updateInfo.outlineColor);
                            propertyBlock.SetFloat("_Saturation", updateInfo.Saturation);
                                renderer.SetPropertyBlock(propertyBlock);
                            }
                        }
                    }
                }
            };

            SetupCharacterLocator();
            foreach (var sheet in _liveTimelineControl.data.worksheetList)
                sheet?.InitializeProps(_liveTimelineControl);
            var propsLocators = new List<LiveTimelineCharaLocator>(CharaContainerScript.Count);
            foreach (var character in CharaContainerScript)
                propsLocators.Add(character != null ? character.LiveLocator : null);
            _livePropsController?.Dispose();
            _livePropsController = new LivePropsController(transform,
                _liveTimelineControl.data.propsSettings, CharaContainerScript, propsLocators);
            _livePropsController.VariationEnabled = variation => variation == 0;
            _stageParticleController = new LiveStageParticleController(
                _stageController != null ? _stageController.transform : null);
            _liveTimelineControl.OnUpdateParticle += _stageParticleController.Update;
            _liveTimelineControl.OnUpdateParticleGroup += _stageParticleController.UpdateGroup;
            InitializeCamera();
            InitializeMirrorReflections();
            _volumeLightController = new LiveVolumeLightController();
            _volumeLightController.Initialize(_stageController != null ? _stageController.transform : null,
                "Sunshafts", _liveTimelineControl.data.sunShaftsSettings, MainRenderCamera);
            _volumeLightController.SetLightShaftsTextures(_liveTimelineControl.data.indirectLightShaftsSettings,
                LiveLightShaftsResources.LoadTextures(_liveTimelineControl.data.indirectLightShaftsSettings));
            UpdateMainCamera();
            InitializeMultiCamera(_liveTimelineControl);
            InitializeLiveEffects(GraphicSettings.GetLayer(GraphicSettings.LayerIndex.LayerEFFECT),
                GraphicSettings.GetLayer(GraphicSettings.LayerIndex.LayerCircleProfile),
                GraphicSettings.GetLayer(GraphicSettings.LayerIndex.LayerTransparentFX));
            InitializeLiveSpotlights();
            if (_lightProjectionController != null)
            {
                _liveTimelineControl.OnUpdateLightProjection -= _lightProjectionController.Apply;
                _lightProjectionController.Dispose();
                _lightProjectionController = null;
                _lightProjectionTextures.Clear();
                _mirrorBallProjectionTextures.Clear();
            }
            if (_stageController != null)
            {
                _lightProjectionController = new LightProjectionRuntimeController();
                _lightProjectionController.Initialize(_stageController.transform,
                    ResolveLightProjectionTexture, ResolveEffectCharacter);
                _lightProjectionController.ConfigureCharacterPosition(index =>
                {
                    var locators = _liveTimelineControl.liveCharactorLocators;
                    return index >= 0 && index < locators.Length && locators[index] != null
                        ? locators[index].liveCharaPosition : Vector3.zero;
                });
                _lightProjectionController.ConfigureMirrorBallTextures(ResolveMirrorBallProjectionTexture);
                _liveTimelineControl.OnUpdateLightProjection += _lightProjectionController.Apply;
                var monitorDriver = GetComponentInParent<StageMonitorDriver>();
                if (monitorDriver != null)
                    _lightProjectionController.ConfigureMovieFrames(monitorDriver.TryGetProjectionMovieFrame);
            }
            var footRoots = new List<Transform>(CharaContainerScript.Count);
            for (int i = 0; i < CharaContainerScript.Count; i++)
                footRoots.Add(ResolveEffectCharacter(i));
            _footLightRuntime = new FootLightRuntime();
            _footLightRuntime.Initialize(
                _stageController != null ? _stageController.transform : transform,
                footRoots,
                index =>
                {
                    var locators = _liveTimelineControl.liveCharactorLocators;
                    return index >= 0 && index < locators.Length && locators[index] != null
                        ? locators[index].liveCharaPosition : Vector3.zero;
                },
                path => LiveFlashResourceUtility.LoadOnView<GameObject>(path),
                (index, left) =>
                {
                    var character = CharaContainerScript[index];
                    if (character == null || character.LiveLocator?.Bones == null) return null;
                    character.LiveLocator.Bones.TryGetValue(left ? "Ankle_L" : "Ankle_R", out var foot);
                    return foot;
                });
            _liveTimelineControl.OnUpdateBgColor1 += _footLightRuntime.ApplySpotlight;
            var flareSetting = _liveTimelineControl.data.lensFlareSetting;
            _stageFlares = new Gallop.RenderPipeline.RecoveredLensFlareController(
                _stageController != null ? _stageController.transform : null,
                flareSetting != null ? flareSetting.lightStandard : 3f,
                flareSetting != null ? flareSetting.underLimit : 0f);
            foreach (var sheet in _liveTimelineControl.data.worksheetList)
            {
                if (sheet.bgColor1List != null)
                    foreach (var group in sheet.bgColor1List) group?.UpdateStatus();
                if (sheet.lensFlareList != null)
                    foreach (var group in sheet.lensFlareList) group?.UpdateStatus();
            }
            _liveTimelineControl.OnUpdateLensFlare += _stageFlares.Apply;
            for (int i = 0; i < kTimelineCameraIndices.Length; i++)
            {
                int num = kTimelineCameraIndices[i];
                if (num < _cameraObjects.Length)
                {
                    _liveTimelineControl.SetTimelineCamera(_cameraObjects[num], i);
                }
            }
            _liveTimelineControl.OnUpdatePostEffect_BloomDiffusion += OnUpdatePostEffect_BloomDiffusion;
            _liveTimelineControl.OnUpdatePostEffect_Dof += OnUpdatePostEffect_Dof;
            _liveTimelineControl.OnUpdatePostFilm += OnUpdatePostFilm;
            _liveTimelineControl.OnUpdateRadialBlur += OnUpdateRadialBlur;
            _liveTimelineControl.OnUpdateColorCorrection += OnUpdateColorCorrection;
            _liveTimelineControl.OnUpdateGlobalFog += OnUpdateGlobalFog;

            // [A7b][原生订阅点] Director.InitializeTimeline 在
            // 0x181a5f21d（add_OnUpdateExposure RVA=0x1ae96c0）与
            // 0x181a5f1c9（add_OnUpdateToneCurve RVA=0x1aea4b0）订阅两条轨道，
            // 顺序：BloomDiffusion → ToneCurve → Spotlight3d → Exposure → PostFilm。
            // master7 统一在 Initialize 事件绑定块订阅，行为一致。
            _liveTimelineControl.OnUpdateVolumeLight += OnUpdateVolumeLight;
            _liveTimelineControl.OnUpdateLightShafts += OnUpdateLightShafts;
            _liveTimelineControl.OnUpdateExposure += OnUpdateExposure;
            _liveTimelineControl.OnUpdateToneCurve += OnUpdateToneCurve;


            _liveTimelineControl.OnUpdateCameraSwitcher += delegate (int cameraIndex_)
            {
                if (cameraIndex_ < 0)
                {
                    _activeCameraIndex = 0;
                }
                else if (cameraIndex_ < kTimelineCameraIndices.Length)
                {
                    _activeCameraIndex = kTimelineCameraIndices[cameraIndex_];
                }
                UpdateMainCamera();
            };
            
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
            var cameraCount = control.data.multiCameraSettings != null ? control.data.multiCameraSettings.cameraNum : 0;
            MultiCamera[] cameras = new MultiCamera[cameraCount];
            var root = new GameObject("MultiCameras");
            root.transform.SetParent(control.transform);
            MultiCameraFinalComposite = root.AddComponent<MultiCameraFinalComposite>();
            var presentationCamera = MainRenderCamera;
            for (int i = 0; i < cameraCount; i++)
            {
                var camObj = new GameObject($"MultiCamera_{i}");
                camObj.transform.SetParent(root.transform);

                var sceneCamera = camObj.AddComponent<Camera>();
                if (presentationCamera != null) sceneCamera.CopyFrom(presentationCamera);
                sceneCamera.targetTexture = null;
                sceneCamera.depth = i + 1;
                var cameraData = camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                cameraData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
                var cam = camObj.AddComponent<MultiCamera>();
                cam.Initialize(MultiCameraFinalComposite, i);
                camObj.SetActive(false);
                cameras[i] = cam;
                control.MultiRecordFrames.Add(new List<LiveCameraFrame>());
            }
            _multiCameras = cameras;
            MultiCameraFinalComposite.Initialize(cameras, presentationCamera);
            if (presentationCamera != null) presentationCamera.depth = cameraCount + 1;
            control.SetMultiCamera(cameras);
        }

        private void UpdateMainCamera()
        {
            if (_cameraObjects == null) return;
            for (int i = 0; i < _cameraNodes.Length; i++)
            {
                bool activeSelf = _cameraNodes[i].activeSelf;
                bool flag = i == _activeCameraIndex;
                _cameraNodes[i].SetActive(flag);
                // Camera.main consumers (eye tracking) must follow the same presentation switch.
                var camera = _cameraObjects[i];
                if (camera != null)
                {
                    if (flag) camera.gameObject.tag = "MainCamera";
                    if (flag)
                    {
                        var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                        if (cameraData == null)
                            cameraData = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                        cameraData.renderPostProcessing = true;
                    }
                    else if (camera.CompareTag("MainCamera")) camera.gameObject.tag = "Untagged";
                }
                if (i == 0 && activeSelf != flag && flag && _cameraLookAt != null)
                {
                    _cameraLookAt.ActivationUpdate();
                }
            }
            _mainCameraTransform = _cameraTransforms[_activeCameraIndex];
            if (MultiCameraFinalComposite != null)
            {
                MainRenderCamera.depth = (_multiCameras != null ? _multiCameras.Length : 0) + 1;
                MultiCameraFinalComposite.SetRenderCamera(MainRenderCamera);
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

        public void InitializeMusic(int songid, List<LiveCharacterLoadData> characters)
        {

            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name != "" && i < partInfo.SingerCount)
                {
                    var charaid = characters[i].CharaEntry.Id;

                    var entry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(VOCAL_PATH, songid, charaid)) && a.Name.EndsWith("awb"));
                    if (entry == null)
                    {
                        List<UmaDatabaseEntry> entries = new List<UmaDatabaseEntry>();
                        foreach (var random in UmaViewerMain.Instance.AbSounds.Where(a => (a.Name.Contains(string.Format(RANDOM_VOCAL_PATH, songid)) && a.Name.EndsWith("awb"))))
                        {
                            entries.Add(random);
                        }
                        if (entries.Count > 0)
                        {
                            entry = entries[UnityEngine.Random.Range(0, entries.Count - 1)];
                        }
                    }

                    if (entry != null)
                    {
                        Debug.Log(entry.Name);
                        liveVocal.Add(UmaViewerAudio.ApplySound(entry.Name.Split('.')[0], i));
                    }
                }
            }


            liveMusic = UmaViewerAudio.ApplySound(string.Format(SONG_PATH, songid), -1);
        }

        public void Play()
        {

            foreach (var vocal in liveVocal)
            {
                UmaViewerAudio.Play(vocal);
            }
            UmaViewerAudio.Play(liveMusic);

            _isLiveSetup = true;
            _liveCurrentTime = 0;
            LiveRuntimeDiagnostics.RecordPhase("play_started", this);

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
            if (_effectController != null)
            {
                bool seek = sliderControl.is_Touched || sliderControl.is_Outed ||
                    _liveCurrentTime < _effectLastTimelineTime;
                if (seek && _liveCurrentTime != _effectLastTimelineTime)
                {
                    _effectController.ResetForSeek();
                    _liveTimelineControl.ResetEffectTimelineForSeek();
                }
                _effectLastTimelineTime = _liveCurrentTime;
            }
            _liveTimelineControl.AlterUpdate(_liveCurrentTime);
            if (!_soloMode)
            {
                UmaViewerAudio.AlterUpdate(_liveCurrentTime, partInfo, liveVocal, sliderControl.is_Outed);
            }
        }

        private void ApplyTimelineLateUpdate()
        {
            if (_lateTimelineAppliedThisFrame || _liveTimelineControl == null)
                return;

            _liveTimelineControl.AlterLateUpdate();
            _livePropsController?.Evaluate(_liveTimelineControl.data.worksheetList,
                _liveTimelineControl.currentLiveTime * 60f, _liveTimelineControl.PlayMode);
            _footLightRuntime?.AlterLateUpdate();
            _effectController?.Pause(!IsRecordVMD && (!_syncTime || sliderControl.is_Touched));
            _spotlightRuntime?.AlterLateUpdate();
            _stageFlares?.AlterUpdate(MainRenderCamera);
            if (_volumeLightController != null)
            {
                _volumeLightController.LateUpdateSunShafts(MainRenderCamera);
                var parameter = Gallop.RenderPipeline.PostImageEffectFeature.RuntimeParameter;
                parameter.SunShafts = _volumeLightController.SunShafts;
                parameter.LightShafts = _volumeLightController.LightShafts;
            }
            _lateTimelineAppliedThisFrame = true;
        }

        bool isExit;
        void Update()
        {
            if (isExit) return;

            if (_isLiveSetup)
            {
                _lateTimelineAppliedThisFrame = false;

                if ((!UmaViewerMain.TryConsumeEscapeForFullScreen() && Input.GetKeyDown(KeyCode.Escape)) || _liveCurrentTime >= totalTime)
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
                        _liveCurrentTime = UI.ProgressBar.value * totalTime;
                        _liveCurrentTime = Mathf.Clamp(_liveCurrentTime, 0f, Mathf.Max(0f, totalTime - 0.001f));
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
                        ApplyTimelineLateUpdate();
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
                        ApplyTimelineLateUpdate();

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
                        ApplyTimelineLateUpdate();
                    }
                    else
                    {
                        _liveCurrentTime += Time.deltaTime;
                        UI.ProgressBar.SetValueWithoutNotify(_liveCurrentTime / totalTime);
                        OnTimelineUpdate(_liveCurrentTime);
                        ApplyTimelineLateUpdate();
                    }
                }

                UpdateMainCamera();

                // 时间轴和主相机都更新完后再同步 Laser Renderer/朝向。
                // 这样既不会读取上一帧 LaserUpdateInfo，也不会读取上一帧相机姿态。
                if (_stageController != null)
                    _stageController.AlterUpdateLaserControllers();

                LiveRuntimeDiagnostics.RecordFrame(this);
            }
        }

        private void LateUpdate()
        {
            if (_isLiveSetup && _syncTime && !IsRecordVMD)
            {
                ApplyTimelineLateUpdate();
            }
            
            if (_enableMirrorReflection && _mirrorRenderInLateUpdate)
            {
                UpdateMirrorReflections();
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
            UmaSceneController.LoadScene(
                "Version2",
                null,
                delegate
                {
                    // 等旧 LiveScene 完全销毁后再清理，避免过场期间角色/舞台对象失去资源。
                    UmaAssetManager.UnloadAllBundle(true);
                });
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

        private void SaveMultiCameraVMD()
        {
            for (int i = 0; i < _liveTimelineControl.data.worksheetList[0].multiCameraPosKeys.Count; i++)
            {
                var frames = _liveTimelineControl.MultiRecordFrames[i];
                frames[0].FovVaild = true;
                var fov = _liveTimelineControl.data.worksheetList[0].multiCameraPosKeys[i].keys.thisList;
                fov.ForEach(k =>
                {
                    var keyframe = frames.Find(f => f.frameIndex == k.frame);
                    if (keyframe != null)
                    {
                        var index = frames.IndexOf(keyframe);
                        keyframe.FovVaild = true;
                        if (index + 1 < frames.Count) frames[index + 1].FovVaild = true;
                        if (index - 1 > 0) frames[index - 1].FovVaild = true;
                        if (index - 2 > 0) frames[index - 2].FovVaild = true;
                        if (index - 3 > 0) frames[index - 3].FovVaild = true;
                    }
                });

                UnityCameraVMDRecorder.SaveLiveCameraVMD(live, ExitTime, frames, i);
            }
        }

        private void SaveCameraVMD()
        {
            var frames = _liveTimelineControl.RecordFrames;
            frames[0].FovVaild = true;
            var fov = _liveTimelineControl.data.worksheetList[0].cameraFovKeys.thisList;
            fov.ForEach(k =>
            {

                var keyframe = frames.Find(f => f.frameIndex == k.frame);
                if (keyframe != null)
                {
                    var index = frames.IndexOf(keyframe);
                    keyframe.FovVaild = true;
                    if (index + 1 < frames.Count) frames[index + 1].FovVaild = true;
                    if (index - 1 > 0) frames[index - 1].FovVaild = true;
                    if (index - 2 > 0) frames[index - 2].FovVaild = true;
                    if (index - 3 > 0) frames[index - 3].FovVaild = true;
                }
            });

            UnityCameraVMDRecorder.SaveLiveCameraVMD(live, ExitTime, frames);
        }

        public static List<UmaDatabaseEntry> GetLiveAllVoiceEntry(int songid, List<LiveCharacterLoadData> characters)
        {
            List<UmaDatabaseEntry> entryList = new List <UmaDatabaseEntry>();
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].CharaEntry.Name != "")
                {
                    var charaid = characters[i].CharaEntry.Id;

                    var entry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(VOCAL_PATH, songid, charaid)) && a.Name.EndsWith("awb"));
                    if (entry == null)
                    {
                        List<UmaDatabaseEntry> entries = new List<UmaDatabaseEntry>();
                        foreach (var random in UmaViewerMain.Instance.AbSounds.Where(a => (a.Name.Contains(string.Format(RANDOM_VOCAL_PATH, songid)) && a.Name.EndsWith("awb"))))
                        {
                            entries.Add(random);
                        }
                        if (entries.Count > 0)
                        {
                            entry = entries[UnityEngine.Random.Range(0, entries.Count - 1)];
                        }
                    }

                    if (entry != null)
                    {
                        entryList.Add(entry);
                    }
                }
            }

            var bgEntry = UmaViewerMain.Instance.AbSounds.FirstOrDefault(a => a.Name.Contains(string.Format(SONG_PATH, songid)) && a.Name.EndsWith("awb"));
            if (bgEntry != null)
            {
                entryList.Add(bgEntry);
            }
            return entryList;
        }
        public static List<UmaDatabaseEntry> GetLivePreloadEntries(
            LiveEntry live,
            List<LiveCharacterLoadData> characters,
            bool requireStage)
        {
            var result = new List<UmaDatabaseEntry>();
            if (live == null)
                return result;

            result.AddRange(GetLiveAllVoiceEntry(live.MusicId, characters));

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return result;

            void AddByKey(string key)
            {
                if (main.AbList.TryGetValue(key, out var entry) && entry != null)
                    result.Add(entry);
            }

            // Cutt 和歌曲 part 也提前加载，避免进入场景后同步卡顿。
            AddByKey(string.Format(CUTT_PATH, live.MusicId));
            AddByKey(string.Format(LIVE_PART_PATH, live.MusicId));
            // Target GetLivePreloadEntries calls ResourcePath.GetCyalumeScorePath
            // and passes its result to AddByKey (RVA 0x1a5c18f -> 0x1a5c19f).
            AddByKey(string.Format(CYALUME_SCORE_PATH, live.MusicId));

            if (requireStage && !string.IsNullOrEmpty(live.BackGroundId))
            {
                string folderPrefix = $"3d/env/live/live{live.BackGroundId}/";

                // 保留该舞台目录下全部 AssetBundle，不删 laser、light、monitor 等任何资源。
                foreach (var kv in main.AbList)
                {
                    UmaDatabaseEntry entry = kv.Value;
                    if (entry == null || !entry.IsAssetBundle)
                        continue;

                    bool keyMatches = kv.Key.StartsWith(
                        folderPrefix,
                        StringComparison.OrdinalIgnoreCase);

                    bool nameMatches = entry.Name != null && entry.Name.StartsWith(
                        folderPrefix,
                        StringComparison.OrdinalIgnoreCase);

                    if (keyMatches || nameMatches)
                        result.Add(entry);
                }
            }

            return result
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private void PreloadStageBundlesBeforeInstantiate(string bgId)
        {
            if (string.IsNullOrEmpty(bgId))
                return;

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return;

            string folderPrefix = $"3d/env/live/live{bgId}/";
            var required = new List<UmaDatabaseEntry>();

            foreach (var kv in main.AbList)
            {
                UmaDatabaseEntry entry = kv.Value;
                if (entry == null || !entry.IsAssetBundle)
                    continue;

                bool keyMatches = kv.Key.StartsWith(
                    folderPrefix,
                    StringComparison.OrdinalIgnoreCase);

                bool nameMatches = entry.Name != null && entry.Name.StartsWith(
                    folderPrefix,
                    StringComparison.OrdinalIgnoreCase);

                if (keyMatches || nameMatches)
                    required.AddRange(UmaAssetManager.SearchAB(main, entry));
            }

            // 兜底路径也只做“新增加载”，不调用任何 Unload；依赖去重后每个只处理一次。
            foreach (UmaDatabaseEntry entry in required
                         .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                         .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                         .Select(g => g.First()))
            {
                UmaAssetManager.LoadAssetBundle(
                    entry,
                    neverUnload: false,
                    isRecursive: false);
            }

            Debug.Log($"[StagePreloadFallback] bgId={bgId}, bundles={required.Count}");
        }
        private void InitializeMirrorReflections()
        {
            if (!_enableMirrorReflection)
                return;

            _mirrorReflections.Clear();

            AddMirrorReflections(_mirrorReflections, GetComponentsInChildren<MirrorReflection>(true));

            if (_stageController != null)
                AddMirrorReflections(_mirrorReflections, _stageController.GetComponentsInChildren<MirrorReflection>(true));

            if (_mirrorReflections.Count == 0)
                AddMirrorReflections(_mirrorReflections, FindObjectsOfType<MirrorReflection>(true));

            if (_mirrorReflections.Count == 0)
            {
                Debug.Log("[Mirror] No MirrorReflection found.");
                return;
            }

            Camera mainCam = null;
            if (_cameraObjects != null && _activeCameraIndex >= 0 && _activeCameraIndex < _cameraObjects.Length)
                mainCam = _cameraObjects[_activeCameraIndex];

            if (mainCam == null)
                mainCam = Camera.main;

            for (int i = 0; i < _mirrorReflections.Count; i++)
            {
                var mirror = _mirrorReflections[i];
                if (mirror == null) continue;

                mirror.Initialize(mainCam, i, false);
                mirror.SetupBaseCamera(mainCam, GetMainCameraFovFactor);
            }

            Debug.Log($"[Mirror] Initialized {_mirrorReflections.Count} mirrors.");
        }

        private static void AddMirrorReflections(List<MirrorReflection> target, MirrorReflection[] mirrors)
        {
            if (target == null || mirrors == null)
                return;

            for (int i = 0; i < mirrors.Length; i++)
            {
                var mirror = mirrors[i];
                if (mirror == null || target.Contains(mirror))
                    continue;

                target.Add(mirror);
            }
        }

        private float GetMainCameraFovFactor()
        {
            return 1f;
        }

        private void UpdateMirrorReflections()
        {
            if (_mirrorReflections == null || _mirrorReflections.Count == 0)
                return;

            Camera mainCam = null;
            if (_cameraObjects != null && _activeCameraIndex >= 0 && _activeCameraIndex < _cameraObjects.Length)
                mainCam = _cameraObjects[_activeCameraIndex];

            if (mainCam == null)
                mainCam = Camera.main;

            for (int i = 0; i < _mirrorReflections.Count; i++)
            {
                var mirror = _mirrorReflections[i];
                if (mirror == null) continue;

                mirror.SetBaseCamera(mainCam);
                mirror.SetFovFactorGetter(GetMainCameraFovFactor);
                mirror.ForceRenderOnce();
            }
        }
        private GallopImageEffect GetActivePostEffect()
        {
            Camera mainCamera = null;

            if (_cameraObjects != null &&
                _activeCameraIndex >= 0 &&
                _activeCameraIndex < _cameraObjects.Length)
            {
                mainCamera = _cameraObjects[_activeCameraIndex];
            }

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
                return null;

            if (_mainGallopImageEffect != null && _mainGallopImageEffect.gameObject == mainCamera.gameObject)
                return _mainGallopImageEffect;

            _mainGallopImageEffect =
                mainCamera.GetComponent<GallopImageEffect>();

            if (_mainGallopImageEffect == null)
            {
                _mainGallopImageEffect =
                    mainCamera.gameObject
                        .AddComponent<GallopImageEffect>();
            }

            return _mainGallopImageEffect;
        }
        private string _lastDofBypassReason;

        private void OnUpdatePostEffect_Dof(LiveTimelineKeyPostEffectDOFData key)
        {
            var effect = GetActivePostEffect();
            if (effect == null) return;
            var param = effect.DofDiffusionBloomOverlayParam;
            param.IsEnableDof = false;
            param.IsEnableOldDof = false;
            var renderCamera = MainRenderCamera;
            string bypassReason = null;
            // [REPORT §10.3-B3] 原生无跨相机 DOF bypass（专用多相机 DOF 轨道是独立轨道，
            // 主 DOF 轨道始终作用于活跃相机）。删除 master7 自造的
            // "renderCamera != DofTrackCamera（引用相等）即整体关闭 DOF"——
            // 该绑定导致多相机/镜面反射/相机切换帧整帧失焦。
            // 仅保留 renderCamera==null 的退化保护：WorldToViewportPoint/nearClipPlane
            // 求值需要相机，null 相机时无法算焦点（此时场景本身也无渲染相机）。
            if (key != null && renderCamera == null)
                bypassReason = "no render camera available for DOF evaluation";
            if (key != null && bypassReason == null)
            {
                // master5's screenshot-tested inference; official flag names remain unknown.
                bool metric = ((int)key.attribute & (1 << 17)) != 0;
                bool cameraTarget = ((int)key.attribute & (1 << 16)) != 0;
                Vector3 focus = Vector3.zero;
                bool valid = metric;
                if (!metric && cameraTarget)
                {
                    valid = _liveTimelineControl.HasDofCameraLookAt &&
                        _liveTimelineControl.DofLookAtCamera == renderCamera;
                    focus = _liveTimelineControl.DofCameraLookAt;
                }
                else if (!metric)
                {
                    int count = 0;
                    var locators = _liveTimelineControl.liveCharactorLocators;
                    for (int i = 0; i < locators.Length && i < 18; ++i)
                        if ((key.charactor & (1 << i)) != 0 && locators[i] != null)
                        { focus += locators[i].liveCharaHeadPosition; ++count; }
                    valid = count > 0;
                    if (valid) focus /= count;
                }
                // Validate before the backend normalizes depth. In particular, do
                // not clamp a behind-camera target to zero and render a full-frame blur.
                // [REPORT §10.3-B3/§4.4] 原生 PrepareDofParam 只做 focal01<0→0 的下限夹紧
                // （§4.4：只夹下限，无 Clamp01、无 farClipPlane 上限判定）。master7 曾把
                // metric 焦距再减 nearClipPlane 且要求 focusDepth<farClipPlane，使
                // 0<d<near 的近距焦点与超远焦点被二次关闭 DOF。对齐后只要求焦点在相机
                // 前方（>0）；相机背后目标仍按下述原注释保持关闭（不还原 focal01=0 全屏模糊）。
                float focusDepth = metric ? key.dofFocalPoint
                    : renderCamera.WorldToViewportPoint(focus).z;
                valid = valid && !float.IsNaN(focusDepth) && !float.IsInfinity(focusDepth) &&
                    focusDepth > 0.0001f;
                if (!valid) bypassReason = "missing or invalid focus depth";
                param.IsEnableDof = valid;
                param.DofFocalPosition = focus;
                param.DofFocalTransfrom = null;
                // [REPORT §10.3-B4/§4.4] 原生 PrepareDofParam(RVA 0x1a20030) 不钳制
                // DofFocalPoint 本身，只在后端对归一化 focalDistance01<0 夹 0
                // （DofDiffusionBloomOverlayPass.cs:433-434）。删除 master7 自加的
                // Max(0.01f)——它把 0~0.01m 的合法近距焦点系统性抬高到 0.01m，
                // 近距离特写 DOF 焦点被推远；setter 无隐藏钳制（直接存值）。
                param.DofFocalPoint = key.dofFocalPoint;
                // Unlike master5's render Parameter, these source-property setters
                // select Position/Transform/Point as a side effect. Restore the
                // resolved mode LAST, or the default 1m point overwrites look-at
                // and character focus before GallopImageEffect copies the state.
                param.DofFocalType = metric ? DepthBlurAndBloom.DofFocalType.Point : DepthBlurAndBloom.DofFocalType.Position;
                param.DofQualityType = key.dofQuality == 5 ? DepthBlurAndBloom.DofQuality.BackgroundAndForeground : DepthBlurAndBloom.DofQuality.OnlyBackground;
                param.DofFocalSize = Mathf.Max(0f, key.forcalSize);
                param.DofMaxFocalSize = Mathf.Max(param.DofMaxFocalSize, param.DofFocalSize);
                param.DofMaxBlurSpread = Mathf.Max(0f, key.blurSpread);
                param.DofForegroundSize = Mathf.Max(0f, key.dofForegroundSize);
                param.DofSmoothness = Mathf.Max(0.1f, key.dofSmoothness);
                param.DofBlurType = (DepthBlurAndBloom.DofBlur)Mathf.Clamp(key.dofBlurType, 0, 3);
                param.BallBlurPowerFactor = key.BallBlurPowerFactor;
                param.BallBlurBrightnessThreshhold = key.BallBlurBrightnessThreshhold;
                param.BallBlurBrightnessIntensity = key.BallBlurBrightnessIntensity;
                param.BallBlurSpread = key.BallBlurSpread;
            }
            if (bypassReason != _lastDofBypassReason)
            {
                _lastDofBypassReason = bypassReason;
                if (bypassReason != null)
                    Debug.LogWarning($"[LiveDOF] Bypass DOF only: {bypassReason}; time={_liveCurrentTime:F3}, " +
                        $"camera={renderCamera?.name}, key={key?.frame}, attr=0x{(key != null ? (int)key.attribute : 0):X}");
            }
            effect.ApplyBloomParameter();
        }

        private void OnUpdateColorCorrection(Gallop.RenderPipeline.SimpleColorCorrectionPass.Parameter sample)
        {
            Gallop.RenderPipeline.PostImageEffectFeature.RuntimeParameter.ColorCorrection = sample;
        }

        // [A7b] 原生 Director.OnUpdateExposure(RVA=0x1a63430) 三连：
        // GetActivePostEffect → get_ExposureParam 整块覆盖写（0x1a6349e-0x1a634ae，
        // IsEnable/DepthMask/Gain/Lift/MaskGain/MaskLift 顺序）→ 空值门后
        // CameraData.UpdateImageEffectParameter(0x1a0da30)。master7 的参数发布
        // 由 ApplyBloomParameter 统一完成（含 TargetCamera/DOF），因此这里只写参数，
        // 末尾调 ApplyBloomParameter 等价原生 UpdateImageEffectParameter。
        private void OnUpdateExposure(LiveExposureToneCurveTimeline.ExposureUpdateInfo info)
        {
            var effect = GetActivePostEffect();
            if (effect == null) return;
            // 原生无 null 门（事件触发即写），这里 GetActivePostEffect 的 null
            // 返回已在上方处理；直接整块覆盖。
            effect.ExposureParam.Set(info.IsEnable, info.DepthMask, info.Gain,
                info.Lift, info.MaskGain, info.MaskLift);
            effect.ApplyBloomParameter();
        }

        // [A7b] 原生 Director.OnUpdateToneCurve(RVA=0x1a63f80) 三连：
        // GetActivePostEffect → get_ToneCurveParam → IsValidity 门(0x1a64000)
        // 通过才拷贝曲线引用（0x1a64005-0x1a64026）→ UpdateImageEffectParameter。
        private void OnUpdateToneCurve(LiveExposureToneCurveTimeline.ToneCurveUpdateInfo info)
        {
            var effect = GetActivePostEffect();
            if (effect == null) return;
            effect.ToneCurveParam.Set(info.IsEnable, info.ToneCurve, info.MaskToneCurve);
            effect.ApplyBloomParameter();
        }

        private void OnUpdateRadialBlur(Gallop.RenderPipeline.RadialBlurPass.Parameter sample)
        {
            Gallop.RenderPipeline.PostImageEffectFeature.RuntimeParameter.RadialBlur = sample;
        }

        private void OnUpdateGlobalFog(LiveTimelineGlobalFogData fogData, LiveTimelineKeyGlobalFogData key)
        {
            if (key == null) return;

            // Height fog: apply via RenderSettings when isHeight is true.
            // Distance fog: apply via RenderSettings when isDistance is true.
            // Either branch enables scene fog; both false disables it.
            bool applyDistanceFog = key.isDistance && !key.isHeight;
            bool applyHeightFog   = key.isHeight;

            RenderSettings.fog      = applyDistanceFog || applyHeightFog;
            RenderSettings.fogColor = key.color;

            if (applyHeightFog)
            {
                RenderSettings.fogMode    = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = key.heightDensity;
                // Forward height and fog length as globals so stage shaders can consume them.
                float fogLength = Mathf.Max(0.001f, key.end - key.start);
                Shader.SetGlobalFloat("_Global_FogHeight",       key.height);
                Shader.SetGlobalFloat("_Global_FogHeightDensity", key.heightDensity);
                Shader.SetGlobalVector("_Global_FogLength",
                    new Vector4(fogLength, fogLength, fogLength, fogLength));
                Shader.SetGlobalVector("_Global_FogWorld_Origin", Vector4.zero);
            }
            else if (applyDistanceFog)
            {
                switch (key.fogMode)
                {
                    case 1:
                        RenderSettings.fogMode    = FogMode.Linear;
                        RenderSettings.fogStartDistance = key.start + key.startDistance;
                        RenderSettings.fogEndDistance   = key.end;
                        break;
                    case 2:
                        RenderSettings.fogMode    = FogMode.Exponential;
                        RenderSettings.fogDensity = key.expDensity;
                        break;
                    default:
                        RenderSettings.fogMode    = FogMode.ExponentialSquared;
                        RenderSettings.fogDensity = key.expDensity;
                        break;
                }
            }
        }

        private void OnUpdatePostFilm(int layer, Gallop.ImageEffect.ScreenOverlay.Overlay sample)
        {
            var effect = GetActivePostEffect();
            if (effect == null) return;
            var overlay = effect.DofDiffusionBloomOverlayParam.ScreenOverlay;
            var target = layer == 0 ? overlay.Overlay1 : layer == 1 ? overlay.Overlay2 : overlay.Overlay3;
            LivePostFilmTimeline.Copy(sample, target);
            // Publish only after all three layers, including disabled/empty tracks,
            // have replaced the active camera's old state.
            if (layer == 2) effect.ApplyBloomParameter();
        }

        private void OnUpdatePostEffect_BloomDiffusion(PostEffectUpdateInfo_BloomDiffusion updateInfo)
        {
            GallopImageEffect imageEffect = GetActivePostEffect();
            

            if (imageEffect == null) return;

            DofDiffusionBloomOverlayParam param =
                imageEffect.DofDiffusionBloomOverlayParam;

            param.IsEnableBloom =
                updateInfo.IsEnabledBloom;

            param.BloomDofWeight =
                updateInfo.bloomDofWeight;

            param.BloomThreshold =
                updateInfo.threshold;

            param.BloomIntensity =
                updateInfo.intensity;

            param.BloomBlurSize =
                updateInfo.BloomBlurSize;

            param.BloomBlendMode =
                updateInfo.BloomBlendMode;

            param.IsEnableDiffusion =
                updateInfo.IsEnabledDiffusion;

            param.DiffusionBlurSize =
                updateInfo.diffusionBlurSize;

            param.DiffusionBright =
                updateInfo.diffusionBright;

            param.DiffusionThreshold =
                updateInfo.diffusionThreshold;

            param.DiffusionSaturation =
                updateInfo.diffusionSaturation;

            param.DiffusionContrast =
                updateInfo.diffusionContrast;

            imageEffect.ApplyBloomParameter();
    //         Debug.Log(
    // $"[BloomDirector] activeCameraIndex={_activeCameraIndex}, " +
    // $"imageEffect={(imageEffect != null ? imageEffect.name : "null")}");
        }
        private void OnUpdateVolumeLight(LiveVolumeLightTimeline.VolumeUpdateInfo info)
        {
            _volumeLightController?.UpdateVolume(info);
        }

        private void OnUpdateLightShafts(LiveVolumeLightTimeline.ShaftsUpdateInfo info)
        {
            _volumeLightController?.UpdateLightShafts(info);
        }

        private void OnDestroy()
        {
            UnbindTimelineEvents();
            _footLightRuntime?.Dispose();
            _footLightRuntime = null;
            _livePropsController?.Dispose();
            _livePropsController = null;
            _stageParticleController?.Dispose();
            _effectController?.Dispose();
            _spotlightRuntime?.Dispose();
            _lightProjectionController?.Dispose();
            _mirrorBallProjectionTextures.Clear();
            _effectStageObjects.Clear();

            if (_instance == this)
            {
                Gallop.RenderPipeline.PostImageEffectFeature.ResetRuntimeParameter();
                LiveRuntimeDiagnostics.End();
                _instance = null;
            }
        }
        private void UnbindTimelineEvents()
        {
            if (_liveTimelineControl == null)
                return;

            if (_footLightRuntime != null)
                _liveTimelineControl.OnUpdateBgColor1 -= _footLightRuntime.ApplySpotlight;
            _liveTimelineControl.OnUpdatePostEffect_Dof -= OnUpdatePostEffect_Dof;
            _liveTimelineControl.OnUpdatePostFilm -= OnUpdatePostFilm;
            _liveTimelineControl.OnUpdateRadialBlur -= OnUpdateRadialBlur;
            _liveTimelineControl.OnUpdateColorCorrection -= OnUpdateColorCorrection;
            _liveTimelineControl.OnUpdateGlobalFog -= OnUpdateGlobalFog;
            _liveTimelineControl.OnUpdatePostEffect_BloomDiffusion -=
                OnUpdatePostEffect_BloomDiffusion;
            // [A7b] 原生 UnbindTimelineEvents(RVA=0x1a69920) 对应解除两条轨道。
            _liveTimelineControl.OnUpdateExposure -= OnUpdateExposure;
            _liveTimelineControl.OnUpdateToneCurve -= OnUpdateToneCurve;
            _liveTimelineControl.OnUpdateVolumeLight -= OnUpdateVolumeLight;
            _liveTimelineControl.OnUpdateLightShafts -= OnUpdateLightShafts;
            if (_stageParticleController != null)
            {
                _liveTimelineControl.OnUpdateParticle -= _stageParticleController.Update;
                _liveTimelineControl.OnUpdateParticleGroup -= _stageParticleController.UpdateGroup;
            }
            if (_effectController != null)
            {
                _liveTimelineControl.OnUpdateEffect -= _effectController.Update;
                _liveTimelineControl.OnUpdateEffectScale -= _effectController.UpdateScale;
            }
            if (_spotlightRuntime != null) _liveTimelineControl.OnUpdateSpotlight3d -= _spotlightRuntime.Update;
            if (_lightProjectionController != null)
                _liveTimelineControl.OnUpdateLightProjection -= _lightProjectionController.Apply;
        }
    }

}
