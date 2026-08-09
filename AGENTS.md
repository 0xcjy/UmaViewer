# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UmaViewer is a Unity application (2022.3.62f1) that loads and displays assets from Uma Musume: Pretty Derby. It reads encrypted asset bundles directly from the game installation without modifying game files.

## Development

**Open the project:** Unity Hub → Add → `C:\Users\riten\Desktop\UmaBuild`
**Run:** Open `Assets/Scenes/Version2` scene → Play in Unity Editor
**Build:** File → Build Settings → PC Standalone → Build

No CLI build commands — Unity Editor is required.

## Game Data & Asset Pipeline

Game assets are encrypted. Key files:
- `meta` (SQLite-MC ChaCha20 encrypted) — maps bundle names → hash filenames in `dat/`
- `dat/{2-char-prefix}/{hash}` — encrypted asset bundles, each with a per-bundle XOR key from meta

**Bundle decryption** (`UmaAssetBundleStream.cs`): header (first 256 bytes) is plain; bytes ≥256 are XOR'd with `FKey = ABKey[i] XOR bundleKey_bytes[j]`.

**meta DB decryption**: `UmaDatabaseController.ReadMetaFromEncryptedDb()` uses `sqlite3mc_x64.dll` with cipher=3 (ChaCha20). Final key = `DBKey[i] XOR DBBaseKey[i % 13]`.

**Asset bundle paths** (key formats):
- Live cutt timelines: `cutt/cutt_son{music_id}/cutt_son{music_id}`
- Live effect prefabs: `3d/effect/live/pfb_{effectName}`
- Stage prefabs: `3d/env/live/live{bgId}/pfb_env_live{bgId}_controller000`

**Python tools** (`tools/`) — rebuilt 2026-08-04 after the old `UmaCrack\` toolchain was deleted. Run with `~/.venvs/umatools/bin/python3` (needs `UnityPy` + `apsw-sqlite3mc`, **not** plain `apsw`; see `tools/requirements.txt`).

| Script | Use |
|---|---|
| `uma_common.py` | Library: key constants, `decrypt_bundle_bytes()`, `MetaDb` (path→hash, UnityPy load) |
| `dump_meta.py` | `plain` → `out/meta_plain.db` · `find <LIKE>` · `info <path>` (hash, dat path, deps, `[NOT DOWNLOADED]`) · `songs` |
| `dump_bundle.py` | `list` / `tree` / `json` / `save` / `gameobjects`, `--deps` |
| `dump_cutt_typetree.py` | **Authoritative track schema.** `--fields <song>` · `--tree <field> <song>` · `--sample <field> <song>` · `--groups` |
| `scan_live_tracks.py` | All 59 songs → `out/scan_keys.csv`, `scan_groups.csv`, `scan_summary.txt`, `scan.json` (~21 s) |

Notes: `meta` decrypts via sqlite3mc `PRAGMA cipher='chacha20'` + `PRAGMA hexkey='<64 hex>'` (the `key=x'...'` and `raw:` forms both fail); `VACUUM INTO` re-encrypts the target, so `dump_meta.py plain` copies schema+rows explicitly.

**Cutt bundles ship full TypeTrees, so field names and keyframe counts from these tools are ground truth.** All 70 worksheets across all 59 songs share one 128-field schema — no version drift. Only `son<id>_camera` and `type_01..type_10` carry the trees; the `cutt_son<id>` prefab and `data` bundles are stripped.

**Getting a track's real field name without the dump tools** — Unity matches bundle TypeTree fields by name, case-sensitively. A field whose name doesn't match is never populated (it does *not* become null — Unity initialises every serialised `List<T>` to an empty list, so null-vs-empty tells you nothing). To test a candidate name, declare it on `LiveTimelineWorkSheet` and check whether it receives keys:

```csharp
LiveTimelineWorksheetDiag.Probe(sheet, "candidateFieldName");
```

Unsure about casing? Declare both spellings side by side — only the correct one fills. This is how `WashLightList` (capital W) was confirmed; note upstream katboi01 uses `washLightList`, which never populates.

## Live Timeline Architecture

The Live playback system is in `Assets/Scripts/umamusume/Gallop/Live/`:

```
Director.cs              ← singleton, wires everything together
Cutt/
  LiveTimelineControl.cs ← dispatches timeline events each frame (~1900 lines)
  LiveTimelineWorkSheet.cs ← all track data fields (ScriptableObject)
  LiveTimelineDataList/   ← one .cs per track type
  UpdateInfo/             ← structs passed to event handlers
StageController.cs        ← manages stage GameObjects, StageObjectMap
```

**Track implementation pattern** (repeat for each new track):
1. Add data class in `LiveTimelineDataList/` (extend `LiveTimelineKeyWithInterpolate` for key, add group wrapper implementing `ILiveTimelineGroupDataWithName`)
2. Add `[SerializeField] public List<...> trackList;` to `LiveTimelineWorkSheet`
3. Add `public event Action<TData, TKey> OnUpdateXxx;` in `LiveTimelineControl`
4. Call `AlterUpdate_SimpleListControl(workSheet.trackList, d => d.keys, OnUpdateXxx, _currentFrame)` in `AlterLateUpdate()`
5. Subscribe `_liveTimelineControl.OnUpdateXxx += handler` in `Director.InitializeTimeline()`

**Important:** `LiveTimelineKeyWithInterpolate` already declares `frame`, `attribute`, `interpolateType`, `curve`, `easingType` — do NOT redeclare these in subclasses.

**Stage shader colour channels** (enumerated at runtime via `Shader.GetPropertyType`, confirmed on stage live10149 / song 1177):

| shader | colour | brightness | other |
|---|---|---|---|
| `Gallop/3D/Live/Stage/DefaultNoAmbient` | `_MulColor0` | `_ColorPower` | |
| `Gallop/3D/Live/Stage/DefaultEnvMapNoAmbient` | `_MulColor0` | `_ColorPower` | `_AddColor`, `_EnvMap` |
| `Gallop/3D/Live/Stage/DefaultTransparentNoAmbient` | `_MulColor0` | `_ColorPower` | `_AmbientColor` |
| `Gallop/3D/Live/Stage/LightBlinkBlend` | `_BlinkLightColor` | `_ColorPower` | `_SrcBlend`/`_DstBlend` |
| `Gallop/3D/Live/Stage/LightBlend` / `LightAdd1` | `_MulColor0`, `_MulColor1` | `_ColorPower`, `_ColorPowerMultiply` | |
| `Gallop/3D/Live/Stage/Laser` | `_MulColor0`, `_MulColor1` | `_ColorPower` | `_RandomValue` |
| `Gallop/3D/Bg/BgMirrorBall` | `_MulColor0` | **(none)** | |
| `Gallop/3D/Live/Stage/StageMirrorBallShine` | (none) | | |
| `Gallop/3D/Live/Stage/StageTransmittedLightMask` | (none) | | `_TransmittedLightMaskScale` |

**Brightness is a separate channel — do not fold it into the colour.** Write the key's colour to the colour property and its `power` to `_ColorPower`; let the shader combine them. Only fall back to `colour * power` where `_ColorPower` is absent (`BgMirrorBall`).

**Writing shader properties: one shared block, always Get → modify → Set.** All of `Director`'s
handlers write through the single `PropBlock` (`Director._propBlock`) and must call
`renderer.GetPropertyBlock(block)` first — `SetPropertyBlock()` **replaces** the block rather than
merging, so a handler that starts from an empty block wipes whatever another track wrote to the
same renderer earlier in the same frame. That is exactly how GlobalLight was silently dead for
59/59 songs (BgColor1's character branch runs right after it and cleared all 13 rim properties).
Never `new MaterialPropertyBlock()` per frame, and never write `_Color` without first checking the
target shader's real property table — three separate tracks shipped no-op colour writes that way.

Dump any shader's real property table with:

```python
# tools/, via ~/.venvs/umatools/bin/python3
env = MetaDb().load("shader")          # 499 shaders
pf = obj.read_typetree()["m_ParsedForm"]   # m_Name, m_PropInfo.m_Props
```

**`_MulColor0` is on every stage shader and is BgColor1's channel; `_AmbientColor` belongs to BgColor2.** They are separate properties on the same shader, so the two tracks must not write the same one. Same naming family as WashLight's `MulColor0` and UVScrollLight's `mulColor1`.

⚠️ A material's `m_SavedProperties` in the bundle lists properties it no longer has — it retains entries from whatever shader it was authored against. `Material.HasProperty()` asks the *current shader*. Always enumerate the shader, never trust the bundle's saved-property table.

**渲染状态也从这张过期表里读，后果最严重。** Gallop 的发光类 shader 把混合写成
`Blend [_SrcBlend] [_DstBlend]`，也就是**从材质属性取渲染状态**。live10149 的
**26/26 个材质**存的都是 `_SrcBlend=One, _DstBlend=Zero`（不透明覆盖），且每一个的 float 表里
都带着 `_WorkflowMode`/`_Surface`/`_ClearCoatMask`/`_Smoothness`/`_Blend` —— 那是 **URP Lit
的属性表**，整批盖上去的，One/Zero 正是 URP Lit 的不透明预设。

作者意图可查：全库 16 个声明这两个属性的 shader 里，**14 个默认 `One/One`（加法混合）** ——
`LightBlinkBlend`、`LightBlend`、`StageBeamLight{,Cutoff,Fadeout}`、
`StageLightBlink{Cutoff,Fadeout}`、`StageProjectorBlend{,VertexAlpha,AnimVertexAlpha}`、
`StageMonitorBlend*`、`MirrorBallProjector`、`BgUnlitStencilAlpha`，全是发光/投影那一批。

被压成不透明后，加法光柱「颜色为黑 = 什么都不加 = 不可见」变成了**一堵实心黑墙**。

⚠️ **实际受害面比存档表窄得多，别照着 26 这个数推。** 只有**当前 shader 真的声明了**这两个
属性的材质才会被读到；其余材质虽然也存着 `One/Zero`，但那只是过期条目，无人问津。
live10149 实测（`[StageBlend]` 日志）：31 个材质里**只有 1 个**被修正 ——
`mtl_env_live10149_blinklight000` / `LightBlinkBlend`，`_DstBlend` 0 → 1。

**但「1 个材质」严重低估了影响面 —— 材质在舞台上是高度共享的。** 那一个材质铺满
27 个 BlinkLight 组、约 **1450 个渲染器**（观众席 95、mob000 570、glow_object 67、
glow_ramp 58、镜面球闪片 83、全部 wash 组、各种 LED 板），实机确认修好的不只是地面 wash 灯的
黑楔子，长期挂着的「小灯泡渲染成黑块」也一并正常了。
**教训（第三次同一天）：报数要报对单位。** 舞台上约 1500 个渲染器只有 31 个材质，
按材质计数会把影响面说小两个数量级 —— `[StageBlend]` 日志现在同时报渲染器数。
`StageController.RestoreAuthoredBlendState()` 在舞台初始化时把这两个属性写回 **shader 声明的
默认值**（`Shader.GetPropertyDefaultFloatValue`），对本来就该不透明的 shader 是空操作，自限。
改了哪些材质会逐条打进 `[StageBlend]` 日志。

> 教训比这个 bug 本身通用：**过期存档表不只影响颜色，还能改渲染状态**。
> 看到「本该发光的东西是黑的/挡住后面的东西」，先查 `_SrcBlend`/`_DstBlend`，
> 再查颜色写对没有。

Worked example — `mtl_env_live10149_projector001` (shader `Gallop/3D/MirrorAndShadow/MirrorBallProjector`, 22 real properties) saves 40+, including Standard-shader leftovers (`_Glossiness`, `_Metallic`, `_Parallax`, `_Mode`, `_UVSec`) **and** four plausible-looking ghosts the current shader never declares: `_IsLoopYRotation`, `_LoopYRotationSpeed`, `_MirrorBallProjectionIntensity`, `_MirrorBallRotateWS`. Two of those are older spellings of properties that *do* exist (`_MirrorBallIsLoopRotation`, `_MirrorBallLoopRotationSpeed`) — exactly the kind of near-miss that reads as a discovery. Note also that where a component field and a saved property disagree (`MirrorBallFallOffPower` = 1.0 on the component vs `_MirrorBallFalloffPower` = 2.0 in the material), the component wins at runtime: these controllers push their own fields into the material.

**Track group names are not always GameObject names.** Four separate silent failures were traced to this in one session, so check the mapping before assuming a track is unimplemented:
- a whitelist filter in the dispatcher dropped 92% of BgColor1's keys (`validBgColorNames`)
- `StageObjectMap` dedupes by name, so a crowd of 66 same-named objects only ever got its first member
- the Object track's `neonsign` is a `_stageObjectUnits` **unit name**, not a GameObject — no object of that name exists
- `ObjectUpdateInfo.OffsetType` was populated by the dispatcher but never read by `StageController`, so `Add`-relative positions were written as absolute

All four failed silently, with no error. Treat a track that "works" but looks wrong as a routing problem first.

**StageObjectMap:** Stage child GameObjects are indexed by name. Objects whose names match `IsTimelineControlledLight()` (`blinklight`, `spotlight3d`, `_wash_`, `laser`) start `SetActive(false)` — their handlers call `SetActive(true)` when the track fires. All other objects (neonsigns, glow meshes, UV scroll objects, etc.) keep their prefab default state.

## Ground-truth sources: what is recoverable and what is not

Mapped 2026-08-05. **Check this list before declaring something "unknown"** — several things
previously written off as un-reversible are in fact dumpable, and one whole class of problem
was solved by realising the game's own scripts can be re-bound.

### Available

| 来源 | 给出什么 | 怎么取 |
|---|---|---|
| cutt bundle TypeTree | 128 个轨道字段名 + 全部关键帧值，59 首全带 | `tools/dump_cutt_typetree.py` |
| 舞台 prefab TypeTree | GameObject 层级、组件构成，**以及缺失脚本 MonoBehaviour 的字段名和值**（离线可读） | `MetaDb().load('3d/env/live/liveNNNNN/pfb_..._controller000')` |
| shader bundle | 499 个 shader 的真实属性表（名/类型/默认值/range） | 枚举 `m_ParsedForm.m_PropInfo.m_Props` |
| material bundle | 材质的 shader 绑定与存档属性（⚠ 存档表含过期条目，见下） | `sourceresources/3d/env/live/liveNNNNN/materials/mtl_*` |
| AnimationClip | 曲线的 path / 目标属性 / 关键帧时间与值 | `m_RotationCurves` 等 |
| MonoScript | **类名 + namespace + 程序集名** | 见下，这是最重要的一条 |

### ⭐ 用原签名重建脚本，可直接拿到真实序列化字段

舞台 prefab 引用了 11 个脚本类，UmaViewer 起初只实现了 `StageController`，其余 892 个
MonoBehaviour 实例脚本为空。**但这些脚本是可以「接管」的** —— Unity 按
**类名 + namespace + 程序集名** 解析 MonoScript，而 `Assets/Scripts/umamusume.asmdef`
的程序集名正是 bundle 里写的 `umamusume`。所以只要按签名建类，字段就会被真正反序列化进来。

已验证：新建 `Gallop.Live.BillboardController` 后，`[StageScripts]` 的「脚本丢失」
从 **892 降到 728**（正好 −164 个实例），`_rotationType` 等字段直接可读。

签名清单（`Assets/Scripts/` 下任意位置均可，只要在 umamusume 程序集内）：

| namespace | 类 | live10149 实例数 | 状态 / 已 dump 到的字段 |
|---|---|---|---|
| `Gallop.Live` | `AnimationObjectController` | 282 | ❌ **别建，建了也没用**：实测序列化字段 **一个都没有**（282 个实例全空）。重建只会让「脚本丢失」892→446，拿到 0 bit 新信息 —— 它是纯行为类，播哪个 clip / 何时播 / 多快全在拿不到的方法体里。顶替版已于 2026-08-07 撤除，舞台 Animation 现在**有意不播**，见下 |
| `Gallop.Live` | `UnityLensFlareController` | 205 | ❌ 只有 1 个字段 `_enableAngleDegree`(0)，本体不在这里 |
| `Gallop.Live` | `BillboardController` | 164 | ✅ 已建（字段已读到，朝向行为待定）|
| `Gallop.Live` | `WashLightController` | 27 | ❌ 24 个字段全是具体数值 + 两张投影贴图（`_projectionTexture`/`_cameraProjectionTexture`），缺的是 URP 下的投影实现 |
| `Gallop.Live` | `LightProjection` | 4 | ❌ 只有 `_projectionType`(1) |
| `Gallop.RenderPipeline` | `CustomLensFlare` | 200 | ❌ **数据最完整、最值得做的一个**，见下 |
| `Gallop.RenderPipeline` | `MirrorBallProjector` | 4 | ❌ 字段已 dump 但**运行时绑定不可见**，见 Blocked |
| `Gallop.RenderPipeline` | `CustomProjector` | 4 | ❌ 就是一套 legacy `Projector` 参数（near .1 / far 60 / fov 30 / aspect 1 + `Material`）|
| `Gallop` | `MirrorReflection` | 1 | ✅ 已建并实现（平面镜反射，见下）|
| `Gallop.Live.ShaderParam` | `ShaderParamController` | 1 | ⚠ **不值得做**：实测只有 `_AmbientColor`/`_CharaColor` 两个向量，值都是 (1,1,1,1)，实现出来是空操作 |

**LensFlare 这条线数据是齐的**（这也是目前性价比最高的一块）：

- `CustomLensFlare` 字段：`Flare`(PPtr) / `Brightness` / `FadeSpeed` / `Color` / `IgnoreLayers` / `IsDirectional`。
- `Flare` 指向的**不是** Unity 内置 `Flare` 资产，而是 `Gallop.RenderPipeline.LensFlareData`
  这个 ScriptableObject（同样在 umamusume 程序集里，照样可以按签名重建）。
  路径形如 `sourceresources/3d/env/live/common/lensflare/flare013/flares/flare013_00`。
- 它的字段是 Unity legacy Flare 的逐字翻版：
  `TextureLayout` / `Texture` / `ElementArray[{ImageIndex, Position, Size, Color, UseLightColor,
  Rotate, Zoom, Fade}]` / `UseFog`。**所以语义不受 IL2CPP 阻塞** —— 这些是 Unity 自己的概念。
- live10149 上 199 个物件同时挂 `CustomLensFlare` + `UnityLensFlareController` + MeshRenderer，
  另有 5 个只挂 `UnityLensFlareController`。舞台上**没有任何 legacy `LensFlare` 组件**，
  原版是自己画的。URP 侧对应物是 `LensFlareComponentSRP` + `LensFlareDataSRP`。

字段名必须和 TypeTree 逐字一致（大小写敏感），dump 一下即可。
这条同样适用于任何其它 bundle 里的 MonoBehaviour。

**重建签名只拿得到数据，拿不到方法体。** 两个已建的类正好是两种情形：

- `MirrorReflection`（`Assets/Scripts/umamusume/Gallop/MirrorReflection.cs`）—— 接收端能
  自己查出来，所以能补完行为：宿主 `mirror_a` 的材质用 `Cygames/MirrorAndShadow/ReceiveMirror`，
  该 shader 只有 `_MainTex`/`_ReflectionTex`/`_Color`/`_ReflectionRate`，而组件字段
  `_mirrorReflectionColor`/`_mirrorReflectionRate` 与后两者逐字对应。实现方式是第二摄像机
  + 反射矩阵 + 斜投影渲进 RT 再写回 `_ReflectionTex`（URP 下不能用 `Camera.Render()`，
  必须挂 `beginCameraRendering` 拿 `ScriptableRenderContext` 后 `RenderSingleCamera`）。
  这同时解决了长期的「地板发白」——`_ReflectionTex` 未绑定时 Unity 代入白贴图，
  而 `_ReflectionRate` 默认 1.0，等于满强度白反射。
- `BillboardController` —— 字段读到了，**但行为写了一版又关掉了**，这是「重建签名」的能力边界。
  `_rotationType == 0` 是不是「整轴朝向摄像机」、朝向该用摄像机 up（跟镜头 roll）还是世界 up
  （只绕 Y），都没有 ground truth；实机看是「一直转来转去，而那东西不该转」，
  于是 `BillboardController.EnableRotation` 默认 false，面片保持 prefab 作者摆好的朝向。
  同一次排查里揪出一个**确定的** bug（已修）：目标摄像机只解析了一次就缓存，
  而生效摄像机随 CameraSwitcher 每帧变 —— 切换后面片朝的是观众看不见的那台，
  那台还在自己动，看上去就是无缘无故乱转。**凡是引用「当前摄像机」的舞台脚本都要注意这条。**

**舞台自带 Animation 有意不播**（283 个组件，2026-08-07 撤除了替代实现）：
它们全部 `playAutomatically = true` 但默认 clip 为空，原版靠 `AnimationObjectController`
显式 `Play()`，而那个类**零序列化字段**，纯行为，方法体拿不到 —— 「播哪个 / 何时播 / 多快」
没有任何数据能还原。写过一版 `StageAnimationPlayer` 顶替，策略是「只有一个 clip 就播」，
错在把「这物件只有一个**状态**」当成「这个状态**一直在播**」（后者正是那个缺失的类每帧做的
决定）：wash 灯 2 秒一个来回疯狂摆头（原版不扫只闪），镜面球转速无从校验。已整体撤除。
**完整理由与复活条件见 `LIVE_TRACKS.md` →「舞台自带 Animation 为什么不播」专条。**

⚠ 撤除时唯一保留下来的结论：**驱动舞台动画不要用 `Animation.Play()`**，
那跑 Unity 自己的时钟，暂停/拖动进度条都会脱节；应每帧把 `AnimationState.time` 设成
`LiveTimelineControl.currentLiveTime`（秒）再 `Sample()`。将来重做直接用这条。

### 🔎 舞台上「行为不对」的第一嫌疑：缺组件，不是解析错

**这是分诊规则，不是某一次的结论。** 舞台 prefab 引用 11 个脚本类，至今仍有 **728 个
MonoBehaviour 实例是空的**（基线 892，已补 `BillboardController` −164）。其中
`AnimationObjectController` 一家就占 282 个实例 —— 舞台上所有「什么时候动、动多快、
动哪一个」的决定原本都在它手里，而它整个不存在。

所以当某个舞台元素表现不对（该动的不动、不该动的在动、动的节奏不对），
**默认假设应该是「原版那个组件缺失，现在由我们猜的行为顶着」，而不是「数据解析错了」**。
理由是可检验的：字段值、clip 曲线、shader 属性表这些**数据**层面的东西全都能 dump 出来核对，
而且一路核对下来**基本都是对的**；反复出错的是**行为**层面 —— 那正好是拿不到方法体的部分。

已经有两个同形状的例子，都是「物件选对了、行为猜错了」：

| 现象 | 数据对不对 | 真实原因 |
|---|---|---|
| wash 灯疯狂摆头 | clip 曲线完全正确（实测 90°→0°→90°） | 缺 `AnimationObjectController`，我们替它决定了「一直播」 |
| 面片一直乱转 | 字段全部正确读到 | 缺原方法体，`_rotationType==0` 的语义是我们猜的 |

推论一：动手前先查一眼**这个物件挂过什么脚本、那个脚本我们有没有**
（`[StageScripts]` 日志 + `CLAUDE.md` 的签名清单），比直接怀疑解析快得多。

推论零：**先 dump 一眼那个缺失组件有没有字段，再决定值不值得重建。**
`AnimationObjectController` 实测**零字段**（282 个实例全空），所以重建它拿不到任何信息，
舞台动画的那些时机问题不可能靠「把原类建出来」解决 —— 这个方向是死的，
别再试。反过来 `CustomLensFlare` 字段齐全（还牵出 `LensFlareData`），就非常值得建。
判断只要一条命令，不要凭实例数多就以为收益大。

推论二：**顶替缺失组件时，要把「我在替谁做决定」写清楚**，并且区分
「从 bundle 数据推出来的」和「看参考视频观察到的」—— 后者是有效证据，但不是 ground truth，
换个舞台可能就不成立。（反面教材：舞台动画的 `_wash_` 排除规则就是这么来的，
后来连同整个替代实现一起撤掉了 —— 观察能证伪一个实现，但支撑不起一个实现。）

反例也要记住：**并非所有舞台画面问题都是缺组件。** 光柱渲染成黑墙那次，
根因是材质被盖了 URP Lit 的属性表、`_DstBlend` 从 One 变成 Zero，
把加法混合压成了不透明 —— 那是**数据**问题，而且是 dump 得出来的（见下）。
分诊规则的意思是「先查缺不缺组件」，不是「一律归咎于缺组件」。

### Blocked

**IL2CPP 元数据拿不到。** 游戏是 IL2CPP 构建，但 `global-metadata.dat` 既不在
`umamusume_Data/il2cpp_data/`（那里只有 `Resources/`），也没嵌在 `GameAssembly.dll` 里
—— 魔数 `0xFAB11BAF` 零命中，`BillboardController` / `_rotationType` 等标识符字符串
也全部搜不到。这是该游戏已知的反篡改，元数据运行时才还原。

因此以下**永远拿不到标准答案**，只能靠交叉验证或承认未知：

- **游戏自己定义的枚举语义** —— `_rotationType`(0/2)、`LightBlendMode`、`CmnColorType0/1`、
  BlinkLight 的 `pattern`、`ColorType` 等。字段值读得到，含义读不到。
  ⚠ 但**先分清是谁的枚举**：`LensFlareData.TextureLayout` 长得像这一类，其实是 Unity
  legacy `Flare` 资产的 `FlareTextureLayout`，语义在 Unity 侧公开，不受此限。
- **对象之间的运行时绑定** —— 重建签名只能拿到**数据**，拿不到原方法体，所以「这个组件
  跑起来去找谁」是不可见的。实例：`MirrorBallProjector` 的 shader 需要
  `_MirrorBallPosWS`/`_MirrorBallScale`/`_MirrorBallRotateValue`，而组件里**没有任何指向
  灯球的引用字段**，4 个 projector 全在原点、共用同一个材质，灯球却散布在 6 个位置。
  绑定关系只存在于方法体里。
- **速度/系数一类的常量** —— 如果原版在代码里对动画做了调速，那个倍率无从得知。
  （注意这条不要滥用：镜面球转速一度被记成此类，实测其实完全确定，见下。）
- 未随包发布的 shader（PostFilm 的定义性 shader 就在 app 二进制里）。

**「灯球转速」到底确定到什么程度（2026-08-07，当天两次修正）**

⚠️ **先说结论的边界**：clip 里的转速是硬数据，**但「原版按 1.0 倍速播这条 clip」不是**。
实机跑下来观察是「所有迪斯科灯球还是太快」，也就是说原版多半有个播放倍率或者压根不用
这条 clip 驱动球体 —— 那正是本节上面「速度/系数一类的常量」说的那类不可知量。
下面的数字只证明「**我们忠实地播了这条 clip**」，不证明「和原版一致」。

已确定的部分：

- live10149 的 4 个 `MirrorBallProjector` 实例**全部** `MirrorBallIsLoopRotation = 0`、
  `MirrorBallLoopRotationSpeed = 0`、`MirrorBallLoopRotationValue = 0` ——
  shader 自带的 loop-rotation 通路在这个舞台上是**关掉的**，不是转速来源。
- 真正在转的是 clip `anm_env_live10149_blinklight_mirrorball_flarelight_loop_000`：
  6 条 quaternion 曲线（`mirrorball_a_000` + `b_000..004`），7 个关键帧，
  **恒定 −179.50°/s 绕 Y**（每个关键帧算出来都是这个数），2.0 s 一圈，wrapMode = Loop。
  末帧 −359° 而不是 −360°，是为了循环接缝不重复一帧。
- 曾接管过这条 clip，采样用的 `currentLiveTime` 单位是秒（帧号 = ×60），**没有单位换算错误**，
  放出来就是 clip 写的 2.0 s 一圈 —— 但实机仍然偏快，这正是下面第 2 条教训的由来。
  该实现已于 2026-08-07 撤除，球现在不转。

两条教训，方向相反，都要记：

1. 把「值是 0」当成「值未知」是两回事 —— dump 之前先看一眼开关字段
   （`MirrorBallIsLoopRotation` 就是那个开关）。
2. **但「clip 的转速是确定的」推不出「转速问题解决了」**。我上一版文档写成了
   「我们的构建里转速本来就是对的」，实机观察当天就否掉了它。
   数据确定的是**素材**，不是**原版怎么用这个素材**；中间那个倍率仍然是不可知量。

### ⚠ 现象描述不是 ground truth

字段值、shader 属性表是硬数据；**「哪个物件看起来不对」不是** —— 文档里的现象描述往往
没经过验证。地板发白排查栽在这上面：文档记着 `plane_000` / `stage_object_001` /
`specular_002` 发白，整轮排查都围着这三个转，最后查明白的其实是**叠在同一位置的
`mirror_a`**（同在 `pfb_env_live10149_main000` 下、局部变换全为单位，肉眼分不出）。
它用 `Cygames/MirrorAndShadow/ReceiveMirror`，`_ReflectionRate` 默认 1.0 而
`_ReflectionTex` 从没赋值 —— Unity 对未绑定采样器代入白贴图，满强度白反射。

期间那个「把 `_EnvRate` 归零无变化」的实验因此也是误导：切的是另一批物件。

**排查视觉问题的第一步应该是确认现象归属于哪个物件**（让代码把可疑物件隔离/高亮），
而不是从文档里的名字出发。

### 间接手段（已被证明有效）

拿不到直接答案时，**相关性交叉验证**多次奏效，比猜测可靠得多：

- BlinkLight 调色板槽位规则：靠「物件名 `lightNNN_`/`alphaNNN_` 前缀的种数」与
  「调色板里去重颜色数」在**每一组都精确相等**推出，7 组无一例外。
- BgColor1 的通道 `_MulColor0`：靠枚举 shader 属性表 + 排除法确认。
- 「某属性存在」≠「目标 shader 上有该属性」—— `_Color` 空写连续栽了三次
  （BlinkLight / Spotlight3d / UVScrollLight），扫一遍 shader 属性表即可批量排查。

## Working rule: no ground truth, no implementation

Field names, keyframe values and shader property tables are all recoverable from the bundles
(`tools/`), so **verify before you write code** — and when something genuinely cannot be
recovered, leave it unimplemented with a TODO rather than guessing a value.

Guessing has cost real time in this project: the PostFilm vignette geometry was guessed twice
(full-screen wash, then a huge border) before it turned out the defining shader isn't shipped in
any bundle. `PostFilmRendererFeature._enableRendering` is now `false` for exactly this reason.

Note the distinction that has caught me out: *"the property exists"* is not *"this field maps to
that property"*. `_ColorPower` is present on the stage shaders, but there is no evidence that
BgColor1's `power` field drives it — so that write was reverted while BlinkLight's was kept
(there the shader `LightBlinkBlend` is named for the track and pairs `_BlinkLightColor` with
`_ColorPower`, which is a much tighter fit).

Open items and why each is blocked are tracked in `LIVE_TRACKS.md` → 「已知未解问题」.

## Currently Implemented Tracks

| Track | Data class | Notes |
|-------|-----------|-------|
| Camera (pos/lookat/fov/roll/switcher) | existing | fully working |
| CharaMotionSequence | existing | |
| Facial / LipSync / FormationOffset | existing | |
| GlobalLight (48) | existing | sets rim light shader props on characters |
| BgColor1 | existing | **two branches.** Groups named `CharaCenter/CharaLeft/CharaRight/CharaColor` (`LiveTimelineControl.CharaBgColorNames`) → character toon props (`_CharaColor`, `_ToonDarkColor`, …). Every other group name → stage renderers, resolved by walking the whole stage hierarchy for same-named Transforms (`Director.ResolveStageTargets`), writing **`_MulColor0`** via `MaterialPropertyBlock`. |
| BgColor2 | existing | sets `_AmbientColor` on stage renderers via `Lerp(color1, color2, value)` |
| Transform / Object | existing | handled by `StageController.cs` |
| Effect (60) | `LiveTimelineEffectData` | loads prefab from `3d/effect/live/pfb_{name}` |
| GlobalFog (49) | `LiveTimelineGlobalFogData` | sets `RenderSettings.fog*` |
| Spotlight3d (68) | `LiveTimelineSpotlight3dData` | looks up `keyData.assetName` in StageObjectMap |
| UVScrollLight (46) | `LiveTimelineUVScrollLightData` | sets texture UV on stage materials |
| VolumeLight (37) | `LiveTimelineVolumeLightData` | data deserialized, no visual (component absent) |
| LightShafts (50) | `LiveTimelineLightShaftsData` | data deserialized, no visual (component absent) |
| Particle (41) | `LiveTimelineParticleData` | sets ParticleSystem.emission.rateOverTime |
| ParticleGroup (42) | `LiveTimelineParticleGroupData` | sets FlickerLightRate |
| ChromaticAberration (73) | `LiveTimelineChromaticAberrationData` | URP `ChromaticAberration.intensity` ← `power`；per-channel offset 无法映射 |
| HdrBloom (38) | `LiveTimelineHdrBloomData` | **no handler, deliberately** — 0/58 songs carry data (`hdrBloomKeys` empty in every scan and in the song-1177 dump), and the field→URP-`Bloom` mapping was never verifiable. Data layer (data class, worksheet field, event) is kept; the Director handler and the `Bloom` volume override were removed 2026-08-07. |
| ColorCorrection (61) | `LiveTimelineColorCorrectionData` | URP `ColorAdjustments.saturation` + `ColorCurves` RGB；depth/blend curve 无 URP 对应 |
| BlinkLight | `LiveTimelineBlinkLightData` | SetActive + writes `_BlinkLightColor`/`_ColorPower` on the group's renderers (56/58 songs). **Biggest lighting payload by far** — 全语料 19420 keys / 57 songs, and over half are named `*_wash_*` (`wash_truss_a` 147, `wash_truss_b` 147, `wash_ground_a` 51, …). Palette slot = the `lightNNN_`/`alphaNNN_` prefix of the renderer or its nearest ancestor; renderers with no prefix are left to their real owner (BgColor1) when the group has slotted ones. `color1Array` / `LightBlendMode` / `isReverseHueArray` still unused. |
| WashLight | `LiveTimelineWashLightData` | SetActive only (5/58 songs). Low value: real "wash light" output comes from the BlinkLight `*_wash_*` groups above, not this track — song 1177 has just 1 group / 11 keys here. |
| Laser | `LiveTimelineLaserData` | SetActive + position/rotation/scale — blink/raycast not implemented (6/58 songs) |

## Track Coverage (from 58-song scan)

**The pre-2026-08-04 scan data in `LIVE_TRACKS.md` was wrong** and has been re-measured with `tools/`. Two systematic errors: a batch of tracks marked "WorkSheet 无 X 字段" do have the field (`MobControlKeys`, `CyalumeControlKeys`, `nodeScaleList`, `sweatLocatorList`, `monitorCameraPos/LookAtKeys`, `facialNoiseKeys`, `charaMotionNoiseKeys`, …), and a batch marked "全 0 keyframe 空占位" carry thousands of keys (`postFilmKeys`/`postFilm2Keys`/`postFilm3Keys` ≈20 200 keys over 59/59 songs, `radialBlurKeys`, `tiltShiftKeys`, `charaFootLightKeys`, …). Rows tagged ✅实测 are trustworthy; untagged rows are still old data. **Re-verify with `tools/` before acting on any untagged row.**

Coverage numbers measure **what exists in the bundles**, not what the C# consumes — the two diverge. When a track looks under-powered, check the dispatcher in `AlterUpdate_*` for filters before assuming it's unimplemented (BgColor1 was dropping ~92% of its keys that way).

**Measuring what a song actually carries**: `LiveTimelineWorksheetDiag.Dump()` is called from `Director.InitializeTimeline()` and logs, per worksheet, every track field with its group names and key counts. Confirmed on song 1177: `worksheetList.Count == 1`, `SheetType == MainLive`, so reading `worksheetList[0]` loses nothing.
