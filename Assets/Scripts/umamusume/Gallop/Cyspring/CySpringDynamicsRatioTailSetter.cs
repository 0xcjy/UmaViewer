using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;

namespace Gallop
{
    /// 控制尾巴CySpring骨骼的动态比例
    /// 使用DOTween在指定时间内渐变DynamicsRatio
    public class CySpringDynamicsRatioTailSetter
    {
        private const string IGNORE_CYSPRING_DYNAMICS_RATIO_TARGET_NAME_PARTS_1 = "Sp_Hi_Tail0_B_00";
        private const string IGNORE_CYSPRING_DYNAMICS_RATIO_TARGET_NAME_PARTS_2 = "Sp_Hi_Tail0_B_01";

        protected CySpringController _cySpringController;
        private List<CySpringBoneBase> _cySpringDynamicsTailBoneAll;
        private bool[] _cySpringDynamicsRatioTailAnimationTarget;
        protected int[] _cySpringDynamicsTailBoneIndex;
        private int _cySpringDynamicsTailBoneNum;
        protected float _cySpringTailDynamicsRatio;
        private int _cySpringDynamicsTailTotalBoneNum;
        private string[] _cySpringAnimTailBoneNameArray;
        public bool IsDurationDynamicsRatioTail;
        public bool _stopBlendTweenDynamicsRatioTail;

        public CySpringDynamicsRatioTailSetter()
        {
            // *(_QWORD *)(a1 + 52) = 0x53F800000LL;
            // 0x34 float = 1.0f
            // 0x38 int   = 5
            _cySpringTailDynamicsRatio = 1.0f;
            _cySpringDynamicsTailTotalBoneNum = 5;
        }

        public static bool IsCySpringDynamicRatioTarget(string boneName)
        {
            if (boneName == null)
                throw new NullReferenceException();


            return boneName.Contains("Sp_Hi_Tail0_B_");
        }

        public static void SetCySpringDynamicsRatioAction(
            float ratio,
            CySpringDynamicsRatioTailSetter obj,
            int cySpringDynamicsTailTotalBoneNum)
        {
            if (cySpringDynamicsTailTotalBoneNum < 1)
                return;

            if (obj == null)
                throw new NullReferenceException();

            for (int i = 0; i < cySpringDynamicsTailTotalBoneNum; i++)
            {
                if (obj._cySpringDynamicsTailBoneIndex == null)
                    throw new NullReferenceException();

                bool isTargetIndex = false;


                for (int j = 0; j < obj._cySpringDynamicsTailBoneIndex.Length; j++)
                {
                    if (i == obj._cySpringDynamicsTailBoneIndex[j])
                    {
                        isTargetIndex = true;
                        break;
                    }
                }

                if (obj._cySpringDynamicsTailBoneAll == null)
                    throw new NullReferenceException();

                CySpringBoneBase bone = obj._cySpringDynamicsTailBoneAll[i];
                if (bone == null)
                    throw new NullReferenceException();

                if (isTargetIndex)
                {
                    if (obj._cySpringDynamicsRatioTailAnimationTarget == null)
                        throw new NullReferenceException();

                    if (obj._cySpringDynamicsRatioTailAnimationTarget[i])
                    {
                        bone.DynamicsRatio = ratio;
                    }
                }
                else
                {
                    bone.DynamicsRatio = 1.0f;
                }
            }
        }

        public static void SetDynamicsRatioTailAllAction(
            float ratio,
            CySpringDynamicsRatioTailSetter obj,
            int cySpringDynamicsTailTotalBoneNum)
        {
            if (obj == null)
                throw new NullReferenceException();

            if (obj._cySpringDynamicsTailBoneAll == null)
                throw new NullReferenceException();

            int count = obj._cySpringDynamicsTailBoneAll.Count;

            for (int i = 0; i < count; i++)
            {
                if (obj._cySpringDynamicsRatioTailAnimationTarget == null)
                    throw new NullReferenceException();

                if (obj._cySpringDynamicsRatioTailAnimationTarget[i])
                {
                    CySpringBoneBase bone = obj._cySpringDynamicsTailBoneAll[i];
                    if (bone == null)
                        throw new NullReferenceException();

                    bone.DynamicsRatio = ratio;
                }
            }
        }

        public void Init(
            CySpringController cySpringController,
            int cySpringDynamicsTailTotalBoneNum,
            string[] cySpringAnimTailBoneNameArray)
        {
            _cySpringController = cySpringController;
            _cySpringAnimTailBoneNameArray = cySpringAnimTailBoneNameArray;
            _cySpringDynamicsTailTotalBoneNum = cySpringDynamicsTailTotalBoneNum;
            IsDurationDynamicsRatioTail = false;
        }

        public void SetDynamicsRatioTailAll(float targetRatio, float duration)
        {
            SetDynamicsRatioTail(
                targetRatio,
                duration,
                SetDynamicsRatioTailAllAction
            );
        }

        public void StopBlendTween()
        {
            _stopBlendTweenDynamicsRatioTail = true;
        }

        public void SetDynamicsRatioTail(
            float targetRatio,
            float duration,
            Action<float, CySpringDynamicsRatioTailSetter, int> setDynamicsRatioAction = null)
        {
            if (_cySpringDynamicsTailBoneNum == 0)
            {
                _cySpringDynamicsTailBoneAll =
                    new List<CySpringBoneBase>(_cySpringDynamicsTailTotalBoneNum);

                if (_cySpringAnimTailBoneNameArray == null)
                    throw new NullReferenceException();

                _cySpringDynamicsTailBoneIndex =
                    new int[_cySpringAnimTailBoneNameArray.Length];

                if (_cySpringController == null)
                    throw new NullReferenceException();

                // 官方：
                // CySpringController.FindSpring(
                //     list,
                //     IsCySpringDynamicRatioTarget,
                //     2,
                //     true,
                //     false
                // );
                CallOfficialFindSpring(
                    _cySpringController,
                    _cySpringDynamicsTailBoneAll
                );

                int targetCount = 0;
                int allCount = _cySpringDynamicsTailBoneAll.Count;

                for (int i = 0; i < allCount; i++)
                {
                    CySpringBoneBase bone = _cySpringDynamicsTailBoneAll[i];
                    if (bone == null)
                        throw new NullReferenceException();

                    string boneName = GetCySpringBoneName(bone);

                    if (StringArrayContains(_cySpringAnimTailBoneNameArray, boneName))
                    {
                        _cySpringDynamicsTailBoneIndex[targetCount] = i;
                        targetCount++;
                    }
                }

                _cySpringDynamicsRatioTailAnimationTarget = new bool[allCount];
                _cySpringDynamicsTailBoneNum = targetCount;
            }

            if (_cySpringDynamicsTailBoneNum < 1)
                return;

            if (setDynamicsRatioAction == null)
            {
                setDynamicsRatioAction = SetCySpringDynamicsRatioAction;
            }

            if (_cySpringDynamicsRatioTailAnimationTarget == null)
                throw new NullReferenceException();

            float startRatio = _cySpringTailDynamicsRatio;

            for (int i = 0; i < _cySpringDynamicsRatioTailAnimationTarget.Length; i++)
            {
                CySpringBoneBase bone = _cySpringDynamicsTailBoneAll[i];
                if (bone == null)
                    throw new NullReferenceException();

                _cySpringDynamicsRatioTailAnimationTarget[i] =
                    !Gallop.Math.IsFloatEqualLight(bone.DynamicsRatio, targetRatio);
            }

            _cySpringTailDynamicsRatio = targetRatio;

            if (_stopBlendTweenDynamicsRatioTail)
            {
                IsDurationDynamicsRatioTail = false;
                return;
            }

            IsDurationDynamicsRatioTail = true;

            DOTween.To(
                ratio =>
                {
                    setDynamicsRatioAction(
                        ratio,
                        this,
                        _cySpringDynamicsTailTotalBoneNum
                    );
                },
                startRatio,
                targetRatio,
                duration
            )
            .OnComplete(() =>
            {
                IsDurationDynamicsRatioTail = false;
            });
        }

        private static bool StringArrayContains(string[] array, string value)
        {
            if (array == null)
                throw new NullReferenceException();

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == value)
                    return true;
            }

            return false;
        }

        private static void CallOfficialFindSpring(
            CySpringController controller,
            List<CySpringBoneBase> result)
        {
            if (controller == null)
                throw new NullReferenceException();

            Type controllerType = controller.GetType();

            MethodInfo[] methods = controllerType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            MethodInfo targetMethod = null;

            foreach (MethodInfo method in methods)
            {
                if (method.Name != "FindSpring")
                    continue;

                ParameterInfo[] ps = method.GetParameters();

                if (ps.Length != 5)
                    continue;

                if (!ps[0].ParameterType.IsAssignableFrom(result.GetType()))
                    continue;

                if (!typeof(Delegate).IsAssignableFrom(ps[1].ParameterType))
                    continue;

                targetMethod = method;
                break;
            }

            if (targetMethod == null)
            {
                throw new MissingMethodException(
                    controllerType.FullName,
                    "FindSpring(List<CySpringBoneBase>, delegate, 2, true, false)"
                );
            }

            ParameterInfo[] p = targetMethod.GetParameters();

            MethodInfo predicateMethod = typeof(CySpringDynamicsRatioTailSetter)
                .GetMethod(
                    nameof(IsCySpringDynamicRatioTarget),
                    BindingFlags.Public | BindingFlags.Static
                );

            Delegate predicateDelegate =
                Delegate.CreateDelegate(p[1].ParameterType, predicateMethod);

            object springTypeArg;

            if (p[2].ParameterType.IsEnum)
            {
                springTypeArg = Enum.ToObject(p[2].ParameterType, 2);
            }
            else
            {
                springTypeArg = Convert.ChangeType(2, p[2].ParameterType);
            }

            targetMethod.Invoke(
                controller,
                new object[]
                {
                    result,
                    predicateDelegate,
                    springTypeArg,
                    true,
                    false
                }
            );
        }

        private static string GetCySpringBoneName(CySpringBoneBase bone)
        {
            if (bone == null)
                throw new NullReferenceException();

            Type type = bone.GetType();

            string[] candidateNames =
            {
                "Name",
                "name",
                "BoneName",
                "boneName",
                "_name",
                "_boneName",
                "springBoneName",
                "_springBoneName"
            };

            Type t = type;
            while (t != null)
            {
                for (int i = 0; i < candidateNames.Length; i++)
                {
                    FieldInfo field = t.GetField(
                        candidateNames[i],
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

                    if (field != null && field.FieldType == typeof(string))
                    {
                        return (string)field.GetValue(bone);
                    }

                    PropertyInfo property = t.GetProperty(
                        candidateNames[i],
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

                    if (property != null &&
                        property.PropertyType == typeof(string) &&
                        property.GetIndexParameters().Length == 0)
                    {
                        return (string)property.GetValue(bone, null);
                    }
                }

                t = t.BaseType;
            }

            // 官方伪代码里是 Item + 0x10 的 string
            // 如果当前项目的 CySpringBoneBase 字段名不在上面，就在这里改成官方字段。
            throw new MissingFieldException(
                type.FullName,
                "CySpringBoneBase name string at offset 0x10"
            );
        }
    }
}