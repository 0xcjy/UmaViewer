using UnityEngine;

namespace Gallop.Live.Cutt
{
    //贝塞尔曲线计算过程中的数据容器
    //从时间轴关键帧中读取控制点
    //保存贝塞尔曲线的起点、终点和控制点
    public class BezierCalcWork
    {
        public static readonly BezierCalcWork cameraPos = new BezierCalcWork();
        public static readonly BezierCalcWork cameraLookAt = new BezierCalcWork();

        private readonly Vector3[] _pointArray = new Vector3[17];

        public void Set(Vector3 startPos, Vector3 endPos, int bezierNum)
        {
            _pointArray[0] = startPos;
            _pointArray[bezierNum + 1] = endPos;
        }

        public void UpdatePoints(
            LiveTimelineKeyCameraPositionData posKey,
            LiveTimelineControl timelineControl)
        {
            posKey.GetBezierPoints(timelineControl, _pointArray, 1);
        }

        public void UpdatePoints(
            LiveTimelineKeyCameraLookAtData lookAtKey,
            LiveTimelineControl timelineControl)
        {
            lookAtKey.GetBezierPoints(timelineControl, _pointArray, 1);
        }

        public void Calc(int bezierNum, float t, out Vector3 pos)
        {
            BezierUtil.Calc(_pointArray, bezierNum + 2, t, out pos);
        }
        public void UpdatePoints(
            LiveTimelineKeyCameraLookAtData lookAtKey,
            LiveTimelineControl timelineControl,
            Vector3 camPos)
        {
            lookAtKey.GetBezierPoints(timelineControl, camPos, _pointArray, 1);
        }
    }
}