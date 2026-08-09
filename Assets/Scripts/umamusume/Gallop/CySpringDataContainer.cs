using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    public class CySpringDataContainer : MonoBehaviour
    {
        public List<CySpringCollisionData> collisionParam;
        public List<CySpringParamDataElement> springParam;
        public List<ConnectedBoneData> ConnectedBoneList;
        public bool enableVerticalWind;
        public bool enableHorizontalWind;
        public float centerWindAngleSlow;
        public float centerWindAngleFast;
        public float verticalCycleSlow;
        public float horizontalCycleSlow;
        public float verticalAngleWidthSlow;
        public float horizontalAngleWidthSlow;
        public float verticalCycleFast;
        public float horizontalCycleFast;
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
                            // DynamicBone stores full capsule height, including both hemispheres.
                            dynamic.m_Height = capsuleAxis.magnitude + collider._radius * 2f;
                            dynamic.m_Radius = collider._radius;
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
                    Transform hipBone;
                    bones.TryGetValue("Hip", out hipBone);
                    foreach (DynamicBoneColliderBase collider in skirtColliders)
                        AddColliderToSkirtParticles(dynamic, collider);

                    foreach (Transform transform in colliders.Values)
                    {
                        DynamicBoneColliderBase collider = transform.GetComponent<DynamicBoneColliderBase>();
                        if (collider == null || !IsHipSkirtCollider(collider.ColliderName) ||
                            !IsSameSkirtSide(dynamic.m_Root, collider, hipBone))
                            continue;
                        AddColliderToSkirtParticles(dynamic, collider);
                    }
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

            bool isHipSkirt = collider.ColliderName.IndexOf(
                "Hip", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                collider.ColliderName.IndexOf(
                    "Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // The original CySpring solver can use broad rear/center hip spheres,
            // but they over-expand an independent DynamicBone skirt chain. This is
            // true for both the old MSkirt names and newer Col_B_Hip_Skirt_B/BLR.
            // Keep only an explicitly left/right localized hip pair.
            return !isHipSkirt || IsHipSkirtCollider(collider.ColliderName);
        }

        private static bool IsHipSkirtCollider(string colliderName)
        {
            return !string.IsNullOrEmpty(colliderName) &&
                colliderName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                colliderName.IndexOf("Hip", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                (colliderName.EndsWith("_L", System.StringComparison.OrdinalIgnoreCase) ||
                 colliderName.EndsWith("_R", System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSameSkirtSide(Transform skirtRoot, DynamicBoneColliderBase collider, Transform hip)
        {
            if (skirtRoot == null || collider == null || hip == null)
                return true;

            bool rootLeft = hip.InverseTransformPoint(skirtRoot.position).x < 0f;
            bool colliderLeft = hip.InverseTransformPoint(collider.transform.position).x < 0f;
            return rootLeft == colliderLeft;
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
    }
}
