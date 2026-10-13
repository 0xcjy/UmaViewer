using Gallop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
//修改(载入通用服装ColorSet相关)(保存当前选中的颜色配置到TXT文件)
using System.Text;

public class UmaViewerUI : MonoBehaviour
{
    public static UmaViewerUI Instance;
    private UmaViewerMain Main => UmaViewerMain.Instance;
    private UmaViewerBuilder Builder => UmaViewerBuilder.Instance;

    public CanvasScaler canvasScaler;

    [Header("normal models")]
    public ScrollRect CharactersList;
    public ScrollRect MobCharactersList;
    public PageManager MobCharactersPageCtrl;
    public ScrollRect CostumeList;
    public ScrollRect HeadCostumeList;
    public ScrollRect MobCostumeList;
    public ScrollRect AnimationSetList;
    public ScrollRect AnimationList;
    public PageManager AnimationPageCtrl;

    [Header("mini models")]
    public ScrollRect MiniCharactersList;
    public ScrollRect MiniCostumeList;
    public ScrollRect MiniAnimationSetList;
    public ScrollRect MiniAnimationList;
    public PageManager MiniAnimationPageCtrl;

    [Header("other")]
    public ScrollRect PropList;
    public PageManager PropPageCtrl;
    public ScrollRect SceneList;
    public PageManager ScenePageCtrl;
    public ScrollRect MessageScrollRect;
    public TextMeshProUGUI MessageText;
    public PoseManager PoseManager;
    public HandleManager HandleManager;


    //修改(载入通用服装ColorSet相关)
    public ScrollRect DressColorSetList;


    [Header("lists")]
    public ScrollRect EmotionList;
    public Transform FacialList;
    public ScrollRect EarList;
    public ScrollRect EyeBrowList;
    public ScrollRect EyeList;
    public ScrollRect MouthList;
    public ScrollRect OtherList;
    public ScrollRect LiveList;
    public ScrollRect LiveSoundList;
    public ScrollRect LiveCharaSoundList;
    public ScrollRect NormalSoundList;
    public ScrollRect NormalSubSoundList;
    public PageManager NormalSoundCtrl;

    [Header("jukebox ui resources")]
    public Button JukeboxButton;
    public JukeboxSelectionView JukeboxPanel;

    private GameObject JukeboxSoundPanel;
    private ScrollRect JukeboxSoundList;
    private Image JukeboxSelectedJacket;
    private GameObject JukeboxSelectedPlaceholder;
    private Text JukeboxSelectedInfo;
    private Toggle JukeboxShortVersionToggle;
    private Toggle JukeboxGameSizeVersionToggle;
    private UmaUIContainer JukeboxMusicItemPrefab;
    private JukeboxMusicSelect JukeboxSelectedItem;
    private MasterJukeboxMusicData.JukeboxMusicData JukeboxSelectedMusic;
    private bool JukeboxUseGameSize;
    private bool JukeboxListLoaded;
    private bool JukeboxListLoading;

    [Header("pose mode")]
    public Transform HandlesPanel;

    [Header("live settings")]
    public Sprite CharaIconDefault;
    public Sprite CostumeIconDefault;
    public LiveCharacterSelect CurrentSeletChara;
    public GameObject LiveSelectPrefab;
    public GameObject LiveSelectPannel;
    public GameObject SelectCharacterPannel;
    public ScrollRect LiveSelectList;
    public Image LiveSelectImage;
    public Text LiveSelectInfoText;
    private LiveEntry currentLive;

    //(Live: selector de modo de voz y bloque VocalSelect)
    [Header("live vocal select")]
    public LiveVocalMode VocalMode = LiveVocalMode.SoloConcert;
    //-1 = sin elegir ("Random": el Director escoge una voz al azar para esa posición).
    public int[] VocalCharaIds = new int[] { -1, -1, -1, -1, -1, -1, -1 };
    private const int VocalMaxSlots = 7;
    //Rótulo de las comboboxes: techo 15 (el del "Label" de la escena) / suelo 10.
    private const int VocalComboFontMax = 15;
    private const int VocalComboFontMin = 10;
    //El desplegable se cierra y reabre en cada tecla al buscar; a 0.15 s (valor por defecto) la lista
    //parpadearía. 0.05 s lo deja en 3-4 frames, imperceptible.
    private const float VocalListFadeSpeed = 0.05f;
    private List<int> vocalOptions = new List<int>();
    private int vocalSongId = -1;
    private int vocalDropdownCount = 3;              //3 siempre visibles; los extras aparecen si SingerCount > 3
    private GameObject VocalSelectContainer;
    private TMP_Dropdown VocalModeDropdown;
    private readonly TMP_Dropdown[] VocalCharaDropdowns = new TMP_Dropdown[VocalMaxSlots];
    //vocalFilters[k]     = lo tecleado ahora mismo en el desplegable k ("" = sin filtro).
    //vocalEscuchando     = el desplegable que está leyendo el teclado, o -1 si ninguno.
    //VocalComboMarks[k]  = el texto "id o nombre" que invita a escribir (null si no se pudo crear).
    //VocalComboColors[k] = su color base, para atenuarlo cuando el desplegable no es editable.
    private readonly string[] vocalFilters = new string[VocalMaxSlots];
    private readonly TMP_Text[] VocalComboMarks = new TMP_Text[VocalMaxSlots];
    private readonly Color[] VocalComboColors = new Color[VocalMaxSlots];
    //vocalOptionIds[slot] = el id REAL de cada opción visible del desplegable k, en el MISMO orden que
    //dd.options (la posición 0 es el "-"/Random → -1). La lista enseña sólo el nombre, así que el id
    //se guarda aquí; SetVocalChara lo lee de esta lista (lo único que no cambia al filtrar).
    private readonly List<int>[] vocalOptionIds = new List<int>[VocalMaxSlots];
    private int vocalEscuchando = -1;
    //Al escribir cambia dd.options, pero un Dropdown ABIERTO tiene su propia copia de la lista: hay que
    //cerrar y reabrir. Estos flags evitan encadenar corrutinas:
    //  vocalListaQuiere[slot] = el usuario ha pedido ver la lista (se enciende al teclear).
    //  vocalListaEspera[slot] = hay un cierre+apertura en marcha para ese desplegable.
    //  vocalListaSucia[slot]  = se ha tecleado otra vez con el refresco a medias: hay que repetirlo.
    private readonly bool[] vocalListaQuiere = new bool[VocalMaxSlots];
    private readonly bool[] vocalListaEspera = new bool[VocalMaxSlots];
    private readonly bool[] vocalListaSucia = new bool[VocalMaxSlots];

    [Header("settings")]
    public UISettingsCamera CameraSettings;
    public UISettingsModel ModelSettings;
    public UISettingsSound AudioSettings;
    public UISettingsAnimation AnimationSettings;
    public UISettingsScreenshot ScreenshotSettings;
    public UISettingsAssets AssetSettings;
    public UISettingsOther OtherSettings;
    public List<GameObject> TogglablePanels = new List<GameObject>();
    public List<GameObject> TogglableFacials = new List<GameObject>();

    [Header("prefabs")]
    public GameObject UmaContainerPrefab;
    public GameObject UmaContainerCostumePrefab;
    public GameObject UmaContainerLivePrefab;
    public GameObject UmaContainerSliderPrefab;
    public UmaUIContainer UmaContainerTogglePrefab;

    public Color UIColor1, UIColor2;

    public string CurrentHeadCostumeId = string.Empty;

    [Header("Live")]
    public bool LiveTime = false;
    public bool isRecordVMD;
    public bool isRequireStage = true;


    //修改(Live演出的显隐控制和位置变换相关)
    public bool isControlMode = true;


    public Dropdown LiveRecoedToggle;
    public int LiveMode = 1;

    public FaceDrivenKeyTarget currentFaceDrivenKeyTarget;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        OtherSettings.ApplySettings();
        SetupJukeboxSoundUI();
        CameraSettings.AAModeDropdown.SetValueWithoutNotify(Config.Instance.AntiAliasing);
        AssetSettings.LoadedAssetsClear();
        UmaAssetManager.OnLoadedBundleUpdate += AssetSettings.LoadedAssetsAdd;
        UmaAssetManager.OnLoadedBundleRemove += AssetSettings.LoadedAssetsRemove;
        UmaAssetManager.OnLoadedBundleClear += AssetSettings.LoadedAssetsClear;
        
        PoseManager.LoadLocalPoseFiles();
#if UNITY_ANDROID && !UNITY_EDITOR
        canvasScaler.referenceResolution = new Vector2(1280, 720);
#endif
        StartCoroutine(ApplyGraphicsSettings());
    }

    private void OnDestroy()
    {
        UmaAssetManager.OnLoadedBundleUpdate -= AssetSettings.LoadedAssetsAdd;
        UmaAssetManager.OnLoadedBundleRemove -= AssetSettings.LoadedAssetsRemove;
        UmaAssetManager.OnLoadedBundleClear -= AssetSettings.LoadedAssetsClear;
    }

    private void Update()
    {
        if (Builder.CurrentAudioSources.Count > 0 && Builder.CurrentAudioSources[0])
        {
            AudioSource MianSource = Builder.CurrentAudioSources[0];
            if (MianSource.clip)
            {
                AudioSettings.UpdateTrack(MianSource);
            }
        }

        var umaContainer = Builder.CurrentUMAContainer;
        if (umaContainer != null && umaContainer.OverrideController != null)
        {
            AnimationSettings.UpdateAnimationInfo(umaContainer);
        }

        //(Live: búsqueda por teclado de los desplegables de voz. Sin InputField: el teclado se lee
        // aquí con Input.inputString y se dibuja en el propio rótulo del desplegable.)
        UpdateVocalSearch();
    }

    /// <summary>Some settings may not be saved when set in Start()</summary>
    public IEnumerator ApplyGraphicsSettings()
    {
        yield return 0;
        CameraSettings.AAModeDropdown.SetValueWithoutNotify(Config.Instance.AntiAliasing);
        GraphicsSettings.renderPipelineAsset = Config.Instance.Region == Region.Global ? null : UmaViewerMain.Instance.DefaultRenderPipeline;
        RuntimeGraphicsSettings.Apply();
    }

    public void HighlightChildImage(Transform mainObject, UmaUIContainer child)
    {
        foreach (var t in mainObject.GetComponentsInChildren<UmaUIContainer>())
        {
            if (t.transform.parent != mainObject) continue;
            t.ToggleImage.enabled = (t == child);
        }
    }

    public void LoadModelPanels()
    {
        var containerG = Instantiate(UmaContainerPrefab, AnimationSetList.content).GetComponent<UmaUIContainer>();
        containerG.Name = containerG.name = "Generic";
        containerG.Button.onClick.AddListener(() =>
        {
            HighlightChildImage(AnimationSetList.content, containerG);
            ListAnimations(-1, false);
        });

        var containerT = Instantiate(UmaContainerPrefab, AnimationSetList.content).GetComponent<UmaUIContainer>();
        containerT.Name = containerT.name = "Tail";
        containerT.Button.onClick.AddListener(() =>
        {
            HighlightChildImage(AnimationSetList.content, containerT);
            ListAnimations(-2, false);
        });

        var containerE = Instantiate(UmaContainerPrefab, AnimationSetList.content).GetComponent<UmaUIContainer>();
        containerE.Name = containerE.name = "Ear";
        containerE.Button.onClick.AddListener(() =>
        {
            HighlightChildImage(AnimationSetList.content, containerE);
            ListAnimations(-3, false);
        });

        foreach (var chara in Main.Characters.OrderBy(c => c.Id))
        {
            var charaInstance = chara;

            var container3 = Instantiate(UmaContainerPrefab,CharactersList.content).GetComponent<UmaUIContainer>();
            UIPressScaleFeedback.AddTo(container3.gameObject);
            container3.UseLocalizedText();
            container3.Name = container3.name = chara.Id + " " + chara.GetName();
            container3.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(CharactersList.content, container3);
                ListCostumes(charaInstance, false);
            });

            if (chara.Icon)
            {
                container3.Image.sprite = (chara.Icon == null ? CharaIconDefault : chara.Icon);
                container3.Image.enabled = true;
            }

            var container4 = Instantiate(UmaContainerPrefab,AnimationSetList.content).GetComponent<UmaUIContainer>();

            container4.UseLocalizedText();

            container4.Name = container4.name = chara.Id + " " + chara.GetName();
            container4.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(AnimationSetList.content, container4);
                ListAnimations(charaInstance.Id, false);
            });

            if (chara.Icon)
            {
                container4.Image.sprite = (chara.Icon == null ? CharaIconDefault : chara.Icon);
                container4.Image.enabled = true;
            }
        }

        var pageentrys = new List<PageManager.Entry>();
        foreach (var chara in Main.MobCharacters.OrderBy(c => c.Id))
        {
            var charaInstance = chara;
            var pageentry = new PageManager.Entry();

            pageentry.Name = chara.GetName();
            pageentry.UseLocalizedText = true;
            pageentry.EnablePressScaleFeedback = true;
            pageentry.OnClick = (container) =>
            {
                HighlightChildImage(MobCharactersList.content, container);
                ListCostumes(charaInstance, false);
            };
            if (chara.Icon)
            {
                pageentry.Sprite = chara.Icon;
            }
            pageentrys.Add(pageentry);
        }
        MobCharactersPageCtrl.Initialize(pageentrys, MobCharactersList);
    }

    public void LoadMiniModelPanels()
    {
        var container1 = Instantiate(UmaContainerPrefab, MiniAnimationSetList.content).GetComponent<UmaUIContainer>();
        container1.Name = container1.name = "General";
        container1.Button.onClick.AddListener(() =>
        {
            HighlightChildImage(MiniAnimationSetList.content, container1);
            ListAnimations(-1, true);
        });

        foreach (var chara in Main.Characters.OrderBy(c => c.Id))
        {
            var charaInstance = chara;
            var container2 = Instantiate(UmaContainerPrefab,MiniCharactersList.content).GetComponent<UmaUIContainer>();
            UIPressScaleFeedback.AddTo(container2.gameObject);
            container2.UseLocalizedText();
            container2.Name = container2.name = chara.Id + " " + chara.GetName();
            
            container2.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(MiniCharactersList.content, container2);
                ListCostumes(charaInstance, true);
            });

            if (chara.Icon)
            {
                container2.Image.sprite = chara.Icon;
                container2.Image.enabled = true;
            }

            var container3 = Instantiate(UmaContainerPrefab, MiniAnimationSetList.content).GetComponent<UmaUIContainer>();
            container3.UseLocalizedText();
            container3.Name = container3.name = chara.Id + " " + chara.GetName();
            container3.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(MiniAnimationSetList.content, container3);
                ListAnimations(charaInstance.Id, true);
            });

            if (chara.Icon)
            {
                container3.Image.sprite = chara.Icon;
                container3.Image.enabled = true;
            }
        }
    }

    public void LoadFacialPanels(FaceDrivenKeyTarget target)
    {
        currentFaceDrivenKeyTarget = target;
        if(FacialList)
        foreach (UmaUIContainer ui in FacialList.GetComponentsInChildren<UmaUIContainer>(true))
        {
            Destroy(ui.gameObject);
        }

        if (target == null) return;
        List<FacialMorph> targetMorph = new List<FacialMorph>(target.OtherMorphs);
        Action<GameObject, FaceDrivenKeyTarget, List<FacialMorph>, ScrollRect> action = (UmaContainerSliderPrefab, target, targetMorph, TargetList) =>
        {
            foreach (FacialMorph morph in targetMorph)
            {
                if (morph is FacialOtherMorph extraMorph)
                {
                    foreach (var prop in extraMorph.BindProperties)
                    {
                        var container = Instantiate(UmaContainerSliderPrefab, TargetList.content).GetComponent<UmaUIContainer>();
                        container.Name = $"{morph.name}({prop.Type})";
                        container.Slider.value = prop.Value;
                        switch (prop.Type)
                        {
                            case BindProperty.BindType.Shader:
                            case BindProperty.BindType.Texture:
                                container.Slider.maxValue = 1;
                                container.Slider.minValue = 0;
                                break;
                            case BindProperty.BindType.Select:
                                container.Slider.wholeNumbers = true;
                                container.Slider.maxValue = prop.BindPrefabs.Count - 1;
                                container.Slider.minValue = 0;
                                break;
                            case BindProperty.BindType.EyeSelect:
                                container.Slider.wholeNumbers = true;
                                container.Slider.maxValue = prop.BindPrefabs.Count / 2 - 1;
                                container.Slider.minValue = 0;
                                break;
                            case BindProperty.BindType.Enable:
                            case BindProperty.BindType.TearSelect:
                            case BindProperty.BindType.TearSide:
                                container.Slider.wholeNumbers = true;
                                container.Slider.maxValue = 1;
                                container.Slider.minValue = 0;
                                break;
                            case BindProperty.BindType.TearWeight:
                                container.Slider.maxValue = 1;
                                container.Slider.minValue = 0;
                                break;
                            case BindProperty.BindType.TearSpeed:
                                container.Slider.maxValue = 2;
                                container.Slider.minValue = 0;
                                break;
                        }
                        container.Slider.onValueChanged.AddListener((a) =>
                        {
                            target.ChangeMorphWeight(prop, a, morph);
                        });
                    }
                }
                else
                {
                    var container = Instantiate(UmaContainerSliderPrefab, TargetList.content).GetComponent<UmaUIContainer>();
                    container.Name = $"{morph.name}({morph.tag})";
                    container.Slider.value = morph.weight;
                    container.Slider.maxValue = 1;
                    container.Slider.minValue = morph.tag.Contains("Range") ? -1 : 0;
                    container.Slider.onValueChanged.AddListener((a) => { target.ChangeMorphWeight(morph, a); });
                }
            }
        };
        action(UmaContainerSliderPrefab, target, target.EarMorphs, EarList);
        action(UmaContainerSliderPrefab, target, target.EyeBrowMorphs, EyeBrowList);
        action(UmaContainerSliderPrefab, target, target.EyeMorphs, EyeList);
        action(UmaContainerSliderPrefab, target, target.MouthMorphs, MouthList);
        action(UmaContainerSliderPrefab, target, targetMorph, OtherList);
    }



    //修改(Mini角色表情滑条修复)
    public void LoadFacialPanelsMini(Material Eye, Material MayuL, Material MayuR, Material Mouth)
    {
        currentFaceDrivenKeyTarget = null;
        foreach (UmaUIContainer ui in FacialList.GetComponentsInChildren<UmaUIContainer>(true))
        {
            Destroy(ui.gameObject);
        }

        var action = new Action<ScrollRect, string, Material, int>((list, name, mat, type) =>
        {
            if (mat == null) return;
            var container = Instantiate(UmaContainerSliderPrefab, list.content).GetComponent<UmaUIContainer>();
            container.Name = name;
            container.Slider.wholeNumbers = true;
            container.Slider.value = 0;

            if (type == 0)
                container.Slider.maxValue = 18;
            else if (type == 1)
                container.Slider.maxValue = 14;
            else if (type == 2)
                container.Slider.maxValue = 14;
            else if (type == 3)
                container.Slider.maxValue = 8;

            container.Slider.minValue = 0;
            container.Slider.onValueChanged.AddListener((a) =>
            {
                var val = Convert.ToInt32(a);
                if (type == 0)
                {
                    mat.mainTextureOffset = new Vector2(0.25f * (val % 4), -0.125f * (val / 4));
                }
                else if (type == 1)
                {
                    var vector = mat.GetVector("_UVOffset");
                    mat.SetVector("_UVOffset", new Vector4(0.25f * (val % 4), -0.125f * (val / 4), vector.z, vector.w));
                }
                else if (type == 2)
                {
                    var vector = mat.GetVector("_UVOffset");
                    mat.SetVector("_UVOffset", new Vector4(vector.x, vector.y, 0.25f * (val % 4), -0.125f * (val / 4)));
                }
                else if (type == 3)
                {
                    mat.mainTextureOffset = new Vector2(0.25f * (val % 4), -0.25f * (val / 4));
                }
            });
        });

        action(EyeList, "Eye_L_Select", Eye, 2);
        action(EyeList, "Eye_R_Select", Eye, 1);
        action(MouthList, "Mouth_Select", Mouth, 0);
        action(EyeBrowList, "Mayu_L_Select", MayuL, 3);
        action(EyeBrowList, "Mayu_R_Select", MayuR, 3);
    }



    public void UpdateFacialPanels()
    {
        if (currentFaceDrivenKeyTarget)
            LoadFacialPanels(currentFaceDrivenKeyTarget);
    }

    public void LoadEmotionPanels(FaceEmotionKeyTarget target)
    {
        if (EmotionList)
        foreach (UmaUIContainer ui in EmotionList.content.GetComponentsInChildren<UmaUIContainer>())
        {
            Destroy(ui.gameObject);
        }

        if (target == null) return;

        foreach (var emotion in target.FaceEmotionKey)
        {
            if (emotion.label == "Base")
            {
                continue;
            }
            var container = Instantiate(UmaContainerSliderPrefab, EmotionList.content).GetComponent<UmaUIContainer>();
            container.Name = emotion.label;
            container.Slider.value = emotion.weight;
            container.Slider.maxValue = 1;
            container.Slider.minValue = 0;
            container.Slider.onValueChanged.AddListener((a) => { target.ChangeEmotionWeight(emotion, a); });
        }
    }

    public void LoadLivePanels()
    {
        Action<LiveEntry, GameObject, ScrollRect, UnityAction> createContainer = (live, containerPrefab, parent, onClickAction) =>
        {
            var container = Instantiate(containerPrefab, parent.content).GetComponent<UmaUIContainer>();
            container.Name = " " + live.MusicId + " " + live.SongName;
            container.Image.sprite = live.Icon;
            container.Image.enabled = container.Image.sprite;
            container.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(parent.content, container);
                onClickAction.Invoke();
            });
        };

        foreach (var live in Main.Lives.OrderBy(c => c.MusicId))
        {
            createContainer(live, UmaContainerLivePrefab, LiveList, () => ShowLiveSelectPanel(live));
            createContainer(live, UmaContainerLivePrefab, LiveSoundList, () => ListLiveSounds(live.MusicId));
        }
    }

    public void LoadNormalSoundPanels()
    {
        NormalSoundCtrl.Clear();
        var pageentrys = new List<PageManager.Entry>();
        foreach (var entry in Main.AbSounds.Where(a => (!a.Name.Contains("live")) && a.Name.EndsWith("awb")))
        {
            var pageentry = new PageManager.Entry();
            pageentry.Name = Path.GetFileNameWithoutExtension(entry.Name);
            pageentry.OnClick = (container) =>
            {
                HighlightChildImage(NormalSoundList.content, container);
                ListNormalSubSounds(entry);
            };
            pageentrys.Add(pageentry);
        }
        NormalSoundCtrl.Initialize(pageentrys, NormalSoundList);
    }

    private void SetupJukeboxSoundUI()
    {
        if (JukeboxButton == null || JukeboxPanel == null || JukeboxPanel.SongItemPrefab == null)
        {
            Debug.LogWarning(
                "[UmaViewerUI] Assign JukeboxButton, JukeboxPanel and its SongItemPrefab in the Inspector.");
            return;
        }

        if (JukeboxPanel.CloseButton == null ||
            JukeboxPanel.ShortVersionToggle == null ||
            JukeboxPanel.GameSizeVersionToggle == null ||
            JukeboxPanel.SelectedJacket == null ||
            JukeboxPanel.SelectedInfo == null ||
            JukeboxPanel.SongList == null ||
            JukeboxPanel.SongList.content == null ||
            JukeboxPanel.SongItemPrefab.Button == null ||
            JukeboxPanel.SongItemPrefab.Image == null)
        {
            Debug.LogWarning("[UmaViewerUI] JukeboxPanel has missing Inspector references.");
            return;
        }

        JukeboxSoundPanel = JukeboxPanel.gameObject;
        JukeboxSoundList = JukeboxPanel.SongList;
        JukeboxSelectedJacket = JukeboxPanel.SelectedJacket;
        JukeboxSelectedPlaceholder = JukeboxPanel.SelectedJacketPlaceholder;
        JukeboxSelectedInfo = JukeboxPanel.SelectedInfo;
        JukeboxShortVersionToggle = JukeboxPanel.ShortVersionToggle;
        JukeboxGameSizeVersionToggle = JukeboxPanel.GameSizeVersionToggle;
        JukeboxMusicItemPrefab = JukeboxPanel.SongItemPrefab;

        JukeboxButton.onClick.AddListener(OpenJukeboxSoundPanel);
        JukeboxPanel.CloseButton.onClick.AddListener(CloseJukeboxSoundPanel);
        JukeboxShortVersionToggle.onValueChanged.AddListener(OnJukeboxShortVersionChanged);
        JukeboxGameSizeVersionToggle.onValueChanged.AddListener(OnJukeboxGameSizeVersionChanged);
        JukeboxShortVersionToggle.SetIsOnWithoutNotify(true);
        JukeboxGameSizeVersionToggle.SetIsOnWithoutNotify(false);
        JukeboxSoundPanel.SetActive(false);
        RefreshJukeboxVersionUI();
    }

    private void OpenJukeboxSoundPanel()
    {
        JukeboxButton.interactable = false;
        JukeboxSoundPanel.transform.SetAsLastSibling();
        JukeboxSoundPanel.SetActive(true);
        LoadJukeboxSoundPanels();
    }

    private void CloseJukeboxSoundPanel()
    {
        if (JukeboxSoundPanel != null)
            JukeboxSoundPanel.SetActive(false);
        if (JukeboxButton != null)
            JukeboxButton.interactable = true;
    }

    private IEnumerator PopulateJukeboxGrid(List<MasterJukeboxMusicData.JukeboxMusicData> musicEntries)
    {
        for (int i = JukeboxSoundList.content.childCount - 1; i >= 0; i--)
            Destroy(JukeboxSoundList.content.GetChild(i).gameObject);

        for (int i = 0; i < musicEntries.Count; i++)
        {
            if (JukeboxSoundPanel == null || JukeboxSoundList == null)
                yield break;

            MasterJukeboxMusicData.JukeboxMusicData music = musicEntries[i];
            Sprite jacket = null;
            try
            {
                jacket = Builder.LoadLiveIcon(music.MusicId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UmaViewerUI] Could not load jacket for music {music.MusicId}: {ex.Message}");
            }

            JukeboxMusicSelect item = CreateJukeboxGridItem(music, jacket);
            if (JukeboxSelectedMusic == null)
                SelectJukeboxMusic(item, false);

            // AssetBundle loading is synchronous. Yield in small batches so opening
            // a large regional jukebox does not freeze the UI for one long frame.
            if ((i + 1) % 8 == 0)
                yield return null;
        }

        JukeboxSoundList.verticalNormalizedPosition = 1f;
        JukeboxListLoaded = true;
        JukeboxListLoading = false;

        if (musicEntries.Count == 0)
        {
            JukeboxSelectedInfo.text = "No playable music found";
        }
    }

    private JukeboxMusicSelect CreateJukeboxGridItem(
        MasterJukeboxMusicData.JukeboxMusicData music,
        Sprite jacket)
    {
        UmaUIContainer view = Instantiate(JukeboxMusicItemPrefab, JukeboxSoundList.content);
        JukeboxMusicSelect item = view.GetComponent<JukeboxMusicSelect>();
        if (item == null)
            item = view.gameObject.AddComponent<JukeboxMusicSelect>();

        string title = JukeboxUtil.ResolveSongTitle(music, UmaDatabaseController.Instance.LiveData);
        item.Initialize(this, view, music, jacket, title);
        return item;
    }

    public void SelectJukeboxMusic(JukeboxMusicSelect item, bool playImmediately = true)
    {
        if (item == null || item.Music == null)
            return;

        if (JukeboxSelectedItem != null)
            JukeboxSelectedItem.SetSelected(false);

        JukeboxSelectedItem = item;
        JukeboxSelectedItem.SetSelected(true);
        JukeboxSelectedMusic = item.Music;
        JukeboxSelectedJacket.sprite = item.Jacket;
        JukeboxSelectedJacket.enabled = item.Jacket != null;
        if (JukeboxSelectedPlaceholder != null)
            JukeboxSelectedPlaceholder.SetActive(item.Jacket == null);

        if (JukeboxUseGameSize && !item.Music.HasGameSizeAudio)
            JukeboxUseGameSize = false;
        else if (!JukeboxUseGameSize && !item.Music.HasShortAudio && item.Music.HasGameSizeAudio)
            JukeboxUseGameSize = true;

        RefreshJukeboxVersionUI();
        if (playImmediately)
            PlaySelectedJukeboxMusic();
    }

    private void SelectJukeboxVersion(bool useGameSize)
    {
        if (JukeboxSelectedMusic != null)
        {
            if (useGameSize && !JukeboxSelectedMusic.HasGameSizeAudio)
                return;
            if (!useGameSize && !JukeboxSelectedMusic.HasShortAudio)
                return;
        }

        JukeboxUseGameSize = useGameSize;
        RefreshJukeboxVersionUI();
        PlaySelectedJukeboxMusic();
    }

    private void OnJukeboxShortVersionChanged(bool isOn)
    {
        if (!isOn)
        {
            if (!JukeboxGameSizeVersionToggle.isOn)
                JukeboxShortVersionToggle.SetIsOnWithoutNotify(true);
            return;
        }

        JukeboxGameSizeVersionToggle.SetIsOnWithoutNotify(false);
        SelectJukeboxVersion(false);
    }

    private void OnJukeboxGameSizeVersionChanged(bool isOn)
    {
        if (!isOn)
        {
            if (!JukeboxShortVersionToggle.isOn)
                JukeboxGameSizeVersionToggle.SetIsOnWithoutNotify(true);
            return;
        }

        JukeboxShortVersionToggle.SetIsOnWithoutNotify(false);
        SelectJukeboxVersion(true);
    }

    private void RefreshJukeboxVersionUI()
    {
        bool hasShort = JukeboxSelectedMusic == null || JukeboxSelectedMusic.HasShortAudio;
        bool hasGameSize = JukeboxSelectedMusic == null || JukeboxSelectedMusic.HasGameSizeAudio;

        if (JukeboxShortVersionToggle != null)
        {
            JukeboxShortVersionToggle.interactable = hasShort;
            JukeboxShortVersionToggle.SetIsOnWithoutNotify(!JukeboxUseGameSize);
        }
        if (JukeboxGameSizeVersionToggle != null)
        {
            JukeboxGameSizeVersionToggle.interactable = hasGameSize;
            JukeboxGameSizeVersionToggle.SetIsOnWithoutNotify(JukeboxUseGameSize);
        }

        if (JukeboxSelectedMusic != null && JukeboxSelectedInfo != null)
        {
            string title = JukeboxUtil.ResolveSongTitle(
                JukeboxSelectedMusic,
                UmaDatabaseController.Instance.LiveData);
            string version = JukeboxUseGameSize ? "GAME SIZE Ver." : "SHORT Ver.";
            JukeboxSelectedInfo.text =
                $"{title}\nMUSIC ID  {JukeboxSelectedMusic.MusicId}\n{version}";
        }
    }

    private void PlaySelectedJukeboxMusic()
    {
        if (JukeboxSelectedMusic != null)
            HomeJukeboxController.Instance.SetupMusicAndPlay(
                JukeboxSelectedMusic.MusicId,
                JukeboxUseGameSize);
    }

    public void LoadJukeboxSoundPanels()
    {
        if (JukeboxSoundList == null || JukeboxListLoaded || JukeboxListLoading)
            return;

        try
        {
            MasterJukeboxMusicData master = UmaDatabaseController.Instance.JukeboxMusicData;
            if (master == null)
            {
                ShowMessage("Jukebox master data is not available.", UIMessageType.Warning);
                return;
            }

            var musicEntries = new List<MasterJukeboxMusicData.JukeboxMusicData>();
            foreach (MasterJukeboxMusicData.JukeboxMusicData music in master.GetAll())
            {
                if (!music.TryGetPreferredAudio(out _, out _))
                    continue;
                musicEntries.Add(music);
            }

            JukeboxListLoading = true;
            StartCoroutine(PopulateJukeboxGrid(musicEntries));
        }
        catch (Exception ex)
        {
            JukeboxListLoading = false;
            Debug.LogError($"[UmaViewerUI] Could not load Jukebox UI: {ex}");
            ShowMessage($"Could not load Jukebox: {ex.Message}", UIMessageType.Error);
        }
    }

    public void LoadPropPanel()
    {
        var pageentrys = new List<PageManager.Entry>();
        foreach (var prop in Main.AbList
            .Where(a => IsPreviewableProp(a.Key))
            .OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase))
        {
            var pageentry = new PageManager.Entry();
            pageentry.Name = Path.GetFileName(prop.Key);
            pageentry.OnClick = (container) =>
            {
                HighlightChildImage(PropList.content, container);
                Builder.LoadProp(prop.Value);
            };
            pageentrys.Add(pageentry);
        }
        PropPageCtrl.Initialize(pageentrys, PropList);
    }

    private static bool IsPreviewableProp(string path)
    {
        if (string.IsNullOrEmpty(path) || path.IndexOf("clothes", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        string fileName = Path.GetFileName(path);
        if (!fileName.StartsWith("pfb_", StringComparison.OrdinalIgnoreCase))
            return false;

        // Legacy assets use pfb_chr_prop; newer hand-held assets may be filed under
        // prop/item/accessory directories with different prefab names.
        return path.IndexOf("/prop/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/props/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/item/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/accessory/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               fileName.IndexOf("prop", StringComparison.OrdinalIgnoreCase) >= 0 ||
               fileName.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0 ||
               fileName.IndexOf("accessory", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void LoadMapPanel()
    {
        var pageentrys = new List<PageManager.Entry>();
        foreach (var scene in Main.AbList.Where(a => ((a.Key.StartsWith("3d/env") && Path.GetFileName(a.Key).StartsWith("pfb_")) || a.Key.StartsWith("cutt/cutt_son") && Path.GetFileName(a.Key).StartsWith("cutt_son"))))
        {
            var pageentry = new PageManager.Entry();
            pageentry.Name = Path.GetFileName(scene.Key);
            pageentry.FontSize = 19;
            pageentry.OnClick = (container) =>
            {
                HighlightChildImage(SceneList.content, container);
                Builder.LoadProp(scene.Value);
            };
            pageentrys.Add(pageentry);
        }
        ScenePageCtrl.Initialize(pageentrys, SceneList);
    }

    public void PlayLive()
    {
        if (currentLive == null) return;
        PoseManager.SetPoseMode(false);
        var selectlist = LiveSelectList.content.GetComponentsInChildren<LiveCharacterSelect>();
        if (selectlist != null)
        {
            LiveTime = true;
            ModelSettings.SetEyeTrackingEnable(false);
            Builder.LoadLive(currentLive, new List<LiveCharacterSelect>(selectlist), VocalMode, VocalCharaIds);
            LiveSelectPannel.SetActive(false);
        }
    }

    void ShowLiveSelectPanel(LiveEntry entry, bool loadPreview = true)
    {
        LiveSelectPannel.SetActive(true);
        if (loadPreview) Builder.loadLivePreviewSound(entry.MusicId);
        bool keepSelections = currentLive == entry;
        CurrentSeletChara = null;
        if (!keepSelections)
        {
            for (int i = LiveSelectList.content.childCount - 1; i >= 0; i--)
            {
                var child = LiveSelectList.content.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }

        currentLive = entry;
        LiveSelectImage.sprite = entry.Icon;
        int memberCount = entry.MemberCount;
        if (LiveMode == 0)
        {
            memberCount = Main.AbMotions.Count(a => a.Name.StartsWith($"3d/motion/live/body/son{entry.MusicId}") && Path.GetFileName(a.Name).Split('_').Length == 4);
            LiveSelectInfoText.text = $"{entry.SongName}\nMain Members Count: {memberCount}";
        }
        else
        {
            LiveSelectInfoText.text = $"{entry.SongName}\nMembers Count: {memberCount}";
        }

        var selections = new List<LiveCharacterSelect>(LiveSelectList.content.GetComponentsInChildren<LiveCharacterSelect>());
        for (int i = selections.Count - 1; i >= memberCount; i--)
        {
            selections[i].transform.SetParent(null);
            Destroy(selections[i].gameObject);
            selections.RemoveAt(i);
        }
        for (int i = selections.Count; i < memberCount; i++)
        {
            var chara = Instantiate(LiveSelectPrefab, LiveSelectList.content).GetComponent<LiveCharacterSelect>();
            chara.IndexText.text = (i + 1).ToString();
            chara.GetComponent<Button>().onClick.AddListener(() =>
            {
                chara.SelectChara(this);
                foreach (var t in LiveSelectList.content.GetComponentsInChildren<LiveCharacterSelect>())
                {
                    t.GetComponentInChildren<Text>().color = (t == chara ? (LiveMode == 1 ? Color.green : Color.white) : Color.black);
                }
            });
            selections.Add(chara);
        }
        foreach (var selection in selections)
            selection.GetComponentInChildren<Text>().color = Color.black;
        if (!LiveAutoSelection.FillMissing(Main, entry, selections, CostumeIconDefault))
            ShowMessage("Cannot fill the live selection: no available character-owned winning costume matches an empty slot.", UIMessageType.Warning);

        //(Live: bloque de desplegables de voz — se resuelve y se rellena al final, cuando ya existen
        //los slots. El diseño vive en la escena; aquí sólo se cablea el comportamiento.)
        BuildVocalSelectors();
        RefreshVocalOptions(entry.MusicId);
    }

    void ListCostumes(CharaEntry chara, bool mini)
    {
        ScrollRect costumeList = mini ? MiniCostumeList : chara.IsMob ? MobCostumeList : CostumeList;
        for (int i = costumeList.content.childCount - 1; i >= 0; i--)
        {
            Destroy(costumeList.content.GetChild(i).gameObject);
        }

        Action<CharaEntry> action = delegate (CharaEntry achara)
        {
            string nameVar = mini ? $"pfb_mbdy{achara.Id}" : $"pfb_bdy{achara.Id}";
            string bodyPath = mini ? UmaDatabaseController.MiniBodyPath : UmaDatabaseController.BodyPath;
            foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith(bodyPath) && !a.Name.Contains("clothes") && a.Name.Contains(nameVar)))
            {
                var container = Instantiate(UmaContainerCostumePrefab, costumeList.content).GetComponent<UmaUIContainer>();
                UIPressScaleFeedback.AddTo(container.gameObject);
                string[] split = entry.Name.Split('_');
                string costumeId = split[split.Length - 1];
                var dressdata = Main.Costumes.FirstOrDefault(a => (a.CharaId == achara.Id && a.BodyTypeSub == int.Parse(costumeId)));
                container.Name = container.name = GetCostumeName(costumeId, (dressdata == null ? costumeId : dressdata.DressName));
                container.Image.sprite = (dressdata == null ? CostumeIconDefault : dressdata.Icon);
                container.Image.enabled = true;
                container.Id = costumeId;
                container.Button.onClick.AddListener(() =>
                {
                    if (LiveSelectPannel.activeInHierarchy && CurrentSeletChara)
                    {
                        CurrentSeletChara.SetValue(achara, costumeId, container.Image.sprite, CurrentHeadCostumeId);
                    }
                    else
                    {
                        HighlightChildImage(costumeList.content, container);
                        var list = new List<UmaDatabaseEntry>();
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.BodyPath) && a.Name.Contains(chara.Id.ToString())));
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.HeadPath) && a.Name.Contains(chara.Id.ToString())));
                        var tail_path = getCharaTailPath(chara);
                        if (!string.IsNullOrEmpty(tail_path))
                        {
                            list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(tail_path)));
                        }
                        list.Add(Main.AbList["3d/animator/drivenkeylocator"]);
                        list.Add(Main.AbList[$"3d/motion/event/body/chara/chr{achara.Id}_00/anm_eve_chr{achara.Id}_00_idle01_loop"]);

                        Builder.UnloadUma();
                        //UmaAssetManager.UnloadAllBundle(true);
                        UmaAssetManager.PreLoadAndRun(list , delegate {
                            StartCoroutine(Builder.LoadUma(achara, costumeId, mini, CurrentHeadCostumeId));
                        });
                        //StartCoroutine(Builder.LoadUma(achara, costumeId, mini));
                    }
                });
            }
        };

        if (!chara.IsMob)
        {
            action.Invoke(chara);
        }

        //Common costumes
        List<string> costumes = new List<string>();
        var nameVar = mini ? "pfb_mbdy0" : $"pfb_bdy0";
        string bodyPath = mini ? UmaDatabaseController.MiniBodyPath : UmaDatabaseController.BodyPath;
        foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith(bodyPath) && !a.Name.Contains("/clothes/") && a.Name.Contains(nameVar)))
        {
            string id = Path.GetFileName(entry.Name);
            string[] split = id.Split('_');
            if (split.Length < 4) continue; //prevents error for mini umas
            id = split[1].Substring(mini ? 4 : 3) + "_" + split[2] + "_" + split[3];
            if (!costumes.Contains(id))
            {
                costumes.Add(id);
                string costumeId = id;
                var container = Instantiate(UmaContainerCostumePrefab, costumeList.content).GetComponent<UmaUIContainer>();
                UIPressScaleFeedback.AddTo(container.gameObject);
                var data = costumeId.Split('_');
                var bodytype = int.Parse(data[0]);
                var bodysub = int.Parse(data[1]);

                var dressdata = Main.Costumes.FirstOrDefault(a => a.BodyType == bodytype && a.BodyTypeSub == bodysub);
                container.Name = container.name = GetCostumeName(id, (dressdata == null ? id : dressdata.DressName));
                container.Image.sprite = (dressdata == null ? CostumeIconDefault : dressdata.Icon);
                container.Image.enabled = true;
                container.Id = costumeId;
                container.Button.onClick.AddListener(() =>
                {
                    if (LiveSelectPannel.activeInHierarchy && CurrentSeletChara)
                    {
                        CurrentSeletChara.SetValue(chara, costumeId, container.Image.sprite, CurrentHeadCostumeId);
                    }
                    else
                    {
                        HighlightChildImage(costumeList.content, container);
                        var list = new List<UmaDatabaseEntry>();
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.BodyPath) && a.Name.Contains(chara.Id.ToString())));
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.HeadPath) && a.Name.Contains(chara.Id.ToString())));
                        var tail_path = getCharaTailPath(chara);
                        if (!string.IsNullOrEmpty(tail_path))
                        {
                            list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(tail_path)));
                        }
                        list.Add(Main.AbList["3d/animator/drivenkeylocator"]);
                        var motion_path = $"3d/motion/event/body/chara/chr{chara.Id}_00/anm_eve_chr{chara.Id}_00_idle01_loop";
                        if (!chara.IsMob && !ModelSettings.IsHeadFix && Main.AbList.TryGetValue(motion_path, out var motion_entry))
                        {
                            list.Add(motion_entry);
                        }

                        Builder.UnloadUma();
                        //UmaAssetManager.UnloadAllBundle(true);
                        UmaAssetManager.PreLoadAndRun(list, delegate { StartCoroutine(Builder.LoadUma(chara, costumeId, mini, CurrentHeadCostumeId)); });
                        //StartCoroutine(Builder.LoadUma(achara, costumeId, mini));
                    }
                });
            }
        }

        if(!chara.IsMob && !mini)
        {
            for (int i = HeadCostumeList.content.childCount - 1; i >= 0; i--)
            {
                Destroy(HeadCostumeList.content.GetChild(i).gameObject);
            }
            CurrentHeadCostumeId = string.Empty;
            nameVar = $"/pfb_chr{chara.Id}_";
            foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith($"{UmaDatabaseController.HeadPath}chr{chara.Id}_") && !a.Name.Contains("clothes") && a.Name.Contains(nameVar)))
            {
                var container = Instantiate(UmaContainerCostumePrefab, HeadCostumeList.content).GetComponent<UmaUIContainer>();
                UIPressScaleFeedback.AddTo(container.gameObject);
                string[] split = entry.Name.Split('_');
                string head_costumeId = split[split.Length - 1];
                var dressdata = Main.Costumes.FirstOrDefault(a => (a.CharaId == chara.Id && a.BodyTypeSub == int.Parse(head_costumeId)));
                container.Name = container.name = GetCostumeName(head_costumeId, (dressdata == null ? head_costumeId : dressdata.DressName));
                container.Image.sprite = (dressdata == null ? CostumeIconDefault : dressdata.Icon);
                container.Image.enabled = true;
                container.Id = head_costumeId;
                container.Button.onClick.AddListener(() =>
                {
                    CurrentHeadCostumeId = head_costumeId;
                    if (!LiveSelectPannel.activeInHierarchy || !CurrentSeletChara)
                    {
                        HighlightChildImage(HeadCostumeList.content, container);
                    }
                    bool hasBreak = false;
                    foreach (var t in CostumeList.GetComponentsInChildren<UmaUIContainer>())
                    {
                        if (t.ToggleImage.enabled)
                        {
                            hasBreak = true;
                            t.Button.onClick.Invoke();
                            break;
                        }
                        else if(CurrentSeletChara && CurrentSeletChara.CostumeId == t.Id)
                        {
                            hasBreak = true;
                            t.Button.onClick.Invoke();
                            break;
                        }
                    }
                    if (!hasBreak)
                    {
                        //if no costume selected, select first costume
                        if (CostumeList.content.childCount > 0)
                        {
                            CostumeList.content.GetChild(0).GetComponent<UmaUIContainer>().Button.onClick.Invoke();
                        }
                    }
                });
            }
        }
    }

    void ListLiveSounds(int songid)
    {
        for (int i = LiveCharaSoundList.content.childCount - 1; i >= 0; i--)
        {
            Destroy(LiveCharaSoundList.content.GetChild(i).gameObject);
        }
        string nameVar = $"snd_bgm_live_{songid}_chara";
        foreach (var entry in Main.AbSounds.Where(a => a.Name.Contains(nameVar) && a.Name.EndsWith("awb")))
        {
            var container = Instantiate(UmaContainerPrefab, LiveCharaSoundList.content).GetComponent<UmaUIContainer>();
            string[] split = Path.GetFileNameWithoutExtension(entry.Name).Split('_');
            string name = $"{split[split.Length - 2]} {getCharaName(split[split.Length - 2])}";
            container.Name = name;

            var icon = Main.Characters.Find(a => a.Id.ToString().Equals(split[split.Length - 2]));
            if (icon != null)
            {
                container.Image.sprite = icon.Icon;
                container.Image.enabled = true;
            }

            container.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(LiveCharaSoundList.content, container);
                Builder.LoadLiveSound(songid, entry);
            });
        }

    }

    void ListNormalSubSounds(UmaDatabaseEntry awb)
    {
        for (int i = NormalSubSoundList.content.childCount - 1; i >= 0; i--)
        {
            Destroy(NormalSubSoundList.content.GetChild(i).gameObject);
        }

        var subSounds = UmaViewerBuilder.LoadAudio(awb);

        foreach (var sound in subSounds)
        {
            var container = Instantiate(UmaContainerPrefab, NormalSubSoundList.content).GetComponent<UmaUIContainer>();
            container.Name = $"Clip_{subSounds.IndexOf(sound)}";
            container.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(NormalSubSoundList.content, container);
                Builder.SetLastAudio(awb, subSounds.IndexOf(sound));
                Builder.PlaySound(awb, subSounds.IndexOf(sound));
            });
        }
    }

    void ListAnimations(int umaId, bool mini)
    {
        ScrollRect animationList = mini ? MiniAnimationList : AnimationList;
        PageManager pageManager = mini ? MiniAnimationPageCtrl : AnimationPageCtrl;
        for (int i = animationList.content.childCount - 1; i >= 0; i--)
        {
            Destroy(animationList.content.GetChild(i).gameObject);
        }

        var filteredList = Main.AbMotions.Where(a => !a.Name.Contains($"mirror")
        && (mini ? a.Name.Contains($"mini") : !a.Name.Contains($"mini"))
        && !a.Name.Contains($"facial")
        && !a.Name.Contains($"_cam")
        && !a.Name.EndsWith($"_s")
        && !a.Name.EndsWith($"_e")
        );

        if (umaId == -1)
        {
            var pageentrys = new List<PageManager.Entry>();
            foreach (var entry in filteredList.Where(a => (a.Name.Contains($"/type0") || a.Name.Contains($"/type99") || a.Name.Contains($"anm_sty_")) && !a.Name.Contains($"/tail") && !a.Name.EndsWith($"_pos") && !a.Name.Contains($"prop") && !a.Name.EndsWith($"_pose") && !a.Name.Contains($"_defaultmotion")))
            {
                var pageentry = new PageManager.Entry();
                pageentry.Name = Path.GetFileName(entry.Name);
                pageentry.FontSize = 20;
                pageentry.OnClick = (container) =>
                {
                    HighlightChildImage(animationList.content, container);
                    (Builder.CurrentUMAContainer)?.LoadAnimation(entry);
                    LoadedAnimation();
                };
                pageentrys.Add(pageentry);
            }
            pageManager.Initialize(pageentrys, animationList);
        }
        else if (umaId == -2)
        {
            foreach (var entry in filteredList.Where(a => a.Name.Contains($"/tail")))
            {
                CreateAnimationSelectionPanel(entry, animationList.content);
            }
            pageManager.ResetCtrl();
        }
        else if (umaId == -3)
        {
            foreach (var entry in Main.AbMotions.Where(a => a.Name.Contains($"type00_ear") && !a.Name.EndsWith("driven") && !a.Name.Contains("touch")))
            {
                CreateAnimationSelectionPanel(entry, animationList.content);
            }
            pageManager.ResetCtrl();
        }
        else
        {
            //Common animations
            foreach (var entry in filteredList.Where(a => a.Name.Contains($"chara/chr{umaId}") && !a.Name.Contains("pose")))
            {
                CreateAnimationSelectionPanel(entry, animationList.content);
            }

            //Skill animations
            foreach (var entry in filteredList.Where(a => a.Name.Contains($"card/body/crd{umaId}")))
            {
                CreateAnimationSelectionPanel(entry, animationList.content);
            }
            pageManager.ResetCtrl();
        }
    }

    void CreateAnimationSelectionPanel(UmaDatabaseEntry entry, Transform parent)
    {
        var container = Instantiate(UmaContainerPrefab, parent).GetComponent<UmaUIContainer>();
        container.Name = container.name = Path.GetFileName(entry.Name);
        container.FontSize = 20;
        container.Button.onClick.AddListener(() =>
        {
            HighlightChildImage(parent, container);
            (Builder.CurrentUMAContainer)?.LoadAnimation(entry);
            LoadedAnimation();
        });
    }

    string getCharaName(string id)
    {
        var entry = Main.Characters.FirstOrDefault(a => a.Id.ToString().Equals(id));
        return (entry == null) ? id.ToString() : entry.GetName();
    }

    public string getCharaTailPath(CharaEntry chara)
    {
        int tailId = 0;
        if (chara.IsMob)
        {
            tailId = 1;
        }
        else
        {
            var charaData = UmaDatabaseController.ReadCharaData(chara);
            tailId = Convert.ToInt32(charaData["tail_model_id"]);
        }
        if (tailId <= 0)
        {
            return string.Empty;
        }
        var tailName = $"tail{tailId.ToString().PadLeft(4, '0')}_00";
        var tailPath = $"3d/chara/tail/{tailName}/";
        var tailPfb = tailPath + $"pfb_{tailName}";
        return tailPfb;
    }

    public static string GetCostumeName(string costumeId, string defaultname)
    {
        switch (costumeId)
        {
            // case "00":return "Default";
            case "90":
                return "Upgraded";
            case "0001_00_01":
                return "Race Shorts";
            case "0001_00_02":
                return "Race Bloomers";
            case "0002_00_00":
                return "School Short Sleeves";
            case "0002_00_03":
                return "School Short Sleeves Big Belly";
            case "0002_01_00":
                return "School Long Sleeves";
            case "0002_01_03":
                return "School Long Sleeves Big Belly";
            case "0003_00_01":
                return "Tracksuit Shorts";
            case "0003_00_02":
                return "Tracksuit Bloomers";
            case "0003_01_01":
                return "Tracksuit Long Pants";
            case "0003_01_02":
                return "Tracksuit Rolled Up Pants";
            case "0004_00_00":
                return "Swimsuit";
            case "0004_01_00":
                return "Towel";
            default:
                return (defaultname == "00") ? "Default" : defaultname;
        }
    }

    public void LoadedAnimation()
    {
        var container = Builder.CurrentUMAContainer;
        if (!container || !container.UmaAnimator) return;
        AnimationSettings.ProgressSlider.SetValueWithoutNotify(0);
        // Reset settings by Panel
        container.UmaAnimator.speed = AnimationSettings.SpeedSlider.value;
        Builder.AnimationCameraAnimator.speed = AnimationSettings.SpeedSlider.value;
        if (container.UmaFaceAnimator)
            container.UmaFaceAnimator.speed = AnimationSettings.SpeedSlider.value;
    }

    /// <summary> Toggles one object ON and all others from UI.TogglablePanels list OFF </summary>
    public void ToggleUIPanel(GameObject go)
    {

        if (go.activeSelf || !TogglablePanels.Contains(go))
        {
            go.SetActive(!go.activeSelf);
            return;
        }

        foreach (var panel in TogglablePanels)
        {
            panel.SetActive(panel == go);
        }
    }

    public void ToggleUIFacial(GameObject go)
    {
        if (go.activeSelf || !TogglableFacials.Contains(go))
        {
            go.SetActive(!go.activeSelf);
            return;
        }

        foreach (var panel in TogglableFacials)
        {
            panel.SetActive(panel == go);
        }
    }

    public void RecordVMD()
    {
        var container = Builder.CurrentUMAContainer;
        var camera = Builder.AnimationCamera;
        var buttonText = AnimationSettings.VMDButton.GetComponentInChildren<TextMeshProUGUI>();

        if (!container || container.IsMini)
        {
            buttonText.text = string.Format("<color=#FF0000>{0}</color>", "Need Normal UMA");
            return;
        }

        var rootbone = container.transform.Find("Position");
        if (rootbone.gameObject.TryGetComponent(out UnityHumanoidVMDRecorder recorder))
        {
            if (recorder.IsRecording)
            {
                if (camera.enabled)
                {
                    var cameraRecorder = camera.GetComponent<UnityCameraVMDRecorder>();
                    if (cameraRecorder != null)
                    {
                        cameraRecorder.StopRecording();
                        cameraRecorder.SaveVMD();
                    }
                }
                recorder.StopRecording();
                buttonText.text = "Saving";
                recorder.SaveVMD(container.name, Config.Instance.VmdKeyReductionLevel);
                buttonText.text = "Record VMD";
                ShowMessage($"VMD is saved in {Path.GetFullPath(Application.dataPath + UnityHumanoidVMDRecorder.FileSavePath)}", UIMessageType.Success);
            }
        }
        else
        {
            var newRecorder = rootbone.gameObject.AddComponent<UnityHumanoidVMDRecorder>();
            newRecorder.Initialize();
            if (!newRecorder.IsRecording)
            {
                if (camera.enabled)
                {
                    var cameraRecorder = camera.gameObject.AddComponent<UnityCameraVMDRecorder>();
                    cameraRecorder.Initialize();
                    cameraRecorder.StartRecording();
                }
                newRecorder.StartRecording();
                buttonText.text = "Recording...";
            }
        }
    }

    public void UpdateLiveMode(int val)
    {
        LiveMode = val;
        if (LiveSelectPannel.activeSelf && currentLive != null)
            ShowLiveSelectPanel(currentLive, false);
    }

    public void SetLiveRecordMode(bool val) { isRecordVMD = val; }

    public void SetLiveRequireStage(bool val) { isRequireStage = val; }


    //修改(Live演出的显隐控制和位置变换相关)
    public void SetLiveControlMode(bool val) { isControlMode = val; }



    public void ShowMessage(string msg, UIMessageType type)
    {
        if (!MessageText) return;
        MessageText.text += type switch
        {
            UIMessageType.Error => string.Format("<color=red>{0}</color>\n", msg),
            UIMessageType.Warning => string.Format("<color=yellow>{0}</color>\n", msg),
            UIMessageType.Success => string.Format("<color=green>{0}</color>\n", msg),
            UIMessageType.Close => string.Format("<color=green>{0}</color>\n", msg),
            _ => $"{msg}\n",
        };
        MessageScrollRect.gameObject.SetActive(type != UIMessageType.Close);
        LayoutRebuilder.ForceRebuildLayoutImmediate(MessageScrollRect.content);
        MessageScrollRect.verticalNormalizedPosition = 0;
    }

    public void ToggleVisible(GameObject go)
    {
        go.SetActive(!go.activeSelf);
    }




    //修改(载入通用服装ColorSet相关)(颜色组UI)
    public static string CurrentSelectedColorSetId;

    public void LoadColorSetButtons()
    {
        ClearColorSetButtons();

        var uma = Builder.CurrentUMAContainer;
        if (uma == null || string.IsNullOrEmpty(uma.VarCostumeIdShort)) return;

        var parts = uma.VarCostumeIdShort.Split('_');
        if (parts.Length < 2) return;

        if (!int.TryParse(parts[0], out int bodyType)) return;
        if (!int.TryParse(parts[1], out int bodyTypeSub)) return;

        if (bodyType == 7 || bodyType == 8)
        {
            var colorSetIds = UmaDatabaseController.Instance.CharaDressColor
                .Select(row => row["id"].ToString());

            foreach (var id in colorSetIds)
            {
                var buttonObj = Instantiate(UmaContainerPrefab, DressColorSetList.content);
                var container = buttonObj.GetComponent<UmaUIContainer>();

                container.Name = id;
                var buttonText = buttonObj.GetComponentInChildren<Text>();
                if (buttonText != null) buttonText.text = id;

                container.Button.onClick.AddListener(() =>
                {
                    HighlightChildImage(DressColorSetList.content, container);
                    CurrentSelectedColorSetId = id;
                    Builder.CurrentUMAContainer?.RefreshDressColor();
                });

                if (container.ToggleImage != null)
                    container.ToggleImage.enabled = (id == CurrentSelectedColorSetId);
            }
            return;
        }

        var dressRow = UmaDatabaseController.Instance.DressData.FirstOrDefault(row =>
            int.Parse(row["body_type"].ToString()) == bodyType &&
            int.Parse(row["body_type_sub"].ToString()) == bodyTypeSub);

        if (dressRow == null || dressRow["body_setting"].ToString() != "4") return;

        string dressId = dressRow["id"].ToString();

        var colorSetRows = UmaDatabaseController.Instance.CharaDressColor
            .Where(row => row["dress_id"].ToString() == dressId);

        foreach (var row in colorSetRows)
        {
            string id = row["id"].ToString();

            var buttonObj = Instantiate(UmaContainerPrefab, DressColorSetList.content);
            var container = buttonObj.GetComponent<UmaUIContainer>();

            container.Name = id;
            var buttonText = buttonObj.GetComponentInChildren<Text>();
            if (buttonText != null) buttonText.text = id;

            container.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(DressColorSetList.content, container);
                CurrentSelectedColorSetId = id;
                Builder.CurrentUMAContainer?.RefreshDressColor();
            });

            if (container.ToggleImage != null)
                container.ToggleImage.enabled = (id == CurrentSelectedColorSetId);
        }
    }



    //修改(载入通用服装ColorSet相关)(清理所有颜色组按钮)
    public void ClearColorSetButtons()
    {
        foreach (Transform child in DressColorSetList.content)
        {
            Destroy(child.gameObject);
        }
    }


    
    //修改(载入通用服装ColorSet相关)(解析颜色字符串)
    private static bool TryParseHexColor(
        string hex,
        out Tuple<byte, byte, byte> rgb255,
        out Tuple<float, float, float> rgbNormalized)
    {
        rgb255 = null;
        rgbNormalized = null;

        if (string.IsNullOrWhiteSpace(hex) || !hex.StartsWith("#") || hex.Length != 7)
            return false;

        byte r, g, b;
        bool okR = byte.TryParse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, null, out r);
        bool okG = byte.TryParse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, null, out g);
        bool okB = byte.TryParse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, null, out b);

        if (!okR || !okG || !okB)
            return false;

        rgb255 = Tuple.Create(r, g, b);
        rgbNormalized = Tuple.Create(r / 255f, g / 255f, b / 255f);
        return true;
    }



    //修改(载入通用服装ColorSet相关)(保存当前选中的颜色配置到TXT文件)
    public static void SaveCurrentColorSetRow()
    {
        if (string.IsNullOrEmpty(CurrentSelectedColorSetId))
        {
            UmaViewerUI.Instance.ShowMessage("No color set ID selected!", UIMessageType.Error);
            return;
        }

        var charaColor = UmaDatabaseController.Instance.CharaDressColor
            .FirstOrDefault(row => row["id"].ToString() == CurrentSelectedColorSetId);

        if (charaColor == null)
        {
            UmaViewerUI.Instance.ShowMessage($"Color set ID {CurrentSelectedColorSetId} not found!", UIMessageType.Error);
            return;
        }

    #if UNITY_ANDROID && !UNITY_EDITOR
        string fileDirectory = Application.persistentDataPath + "/../ColorCode/";
    #else
        string fileDirectory = Application.dataPath + "/../ColorCode/";
    #endif
        Directory.CreateDirectory(fileDirectory);

        string idValue = charaColor["id"]?.ToString() ?? "unknown";
        string fileName = $"{idValue}.txt";
        string fullPath = Path.GetFullPath(Path.Combine(fileDirectory, fileName));

        List<string> lines = new List<string>();
        foreach (var column in charaColor.Table.Columns)
        {
            string columnName = column.ToString();
            string value = charaColor[columnName]?.ToString() ?? "";

            if (TryParseHexColor(value, out var rgb255, out var rgbNormalized))
            {
                string prefix = (columnName + ":").PadRight(17);
                lines.Add(
                    $"{prefix}\t{value}\t" +
                    $"({rgb255.Item1,3}, {rgb255.Item2,3}, {rgb255.Item3,3})\t" +
                    $"({rgbNormalized.Item1:0.####}, {rgbNormalized.Item2:0.####}, {rgbNormalized.Item3:0.####})"
                );
            }
            else
            {
                lines.Add($"{columnName}: {value}");
            }
        }

        string content = string.Join("\n", lines);
        File.WriteAllText(fullPath, content, Encoding.UTF8);

        UmaViewerUI.Instance.ShowMessage($"ColorSet data saved: {fullPath}", UIMessageType.Success);
    }



    //==========================================================================
    // (Live: bloque VocalSelect — selector de modo de voz y comboboxes de cantantes)
    // El diseño (posición/tamaño/rótulos) vive en Version2.unity; aquí SÓLO se resuelve por nombre
    // y se cablea el comportamiento. Nada se redimensiona ni se recoloca.
    //==========================================================================

    //(Live: búsqueda por teclado de los desplegables de voz, leída A MANO, sin InputField.)
    //  1) Un clic dentro de un desplegable de voz (rótulo o flecha) abre su lista y empieza a
    //     escuchar el teclado.
    //  2) Lo tecleado filtra SÓLO ese desplegable y la lista, ya abierta, se rehace con el filtro.
    //Por qué sin InputField: Show() selecciona el item marcado (Dropdown.cs) y un InputField dentro de
    //un Dropdown abierto acaba perdiendo el foco en cada tecla (OnFocus -> SelectAll). Leyendo
    //Input.inputString no hace falta que el teclado vaya a ninguna parte (Input Manager clásico).
    void UpdateVocalSearch()
    {
        //--- 1) El clic: dentro de un desplegable de voz = abrir la lista y escuchar.
        if (Input.GetMouseButtonDown(0))
        {
            //Si el clic cae en una lista de voz YA ABIERTA, el usuario está eligiendo una entrada:
            //no se toca nada y se deja que SetVocalChara lo limpie. (El Update de este script y el del
            //EventSystem van en orden desconocido, así que esta comprobación es imprescindible.)
            if (!HayListaDeVozBajoElPuntero())
            {
                int slot = VocalDropdownBajoElPuntero();
                if (slot >= 0) BeginVocalSearch(slot);
                else TerminarBusquedaVoz();
            }
            return;
        }

        if (vocalEscuchando < 0) return;

        //Si el desplegable que escuchaba ha dejado de estar disponible (se cerró el panel, o el modo
        //pasó a Solo concert y se apagó), se suelta el teclado en vez de seguir filtrando en vano.
        var activo = VocalCharaDropdowns[vocalEscuchando];
        if (activo == null || !activo.gameObject.activeInHierarchy || !activo.IsInteractable())
        {
            TerminarBusquedaVoz();
            return;
        }

        //--- 2) El teclado: cada carácter añadido o borrado refiltra la lista abierta.
        string tecleado = Input.inputString;
        if (string.IsNullOrEmpty(tecleado)) return;

        var buffer = new StringBuilder(vocalFilters[vocalEscuchando] ?? "");
        bool cambio = false;
        foreach (var c in tecleado)
        {
            if (c == '\b')                          //Retroceso: Input.inputString lo devuelve como '\b'.
            {
                if (buffer.Length > 0) buffer.Length--;
                cambio = true;
            }
            else if (char.IsControl(c)) continue;   //Enter/tabulador y demás no son texto.
            else
            {
                buffer.Append(c);
                cambio = true;
            }
        }
        if (!cambio) return;

        int k = vocalEscuchando;
        vocalFilters[k] = buffer.ToString();
        //Orden importante: Show() construye la lista con las opciones que haya EN ESE MOMENTO, así que
        //hay que filtrar antes de pedir que se abra otra vez.
        RebuildVocalDropdownOptions(k);
        KeepVocalListOpen(k);
    }

    //Cámara con la que resolver los rect del panel de voz (Overlay → null).
    Camera VocalCanvasCamara()
    {
        var canvas = VocalSelectContainer != null ? VocalSelectContainer.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        return canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    //El desplegable de voz sobre el que está el ratón, o -1. Se usa el rect COMPLETO del desplegable,
    //así un clic en el rótulo hace lo mismo que un clic en la flecha. Sólo responden los visibles e
    //interactables (en Solo concert los apagados no se abren).
    int VocalDropdownBajoElPuntero()
    {
        var cam = VocalCanvasCamara();
        var pos = Input.mousePosition;
        for (int k = 0; k < VocalMaxSlots; k++)
        {
            var dd = VocalCharaDropdowns[k];
            if (dd == null) continue;
            if (!dd.gameObject.activeInHierarchy || !dd.IsInteractable()) continue;
            var rt = dd.transform as RectTransform;
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return k;
        }
        return -1;
    }

    //¿Hay alguna lista de voz ABIERTA bajo el puntero? Se busca por nombre ("Dropdown List") desde
    //dd.template.parent, porque Show() clona la plantilla y cuelga la copia junto a ella (no del
    //desplegable). activeInHierarchy descarta las copias que se están desvaneciendo de un Hide().
    bool HayListaDeVozBajoElPuntero()
    {
        var cam = VocalCanvasCamara();
        var pos = Input.mousePosition;
        for (int k = 0; k < VocalMaxSlots; k++)
        {
            var dd = VocalCharaDropdowns[k];
            if (dd == null || dd.template == null || dd.template.parent == null) continue;
            var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < hijos.Length; i++)
            {
                var lista = hijos[i];
                if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;
                var rt = lista as RectTransform;
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return true;
            }
        }
        return false;
    }

    //Busca un hijo por nombre a cualquier profundidad; respaldo a toda la escena por si el bloque no
    //cuelga del panel. No hace falta tocar el YAML ni el Inspector.
    static GameObject FindChildByName(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == name) return t.gameObject;
        }
        foreach (var t in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
            {
                if (c != null && c.name == name) return c.gameObject;
            }
        }
        return null;
    }

    //Busca un hijo por nombre SIN salir del subárbol indicado. Hace falta para la marca "id o nombre":
    //FindChildByName tiene un respaldo a TODA la escena que confundiría la marca de un desplegable con
    //la de otro (la primera creada "ganaría" y los demás desplegables se quedarían sin la suya).
    static Transform FindChildByNameScoped(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == name) return t;
        }
        return null;
    }

    //TMP_Dropdown por nombre dentro de un contenedor (el componente tiene que estar en ese mismo
    //GameObject, que es como están en la escena: los desplegables del proyecto son TMPro.TMP_Dropdown).
    static TMP_Dropdown FindTMPDropdownUnder(Transform root, string name)
    {
        var go = FindChildByName(root, name);
        return go != null ? go.GetComponent<TMP_Dropdown>() : null;
    }

    //Los desplegables de voz pueden llamarse "VocalCharaDropdownN" o, si se duplicaron en la escena,
    //"VocalCharaDropdown (N)": Unity añade el sufijo con espacio y paréntesis al duplicar. Se aceptan
    //las dos formas.
    TMP_Dropdown FindVocalCharaDropdown(int slot)
    {
        var dd = FindTMPDropdownUnder(VocalSelectContainer.transform, $"VocalCharaDropdown{slot + 1}");
        if (dd != null) return dd;
        return FindTMPDropdownUnder(VocalSelectContainer.transform, $"VocalCharaDropdown ({slot + 1})");
    }

    //Resuelve el bloque VocalSelect y cablea sus desplegables. Se busca por nombre desde el panel Live.
    void BuildVocalSelectors()
    {
        if (VocalSelectContainer != null) return;   //ya resuelto
        if (LiveSelectPannel == null) return;

        VocalSelectContainer = FindChildByName(LiveSelectPannel.transform, "VocalSelect");
        if (VocalSelectContainer == null)
        {
            Debug.LogWarning("[UmaViewerUI] No se encuentra el bloque 'VocalSelect' en el panel Live: " +
                             "no habrá selector de voz. (¿Se ha borrado de Version2.unity?)");
            return;
        }

        VocalModeDropdown = FindTMPDropdownUnder(VocalSelectContainer.transform, "VocalModeDropdown");
        if (VocalModeDropdown != null)
        {
            //No arrastrar el onValueChanged persistente del YAML (si lo tuviera): se sustituye entero.
            VocalModeDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            VocalModeDropdown.options.Clear();
            VocalModeDropdown.options.Add(new TMP_Dropdown.OptionData("Solo concert"));
            VocalModeDropdown.options.Add(new TMP_Dropdown.OptionData("Mix voices"));
            VocalModeDropdown.SetValueWithoutNotify((int)VocalMode);
            VocalModeDropdown.RefreshShownValue();
            VocalModeDropdown.onValueChanged.AddListener(SetVocalMode);
        }
        else
        {
            Debug.LogWarning("[UmaViewerUI] No se encuentra 'VocalModeDropdown' dentro de VocalSelect.");
        }

        for (int k = 0; k < VocalMaxSlots; k++)
        {
            //Copia por iteración (int slot = k): en C# la variable de un `for` es UNA sola y una lambda
            //que la capture acaba viendo el valor final (7).
            int slot = k;
            var dd = FindVocalCharaDropdown(slot);
            if (dd == null)
            {
                Debug.LogWarning($"[UmaViewerUI] No se encuentra 'VocalCharaDropdown{slot + 1}' dentro de VocalSelect.");
                continue;
            }
            dd.onValueChanged = new TMP_Dropdown.DropdownEvent();
            dd.interactable = true;
            dd.options.Clear();
            dd.RefreshShownValue();
            dd.onValueChanged.AddListener(idx => SetVocalChara(slot, idx));
            VocalCharaDropdowns[slot] = dd;
            PrepareVocalComboCaption(dd, slot);
        }

        //El LiveInfo es sólo texto: que no se coma los clics de los desplegables.
        if (LiveSelectInfoText != null) LiveSelectInfoText.raycastTarget = false;
    }

    //Rellena la lista de cantantes que suenan en la canción (mismas fuentes que el panel Sounds) y
    //reconstruye los desplegables. Se descarta toda cantante cuyo .awb no esté en disco: elegirla
    //dejaría su pista muda sin explicación. Si el filtro deja la lista vacía, se vuelve a la completa.
    void RefreshVocalOptions(int songid)
    {
        if (VocalSelectContainer == null) return;
        vocalSongId = songid;

        IEnumerable<UmaDatabaseEntry> VocalAwbs()
        {
            return Main.AbSounds
                .Where(a => a.Name.Contains($"snd_bgm_live_{songid}_chara") && a.Name.EndsWith("awb"));
        }

        static int ExtractCharaId(UmaDatabaseEntry a)
        {
            var split = Path.GetFileNameWithoutExtension(a.Name).Split('_');
            return int.TryParse(split[split.Length - 2], out int id) ? id : -1;
        }

        var allVocalIds = VocalAwbs().Select(ExtractCharaId).Where(id => id >= 0).Distinct().ToList();
        var presentIds = VocalAwbs()
            .Where(a => { try { return File.Exists(a.Path); } catch { return false; } })
            .Select(ExtractCharaId).Where(id => id >= 0).Distinct().ToList();

        int descartadas = allVocalIds.Count - presentIds.Count;
        vocalOptions = (presentIds.Count > 0 ? presentIds : allVocalIds).OrderBy(x => x).ToList();
        if (descartadas > 0 && presentIds.Count > 0)
        {
            Debug.Log($"[UmaViewerUI] Cancion {songid}: {descartadas} cantante(s) oculta(s) por no tener " +
                      $"su .awb en disco ({presentIds.Count} disponibles).");
        }

        //Cambiar de canción cambia el elenco: se limpian filtros y selecciones antes de reconstruir.
        for (int k = 0; k < VocalMaxSlots; k++) vocalFilters[k] = "";
        EnsureVocalCharaIdsLength();
        for (int k = 0; k < VocalMaxSlots; k++) VocalCharaIds[k] = -1;
        RebuildAllVocalDropdowns();

        ApplyVocalDefaults();
        RefreshVocalDropdownVisibility();
        RequestVocalSingerCount(songid);
    }

    //nº de cantantes de la tabla _part (que normalmente sólo se carga al entrar en el live). Aquí se
    //replica esa carga en el menú para poder mostrar los desplegables extra (hasta 7).
    void RequestVocalSingerCount(int songid)
    {
        vocalDropdownCount = 3;   //3 siempre visibles; los extras aparecen si SingerCount > 3
        string partName = $"live/musicscores/m{songid}/m{songid}_part";
        if (Main == null || !Main.AbList.TryGetValue(partName, out UmaDatabaseEntry partAsset) || partAsset == null)
            return;   //la canción no trae _part → se queda en 3 desplegables

        UmaAssetManager.PreLoadAndRun(new List<UmaDatabaseEntry> { partAsset }, () =>
        {
            //El usuario pudo cambiar de canción mientras cargaba el bundle.
            if (songid != vocalSongId || VocalSelectContainer == null) return;
            try
            {
                AssetBundle bundle = UmaAssetManager.LoadAssetBundle(partAsset);
                var partData = bundle?.LoadAsset<TextAsset>($"m{songid}_part");
                if (partData == null) return;
                int singers = new PartEntry(partData.text).SingerCount;
                vocalDropdownCount = Mathf.Clamp(singers, 3, VocalMaxSlots);
                //Si hay más cantantes que opciones listadas, no tiene sentido mostrar celdas vacías.
                vocalDropdownCount = Mathf.Min(vocalDropdownCount, Mathf.Max(vocalOptions.Count, 3));
                RefreshVocalDropdownVisibility();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UmaViewerUI] No se pudo leer el _part de la cancion {songid}: {e.Message}");
            }
        });
    }

    //Valor por defecto de TODO el bloque: modo Solo concert y todos los desplegables en "Random" (-1),
    //que es la opción 0.
    void ApplyVocalDefaults()
    {
        EnsureVocalCharaIdsLength();
        VocalMode = LiveVocalMode.SoloConcert;
        if (VocalModeDropdown != null)
        {
            VocalModeDropdown.SetValueWithoutNotify((int)VocalMode);
            VocalModeDropdown.RefreshShownValue();
        }
        for (int k = 0; k < VocalMaxSlots; k++) VocalCharaIds[k] = -1;
        RebuildAllVocalDropdowns();
    }

    //Visibilidad del bloque:
    //  · Solo concert → SÓLO el desplegable 1 (los 2..7 se ocultan).
    //  · Mix voices   → tantos como pida la canción (vocalDropdownCount, 1..7).
    //Sólo se muestra/oculta con SetActive: no se redimensiona ni se recoloca nada.
    void RefreshVocalDropdownVisibility()
    {
        int visibles = (VocalMode == LiveVocalMode.SoloConcert) ? 1 : vocalDropdownCount;
        for (int k = 0; k < VocalMaxSlots; k++)
        {
            var dd = VocalCharaDropdowns[k];
            if (dd == null) continue;
            dd.gameObject.SetActive(k < visibles);
            dd.interactable = (VocalMode == LiveVocalMode.MixVoices || k == 0);
            RefreshVocalComboInteractable(k, dd.interactable);
        }
        if (VocalModeDropdown != null) VocalModeDropdown.gameObject.SetActive(true);
    }

    void RebuildAllVocalDropdowns()
    {
        for (int k = 0; k < VocalMaxSlots; k++) RebuildVocalDropdownOptions(k);
    }

    //Construye las opciones del desplegable 'slot' a partir de 'vocalOptions', aplicando el texto
    //tecleado y respetando lo ya elegido en VocalCharaIds. El índice visible es un DERIVADO de
    //VocalCharaIds, no la fuente de verdad, así que filtrar la lista nunca "pierde" una selección.
    //El TEXTO de la opción es SÓLO el nombre; el id se guarda en paralelo en vocalOptionIds[slot].
    void RebuildVocalDropdownOptions(int slot)
    {
        EnsureVocalCharaIdsLength();
        var dd = VocalCharaDropdowns[slot];
        if (dd == null) return;
        int elegida = VocalCharaIds[slot];
        var visibles = FilterVocalOptions(slot);

        //La lista de ids se construye a la vez y en el MISMO orden que dd.options. Este método es el
        //ÚNICO sitio que toca dd.options, así que no pueden separarse.
        var ids = new List<int>(visibles.Count + 2);
        dd.options.Clear();
        ids.Add(-1);                                    //-1 = "Random" (opción por defecto)
        dd.options.Add(new TMP_Dropdown.OptionData("Random"));
        foreach (var id in visibles)
        {
            ids.Add(id);
            dd.options.Add(new TMP_Dropdown.OptionData(VocalOptionLabel(id)));
        }
        //Si el filtro deja fuera la voz ya elegida se mete igual: si no, el desplegable enseñaría "-"
        //mientras VocalCharaIds sigue guardando el id (síntoma del bug de las voces equivocadas).
        if (elegida >= 0 && !visibles.Contains(elegida))
        {
            ids.Add(elegida);
            dd.options.Add(new TMP_Dropdown.OptionData(VocalOptionLabel(elegida)));
        }
        vocalOptionIds[slot] = ids;

        int idx = 0;
        if (elegida >= 0)
        {
            int f = ids.IndexOf(elegida);   //Se busca por ID, no por texto (el texto ya no lleva el id).
            if (f >= 0) idx = f;
        }
        dd.SetValueWithoutNotify(idx);
        dd.RefreshShownValue();
        RefreshVocalComboText(slot);
    }

    //Lo que se pinta de una voz en la lista: SÓLO el nombre. getCharaName() devuelve el propio id
    //cuando no encuentra al personaje, así que en ese caso se deja el id (mejor que un blanco).
    string VocalOptionLabel(int id)
    {
        string nombre = getCharaName(id.ToString());
        return string.IsNullOrEmpty(nombre) ? id.ToString() : nombre;
    }

    //Lista reducida según lo tecleado en el desplegable 'slot'. Casa por id o por nombre, sin
    //distinguir mayúsculas. Sin texto devuelve la lista completa SIN COPIAR.
    List<int> FilterVocalOptions(int slot)
    {
        string f = (vocalFilters[slot] ?? "").Trim();
        if (f.Length == 0) return vocalOptions;
        return vocalOptions
            .Where(id => id.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (getCharaName(id.ToString()) ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
    }

    //El usuario acaba de hacer clic dentro del desplegable 'slot': empieza la búsqueda, limpia el
    //filtro anterior y abre la lista. Show() es idempotente (si m_Dropdown != null no hace nada).
    void BeginVocalSearch(int slot)
    {
        var dd = VocalCharaDropdowns[slot];
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return;

        vocalEscuchando = slot;
        if (!string.IsNullOrEmpty(vocalFilters[slot]))
        {
            vocalFilters[slot] = "";
            RebuildVocalDropdownOptions(slot);
        }
        vocalListaQuiere[slot] = true;
        vocalListaSucia[slot] = false;
        dd.Show();
    }

    //Se deja de escuchar el teclado (clic fuera). Se suelta el filtro a medias para que el rótulo
    //vuelva a enseñar la voz que realmente está sonando.
    void TerminarBusquedaVoz()
    {
        int slot = vocalEscuchando;
        vocalEscuchando = -1;
        if (slot < 0) return;
        var dd = VocalCharaDropdowns[slot];
        if (dd != null && dd.IsActive()) dd.Hide();
        if (string.IsNullOrEmpty(vocalFilters[slot])) return;
        vocalFilters[slot] = "";
        RebuildVocalDropdownOptions(slot);
    }

    //Deja el desplegable 'slot' con la lista a la vista. Si ya estaba abierto, su copia de la lista no
    //ve el filtro nuevo: hay que rehacerla (lo hace RefreshVocalOpenList, que espera el retardo).
    void KeepVocalListOpen(int slot)
    {
        var dd = VocalCharaDropdowns[slot];
        if (dd == null || !dd.IsInteractable()) return;

        vocalListaQuiere[slot] = true;
        if (!dd.IsActive())
        {
            vocalListaSucia[slot] = false;
            dd.Show();
            return;
        }
        if (vocalListaEspera[slot]) { vocalListaSucia[slot] = true; return; }   //ya hay un refresco
        vocalListaEspera[slot] = true;
        StartCoroutine(RefreshVocalOpenList(slot, dd));
    }

    //Rehace la lista ABIERTA de un desplegable para que enseñe el filtro. Es un bucle porque se teclea
    //más rápido de lo que tarda el retardo: si entra otra tecla con el refresco a medias, dd.options ya
    //está al día y basta con repetir el cierre+apertura.
    IEnumerator RefreshVocalOpenList(int slot, TMP_Dropdown dd)
    {
        while (true)
        {
            //Hide() desvanece la lista y la destruye VocalListFadeSpeed después; Show() no reabre
            //mientras esa copia siga viva. Hay que esperar a que pase el retardo, con margen.
            dd.Hide();
            yield return new WaitForSecondsRealtime(VocalListFadeSpeed + 0.02f);
            vocalListaSucia[slot] = false;
            //Si mientras tanto se eligió una opción, se soltó el desplegable o cambió el modo, NO se
            //reabre: dejarle la lista encima de la voz recién elegida sería una sorpresa desagradable.
            if (!vocalListaQuiere[slot]) break;
            if (VocalCharaDropdowns[slot] != dd || !dd.IsInteractable()) break;
            dd.Show();
            if (!vocalListaSucia[slot]) break;
        }
        vocalListaEspera[slot] = false;
        vocalListaSucia[slot] = false;
    }

    //Qué debe verse en el rótulo del desplegable 'slot': lo tecleado si hay filtro; si no, la opción
    //elegida. Sin filtro y con "Random" se ve "Random". La marca "id o nombre" sólo se enciende cuando
    //no hay texto. El rótulo es el propio captionText del Dropdown.
    void RefreshVocalComboText(int slot)
    {
        var dd = VocalCharaDropdowns[slot];
        if (dd == null) return;
        var cap = dd.captionText;
        if (cap == null) return;

        string texto = vocalFilters[slot] ?? "";
        if (texto.Length == 0 && dd.value >= 0 && dd.value < dd.options.Count)
            texto = dd.options[dd.value].text;

        if (!string.Equals(cap.text, texto, StringComparison.Ordinal))
            cap.text = texto;

        var marca = VocalComboMarks[slot];
        if (marca != null) marca.enabled = (texto.Length == 0);
    }

    //Atenúa el rótulo junto con su desplegable, para que se note que en Solo concert sólo se puede
    //cambiar la voz 1.
    void RefreshVocalComboInteractable(int slot, bool interactable)
    {
        var dd = VocalCharaDropdowns[slot];
        if (dd == null) return;
        var baseColor = VocalComboColors[slot];
        var cap = dd.captionText;
        if (cap != null)
        {
            var c = baseColor;
            c.a = baseColor.a * (interactable ? 1f : 0.45f);
            cap.color = c;
        }
        var marca = VocalComboMarks[slot];
        if (marca != null)
        {
            var pc = baseColor;
            pc.a = baseColor.a * (interactable ? 0.55f : 0.25f);
            marca.color = pc;
        }
    }

    //Deja el rótulo de un desplegable de voz listo para ser la parte visible de una COMBOBOX: se pinta
    //en el captionText del propio desplegable y se le cuelga encima la marca "id o nombre". No se monta
    //ningún InputField (el teclado se lee a mano).
    void PrepareVocalComboCaption(TMP_Dropdown dd, int slot)
    {
        if (dd == null) return;
        var cap = dd.captionText;
        if (cap == null)
        {
            Debug.LogWarning($"[UmaViewerUI] El desplegable de voz {slot + 1} no tiene rótulo: se queda " +
                             "como desplegable normal (sin poder buscar).");
            return;
        }

        //El fundido se baja a 0.05 s porque la combobox cierra y reabre en cada tecla.
        dd.alphaFadeSpeed = VocalListFadeSpeed;

        //(Sin ajuste de línea y con desbordamiento: el rótulo se queda en UNA línea; con el auto
        //tamaño los nombres largos se encogen entre el techo (15) y el suelo (10).)
        cap.enableWordWrapping = false;
        cap.overflowMode = TextOverflowModes.Overflow;
        cap.enableAutoSizing = true;
        cap.fontSizeMin = VocalComboFontMin;
        cap.fontSizeMax = VocalComboFontMax;
        cap.richText = false;
        //font 15 también en las OPCIONES de la lista (itemText), no sólo en el rótulo.
        if (dd.itemText != null)
        {
            dd.itemText.enableAutoSizing = false;
            dd.itemText.fontSize = VocalComboFontMax;
        }
        //Ni el rótulo ni la marca se llevan ningún clic: tiene que llegar al Dropdown.
        cap.raycastTarget = false;

        VocalComboColors[slot] = cap.color;

        //(La marca se cuelga DEL PROPIO RÓTULO y se estira a su rect. Si el desplegable ya trae un hijo
        //"VocalComboHint" se REUTILIZA; si no, se crea.)
        TMP_Text marca = null;
        var hintExistente = FindChildByNameScoped(cap.transform, "VocalComboHint");
        if (hintExistente != null) marca = hintExistente.GetComponent<TMP_Text>();
        if (marca == null) marca = MakeVocalFieldChild("VocalComboHint", cap.transform);
        marca.font = cap.font;
        marca.fontStyle = FontStyles.Italic;
        marca.fontSize = VocalComboFontMax;
        marca.alignment = cap.alignment;
        marca.text = "id o nombre";
        marca.richText = false;
        marca.raycastTarget = false;
        marca.enableWordWrapping = false;
        marca.overflowMode = TextOverflowModes.Overflow;
        marca.enableAutoSizing = true;
        marca.fontSizeMin = VocalComboFontMin;
        marca.fontSizeMax = VocalComboFontMax;

        VocalComboMarks[slot] = marca;
        RefreshVocalComboInteractable(slot, dd.interactable);
        RefreshVocalComboText(slot);
    }

    //Hijo TMP que se estira JUSTO al rect del padre (0 px de margen): la marca hereda la geometría
    //del rótulo, que ya está medida contra la flecha.
    TMP_Text MakeVocalFieldChild(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        var rt = child.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return child.AddComponent<TextMeshProUGUI>();
    }

    //VocalCharaIds es un campo público serializado; si alguien lo encoge en el Inspector, los bucles
    //de tamaño VocalMaxSlots lanzarían IndexOutOfRangeException.
    void EnsureVocalCharaIdsLength()
    {
        if (VocalCharaIds == null)
        {
            VocalCharaIds = new int[VocalMaxSlots];
            for (int k = 0; k < VocalMaxSlots; k++) VocalCharaIds[k] = -1;
            return;
        }
        if (VocalCharaIds.Length == VocalMaxSlots) return;
        var resized = new int[VocalMaxSlots];
        for (int k = 0; k < VocalMaxSlots; k++)
            resized[k] = (k < VocalCharaIds.Length) ? VocalCharaIds[k] : -1;
        VocalCharaIds = resized;
    }

    public void SetVocalMode(int val)
    {
        VocalMode = (LiveVocalMode)val;
        RefreshVocalDropdownVisibility();
    }

    public void SetVocalChara(int dropdownIndex, int optionIndex)
    {
        //Un índice fuera de rango aquí no es un caso normal: sólo puede venir de un cableado que
        //capture la variable de un `for` (ver BuildVocalSelectors). Ahora avisa.
        if (dropdownIndex < 0 || dropdownIndex >= VocalMaxSlots)
        {
            Debug.LogWarning($"[UmaViewerUI] SetVocalChara recibio dropdownIndex={dropdownIndex} " +
                             $"(limite 0..{VocalMaxSlots - 1}); la seleccion se descarta.");
            return;
        }
        EnsureVocalCharaIdsLength();
        //El id se lee de vocalOptionIds[dropdownIndex], la lista paralela a dd.options. Es lo único
        //que no cambia al filtrar (el texto de la opción ya no lleva el id, sólo el nombre).
        VocalCharaIds[dropdownIndex] = VocalIdFromOption(dropdownIndex, optionIndex);
        //Al elegir se limpia el filtro de ESE desplegable y se repinta su rótulo (es lo que hace una
        //combobox). Se reconstruye la lista sólo si había filtro.
        bool habiaFiltro = !string.IsNullOrEmpty(vocalFilters[dropdownIndex]);
        vocalFilters[dropdownIndex] = "";
        //Elegir una opción es un "hecho": se suelta la intención de tener la lista abierta.
        vocalListaQuiere[dropdownIndex] = false;
        //Y se deja de escuchar el teclado de este desplegable.
        if (vocalEscuchando == dropdownIndex) vocalEscuchando = -1;
        if (habiaFiltro) RebuildVocalDropdownOptions(dropdownIndex);
        else RefreshVocalComboText(dropdownIndex);
    }

    //id de la voz que hay en la opción 'optionIndex' del desplegable 'slot'. Se lee de
    //vocalOptionIds[slot] (el texto ya no lleva el id). Devuelve -1 (sin elegir) si el índice no
    //existe o si la opción es la cabecera "Random".
    int VocalIdFromOption(int slot, int optionIndex)
    {
        var ids = vocalOptionIds[slot];
        if (ids == null || optionIndex < 0 || optionIndex >= ids.Count) return -1;
        return ids[optionIndex];
    }

}
