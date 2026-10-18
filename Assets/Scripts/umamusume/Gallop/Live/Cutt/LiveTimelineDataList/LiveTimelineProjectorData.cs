using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native LiveTimelineKeyProjectorData (the `projecterList` track = mirror-scan light / mslight).
    // Field names and order mirror the authored TypeTree so Unity binds the serialized worksheet.
    [Serializable]
    public class LiveTimelineKeyProjectorData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Projector;

        public int motionID;
        public int materialID;
        public float speed = 1f;
        public Color color1 = Color.white;
        public float power;
        public Vector3 position;
        public float rotate;
        public Vector2 size = Vector2.one;
        public int LightBlendMode;
        public int loopType;
        public int loopCount;
        public int loopExecutedCount;
        public int loopIntervalFrame;
        public bool isPasteLoopUnit;
        public bool isChangeLoopInterpolate;
    }

    [Serializable]
    public class LiveTimelineKeyProjectorDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyProjectorData> { }

    [Serializable]
    public class LiveTimelineProjectorData : ILiveTimelineGroupDataWithName
    {
        // Native StageController.GetMirrorScanLightKeyName: "mslight" + index.
        public const string DefaultName = "mslight";

        public LiveTimelineKeyProjectorDataList keys = new LiveTimelineKeyProjectorDataList();

        public override ILiveTimelineKeyDataList GetKeyList() => keys;

        public LiveTimelineProjectorData() : base(DefaultName) { }
    }
}
