using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyCameraLookAtData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.CameraLookAt;}
        }
        public LiveCameraLookAtType lookAtType;
        public Vector3 position;
        public LiveCharaPositionFlag lookAtCharaPos;
        public LiveCameraCharaParts lookAtCharaParts;
        public Vector3 charaPos;
        public Vector3[] bezierPoints;
        public float traceSpeed;

        public Vector3 rotation = Vector3.forward;
        public float eyeLength = 10f;
        public Vector3 offset = Vector3.zero;
        public bool newBezierCalcMethod;

        public bool necessaryToUseNewBezierCalcMethod
        {
            get
            {
                if (!newBezierCalcMethod)
                {
                    return GetBezierPointCount() > 3;
                }

                return true;
            }
        }

        public bool HasBezier()
        {
            return bezierPoints != null && bezierPoints.Length != 0;
        }

        public int GetBezierPointCount()
        {
            if (!HasBezier())
            {
                return 0;
            }

            return bezierPoints.Length;
        }

        public Vector3 GetBezierPoint(
            int index,
            LiveTimelineControl timelineControl,
            Vector3 camPos)
        {
            Vector3 value = GetValue(timelineControl, camPos);

            if (HasBezier() && index >= 0 && index < bezierPoints.Length)
            {
                return value + bezierPoints[index];
            }

            return value;
        }

        public void GetBezierPoints(
            LiveTimelineControl timelineControl,
            Vector3 camPos,
            Vector3[] outPoints,
            int startIndex)
        {
            if (!HasBezier())
            {
                return;
            }

            if (outPoints == null)
            {
                return;
            }

            if (startIndex < 0 || startIndex >= outPoints.Length)
            {
                return;
            }

            int num = Mathf.Min(outPoints.Length - startIndex, bezierPoints.Length);
            Vector3 value = GetValue(timelineControl, camPos);

            for (int i = 0; i < num; i++)
            {
                outPoints[startIndex + i] = value + bezierPoints[i];
            }
        }

        public virtual Vector3 GetValue(
            LiveTimelineControl timelineControl,
            Vector3 camPos)
        {
            return GetValue(timelineControl, lookAtType, camPos, true);
        }

        protected virtual Vector3 GetValue(
            LiveTimelineControl timelineControl,
            LiveCameraLookAtType type,
            Vector3 camPos,
            bool containOffset)
        {
            Vector3 vector = position;

            switch (type)
            {
                case LiveCameraLookAtType.Character:
                    if (timelineControl != null)
                    {
                        vector += timelineControl.GetPositionWithCharacters(
                            lookAtCharaPos,
                            lookAtCharaParts,
                            charaPos);
                    }
                    break;

                case LiveCameraLookAtType.Direct:
                    {
                        // 官方这里如果 IDA 里确认 Direct 会用 rotation/eyeLength，
                        // 再打开下面两行。
                        // Vector3 vector2 = Quaternion.Euler(rotation) * Vector3.forward * eyeLength;
                        // vector = camPos + vector2;
                        break;
                    }
            }

            if (!containOffset)
            {
                return vector;
            }

            return vector + offset;
        }

        public Vector3 GetValue(LiveTimelineControl timelineControl)
        {
            return GetValue(timelineControl, Vector3.zero);
        }

        public Vector3 GetBezierPoint(
            int index,
            LiveTimelineControl timelineControl)
        {
            return GetBezierPoint(index, timelineControl, Vector3.zero);
        }

        public void GetBezierPoints(
            LiveTimelineControl timelineControl,
            Vector3[] outPoints,
            int startIndex)
        {
            GetBezierPoints(timelineControl, Vector3.zero, outPoints, startIndex);
        }
    }
}