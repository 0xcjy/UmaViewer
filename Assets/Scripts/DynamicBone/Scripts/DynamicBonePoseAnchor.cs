using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// (T012d) Re-ancla las partículas de DynamicBone a la pose REAL de sus huesos cuando la
/// simulación se ha desacoplado de ella.
///
/// Problema medido (Chara_1013, 1 PJ): la malla del pelo está skineada a los huesos `Sp_He_Hair*`,
/// que siguen la animación y el giro manual de cabeza. Pero las partículas de DynamicBone —y con
/// ellas la COLISIÓN— se quedan ancladas a la pose que había al inicializar. Medido: la partícula
/// de `Sp_He_Hair4_L_01` quedaba a 0.078 m de su propio hueso, `_02` a 0.070 m, `_03` a 0.106 m.
/// Como la malla sigue al hueso y la física vive en la partícula, la colisión protege donde no se
/// ve y el mechón se solapa con el cuerpo.
///
/// Solución: cuando el desfase partícula↔hueso supera un umbral, o cuando el usuario mueve la
/// cabeza (evento), se llama `ResetParticlesPosition()` para que las partículas se re-anclen a la
/// pose actual. Se hace por evento + umbral, no cada frame, para no matar la inercia de la física.
/// Aplica por igual a pelo, orejas y cola.
/// </summary>
[DisallowMultipleComponent]
public class DynamicBonePoseAnchor : MonoBehaviour
{
    [Tooltip("Desfase partícula↔hueso (m) a partir del cual se re-ancla la simulación.")]
    public float driftThreshold = 0.05f;

    [Tooltip("Desfase máximo permitido antes de forzar el re-anclaje, aunque no cambie la pose.")]
    public float hardDriftThreshold = 0.10f;

    [Tooltip("Cadenas de DynamicBone a vigilar. Si está vacío, se recogen de los hijos.")]
    public List<DynamicBone> bones = new List<DynamicBone>();

    // Cambio de rotación del hueso raíz (grado) que cuenta como "movimiento manual".
    private const float RootRotateEvent = 2.0f;

    private readonly List<Quaternion> _lastRootRot = new List<Quaternion>();
    private bool _initialized;

    void Awake()
    {
        CollectBones();
        CaptureRootRotations();
        _initialized = true;
    }

    void OnEnable()
    {
        // Al reactivarse (cambio de personaje/pose), re-anclar una vez para partir de la pose real.
        if (_initialized)
            ResetAll();
    }

    private void CollectBones()
    {
        if (bones != null && bones.Count > 0)
            return;

        bones = new List<DynamicBone>();
        var found = GetComponentsInChildren<DynamicBone>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                bones.Add(found[i]);
        }
    }

    private void CaptureRootRotations()
    {
        _lastRootRot.Clear();
        if (bones == null)
            return;

        for (int i = 0; i < bones.Count; i++)
        {
            var db = bones[i];
            _lastRootRot.Add(db != null && db.m_Root != null ? db.m_Root.rotation : Quaternion.identity);
        }
    }

    /// <summary>
    /// Re-ancla todas las cadenas vigiladas a la pose actual de sus huesos.
    /// </summary>
    public void ResetAll()
    {
        if (bones == null)
            return;

        for (int i = 0; i < bones.Count; i++)
        {
            if (bones[i] != null)
                bones[i].ResetParticlesPosition();
        }
        CaptureRootRotations();
    }

    void LateUpdate()
    {
        if (bones == null || bones.Count == 0)
            return;

        bool poseChanged = false;
        bool driftExceeded = false;

        for (int i = 0; i < bones.Count; i++)
        {
            var db = bones[i];
            if (db == null || db.m_Root == null)
                continue;

            // Evento 1: el hueso raíz giró (giro manual de cabeza/cuerpo).
            Quaternion now = db.m_Root.rotation;
            if (i < _lastRootRot.Count)
            {
                if (Quaternion.Angle(_lastRootRot[i], now) > RootRotateEvent)
                    poseChanged = true;
            }

            // Evento 2: la simulación se ha desacoplado de la pose real.
            float drift = db.GetMaxParticleBoneDrift();
            if (drift > hardDriftThreshold || (poseChanged && drift > driftThreshold))
                driftExceeded = true;
        }

        if (poseChanged || driftExceeded)
            ResetAll();
    }
}
