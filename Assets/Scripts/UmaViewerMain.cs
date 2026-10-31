using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Rendering;
using Newtonsoft.Json.Linq;

public class UmaViewerMain : MonoBehaviour
{
    private const string EnglishNamesCacheFileName = "english_names.cache";
    private const string ChineseNamesCacheFileName = "chinese_names.cache";
    private const string EnglishNamesCacheMagic = "UmaViewerEnglishNames";
    private const int EnglishNamesCacheVersion = 1;

    public static UmaViewerMain Instance;
    public static bool WasEscapeConsumedThisFrame { get; private set; }
    private UmaViewerUI UI => UmaViewerUI.Instance;
    private UmaViewerBuilder Builder => UmaViewerBuilder.Instance;

    public RenderPipelineAsset DefaultRenderPipeline;
    public List<CharaEntry> Characters = new List<CharaEntry>();
    public List<CharaEntry> MobCharacters = new List<CharaEntry>();
    public List<LiveEntry> Lives = new List<LiveEntry>();
    public List<CostumeEntry> Costumes = new List<CostumeEntry>();
    // (Color) Colores de cada traje COMUN, por "<body_type>_<body_type_sub>" (p.ej. "0015_00").
    // Es la lista BASE: se construye UNA vez al arrancar (paso "Loading Costumes Data") a partir de
    // dress_data.color_num y de los iconos dress_<dressId>_0_<color>, y ya no cambia. El listado de
    // trajes la consulta en vez de resolver el icono cada vez: así la variante de color está siempre,
    // sin depender de si el traje tiene fila asignada, del estado de la UI ni de volver a listar.
    public class CostumeColor
    {
        public int Index;           // 0 = el de siempre
        public Sprite Icon;         // icono de ese color (dress_<id>_0_<color>)
        public string DressName;    // nombre del traje (no hay nombres por color en el master)
    }

    public Dictionary<string, List<CostumeColor>> CostumeColorsByBody = new Dictionary<string, List<CostumeColor>>();

    public Dictionary<string,UmaDatabaseEntry> AbList = new Dictionary<string, UmaDatabaseEntry>();
    public List<UmaDatabaseEntry> AbMotions = new List<UmaDatabaseEntry>();
    public List<UmaDatabaseEntry> AbSounds = new List<UmaDatabaseEntry>();
    public List<UmaDatabaseEntry> AbChara = new List<UmaDatabaseEntry>();
    public List<UmaDatabaseEntry> AbEffect = new List<UmaDatabaseEntry>();
    public List<UmaDatabaseEntry> CostumeList = new List<UmaDatabaseEntry>();

    /// <summary>
    /// Set when Standalone mode has no database at all. Awake must not touch
    /// UmaDatabaseController.Instance in that case: the constructor throws and only shows
    /// "Only `Default` mode is supported", long before the rebuild in Start() would run.
    /// </summary>
    private bool _needsDatabaseRebuild;

    private void Awake()
    {
        Instance = this;
        new Config();
        ApplyFrameRateLimit();

        if (Config.Instance != null && Config.Instance.WorkMode == WorkMode.Standalone)
        {
            string nativeMeta = Path.Combine(Config.Instance.MainPath, "meta");
            string rebuiltMeta = Path.Combine(Config.Instance.MainPath, "meta_umaviewer");

            if (!File.Exists(nativeMeta) && !File.Exists(rebuiltMeta))
            {
                _needsDatabaseRebuild = true;
                StandaloneLogger.Info(
                    "Awake: no database yet, deferring asset list build until the rebuild finishes");
                return;
            }
        }

        LoadDatabaseLists();
    }

    /// <summary>Fills AbList/AbChara/... from the meta database. Safe to call after a rebuild.</summary>
    private void LoadDatabaseLists()
    {
        AbList = UmaDatabaseController.Instance.MetaEntries;
        if (AbList == null) return;
        var chara_3d = AbList.Where(ab => ab.Value.Type == UmaFileType._3d_cutt).Select(ab => ab.Value).ToList();
        AbChara = chara_3d.FindAll(ab => ab.Name.StartsWith(UmaDatabaseController.CharaPath));
        AbMotions = chara_3d.FindAll(ab => ab.Name.StartsWith(UmaDatabaseController.MotionPath));
        AbEffect = chara_3d.FindAll(ab => ab.Name.StartsWith(UmaDatabaseController.EffectPath));
        AbSounds = AbList.Where(ab => ab.Value.Type == UmaFileType.sound).Select(ab => ab.Value).ToList();
        var outgame = AbList.Where(ab => ab.Value.Type == UmaFileType.outgame).Select(ab => ab.Value).ToList();
        CostumeList = outgame.FindAll(e => e.Name.StartsWith(UmaDatabaseController.CostumePath));
    }

    // Version2 (UI): aplica el frame rate PROPIO del viewer.
    public static void ApplyFrameRateLimit()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Config.Instance.GetUIFrameRate();
    }

    // Live: aplica el frame rate PROPIO del live.
    public static void ApplyFrameRateLimitLive()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Config.Instance.GetTargetFrameRate();
    }

    private void Update()
    {
        WasEscapeConsumedThisFrame = false;
        TryConsumeEscapeForFullScreen();
    }

    public static bool TryConsumeEscapeForFullScreen()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || !Screen.fullScreen)
            return false;

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
        WasEscapeConsumedThisFrame = true;
        return true;
    }

    /// <summary>
    /// Builds meta_umaviewer from the CDN manifests and then fills in the per-entry keys, so a
    /// fresh Standalone install can start without the user touching `Other` settings first.
    /// </summary>
    private IEnumerator RebuildStandaloneDatabase()
    {
        StandaloneLogger.Begin("Standalone auto-rebuild");
        StandaloneLogger.Info(
            "No meta/meta_umaviewer under " + Config.Instance.MainPath +
            "; rebuilding from CDN manifests");

        if (UmaSceneController.instance != null)
        {
            UmaSceneController.instance.LoadingProgressChange(0, 10, "Building Database");
        }

        ManifestDB db = new ManifestDB();

        yield return db.UpdateResourceVersion(delegate (string msg, UIMessageType type)
        {
            if (UmaViewerUI.Instance != null)
            {
                UmaViewerUI.Instance.ShowMessage(msg, type);
            }
        });

        // UpdateResourceVersion already syncs the keys as part of the same pass.
        StandaloneLogger.Info(
            "Rebuild finished; meta_umaviewer now carries the decryption keys");

        StandaloneLogger.End("Standalone auto-rebuild");
    }

    private IEnumerator Start()
    {
        // Swap the scene's English labels for the Chinese table when that language is selected.
        UiLocalization.Apply(Config.Instance != null ? Config.Instance.Language : Language.En);

        // Awake deferred the asset-list build because there was no database at all; build it
        // now, fill in the keys, then re-read the lists before anything else touches them.
        if (_needsDatabaseRebuild)
        {
            yield return RebuildStandaloneDatabase();

            _needsDatabaseRebuild = false;

            if (UmaDatabaseController.Instance != null)
            {
                UmaDatabaseController.Instance.CloseAllConnection();

                // The controller memoised its failure path; reload from the fresh database.
                UmaDatabaseController.ResetInstance();
            }

            LoadDatabaseLists();
        }

        if (AbList == null) yield break;

        // Rebuilt databases come from the CDN manifests, which carry no per-entry key. If the
        // one we are about to use has no `e` column (or a completely empty one), pull the key
        // list from the API and fill it in before anything tries to decrypt a bundle.
        // Use the path that is actually going to be loaded: native meta already ships keys,
        // only a CDN-rebuilt meta_umaviewer needs the sync.
        string keySyncPath = UmaDatabaseController.Instance != null
            ? UmaDatabaseController.Instance.MetaDatabasePath
            : ManifestDB.DBPath;

        if (UmaKeySync.NeedsSync(keySyncPath))
        {
            if (UmaSceneController.instance != null)
            {
                UmaSceneController.instance.LoadingProgressChange(0, 10, "Syncing Keys");
            }

            yield return UmaKeySync.Ensure(keySyncPath);

            // The meta was already read into memory before the keys were written, so every
            // entry still carries Key = 0 and decryption silently falls back to "plaintext".
            // Drop the cached controller and re-read so the freshly written e column is used.
            if (UmaDatabaseController.Instance != null)
            {
                UmaDatabaseController.Instance.CloseAllConnection();
                UmaDatabaseController.ResetInstance();
            }

            LoadDatabaseLists();

            StandaloneLogger.Info(
                "Reloaded meta after key sync; entries = " + (AbList == null ? 0 : AbList.Count));
        }

        // Traduce nombres JP->EN con la instalacion GLOBAL (solo nombres; si no esta,
        // no pasa nada y se conservan los japoneses).
        GlobalNames.EnsureLoaded(Config.Instance.GlobalNamesPath);

        int loadingStep = 0;
        int loadingStepsTotal = 10;
        var UmaCharaData = UmaDatabaseController.Instance.CharaData;
        var MobCharaData = UmaDatabaseController.Instance.MobCharaData;
        var loadingUI = UmaSceneController.instance;


        //修改(载入通用服装ColorSet相关)
        var charaDressColor = UmaDatabaseController.Instance.CharaDressColor;



        // Reuse the downloaded names; only fetch when no usable cache exists.
        bool isChinese = Config.Instance.Language == Language.Cn;
        bool useNames = Config.Instance.Language == Language.En || isChinese;

        string cachePath = Path.Combine(
            Application.persistentDataPath,
            isChinese ? ChineseNamesCacheFileName : EnglishNamesCacheFileName);

        bool downloadTranslations = useNames && !TryLoadEnglishNamesCache(cachePath);

        loadingUI.LoadingProgressChange(
            loadingStep++,
            loadingStepsTotal,
            downloadTranslations ? "Downloading Translations" : "Loading Translations");

        if (downloadTranslations)
        {
            if (isChinese)
            {
                // A zh-CN.json dropped in {exe}/translations wins over the network copy so the
                // viewer keeps working offline (same hachimi text_data_dict.json layout).
                string localTranslation = LocalizeEn.FindLocalTranslationFile();

                // Bundled table first: on Android there is no translations/ folder next to the
                // player and downloading 5.8 MB with 3-second timeouts kept failing.
                TextAsset bundledNames = Resources.Load<TextAsset>("Localize/zh-CN-names");

                if (bundledNames != null &&
                    TryParseEnglishNames(bundledNames.text, out Dictionary<int, string> bundledChara, out Dictionary<int, string> bundledMob))
                {
                    ApplyEnglishNames(bundledChara, bundledMob);
                    TrySaveEnglishNamesCache(cachePath, bundledChara, bundledMob);
                    Debug.Log("[Localize] Chinese names loaded from Resources/Localize/zh-CN-names.json");
                }
                else if (!string.IsNullOrEmpty(localTranslation))
                {
                    try
                    {
                        string localJson = File.ReadAllText(localTranslation);

                        if (TryParseEnglishNames(localJson, out Dictionary<int, string> localChara, out Dictionary<int, string> localMob))
                        {
                            ApplyEnglishNames(localChara, localMob);
                            TrySaveEnglishNamesCache(cachePath, localChara, localMob);
                            Debug.Log($"[Localize] Chinese names loaded from {localTranslation}");
                        }
                        else
                        {
                            Debug.LogWarning($"[Localize] {localTranslation} does not contain the required name tables.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Localize] Could not read {localTranslation}: {ex.Message}");
                    }
                }
                else if (LocalizeEn.TranslationUrlCnMirrors != null && LocalizeEn.TranslationUrlCnMirrors.Length > 0)
                {
                    // Try each mirror until one yields the name tables (the hosts are copies of
                    // the same hachimi payload; some are blocked from certain networks).
                    foreach (string mirror in LocalizeEn.TranslationUrlCnMirrors)
                    {
                        bool applied = false;

                        yield return UmaViewerDownload.DownloadText(mirror, json =>
                        {
                            if (string.IsNullOrEmpty(json)) return;

                            try
                            {
                                if (!TryParseEnglishNames(json, out Dictionary<int, string> charaNames, out Dictionary<int, string> mobNames))
                                {
                                    Debug.LogWarning("[Localize] " + mirror + " did not contain the required name tables.");
                                    return;
                                }

                                ApplyEnglishNames(charaNames, mobNames);
                                TrySaveEnglishNamesCache(cachePath, charaNames, mobNames);
                                applied = true;
                                Debug.Log("[Localize] Chinese names loaded from " + mirror);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[Localize] Could not parse {mirror}: {ex.Message}");
                            }
                        });

                        if (applied) break;
                    }
                }
                else
                {
                    Debug.LogWarning("[Localize] Chinese selected but no translation file or URL is available.");
                }
            }
            else
            {
                yield return UmaViewerDownload.DownloadText(LocalizeEn.TranslationUrl, json =>
                {
                    if (string.IsNullOrEmpty(json)) return;

                    try
                    {
                        if (!TryParseEnglishNames(json, out Dictionary<int, string> charaNames, out Dictionary<int, string> mobNames))
                        {
                            Debug.LogWarning("Online English translations did not contain the required name tables. Using LocalizeEn fallback.");
                            return;
                        }

                        ApplyEnglishNames(charaNames, mobNames);
                        TrySaveEnglishNamesCache(cachePath, charaNames, mobNames);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Could not parse online English translations. Using LocalizeEn fallback: {ex.Message}");
                    }
                });
            }
        }

        if (Config.Instance.WorkMode == WorkMode.Standalone)
        {
            var umaIcons = UmaCharaData.Select(item => AbList.TryGetValue($"chara/chr{item["id"]}/chr_icon_{item["id"]}", out UmaDatabaseEntry entry) ? entry : null).Where(entry => entry != null);
            var mobIcons = MobCharaData.Select(item => AbList.TryGetValue($"mob/mob_chr_icon_{item["mob_id"]}_000001_01", out UmaDatabaseEntry entry) ? entry : null).Where(entry => entry != null);
            var costumeIcons = CostumeList;
            List<UmaDatabaseEntry> filesToDownload = umaIcons.Concat(mobIcons).Concat(costumeIcons).ToList();
            yield return UmaViewerDownload.DownloadAssets(filesToDownload, UmaSceneController.instance.LoadingProgressChange);
            filesToDownload.Clear();
        }

        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading Character Data");
        yield return null;
        foreach (var item in UmaCharaData)
        {
            var id = Convert.ToInt32(item["id"]);
            if (!Characters.Where(c => c.Id == id).Any())
            {
                Characters.Add(new CharaEntry()
                {
                    Name = item["charaname"].ToString(),
                    EnName = GlobalNames.Get(GlobalNames.CategoryChara, id, LocalizeEn.GetCharaName(id)),
                    Icon = UmaViewerBuilder.Instance.LoadCharaIcon(id.ToString()),
                    Id = id,
                    ThemeColor = "#" + item["ui_nameplate_color_1"].ToString()
                });
            }
        }

        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading Mob Data");
        yield return null;
        foreach (var item in MobCharaData)
        {
            if (Convert.ToInt32(item["use_live"]) == 0) 
            {
                continue;
            }

            var id = Convert.ToInt32(item["mob_id"]);
            MobCharacters.Add(new CharaEntry()
            {
                Name = item["charaname"].ToString(),
                EnName = GlobalNames.Get(GlobalNames.CategoryMob, id, LocalizeEn.GetMobName(id)),
                Icon = UmaViewerBuilder.Instance.LoadMobCharaIcon(id.ToString()),
                Id = id,
                IsMob = true
            });
        }

        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading Costumes Data");
        yield return null;
        foreach (var item in CostumeList)
        {
            var costume = new CostumeEntry();
            var name = Path.GetFileName(item.Name);
            costume.Id = name.Replace("dress_","");
            costume.Icon = Builder.LoadSprite(item);
            Costumes.Add(costume);
        }

        var DressData = UmaDatabaseController.Instance.DressData;
        foreach (var data in DressData)
        {
            var costume = Costumes.FirstOrDefault(a => a.Id.Split('_')[0].Contains(data["id"].ToString()) );
            if (costume != null)
            {
                costume.CharaId = Convert.ToInt32(data["chara_id"]);
                costume.DressName = data["dressname"].ToString();
                // (AÑADIDO) Traduccion de nombres de traje con el global (solo si hay idioma EN).
                if (Config.Instance.Language == Language.En &&
                    int.TryParse(data["id"].ToString(), out int dressTextId))
                {
                    costume.DressName = GlobalNames.Get(GlobalNames.CategoryDress, dressTextId, costume.DressName);
                }
                costume.BodyType = Convert.ToInt32(data["body_type"]);
                costume.BodyTypeSub = Convert.ToInt32(data["body_type_sub"]);
            }
        }

        // (Color) Lista BASE de colores de los trajes comunes. Se calcula aquí, con el master y los
        // iconos ya cargados, y es lo que consulta el panel de trajes: así el color extra (p.ej. la
        // camisa negra del 3.5周年) está siempre, en cualquier listado y en cualquier orden.
        BuildCostumeColors();

        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading Live Data");
        yield return null;
        var asset = AbList["livesettings"];
        if (asset != null)
        {
            string filePath = asset.FilePath;
            if (File.Exists(filePath))
            {
                AssetBundle bundle = UmaAssetManager.LoadAssetBundle(asset, true);
                foreach (var item in UmaDatabaseController.Instance.LiveData)
                {
                    var musicId = Convert.ToInt32(item["music_id"]);
                    var songName = item["songname"].ToString();
                    // (AÑADIDO) Traduccion de nombres de cancion con el global (solo si idioma EN).
                    if (Config.Instance.Language == Language.En)
                    {
                        songName = GlobalNames.Get(GlobalNames.CategorySong, musicId, songName);
                    }
                    var membercount = Convert.ToInt32(item["live_member_number"]);
                    var defaultdress = Convert.ToInt32(item["default_main_dress"]);

                    if (!Lives.Where(c => c.MusicId == musicId).Any())
                    {
                        if (bundle.Contains(musicId.ToString()))
                        {
                            TextAsset liveData = bundle.LoadAsset<TextAsset>(musicId.ToString());

                            Lives.Add(new LiveEntry(liveData.text)
                            {
                                MusicId = musicId,
                                SongName = songName,
                                MemberCount = membercount,
                                DefaultDress = defaultdress,
                                Icon = UmaViewerBuilder.Instance.LoadLiveIcon(musicId)
                            }); 
                        }
                    }
                }
            }
        }

        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading UI");
        yield return null;
        UI.LoadModelPanels();
        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading UI");
        yield return null;
        UI.LoadMiniModelPanels();
        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading UI");
        yield return null;
        UI.LoadPropPanel();
        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading UI");
        yield return null;
        UI.LoadMapPanel();
        loadingUI.LoadingProgressChange(loadingStep++, loadingStepsTotal, "Loading UI");
        yield return null;
        UI.LoadLivePanels();
        loadingUI.LoadingProgressChange(-1, -1);

        //Load Shader First
        var shaders = UmaAssetManager.LoadAssetBundle(AbList["shader"], true);
        Builder.ShaderList = new List<Shader>(shaders.LoadAllAssets<Shader>()); 
        Gallop.ShaderManager.InitManager();
        Gallop.ShaderManager.WarmupDofBloomShader();
    }

    //==========================================================================
    // (Color) Colores de los trajes COMUNES: lista base que se construye al arrancar.
    // Fuente de verdad: los iconos que el juego trae por color (outgame/dress/dress_<dressId>_0_<color>)
    // más la fila de dress_data que identifica la malla. El color no forma parte del id del traje
    // ("0015_00_00"), así que se guarda por "<body_type>_<body_type_sub>" ("0015_00"), que es lo que
    // identifica el cuerpo y lo que usan los trajes comunes al cambiar de color.
    //==========================================================================
    void BuildCostumeColors()
    {
        CostumeColorsByBody.Clear();

        var dressRows = UmaDatabaseController.Instance.DressData;
        if (dressRows == null || Costumes == null) return;

        // Iconos ya cargados (uno por traje de datos): "000050_0_00", "000050_0_01"...
        var iconsById = new Dictionary<string, CostumeEntry>(StringComparer.Ordinal);
        foreach (var costume in Costumes)
        {
            if (costume == null || string.IsNullOrEmpty(costume.Id)) continue;
            if (!iconsById.ContainsKey(costume.Id)) iconsById[costume.Id] = costume;
        }

        foreach (var row in dressRows)
        {
            int bodyType, bodySub;
            if (!int.TryParse(row["body_type"].ToString(), out bodyType)) continue;
            if (!int.TryParse(row["body_type_sub"].ToString(), out bodySub)) continue;

            // Solo hay un traje por (body_type, body_type_sub): se queda el primero, igual que el
            // resto del programa al buscar su fila.
            string key = bodyType.ToString("D4") + "_" + bodySub.ToString("D2");
            if (CostumeColorsByBody.ContainsKey(key)) continue;

            // El master no guarda nombres por color: todos los colores se llaman como el traje.
            string dressName = row["dressname"].ToString();
            int dressTextId;
            if (Config.Instance.Language == Language.En && int.TryParse(row["id"].ToString(), out dressTextId))
            {
                dressName = GlobalNames.Get(GlobalNames.CategoryDress, dressTextId, dressName);
            }

            // Los colores que existen SON los iconos que trae el juego para ese traje, en orden
            // (dress_<dressId>_0_00, _0_01...). dress_data.color_num cuadra con ellos, pero lo que
            // manda aquí es que el icono exista: sin icono no hay item que enseñar.
            string prefix = row["id"].ToString().PadLeft(6, '0') + "_0_";
            var colors = new List<CostumeColor>();
            for (int color = 0; color < 16; color++)
            {
                CostumeEntry icon;
                if (!iconsById.TryGetValue(prefix + color.ToString("D2"), out icon) || icon.Icon == null) break;
                colors.Add(new CostumeColor { Index = color, Icon = icon.Icon, DressName = dressName });
            }

            if (colors.Count <= 1) continue;   // sin colores extra no hace falta tabla
            CostumeColorsByBody[key] = colors;
        }
    }

    /// <summary>
    /// (Color) Colores de un traje COMUN a partir de su id de listado ("0015_00_00"). Devuelve la
    /// lista base con el color 0 (el de siempre) incluido, o null si ese traje no tiene más colores.
    /// </summary>
    public List<CostumeColor> GetCostumeColors(string costumeId)
    {
        if (string.IsNullOrEmpty(costumeId)) return null;
        string[] parts = costumeId.Split('_');
        if (parts.Length < 2) return null;

        List<CostumeColor> colors;
        return CostumeColorsByBody.TryGetValue(parts[0] + "_" + parts[1], out colors) ? colors : null;
    }

    private static bool TryParseEnglishNames(
        string json,
        out Dictionary<int, string> charaNames,
        out Dictionary<int, string> mobNames)
    {
        JObject translations = JObject.Parse(json);
        charaNames = translations[LocalizeEn.CharaTranslationTableKey]
            ?.ToObject<Dictionary<int, string>>();
        mobNames = translations[LocalizeEn.MobTranslationTableKey]
            ?.ToObject<Dictionary<int, string>>();

        return charaNames != null && charaNames.Count > 0 &&
               mobNames != null && mobNames.Count > 0;
    }

    private static void ApplyEnglishNames(
        Dictionary<int, string> charaNames,
        Dictionary<int, string> mobNames)
    {
        foreach (KeyValuePair<int, string> pair in charaNames)
        {
            if (!string.IsNullOrEmpty(pair.Value))
                LocalizeEn.SetCharaName(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<int, string> pair in mobNames)
        {
            if (!string.IsNullOrEmpty(pair.Value))
                LocalizeEn.SetMobName(pair.Key, pair.Value);
        }
    }

    private static bool TryLoadEnglishNamesCache(string cachePath)
    {
        if (!File.Exists(cachePath)) return false;

        try
        {
            using (var stream = File.OpenRead(cachePath))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadString() != EnglishNamesCacheMagic ||
                    reader.ReadInt32() != EnglishNamesCacheVersion)
                {
                    throw new InvalidDataException("Unsupported English name cache format.");
                }

                Dictionary<int, string> charaNames = ReadEnglishNameTable(reader);
                Dictionary<int, string> mobNames = ReadEnglishNameTable(reader);
                if (charaNames.Count == 0 || mobNames.Count == 0)
                    throw new InvalidDataException("English name cache is missing required name tables.");
                ApplyEnglishNames(charaNames, mobNames);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not load the cached English names. Using LocalizeEn fallback: {ex.Message}");
            return false;
        }
    }

    private static void TrySaveEnglishNamesCache(
        string cachePath,
        Dictionary<int, string> charaNames,
        Dictionary<int, string> mobNames)
    {
        try
        {
            using (var stream = File.Create(cachePath))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(EnglishNamesCacheMagic);
                writer.Write(EnglishNamesCacheVersion);
                WriteEnglishNameTable(writer, charaNames);
                WriteEnglishNameTable(writer, mobNames);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not save the updated English name cache: {ex.Message}");
        }
    }

    private static Dictionary<int, string> ReadEnglishNameTable(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > 100000)
            throw new InvalidDataException("Invalid English name cache entry count.");

        var names = new Dictionary<int, string>(count);
        for (int i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            string value = reader.ReadString();
            if (!string.IsNullOrEmpty(value))
                names[id] = value;
        }

        return names;
    }

    private static void WriteEnglishNameTable(
        BinaryWriter writer,
        Dictionary<int, string> names)
    {
        List<KeyValuePair<int, string>> validNames = names
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .ToList();
        writer.Write(validNames.Count);
        foreach (KeyValuePair<int, string> pair in validNames)
        {
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }
    }

    public void OpenUrl(string url)
    {
        Application.OpenURL(url);
    }

}
