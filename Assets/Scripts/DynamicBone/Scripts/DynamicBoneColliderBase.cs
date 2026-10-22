using UnityEngine;

public class DynamicBoneColliderBase : MonoBehaviour
{
    public string ColliderName;
    public enum Direction
    {
        X, Y, Z
    }

#if UNITY_5_3_OR_NEWER
    [Tooltip("The axis of the capsule's height.")]
#endif
    public Direction m_Direction = Direction.Y;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The center of the sphere or capsule, in the object's local space.")]
#endif
    public Vector3 m_Center = Vector3.zero;

    public enum Bound
    {
        Outside,
        Inside
    }

#if UNITY_5_3_OR_NEWER
    [Tooltip("Constrain bones to outside bound or inside bound.")]
#endif
    public Bound m_Bound = Bound.Outside;

    /// <summary>
    /// Volver a aplicar este colisor DESPUES de la restriccion de longitud del solver.
    ///
    /// Motivo: `DynamicBone.UpdateParticles2` resuelve la colision ANTES de "keep length", y ese
    /// ultimo paso devuelve SIEMPRE la particula a `restLen` respecto a su padre, asi que la puede
    /// meter otra vez dentro de la esfera (medido en el live: 2.3 cm dentro del colisor del pecho,
    /// con `m_isCollide` a true). Marcando aqui el colisor, el frame termina sin penetracion
    /// (ver `DynamicBone.ResolveCollisionsAfterSolve`).
    ///
    /// Apagado por defecto: los colisores del asset (esferas de hasta ~0.9 m) siguen comportandose
    /// como siempre y sólo las esferas generadas por el cargador piden este segundo pase.
    /// </summary>
    public bool ResolveAfterSolve = false;

    public virtual bool Collide(ref Vector3 particlePosition, float particleRadius)
    {
        return false;
    }
}
