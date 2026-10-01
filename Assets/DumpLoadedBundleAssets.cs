using System;
using UnityEngine;

public class DumpLoadedBundleAssets : MonoBehaviour
{
    [TextArea]
    public string[] keywords = new string[]
    {
        "pfb_env_live10108_blinklight_wash_roof_a_circle",
        "pfb_env_live10108_blinklight_wash_wall_a_circle",
        "anm_env_live10108_blinklight_wash_wall_a_circle",
        "Take 001",
        "wash_roof",
        "wash_wall",
        "live10108"
    };

    [ContextMenu("Dump Loaded AssetBundles")]
    public void DumpLoadedAssetBundles()
    {
        int bundleCount = 0;
        int hitCount = 0;

        foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (bundle == null) continue;
            bundleCount++;

            string bundleName = bundle.name;
            if (string.IsNullOrEmpty(bundleName))
                bundleName = "<unnamed bundle>";

            string[] assetNames = null;
            try
            {
                assetNames = bundle.GetAllAssetNames();
            }
            catch (Exception e)
            {
                Debug.Log($"[DumpLoadedBundleAssets] Failed reading asset names from bundle {bundleName}: {e.Message}");
                continue;
            }

            bool bundleHeaderPrinted = false;

            if (assetNames != null)
            {
                foreach (var assetName in assetNames)
                {
                    if (string.IsNullOrEmpty(assetName)) continue;

                    if (IsMatch(assetName))
                    {
                        if (!bundleHeaderPrinted)
                        {
                            Debug.Log($"[DumpLoadedBundleAssets] HIT BUNDLE = {bundleName}");
                            bundleHeaderPrinted = true;
                        }

                        Debug.Log($"[DumpLoadedBundleAssets]   Asset = {assetName}");
                        hitCount++;
                    }
                }
            }
        }

        Debug.Log($"[DumpLoadedBundleAssets] Scan finished. Loaded bundles = {bundleCount}, Hits = {hitCount}");
    }

    private bool IsMatch(string assetName)
    {
        string lower = assetName.ToLowerInvariant();

        foreach (var k in keywords)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (lower.Contains(k.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private void Start()
    {
        DumpLoadedAssetBundles();
    }
}