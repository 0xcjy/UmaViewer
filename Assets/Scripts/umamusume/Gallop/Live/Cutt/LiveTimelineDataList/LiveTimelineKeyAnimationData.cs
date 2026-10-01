using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyAnimationData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return (LiveTimelineKeyDataType)29;
            }
        }

        public int animationID;
        public WrapMode wrapMode;
        public float speed;
        public float offsetTime;

        public LiveTimelineKeyAnimationData()
        {
            speed = 1.0f;
        }
    }
}