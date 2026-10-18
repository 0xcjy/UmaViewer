using UnityEngine;

/// <summary>
/// (TEST) Colisor de prueba con teclas: al pulsar la combinacion (Ctrl+Shift+T por defecto) aparece una
/// esfera que puedes MOVER y ESCALAR libremente (herramientas del Scene view o el Inspector) y que es
/// SOLIDA para la fisica del pelo: el pelo se aparta de ella igual que del brazo.
///
/// Como se hace "solida" sin re-bindear nada: reutiliza uno de los colisores de RESERVA del pelo
/// (`Col_HairArm_R_07`), que ya esta en las listas de colision de las cadenas y esta desactivado. Se
/// activa, se le pone el radio del objeto y se le sigue la posicion cada frame.
///
/// Nota: CySpring NO usa los Collider de Unity (los suyos son datos propios), asi que al objeto creado
/// se le quita el collider de Unity para no estorbar a los raycasts de la UI.
/// </summary>
public class CySpringTestCollider : MonoBehaviour
{
    [Header("Teclas (alterna crear / quitar)")]
    [Tooltip("Tecla principal.")]
    public KeyCode Key = KeyCode.T;
    public bool RequireCtrl = true;
    public bool RequireShift = true;

    [Header("Objeto de prueba")]
    [Tooltip("Diametro inicial del objeto (metros). Luego lo escalas como quieras.")]
    public float DefaultDiameter = 0.10f;
    [Tooltip("Distancia por delante del pecho donde aparece.")]
    public float SpawnForwardOffset = 0.20f;

    [Header("Colisor de reserva que se reutiliza")]
    [Tooltip("Debe existir en las listas de colision del pelo y estar desactivado (los 'reserva' lo estan).")]
    public string ReservedColliderName = "Col_HairArm_R_07";

    private const string TestObjectName = "TEST_ColisorPelo";

    private UmaContainerCharacter _character;
    private Gallop.CySpringController _controller;
    private Gallop.CySpringCollisionRuntimeData _entry;
    private GameObject _testObject;
    private float _lastRadius = -1.0f;
    private Vector3 _lastLocal = new Vector3(9999.0f, 9999.0f, 9999.0f);

    private void Awake()
    {
        _character = GetComponent<UmaContainerCharacter>();
        _controller = GetComponent<Gallop.CySpringController>();
    }

    private void OnDisable()
    {
        if (_testObject != null)
            _testObject.SetActive(false);

        ReleaseCollider();
    }

    private void Update()
    {
        if (Key != KeyCode.None && Input.GetKeyDown(Key) && ModifiersMatch())
        {
            ToggleTestObject();
            return;
        }

        if (_testObject != null && _testObject.activeSelf)
            SyncCollider();
    }

    /// <summary>Crea el objeto de prueba si no esta, o lo quita si ya esta.</summary>
    public void ToggleTestObject()
    {
        if (_testObject != null)
        {
            _testObject.SetActive(false);
            Destroy(_testObject);
            _testObject = null;
            ReleaseCollider();
            Debug.Log("[PeloTest] colisor de prueba quitado.");
            return;
        }

        CreateTestObject();
    }

    private bool ModifiersMatch()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        return ctrl == RequireCtrl && shift == RequireShift;
    }

    private void CreateTestObject()
    {
        _testObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _testObject.name = TestObjectName;

        // CySpring no usa los Collider de Unity: se quita para no estorbar a los raycasts de la UI.
        Collider unityCollider = _testObject.GetComponent<Collider>();
        if (unityCollider != null)
            Destroy(unityCollider);

        Renderer objectRenderer = _testObject.GetComponent<Renderer>();
        if (objectRenderer != null)
            objectRenderer.sharedMaterial = CreateTestMaterial();

        _testObject.transform.position = GetSpawnPosition();
        _testObject.transform.localScale = Vector3.one * DefaultDiameter;

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = _testObject;
#endif

        _entry = FindReservedEntry();
        if (_entry == null)
        {
            Debug.LogWarning(
                $"[PeloTest] no encuentro el colisor de reserva '{ReservedColliderName}'. " +
                "Usa un nombre que exista en el contenedor del pelo y este desactivado.");
        }
        else
        {
            _entry.CollisionType = Gallop.CySpringCollisionData.CollisionType.Sphere;
        }

        _lastRadius = -1.0f;
        _lastLocal = new Vector3(9999.0f, 9999.0f, 9999.0f);
        SyncCollider();

        Debug.Log(
            $"[PeloTest] colisor de prueba CREADO (diametro {DefaultDiameter:F3} m) usando '{ReservedColliderName}'. " +
            "Muevelo y escalalo con las herramientas del Scene view (W y R) o desde el Inspector: el pelo choca con el.");
    }

    private Vector3 GetSpawnPosition()
    {
        Transform chest = null;
        if (_character != null)
        {
            Transform[] all = _character.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "Chest")
                {
                    chest = all[i];
                    break;
                }
            }
        }

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.forward;

        if (chest != null)
            return chest.position + forward * SpawnForwardOffset;

        return transform.position + Vector3.up * 1.1f + forward * SpawnForwardOffset;
    }

    private Gallop.CySpringCollisionRuntimeData FindReservedEntry()
    {
        if (_controller == null)
            _controller = GetComponent<Gallop.CySpringController>();

        if (_controller == null)
        {
            Debug.LogWarning("[PeloTest] no hay CySpringController: ¿esta cargado el personaje?");
            return null;
        }

        // Part 0 = contenedor del pelo (es donde viven los colisores generados).
        System.Collections.Generic.List<Gallop.CySpringCollisionRuntimeData> list =
            _controller.GetCollisionRuntimeList(0);

        if (list == null)
            return null;

        for (int i = 0; i < list.Count; i++)
        {
            Gallop.CySpringCollisionRuntimeData runtime = list[i];
            if (runtime != null && runtime.Name == ReservedColliderName)
                return runtime;
        }

        return null;
    }

    // Copia la posicion y el tamano del objeto al colisor de CySpring (radio = mitad del diametro).
    private void SyncCollider()
    {
        if (_entry == null || _entry.TargetTransform == null || _controller == null || _testObject == null)
            return;

        if (_entry.CollisionType != Gallop.CySpringCollisionData.CollisionType.Sphere)
            _entry.CollisionType = Gallop.CySpringCollisionData.CollisionType.Sphere;

        float radius = Mathf.Max(0.001f, _testObject.transform.lossyScale.x * 0.5f);

        Transform parent = _entry.TargetTransform.parent;
        Vector3 local = parent != null
            ? parent.InverseTransformPoint(_testObject.transform.position)
            : _testObject.transform.position;

        bool changed = !Mathf.Approximately(radius, _lastRadius) ||
                       (local - _lastLocal).sqrMagnitude > 1e-10f;

        if (!changed)
            return;

        _lastRadius = radius;
        _lastLocal = local;

        _entry.Radius = radius;
        _entry.TargetTransform.localPosition = local;
        _entry.SetDefaultParams(radius, local);

        // Refresco LIGERO (sin recrear el array nativo ni re-registrar parent works): asi el arrastre
        // es fluido y no inunda la consola.
        _controller.RefreshNativeCollisionPositions(0);
    }

    private void ReleaseCollider()
    {
        if (_entry == null || _controller == null)
            return;

        _entry.CollisionType = Gallop.CySpringCollisionData.CollisionType.None;
        _entry.Radius = 0.0f;
        _entry.SetDefaultParams(0.0f, Vector3.zero);
        _controller.RefreshNativeCollisionPositions(0);
        _entry = null;
    }

    private static Material CreateTestMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader);
        material.color = new Color(1.0f, 0.45f, 0.1f, 0.35f);
        return material;
    }
}
