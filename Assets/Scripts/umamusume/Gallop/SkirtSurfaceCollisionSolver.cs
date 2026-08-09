using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves collisions across the horizontal spans between adjacent skirt chains.
/// DynamicBone itself only checks each independent vertical chain.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class SkirtSurfaceCollisionSolver : MonoBehaviour
{
    private sealed class HorizontalLink
    {
        public DynamicBone.Particle Left;
        public DynamicBone.Particle Right;
        public float RestLength;
    }

    private readonly List<List<DynamicBone>> _rings = new List<List<DynamicBone>>();
    private readonly List<DynamicBoneColliderBase> _colliders = new List<DynamicBoneColliderBase>();
    private readonly List<HorizontalLink> _horizontalLinks = new List<HorizontalLink>();
    private Transform _hip;

    public int RingCount => _rings.Count;
    public int ColliderCount => _colliders.Count;
    public int LastContactCount { get; private set; }
    public float HorizontalStiffness { get; set; } = 0.25f;

    public void Initialize(DynamicBone[] dynamics, Transform hip, IEnumerable<DynamicBoneColliderBase> colliders)
    {
        _hip = hip;
        _rings.Clear();
        _colliders.Clear();
        _horizontalLinks.Clear();
        foreach (DynamicBoneColliderBase collider in colliders)
        {
            if (IsSurfaceCollider(collider) && !_colliders.Contains(collider))
                _colliders.Add(collider);
        }

        var layers = new Dictionary<string, List<DynamicBone>>(StringComparer.OrdinalIgnoreCase);
        foreach (DynamicBone dynamicBone in dynamics)
        {
            if (dynamicBone == null || !dynamicBone.m_ContinuousCollision ||
                dynamicBone.m_Root == null || dynamicBone.Particles.Count < 2)
                continue;

            string layer = GetSkirtLayer(dynamicBone.m_Root.name);
            if (!layers.TryGetValue(layer, out List<DynamicBone> ring))
            {
                ring = new List<DynamicBone>();
                layers.Add(layer, ring);
            }
            ring.Add(dynamicBone);
        }

        foreach (List<DynamicBone> ring in layers.Values)
        {
            if (ring.Count < 3)
                continue;

            ring.Sort((left, right) => GetAngle(left).CompareTo(GetAngle(right)));
            _rings.Add(ring);
            AddHorizontalLinks(ring);
        }
    }

    private void LateUpdate()
    {
        LastContactCount = 0;
        SolveHorizontalLinks();
        for (int iteration = 0; iteration < 1; ++iteration)
        {
            foreach (List<DynamicBone> ring in _rings)
                LastContactCount += ResolveRing(ring, _colliders);
        }

        foreach (List<DynamicBone> ring in _rings)
        {
            foreach (DynamicBone dynamicBone in ring)
                dynamicBone.ApplyParticlesToTransformsNow();
        }
    }

    private void AddHorizontalLinks(List<DynamicBone> ring)
    {
        for (int chainIndex = 0; chainIndex < ring.Count; ++chainIndex)
        {
            DynamicBone leftChain = ring[chainIndex];
            DynamicBone rightChain = ring[(chainIndex + 1) % ring.Count];
            int depthCount = Mathf.Min(leftChain.Particles.Count, rightChain.Particles.Count);
            for (int depth = 1; depth < depthCount; ++depth)
            {
                DynamicBone.Particle left = leftChain.Particles[depth];
                DynamicBone.Particle right = rightChain.Particles[depth];
                _horizontalLinks.Add(new HorizontalLink
                {
                    Left = left,
                    Right = right,
                    RestLength = Vector3.Distance(left.m_Position, right.m_Position)
                });
            }
        }
    }

    private void SolveHorizontalLinks()
    {
        float strength = Mathf.Clamp01(HorizontalStiffness);
        if (strength <= 0f)
            return;

        foreach (HorizontalLink link in _horizontalLinks)
        {
            Vector3 delta = link.Right.m_Position - link.Left.m_Position;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon)
                continue;

            Vector3 correction = delta * ((length - link.RestLength) / length * strength * 0.5f);
            link.Left.m_Position += correction;
            link.Left.m_PrevPosition += correction;
            link.Right.m_Position -= correction;
            link.Right.m_PrevPosition -= correction;
        }
    }

    private static int ResolveRing(List<DynamicBone> ring, List<DynamicBoneColliderBase> colliders)
    {
        int contacts = 0;
        for (int chainIndex = 0; chainIndex < ring.Count; ++chainIndex)
        {
            DynamicBone leftChain = ring[chainIndex];
            DynamicBone rightChain = ring[(chainIndex + 1) % ring.Count];
            int depthCount = Mathf.Min(leftChain.Particles.Count, rightChain.Particles.Count);

            for (int depth = 1; depth < depthCount; ++depth)
            {
                DynamicBone.Particle left = leftChain.Particles[depth];
                DynamicBone.Particle right = rightChain.Particles[depth];
                float objectScale = Mathf.Abs(leftChain.transform.lossyScale.x);
                float particleRadius = Mathf.Max(left.m_Radius, right.m_Radius) * objectScale;
                float maxCorrection = Mathf.Max(
                    Vector3.Distance(leftChain.Particles[depth - 1].m_Position, left.m_Position),
                    Vector3.Distance(rightChain.Particles[depth - 1].m_Position, right.m_Position)) * 0.75f;
                maxCorrection = Mathf.Max(maxCorrection, 0.005f * objectScale);
                contacts += ResolveSpan(left, right, colliders, particleRadius, maxCorrection);
                contacts += ResolvePanel(
                    leftChain.Particles[depth - 1], rightChain.Particles[depth - 1],
                    left, right, depth > 1, colliders, particleRadius, maxCorrection);
            }
        }
        return contacts;
    }

    private static int ResolvePanel(
        DynamicBone.Particle leftTop,
        DynamicBone.Particle rightTop,
        DynamicBone.Particle leftBottom,
        DynamicBone.Particle rightBottom,
        bool topIsMovable,
        List<DynamicBoneColliderBase> colliders,
        float particleRadius,
        float maxCorrection)
    {
        if (colliders.Count == 0)
            return 0;

        int contacts = 0;

        for (int verticalIndex = 1; verticalIndex <= 2; ++verticalIndex)
        {
            float vertical = verticalIndex / 3f;
            for (int horizontalIndex = 1; horizontalIndex <= 2; ++horizontalIndex)
            {
                float horizontal = horizontalIndex / 3f;
                Vector3 top = Vector3.Lerp(leftTop.m_Position, rightTop.m_Position, horizontal);
                Vector3 bottom = Vector3.Lerp(leftBottom.m_Position, rightBottom.m_Position, horizontal);
                Vector3 sample = Vector3.Lerp(top, bottom, vertical);

                foreach (DynamicBoneColliderBase collider in colliders)
                {
                    if (!IsSurfaceCollider(collider))
                        continue;

                    Vector3 projected = sample;
                    if (!collider.Collide(ref projected, particleRadius))
                        continue;

                    ++contacts;

                    float leftTopWeight = (1f - horizontal) * (1f - vertical);
                    float rightTopWeight = horizontal * (1f - vertical);
                    float leftBottomWeight = (1f - horizontal) * vertical;
                    float rightBottomWeight = horizontal * vertical;
                    float normalization = leftBottomWeight * leftBottomWeight + rightBottomWeight * rightBottomWeight;
                    if (topIsMovable)
                        normalization += leftTopWeight * leftTopWeight + rightTopWeight * rightTopWeight;

                    Vector3 correction = projected - sample;
                    if (topIsMovable)
                    {
                        ApplyCorrection(leftTop, correction * (leftTopWeight / normalization), maxCorrection);
                        ApplyCorrection(rightTop, correction * (rightTopWeight / normalization), maxCorrection);
                    }
                    ApplyCorrection(leftBottom, correction * (leftBottomWeight / normalization), maxCorrection);
                    ApplyCorrection(rightBottom, correction * (rightBottomWeight / normalization), maxCorrection);
                    sample = projected;
                }
            }
        }
        return contacts;
    }

    private static int ResolveSpan(
        DynamicBone.Particle left,
        DynamicBone.Particle right,
        List<DynamicBoneColliderBase> colliders,
        float particleRadius,
        float maxCorrection)
    {
        if (colliders.Count == 0)
            return 0;

        int contacts = 0;

        for (int sampleIndex = 1; sampleIndex <= 1; ++sampleIndex)
        {
            float t = 0.5f;
            Vector3 sample = Vector3.Lerp(left.m_Position, right.m_Position, t);

            foreach (DynamicBoneColliderBase collider in colliders)
            {
                if (!IsSurfaceCollider(collider))
                    continue;

                Vector3 projected = sample;
                if (!collider.Collide(ref projected, particleRadius))
                    continue;

                ++contacts;

                Vector3 correction = projected - sample;
                float leftWeight = 1f - t;
                float rightWeight = t;
                float normalization = leftWeight * leftWeight + rightWeight * rightWeight;
                Vector3 leftCorrection = correction * (leftWeight / normalization);
                Vector3 rightCorrection = correction * (rightWeight / normalization);

                ApplyCorrection(left, leftCorrection, maxCorrection);
                ApplyCorrection(right, rightCorrection, maxCorrection);
                sample = projected;
            }
        }
        return contacts;
    }

    private static bool IsSurfaceCollider(DynamicBoneColliderBase collider)
    {
        return collider != null && collider.enabled &&
            IsBodySkirtCollider(collider);
    }

    private static bool IsBodySkirtCollider(DynamicBoneColliderBase collider)
    {
        if (collider == null || string.IsNullOrEmpty(collider.ColliderName) ||
            collider.ColliderName.IndexOf("Skirt", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        if (collider.ColliderName.IndexOf("Thigh", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        DynamicBoneCollider dynamicCollider = collider as DynamicBoneCollider;
        return collider.ColliderName.IndexOf("Hip", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (dynamicCollider == null || dynamicCollider.m_Radius <= 0.25f);
    }

    private static void ApplyCorrection(DynamicBone.Particle particle, Vector3 correction, float maxCorrection)
    {
        correction = Vector3.ClampMagnitude(correction, maxCorrection);
        particle.m_Position += correction;
        particle.m_PrevPosition += correction;
        particle.m_isCollide = true;
    }

    private float GetAngle(DynamicBone dynamicBone)
    {
        Vector3 center = _hip != null ? _hip.position : transform.position;
        Vector3 direction = dynamicBone.m_Root.position - center;
        if (_hip != null)
            direction = _hip.InverseTransformDirection(direction);
        return Mathf.Atan2(direction.x, direction.z);
    }

    private static string GetSkirtLayer(string boneName)
    {
        int marker = boneName.IndexOf("MSkirt", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
            return boneName;

        int end = marker + "MSkirt".Length;
        while (end < boneName.Length && char.IsDigit(boneName[end]))
            ++end;
        return boneName.Substring(marker, end - marker);
    }
}
