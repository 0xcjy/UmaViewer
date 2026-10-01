using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class UmaContainerProp : UmaContainer
{
    public bool LoadProp(UmaDatabaseEntry entry, Transform SetParent = null)
    {
        AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry, true);
        if (bundle == null)
        {
            Debug.LogError($"[UmaContainerProp] Failed to load prop bundle: {entry.Name}");
            return false;
        }

        // A bundle can contain several prefabs. Prefer the one matching its metadata entry,
        // then use the first prefab so newer prop naming conventions remain previewable.
        GameObject[] prefabs = bundle.LoadAllAssets<GameObject>();
        string expectedName = System.IO.Path.GetFileName(entry.Name);
        GameObject go = prefabs.FirstOrDefault(prefab => prefab.name == expectedName)
            ?? prefabs.FirstOrDefault();

        if (go == null)
        {
            Debug.LogError($"[UmaContainerProp] No GameObject prefab found in prop bundle: {entry.Name}");
            UmaViewerUI.Instance?.ShowMessage($"Cannot preview '{expectedName}': no prefab was found.", UIMessageType.Error);
            return false;
        }

        Instantiate(go, SetParent ? SetParent : transform);
        return true;

        
        /*foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                //Shaders can be differentiated by checking m.shader.name
                m.shader = Shader.Find("Unlit/Transparent Cutout");
            }
        }*/
        
    }
}
