# 重复模块程序集兼容实施计划（待批准）

## 目标

恢复 `e362e39` 删除的 Gallop 与 `Assets/Plugins/StandaloneFileBrowser` 历史内容，同时保证 Unity 编辑器和 Unity 生成的 `.sln/.csproj` 不会因同名类型产生 `CS0101`、`CS0111` 或 `CS0433`。

## 已确认事实

- 保留的 `Assets/StandaloneFileBrowser` 是当前跨平台实现：包含 Windows、macOS、Linux 和 Editor 包装器。
- 被删除的 `Assets/Plugins/StandaloneFileBrowser` 已有 `StandalongFileBrowser.runtime.asmdef`，但它和保留版本都定义 `SFB.StandaloneFileBrowser` 等相同完整类型名，且两个 asmdef 都会自动被主程序集引用。因此仅恢复或仅新建 asmdef 都会重新触发类型歧义。
- 旧文件选择器缺少 macOS/Linux 的 C# 包装器；其 Windows 包装器还依赖旧的 `Ookii.Dialogs` 命名空间。它不能作为保留版本的等价跨平台替代。
- Gallop 删除项有 34 个 C# 文件。它们的内容和现有实现是否完全重合尚未完成逐类型审计，不能直接假设可同时编译。

## 方案

1. 以反向提交恢复删除内容，保留现有跨平台文件选择器作为默认实现。
2. 将恢复的旧文件选择器改为明确的“历史 Windows 兼容程序集”：使用独立 asmdef、关闭自动引用，并赋予不与默认 `SFB` 冲突的命名空间。
3. 该历史程序集只在确有旧调用方时由调用程序集显式引用；默认应用程序集只引用 `Assets/StandaloneFileBrowser` 的跨平台实现。
4. 逐个比较恢复的 Gallop 类型与保留类型的命名空间、公共成员、调用方和运行职责。完全等价的重复项不进入默认构建；存在真实行为差异的类型将被迁移到显式的兼容命名空间或通过版本定义作为互斥实现选择，绝不让两个同名类型同时暴露给同一调用程序集。
5. 将 Unity asmdef 的依赖图作为唯一事实来源。`.sln/.csproj` 仅由 Unity 重新生成，不手写一份会与 Unity 构建规则脱节的解决方案文件。

## 验证

- Unity 脚本重新导入后控制台无重复类型或程序集歧义错误。
- `dotnet build umamusume.csproj -nologo` 无错误。
- 默认 Windows、macOS、Linux 构建均选择保留的跨平台文件选择器。
- 历史 Windows 兼容程序集开启时，其类型不会与默认 `SFB` 类型冲突。
- Gallop 每个恢复类型均有比对结论：保留、迁移兼容命名空间、条件选择，或确认删除。

## 风险与取舍

“两个内容不同但完整类型名相同的模块同时给同一个调用方使用”在 .NET/Unity 中没有无损自动兼容方案：调用方必须明确依赖其中一个实现。asmdef 只能隔离程序集，不能消除公开类型的同名歧义；命名空间迁移或条件化选择是必要步骤。

## 执行状态

- [x] 完成现状、程序集关系和文件选择器版本差异初查。
- [x] 恢复历史文件并建立隔离程序集。
- [ ] 完成 Gallop 逐类型差异与依赖审计。
- [x] 调整 asmdef 依赖与版本定义。
- [x] 完成 `dotnet build` 验证；Unity 已打开，待编辑器自动重导入后确认。

## 开发者约定

`Assets/StandaloneFileBrowser` 是默认、跨平台且必须受 Git 跟踪的实现。不要将它的源码或 `SFB.asmdef` 加入 `.gitignore`：`.gitignore` 不能关闭已跟踪程序集的编译，反而会使新克隆的工程缺少必要文件。

`Assets/Plugins/StandaloneFileBrowser` 仅保留为 `SFB.LegacyWindows` 历史兼容程序集，默认不自动引用。业务代码必须继续使用默认的 `SFB`，除非调用方明确需要旧 Windows 行为并显式依赖 `SFB.LegacyWindows`。

`Assets/Scripts/umamusume/Gallop/LegacyCompatibility` 的恢复代码属于 `Gallop.Legacy`。该程序集目前由未定义的 `GALLOP_LEGACY_COMPAT_UNSUPPORTED` 约束禁用，既不会自动引用，也不会参与默认编译。后续完成 API 迁移前，禁止定义该符号。


## 当前审计结果

- `StandaloneFileBrowser.LegacyWindows` 已独立编译通过；默认程序集仍只使用跨平台的 `SFB`。
- Gallop 的 34 个恢复脚本不能直接与 `umamusume` 分离编译。验证中出现 111 个错误：同名的 `LiveTimelineDefine` 枚举、`ILiveTimelineKeyDataList` 接口及其集合跨程序集后具有不同的类型标识，无法相互赋值。
- 因此 `Gallop.Legacy` 现阶段只作为受版本控制的历史源码保留，默认禁用。若要让它成为可启用模块，下一阶段必须逐个迁移其公开 API 到 `Gallop.Legacy` 命名空间，并为与主程序集交互的类型建立显式适配层。

