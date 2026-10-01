// Copied into Assets/Editor of an isolated project by tools/export_exclusive_models.py.
// Never execute this against the user's open Unity project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibMMD.Model;
using LibMMD.Reader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExclusiveModelBatch
{
    private sealed class Outfit
    {
        public int id;
        public string name;
        public string costume;
        public string error;
        public long bytes;
        public int textures;
    }

    private sealed class State
    {
        public List<Outfit> outfits = new List<Outfit>();
        public DateTime updatedUtc;
    }

    private static State state;
    private static string root;
    private static List<Outfit> queue;
    private static string statePath;
    private static int cursor;
    private static int attempted;
    private static int batchSize;
    private static int readyFrames;
    private static int cleanupFrames;
    private static int cleanupStartFrame;
    private static AsyncOperation cleanupOperation;
    private static bool loading;
    private static bool done;
    private static DateTime started;
    private static readonly Regex Body = new Regex(@"^3d/chara/body/bdy(?<id>\d+)_(?<costume>\d{2,3})/pfb_bdy\k<id>_\k<costume>$", RegexOptions.Compiled);

    public static void Run()
    {
        root = Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_OUTPUT");
        if (string.IsNullOrEmpty(root)) throw new Exception("UMA_EXCLUSIVE_OUTPUT not set");
        Directory.CreateDirectory(root);
        statePath = Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_REPORT") ?? throw new Exception("UMA_EXCLUSIVE_REPORT not set");
        batchSize = Math.Max(1, int.Parse(Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_BATCH") ?? "30"));
        started = DateTime.UtcNow;
        SessionState.SetBool("ExclusiveModelBatch.Active", true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/Version2.unity");
        EditorApplication.isPlaying = true;
        Debug.Log("[ExclusiveModelBatch] Entering isolated play mode");
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool("ExclusiveModelBatch.Active", false)) return;
        root = Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_OUTPUT");
        if (string.IsNullOrEmpty(root)) return;
        statePath = Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_REPORT") ?? throw new Exception("UMA_EXCLUSIVE_REPORT not set");
        batchSize = Math.Max(1, int.Parse(Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_BATCH") ?? "30"));
        started = DateTime.UtcNow;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (done || !EditorApplication.isPlaying || UmaViewerMain.Instance == null ||
            UmaViewerUI.Instance == null || UmaViewerBuilder.Instance == null ||
            UmaAssetManager.instance == null || UmaViewerMain.Instance.Characters.Count == 0 ||
            UmaViewerBuilder.Instance.ShaderList == null || UmaViewerBuilder.Instance.ShaderList.Count == 0 ||
            UmaViewerUI.Instance.ModelSettings == null) return;

        if (state == null)
        {
            if (++readyFrames < 15) return;
            Initialize();
            return;
        }
        if (loading) return;
        if (cleanupFrames > 0)
        {
            if (Time.frameCount < cleanupStartFrame + 2) return;
            cleanupFrames = 0;
        }
        if (cleanupFrames == 0 && cleanupOperation == null && attempted > 0)
        {
            // Destroy from UnloadUma is deferred; never unload the mesh's bundle in that frame.
            UmaAssetManager.UnloadAllBundle(true);
            cleanupOperation = Resources.UnloadUnusedAssets();
            cleanupFrames = -1;
            return;
        }
        if (cleanupOperation != null)
        {
            if (!cleanupOperation.isDone) return;
            cleanupOperation = null;
            GC.Collect();
        }
        if (attempted >= batchSize || cursor >= queue.Count)
        {
            Finish();
            return;
        }

        var outfit = queue[cursor++];
        var chara = UmaViewerMain.Instance.Characters.FirstOrDefault(c => !c.IsMob && c.Id == outfit.id);
        if (chara == null) { Fail(outfit, "Character absent from Main.Characters"); return; }
        try
        {
            if (Validate(outfit, false)) return;
            string path = $"3d/chara/body/bdy{outfit.id}_{outfit.costume}/pfb_bdy{outfit.id}_{outfit.costume}";
            if (!UmaViewerMain.Instance.AbList.TryGetValue(path, out var body) || !File.Exists(body.Path))
            {
                Fail(outfit, "Dedicated body asset absent on disk: " + path);
                return;
            }
            var list = new List<UmaDatabaseEntry>();
            var main = UmaViewerMain.Instance;
            string bodyPrefix = $"3d/chara/body/bdy{outfit.id}_{outfit.costume}/";
            string headPrefix = $"3d/chara/head/chr{outfit.id}_{outfit.costume}/";
            string baseHeadPrefix = $"3d/chara/head/chr{outfit.id}_00/";
            string tailPrefix = $"3d/chara/tail/tail{outfit.id}_{outfit.costume}/";
            list.AddRange(main.AbChara.Where(e => e.Name.StartsWith(bodyPrefix, StringComparison.Ordinal) ||
                e.Name.StartsWith(headPrefix, StringComparison.Ordinal) ||
                e.Name.StartsWith(baseHeadPrefix, StringComparison.Ordinal) ||
                e.Name.StartsWith(tailPrefix, StringComparison.Ordinal)));
            // The ordinary fallback tail may be shared with many characters; include its bundles too.
            int tailId = Convert.ToInt32(UmaDatabaseController.ReadCharaData(chara)["tail_model_id"]);
            if (tailId > 0)
            {
                string fallback = $"3d/chara/tail/tail{tailId:0000}_00/";
                list.AddRange(main.AbChara.Where(e => e.Name.StartsWith(fallback, StringComparison.Ordinal)));
            }
            list.Add(main.AbList["3d/animator/drivenkeylocator"]);
            attempted++;
            loading = true;
            UmaViewerUI.Instance.ModelSettings.SetHeadFix(false);
            UmaViewerUI.Instance.ModelSettings.SetTPose(true);
            UmaAssetManager.PreLoadAndRun(list, () =>
            {
                try
                {
                    // LoadUma is synchronous up to its yield break, as in the existing UI callsite.
                    var routine = UmaViewerBuilder.Instance.LoadUma(chara, outfit.costume, false);
                    while (routine.MoveNext()) { }
                    var container = UmaViewerBuilder.Instance.CurrentUMAContainer;
                    if (container == null || container.Body == null || container.Head == null ||
                        container.CharaEntry != chara || container.IsGeneric)
                        throw new Exception("Builder did not construct a complete exclusive model");
                    string dir = OutfitDirectory(outfit);
                    // Drop an interrupted prior attempt entirely: PNG exporter skips filenames already present.
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    ModelExporter.ExportModel(container, Path.Combine(dir, "model.pmx"));
                    if (!Validate(outfit, true)) throw new Exception("PMX readback or texture verification failed");
                    outfit.error = null;
                    Debug.Log($"[ExclusiveModelBatch] PASS {outfit.id}_{outfit.costume} pmxBytes={outfit.bytes} textures={outfit.textures}");
                }
                catch (Exception ex)
                {
                    Fail(outfit, ex.ToString());
                    try
                    {
                        string partial = OutfitDirectory(outfit);
                        if (Directory.Exists(partial)) Directory.Delete(partial, true);
                    }
                    catch (Exception cleanupError)
                    {
                        outfit.error += "\nFailed to remove partial output: " + cleanupError;
                    }
                }
                finally
                {
                    try
                    {
                        UmaViewerBuilder.Instance.UnloadUma();
                        UmaViewerBuilder.Instance.CurrentUMAContainer = null;
                    }
                    catch (Exception ex) { Debug.LogError("[ExclusiveModelBatch] Cleanup: " + ex); }
                    // Destroy is deferred until Unity advances its play-mode frame.
                    cleanupStartFrame = Time.frameCount;
                    cleanupFrames = 1;
                    cleanupOperation = null;
                    loading = false;
                    Save();
                }
            });
        }
        catch (Exception ex) { Fail(outfit, ex.ToString()); loading = false; }
    }

    private static void Initialize()
    {
        var main = UmaViewerMain.Instance;
        if (File.Exists(statePath)) state = JsonConvert.DeserializeObject<State>(File.ReadAllText(statePath));
        if (state == null) state = new State();
        var prior = state.outfits.ToDictionary(o => o.id + "_" + o.costume);
        var characters = main.Characters.Where(c => !c.IsMob).ToDictionary(c => c.Id);
        state.outfits = main.AbList.Keys.Select(key => Body.Match(key))
            .Where(match => match.Success)
            .Select(match => new { id = int.Parse(match.Groups["id"].Value), costume = match.Groups["costume"].Value })
            .Where(x => characters.ContainsKey(x.id))
            .GroupBy(x => x.id + "_" + x.costume).Select(group => group.First())
            .OrderBy(x => x.id).ThenBy(x => x.costume)
            .Select(x => prior.TryGetValue(x.id + "_" + x.costume, out var old)
                ? new Outfit { id = x.id, name = characters[x.id].Name, costume = x.costume, bytes = old.bytes, textures = old.textures, error = old.error }
                : new Outfit { id = x.id, name = characters[x.id].Name, costume = x.costume })
            .ToList();
        foreach (var outfit in state.outfits.Where(o => o.bytes > 0))
        {
            long expected = outfit.bytes;
            string pmx = Path.Combine(OutfitDirectory(outfit), "model.pmx");
            if (!Validate(outfit, false) || outfit.bytes != expected || new FileInfo(pmx).Length != expected)
            {
                outfit.bytes = 0;
                outfit.error = null;
            }
        }
        string requested = Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_RETRY_IDS") ?? "";
        var retryIds = new HashSet<string>(requested.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        queue = state.outfits.Where(o => o.bytes == 0 &&
            (o.error == null || retryIds.Contains(o.id + "_" + o.costume))).ToList();
        string only = Environment.GetEnvironmentVariable("UMA_EXCLUSIVE_ONLY");
        if (!string.IsNullOrEmpty(only))
        {
            var targetIds = new HashSet<string>(only.Split(','));
            queue = queue.Where(o => targetIds.Contains(o.id + "_" + o.costume)).ToList();
        }
        // Missing local bundles are reportable candidates, never silently omitted from the asset/master intersection.
        Debug.Log($"[ExclusiveModelBatch] DISCOVER characters={main.Characters.Count} exclusiveOutfits={state.outfits.Count} pending={queue.Count} batch={batchSize}");
        Save();
    }

    private static string OutfitDirectory(Outfit outfit) => Path.Combine(root, "models", outfit.id.ToString(), outfit.costume);

    private static bool Validate(Outfit outfit, bool strict)
    {
        string dir = OutfitDirectory(outfit);
        string path = Path.Combine(dir, "model.pmx");
        if (!File.Exists(path)) return false;
        try
        {
            var pmx = new PMXReader().Read(path, new ModelConfig { GlobalToonPath = "Toon" });
            if (pmx.Vertices == null || pmx.Vertices.Length == 0 || pmx.Parts == null || pmx.Parts.Length == 0 ||
                pmx.Bones == null || pmx.Bones.Length == 0 || pmx.TextureList.Count == 0)
                throw new Exception("PMX has empty geometry, bones, or texture table");
            if (pmx.TriangleIndexes == null || pmx.TriangleIndexes.Length == 0 ||
                pmx.Parts.Sum(part => part.TriangleIndexNum) != pmx.TriangleIndexes.Length)
                throw new Exception("PMX material groups do not cover all triangle indices");
            foreach (int index in pmx.TriangleIndexes)
                if (index < 0 || index >= pmx.Vertices.Length)
                    throw new Exception("PMX triangle references an invalid vertex " + index);
            foreach (var part in pmx.Parts)
                if (part.TriangleIndexNum < 0 || part.TriangleIndexNum % 3 != 0 ||
                    part.Material == null || part.Material.Texture == null ||
                    !pmx.TextureList.Contains(part.Material.Texture))
                    throw new Exception("PMX has invalid material coverage or texture reference");
            foreach (var vertex in pmx.Vertices)
            {
                if (!Finite(vertex.Coordinate.x) || !Finite(vertex.Coordinate.y) || !Finite(vertex.Coordinate.z) ||
                    !Finite(vertex.Normal.x) || !Finite(vertex.Normal.y) || !Finite(vertex.Normal.z) ||
                    !Finite(vertex.UvCoordinate.x) || !Finite(vertex.UvCoordinate.y))
                    throw new Exception("PMX contains non-finite vertex geometry");
                ValidateSkinning(vertex.SkinningOperator, pmx.Bones.Length);
            }
            foreach (var texture in pmx.TextureList)
            {
                string relative = texture.TexturePath.Replace('\\', '/');
                if (!relative.StartsWith("Texture2D/", StringComparison.Ordinal) || relative.Contains("..") ||
                    !relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Invalid texture path " + relative);
                string texturePath = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
                using (var file = File.OpenRead(texturePath))
                {
                    var signature = new byte[8];
                    if (file.Length < 32 || file.Read(signature, 0, 8) != 8 ||
                        !signature.SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10}))
                        throw new Exception("Missing/corrupt PNG " + relative);
                }
            }
            outfit.bytes = new FileInfo(path).Length;
            outfit.textures = pmx.TextureList.Count;
            outfit.error = null;
            return true;
        }
        catch (Exception ex)
        {
            if (strict) throw;
            outfit.error = "Existing output invalid; re-export: " + ex.Message;
            return false;
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void ValidateInfluence(int bone, float weight, int boneCount)
    {
        if (!Finite(weight) || weight < 0 || weight > 1 ||
            (weight > 0 && (bone < 0 || bone >= boneCount)))
            throw new Exception($"PMX has invalid bone influence bone={bone} weight={weight}");
    }

    private static void ValidateSkinning(SkinningOperator skin, int boneCount)
    {
        if (skin == null) throw new Exception("PMX vertex lacks bone skinning");
        switch (skin.Type)
        {
            case SkinningOperator.SkinningType.SkinningBdef1:
                ValidateInfluence(((SkinningOperator.Bdef1)skin.Param).BoneId, 1, boneCount);
                break;
            case SkinningOperator.SkinningType.SkinningBdef2:
                var two = (SkinningOperator.Bdef2)skin.Param;
                ValidateInfluence(two.BoneId[0], two.BoneWeight, boneCount);
                ValidateInfluence(two.BoneId[1], 1 - two.BoneWeight, boneCount);
                break;
            case SkinningOperator.SkinningType.SkinningBdef4:
                var four = (SkinningOperator.Bdef4)skin.Param;
                float total = 0;
                for (int i = 0; i < 4; i++)
                {
                    ValidateInfluence(four.BoneId[i], four.BoneWeight[i], boneCount);
                    total += four.BoneWeight[i];
                }
                if (Math.Abs(total - 1) > 0.001f)
                    throw new Exception("PMX bone weights do not sum to one: " + total);
                break;
            case SkinningOperator.SkinningType.SkinningSdef:
                var sdef = (SkinningOperator.Sdef)skin.Param;
                ValidateInfluence(sdef.BoneId[0], sdef.BoneWeight, boneCount);
                ValidateInfluence(sdef.BoneId[1], 1 - sdef.BoneWeight, boneCount);
                break;
            default:
                throw new Exception("PMX vertex has unsupported skinning type " + skin.Type);
        }
    }

    private static void Fail(Outfit outfit, string error)
    {
        outfit.bytes = 0;
        outfit.textures = 0;
        outfit.error = error;
        Debug.LogError($"[ExclusiveModelBatch] FAIL {outfit.id}_{outfit.costume}: {error}");
        Save();
    }

    private static void Save()
    {
        state.updatedUtc = DateTime.UtcNow;
        string temp = statePath + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(state, Formatting.Indented));
        if (File.Exists(statePath)) File.Replace(temp, statePath, null);
        else File.Move(temp, statePath);
    }

    private static void Finish()
    {
        done = true;
        Save();
        int valid = state.outfits.Count(o => o.bytes > 0 && o.error == null);
        int failed = state.outfits.Count(o => o.error != null);
        Debug.Log($"[ExclusiveModelBatch] DONE total={state.outfits.Count} valid={valid} failures={failed} attempted={attempted} duration={DateTime.UtcNow - started}");
        SessionState.SetBool("ExclusiveModelBatch.Active", false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(0);
    }
}
