# Uma Viewer (2)
⚠️ If you see **"Failed to load il2cpp"** or the app cannot start on Windows, it may be blocked by **Windows Smart App Control**.

 If it fails, turn off Smart App Control:

   `Windows Security → App & browser control → Smart App Control settings → Off`

This issue is caused by Windows blocking unsigned builds. The program should start normally after moving it to a local folder or disabling Smart App Control.

Unity application that makes it easy to view assets from Uma Musume: Pretty Derby.

| Version   | Supported |
|-----------|-----------|
| JP (DMM)  | ✅        |
| JP (Steam)| ✅        |
| KR        | ✅        |
| Global    | ✅        |

------------

## ⚠️ 🌍 EN/Global users ⚠️

In UmaViewer, set **WorkMode** to **Default** and **Region** to **Global** in the **'Other'** Settings tab.

Currently only the default work mode is supported - you need to download assets using the Download All button in the game's settings for the viewer to work.

------------

### Requirements/Installation
1. [Uma Musume: Pretty Derby](https://dmg.umamusume.jp/) with full data download is required to run the viewer.
2. Depending on your version and update status, the game stores its data in **different locations**
 - **DMM/Steam Older installations :** C:\Users\\*your_username*\AppData\LocalLow\Cygames\umamusume(?)\
 - **DMM/Steam Fresh installations :** ...\\*Umamusume installation directory*\Umamusume_Data\Persistent(?)\
 - In any case，confirm your file listing in target folder looks like this
   * Target Folder\
     * **meta**
     * master\
       * **master.mdb**
     * dat\
       - 2A\\...
       - 2B\\...
       - ...\\...
3. Download the most recent UmaViewer.zip file from [Releases](https://github.com/zuoy865-stack/UmaViewer/releases) tab.
4. Extract the archive anywhere, can be extracted over previous version.
5. Run the UmaViewer.exe. 
6. UmaViewer will try to automatically detect the game data folder.  
   - If it fails or shows an error, go to **Settings → Other → Change DataPath** and manually select target folder.

------------

- For Developers/Contributors
1. [Unity Hub](https://unity3d.com/get-unity/download) with [Unity Engine Version 2022.3.62f1](https://unity.com/releases/editor/archive) is recommended. It should be possible to run it on newer 2022.3.X versions.
1. Clone or download and extract this repository.
1. Import and Open the project in Unity Hub, missing files should be automatically repaired.
1. Open the Assets/Scenes/Version2 scene.
   - note: If there are errors in the console, [JSON .NET For Unity](https://assetstore.unity.com/packages/tools/input-management/json-net-for-unity-11347) may be required

PMX exporter maintainers should also read [PMX Export Standard and Postmortem](docs/pmx-export-standard-and-postmortem.md).

Live restoration maintainers should read [2026-10-01 improvement summary and publication boundaries](docs/IMPROVEMENTS_20261001.md). This source snapshot does not establish full visual parity and excludes local keys, extracted game assets, authorization audio payloads, and the native CySpring plugin; these dependencies must be supplied separately.

     脚本里面有错误的单词不要改! (Cy拼错一堆单词,我都无语了😓)

### PMX 网页查看器

`web/pmx-viewer` 可在浏览器中预览本次导出的无声铃鹿（1002，服装 00），并播放角色 VMD 动作。需要 Node.js 18 或更高版本，以及以下本地导出文件（游戏模型与动作不随网页依赖安装）：

```text
exports/pmx-test/1002_00_20260926-221744/1002_00.pmx
exports/pmx-test/1002_00_20260926-221744/Texture2D/
exports/pmx-test/1002_00_20260926-221744/1002_idle.vmd
```

从项目根目录启动：

```powershell
cd web/pmx-viewer
npm ci
npm start
```

打开 <http://127.0.0.1:8765>，不要直接双击 HTML。服务仅监听本机地址，只提供查看器文件、Three.js 依赖和指定模型目录，不开放整个项目。首次安装依赖需要网络，安装后查看模型不依赖 CDN 或 Unity 运行进程；终端按 Ctrl+C 停止服务。

- 左键拖动旋转、滚轮缩放、右键拖动平移；触屏使用单指旋转和双指缩放/平移。
- 支持自动旋转、地面网格、线框显示和重置视角；模型统计来自实际解析结果。
- 贴图缺失或 PMX 加载失败会显示错误，不会显示为加载完成。
- 点击“载入待机动作”，再点击“播放”，即可查看从真实游戏资源 `anm_eve_chr1002_00_idle01_loop` 导出的待机动作。示例约 3.3 秒，按 30 fps 采样，共 100 帧；骨骼有实际运动，但该资源没有配套面部动作，示例表情保持中性。
- “导入本地 VMD”在浏览器内读取文件，不上传。支持与模型同名匹配的骨骼、顶点表情轨道及 VMD IK 开关；不播放镜头、灯光、音乐或模型显隐轨道。其他模型制作的 VMD 仍需检查骨骼名称和参考姿势兼容性。
- 导入成功后暂停在首帧；支持播放/暂停、从头播放、循环、进度拖动和 0.25–2 倍速。关闭循环后停在末帧，再次播放从头开始；“清除动作 / 还原姿势”恢复 PMX 参考姿势。导入无效文件或仅镜头 VMD 时保留原动作。
- 使用 MMD 动画求解的 IK 与骨骼继承，但明确禁用刚体物理，不加载 Ammo，也不改变现有着色器或材质；外观不保证与游戏渲染一致。
- 已用真实 PMX/VMD 验证骨骼随时间变化、暂停、倍速、循环、结束重播、往返定位无累积漂移、动作替换和清除，以及桌面/窄屏操作。当前 PMX 的脚趾 IK 层级会触发 Three.js 的构造警告；示例 VMD 明确关闭这四条足部/脚趾 IK 链，未验证依赖脚趾 IK 的外部动作效果。
- 下载按钮仅下载 PMX，使用导出文件时必须同时保留原目录的 `Texture2D` 文件夹。

同级网站 `UmaAudioSite` 使用独立的原生导航页 `/models`：从 `data/model-viewer/catalog.json` 列出已成功导出的普通角色专属服装，按需读取每套服装的 PMX 和贴图。不上传动作，网页不运行物理或原版 Shader。上面的 `web/pmx-viewer/` 仍是仅供本地单角色调试的独立示例，**不再作为网站的部署入口**。网站的服务器源码与本地源码存在独立修改，线上更新前要按变更对照，不能用本地网站的 `dist` 全量覆盖线上文件。

批量导出普通角色的专属服装（排除通用服装、路人和动作）：在项目根目录运行 `python tools/export_exclusive_models.py --batch 24`。脚本使用 Unity 2022.3.62f1 的隔离项目，读取当前 `Config.json` 的游戏资源；中断后重新执行会校验已有输出并续跑。全部候选尝试后，可用 `--retry-failures` 重试失败项。输出位于同级网站的 `data/model-viewer/models/<角色ID>/<服装ID>/`，成功目录写入 `catalog.json`；检查点及失败原因保存在本项目 `exports/exclusive-model-report.json`。2026-09-30 的资源快照共验证通过 173 位角色、450 套服装及 10,605 张 PNG；完整验证记录为 `exports/exclusive-model-validation.json`。

### Features

||||
| ------------ | ------------ | ------------ |
| ✓ - Working | / - Incomplete  | x - Unsupported  |

|||
| ------------ | ------------ |
| Viewing main character models/animations | ✓  |
| Swapping costumes/animations between characters | ✓  |
| Viewing chibi models/animations | ✓  |
| Viewing mob (NPC) models | ✓ |
| Playing facial animations, custom sliders | ✓  |
| Cloth/Hair physics | ✓  |
| Playing Live Audio with Lyrics | ✓  |
| Exporting animations to MMD | ✓  |
| Recording animations (.gif), screenshots | ✓  |
| Viewing Props, Scenery, Live scenes | /  |
| Exporting models | /  |


All characters and animations are supported

<img src="https://user-images.githubusercontent.com/59540382/222418271-a6e4ce82-b3a5-47ba-9fc9-4d85120218ec.png" height="350" />

Mobs / background characters as well

<img src="https://user-images.githubusercontent.com/32562737/219174232-7d0a0eec-8b1c-4571-9c08-8474e06dd3a8.png" height="350" />

Mixing outfits and animations

<img src="https://user-images.githubusercontent.com/59540382/222420757-609e1f77-d762-4b39-a7d0-d1fb2d3b79a3.png" height="350" />

Screenshot and .gif recording

<img src="https://user-images.githubusercontent.com/59540382/222421579-582be5db-5839-4f7c-bf1b-80efc812c4e0.gif" height="350" />

and more

<img src="https://user-images.githubusercontent.com/59540382/222422871-12e80e0b-778b-4f42-b581-5e4af5cd6df9.png" height="350" />

### Also check out:
[UmaChat by kagari](https://github.com/kagari-bi/UmaChat) - model viewer fork that lets you chat with Umas using AI + TTS

### Special Thank to:
MarshmallowAndroid: [UmaMusumeExplorer](https://github.com/MarshmallowAndroid/UmaMusumeExplorer) for acb/awb decoder.
