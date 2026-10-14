#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera `Assets/Resources/blacklist_trajes.json` a partir de los datos ya cargados: para cada
/// personaje normal, los trajes cuyo cuerpo no existe en la base de assets (los que darian
/// "No body, can't load!").
///
/// Como necesita la base ya cargada, hay que entrar en Play Mode, esperar a que termine de
/// cargar y ejecutar el menu. El archivo se puede subir al repo para que otros no tengan que
/// regenerarlo.
/// </summary>
public static class CostumeBlacklistGenerator
{
    [MenuItem("Tools/UmaViewer/Generate Costume Blacklist")]
    public static void Generate()
    {
        if (!CostumeBlacklist.BuildAndSave(out int count, out string error))
        {
            EditorUtility.DisplayDialog(
                "Costume Blacklist",
                "Could not build the blacklist:\n\n" + error +
                "\n\nEnter Play Mode, wait for it to finish loading, then run this menu again.",
                "Ok");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[CostumeBlacklist] Generado Assets/Resources/blacklist_trajes.json ({count} entradas).");
    }
}
#endif
