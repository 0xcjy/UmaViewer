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
using System.Text.RegularExpressions;

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
    //Contenedor de los botones rápidos ("QuickPreset") del panel de selección del live.
    private GameObject SlotActionsContainer;

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

    //==========================================================================
    // (Selector Uniques/General/Extras/Other Characters del listado de trajes)
    // Los desplegables los coloca el usuario en la escena ("ModelDropdown" y "OtherCharactersDropdown").
    // Se resuelven por nombre en runtime y se repara su plantilla si falta. 'costumeListMode' decide
    // qué rama del listado pinta ListCostumes.
    //==========================================================================
    private TMP_Dropdown ModelDropdown;
    //Modo del listado de trajes del modelo normal. 'All' NO sale en el desplegable: es el
    //comportamiento de siempre para mobs/mini (propios + comunes, con y sin nombre).
    private enum CostumeListMode { Uniques = 0, General = 1, Extras = 2, OtherCharacters = 3, All = 4 }
    private CostumeListMode costumeListMode = CostumeListMode.Uniques;
    private CharaEntry currentCostumeChara;
    private bool currentCostumeMini = false;

    //(Modo "Other Characters"): desplegable con el resto de PJ y el elegido.
    private TMP_Dropdown OtherCharactersDropdown;
    private readonly List<CharaEntry> otherCharactersList = new List<CharaEntry>();
    private CharaEntry selectedOtherChara;

    //Búsqueda por teclado del desplegable "Other Characters": mismo comportamiento que la combobox
    //de voz. Sin InputField; el teclado se lee en Update() con Input.inputString (Input Manager
    //clásico). Al hacer clic dentro se abre la lista y se escucha; lo tecleado filtra por nombre y
    //la lista se mantiene abierta cerrando+reabriendo (ver RefreshOtherOpenList).
    private bool otherSearchActive = false;
    private string otherSearchFilter = "";
    private bool otherListQuiere = false;
    private bool otherListEspera = false;
    private bool otherListSucia = false;
    //PJ del último item clicado en la lista abierta. TMP_Dropdown dispara su onValueChanged al SOLTAR
    //el ratón (no al pulsar) y con un índice que puede haber quedado obsoleto si la lista se rehizo:
    //se guarda aquí el PJ real clicado (resuelto por el TEXTO del item) y el callback usa ESTE.
    private CharaEntry pendingOtherChara;
    //Último frame en que se recargó la lista de trajes desde el buscador (evita recargarla dos veces
    //en el mismo frame: TMP puede disparar el callback dos veces).
    private int lastOtherCostumeFrame = -1;
    //Opciones que se están viendo AHORA (filtradas o no); dd.value es un índice en ESTA lista.
    private readonly List<CharaEntry> otherOptionsVisible = new List<CharaEntry>();

    //==========================================================================
    // (Buscador de personajes de "Canvas/UI/Panels/Characters/SearchCharacter")
    //Se rellena con el listado que AHORA MISMO muestra el panel de personajes activo (Normal / Mini /
    //Mob); al elegir una entrada se dispara el MISMO click que su item de la lista, y mientras se
    //teclea las opciones se filtran por nombre.
    //==========================================================================
    private TMP_Dropdown SearchCharacterDropdown;
    private GameObject SearchCharacterRow;
    //Item de lista (Transform) y su nombre visible, en el MISMO orden que dd.options sin filtrar.
    private readonly List<Transform> searchCharacterItems = new List<Transform>();
    private readonly List<string> searchCharacterNames = new List<string>();
    //Índices de searchCharacterItems que se están viendo AHORA (filtrados); dd.value indexa ESTA.
    private readonly List<int> searchCharacterVisible = new List<int>();
    //Firma barata del contenido activo (panel + nº de items + primero/último) para saber cuándo
    //reconstruir sin recorrer la lista entera en cada frame.
    private string searchCharacterSignature = "";
    private string searchCharacterFilter = "";
    private bool searchCharacterActive = false;
    private bool searchCharacterListQuiere = false;
    private bool searchCharacterListEspera = false;
    private bool searchCharacterListSucia = false;
    //Selección: el Dropdown NO dispara onValueChanged si el índice elegido coincide con el actual, así
    //que también se detecta el CIERRE de la lista. Estos flags evitan el doble disparo.
    private bool searchCharacterClickOnList = false;
    private bool searchCharacterListWasOpen = false;
    private bool searchCharacterValueHandled = false;
    private bool searchCharacterNeedsRefresh = false;
    private int searchCharacterSelectedItemIndex = -1;
    //Los 3 paneles de tipo y el Content de su lista de personajes (se cachean al resolver el dropdown).
    private GameObject searchPanelNormal, searchPanelMini, searchPanelMob;
    private Transform searchContentNormal, searchContentMini, searchContentMob;

    //==========================================================================
    // (Buscador de shows de "Canvas/UI/Panels/Shows/SearchLive")
    //Se rellena con el TEXTO de cada item de "Shows/Live List/Viewport/Content" (su hijo "Text"),
    //filtra al teclear y al elegir un show dispara su click (abre el panel de selección del show).
    //==========================================================================
    private TMP_Dropdown SearchLiveDropdown;
    private GameObject SearchLiveRow;
    private readonly List<Transform> searchLiveItems = new List<Transform>();
    private readonly List<string> searchLiveNames = new List<string>();
    private readonly List<int> searchLiveVisible = new List<int>();
    private string searchLiveSignature = "";
    private string searchLiveFilter = "";
    private bool searchLiveActive = false;
    private bool searchLiveListQuiere = false;
    private bool searchLiveListEspera = false;
    private bool searchLiveListSucia = false;
    private bool searchLiveClickOnList = false;
    private bool searchLiveListWasOpen = false;
    private bool searchLiveValueHandled = false;
    private bool searchLiveNeedsRefresh = false;
    private int searchLiveSelectedItemIndex = -1;

    //==========================================================================
    // (Buscador de canciones/sonidos de "Canvas/UI/Panels/Sounds/SearchSounds")
    //Se adapta a la pestaña activa: en la pestaña Live busca las CANCIONES ("Sounds/LiveSound
    //List/Viewport/Content") y en la Normal los sonidos ("Sounds/NormalSound List/NormalSound/
    //Viewport/Content"). El nombre de cada item se lee de su hijo "Text".
    //==========================================================================
    private TMP_Dropdown SearchSoundsDropdown;
    private GameObject SearchSoundsRow;
    private readonly List<Transform> searchSoundsItems = new List<Transform>();
    private readonly List<string> searchSoundsNames = new List<string>();
    private readonly List<int> searchSoundsVisible = new List<int>();
    private string searchSoundsSignature = "";
    private string searchSoundsFilter = "";
    private bool searchSoundsActive = false;
    private bool searchSoundsListQuiere = false;
    private bool searchSoundsListEspera = false;
    private bool searchSoundsListSucia = false;
    private bool searchSoundsClickOnList = false;
    private bool searchSoundsListWasOpen = false;
    private bool searchSoundsValueHandled = false;
    private bool searchSoundsNeedsRefresh = false;
    private int searchSoundsSelectedItemIndex = -1;
    //Última canción elegida en la lista (sin usar el buscador). Sirve para que el desplegable
    //"SearchSounds" se sincronice con la selección real de la lista al abrir el panel.
    private LiveEntry selectedLiveSound;

    //==========================================================================
    // (Segundo buscador del panel Sounds: "Canvas/UI/Panels/Sounds/SearchSoundsbyCharacter")
    //Carga el listado de PJ de "Characters/NormalCharacters/Character List" y arranca SIN selección.
    //Al elegir un PJ deja en "Sounds/LiveSound List" sólo las canciones en cuyas voces
    //("Sounds/LiveCharaSound List") aparezca ese PJ, y con ello "SearchSounds" se recarga con el
    //listado filtrado. También admite filtrar al teclear la lista de PJ.
    //==========================================================================
    private TMP_Dropdown SearchByCharaDropdown;
    private GameObject SearchByCharaRow;
    private readonly List<Transform> searchByCharaItems = new List<Transform>();
    private readonly List<string> searchByCharaNames = new List<string>();
    private readonly List<int> searchByCharaIds = new List<int>();
    private readonly List<int> searchByCharaVisible = new List<int>();
    private string searchByCharaSignature = "";
    private string searchByCharaFilter = "";
    private bool searchByCharaActive = false;
    private bool searchByCharaListQuiere = false;
    private bool searchByCharaListEspera = false;
    private bool searchByCharaListSucia = false;
    private bool searchByCharaClickOnList = false;
    private bool searchByCharaListWasOpen = false;
    private bool searchByCharaValueHandled = false;
    private bool searchByCharaNeedsRefresh = false;
    private int searchByCharaSelectedItemIndex = -1;
    //-1 = sin PJ elegido (no se filtra la lista de canciones).
    private int searchByCharaSelectedCharaId = -1;

    //Fuente del juego que deben usar los desplegables (rótulo + lista). Se resuelve una sola vez.
    private static TMP_FontAsset _dropdownFont;

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
        //(Fuentes: cadena de respaldo + reparación de atlas con huecos nulos. Ver SetupFontFallbacks.)
        SetupFontFallbacks();

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

        //(Misma búsqueda por teclado para el desplegable "Other Characters".)
        UpdateOtherCharacterSearch();

        //(Buscador de personajes: refresca su lista según el panel activo y le da la búsqueda.)
        UpdateSearchCharacter();
        UpdateSearchCharacterSearch();

        //(Buscador de shows: misma mecánica sobre la lista de shows.)
        UpdateSearchLive();
        UpdateSearchLiveSearch();

        //(Buscador de canciones/sonidos del panel Sounds.)
        UpdateSearchSounds();
        UpdateSearchSoundsSearch();

        //(Segundo buscador del panel Sounds: filtra canciones por PJ cantante.)
        UpdateSearchByChara();
        UpdateSearchByCharaSearch();
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
        //(Prepara los desplegables del panel de personajes: modo de listado de trajes y buscador.)
        EnsureModelDropdown();
        EnsureOtherCharactersDropdown();
        EnsureSearchCharacterDropdown();

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

        //La lista de mobs tenía su propio PageManager (paginación + buscador). El buscador
        //"SearchCharacter" ya cubre la búsqueda, así que si el PageManager de mobs NO existe en la
        //escena se listan TODOS los mobs directamente en el Content (igual que los personajes
        //normales) y el buscador filtra la lista completa. Si siguiera existiendo, se usa la
        //paginación de siempre.
        var pageentrys = new List<PageManager.Entry>();
        foreach (var chara in Main.MobCharacters.OrderBy(c => c.Id))
        {
            var charaInstance = chara;

            if (MobCharactersPageCtrl == null && MobCharactersList != null && MobCharactersList.content != null)
            {
                var mobContainer = Instantiate(UmaContainerPrefab, MobCharactersList.content).GetComponent<UmaUIContainer>();
                UIPressScaleFeedback.AddTo(mobContainer.gameObject);
                mobContainer.UseLocalizedText();
                mobContainer.Name = mobContainer.name = chara.GetName();
                mobContainer.Button.onClick.AddListener(() =>
                {
                    HighlightChildImage(MobCharactersList.content, mobContainer);
                    ListCostumes(charaInstance, false);
                });
                if (chara.Icon)
                {
                    mobContainer.Image.sprite = chara.Icon;
                    mobContainer.Image.enabled = true;
                }
                continue;
            }

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
        if (MobCharactersPageCtrl != null && MobCharactersList != null)
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
        //(Prepara los buscadores de los paneles Shows y Sounds; el buscador por PJ necesita la lista de
        //personajes, que ya se construyó en LoadModelPanels.)
        EnsureSearchLiveDropdown();
        EnsureSearchSoundsDropdown();
        EnsureSearchByCharaDropdown();

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
            createContainer(live, UmaContainerLivePrefab, LiveList, () => { selectedLiveSound = live; ShowLiveSelectPanel(live); });
            createContainer(live, UmaContainerLivePrefab, LiveSoundList, () => { selectedLiveSound = live; ListLiveSounds(live.MusicId); });
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

        //(Live: botones rápidos del panel: copiar slot 1 / aleatorizar trajes. Viven fuera del content
        //del scroll, así que sobreviven al borrado de slots.)
        BuildSlotActions();
    }

    void ListCostumes(CharaEntry chara, bool mini)
    {
        ScrollRect costumeList = mini ? MiniCostumeList : chara.IsMob ? MobCostumeList : CostumeList;
        for (int i = costumeList.content.childCount - 1; i >= 0; i--)
        {
            Destroy(costumeList.content.GetChild(i).gameObject);
        }

        //(Recordamos sobre qué personaje se está pintando para poder repintar cuando cambie el
        //selector Uniques/General/Extras/Other Characters — OnModelDropdownChanged.)
        currentCostumeChara = chara;
        currentCostumeMini = mini;

        //(El selector Uniques/General/Extras/Other Characters sólo aplica al listado normal; mobs y
        //mini conservan el comportamiento de siempre: todos los trajes, 'All'.)
        CostumeListMode mode = (!mini && !chara.IsMob) ? costumeListMode : CostumeListMode.All;

        //(En "Other Characters" se listan los Uniques del PJ elegido en el desplegable, pero al cargar
        //se conserva la cabeza/pelo/cola del PJ ACTUAL (chara), no la del otro.)
        bool otherMode = mode == CostumeListMode.OtherCharacters && selectedOtherChara != null;
        CharaEntry listChara = otherMode ? selectedOtherChara : chara;
        CharaEntry headChara = otherMode ? chara : listChara;

        //--- Trajes PROPIOS del personaje (id corto). Los mobs no los tienen. (En 'Uniques',
        //'Other Characters' y 'All'; en General/Extras no salen.)
        bool includeOwn = !listChara.IsMob &&
            (mode == CostumeListMode.Uniques || mode == CostumeListMode.OtherCharacters || mode == CostumeListMode.All);
        if (includeOwn)
        {
            string ownNameVar = mini ? $"pfb_mbdy{listChara.Id}" : $"pfb_bdy{listChara.Id}";
            string ownBodyPath = mini ? UmaDatabaseController.MiniBodyPath : UmaDatabaseController.BodyPath;
            foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith(ownBodyPath) && !a.Name.Contains("clothes") && a.Name.Contains(ownNameVar)))
            {
                var container = Instantiate(UmaContainerCostumePrefab, costumeList.content).GetComponent<UmaUIContainer>();
                UIPressScaleFeedback.AddTo(container.gameObject);
                string[] split = entry.Name.Split('_');
                string costumeId = split[split.Length - 1];
                var dressdata = Main.Costumes.FirstOrDefault(a => (a.CharaId == listChara.Id && a.BodyTypeSub == int.Parse(costumeId)));
                container.Name = container.name = GetCostumeName(costumeId, (dressdata == null ? costumeId : dressdata.DressName));
                container.Image.sprite = (dressdata == null ? CostumeIconDefault : dressdata.Icon);
                container.Image.enabled = true;
                container.Id = costumeId;
                container.Button.onClick.AddListener(() =>
                {
                    if (LiveSelectPannel.activeInHierarchy && CurrentSeletChara)
                    {
                        CurrentSeletChara.SetValue(listChara, costumeId, container.Image.sprite, CurrentHeadCostumeId);
                    }
                    else
                    {
                        HighlightChildImage(costumeList.content, container);
                        var list = new List<UmaDatabaseEntry>();
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.BodyPath) && a.Name.Contains(listChara.Id.ToString())));
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.HeadPath) && a.Name.Contains(headChara.Id.ToString())));
                        var tail_path = getCharaTailPath(headChara);
                        if (!string.IsNullOrEmpty(tail_path))
                        {
                            list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(tail_path)));
                        }
                        list.Add(Main.AbList["3d/animator/drivenkeylocator"]);
                        list.Add(Main.AbList[$"3d/motion/event/body/chara/chr{listChara.Id}_00/anm_eve_chr{listChara.Id}_00_idle01_loop"]);

                        Builder.UnloadUma();
                        //UmaAssetManager.UnloadAllBundle(true);
                        UmaAssetManager.PreLoadAndRun(list , delegate {
                            StartCoroutine(Builder.LoadUma(listChara, costumeId, mini, CurrentHeadCostumeId, otherMode ? chara : null));
                        });
                        //StartCoroutine(Builder.LoadUma(listChara, costumeId, mini));
                    }
                });
            }
        }

        //Common costumes
        List<string> costumes = new List<string>();
        var commonVar = mini ? "pfb_mbdy0" : $"pfb_bdy0";
        string commonPath = mini ? UmaDatabaseController.MiniBodyPath : UmaDatabaseController.BodyPath;
        foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith(commonPath) && !a.Name.Contains("/clothes/") && a.Name.Contains(commonVar)))
        {
            string id = Path.GetFileName(entry.Name);
            string[] split = id.Split('_');
            if (split.Length < 4) continue; //prevents error for mini umas
            id = split[1].Substring(mini ? 4 : 3) + "_" + split[2] + "_" + split[3];
            if (costumes.Contains(id)) continue;
            costumes.Add(id);

            string costumeId = id;
            var data = costumeId.Split('_');
            var bodytype = int.Parse(data[0]);
            var bodysub = int.Parse(data[1]);

            var dressdata = Main.Costumes.FirstOrDefault(a => a.BodyType == bodytype && a.BodyTypeSub == bodysub);

            //(Tres listas: 'Uniques'/'Other Characters' no llevan comunes; 'General' los comunes CON
            //nombre; 'Extras' los comunes de testeo (id en crudo) más los que marca EsTrajeExtra
            //(variantes Big Belly). 'All' (mobs/mini) los lleva todos.)
            bool isExtra = EsTrajeExtra(id, dressdata);
            bool wanted = isExtra
                ? (mode == CostumeListMode.Extras || mode == CostumeListMode.All)
                : (mode == CostumeListMode.General || mode == CostumeListMode.All);
            if (!wanted) continue;

            var container = Instantiate(UmaContainerCostumePrefab, costumeList.content).GetComponent<UmaUIContainer>();
            UIPressScaleFeedback.AddTo(container.gameObject);
            container.Name = container.name = GetCostumeName(id, (dressdata == null ? id : dressdata.DressName));
            container.Image.sprite = (dressdata == null ? CostumeIconDefault : dressdata.Icon);
            container.Image.enabled = true;
            container.Id = costumeId;
            container.Button.onClick.AddListener(() =>
            {
                if (LiveSelectPannel.activeInHierarchy && CurrentSeletChara)
                {
                    CurrentSeletChara.SetValue(listChara, costumeId, container.Image.sprite, CurrentHeadCostumeId);
                }
                else
                {
                    HighlightChildImage(costumeList.content, container);
                    var list = new List<UmaDatabaseEntry>();
                    list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.BodyPath) && a.Name.Contains(listChara.Id.ToString())));
                    list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.HeadPath) && a.Name.Contains(headChara.Id.ToString())));
                    var tail_path = getCharaTailPath(headChara);
                    if (!string.IsNullOrEmpty(tail_path))
                    {
                        list.AddRange(Main.AbChara.Where(a => a.Name.StartsWith(tail_path)));
                    }
                    list.Add(Main.AbList["3d/animator/drivenkeylocator"]);
                    var motion_path = $"3d/motion/event/body/chara/chr{listChara.Id}_00/anm_eve_chr{listChara.Id}_00_idle01_loop";
                    if (!listChara.IsMob && !ModelSettings.IsHeadFix && Main.AbList.TryGetValue(motion_path, out var motion_entry))
                    {
                        list.Add(motion_entry);
                    }

                    Builder.UnloadUma();
                    //UmaAssetManager.UnloadAllBundle(true);
                    UmaAssetManager.PreLoadAndRun(list, delegate { StartCoroutine(Builder.LoadUma(listChara, costumeId, mini, CurrentHeadCostumeId, otherMode ? chara : null)); });
                    //StartCoroutine(Builder.LoadUma(listChara, costumeId, mini));
                }
            });
        }

        if(!chara.IsMob && !mini)
        {
            for (int i = HeadCostumeList.content.childCount - 1; i >= 0; i--)
            {
                Destroy(HeadCostumeList.content.GetChild(i).gameObject);
            }
            CurrentHeadCostumeId = string.Empty;
            var headNameVar = $"/pfb_chr{chara.Id}_";
            foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith($"{UmaDatabaseController.HeadPath}chr{chara.Id}_") && !a.Name.Contains("clothes") && a.Name.Contains(headNameVar)))
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
            PrepareDropdownScroll(VocalModeDropdown);
            PrepareDropdownFont(VocalModeDropdown);
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
        PrepareDropdownScroll(dd);
        PrepareDropdownFont(dd);

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



    //==========================================================================
    // (Live: botones rápidos del panel de selección — "copiar slot 1" y "aleatorizar trajes")
    // El contenedor "QuickPreset" y sus botones viven en la escena (posición, tamaño y rótulo); aquí
    // sólo se localizan por nombre y se les cablea el onClick. Como cuelgan directamente del panel y
    // no del content del scroll, sobreviven al borrado de slots.
    //==========================================================================

    void BuildSlotActions()
    {
        if (LiveSelectPannel == null) return;

        if (SlotActionsContainer == null)
            SlotActionsContainer = FindChildByName(LiveSelectPannel.transform, "QuickPreset");
        if (SlotActionsContainer == null)
        {
            Debug.LogWarning("[UmaViewerUI] No se encuentra 'QuickPreset' en el panel de selección: no habrá " +
                             "botones de copiar slot 1 / aleatorizar trajes.");
            return;
        }

        WireSlotAction("Copy", CopySlot1ToRest);
        WireSlotAction("Random", RandomizeCostumes);
    }

    //Cablea el onClick del botón con ese nombre dentro de QuickPreset. Reemplaza el evento entero para
    //no duplicar listeners si el panel se abre más de una vez. No toca el diseño: sólo el evento.
    void WireSlotAction(string buttonName, UnityAction onClick)
    {
        if (SlotActionsContainer == null) return;

        var go = FindChildByName(SlotActionsContainer.transform, buttonName);
        if (go == null)
        {
            Debug.LogWarning($"[UmaViewerUI] No se encuentra el botón '{buttonName}' dentro de QuickPreset.");
            return;
        }

        var button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Debug.LogWarning($"[UmaViewerUI] El botón '{buttonName}' no tiene componente Button.");
            return;
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
    }

    //Botón "copiar slot 1": pone el MISMO modelo y el MISMO traje de la posición 1 en todas las demás.
    //No precarga nada: SetValue sólo escribe datos y el modelo 3D se monta después al iniciar el live.
    void CopySlot1ToRest()
    {
        var slots = LiveSelectList.content.GetComponentsInChildren<LiveCharacterSelect>();
        if (slots.Length < 2) return;

        var src = EnsureSlot1Filled(slots[0]);
        if (src == null) return;   //EnsureSlot1Filled ya ha avisado por consola

        //Se empieza en i=1 para no pisar la posición 1, que es la fuente.
        for (int i = 1; i < slots.Length; i++)
        {
            slots[i].SetValue(src.CharaEntry, src.CostumeId,
                              src.CostumeImage.sprite, src.HeadCostumeId);
        }
        //Sin mensaje de éxito a propósito: ShowMessage activa el panel de mensajes y se abriría encima
        //del propio panel de selección. Sólo se avisa de los fallos.
    }

    //Si la posición 1 está vacía, se autorrellena con un personaje y el uniforme por defecto, igual
    //que el fallback que usa el builder al iniciar el live, para que el resultado no dependa del
    //momento del clic. Devuelve el slot ya rellenado, o null si no hay datos de personajes.
    LiveCharacterSelect EnsureSlot1Filled(LiveCharacterSelect src)
    {
        if (src == null) return null;
        if (src.CharaEntry != null && !string.IsNullOrEmpty(src.CostumeId)) return src;

        if (Main.Characters == null || Main.Characters.Count == 0)
        {
            ShowMessage("No character data available.", UIMessageType.Warning);
            return null;
        }

        //OJO: 'Random' a secas sería ambiguo (el archivo tiene 'using System;' y 'using UnityEngine;'),
        //así que se fully-qualifica.
        src.CharaEntry = Main.Characters[UnityEngine.Random.Range(0, Main.Characters.Count / 2)];
        src.CostumeId = "0002_00_00";
        src.SetValue(src.CharaEntry, src.CostumeId, CostumeIconDefault, src.HeadCostumeId);
        return src;
    }

    //Botón "aleatorizar trajes": reparte trajes con nombre (propios 'Unique' y comunes 'General') del
    //MISMO modelo de la posición 1 en las posiciones 2..N. Queda fuera lo de testeo y las variantes
    //"Big Belly" (EsTrajeExtra). El modelo (CharaEntry) y el HeadCostumeId son SIEMPRE los de la
    //posición 1: sólo cambia el CostumeId. Con repetición permitida, para no dejar huecos con pocos
    //trajes y muchos slots. No precarga nada: el modelo 3D se monta después al iniciar el live.
    void RandomizeCostumes()
    {
        var slots = LiveSelectList.content.GetComponentsInChildren<LiveCharacterSelect>();
        if (slots.Length < 2) return;

        var src = EnsureSlot1Filled(slots[0]);
        if (src == null) return;   //EnsureSlot1Filled ya ha avisado por consola

        var pool = new List<CostumeOption>();
        foreach (var c in EnumerateCostumes(src.CharaEntry, false))
        {
            if (!EsTrajeExtra(c.Id, c.Dress)) pool.Add(c);
        }

        if (pool.Count == 0)   //personaje sin ningún traje con nombre visible
        {
            ShowMessage("No valid costumes found for this character.", UIMessageType.Warning);
            return;
        }

        for (int i = 1; i < slots.Length; i++)
        {
            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            slots[i].SetValue(src.CharaEntry, pick.Id, pick.Icon, src.HeadCostumeId);
        }
        //Sin mensaje de éxito a propósito (ver CopySlot1ToRest).
    }

    //(Live: dato de un traje, sin UI. La lista la construye EnumerateCostumes y la consume el botón
    //"aleatorizar trajes".)
    struct CostumeOption
    {
        public string Id;           //corto "00"/"01"/"90" (propio) o "0007_05_00" (común)
        public Sprite Icon;         //dressdata.Icon, o CostumeIconDefault si no hay dressdata
        public CostumeEntry Dress;  //puede ser null
        public string DisplayName;  //lo que se pinta en el panel (GetCostumeName)
        public bool IsCommon;       //true = id "XXXX_XX_XX"; false = propio (id corto)
    }

    //(Trajes de un personaje sin crear UI: primero los PROPIOS (id corto) y después los COMUNES
    //(XXXX_XX_XX), en el mismo orden que el listado del panel, para que el aleatorio y el selector de
    //modo elijan entre los mismos trajes que se ven. 'mode' decide qué ramas entran: por defecto 'All'
    //(propios + comunes con y sin nombre), que es el comportamiento de siempre para mobs/mini.)
    List<CostumeOption> EnumerateCostumes(CharaEntry chara, bool mini, CostumeListMode mode = CostumeListMode.All)
    {
        var result = new List<CostumeOption>();
        if (chara == null) return result;
        if (Main == null || Main.AbChara == null || Main.Costumes == null) return result;

        string bodyPath = mini ? UmaDatabaseController.MiniBodyPath : UmaDatabaseController.BodyPath;

        //--- Trajes PROPIOS del personaje (id corto). Los mobs no los tienen. (En 'Uniques',
        //'Other Characters' y 'All'; en General/Extras no salen.)
        if (!chara.IsMob && (mode == CostumeListMode.Uniques || mode == CostumeListMode.OtherCharacters || mode == CostumeListMode.All))
        {
            string nameVar = mini ? $"pfb_mbdy{chara.Id}" : $"pfb_bdy{chara.Id}";
            foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith(bodyPath) && !a.Name.Contains("clothes") && a.Name.Contains(nameVar)))
            {
                string[] split = entry.Name.Split('_');
                string costumeId = split[split.Length - 1];
                int sub;
                if (!int.TryParse(costumeId, out sub)) sub = int.MinValue;   //nunca casa con un BodyTypeSub real
                var dressdata = Main.Costumes.FirstOrDefault(a => a.CharaId == chara.Id && a.BodyTypeSub == sub);
                result.Add(MakeCostumeOption(costumeId, dressdata, false));
            }
        }

        //--- Trajes COMUNES (id "XXXX_XX_XX"), deduplicados por id.
        var seen = new List<string>();
        string commonVar = mini ? "pfb_mbdy0" : "pfb_bdy0";
        foreach (var entry in Main.AbChara.Where(a => a.Name.StartsWith(bodyPath) && !a.Name.Contains("/clothes/") && a.Name.Contains(commonVar)))
        {
            string id = Path.GetFileName(entry.Name);
            string[] split = id.Split('_');
            if (split.Length < 4) continue;   //previene error con las mini umas
            id = split[1].Substring(mini ? 4 : 3) + "_" + split[2] + "_" + split[3];
            if (seen.Contains(id)) continue;
            seen.Add(id);

            string[] data = id.Split('_');
            int bodytype, bodysub;
            if (!int.TryParse(data[0], out bodytype) || !int.TryParse(data[1], out bodysub))
            {
                bodytype = int.MinValue;
                bodysub = int.MinValue;
            }
            var dressdata = Main.Costumes.FirstOrDefault(a => a.BodyType == bodytype && a.BodyTypeSub == bodysub);

            //(Tres listas: 'Uniques'/'Other Characters' no llevan comunes; 'General' los comunes CON
            //nombre; 'Extras' los de testeo (id en crudo) más los que marca EsTrajeExtra (Big Belly).
            //'All' (mobs/mini) los lleva todos.)
            bool isExtra = EsTrajeExtra(id, dressdata);
            bool wanted = isExtra
                ? (mode == CostumeListMode.Extras || mode == CostumeListMode.All)
                : (mode == CostumeListMode.General || mode == CostumeListMode.All);
            if (!wanted) continue;

            result.Add(MakeCostumeOption(id, dressdata, true));
        }

        return result;
    }

    CostumeOption MakeCostumeOption(string id, CostumeEntry dressdata, bool isCommon)
    {
        return new CostumeOption
        {
            Id = id,
            Dress = dressdata,
            Icon = dressdata == null ? CostumeIconDefault : dressdata.Icon,
            DisplayName = GetCostumeName(id, dressdata == null ? id : dressdata.DressName),
            IsCommon = isCommon
        };
    }

    //Forma de los IDs de los trajes COMUNES: XXXX_XX_XX. Los PROPIOS usan IDs cortos ("00", "01",
    //"90") y jamás deben filtrarse.
    static readonly Regex TrajeComun = new Regex(@"^\d{4}_\d{2}_\d{2}$", RegexOptions.Compiled);

    //true = es un traje de testeo: tiene forma de ID común Y además se muestra con el ID en crudo (no
    //hay fila en dress_data ni nombre fijo en GetCostumeName), que es justo lo que lo hace inservible.
    //Hacen falta LAS DOS condiciones: con la forma sola se descartarían los que sí funcionan.
    public static bool EsTrajeDeTest(string costumeId, CostumeEntry dress)
    {
        if (string.IsNullOrEmpty(costumeId)) return false;
        string shown = (dress == null || string.IsNullOrEmpty(dress.DressName)) ? costumeId : dress.DressName;
        return TrajeComun.IsMatch(costumeId) && GetCostumeName(costumeId, shown) == costumeId;
    }

    //Trajes que, aunque tengan nombre, no deben entrar en el aleatorio: hoy, las variantes "Big Belly"
    //del uniforme escolar. El botón "aleatorizar trajes" los excluye junto con los de testeo.
    static bool EsTrajeExtra(string costumeId, CostumeEntry dress)
    {
        return EsTrajeDeTest(costumeId, dress)
            || costumeId == "0002_00_03"   // School Short Sleeves Big Belly
            || costumeId == "0002_01_03";  // School Long Sleeves Big Belly
    }



    //==========================================================================
    // (Buscadores / desplegables con búsqueda por teclado y selector de modo de trajes)
    // El diseño (posición, tamaño, rótulos) vive en Version2.unity; aquí SÓLO se resuelve por nombre
    // y se cablea el comportamiento. Nada se redimensiona ni se recoloca. Ningún InputField: el
    // teclado se lee a mano en Update() con Input.inputString (Input Manager clásico).
    //==========================================================================

    //Cámara con la que resolver los rect de un desplegable (Overlay → null).
    Camera DropdownCanvasCamara(TMP_Dropdown dd)
    {
        var canvas = dd != null ? dd.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        return canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    //Busca un descendiente por nombre ACOTADO a root. A diferencia de FindChildByName (que cae a
    //buscar en toda la escena y devolvería un "Content" ajeno), esto nunca sale del subárbol.
    static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c == null) continue;
            if (c.name == name) return c;
            var deep = FindDescendant(c, name);
            if (deep != null) return deep;
        }
        return null;
    }

    static Transform FindListContent(Transform panel, string listName)
    {
        if (panel == null) return null;
        var list = FindDescendant(panel, listName);
        return list != null ? FindDescendant(list, "Content") : null;
    }

    //Un Dropdown necesita SIEMPRE plantilla (Template) + itemText para poder desplegar. Si vienen
    //vacíos, se clona la plantilla de otro Dropdown de la escena (se prefiere "LiveMode") y se
    //engancha al nuestro. Así no hay que tocar el YAML ni el Inspector.
    void RepairDropdownTemplate(TMP_Dropdown dd)
    {
        if (dd == null) return;

        if (dd.template == null || dd.itemText == null)
        {
            TMP_Dropdown src = null;
            foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
            {
                if (d == null || d == dd || d.template == null) continue;
                if (d.gameObject.name == "LiveMode") { src = d; break; }
                if (src == null) src = d;
            }
            if (src == null)
            {
                Debug.LogError("[UmaViewerUI] Un Dropdown no tiene plantilla y no hay ningún otro con plantilla de donde copiarla.");
                return;
            }

            if (dd.template == null)
            {
                var tpl = Instantiate(src.template.gameObject, dd.transform);
                tpl.name = "Template";
                var tplRt = tpl.GetComponent<RectTransform>();
                tplRt.SetAsLastSibling();
                tpl.SetActive(false);          //la plantilla debe estar oculta hasta desplegar
                dd.template = tplRt;
            }

            if (dd.itemText == null && dd.template != null)
            {
                var item = dd.template.Find("Viewport/Content/Item");
                if (item != null) dd.itemText = item.GetComponentInChildren<TMP_Text>(true);
            }
        }

        //Los items del desplegable, a 15 fijo (igual que las combobox de voz).
        if (dd.itemText != null)
        {
            dd.itemText.enableAutoSizing = false;
            dd.itemText.fontSize = VocalComboFontMax;
        }

        PrepareDropdownMarquee(dd);
        PrepareDropdownScroll(dd);
        PrepareDropdownFont(dd);
    }

    //La rueda del ratón mueve muy poco las listas de los desplegables: se multiplica por 10 la
    //sensibilidad de scroll. Se aplica a la plantilla, así lo heredan las listas que Show() clona.
    void PrepareDropdownScroll(TMP_Dropdown dd)
    {
        if (dd == null || dd.template == null) return;
        var scroll = dd.template.GetComponentInChildren<ScrollRect>(true);
        if (scroll == null) return;
        scroll.scrollSensitivity *= 10f;
    }

    //El rótulo y la lista de los desplegables deben usar la fuente del juego ("dynamic01 SDF"). Al
    //entrar en Play, algunos templates pierden esa fuente (vuelven a LiberationSans) y se ve distinta.
    //Aquí se fuerza en runtime, tanto en el rótulo (captionText) como en las etiquetas de la lista
    //(itemText; las copias que clona Show() la heredan de la plantilla).
    //El rótulo y la lista de los desplegables deben usar la fuente del juego ("dynamic01 SDF"). Al
    //entrar en Play, algunos templates pierden esa fuente (vuelven a LiberationSans) y se ve distinta.
    //Aquí se fuerza en runtime, tanto en el rótulo (captionText) como en las etiquetas de la lista
    //(itemText; las copias que clona Show() la heredan de la plantilla).
    void PrepareDropdownFont(TMP_Dropdown dd)
    {
        if (dd == null) return;
        var font = DropdownFontAsset();
        if (font == null) return;
        ApplyDropdownFont(dd.captionText, font);
        ApplyDropdownFont(dd.itemText, font);
    }

    static TMP_FontAsset DropdownFontAsset()
    {
        if (_dropdownFont != null) return _dropdownFont;
        _dropdownFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/dynamic01 SDF");
        if (_dropdownFont == null) _dropdownFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/dynamic01 SDF 1");
        if (_dropdownFont == null)
            Debug.LogWarning("[UmaViewerUI] No se encuentra la fuente 'dynamic01 SDF' en Resources: " +
                             "los desplegables se quedan con la que traiga la escena.");
        return _dropdownFont;
    }

    static void ApplyDropdownFont(TMP_Text label, TMP_FontAsset font)
    {
        if (label == null || font == null) return;
        if (label.font != font) label.font = font;
    }

    //==========================================================================
    // (Fuentes: cadena de respaldo y descarte de fuentes rotas)
    // Algunos textos traen caracteres (kanji/kana o signos especiales, p. ej. "ū") que la fuente
    // principal no incluye; esos van a la cadena de respaldo. Y alguna fuente de la escena (p. ej.
    // la copia "华文仿宋 SDF") tiene los glifos apuntando a atlas que ya no existen: al hornearla o
    // representarla, TMP lanza UnassignedReferenceException / IndexOutOfRangeException.
    //  · Se DESCARTAN de todas las cadenas las fuentes rotas (nunca se llega a ellas).
    //  · Se engancha la cadena pedida: principal -> "dynamic01 SDF 1" -> genérica de Unity.
    //==========================================================================
    static readonly System.Reflection.BindingFlags FontFlags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    void SetupFontFallbacks()
    {
        var primary = Resources.Load<TMP_FontAsset>("Fonts & Materials/dynamic01 SDF");
        var game = Resources.Load<TMP_FontAsset>("Fonts & Materials/dynamic01 SDF 1");
        var generic = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        //Quita de todas las cadenas las fuentes rotas (incluidas las de los propios respaldos).
        SanitizeFallbacks(primary);
        SanitizeFallbacks(game);
        SanitizeFallbacks(generic);

        if (primary == null) return;

        //La principal es dinámica: que pueda crecer de atlas si se queda corta.
        primary.isMultiAtlasTexturesEnabled = true;

        //Cadena de respaldo pedida: principal -> "dynamic01 SDF 1" (tiene más glifos JP) -> genérica
        //de Unity. Nunca se incluye una fuente rota.
        var chain = new List<TMP_FontAsset>();
        if (game != null && game != primary && !IsFontBroken(game)) chain.Add(game);
        if (generic != null && generic != primary && !IsFontBroken(generic)) chain.Add(generic);
        primary.fallbackFontAssetTable = chain;

        if (game != null)
        {
            game.isMultiAtlasTexturesEnabled = true;
            var gameChain = new List<TMP_FontAsset>();
            if (generic != null && generic != game && !IsFontBroken(generic)) gameChain.Add(generic);
            game.fallbackFontAssetTable = gameChain;
        }
    }

    //Deja en la cadena de respaldo sólo fuentes utilizables (quita las rotas).
    static void SanitizeFallbacks(TMP_FontAsset font)
    {
        if (font == null || font.fallbackFontAssetTable == null) return;
        var kept = new List<TMP_FontAsset>();
        foreach (var fb in font.fallbackFontAssetTable)
        {
            if (fb == null) continue;
            //Fuente inservible (glifos apuntando a atlas inexistentes): no se añade a la cadena. Se
            //hace en silencio a propósito: en esta escena hay una copia corrupta y avisar cada vez
            //sólo llenaba la consola.
            if (IsFontBroken(fb)) continue;
            kept.Add(fb);
        }
        font.fallbackFontAssetTable = kept;
    }

    //true = la fuente tiene glifos que apuntan a un atlas inexistente (o el atlas activo es un
    //hueco nulo): hornear/representar con ella lanzaría excepción.
    static bool IsFontBroken(TMP_FontAsset font)
    {
        if (font == null) return false;
        var tex = font.atlasTextures;
        if (tex == null || tex.Length == 0) return true;

        var idxField = typeof(TMP_FontAsset).GetField("m_AtlasTextureIndex", FontFlags);
        if (idxField != null)
        {
            int idx = (int)idxField.GetValue(font);
            if (idx < 0 || idx >= tex.Length || tex[idx] == null) return true;
        }

        var glyphField = typeof(TMP_FontAsset).GetField("m_GlyphTable", FontFlags);
        var table = glyphField != null ? glyphField.GetValue(font) as System.Collections.IList : null;
        if (table != null && table.Count > 0)
        {
            var prop = table[0].GetType().GetProperty("atlasIndex");
            if (prop != null)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    int ai = (int)prop.GetValue(table[i]);
                    if (ai < 0 || ai >= tex.Length || tex[ai] == null) return true;
                }
            }
        }
        return false;
    }

    //(Tarea 2) Un texto demasiado largo para una línea se parte y se solapa con la fila siguiente.
    //En vez de eso, la etiqueta se deja en UNA línea con una marquesina (TMPMarquee) y la fila recorta
    //(RectMask2D). Se aplica a la etiqueta de los items del desplegable; y al rótulo (captionText)
    //sólo si cuelga de un contenedor propio (para poder recortarlo sin comerse la lista).
    void PrepareDropdownMarquee(TMP_Dropdown dd)
    {
        if (dd == null) return;

        SetupMarqueeLabel(dd.itemText, true);

        var caption = dd.captionText;
        if (caption != null && caption.transform.parent != null && caption.transform.parent != dd.transform)
            SetupMarqueeLabel(caption, true);
    }

    void SetupMarqueeLabel(TMP_Text label, bool maskParent)
    {
        if (label == null) return;

        var mq = label.GetComponent<TMPMarquee>();
        if (mq == null) mq = label.gameObject.AddComponent<TMPMarquee>();
        mq.Target = label;

        //Una sola línea, sin partirse; el recorte lo hace el RectMask2D.
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enableAutoSizing = false;

        if (maskParent)
        {
            var row = label.transform.parent;
            if (row != null && row.GetComponent<RectMask2D>() == null)
                row.gameObject.AddComponent<RectMask2D>();
        }
    }

    //Nombre visible de un item de lista (show/sonido): el texto de su hijo "Text" (legacy o TMP); si
    //no, el Name del UmaUIContainer; si no, el nombre del GameObject.
    static string SearchComboItemText(Transform item)
    {
        if (item == null) return "";
        var textGo = FindDescendant(item, "Text");
        if (textGo != null)
        {
            var legacy = textGo.GetComponent<Text>();
            if (legacy != null && !string.IsNullOrEmpty(legacy.text)) return legacy.text;
            var tmp = textGo.GetComponent<TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text;
        }
        var container = item.GetComponent<UmaUIContainer>();
        if (container != null && !string.IsNullOrEmpty(container.Name)) return container.Name;
        return item.name;
    }

    //Desplaza la lista hasta el item elegido. Se calcula de forma agnóstica al layout: se compara el
    //rect del item con el del viewport del ScrollRect y se mueve el content lo justo para dejarlo
    //dentro.
    void ScrollListToItem(Transform item)
    {
        if (item == null) return;
        var itemRect = item as RectTransform;
        if (itemRect == null) return;

        var scrollRect = item.GetComponentInParent<ScrollRect>();
        if (scrollRect == null || scrollRect.content == null) return;
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        if (viewport == null) return;

        //Asegura que el layout esté calculado antes de medir (los items son dinámicos).
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, itemRect);
        var view = viewport.rect;

        float deltaY = 0f;
        if (itemBounds.max.y > view.yMax) deltaY = -(itemBounds.max.y - view.yMax);      //está por arriba
        else if (itemBounds.min.y < view.yMin) deltaY = view.yMin - itemBounds.min.y;   //está por abajo
        if (Mathf.Approximately(deltaY, 0f)) return;

        scrollRect.content.anchoredPosition += new Vector2(0f, deltaY);
        //Reaplica el clamp del ScrollRect (por si el item estaba cerca de un extremo).
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
    }

    //Id numérico con el que empieza el nombre del item ("1013 Mejiro McQueen" -> 1013).
    static int ParseLeadingId(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return -1;
        nombre = nombre.TrimStart();
        int i = 0;
        while (i < nombre.Length && char.IsDigit(nombre[i])) i++;
        if (i == 0) return -1;
        int id;
        return int.TryParse(nombre.Substring(0, i), out id) ? id : -1;
    }


    //==========================================================================
    // (Selector Uniques/General/Extras/Other Characters del listado de trajes)
    //==========================================================================

    void EnsureModelDropdown()
    {
        if (ModelDropdown != null) return;

        foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (d != null && d.gameObject.name == "ModelDropdown") { ModelDropdown = d; break; }
        }

        if (ModelDropdown == null)
        {
            Debug.LogError("[UmaViewerUI] No se encuentra el Dropdown 'ModelDropdown' del panel de personajes normales.");
            return;
        }

        RepairDropdownTemplate(ModelDropdown);

        //Descarta cualquier cableado previo (persistente o no) y deja sólo el nuestro.
        ModelDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        ModelDropdown.options.Clear();
        ModelDropdown.options.Add(new TMP_Dropdown.OptionData("Uniques"));
        ModelDropdown.options.Add(new TMP_Dropdown.OptionData("General"));
        ModelDropdown.options.Add(new TMP_Dropdown.OptionData("Extras"));
        ModelDropdown.options.Add(new TMP_Dropdown.OptionData("Other Characters"));
        ModelDropdown.SetValueWithoutNotify((int)costumeListMode);
        ModelDropdown.RefreshShownValue();
        ModelDropdown.onValueChanged.AddListener(OnModelDropdownChanged);
    }

    //El desplegable "Other Characters" se rellena con TODOS los PJ de Main.Characters, mostrando el
    //nombre sin el número inicial. Por defecto queda el primero. Sólo se ve en su modo.
    void EnsureOtherCharactersDropdown()
    {
        if (OtherCharactersDropdown != null) return;

        foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (d != null && d.gameObject.name == "OtherCharactersDropdown") { OtherCharactersDropdown = d; break; }
        }

        if (OtherCharactersDropdown == null)
        {
            Debug.LogError("[UmaViewerUI] No se encuentra el Dropdown 'OtherCharactersDropdown'.");
            return;
        }

        RepairDropdownTemplate(OtherCharactersDropdown);

        //La combobox se dibuja en el propio rótulo del desplegable, así que el clic debe pasar de
        //largo hasta el Dropdown (igual que en las combobox de voz).
        if (OtherCharactersDropdown.captionText != null)
        {
            OtherCharactersDropdown.captionText.raycastTarget = false;
            OtherCharactersDropdown.captionText.overflowMode = TextOverflowModes.Overflow;
        }
        //Cierra y reabre en cada tecla (RefreshOtherOpenList): fundido corto para que no parpadee.
        OtherCharactersDropdown.alphaFadeSpeed = VocalListFadeSpeed;

        OtherCharactersDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        otherCharactersList.Clear();
        foreach (var c in Main.Characters.OrderBy(c => c.Id)) otherCharactersList.Add(c);
        selectedOtherChara = otherCharactersList.Count > 0 ? otherCharactersList[0] : null;
        RebuildOtherCharactersOptions();
        OtherCharactersDropdown.onValueChanged.AddListener(OnOtherCharactersChanged);

        OtherCharactersDropdown.gameObject.SetActive(costumeListMode == CostumeListMode.OtherCharacters);
    }

    //Rehace las opciones del desplegable "Other Characters" según el filtro tecleado.
    //otherOptionsVisible[] queda en el MISMO orden que dd.options, para que dd.value (y por tanto
    //OnOtherCharactersChanged) apunten al PJ correcto aunque la lista esté filtrada.
    void RebuildOtherCharactersOptions()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null) return;

        string f = (otherSearchFilter ?? "").Trim();
        otherOptionsVisible.Clear();
        dd.options.Clear();
        foreach (var c in otherCharactersList)
        {
            string nombre = c.GetName() ?? "";
            if (f.Length > 0 && nombre.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
            otherOptionsVisible.Add(c);
            dd.options.Add(new TMP_Dropdown.OptionData(nombre));
        }

        if (otherOptionsVisible.Count > 0)
        {
            int idx = IndexOfOtherChara(selectedOtherChara);
            if (idx < 0) idx = 0;
            selectedOtherChara = otherOptionsVisible[idx];
            dd.SetValueWithoutNotify(idx);
        }
        dd.RefreshShownValue();
        RefreshOtherComboText();
    }

    //Índice del PJ en la lista visible: por referencia y, si no está, por NOMBRE (la lista puede
    //haberse reconstruido con instancias nuevas).
    int IndexOfOtherChara(CharaEntry chara)
    {
        if (chara == null) return -1;
        int idx = otherOptionsVisible.IndexOf(chara);
        if (idx >= 0) return idx;

        string nombre = chara.GetName();
        for (int i = 0; i < otherOptionsVisible.Count; i++)
            if (otherOptionsVisible[i] != null && otherOptionsVisible[i].GetName() == nombre) return i;
        return -1;
    }

    //El rótulo enseña lo tecleado mientras se busca; si no hay filtro, el PJ elegido.
    void RefreshOtherComboText()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || dd.captionText == null) return;

        string texto = otherSearchFilter ?? "";
        if (texto.Length == 0 && dd.value >= 0 && dd.value < dd.options.Count)
            texto = dd.options[dd.value].text;

        if (!string.Equals(dd.captionText.text, texto, StringComparison.Ordinal))
            dd.captionText.text = texto;
    }

    void OnModelDropdownChanged(int value)
    {
        costumeListMode = (CostumeListMode)value;

        //El desplegable de "otros personajes" sólo se ve en su modo.
        if (OtherCharactersDropdown != null)
            OtherCharactersDropdown.gameObject.SetActive(costumeListMode == CostumeListMode.OtherCharacters);

        if (currentCostumeChara != null) ListCostumes(currentCostumeChara, currentCostumeMini);
    }

    void OnOtherCharactersChanged(int value)
    {
        //Si venimos de un clic en la lista, manda el PJ clicado: TMP dispara esto al SOLTAR el ratón, y
        //para entonces la lista ya puede estar rehecha (sin filtro), así que su índice apuntaría a otro
        //PJ (el primero de la lista completa).
        if (pendingOtherChara != null)
        {
            CharaEntry clicado = pendingOtherChara;
            //NO se consume: TMP_Dropdown puede disparar el callback DOS veces en el mismo frame (el
            //item clicado estaba ya ON ⇒ OnSelectItem lo reenciende y se llama a sí mismo: el anidado
            //dispara con el PJ pendiente y el externo vuelve a disparar con el índice ya obsoleto).
            //El pendiente se limpia al hacer clic fuera de la lista o al empezar/terminar la búsqueda.
            ApplyOtherCharaSelection(clicado);
            return;
        }

        //dd.value indexa la lista VISIBLE (filtrada o no), no la completa.
        if (value >= 0 && value < otherOptionsVisible.Count)
            ApplyOtherCharaSelection(otherOptionsVisible[value]);
    }

    //Fija el PJ elegido, quita el filtro y recarga la lista de trajes. OJO: NO toca `pendingOtherChara`
    //(lo limpia quien corresponda): el clic de la lista lo deja puesto para que los callbacks de TMP
    //—que llegan al SOLTAR el ratón y pueden ser DOS en el mismo frame— usen ese PJ y no su índice.
    void ApplyOtherCharaSelection(CharaEntry chara)
    {
        if (chara == null) return;

        selectedOtherChara = chara;
        otherSearchFilter = "";
        otherSearchActive = false;
        otherListQuiere = false;
        RebuildOtherCharactersOptions();

        //Los dos callbacks del mismo frame no deben reconstruir la lista de trajes dos veces (Destroy
        //se difiere al final del frame y quedarían items duplicados).
        if (costumeListMode == CostumeListMode.OtherCharacters && currentCostumeChara != null &&
            lastOtherCostumeFrame != Time.frameCount)
        {
            lastOtherCostumeFrame = Time.frameCount;
            ListCostumes(currentCostumeChara, currentCostumeMini);
        }
    }

    //===== Búsqueda por teclado del desplegable "Other Characters" =====
    bool HayListaDeOtrosBajoElPuntero()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var cam = DropdownCanvasCamara(dd);
        var pos = Input.mousePosition;
        var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            var lista = hijos[i];
            if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;
            var rt = lista as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return true;
        }
        return false;
    }

    bool OtherDropdownBajoElPuntero()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return false;
        var rt = dd.transform as RectTransform;
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, DropdownCanvasCamara(dd));
    }

    //Índice de OPCIÓN del item de la lista ABIERTA bajo el puntero, o -1. El Content de la lista tiene
    //el item-plantilla en el índice 0 y los clones a partir del 1, así que el índice de opción es
    //(hermano - 1): la MISMA cuenta que hace TMP_Dropdown.OnSelectItem.
    int OtherListItemBajoElPuntero()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return -1;

        var cam = DropdownCanvasCamara(dd);
        var pos = Input.mousePosition;
        var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            var lista = hijos[i];
            if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;

            Transform content = null;
            foreach (var t in lista.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "Content") { content = t; break; }
            }
            if (content == null) continue;

            for (int j = 1; j < content.childCount; j++)   // 0 = item-plantilla
            {
                var item = content.GetChild(j) as RectTransform;
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(item, pos, cam)) return j - 1;
            }
        }
        return -1;
    }

    //PJ del item `index` de la lista abierta, resuelto por el TEXTO del item (lo que el usuario ve).
    //Así no depende de que `otherOptionsVisible` siga siendo la lista filtrada cuando llegue el
    //callback de TMP_Dropdown.
    CharaEntry OtherCharaOfItem(int index)
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || index < 0 || index >= dd.options.Count) return null;

        string texto = dd.options[index].text;
        if (string.IsNullOrEmpty(texto)) return null;

        for (int i = 0; i < otherCharactersList.Count; i++)
        {
            var c = otherCharactersList[i];
            if (c != null && c.GetName() == texto) return c;
        }
        return null;
    }

    void UpdateOtherCharacterSearch()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (!HayListaDeOtrosBajoElPuntero())
            {
                //Clic fuera de la lista: el PJ apuntado por un clic anterior ya no vale.
                pendingOtherChara = null;
                if (OtherDropdownBajoElPuntero()) BeginOtherSearch();
                else EndOtherSearch();
            }
            else
            {
                //Clic en un item: se resuelve el PJ por el TEXTO del item (lo que el usuario ve) y se
                //aplica YA. TMP_Dropdown puede ignorar el clic (si el índice ya es el "actual"; con un
                //solo resultado del filtro pasa SIEMPRE) y su onValueChanged llega al SOLTAR el ratón,
                //ya con el índice obsoleto: por eso se guarda el PJ y el callback usa ESTE.
                CharaEntry clicado = OtherCharaOfItem(OtherListItemBajoElPuntero());
                if (clicado != null)
                {
                    pendingOtherChara = clicado;
                    ApplyOtherCharaSelection(clicado);
                }
            }
            return;
        }

        if (!otherSearchActive) return;

        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable())
        {
            EndOtherSearch();
            return;
        }

        string tecleado = Input.inputString;
        if (string.IsNullOrEmpty(tecleado)) return;

        var buffer = new StringBuilder(otherSearchFilter ?? "");
        bool cambio = false;
        foreach (var c in tecleado)
        {
            if (c == '\b')
            {
                if (buffer.Length > 0) buffer.Length--;
                cambio = true;
            }
            else if (char.IsControl(c)) continue;
            else
            {
                buffer.Append(c);
                cambio = true;
            }
        }
        if (!cambio) return;

        otherSearchFilter = buffer.ToString();
        RebuildOtherCharactersOptions();
        KeepOtherListOpen();
    }

    void BeginOtherSearch()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return;

        otherSearchActive = true;
        pendingOtherChara = null;
        if (!string.IsNullOrEmpty(otherSearchFilter))
        {
            otherSearchFilter = "";
            RebuildOtherCharactersOptions();
        }
        otherListQuiere = true;
        otherListSucia = false;
        dd.Show();
    }

    void EndOtherSearch()
    {
        otherSearchActive = false;
        pendingOtherChara = null;
        var dd = OtherCharactersDropdown;
        if (dd != null && dd.IsActive()) dd.Hide();
        if (string.IsNullOrEmpty(otherSearchFilter)) return;
        otherSearchFilter = "";
        RebuildOtherCharactersOptions();
    }

    void KeepOtherListOpen()
    {
        var dd = OtherCharactersDropdown;
        if (dd == null || !dd.IsInteractable()) return;

        otherListQuiere = true;
        if (!dd.IsActive())
        {
            otherListSucia = false;
            dd.Show();
            return;
        }
        if (otherListEspera) { otherListSucia = true; return; }
        otherListEspera = true;
        StartCoroutine(RefreshOtherOpenList(dd));
    }

    IEnumerator RefreshOtherOpenList(TMP_Dropdown dd)
    {
        while (true)
        {
            dd.Hide();
            yield return new WaitForSecondsRealtime(VocalListFadeSpeed + 0.02f);
            otherListSucia = false;
            if (!otherListQuiere) break;
            if (OtherCharactersDropdown != dd || !dd.IsInteractable()) break;
            dd.Show();
            if (!otherListSucia) break;
        }
        otherListEspera = false;
        otherListSucia = false;
    }


    //==========================================================================
    // (Buscador de personajes del panel Characters)
    //  · Se rellena con el listado del panel de tipo activo leyendo su "Character List/Viewport/
    //    Content" (Normal), "Mini Character List/Viewport/Content" (Mini) o el Content del PageManager
    //    (Mob).
    //  · Al elegir una entrada se dispara el click de ese mismo item de la lista.
    //  · Búsqueda por teclado con filtrado de la lista abierta.
    //==========================================================================

    void EnsureSearchCharacterDropdown()
    {
        if (SearchCharacterDropdown != null) return;

        foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (d != null && d.gameObject.name == "SearchCharacterDropdown") { SearchCharacterDropdown = d; break; }
        }

        if (SearchCharacterDropdown == null)
        {
            Debug.LogError("[UmaViewerUI] No se encuentra el Dropdown 'SearchCharacterDropdown' del panel Characters.");
            return;
        }

        SearchCharacterRow = SearchCharacterDropdown.transform.parent != null
            ? SearchCharacterDropdown.transform.parent.gameObject
            : null;

        RepairDropdownTemplate(SearchCharacterDropdown);

        //La combobox se dibuja en el propio rótulo: el clic debe pasar al Dropdown.
        if (SearchCharacterDropdown.captionText != null)
        {
            SearchCharacterDropdown.captionText.raycastTarget = false;
            SearchCharacterDropdown.captionText.overflowMode = TextOverflowModes.Overflow;
        }
        SearchCharacterDropdown.alphaFadeSpeed = VocalListFadeSpeed;

        SearchCharacterDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        SearchCharacterDropdown.onValueChanged.AddListener(OnSearchCharacterChanged);

        ResolveSearchCharacterPanels();
        RebuildSearchCharacterOptions(true);
        RefreshSearchCharacterVisibility();
    }

    //Cachea los 3 paneles de tipo (Normal/Mini/Mob) y el Content de su lista de personajes. Así el
    //chequeo por frame no recorre la jerarquía entera, sólo mira activeInHierarchy.
    void ResolveSearchCharacterPanels()
    {
        searchPanelNormal = searchPanelMini = searchPanelMob = null;
        searchContentNormal = searchContentMini = searchContentMob = null;

        var characters = SearchCharacterRow != null ? SearchCharacterRow.transform.parent : null;
        if (characters == null) return;

        var normal = FindDescendant(characters, "NormalCharacters");
        var mini = FindDescendant(characters, "MiniCharacters");
        var mob = FindDescendant(characters, "MobCharacters");

        searchPanelNormal = normal != null ? normal.gameObject : null;
        searchPanelMini = mini != null ? mini.gameObject : null;
        searchPanelMob = mob != null ? mob.gameObject : null;

        //Cada tipo nombra distinto su lista (y mobs la pagina el PageManager en el mismo Content).
        searchContentNormal = FindListContent(normal, "Character List");
        searchContentMini = FindListContent(mini, "Mini Character List");
        searchContentMob = FindListContent(mob, "Character List");
    }

    //Devuelve el Content del panel de tipo ACTIVO ("el listado actual"), o null.
    Transform ActiveSearchCharacterContent(out GameObject panel)
    {
        if (searchPanelNormal != null && searchPanelNormal.activeInHierarchy) { panel = searchPanelNormal; return searchContentNormal; }
        if (searchPanelMini != null && searchPanelMini.activeInHierarchy) { panel = searchPanelMini; return searchContentMini; }
        if (searchPanelMob != null && searchPanelMob.activeInHierarchy) { panel = searchPanelMob; return searchContentMob; }
        panel = null;
        return null;
    }

    //Mantiene la fila y el desplegable encendidos (la fila vive siempre bajo Characters y sólo se ve
    //cuando ese panel está activo; aquí sólo se garantiza que no queden apagados).
    void RefreshSearchCharacterVisibility()
    {
        if (SearchCharacterRow != null && !SearchCharacterRow.activeSelf) SearchCharacterRow.SetActive(true);
        if (SearchCharacterDropdown != null && !SearchCharacterDropdown.gameObject.activeSelf)
            SearchCharacterDropdown.gameObject.SetActive(true);
    }

    //Refresca la lista cada vez que cambia el panel de tipo activo o su contenido. La firma es barata
    //(panel + nº de items + primero/último), de modo que no recorre la lista cada frame.
    void UpdateSearchCharacter()
    {
        if (SearchCharacterDropdown == null) return;
        RefreshSearchCharacterVisibility();

        //Tras elegir una entrada se cierra la búsqueda y se vuelve a la lista completa (y al item
        //elegido en el rótulo). Se difiere aquí para no tocar dd.options dentro del callback del
        //propio Dropdown (OnSelectItem), que podría estar en mitad de su Set().
        if (searchCharacterNeedsRefresh && !SearchCharacterListVisible())
        {
            searchCharacterNeedsRefresh = false;
            searchCharacterFilter = "";
            RebuildSearchCharacterOptions(false);
            int pos = searchCharacterSelectedItemIndex >= 0
                ? searchCharacterVisible.IndexOf(searchCharacterSelectedItemIndex)
                : -1;
            if (pos >= 0)
            {
                SearchCharacterDropdown.SetValueWithoutNotify(pos);
                SearchCharacterDropdown.RefreshShownValue();
                RefreshSearchComboText();
            }
        }

        GameObject panel;
        var content = ActiveSearchCharacterContent(out panel);
        string firma = (panel != null ? panel.name : "none") + "|" + (content != null ? content.childCount : 0);
        if (content != null && content.childCount > 0)
            firma += "|" + content.GetChild(0).name + "|" + content.GetChild(content.childCount - 1).name;

        if (firma != searchCharacterSignature && !searchCharacterActive)
        {
            searchCharacterSignature = firma;
            RebuildSearchCharacterOptions(true);
        }
        //Se sincroniza con la selección real de la lista (si el usuario eligió un PJ sin el buscador).
        SyncSearchCharacterSelectionFromCurrent();
        //Reafirma el rótulo: vacío hasta que el usuario elija algo (el Dropdown lo repinta a options[0]
        //al activarse el panel, y no queremos ese texto por defecto).
        RefreshSearchComboText();
    }

    //Si el PJ actual (elegido en la lista, sin el buscador) está en el listado del desplegable, lo
    //adopta como selección para que el rótulo lo muestre al abrir el panel.
    void SyncSearchCharacterSelectionFromCurrent()
    {
        if (SearchCharacterDropdown == null) return;
        if (searchCharacterActive) return;                     //no pisar mientras se busca
        if (currentCostumeChara == null) return;
        if (SearchCharacterRow == null || !SearchCharacterRow.activeInHierarchy) return;

        int idx = -1;
        for (int i = 0; i < searchCharacterNames.Count; i++)
        {
            if (ParseLeadingId(searchCharacterNames[i]) == currentCostumeChara.Id) { idx = i; break; }
        }
        if (idx < 0) return;
        if (searchCharacterFilter == "" && searchCharacterSelectedItemIndex == idx) return;

        searchCharacterSelectedItemIndex = idx;
        searchCharacterFilter = "";
        RebuildSearchCharacterOptions(false);
        int pos = searchCharacterVisible.IndexOf(idx);
        if (pos >= 0)
        {
            SearchCharacterDropdown.SetValueWithoutNotify(pos);
            SearchCharacterDropdown.RefreshShownValue();
            RefreshSearchComboText();
        }
    }

    //Lee los UmaUIContainer del Content activo (su orden = orden visible de la lista).
    void CollectActiveSearchCharacterItems()
    {
        searchCharacterItems.Clear();
        searchCharacterNames.Clear();

        GameObject panel;
        var content = ActiveSearchCharacterContent(out panel);
        if (content == null) return;

        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            if (child == null) continue;
            var container = child.GetComponent<UmaUIContainer>();
            if (container == null) continue;
            string nombre = container.Name;
            if (string.IsNullOrEmpty(nombre)) nombre = child.name;
            searchCharacterItems.Add(child);
            searchCharacterNames.Add(nombre);
        }
    }

    //Rehace las opciones del desplegable. rebuildItems=true vuelve a leer la lista del panel activo;
    //false sólo reaplica el filtro tecleado. searchCharacterVisible[] queda en el MISMO orden que
    //dd.options, para que dd.value (y OnSearchCharacterChanged) apunten al item correcto.
    void RebuildSearchCharacterOptions(bool rebuildItems)
    {
        var dd = SearchCharacterDropdown;
        if (dd == null) return;

        if (rebuildItems) CollectActiveSearchCharacterItems();

        string f = (searchCharacterFilter ?? "").Trim();
        searchCharacterVisible.Clear();
        dd.options.Clear();
        for (int i = 0; i < searchCharacterNames.Count; i++)
        {
            string nombre = searchCharacterNames[i] ?? "";
            if (f.Length > 0 && nombre.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
            searchCharacterVisible.Add(i);
            dd.options.Add(new TMP_Dropdown.OptionData(nombre));
        }

        if (dd.options.Count > 0) dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        RefreshSearchComboText();
    }

    //El rótulo enseña lo tecleado mientras se busca; si no hay filtro, el item elegido.
    void RefreshSearchComboText()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || dd.captionText == null) return;

        string texto = searchCharacterFilter ?? "";
        if (texto.Length == 0 && searchCharacterSelectedItemIndex >= 0 && dd.value >= 0 && dd.value < dd.options.Count)
            texto = dd.options[dd.value].text;

        if (!string.Equals(dd.captionText.text, texto, StringComparison.Ordinal))
            dd.captionText.text = texto;
    }

    //Callback del Dropdown: aplica la entrada elegida. Marca que onValueChanged ya la aplicó para que
    //la detección de cierre de lista no la aplique dos veces.
    void OnSearchCharacterChanged(int value)
    {
        searchCharacterValueHandled = true;
        ApplySearchCharacterSelection(value);
    }

    //Al elegir una entrada, se pulsa su item de la lista (carga el personaje/traje).
    void ApplySearchCharacterSelection(int value)
    {
        if (value < 0 || value >= searchCharacterVisible.Count) return;
        int itemIndex = searchCharacterVisible[value];
        if (itemIndex < 0 || itemIndex >= searchCharacterItems.Count) return;

        var item = searchCharacterItems[itemIndex];
        if (item == null) return;

        var container = item.GetComponent<UmaUIContainer>();
        var btn = container != null ? container.Button : item.GetComponent<Button>();
        if (btn == null) btn = item.GetComponentInChildren<Button>(true);
        if (btn != null && btn.IsInteractable()) btn.onClick.Invoke();

        //Desplaza el ScrollRect de la lista activa hasta la entrada elegida para dejarla a la vista.
        ScrollListToItem(item);

        //Cierra la búsqueda y programa la vuelta a la lista completa (ver UpdateSearchCharacter).
        searchCharacterSelectedItemIndex = itemIndex;
        searchCharacterActive = false;
        searchCharacterNeedsRefresh = true;
    }

    //===== Búsqueda por teclado del buscador de personajes =====
    //OJO: Dropdown.IsActive() NO sirve aquí: es el de Selectable y responde a "el desplegable está
    //activo e interactuable", no a "la lista está abierta". Se busca el GameObject "Dropdown List".
    bool SearchCharacterListVisible()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var parent = dd.template.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var t = parent.GetChild(i);
            if (t != null && t.name == "Dropdown List" && t.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    bool HayListaDeBusquedaBajoElPuntero()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var cam = DropdownCanvasCamara(dd);
        var pos = Input.mousePosition;
        var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            var lista = hijos[i];
            if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;
            var rt = lista as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return true;
        }
        return false;
    }

    bool SearchDropdownBajoElPuntero()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return false;
        var rt = dd.transform as RectTransform;
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, DropdownCanvasCamara(dd));
    }

    void UpdateSearchCharacterSearch()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy)
        {
            searchCharacterListWasOpen = false;
            searchCharacterClickOnList = false;
            return;
        }

        //La lista acaba de cerrarse: si el cierre vino de pulsar una entrada, se aplica (aunque el
        //índice elegido coincida con el actual; ver el comentario de los flags de selección).
        bool abierta = SearchCharacterListVisible();
        if (searchCharacterListWasOpen && !abierta && searchCharacterClickOnList)
        {
            searchCharacterClickOnList = false;
            if (!searchCharacterValueHandled) ApplySearchCharacterSelection(dd.value);
        }
        searchCharacterListWasOpen = abierta;

        if (Input.GetMouseButtonDown(0))
        {
            if (!HayListaDeBusquedaBajoElPuntero())
            {
                if (SearchDropdownBajoElPuntero()) BeginSearchCharacterSearch();
                else EndSearchCharacterSearch();
            }
            else
            {
                //El clic es sobre una entrada de la lista abierta: la procesará el propio Dropdown en
                //el pointer-up. Se marca para aplicar la selección al cerrarse la lista.
                searchCharacterClickOnList = true;
                searchCharacterValueHandled = false;
            }
            return;
        }

        if (!searchCharacterActive) return;

        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable())
        {
            EndSearchCharacterSearch();
            return;
        }

        string tecleado = Input.inputString;
        if (string.IsNullOrEmpty(tecleado)) return;

        var buffer = new StringBuilder(searchCharacterFilter ?? "");
        bool cambio = false;
        foreach (var c in tecleado)
        {
            if (c == '\b')
            {
                if (buffer.Length > 0) buffer.Length--;
                cambio = true;
            }
            else if (char.IsControl(c)) continue;
            else
            {
                buffer.Append(c);
                cambio = true;
            }
        }
        if (!cambio) return;

        searchCharacterFilter = buffer.ToString();
        RebuildSearchCharacterOptions(false);
        KeepSearchCharacterListOpen();
    }

    void BeginSearchCharacterSearch()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return;

        searchCharacterActive = true;
        if (!string.IsNullOrEmpty(searchCharacterFilter))
        {
            searchCharacterFilter = "";
            RebuildSearchCharacterOptions(false);
        }
        searchCharacterListQuiere = true;
        searchCharacterListSucia = false;
        dd.Show();
    }

    void EndSearchCharacterSearch()
    {
        searchCharacterActive = false;
        var dd = SearchCharacterDropdown;
        if (dd != null && dd.IsActive()) dd.Hide();
        if (string.IsNullOrEmpty(searchCharacterFilter)) return;
        searchCharacterFilter = "";
        RebuildSearchCharacterOptions(false);
    }

    void KeepSearchCharacterListOpen()
    {
        var dd = SearchCharacterDropdown;
        if (dd == null || !dd.IsInteractable()) return;

        searchCharacterListQuiere = true;
        if (!dd.IsActive())
        {
            searchCharacterListSucia = false;
            dd.Show();
            return;
        }
        if (searchCharacterListEspera) { searchCharacterListSucia = true; return; }
        searchCharacterListEspera = true;
        StartCoroutine(RefreshSearchCharacterOpenList(dd));
    }

    IEnumerator RefreshSearchCharacterOpenList(TMP_Dropdown dd)
    {
        while (true)
        {
            dd.Hide();
            yield return new WaitForSecondsRealtime(VocalListFadeSpeed + 0.02f);
            searchCharacterListSucia = false;
            if (!searchCharacterListQuiere) break;
            if (SearchCharacterDropdown != dd || !dd.IsInteractable()) break;
            dd.Show();
            if (!searchCharacterListSucia) break;
        }
        searchCharacterListEspera = false;
        searchCharacterListSucia = false;
    }


    //==========================================================================
    // (Buscador de shows del panel Shows — SearchLive)
    //==========================================================================

    void EnsureSearchLiveDropdown()
    {
        if (SearchLiveDropdown != null) return;

        foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (d != null && d.gameObject.name == "SearchLiveDropdown") { SearchLiveDropdown = d; break; }
        }

        if (SearchLiveDropdown == null)
        {
            Debug.LogError("[UmaViewerUI] No se encuentra el Dropdown 'SearchLiveDropdown' del panel Shows.");
            return;
        }

        SearchLiveRow = SearchLiveDropdown.transform.parent != null
            ? SearchLiveDropdown.transform.parent.gameObject
            : null;

        RepairDropdownTemplate(SearchLiveDropdown);

        if (SearchLiveDropdown.captionText != null)
        {
            SearchLiveDropdown.captionText.raycastTarget = false;
            SearchLiveDropdown.captionText.overflowMode = TextOverflowModes.Overflow;
        }
        SearchLiveDropdown.alphaFadeSpeed = VocalListFadeSpeed;

        SearchLiveDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        SearchLiveDropdown.onValueChanged.AddListener(OnSearchLiveChanged);

        RebuildSearchLiveOptions(true);
        RefreshSearchLiveVisibility();
    }

    Transform GetSearchLiveContent()
    {
        if (LiveList != null && LiveList.content != null) return LiveList.content;
        var shows = SearchLiveRow != null ? SearchLiveRow.transform.parent : null;
        var list = shows != null ? FindDescendant(shows, "Live List") : null;
        return list != null ? FindDescendant(list, "Content") : null;
    }

    void CollectSearchLiveItems()
    {
        searchLiveItems.Clear();
        searchLiveNames.Clear();

        var content = GetSearchLiveContent();
        if (content == null) return;

        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            if (child == null) continue;
            string nombre = SearchComboItemText(child);
            if (string.IsNullOrEmpty(nombre)) nombre = child.name;
            searchLiveItems.Add(child);
            searchLiveNames.Add(nombre);
        }
    }

    string SearchLiveSignature(Transform content)
    {
        if (content == null || content.childCount == 0) return "none|0";
        string first = SearchComboItemText(content.GetChild(0));
        string last = SearchComboItemText(content.GetChild(content.childCount - 1));
        return content.childCount + "|" + first + "|" + last;
    }

    void RefreshSearchLiveVisibility()
    {
        if (SearchLiveRow != null && !SearchLiveRow.activeSelf) SearchLiveRow.SetActive(true);
        if (SearchLiveDropdown != null && !SearchLiveDropdown.gameObject.activeSelf)
            SearchLiveDropdown.gameObject.SetActive(true);
    }

    void UpdateSearchLive()
    {
        if (SearchLiveDropdown == null) return;
        RefreshSearchLiveVisibility();

        if (searchLiveNeedsRefresh && !SearchLiveListVisible())
        {
            searchLiveNeedsRefresh = false;
            searchLiveFilter = "";
            RebuildSearchLiveOptions(false);
            int pos = searchLiveSelectedItemIndex >= 0
                ? searchLiveVisible.IndexOf(searchLiveSelectedItemIndex)
                : -1;
            if (pos >= 0)
            {
                SearchLiveDropdown.SetValueWithoutNotify(pos);
                SearchLiveDropdown.RefreshShownValue();
                RefreshSearchLiveComboText();
            }
        }

        var content = GetSearchLiveContent();
        string firma = SearchLiveSignature(content);
        if (firma != searchLiveSignature && !searchLiveActive)
        {
            searchLiveSignature = firma;
            RebuildSearchLiveOptions(true);
        }
        //Reafirma el rótulo: vacío hasta que el usuario elija algo.
        RefreshSearchLiveComboText();
    }

    void RebuildSearchLiveOptions(bool rebuildItems)
    {
        var dd = SearchLiveDropdown;
        if (dd == null) return;

        if (rebuildItems) CollectSearchLiveItems();

        string f = (searchLiveFilter ?? "").Trim();
        searchLiveVisible.Clear();
        dd.options.Clear();
        for (int i = 0; i < searchLiveNames.Count; i++)
        {
            string nombre = searchLiveNames[i] ?? "";
            if (f.Length > 0 && nombre.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
            searchLiveVisible.Add(i);
            dd.options.Add(new TMP_Dropdown.OptionData(nombre));
        }

        if (dd.options.Count > 0) dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        RefreshSearchLiveComboText();
    }

    void RefreshSearchLiveComboText()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || dd.captionText == null) return;

        string texto = searchLiveFilter ?? "";
        if (texto.Length == 0 && searchLiveSelectedItemIndex >= 0 && dd.value >= 0 && dd.value < dd.options.Count)
            texto = dd.options[dd.value].text;

        if (!string.Equals(dd.captionText.text, texto, StringComparison.Ordinal))
            dd.captionText.text = texto;
    }

    void OnSearchLiveChanged(int value)
    {
        searchLiveValueHandled = true;
        ApplySearchLiveSelection(value);
    }

    void ApplySearchLiveSelection(int value)
    {
        if (value < 0 || value >= searchLiveVisible.Count) return;
        int itemIndex = searchLiveVisible[value];
        if (itemIndex < 0 || itemIndex >= searchLiveItems.Count) return;

        var item = searchLiveItems[itemIndex];
        if (item == null) return;

        var container = item.GetComponent<UmaUIContainer>();
        var btn = container != null ? container.Button : item.GetComponent<Button>();
        if (btn == null) btn = item.GetComponentInChildren<Button>(true);
        if (btn != null && btn.IsInteractable()) btn.onClick.Invoke();

        ScrollListToItem(item);

        searchLiveSelectedItemIndex = itemIndex;
        searchLiveActive = false;
        searchLiveNeedsRefresh = true;
    }

    //===== Búsqueda por teclado del buscador de shows =====
    bool SearchLiveListVisible()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var parent = dd.template.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var t = parent.GetChild(i);
            if (t != null && t.name == "Dropdown List" && t.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    bool HayListaDeBusquedaLiveBajoElPuntero()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var cam = DropdownCanvasCamara(dd);
        var pos = Input.mousePosition;
        var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            var lista = hijos[i];
            if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;
            var rt = lista as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return true;
        }
        return false;
    }

    bool SearchLiveDropdownBajoElPuntero()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return false;
        var rt = dd.transform as RectTransform;
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, DropdownCanvasCamara(dd));
    }

    void UpdateSearchLiveSearch()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy)
        {
            searchLiveListWasOpen = false;
            searchLiveClickOnList = false;
            return;
        }

        bool abierta = SearchLiveListVisible();
        if (searchLiveListWasOpen && !abierta && searchLiveClickOnList)
        {
            searchLiveClickOnList = false;
            if (!searchLiveValueHandled) ApplySearchLiveSelection(dd.value);
        }
        searchLiveListWasOpen = abierta;

        if (Input.GetMouseButtonDown(0))
        {
            if (!HayListaDeBusquedaLiveBajoElPuntero())
            {
                if (SearchLiveDropdownBajoElPuntero()) BeginSearchLiveSearch();
                else EndSearchLiveSearch();
            }
            else
            {
                searchLiveClickOnList = true;
                searchLiveValueHandled = false;
            }
            return;
        }

        if (!searchLiveActive) return;

        if (!dd.gameObject.activeInHierarchy || !dd.IsInteractable())
        {
            EndSearchLiveSearch();
            return;
        }

        string tecleado = Input.inputString;
        if (string.IsNullOrEmpty(tecleado)) return;

        var buffer = new StringBuilder(searchLiveFilter ?? "");
        bool cambio = false;
        foreach (var c in tecleado)
        {
            if (c == '\b')
            {
                if (buffer.Length > 0) buffer.Length--;
                cambio = true;
            }
            else if (char.IsControl(c)) continue;
            else
            {
                buffer.Append(c);
                cambio = true;
            }
        }
        if (!cambio) return;

        searchLiveFilter = buffer.ToString();
        RebuildSearchLiveOptions(false);
        KeepSearchLiveListOpen();
    }

    void BeginSearchLiveSearch()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return;

        searchLiveActive = true;
        if (!string.IsNullOrEmpty(searchLiveFilter))
        {
            searchLiveFilter = "";
            RebuildSearchLiveOptions(false);
        }
        searchLiveListQuiere = true;
        searchLiveListSucia = false;
        dd.Show();
    }

    void EndSearchLiveSearch()
    {
        searchLiveActive = false;
        var dd = SearchLiveDropdown;
        if (dd != null && dd.IsActive()) dd.Hide();
        if (string.IsNullOrEmpty(searchLiveFilter)) return;
        searchLiveFilter = "";
        RebuildSearchLiveOptions(false);
    }

    void KeepSearchLiveListOpen()
    {
        var dd = SearchLiveDropdown;
        if (dd == null || !dd.IsInteractable()) return;

        searchLiveListQuiere = true;
        if (!dd.IsActive())
        {
            searchLiveListSucia = false;
            dd.Show();
            return;
        }
        if (searchLiveListEspera) { searchLiveListSucia = true; return; }
        searchLiveListEspera = true;
        StartCoroutine(RefreshSearchLiveOpenList(dd));
    }

    IEnumerator RefreshSearchLiveOpenList(TMP_Dropdown dd)
    {
        while (true)
        {
            dd.Hide();
            yield return new WaitForSecondsRealtime(VocalListFadeSpeed + 0.02f);
            searchLiveListSucia = false;
            if (!searchLiveListQuiere) break;
            if (SearchLiveDropdown != dd || !dd.IsInteractable()) break;
            dd.Show();
            if (!searchLiveListSucia) break;
        }
        searchLiveListEspera = false;
        searchLiveListSucia = false;
    }


    //==========================================================================
    // (Buscador de canciones/sonidos del panel Sounds — SearchSounds)
    //  · Live   -> canciones:  "Sounds/LiveSound List/Viewport/Content"
    //  · Normal -> sonidos:    "Sounds/NormalSound List/NormalSound/Viewport/Content"
    //==========================================================================

    void EnsureSearchSoundsDropdown()
    {
        if (SearchSoundsDropdown != null) return;

        foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (d != null && d.gameObject.name == "SearchSoundsDropdown") { SearchSoundsDropdown = d; break; }
        }

        if (SearchSoundsDropdown == null)
        {
            Debug.LogError("[UmaViewerUI] No se encuentra el Dropdown 'SearchSoundsDropdown' del panel Sounds.");
            return;
        }

        SearchSoundsRow = SearchSoundsDropdown.transform.parent != null
            ? SearchSoundsDropdown.transform.parent.gameObject
            : null;

        RepairDropdownTemplate(SearchSoundsDropdown);

        if (SearchSoundsDropdown.captionText != null)
        {
            SearchSoundsDropdown.captionText.raycastTarget = false;
            SearchSoundsDropdown.captionText.overflowMode = TextOverflowModes.Overflow;
        }
        SearchSoundsDropdown.alphaFadeSpeed = VocalListFadeSpeed;

        SearchSoundsDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        SearchSoundsDropdown.onValueChanged.AddListener(OnSearchSoundsChanged);

        RebuildSearchSoundsOptions(true);
        RefreshSearchSoundsVisibility();
    }

    //Content de la pestaña activa: canciones (Live) o sonidos (Normal).
    Transform GetSearchSoundsContent()
    {
        if (LiveSoundList != null && LiveSoundList.gameObject.activeInHierarchy && LiveSoundList.content != null)
            return LiveSoundList.content;
        if (NormalSoundList != null && NormalSoundList.gameObject.activeInHierarchy && NormalSoundList.content != null)
            return NormalSoundList.content;
        return null;
    }

    void CollectSearchSoundsItems()
    {
        searchSoundsItems.Clear();
        searchSoundsNames.Clear();

        var content = GetSearchSoundsContent();
        if (content == null) return;

        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            if (child == null) continue;
            string nombre = SearchComboItemText(child);
            if (string.IsNullOrEmpty(nombre)) nombre = child.name;
            searchSoundsItems.Add(child);
            searchSoundsNames.Add(nombre);
        }
    }

    string SearchSoundsSignature(Transform content)
    {
        if (content == null || content.childCount == 0) return "none|0";
        string first = SearchComboItemText(content.GetChild(0));
        string last = SearchComboItemText(content.GetChild(content.childCount - 1));
        return content.childCount + "|" + first + "|" + last;
    }

    void RefreshSearchSoundsVisibility()
    {
        if (SearchSoundsRow != null && !SearchSoundsRow.activeSelf) SearchSoundsRow.SetActive(true);
        if (SearchSoundsDropdown != null && !SearchSoundsDropdown.gameObject.activeSelf)
            SearchSoundsDropdown.gameObject.SetActive(true);
    }

    void UpdateSearchSounds()
    {
        if (SearchSoundsDropdown == null) return;
        RefreshSearchSoundsVisibility();

        if (searchSoundsNeedsRefresh && !SearchSoundsListVisible())
        {
            searchSoundsNeedsRefresh = false;
            searchSoundsFilter = "";
            RebuildSearchSoundsOptions(false);
            int pos = searchSoundsSelectedItemIndex >= 0
                ? searchSoundsVisible.IndexOf(searchSoundsSelectedItemIndex)
                : -1;
            if (pos >= 0)
            {
                SearchSoundsDropdown.SetValueWithoutNotify(pos);
                SearchSoundsDropdown.RefreshShownValue();
                RefreshSearchSoundsComboText();
            }
        }

        var content = GetSearchSoundsContent();
        string firma = SearchSoundsSignature(content);
        if (firma != searchSoundsSignature && !searchSoundsActive)
        {
            searchSoundsSignature = firma;
            RebuildSearchSoundsOptions(true);
        }
        //Se sincroniza con la canción elegida en la lista (si se eligió sin el buscador).
        SyncSearchSoundsSelectionFromCurrent();
        //Reafirma el rótulo: vacío hasta que el usuario elija algo.
        RefreshSearchSoundsComboText();
    }

    //Si hay una canción elegida en la lista (sin el buscador) y sigue en el listado actual, el
    //desplegable "SearchSounds" la adopta como selección al abrir el panel.
    void SyncSearchSoundsSelectionFromCurrent()
    {
        if (SearchSoundsDropdown == null) return;
        if (searchSoundsActive) return;
        if (selectedLiveSound == null) return;
        if (LiveSoundList == null || !LiveSoundList.gameObject.activeInHierarchy) return;

        string prefix = " " + selectedLiveSound.MusicId + " ";
        int idx = -1;
        for (int i = 0; i < searchSoundsNames.Count; i++)
        {
            if (searchSoundsNames[i] != null && searchSoundsNames[i].StartsWith(prefix)) { idx = i; break; }
        }
        if (idx < 0) return;
        if (searchSoundsFilter == "" && searchSoundsSelectedItemIndex == idx) return;

        searchSoundsSelectedItemIndex = idx;
        searchSoundsFilter = "";
        RebuildSearchSoundsOptions(false);
        int pos = searchSoundsVisible.IndexOf(idx);
        if (pos >= 0)
        {
            SearchSoundsDropdown.SetValueWithoutNotify(pos);
            SearchSoundsDropdown.RefreshShownValue();
            RefreshSearchSoundsComboText();
        }
    }

    void RebuildSearchSoundsOptions(bool rebuildItems)
    {
        var dd = SearchSoundsDropdown;
        if (dd == null) return;

        if (rebuildItems) CollectSearchSoundsItems();

        string f = (searchSoundsFilter ?? "").Trim();
        searchSoundsVisible.Clear();
        dd.options.Clear();
        for (int i = 0; i < searchSoundsNames.Count; i++)
        {
            string nombre = searchSoundsNames[i] ?? "";
            if (f.Length > 0 && nombre.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
            searchSoundsVisible.Add(i);
            dd.options.Add(new TMP_Dropdown.OptionData(nombre));
        }

        if (dd.options.Count > 0) dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        RefreshSearchSoundsComboText();
    }

    void RefreshSearchSoundsComboText()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || dd.captionText == null) return;

        string texto = searchSoundsFilter ?? "";
        if (texto.Length == 0 && searchSoundsSelectedItemIndex >= 0 && dd.value >= 0 && dd.value < dd.options.Count)
            texto = dd.options[dd.value].text;

        if (!string.Equals(dd.captionText.text, texto, StringComparison.Ordinal))
            dd.captionText.text = texto;
    }

    void OnSearchSoundsChanged(int value)
    {
        searchSoundsValueHandled = true;
        ApplySearchSoundsSelection(value);
    }

    void ApplySearchSoundsSelection(int value)
    {
        if (value < 0 || value >= searchSoundsVisible.Count) return;
        int itemIndex = searchSoundsVisible[value];
        if (itemIndex < 0 || itemIndex >= searchSoundsItems.Count) return;

        var item = searchSoundsItems[itemIndex];
        if (item == null) return;

        var container = item.GetComponent<UmaUIContainer>();
        var btn = container != null ? container.Button : item.GetComponent<Button>();
        if (btn == null) btn = item.GetComponentInChildren<Button>(true);
        if (btn != null && btn.IsInteractable()) btn.onClick.Invoke();

        ScrollListToItem(item);

        searchSoundsSelectedItemIndex = itemIndex;
        searchSoundsActive = false;
        searchSoundsNeedsRefresh = true;
    }

    //===== Búsqueda por teclado del buscador de sonidos =====
    bool SearchSoundsListVisible()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var parent = dd.template.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var t = parent.GetChild(i);
            if (t != null && t.name == "Dropdown List" && t.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    bool HayListaDeBusquedaSoundsBajoElPuntero()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var cam = DropdownCanvasCamara(dd);
        var pos = Input.mousePosition;
        var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            var lista = hijos[i];
            if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;
            var rt = lista as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return true;
        }
        return false;
    }

    bool SearchSoundsDropdownBajoElPuntero()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return false;
        var rt = dd.transform as RectTransform;
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, DropdownCanvasCamara(dd));
    }

    void UpdateSearchSoundsSearch()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy)
        {
            searchSoundsListWasOpen = false;
            searchSoundsClickOnList = false;
            return;
        }

        bool abierta = SearchSoundsListVisible();
        if (searchSoundsListWasOpen && !abierta && searchSoundsClickOnList)
        {
            searchSoundsClickOnList = false;
            if (!searchSoundsValueHandled) ApplySearchSoundsSelection(dd.value);
        }
        searchSoundsListWasOpen = abierta;

        if (Input.GetMouseButtonDown(0))
        {
            if (!HayListaDeBusquedaSoundsBajoElPuntero())
            {
                if (SearchSoundsDropdownBajoElPuntero()) BeginSearchSoundsSearch();
                else EndSearchSoundsSearch();
            }
            else
            {
                searchSoundsClickOnList = true;
                searchSoundsValueHandled = false;
            }
            return;
        }

        if (!searchSoundsActive) return;

        if (!dd.gameObject.activeInHierarchy || !dd.IsInteractable())
        {
            EndSearchSoundsSearch();
            return;
        }

        string tecleado = Input.inputString;
        if (string.IsNullOrEmpty(tecleado)) return;

        var buffer = new StringBuilder(searchSoundsFilter ?? "");
        bool cambio = false;
        foreach (var c in tecleado)
        {
            if (c == '\b')
            {
                if (buffer.Length > 0) buffer.Length--;
                cambio = true;
            }
            else if (char.IsControl(c)) continue;
            else
            {
                buffer.Append(c);
                cambio = true;
            }
        }
        if (!cambio) return;

        searchSoundsFilter = buffer.ToString();
        RebuildSearchSoundsOptions(false);
        KeepSearchSoundsListOpen();
    }

    void BeginSearchSoundsSearch()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return;

        searchSoundsActive = true;
        if (!string.IsNullOrEmpty(searchSoundsFilter))
        {
            searchSoundsFilter = "";
            RebuildSearchSoundsOptions(false);
        }
        searchSoundsListQuiere = true;
        searchSoundsListSucia = false;
        dd.Show();
    }

    void EndSearchSoundsSearch()
    {
        searchSoundsActive = false;
        var dd = SearchSoundsDropdown;
        if (dd != null && dd.IsActive()) dd.Hide();
        if (string.IsNullOrEmpty(searchSoundsFilter)) return;
        searchSoundsFilter = "";
        RebuildSearchSoundsOptions(false);
    }

    void KeepSearchSoundsListOpen()
    {
        var dd = SearchSoundsDropdown;
        if (dd == null || !dd.IsInteractable()) return;

        searchSoundsListQuiere = true;
        if (!dd.IsActive())
        {
            searchSoundsListSucia = false;
            dd.Show();
            return;
        }
        if (searchSoundsListEspera) { searchSoundsListSucia = true; return; }
        searchSoundsListEspera = true;
        StartCoroutine(RefreshSearchSoundsOpenList(dd));
    }

    IEnumerator RefreshSearchSoundsOpenList(TMP_Dropdown dd)
    {
        while (true)
        {
            dd.Hide();
            yield return new WaitForSecondsRealtime(VocalListFadeSpeed + 0.02f);
            searchSoundsListSucia = false;
            if (!searchSoundsListQuiere) break;
            if (SearchSoundsDropdown != dd || !dd.IsInteractable()) break;
            dd.Show();
            if (!searchSoundsListSucia) break;
        }
        searchSoundsListEspera = false;
        searchSoundsListSucia = false;
    }


    //==========================================================================
    // (Segundo buscador del panel Sounds — SearchSoundsbyCharacter)
    //  · Se rellena con el listado de PJ de "Characters/NormalCharacters/Character List" y arranca SIN
    //    selección.
    //  · Al elegir un PJ deja en "Sounds/LiveSound List" sólo las canciones cuyas voces
    //    ("Sounds/LiveCharaSound List") incluyan a ese PJ; "SearchSounds" se recarga con el listado
    //    filtrado.
    //  · También filtra al teclear.
    //==========================================================================

    void EnsureSearchByCharaDropdown()
    {
        if (SearchByCharaDropdown != null) return;

        foreach (var d in FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (d != null && d.transform.parent != null && d.transform.parent.name == "SearchSoundsbyCharacter")
            { SearchByCharaDropdown = d; break; }
        }

        if (SearchByCharaDropdown == null)
        {
            Debug.LogError("[UmaViewerUI] No se encuentra el Dropdown de 'SearchSoundsbyCharacter'.");
            return;
        }

        SearchByCharaRow = SearchByCharaDropdown.transform.parent.gameObject;

        RepairDropdownTemplate(SearchByCharaDropdown);

        if (SearchByCharaDropdown.captionText != null)
        {
            SearchByCharaDropdown.captionText.raycastTarget = false;
            SearchByCharaDropdown.captionText.overflowMode = TextOverflowModes.Overflow;
        }
        SearchByCharaDropdown.alphaFadeSpeed = VocalListFadeSpeed;

        SearchByCharaDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        SearchByCharaDropdown.onValueChanged.AddListener(OnSearchByCharaChanged);

        RebuildSearchByCharaOptions(true);
        RefreshSearchByCharaVisibility();
    }

    //Content del listado de PJ normales ("Characters/NormalCharacters/Character List").
    Transform GetCharacterListContent()
    {
        if (searchContentNormal != null) return searchContentNormal;
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var nc = FindDescendant(roots[i].transform, "NormalCharacters");
            if (nc == null) continue;
            var list = FindDescendant(nc, "Character List");
            if (list != null) return FindDescendant(list, "Content");
        }
        return null;
    }

    void CollectSearchByCharaItems()
    {
        searchByCharaItems.Clear();
        searchByCharaNames.Clear();
        searchByCharaIds.Clear();

        var content = GetCharacterListContent();
        if (content == null) return;

        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            if (child == null) continue;
            var container = child.GetComponent<UmaUIContainer>();
            string nombre = container != null ? container.Name : child.name;
            if (string.IsNullOrEmpty(nombre)) nombre = child.name;
            searchByCharaItems.Add(child);
            searchByCharaNames.Add(nombre);
            searchByCharaIds.Add(ParseLeadingId(nombre));
        }
    }

    string SearchByCharaSignature(Transform content)
    {
        if (content == null || content.childCount == 0) return "none|0";
        return content.childCount + "|" + content.GetChild(0).name + "|" + content.GetChild(content.childCount - 1).name;
    }

    void RefreshSearchByCharaVisibility()
    {
        if (SearchByCharaRow != null && !SearchByCharaRow.activeSelf) SearchByCharaRow.SetActive(true);
        if (SearchByCharaDropdown != null && !SearchByCharaDropdown.gameObject.activeSelf)
            SearchByCharaDropdown.gameObject.SetActive(true);
    }

    void UpdateSearchByChara()
    {
        if (SearchByCharaDropdown == null) return;
        RefreshSearchByCharaVisibility();

        if (searchByCharaNeedsRefresh && !SearchByCharaListVisible())
        {
            searchByCharaNeedsRefresh = false;
            searchByCharaFilter = "";
            RebuildSearchByCharaOptions(false);
            int pos = searchByCharaSelectedItemIndex >= 0
                ? searchByCharaVisible.IndexOf(searchByCharaSelectedItemIndex)
                : -1;
            if (pos >= 0)
            {
                SearchByCharaDropdown.SetValueWithoutNotify(pos);
                SearchByCharaDropdown.RefreshShownValue();
                RefreshSearchByCharaComboText();
            }
        }

        var content = GetCharacterListContent();
        string firma = SearchByCharaSignature(content);
        if (firma == searchByCharaSignature)
        {
            //Sin cambios de lista: basta con reafirmar el rótulo (el Dropdown lo repinta a options[0]
            //al activarse el panel, y queremos que sin PJ elegido siga vacío).
            RefreshSearchByCharaComboText();
            return;
        }
        if (searchByCharaActive)
        {
            RefreshSearchByCharaComboText();
            return;
        }
        searchByCharaSignature = firma;
        RebuildSearchByCharaOptions(true);
    }

    void RebuildSearchByCharaOptions(bool rebuildItems)
    {
        var dd = SearchByCharaDropdown;
        if (dd == null) return;

        if (rebuildItems) CollectSearchByCharaItems();

        string f = (searchByCharaFilter ?? "").Trim();
        searchByCharaVisible.Clear();
        dd.options.Clear();
        for (int i = 0; i < searchByCharaNames.Count; i++)
        {
            string nombre = searchByCharaNames[i] ?? "";
            if (f.Length > 0 && nombre.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
            searchByCharaVisible.Add(i);
            dd.options.Add(new TMP_Dropdown.OptionData(nombre));
        }

        if (dd.options.Count > 0) dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        RefreshSearchByCharaComboText();
    }

    //El rótulo enseña lo tecleado; si no hay filtro, el PJ elegido; si no hay PJ, vacío.
    void RefreshSearchByCharaComboText()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || dd.captionText == null) return;

        string texto = searchByCharaFilter ?? "";
        if (texto.Length == 0 && searchByCharaSelectedCharaId >= 0 && dd.value >= 0 && dd.value < dd.options.Count)
            texto = dd.options[dd.value].text;

        if (!string.Equals(dd.captionText.text, texto, StringComparison.Ordinal))
            dd.captionText.text = texto;
    }

    void OnSearchByCharaChanged(int value)
    {
        searchByCharaValueHandled = true;
        ApplySearchByCharaSelection(value);
    }

    void ApplySearchByCharaSelection(int value)
    {
        if (value < 0 || value >= searchByCharaVisible.Count) return;
        int itemIndex = searchByCharaVisible[value];
        if (itemIndex < 0 || itemIndex >= searchByCharaItems.Count) return;

        searchByCharaSelectedItemIndex = itemIndex;
        searchByCharaSelectedCharaId = searchByCharaIds[itemIndex];
        searchByCharaActive = false;
        searchByCharaNeedsRefresh = true;

        //Deja en "Sounds/LiveSound List" sólo las canciones de ese PJ; "SearchSounds" se recarga solo
        //al detectar el cambio de contenido.
        RebuildLiveSoundList(searchByCharaSelectedCharaId);
    }

    //¿Canta este PJ (charaId) en esta canción? Misma lógica que ListLiveSounds: los ids de cantante
    //salen del penúltimo token de los awb "snd_bgm_live_{songid}_chara_...".
    bool LiveSongHasSinger(int musicId, int charaId)
    {
        if (charaId < 0) return true;
        string nameVar = "snd_bgm_live_" + musicId + "_chara";
        string idStr = charaId.ToString();
        foreach (var a in Main.AbSounds)
        {
            if (a == null || !a.Name.Contains(nameVar) || !a.Name.EndsWith("awb")) continue;
            var split = Path.GetFileNameWithoutExtension(a.Name).Split('_');
            if (split.Length >= 2 && split[split.Length - 2] == idStr) return true;
        }
        return false;
    }

    //Rehace "Sounds/LiveSound List" con todas las canciones o sólo las del PJ elegido.
    void RebuildLiveSoundList(int charaId)
    {
        if (LiveSoundList == null || LiveSoundList.content == null) return;

        for (int i = LiveSoundList.content.childCount - 1; i >= 0; i--)
            Destroy(LiveSoundList.content.GetChild(i).gameObject);

        foreach (var live in Main.Lives.OrderBy(c => c.MusicId))
        {
            if (!LiveSongHasSinger(live.MusicId, charaId)) continue;
            var liveEntry = live;
            var container = Instantiate(UmaContainerLivePrefab, LiveSoundList.content).GetComponent<UmaUIContainer>();
            container.Name = " " + liveEntry.MusicId + " " + liveEntry.SongName;
            container.Image.sprite = liveEntry.Icon;
            container.Image.enabled = container.Image.sprite;
            container.Button.onClick.AddListener(() =>
            {
                HighlightChildImage(LiveSoundList.content, container);
                selectedLiveSound = liveEntry;
                ListLiveSounds(liveEntry.MusicId);
            });
        }
    }

    //===== Búsqueda por teclado del buscador por PJ =====
    bool SearchByCharaListVisible()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var parent = dd.template.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var t = parent.GetChild(i);
            if (t != null && t.name == "Dropdown List" && t.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    bool HayListaDeBusquedaByCharaBajoElPuntero()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || dd.template == null || dd.template.parent == null) return false;
        var cam = DropdownCanvasCamara(dd);
        var pos = Input.mousePosition;
        var hijos = dd.template.parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            var lista = hijos[i];
            if (lista == null || lista.name != "Dropdown List" || !lista.gameObject.activeInHierarchy) continue;
            var rt = lista as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam)) return true;
        }
        return false;
    }

    bool SearchByCharaDropdownBajoElPuntero()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return false;
        var rt = dd.transform as RectTransform;
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, DropdownCanvasCamara(dd));
    }

    void UpdateSearchByCharaSearch()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy)
        {
            searchByCharaListWasOpen = false;
            searchByCharaClickOnList = false;
            return;
        }

        bool abierta = SearchByCharaListVisible();
        if (searchByCharaListWasOpen && !abierta && searchByCharaClickOnList)
        {
            searchByCharaClickOnList = false;
            if (!searchByCharaValueHandled) ApplySearchByCharaSelection(dd.value);
        }
        searchByCharaListWasOpen = abierta;

        if (Input.GetMouseButtonDown(0))
        {
            if (!HayListaDeBusquedaByCharaBajoElPuntero())
            {
                if (SearchByCharaDropdownBajoElPuntero()) BeginSearchByCharaSearch();
                else EndSearchByCharaSearch();
            }
            else
            {
                searchByCharaClickOnList = true;
                searchByCharaValueHandled = false;
            }
            return;
        }

        if (!searchByCharaActive) return;

        if (!dd.gameObject.activeInHierarchy || !dd.IsInteractable())
        {
            EndSearchByCharaSearch();
            return;
        }

        string tecleado = Input.inputString;
        if (string.IsNullOrEmpty(tecleado)) return;

        var buffer = new StringBuilder(searchByCharaFilter ?? "");
        bool cambio = false;
        foreach (var c in tecleado)
        {
            if (c == '\b')
            {
                if (buffer.Length > 0) buffer.Length--;
                cambio = true;
            }
            else if (char.IsControl(c)) continue;
            else
            {
                buffer.Append(c);
                cambio = true;
            }
        }
        if (!cambio) return;

        searchByCharaFilter = buffer.ToString();
        RebuildSearchByCharaOptions(false);
        KeepSearchByCharaListOpen();
    }

    void BeginSearchByCharaSearch()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || !dd.gameObject.activeInHierarchy || !dd.IsInteractable()) return;

        searchByCharaActive = true;
        if (!string.IsNullOrEmpty(searchByCharaFilter))
        {
            searchByCharaFilter = "";
            RebuildSearchByCharaOptions(false);
        }
        searchByCharaListQuiere = true;
        searchByCharaListSucia = false;
        dd.Show();
    }

    void EndSearchByCharaSearch()
    {
        searchByCharaActive = false;
        var dd = SearchByCharaDropdown;
        if (dd != null && dd.IsActive()) dd.Hide();
        if (string.IsNullOrEmpty(searchByCharaFilter)) return;
        searchByCharaFilter = "";
        RebuildSearchByCharaOptions(false);
    }

    void KeepSearchByCharaListOpen()
    {
        var dd = SearchByCharaDropdown;
        if (dd == null || !dd.IsInteractable()) return;

        searchByCharaListQuiere = true;
        if (!dd.IsActive())
        {
            searchByCharaListSucia = false;
            dd.Show();
            return;
        }
        if (searchByCharaListEspera) { searchByCharaListSucia = true; return; }
        searchByCharaListEspera = true;
        StartCoroutine(RefreshSearchByCharaOpenList(dd));
    }

    IEnumerator RefreshSearchByCharaOpenList(TMP_Dropdown dd)
    {
        while (true)
        {
            dd.Hide();
            yield return new WaitForSecondsRealtime(VocalListFadeSpeed + 0.02f);
            searchByCharaListSucia = false;
            if (!searchByCharaListQuiere) break;
            if (SearchByCharaDropdown != dd || !dd.IsInteractable()) break;
            dd.Show();
            if (!searchByCharaListSucia) break;
        }
        searchByCharaListEspera = false;
        searchByCharaListSucia = false;
    }

}
