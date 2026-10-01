#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PhysicsCollisionCacheTests
{
    [MenuItem("UmaViewer/Physics/Toggle collision geometry cache")]
    public static void ToggleCache()
    {
        DynamicBoneCollider.UseGeometryCache = !DynamicBoneCollider.UseGeometryCache;
        Debug.Log("[PhysicsPerformance] Collision geometry cache: " + DynamicBoneCollider.UseGeometryCache);
    }

    [MenuItem("UmaViewer/Physics/Test collision cache equivalence")]
    public static void Run()
    {
        bool previous = DynamicBoneCollider.UseGeometryCache;
        var root = new GameObject("CollisionCacheTest") { hideFlags = HideFlags.HideAndDontSave };
        int checks = 0;
        try
        {
            var collider = root.AddComponent<DynamicBoneCollider>();
            var random = new System.Random(1709);
            var samples = new Vector3[512];
            for (int i = 0; i < samples.Length; ++i)
                samples[i] = new Vector3((float)random.NextDouble() * 4 - 2,
                    (float)random.NextDouble() * 4 - 2, (float)random.NextDouble() * 4 - 2);
            for (int shape = 0; shape < 2; ++shape)
            for (int bound = 0; bound < 2; ++bound)
            for (int axis = 0; axis < 3; ++axis)
            for (int pose = 0; pose < 4; ++pose)
            {
                root.transform.position = new Vector3(pose * 0.1f, -0.2f, 0.3f);
                root.transform.rotation = Quaternion.Euler(pose * 23, pose * 11, -17);
                root.transform.localScale = new Vector3(pose == 3 ? -1.2f : 1.2f, 0.7f + pose * 0.1f, 1.5f);
                collider.m_Radius = 0.4f + pose * 0.02f;
                collider.m_Height = shape == 0 ? 0 : 2;
                collider.m_Center = new Vector3(0.1f, -0.15f, 0.2f);
                collider.m_Direction = (DynamicBoneColliderBase.Direction)axis;
                collider.m_Bound = (DynamicBoneColliderBase.Bound)bound;
                using (DynamicBoneCollider.BeginCollisionBatch())
                {
                    foreach (Vector3 sample in samples)
                    {
                        Check(collider, sample, 0.015f);
                        ++checks;
                    }
                    using (DynamicBoneCollider.BeginCollisionBatch())
                        Check(collider, samples[0], 0.015f);
                    Check(collider, samples[1], 0.015f);
                }
                // No batch: a moved collider must be read immediately, not from the last cache.
                root.transform.position += Vector3.up * 0.2f;
                Check(collider, samples[2], 0.03f);
            }
            collider.m_Bound = DynamicBoneColliderBase.Bound.Outside;
            collider.m_Height = 2;
            const int rounds = 1024;
            // Warm both paths and use two orders to reduce ordering/JIT bias.
            Measure(collider, samples, 8, false);
            Measure(collider, samples, 8, true);
            double uncached = Measure(collider, samples, rounds, false);
            double cached = Measure(collider, samples, rounds, true);
            cached += Measure(collider, samples, rounds, true);
            uncached += Measure(collider, samples, rounds, false);
            string report = $"[PhysicsPerformance] PASS: {checks} point comparisons + nested scopes and moved colliders. " +
                $"Synthetic {rounds * samples.Length * 2} queries/path: uncached={uncached:F2}ms, cached={cached:F2}ms, " +
                $"ratio={uncached / Math.Max(cached, 0.001):F2}x. This is NOT a Live frame-time benchmark.";
            Debug.Log(report);
            string output = Environment.GetEnvironmentVariable("UMA_PHYSICS_TEST_REPORT");
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(output, report);
        }
        finally
        {
            DynamicBoneCollider.UseGeometryCache = previous;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Check(DynamicBoneCollider collider, Vector3 input, float radius)
    {
        Vector3 expected = input, actual = input;
        DynamicBoneCollider.UseGeometryCache = false;
        bool expectedHit = collider.Collide(ref expected, radius);
        DynamicBoneCollider.UseGeometryCache = true;
        bool actualHit = collider.Collide(ref actual, radius);
        if (expectedHit != actualHit || (actual - expected).sqrMagnitude > 1e-12f ||
            float.IsNaN(actual.x) || float.IsNaN(actual.y) || float.IsNaN(actual.z))
            throw new Exception($"Collision cache mismatch: expected {expectedHit}/{expected}, actual {actualHit}/{actual}");
    }

    private static double Measure(DynamicBoneCollider collider, Vector3[] samples, int rounds, bool cached)
    {
        DynamicBoneCollider.UseGeometryCache = cached;
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < rounds; ++i)
            using (DynamicBoneCollider.BeginCollisionBatch())
                foreach (var sample in samples)
                {
                    Vector3 point = sample;
                    collider.Collide(ref point, 0.015f);
                }
        timer.Stop();
        return timer.Elapsed.TotalMilliseconds;
    }
}
#endif
