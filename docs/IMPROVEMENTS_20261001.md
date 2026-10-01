# Live 还原改进汇总与源码发布边界（2026-10-01）

## 发布状态与证据范围

本次发布为 `UmaViewer-master7` 的冻结源码快照，基于目标仓库既有 `master` 历史创建普通分支；不替换、重写或强推 `master`。冻结后正在进行的 **1093 观众缺失调查及任何后续修复不纳入本次快照**。

本轮改进并不意味着已达到原游戏全部视觉一致。下文的“通过”只对应明确列出的行为、歌曲、帧段或 GPU 场景。连续捕获报告的 `verified=true` 仅表示捕获身份/时间/连续历史合同通过，不是像素等价通过。用户最新实测优先于旧组件测试与旧截图。

汇总依据：本地中央工作区 `HANDOFF.md`（§3、§11、§12 最新追加）和 `REPORT.md`（§11、§12.1–12.11），以及项目原有 `docs/LIVE_RENDER_RESTORATION.md`。原始游戏数据、反汇编、截图与验证工程均不随源码发布；下面的 `tmp/...` 路径是维护者保留的本地证据索引，不是仓库内可下载附件。本发布代理只进行文件选择、安全审查与 Git 上传，未重跑 Unity、编译门或测试；运行结果为上述记录中的实际验证。

## 1. 驱动、时间轴与资源生命周期

- 阶段一删除重复 `AlterUpdate_BgColor1` 分发；移除跨相机自造 DOF bypass，恢复焦点深度数学，移除焦点预钳制与多减 near 的偏差。
- 后处理相机门控改为演示相机组成员判定；`ScreenOverlayRender.Parameter.Setup` 补拷 `ColorBlendFactor`，恢复作者 colorBlend 权重；shader 预热补 RadialBlur、ColorCorrection 三件。
- Exposure/ToneCurve 的序列化数据、worksheet、求值与渲染参数路由接入；不把仅编译接入称为整首亮度已匹配。
- A4 Effect/Particle 接入 MainLive SheetType 查表、事件分发、真实资源预载、父节点初始化、销毁释放、pause 与 seek 清空。1176 实际加载 27 个 prefab、观测 841 个粒子。修复 Effect 键漏 `OnLoad`、每帧重启已结束非循环粒子、Vr03 缺失/重复槽被压缩；实际修改前失败、修改后通过。
- 原生动画时钟恢复 `_isTimescaleDisabled` 与 approximately-zero speed 按 1 处理。真实 Animation 曲线前后证明错误 1.25/0 改为期望 5；不声称由此解决全部姿态差异。
- 显式 Animation.Sample 前按既有 IK 合同 reset，关闭后置自动 reset，保留 lookAt 求解；1176 腿部采样差消失，但头部约 33° 的差异仍在，不称完整姿态一致。
- Director 在角色构造前建立演示相机并维护唯一 MainCamera 标签，解决角色 FixedUpdate 的 `Camera.main` 空引用。308/868/3848 三个切换帧眼神追踪异常为零；不是在角色物理路径加空值绕过。
- 1176 激光合法空数组初始化不再每帧释放/扫描/重建：成功的非 null（含空）结果标记完成，真正资源失败仍可重试。实际 308 帧初始化调用由 328 次降至 1 次。

主要证据：`tmp/goal-effect-live-result.txt`、`goal-final-review-smoke-{before,after}.txt`、`goal-motion-clock-{before,after}.txt`、`goal-pose-reset-order-after.txt`、`goal-camera-ownership-result.txt`、`goal-laser-setup-{before,after}.txt`。

## 2. 多摄像头、最终合成与实际显示表面

- 辅相机独立 RT 捕获、原生 Composite/FinalComposite 消费、Single 冻结图及尺寸切换/恢复、独立后处理参数接入；可选后处理开关不再关闭必需多相机合成。
- 六项最终评审缺陷均有修改前失败/修改后通过证据：三项 Effect/Light 生命周期问题，以及 Single 冻结重建时机、禁用 owner/aux 生命周期旁路、aux Setup 后未重新 `DecideDrawType` 导致 Bloom 不生效。
- 真正生产显示链修复：`CopyCameraToRTFeature` 原 event1000 抢在 composition 之前复制，camera target 已分屏而 `RT_Screen` 仍单人。现仅复制 Director 主演示相机最终输出，pass/renderer 配置置 event1004。真实 1053 前后 `copied-display.png` 从单人变三栏。
- 1093 frame840 发现每相机独立 render context 重复触发 beginFrameRendering。FinalComposite 每个 Unity 帧只准备/清空一次，OnEnable 重置帧标记；最终 pass 按 URP projectionFlipped 处理 live framebuffer 方向，Single 冻结路径保持。
- 1093 最终实际捕获输出正向副相机人物；既有十一场景真实 URP GPU 回归全部通过，覆盖可见贡献、禁用/alpha-zero 旁路、Single 捕获/冻结/尺寸切换/恢复、aux Bloom 启闭。用户最新明确确认多摄像头已修复。

边界：十一场景不覆盖所有曲目、所有非对称分屏遮罩或全部视觉一致性。

主要证据：`tmp/goal-display-copy-{before,after}/`、`goal-composition-lifecycle-{before,after}.txt`、`goal-aux-draw-{before,after}.txt`、`goal-composite-draw-mode-smoke/result.txt`；实际 1093 捕获 `20261001_110949_870/frame_000840.png`。

## 3. 渲染、光照、镜面、屏幕与可见性

- A5 VolumeLight/LightShafts 真实贴图预载、timeline 事件与 URP passes 接入；13 场景、141 断言、311296 GPU 像素采样通过。A3 Spotlight3d 使用真实 AssetHolder prefab、角色附着与生命周期，1001 frame4000 三组 renderer 启用于 BG12。
- 恢复 GraphicSettings 逻辑槽到实际图层映射（effect11、excluded30、transparent1、spotlight12），保留 viewer Background23/UIHandle24 与碰撞矩阵。
- GlobalLight、角色颜色与舞台颜色按 Renderer 的 Get→修改→Set 保留其他 MPB 字段，避免覆盖 Rim/Ambient；方向光也不再逐帧创建 materials 实例数组。Blink 开关路径保留 Ambient 的前后实验通过。
- 舞台颜色按 FNV/nameHash 与 unit 子哈希寻址；恢复实例 enabled、原 parent/start TRS、`_MulColor0/_ColorPower`（包括 0）及精确 BgBL Ambient 语义。1001 角色颜色补消费 power，实际 frame298 从错误亮色恢复作者暗剪影；Control power 的无钳制插值实际中点 .5 通过。
- Blink 色彩恢复原生 HDR HSV、白端点继承 hue、reverse/直接 hue 插值等语义；修改前 HDR3 被截为1、期望青变红，修改后通过。Blink 材质缓存复用 List 并逐材料引用比较，不再因 sharedMaterials getter 新数组而每帧重建。
- 镜面双提交实际由 2 次降至 1 次，owner/receiver 禁用不再额外提交。恢复 `MirrorReflection.SetActive` 的 GameObject 生命周期语义，隐藏/零提交/恢复通过。
- 镜面组件恢复 `Gallop.MirrorReflection` namespace 与作者字段 `_mirrorCliipPlaneOffset`，避免丢原组件而补全层默认组件。1093 恢复 size512/layers12544/offset0，1176 恢复 size512/layers78080/offset0 与原 distortion 参数；真实 RT 从错误 1024×768 回到作者 512×384，不是随意降质量。
- 舞台监视器沿用 Builder 单 owner，删除重复 RT owner；1093 frame1198 主屏实际显示人物近景。Monitor timeline position/size 改为主纹理居中裁切，不覆盖作者物理 Width/Height；恢复 UVAdjust 与作者 FilterTexScale 合同，crop/aspect 烟测通过。
- 监视相机恢复 position key attribute0x20000 裁剪与0x40000背景，按原生 cullingLayer 转 mask，未标记 key 不被 CopyFrom 覆写历史状态。真实 camera8257→未标记保持→flagged4161/background切换通过；1093 自然 frame5556.599 验证 mask/background 正确，但横纹仍明显。
- 星芒迁移启用演示相机 URP postProcessing：真实1001 frame300、163 emitters 开关产生84883变化像素，画面直接可见交叉星芒。仍非完整 fog/directional/IgnoreLayers/fade 等价。
- StageWashBeam 使用原网格单层 sibling proxy、保留 TRS/生命周期，按每实际相机回调更新；清除空 material-index MPB 的 override，避免遮掉 renderer-wide Blink颜色。44 个真实 proxy、八同距离视角产生6170–6479变化像素，可见宽均32px；不是不同距离恒定屏幕像素宽。
- LightProjection worksheet/event/插值/角色附着与 URP Projector 绘制链接入；恢复 Near/Far timeline 无钳制插值与释放后的作者裁剪。真实 frustum 作者 far60 拒绝 z70，轨道 far75 接受z70且拒绝z80，Dispose 恢复。1001 局部投影贡献实测不等于用户最新地面镜头验收已通过。
- 1053 放射光锁定为 IndirectLightShafts 的真实 def_A/def_B 纹理；恢复 `_Offset=info.Offset`、`_Angle=info.Angle/360`，不修改作者 alpha/亮度。1004 残余过亮未据此关闭。
- 1037 Blink driver 两处强制 `renderer.enabled=true` 覆盖 object timeline：删除越权启用，真实 frame30/300/1100 六个 stardust 对象按作者保持关闭，而803个应可见 renderer 仍启用；早期 geometry CLEAR 点1694后四组粒子实际 playing=false/count0。未把整首粒子状态全部关闭问题标完成。
- Cyalume 实例初始化按原生 shader manager 使用 HQ 原 shader，清理无效 simple/全场景每帧修补；保留作者 mesh/TRS/ST。1093 天空棒/灰色观众问题仍开放，不移动相机或棒、不按高度猜裁剪。
- WashLight 收集作者现有组件并初始化，按 IsInitialized 注册；恢复 projection child TRS 重置。1093 上方光斑仍无贡献，原生 indices 已核对，不任意反转生产面序。

用户明确验收完成：1053 多余灰模型、1001 暗部舞台过亮、1093 橙色半透明覆盖。不得据此扩大为高光过曝、地面闪烁或性能全部解决。

主要证据：`tmp/goal-lighting-observation.txt`、`goal-blink-native-color-{before,after}.txt`、`goal-character-power-{before,after}.txt`、`goal-mirror-submit-{before,after}.txt`、`goal-mirror-activation-{before,after}.txt`、`goal-mirror-binding-check.json`、`goal-authored-flare-pixels.txt`、`goal-actual-beam-views/`、`goal-monitor-uv-scenario-result.txt`、`goal1093-monitor-evidence/monitor-native-state-result.txt`、`goal1037-visibility/visibility-proof.txt`、`projection-clip-proof.txt`。

## 4. 脚底圈光与每脚十字阴影

- 按真实资源 TypeTree 接入 common spotlight/shadow、layer13、原 mesh/material/TRS、预载和释放。
- 修复圆斑误常亮根因：FollowSpotCenter/Left/Right/Back BgColor1 未消费导致材质默认 power1。默认关闭，按作者角色 mask/flags、color/power/scale 更新；不统一gain、不以alpha启闭、不按歌曲硬编码。
- 真实1001 frame300全18人关、1248仅后排15人power.15、1422前三人.5/.4/.4及后排.15、5656全关，72个状态检查通过。1053 在作者开启时段仍可出现圈光，非整首禁用。
- 用户纠正后每角色两个 cross 分别绑定 Ankle_L/Ankle_R，XZ 独立随脚、Y 使用当前 timeline 地面输入；保留作者淡cross纹理、强度及 disabled projector。1053 38.4秒18角色/36实例逐脚检查和单独左脚位移隔离通过，真实近景直接可见双脚下独立cross。
- 1001 23.7秒逐脚绑定同样通过，但独立近景未清楚显示圈光/cross，不能关闭该歌曲脚底视觉验收。旧单中心 pixel PASS 不作为双脚验收证据；locator Position 可能是动画骨骼而非静态地面，未凭假设改 rootY/0 或 clamp。

主要证据：`tmp/goal-foot-timeline-result.txt`、`goal-two-foot-shadow-result.txt`、`goal-two-foot-shadow/feet.png`、`goal1001-two-foot-shadow-result.txt`。

## 5. 手持道具生产链

- 新增真实 Props/Attach 序列化 schema、完整 settings 与 worksheet 初始化；Builder 条件预载，Director 构造/late-evaluate/dispose controller，删除无消费者旧 bridge。
- 61首可用样本中30首声明道具、121组设置，不是全游戏全集。1093 DressId50 实例真实 M_ToonProp 在5232/6944/7074显示→隐藏→恢复并跟 Hand_Attach_R。
- 修复 propsID 为每角色条件过滤后的局部编号，而非全局setting索引；匹配但加载失败保留未解析slot，避免后续编号漂移。1177 修改前第二角色隐藏，修改后两个角色 local0 真道具均显示/跟手。
- 1004 body prefab 缺少作者 motion 曲线路径中的 Mic 附件节点，导致 timeline override target=null 而隐藏。Animation/locator 缓存前仅补缺失的四个真实动画节点，已有TRS不变、缺Position不猜 fallback，不偷改到手节点。自然播放630/634/1843/1922共12次显隐及 animated target 绑定通过；无清晰麦克风近景，不宣称原游戏视觉等价。
- canonical DressId 改为现有 costume选择对应 DressData row.id，而非资源前缀；缺失/歧义拒绝猜测。1032/00 predicate 前后通过；1037 实际自然播放30/5211/5305六次道具显隐与 Hand_Attach_L 跟随通过，闭合此前仅 predicate 烟测的边界。
- 1174 AlphaToon 作者材质 ZWrite1/Cutoff.2 的实际加载烟测通过，未盲目改材质。1181 默认输入原生明确 variation0；不把作者存在1/2轨道当作必须全启用。

仍未闭合：非默认 live variation 输入、1092 linked attachment、depth-alpha完整策略、flare collision/reserve warming等；不能写成“全部有道具Live已完整还原”。

主要证据：`tmp/goal-props-live-result.txt`、`goal-props1177/{before-index,after-index}-result.txt`、`goal-props1004/missing-motion-node-experiment/production-after-result.txt`、`canonical-{before,after}-result.txt`、`goal1037-visibility/props-canonical-result.txt`、`goal-prop-depth-material-result.txt`。

## 6. 验证总边界与开放问题

阶段一六套GPU回归（DOF焦点、前景CoC80采样、Bloom/Diffusion60检查、RadialBlur、ColorCorrection124902像素、PostFilm）有实际PASS；阶段二编译门及多个实际Live/URP前后证明见上述索引。验证使用隔离工程，不干扰用户编辑器；原生产 Config、CySpring 与角色物理不由本轮渲染调查修改。

1176严格连续308/868/3848对比 score约.45809/.52095/.47640、SSIM约.35684/.23779/.25706，仍有明显姿态/曝光差；没有像素等价验收。

明确仍开放：

1. **1093灰色观众缺失**：用户确认真实游戏有、viewer没有；保留作者荧光棒与相机位置，后续恢复真实mob加载/显隐/绘制，不统一给所有Live加观众。冻结后的诊断/修复不在本发布完成项。
2. **1093地面闪烁**：应集中约25秒及随后几秒，不用其他时段静态归因替代；橙色覆盖已由用户确认解决，不重开。
3. **1093屏幕横纹过重**：框架纹理来源及真实裁剪修复已证实，横纹仍在；不任意置黑框架、降低gain、隐藏draw或换镜面pass。
4. **1093舞台照地光斑**：作者wash已加载/初始化，但上方绘制贡献缺失，原native面序一致；未完成根因。
5. **1004约37.7秒亮度**：真实读回UNorm，before/after RGB最大均1，不支持“shaft输出HDR>1再进bloom”假设；alpha变化不足以单独证明偏差，不统一压曝光。
6. **1001约55.6秒远景DOF**：焦点目标/深度输入与参考存在差异；未通过关DOF、改size/spread掩盖。
7. **1001生产地面发光投影与脚底圆光/cross镜头验收**：局部贡献、轨道与绑定PASS不能覆盖用户最新反馈。
8. **全部道具Live与1037全场错误常显**：已修具体可复现分支，特殊策略/其他时段仍未完全验收。
9. **完整姿态、高光曝光及原游戏视觉一致**：仍有残差，不写成全部还原。
10. **1176性能研究暂停**：用户要求效果完整还原前暂停。既有正确性/分配/重复初始化修复保留；历史batch墙钟样本不等于用户实时FPS，不再开展成本/降质量实验。

## 7. 安全发布清单与运行前提

发布包含普通 Unity C#、自写 shader、asmdef/meta、必要普通材质/RT/场景/UI prefab、Packages、ProjectSettings、现有通用第三方插件、手写工具与本地网页查看器源码，以及项目文档。保留 Unity GUID，不广泛忽略全部 `.asset/.mat/.unity/.dll/.a`。

排除：Config.json与本地密钥、Library/Temp/Logs/obj/UserSettings、build/IDE生成物、tmp隔离验证工程、captures/exports/PMX/VMD游戏导出、备份、内存dump/加密metadata/反汇编产物、shader.a及提取bundle、游戏still/bg/flare/UI提取图、cri_auth、sound_proj.acf、游戏原生CySpringPlugin.dll。

**发布副本的 Config.cs 四组本地游戏密钥数组已清空，保留现有配置API；原master7不改。** 不新增密钥、不复制本地Config.json，不声称存在可运行fallback。音频源码中的既有公共常量文件与远端完全相同，未新增或变更该常量；既有Git历史未重写。

因此这不是开箱即用的二进制发行版。使用者必须自行合法取得所需游戏数据、授权音频资产与原生CySpring插件，并自行填写正确密钥/路径；缺省密钥不提供解密能力。排除的游戏UI纹理亦可能造成缺图，需要使用者自行提供。源工程所用Unity版本见 `ProjectSettings/ProjectVersion.txt`；本次不触发master/tag release工作流、不发布二进制、不声称新克隆无依赖即可构建运行。

远端已有历史保留；原本地项目无.git，本次Git集成在独立发布副本进行，避免覆盖正在调查的源码与用户状态。旧远端独有、与当前项目目录不兼容的源码以本冻结树为准迁移；旧提交仍可访问。历史文档可能描述较早状态，本页是本次冻结发布的状态入口。
