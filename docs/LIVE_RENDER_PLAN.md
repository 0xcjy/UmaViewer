# UmaViewer Live 还原执行计划

> 目标目录：`D:\Projects\UmaTools\UmaViewer-master7`  
> 目标对象：已编译的 `D:\Projects\UmaTools\UmaViewer`  
> 建立日期：2026-09-25  
> 当前状态：阶段一执行中

## 总目标

把 master7 从“能加载并播放 Live 的局部模拟”推进到“资源集合、对象生命周期、时间轴、相机、渲染链和后处理都有可追溯证据的 Live 复现器”。最终验收以同歌曲、同舞台、同角色、同帧、同机位、同分辨率的真实素材截图为准；隔离 Shader/GPU 测试只能证明局部契约，不得作为完整还原证明。

## 不可破坏的约束

- 不修改物理实现；用户已经确认当前物理正常。
- 不修改用户已经确认基本正常的景深算法，除非得到新的运行时/二进制证据。
- 不直接复制 master5 的 HDR 设置、Director 或 Config；先确认 RT、颜色转换和调用顺序。
- 不凭截图盲调 Bloom、Diffusion、PostFilm、曝光或颜色强度。
- 不把 `GetAllAssetNames` 的潜在资产数量冒充实际加载数量。
- 不关闭、重启或接管用户主 Unity Editor；隔离 GPU 测试只使用 `tmp\dof-focus-test-project`，且同一时间只运行一个。
- 不读取或输出账号、令牌或 `Config.json` 内容。

## 四阶段目标与产物

### 阶段一：建立可重复的真实 Live 基线与逐帧诊断（当前）

**目的**：先回答“master7 实际加载了什么、实例化了什么、用哪个相机、每帧的渲染输入是什么”，停止用截图猜测根因。

**产物**：

- `docs/LIVE_RENDER_PLAN.md`：本计划和每轮证据记录。
- 可开关的 Live 运行时诊断，记录 JSONL 会话：时间轴帧/秒数、活动相机、相机参数、角色/舞台/特效对象规模、已打开 Bundle、句柄复用、缺失/失败、后处理状态和相机目标信息。
- 真实 Live1176 基线捕获约定：歌曲 1176、舞台 10148、14 个角色槽、60 FPS、1920×1080，优先帧 308、868、3848。
- 编译和隔离 GPU 回归日志；明确“局部测试通过”与“真实 Live 未验证”的边界。

**验收标准**：

1. 同一会话能导出加载阶段和播放阶段的可比较日志；
2. 进度条数字能拆分为根条目、依赖展开、重复请求、实际打开、句柄复用和运行期间按需加载；
3. 诊断默认关闭，开启时不改变物理、景深和后处理参数；
4. 能指出下一步需要补资源集合还是补渲染链，而不是只给出“画面更亮/更暗”。

### 阶段二：对齐资源加载、场景对象和实例化生命周期

**目的**：把二进制版的“数千级资源加载”转化为同口径数据，确认 master7 是否缺少角色动作、舞台子对象、灯光、屏幕、特效、Shader/纹理或只是统计口径不同。

**产物**：

- 二进制版与 master7 的资源计数口径表；
- Live1176 的根条目、依赖图、实际打开 Bundle、`LoadAsset`/`LoadAllAssets` 请求和实例化对象清单；
- 依据证据修复的资源集合/按需加载顺序。

**验收标准**：加载数已能解释为同一统计口径；缺项可落到具体 Bundle/资产/调用点；不以 UI 放大数字代替修复。

### 阶段三：对齐真实渲染链、相机和 RT 格式

**目的**：确认每个相机和每个 Render Feature 的输入/输出、RT 格式、深度纹理、MSAA、HDR/LDR 转换与最终 Blit，定位过亮是否来自重复合成或错误颜色空间。

**产物**：

- 主相机、镜面/监视器/反射相机的渲染清单；
- `SourceSetup -> DOF/Diffusion/Bloom/Overlay -> RadialBlur -> ColorCorrection -> FinalBlit` 的实际运行时证据；
- 过亮帧的输入输出纹理与参数快照。

**验收标准**：每个可见后处理有真实输入/输出和启用条件；确认是否存在第二次合成、多相机误入或缺失最终转换；不凭截图调强度。

### 阶段四：二进制调用链、启用条件和纹理交接确认

**目的**：将静态逆向证据与运行时日志对应起来，确认资源集合、时间轴 key、相机切换、后处理参数和纹理交接的调用顺序。

**产物**：

- `D:\Projects\UmaTools\UmaViewer-master5\tmp\render-*.txt`、运行时日志、GameAssembly/Shader 证据的映射表；
- 未确认项目明确标记为“未知”，不得用相近变量名代替证据；
- 只有经过调用链和同帧截图双重验证的源代码修复。

**验收标准**：每项修复能回答“哪个函数/哪个 key/哪个条件/哪个纹理/哪个帧”；完整素材同帧校验后才报告画面改善或过亮结论。

## 当前执行记录

### 2026-09-25：阶段一第 1 个可测量增量

- 新建本计划文档，确认四阶段目标和验收边界。
- 下一步实现只读 Live 运行时 JSONL 诊断：不改变物理、景深或后处理参数；先把加载计数和播放时的对象/相机/RT 快照分离记录。
- 当前主 Unity Editor 已检测到运行中，不关闭、不重启；本轮不启动主项目的批处理实例。
- 当前基线仍保持 HDR 关闭；径向模糊 bit17/bit18 矩形标志保持未确认，不启用。


### 2026-09-25：阶段一第 2 个可测量增量

- 修复 `LiveRuntimeDiagnostics.cs` 的命名空间引用；`tools/check_live_compile.ps1` 通过，仅保留原有警告。
- 在隔离 `tmp/dof-focus-test-project` 中补齐本地真实参考 manifest、Live1176 曲线 fixture、Live1001 radial fixture 和 shader bundle 后，完整后处理回归通过：
  - LiveCaptureContract：两份真实参考 manifest 的身份、帧选择、稳定性门控通过；未加载完整 Live。
  - ColorCorrection：1176 六组曲线、字节打包、54 个 GPU 颜色/Alpha 检查通过。
  - Bloom/Diffusion：提取、Bloom、Diffusion 方程及三层 PostFilm 一次 Bloom 合成检查通过。
  - RadialBlur：Live1001 时间轴 7901 个样本、GPU 检查通过；bit17/bit18 未被猜测启用。
  - PostFilm：时间轴与 GPU 检查通过。
  - DOF：焦点路由和前景/远景 CoC GPU 检查通过。
- 为已打开的 Unity Editor 增加本地 `tmp/enable-live-diagnostics` 标记入口，避免依赖启动前环境变量；它只启用 JSONL 观测，不改变物理、景深、HDR、Bloom、径向模糊或颜色参数。该标记当前已创建，待用户在编辑器中加载一次 Live1176 后读取 `tmp/live-diagnostics-*.jsonl`。
- 当前主 Unity Editor 仍为 PID 22128，未关闭、未重启；隔离批处理验证已结束。

**本阶段结论**：隔离测试已经证明“已接入的方程、时间轴解析和参数路由”在独立测试中可复现，但不能替代真实 Live 的完整渲染帧。资源数量差异、按需加载规模、真实相机/RT 格式及多相机叠加仍未取得运行时证据，阶段一尚未验收。
## 证据与未解决问题

- 真实游戏加载进度为“数千级”的观察尚未有同口径日志，不能直接等同于 master7 少加载资源。
- master7 当前预载路径只覆盖语音、Cutt、歌曲 part 和可选舞台目录；角色、动作、屏幕、灯光和特效存在预载后按需路径。
- `D:\Projects\UmaTools\UmaViewer-master5\captures\reference\live1176-content-aligned-20260912\manifest.json` 是优先的真实画面参考；尚未完成完整素材同帧捕获。
- `D:\Projects\UmaTools\UmaViewer-master5\tmp\render-analysis.txt`、`render-call-map.txt`、`render-disassembly.txt`、`render-pipeline-api.txt` 是已有逆向证据；磁盘原始 metadata 仅 21 字节，暂不重复普通 Cpp2IL 流程。

## 执行顺序

1. 完成阶段一诊断并通过 `tools\check_live_compile.ps1`；
2. 用真实 Live1176 会话导出诊断，确认加载统计口径和实际对象集合；
3. 只修阶段二中有证据的资源/生命周期缺项，再编译和隔离回归；
4. 记录真实相机/RT/Feature 顺序，进入阶段三；
5. 用本地二进制和运行时日志完成阶段四调用链映射；
6. 最后用同帧截图校验，不以局部合成 Shader 测试宣称完整还原。

### 2026-09-25：阶段一第 3 个增量——加载口径与实例化取证（未验收）

- 修复上轮中断的 `RecordProgress` 编辑：移除误写入的字面转义/单引号，进度记录只取第 1 项、每 64 项和末项以及完成事件，避免每一项都同步写盘。日志字段明确为 `progressCurrent/progressTarget/progressMessage`。进度条本身不改动。
- 在默认关闭的 JSONL 中区分 `UmaDatabaseEntry.Get/GetAll` 实际物化的资产、阶段/Part 的部分直接 `LoadAsset`，以及 Live 角色容器、Cutt、舞台对象的实例化。单次 `LoadAllAssets` 返回数、类型匹配数、选中资产与实例化的 Transform/Renderer/Light/ParticleSystem 数分别记录；**这只是已挂钩路径的观测，不是整个应用的资产加载总数**。没有加载新资源，也没有改动物理、景深、HDR 或后处理计算。
- 新增 `tools/summarize_live_diagnostics.py`：对单次 JSONL 输出预载根/依赖展开/含重复请求、实际打开/复用/失败、预载后的按需 Bundle、已观测资产获取、对象实例、播放相机及参考帧 308/868/3848。用合成的计数样本检查了分类输出；没有把其数值当作真实 Live 证据。
- `tools/check_live_compile.ps1` 通过，日志 `tmp/stage1-assets-compile-20260925.log`，仅既有警告。隔离 `tmp/dof-focus-test-project` 的 GPU/景深回归最终 exit 0，日志 `tmp/stage1-assets-gpu-verified-20260925.log`。首次隔离尝试因为未提供真实参考 manifest 环境变量而按预期在契约门控处退出 1；补齐已存在的本地参考、worksheet、radial fixture、shader bundle 后重跑通过。隔离验证不等同于完整 Live。
- 主 Unity Editor 保持原进程未关闭/未重启；隔离批处理已退出。`tmp/enable-live-diagnostics` 标记仍在。到本次结束仍**没有** `tmp/live-diagnostics-*.jsonl`，因此尚不能解释用户观察到的“数千级 vs 两位数”，更不能声称画面改善或阶段一验收。

**下一步硬门槛**：在主编辑器加载、播放一遍真实 Live1176（舞台 10148、参考阵容 14 槽）；完成后用 `python tools/summarize_live_diagnostics.py tmp/live-diagnostics-<会话>.jsonl` 分析。同次会话要包含预载和播放帧。不能安全驱动正在使用的编辑器，因此需要用户自己触发一次；随后对照二进制版同口径加载证据，而非复制进度数字。`AssetBundle.LoadAsset` 的其他直接调用点（包括其他监视器、特效路径）仍需按实际诊断缺口追加，不能把当前 `asset_load` 数当总数。

### 2026-09-25：编译版角色 flare-collision 资源链静态核对（阶段二取证，未实施）

- 已编译目标 `D:\Projects\UmaTools\UmaViewer\GameAssembly.dll` 与先前用于恢复的 `D:\Download\UmaViewer\GameAssembly.dll` 的 SHA256 相同（`d678a071...15b28b78d`）。`master5/tmp/scan_target_memory.py` 从另起的目标进程只读转储运行时映像，`convert_memory_image.py` 将映像重排成 `target-gameassembly-unpacked.dll`。这不是原始磁盘 DLL 的直接反编译。`tools/inspect_live_native_calls.py` 现在校验原始和恢复文件的完整 SHA256，版本不符则拒绝输出地址结论。
- 恢复版 `Gallop.Live.Director.GetLivePreloadEntries`（RVA `0x1a5be60`）在角色循环中于 `0x181a5c1ee` 调用 `TryGetCharacterFlareCollisionPaths`；仅在返回成功时，于 `0x181a5c21c`、`0x181a5c22e`、`0x181a5c240` 将三个输出逐一交给 `AddByOfficialPath`。`InitializeTimeline` 于 `0x181a5e755` 调用 `InitializeCharacterFlareCollision`，该方法又调用同一路径解析器并三次调用 `AddCharacterFlareCollision`。这证明预载和实例化复用同一角色资源解析流程，不证明每个角色三条都实际存在或成功加载。
- `TryGetCharacterFlareCollisionPaths`（RVA `0x1a68610`）在构造输出之前有空角色/服装检查、`TryGetMasterDressData`、多个 `TryReadMasterInt` 和 `TryResolveNormalHeadModel` 的成功分支；失败则返回 false。恢复的 IL2CPP metadata v31 的字符串字面量 1460–1464 包含 `3d/Chara/{Body,Head,Tail}/.../Flares/ast_..._flare` 五个模板。构造函数直接调用的三个格式化函数地址为 `0x1819ba670`、`0x1819bac20`、`0x1819bb080`，**尚未确认格式函数与五个字面量的逐一映射、实际服装字段值及官方路径到资源索引的解析结果**。不可把这些碰撞资源当普通可见 flare 贴图加载。
- master7 的 `Director.GetLivePreloadEntries` 当前没有这一角色分支；但预载部分最多每角色三个路径，单凭它无法解释用户观察的“数千级 vs 两位数”。它可能影响 flare 遮挡，而不是 Bloom 重复合成或总体亮度。现阶段没有行为改动。诊断标记仍在，到本次核对仍无 `tmp/live-diagnostics-*.jsonl`；主编辑器没有被关闭或重启。下一步继续解析格式函数的 IL2CPP 字面量引用及真实资产索引，并取得一次同一会话包含加载与播放的 Live1176 诊断，再决定是否实施。

### 2026-09-25：阶段一第 4 个增量——扩大实际资产调用观测（仍未验收）

- 默认关闭的诊断现在也记录舞台 flare 的 `LoadAllAssets<GameObject>` 和纹理/材质、LiveFlash 直接资产/兜底全量获取、监视器 livesettings/uvmovie、灯棒 CSV/图案纹理、旧模式动作的直接 `LoadAsset`。记录请求资产路径、返回资产名称、类型和结果，不增减加载或实例化。以 `AssetBundle.name` 标记的路径可能不是官方索引名称；`GetAllAssetNames` 仍不计作已加载资产。完整应用还有其它调用点，因此汇总统计明确为已观测下界而非资源总数。
- `tools/check_live_compile.ps1` 已通过，仅现有警告。独立项目 `tmp/dof-focus-test-project` 的 D3D11 回归日志 `tmp/stage1-coverage-gpu-20260925.log` 显示真实参考 manifest 契约、颜色、Bloom/Diffusion、PostFilm、RadialBlur、景深焦点和 80 个前景/远景 GPU 样本均 PASS，批处理成功退出。此测试项目没有完整 Live 场景及角色资源，**不构成视觉对齐验收**。没有修改物理、景深算法、HDR 开关或后处理强度。
- 仍需用户在主编辑器触发一次 Live1176（舞台 10148、参考 14 槽）的完整加载和播放。诊断标记已存在；此次没有会话 JSONL，未关闭/重启主编辑器，也未声称画面变化。随后先用 `tools/summarize_live_diagnostics.py` 分解进度数字与已观测资产，再选具体缺项继续查二进制。
- 诊断汇总复核修正了两种统计假象：`LoadAsset` 返回 null 不再计为 1 个实际物化资产；成功的 `LoadAllAssets` 不再被标成 empty。最终重新运行编译通过，独立 D3D11 回归 `tmp/stage1-coverage-gpu-final-20260925.log` 退出码 0，Bloom、PostFilm、径向模糊和景深用例均 PASS。仍未取得真实 Live 日志或完整画面，不能据此推断资源总数及视觉改善。

### 2026-09-25：阶段一第 5 个增量——资源索引与场景规模分口径快照

- 默认关闭的真实 Live 诊断在 `preload_callback`、`director_initialized`、`characters_loaded`、`timeline_music_initialized` 四个边界记录 `bundle_inventory` 和 `scene_inventory`；每个 Bundle 另记 `bundle_index`。统计已加载句柄、有效 AssetBundle、非 Bundle 句柄、`GetAllAssetNames()` 的索引候选数和索引错误，并统计已加载场景的根对象、Transform、Renderer、Light、ParticleSystem、Camera、VideoPlayer。它们都不是实际 `LoadAsset` 成功数；`DontDestroyOnLoad` 与编辑器对象不计入场景快照。扫描只发生在阶段边界，且只在诊断开启时；不逐帧扫描。
- 汇总工具新增按阶段的独立口径，保留原有预载请求、实际打开、已观测资产请求计数；合成 JSONL fixture 验证解析通过。`tools/check_live_compile.ps1` 通过，仅现有警告。当前仍没有真实会话 `tmp/live-diagnostics-*.jsonl`，所以这些统计尚无实际 Live 值。
- 针对恢复版 IL2CPP metadata/binary 增加 hash 校验的只读 `tools/TargetMethodLookup`。在目标 `Director.GetLivePreloadEntries` 函数范围内，`0x181a5c18f` 调用 `Gallop.ResourcePath.GetCyalumeScorePath`（RVA `0x19ba070`），返回值在 `0x181a5c19f` 交给 `AddByKey`。目标 metadata 有字面量 `Live/MusicScores/m{0:0000}/m{0:0000}_cyalume`；master7 原来只预载 part，没有预载 cyalume。现在仅在 `AbList` 真正存在对应 key 时将其加入现有去重预载列表；没有添加凭空资源，也没有修改渲染参数。这是可定位的一项预载缺口，不足以解释数千与两位数差异。
- **静态范围纠正**：`GetLivePreloadEntries` 的 RVA 起点为 `0x1a5be60`，其函数边界前的直接调用库存到 `0x1a5c681` 为止。此前待查列表中 `LiveTimelineData.GetWorkSheetBySheetIndexAndVariationId`（`0x1af2a20`）、`UmaAssetManager.LoadAssetBundle`（`0x1b5c300`）、`AssetBundle.LoadAsset`（`0x459a00`）、`PartEntry..ctor`（`0x1b7a930`）并非这一预载函数的直接调用；不能据此声称预载函数亲自解析 worksheet 或打开 Bundle。需要分别追查它们的真实调用方。
- 仍然需要同一次真实 Live1176 加载/播放诊断确认索引候选、实际已观测加载与对象数量。新增 Cyalume key 是否存在、是否从按需加载转为预载，也必须以实际日志为准；完整画质还原与过亮结论均未验收。物理、景深、HDR 及后处理强度保持不变。
- 隔离 `tmp/dof-focus-test-project` 的 D3D11 GPU/景深回归 `tmp/stage1-inventory-gpu-20260925.log` 完成，Unity batchmode return code 0；颜色、Bloom/Diffusion、PostFilm、Radial 和景深用例均 PASS。这仅验证现有局部 Shader/算法分支，本次真实 Live 的新预载和资源快照没有在该精简测试项目里执行。

### GLM 逆向资料审阅（`D:\Projects\UmaTools\umaviewer_re`）

- 实际目录名为 `umaviewer_re`，不是 `umaviewer\_re`。已阅读 `REPORT.md` 并抽查 `out/Gallop_Live_disasm.txt`、`out/Gallop_RenderPipeline_disasm.txt` 与工具源码；不是只转述报告。该工具用运行时解密 metadata + 解包 GameAssembly 做方法地址/字段/字面量注释，产物可以按 RVA 再核对。报告所列 `D678…78D` 是**磁盘上加壳 GameAssembly.dll** 的 SHA256，运行时解包映像的 SHA256 不同，不能把二者当作逐字节同一文件。
- **高可信且有立即价值**：目标 `Director.GetLivePreloadEntries` 的 `0x181a5c1ee` 调 `TryGetCharacterFlareCollisionPaths`，成功后 `0x181a5c21c/22e/240` 三次调用 `AddByOfficialPath`。master7 的 `Director.GetLivePreloadEntries` 现有 voice/cutt/part/cyalume/舞台目录，却没有这条按角色预载链。先核对官方路径解析和使用条件，再考虑补齐；它最多每角色三项，**不能解释数千与两位数差异**。
- **架构缺口**：目标有 `MultiCamera.MakeRenderTexture`（RVA `0x1a7c710`）、`MultiCameraComposite.Initialize`/`AfterRenderingCallback`/`UpdateCameraDepthRenderOrder`（`0x1a7ad70/0x1a7a810/0x1a7b3e0`）及 `MultiCameraFinalComposite`。master7 的 `MultiCameraComposite.cs` 只有枚举，`MultiCamera.Initialize()` 生成禁用相机而未建立合成纹理/材质链；`Director.InitializeMultiCamera` 只是创建对象。真实 Live 是否触发须结合时间轴和会话证明，不能把“存在方法”直接当作每帧执行。
- **渲染链证据**：`DofDiffusionBloomOverlayPass.Execute` RVA `0x1a19b90` 确有模式分支和 `_Bloom` 默认黑纹理绑定；`PostImageEffectFeature.AddRenderPasses` RVA `0x1a342c0` 可用于核对主相机过滤和 RT 交接。DXBC 公式是 Shader 层静态证据；报告中“一帧完整路径”混合了调用点、不同模式分支与推论，**不是某个真实 Live 帧的逐 pass 捕获**。master7 的 `PostImageEffectFeature` 自注尚有未实现的目标 pass，且用了 `ColorChainSchedule` / early resolve 等本地适配，须实测输入/输出后判断过亮，不直接加减 Bloom 强度。
- **未由这份资料解决**：目标 UI 进度数字的计数实现、目标实际打开/物化资产数量、Live1176 某帧是否启用 MultiCamera/flare/各后处理、相机 RT 格式和完整可见画面的同帧差异。当前没有 `tmp/live-diagnostics-*.jsonl` 实际会话；报告不能证明数千数字的口径或画面已经对齐。
- **下一顺序**：① 追目标加载进度更新函数及计数口径，并采 master7 完整 Live 会话；② 对照实际歌曲的时间轴和场景对象，优先验证并补 MultiCamera/监视器/灯光缺环（限确实启用者）；③ 给渲染 pass 输入/输出、相机过滤、事件顺序加同帧可观测证据，排除重复合成；④ 最后同帧截图校验。物理、已确认景深和 HDR 状态不动。静态反汇编查的是“可能路径”，不能代替运行时触发证据。

### 2026-09-25：GLM 进度计数反汇编后的资源根注册修复（阶段二，待真实 Live 验证）

- GLM 的 `<PreLoadAsset>d__35.MoveNext`（RVA `0x1b564c0`）与本地代码交叉核对：进度分母是每个 root 经 `SearchAB` 展开的 `loadItems.Count`，重复依赖及缓存命中仍计入；不是 Bundle 内 `LoadAsset` 数。master7 的分母/递增路径原本已基本同口径，不能通过修改 UI 数字解决差距。
- 查 `D:\Projects\UmaTools\umaviewer_re\out\global_disasm.txt`：目标 `LiveResourceRegister.RegisterDownload`（`0x1ab98b0`）在 `Director.GetLivePreloadEntries` 后逐角色读取 `ReadCharaData`，调用 `CharacterResourceRegister.RegisterNormal`（`0x1ab2130`）/`RegisterMob`（`0x1ab1e60`）。`RegisterFolder`（`0x1ab1910`）遍历 AbList.Values，以 `OrdinalIgnoreCase` 匹配路径前缀；`RegisterFaceRuntime`（`0x1ab15e0`）注册 driven-key、tear、eye-emotion 等存在的路径。`DownloadPathRegister.RegisterEntries`（`0x1c0a240`）取 Entry.Name，`RegisterPathWithoutInfo`（`0x1c0a410`）归一化并去重，最后 `ResolveEntries`（`0x1c0a710`）解析。因此 master7 原先只用 Director roots 是明确的上游缺口。
- 新增 `Assets/Scripts/umamusume/Gallop/Live/LiveResourceRegister.cs`，按角色注册元数据中实际存在的身体、头部、尾巴前缀与共享表情资产，先 Director 后角色，按 Name 去重。普通角色的特定头/默认头、专用尾/默认尾分支及 Mob 的固定头/尾路径依据上述 RVA；仅对当前选择的角色做扫描，不虚构条目、不全库预载。`LoadLive` 现改为用此结果启动原有 `PreLoadAndRun`。目标可选 idle motion 条件因调用方传 false 未启用。
- JSONL 新增 `resource_roots` 分类（Director、普通角色、Mob、跳过角色、独立总数）；`tools/summarize_live_diagnostics.py` 输出分类，同时保留 `preload_counts`、重复请求、实际打开和缓存命中。编译 `tools/check_live_compile.ps1` 通过，只有旧警告；合成 JSONL 摘要测试通过。物理、景深、HDR、后处理 Shader 与强度未修改；用户主 Unity Editor 未关闭/重启。
- **尚未验收**：没有真实 `tmp/live-diagnostics-*.jsonl`，无法声称数千级数字已经对齐、所有预载 Bundle 已成功打开或 Live 画面有提升；隔离 GPU Shader 回归不覆盖此新资源注册分支。需要在已有 Editor 中启用标记后加载一次目标 Live，读取同一会话的 `resource_roots` → `preload_counts` → `preload_complete` → 播放帧与缺失/失败项。比较目标二进制在相同歌曲/角色/舞台下的分母，再继续追差异；若大规模缺失先修具体资源路径/下载链，而非上调后处理。`SearchAB` 闭包细节和目标 `ResolveEntries` 过滤/排序仍有待精确核对。

### 2026-09-26: Per-root preload accounting (stage 1/2)

- Cross-checked the supplied progress chain: target `PreLoadAsset` invokes `SearchAB` for each root at RVA `0x1b56cdf`; after `AcquireOne` at `0x1b56dff`, completed increments unconditionally at `0x1b56e07`. The denominator counts dependency-expanded **bundle requests**, including repeat requests and cache hits, not inner assets.
- Target `SearchAB` RVA `0x1b5d6a0` reads the semicolon-separated dependencies at `entry+0x58`, caches them at `entry+0x70`, and returns root plus dependencies. Master7 is substantially similar, but contains an extra `ResolveLaserCandidateIfMissing` fallback. Whether it triggers in this Live is unverified; do not remove it just to change the progress number.
- Target excludes an unready optional audio request during `loadItems` construction via `IsOptionalAudioEntry` RVA `0x1b5c210` and `UmaViewerDownload.IsAssetReady` RVA `0x1bab1a0`. The latter calls `TryGetReadablePath` RVA `0x1bab710`, which also checks pending download state and integrity metadata. It is NOT equivalent to `File.Exists`. Master7 currently has no such filter, so its denominator may be too high when optional audio is absent. This round does not guess at that behavior or change loading.
- Added opt-in `preload_root` JSONL records: root name/index, expanded request count, and repeated requests across roots. The summary lists the 20 largest contributors and checks the sum against master7's UI denominator. This is measurement, not a fabricated load count. C# compile passed with prior warnings; synthetic JSONL accounting passed. No real `tmp/live-diagnostics-*.jsonl` exists yet; numerical and visual parity are unverified.
- Next: load Live1176 once in the already-open Editor, inspect `perRootCoverage`, root sources, and missing bundles. Compare the same song/characters/stage against target root and `SearchAB` counts. Only then repair a specific missing root/dependency. Physics, DOF, HDR, and post-effect strengths were unchanged.

### Real Live preload validation: sessions 20260925-170518 and 20260925-170638

- The first opt-in real session is song 1176, stage 10148, 14 selected characters. It completed preload with 1,207 distinct roots (18 Director + 1,189 character roots), 1,388 unique dependency-expanded entries and **1,757 request attempts / UI target**. The per-root sum is exactly 1,757; 369 are repeated names across expansions. Outcomes: 1,373 bundle opens, 14 non-bundle loads, 370 handle reuses; no recorded missing files, load failures or exceptions in this preload. Count units are requests, not 1,757 distinct assets.
- A second real session is song 1001, stage 10102, 18 selected characters: 1,300 roots (20 Director + 1,280 character), 1,495 unique expanded entries, **1,882 request attempts / UI target**; 387 repeats. Outcomes: 1,473 bundle opens, 16 non-bundle loads, 393 handle reuses; no recorded preload failure. This independently confirms the request-count arithmetic, not target-binary parity.
- The Live1176 `preload_callback` inventory reports 2,690 loaded handles, which includes handles that were loaded before the counted preload; do not mislabel this snapshot as 2,690 opens by this session. Its sampled playback ran through frame 1830 (~30.5s), with `CameraTimeline1` and the `DiffusionDofBloom` mode in sampled frames. It does not cover reference frame 868 exactly or frame 3848 (~64s), nor does a pass log prove pixel equivalence.
- Result: the former two-digit progress was substantially due to missing selected-character resource roots, now corrected structurally. The counts are in the same *order of magnitude* as the user's observation of the binary viewer, but there is no exact same-config binary numerator/denominator or matched-frame visual test yet. Stage 1 resource-count measurement and stage 2 root-accounting portions have real evidence; full visual/render-chain acceptance remains open. Do not raise Bloom or change physics/DOF based on this count alone.
- Next checkpoint: obtain a target-viewer load count for song 1176/stage 10148 with identical 14-character selections, then capture master7 at frames 868 and 3848 and inspect actual active camera/RT/pass sequence and output. Keep registration as-is unless those measurements reveal a specific missing resource class.

### 2026-09-26: Render-pass execution and RT handoff checkpoint

- Native disassembly `D:\Projects\UmaTools\umaviewer_re\out\Gallop_RenderPipeline_disasm.txt` confirms that `DofDiffusionBloomOverlayPass.Execute` (RVA `0x1a19b90`) dispatches `DiffusionDofBloom` to `OnRenderImageDiffusionDofBloom` (RVA `0x1a1bbb0`), and calls `PostImageEffectFeature.SetSourceRT` at `0x181a1a661` in the temporary-output branch before `ExecuteCommandBuffer` at `0x181a1a691`. Target `RegisterPass` (RVA `0x1a35ac0`) contains conditional exposure and grading branches not yet present in master7's reduced feature. These are static branch/order facts, **not** proof of which branch executes at any particular Live frame.
- Audited master7's scheduling: `SourceSetup` sets the current camera target; DOF at event <=550 hands its temporary RT to the feature and at event >550 writes back to camera; radial and color each replace the feature's source RT; `FinalBlit` copies the remaining RT back and releases it. With color at 550 and a later DOF event, `ColorEarlyResolve` is a separate pre-DOF camera handoff. The current code contains no evident unconditional second DOF composite, but static inspection cannot rule out frame-specific duplicate composition elsewhere (e.g. URP post-processing or other renderer features).
- Added **opt-in, read-only** `render_execute` JSONL records to `LiveRuntimeDiagnostics`. These are emitted from the pass `Execute` methods **after command-buffer submission**, recording `renderStep`, pass event, input/output RT identifiers and IDs, camera, song, Live frame and Unity frame. Unlike the existing `render_pass` record, this establishes that the managed execution path ran; it still does **not** establish GPU completion or pixel equivalence. `ColorEarlyResolve` and final resolve have distinct labels.
- Activate only when ordinary Live diagnostics are enabled **and** `tmp/enable-live-pass-trace` exists (or `UMA_LIVE_PASS_TRACE=1` was inherited by the Editor). The marker is currently **absent**, so this addition does not alter the user's running playback. It samples only selected Live frames (defaults 308, 868, 3848, accepting a one-frame timing offset). After the Editor recompiles, run Live1176, stage10148 with the same 14-character configuration and capture frames 868 and 3848. Compare all `render_execute` entries for the same `unityFrame` and `camera` with `render_pass` records; only then investigate over-bright compositing. Do not increase or decrease Bloom merely from the old enqueue count.
- `tools/check_live_compile.ps1` passed (pre-existing warnings). No Shader, physics, DOF, HDR or effect intensity was changed; no isolated GPU or real Live playback was run in this round. Remaining: the binary viewer's same-configuration RT trace and matched screenshots, and runtime master7 execution data at 868/3848.

- The `render_pass` record now also includes URP's actual `cameraData.postProcessEnabled` flag (`urpPostProcessing`). The serialized Live prefab has `m_RenderPostProcessing: 0` on its camera entries, but only a same-frame runtime record can establish whether that remained off; do not infer URP Bloom absence from the prefab alone.

### 2026-09-26: third real Live1176 session and follow-up

- Inspected `tmp/live-diagnostics-20260925-172955-249.jsonl`: song1176/stage10148/14 character slots, 1,224 roots, 1,417 unique expanded entries, **1,803** requests; preload complete reports zero missing files and zero load failures. The 14 selections are not proven identical to the earlier 1,757-request session; the count difference must not be attributed to a regression without the character IDs/configuration.
- Across 543 `render_pass` records for `CameraTimeline1`, `cameraData.postProcessEnabled` is **false**; descriptor `R8G8B8A8_UNorm`, 941?488, event `BeforeRenderingPostProcessing`. This excludes URP's built-in post-process pass as a likely *second* composite for this session, not the custom feature or any other renderer feature. Existing `render_pass` data still only represents enqueue.
- There were zero `render_execute` records because the additional trace marker was absent for that run. Sparse `live_frame` samples include 60..2670, then 7230 and 9240, but not 868/3848. Since observed playback may advance about five Live frames per Editor frame, the execution trace now accepts samples within ?6 Live frames of each selected target and records the actual frame rather than claiming exact parity. C# compile passed; `tmp/enable-live-pass-trace` is now present for the **next** Live session. This opt-in logging does not change effect values or the renderer. Run/seek to ~14.47s and ~64.13s after recompilation; stop the playback once both points have been crossed, then inspect the resulting JSONL. Do not call the missing records a render failure.

### 2026-09-26: real submitted pass chains validated (session 20260925-173827-227)

- Real song1176/stage10148/14 slots produced **34 render_execute records across 8 camera/Unity frames**. At Live frames 302,308,867,865,870,874 the submitted chain is SourceSetup -> DofDiffusionBloomOverlay -> SimpleColorCorrection -> FinalBlit. At frames 3845,3850 it is SourceSetup -> DofDiffusionBloomOverlay -> RadialBlur -> SimpleColorCorrection -> FinalBlit. Revisits around 868 have different Unity frames and must not be mistaken for duplicate passes within one frame.
- All 8 groups match their scheduled pass counts (4 or 5), contain no repeated recorded step, have identical preceding output/current input RT identifiers, and end at the initial camera RT. All 279 enqueue records have URP built-in post-processing disabled. This is evidence against whole-pass repetition or lost radial output **in these sampled frames only**. Exact reference frames868/3848 and pixel luminance were not captured.
- Added execution-chain reporting to `tools/summarize_live_diagnostics.py`, grouping by Unity frame AND camera, checking counts, adjacent RT continuity and camera return. Validated against the actual 34 records and a deliberately broken-handoff fixture. Saved complete report to `tmp/live-diagnostics-20260925-173827-227-summary.json`. No runtime renderer changes in this round; no Unity compile/GPU regression needed for the offline reporting change.
- Continued native internal audit: `OnRenderImageDiffusionDofBloom` calls BlurBlt at VA0x181a1c240, DiffusionFilterProcess at0x181a1c2ae, conditionally CreateBloomTexture at0x181a1c3a7, and PostFilmBlit at0x181a1c5c4; the source follows this outline. Native PostFilmBlit submits/clears at0x181a4cfb3/0x181a4cfc9 and0x181a4d192/0x181a4d19c. The current source likewise clears after these internal submissions, so the outer submission does not by itself prove repeated Bloom commands. Internal shader variants/parameters and pixel output still require verification; this is not visual parity.
- Next investigation can proceed using the recovered assets/binary: trace Bloom/Diffusion/PostFilm enable flags and numerical parameters at these timeline points, and verify actual Shader pass selection and intermediate texture formats. There is no reason to request another identical outer-chain playback run. A future visual capture should include intermediate images and parameter evidence together.

### 2026-09-26���� GLM �����ƽ� MultiCamera �ṹȡ֤�����-only��
- δֱ��ʵ��δ����֤�� MultiCameraComposite���ȸ�ʵ�� Live ֡����ֻ���ֶ� `multiCameraCount`��`multiCameraActive`��`multiCameraDetails`��
- ������ڿɼ�¼�Ѵ����� MultiCamera ������ʱ�����Ƿ��ڵ�ǰ֡���á�ÿ������� enabled/depth/targetTexture������ѡ�Դ����� MultiCamera������Ϊ����ǰ Live �Ѳ������պϳɡ���
- δ�ı����������HDR��Bloom��PostFilm��������ػ���Ļ���ʣ�`tools/check_live_compile.ps1` ͨ���������� warning����
- ��һӲ�ż����û����� Editor ����ͬ���� Live1176 һ�Σ���ȡ�� JSONL �� `live_frame`���� `multiCameraActive=1` ������� target/composite ���ѣ�����ʵ����֤��֧�ֵĺϳ�·������ʼ��Ϊ 0����ת�� Monitor/Particle/Spotlight ʵ�ʹ����

### 2026-09-26��MultiCamera ��ʵ Live ȡ֤���
- �»Ự��`tmp/live-diagnostics-20260926-003519-764.jsonl`��
- 11 �� `live_frame` ����ȫ��Ϊ `multiCameraCount=2`��`multiCameraActive=0`������ MultiCamera �� `off`��`depth=-3`��`targetTexture=screen`��
- ����ڱ��� song1176/stage10148/14��ɫ�Ự�Ĳ���֡�У����ܰ� MultiCameraComposite ��Ϊ��ǰ����ȱʧ����Ҫ�޸���ݲ�ʵ��δ��֤ʵ�ĺϳ�����
- ͬһ�������ȶ���¼ `cameras=6`��`lights=0`��`particleSystems=0`��`videos=0`��`stageObjects=33`��`stageMapObjects=1466`��`renderers=846`����ʹ�����е� Spotlight/Particle/Monitor/VolumeLight ȱ�ڳ�Ϊ��һ����ֵ�ú˶Եķ��򣬵����������Բ���֤��Ŀ��ͬ֡һ����������Щ����
- ��һ������Ƶ�ǰ��̨/ʱ������Դ�Ƿ��� particle��spotlight��monitor��light-shaft ���ݣ�����Ŀ���������׷��Ӧ��Դ������ڣ�����ʵ���е�ǰ Live ���ݺ͵�����˫��֤�ݵĹ����

### 2026-09-26��ʱ������Чʵ������ȡ֤���ȴ��û����У�
- ��չ `live_frame` ֻ����ϣ�`timelineSheets`��`particlePrefabNames`��`spotlightPrefabNames`��`lensFlareGroups`��`monitorCameraEnabled`��`monitorPosTracks`��`monitorLookAtTracks`��
- ��Щ�ֶ�ֻ��ȡ�Ѽ��� `LiveTimelineData`/�׸� worksheet����ʵ�����κ���Ч�����ı�ʱ����ͻ��档
- ����ͨ������һ����Ҫ�û����� Editor ������һ��ͬ���� Live��ͨ����ʵ�����жϱ����г��� Particle/Spotlight/LensFlare/MonitorCamera ��Щȷʵ���ڵ�ǰ Live�������ǽ����������Ͷ���������ͨ��·����

### 2026-09-26����ǰ Live ʱ������Ч���ݽ��
- �»Ự��`tmp/live-diagnostics-20260926-004117-663.jsonl`��8 �� `live_frame` ������
- ȫ��������`timelineSheets=1`��`particlePrefabNames=0`��`spotlightPrefabNames=0`��`lensFlareGroups=0`��`monitorCameraEnabled=false`��
- `monitorPosTracks=1`��`monitorLookAtTracks=1`��˵��ʱ���ᱣ�� MonitorCamera ����ṹ������ǰ Live ������ģʽ���عرգ����ܾݴ�ʵ�� MonitorCamera �����·��
- ��ͷ 1/4/7 ����� `particleSystems=104`��֮��Ϊ 0������񿪳�/Flash ��Դ��ʱ���ڣ������ǵ�ǰ����ȱʧ�� Particle timeline����ǰ����û�� particlePrefabNames�����ܽ������ Particle ϵͳ��Ϊ������Ҫ�޸��
- ��ʵ��������δ֧���ڵ�ǰ Live ֱ��ʵ�� Spotlight3d��LensFlare��MonitorCamera ���Զ��� Particle �������������֤��������Դ/��Ч��
- ��һ��ת��̬�˶����� `GallopImageEffectParameter`/timeline ����������Դ�� `PostFilmBlit` gate������ͬ֡�����Ӿ�ȷ�ϣ�������һ�δ� pass trace �Ͳο�֡�����С�

### 2026-09-26������ʱ�����ֶ������ڣ����-only��
- ׷�� `live_frame` �ֶΣ�`postFilm1Keys/2Keys/3Keys`��`bloomKeys`��`dofKeys`��`radialKeys`��`colorCorrectionGroups`��
- Ŀ�ģ��ڼ����Ĳ���ǰȷ�ϵ�ǰ Live ��ʵ�ʺ���������������֡�������ڵ�δ���á��͡�master7 ȱ�ٹ�����ѡ���
- ��ǰԴ����ȷ�� PostFilm ������ `AlterUpdate_PostFilm` ÿ֡�������� layer 2 �󷢲���ColorCorrection/RadialBlur Ҳ����ȷ�¼����ѡ���δʵ��Ŀ��������е� Exposure/Grading����Ϊ��ǰ master7 û��ͬ��ʱ�������ݽṹ������ʱ����֤�ݡ�
- ����ͨ������һ����ʵ������Ҫ�û���������ȡ�ù��������������λ����֡�Ĳ�����Դ��

### 2026-09-26�����������ʵʱ��������
- �»Ự `tmp/live-diagnostics-20260926-004823-272.jsonl` ��ʾ��ǰ Live �� 70/129/106 �� PostFilm key��Bloom 72��DOF 113��Radial 78��ColorCorrection 1������֡�� DOF/Bloom/Diffusion/ColorCorrection ������Ч��Radial ��ʱ�����Ъ���á�
- ��֤����ǰ Live ȷʵ�������� PostFilm �� Bloom/Diffusion/DOF����Ӧ�ر�����Ч����Ҳ˵������������밴ʵʱ��ֵ��Ŀ������ƻ�Ϸ�ʽ��λ��
- �����׷�� Bloom threshold/intensity/blur/DOF weight �� Diffusion bright/threshold/blur/saturation/contrast ��ֵ�ֶΣ����޸���ֵ��
- ����ͨ������һӲ�ż����û���������һ�Σ���ȡ��Щ��ֵ������ο�֡ 868/3848 ���������ܸ��ǣ���Ȼ�������Ŀ�������Ĭ��ֵ/������Դ�˶ԡ�

### 2026-09-26��PostFilm/Bloom ����ȡ֤�����ƽ�
- �û���һ����־������ Live frame 3848��64.132s����ȷ�� BloomIntensity=6.69��Threshold=0.95��BlurSize=2��DofWeight=1��Diffusion Ϊ Bright=1��Threshold=.075��Blur=1��Saturation=1��Contrast=1��DOF/Bloom/Diffusion/Radial ȫ����Ч��
- ׷��ֻ���ֶΣ����� PostFilm �� mode��Validity��Power��ColorBlend������ȷ�Ϲ���֡�Ƿ�����ʵ����ӡ�Add/Multiply/Screen ģʽ��Ĭ�ϲ�������
- `tools/check_live_compile.ps1` ͨ����û���޸�Ч����������һ����ʵ���в���Ҫ�û���������ǰ������̬�˶�Ŀ�� PostFilm pass/Blend �� master7 ʵ�֡�


## 2026-09-26: autonomous continuation, provenance audit and corrections

This entry supersedes the overly broad exclusions in the preceding sampling notes.

### Corrections to prior conclusions
- `monitorCameraEnabled` was populated from `MonitorCameraSettings.IsEnabledTextureWidthRate`. It was NOT camera activation. Renamed the field to `monitorTextureWidthRateEnabled`; old logs cannot exclude monitor capture/compositing.
- `live_frame` formerly required an exact frame multiple of 30. Playback skipping those frames produced extremely sparse samples. Sampling now records the first observed frame in each 30-frame bucket (plus exact target frames), including backward bucket transitions. It still is not an exhaustive per-frame activation trace.
- Particle/Light counts are scoped to Director children, not the entire scene. The earlier session had 104 particles at 1/4 seconds, zero from 7 seconds in the observed samples. Neither result excludes particles elsewhere.
- PostFilm key counts prove track presence, not valid/enabled layers at a given time. The null camera targetTexture label likewise does not prove absence of SetTargetBuffers.

### Work performed without another user playback
- Exported all Bloom and three PostFilm tracks directly from the local game's son1176_Camera typetree to `tmp/live1176-effect-source.json` using master5's read-only asset utility. No Config.json was read.
- Added `tools/audit_live_bloom_source.py`: checks nine numerical fields against held-key intervals, uses the NEXT key's interpolation mode, skips curves instead of approximating them, and filters song identity. Six existing live_frame records in session20260926-005328-470 match all nine fields within 0.0001. Saved `tmp/live1176-bloom-source-comparison.json`. Injected-value mismatch and wrong-song negative controls pass.
- At frame3848 the bounding keys are3819/3972; next-key interpolation is None. Asset key3819 has threshold0.949999988, intensity6.690000057, blur2, weight1, diffusion threshold0.075000003, brightness/saturation/contrast1. Thus these sampled numbers were not invented intensity settings. This does NOT establish target-viewer evaluation parity, intermediate image correctness or absence of internal double composition.
- BloomBlendMode on this key is0 (Screen); master7 maps0 to `_BloomIsScreenBlend=1`. Added the runtime enum name to future diagnostic snapshots. Existing logs do not measure this uniform's actual draw-time GPU value.
- At the same interval PostFilm layer1 mode5/power4.159999847, layer2 mode5/power0.059999999; layer3 mode0 (disabled), although it retains a nonzero stored power. Counts alone were misleading.
- Replaced DofDiffusionBloomOverlayPass's reflection-based PostFilm dispatch with the concrete public typed method/ref arguments. Native direct-call evidence includes VA0x181a1c5c4 to0x181a4cad0. Retained null/error fallback and existing command ordering. Removes boxing/method discovery, NOT a claim of brighter/darker visual improvement.
- Removed an accidental literal-backslash-n diagnostic insertion from the disabled LegacyCompatibility timeline file. Active timeline accessors are retained.
- Investigated the missing movie/blend-factor transfer in Parameter.Setup but did NOT change it without target nested-method evidence; the renderer disassembly file does not contain this nested Setup implementation. Current color-only timeline bypasses UV movie tracks, so wiring texture fields alone would not restore movie rendering.

### Tests and remaining boundary
- `tools/check_live_compile.ps1`: passed (existing warnings).
- Isolated project, refreshed umamusume.dll: PostFilm timeline/GPU passed; DOF focus passed; foreground DOF80 GPU samples passed; Bloom/Diffusion24 extraction +9 Bloom +27 diffusion cases passed, including existing Bloom-once layer checks. These tests are NOT an end-to-end execution of the changed typed wrapper in a real Live scene.
- Initial PostFilm GPU invocation lacked UMA_POSTFILM_TEST_BUNDLE and failed its precondition. Retried with existing local tmp/postfilm-shaders.unity3d and passed. No shader assets downloaded.
- Added an explicit user-invoked menu `UmaViewer/Rendering/Capture current Live diagnostic frames (308,868,3848)`. It uses the actual currently loaded song/stage/cast, unlike the reference menu's default song1001 preset. It delegates to the existing settled off/on capture and restores playback/render-feature state afterwards. It is a same-session diagnostic capture, NOT a matched game-reference comparison. Not auto-invoked on the user's Editor.
- New capture entry compiled in the isolated Editor. Capture-contract test initially lacked its two reference-manifest environment paths; retry uses existing master5 live1001/live1176 manifests.
- Physics, DOF math, HDR state, Bloom/Diffusion strengths and radial unknown bits unchanged. Main Unity not closed/restarted/controlled.
- Next actual acquisition: in the loaded Live1176 choose the new diagnostic menu once. It saves six off/on images plus parameters/identity under captures/render-smoke. This is the first pixel-input/output checkpoint, not another full-song counter-only run. It does not yet capture individual internal Bloom textures. Before using game-reference images, verify exact identity and alignment.

- Final capture-contract retry: PASS, exit0 (tmp/continuation-capture-contract-retry.log). New menu was compiled; actual interactive capture remains unexecuted. All isolated Unity instances exited; main Editor remained running.


## 2026-09-26: capture identity failure fixed; session011553 examined
- User invoked diagnostic capture, but no captures/render-smoke directory was created. Editor exception points to LiveCapturePlan.Validate from BeginCurrentLiveCapture line106: Invalid reference character/costume identity. Builder retains full generic costume IDs with underscores; capture validation incorrectly allowed exactly one separator for the entire character+costume string.
- LiveCapturePlan.ParseCharacterIdentity now splits only at the FIRST underscore, validates numeric nonempty costume components, and retains the complete costume. Both validation and isolated automatic character selection use the same parser (the old selection also truncated costumes). Exact cast comparison remains strict; no costume normalization/guessing.
- Regression covers short and generic costume identities, invalid/empty/path-like values, serialization roundtrip, and rejection of a different costume. Runtime compile passed; isolated Editor compiled all changed editor sources and LiveCaptureContractRegression.Run passed, exit0 (tmp/capture-identity-regression.log). This verifies parsing, not successful real scene capture. No physics/DOF/render strength changes.
- Session live-diagnostics-20260926-011553-248 provides309 live_frame snapshots, showing the new sampling is substantially less sparse. Offline held-key audit compares300 snapshots:299 match all nine Bloom/Diffusion values; the sole mismatch is frame0 with initial/default parameters. This is not evidence by itself that the default was actually drawn, and it is retained in the report instead of silently excluded. Nine remaining frame snapshots are outside this held-key comparison coverage.
-30 submitted pass records form7 camera/frame groups; the existing chain checker reports no issues for these sampled outer chains. Still no pixel capture and no exclusion of internal composition faults.
- Correction to preceding note: PostFilm key3819 layer1 raw power4.16 is NOT the evaluated power at frame3848. The next key3874 selects Curve interpolation; actual sampled frame3844 power0.7138 and frame3870 power0.3087. Bloom's next key differs and selects None. Do not apply Bloom's held-key reasoning to PostFilm. At frame3844 recorded BloomBlendMode=Screen, film validity True|True|False, corroborating layer3 disabled in that sample.
- Required next user action: after editor script compilation, load Live1176 and invoke the same diagnostic capture menu. Successful completion must log [LiveRenderCapture] Completed and save six PNGs; an ordinary complete-song playback does not substitute for this capture. Main Editor was not controlled; only isolated verification was launched.


## 2026-09-26: successful real capture and off-screen preview UX
- Latest session captures/render-smoke/20260926_092541_878 completed all six PNGs and per-frame metadata (308/868/3848 off/on). Earlier incomplete folder is not the accepted set. User's "No cameras rendering" screenshot is explained by capture assigning the sole display camera's targetTexture to an off-screen RT; timeline is deliberately held for the 60-render stability gate. No Editor pause is requested by this tool. Completion is confirmed in Editor log, not inferred from the image.
- Added a utility preview showing the captured RT, target frame, off/on state and saved count, plus cancel/restore button and menu. Closing preview cancels. Added cleanup before script reload and guard against starting during delayed restoration. Game view itself still has no display-target camera during capture; this deliberately avoids injecting a second camera/composite pass into measurement. Preview UI has only compile validation, not interactive verification.
- Runtime compile passed; isolated Editor compiled the changed capture script; capture-contract regression passed exit0 in tmp/capture-preview-regression.log. No renderer/physics/DOF math changes; no need for another real acquisition solely to validate preview.
- Created comparison.png and pixel-summary.json in the successful capture folder. All source PNGs are1920x1080. Post-effects visibly add blue coloration/glow; pre-post frame868 already contains extensive white stage panels. Near-white(all RGB>=250) percentages off/on:308=6.92/4.08,868=24.46/18.34,3848=12.70/7.03. At868 any-channel>=250 rises30.43->36.54 percent. These are encoded output pixel counts, NOT linear luminance or proof of overexposure. White area alone cannot diagnose repeated Bloom.
- Off/on toggles the entire custom feature including DOF/color/film, not Bloom alone. Current real cast includes full0002_00_00 costumes; do not claim parity to reference manifests with different cast/costumes. Need internal-effect attribution and matched reference before changing strengths.


### 2026-09-26: stage 4, evidence-backed film source/enable synchronization

- Completed the local portion of a missing parameter-source chain: native RVA `0x1ae70c0` PostFilm four-corner BlinkLight sync, key brightness/adjustment, alpha-preserving RGB and next-sync hold are implemented and fixture/GPU tested. A real captured pre-post PNG film-only attribution demonstrates that the next-sync correction changes frame3848 output without altering any effect strength; it is **not** a full Live visual comparison.
- Created a reproducible SHA-pinned attribution fixture generator and isolated GPU experiment; kept existing physical simulation, DOF math and HDR settings intact. Capturing synced Live1176 windows at frames900/1240/3900, checking `filmBlinkLookup`/`filmColor0`, then comparing same-identity full frames is the next runtime validation boundary. Do not mark stage4 complete until the stage lookup/time-owner and complete rendering are checked.
- Previous basic GPU tests cannot establish full-stage lighting/RT parity. Native mixed ARGBHalf/default temporary RT formats and `Parameter.Setup`'s omission of `ColorBlendFactor` have direct disassembly evidence; do not alter these speculatively.

### 2026-09-26：阶段四继续——真实同步窗口及三态归因
- 完成同一会话 900/1240/3900 的真实 off/on 捕获和身份审核；Stage Blink RGB 同步在这三个稳定帧均命中，层1在1240功率0、层3在3900 mode=None。旧参考角色/服装与本次不同，仍不能作逐像素验收。
- 扩充 SHA 锁定的 Film-only 官方 Shader GPU 归因：九个功率及九个 Color0 与元数据一致，蓝色叠加无需假设 Bloom 重复也能出现，但实验没有实际深度/Bloom，不能作为完整渲染还原证明。
- 新增仅诊断用 off/noFilm/on 三态菜单，隔离 PostFilm 与其他链条；脚本编译和隔离采集契约通过。下一步只需一次**不同于此前六张图的三态真实采集**来判断过亮的归属；转换帧灯色时钟及同身份游戏画面对齐依然未完成。阶段四尚未验收，物理/DOF 保持不变。

### 2026-09-26: three-state capture reliability gate
- Verified URP 14 begin/AddRenderPasses/Execute/Submit/end ordering against local package source and shared runtime parameter routing. Added a per-render `noFilm` callback-success gate and mode-only restoration, plus isolated Editor contract coverage. Runtime compile and `tmp/film-isolation-scope-contract.log` succeeded. No physics/DOF/render-strength changes.
- Stage 4 remains in progress. The nine-image real-Live three-state comparison is not yet acquired. The available six off/on frames and synthetic film-only GPU experiment do not establish full-chain or reference parity. Next required observation: one real run of the new `off/noFilm/on (900,1240,3900)` menu; do not repeat the completed off/on acquisition.


### 2026-09-26 Stage 4: Live1093 stage geometry/shafts branch
- User visual feedback selects Film-only as a useful target but not full-chain proof; actual Live1093 shows front orange translucency and reported floor depth fighting, while original has warm atmospheric shafts.
- Fixed a definite stage-ground-panel exclusivity ordering/depth-visibility bug; compile and isolated DOF regression pass, but Live1093 runtime resolution is **unverified**. Added opt-in stage-renderer/possible coplanar-pair snapshot at 20 s; use existing diagnostic workflow on next natural playback, not a new nine-frame capture request.
- Open: identify exact translucent object and floor mesh pair; check live1093 authored shafts/fog keys plus original pass flow. Do not suppress stage geometry or add speculative fog until those are evidenced. Stage 4 remains open.
- Isolated synthetic ground-panel regression passed (Unity exit0); this narrows one provable depth-writing bug but does not establish the user's entire floor z-fight is fixed. Native SunShafts pass/initialization confirmed; authored settings will be logged at next natural playback before any implementation.

### 2026-09-26 Stage 4: Live1093 natural playback interpretation
- Frame1201 confirms mirror/shadow coplanar **bounds**, not overlapping triangles. `_blinklight_ground_panel` is absent: its previous fix is not a Live1093 repair. Main ground meshes escaped the thin-AABB filter. The next natural playback can supply triangle-height and material/mirror bindings through the extended read-only diagnostic.
- SunShafts settings exist (intensity0.5, sunPower0), but active enable and RT flow remain unproven. Keep Stage 4 open; no speculative geometry shift, fog or strength changes.
