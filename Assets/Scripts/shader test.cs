using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DebugMatShader : MonoBehaviour
{
    void Start()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.Log("No Renderer (including children)");
            return;
        }

        Debug.Log($"Found {renderers.Length} renderer(s) under: {gameObject.name}");

        foreach (var r in renderers)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            if (mats == null) continue;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;

                var sh = m.shader;
                string shName = sh ? sh.name : "(null)";

#if UNITY_EDITOR
                string shPath = sh ? AssetDatabase.GetAssetPath(sh) : "";
                Debug.Log($"Renderer={r.name}  Mat[{i}]={m.name}  Shader={shName}  ShaderPath={shPath}");
#else
                Debug.Log($"Renderer={r.name}  Mat[{i}]={m.name}  Shader={shName}");
#endif
            }
        }
    }
}

