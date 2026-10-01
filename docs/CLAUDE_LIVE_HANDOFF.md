# UmaViewer Live 还原交接（2026-09-25）

## 首要问题

工作区为 `D:\Projects\UmaTools\UmaViewer-master7`，目标是还原已编译的 `D:\Projects\UmaTools\UmaViewer` 的完整 Live 画面。用户指出：二进制版加载 Live 时进度数字为数千级，master7 为两位数；此前长期修后处理却没有明显画质改进，并且部分画面过亮。**下一步先对齐资源收集、加载和进度计数口径，再判断渲染链是否需要修复。**

这两个数字目前来自用户目测，尚无同条件进度日志、对应的二进制计数函数或两边完整资源清单。不能直接断言 master7 少加载数千项，也不能排除缺资源；绝不可只增大 UI 的计数来掩盖差异。

## 已核实的 master7 链路

- `Assets/Scripts/UmaViewerBuilder.cs:840`：`LoadLive` 经 `Director.GetLivePreloadEntries`、`UmaAssetManager.PreLoadAndRun`，再切换 `LiveScene`、`Director.Initialize`、`LoadLiveUma`、初始化时间轴与音乐。
- `Assets/Scripts/umamusume/Gallop/Live/Director.cs:731`：预载根为语音、Cutt、歌曲 part 和可选的 `3d/env/live/live{BackGroundId}/` 下舞台 bundle，最后按名称去重。`Director.cs:787` 有舞台依赖的同步兜底。这不是完整的角色、动作、特效资产清单。
- `Assets/Scripts/UmaAssetManager.cs:103-147`：根条目去重、展开依赖。Standalone 模式下 `UmaViewerDownload.cs:87-105` 的 `DownLoading(x/y)` 只统计本机缺失的展开条目；本机都有文件时该阶段不显示。之后对每个根执行 `SearchAB`，`Loading(x/y)` 的 `y=loadItems.Count`，可含重复的共享依赖。每次 `AcquireOne` 返回后即递增，**不表示成功打开了 y 个唯一 bundle**：`UmaAssetManager.cs:193-245` 有句柄复用、文件缺失等路径。`UmaSceneController.cs:73-91` 原样显示数字。
- `UmaAssetManager.cs:150` 的 `LoadAssetBundle` 是上述进度回调之外的按需入口。预载结束后 `UmaViewerBuilder.cs:102` 加载角色；`LiveTimelineControl.cs:355` 加载动作；`StageController.cs:284`、`LiveFlashController.cs:650`、`StageLensFlareDriver.cs:237`、`MonitorUvMovieProvider.cs:1741`、`CyalumePlaybackProvider.cs:377` 等有其它加载路径。`UmaViewerMain.cs:77` 另有初始化界面固定 10 步进度，不能和 Live 进度混同。
- master5 的旧预载流程更窄，不能认为它是完整正确的范本；master7 当前目录无 Git 元数据，避免覆盖式拷贝。

## 二进制逆向的起点

- `D:\Projects\UmaTools\UmaViewer\GameAssembly.dll` 与 `UmaViewer_Data\il2cpp_data\Metadata\global-metadata.dat`。磁盘上的 metadata 只有 **21 字节**；master5 的 `tmp/global-metadata.runtime.dat`、`tmp/render-analysis.txt`、`tmp/render-call-map.txt`、`tmp/render-disassembly.txt`、`tmp/render-pipeline-api.txt` 为已有线索。详情见 `docs/LIVE_RENDER_RESTORATION.md:6-11`。本地 Cpp2IL、Il2CppDumper、Il2CppInspector、IDA 可用；此前的 RVA/Shader 研究**没有**建立二进制 Live 资源图和进度计数链。
- 核对正在对比的 exe/DLL 身份与哈希；沿进度 UI 调用、回调源、Live 资源集合构造、依赖展开、资产实际打开和角色/舞台/时间轴阶段建立调用链。函数名相似不等于行为相同；必要时用受控运行日志验证静态推断。不要读取或输出账号、令牌及 `Config.json` 内容。

## 渲染状态与约束

- `docs/LIVE_RENDER_RESTORATION.md` 详列 Bloom/柔光、三层 PostFilm、径向模糊、简单颜色曲线、景深的证据、改动和隔离 GPU 回归。2026-09-25 的主相机检查未发现明确的第二次 Bloom 合成，但不排除多相机、真实帧 RT 交接、参数或素材差异引起过亮。径向模糊时间轴 bit17/bit18 矩形标志未确认。
- `tmp/dof-focus-test-project` 只含最小 Shader 测试素材；GPU PASS 不是全场景 Live 画质对齐。**物理和用户认可的景深算法不要改；不要凭截图盲调后处理强度。**不要关闭或重启用户主 Unity Editor（此前 PID 15476；操作前重查），不要同时启动两个同一隔离测试项目。
- 真实游戏参考是 `D:\Projects\UmaTools\UmaViewer-master5\captures\reference`，不是 master5 的输出。`live1176-content-aligned-20260912/manifest.json` 有歌曲 1176、舞台 10148、14 角色槽、60 fps、1920x1080，优先同条件检查 308、868、3848 帧。完整 Live 对齐截图**尚未完成**。

## 下一阶段的验收

1. 在相同歌曲、舞台、角色、资源库和工作模式下记录两程序进度文字、阶段、最大值；给出二进制计数来源及两边**同口径**的数字定义。
2. master7 增加可开关的只读诊断：预载根数、去重与含重复的依赖请求数、本地缺文件数、实际打开的唯一 bundle、句柄复用、失败和缺依赖；分阶段记录实际 `LoadAsset`/`LoadAllAssets` 请求及成功项。日志用规范名和阶段，不泄露配置/私有路径；`GetAllAssetNames` 的潜在资产数不可冒充实际加载数。
3. 逆向并对照二进制的资源收集和依赖图，明确列出多出/缺少的模型、动作、舞台、灯光/特效、屏幕素材、Shader/纹理，指出可见画面影响。静态无法确认的项目标为未知。
4. 只修有明确证据的缺项；运行 `tools/check_live_compile.ps1` 和隔离 GPU 回归。最后用**完整素材**对齐同一帧、机位及参数，报告资源差异、画面差异和过亮帧结论。若不能运行全场景，明确承认限制，不声称已经完成还原。
