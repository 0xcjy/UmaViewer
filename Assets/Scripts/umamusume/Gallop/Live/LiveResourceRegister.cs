using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace Gallop.Live
{
    /// <summary>
    /// Builds the Live download roots before UmaAssetManager expands bundle dependencies.
    /// The target registers Director roots first, then the body/head/tail folders and
    /// shared face assets for each selected character (RegisterDownload RVA 0x1ab98b0).
    /// This is not an AssetBundle.GetAllAssetNames/LoadAsset count.
    /// </summary>
    public static class LiveResourceRegister
    {
        public static List<UmaDatabaseEntry> RegisterDownload(
            LiveEntry live, IList<LiveCharacterLoadData> characters, bool requireStage)
        {
            var roots = new List<UmaDatabaseEntry>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return roots;

            void Add(UmaDatabaseEntry entry)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Name) && names.Add(entry.Name))
                    roots.Add(entry);
            }

            void TryRegister(string path)
            {
                if (!string.IsNullOrEmpty(path) && main.AbList.TryGetValue(path, out var entry))
                    Add(entry);
            }

            // RegisterFolder: AbList.Values.Where(entry.Name.StartsWith(prefix,
            // OrdinalIgnoreCase)) -> RegisterEntries. No arbitrary global chara scan.
            void RegisterFolder(string prefix)
            {
                if (string.IsNullOrEmpty(prefix)) return;
                foreach (var entry in main.AbList.Values)
                    if (entry != null && entry.Name != null &&
                        entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        Add(entry);
            }

            void RegisterFaceRuntime()
            {
                TryRegister("3d/animator/drivenkeylocator");
                TryRegister("3d/chara/common/textures/tex_chr_tear00");
                for (int i = 0; i < 4; i++)
                    TryRegister("3d/effect/charaemotion/pfb_eff_chr_emo_eye_" +
                                i.ToString("000", CultureInfo.InvariantCulture));
                TryRegister("3d/chara/common/tear/tear000/pfb_chr_tear000");
                TryRegister("3d/chara/common/tear/tear001/pfb_chr_tear001");
            }

            foreach (var entry in Director.GetLivePreloadEntries(
                         live, characters == null ? null : new List<LiveCharacterLoadData>(characters), requireStage))
                Add(entry);
            int directorRoots = roots.Count;
            int normalRoots = 0, mobRoots = 0, normalCharacters = 0, mobCharacters = 0, skipped = 0;

            if (characters != null)
            {
                foreach (var selection in characters)
                {
                    var chara = selection?.CharaEntry;
                    string costume = selection?.CostumeId;
                    if (chara == null || string.IsNullOrEmpty(costume))
                    {
                        skipped++;
                        continue;
                    }

                    DataRow data = UmaDatabaseController.ReadCharaData(chara);
                    if (data == null)
                    {
                        skipped++;
                        continue;
                    }

                    int before = roots.Count;
                    string bodyFolder = costume.Length >= 4 && costume.LastIndexOf('_') > 0
                        ? "bdy" + costume.Substring(0, costume.LastIndexOf('_')) + "/"
                        : "bdy" + chara.Id + "_" + costume + "/";
                    RegisterFolder(UmaDatabaseController.BodyPath + bodyFolder);

                    if (chara.IsMob)
                    {
                        // RegisterMob RVA 0x1ab1e60: fixed mob head/tail folders.
                        RegisterFolder(UmaDatabaseController.HeadPath + "chr0001_00/");
                        RegisterFolder("3d/chara/tail/tail0001_00/");
                        RegisterFaceRuntime();
                        mobRoots += roots.Count - before;
                        mobCharacters++;
                        continue;
                    }

                    // RegisterNormal RVA 0x1ab2130: explicit head prefab if present,
                    // otherwise the default chr{id}_00 folder. Match Live's head override.
                    int headId = chara.Id;
                    string headCostume = string.IsNullOrEmpty(selection.HeadCostumeId)
                        ? costume : selection.HeadCostumeId;
                    int tailId = Convert.ToInt32(data["tail_model_id"], CultureInfo.InvariantCulture);
                    var builder = UmaViewerBuilder.Instance;
                    if (UmaViewerUI.Instance != null &&
                        UmaViewerUI.Instance.ModelSettings.IsHeadFix &&
                        builder != null && builder.CurrentHead != null)
                    {
                        headId = builder.CurrentHead.id;
                        headCostume = builder.CurrentHead.costumeId;
                        tailId = builder.CurrentHead.tailId;
                    }

                    string headFolder = UmaDatabaseController.HeadPath +
                                        "chr" + headId + "_" + headCostume + "/";
                    string headPrefab = headFolder + "pfb_chr" + headId + "_" + headCostume;
                    if (main.AbList.ContainsKey(headPrefab))
                    {
                        TryRegister(headPrefab);
                        RegisterFolder(headFolder);
                    }
                    else
                    {
                        RegisterFolder(UmaDatabaseController.HeadPath + "chr" + headId + "_00/");
                    }

                    if (tailId > 0)
                    {
                        string exclusive = "3d/chara/tail/tail" + chara.Id + "_" + costume + "/";
                        string exclusivePrefab = exclusive + "pfb_tail" + chara.Id + "_" + costume;
                        if (main.AbList.ContainsKey(exclusivePrefab))
                        {
                            TryRegister(exclusivePrefab);
                            RegisterFolder(exclusive);
                        }
                        else
                        {
                            RegisterFolder("3d/chara/tail/tail" +
                                           tailId.ToString("0000", CultureInfo.InvariantCulture) + "_00/");
                        }
                    }

                    RegisterFaceRuntime();
                    // The target's optional event idle path is conditional; LoadLive
                    // passes false, so it is not registered here.
                    normalRoots += roots.Count - before;
                    normalCharacters++;
                }
            }

            LiveRuntimeDiagnostics.RecordResourceRoots(
                directorRoots, normalRoots, mobRoots, normalCharacters, mobCharacters, skipped, roots.Count);
            return roots;
        }
    }
}
