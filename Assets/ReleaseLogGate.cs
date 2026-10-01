using UnityEngine;

public static class ReleaseLogGate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.unityLogger.logEnabled = true;
        Debug.unityLogger.filterLogType = LogType.Log;
#else
        Debug.unityLogger.logEnabled = false;
#endif
    }
}