using System;
namespace Gallop.Live.Cutt
{
    //这里面的Math.并非system.Math,而是gallop专用数学库gallop math!
    //时间轴缓动函数工具类
    //缓动函数用于改变普通线性插值的运动节奏
    public static class LiveTimelineEasing
    {
        public enum Type
        {
            Linear,
            ExpoEaseOut,
            ExpoEaseIn,
            ExpoEaseInOut,
            ExpoEaseOutIn,
            CircEaseOut,
            CircEaseIn,
            CircEaseInOut,
            CircEaseOutIn,
            QuadEaseOut,
            QuadEaseIn,
            QuadEaseInOut,
            QuadEaseOutIn,
            SineEaseOut,
            SineEaseIn,
            SineEaseInOut,
            SineEaseOutIn,
            CubicEaseOut,
            CubicEaseIn,
            CubicEaseInOut,
            CubicEaseOutIn,
            QuartEaseOut,
            QuartEaseIn,
            QuartEaseInOut,
            QuartEaseOutIn,
            QuintEaseOut,
            QuintEaseIn,
            QuintEaseInOut,
            QuintEaseOutIn,
            ElasticEaseOut,
            ElasticEaseIn,
            ElasticEaseInOut,
            ElasticEaseOutIn,
            BounceEaseOut,
            BounceEaseIn,
            BounceEaseInOut,
            BounceEaseOutIn,
            BackEaseOut,
            BackEaseIn,
            BackEaseInOut,
            BackEaseOutIn
        }

        private delegate double EasingFunctionDelegate(double t, double b, double c, double d);

        private static int _easeTypeMax = -1;

        private static EasingFunctionDelegate[] _functions = new EasingFunctionDelegate[41]
        {
            Linear,
            ExpoEaseOut,
            ExpoEaseIn,
            ExpoEaseInOut,
            ExpoEaseOutIn,
            CircEaseOut,
            CircEaseIn,
            CircEaseInOut,
            CircEaseOutIn,
            QuadEaseOut,
            QuadEaseIn,
            QuadEaseInOut,
            QuadEaseOutIn,
            SineEaseOut,
            SineEaseIn,
            SineEaseInOut,
            SineEaseOutIn,
            CubicEaseOut,
            CubicEaseIn,
            CubicEaseInOut,
            CubicEaseOutIn,
            QuartEaseOut,
            QuartEaseIn,
            QuartEaseInOut,
            QuartEaseOutIn,
            QuintEaseOut,
            QuintEaseIn,
            QuintEaseInOut,
            QuintEaseOutIn,
            ElasticEaseOut,
            ElasticEaseIn,
            ElasticEaseInOut,
            ElasticEaseOutIn,
            BounceEaseOut,
            BounceEaseIn,
            BounceEaseInOut,
            BounceEaseOutIn,
            BackEaseOut,
            BackEaseIn,
            BackEaseInOut,
            BackEaseOutIn
        };

        public static int easeTypeMax
        {
            get
            {
                if (_easeTypeMax < 0)
                {
                    foreach (object value in Enum.GetValues(typeof(Type)))
                    {
                        if ((int)value > _easeTypeMax)
                        {
                            _easeTypeMax = (int)value;
                        }
                    }
                    _easeTypeMax++;
                }
                return _easeTypeMax;
            }
        }

        public static float GetValue(Type type, float t, float b, float c, float d)
        {
            if ((int)type < easeTypeMax)
            {
                return (float)_functions[(int)type](t, b, c, d);
            }
            return 0f;
        }

        public static double Linear(double t, double b, double c, double d)
        {
            return c * t / d + b;
        }

        public static double ExpoEaseOut(double t, double b, double c, double d)
        {
            if (Math.IsDoubleEqual(t, d))
                return b + c;

            return (1.0 - System.Math.Pow(2.0, t * -10.0 / d)) * c + b;
        }

        public static double ExpoEaseIn(double t, double b, double c, double d)
        {
            if (Math.IsDoubleEqual(t, 0.0))
                return b;

            return System.Math.Pow(2.0, (t / d - 1.0) * 10.0) * c + b;
        }

        public static double ExpoEaseInOut(double t, double b, double c, double d)
        {
            if (Gallop.Math.IsDoubleEqual(t, 0.0))
                return b;

            if (Gallop.Math.IsDoubleEqual(t, d))
                return b + c;

            double t2 = t / (d * 0.5);

            if (t2 < 1.0)
            {
                return (c * 0.5) * System.Math.Pow(2.0, (t2 - 1.0) * 10.0) + b;
            }

            t2 = t2 - 1.0;

            return (c * 0.5) * (2.0 - System.Math.Pow(2.0, t2 * -10.0)) + b;
        }

        public static double ExpoEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return ExpoEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return ExpoEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double CircEaseOut(double t, double b, double c, double d)
        {
            return c * System.Math.Sqrt(1.0 - (t = t / d - 1.0) * t) + b;
        }

        public static double CircEaseIn(double t, double b, double c, double d)
        {
            return (0.0 - c) * (System.Math.Sqrt(1.0 - (t /= d) * t) - 1.0) + b;
        }

        public static double CircEaseInOut(double t, double b, double c, double d)
        {
            if ((t /= d / 2.0) < 1.0)
            {
                return (0.0 - c) / 2.0 * (System.Math.Sqrt(1.0 - t * t) - 1.0) + b;
            }
            return c / 2.0 * (System.Math.Sqrt(1.0 - (t -= 2.0) * t) + 1.0) + b;
        }

        public static double CircEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return CircEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return CircEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double QuadEaseOut(double t, double b, double c, double d)
        {
            return (0.0 - c) * (t /= d) * (t - 2.0) + b;
        }

        public static double QuadEaseIn(double t, double b, double c, double d)
        {
            return c * (t /= d) * t + b;
        }

        public static double QuadEaseInOut(double t, double b, double c, double d)
        {
            if ((t /= d / 2.0) < 1.0)
            {
                return c / 2.0 * t * t + b;
            }
            return (0.0 - c) / 2.0 * ((t -= 1.0) * (t - 2.0) - 1.0) + b;
        }

        public static double QuadEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return QuadEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return QuadEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double SineEaseOut(double t, double b, double c, double d)
        {
            return c * System.Math.Sin(t / d * (System.Math.PI / 2.0)) + b;
        }

        public static double SineEaseIn(double t, double b, double c, double d)
        {
            return (0.0 - c) * System.Math.Cos(t / d * (System.Math.PI / 2.0)) + c + b;
        }

        public static double SineEaseInOut(double t, double b, double c, double d)
        {
            if ((t /= d / 2.0) < 1.0)
            {
                return c / 2.0 * System.Math.Sin(System.Math.PI * t / 2.0) + b;
            }
            return (0.0 - c) / 2.0 * (System.Math.Cos(System.Math.PI * (t -= 1.0) / 2.0) - 2.0) + b;
        }

        public static double SineEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return SineEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return SineEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double CubicEaseOut(double t, double b, double c, double d)
        {
            return c * ((t = t / d - 1.0) * t * t + 1.0) + b;
        }

        public static double CubicEaseIn(double t, double b, double c, double d)
        {
            return c * (t /= d) * t * t + b;
        }

        public static double CubicEaseInOut(double t, double b, double c, double d)
        {
            if ((t /= d / 2.0) < 1.0)
            {
                return c / 2.0 * t * t * t + b;
            }
            return c / 2.0 * ((t -= 2.0) * t * t + 2.0) + b;
        }

        public static double CubicEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return CubicEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return CubicEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double QuartEaseOut(double t, double b, double c, double d)
        {
            return (0.0 - c) * ((t = t / d - 1.0) * t * t * t - 1.0) + b;
        }

        public static double QuartEaseIn(double t, double b, double c, double d)
        {
            return c * (t /= d) * t * t * t + b;
        }

        public static double QuartEaseInOut(double t, double b, double c, double d)
        {
            if ((t /= d / 2.0) < 1.0)
            {
                return c / 2.0 * t * t * t * t + b;
            }
            return (0.0 - c) / 2.0 * ((t -= 2.0) * t * t * t - 2.0) + b;
        }

        public static double QuartEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return QuartEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return QuartEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double QuintEaseOut(double t, double b, double c, double d)
        {
            return c * ((t = t / d - 1.0) * t * t * t * t + 1.0) + b;
        }

        public static double QuintEaseIn(double t, double b, double c, double d)
        {
            return c * (t /= d) * t * t * t * t + b;
        }

        public static double QuintEaseInOut(double t, double b, double c, double d)
        {
            if ((t /= d / 2.0) < 1.0)
            {
                return c / 2.0 * t * t * t * t * t + b;
            }
            return c / 2.0 * ((t -= 2.0) * t * t * t * t + 2.0) + b;
        }

        public static double QuintEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return QuintEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return QuintEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double ElasticEaseOut(double t, double b, double c, double d)
        {
            double t2 = t / d;

            if (Gallop.Math.IsDoubleEqual(t2, 1.0))
                return b + c;

            double p = d * 0.3;
            double s = p * 0.25;

            return c *System.Math.Pow(2.0, t2 * -10.0) * System.Math.Sin((t2 * d - s) * 6.28318531 / p) + c + b;
        }

        public static double ElasticEaseIn(double t, double b, double c, double d)
        {
            double t2 = t / d;

            if (Gallop.Math.IsDoubleEqual(t2, 1.0))
                return b + c;

            double p = d * 0.3;
            double s = p * 0.25;

            t2 = t2 - 1.0;

            return c * System.Math.Pow(2.0, t2 * 10.0) * System.Math.Sin((t2 * d - s) * -6.28318531 / p) + b;
        }

        public static double ElasticEaseInOut(double t, double b, double c, double d)
        {
            double t2 = t / (d * 0.5);

            if (Gallop.Math.IsDoubleEqual(t2, 2.0))
                return b + c;

            double p = d * 0.45;
            double s = p * 0.25;

            double t3 = t2 - 1.0;

            if (t2 < 1.0)
            {
                return (c * 0.5) * System.Math.Pow(2.0, t3 * 10.0) * System.Math.Sin((t3 * d - s) * -6.28318531 / p) + b;
            }

            return (c * 0.5) * System.Math.Pow(2.0, t3 * -10.0) * System.Math.Sin((t3 * d - s) * 6.28318531 / p)+ c + b;
        }

        public static double ElasticEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return ElasticEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return ElasticEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double BounceEaseOut(double t, double b, double c, double d)
        {
            if ((t /= d) < 0.36363636363636365)
            {
                return c * (7.5625 * t * t) + b;
            }
            if (t < 0.72727272727272729)
            {
                return c * (7.5625 * (t -= 0.54545454545454541) * t + 0.75) + b;
            }
            if (t < 0.90909090909090906)
            {
                return c * (7.5625 * (t -= 0.81818181818181823) * t + 0.9375) + b;
            }
            return c * (7.5625 * (t -= 21.0 / 22.0) * t + 63.0 / 64.0) + b;
        }

        public static double BounceEaseIn(double t, double b, double c, double d)
        {
            return c - BounceEaseOut(d - t, 0.0, c, d) + b;
        }

        public static double BounceEaseInOut(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return BounceEaseIn(t * 2.0, 0.0, c, d) * 0.5 + b;
            }
            return BounceEaseOut(t * 2.0 - d, 0.0, c, d) * 0.5 + c * 0.5 + b;
        }

        public static double BounceEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return BounceEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return BounceEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }

        public static double BackEaseOut(double t, double b, double c, double d)
        {
            return c * ((t = t / d - 1.0) * t * (2.70158 * t + 1.70158) + 1.0) + b;
        }

        public static double BackEaseIn(double t, double b, double c, double d)
        {
            return c * (t /= d) * t * (2.70158 * t - 1.70158) + b;
        }

        public static double BackEaseInOut(double t, double b, double c, double d)
        {
            double num = 1.70158;
            if ((t /= d / 2.0) < 1.0)
            {
                return c / 2.0 * (t * t * (((num *= 1.525) + 1.0) * t - num)) + b;
            }
            return c / 2.0 * ((t -= 2.0) * t * (((num *= 1.525) + 1.0) * t + num) + 2.0) + b;
        }

        public static double BackEaseOutIn(double t, double b, double c, double d)
        {
            if (t < d / 2.0)
            {
                return BackEaseOut(t * 2.0, b, c / 2.0, d);
            }
            return BackEaseIn(t * 2.0 - d, b + c / 2.0, c / 2.0, d);
        }
    }
}
