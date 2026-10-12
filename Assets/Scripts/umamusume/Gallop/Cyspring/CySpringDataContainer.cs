using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    [Serializable]
    public class CySpringDataContainer : MonoBehaviour
    {
        public const int CATEGORY_NUM = 5;

        // Largest radius a skirt leg (thigh) collider may keep when it becomes a DynamicBone
        // capsule. The authored value is cloth clearance (0.085 on the school uniform), not
        // the leg surface: DynamicBone pushes every panel that far from the leg axis and the
        // hem ends up 2-4 cm wider than the game. The 160-scale leg measures ~0.05 and each
        // panel adds its own particle radius (~0.028), so clamping here keeps the cloth just
        // off the leg instead of holding it away.
        private const float MaxSkirtLegColliderRadius = 0.04f;

        // The thigh skirt volume is built as a stack of spheres along the thigh bone instead
        // of the single authored capsule, so the cloth is held off the whole thigh rather than
        // only near the hip. Every value is a fraction of the thigh->knee segment, so it scales
        // with the character height (the Position BodyScale) and with each costume's leg length,
        // with no per-character data.
        private const int SkirtThighSphereCount = 6;
        private const float SkirtThighSphereTopFraction = 0.05f;
        private const float SkirtThighSphereBottomFraction = 0.98f;
        private const float SkirtThighSphereTopRadiusFraction = 0.148f;
        private const float SkirtThighSphereBottomRadiusFraction = 0.113f;

        public List<CySpringCollisionData> collisionParam = new List<CySpringCollisionData>();
        public List<CySpringParamDataElement> springParam = new List<CySpringParamDataElement>();
        public List<ConnectedBoneData> ConnectedBoneList = new List<ConnectedBoneData>();

        public bool enableVerticalWind;
        public bool enableHorizontalWind;

        public float centerWindAngleSlow;
        public float centerWindAngleFast;

        public float verticalCycleSlow = 1f;
        public float horizontalCycleSlow = 1f;

        public float verticalAngleWidthSlow;
        public float horizontalAngleWidthSlow;

        public float verticalCycleFast = 1f;
        public float horizontalCycleFast = 1f;

        public float verticalAngleWidthFast;
        public float horizontalAngleWidthFast;

        public bool IsEnableHipMoveParam;
        public float HipMoveInfluenceDistance;
        public float HipMoveInfluenceMaxDistance;

        public bool UseCorrectScaleCalc;

        public List<DynamicBone> DynamicBones = new List<DynamicBone>();

        public Dictionary<string, Transform> InitiallizeCollider(Dictionary<string, Transform> bones)
        {
            var colliders = new Dictionary<string, Transform>();
            foreach (CySpringCollisionData collider in collisionParam)
            {
                if (collider._isInner) continue;
                if (IsSkirtLegOrHipCollider(collider._collisionName) &&
                    !IsActiveSkirtBodyCollider(collider._collisionName))
                    continue;
                Transform child;
                Transform bone = null;
                bool authoredTransform = bones.TryGetValue(collider._collisionName, out child);
                if (!authoredTransform && !bones.TryGetValue(collider._targetObjectName, out bone))
                    continue;

                // Build the thigh skirt volume as a stack of spheres along the thigh instead
                // of a single capsule, so the cloth is held off the whole thigh.
                if (IsThighSkirtCollider(collider._collisionName))
                {
                    Transform thighBone = authoredTransform ? child.parent : bone;
                    Transform kneeBone = thighBone != null ? FindKneeBone(bones, thighBone.name) : null;
                    if (thighBone != null && kneeBone != null)
                    {
                        BuildThighSkirtSphereStack(colliders, thighBone, kneeBone, collider._collisionName);
                        continue;
                    }
                }

                if (!authoredTransform)
                {
                    child = new GameObject(collider._collisionName).transform;
                    child.transform.SetParent(bone);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                }
                if (!colliders.ContainsKey(child.name))
                    colliders.Add(child.name, child);

                    //修改(动骨与碰撞相关)
                    if (collider._collisionName == "Col_B_Hip_Tail")
                    {
                        collider._radius *= 0.96f;
                    }
                    else if (collider._collisionName == "Col_B_Chest_Tail")
                    {
                        collider._radius *= 1.14f;
                    }
                    else if (collider._collisionName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Skirt collision volumes are authored for the skirt and must stay at source size.
                    }
                    else if (collider._collisionName.Contains("Col_B_Hip_Jacket"))
                    {
                        collider._radius *= 1f;
                    }
                    else if (collider._collisionName == "Col_Elbow_R_Hair")
                    {
                        collider._radius *= 0.3f;
                    }
                    else if (collider._collisionName == "Col_Elbow_L_Hair")
                    {
                        collider._radius *= 0.3f;
                    }
                    else
                    {
                        collider._radius *= 0.9f;
                    }



                    switch (collider._type)
                    {
                        case CySpringCollisionData.CollisionType.Capsule:
                            var dynamic = child.gameObject.AddComponent<DynamicBoneCollider>();
                            dynamic.ColliderName = collider._collisionName;
                            Vector3 capsuleAxis = collider._offset2 - collider._offset;
                            if (!authoredTransform)
                            {
                                child.transform.localPosition = (collider._offset + collider._offset2) / 2;
                                if (capsuleAxis.sqrMagnitude > 0.000001f)
                                    child.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, capsuleAxis);
                            }
                            dynamic.m_Direction = authoredTransform
                                ? DynamicBoneColliderBase.Direction.Y
                                : DynamicBoneColliderBase.Direction.Z;
                            float capsuleRadius = IsThighSkirtCollider(collider._collisionName)
                                ? Mathf.Min(collider._radius, MaxSkirtLegColliderRadius)
                                : collider._radius;
                            // DynamicBone stores full capsule height, including both hemispheres.
                            dynamic.m_Height = capsuleAxis.magnitude + capsuleRadius * 2f;
                            dynamic.m_Radius = capsuleRadius;
                            dynamic.m_Bound = collider._isInner ? DynamicBoneColliderBase.Bound.Inside : DynamicBoneColliderBase.Bound.Outside;
                            break;
                        case CySpringCollisionData.CollisionType.Sphere:
                            var Spheredynamic = child.gameObject.AddComponent<DynamicBoneCollider>();
                            Spheredynamic.ColliderName = collider._collisionName;
                            if (!authoredTransform)
                                child.transform.localPosition = collider._offset;
                            Spheredynamic.m_Radius = collider._radius;
                            // A CySpring sphere's distance is not DynamicBone capsule height.
                            // Giving it a height turns the authored hip sphere into a long capsule.
                            Spheredynamic.m_Height = 0f;
                            Spheredynamic.m_Bound = collider._isInner ? DynamicBoneColliderBase.Bound.Inside : DynamicBoneColliderBase.Bound.Outside;
                            break;
                        case CySpringCollisionData.CollisionType.Plane:
                            var planedynamic = child.gameObject.AddComponent<DynamicBonePlaneCollider>();
                            planedynamic.ColliderName = collider._collisionName;
                            if (!authoredTransform)
                                child.transform.localPosition = collider._offset;
                            planedynamic.m_Bound = collider._isInner ? DynamicBoneColliderBase.Bound.Inside : DynamicBoneColliderBase.Bound.Outside;
                            break;
                        case CySpringCollisionData.CollisionType.None:
                            break;
                    }
            }
            return colliders;
        }



        //修改(动骨与碰撞相关)
        public void InitializePhysics(Dictionary<string, Transform> bones, Dictionary<string, Transform> colliders)
        {
            DynamicBones.Clear();
            var skirtColliders = new List<DynamicBoneColliderBase>();
            foreach (Transform transform in colliders.Values)
            {
                DynamicBoneColliderBase collider = transform.GetComponent<DynamicBoneColliderBase>();
                if (collider != null && IsSharedThighSkirtCollider(collider.ColliderName))
                {
                    skirtColliders.Add(collider);
                }
            }
        
            // 获取对象名称并判断类型
            string nameLower = gameObject.name.ToLower();
            bool isTailObject = nameLower.Contains("tail"); // 判断是否为尾巴
            bool isClothObject = !isTailObject && nameLower.Contains("chr"); // 判断是否为头发，排除尾巴避免重复匹配
            bool isChestBowObject = !isTailObject &&
                (nameLower.Contains("ribbon") || nameLower.Contains("bow") ||
                 nameLower.Contains("tie") || nameLower.Contains("bust"));
        
            foreach (CySpringParamDataElement spring in springParam)
            {
                if (!bones.TryGetValue(spring._boneName, out Transform bone)) continue;
        
                var dynamic = bone.gameObject.AddComponent<DynamicBone>();
                dynamic.m_Root = bone;
                dynamic.m_MotionGroup = isChestBowObject ? "ChestBow" :
                    (isClothObject ? "Hair" : string.Empty);
        
                // 设置重力
                dynamic.m_Gravity = new Vector3(0, Mathf.Clamp01(-30f / spring._gravity), 0);
                dynamic.m_LimitAngel_Min = spring._limitAngleMin;
                dynamic.m_LimitAngel_Max = spring._limitAngleMax;
        
                // 主参数设置
                if (isTailObject)
                {
                    // 尾巴主骨骼
                    dynamic.m_Damping = 0.05f;
                    dynamic.m_Stiffness = Mathf.Clamp01(20f / spring._stiffnessForce);
                    dynamic.m_Elasticity = Mathf.Clamp01(20f / spring._dragForce);
                    dynamic.m_Radius = spring._collisionRadius * 0.9f;
                    dynamic.m_Inert = spring.MoveSpringApplyRate / 4f;
                }
                else if (isClothObject)
                {
                    // 头发主骨骼
                    dynamic.m_Damping = 0.1f;
                    dynamic.m_Stiffness = Mathf.Clamp01(15f / spring._stiffnessForce);
                    dynamic.m_Elasticity = Mathf.Clamp01(20f / spring._dragForce);
                    dynamic.m_Radius = spring._collisionRadius * 0.9f;
                    dynamic.m_Inert = spring.MoveSpringApplyRate / 2.5f;
                }
                else
                {
                    // 衣服主骨骼
                    dynamic.m_Damping = 0.1f;
                    dynamic.m_Stiffness = Mathf.Clamp01(20f / spring._stiffnessForce);
                    dynamic.m_Elasticity = Mathf.Clamp01(30f / spring._dragForce);
                    dynamic.m_Radius = spring._collisionRadius * 0.8f;
                    dynamic.m_Inert = spring.MoveSpringApplyRate / 3f;
                }
        
                dynamic.SetupParticles();
                DynamicBones.Add(dynamic);

                bool isSkirtChain = spring._boneName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isSkirtChain)
                {
                    dynamic.m_ContinuousCollision = true;
                }
        
                // The source list belongs to the root spring node. DynamicBone particle zero
                // is fixed, so apply it to the first simulated particle for skirt chains.
                foreach (string collisionName in spring._collisionNameList)
                {
                    if (!colliders.TryGetValue(collisionName, out Transform tmp))
                        continue;

                    DynamicBoneColliderBase collider = tmp.GetComponent<DynamicBoneColliderBase>();
                    if (isSkirtChain && !ShouldUseAuthoredSkirtCollider(collider))
                        continue;
                    int particleIndex = isSkirtChain && dynamic.Particles.Count > 1 ? 1 : 0;
                    AddCollider(dynamic.Particles[particleIndex], collider);
                }

                if (isSkirtChain)
                {
                    // Leg capsules only. Hip spheres are dropped here (see
                    // ShouldUseAuthoredSkirtCollider): a DynamicBone chain can only be
                    // pushed radially out of a collider, so a hip volume inflates the
                    // whole skirt instead of tucking it around the pelvis.
                    foreach (DynamicBoneColliderBase collider in skirtColliders)
                        AddColliderToSkirtParticles(dynamic, collider);
                }
        
                // 子参数设置
                foreach (var child in spring._childElements)
                {
                    var tempParticle = dynamic.Particles.Find(p => p.m_Transform.gameObject.name == child._boneName);
                    if (tempParticle == null) continue;
        
                    if (isTailObject)
                    {
                        // 尾巴子粒子
                        tempParticle.m_Damping = 0.05f;
                        tempParticle.m_Stiffness = Mathf.Clamp01(20f / child._stiffnessForce);
                        tempParticle.m_Elasticity = Mathf.Clamp01(20f / child._dragForce);
                        tempParticle.m_Radius = child._collisionRadius * 1f;
                        tempParticle.m_Inert = child.MoveSpringApplyRate / 4f;
                    }
                    else if (isClothObject)
                    {
                        // 头发子粒子
                        tempParticle.m_Damping = 0.1f;
                        tempParticle.m_Stiffness = Mathf.Clamp01(15f / child._stiffnessForce);
                        tempParticle.m_Elasticity = Mathf.Clamp01(20f / child._dragForce);
                        tempParticle.m_Radius = child._collisionRadius * 1f;
                        tempParticle.m_Inert = child.MoveSpringApplyRate / 2.5f;
                    }
                    else
                    {
                        // 衣服子粒子
                        tempParticle.m_Damping = 0.1f;
                        tempParticle.m_Stiffness = Mathf.Clamp01(20f / child._stiffnessForce);
                        tempParticle.m_Elasticity = Mathf.Clamp01(30f / child._dragForce);
                        tempParticle.m_Radius = child._collisionRadius * 0.8f;
                        tempParticle.m_Inert = child.MoveSpringApplyRate / 2f;
                    }
        
                    tempParticle.m_LimitAngel_Min = child._limitAngleMin;
                    tempParticle.m_LimitAngel_Max = child._limitAngleMax;
        
                    // 子碰撞器添加
                    foreach (string collisionName in child._collisionNameList)
                    {
                        if (colliders.TryGetValue(collisionName, out Transform tmp))
                        {
                            DynamicBoneColliderBase collider = tmp.GetComponent<DynamicBoneColliderBase>();
                            if (!isSkirtChain || ShouldUseAuthoredSkirtCollider(collider))
                                AddCollider(tempParticle, collider);
                        }
                    }
                }
            }
        }

        private static void AddColliderToSkirtParticles(DynamicBone dynamic, DynamicBoneColliderBase collider)
        {
            // Particle zero is the fixed skirt root and is not simulated.
            for (int i = 1; i < dynamic.Particles.Count; i++)
                AddCollider(dynamic.Particles[i], collider);
        }

        private static void AddCollider(DynamicBone.Particle particle, DynamicBoneColliderBase collider)
        {
            if (particle != null && collider != null && !particle.m_Colliders.Contains(collider))
                particle.m_Colliders.Add(collider);
        }

        private static bool IsThighSkirtCollider(string colliderName)
        {
            return !string.IsNullOrEmpty(colliderName) &&
                colliderName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                colliderName.IndexOf("Thigh", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Transform FindKneeBone(Dictionary<string, Transform> bones, string thighName)
        {
            if (string.IsNullOrEmpty(thighName) ||
                thighName.IndexOf("Thigh", StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            Transform knee;
            return bones.TryGetValue(thighName.Replace("Thigh", "Knee"), out knee) ? knee : null;
        }

        // Places SkirtThighSphereCount sphere colliders evenly along thigh->knee, each with a
        // radius proportional to the thigh length, and registers them as skirt thigh colliders
        // so InitializePhysics (and the surface solver) pick them up.
        private static void BuildThighSkirtSphereStack(
            Dictionary<string, Transform> colliders, Transform thigh, Transform knee, string baseName)
        {
            Vector3 kneeLocal = thigh.InverseTransformPoint(knee.position);
            float length = kneeLocal.magnitude;
            if (length <= 0.000001f)
                return;

            if (colliders.ContainsKey(baseName + "_Sphere00"))
                return;

            for (int i = 0; i < SkirtThighSphereCount; ++i)
            {
                float a = SkirtThighSphereCount > 1 ? (float)i / (SkirtThighSphereCount - 1) : 0f;
                float t = Mathf.Lerp(SkirtThighSphereTopFraction, SkirtThighSphereBottomFraction, a);

                string name = baseName + "_Sphere" + i.ToString("00");
                GameObject go = new GameObject(name);
                go.transform.SetParent(thigh, false);
                go.transform.localPosition = Vector3.Lerp(Vector3.zero, kneeLocal, t);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                DynamicBoneCollider sphere = go.AddComponent<DynamicBoneCollider>();
                sphere.ColliderName = name;
                sphere.m_Height = 0f;
                sphere.m_Radius = Mathf.Lerp(SkirtThighSphereTopRadiusFraction, SkirtThighSphereBottomRadiusFraction, a) * length;
                sphere.m_Bound = DynamicBoneColliderBase.Bound.Outside;

                if (!colliders.ContainsKey(name))
                    colliders.Add(name, go.transform);
            }
        }

        private static bool IsSkirtLegOrHipCollider(string colliderName)
        {
            return !string.IsNullOrEmpty(colliderName) &&
                colliderName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                (colliderName.IndexOf("Thigh", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 colliderName.IndexOf("Hip", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsActiveSkirtBodyCollider(string colliderName)
        {
            return string.Equals(colliderName, "Col_B_Thigh_L_MSkirt_L", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(colliderName, "Col_B_Thigh_R_MSkirt_R", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(colliderName, "Col_B_Hip_MSkirt_L", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(colliderName, "Col_B_Hip_MSkirt_R", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSharedThighSkirtCollider(string colliderName)
        {
            return !string.IsNullOrEmpty(colliderName) &&
                colliderName.IndexOf("Thigh", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                colliderName.IndexOf("MSkirt", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldUseAuthoredSkirtCollider(DynamicBoneColliderBase collider)
        {
            if (collider == null || string.IsNullOrEmpty(collider.ColliderName))
                return false;

            if (IsThighSkirtCollider(collider.ColliderName))
                return false;

            // A DynamicBone chain can only be pushed radially *out* of a collider, and
            // every panel of the skirt hangs at a different distance from the pelvis.
            // A hip sphere (authored 0.2-0.4, centred on the hip) therefore inflates the
            // whole chain until the hem sits on the sphere surface: the skirt opens like
            // a lampshade and lifts. The original CySpring solver used those volumes as
            // an angular correction instead, which DynamicBone cannot express, so keep
            // only the leg capsules that stop the legs poking through the cloth. The
            // skirt root is skinned to the hip bone and follows it rigidly, so no hip
            // volume is needed to keep the cloth off the pelvis.
            return !IsHipSkirtVolume(collider.ColliderName);
        }

        private static bool IsHipSkirtVolume(string colliderName)
        {
            return !string.IsNullOrEmpty(colliderName) &&
                colliderName.IndexOf("Hip", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                colliderName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void EnablePhysics(bool isOn)
        {
            foreach(DynamicBone dynamic in DynamicBones)
            {
                dynamic.enabled = isOn;
            }
        }

        public void ResetPhysics()
        {
            foreach (DynamicBone dynamic in DynamicBones)
            {
                dynamic.ResetParticlesPosition();
            }
        }

        public void SetWindParam(CySpringParamDataAsset asset)
        {
            if (asset == null)
                return;

            enableVerticalWind = asset.EnableVerticalWind;
            enableHorizontalWind = asset.EnableHorizontalWind;

            centerWindAngleSlow = asset.CenterWindAngleSlow;
            centerWindAngleFast = asset.CenterWindAngleFast;

            verticalCycleSlow = asset.VerticalCycleSlow;
            horizontalCycleSlow = asset.HorizontalCycleSlow;

            verticalAngleWidthSlow = asset.VerticalAngleWidthSlow;
            horizontalAngleWidthSlow = asset.HorizontalAngleWidthSlow;

            verticalCycleFast = asset.VerticalCycleFast;
            horizontalCycleFast = asset.HorizontalCycleFast;

            verticalAngleWidthFast = asset.VerticalAngleWidthFast;
            horizontalAngleWidthFast = asset.HorizontalAngleWidthFast;
        }

        public void SetMoveSpringParam(CySpringParamDataAsset asset)
        {
            if (asset == null)
                return;

            IsEnableHipMoveParam = asset.IsEnableHipMoveParam;
            HipMoveInfluenceDistance = asset.HipMoveInfluenceDistance;
            HipMoveInfluenceMaxDistance = asset.HipMoveInfluenceMaxDistance;
            UseCorrectScaleCalc = asset.UseCorrectScaleCalc;
        }

        public void SetConnectedBonePara(CySpringParamDataAsset asset)
        {
            if (asset == null || asset.ConnectedBoneList == null)
            {
                ConnectedBoneList = new List<ConnectedBoneData>();
                return;
            }

            ConnectedBoneList = new List<ConnectedBoneData>(asset.ConnectedBoneList);
        }

        public CySpringDataContainer()
        {
            collisionParam = new List<CySpringCollisionData>();
            springParam = new List<CySpringParamDataElement>();
            ConnectedBoneList = new List<ConnectedBoneData>();

            verticalCycleSlow = 1f;
            horizontalCycleSlow = 1f;
            verticalCycleFast = 1f;
            horizontalCycleFast = 1f;
        }

        private void OnValidate()
        {
            if (collisionParam == null)
                collisionParam = new List<CySpringCollisionData>();

            if (springParam == null)
                springParam = new List<CySpringParamDataElement>();

            if (ConnectedBoneList == null)
                ConnectedBoneList = new List<ConnectedBoneData>();

            if (verticalCycleSlow <= 0f)
                verticalCycleSlow = 1f;

            if (horizontalCycleSlow <= 0f)
                horizontalCycleSlow = 1f;

            if (verticalCycleFast <= 0f)
                verticalCycleFast = 1f;

            if (horizontalCycleFast <= 0f)
                horizontalCycleFast = 1f;
        }

        public enum Category
        {
            Invalid = -1,
            Live = 0,
            Race = 1,
            Story = 2,
            Home = 3,
            Training = 4
        }
    }
}