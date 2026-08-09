# Live 后处理轨道 TODO

全局开发地图见 `LIVE_DEV_MAP.md`。本文件只覆盖需要 URP 移植的后处理效果。

> **覆盖率数字已于 2026-08-04 用 `tools/` 全量重测更新**（59 首全语料）。带 ✅实测 的是权威数据。dump 字段结构：`~/.venvs/umatools/bin/python3 tools/dump_cutt_typetree.py --tree <字段名> <songid>`。

---

## 为什么不能直接用旧方法

Built-in 管线支持 `OnRenderImage()`，脚本可直接拦截帧缓冲做全屏处理。**URP 删掉了这个接口**。

项目同时装了 `com.unity.postprocessing 3.4.0`（旧 Built-in 包）和 URP 14。`Screenshot.cs` 在用旧包的 `PostProcessLayer`，但该组件在 URP 管线下**不渲染任何效果**，只是没报错。后续实现不要依赖旧包。

---

## 两种实现路径

### 路径 A — URP Volume Override（适用于内置效果）

摄像机上挂 `Volume` 组件，`VolumeProfile` 里添加对应 Override，每帧在 Director handler 里设值：

```csharp
_liveTimelineControl.OnUpdateXxx += (data, keyData) => {
    if (_postProcessVolume.profile.TryGet<SomeUrpEffect>(out var fx))
        fx.someParam.Override(keyData.value);
};
```

适用：ChromaticAberration、Bloom、ColorAdjustments、DepthOfField。

### 路径 B — ScriptableRendererFeature（适用于自定义效果）

URP 在管线中插入自定义 Render Pass，用 Material 做全屏 Blit：

```
ScriptableRendererFeature         ← 注册到 Renderer Asset
  └── ScriptableRenderPass
        └── Blitter.BlitCameraTexture(cmd, src, dst, material, pass)
```

Feature 暴露公开字段，Director 持有 Feature 引用，每帧写入参数：

```csharp
private PostFilmRendererFeature _postFilmFeature;
// handler 里：
_postFilmFeature.settings.color0 = keyData.color0;
```

Feature 必须在 `UMAUniversalRenderPipelineAsset_Renderer.asset` 的 Renderer Features 列表里手动添加。

适用：PostFilm、RadialBlur、TiltShift、SunShafts。

---

## 优先顺序

### 第一组：URP 内置，工作量小（路径 A）

| 优先级 | Track ID | 字段名 | URP 类型 | 覆盖率 |
|--------|----------|--------|---------|--------|
| 1 | 73 | `chromaticAberrationList` | `ChromaticAberration` | ✅实测 22/59首 / 487 keys |
| 2 | 61 | `colorCorrectionDataLists` | `ColorAdjustments` + `ColorCurves` | ✅实测 47/59首 / 297 keys |
| 3 | 13 | `postEffectDOFKeys` | `DepthOfField` | ✅实测 **59/59首 / 3879 keys** |
| — | 38 | `hdrBloomKeys` | — | ✅实测 **0/59 确认为空**。但 Director 里已经实现了 → 死代码，建议删 |

### 第二组：自定义 shader，工作量大（路径 B）

| 优先级 | Track ID | 字段名 | 覆盖率 | 备注 |
|--------|----------|--------|--------|------|
| 4 | 39 | `postFilmKeys` / `postFilm2Keys` / `postFilm3Keys` | **59/59** | ✅ 三字段合计 **~20200 keys**，最大的一块。**数据层+分发层已完成，渲染层默认关闭**，原因见下方专节 |
| 5 | 37 | `volumeLightKeys` | **58/59** | 数据类已有，shader 需移植。✅实测 2048 keys |
| 6 | 15 | `radialBlurKeys` | **59/59** | ✅实测 1667 keys（单曲最多95）——**不是骨架**，旧记录错误 |
| 7 | 63 | `tiltShiftKeys` | **58/59** | ✅实测 1202 keys（单曲最多127）——**不是骨架**，旧记录错误 |

---

## 各效果详情

### 1. ChromaticAberration（路径 A）

**Track ID**: 73 | **覆盖**: 22/58 首  
**URP 类型**: `UnityEngine.Rendering.Universal.ChromaticAberration`

已知字段：`power`（intensity）、`effectType`（目前值为 0，语义待确认）、per-channel offset（`redOffset`/`greenOffset`/`blueOffset`，URP 内置无对应，暂忽略）

实现：摄像机 Volume → Profile 加 ChromaticAberration override → handler 设 `intensity.Override(power)`。

---

### 2. ColorCorrection（路径 A）

**Track ID**: 61 | **覆盖**: 47/58 首  
**URP 类型**: `ColorAdjustments` + `ColorCurves`

已知字段：`saturation`（已实现）、RGB 曲线（已部分实现）、`depthRedCurve/Green/Blue`/`blendCurve`/`mode`/`selective`/`keyColor`/`targetColor` 无 URP 对应，忽略。

---

### 3. DepthOfField（路径 A）

**Track ID**: 13  
**URP 类型**: `UnityEngine.Rendering.Universal.DepthOfField`

✅实测 59/59 首 / 3879 keys。字段用 `tools/dump_cutt_typetree.py --tree postEffectDOFKeys <songid>` dump。URP DOF 分 Gaussian 和 Bokeh 两种模式，选哪种取决于原版字段。

---

### 4. PostFilm（路径 B）—— 数据层已通，渲染层卡在缺 ground truth

**2026-08-05 现状**

已完成并验证：数据类字段（按 bundle TypeTree 顺序重排 + 补齐 5 个 `BlinkLight*`）、
`LiveTimelineKeyPostFilmDataList`、WorkSheet 三字段、`AlterUpdate_PostFilm` 分发与插值、
`PostFilmUpdateInfo`、`PostFilmRendererFeature`、`Assets/Resources/Shaders/PostFilm.shader`。
song 1177 运行时实测 100 / 161 / 87 keys，与离线 dump 一致。

**渲染默认关闭**：`PostFilmRendererFeature._enableRendering = false`。

卡点：song 1177 的 100 个 key **全是 Vignette 变体**（VigLerp 60 / VigAdd 39 / VigMul 1），
所以晕影衰减是决定观感的关键，而 `filmOptionParam` 的几何语义还原不出来。实际取值：

    (0, 0.05)  (0.05, 0.05)  (0.08, 0.05)  (0.2, 0.05)
    (0.04, 0.17)  (0.1, 0.17)  (0.2, 0.25)

`y < x` 的组合排除了「内径/外径」；按「离边缘距离」解释试过两版，一版全屏泛色、一版大边框。
**游戏自己的 PostFilm shader 不在 `shader` bundle 里** —— 该 bundle 的 499 个 shader
按名字（ImageEffect 家族 34 个，无 PostFilm/Vignette）和按属性签名都搜过，
它应该编在 app 本体的 `resources.assets`，我们拿不到。

继续做的唯一路径：对着参考视频找一帧晕影明显的画面，在 Inspector 勾上 `Enable Rendering`，
调 `Vignette Width` / `Vignette Strength` 直到吻合，再把数值写死并注明标定依据。

未实现：`depthPower` / `DepthClip`（深度混合）、`layerMode = UVMovie`（图层贴图，
和 MonitorControl 共用 `live/uvmovie/gal_uvmovie_<songid>_<NNN>` 资源，寻址规则同样未解）。

---

### 4b. PostFilm 字段参考（原记录，供实现时查）


**Track ID**: 39（还有 `postFilm2Keys`、`postFilm3Keys`，song 1177 有数据）  
**数据类**: `LiveTimelineKeyPostFilmData`（`LiveTimelineKeyPostFilmDataList.cs` 已存在）

`filmMode` 决定混合逻辑（8 种），`colorType` 决定颜色布局（4 种）：

```
filmMode:  None=0  Lerp=1  Add=2  Mul=3
           VignetteLerp=4  VignetteAdd=5  VignetteMul=6  Monochrome=7

colorType: ColorAll=0  Color2TopBottom=1  Color2LeftRight=2  Color4=3
```

已知字段：
```
filmMode, colorType
filmPower        → 全局强度
filmOffsetParam  → 位移参数
filmOptionParam  → 附加参数（Vector4）
color0/1/2/3     → 最多 4 个颜色分区
depthPower       → 深度混合强度
DepthClip        → 深度剪裁
RollAngle        → 旋转角度
FilmScale        → 缩放
layerMode        → Color=0 / UVMovie=1
colorBlend       → None/Lerp/Additive/Multiply
colorBlendFactor → 混合系数
```

实现步骤：
1. 创建 `PostFilmRendererFeature.cs` + `PostFilmRenderPass.cs`
2. 写 `PostFilm.shader`（HLSL，`AfterRenderingPostProcessing` 插入点）
3. 在 `UMAUniversalRenderPipelineAsset_Renderer.asset` 添加该 Feature
4. Director 持有 Feature 引用，`OnPostFilmUpdate` handler 写入参数
5. WorkSheet 加 `postFilmKeys`/`postFilm2Keys`/`postFilm3Keys` 字段，Control 加事件

---

### 5. SunShafts / VolumeLight（路径 B）

**Track ID**: 37 | **覆盖**: 57/58 首  
**数据类**: `LiveTimelineVolumeLightData`（已有，handler 目前是存根）

已知字段：
```
sunPosition: Vector3  → 光源世界坐标（需转屏幕空间）
color1: Color         → 光线颜色
power: float          → 光线强度
komorebi: float       → 丁达尔分量（单独 pass，可最后做）
blurRadius: float     → 径向模糊半径
ScreenColorPower      → 屏幕混合强度
EffectColorPower      → 效果强度
enable: bool          → 启用开关
```

实现步骤：
1. 参考 Unity ImageEffects 仓库 `SunShaftsComposite.shader`
2. 改写为 URP HLSL（去掉 `GrabPass`，用 `_CameraOpaqueTexture`）
3. Feature 在 `BeforeRenderingPostProcessing` 插入，两个 pass：遮罩 + 径向模糊
4. `OnVolumeLightUpdate` handler 填充参数

---

### 6. RadialBlur（路径 B）

**Track ID**: 15 | **覆盖**: ✅实测 59/59 首 / 1667 keys（**有真实数据，不是骨架**）

字段可用 `tools/dump_cutt_typetree.py --tree radialBlurKeys <songid>` dump。算法：屏幕空间径向模糊，从中心向外方向采样 N 次，`offset = direction × step × i`。

（原记「骨架数据居多」已被实测推翻，可以直接按真实数据实现。）

---

### 7. TiltShift（路径 B）

**Track ID**: 63 | **覆盖**: ✅实测 58/59 首 / 1202 keys（**有真实数据，不是骨架**）

字段可用 `tools/dump_cutt_typetree.py --tree tiltShiftKeys <songid>` dump。算法：屏幕中间清晰，上下做高斯模糊，用渐变 mask 混合。

（原记「骨架数据居多」已被实测推翻。）

---

### 8. Komorebi / 丁达尔（SunShafts 子项）

`volumeLightKeys[i].keys[j].komorebi` 字段目前读取但未使用。归并到 SunShafts Feature 里作为额外 pass，在 SunShafts 基础实现完成后再加。

---

## 项目信息

- Unity: 2022.3.62f1（URP 14.0.12）
- 渲染资产: `Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset.asset`
- 渲染器（Feature 注册位置）: `UMAUniversalRenderPipelineAsset_Renderer.asset`
- Director 入口: `Assets/Scripts/umamusume/Gallop/Live/Director.cs` → `InitializeTimeline()`
- Control 事件模板: `LiveTimelineControl.cs` → `AlterUpdate_SimpleListControl`
