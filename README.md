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
3. Download the most recent UmaViewer.zip file from [Releases](https://github.com/0xcjy/UmaViewer/releases) tab.
4. Extract the archive anywhere, can be extracted over previous version.
5. Run the UmaViewer.exe. 
6. UmaViewer will try to automatically detect the game data folder.  
   - If it fails or shows an error, go to **Settings → Other → Change DataPath** and manually select target folder.

------------

### For Developers/Contributors (Windows x64)
1. Install [Unity Hub](https://unity3d.com/get-unity/download) and [Unity Engine Version 2022.3.62f1](https://unity.com/releases/editor/archive), including Windows Build Support if you intend to build a player. This is the project version in `ProjectSettings/ProjectVersion.txt`.
2. Clone or download and extract this repository.
3. Add the project directory in Unity Hub and open it with Unity 2022.3.62f1. Allow Unity to import the supplied assets and resolve the dependencies in `Packages/manifest.json` (including JSON .NET); an initial package download requires internet access. Runtime files are retained in source control, not repaired by an unspecified missing-file download.
4. Open `Assets/Scenes/Version2.unity` and enter Play mode, or build a Windows x64 player from this scene.
5. Use your existing complete game data as described above. If detection selects the wrong location or your installation is non-default, choose **Settings → Other → Change DataPath** and select the folder containing `meta`, `master/master.mdb`, and `dat`.

PMX exporter maintainers should also read [PMX Export Standard and Postmortem](docs/pmx-export-standard-and-postmortem.md).

Live restoration maintainers should read [2026-10-01 improvement summary and current source-delivery boundary](docs/IMPROVEMENTS_20261001.md). The historical rendering and physics limitations remain; supplying the runtime dependencies does not establish full visual parity.

### Supplied runtime files and local configuration

This source retains the plugin, resources, and runtime defaults supplied by [katboi01/UmaViewer](https://github.com/katboi01/UmaViewer/tree/07f82e9fa08f23c1cda3a9be3045a09372eae8bb): `Assets/Plugins/CySpringPlugin.dll` and its Unity metadata, `Assets/StreamingAssets/cri_auth` and `sound_proj.acf`, and upstream UI/still/background/environment images. Upstream database, asset-bundle, and audio defaults are included. On Windows x64, you do **not** need to manually add keys, native plugins, or these resources for the upstream-supported setup. Full game data must still be obtained separately; it is not bundled with this repository.

`Config.json` is generated locally when absent: at the project root in the Editor, or beside the built player. It stores your machine-specific data path and preferences and remains ignored by Git. Optional `DBBaseKeyText`, `DBKeyText`, `GlobalDBKeyText`, `ABKeyText`, and `AudioKeyText` overrides remain available for compatible alternate data; blank overrides use the supplied upstream defaults. Overrides use hexadecimal text without a `0x` prefix; an audio override must be nonzero and at most 16 hex digits. Restart after editing overrides. A pre-existing local config is not replaced, so review stale paths or custom overrides if you reuse one. No personal `Config.json`, private credentials, Unity caches, or generated game exports are published. See [current publication manifest](docs/PUBLICATION_MANIFEST_20261002.json) for source hashes and the delivery inventory. These source-delivery instructions are not a claim that all runtime or rendering checks have passed.

   

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
