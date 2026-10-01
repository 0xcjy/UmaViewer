using System;
using System.Globalization;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeySpotlight3dData : LiveTimelineKeyWithInterpolate
    {
        public const int ATTR_DISABLE_BILLBOARD = 0x10000;
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Spotlight3d;
        public bool isActive;
        public Color color = Color.white;
        public float colorPower = 1f;
        public float localHeight;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public Vector3 characterPosition;
        public int targetCameraType;
        public int targetCameraIndex;
        public string assetName = "spotlight3d000";
        public int characterIndex = -1;

        // Native 0x1af80e0/0x1af80f0: bit 16 disables billboarding.
        public bool IsEnabledBillboard
        {
            get => ((int)attribute & ATTR_DISABLE_BILLBOARD) == 0;
            set => attribute = (LiveTimelineKeyAttribute)(value
                ? (int)attribute & ~ATTR_DISABLE_BILLBOARD
                : (int)attribute | ATTR_DISABLE_BILLBOARD);
        }
    }

    [Serializable]
    public class LiveTimelineKeySpotlight3dDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeySpotlight3dData>
    {
    }

    [Serializable]
    public class LiveTimelineSpotlight3dData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeySpotlight3dDataList keys = new LiveTimelineKeySpotlight3dDataList();
        // These are runtime bindings, not additional serialized track fields.
        public int CharacterIndex { get; set; } = -1;
        public int AssetId { get; set; }

        public LiveTimelineSpotlight3dData() : base("Spotlight3d") { }
        public override ILiveTimelineKeyDataList GetKeyList() => keys;

        // The shipped worksheets serialize binding on each key, not the cached
        // group fields. All keys in the recovered 1001/1093 tracks carry the same
        // binding; -1 explicitly remains unbound. Do not infer it from a group label.
        public override void UpdateStatus()
        {
            base.UpdateStatus();
            CharacterIndex = -1;
            AssetId = -1;
            if (keys == null || keys.Count == 0 || keys[0] == null) return;
            var first = (LiveTimelineKeySpotlight3dData)keys[0];
            if (first.characterIndex < 0) return;
            const string prefix = "spotlight3d";
            string assetName = first.assetName;
            if (assetName == prefix)
            {
                CharacterIndex = first.characterIndex;
                return;
            }
            if (assetName == null || !assetName.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(assetName.Substring(prefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int assetId))
            {
                Debug.LogError("[Spotlight3d] Invalid authored asset name: " + assetName);
                return;
            }
            AssetId = assetId;
            CharacterIndex = first.characterIndex;
        }

        // Native GetAssetName 0x1afa8d0, with invariant numeric formatting.
        public static string GetAssetName(int assetId) => assetId < 0
            ? "spotlight3d"
            : "spotlight3d" + assetId.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
