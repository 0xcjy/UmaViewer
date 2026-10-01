using System;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public abstract class LiveTimelineKey
    {
        public abstract LiveTimelineKeyDataType dataType { get; }

        public int frame;

        public LiveTimelineKeyAttribute attribute;

        // 项目兼容用：官方 dummy 没有这个属性，但你现有很多代码在用 arg.FrameSecond
        public double FrameSecond
        {
            get
            {
                return (double)frame / 60.0;
            }
            set
            {
                frame = (int)System.Math.Round(value * 60.0);
            }
        }

        public virtual bool IsInterpolateKey()
        {
            return false;
        }

        public virtual void OnLoad(LiveTimelineControl timelineControl)
        {
        }

        protected LiveTimelineKey()
        {
        }
    }
}