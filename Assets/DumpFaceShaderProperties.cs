using UnityEngine;
using UnityEngine.Rendering;

public class DumpFaceShaderProperties : MonoBehaviour
{
    void Start()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (!m || !m.shader) continue;

                string rn = r.name.ToLower();
                string mn = m.name.ToLower();
                string sn = m.shader.name.ToLower();

                if (!rn.Contains("face") && !mn.Contains("face") && !sn.Contains("face")
                    && !rn.Contains("head") && !mn.Contains("head"))
                    continue;

                Debug.Log($"==== Renderer: {r.name} ====");
                Debug.Log($"Material: {m.name}");
                Debug.Log($"Shader: {m.shader.name}");
                Debug.Log($"receiveShadows={r.receiveShadows}, shadowCastingMode={r.shadowCastingMode}");

                var shader = m.shader;
                int count = shader.GetPropertyCount();

                for (int i = 0; i < count; i++)
                {
                    string prop = shader.GetPropertyName(i);
                    var type = shader.GetPropertyType(i);

                    string lower = prop.ToLower();
                    bool important =
                        lower.Contains("shadow") ||
                        lower.Contains("light") ||
                        lower.Contains("face") ||
                        lower.Contains("head") ||
                        lower.Contains("toon") ||
                        lower.Contains("rim") ||
                        lower.Contains("normal") ||
                        lower.Contains("camera") ||
                        lower.Contains("view");

                    if (!important) continue;

                    Debug.Log($"[{type}] {prop}");
                }
            }
        }
    }
}