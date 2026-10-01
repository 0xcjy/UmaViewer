# PMX 导出规范与修复复盘

最后更新：2026-09-26

本文记录 UmaViewer PMX 导出修复的根因、实现原则、PMX/MMD 兼容边界和回归验证方法。目标是让后续修改继续面向通用 PMX/MMD 工具链，而不是只保证某一个加载器能够读取。

## 1. 结论

这次故障不是单一骨骼角度错误，而是三个问题叠加：

1. 导出参考姿势来自不完整、时机不稳定的运行时快照，特殊预览动作会残留到 PMX 基础姿势。
2. Unity 运行时骨架被直接当作 MMD 控制骨架导出，缺少稳定的 MMD 控制层级、腿部 IK 和严格的索引关系。
3. PMX IK 角度限制错误复用了空间向量的坐标转换函数，导致符号翻转、上下限倒置，并最终使部分求解器在执行角度限制时崩溃。

最终方案遵循以下原则：

- PMX 导出结果必须与当前预览动作无关。
- PMX 与 VMD 必须使用同一套项目参考姿势。
- 导出是一个有完整回滚能力的事务。
- PMX 字段按语义分类读写，位置、方向、角度不能共用同一种转换。
- PMX 文件格式要求与 MMD 生态骨骼约定必须分开描述。

## 2. “PMX 标准”与“MMD 标准骨架”的边界

需要先澄清一个容易造成错误实现的概念：

- PMX 2.x 是二进制模型格式规范，规定数据布局、索引、骨骼标志、IK 数据、权重和材质等字段。
- MMD 标准骨骼是为了让 VMD 动作能够按名称和结构复用而形成的生态约定。
- `全ての親`、`グルーブ`、`腰`、`上半身2`、`足IK親` 等通常被称为准标准骨骼或事实标准扩展，并不是 PMX 文件语法要求。
- PMX 没有规定模型必须是 A-pose 或 T-pose。它要求顶点、骨骼位置、权重和动作参考基准彼此一致。
- 本项目双臂各下压 `38.5°` 是 UmaViewer 的 MMD/VMD 对齐参数，不应写成适用于所有模型的通用 MMD 常量。

因此，本项目所说的“MMD 参考 A-pose”是导出约定：先恢复角色初始化姿势，再对左右上臂施加 `-38.5°`、`+38.5°` 的本地旋转，并让 PMX 与 VMD 共用这一基准。

## 3. 故障现象与根因

### 3.1 IK 限制导致加载器崩溃

日志中的关键错误是：

```text
min > max, or either was NaN. min = -0.0087, max = -3.1415927
```

预期膝盖 X 轴限制为：

```text
lower = -π
upper = -0.0087
```

旧实现使用通用 `WriteVector3` 写入 IK 角度。该函数面向空间坐标，会执行模型倍率和 Unity/PMX 坐标轴转换。角度限制不是位置向量，经过同一转换后端点符号和顺序发生变化，文件中出现 `lower > upper`。

修复后：

- IK 限制使用专用的原始 `float3` 读写函数。
- 写入前拒绝 `NaN` 和 `Infinity`。
- 写入和读取时逐轴规范化上下限。
- 骨架生成结束后再次校验 IK 目标、链接索引和限制区间。

这里的核心经验是：两个字段即使二进制布局都是 `float3`，也不代表它们拥有相同的坐标语义。

### 3.2 预览动作污染基础姿势

旧快照只保存身体主蒙皮引用骨骼的世界位置和世界旋转，而且捕获时机位于模型合并流程中。它没有覆盖：

- Animator 可能驱动的非蒙皮节点；
- 控制节点和辅助层级；
- 局部缩放；
- 父节点变换对后代节点的影响；
- 模型合并、身高处理和动画加载之间的初始化时序。

这解释了为什么某些动作导出正常，而某些动作导出后在 Blender 中像被额外套用动作：残留状态取决于该动作实际驱动了哪些 Transform。

修复后，初始化参考姿势在模型合并和身高设置完成、预览动画加载前捕获，并保存完整模型层级的：

```text
localPosition
localRotation
localScale
```

恢复时同样使用本地变换。这样父子层级按原始局部关系重建，不会因父节点的旋转或缩放产生二次偏移。

### 3.3 导出过程缺少事务边界

PMX 导出会临时改变 Animator、表情、眼动、动态骨、物理和 Transform。旧流程中的恢复逻辑分散，一旦纹理、模型构建或文件写入中途抛出异常，就可能把角色留在导出状态。

现在由 `PMXExportStateSnapshot` 统一执行：

```text
捕获当前运行时状态
  -> 冻结 Animator、眼动和物理
  -> 恢复初始化姿势
  -> 应用项目 MMD 参考 A-pose
  -> 构建并写出 PMX
  -> finally 清理临时资源
  -> 恢复表情、完整 Transform 和运行时组件
```

`Dispose()` 使用嵌套 `finally`，保证某一类状态恢复失败时，后续恢复步骤仍会执行。

### 3.4 Unity 骨架不能直接等同于 MMD 控制骨架

Unity 运行时层级包含蒙皮骨、控制节点、碰撞节点、Handle、Pole、Target 和其他辅助对象。全部导出会造成：

- 非动画骨进入 PMX；
- 父子关系不符合 MMD 动作预期；
- 权重引用不存在或错误的骨骼；
- 缺少 `センター`、`下半身` 和足 IK 等控制层。

新的骨架构建流程从 `SkinnedMeshRenderer.bones` 收集实际蒙皮引用，再补入动作和父链所需骨骼，并过滤明确的运行时辅助节点。被过滤骨骼的顶点权重会转交给最近的有效祖先，而不是无条件落到索引 0。

## 4. 当前导出骨架约定

当前体干控制层级为：

```text
全ての親
└─ センター
   └─ グルーブ
      └─ 腰
         ├─ 上半身
         │  └─ 上半身2
         └─ 下半身
            ├─ 左足
            └─ 右足
```

腿部 IK 控制层级为：

```text
全ての親
├─ 左足IK親
│  └─ 左足ＩＫ
│     └─ 左つま先ＩＫ
└─ 右足IK親
   └─ 右足ＩＫ
      └─ 右つま先ＩＫ
```

足 IK 的目标和链接关系为：

```text
足ＩＫ
  target: 足首
  links : ひざ（有限制） -> 足（无限制）

つま先ＩＫ
  target: つま先
  links : 足首
```

膝盖限制使用 PMX 文件坐标语义下的负 X 区间：

```text
X: [-π, -0.0087]
Y: [0, 0]
Z: [0, 0]
```

此区间用于避免膝盖向错误方向折叠。`lower <= upper` 必须逐轴成立，且所有值必须是有限浮点数。

## 5. PMX 导出必须遵守的规则

### 5.1 索引完整性

- 父骨骼索引只能是 `-1` 或有效骨骼索引。
- 骨骼不能以自身作为父骨骼。
- 使用骨骼索引作为尾端时，尾端索引必须有效。
- IK 目标和每个 IK 链接都必须引用有效骨骼。
- 顶点权重不能引用未导出的 Unity Transform。
- 过滤骨骼后必须重新建立 Transform 到 PMX 索引的映射。

### 5.2 标志与后续数据一致

PMX 骨骼数据是由标志控制的可变结构：

- 尾端为骨骼索引或位置偏移，只能按对应标志写入一种。
- IK 标志开启时，必须写入目标、迭代次数、单次限制角、链接数量和链接数据。
- IK 链接开启角度限制时，必须继续写入下限和上限两个 `float3`。
- 读写器必须保持对称，不能只修改 Writer 而让 Reader 延续旧语义。

### 5.3 数值和单位

- PMX 浮点数据写入前应检查 `NaN` 和 `Infinity`。
- IK 单次限制角和链接角度上下限使用弧度。
- 位置向量可以经过模型尺度与坐标系转换。
- IK 欧拉角限制不能经过位置倍率。
- 通用坐标翻轴函数不能直接用于角度区间；角度应在 PMX 坐标语义下明确构造和写入。

### 5.4 基础姿势一致性

- 导出的顶点位置、骨骼位置和权重必须来自同一参考姿势。
- 导出结果不能依赖按钮按下时 Animator 恰好停在哪一帧。
- 禁止用 `Animator.Rebind()` 作为不透明的“复位”手段，除非同时证明它不会改变参考姿势、Animator 层状态和用户预览状态。
- 若模型初始化后会新增可动画 Transform，应重新评估参考姿势快照的覆盖时机。
- PMX 和 VMD 使用不同参考姿势会造成动作应用后的固定肩臂偏差。

### 5.5 MMD 名称兼容

VMD 主要通过骨骼名匹配动作，因此日文主名称必须稳定且使用正确字符。尤其要注意：

- `左足ＩＫ`、`右足ＩＫ`、`左つま先ＩＫ`、`右つま先ＩＫ` 中的 `ＩＫ` 是全角字符。
- `左ひじ`、`右ひじ` 等名称应保持 MMD 生态常用写法。
- 英文名用于辅助识别，不能替代日文主名称的动作兼容作用。
- 准标准骨骼不是 PMX 语法强制项，应按目标动作和控制需求加入，不能把所有扩展骨都称为“PMX 必需骨”。

## 6. 代码职责

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Exporters/ModelExporter.cs` | PMX 导出事务入口、网格/材质/权重构建和文件写出 |
| `Assets/Scripts/Exporters/PMXExportStateSnapshot.cs` | 捕获、冻结、应用参考姿势和完整恢复运行时状态 |
| `Assets/Scripts/Exporters/PMXBoneExporter.cs` | 收集有效骨骼、建立 MMD 控制层级、生成足 IK 和校验索引 |
| `Assets/Scripts/UmaContainerCharacter.cs` | 在稳定初始化时机捕获完整本地参考姿势 |
| `Assets/Scripts/Exporters/VMDRecorder/UnityHumanoidVMDRecorder.cs` | 与 PMX 共用项目 MMD 参考 A-pose |
| `Assets/Scripts/Exporters/UnityPMXRuntimeLoader/LibMMD/Writer/PMXWriter.cs` | 按 PMX 原始角度语义写入 IK 限制 |
| `Assets/Scripts/Exporters/UnityPMXRuntimeLoader/LibMMD/Reader/PMXReader.cs` | 对称读取 IK 限制，并对异常端点排序 |
| `Assets/Scripts/Exporters/UnityPMXRuntimeLoader/LibMMD/Util/MMDReaderWriteUtil.cs` | 区分空间向量与原始 `float3` 的二进制读写 |

`UnityPMXRuntimeLoader` 目录虽然名称包含 Loader，但其中同时放置了 PMX Reader、Writer 和共享二进制工具。导出器实际调用该目录中的 `PMXWriter`，所以修复 PMX 写出语义必须修改这里；Reader 同步修改用于维持往返一致性。

## 7. 回归验证清单

每次修改 PMX 导出、角色初始化、Animator 控制或骨架映射后，至少执行以下验证：

1. 分别选择一个曾导致姿势污染的动作和一个正常动作，导出同一角色。
2. 在 Blender 的 Rest Position 或等价静止骨架视图中比较两份 PMX，基础姿势应一致。
3. 在 MMD Skin 中加载，确认不崩溃，足 IK 和动作播放正常。
4. 使用另一个独立 PMX 工具加载，例如 Blender mmd_tools、nanoem 或 PMXEditor。
5. 检查左右上臂相对初始化姿势分别为 `-38.5°` 和 `+38.5°`。
6. 检查膝盖 IK 回读值约为 `[-π, -0.0087]`，且逐轴满足下限不大于上限。
7. 检查所有顶点权重、父骨、尾端、IK 目标和 IK 链接索引均有效。
8. 导出后确认预览动作、表情、眼动、动态骨和物理状态恢复。
9. 执行：

```powershell
dotnet build umamusume.csproj -nologo
git diff --check
```

本次修复已完成问题动作、正常动作、Blender 和 MMD Skin 两方面实机验证，结果全部通过。

### 7.1 普通角色单次导出实测（2026-09-26）

- 在隔离的 Unity 2022.3 编辑器中加载无声铃鹿（1002）、服装 `00`，调用现有 `ModelExporter.ExportModel`。
- 首次运行在材质处理抛出 `InvalidCastException`；单独修正材质分支后，表情处理也暴露同一类型假设。根因是收集所有 `Renderer`，却将非 `MeshRenderer` 一律当作 `SkinnedMeshRenderer`。
- 在 `ReadPMXModel` 入口统一保留 `MeshRenderer` 和 `SkinnedMeshRenderer`，使顶点、材质、表情处理共享同一网格渲染器序列；非网格渲染器不参与模型转换。
- 修复后完成真实 PMX 写出及 `PMXReader` 读回：11,110 顶点、23,477 三角形、240 骨骼、214 表情、47 刚体、36 关节。三角形顶点索引检查通过，22 个贴图引用均存在；输出的 23 个 PNG（含完整眼贴图）均通过解码校验。
- 此次验证不包含 Blender/MMD 外部加载、视觉效果或动作播放验证，也不代表全角色批量导出已经实现。

## 8. 禁止回退的实现模式

- 不要从当前 Animator 帧直接读取 PMX 基础骨架。
- 不要只快照 `SkinnedMeshRenderer.bones`。
- 不要使用世界坐标快照恢复完整骨架层级。
- 不要把碰撞体、Handle、Pole、Target 或控制节点无筛选地导出。
- 不要在过滤骨骼后继续使用旧列表位置作为 PMX 索引。
- 不要把角度、位置、方向和颜色仅因为都是 `Vector3` 就交给同一转换函数。
- 不要只为单一加载器交换 IK 上下限；文件本身应首先满足 PMX 语义和通用数值不变量。
- 不要把 `38.5°` 描述成 PMX 规范要求；它是当前角色与 VMD 导出的校准参数。

## 9. 参考资料

- [PMXEditor 0.2.3.6 所附 PMX 规格备份（日文）](https://gist.github.com/FlandreDaisuki/90ae5abf3138a15994526b6bfec73c2c)
- [PMX 2.0 文件格式英文整理](https://gist.github.com/DeXP/16ccdd09841bdc1961e0)
- [nanoem 模型编辑文档](https://nanoem.readthedocs.io/ja/latest/model.html)
- [nanoem 技术架构与 PMX 数据模型](https://nanoem.readthedocs.io/ja/latest/architecture.html)
- [babylon-mmd：PMX、标准骨架与准标准骨架说明](https://noname0310.github.io/babylon-mmd/docs/reference/understanding-mmd-behaviour/introduction-to-pmx-and-pmd/)
- [Blender mmd_tools 文档：Rest Position 与导出注意事项](https://github.com/powroupi/blender_mmd_tools/wiki/Documentation)
