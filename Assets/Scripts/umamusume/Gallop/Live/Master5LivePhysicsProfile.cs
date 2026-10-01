using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>Physics-only defaults from master5's SkirtCollisionRuntimeTuner.
    /// Keep this separate from its rendering controls and apply only to Live characters.</summary>
    [DisallowMultipleComponent]
    public sealed class Master5LivePhysicsProfile : MonoBehaviour
    {
        [Serializable]
        private sealed class ColliderBaseline
        {
            public DynamicBoneCollider Collider;
            public float Radius;
        }

        // Persist original radii across editor domain reloads: never multiply a tuned radius again.
        [SerializeField] private List<ColliderBaseline> colliderBaselines = new List<ColliderBaseline>();
        private readonly HashSet<DynamicBone.Particle> knownParticles = new HashSet<DynamicBone.Particle>();
        private float nextDiscoveryTime;
        private readonly List<DynamicBone> discoveredBones = new List<DynamicBone>();
        private readonly List<DynamicBoneCollider> discoveredColliders = new List<DynamicBoneCollider>();

        public int TunedColliderCount => colliderBaselines.Count;

        private void Update()
        {
            if (Time.realtimeSinceStartup < nextDiscoveryTime) return;
            nextDiscoveryTime = Time.realtimeSinceStartup + 0.5f;
            ApplyToNewPhysics();
        }

        public void ApplyToNewPhysics()
        {
            var character = GetComponent<UmaContainerCharacter>();
            if (character == null || !character.IsLive || character.IsMini) return;

            GetComponentsInChildren(true, discoveredBones);
            foreach (var bone in discoveredBones)
            {
                bool skirt = false;
                bool classified = false;
                foreach (var particle in bone.Particles)
                {
                    if (!knownParticles.Add(particle)) continue;
                    if (!classified) { skirt = IsSkirt(bone); classified = true; }
                    // master5 applies the Other multipliers on top of the common ones.
                    particle.m_Damping = Mathf.Clamp01(particle.m_Damping * (skirt ? 3f : 9f));
                    particle.m_Elasticity = Mathf.Clamp01(particle.m_Elasticity * (skirt ? 1.5f : 2.25f));
                    particle.m_Stiffness = Mathf.Clamp01(particle.m_Stiffness * (skirt ? 2f : 4f));
                    particle.m_Inert = Mathf.Clamp01(particle.m_Inert * (skirt ? 1.1f : 1.1f * 1.1f));
                    if (!skirt)
                    {
                        particle.m_Damping = Mathf.Max(particle.m_Damping, Mathf.Lerp(0f, 0.9f, 0.85f));
                        particle.m_Stiffness = Mathf.Max(particle.m_Stiffness, Mathf.Lerp(0f, 0.98f, 0.85f));
                        particle.m_Elasticity = Mathf.Min(particle.m_Elasticity, Mathf.Lerp(1f, 0.05f, 0.85f));
                        particle.m_Inert = Mathf.Min(particle.m_Inert, Mathf.Lerp(1f, 0f, 0.85f));
                    }
                }
            }

            colliderBaselines.RemoveAll(entry => entry.Collider == null);
            GetComponentsInChildren(true, discoveredColliders);
            foreach (var collider in discoveredColliders)
            {
                float scale = RadiusScale(collider.ColliderName);
                if (scale == 1f) continue;
                ColliderBaseline baseline = null;
                foreach (var entry in colliderBaselines)
                    if (entry.Collider == collider) { baseline = entry; break; }
                if (baseline == null)
                {
                    baseline = new ColliderBaseline { Collider = collider, Radius = collider.m_Radius };
                    colliderBaselines.Add(baseline);
                }
                collider.m_Radius = baseline.Radius * scale;
                // master5 changes radius only; capsule height/center remain authored values.
            }

            var solver = GetComponent<SkirtSurfaceCollisionSolver>();
            if (solver != null) solver.HorizontalStiffness = 0.5f;
        }

        public static float RadiusScale(string name)
        {
            if (string.IsNullOrEmpty(name)) return 1f;
            if (name.IndexOf("Hip", StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.IndexOf("Skirt", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (name.EndsWith("_L", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith("_R", StringComparison.OrdinalIgnoreCase))) return 0.2f;
            if (name.IndexOf("Thigh", StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.IndexOf("MSkirt", StringComparison.OrdinalIgnoreCase) >= 0) return 0.8f;
            return 1f;
        }

        private static bool IsSkirt(DynamicBone bone)
        {
            string name = bone.m_Root != null ? bone.m_Root.name : string.Empty;
            for (Transform parent = bone.transform.parent; parent != null; parent = parent.parent)
                name += "/" + parent.name;
            return bone.m_ContinuousCollision || name.IndexOf("Skirt", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
