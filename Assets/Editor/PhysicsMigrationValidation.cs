#if UNITY_EDITOR
using System;
using System.Linq;
using Gallop;
using Gallop.Live;
using UnityEditor;
using UnityEngine;

/// <summary>Checks loaded characters without changing their pose or stopping playback.</summary>
[InitializeOnLoad]
public static class PhysicsMigrationValidation
{
    static PhysicsMigrationValidation() { EditorApplication.delayCall += ValidateIfPlaying; }
    static void ValidateIfPlaying() { if (EditorApplication.isPlaying) Validate(); }

    [MenuItem("UmaViewer/Physics/Validate master5 migration")]
    public static void Validate()
    {
        var characters = UnityEngine.Object.FindObjectsOfType<UmaContainerCharacter>();
        foreach (var character in characters.Where(c => !c.IsMini))
        {
            var bones = character.GetComponentsInChildren<DynamicBone>(true);
            var surface = character.GetComponent<SkirtSurfaceCollisionSolver>();
            var oldController = character.GetComponent<CySpringController>();
            var oldSkirt = character.GetComponent<SkirtController>();
            string report = $"[PhysicsMigration] character={character.name} dynamicBones={bones.Length} " +
                $"rings={(surface != null ? surface.RingCount : 0)} " +
                $"thighColliders={(surface != null ? surface.ColliderCount : 0)} " +
                $"oldCySpring={(oldController != null)} oldSkirt={(oldSkirt != null)}";
            if (oldController != null || oldSkirt != null || bones.Length == 0)
                Debug.LogWarning(report + " Reload this character/Live after script migration.");
            else Debug.Log(report);
            if (character.IsLive)
            {
                var profile = character.GetComponent<Master5LivePhysicsProfile>();
                if (profile == null)
                    Debug.LogWarning("[PhysicsMigration] Missing master5 Live profile; reload Live to apply radius tuning.");
                else
                    Debug.Log($"[PhysicsMigration] master5 profile colliders={profile.TunedColliderCount} horizontalStiffness={surface?.HorizontalStiffness}");
                foreach (var collider in character.GetComponentsInChildren<DynamicBoneCollider>(true))
                {
                    float scale = Master5LivePhysicsProfile.RadiusScale(collider.ColliderName);
                    if (scale == 1f) continue;
                    Debug.Log($"[PhysicsMigration] {collider.ColliderName}: localRadius={collider.m_Radius:F5}, " +
                        $"height={collider.m_Height:F5}, lossyScale={collider.transform.lossyScale}, expectedRadiusMultiplier={scale:F2}", collider);
                }
            }
        }
        if (characters.Length == 0) Debug.Log("[PhysicsMigration] No loaded characters; load a character to validate runtime binding.");
    }
}
#endif
