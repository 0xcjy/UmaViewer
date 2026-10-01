using System;
using UnityEngine;

namespace Gallop
{
    [Serializable]
    public class CySpringWindParam
    {
        [SerializeField] private bool _isEnableVertical;
        [SerializeField] private bool _isEnableHorizontal;

        [SerializeField] private float _verticalCycle;
        [SerializeField] private float _horizontalCycle;

        [SerializeField] private float _verticalAngleWidth;
        [SerializeField] private float _horizontalAngleWidth;

        [SerializeField] private int _partsMask;

        [SerializeField] private float[] _powerScaleArray;

        [SerializeField] private Vector3 _direction;
        [SerializeField] private Vector3 _right;

        [SerializeField] private bool _isLocalDirection;

        public bool IsEnableVertical
        {
            get { return _isEnableVertical; }
            set { _isEnableVertical = value; }
        }

        public bool IsEnableHorizontal
        {
            get { return _isEnableHorizontal; }
            set { _isEnableHorizontal = value; }
        }

        public float VerticalCycle
        {
            get { return _verticalCycle; }
            set { _verticalCycle = value; }
        }

        public float HorizontalCycle
        {
            get { return _horizontalCycle; }
            set { _horizontalCycle = value; }
        }

        public float VerticalAngleWidth
        {
            get { return _verticalAngleWidth; }
            set { _verticalAngleWidth = value; }
        }

        public float HorizontalAngleWidth
        {
            get { return _horizontalAngleWidth; }
            set { _horizontalAngleWidth = value; }
        }

        public int PartsMask
        {
            get { return _partsMask; }
            set { _partsMask = value; }
        }

        public float[] PowerScaleArray
        {
            get { return _powerScaleArray; }
            set { _powerScaleArray = value; }
        }

        public Vector3 Direction
        {
            get { return _direction; }
            set { _direction = value; }
        }

        public Vector3 Right
        {
            get { return _right; }
            set { _right = value; }
        }

        public bool IsLocalDirection
        {
            get { return _isLocalDirection; }
            set { _isLocalDirection = value; }
        }

        public CySpringWindParam()
        {
            Initialize();
        }

        public CySpringWindParam(
            bool isEnableV,
            bool isEnableH,
            float vCycle,
            float hCycle,
            float vAngleWidth,
            float hAngleWidth,
            Vector3 direction,
            int partsMask,
            float[] powerScaleArray)
        {
            Initialize();

            _isEnableVertical = isEnableV;
            _isEnableHorizontal = isEnableH;

            _verticalCycle = vCycle;
            _horizontalCycle = hCycle;

            _verticalAngleWidth = vAngleWidth;
            _horizontalAngleWidth = hAngleWidth;

            _direction = SafeNormalize(direction, Vector3.forward);
            _right = BuildRightFromDirection(_direction);

            _partsMask = partsMask;
            _powerScaleArray = ClonePowerScaleArray(powerScaleArray);
        }

        public CySpringWindParam(CySpringWindParam other)
        {
            Initialize();
            Copy(other);
        }

        public void Initialize()
        {
            _isEnableVertical = true;
            _isEnableHorizontal = true;

            _verticalCycle = 1f;
            _horizontalCycle = 1f;

            _verticalAngleWidth = 0f;
            _horizontalAngleWidth = 0f;

            _partsMask = -1;

            _powerScaleArray = new float[]
            {
                1f, 1f, 1f
            };

            _direction = Vector3.forward;
            _right = Vector3.right;

            _isLocalDirection = false;
        }

        public void ResetParam()
        {
            Initialize();
        }

        public void GetDirection(Quaternion parentRotation, ref Vector3 direction, ref Vector3 right)
        {
            direction = SafeNormalize(_direction, Vector3.forward);
            right = SafeNormalize(_right, Vector3.right);

            if (_isLocalDirection)
            {
                direction = parentRotation * direction;
                right = parentRotation * right;
            }

            direction = SafeNormalize(direction, Vector3.forward);
            right = SafeNormalize(right, Vector3.right);
        }

        public static void SetInterp(
            CySpringWindParam prevParam,
            CySpringWindParam nextParam,
            CySpringWindParam outPatam,
            float t)
        {
            if (outPatam == null)
                return;

            if (prevParam == null && nextParam == null)
            {
                outPatam.ResetParam();
                return;
            }

            if (prevParam == null)
            {
                outPatam.Copy(nextParam);
                return;
            }

            if (nextParam == null)
            {
                outPatam.Copy(prevParam);
                return;
            }

            t = Mathf.Clamp01(t);

            outPatam.IsEnableVertical = t < 1f ? prevParam.IsEnableVertical : nextParam.IsEnableVertical;
            outPatam.IsEnableHorizontal = t < 1f ? prevParam.IsEnableHorizontal : nextParam.IsEnableHorizontal;

            outPatam.VerticalCycle = Mathf.Lerp(prevParam.VerticalCycle, nextParam.VerticalCycle, t);
            outPatam.HorizontalCycle = Mathf.Lerp(prevParam.HorizontalCycle, nextParam.HorizontalCycle, t);

            outPatam.VerticalAngleWidth = Mathf.Lerp(prevParam.VerticalAngleWidth, nextParam.VerticalAngleWidth, t);
            outPatam.HorizontalAngleWidth = Mathf.Lerp(prevParam.HorizontalAngleWidth, nextParam.HorizontalAngleWidth, t);

            outPatam.PartsMask = t < 1f ? prevParam.PartsMask : nextParam.PartsMask;

            Vector3 dir = Vector3.Lerp(prevParam.Direction, nextParam.Direction, t);
            Vector3 r = Vector3.Lerp(prevParam.Right, nextParam.Right, t);

            outPatam.Direction = SafeNormalize(dir, Vector3.forward);
            outPatam.Right = SafeNormalize(r, BuildRightFromDirection(outPatam.Direction));

            outPatam.IsLocalDirection = t < 1f ? prevParam.IsLocalDirection : nextParam.IsLocalDirection;

            outPatam.PowerScaleArray = InterpPowerScaleArray(
                prevParam.PowerScaleArray,
                nextParam.PowerScaleArray,
                t
            );
        }

        public void Copy(CySpringWindParam src)
        {
            if (src == null)
            {
                ResetParam();
                return;
            }

            _isEnableVertical = src.IsEnableVertical;
            _isEnableHorizontal = src.IsEnableHorizontal;

            _verticalCycle = src.VerticalCycle;
            _horizontalCycle = src.HorizontalCycle;

            _verticalAngleWidth = src.VerticalAngleWidth;
            _horizontalAngleWidth = src.HorizontalAngleWidth;

            _partsMask = src.PartsMask;

            _powerScaleArray = ClonePowerScaleArray(src.PowerScaleArray);

            _direction = src.Direction;
            _right = src.Right;

            _isLocalDirection = src.IsLocalDirection;
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            if (value.sqrMagnitude <= 0.000001f)
                return fallback;

            return value.normalized;
        }

        private static Vector3 BuildRightFromDirection(Vector3 direction)
        {
            direction = SafeNormalize(direction, Vector3.forward);

            Vector3 right = Vector3.Cross(Vector3.up, direction);

            if (right.sqrMagnitude <= 0.000001f)
                right = Vector3.right;

            return right.normalized;
        }

        private static float[] ClonePowerScaleArray(float[] src)
        {
            if (src == null || src.Length == 0)
            {
                return new float[]
                {
                    1f, 1f, 1f
                };
            }

            float[] dst = new float[src.Length];

            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i];

            return dst;
        }

        private static float[] InterpPowerScaleArray(float[] prev, float[] next, float t)
        {
            if (prev == null && next == null)
            {
                return new float[]
                {
                    1f, 1f, 1f
                };
            }

            if (prev == null)
                return ClonePowerScaleArray(next);

            if (next == null)
                return ClonePowerScaleArray(prev);

            int length = Mathf.Max(prev.Length, next.Length);
            float[] result = new float[length];

            for (int i = 0; i < length; i++)
            {
                float a = i < prev.Length ? prev[i] : 1f;
                float b = i < next.Length ? next[i] : 1f;

                result[i] = Mathf.Lerp(a, b, t);
            }

            return result;
        }
    }
}