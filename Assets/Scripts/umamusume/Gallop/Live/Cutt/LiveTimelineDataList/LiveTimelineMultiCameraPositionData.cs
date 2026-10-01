using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [System.Serializable]
    public class LiveTimelineKeyMultiCameraPositionData : LiveTimelineKeyCameraPositionData
    {
        public enum MaskType
        {
            Single = 0,
            All = 1,
            Up = 2,
            Down = 3,
            Left = 4,
            Right = 5,
            LeftUp = 6,
            RightUp = 7,
            LeftDown = 8,
            RightDown = 9,
            Fan = 10
        }

        public bool enableMultiCamera;
        public float fadeTime;
        public float lineThickness = 0.015f;
        public MultiCameraComposite.DivideLineType LineType;
        public Color LineColor = Color.white;
        public float LineAntialiasing = 0.1f;
        public LiveTimelineKeyMultiCameraPositionData.MaskType maskType;
        public bool updateMainCamera;
        public float roll;
        public float fov = 20f;
        public float maskRoll;
        public Vector2 maskOffset;
        public float MaskCentralAngle;

        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraPos;


    }

    [System.Serializable]
    public class LiveTimelineKeyMultiCameraPositionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraPositionData>
    {

    }

    [System.Serializable]
    public class LiveTimelineMultiCameraPositionData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "MultiCameraPos";
        public int MultiCameraNo;
        public LiveTimelineKeyMultiCameraPositionDataList keys;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }
}