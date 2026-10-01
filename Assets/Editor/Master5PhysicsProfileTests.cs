#if UNITY_EDITOR
using System;
using Gallop.Live;
using UnityEditor;
using UnityEngine;

public static class Master5PhysicsProfileTests
{
    [MenuItem("UmaViewer/Physics/Test master5 Live profile")]
    public static void Run()
    {
        GameObject root = new GameObject("Master5PhysicsProfileTest");
        root.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            CheckScale(null, 1f);
            CheckScale("Hip_Skirt_L", 0.2f);
            CheckScale("hip_mskirt_r", 0.2f);
            CheckScale("Hip_Skirt", 1f);
            CheckScale("Hip_Tail_L", 1f);
            CheckScale("Thigh_MSkirt_L", 0.8f);
            CheckScale("Thigh_Skirt_L", 1f);
            var character = root.AddComponent<UmaContainerCharacter>();
            character.IsLive = true;
            var hip = root.AddComponent<DynamicBoneCollider>();
            hip.ColliderName = "Hip_Skirt_L";
            hip.m_Radius = 0.5f;
            hip.m_Height = 1.4f;
            var leg = root.AddComponent<DynamicBoneCollider>();
            leg.ColliderName = "Thigh_MSkirt_R";
            leg.m_Radius = 0.1f;
            var other = root.AddComponent<DynamicBoneCollider>();
            other.ColliderName = "Hip_Tail_L";
            other.m_Radius = 0.25f;
            var skirtBone = root.AddComponent<DynamicBone>();
            skirtBone.m_ContinuousCollision = true;
            var skirt = NewParticle();
            skirtBone.Particles.Add(skirt);
            var otherBone = root.AddComponent<DynamicBone>();
            var hair = NewParticle();
            otherBone.Particles.Add(hair);
            var profile = root.AddComponent<Master5LivePhysicsProfile>();
            for (int i = 0; i < 5; ++i) profile.ApplyToNewPhysics();
            Near(hip.m_Radius, 0.1f, "hip radius / repeat application");
            Near(leg.m_Radius, 0.08f, "leg radius / repeat application");
            Near(other.m_Radius, 0.25f, "unrelated collider");
            Near(hip.m_Height, 1.4f, "capsule height preserved");
            Near(skirt.m_Damping, 0.3f, "skirt damping");
            Near(skirt.m_Elasticity, 0.15f, "skirt elasticity");
            Near(skirt.m_Stiffness, 0.2f, "skirt stiffness");
            Near(skirt.m_Inert, 0.11f, "skirt inertia");
            Near(hair.m_Damping, 0.9f, "other damping");
            Near(hair.m_Stiffness, 0.833f, "other rigidity");
            Near(hair.m_Elasticity, 0.1925f, "other rebound cap");
            Near(hair.m_Inert, 0.121f, "other inertia");
            // Newly rebuilt particles must receive the profile, but colliders must not shrink again.
            skirtBone.Particles.Clear();
            var rebuilt = NewParticle();
            skirtBone.Particles.Add(rebuilt);
            profile.ApplyToNewPhysics();
            Near(rebuilt.m_Damping, 0.3f, "rebuilt particles");
            Near(hip.m_Radius, 0.1f, "rebuild preserves radius");
            Debug.Log("[PhysicsMigration] master5 profile regression checks PASSED.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static DynamicBone.Particle NewParticle() => new DynamicBone.Particle
    {
        m_Damping = 0.1f, m_Elasticity = 0.1f, m_Stiffness = 0.1f, m_Inert = 0.1f
    };
    private static void CheckScale(string name, float expected) =>
        Near(Master5LivePhysicsProfile.RadiusScale(name), expected, "filter: " + name);
    private static void Near(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > 0.00001f)
            throw new Exception($"[PhysicsMigration] {label}: expected {expected}, got {actual}");
    }
}
#endif
