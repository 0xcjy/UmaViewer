using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Gallop
{
    public static class Math
    {
        //常量
        public const float PI = 3.1415927f;
        public const float PI_HALF = 1.5707964f;
        public const float PI_DEG = 180f;
        public const float DOUBLE_PI_DEG = 360f;
        public const float DEG_2_RAD = 0.017453292f;
        public const float EPSILON = 1E-06f;
        private const float ANG_EPSILON = 0.00001f;

        //常用向量
        public static readonly Vector2 VECTOR2_ZERO = Vector2.zero;
        public static readonly Vector3 VECTOR3_ZERO = Vector3.zero;
        public static readonly Vector4 VECTOR4_ZERO = Vector4.zero;

        public static readonly Vector2 VECTOR2_ONE = Vector2.one;
        public static readonly Vector2 VECTOR2_HALF = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 VECTOR2_HALF_ZERO = new Vector2(0.5f, 0.0f);
        public static readonly Vector2 VECTOR2_UPPER_CENTER = new Vector2(0.5f, 1.0f);
        public static readonly Vector2 VECTOR2_CENTER_RIGHT = new Vector2(1.0f, 0.5f);

        public static readonly Vector3 VECTOR3_ONE = Vector3.one;
        public static readonly Vector4 VECTOR4_ONE = Vector4.one;

        public static readonly Vector2 VECTOR2_UP = Vector2.up;

        public static readonly Vector3 VECTOR3_UP = Vector3.up;
        public static readonly Vector3 VECTOR3_DOWN = Vector3.down;
        public static readonly Vector3 VECTOR3_FORWARD = Vector3.forward;
        public static readonly Vector3 VECTOR3_RIGHT = Vector3.right;
        public static readonly Vector3 VECTOR3_LEFT = Vector3.left;
        public static readonly Vector3 VECTOR3_BACK = Vector3.back;

        public static readonly Vector3 VECTOR3_INVERSE_ROTATION = new Vector3(0.0f, 180.0f, 0.0f);

        public static readonly Quaternion QUATERNION_IDENTITY = Quaternion.identity;

        public static readonly Matrix4x4 MATRIX4X4_IDENTITY = Matrix4x4.identity;
        public static readonly Matrix4x4 MATRIX4X4_ZERO = Matrix4x4.zero;

        public static readonly Vector2Int VECTOR2_INT_ZERO = Vector2Int.zero;

        private const float MasterFloatAccuracy = 10000f;
        private const float MasterInt2FloatAccuracy = 0.0001f;

        //数学函数
        public static Vector2 Bezier(Vector2[] controlPoints, float t)
        {
            if (controlPoints == null)
                throw new NullReferenceException();

            if ((uint)controlPoints.Length < 4u)
                throw new IndexOutOfRangeException();

            float invT = 1.0f - t;

            return
                controlPoints[0] * (invT * invT * invT) +
                controlPoints[1] * (invT * ((t * 3.0f) * invT)) +
                controlPoints[2] * (invT * ((t * 3.0f) * t)) +
                controlPoints[3] * ((t * t) * t);
        }

        public static float Linear(ref AnimationCurve curve, float time)
        {
            if (curve == null)
                throw new NullReferenceException();

            int length = curve.length;
            int lastIndex = length - 1;

            if (length < 1)
            {
                // 官方这里实际会走 get_Item(length - 1)，也就是 -1，通常会抛异常
                return curve[lastIndex].value;
            }

            int index = 0;

            while (true)
            {
                if (curve == null)
                    throw new NullReferenceException();

                Keyframe key = curve[index];

                if (key.time > time)
                    break;

                index++;

                if (length == index)
                {
                    if (curve == null)
                        throw new NullReferenceException();

                    return curve[lastIndex].value;
                }
            }

            if (index == 0)
            {
                if (curve == null)
                    throw new NullReferenceException();

                return curve[0].value;
            }

            if (curve == null)
                throw new NullReferenceException();

            Keyframe nextKey = curve[index];

            if (curve == null)
                throw new NullReferenceException();

            Keyframe prevKey = curve[index - 1];

            float nextTime = nextKey.time;
            float prevTime = prevKey.time;

            float prevValue = prevKey.value;
            float nextValue = nextKey.value;

            return prevValue + ((time - prevTime) / (nextTime - prevTime)) * (nextValue - prevValue);
        }

        public static float Linear(ref List<Keyframe> keys, float time)
        {
            if (keys == null)
                throw new NullReferenceException();

            int count = keys.Count;
            int lastIndex = count - 1;

            if (count < 1)
            {
                // 官方这里等价于 List get_Item(-1)
                return keys[lastIndex].value;
            }

            int index = 0;

            while (true)
            {
                if (keys == null)
                    throw new NullReferenceException();

                Keyframe key = keys[index];

                if (key.time > time)
                    break;

                index++;

                if (count == index)
                {
                    if (keys == null)
                        throw new NullReferenceException();

                    return keys[lastIndex].value;
                }
            }

            if (index == 0)
            {
                if (keys == null)
                    throw new NullReferenceException();

                return keys[0].value;
            }

            if (keys == null)
                throw new NullReferenceException();

            Keyframe nextKey = keys[index];

            if (keys == null)
                throw new NullReferenceException();

            Keyframe prevKey = keys[index - 1];

            float nextTime = nextKey.time;
            float prevTime = prevKey.time;

            float prevValue = prevKey.value;
            float nextValue = nextKey.value;

            return prevValue + ((time - prevTime) / (nextTime - prevTime)) * (nextValue - prevValue);
        }

        public static float EaseOut(float start, float end, float time)
        {
            return start - ((end * time) * (time - 2.0f));
        }

        
        public static float MapClamp(float value, float start1, float stop1, float start2, float stop2)
        {
            float max = start2 > stop2 ? start2 : stop2;
            float min = start2 < stop2 ? start2 : stop2;

            float mapped = (((value - start1) / (stop1 - start1)) * (stop2 - start2)) + start2;

            if (mapped > max)
                return max;

            if (mapped < min)
                return min;

            return mapped;
        }

        public static float Map(float value, float start1, float stop1, float start2, float stop2)
        {
            return ((value - start1) / (stop1 - start1)) * (stop2 - start2) + start2;
        }

        public static Quaternion FromMayaEuler(Vector3 mayaEuler)
        {
            Quaternion qZ = Quaternion.Euler(0.0f, 0.0f, mayaEuler.z);
            Quaternion qY = Quaternion.Euler(0.0f, mayaEuler.y, 0.0f);
            Quaternion qX = Quaternion.Euler(mayaEuler.x, 0.0f, 0.0f);

            return qZ * qY * qX;
        }


        public static float GetFocalLength(float fov, float aperture)
        {
            return (1.0f / (Mathf.Tan((fov * 0.5f) * DEG_2_RAD) / (aperture * 25.4f))) * 0.5f;
        }

        public static float GetFocalLength(float fov)
        {
            return (1.0f / (Mathf.Tan((fov * 0.5f) * DEG_2_RAD) / 24.003f)) * 0.5f;
        }

        public static void Normalized(ref Vector3 vec)
        {
            // 官方没有 zero guard，0 向量会除 0，保持一致
            float z = vec.z;
            float length = Mathf.Sqrt((vec.x * vec.x) + (vec.y * vec.y) + (z * z));

            vec.x = vec.x / length;
            vec.y = vec.y / length;
            vec.z = z / length;
        }

        public static float GetAng(Vector3 p1, Vector3 p2)
        {
            // 官方只看 XZ 平面，忽略 y
            float x = p2.x - p1.x;
            float y = p2.z - p1.z;

            return GetAngInternal(x, y);
        }

        public static float GetAng(Vector3 p)
        {
            //官方只看XZ平面,忽略y
            return GetAngInternal(p.x, p.z);
        }

        public static float GetAng(Vector2 p)
        {
            return GetAngInternal(p.x, p.y);
        }

        private static float GetAngInternal(float x, float y)
        {
            float length = Mathf.Sqrt((x * x) + (y * y));

            float nx;
            float ny;

            if (length <= ANG_EPSILON)
            {
                // 这里读 UnityEngine.Vector2 的第一个 static field,也就是 Vector2.zero
                nx = Vector2.zero.x;
                ny = Vector2.zero.y;
            }
            else
            {
                nx = x / length;
                ny = y / length;
            }

            //返回 acosf,不乘 Rad2Deg,所以返回值是弧度
            return Mathf.Acos(ny / Mathf.Sqrt((nx * nx) + (ny * ny)));
        }

        public static bool LineIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 point)
        {
            //官方做法先把 out point 设成 Vector2.zero
            point = Vector2.zero;

            Vector2 ab = b - a;

            float denom = ((d.y - c.y) * ab.x) - ((d.x - c.x) * ab.y);

            if (denom == 0.0f)
                return false;

            double t =
                (((d.x - c.x) * (a.y - c.y)) -
                 ((d.y - c.y) * (a.x - c.x))) / denom;

            double u =
                ((ab.x * (a.y - c.y)) -
                 (ab.y * (a.x - c.x))) / denom;

            if (t > 1.0 || u > 1.0 || t < 0.0 || u < 0.0)
                return false;

            point = new Vector2(
                a.x + (ab.x * (float)t),
                a.y + (ab.y * (float)t)
            );

            return true;
        }


        public static Vector3 CreatePolygonNormal(Vector3 A, Vector3 B, Vector3 C)
        {
            float v17 = B.x - A.x;
            float v18 = B.y - A.y;
            float v19 = B.z - A.z;

            float v20 = C.x - B.x;
            float v21 = C.y - B.y;
            float v22 = C.z - B.z;

            float x = (v18 * v22) - (v19 * v21);
            float y = (v19 * v20) - (v17 * v22);
            float z = (v17 * v21) - (v18 * v20);

            float length = Mathf.Sqrt((x * x) + (y * y) + (z * z));

            if (length > ANG_EPSILON)
            {
                return new Vector3(
                    x / length,
                    y / length,
                    z / length
                );
            }
            //fallback读 UnityEngine.Vector3第一个 static field,也就是 Vector3.zero
            return Vector3.zero;
        }

        public static int Round(float value)
        {

            double rounded = System.Math.Round((double)value, 0, MidpointRounding.AwayFromZero);

            if (double.IsPositiveInfinity(rounded))
                return int.MinValue;

            return (int)rounded;
        }

        
        public static bool IsFloatEqual(float value1, float value2)
        {
            float abs1 = Mathf.Abs(value1);
            float abs2 = Mathf.Abs(value2);

            float maxAbs = abs1 <= abs2 ? abs2 : abs1;

            float tolerance = maxAbs * 0.000001f;

            float minTolerance = float.Epsilon * 8.0f;
            if (tolerance <= minTolerance)
                tolerance = minTolerance;

            return Mathf.Abs(value2 - value1) < tolerance;
        }

        public static bool IsDoubleEqual(double value1, double value2)
        {
            return System.Math.Abs(value1 - value2) < 0.000001;
        }

        public static bool IsFloatEqualLight(float value1, float value2)
        {
            return (value2 + 0.000001f) >= value1 && (value2 - 0.000001f) <= value1;
        }

        public static bool IsEqualVector(ref Vector3 posA, ref Vector3 posB)
        {
            float ax = posA.x;
            float bx = posB.x;

            if ((bx - 0.000001f) > ax || (bx + 0.000001f) < ax)
                return false;

            float ay = posA.y;
            float by = posB.y;

            if ((by - 0.000001f) > ay || (by + 0.000001f) < ay)
                return false;

            float az = posA.z;
            float bz = posB.z;

            return (bz + 0.000001f) >= az &&
                   (bz - 0.000001f) <= az;
        }

        public static bool IsEqualVector(ref Vector2 vecA, ref Vector2 vecB)
        {
            float ax = vecA.x;
            float bx = vecB.x;

            if ((bx - 0.000001f) > ax || (bx + 0.000001f) < ax)
                return false;

            float ay = vecA.y;
            float by = vecB.y;

            return (by + 0.000001f) >= ay &&
                   (by - 0.000001f) <= ay;
        }

        public static bool IsEqualColor(in Color colA, in Color colB)
        {
            float ar = colA.r;
            float br = colB.r;

            if ((br - 0.000001f) > ar || (br + 0.000001f) < ar)
                return false;

            float ag = colA.g;
            float bg = colB.g;

            if ((bg - 0.000001f) > ag || (bg + 0.000001f) < ag)
                return false;

            float ab = colA.b;
            float bb = colB.b;

            if ((bb - 0.000001f) > ab || (bb + 0.000001f) < ab)
                return false;

            float aa = colA.a;
            float ba = colB.a;

            return (ba + 0.000001f) >= aa &&
                   (ba - 0.000001f) <= aa;
        }

        private static bool IsQuaternionEqualLight(ref Quaternion rotationA, ref Quaternion rotationB)
        {
            float ax = rotationA.x;
            float bx = rotationB.x;

            if ((bx - 0.000001f) > ax || (bx + 0.000001f) < ax)
                return false;

            float ay = rotationA.y;
            float by = rotationB.y;

            if ((by - 0.000001f) > ay || (by + 0.000001f) < ay)
                return false;

            float az = rotationA.z;
            float bz = rotationB.z;

            if ((bz - 0.000001f) > az || (bz + 0.000001f) < az)
                return false;

            float aw = rotationA.w;
            float bw = rotationB.w;

            return (bw + 0.000001f) >= aw &&
                   (bw - 0.000001f) <= aw;
        }

        public static float Sinf(float degree)
        {
            return Mathf.Sin(degree * 0.017453f);
        }

        public static float Cosf(float degree)
        {
            return Mathf.Cos((degree * 3.1416f) / 180.0f);
        }

        public static float Tanf(float degree)
        {
            return Mathf.Tan((degree * 3.1416f) / 180.0f);
        }

        public static float InnerVector(Vector2 v1, Vector2 v2)
        {
            return (v1.x * v2.x) + (v1.y * v2.y);
        }

        public static bool GetRotation(ref float current, float next, float speed)
        {
            float oldCurrent = current;

            float diff = next - oldCurrent;

            float wrapped = diff + (Mathf.Floor(diff / 360.0f) * -360.0f);

            float v4 = 360.0f;
            if (wrapped <= 360.0f)
                v4 = wrapped;

            float rot = 0.0f;
            if (!(wrapped < 0.0f))
                rot = v4;

            if (rot > 180.0f)
                rot = rot - 360.0f;

            if (rot >= -speed)
            {
                if (rot <= speed)
                {
                    current = next;
                    return true;
                }

                current = oldCurrent + speed;
                return false;
            }

            current = oldCurrent - speed;
            return false;
        }

        public static float Gcd(float x, float y)
        {
            float dividend = x;
            float divisor = y;

            while (true)
            {
                float oldDivisor = divisor;
                divisor = dividend;

                if (divisor >= oldDivisor)
                {
                    dividend = divisor;
                    divisor = oldDivisor;
                    break;
                }

                dividend = oldDivisor;
            }

            if (Mathf.Abs(divisor) <= 0.000001f)
                return dividend;

            do
            {
                float oldDivisor = divisor;
                divisor = dividend % divisor;
                dividend = oldDivisor;
            }
            while (Mathf.Abs(divisor) > 0.000001f);

            return dividend;
        }

        public static int MasterFloat2Int(float value)
        {
            float v = value * 10000.0f;

            if (float.IsPositiveInfinity(v))
                return int.MinValue;

            return (int)v;
        }

        public static float MasterInt2Float(int value)
        {
            return value * 0.0001f;
        }

        public static float MasterInt2FloatPercent(int value)
        {
            decimal v = (decimal)value * new decimal(10, 0, 0, false, 3);
            return (float)v;
        }

        public static int Discount(int targetValue, int discountRate)
        {
            uint unsignedRate = (uint)discountRate;

            uint rateValue;
            if (unsignedRate <= 100u)
                rateValue = (uint)(100 - discountRate);
            else
                rateValue = 0u;

            int finalRate;
            if (discountRate >= 0)
                finalRate = (int)rateValue;
            else
                finalRate = 100;

            //官方用decimal做除法和乘法,然后Floor
            decimal rateDecimal = (decimal)finalRate / new decimal(100);
            decimal targetDecimal = (decimal)targetValue;
            decimal result = rateDecimal * targetDecimal;

            return (int)decimal.Floor(result);
        }

        public static float Percentage(float numerator, float denominator)
        {
            return (numerator / denominator) * 100.0f;
        }

        public static bool WonLottery(int percent)
        {
            return UnityEngine.Random.Range(0, 100) < percent;
        }


        public static float Demical(float targetValue, int demicalValue)
        {
            double pow = System.Math.Pow(10.0, demicalValue);

            float baseValue;
            if (double.IsPositiveInfinity(pow))
                baseValue = -2147500000.0f;
            else
                baseValue = (float)(int)pow;

            double rounded = System.Math.Round(
                (double)(baseValue * targetValue),
                0,
                MidpointRounding.AwayFromZero
            );

            float roundedValue;
            if (double.IsPositiveInfinity(rounded))
                roundedValue = -2147500000.0f;
            else
                roundedValue = (float)(int)rounded;

            return roundedValue / baseValue;
        }

        public static float DivideSafe(float numerator, float denominator)
        {
            //这个函数伪代码只剩class init,没有显示真实返回逻辑,先按DivideSafe的官方意图写,之后应该再看一次汇编确认
            if (Mathf.Abs(denominator) < 0.000001f)
                return 0.0f;

            return numerator / denominator;
        }

        public static float Ceil(float value)
        {
            return Mathf.Ceil(value - 0.000001f);
        }

        public static float Floor(float value)
        {
            return Mathf.Floor(value + 0.000001f);
        }

        public static int CeilToInt(float value)
        {
            float v = value - 0.000001f;
            float ceil = Mathf.Ceil(v);

            if (float.IsPositiveInfinity(ceil))
                return int.MinValue;

            return Mathf.CeilToInt(v);
        }

        public static int FloorToInt(float value)
        {
            float v = value + 0.000001f;
            float floor = Mathf.Floor(v);

            if (float.IsPositiveInfinity(floor))
                return int.MinValue;

            return Mathf.FloorToInt(v);
        }

        public static int Digit(int num)
        {
            if (num < 10)
                return 1;

            uint v = (uint)num;
            int result = 1;

            do
            {
                result++;
                bool keep = v > 99u;
                v /= 10u;

                if (!keep)
                    break;
            }
            while (true);

            return result;
        }

        public static int Digit(double num)
        {
            int i = 1;

            while (num >= 10.0)
            {
                num /= 10.0;
                i++;
            }

            return i;
        }

        public static int GetPointDigit(int num, int digit)
        {
            float div = num / Mathf.Pow(10.0f, digit - 1);

            if (float.IsPositiveInfinity(div))
                return -8;

            return ((int)div) % 10;
        }

        public static int GetParcentage(float value)
        {
            double rounded = System.Math.Round(
                (double)(value * 100.0f),
                0,
                MidpointRounding.AwayFromZero
            );

            if (double.IsPositiveInfinity(rounded))
                return int.MinValue;

            return (int)rounded;
        }

        public static float GetMemorySizeMiB(long memorySize)
        {
            return memorySize / 1048576.0f;
        }

        public static int Combination(int n, int r)
        {
            int v2 = n;

            if (r != 0)
            {
                int result = 0;
                int i = r - 1;

                while (true)
                {
                    if (v2 < r)
                        return result;

                    if (r == v2)
                        return result + 1;

                    v2--;
                    result += Combination(v2, i);
                }
            }
            else
            {
                return v2 >= 0 ? 1 : 0;
            }
        }
    }
}