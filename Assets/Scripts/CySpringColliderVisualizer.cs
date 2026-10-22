using UnityEngine;

/// <summary>
/// Dibuja en la SCENE VIEW (gizmos) una esfera/capsula de alambre, para afinar a ojo la posicion y
/// el tamano de los colisores de CySpring sin tocar el motor. Solo dibuja en el Editor: no afecta
/// al juego, ni al render, ni a una build.
///
/// Lo usan dos cosas (ver UmaContainerCharacter):
///  - los COLISORES (esferas que el cargador genera para el pelo); `IsBone = false`.
///  - el RADIO PROPIO de cada hueso de tela (`CollisionRadius`), que el motor SUMA al del colisor
///    (datamine 04 §1.1: R = colider.Radius + hueso.CollisionRadius); `IsBone = true`.
///
/// Los flags estaticos permiten encender/apagar todos los gizmos de golpe desde el Inspector del
/// cargador, sin tener que quitar los componentes.
/// </summary>
public class CySpringColliderVisualizer : MonoBehaviour
{
    /// <summary>Dibuja los colisores (esferas anadidas). Lo fija el cargador cada frame.</summary>
    public static bool CollidersEnabled = true;

    /// <summary>Dibuja el radio propio de cada hueso de tela. Lo fija el cargador cada frame.</summary>
    public static bool BonesEnabled = true;

    /// <summary>True si esta esfera representa el radio propio de un hueso (no un colisor).</summary>
    public bool IsBone = false;

    /// <summary>Radio actual. Si `BoneSource` esta puesto, se IGNORA y manda el radio del hueso.</summary>
    public float Radius = 0.1f;

    /// <summary>
    /// Hueso de tela del que LEER el radio en cada dibujado. Se usa para los gizmos de hueso: asi el
    /// tamano que se ve es SIEMPRE el que el motor esta usando de verdad (aunque el ajuste se cambie
    /// en caliente desde el Inspector), sin depender de que alguien se acuerde de sincronizarlo.
    /// </summary>
    public Gallop.CySpringBoneBase BoneSource;

    /// <summary>Colisor del que leer el radio en cada dibujado (opcional, misma idea que BoneSource).</summary>
    public Gallop.CySpringCollisionRuntimeData ColliderSource;

    /// <summary>
    /// Colisor de DynamicBone del que leer el radio en cada dibujado. DynamicBone multiplica su
    /// radio por la escala del objeto (ver DynamicBoneCollider.Collide), asi que el gizmo hace lo
    /// mismo y el tamano dibujado es el que el solver esta usando de verdad.
    /// </summary>
    public DynamicBoneCollider DynamicBoneSource;

    /// <summary>Si es una capsula (True) o una esfera (False).</summary>
    public bool IsCapsule = false;

    /// <summary>Extremo B de la capsula, en local al hueso de anclaje.</summary>
    public Vector3 Offset2Local = Vector3.zero;

    /// <summary>Color del alambre.</summary>
    public Color Color = new Color(1.0f, 0.5f, 0.0f, 1.0f);

    private float CurrentRadius
    {
        get
        {
            if (BoneSource != null)
                return BoneSource.NativeState.CollisionRadius;

            if (ColliderSource != null)
                return ColliderSource.Radius;

            if (DynamicBoneSource != null)
                return DynamicBoneSource.m_Radius * Mathf.Abs(transform.lossyScale.x);

            return Radius;
        }
    }

    private void OnDrawGizmos()
    {
        // (OCULTO) Gizmos COMENTADOS a peticion: no deben aparecer en la Scene view. El codigo se
        // queda intacto para poder reactivarlo (descomentar este metodo).
        //
        // if (IsBone ? !BonesEnabled : !CollidersEnabled)
        //     return;
        //
        // float radius = CurrentRadius;
        // if (radius <= 0.00001f)
        //     return;
        //
        // Gizmos.color = Color;
        // Gizmos.DrawWireSphere(transform.position, radius);
        //
        // if (IsBone || !IsCapsule)
        //     return;
        //
        // Vector3 b = transform.TransformPoint(Offset2Local);
        // Gizmos.DrawLine(transform.position, b);
        // Gizmos.DrawWireSphere(b, radius);
    }
}
