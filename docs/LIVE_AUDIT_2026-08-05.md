# ✅ 轨道审计报告 — 2026-08-05

对 `LIVE_DEV_MAP.md` 中标记为 ✅「已完成」的轨道做实现审计。
所有结论都用 `tools/` 从 bundle 里取了 ground truth，未验证的猜测不写进来。

结论：**4 条标 ✅ 的轨道实际上不工作或工作错误**，另有一组性能问题。
它们全部属于同一个已知家族——数据解析正确，然后在下游被静默丢弃，无任何报错
（和 commit `a3a7b83` 修的六条是同一形状）。

## 当前状态

| # | 问题 | 状态 |
|---|------|------|
| A1 | GlobalLight 被 BgColor1 每帧清空（59/59 首） | ✅ 已修 |
| A2 | BgColor2 丢弃组名（15 首 / 14346 keys） | ⚠️ 部分修复 —— 组名已接通、性能已修；**组名到底指向什么仍无 ground truth**，解析不到时退回原全舞台语义并打警告 |
| A3 | Spotlight3d 写 `_Color` 是空操作（31 首） | ✅ 已修 |
| A4 | Transform 只认 unit 名（12 首 / 681 keys） | ✅ 已修 |
| A5 | UVScrollLight 写 `_Color` 是空操作（**同一 bug 第三例**） | ✅ 已修。目标材质实测是 `LightAdd1_UV` / `StageLightAdd1_UVAlphaMask_TransmittedLightMask`，通道为 `_MulColor0`/`_MulColor1`/`_ColorPower`；顺带补上一直没用的 `mulColor1`，并改 MPB + 缓存（原先每帧遍历全舞台 + `r.materials`）|
| B1–B4 | 每帧 `r.materials` / 全场景遍历 / 每帧 new MPB | ✅ 随 A1–A3 一并修掉（Wash/Laser 的 `r.materials` 仍在，属 ⚠️ 轨道，未动） |
| C1 | GlobalFog 把高度雾当距离雾 | ❌ 未动，需先做语料统计定方案 |
| C2 | 初始化时同名子物件只有第一个被关，且没人再打开 | ✅ 已修（**前两稿的诊断都是错的**，见下文） |
| C4 | 舞台地板 / 高光面发白 | ✅ 已解决 —— 但**排查对象从头到尾都错了**：白的是叠在同位置的 `mirror_a`，不是 `DefaultEnvMapNoAmbient` 那三个物件。实现 `Gallop.MirrorReflection` 后正常。诊断用的 `StageEnvMapDiag`/`StageEnvRateToggle`（F9）验证假设不成立后已于 2026-08-07 删除 |
| C3 | ParticleGroup 覆盖 Particle | ❌ 未动，优先级低 |

**均未经实机验证** —— 环境里没有 Unity，也没有 C# 编译器，只做了括号配平和符号引用检查。
下面每条的证据和改法都写在原处。

### 哪首歌能验哪条

本地目前只下载了 **1177** 的 live 数据。1177 里有 BgColor1(20 组) / BlinkLight / Object /
Effect / GlobalLight / GlobalFog / PostFilm，但**下面三条轨道在 1177 里是空的**，
改了也看不到任何效果：

| 修复 | 1177 能验？ | 需要的歌（按 keyframe 数排序） |
|------|------------|------------------------------|
| A1 GlobalLight | ✅ **能** | 1177 有 2 组 / 125+24 keys |
| A2 BgColor2 | ❌ 1177 无数据 | son1006 (4338) · son1009 (3446) · son1007 (3308) |
| A3 Spotlight3d | ❌ 1177 无数据 | son1081 (122) · son1048 (103) · son1051 (95) |
| A4 Transform | ❌ 1177 无数据 | son1028 (206) · son1009 (131) · son1027 (129) |

A1 在 1177 上的判据见下方「A1」条目末尾。注意 1177 的 rim **颜色和宽度几乎不变**
（`rimColor` RGB 125 帧只有 8 个值且基本同色，`rimFeather`/`rimSpecRate` 全程恒定），
真正在动的是 `rimColor.a`（0.392→1.0）和 `lightDir`（125 帧 102 个不同值）。
所以「看不出在动」是正常的，要盯**亮边在剪影上的位置**而不是颜色。

查某首歌带哪些轨道：`~/.venvs/umatools/bin/python3 -c "import csv; ..."` 读
`out/scan_keys.csv` 对应列即可（该 CSV 是全 59 首的宽表，一行一首）。

---

## A. 功能性 Bug（标 ✅ 但实际不工作）

### A1. GlobalLight (48) 被 BgColor1 每帧清空 —— 全语料 59/59 首受影响

**证据**

```
LiveTimelineControl.cs:364   AlterUpdate_GlobalLight(...)
LiveTimelineControl.cs:365   AlterUpdate_BgColor1(...)      ← 紧接着跑
```

两个 handler 都对**同一批** `container.Renderers` 做整块替换：

```csharp
// Director.cs:347   OnGlobalLightUpdate
MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
propertyBlock.SetFloat("_RimShadowRate", ...);   // 共 13 个 rim 属性
foreach (var renderer in container.Renderers)
    renderer.SetPropertyBlock(propertyBlock);

// Director.cs:413   OnBgColor1Update（角色分支）
MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
propertyBlock.SetColor("_CharaColor", ...);      // 只有 5 个属性
foreach (var renderer in container.Renderers)
    renderer.SetPropertyBlock(propertyBlock);     // ← 整块替换，不是合并
```

`Renderer.SetPropertyBlock()` 是**替换**语义，不是合并。BgColor1 在 GlobalLight
之后跑，于是每一帧 GlobalLight 刚写进去的 13 个 rim 属性立刻被抹掉，回落到材质默认值。

歌曲 1177 实测同时存在两者：

```
bgColor1 groups (20): ['BgBL', 'CharaColor', 'CharaColor', 'Shadow', ...]
globalLight groups:   ['GlobalLight', 'GlobalLight']
```

覆盖率：`globalLightDataLists` 9636 keys / 59 首，`bgColor1List` 43617 keys / 59 首。
两条轨道在**每一首歌**里都同时有数据，所以这个 bug 是全语料生效的。

**方案** —— 改成读-改-写，并复用一个常驻 block（顺带干掉每帧 new）：

```csharp
private MaterialPropertyBlock _charaBlock;

// 两个 handler 共用
private void WriteCharaBlock(Renderer r, Action<MaterialPropertyBlock> fill)
{
    _charaBlock ??= new MaterialPropertyBlock();
    r.GetPropertyBlock(_charaBlock);   // 先取回渲染器上已有的块
    fill(_charaBlock);
    r.SetPropertyBlock(_charaBlock);
}
```

`GetPropertyBlock` 会用渲染器当前的块内容覆盖填充传入的 block，所以两条轨道各写各的
属性、互不清除。

---

### A2. BgColor2 (9) 完全忽略组名，10 个组互相覆盖 —— 15 首 / 14346 keys

**证据** —— 歌曲 1006 的组名（4338 keys，语料内最多）：

```
bgColor2List -> ['BgWashA','BgWashB','BgWashC','BgWashD','BgWashE',
                 'BgWashF','BgWashG','BgWashH','BgWashI','BgWashJ']
```

10 个独立的 wash 分区。但：

```csharp
// LiveTimelineControl.cs:1879  —— TimelineNameHash 字段存在，dispatcher 从不填
BgColor2UpdateInfo updateInfo = default;
updateInfo.color1 = ...;  updateInfo.color2 = ...;  updateInfo.value = ...;

// Director.cs:664  —— handler 也从不读名字，直接刷整个舞台
foreach (var r in _stageController.GetComponentsInChildren<Renderer>())
    foreach (var mat in r.materials)
        if (mat.HasProperty("_AmbientColor"))
            mat.SetColor("_AmbientColor", c);
```

后果是双重的：每帧 10 个组按顺序把**整个舞台**刷一遍，**只有最后一个 `BgWashJ` 留下来**，
而且它会把本该只属于 J 区的颜色涂到所有分区上。前 9 个组的 3900 多个 key 等于不存在。

这和 `a3a7b83` 修掉的 BgColor1 `validBgColorNames` 白名单是同一类错误——组名路由缺失。

**已做**

1. `BgColor2UpdateInfo` 补了 `TimelineName`，dispatcher 从 `sheet.bgColor2List[i].name` 填。
   （组名一直是可读的 —— 它继承自 `ILiveTimelineGroupDataWithName` 基类，只是从没被传下去。）
2. handler 改走 `ResolveStageTargets(key, kStageBgColor2Props)`，通道 `_AmbientColor`，
   首帧解析一次并缓存，写入用 `MaterialPropertyBlock`。B1/B2 一并解决。

**没做，也不该猜：组名到底指向什么**

全语料 15 首里出现过的组名只有这些：

```
BgWashA..BgWashO   LaserA/B/C   BgColor2
```

已经排除了两种可能：

- **不是 GameObject 名** —— 舞台层级里没有同名对象。
- **不是 unit 名** —— 已下载的三个舞台（live10132 / 10149 / 10151）的
  `_stageObjectUnits` 分别是 `blinklight_washlight_wall_a_vertical_000_set`、
  `neonsign`、`stage000_shop` 这类具体名字，没有 `BgWash*`。

也查过 BlinkLight 的键里有没有回指 BgColor2 的索引字段 —— 没有
（有 `CmnColorType0Array`/`CmnColorType1Array` 两个 int 数组，语义未知，可能相关但无证据）。

所以它和 BgColor1 的 `BgBL`/`FollowSpotColor`/`Shadow` 属于同一类「非物件名的组」，
映射未知。当前实现：**名字能解析到渲染器就只写那些；解析不到就退回原来的全舞台写入
并打一次警告**。行为不会比改动前更差，映射查明后删掉 fallback 分支即可。

---

### A3. Spotlight3d (68) 的颜色写入是空操作 —— 31 首 / 1089 keys

```csharp
// Director.cs:705
mat.SetColor("_Color", keyData.color);        // ← 没有任何舞台 shader 有 _Color
mat.SetFloat("_ColorPower", keyData.colorPower);   // ← 这个是有的，会生效
```

**证据** —— 枚举 `shader` bundle 里全部 133 个 `Gallop/3D/{Live,Bg,Stage}` shader，
暴露 `_Color` 的只有两个，都和聚光灯无关：

```
Gallop/3D/Bg/BgShadowOnly
Gallop/3D/Bg/RedAlphaGreenColorShadowFogUVScroll
```

`Gallop/3D/Live/Stage/*` 里**一个都没有**。灯柱用的是 `StageBeamLight` 系列：

```
Gallop/3D/Live/Stage/StageBeamLight          ['_MulColor0','_MulColor1','_ColorPowerMultiply','_ColorPower']
Gallop/3D/Live/Stage/StageBeamLightCutoff    (同上)
Gallop/3D/Live/Stage/StageBeamLightFadeout   (同上)
```

这就是 `a3a7b83` 里 BlinkLight 写 `_Color` 那个 bug 的同一份，只是当时没顺手扫到
Spotlight3d。因为 `_ColorPower` **确实生效**，聚光灯现在的表现是「材质原色 × 关键帧亮度」——
亮度会跟着音乐动，颜色永远不对。这正是那种「看着能用但不对」的路由问题。

**方案** —— 照搬 `OnBlinkLightUpdate` 已经写好的 shader 探测逻辑：按
`_MulColor0` → `_MulColor1` → `_BlinkLightColor` 的顺序问 `Shader.GetPropertyType`，
写命中的那个，`colorPower` 单独进 `_ColorPower`（**不要折进颜色里**，遵守 CLAUDE.md 的规则）。

顺带确认过 key 结构是对的，不用动数据层：`assetName: "spotlight3d000"`、
`characterIndex: -1`，C# 的 `LiveTimelineKeySpotlight3dData` 字段顺序与 TypeTree 完全一致。

---

### A4. Transform (31) 只认 unit 名，GameObject 名整条轨道静默失效 —— 12 首 / 681 keys

```csharp
// StageController.cs:186   UpdateTransform
if (StageObjectUnitMap.TryGetValue(updateInfo.data.name, out StageObjectUnit objectUnit))
{ ... }
// 没有 else —— 组名不是 unit 名就什么都不做，也不报错
```

**证据** —— 歌曲 1028（transformList 语料最多，206 keys）第一个组名：

```
name: "light001_glow_001"
position: (-2.0, 0.5, 0.0)   scale: (0.2, 0.2, 0.2)
```

这是 GameObject 命名风格，不是 `neonsign` 那种 unit 名。

这是 `a3a7b83` 修的 Object 轨道 bug 的**镜像**：那次是 handler 只查 `StageObjectMap`、
漏了 unit 名；这次是只查 `StageObjectUnitMap`、漏了 GameObject 名。同一个函数对里，
`UpdateObject` 两种都查，`UpdateTransform` 只查一种。

同一处还有一个次要缺陷：

- `StageObjectMap.TryGetValue(child.name, ...)`（:190）没有 `.Replace("(Clone)","")`，
  而 `UpdateObject` 有（:96）——map 里存的是剥掉 `(Clone)` 的名字。

> **更正**：初稿说这里「完全无视 `OffsetType`」，这条是错的。
> `--tree transformList 1028` 实测 `LiveTimelineKeyTransformData` 只有
> `position` / `rotate` / `scale` 三个字段，**没有 OffsetType**（和 Object 轨道不同），
> C# 类和 TypeTree 完全一致。所以绝对写入本来就是对的，不需要 Add 相对语义。

**方案** —— 先查 `StageObjectUnitMap`，miss 再查 `StageObjectMap`（剥 `(Clone)`），
TRS 抽成一个 `ApplyTransformTrs` helper 供两个分支共用。

---

## A5 补记：`_Color` 空写的系统性排查

同一个 bug 出现三次（BlinkLight → Spotlight3d → UVScrollLight）之后，做了一次全量对照，
以后加新的属性写入可以照跑一遍当回归检查：

```bash
# 1. 抽出 Director 里写过的所有 shader 属性名
grep -oE 'Set(Color|Float|Vector|Texture|Int)\("_[A-Za-z0-9_]+"' Director.cs \
  | sed 's/.*("//;s/"//' | sort -u > /tmp/props.txt

# 2. 拿 499 个 shader 的属性表逐个核对（见本文件末尾的 MetaDb 用法）
#    重点不是「这个属性存不存在」，而是「它存在于**目标那一族** shader 上吗」
```

2026-08-05 的结果（23 个属性名）：

| 属性 | 拥有它的 shader | 判断 |
|---|---|---|
| `_Color` | 152 个，**舞台/Bg 只有 2 个**（`BgShadowOnly`、`RedAlphaGreenColorShadowFogUVScroll`，都与灯光无关）| ❌ 就是这条暴露了 UVScrollLight |
| `_ProjectorColorPower` | 7 个，其中 6 个是舞台 | ✅ WashLight 的写入是对的 |
| rim / toon 那批（`_RimColor`…`_CharaColor`）| 100+ 个角色 shader，舞台 0 个 | ✅ 符合预期 |

**教训**：「这个属性存在」不等于「目标 shader 上有这个属性」。
三次都是栽在这一步 —— 属性名在别的 shader 家族里是真的，于是看起来很合理。

---

## B. 性能 / 一致性（不改变画面，但每帧都在烧）

| # | 位置 | 问题 |
|---|------|------|
| B1 | `Director.cs` 668-669 / 702-703 / 719 / 365 | `Renderer.materials` 每帧调用。这个 getter **每次都实例化材质副本并新分配数组**，彻底破坏批处理。`a3a7b83` 已经为 BlinkLight 修过（改 MPB），BgColor2 / Spotlight3d / UVScrollLight / GlobalLight 四处漏网。BgColor2 最严重：最多 15 个组 × 全舞台渲染器 = 每帧上千次材质实例化 |
| B2 | 同上 + `OnParticleUpdate` / `OnParticleGroupUpdate` | `GetComponentsInChildren<Renderer/ParticleSystem>()` 每帧全场景遍历 + 分配。应照 `_bgColor1StageCache` 的做法首帧解析一次 |
| B3 | `Director.cs` 668 / 977 / 988 | 漏了 `true` 参数 → **跳过 inactive 对象**。而 `IsTimelineControlledLight` 命中的灯全部以 inactive 起步，UVScrollLight(:717) 和 Spotlight3d(:702) 传了 `true`，这几处没传。同一份代码里两种写法，是不一致而不是有意设计 |
| B4 | `Director.cs` 347 / 413 | 每个 locator 每帧 `new MaterialPropertyBlock()`。A1 的修法顺带解决 |

---

## C. 语义存疑（有证据但需要更多 ground truth 才能定方案）

### C1. GlobalFog (49)：把高度雾当成距离雾在做

歌曲 1177 的 key 实测：

```json
"isDistance": 0,  "isHeight": 1,  "height": 15.8,  "heightDensity": 0.03,
"fogMode": 2,     "expDensity": 0.02,  "start": 0.0,  "end": 300.0
```

即「**不要**距离雾，要一层 y=15.8 的高度雾」。而 handler：

```csharp
RenderSettings.fog = keyData.isDistance || keyData.isHeight || keyData.fogMode != 0;  // → true
RenderSettings.fogMode = (FogMode)keyData.fogMode;      // → Exponential，枚举值没问题
RenderSettings.fogDensity = keyData.expDensity;         // 距离雾密度
```

于是数据说「没有距离雾」，画面上却出现了距离雾。`height` / `heightDensity` /
`startDistance` 三个字段完全没用上。

`(FogMode)2` = `Exponential` 是合法的，枚举转换本身**没有**越界问题——问题只在开关条件。

Unity 的 `RenderSettings` 没有高度雾，做不了 1:1。两条路：

- **保守**：`RenderSettings.fog = keyData.isDistance != 0;`，高度雾留 TODO。
  代价是 1177 这类只有高度雾的歌会完全没有雾——比现在错误的雾更接近真相，但视觉上更空。
- **正确**：高度雾需要自写 URP RendererFeature，和 PostFilm 同一档工作量。

建议先跑一遍 41 首有 globalFog 的歌，统计 `isDistance` / `isHeight` 的分布再决定，
这个统计 `tools/` 一条命令就能出。

### C2. `StageObjectMap` 去重让重名灯的第 2..N 个永远关不掉

```csharp
// StageController.cs:60
if (!StageObjectMap.ContainsKey(child.name))       // ← 用未剥 (Clone) 的名字判重
{
    if (IsTimelineControlledLight(child.name))
        child.gameObject.SetActive(false);
    var tmp_name = child.name.Replace("(Clone)", "");
    StageObjectMap.Add(tmp_name, child.gameObject); // ← 存剥掉的名字
}
```

`SetActive(false)` 在判重的 `if` **里面**，所以同名对象只有第一个被关掉。
另外判重用 `child.name`、插入用 `tmp_name`，两个键不一致。

> **这条我连着猜错了两次，实测数据两次都把方案推翻了。记在这里当反面教材。**
>
> **初稿的说法**：「一组 66 个同名 `wash_truss_a` 只有第一个被关，其余 65 个常亮」。
> **错。** 那个 66 是观众 `mob_a000` 的数量（BgColor1 的目标），不是灯。
> live10149 上重名的受控灯只有 4 个名字 / 10 个对象。
>
> **第二稿的说法**：「要先给三个灯光 handler 加 name→List 支持，否则移动 SetActive
> 会让 65 盏灯全灭」。**也错。** 实测 1177 的 28 个 BlinkLight 组名**全部一一对应
> 一个 GameObject**（27 命中 + 1 个 `wash_discoball` 无对应物件），组名指向的是**容器**，
> 真正的灯是子物件，`GetComponentsInChildren` 早就覆盖了。所以 name→List 完全没必要。

**真实的 bug，方向和前两稿都相反**

重名的灯全是容器的**子孙**：

```
light001_wash_ground_a  < swing_wash_ground_a_003 < wash_ground_a_000_l_003_set
                        < pfb_env_live10149_blinklight_wash_ground_a   ← 轨道寻址的是这个
```

容器名含 `blinklight` → 被关掉（正确，等 handler 打开）。
子物件名含 `_wash_` → **只有第一个**被关掉，而 handler 只 `SetActive(true)` 容器，
那第一个子物件再没有人打开 —— **全程是黑的**。

live10149 上的受害者：`light001_wash_ground_a`(×4)、`swing_wash_ground_a`(×2)、
`base_rotate_wash_ground_a`(×2)、`light000_wash_ground_a`(×2)，各有 1 盏常黑，共 4 盏。

**已修** —— 只关「本分支里最上层」的那盏受控灯：祖先已经是受控灯的就跳过，
跟着祖先一起显隐。容器照常关，子物件保持 prefab 状态。判重键也统一成 `tmp_name`。
纯增益，没有「某盏灯从此不亮」的倒退风险 —— 因为不再有子物件被单独关掉。

### C4. 舞台地板发白 —— ✅ 已解决，但**我整段都在查错的物件**

**结论**：发白的是 `mirror_a`，它用 `Cygames/MirrorAndShadow/ReceiveMirror`，
该 shader 的 `_ReflectionRate` 默认 **1.0**，而 `_ReflectionTex` 从来没有人赋值 ——
**Unity 对未绑定的贴图采样器代入白贴图**，于是满强度白色反射 = 一块纯白地板。
实现 `MirrorReflection`（把平面反射 RT 接上 `_ReflectionTex`）之后，地板变成了正常镜面。

**我错在哪**：CLAUDE.md 记着「地板 `plane_000` / `stage_object_001` / `specular_002` 发白」，
我**直接采信了这个前提**，整段排查都围绕这三个物件（它们用 `DefaultEnvMapNoAmbient`），
从没验证过屏幕上白的那块到底是哪个物件。

`mirror_a` 与 `plane_000` **同位同尺度**（同在 `pfb_env_live10149_main000` 下，
局部变换全为单位），是叠在一起的两层地面 —— 肉眼分不出，名字却完全不同。

这也解释了那个 F9 实验为什么"无变化"：**我切的是另一批物件的 `_EnvRate`**，
自然影响不到 `mirror_a`。当时我把这个结果当成"env 反射项无关"的证据，
其实它只说明"那三个物件不是白的来源"。

**教训**：文档里的现象描述（"哪个物件看起来不对"）和字段值、shader 属性表不一样，
**它没有经过验证**。排查前先确认现象归属于哪个物件，再去查那个物件。
下次遇到"某某看起来不对"，第一步应该是让代码把可疑物件高亮/隔离，而不是从名字出发。

以下为定位过程的记录（结论已被上面推翻，保留是因为排除法本身有效）。

### C4-旧. 曾经的排查路径

CLAUDE.md 长期记着「地板 `plane_000` / `stage_object_001` / `specular_002` 偏亮发白，
早于时间轴的改动就存在」。这次查清了它们的共同点。

**已确认（bundle ground truth）**

三个物件用的是**同一个** shader，而且是舞台 shader 里唯一带环境反射的一族：

```
Gallop/3D/Live/Stage/DefaultEnvMapNoAmbient
  真实属性表（只有 8 个）：
    _MainTex  _MulColor0  _ColorPower  _AddColor
    _TripleMaskMap  _EnvMap  _EnvRate(Range 0..1, 默认 0.5)  _EnvBias(Range 0..8, 默认 1)
```

| 物件 | 材质 | `_EnvRate` | `_EnvBias` |
|---|---|---|---|
| `plane_000` | `mtl_env_live10149_default_env001` | **0.75** | 2.0 |
| `stage_object_001` | `mtl_env_live10149_default_env002` | **1.00** | 2.0 |
| `specular_002` | `mtl_env_live10149_glitter002` | **1.00** | 5.0 |

三个材质的 `_EnvMap` 在数据里都是**绑定了**的（指向其它 bundle，`m_FileID` 1/2，不是 null）。

顺带印证了 CLAUDE.md 那条警告：这三个材质的 `m_SavedProperties` 里还留着一大堆
URP Lit 的属性（`_WorkflowMode`、`_Smoothness`、`_ClearCoatMask`、`_BlendModePreserveSpecular`…），
而当前 shader 一个都没声明。**存档表不可信，必须问 shader。**

**实机日志结果：贴图缺失假设被证伪**

```
[EnvMapDiag] DefaultEnvMapNoAmbient：3 个渲染器，其中 _EnvMap 为 null 的有 0 个
  plane_000        mat=mtl_env_live10149_default_env001  _EnvMap=tex_env_live10149_reflection000 _EnvRate=0.75 _EnvBias=2.00
  specular_002     mat=mtl_env_live10149_glitter002      _EnvMap=tex_env_live10149_reflection001 _EnvRate=1.00 _EnvBias=5.00
  stage_object_001 mat=mtl_env_live10149_default_env002  _EnvMap=tex_env_live10149_reflection000 _EnvRate=1.00 _EnvBias=2.00
```

贴图全部解析成功，不是缺贴图。假设作废。

**已排除的三条**

| 排除项 | 依据 |
|---|---|
| 光照太强 | 舞台 `Light` 组件 0 个，Live 代码不碰 `Light` |
| `_EnvMap` 缺失 | 上面的日志，0 个 null |
| BgColor1 的 `power` 没写进 `_ColorPower` | 三个组的 `power` **中位数都是 1.000**（范围 0.09–1.0，13 个不同值）。就算写了，绝大多数时间也是乘 1，**解释不了持续发白**。这条负面结果反过来支持了当初撤回该写入的决定 |

**候选 A（证据较强）：env 反射项不受颜色通道调制**

这是一个干净的对照：

- 发白的 3 个物件 = 全场仅有的 3 个用 `DefaultEnvMapNoAmbient` 的物件。
- 它们的 `_MulColor0` 被 BgColor1 写成**接近黑**：1177 开场是 `(0.06, 0.06, 0.08)`，
  257 个关键帧 44 种颜色，确实在动。
- 同样由 BgColor1 写同一个 `_MulColor0` 通道、但用 `DefaultNoAmbient`（无 env）的
  `mob_a000`(50 renderer) / `mob_b000`(57) / `truss_neonsign` 等**没有发白问题**。

同一个写入者、同一个通道，唯一变量是 env 路径 —— 颜色都压到近黑了还是亮，
说明亮的那一项**不经过 `_MulColor0`**。

~~可证伪预测：`plane_000` 的 `_EnvRate` 是 0.75、另两个是 1.0，所以 `plane_000` 应当
明显比另外两个轻。如果三者一样白，候选 A 也要丢掉。~~

> **这个检验设计得不好，实测「看不出差异」并不能否定候选 A。**
> 0.75 与 1.00 只差 25%，如果反射项本来就超过 1.0 被钳到白，两者会一样白。
> 也就是说这个对比无论结果如何都区分不了 —— 它不是一个有效的可证伪检验，是我把它说过头了。
>
> 真正二元的检验是把 `_EnvRate` **直接归零**：
> 发白消失 → 就是 env 反射项；发白照旧 → env 路径无关。
> 已实现为 `StageEnvRateToggle`（按 **F9** 在「原始值 ↔ 0」间来回切，走 MPB 不动材质，
> 随 `StageEnvMapDiag.Dump()` 自动挂上）。
>
> **2026-08-07：这两个诊断类已删除。** 判决实验做完了、结论是「env 路径无关」，
> 而真正的成因（`mirror_a`）也已查明并修复，它们没有留下来的理由 ——
> 留着只会让每次载入舞台都多跑一遍无用的全场景遍历，还挂一个 F9 热键。

**候选 B（可能相关，未证实）：缺失的平面镜面反射系统**

Environment (58) 轨道（1375 keys / 49 首，MAP 记为「Planar Reflection 完全没有」）在 1177 带着：

```
isValidMirror: 1   isBgMirror: 1   mirrorReflectionRate: 0.134
```

反射强度 0.134，而材质里是 0.75–1.0。**但 `mirrorReflectionRate` → `_EnvRate` 没有任何证据**：
前者属于平面镜系统（第二摄像机 → RenderTexture），后者是静态 env 贴图的混合率，
是两套东西。Environment 的 key 里也没有任何直接对应 env map 的字段（其余全是 water/fov）。
所以这条只当线索，不当结论。

**判决实验结果：候选 A、B 均被否定**

F9 实测。日志确认开关确实生效（9 次切换 × 3 个渲染器，`[EnvMapDiag] _EnvRate → 0/原始值`），
**画面无变化**。所以 env 反射项不是发白的来源，候选 A 作废；候选 B 依附于同一表面的
反射设想，一并降级。

**同时发现：地板本来就该是亮的**

之前只看了第 0 帧的 `(0.06, 0.06, 0.08)` 就默认"数据要求地板是暗的"，这是取样偏差。
全曲 257 个关键帧的亮度分布（Rec.601 luma）：

| | plane_000 |
|---|---|
| 中位数 | **0.548** |
| 均值 | 0.505 |
| > 0.3 | 207 / 257（81%）|
| > 0.5 | 133 / 257（52%）|
| > 0.8 | 36 / 257（14%，最高 0.95）|

近黑只出现在开场几帧。**全曲一多半时间地板本来就该是中等偏亮的** ——
所以"偏亮"这个主观印象里有多少是 bug、多少是原本的美术意图，目前无法区分。

**已排除的机制汇总（4 条）**

1. 光照太强 —— 舞台 `Light` 组件 0 个
2. `_EnvMap` 缺失 —— 日志实测 0 个 null
3. BgColor1 `power` → `_ColorPower` —— 中位数 1.000，写了也是乘 1
4. env 反射项 —— `_EnvRate` 归零无变化

**下一个二元实验（未做）**：把 `_MulColor0` 强制设为纯黑。
地板变黑 → BgColor1 的写入确实主导表面，亮度就是数据本身，"发白"是美术预期问题；
地板仍白 → 写入没有到达表面，回头查 MPB / renderer 解析 / shader 是否真的用这个通道。

### C3. ParticleGroup 覆盖 Particle（6 首 / 31+27 keys，优先级低）

两个 handler 都按 `ps.gameObject.name == data.name` 匹配并写同一个
`emission.rateOverTime`，ParticleGroup 在后（:376 vs :375），同名时覆盖 Particle。
另外 `new MinMaxCurve(FlickerDarkRate, FlickerLightRate)` 隐含 dark < light，
数据里是否成立没验证过。数据量很小，可以最后处理。

---

## 建议顺序

按「影响面 × 修复成本」排：

1. **A1** GlobalLight 被清空 —— 59/59 首，改动约 15 行，收益最大
2. **A2** BgColor2 组名路由 —— 15 首 / 14346 keys，可复用 BgColor1 现成的解析缓存，顺带清掉 B1/B2 最重的一处
3. **A3** Spotlight3d `_Color` —— 31 首，照抄 BlinkLight 的探测逻辑
4. **C2** StageObjectMap 去重 —— 4 行，且会影响 A2/A3 的解析结果，建议和它们一起做
5. **A4** Transform 解析 —— 12 首，需要先 dump 确认 OffsetType 是否存在
6. **B1–B4** 性能 —— 多数在 1–4 做完时自然消掉，剩余单独扫一遍
7. **C1 / C3** —— 需要先做语料统计才能定方案

前 4 项之后建议重跑 `LiveTimelineWorksheetDiag.Dump()` 对照歌曲 1177 与 1006，
确认组名解析命中率，再更新 `LIVE_DEV_MAP.md` 里这几行的 ✅/⚠️ 标记。

**进度（2026-08-07）**：1（A1）、2（A2，部分）、3（A3）、4（C2）、5（A4）均已落地，
A5 一并做掉；剩 6（B 类残余：WashLight/Laser 的 `r.materials`）与 7（C1 / C3，都需要先做语料统计）。

---

## 审计之后（2026-08-06 ~ 08-07）

审计只覆盖了「已标 ✅ 的轨道对不对」。这两天做的是另一类问题 —— **舞台侧原版脚本缺失**，
以及随之而来的一次代码整理。

| 事项 | 结果 |
|---|---|
| 舞台 Animation 全都不播 | 283 个 `Animation` 组件 `playAutomatically = true` 但默认 clip 为空，Unity 什么都不播；原版靠 `AnimationObjectController` 显式 `Play("clip名")`。**曾写 `StageAnimationPlayer` 顶替，2026-08-07 撤除** —— 那个类实测零序列化字段，「播哪个/何时播/多快」无从还原，顶替版把 wash 灯播成 2 秒一个来回。现状是有意不播，理由见 `LIVE_TRACKS.md` 专条 |
| 按签名重建原版脚本 | 打通了。类名 + namespace + 程序集名对上就能让 Unity 真正反序列化字段 —— `BillboardController` 令 `[StageScripts]` 丢失数 892→728（正好 −164）。**但只拿得到数据，拿不到方法体**：`_rotationType` 的枚举语义随 IL2CPP 元数据一起没有 |
| 地板发白（C4） | 查明是 `mirror_a`，实现 `Gallop.MirrorReflection` 后解决。详见 C4 条目 —— 这条的过程比结论值钱 |
| BlinkLight 调色板槽位 | 槽位号 = `lightNNN_`/`alphaNNN_` 名字前缀，7 组实测无一例外。顺带修掉镜面球球体被 BlinkLight 涂成粉红的问题（`_MulColor0` 是 BgColor1 的通道，BlinkLight 排在它之后，抢了） |
| 代码整理 | 六个各自为政的 `MaterialPropertyBlock` 字段合成一个共享块（用法本来就是 Get→改→Set，不带跨帧状态）；`ResolveStageTargets` 的四个诊断实例字段改成显式返回的 `StageResolveReport`；effect 载入、vocal 音源解析、VMD 的 FOV 标记三处重复各去掉一份；`StageEnvMapDiag`/`StageEnvRateToggle` 删除；HdrBloom 的 handler 与 `Bloom` volume override 撤掉（全语料 0 keys，映射也没核实过）|

---

## 附：本次用到的验证命令

```bash
cd tools
P=~/.venvs/umatools/bin/python3

# 轨道覆盖率（keys / 首数）
$P -c "import csv;rows=list(csv.DictReader(open('out/scan_keys.csv')));
       v=[int(r['bgColor2List']) for r in rows];print(sum(v), sum(1 for x in v if x>0))"

# 组名 —— 判断轨道是按 GameObject 名还是 unit 名 / 分区名路由
$P -c "from uma_common import MetaDb
env=MetaDb().load('cutt/cutt_son1006/son1006_camera')
for o in env.objects:
    if o.type.name=='MonoBehaviour':
        t=o.read_typetree()
        if t.get('bgColor2List'): print([g['name'] for g in t['bgColor2List']])"

# shader 属性表 —— 确认某个属性名到底存不存在
$P -c "from uma_common import MetaDb
for o in MetaDb().load('shader').objects:
    if o.type.name!='Shader': continue
    pf=o.read_typetree()['m_ParsedForm']
    if pf['m_Name'].startswith('Gallop/3D/Live/Stage'):
        print(pf['m_Name'], [p['m_Name'] for p in pf['m_PropInfo']['m_Props']])"

# 单个 key 的真实值
$P dump_cutt_typetree.py --sample spotlight3dList 1081
$P dump_cutt_typetree.py --tree   transformList  1028
```
