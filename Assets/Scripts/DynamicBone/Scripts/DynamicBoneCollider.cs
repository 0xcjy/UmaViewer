using UnityEngine;

[AddComponentMenu("Dynamic Bone/Dynamic Bone Collider")]
public class DynamicBoneCollider : DynamicBoneColliderBase
{
#if UNITY_5_3_OR_NEWER
	[Tooltip("The radius of the sphere or capsule.")]
#endif	
    public float m_Radius = 0.5f;
	
#if UNITY_5_3_OR_NEWER
	[Tooltip("The height of the capsule.")]
#endif		
    public float m_Height = 0;

    // =========================================================================================
    // (RENDIMIENTO) Cache por frame de la geometria en MUNDO.
    //
    // `Collide` lee `transform.lossyScale` y `transform.TransformPoint(...)` en CADA llamada, y un
    // acceso a Transform desde script es una llamada nativa cara. En el pase de colision final se
    // llama por particula x colisor x iteracion (cientos de miles por frame en el live), y ahi se
    // iba casi todo el tiempo. Aqui se lee UNA vez por colisor y frame y el resto es matematica pura.
    // =========================================================================================
    [System.NonSerialized] public Vector3 CachedWorldCenter;
    [System.NonSerialized] public float CachedWorldRadius;
    [System.NonSerialized] public int CachedFrame = -1;

    /// <summary>Refresca la cache si el frame es distinto (deduplicado por frame; lo llama el pase de colision).</summary>
    public void UpdateWorldCache(int frame)
    {
        if (CachedFrame == frame)
            return;

        CachedFrame = frame;
        CachedWorldCenter = transform.TransformPoint(m_Center);
        CachedWorldRadius = m_Radius * Mathf.Abs(transform.lossyScale.x);
    }

    /// <summary>
    /// Version rapida para ESFERAS con `Bound.Outside` (las que genera el cargador) usando la cache:
    /// sin accesos a Transform ni a Time. El que llama DEBE haber refrescado la cache este frame
    /// (`UpdateWorldCache(frame)`). Devuelve true si movio la particula.
    /// </summary>
    public bool CollideCachedSphere(ref Vector3 particlePosition, float particleRadius)
    {
        float r = CachedWorldRadius + particleRadius;
        Vector3 d = particlePosition - CachedWorldCenter;
        float len2 = d.sqrMagnitude;
        if (len2 >= r * r)
            return false;

        float len = Mathf.Sqrt(len2);
        if (len > 1e-6f)
            particlePosition = CachedWorldCenter + d * (r / len);
        else
            particlePosition = CachedWorldCenter + Vector3.up * r;

        return true;
    }

    void OnValidate()
    {
        m_Radius = Mathf.Max(m_Radius, 0);
        m_Height = Mathf.Max(m_Height, 0);
    }

    // (RENDIMIENTO) Frame actual para la cache. Lo fija `DynamicBone.UpdateDynamicBones` una vez por
    // cadena y frame (barato) para que `Collide` no tenga que leer `Time.frameCount` en cada llamada.
    [System.NonSerialized] public static int s_FrameStamp = -1;

    public override bool Collide(ref Vector3 particlePosition, float particleRadius)
    {
        // La geometria (centro y radio en mundo) se refresca 1 vez por colisor y frame. Antes se
        // leian `transform.lossyScale` y `TransformPoint` en CADA llamada (llamada nativa), y esto
        // se invoca por particula x colisor: medido 362 ns vs 62 ns (x5,9).
        UpdateWorldCache(s_FrameStamp);

        float radius = CachedWorldRadius;
        float h = m_Height * 0.5f - m_Radius;
        if (h <= 0)
        {
            if (m_Bound == Bound.Outside)
                return OutsideSphere(ref particlePosition, particleRadius, CachedWorldCenter, radius);
            else
                return InsideSphere(ref particlePosition, particleRadius, CachedWorldCenter, radius);
        }
        else
        {
            // Capsula: los extremos dependen de la rotacion del transform, se quedan como estaban.
            Vector3 c0 = m_Center;
            Vector3 c1 = m_Center;

            switch (m_Direction)
            {
                case Direction.X:
                    c0.x -= h;
                    c1.x += h;
                    break;
                case Direction.Y:
                    c0.y -= h;
                    c1.y += h;
                    break;
                case Direction.Z:
                    c0.z -= h;
                    c1.z += h;
                    break;
            }
            if (m_Bound == Bound.Outside)
                return OutsideCapsule(ref particlePosition, particleRadius, transform.TransformPoint(c0), transform.TransformPoint(c1), radius);
            else
                return InsideCapsule(ref particlePosition, particleRadius, transform.TransformPoint(c0), transform.TransformPoint(c1), radius);
        }
    }

    static bool OutsideSphere(ref Vector3 particlePosition, float particleRadius, Vector3 sphereCenter, float sphereRadius)
    {
        float r = sphereRadius + particleRadius;
        float r2 = r * r;
        Vector3 d = particlePosition - sphereCenter;
        float len2 = d.sqrMagnitude;

        // if is inside sphere, project onto sphere surface
        if (len2 > 0 && len2 < r2)
        {
            float len = Mathf.Sqrt(len2);
            particlePosition = sphereCenter + d * (r / len);
            return true;
        }
        return false;
    }

    static bool InsideSphere(ref Vector3 particlePosition, float particleRadius, Vector3 sphereCenter, float sphereRadius)
    {
        float r = sphereRadius - particleRadius;
        float r2 = r * r;
        Vector3 d = particlePosition - sphereCenter;
        float len2 = d.sqrMagnitude;

        // if is outside sphere, project onto sphere surface
        if (len2 > r2)
        {
            float len = Mathf.Sqrt(len2);
            particlePosition = sphereCenter + d * (r / len);
            return true;
        }
        return false;
    }

    static bool OutsideCapsule(ref Vector3 particlePosition, float particleRadius, Vector3 capsuleP0, Vector3 capsuleP1, float capsuleRadius)
    {
        float r = capsuleRadius + particleRadius;
        float r2 = r * r;
        Vector3 dir = capsuleP1 - capsuleP0;
        Vector3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float len2 = d.sqrMagnitude;
            if (len2 > 0 && len2 < r2)
            {
                float len = Mathf.Sqrt(len2);
                particlePosition = capsuleP0 + d * (r / len);
                return true;
            }
        }
        else
        {
            float dl = dir.sqrMagnitude;
            if (t >= dl)
            {
                // check sphere2
                d = particlePosition - capsuleP1;
                float len2 = d.sqrMagnitude;
                if (len2 > 0 && len2 < r2)
                {
                    float len = Mathf.Sqrt(len2);
                    particlePosition = capsuleP1 + d * (r / len);
                    return true;
                }
            }
            else if (dl > 0)
            {
                // check cylinder
                t /= dl;
                d -= dir * t;
                float len2 = d.sqrMagnitude;
                if (len2 > 0 && len2 < r2)
                {
                    float len = Mathf.Sqrt(len2);
                    particlePosition += d * ((r - len) / len);
                    return true;
                }
            }
        }
        return false;
    }

    static bool InsideCapsule(ref Vector3 particlePosition, float particleRadius, Vector3 capsuleP0, Vector3 capsuleP1, float capsuleRadius)
    {
        float r = capsuleRadius - particleRadius;
        float r2 = r * r;
        Vector3 dir = capsuleP1 - capsuleP0;
        Vector3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float len2 = d.sqrMagnitude;
            if (len2 > r2)
            {
                float len = Mathf.Sqrt(len2);
                particlePosition = capsuleP0 + d * (r / len);
                return true;
            }
        }
        else
        {
            float dl = dir.sqrMagnitude;
            if (t >= dl)
            {
                // check sphere2
                d = particlePosition - capsuleP1;
                float len2 = d.sqrMagnitude;
                if (len2 > r2)
                {
                    float len = Mathf.Sqrt(len2);
                    particlePosition = capsuleP1 + d * (r / len);
                    return true;
                }
            }
            else if (dl > 0)
            {
                // check cylinder
                t /= dl;
                d -= dir * t;
                float len2 = d.sqrMagnitude;
                if (len2 > r2)
                {
                    float len = Mathf.Sqrt(len2);
                    particlePosition += d * ((r - len) / len);
                    return true;
                }
            }
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!enabled)
            return;

        if (m_Bound == Bound.Outside)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.magenta;
        float radius = m_Radius * Mathf.Abs(transform.lossyScale.x);
        float h = m_Height * 0.5f - m_Radius;
        if (h <= 0)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(m_Center), radius);
        }
        else
        {
            Vector3 c0 = m_Center;
            Vector3 c1 = m_Center;

            switch (m_Direction)
            {
                case Direction.X:
                    c0.x -= h;
                    c1.x += h;
                    break;
                case Direction.Y:
                    c0.y -= h;
                    c1.y += h;
                    break;
                case Direction.Z:
                    c0.z -= h;
                    c1.z += h;
                    break;
            }
            Gizmos.DrawWireSphere(transform.TransformPoint(c0), radius);
            Gizmos.DrawWireSphere(transform.TransformPoint(c1), radius);
        }
    }
}
