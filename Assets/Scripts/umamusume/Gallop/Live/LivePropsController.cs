using System;
using System.Collections.Generic;
using System.Globalization;
using Gallop.Live.Cutt;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Gallop.Live
{
    // Uses the serialized game keys directly. Unrecovered branches are diagnosed, not
    // presented as native-equivalent behavior. Prefab skinning/materials remain intact.
    public sealed class LivePropsController : IDisposable
    {
        private const int CharacterMaskWidth = 20;
        private readonly Transform owner;
        private readonly IList<UmaContainerCharacter> characters;
        private readonly IList<LiveTimelineCharaLocator> locators;
        private readonly Dictionary<string, Transform>[] characterBones;
        private readonly Dictionary<int, Transform>[] characterBoneHashes;
        private readonly RuntimeProp[,] props;
        private readonly bool[,] unresolved;
        private readonly List<RuntimeProp> instances = new List<RuntimeProp>();
        private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
        private bool disposed;
        private StageController registeredStage;
        private StageBlinkLightDriver registeredBlinkDriver;
        private Gallop.RenderPipeline.RecoveredLensFlareController registeredFlares;

        // Same predicate used by LiveEffectController. Gated tracks are not silently
        // enabled when the Director has not supplied a variation selection.
        public Func<int, bool> VariationEnabled { get; set; }

        private static readonly string[] PropertyNames =
        {
            "_Color", "_CharaColor", "_RootColor", "_TipColor", "_ColorPower",
            "_ToonDarkColor", "_ToonBrightColor", "_OutlineWidth", "_OutlineColor",
            "_EmissiveColor", "_EmissiveScrollTimeScale", "_EmissiveScrollEnergyScale",
            "_UseOriginalDirectionalLight", "_OriginalDirectionalLightDir"
        };
        private static readonly int[] PropertyIds = MakePropertyIds();

        private static int[] MakePropertyIds()
        {
            var ids = new int[PropertyNames.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = Shader.PropertyToID(PropertyNames[i]);
            return ids;
        }

        private sealed class MaterialSlot
        {
            public int Properties;
            public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
            public bool HasIndexedBlock;
            public float OutlineWidth, EmissiveTime, EmissiveEnergy;
            public Color OutlineColor, EmissiveColor;
        }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public bool OriginallyEnabled;
            public MaterialSlot[] Slots;
            public MaterialSlot Global = new MaterialSlot();
        }

        private sealed class RuntimeProp
        {
            public int CharacterIndex, PropsId;
            public LiveTimelinePropsSettings.PropsDataGroup Setting;
            public GameObject Instance;
            public Transform Anchor, DefaultTarget;
            public bool DefaultPropParent;
            public readonly Dictionary<string, Transform> Nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            public RendererState[] Renderers;
            public Props[] StageProps;
            public string NameSuffixToken;
            public LiveTimelineKeyPropsData Appearance, NextAppearance;
            public LiveTimelineKeyPropsAttachData Attachment, NextAttachment;
            public float AppearanceRatio, AttachmentRatio;
            public Animator Animator;
            public PlayableGraph Graph;
            public AnimationClipPlayable ClipPlayable;
            public AnimationClip PlayingClip;
            public Transform[] PoseNodes;
            public Vector3[] PosePositions, PoseScales;
            public Quaternion[] PoseRotations;
        }

        public LivePropsController(Transform owner, LiveTimelinePropsSettings settings,
            IList<UmaContainerCharacter> characters, IList<LiveTimelineCharaLocator> locators)
        {
            this.owner = owner;
            this.characters = characters;
            this.locators = locators;
            int characterCount = System.Math.Min(characters?.Count ?? 0, CharacterMaskWidth);
            int propCount = settings?.propsDataGroup?.Length ?? 0;
            props = new RuntimeProp[characterCount, propCount];
            unresolved = new bool[characterCount, propCount];
            characterBones = new Dictionary<string, Transform>[characterCount];
            characterBoneHashes = new Dictionary<int, Transform>[characterCount];
            if ((characters?.Count ?? 0) > CharacterMaskWidth)
                Warn("Only the authored 20-character props mask is supported.");
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            for (int c = 0; c < characterCount; c++)
            {
                if (characters[c] == null) continue;
                var bones = locators != null && c < locators.Count ? locators[c]?.Bones : null;
                if (bones == null)
                {
                    bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
                    foreach (var node in characters[c].GetComponentsInChildren<Transform>(true))
                        if (!bones.ContainsKey(node.name)) bones.Add(node.name, node);
                }
                characterBones[c] = bones;
                var hashes = new Dictionary<int, Transform>();
                foreach (var pair in bones) hashes[FNVHash.Generate(pair.Key)] = pair.Value;
                characterBoneHashes[c] = hashes;
                int logicalId = 0;
                for (int p = 0; p < propCount; p++)
                {
                    var setting = settings.propsDataGroup[p];
                    if (setting == null || !SatisfiesConditions(setting, characters[c], c)) continue;
                    // Timeline IDs address this character's eligible props, not
                    // the global settings array. Reserve IDs even when loading fails.
                    int id = logicalId++;
                    DiagnoseSetting(setting);
                    string path = ResolveResourcePath(setting, characters[c]);
                    if (setting.isUseGenderDiffPropsId && CharacterSex(characters[c]) != 1 && CharacterSex(characters[c]) != 2)
                        Warn("Gender-differentiated props require CharaData.sex (1 male, 2 female); unknown metadata is not guessed.");
                    if (path == null) { unresolved[c, id] = true; continue; }
                    if (!prefabs.TryGetValue(path, out var prefab))
                    {
                        // Catalog lookup precedes loading; the utility retains the loaded
                        // bundle for the viewer, so this owner only destroys its instances.
                        prefab = LiveFlashResourceUtility.LoadOnView<GameObject>(path);
                        prefabs.Add(path, prefab);
                    }
                    if (prefab == null) { unresolved[c, id] = true; Warn("Missing props prefab: " + path); continue; }
                    var anchor = new GameObject("LiveProps[" + c + ":" + id + "]").transform;
                    anchor.SetParent(owner, false);
                    var instance = Object.Instantiate(prefab, anchor, false);
                    var runtime = new RuntimeProp
                    {
                        CharacterIndex = c,
                        PropsId = id,
                        Setting = setting,
                        Instance = instance,
                        Anchor = anchor,
                        Animator = instance.GetComponentInChildren<Animator>(true)
                    };
                    CacheNodes(runtime);
                    CacheRenderers(runtime);
                    props[c, id] = runtime;
                    instances.Add(runtime);
                }
            }
            // All instances exist before resolving prop-to-prop defaults.
            foreach (var prop in instances)
            {
                ResolveDefaultAttachment(prop);
                if (prop.DefaultTarget != null) ApplyAttachment(prop);
                var character = characters[prop.CharacterIndex];
                prop.StageProps = prop.Instance.GetComponentsInChildren<Props>(true);
                prop.NameSuffixToken = "_" + prop.CharacterIndex.ToString(CultureInfo.InvariantCulture);
                foreach (var component in prop.StageProps)
                    component.SetScale(character.HeadBone.transform, character.Position, character.BodyScale,
                        prop.Setting.isInfluenceOfCharaHeight);
                // Animation transitions restore the initialized pose, not the prefab's
                // unadjusted stand height. Native CreateProps scales once before playback.
                CacheNodes(prop);
            }
        }

        // Actual-game Director.CreateProps registers all three stage work arrays
        // after creating/scaling props (Komoe RVA 0x7154755..0x71547ea).
        internal void RegisterStageLighting(StageController stage,
            Gallop.RenderPipeline.RecoveredLensFlareController flares)
        {
            if (stage == null) return;
            registeredStage = stage;
            registeredBlinkDriver = stage.GetComponent<StageBlinkLightDriver>();
            registeredFlares = flares;
            foreach (var prop in instances)
                foreach (var component in prop.StageProps)
                {
                    var transforms = component.StageObjectTransformArray;
                    if (transforms != null)
                        foreach (var node in transforms)
                            if (node != null) stage.RegisterObjectTransform(node, prop.NameSuffixToken);
                    flares?.AddSources(component.transform, prop.NameSuffixToken);
                    var roots = component.BlinkLightRootObjectArray;
                    if (roots != null && registeredBlinkDriver != null)
                        foreach (var root in roots)
                            if (root != null) registeredBlinkDriver.RegisterRoot(root, prop.NameSuffixToken, flares);
                }
        }

        public static void CollectResourcePaths(LiveTimelinePropsSettings settings,
            IList<UmaContainerCharacter> characters, ISet<string> output)
        {
            if (settings?.propsDataGroup == null || characters == null || output == null) return;
            for (int c = 0; c < System.Math.Min(characters.Count, CharacterMaskWidth); c++)
            {
                if (characters[c] == null) continue;
                foreach (var setting in settings.propsDataGroup)
                {
                    if (setting == null || !SatisfiesConditions(setting, characters[c], c)) continue;
                    string path = ResolveResourcePath(setting, characters[c]);
                    if (path != null) output.Add(path);
                }
            }
        }

        private static bool SatisfiesConditions(LiveTimelinePropsSettings.PropsDataGroup setting,
            UmaContainerCharacter character, int index)
        {
            var groups = setting.propsConditionGroup;
            if (groups == null || groups.Length == 0) return true;
            // Authored1037 pairs a matching IsInvalid conjunction with a normal
            // position group: use exclusions as vetoes, valid groups as alternatives.
            bool included = false, hasInclusion = false;
            for (int g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                if (group == null) continue;
                if (!group.IsInvalid) hasInclusion = true;
                var conditions = group.propsConditionData;
                int count = conditions?.Length ?? 0;
                if (group.propsConditionCount > 0) count = System.Math.Min(count, group.propsConditionCount);
                bool satisfied = group.satisfiesAllConditions || count == 0;
                for (int i = 0; i < count; i++)
                {
                    var condition = conditions[i];
                    bool match = condition != null && SatisfiesCondition(condition, character, index);
                    if (group.satisfiesAllConditions) satisfied &= match;
                    else satisfied |= match;
                }
                if (satisfied && group.IsInvalid) return false;
                if (satisfied) included = true;
            }
            return included || !hasInclusion;
        }

        private static bool SatisfiesCondition(LiveTimelinePropsSettings.PropsConditionData condition,
            UmaContainerCharacter character, int index)
        {
            switch (condition.Type)
            {
                case LiveTimelinePropsSettings.PropsConditionType.Default: return true;
                case LiveTimelinePropsSettings.PropsConditionType.CharaPosition: return index == condition.Value;
                case LiveTimelinePropsSettings.PropsConditionType.CharaId: return character.CharaEntry != null && character.CharaEntry.Id == condition.Value;
                case LiveTimelinePropsSettings.PropsConditionType.DressId:
                    return CanonicalDressId(character) == condition.Value;
                default:
                    Debug.LogWarning("[LivePropsController] Unsupported props condition type: " + condition.Type);
                    return false;
            }
        }

        private static int CanonicalDressId(UmaContainerCharacter character)
        {
            string costume = character.VarCostumeIdLong;
            if (string.IsNullOrEmpty(costume)) costume = character.VarCostumeIdShort;
            var rows = UmaDatabaseController.Instance?.DressData;
            if (rows == null || string.IsNullOrEmpty(costume)) return -1;
            var parts = costume.Split('_');
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int first)) return -1;
            bool generic = parts.Length >= 3;
            int subtype = 0, setting = 0;
            if (generic && (!int.TryParse(parts[1], out subtype) || !int.TryParse(parts[2], out setting))) return -1;
            int found = -1;
            foreach (var row in rows)
            {
                // Use the same character/subtype and generic body tuple as costume selection.
                bool matches = generic
                    ? Convert.ToInt32(row["body_type"]) == first && Convert.ToInt32(row["body_type_sub"]) == subtype && Convert.ToInt32(row["body_setting"]) == setting
                    : character.CharaEntry != null && Convert.ToInt32(row["chara_id"]) == character.CharaEntry.Id && Convert.ToInt32(row["body_type_sub"]) == first;
                if (!matches) continue;
                int id = Convert.ToInt32(row["id"]);
                if (found != -1 && found != id) return -1;
                found = id;
            }
            return found;
        }

        private static string ResolveResourcePath(LiveTimelinePropsSettings.PropsDataGroup setting,
            UmaContainerCharacter character)
        {
            if (!setting.isCharaProps)
            {
                if (!int.TryParse(setting.propsName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int commonId) || commonId < 0) return null;
                string common = "3d/env/live/common/prop/pfb_env_live_cmn_prop" + commonId.ToString("D3", CultureInfo.InvariantCulture);
                return LiveEffectController.FindResourceEntry(common) != null ? common : null;
            }
            int major = setting.charaPropsMajorId, minor = setting.charaPropsMinorId;
            if (setting.isUseGenderDiffPropsId)
            {
                int sex = CharacterSex(character);
                if (sex != 1 && sex != 2) return null;
                major = sex == 1 ? setting.MaleCharaPropsMajorId : setting.FemaleCharaPropsMajorId;
                minor = sex == 1 ? setting.MaleCharaPropsMinorId : setting.FemaleCharaPropsMinorId;
            }
            if (major > 0)
            {
                string path = ResourcePath(major, minor, setting.IsToonProp, setting.IsRichProp);
                if (LiveEffectController.FindResourceEntry(path) != null) return path;
            }
            // Numeric propsName is a regular-character-prop fallback only after
            // the configured family/ID is missing. Conditions were checked first.
            if (int.TryParse(setting.propsName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fallback) && fallback > 0)
            {
                string path = ResourcePath(fallback, 0, false, false);
                if (LiveEffectController.FindResourceEntry(path) != null) return path;
            }
            return null;
        }

        private static string ResourcePath(int major, int minor, bool toon, bool rich)
        {
            string id = major.ToString("D4", CultureInfo.InvariantCulture) + "_" + minor.ToString("D2", CultureInfo.InvariantCulture);
            // Rich wins over Toon in authored1154 (both flags are true).
            return rich ? "3d/chara/richprop/rich_prop" + id + "/pfb_rich_prop" + id
                : toon ? "3d/chara/toonprop/toon_prop" + id + "/pfb_toon_prop" + id
                : "3d/chara/prop/prop" + id + "/pfb_chr_prop" + id;
        }

        private static int CharacterSex(UmaContainerCharacter character)
        {
            var row = character?.CharaData;
            if (row?.Table == null || !row.Table.Columns.Contains("sex")) return 0;
            return int.TryParse(Convert.ToString(row["sex"], CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int sex) ? sex : 0;
        }

        private void DiagnoseSetting(LiveTimelinePropsSettings.PropsDataGroup setting)
        {
            // Metadata is checked for each selected character by resource resolution.
            if (setting.IsFlareCollisionEnabled) Warn("Props flare-collision integration is unrecovered; prefab components are preserved.");
            if (setting.IsDepthWriteAlphaMesh) Warn("Props depth-write-alpha material policy is unrecovered; original prefab materials are preserved.");
        }

        private static void CacheNodes(RuntimeProp prop)
        {
            prop.PoseNodes = prop.Instance.GetComponentsInChildren<Transform>(true);
            int count = prop.PoseNodes.Length;
            prop.PosePositions = new Vector3[count];
            prop.PoseRotations = new Quaternion[count];
            prop.PoseScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var node = prop.PoseNodes[i];
                if (!prop.Nodes.ContainsKey(node.name)) prop.Nodes.Add(node.name, node);
                prop.PosePositions[i] = node.localPosition;
                prop.PoseRotations[i] = node.localRotation;
                prop.PoseScales[i] = node.localScale;
            }
        }

        private static void CacheRenderers(RuntimeProp prop)
        {
            var renderers = prop.Instance.GetComponentsInChildren<Renderer>(true);
            prop.Renderers = new RendererState[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                var materials = renderer.sharedMaterials;
                var state = new RendererState { Renderer = renderer, OriginallyEnabled = renderer.enabled, Slots = new MaterialSlot[materials.Length] };
                renderer.GetPropertyBlock(state.Global.Block);
                for (int m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    var slot = new MaterialSlot();
                    state.Slots[m] = slot;
                    if (material == null) continue;
                    renderer.GetPropertyBlock(slot.Block, m);
                    slot.HasIndexedBlock = !slot.Block.isEmpty;
                    for (int i = 0; i < PropertyIds.Length; i++)
                        if (material.HasProperty(PropertyIds[i])) slot.Properties |= 1 << i;
                    state.Global.Properties |= slot.Properties;
                    if (m == 0)
                    {
                        state.Global.OutlineWidth = InitialFloat(state.Global, material, 7);
                        state.Global.OutlineColor = InitialColor(state.Global, material, 8);
                        state.Global.EmissiveColor = InitialColor(state.Global, material, 9);
                        state.Global.EmissiveTime = InitialFloat(state.Global, material, 10);
                        state.Global.EmissiveEnergy = InitialFloat(state.Global, material, 11);
                    }
                    slot.OutlineWidth = InitialFloat(slot, material, 7);
                    slot.OutlineColor = InitialColor(slot, material, 8);
                    slot.EmissiveColor = InitialColor(slot, material, 9);
                    slot.EmissiveTime = InitialFloat(slot, material, 10);
                    slot.EmissiveEnergy = InitialFloat(slot, material, 11);
                }
                renderer.enabled = false;
                prop.Renderers[r] = state;
            }
        }

        private static float InitialFloat(MaterialSlot slot, Material material, int property)
        {
            int id = PropertyIds[property];
            return (slot.Properties & (1 << property)) == 0 ? 0f : slot.Block.HasFloat(id) ? slot.Block.GetFloat(id) : material.GetFloat(id);
        }

        private static Color InitialColor(MaterialSlot slot, Material material, int property)
        {
            int id = PropertyIds[property];
            return (slot.Properties & (1 << property)) == 0 ? Color.clear : slot.Block.HasColor(id) ? slot.Block.GetColor(id) : material.GetColor(id);
        }

        private Transform CharacterJoint(int characterIndex, string name, int hash = 0)
        {
            var bones = characterBones[characterIndex];
            if (bones == null) return null;
            if (!string.IsNullOrEmpty(name) && bones.TryGetValue(name, out var bone)) return bone;
            return hash != 0 && characterBoneHashes[characterIndex].TryGetValue(hash, out var hashed) ? hashed : null;
        }


        private void ResolveDefaultAttachment(RuntimeProp prop)
        {
            var setting = prop.Setting;
            if (setting.attachJointNames != null)
                foreach (string joint in setting.attachJointNames)
                {
                    prop.DefaultTarget = CharacterJoint(prop.CharacterIndex, joint);
                    if (prop.DefaultTarget != null) break;
                }
            var ids = setting.AttachPropIdArray;
            var names = setting.AttachPropJointNameArray;
            if (ids != null)
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] < 0) continue;
                    var target = PropJoint(prop, ids[i], names != null && i < names.Length ? names[i] : null);
                    if (target != null) { prop.DefaultTarget = target; prop.DefaultPropParent = true; break; }
                }
            // No character/root fallback for an unresolved named attachment.
            // An explicit attach key can still resolve this on the first frame.
        }

        private Transform PropJoint(RuntimeProp prop, int id, string name)
        {
            if (id < 0 || id >= props.GetLength(1)) return null;
            var other = props[prop.CharacterIndex, id];
            if (other == null) return null;
            if (string.IsNullOrEmpty(name)) return other.Instance.transform;
            return other.Nodes.TryGetValue(name, out var node) ? node : null;
        }

        public void Evaluate(IList<LiveTimelineWorkSheet> sheets, float frame, TimelinePlayerMode mode)
        {
            if (disposed) return;
            foreach (var prop in instances)
            {
                prop.Appearance = prop.NextAppearance = null;
                prop.Attachment = prop.NextAttachment = null;
                prop.AppearanceRatio = prop.AttachmentRatio = 0f;
            }
            if (sheets != null)
                for (int s = 0; s < sheets.Count; s++)
                {
                    var sheet = sheets[s];
                    if (sheet == null || !sheet.enableAtRuntime) continue;
                    if (sheet.propsList != null)
                        foreach (var group in sheet.propsList)
                        {
                            if (group == null || !Active(group.keys, group._applyVariation, group._variationId, frame, mode)) continue;
                            LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
                            DiagnoseRequestedResource(current as LiveTimelineKeyPropsData);
                            foreach (var prop in instances)
                            {
                                var a = LatestAppearance(group.keys, current as LiveTimelineKeyPropsData, prop, frame);
                                if (a == null || (prop.Appearance != null && a.frame < prop.Appearance.frame)) continue;
                                var b = next as LiveTimelineKeyPropsData;
                                if (b == null || !Selected(b.settingFlags, b.propsID, prop)) b = a;
                                prop.Appearance = a;
                                prop.NextAppearance = b;
                                prop.AppearanceRatio = b != a && b.IsInterpolateKey() ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
                            }
                        }
                    if (sheet.propsAttachList != null)
                        foreach (var group in sheet.propsAttachList)
                        {
                            if (group == null || !Active(group.keys, group._applyVariation, group._variationId, frame, mode)) continue;
                            LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
                            foreach (var prop in instances)
                            {
                                var a = LatestAttachment(group.keys, current as LiveTimelineKeyPropsAttachData, prop, frame);
                                if (a == null || (prop.Attachment != null && a.frame < prop.Attachment.frame)) continue;
                                var b = next as LiveTimelineKeyPropsAttachData;
                                if (b == null || !Selected(b._settingFlags, b._propsId, prop)) b = a;
                                prop.Attachment = a;
                                prop.NextAttachment = b;
                                prop.AttachmentRatio = b != a && b.IsInterpolateKey() ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
                            }
                        }
                }
            foreach (var prop in instances)
            {
                ApplyAnimation(prop, frame);
                ApplyAppearance(prop);
                ApplyAttachment(prop);
            }
            registeredBlinkDriver?.UpdateRegisteredRoots();
        }

        private void DiagnoseRequestedResource(LiveTimelineKeyPropsData key)
        {
            if (key == null || (!key.rendererEnable && !key.IsApplyAnimation)) return;
            for (int c = 0; c < unresolved.GetLength(0); c++)
            {
                if ((key.settingFlags & (1 << c)) == 0) continue;
                for (int p = 0; p < unresolved.GetLength(1); p++)
                    if (unresolved[c, p] && (key.propsID == -1 || key.propsID == p))
                        Warn("A visible/animated props key requests an unavailable or unsupported catalog resource.");
            }
        }

        private bool Active(ILiveTimelineKeyDataList keys, bool variation, int variationId, float frame, TimelinePlayerMode mode)
        {
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable)
                || !keys.EnablePlayModeTimeline(mode) || keys[0] == null || frame < keys[0].frame) return false;
            if (!variation) return true;
            if (VariationEnabled != null) return VariationEnabled(variationId);
            Warn("Variation-gated props tracks require VariationEnabled; tracks skipped.");
            return false;
        }

        private static bool Selected(int flags, int id, RuntimeProp prop)
        {
            return (flags & (1 << prop.CharacterIndex)) != 0 && (id == -1 || id == prop.PropsId);
        }

        // A mask only updates selected characters. Looking back to the last selected
        // key makes seeking deterministic instead of leaking the previous playhead's
        // visibility/attachment state into characters omitted by the current key.
        private static LiveTimelineKeyPropsData LatestAppearance(ILiveTimelineKeyDataList keys,
            LiveTimelineKeyPropsData current, RuntimeProp prop, float frame)
        {
            if (current != null && Selected(current.settingFlags, current.propsID, prop)) return current;
            for (int i = keys.Count - 1; i >= 0; i--)
                if (keys[i] is LiveTimelineKeyPropsData key && key.frame <= frame && Selected(key.settingFlags, key.propsID, prop)) return key;
            return null;
        }

        private static LiveTimelineKeyPropsAttachData LatestAttachment(ILiveTimelineKeyDataList keys,
            LiveTimelineKeyPropsAttachData current, RuntimeProp prop, float frame)
        {
            if (current != null && Selected(current._settingFlags, current._propsId, prop)) return current;
            for (int i = keys.Count - 1; i >= 0; i--)
                if (keys[i] is LiveTimelineKeyPropsAttachData key && key.frame <= frame && Selected(key._settingFlags, key._propsId, prop)) return key;
            return null;
        }

        private void ApplyAttachment(RuntimeProp prop)
        {
            var a = prop.Attachment;
            var b = prop.NextAttachment;
            Transform target = prop.DefaultTarget;
            Transform copy = null;
            Vector3 position = Vector3.zero, rotation = Vector3.zero, scale = Vector3.one;
            if (a != null)
            {
                if (a._attachType != 0 && a._attachPropId < 0)
                    Warn("Unrecovered nonzero props attachType; resolving the explicit named attachment only.");
                target = a._attachPropId >= 0 ? PropJoint(prop, a._attachPropId, a._attachTargetPropNodeName)
                    : CharacterJoint(prop.CharacterIndex, a._attachJointName, a._attachJointHash);
                if (target == null && string.IsNullOrEmpty(a._attachJointName) && a._attachPropId < 0) target = prop.DefaultTarget;
                copy = CharacterJoint(prop.CharacterIndex, a._copyPositionJointName, a._copyPositionJointHash);
                float t = prop.AttachmentRatio;
                position = Vector3.LerpUnclamped(a._offsetPosition, b._offsetPosition, t);
                rotation = Vector3.LerpUnclamped(a.OffsetRotate, b.OffsetRotate, t);
                scale = Vector3.LerpUnclamped(a.OffsetScale, b.OffsetScale, t);
                if (a.IsLinkAttachBone)
                    Warn("IsLinkAttachBone transform-link override is unrecovered; requested bone parenting and offsets remain active.");
            }
            if (target == null)
            {
                Warn("Requested props attachment joint/node is missing; prop hidden until attachment resolves.");
                foreach (var renderer in prop.Renderers) renderer.Renderer.enabled = false;
                return;
            }
            if (target == prop.Anchor || target.IsChildOf(prop.Anchor))
            {
                Warn("Cyclic prop-to-prop attachment rejected.");
                foreach (var renderer in prop.Renderers) renderer.Renderer.enabled = false;
                return;
            }
            if (prop.Anchor.parent != target) prop.Anchor.SetParent(target, false);
            prop.Anchor.localPosition = position;
            prop.Anchor.localRotation = Quaternion.Euler(rotation);
            // The existing character runtime scales Position uniformly by BodyScale.
            // Height-influenced props naturally inherit that scale through the hand.
            // For non-influenced character attachments only, cancel that known factor.
            bool characterParent = a == null ? !prop.DefaultPropParent : a._attachPropId < 0;
            float bodyScale = characters[prop.CharacterIndex].BodyScale;
            var characterPosition = CharacterJoint(prop.CharacterIndex, "Position");
            bool inheritsHeight = characterPosition != null && (target == characterPosition || target.IsChildOf(characterPosition));
            if (!prop.Setting.isInfluenceOfCharaHeight && characterParent && inheritsHeight && bodyScale > 0f) scale /= bodyScale;
            prop.Anchor.localScale = scale;
            if (copy != null) prop.Anchor.position = copy.position + target.TransformVector(position);
        }

        private void ApplyAppearance(RuntimeProp prop)
        {
            var a = prop.Appearance;
            var b = prop.NextAppearance;
            bool visible = a != null && a.rendererEnable;
            if (visible && a.IsVisibleAttachedCharaLinked)
                visible = characters[prop.CharacterIndex].gameObject.activeInHierarchy
                    && (locators == null || prop.CharacterIndex >= locators.Count || locators[prop.CharacterIndex] == null || locators[prop.CharacterIndex].liveCharaVisible);
            if (a != null && a.AutoSwitchLayerOnMirrorRendering)
                Warn("Props mirror-pass layer switching is unrecovered; authored prefab layers are preserved.");
            float t = prop.AppearanceRatio;
            foreach (var state in prop.Renderers)
            {
                var renderer = state.Renderer;
                if (renderer == null) continue;
                renderer.enabled = visible && state.OriginallyEnabled;
                if (a == null) continue;
                renderer.shadowCastingMode = a.IsCastShadowForced || (prop.Setting.hasShadow && a.IsCastShadow)
                    ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.GetPropertyBlock(state.Global.Block);
                ApplyMaterial(state.Global, a, b, t);
                renderer.SetPropertyBlock(state.Global.Block);
                for (int m = 0; m < state.Slots.Length; m++)
                {
                    var slot = state.Slots[m];
                    // Do not create an indexed block where there was none: that
                    // would mask unrelated renderer-wide lighting parameters.
                    if (!slot.HasIndexedBlock) continue;
                    renderer.GetPropertyBlock(slot.Block, m);
                    ApplyMaterial(slot, a, b, t);
                    renderer.SetPropertyBlock(slot.Block, m);
                }
            }
        }

        private static void ApplyMaterial(MaterialSlot slot, LiveTimelineKeyPropsData a,
            LiveTimelineKeyPropsData b, float t)
        {
            Color color = Color.LerpUnclamped(a.color, b.color, t);
            float power = Mathf.LerpUnclamped(a.colorPower, b.colorPower, t);
            SetColor(slot, 0, color);
            SetColor(slot, 1, color * power);
            SetColor(slot, 2, Color.LerpUnclamped(a.rootColor, b.rootColor, t));
            SetColor(slot, 3, Color.LerpUnclamped(a.tipColor, b.tipColor, t));
            SetFloat(slot, 4, power);
            SetColor(slot, 5, Color.LerpUnclamped(a.ToonDarkColor, b.ToonDarkColor, t));
            SetColor(slot, 6, Color.LerpUnclamped(a.ToonBrightColor, b.ToonBrightColor, t));
            SetFloat(slot, 7, a.IsUpdateOutline ? Mathf.LerpUnclamped(a.OutlineWidth, b.OutlineWidth, t) : slot.OutlineWidth);
            SetColor(slot, 8, a.IsUpdateOutline ? Color.LerpUnclamped(a.OutlineColor, b.OutlineColor, t) : slot.OutlineColor);
            SetColor(slot, 9, a.IsEmissive ? Color.LerpUnclamped(a.EmissiveColor, b.EmissiveColor, t) : slot.EmissiveColor);
            SetFloat(slot, 10, a.IsEmissive ? Mathf.LerpUnclamped(a.EmissiveScrollTimeScale, b.EmissiveScrollTimeScale, t) : slot.EmissiveTime);
            SetFloat(slot, 11, a.IsEmissive ? Mathf.LerpUnclamped(a.EmissiveScrollEnergyScale, b.EmissiveScrollEnergyScale, t) : slot.EmissiveEnergy);
            SetFloat(slot, 12, 1f);
            if ((slot.Properties & (1 << 13)) != 0)
                slot.Block.SetVector(PropertyIds[13], -(Quaternion.Euler(Vector3.LerpUnclamped(a._directionalLightAngle, b._directionalLightAngle, t)) * Vector3.forward).normalized);
        }

        private static void SetColor(MaterialSlot slot, int index, Color value)
        {
            if ((slot.Properties & (1 << index)) != 0) slot.Block.SetColor(PropertyIds[index], value);
        }

        private static void SetFloat(MaterialSlot slot, int index, float value)
        {
            if ((slot.Properties & (1 << index)) != 0) slot.Block.SetFloat(PropertyIds[index], value);
        }

        private void ApplyAnimation(RuntimeProp prop, float frame)
        {
            var key = prop.Appearance;
            AnimationClip clip = key != null && key.IsApplyAnimation ? key.AnimationClip : null;
            if (key != null && key.IsApplyAnimation && clip == null)
                Warn("Props animation key has no resolved AnimationClip reference.");
            if (key != null && key.IsApplyReserveWarming)
                Warn("Props reserve-warming policy is unrecovered; the requested clip is evaluated at timeline time.");
            if (prop.PlayingClip != clip)
            {
                if (prop.Graph.IsValid()) prop.Graph.Destroy();
                RestorePose(prop);
                prop.PlayingClip = clip;
                if (clip != null && !clip.legacy)
                {
                    // A real clip playable, never Animator.Rebind in place of playback.
                    if (prop.Animator == null) prop.Animator = prop.Instance.AddComponent<Animator>();
                    prop.Graph = PlayableGraph.Create("LivePropsAnimation");
                    prop.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    prop.ClipPlayable = AnimationClipPlayable.Create(prop.Graph, clip);
                    prop.ClipPlayable.SetApplyFootIK(false);
                    prop.ClipPlayable.SetApplyPlayableIK(false);
                    var output = AnimationPlayableOutput.Create(prop.Graph, "Props", prop.Animator);
                    output.SetSourcePlayable(prop.ClipPlayable);
                    prop.Graph.Play();
                }
            }
            if (clip == null) return;
            float time = Mathf.Max(0f, key.StartAnimationTime + (frame - key.frame + key.AnimationHeadFrame) / 60f);
            time = clip.isLooping && clip.length > 0f ? Mathf.Repeat(time, clip.length) : Mathf.Min(time, clip.length);
            if (clip.legacy) clip.SampleAnimation(prop.Instance, time);
            else
            {
                prop.ClipPlayable.SetTime(time);
                prop.Graph.Evaluate(0f);
            }
        }

        private static void RestorePose(RuntimeProp prop)
        {
            for (int i = 0; i < prop.PoseNodes.Length; i++)
            {
                var node = prop.PoseNodes[i];
                if (node == null) continue;
                node.localPosition = prop.PosePositions[i];
                node.localRotation = prop.PoseRotations[i];
                node.localScale = prop.PoseScales[i];
            }
        }

        private void Warn(string message)
        {
            if (warnings.Add(message)) Debug.LogWarning("[LivePropsController] " + message);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var prop in instances)
            {
                foreach (var component in prop.StageProps)
                {
                    if (component == null) continue;
                    var transforms = component.StageObjectTransformArray;
                    if (transforms != null && registeredStage != null)
                        foreach (var node in transforms)
                            if (node != null) registeredStage.UnregisterObjectTransform(node, prop.NameSuffixToken);
                    var roots = component.BlinkLightRootObjectArray;
                    if (roots != null && registeredBlinkDriver != null)
                        foreach (var root in roots)
                            if (root != null) registeredBlinkDriver.UnregisterRoot(root, prop.NameSuffixToken);
                    registeredFlares?.RemoveSources(component.transform, prop.NameSuffixToken);
                }
                if (prop.Graph.IsValid()) prop.Graph.Destroy();
                // Destroy separately: a prop-to-prop parent may already have been removed.
                if (prop.Instance != null) Object.Destroy(prop.Instance);
                if (prop.Anchor != null) Object.Destroy(prop.Anchor.gameObject);
            }
            instances.Clear();
            Array.Clear(props, 0, props.Length);
            Array.Clear(unresolved, 0, unresolved.Length);
            Array.Clear(characterBones, 0, characterBones.Length);
            Array.Clear(characterBoneHashes, 0, characterBoneHashes.Length);
            warnings.Clear();
        }
    }
}
