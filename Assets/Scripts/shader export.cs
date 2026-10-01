#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class DumpShaderToFile
{
    [MenuItem("Tools/Dump Selected Material Shader")]
    static void Dump()
    {
        var mat = Selection.activeObject as Material;
        if (!mat || !mat.shader)
        {
            Debug.Log("Select a Material first.");
            return;
        }

        var shader = mat.shader;
        var path = EditorUtility.SaveFilePanel(
            "Save shader as text",
            Application.dataPath,
            shader.name.Replace("/", "_") + ".shader",
            "shader"
        );
        if (string.IsNullOrEmpty(path)) return;

        // 把 Unity 内部序列化后的 shader 资源文本吐出来（有时会是二进制/不可读，取决于来源）
        var text = EditorJsonUtility.ToJson(shader, true);
        File.WriteAllText(path, text);
        Debug.Log("Saved to: " + path);
    }
}
#endif

