using System.Collections;
using Gallop;
using Gallop.Live.Cyalume;
using UnityEngine;

[DisallowMultipleComponent]
public class CyalumeAutoBinder : MonoBehaviour
{
    [Header("live音乐id")]
    [InspectorName("音乐 ID")]
    [Tooltip("可选。设置为 0 时，会自动从 Director.instance.live.MusicId 获取。")]
    public int musicId;

    [Header("组件引用")]
    [InspectorName("3D 荧光棒控制器")]
    public CyalumeController3D controller3D;

    [InspectorName("荧光棒播放状态提供器")]
    public CyalumePlaybackProvider playbackProvider;

    [Header("初始化设置")]
    [InspectorName("启动时强制重新构建")]
    public bool forceRebuildOnStart;

    [InspectorName("输出详细日志")]
    public bool verboseLog = false;

    [Header("手动播放状态覆盖（可选）")]
    [InspectorName("使用手动播放状态")]
    public bool useManualPlaybackState;

    [InspectorName("手动图案 ID")]
    public int manualPatternId;

    [InspectorName("手动图案开始时间")]
    public float manualPatternStartTime;

    [InspectorName("手动播放速度")]
    public float manualPlaySpeed = 1f;

    [InspectorName("手动编舞类型")]
    public int manualChoreographyType;

    private IEnumerator Start()
    {
        if (controller3D == null)
            controller3D = GetComponent<CyalumeController3D>();

        if (controller3D == null)
            controller3D = GetComponentInChildren<CyalumeController3D>(true);

        if (controller3D == null)
        {
            var controllerHost = ResolveControllerHost();
            controller3D = controllerHost.AddComponent<CyalumeController3D>();

            if (verboseLog)
            {
                Debug.Log(
                    $"[CyalumeAutoBinder] Added missing CyalumeController3D " +
                    $"at runtime on '{controllerHost.name}'.");
            }
        }

        if (playbackProvider == null)
            playbackProvider = GetComponent<CyalumePlaybackProvider>();

        if (playbackProvider == null)
            playbackProvider = GetComponentInChildren<CyalumePlaybackProvider>(true);

        if (playbackProvider == null)
        {
            var providerHost =
                controller3D != null ? controller3D.gameObject : gameObject;

            playbackProvider =
                providerHost.AddComponent<CyalumePlaybackProvider>();

            if (verboseLog)
            {
                Debug.Log(
                    $"[CyalumeAutoBinder] Added missing CyalumePlaybackProvider " +
                    $"at runtime on '{providerHost.name}'.");
            }
        }

        if (musicId <= 0 &&
            Gallop.Live.Director.instance != null &&
            Gallop.Live.Director.instance.live != null)
        {
            musicId = Gallop.Live.Director.instance.live.MusicId;
        }

        if (playbackProvider != null && musicId > 0)
            playbackProvider.InitializeForMusicId(musicId);

        if (controller3D == null)
        {
            Debug.LogWarning(
                "[CyalumeAutoBinder] CyalumeController3D not found.");

            yield break;
        }

        controller3D.SetVerboseLog(verboseLog);
        controller3D.SetMusicIdOverride(musicId);
        controller3D.SetPlaybackProvider(playbackProvider);

        if (useManualPlaybackState)
        {
            controller3D.SetManualPlaybackState(
                manualPatternId,
                manualPatternStartTime,
                manualPlaySpeed,
                manualChoreographyType);
        }
        else
        {
            controller3D.ClearManualPlaybackState();
        }

        if (verboseLog)
        {
            string controllerHostName =
                controller3D != null
                    ? controller3D.gameObject.name
                    : "<null>";

            string providerHostName =
                playbackProvider != null
                    ? playbackProvider.gameObject.name
                    : "<null>";

            Debug.Log(
                $"[CyalumeAutoBinder] Start: musicId={musicId}, " +
                $"provider={(playbackProvider != null)}@{providerHostName}, " +
                $"controller={(controller3D != null)}@{controllerHostName}");
        }

        yield return controller3D.SetupOfficialLike(forceRebuildOnStart);
    }

    private GameObject ResolveControllerHost()
    {
        var assetHolder = GetComponent<AssetHolder>();
        if (assetHolder != null)
            return assetHolder.gameObject;

        assetHolder = GetComponentInChildren<AssetHolder>(true);
        if (assetHolder != null)
            return assetHolder.gameObject;

        return gameObject;
    }
}