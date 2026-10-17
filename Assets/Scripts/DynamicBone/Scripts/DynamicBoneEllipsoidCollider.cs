using UnityEngine;

/// <summary>
/// Colisor elipsoidal anisótropo para DynamicBone.
///
/// (T012d) Por qué existe: `DynamicBoneCollider` sólo admite un radio ESCALAR, así que el pelo se
/// empuja igual en las tres direcciones. Pero la geometría real de un mechón de pelo medida sobre
/// `M_Hair` de Chara_1013 es una BANDA PLANA: se extiende mucho de lado (5-9 cm) y muy poco en
/// vertical (0.6-1.7 cm). Con la esfera escalar el mechón no sólo se separa del cuerpo, también
/// ENGORDA hacia arriba y expande la silueta (~+40 % de anchura medida).
///
/// Este colisor modela el mechón con tres semiejes independientes:
///   - `m_Radius`   : semieje lateral principal (sobre el eje X local del GameObject del colisor).
///   - `m_RadiusY`  : semieje en Y local (la dirección "fina" del mechón).
///   - `m_RadiusZ`  : semieje en Z local (largo del mechón si el colisor se orienta a lo largo).
///
/// La prueba de colisión es la estándar de un elipsoide: se lleva la partícula al espacio
/// normalizado del elipsoide, se comprueba la norma y, si está dentro, se proyecta a la superficie
/// resolviendo la raíz positiva de |q + t*d| = 1 (ecuación cuadrática). El resultado es el mismo
/// contrato que `DynamicBoneCollider`: devuelve true y MODIFICA `particlePosition` si hubo choque.
///
/// `m_Bound` se interpreta igual que en el colisor esférico:
///   - Outside: si la partícula está DENTRO, se la saca a la superficie.
///   - Inside : si la partícula está FUERA, se la mete a la superficie.
/// </summary>
[AddComponentMenu("Dynamic Bone/Dynamic Bone Ellipsoid Collider")]
public class DynamicBoneEllipsoidCollider : DynamicBoneColliderBase
{
    [Tooltip("Semieje local X del elipsoide (ancho lateral del mechón).")]
    public float m_Radius = 0.5f;

    [Tooltip("Semieje local Y del elipsoide (dirección fina del mechón; 0 = usar m_Radius).")]
    public float m_RadiusY = 0f;

    [Tooltip("Semieje local Z del elipsoide (largo del mechón; 0 = usar m_Radius).")]
    public float m_RadiusZ = 0f;

    void OnValidate()
    {
        m_Radius = Mathf.Max(m_Radius, 0f);
        m_RadiusY = Mathf.Max(m_RadiusY, 0f);
        m_RadiusZ = Mathf.Max(m_RadiusZ, 0f);
    }

    /// Semiejes efectivos en unidades de mundo (aplicando la escala del colisor).
    void GetWorldRadii(out Vector3 radii, out Vector3 center, out Quaternion rot)
    {
        Vector3 s = transform.lossyScale;
        radii = new Vector3(
            m_Radius * Mathf.Abs(s.x),
            (m_RadiusY > 0f ? m_RadiusY : m_Radius) * Mathf.Abs(s.y),
            (m_RadiusZ > 0f ? m_RadiusZ : m_Radius) * Mathf.Abs(s.z));
        // Un semieje puede quedar a 0 si el GameObject está a escala 0 en ese eje: evitamos división por cero.
        radii.x = Mathf.Max(radii.x, 1e-5f);
        radii.y = Mathf.Max(radii.y, 1e-5f);
        radii.z = Mathf.Max(radii.z, 1e-5f);
        center = transform.TransformPoint(m_Center);
        rot = transform.rotation;
    }

    public override bool Collide(ref Vector3 particlePosition, float particleRadius)
    {
        Vector3 radii, center;
        Quaternion rot;
        GetWorldRadii(out radii, out center, out rot);

        // Trabajamos en un espacio donde el elipsoide es una esfera de radio 1.
        // El radio de la partícula también se normaliza: para un elipsoide anisótropo no hay
        // un único equivalente, así que se usa la media de los semiejes como referencia.
        float avgRadius = (radii.x + radii.y + radii.z) / 3f;

        Vector3 local = Quaternion.Inverse(rot) * (particlePosition - center);
        Vector3 q = new Vector3(local.x / radii.x, local.y / radii.y, local.z / radii.z);

        Vector3 qr = new Vector3(
            particleRadius / radii.x,
            particleRadius / radii.y,
            particleRadius / radii.z);

        float normQ = q.magnitude;
        // Frontera efectiva: 1 + radio de partícula (normalizado por eje). Se usa la media escalar
        // para que el comportamiento sea estable entre ejes.
        float boundary = 1f + (particleRadius / Mathf.Max(avgRadius, 1e-5f));

        bool inside = normQ < boundary;

        if (m_Bound == Bound.Outside)
        {
            if (!inside)
                return false;

            // Proyectar desde el centro hasta la frontera, preservando la dirección.
            if (normQ < 1e-6f)
            {
                // Partícula en el centro exacto: empujar hacia arriba local.
                particlePosition = center + rot * new Vector3(0f, radii.y * boundary, 0f);
                return true;
            }
            Vector3 dir = q / normQ;
            Vector3 target = new Vector3(dir.x * radii.x, dir.y * radii.y, dir.z * radii.z) * boundary;
            particlePosition = center + rot * target;
            return true;
        }
        else
        {
            if (inside)
                return false;

            // Proyectar desde fuera hasta la frontera.
            Vector3 dir = q / normQ;
            Vector3 target = new Vector3(dir.x * radii.x, dir.y * radii.y, dir.z * radii.z) * boundary;
            particlePosition = center + rot * target;
            return true;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!enabled)
            return;

        Vector3 radii, center;
        Quaternion rot;
        GetWorldRadii(out radii, out center, out rot);

        Gizmos.color = m_Bound == Bound.Outside ? Color.yellow : Color.magenta;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
        Gizmos.DrawWireSphere(Vector3.zero, 1f); // dibujado como caja de referencia
        Gizmos.matrix = Matrix4x4.TRS(center, rot, radii * 2f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = old;
    }
}
