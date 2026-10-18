using Gallop;
using RootMotion;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using UnityEngine;
using static SerializableBone;

public class UmaContainerCharacter : UmaContainer
{
    public CharaEntry CharaEntry;
    public DataRow CharaData;
    public GameObject Body;
    public GameObject Tail;
    public GameObject Head;
    public GameObject Hair;

    public Material FaceMaterial;

    public TextureList TailTextures = new TextureList();

    [Header("Animator")]
    public Animator UmaAnimator;
    public AnimatorOverrideController OverrideController;
    public Animator UmaFaceAnimator;
    public AnimatorOverrideController FaceOverrideController;
    public bool isAnimatorControl;

    [Header("Body")]
    public GameObject UpBodyBone;
    public Vector3 UpBodyPosition;
    public Quaternion UpBodyRotation;
    public Dictionary<Transform, (Vector3 localPosition, Quaternion localRotation, Vector3 localScale)> InitBoneTransform;

    [Header("Face")]
    public FaceDrivenKeyTarget FaceDrivenKeyTarget;
    public FaceEmotionKeyTarget FaceEmotionKeyTarget;
    public FaceOverrideData FaceOverrideData;
    public GameObject HeadBone;
    public Transform TrackTarget;
    public float EyeHeight;
    public bool EnableEyeTracking = true;

    [Header("Cheek")]
    public Texture CheekTex_0;
    public Texture CheekTex_1;
    public Texture CheekTex_2;

    [Header("Manga")]
    public List<GameObject> LeftMangaObject = new List<GameObject>();
    public List<GameObject> RightMangaObject = new List<GameObject>();

    [Header("Tear")]
    public GameObject StaticTear_L;
    public GameObject StaticTear_R;
    public GameObject TearPrefab_0;
    public GameObject TearPrefab_1;
    public List<TearController> TearControllers = new List<TearController>();

    [Header("Generic")]
    public bool IsGeneric = false;
    public string VarCostumeIdShort, VarCostumeIdLong, VarSkin, VarHeight, VarSocks, VarBust;
    // Color elegido de un traje COMUN (0 = el de siempre). Algunos comunes traen varios colores
    // (0015 = camisa blanca/negra; 0006 y 0009 = 4 colores) y cada color es una textura propia
    // (_00_diff, _01_diff, ...). Lo usa LoadBody para elegir _diff y _shad_c.
    public int VarColorIndex = 0;
    public TextureList GenericBodyTextures = new TextureList();

    [Header("Mini")]
    public bool IsMini = false;
    public TextureList MiniHeadTextures = new TextureList();

    [Header("Mob")]
    public bool IsMob = false;
    public DataRow MobDressColor;
    public DataRow MobHeadColor;
    public TextureList MobHeadTextures = new TextureList();

    [Header("ModelController FaceLight鼻部阴影")]
    public Transform HeadToonBaseTransform;
    public Renderer FaceRenderer;
    // 这两个先保留你测试时效果好的值。
    // 后面如果继续逆 asset holder，再换成官方 asset 里的值。
    public float FaceCenterUpOffset = 0.03f;
    public float FaceCenterForwardOffset = 0.03f;
    public bool FlipFaceForward = false;

    private Material[] _faceMaterials;
    private bool _faceLightInitialized;

    private readonly HashSet<Renderer> _mirrorHeadRenderers = new HashSet<Renderer>();
    private GameObject[] _mirrorAllRendererObjects = Array.Empty<GameObject>();
    private GameObject[] _mirrorBodyRendererObjects = Array.Empty<GameObject>();
    private GameObject[] _mirrorChangedObjects = Array.Empty<GameObject>();
    private int[] _mirrorOriginalLayers = Array.Empty<int>();
    private int _mirrorChangedCount;
    private bool _mirrorLayerCacheDirty = true;
    private bool _mirrorLayerCacheReady;

    private void RegisterMirrorHeadRenderers(GameObject source)
    {
        RestoreMirrorChangedLayers();
        if (source != null)
        {
            var renderers = source.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                _mirrorHeadRenderers.Add(renderers[i]);
        }
        InvalidateMirrorLayerCache();
    }

    private void InvalidateMirrorLayerCache()
    {
        RestoreMirrorChangedLayers();
        _mirrorLayerCacheDirty = true;
        if (_mirrorLayerCacheReady)
            PrepareMirrorLayerCache();
    }

    internal void PrepareMirrorLayerCache()
    {
        if (!_mirrorLayerCacheDirty)
            return;

        RestoreMirrorChangedLayers();
        var renderers = GetComponentsInChildren<Renderer>(true);
        var all = new List<GameObject>(renderers.Length);
        var body = new List<GameObject>(renderers.Length);
        var seen = new HashSet<GameObject>();
        var headObjects = new HashSet<GameObject>();
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && _mirrorHeadRenderers.Contains(renderers[i]))
                headObjects.Add(renderers[i].gameObject);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || !seen.Add(renderer.gameObject))
                continue;
            all.Add(renderer.gameObject);
            if (!headObjects.Contains(renderer.gameObject))
                body.Add(renderer.gameObject);
        }
        _mirrorAllRendererObjects = all.ToArray();
        _mirrorBodyRendererObjects = body.ToArray();
        if (_mirrorChangedObjects.Length < all.Count)
        {
            _mirrorChangedObjects = new GameObject[all.Count];
            _mirrorOriginalLayers = new int[all.Count];
        }
        _mirrorLayerCacheDirty = false;
        _mirrorLayerCacheReady = true;
    }

    internal void SetLayerBeforeMirrorCameraRendering(bool bodyBit, bool headBit, int layer)
    {
        if (!bodyBit || layer < 0)
            return;

        RestoreMirrorChangedLayers();
        var objects = headBit ? _mirrorAllRendererObjects : _mirrorBodyRendererObjects;
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null)
                continue;
            _mirrorChangedObjects[_mirrorChangedCount] = obj;
            _mirrorOriginalLayers[_mirrorChangedCount] = obj.layer;
            _mirrorChangedCount++;
            obj.layer = layer;
        }
    }

    internal void RestoreMirrorChangedLayers()
    {
        while (_mirrorChangedCount > 0)
        {
            int index = --_mirrorChangedCount;
            var obj = _mirrorChangedObjects[index];
            _mirrorChangedObjects[index] = null;
            if (obj != null)
                obj.layer = _mirrorOriginalLayers[index];
        }
    }

    private void OnDisable()
    {
        RestoreMirrorChangedLayers();
    }

    private void OnDestroy()
    {
        RestoreMirrorChangedLayers();
    }

    private static readonly int ID_FaceCenterPos = Shader.PropertyToID("_FaceCenterPos");
    private static readonly int ID_FaceUp = Shader.PropertyToID("_FaceUp");
    private static readonly int ID_FaceForward = Shader.PropertyToID("_FaceForward");
    private static readonly int ID_FaceShadowHeadMat = Shader.PropertyToID("_faceShadowHeadMat");

    [Header("Physics")]
    public bool EnablePhysics = true;
    public List<CySpringDataContainer> cySpringDataContainers;
    public GameObject PhysicsContainer;
    public float BodyScale = 1;

    // (DATAMINE) El motor CySpring es un solver Verlet que se alimenta UNA vez por frame: el
    // "ancla" (SelfPosition) se recoge al principio del frame y su desplazamiento respecto al
    // frame anterior es la energía de la cadena. Por eso NO se sub-divide el frame en pasos
    // fijos: ver AlterLateUpdateRuntime.
    [Header("CySpring (datamine)")]
    [Tooltip("Warm-up (s) al cargar: asienta la cadena antes de mostrarla (oficial: DEFAULT_WARMUPTIME). 0 = off.")]
    public float CySpringWarmUpTime = 1.0f;
    [Tooltip("Enrutar el contenedor de CABEZA (pelo/orejas) al camino NATIVO CySpring (DLL) en vez de DynamicBone.\n" +
             "OFF (por defecto): pelo/orejas y cola van por DynamicBone; la ropa (body+bust) por el puente nativo.")]
    public bool CySpringHairViaNative = false;
    [Tooltip("(DIAG) Loguea en consola la coherencia del reposo de las cadenas del pelo:\n" +
             "Aim = (ParentRotation*InitLocalRotation)*BoneAxis  vs  direccion real hueso->hijo.\n" +
             "Si el error es grande, el solver arranca con la cadena torcida.")]
    public bool CySpringDiagnoseHair = true;
    [Tooltip("(INTENTO 10) Desactivar IsLimit en los resortes del PELO antes del bind.\n" +
             "Medido: Sp_He_Hair2_L/R_00 (los sidelocks) tienen IsLimit=1 con y en [-50,+10]; el motor\n" +
             "recorta la rotacion relativa a esos limites y los deja clavados/torcidos.")]
    public bool CySpringDisableHairLimits = false;
    private int _diagFrameCounter;
    private int _diagLogsDone;
    // Colisores GENERADOS del pelo: prefijo de los nombres reservados + estado del ultimo ajuste,
    // para poder recolocar/redimensionar las esferas EN CALIENTE desde el Inspector.
    private const string CySpringHairArmCollisionPrefix = "Col_HairArm_";
    private readonly List<string> _generatedHairCollisionNames = new List<string>();
    private System.Collections.Generic.List<CySpringCollisionRuntimeData> _hairRuntimeCollisions;
    private float _lastArmRadius = -1.0f;
    private int _lastArmCount = -1;
    private float _lastArmOverlap = -1.0f;
    private float _lastArmStartOffset = -1.0f;
    // Extremos del tramo hombro->codo EN EL ESPACIO LOCAL del hueso del brazo, capturados una sola
    // vez (pose de carga) para que el reparto no dependa de la pose del momento.
    private Vector3[] _armSegmentLocalStart;
    private Vector3[] _armSegmentLocalEnd;
    private float[] _armSegmentLength;
    [Header("CySpring - colisores del PELO (esferas generadas hombro -> codo)")]
    [Tooltip("Genera las esferas del tramo HOMBRO->CODO de cada brazo y las asigna al pelo\n" +
             "(delantero Y trasero). Sustituye el mecanismo anterior (lista blanca + una unica escala\n" +
             "para todos los colisores del juego), que aplicaba el mismo radio a geometrias distintas.")]
    public bool CySpringArmColliderEnabled = true;
    [Tooltip("Radio de cada esfera, en metros. Aproximadamente el grosor del brazo.\n" +
             "Referencia: el colisor de brazo del propio juego (Col_Arm_L_Hair) usa 0.04.")]
    public float CySpringArmColliderRadius = 0.03f;
    [Tooltip("Esferas por brazo. 0 = AUTOMATICO (las justas para cubrir el tramo sin dejar huecos).\n" +
             "POR ENCIMA DEL RESERVADO no surte efecto sin recargar la escena.")]
    public int CySpringArmColliderCount = 0;
    [Tooltip("Solape entre esferas contiguas: 1 = se tocan justo, 0.85 = se solapan un 15%.")]
    public float CySpringArmColliderOverlap = 0.85f;
    [Tooltip("Metros desde el hombro donde EMPIEZA la primera esfera (0 = en el hombro).\n" +
             "SUBIRLO si la esfera del hombro se 'traga' el pelo: el motor empuja la punta a la\n" +
             "superficie de la esfera y luego la reproyecta al largo del hueso, asi que una esfera que\n" +
             "cubre el hueso entero lo deja ATRAPADO dentro (se ve el brazo atravesando el pelo).")]
    public float CySpringArmColliderStartOffset = 0.0f;
    [Tooltip("Esferas RESERVADAS por brazo (subir la cantidad EN CALIENTE sin recargar). Limite duro\n" +
             "del motor: cada hueso admite 16 indices de colision, y las cadenas centrales (pelo\n" +
             "trasero) usan los dos brazos, asi que 2x reservado debe quedar en 16 o menos.")]
    public int CySpringArmColliderReserve = 8;
    [Tooltip("Nombre base del hueso del brazo al que se anclan las esferas: <base>_<lado> (p. ej. Arm_L).")]
    public string CySpringArmColliderAnchorBone = "Arm";
    [Tooltip("Hueso donde EMPIEZA el tramo: <base>_<lado> (el hombro = origen del brazo).")]
    public string CySpringArmColliderStartBone = "Arm";
    [Tooltip("Hueso donde ACABA el tramo: <base>_<lado> (el codo).")]
    public string CySpringArmColliderEndBone = "Elbow";
    [Tooltip("Sufijos de lado. El nombre del hueso es <base>_<sufijo>.")]
    public string[] CySpringArmColliderSides = new string[] { "L", "R" };
    [Tooltip("(TEMP) Dibuja en la SCENE VIEW los colisores generados y el radio propio de cada hueso\n" +
             "de pelo. Solo editor: apagarlo cuando el ajuste este cerrado.")]
    public bool CySpringHairColliderGizmos = true;

    [Header("CySpring - colisores del CUERPO (clavicula y espalda)")]
    [Tooltip("Genera las esferas del CUERPO para que el pelo no entre en el (ni en la ropa pegada):\n" +
             "una COLUMNA por la espalda (del hueso de arriba al de abajo) y una FILA en la clavicula\n" +
             "(de hombro a hombro). Son las que empujan al pelo por el interior del torso.")]
    public bool CySpringBodyColliderEnabled = true;
    [Tooltip("Radio (m) de las esferas del cuerpo. SUBIRLO hasta que el pelo quede por FUERA de la ropa\n" +
             "(la ropa no empuja al pelo, asi que el colisor debe llegar a su superficie).")]
    public float CySpringBodyColliderRadius = 0.06f;
    [Tooltip("COLUMNA: esferas de la espalda. 0 = automatico (segun el largo y el radio).")]
    public int CySpringBodyColliderColumnCount = 0;
    [Tooltip("COLUMNA: solape entre esferas contiguas.")]
    public float CySpringBodyColliderColumnOverlap = 0.85f;
    [Tooltip("COLUMNA: hueso de ARRIBA (se sube por los padres hasta el de abajo).")]
    public string CySpringBodyColliderTopBone = "Chest";
    [Tooltip("COLUMNA: hueso de ABAJO (la cadera).")]
    public string CySpringBodyColliderBottomBone = "Hip";
    [Tooltip("FILA de la clavicula: esferas entre los dos huesos de abajo (0 = sin fila).")]
    public int CySpringBodyColliderRowCount = 4;
    [Tooltip("FILA: huesos entre los que se reparte. Con Shoulder_L/R queda en la clavicula; con\n" +
             "Arm_L/Arm_R cubre toda la anchura de los hombros.")]
    public string[] CySpringBodyColliderRowBones = new string[] { "Shoulder_L", "Shoulder_R" };

    private readonly List<string> _generatedBodyCollisionNames = new List<string>();
    private readonly List<string> _generatedBodyColumnNames = new List<string>();
    private readonly List<string> _generatedBodyRowNames = new List<string>();
    private float _lastBodyColliderRadius = -1.0f;
    private const string CySpringHairBodyPrefix = "Col_HairBody_";
    private const string CySpringHairRowPrefix = "Col_HairRow_";

    [Header("CySpring - GROSOR del pelo (radio PROPIO de cada hueso, las esferas CIAN)")]
    [Tooltip("Ajusta el radio propio de los huesos de pelo para que represente el GROSOR REAL del\n" +
             "mechon. El motor separa por R = radio_colisor + radio_hueso, asi que el radio que trae el\n" +
             "asset (0.03-0.045) deja que la MALLA del pelo (mucho mas ancha) entre en el brazo aunque\n" +
             "las esferas se solapen. Se aplica EN CALIENTE y se ve en los gizmos cian.")]
    public bool CySpringHairBoneRadiusEnabled = true;
    [Tooltip("Radio (m) en la RAIZ de cada cadena de pelo (nacimiento del mechon).\n" +
             "OJO: no lo subas mucho. El motor empuja la punta a la superficie de la esfera y luego la\n" +
             "reproyecta al largo del hueso, asi que si R = radio_esfera + radio_hueso supera\n" +
             "| |S-hueso - centro_esfera| - largo_hueso |, el hueso se queda ATRAPADO dentro de la esfera\n" +
             "(se ve el brazo atravesando el pelo). Medido: con 0.03-0.04 la cadena se libra.")]
    public float CySpringHairBoneRadiusBase = 0.05f;
    [Tooltip("Radio (m) en la PUNTA de cada cadena de pelo (el mechon se afila).")]
    public float CySpringHairBoneRadiusTip = 0.03f;

    /// <summary>
    /// Grosor (radio propio) DISTINTO para una cadena concreta. La primera entrada cuya `BoneFilter`
    /// este contenida en el hueso RAIZ de la cadena manda; si ninguna coincide se usan los valores
    /// generales (`CySpringHairBoneRadiusBase` / `...Tip`).
    /// </summary>
    [System.Serializable]
    public class CySpringHairRadiusOverride
    {
        [Tooltip("Texto que debe CONTENER el hueso raiz de la cadena (p. ej. 'Sp_He_Hair2').")]
        public string BoneFilter = "Sp_He_Hair2";
        public bool Enabled = true;
        [Tooltip("Radio (m) en la RAIZ del mechon.")]
        public float RadiusBase = 0.0375f;
        [Tooltip("Radio (m) en la PUNTA del mechon.")]
        public float RadiusTip = 0.0225f;
    }

    [Tooltip("Grosor por cadena. Sirve para que las coletas DELANTERAS (Sp_He_Hair2) tengan un radio\n" +
             "propio distinto del pelo de atras: la primera entrada que coincida con el hueso raiz manda.")]
    public List<CySpringHairRadiusOverride> CySpringHairBoneRadiusOverrides = new List<CySpringHairRadiusOverride>
    {
        new CySpringHairRadiusOverride()
    };

    [Header("CySpring - ESCAPE: correccion propia DESPUES del solver nativo")]
    [Tooltip("El solver nativo empuja la PUNTA del hueso a la superficie de la esfera y DESPUES la\n" +
             "reproyecta al largo del hueso; si la esfera cubre tambien el lado opuesto, la punta vuelve\n" +
             "a caer dentro y la cadena queda ATRAPADA (se ve el brazo atravesando el pelo, y AGRANDAR la\n" +
             "esfera lo empeora). Esta pasada, ya terminada la simulacion, gira el hueso lo MINIMO\n" +
             "necesario para que su punta salga (ley del coseno); si no hay salida posible la deja en la\n" +
             "direccion de maximo alejamiento y lo cuenta en consola.")]
    public bool CySpringHairEscapeFix = true;
    [Tooltip("(DIAG) Loguea cada 60 frames cuantos huesos ha rescatado el escape y el giro maximo.")]
    public bool CySpringHairEscapeDiagnose = true;

    private int _escapeCorrections;
    private int _escapeImpossible;
    private float _escapeMaxAngle;
    private int _escapeFrameCounter;

    [Header("CySpring - SONDAS LATERALES del mechon (ancho, no profundidad)")]
    [Tooltip("El motor solo tiene UNA esfera isotropa por hueso (centrada en la punta), asi que su\n" +
             "'ancho' y su 'profundidad' son el mismo radio: engordarlo empuja el mechon en TODAS las\n" +
             "direcciones (hacia arriba incluido) en vez de apartar solo el lado que toca. Con esto la\n" +
             "pasada de escape prueba ademas puntos LATERALES virtuales del hueso (sin crear huesos reales\n" +
             "ni tocar el rig ni la malla) y gira el hueso lo minimo para que salga el lado que toca.")]
    public bool CySpringHairProbeEnabled = true;
    [Tooltip("Puntos laterales por hueso (ademas de la punta). 2 = uno a cada lado.")]
    public int CySpringHairProbeCount = 2;
    [Tooltip("Distancia (m) de cada sonda al eje del hueso: el ANCHO que quieres cubrir.")]
    public float CySpringHairProbeOffset = 0.04f;
    [Tooltip("Cuantos huesos CON Malla desde la punta llevan sondas. 1 (por defecto) = solo el nivel mas\n" +
             "profundo de la jerarquia; 2 añadiria tambien el tramo anterior.")]
    public int CySpringHairProbeLevelsFromTip = 1;
    [Tooltip("Radio efectivo de las sondas LATERALES, como fraccion del radio propio del hueso (1 = igual).\n" +
             "Son puntos de test independientes del hueso, asi que pueden ser mas finos que el: con 0,50 el\n" +
             "centro mantiene el grosor del pelo y los laterales son la mitad de gruesos.")]
    public float CySpringHairProbeRadiusScale = 0.5f;
    [Tooltip("Solo las cadenas cuyo hueso raiz CONTENGA alguno de estos textos. Por defecto los tres\n" +
             "mechones de prueba: Hair4_R, Hair4_L y Hair4_C (el central del trasero).")]
    public string[] CySpringHairProbeBoneFilters = new string[]
    {
        "Sp_He_Hair4_R",
        "Sp_He_Hair4_L",
        "Sp_He_Hair4_C"
    };
    [Tooltip("Distancia (m) a lo largo del mechon DESDE la referencia de abajo: NEGATIVO la sube hacia el\n" +
             "hueso, POSITIVO la baja mas alla de la punta. Con 0 queda en la referencia.")]
    public float CySpringHairProbeAlongOffset = 0.0f;
    [Tooltip("Referencia de la fila de sondas:\n" +
             "  ENCENDIDO (por defecto) = extremo visible de la MALLA (lo mas abajo del mechon).\n" +
             "  APAGADO = posicion del nivel mas profundo de la JERARQUIA (el hueso).")]
    public bool CySpringHairProbeAtMeshEnd = true;
    [Tooltip("Mide el EJE DE ANCHO de la malla (autovector principal de los vertices que domina cada\n" +
             "hueso, en el plano perpendicular al mechon). Recomendado: el ancho real no es una suposicion.")]
    public bool CySpringHairProbeAxisFromMesh = true;
    [Tooltip("Eje de ancho MANUAL (local al hueso) si no se mide de la malla. (1,0,0) = X local.")]
    public Vector3 CySpringHairProbeAxisOverride = new Vector3(1.0f, 0.0f, 0.0f);

    // Eje de ANCHO medido por hueso (local), y cache de posiciones de sonda para los gizmos.
    private readonly Dictionary<Transform, Vector3> _probeAxisLocal = new Dictionary<Transform, Vector3>();
    private readonly HashSet<Transform> _measuredProbeBones = new HashSet<Transform>();
    // Medicion de MALLA por hueso: cuantos vertices domina (0 = hueso "fantasma": girarlo no mueve
    // nada visible) y hasta donde llega su malla a lo largo del mechon (Z local).
    private readonly Dictionary<Transform, int> _probeMeshVertCount = new Dictionary<Transform, int>();
    private readonly Dictionary<Transform, float> _probeMeshZMax = new Dictionary<Transform, float>();
    private CySpringHairProbeVisualizer _probeVisualizer;
    private readonly List<Vector3> _probeWorldCache = new List<Vector3>();
    private readonly List<float> _probeRadiusCache = new List<float>();
    // GameObjects vacios (uno por sonda) para poder verlas/seleccionarlas en la jerarquia.
    private readonly Dictionary<Transform, Dictionary<int, Transform>> _probeObjects =
        new Dictionary<Transform, Dictionary<int, Transform>>();

    private BipedIK IK;
    private List<Transform> _humanoidBones;
    private UIHandleCharacterRoot handleRoot;

    // Official CySpring runtime controller.
    // CySpringDataContainer is only data; CySpringController owns simulation.
    private CySpringController _cySpringController;
    private bool _cySpringLoaded;
    private SkirtController _skirtController;
    private CySpringOwner _cySpringOwner;
    private List<UmaDatabaseEntry> LoadedAssets = new List<UmaDatabaseEntry>();

    public class TextureList
    {
        public class TextureSource
        {
            public Texture2D Texture;
            public UmaDatabaseEntry Entry;
            public bool Used = false;

            public TextureSource(Texture2D texture, UmaDatabaseEntry entry)
            {
                Texture = texture;
                Entry = entry;
            }
        }

        private List<TextureSource> _list = new List<TextureSource>();

        public Texture2D Load(Func<Texture2D, bool> predicate)
        {
            var entry = _list.FirstOrDefault(t => predicate(t.Texture));
            if (entry == null)
            {
                Debug.LogWarning("Texture not found");
                return null;
            }
            entry.Used = true;
            return entry.Texture;
        }

        public Texture2D LoadLast(Func<Texture2D, bool> predicate)
        {
            var entry = _list.LastOrDefault(t => predicate(t.Texture));
            entry.Used = true;
            return entry.Texture;
        }

        public Texture2D Load(int index)
        {
            var entry = _list[index];
            entry.Used = true;
            return entry.Texture;
        }

        public void Add(Texture2D texture, UmaDatabaseEntry entry)
        {
            _list.Add(new TextureSource(texture, entry));
        }

        public IEnumerable<UmaDatabaseEntry> GetLoadedAssets()
        {
            return _list.Select(t => t.Entry).Distinct();
        }
    }

    public void Initialize(bool smile)
    {
        TrackTarget = new GameObject("TrackTarget").transform;
        TrackTarget.SetParent(transform);
        TrackTarget.position = HeadBone.transform.TransformPoint(0, 0, 10);

        UpBodyPosition = UpBodyBone.transform.localPosition;
        UpBodyRotation = UpBodyBone.transform.localRotation;

        // 此时已完成模型合并和身高缩放，且预览动画尚未加载，适合作为稳定的导出参考姿势。
        CaptureInitialBodyPose();

        //Models must be merged before handling extra morphs
        if (FaceDrivenKeyTarget && smile)
            FaceDrivenKeyTarget.ChangeMorphWeight(FaceDrivenKeyTarget.MouthMorphs[3], 1);

        CreateIK();
        SetShaderParameterRuntime();
    }

    public void MergeModel()
    {
        if (!Body) return;
        RestoreMirrorChangedLayers();
        // Capture source identity before MergeModel reparents Head/Hair children.
        RegisterMirrorHeadRenderers(Head);
        RegisterMirrorHeadRenderers(Hair);
        var bodySkinnedMeshRenderer = Body.GetComponentInChildren<SkinnedMeshRenderer>();
        var bodyBones = bodySkinnedMeshRenderer.bones.ToDictionary(bone => bone.name, bone => bone.transform);
        List<Transform> emptyBones = new List<Transform>();
        emptyBones.Add(Body.transform.Find("Position/Hip/Tail_Ctrl"));
        while (Body.transform.childCount > 0)
        {
            Body.transform.GetChild(0).SetParent(transform);
        }
        Body.SetActive(false); //for debugging

        //MergeHead
        if (Head)
        {
            var headskins = Head.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer headskin in headskins)
            {
                MergeBone(headskin, bodyBones, ref emptyBones);
            }
            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(transform);
            while (Head.transform.childCount > 0)
            {
                var child = Head.transform.GetChild(0);
                child.SetParent(child.name.Contains("info") ? eyes.transform : transform);
            }
            Head.SetActive(false); //for debugging
        }

        //MergeHair
        if (IsMini && Hair)
        {
            var hairskin = Hair.GetComponentInChildren<SkinnedMeshRenderer>();
            MergeBone(hairskin, bodyBones, ref emptyBones);
            while (Hair.transform.childCount > 0)
            {
                var child = Hair.transform.GetChild(0);
                child.SetParent(transform);
            }
            Hair.SetActive(false); //for debugging
        }

        //MergeTail
        if (Tail)
        {
            var tailskin = Tail.GetComponentInChildren<SkinnedMeshRenderer>();
            MergeBone(tailskin, bodyBones, ref emptyBones);
            while (Tail.transform.childCount > 0)
            {
                var child = Tail.transform.GetChild(0);
                child.SetParent(transform);
            }
            Tail.SetActive(false); //for debugging
        }


        emptyBones.ForEach(a => { if (a) Destroy(a.gameObject); });

        //MergeAvatar
        UmaAnimator = gameObject.AddComponent<Animator>();
        UmaAnimator.avatar = AvatarBuilder.BuildGenericAvatar(gameObject, gameObject.name);
        OverrideController = Instantiate(UmaViewerBuilder.Instance.OverrideController);
        UmaAnimator.runtimeAnimatorController = OverrideController;

        //Materials
        Renderers = gameObject.GetComponentsInChildren<Renderer>().ToList();
        FaceRenderer = Renderers.FirstOrDefault(r => r != null && r.name == "M_Face");

        if (FaceRenderer != null)
        {
            _faceMaterials = FaceRenderer.materials;

            foreach (var mat in _faceMaterials)
            {
                if (mat == null || mat.shader == null)
                    continue;

                string matName = mat.name.ToLower();
                string shaderName = mat.shader.name;

                if (shaderName.Contains("ToonFace") || matName.Contains("face"))
                {
                    FaceMaterial = mat;
                    break;
                }
            }
        }
        foreach (var rend in Renderers)
        {
            for (int i = 0; i < rend.sharedMaterials.Length; i++)
            {
                Material mat = rend.sharedMaterials[i];

                var matHlp = Materials.FirstOrDefault(m => m.Mat == mat);
                if (matHlp == null)
                {
                    matHlp = new MaterialHelper()
                    {
                        Mat = mat,
                        Renderers = new Dictionary<Renderer, List<int>>(),
                        Toggle = Instantiate(UI.UmaContainerTogglePrefab, UI.ModelSettings.MaterialsList.content)
                    };
                    matHlp.Toggle.Name = mat.name;
                    matHlp.Toggle.Toggle.onValueChanged.AddListener((value) => { matHlp.ToggleMaterials(value); });
                    Materials.Add(matHlp);
                }

                if (!matHlp.Renderers.ContainsKey(rend))
                {
                    matHlp.Renderers.Add(rend, new List<int> { i });
                }
                else
                {
                    matHlp.Renderers[rend].Add(i);
                }
            }
        }
        InvalidateMirrorLayerCache();
        PrepareMirrorLayerCache();

    }

    public void MergeHairModel()
    {
        if (!Head || !Hair) return;
        RestoreMirrorChangedLayers();
        RegisterMirrorHeadRenderers(Hair);

        var bodyBones = Head.GetComponentInChildren<SkinnedMeshRenderer>().bones.ToDictionary(bone => bone.name, bone => bone.transform);
        List<Transform> emptyBones = new List<Transform>();

        var headHolder = Head.GetComponent<AssetHolder>();
        var hairHolder = Hair.GetComponent<AssetHolder>();

        headHolder._assetTable.list.AddRange(hairHolder._assetTable.list);
        headHolder._assetTableValue.list.AddRange(hairHolder._assetTableValue.list);

        //MergeHair
        var hairskins = Hair.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer hairskin in hairskins)
        {
            hairskin.gameObject.transform.SetParent(Head.transform);
            MergeBone(hairskin, bodyBones, ref emptyBones);
        }
        Hair.gameObject.SetActive(false);

        emptyBones.ForEach(a => { if (a) Destroy(a.gameObject); });
        InvalidateMirrorLayerCache();
    }

    public void SetHeight(int scale = 0)
    {
        if (scale == 0)
        {
            BodyScale = 1;
        }
        else if (scale == -1)
        {
            BodyScale = (Convert.ToInt32(CharaData[IsMob ? "chara_height" : "scale"]) / 160.7529f);
        }
        else
        {
            BodyScale = (scale / 160.7529f);
        }
        transform.Find("Position").localScale = new Vector3(BodyScale, BodyScale, BodyScale);


    }

    public void MergeBone(SkinnedMeshRenderer from, Dictionary<string, Transform> targetBones, ref List<Transform> emptyBones)
    {
        if (targetBones.TryGetValue(from.rootBone.name, out Transform rootbone))
        {
            from.rootBone = rootbone;
            Transform[] tmpBone = new Transform[from.bones.Length];
            for (int i = 0; i < tmpBone.Length; i++)
            {
                if (targetBones.TryGetValue(from.bones[i].name, out Transform targetbone))
                {
                    tmpBone[i] = targetbone;
                    from.bones[i].position = targetbone.position;
                    while (from.bones[i].transform.childCount > 0)
                    {
                        from.bones[i].transform.GetChild(0).SetParent(targetbone);
                    }
                    emptyBones.Add(from.bones[i]);
                }
                else
                {
                    tmpBone[i] = from.bones[i];
                }
            }
            from.bones = tmpBone;
        }
        ;
    }

    private sealed class UmaViewerCySpringOwner : CySpringOwner
    {
        private readonly UmaContainerCharacter _chara;

        public UmaViewerCySpringOwner(UmaContainerCharacter chara)
            : base(null)
        {
            _chara = chara;
        }

        public override bool IsCharaModel()
        {
            return true;
        }

        public override int GetCharaID()
        {
            return _chara != null && _chara.CharaEntry != null ? _chara.CharaEntry.Id : 0;
        }

        public override int GetDressID()
        {
            return 0;
        }

        public override int GetHeadID()
        {
            return 0;
        }

        public override float GetCySpringCorrectScale(bool isHead)
        {
            if (_chara == null || _chara.BodyScale <= 0.0f)
                return 1.0f;

            return _chara.BodyScale;
        }

        public override float GetBodyScale()
        {
            if (_chara == null || _chara.BodyScale <= 0.0f)
                return 1.0f;

            return _chara.BodyScale;
        }

        public override float GetTotalScale()
        {
            if (_chara == null || _chara.BodyScale <= 0.0f)
                return 1.0f;

            return _chara.BodyScale;
        }
    }

    public void LoadPhysics()
    {
        if (IsMini)
            return;

        if (PhysicsContainer == null)
        {
            Debug.LogWarning("[UmaContainerCharacter] PhysicsContainer is null.");
            return;
        }

        cySpringDataContainers = new List<CySpringDataContainer>(
            PhysicsContainer.GetComponentsInChildren<CySpringDataContainer>(true)
        );

        if (cySpringDataContainers == null || cySpringDataContainers.Count == 0)
        {
            Debug.LogWarning("[UmaContainerCharacter] No CySpringDataContainer found.");
            return;
        }

        // Física de ropa/pelo: DOS vías, una por tipo de prenda.
        //
        // - DynamicBone: pelo, orejas y cola. Funciona SIN CySpringPlugin.dll y es el
        //   comportamiento de cabello/cola que se quiere conservar.
        // - Puente nativo CySpringController + CySpringPlugin.dll: body (falda/ropa) y bust
        //   (ropa interior). Es lo que arregló la ropa al añadir la DLL.
        //
        // Se reparten por tipo para que no se peleen por los mismos huesos: DynamicBone se
        // queda con head (pelo/orejas) y tail (cola); el puente nativo con body y bust.
        //
        // OJO con el bust: en el flujo oficial el contenedor de bust NO se carga solo, entra
        // como asset SECUNDARIO de la secuencia de HEAD (main=head, add=bust). Por eso, si el
        // pelo se lleva a DynamicBone y head deja de entrar al nativo, CySpringController
        // carga el bust como principal para que la ropa interior no se quede sin física
        // (ver LoadHeadCollisionParamSequence / LoadHeadSpringParamSequence).
        var dbBones = new Dictionary<string, Transform>();
        foreach (var bone in GetComponentsInChildren<Transform>())
        {
            if (bone == null || string.IsNullOrEmpty(bone.name)) continue;
            if (!dbBones.ContainsKey(bone.name))
                dbBones.Add(bone.name, bone);
        }

        var dbColliders = new Dictionary<string, Transform>();

        // Contenedores que va a simular el puente nativo (ropa), y que por tanto NO deben
        // recibir DynamicBone.
        var nativeContainers = new List<CySpringDataContainer>();

        // (DATAMINE) El juego lleva por CySpring el pelo y la tela; aquí el "native kind" decide
        // que contenedores simula el puente nativo y por tanto NO reciben DynamicBone.
        // kind: 0 = head(pelo) · 1 = body(ropa) · 2 = bust · 3 = tail.
        System.Func<int, bool> isNativeKind = (k) => k == 1 || k == 2 || (CySpringHairViaNative && k == 0);

        for (int i = 0; i < cySpringDataContainers.Count; i++)
        {
            CySpringDataContainer container = cySpringDataContainers[i];
            if (container == null) continue;

            int kind = GuessCySpringContainerKind(container);

            // Los contenedores de ropa (y, opcionalmente, el pelo) los lleva el puente nativo
            // (que ya tiene su propia colisión), así que no reciben DynamicBone.
            if (isNativeKind(kind))
                nativeContainers.Add(container);

            // (P1, COLISIÓN) Los colisores se crean de TODOS los contenedores, no solo de
            // los que simula DynamicBone. Motivo MEDIDO: los dos colisores que la cola
            // necesita (Col_B_Hip_Tail y Col_B_Chest_Tail) están definidos en el
            // contenedor de la ROPA (pfb_bdy0002_00_cloth00) y el contenedor de la cola
            // tiene collisionParam VACÍO (n=0). Antes los contenedores de ropa hacían
            // `continue` y nunca llegaban a InitiallizeCollider, así que esos dos
            // colisores no se creaban y la cola se atravesaba el cuerpo y la ropa:
            // medido 0 colisores en las 5 partículas de la cola. El pelo sí los tenía
            // (3 por partícula) porque su contenedor sí pasaba por aquí.
            foreach (var kv in container.InitiallizeCollider(dbBones))
            {
                if (!dbColliders.ContainsKey(kv.Key)) dbColliders.Add(kv.Key, kv.Value);
            }
        }

        for (int i = 0; i < cySpringDataContainers.Count; i++)
        {
            CySpringDataContainer container = cySpringDataContainers[i];
            if (container == null) continue;

            int kind = GuessCySpringContainerKind(container);
            if (isNativeKind(kind)) continue;

            container.InitializePhysics(dbBones, dbColliders);
        }

        _cySpringLoaded = cySpringDataContainers.Count > 0;

        // Puente nativo SOLO para la ropa. Si no hay contenedores de ropa, no se monta (el
        // pelo ya está cubierto por DynamicBone y no necesita la DLL).
        if (nativeContainers.Count == 0)
            return;

        Dictionary<string, Transform> transformCacheDic =
            BuildCySpringTransformCache(transform, out HashSet<string> duplicateBoneNames);

        if (duplicateBoneNames.Count > 0)
        {
            Debug.LogWarning(
                $"[UmaContainerCharacter] Duplicate bone names detected before CySpring bind: {duplicateBoneNames.Count}\n" +
                $"{string.Join("\n", duplicateBoneNames.Take(30))}"
            );
        }

        Transform hip = FindCySpringTransform(transform, "Hip");

        if (hip == null && UpBodyBone != null)
            hip = UpBodyBone.transform;

        if (hip == null)
            hip = transform.Find("Position/Hip");

        if (hip == null)
        {
            Debug.LogWarning("[UmaContainerCharacter] Hip bone not found. CySpring will use character root as fallback.");
            hip = transform;
        }

        // (P1) Solo la ROPA entra al puente nativo: el pelo y la cola ya los simula DynamicBone
        // y meterlos aquí también los deformaba.
        var slots = SplitCySpringContainers(nativeContainers);

        // La falda declara su volumen de sector de cadera (Col_B_Hip_MSkirt_*) SOLO en el hueso
        // raiz de cada cadena; los huesos hijos, que son los que estan a la altura del gluteo,
        // nombran unicamente las capsulas del muslo. Sin el volumen de cadera en los hijos la
        // tela se puede plegar hacia dentro y el cuerpo asoma por detrás. Se unen los nombres
        // de la raiz a los hijos para que toda la cadena colisione igual.
        PropagateClothCollisionNames(slots.body);


        // (SOLO PELO NATIVO) Todo el andamiaje de colisores y límites del pelo pertenece al
        // camino nativo (CySpringPlugin.dll). Con el pelo en DynamicBone (CySpringHairViaNative
        // off, por defecto) no se ejecuta nada de esto: el pelo lo resuelve DynamicBone.
        if (CySpringHairViaNative)
        {
            // =================================================================================
            // COLISORES DEL PELO (camino nuevo). Como funciona el motor (datamine 04 §1.1): una
            // cadena de pelo solo sufre el empuje de los colisores que SU lista de nombres declara,
            // y el choque efectivo es R = colider.Radius + hueso.CollisionRadius. Los colisores que
            // trae el juego (Col_Arm_L_Hair / Col_Elbow_* / Col_Wrist_*) estaban pensados para OTRA
            // geometria y se escalaban TODOS con un unico factor, que aplica el mismo radio a
            // geometrias distintas.
            //
            // Aqui se generan esferas propias: pegadas unas a otras a lo largo del tramo
            // hombro->codo, de radio ~grosor del brazo, de modo que el pelo se desliza sobre un
            // "tubo" y no recibe golpes sueltos. Se reemplaza la lista de TODO el pelo (raiz e
            // hijos, que declaran listas propias) por las esferas generadas.
            //
            // (PASO 1) crear las esferas en el contenedor del pelo.
            // (PASO 2) asignarlas a todas las cadenas de pelo (delantero Y trasero).
            // La colocacion (posicion y radio reales) se hace DESPUES del bind, en
            // ApplyArmColliderLayout(): el motor solo lee los colisores del array nativo.
            // =================================================================================
            BuildArmColliderGrid(slots.head, transformCacheDic);
            BindGeneratedCollidersToHair(slots.head);

            // (INTENTO 10, EN CODIGO) Desactivar IsLimit SOLO en los resortes del PELO, antes del bind.
            // Medido (consola [DiagPelo]): los sidelocks Sp_He_Hair2_L/R_00 tienen IsLimit=1 con
            // min=(150,50,180) max=(90,10,180); el motor recorta la rotacion relativa al reposo a
            // x[-150,+90] y[-50,+10] z[-180,+180] y recomienda TargetPosition desde ahi, dejando la
            // coleta clavada/torcida. El resto del pelo y las orejas ya van con IsLimit=0.
            if (CySpringDisableHairLimits && slots.head != null && slots.head.springParam != null)
            {
                for (int i = 0; i < slots.head.springParam.Count; i++)
                {
                    Gallop.CySpringParamDataElement sp = slots.head.springParam[i];
                    if (sp == null) continue;

                    sp._isLimit = false;

                    if (sp._childElements != null)
                    {
                        for (int k = 0; k < sp._childElements.Count; k++)
                        {
                            if (sp._childElements[k] != null)
                                sp._childElements[k]._isLimit = false;
                        }
                    }
                }
            }
        }

        _cySpringOwner = new UmaViewerCySpringOwner(this);
        _cySpringController = CySpringController.AddController(gameObject, hip, _cySpringOwner);

        if (_cySpringController == null)
        {
            Debug.LogError("[UmaContainerCharacter] Failed to create CySpringController.");
            _cySpringLoaded = false;
            return;
        }

        _cySpringController.LoadFromDataContainers(
            transformCacheDic,
            slots.head,
            slots.body,
            slots.bust,
            slots.tail,
            null,
            null,
            null,
            BodyScale,
            BodyScale,
            BodyScale
        );

        _cySpringController.Reset();
        _cySpringLoaded = true;

        // (INTENTO 9 / DIAG) Coherencia del "reposo" de las cadenas del pelo. El motor
        // (CySpring_SolveBone @180009760) calcula Aim = (ParentRotation*InitLocalRotation)*BoneAxis
        // y FinalRotation = quatFromTo(Aim, dir)*qg. Debe cumplirse
        // Aim ~= normalize(hijo.pos - hueso.pos); si no, el solver arranca con la cadena torcida.
        if (CySpringDiagnoseHair && CySpringHairViaNative)
        {
            CySpring headSpring = _cySpringController.GetSpringArray(0);
            if (headSpring != null)
                headSpring.DiagLogRestCoherence("DIAG");
        }

        if (CySpringHairViaNative)
        {
            // (PASO 3) Capturar el RUNTIME de colisiones del pelo (nuestras esferas existen ya tras
            // el bind) y COLOCARLAS. Se hace ANTES del warm-up: el warm-up ya simula, y con las
            // esferas sin colocar (apiladas en el origen del hueso) el pelo arrancaria deformado. A
            // partir de aqui ApplyArmColliderLayout() reacciona EN CALIENTE a los cambios del Inspector.
            _hairRuntimeCollisions = _cySpringController.GetCollisionRuntimeList(0);
            _lastArmRadius = -1.0f;
            _lastArmCount = -1;
            _lastArmOverlap = -1.0f;
            AttachHairColliderGizmos();
            ApplyArmColliderLayout(true);
            ApplyHairBoneRadiusLayout(true);
            MeasureProbeAxes();
            UpdateHairBoneVisualizers();
        }

        // (DATAMINE/P1) Warm-up oficial: el juego asienta la cadena antes de mostrarla
        // (CharaPropController.ReserveWarmingUpCySpring + TryUpdateWarmUp). Sin esto la pose
        // simulada arranca "clavada" en la de carga.
        if (CySpringWarmUpTime > 0.0f)
        {
            _cySpringController.ReserveWarmingUpCySpring(CySpringWarmUpTime);
            _cySpringController.TryUpdateWarmUp();
        }

        LinkSkirtControllerToCySpring();
    }

    private Dictionary<string, Transform> BuildCySpringTransformCache(Transform root, out HashSet<string> duplicateBoneNames)
    {
        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        duplicateBoneNames = new HashSet<string>();

        if (root == null)
            return bones;

        Transform[] allBones = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < allBones.Length; i++)
        {
            Transform bone = allBones[i];

            if (bone == null || string.IsNullOrEmpty(bone.name))
                continue;

            if (!bones.ContainsKey(bone.name))
            {
                bones.Add(bone.name, bone);
            }
            else
            {
                duplicateBoneNames.Add(bone.name);
            }
        }

        return bones;
    }

    private Transform FindCySpringTransform(Transform root, string boneName)
    {
        if (root == null || string.IsNullOrEmpty(boneName))
            return null;

        Transform[] allBones = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < allBones.Length; i++)
        {
            if (allBones[i] != null && allBones[i].name == boneName)
                return allBones[i];
        }

        return null;
    }

    private void LinkSkirtControllerToCySpring()
    {
        if (_cySpringController == null || _skirtController == null)
            return;

    _cySpringController.CreateLinkSkirtController(_skirtController);
    }

    // (PASO 4) GROSOR DEL PELO: reparte un gradiente de radio PROPIO por cada cadena de pelo
    // (nacimiento -> punta), en las esferas CIAN del gizmo.
    //
    // Por que: el motor separa por `R = colisor.Radius + hueso.CollisionRadius` y coloca el HUESO (la
    // linea central del mechon) a esa distancia. El radio que trae el asset para el pelo es de
    // 0.025-0.045, pero la MALLA del mechon es MUCHO mas ancha: medido en vivo, los vertices que
    // domina cada hueso llegan a 0.09-0.19 m del eje del hueso (media 0.06-0.13). Con esa diferencia
    // el pelo se ve atravesar el brazo aunque la esfera rosa y la cian se solapen: la colision esta
    // funcionando, pero el volumen que representa el pelo es mucho menor que el que se ve.
    //
    // El motor LEE `CollisionRadius` en cada frame (`CySpring_ResolveBoneCollisions`, offset 0xCC),
    // asi que esto se puede ajustar EN CALIENTE sin rehacer el bind ni el array de colisiones.
    private void ApplyHairBoneRadiusLayout(bool force)
    {
        if (_cySpringController == null || !CySpringHairBoneRadiusEnabled)
            return;

        CySpring headSpring = _cySpringController.GetSpringArray(0);
        CySpringRootBone[] roots = headSpring != null ? headSpring.RootBoneArray : null;
        if (roots == null)
            return;

        for (int i = 0; i < roots.Length; i++)
        {
            CySpringRootBone r = roots[i];
            if (r == null || string.IsNullOrEmpty(r.BoneName))
                continue;

            if (r.BoneName.IndexOf("Hair", StringComparison.Ordinal) < 0)
                continue;   // las orejas y demas no se tocan

            // Grosor: el general, o el de ESTA cadena si hay un override que coincida.
            float baseRadius = CySpringHairBoneRadiusBase;
            float tipRadius = CySpringHairBoneRadiusTip;

            CySpringHairRadiusOverride radiusOverride = FindHairRadiusOverride(r.BoneName);
            if (radiusOverride != null)
            {
                baseRadius = radiusOverride.RadiusBase;
                tipRadius = radiusOverride.RadiusTip;
            }

            List<CySpringBoneBase> bl = new List<CySpringBoneBase>();
            bl.Add(r);
            if (r.ChildBoneList != null) bl.AddRange(r.ChildBoneList);

            for (int b = 0; b < bl.Count; b++)
            {
                CySpringBoneBase bone = bl[b];
                if (bone == null)
                    continue;

                float t = bl.Count <= 1 ? 0.0f : (float)b / (bl.Count - 1);
                bone.CollisionRadius = Mathf.Lerp(baseRadius, tipRadius, t);
            }
        }

        // (FIX) Los gizmos cian leen el radio nativo: hay que refrescarlos al cambiar el valor, o el
        // ajuste surte efecto en el motor pero no se ve en la Scene view.
        UpdateHairBoneVisualizers();
    }

    // Primer override de grosor que coincide con el hueso RAIZ de la cadena (o null).
    private CySpringHairRadiusOverride FindHairRadiusOverride(string boneName)
    {
        if (CySpringHairBoneRadiusOverrides == null || string.IsNullOrEmpty(boneName))
            return null;

        for (int i = 0; i < CySpringHairBoneRadiusOverrides.Count; i++)
        {
            CySpringHairRadiusOverride candidate = CySpringHairBoneRadiusOverrides[i];
            if (candidate == null || !candidate.Enabled || string.IsNullOrEmpty(candidate.BoneFilter))
                continue;

            if (boneName.IndexOf(candidate.BoneFilter, StringComparison.Ordinal) >= 0)
                return candidate;
        }

        return null;
    }

    // ---- SONDAS LATERALES DEL MECHON --------------------------------------------------------
    //
    // El motor solo tiene UNA esfera isotropa por hueso (R = colisor + radio del hueso, centrada en la
    // punta), asi que su "ancho" y su "profundidad" son el mismo radio: engordarlo empuja el mechon en
    // TODAS las direcciones (hacia arriba incluido) en lugar de apartar solo el lado que toca.
    //
    // Solucion: probar puntos LATERALES VIRTUALES del hueso. No son huesos nuevos (no hace falta
    // tocar el rig ni repesar la malla) sino puntos rigidos al hueso: si uno cae dentro de un colisor
    // se gira el hueso lo minimo para sacarlo, y asi el mechon se aparta POR EL LADO QUE TOCA.
    //
    // El "ancho" se mide de la propia malla: por cada hueso se toman los vertices que domina (via
    // bindposes, independiente de la pose), se proyectan al plano perpendicular al mechon y se saca el
    // autovector principal de la covarianza 2x2. Ese es el eje del ancho real del mechon.

    private bool PassesProbeFilter(string boneName)
    {
        if (CySpringHairProbeBoneFilters == null || CySpringHairProbeBoneFilters.Length == 0)
            return true;

        if (boneName == null)
            return false;

        for (int i = 0; i < CySpringHairProbeBoneFilters.Length; i++)
        {
            string filter = CySpringHairProbeBoneFilters[i];
            if (string.IsNullOrEmpty(filter))
                continue;

            if (boneName.IndexOf(filter, StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    private void MeasureProbeAxes()
    {
        _probeAxisLocal.Clear();
        _probeMeshVertCount.Clear();
        _probeMeshZMax.Clear();

        if (!CySpringHairProbeAxisFromMesh || _cySpringController == null)
            return;

        CySpring headSpring = _cySpringController.GetSpringArray(0);
        CySpringRootBone[] roots = headSpring != null ? headSpring.RootBoneArray : null;
        if (roots == null)
            return;

        List<Transform> candidates = new List<Transform>();
        for (int i = 0; i < roots.Length; i++)
        {
            CySpringRootBone r = roots[i];
            if (r == null || string.IsNullOrEmpty(r.BoneName)) continue;
            if (!PassesProbeFilter(r.BoneName)) continue;

            if (r.Transform != null) candidates.Add(r.Transform);

            if (r.ChildBoneList != null)
            {
                for (int k = 0; k < r.ChildBoneList.Count; k++)
                {
                    CySpringBoneBase cb = r.ChildBoneList[k];
                    if (cb != null && cb.Transform != null) candidates.Add(cb.Transform);
                }
            }
        }

        if (candidates.Count == 0)
            return;

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        for (int ci = 0; ci < candidates.Count; ci++)
        {
            Transform bt = candidates[ci];
            if (bt == null) continue;

            // Direccion del mechon en ese hueso: hacia su hijo; si es la punta, desde su padre.
            // (Se ignoran los GameObjects de sonda, que son hijos nuestros.)
            Vector3 dirW = Vector3.zero;
            for (int k = 0; k < bt.childCount; k++)
            {
                Transform ch = bt.GetChild(k);
                if (ch == null || ch.name.StartsWith("Sonda_", StringComparison.Ordinal))
                    continue;

                dirW = ch.position - bt.position;
                break;
            }

            if (dirW.sqrMagnitude < 1e-10f && bt.parent != null)
                dirW = bt.position - bt.parent.position;

            if (dirW.sqrMagnitude < 1e-10f)
                continue;

            Vector3 dirL = bt.InverseTransformDirection(dirW.normalized);

            // Base perpendicular al mechon (en local).
            Vector3 u = Vector3.Cross(dirL, Vector3.up);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.Cross(dirL, Vector3.right);
            if (u.sqrMagnitude < 1e-6f) continue;
            u.Normalize();
            Vector3 w = Vector3.Cross(dirL, u).normalized;

            float sx = 0f, sy = 0f, sxx = 0f, syy = 0f, sxy = 0f;
            float zMax = 0f;
            int n = 0;

            for (int si = 0; si < renderers.Length; si++)
            {
                SkinnedMeshRenderer smr = renderers[si];
                if (smr == null) continue;

                Mesh mesh = smr.sharedMesh;
                if (mesh == null) continue;

                Transform[] bones = smr.bones;
                Matrix4x4[] bind = mesh.bindposes;
                if (bones == null || bind == null || bones.Length != bind.Length) continue;

                int bi = -1;
                for (int k = 0; k < bones.Length; k++)
                {
                    if (bones[k] == bt) { bi = k; break; }
                }
                if (bi < 0) continue;

                Vector3[] verts = mesh.vertices;
                BoneWeight[] bw = mesh.boneWeights;
                if (bw == null || bw.Length != verts.Length) continue;

                for (int v = 0; v < verts.Length; v++)
                {
                    BoneWeight weight = bw[v];
                    bool dom0 = weight.weight0 >= weight.weight1;
                    if (dom0 && (int)weight.boneIndex0 != bi) continue;
                    if (!dom0 && (int)weight.boneIndex1 != bi) continue;

                    Vector3 p = bind[bi].MultiplyPoint3x4(verts[v]);
                    float px = Vector3.Dot(p, u);
                    float py = Vector3.Dot(p, w);
                    sx += px; sy += py; sxx += px * px; syy += py * py; sxy += px * py;

                    // Hasta donde llega la malla a lo largo del mechon (eje local del hueso).
                    float pz = Vector3.Dot(p, dirL);
                    if (pz > zMax) zMax = pz;

                    n++;
                }
            }

            // Se guardan SIEMPRE (aunque haya pocos vertices): el recuento decide si el hueso tiene
            // peso de malla (los "fantasma" no mueven nada y no deben llevar sondas).
            _probeMeshVertCount[bt] = n;
            _probeMeshZMax[bt] = zMax;

            if (n < 6)
                continue;

            float mx = sx / n, my = sy / n;
            float cxx = sxx / n - mx * mx;
            float cyy = syy / n - my * my;
            float cxy = sxy / n - mx * my;

            // Autovector principal de la covarianza 2x2 = direccion en la que el mechon es mas ancho.
            float theta = 0.5f * Mathf.Atan2(2.0f * cxy, cxx - cyy);
            Vector3 axisLocal = (u * Mathf.Cos(theta) + w * Mathf.Sin(theta));
            if (axisLocal.sqrMagnitude < 1e-8f)
                continue;

            _probeAxisLocal[bt] = axisLocal.normalized;
        }

    }

    private Vector3 GetProbeAxisLocal(CySpringBoneBase bone)
    {
        if (bone != null && bone.Transform != null && CySpringHairProbeAxisFromMesh)
        {
            Vector3 axis;
            if (_probeAxisLocal.TryGetValue(bone.Transform, out axis) && axis.sqrMagnitude > 1e-8f)
                return axis.normalized;
        }

        Vector3 fallback = CySpringHairProbeAxisOverride;
        if (fallback.sqrMagnitude < 1e-8f)
            fallback = Vector3.right;

        return fallback.normalized;
    }

    // Punta del hueso: la posicion de su HIJO si lo tiene; si es el ULTIMO de la cadena, la punta
    // virtual que usa el propio motor (SelfPosition + rotacion * BoneAxis * InitBoneDistance): el C#
    // copia al ultimo hueso el eje y el largo del padre (ver CySpringBonebase:326), asi que el motor
    // SI le testea colisiones a la punta del ultimo hueso.
    // Hueso con peso de malla: girarlo mueve el mechon. Los "fantasma" (0 vertices) no.
    private bool IsProbeWeighted(CySpringBoneBase bone)
    {
        if (bone == null || bone.Transform == null)
            return true;

        int n;
        if (!_probeMeshVertCount.TryGetValue(bone.Transform, out n))
            return true;   // sin medicion: no se descarta nada

        return n > 0;
    }

    // Cuantos huesos CON PESO quedan por debajo de `index` (0 = el ultimo VISIBLE del mechon). Los
    // "fantasma" no cuentan, porque no mueven malla.
    private int WeightedLevelsFromTip(List<CySpringBoneBase> chain, int index)
    {
        int levels = 0;
        bool measured = _probeMeshVertCount.Count > 0;

        for (int i = chain.Count - 1; i > index; i--)
        {
            CySpringBoneBase b = chain[i];
            if (b == null || b.Transform == null) continue;
            if (measured && !IsProbeWeighted(b)) continue;
            levels++;
        }

        return levels;
    }

    // "Punta" para la pasada de escape y las sondas:
    //  - si el hijo tiene malla, la posicion del hijo (la particula que testea el motor);
    //  - si no (ultimo hueso VISIBLE), hasta donde llega la MALLA de este hueso a lo largo del mechon,
    //    que es el extremo visible.
    // La punta virtual del motor NO sirve para esto: el ultimo hueso es un "fantasma" sin peso y su
    // largo heredado apunta al vacio (medido: 11 cm por debajo de la malla).
    private Vector3 GetBoneTipWorld(CySpringBoneBase bone, CySpringBoneBase child)
    {
        Vector3 tip;

        if (child != null && child.Transform != null)
        {
            // Articulacion: la punta es la posicion del hijo (aunque el hijo sea "fantasma": el motor
            // tambien le testea esa punta al hueso padre).
            tip = child.Transform.position;
        }
        else
        {
            // Nivel MAS PROFUNDO de la jerarquia: por defecto, en el propio hueso; opcionalmente, en el
            // extremo visible de su malla (que suele quedar algunos cm mas abajo).
            tip = CySpringHairProbeAtMeshEnd ? GetBoneMeshTipWorld(bone) : bone.Transform.position;
        }

        // Desplazamiento opcional a lo largo del mechon (0 = en esa referencia).
        if (bone != null && bone.Transform != null && !Mathf.Approximately(CySpringHairProbeAlongOffset, 0.0f))
        {
            NativeClothWorking native = bone.NativeState;
            Vector3 strand = native.BoneAxis;
            if (strand.sqrMagnitude < 1e-8f)
                strand = Vector3.forward;

            tip += bone.Transform.TransformDirection(strand.normalized) * CySpringHairProbeAlongOffset;
        }

        return tip;
    }

    // Extremo VISIBLE de la malla de este hueso (Z local maximo medido), o la punta virtual del motor
    // si no hay medicion. El ultimo hueso del mechon suele ser "fantasma" (0 vertices) y su largo
    // heredado apunta al vacio (medido: 11 cm por debajo de la malla).
    private Vector3 GetBoneMeshTipWorld(CySpringBoneBase bone)
    {
        if (bone == null || bone.Transform == null)
            return Vector3.zero;

        float zMax;
        if (_probeMeshZMax.TryGetValue(bone.Transform, out zMax) && zMax > 0.001f)
        {
            NativeClothWorking nativeAxis = bone.NativeState;
            Vector3 axis = nativeAxis.BoneAxis;
            if (axis.sqrMagnitude < 1e-8f)
                axis = Vector3.forward;

            return bone.Transform.TransformPoint(axis.normalized * zMax);
        }

        NativeClothWorking native = bone.NativeState;
        Vector3 fallbackAxis = native.BoneAxis;
        if (fallbackAxis.sqrMagnitude < 1e-8f)
            fallbackAxis = Vector3.forward;

        return bone.Transform.position + (bone.Transform.rotation * fallbackAxis) * native.InitBoneDistance;
    }

    // Radio y signo de la sonda `index` (0-based): 0 = +Offset, 1 = -Offset, 2 = +2*Offset...
    private static void ProbeRingSign(int index, out int ring, out float sign)
    {
        ring = (index / 2) + 1;
        sign = (index % 2 == 0) ? 1.0f : -1.0f;
    }

    // Posicion en mundo de la sonda lateral `index` (0-based): parte de la PUNTA del hueso y se
    // separa a un lado y a otro a lo largo del eje de ANCHO (que sale de la malla).
    private Vector3 GetProbeWorld(CySpringBoneBase bone, Vector3 tipWorld, int index)
    {
        int ring;
        float sign;
        ProbeRingSign(index, out ring, out sign);

        Vector3 axisWorld = GetProbeAxisWorld(bone);

        return tipWorld + axisWorld * (ring * CySpringHairProbeOffset * sign);
    }

    private Vector3 GetProbeAxisWorld(CySpringBoneBase bone)
    {
        Vector3 axisLocal = GetProbeAxisLocal(bone);
        return bone.Transform.TransformDirection(axisLocal).normalized;
    }

    // Gira el hueso lo MINIMO para que el punto P (rigido al hueso) salga de la esfera (C, radius).
    // Misma ley del coseno que la punta, pero con la distancia de la sonda al origen del hueso en vez
    // del largo del hueso:  cos(theta) = (dP^2 + a^2 - R^2) / (2*dP*a)
    private bool EscapeProbe(CySpringBoneBase bone, Vector3 P, Vector3 C, float radius)
    {
        if (bone == null || bone.Transform == null)
            return false;

        if (Vector3.Distance(P, C) >= radius)
            return false;   // fuera: nada que corregir

        Vector3 S = bone.Transform.position;
        float dP = Vector3.Distance(S, P);
        float a = Vector3.Distance(S, C);

        if (dP <= 1e-5f || a <= 1e-5f)
            return false;

        float cosTarget = (dP * dP + a * a - radius * radius) / (2.0f * dP * a);
        bool impossible = cosTarget < -1.0f;    // R > dP + a: ninguna direccion alcanza

        Vector3 v = (P - S) / dP;
        Vector3 w = (C - S) / a;

        float alpha = Mathf.Acos(Mathf.Clamp(Vector3.Dot(v, w), -1.0f, 1.0f));
        float thetaTarget = impossible ? Mathf.PI : Mathf.Acos(Mathf.Clamp(cosTarget, -1.0f, 1.0f));
        float delta = thetaTarget - alpha;

        if (delta <= 0.0f)
        {
            // Geometria degenerada (p. ej. el centro de la esfera pegado al origen): maximo alejamiento.
            thetaTarget = Mathf.PI;
            delta = thetaTarget - alpha;
            if (delta <= 0.0f) return false;
            impossible = true;
        }

        if (impossible)
            _escapeImpossible++;

        // Eje perpendicular a v y w: girar v en POSITIVO alrededor de (v x w) lo acerca a w, asi que
        // para ALEJARLO se gira en negativo.
        Vector3 axis = Vector3.Cross(v, w);
        if (axis.sqrMagnitude < 1e-8f)
        {
            axis = Vector3.Cross(v, Vector3.up);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(v, Vector3.right);
            if (axis.sqrMagnitude < 1e-8f) return false;
        }

        axis.Normalize();

        bone.Transform.rotation = Quaternion.AngleAxis(-delta * Mathf.Rad2Deg, axis) * bone.Transform.rotation;

        _escapeCorrections++;
        if (delta > _escapeMaxAngle)
            _escapeMaxAngle = delta;

        return true;
    }

    // (TEMP) Rellena el gizmo de las sondas (punta + laterales) con la pose actual.
    private void RefreshProbeVisualizer()
    {
        if (!CySpringHairColliderGizmos || _cySpringController == null)
            return;

        if (_probeVisualizer == null)
        {
            _probeVisualizer = gameObject.GetComponent<CySpringHairProbeVisualizer>();
            if (_probeVisualizer == null)
                _probeVisualizer = gameObject.AddComponent<CySpringHairProbeVisualizer>();
        }

        _probeWorldCache.Clear();
        _probeRadiusCache.Clear();

        CySpring headSpring = _cySpringController.GetSpringArray(0);
        CySpringRootBone[] roots = headSpring != null ? headSpring.RootBoneArray : null;

        if (roots != null)
        {
            int levels = Mathf.Max(1, CySpringHairProbeLevelsFromTip);
            int sides = Mathf.Max(0, CySpringHairProbeCount);

            for (int i = 0; i < roots.Length; i++)
            {
                CySpringRootBone r = roots[i];
                if (r == null || string.IsNullOrEmpty(r.BoneName)) continue;
                if (!PassesProbeFilter(r.BoneName)) continue;

                List<CySpringBoneBase> chain = new List<CySpringBoneBase>();
                chain.Add(r);
                if (r.ChildBoneList != null) chain.AddRange(r.ChildBoneList);

                // TODOS los huesos, INCLUIDO EL ULTIMO: el motor tambien le testea la punta (virtual).
                for (int b = 0; b < chain.Count; b++)
                {
                    CySpringBoneBase bone = chain[b];
                    CySpringBoneBase child = (b + 1 < chain.Count) ? chain[b + 1] : null;
                    if (bone == null || bone.Transform == null)
                        continue;

                    // Los huesos SIN peso de malla (fantasma) no llevan sondas: girarlos no mueve nada y
                    // su "punta" cae fuera de la malla (la del motor hereda el largo del padre).
                    bool phantom = _probeMeshVertCount.Count > 0 && !IsProbeWeighted(bone);

                    if (phantom || !CySpringHairProbeEnabled || WeightedLevelsFromTip(chain, b) >= levels)
                    {
                        SetProbeObjectsActive(bone, 0);
                        continue;
                    }

                    Vector3 tipWorld = GetBoneTipWorld(bone, child);

                    // 0 = punta (referencia), luego las laterales.
                    _probeWorldCache.Add(tipWorld);
                    _probeRadiusCache.Add(bone.NativeState.CollisionRadius);

                    for (int p = 0; p < sides; p++)
                    {
                        Vector3 probeWorld = GetProbeWorld(bone, tipWorld, p);

                        _probeWorldCache.Add(probeWorld);
                        _probeRadiusCache.Add(bone.NativeState.CollisionRadius * Mathf.Clamp(CySpringHairProbeRadiusScale, 0.05f, 2.0f));

                        SyncProbeObject(bone, p, bone.Transform.InverseTransformPoint(probeWorld));
                    }

                    SetProbeObjectsActive(bone, sides);
                }
            }
        }

        _probeVisualizer.Positions = _probeWorldCache.ToArray();
        _probeVisualizer.Radii = _probeRadiusCache.ToArray();
    }

    // (TEMP) Crea (una vez) y recoloca los GameObjects de las sondas: son vacios, solo marcan la
    // posicion para poder VERLAS Y SELECCIONARLAS en la jerarquia (el dibujo lo hace el gizmo de arriba).
    private void SyncProbeObject(CySpringBoneBase bone, int probeIndex, Vector3 localPosition)
    {
        Dictionary<int, Transform> perBone;
        if (!_probeObjects.TryGetValue(bone.Transform, out perBone))
        {
            perBone = new Dictionary<int, Transform>();
            _probeObjects.Add(bone.Transform, perBone);
        }

        Transform t;
        if (!perBone.TryGetValue(probeIndex, out t) || t == null)
        {
            GameObject go = new GameObject($"Sonda_{bone.BoneName}_{probeIndex + 1}");
            go.transform.SetParent(bone.Transform, false);
            t = go.transform;
            perBone[probeIndex] = t;
        }

        t.localPosition = localPosition;
    }

    private void SetProbeObjectsActive(CySpringBoneBase bone, int count)
    {
        Dictionary<int, Transform> perBone;
        if (bone == null || bone.Transform == null || !_probeObjects.TryGetValue(bone.Transform, out perBone))
            return;

        for (int i = 0; i < 8; i++)
        {
            Transform t;
            if (!perBone.TryGetValue(i, out t) || t == null)
                continue;

            bool want = i < count;
            if (t.gameObject.activeSelf != want)
                t.gameObject.SetActive(want);
        }
    }

    // =========================================================================================
    // ESCAPE: correccion propia DESPUES del solver nativo (el DLL no se puede tocar en estatico).
    //
    // El motor hace, por hueso:  p = centro + normalize(p - centro) * R
    //                    y luego  p = SelfPosition + normalize(p - SelfPosition) * InitBoneDistance
    // Esa segunda linea (reproyeccion al largo del hueso) es la que sella la trampa: si la esfera
    // cubre tambien el "lado opuesto" del hueso (R > | |S-C| - L |) la punta vuelve a caer DENTRO y
    // la cadena no puede salir. Aumentar el radio agranda R => lo empeora.
    //
    // Aqui se cumple exactamente la regla pedida: "si me tocas te empujo, y SI ESTAS DENTRO te saco".
    // Se recorre la cadena de raiz a punta (para que el giro arrastre a los hijos, igual que el
    // solver) y se calcula el giro MINIMO que lleva la punta justo a la superficie, con la ley del
    // coseno:
    //     cos(theta) = (L^2 + a^2 - R^2) / (2*L*a)      L = largo del hueso, a = |S - C|
    // Si no existe solucion (R > a + L) no hay direccion que salga: se toma la de maximo alejamiento
    // y se cuenta, porque eso significa "esta esfera es demasiado grande para este hueso".
    // =========================================================================================
    private void ApplyHairEscapeFix()
    {
        if (!CySpringHairEscapeFix || _cySpringController == null || _hairRuntimeCollisions == null)
            return;

        CySpring headSpring = _cySpringController.GetSpringArray(0);
        CySpringRootBone[] roots = headSpring != null ? headSpring.RootBoneArray : null;
        if (roots == null)
            return;

        for (int i = 0; i < roots.Length; i++)
        {
            CySpringRootBone r = roots[i];
            if (r == null || string.IsNullOrEmpty(r.BoneName))
                continue;

            if (r.BoneName.IndexOf("Hair", StringComparison.Ordinal) < 0)
                continue;   // solo el pelo

            List<CySpringBoneBase> chain = new List<CySpringBoneBase>();
            chain.Add(r);
            if (r.ChildBoneList != null) chain.AddRange(r.ChildBoneList);

            // TODOS los huesos, INCLUIDO EL ULTIMO: el motor tambien le testea la punta (le copia el eje
            // y el largo del padre), y esa punta es justo el extremo del mechon.
            for (int b = 0; b < chain.Count; b++)
            {
                CySpringBoneBase bone = chain[b];
                CySpringBoneBase child = (b + 1 < chain.Count) ? chain[b + 1] : null;

                if (bone == null || bone.Transform == null)
                    continue;

                if (bone.CollisionIndexList == null)
                    continue;

                // Un hueso sin peso de malla no mueve nada visible: girarlo no sirve de nada.
                if (_probeMeshVertCount.Count > 0 && !IsProbeWeighted(bone))
                    continue;

                int fromTip = WeightedLevelsFromTip(chain, b);
                int probeLevels = Mathf.Max(1, CySpringHairProbeLevelsFromTip);
                bool probesAllowed = CySpringHairProbeEnabled && PassesProbeFilter(r.BoneName);

                for (int c = 0; c < bone.CollisionIndexList.Count; c++)
                {
                    int index = bone.CollisionIndexList[c];
                    if (index < 0 || index >= _hairRuntimeCollisions.Count)
                        continue;

                    CySpringCollisionRuntimeData col = _hairRuntimeCollisions[index];
                    if (col == null || col.TargetTransform == null)
                        continue;

                    // v1: esferas (que son las que generamos). Capsula/plano quedan para despues.
                    if (col.CollisionType != CySpringCollisionData.CollisionType.Sphere)
                        continue;

                    float radius = col.Radius + bone.NativeState.CollisionRadius;
                    if (radius <= 0.00001f)
                        continue;

                    Vector3 C = col.TargetTransform.position;

                    // Sondas del hueso: la punta (0) y, si estan activas y es un nivel con sondas, las
                    // laterales. La posicion se recalcula en cada vuelta porque una correccion previa
                    // gira el hueso y con el toda la cadena.
                    int probes = 1;
                    if (probesAllowed && fromTip < probeLevels)
                        probes += Mathf.Max(0, CySpringHairProbeCount);

                    for (int p = 0; p < probes; p++)
                    {
                        Vector3 tipWorld = GetBoneTipWorld(bone, child);

                        Vector3 probeWorld = p == 0
                            ? tipWorld
                            : GetProbeWorld(bone, tipWorld, p - 1);

                        // La punta usa el radio del hueso (es la particula del motor); las laterales son
                        // puntos de test propios y pueden ser mas finos (CySpringHairProbeRadiusScale).
                        float probeRadius = p == 0
                            ? radius
                            : col.Radius + bone.NativeState.CollisionRadius * Mathf.Clamp(CySpringHairProbeRadiusScale, 0.05f, 2.0f);

                        EscapeProbe(bone, probeWorld, C, probeRadius);
                    }
                }
            }
        }

        // (TEMP) Gizmos de las sondas con la pose ya corregida.
        RefreshProbeVisualizer();
    }

    // (TEMP/GIZMOS) Dibuja el radio PROPIO de cada hueso de tela: el motor SUMA ese radio al del
    // colisor (datamine 04 §1.1: R = colider.Radius + hueso.CollisionRadius), asi que sin verlo
    // parece que el pelo esta "lejos" de las esferas cuando en realidad ya las esta rozando.
    private void UpdateHairBoneVisualizers()
    {
        if (_cySpringController == null)
            return;

        CySpring headSpring = _cySpringController.GetSpringArray(0);
        CySpringRootBone[] roots = headSpring != null ? headSpring.RootBoneArray : null;
        if (roots == null)
            return;

        for (int i = 0; i < roots.Length; i++)
        {
            CySpringRootBone r = roots[i];
            if (r == null) continue;

            List<CySpringBoneBase> bl = new List<CySpringBoneBase>();
            bl.Add(r);
            if (r.ChildBoneList != null) bl.AddRange(r.ChildBoneList);

            for (int b = 0; b < bl.Count; b++)
            {
                CySpringBoneBase bone = bl[b];
                if (bone == null || bone.GameObject == null) continue;

                CySpringColliderVisualizer vis = bone.GameObject.GetComponent<CySpringColliderVisualizer>();
                if (vis == null)
                    vis = bone.GameObject.AddComponent<CySpringColliderVisualizer>();

                vis.IsBone = true;
                vis.IsCapsule = false;
                // El gizmo LEE el radio del hueso en cada dibujado: asi el tamano que se ve es siempre
                // el que el motor usa, tambien al ajustar el grosor EN CALIENTE desde el Inspector.
                vis.BoneSource = bone;
                vis.Radius = bone.NativeState.CollisionRadius;
                vis.Color = new Color(0.0f, 0.8f, 1.0f, 1.0f);   // cian = radio propio del hueso
            }
        }
    }

    // =========================================================================================
    // (PASO 1) ESFERAS DEL TRAMO HOMBRO -> CODO
    //
    // Se reservan `CySpringArmColliderReserve` esferas por brazo para poder SUBIR la cantidad en
    // caliente: las que sobran se apagan con el tipo None (el motor escribe IsEnable=0 y las salta),
    // asi que cambiar el numero no exige recargar la escena. Limite duro del motor: 16 indices de
    // colision por hueso (CIndex0..CIndex15 en NativeClothWorking).
    // =========================================================================================
    private void BuildArmColliderGrid(CySpringDataContainer head, Dictionary<string, Transform> bones)
    {
        _generatedHairCollisionNames.Clear();

        if (head == null || head.collisionParam == null || bones == null)
            return;

        if (!CySpringArmColliderEnabled)
        {
            return;
        }

        if (CySpringArmColliderSides == null || CySpringArmColliderSides.Length == 0)
            return;

        int reserve = Mathf.Clamp(CySpringArmColliderReserve, 1, 16);

        if (reserve * CySpringArmColliderSides.Length > 16)
        {
            Debug.LogWarning(
                $"[PeloCol] reservado={reserve} por brazo x {CySpringArmColliderSides.Length} brazos = " +
                $"{reserve * CySpringArmColliderSides.Length} indices, pero el motor solo admite 16 por hueso " +
                "(las cadenas centrales del pelo usan los dos brazos). Baja 'Cy Spring Arm Collider Reserve'.");
        }

        for (int s = 0; s < CySpringArmColliderSides.Length; s++)
        {
            string side = CySpringArmColliderSides[s];
            if (string.IsNullOrEmpty(side))
                continue;

            string anchorName = CySpringArmColliderAnchorBone + "_" + side;

            if (!bones.TryGetValue(anchorName, out Transform anchor) || anchor == null)
            {
                Debug.LogWarning($"[PeloCol] no encuentro el hueso '{anchorName}': ese brazo se queda sin esferas.");
                continue;
            }

            for (int i = 0; i < reserve; i++)
            {
                string colName = $"{CySpringHairArmCollisionPrefix}{side}_{i:00}";

                bool exists = false;
                for (int k = 0; k < head.collisionParam.Count; k++)
                {
                    CySpringCollisionData c = head.collisionParam[k];
                    if (c != null && c.CollisionName == colName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    // Radio provisional: Create() DESCARTA los colisores de radio 0, y el radio real lo
                    // fija ApplyArmColliderLayout() (que corre antes del warm-up, no en el bind).
                    head.collisionParam.Add(new CySpringCollisionData(
                        CySpringCollisionData.CollisionType.Sphere,
                        false,
                        colName,
                        anchorName,
                        false,
                        Vector3.zero,
                        Vector3.zero,
                        Mathf.Max(0.001f, CySpringArmColliderRadius),
                        0.0f,
                        Vector3.zero));
                }

                _generatedHairCollisionNames.Add(colName);
            }
        }
    }

    // =========================================================================================
    // (PASO 2) ASIGNACION AL PELO
    //
    // El motor empuja un hueso de tela SOLO con los colisores que su PROPIA lista de nombres
    // declara, y los hijos declaran listas propias (medido: la raiz 5, los hijos 4/3/3). Por eso se
    // reescribe la lista de la raiz Y la de cada hijo, en TODA la cadena del pelo (delantero y
    // trasero). Si el hueso declara lado, solo se le dan las esferas de su brazo; las cadenas
    // centrales (pelo trasero) reciben las de los dos, que es lo que pide el objetivo.
    // =========================================================================================
    private void BindGeneratedCollidersToHair(CySpringDataContainer head)
    {
        if (head == null || head.springParam == null)
            return;

        int chains = 0;
        int childBones = 0;

        for (int i = 0; i < head.springParam.Count; i++)
        {
            Gallop.CySpringParamDataElement sp = head.springParam[i];
            if (sp == null || string.IsNullOrEmpty(sp._boneName))
                continue;

            if (sp._boneName.IndexOf("Hair", StringComparison.Ordinal) < 0)
                continue;   // orejas y demas no se tocan

            chains++;
            sp._collisionNameList = new List<string>(GeneratedColliderNamesForBone(sp._boneName));

            if (sp._childElements == null)
                continue;

            for (int k = 0; k < sp._childElements.Count; k++)
            {
                Gallop.CySpringParamDataChildElement ch = sp._childElements[k];
                if (ch == null)
                    continue;

                string childBone = string.IsNullOrEmpty(ch._boneName) ? sp._boneName : ch._boneName;
                ch._collisionNameList = new List<string>(GeneratedColliderNamesForBone(childBone));

                if (ch._collisionNameList.Count > 16)
                {
                    Debug.LogWarning(
                        $"[PeloCol] {childBone} referencia {ch._collisionNameList.Count} colisores; " +
                        "el motor solo admite 16 por hueso.");
                }

                childBones++;
            }
        }

    }

    // Nombres que le corresponden a una cadena: si el hueso declara lado (_L / _R) solo las esferas
    // de SU brazo; si no declara lado (cadenas centrales) las de todos los brazos.
    private List<string> GeneratedColliderNamesForBone(string boneName)
    {
        if (CySpringArmColliderSides == null || CySpringArmColliderSides.Length <= 1)
            return _generatedHairCollisionNames;

        for (int s = 0; s < CySpringArmColliderSides.Length; s++)
        {
            string side = CySpringArmColliderSides[s];
            if (string.IsNullOrEmpty(side))
                continue;

            int at = boneName.IndexOf("_" + side, StringComparison.Ordinal);
            if (at < 0)
                continue;

            // El sufijo de lado del rig es de un solo caracter: si detras sigue una letra, "_L" es
            // parte de otra palabra y no un lado.
            int after = at + 1 + side.Length;
            if (after < boneName.Length && char.IsLetter(boneName[after]))
                continue;

            string prefix = CySpringHairArmCollisionPrefix + side + "_";
            List<string> only = new List<string>();

            for (int i = 0; i < _generatedHairCollisionNames.Count; i++)
            {
                if (_generatedHairCollisionNames[i].StartsWith(prefix, StringComparison.Ordinal))
                    only.Add(_generatedHairCollisionNames[i]);
            }

            return only;
        }

        return _generatedHairCollisionNames;
    }

    // Captura (UNA vez) el tramo hombro->codo en el espacio LOCAL del hueso del brazo. Se hace una
    // sola vez a proposito: si se recalculara con cada ajuste, las esferas quedarian a merced de la
    // pose del instante (con el codo doblado, la recta hombro->codo es una CUERDA que sale del
    // brazo). Ancladas al hueso, las esferas acompanan luego al brazo al moverse.
    private void CaptureArmColliderSegments()
    {
        int n = CySpringArmColliderSides != null ? CySpringArmColliderSides.Length : 0;

        _armSegmentLocalStart = new Vector3[n];
        _armSegmentLocalEnd = new Vector3[n];
        _armSegmentLength = new float[n];

        for (int s = 0; s < n; s++)
        {
            string side = CySpringArmColliderSides[s];
            if (string.IsNullOrEmpty(side))
                continue;

            Transform anchor = FindCySpringTransform(transform, CySpringArmColliderAnchorBone + "_" + side);
            Transform start = FindCySpringTransform(transform, CySpringArmColliderStartBone + "_" + side);
            Transform end = FindCySpringTransform(transform, CySpringArmColliderEndBone + "_" + side);

            if (anchor == null || start == null || end == null)
            {
                Debug.LogWarning(
                    $"[PeloCol] tramo '{side}' incompleto: anclaje={anchor != null} inicio={start != null} " +
                    $"codo={end != null}.");
                continue;
            }

            _armSegmentLocalStart[s] = anchor.InverseTransformPoint(start.position);
            _armSegmentLocalEnd[s] = anchor.InverseTransformPoint(end.position);
            _armSegmentLength[s] = Vector3.Distance(start.position, end.position);

        }
    }

    // =========================================================================================
    // (PASO 3) COLOCACION DE LAS ESFERAS (posicion + radio reales)
    //
    // Las esferas van ancladas al hueso del brazo, asi que basta con fijar su POSICION LOCAL: el
    // motor la escala con la del padre y el solver la pasa a mundo con el ParentWork del hueso
    // (Position = localPosition * parent.lossyScale, ver CySpringCollisionRuntimeData). Se puede
    // llamar EN CALIENTE; al final reconstruye el array nativo.
    // =========================================================================================
    private void ApplyArmColliderLayout(bool force)
    {
        if (_hairRuntimeCollisions == null || _cySpringController == null)
            return;

        if (CySpringArmColliderSides == null || _generatedHairCollisionNames.Count == 0)
            return;

        if (_armSegmentLocalEnd == null || _armSegmentLocalEnd.Length != CySpringArmColliderSides.Length)
            CaptureArmColliderSegments();

        int requestedCount = CySpringArmColliderCount;
        float radius = CySpringArmColliderRadius;
        float overlap = CySpringArmColliderOverlap;

        if (!force &&
            Mathf.Approximately(radius, _lastArmRadius) &&
            requestedCount == _lastArmCount &&
            Mathf.Approximately(overlap, _lastArmOverlap) &&
            Mathf.Approximately(CySpringArmColliderStartOffset, _lastArmStartOffset))
            return;

        _lastArmRadius = radius;
        _lastArmCount = requestedCount;
        _lastArmOverlap = overlap;
        _lastArmStartOffset = CySpringArmColliderStartOffset;

        float safeRadius = Mathf.Max(0.001f, radius);
        bool changed = false;

        for (int s = 0; s < CySpringArmColliderSides.Length; s++)
        {
            string side = CySpringArmColliderSides[s];
            if (string.IsNullOrEmpty(side))
                continue;

            string prefix = CySpringHairArmCollisionPrefix + side + "_";

            if (_armSegmentLocalEnd == null || s >= _armSegmentLocalEnd.Length)
                continue;

            float length = _armSegmentLength[s];

            // Arranque opcional mas abajo del hombro: la esfera del hombro puede cubrir el hueso del
            // pelo entero y entonces el motor lo deja ATRAPADO dentro (ver la regla de la trampa).
            float inset = length > 0.0001f ? Mathf.Clamp01(CySpringArmColliderStartOffset / length) : 0.0f;
            float coveredLength = length * (1.0f - inset);

            // Limite del motor: 16 indices de colision por hueso (las cadenas centrales llevan los
            // dos brazos, asi que se reserva la mitad para cada uno).
            int perArmMax = Mathf.Clamp(16 / Mathf.Max(1, CySpringArmColliderSides.Length), 1, 16);
            int perSide = Mathf.Clamp(CountArmCollidersFor(requestedCount, coveredLength, safeRadius, overlap), 1, perArmMax);

            for (int i = 0; i < _hairRuntimeCollisions.Count; i++)
            {
                CySpringCollisionRuntimeData rd = _hairRuntimeCollisions[i];
                if (rd == null || string.IsNullOrEmpty(rd.Name) || rd.TargetTransform == null)
                    continue;

                if (!rd.Name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                int index;
                if (!int.TryParse(rd.Name.Substring(prefix.Length), out index))
                    continue;

                if (index >= perSide)
                {
                    // Esfera de RESERVA sin usar: se apaga con el tipo None (el motor escribe
                    // IsEnable=0 y la salta) y a radio 0 para que tampoco se dibuje el gizmo. Asi
                    // subir la cantidad no exige recargar la escena.
                    if (rd.CollisionType != CySpringCollisionData.CollisionType.None)
                    {
                        rd.CollisionType = CySpringCollisionData.CollisionType.None;
                        changed = true;
                    }

                    if (!Mathf.Approximately(rd.Radius, 0.0f))
                    {
                        rd.Radius = 0.0f;
                        changed = true;
                    }
                    continue;
                }

                // Reparto uniforme por el tramo (desde `inset` hasta el codo). Con una sola esfera, al medio.
                float frac = perSide <= 1 ? 0.5f : (float)index / (perSide - 1);
                float t = Mathf.Lerp(inset, 1.0f, frac);
                Vector3 local = Vector3.Lerp(_armSegmentLocalStart[s], _armSegmentLocalEnd[s], t);

                if ((rd.TargetTransform.localPosition - local).sqrMagnitude > 1e-10f)
                {
                    rd.TargetTransform.localPosition = local;
                    changed = true;
                }

                rd.Offset = local;

                if (!Mathf.Approximately(rd.Radius, safeRadius))
                {
                    rd.Radius = safeRadius;
                    changed = true;
                }

                if (rd.CollisionType != CySpringCollisionData.CollisionType.Sphere)
                {
                    rd.CollisionType = CySpringCollisionData.CollisionType.Sphere;
                    changed = true;
                }

                // Que un recálculo de escala del motor no devuelva la esfera al origen del hueso.
                rd.SetDefaultParams(safeRadius, local);
            }
        }

        if (changed)
            _cySpringController.RefreshNativeCollisions();

        UpdateHairColliderGizmos();
    }

    // Esferas que cubren un tramo sin dejar huecos: la separacion entre centros es
    // 2*radio*solape, asi que hacen falta ceil(longitud/separacion) + 1.
    private static int CountArmCollidersFor(int requested, float length, float radius, float overlap)
    {
        if (requested > 0)
            return Mathf.Clamp(requested, 1, 16);

        if (length <= 0.0001f)
            return 1;

        float step = 2.0f * Mathf.Max(0.001f, radius) * Mathf.Clamp(overlap, 0.1f, 1.0f);
        return Mathf.Clamp(Mathf.CeilToInt(length / step) + 1, 1, 16);
    }

    // (TEMP) Gizmos SOLO de los colisores generados del pelo: azul = brazo izquierdo, rosa = derecho.
    // Los que traia el asset (Col_Head_Hair_F_Block r=0.8, Col_Chest_Hair_B r=1.0...) NO se dibujan:
    // el pelo ya no los usa y llenaban la escena de esferas gigantes.
    private void AttachHairColliderGizmos()
    {
        if (_hairRuntimeCollisions == null)
            return;

        string leftPrefix = CySpringHairArmCollisionPrefix
            + ((CySpringArmColliderSides != null && CySpringArmColliderSides.Length > 0) ? CySpringArmColliderSides[0] : "L")
            + "_";

        for (int i = 0; i < _hairRuntimeCollisions.Count; i++)
        {
            CySpringCollisionRuntimeData rd = _hairRuntimeCollisions[i];
            if (rd == null || rd.TargetTransform == null || rd.Name == null)
                continue;

            if (!rd.Name.StartsWith(CySpringHairArmCollisionPrefix, StringComparison.Ordinal))
                continue;

            CySpringColliderVisualizer vis = rd.TargetTransform.GetComponent<CySpringColliderVisualizer>();
            if (vis == null)
                vis = rd.TargetTransform.gameObject.AddComponent<CySpringColliderVisualizer>();

            vis.IsBone = false;
            vis.BoneSource = null;
            vis.ColliderSource = rd;
            vis.IsCapsule = rd.CollisionType == CySpringCollisionData.CollisionType.Capsule;
            vis.Offset2Local = rd.Offset2 - rd.Offset;
            vis.Radius = rd.Radius;
            vis.Color = rd.Name.StartsWith(leftPrefix, StringComparison.Ordinal)
                ? new Color(0.25f, 0.60f, 1.00f, 1.0f)   // azul = brazo izquierdo
                : new Color(1.00f, 0.35f, 0.60f, 1.0f);  // rosa = brazo derecho
        }
    }

    // (TEMP) Mantiene los gizmos sincronizados con el radio REAL del motor y respeta el interruptor
    // del Inspector (los flags estaticos los leen todos los gizmos del proyecto).
    private void UpdateHairColliderGizmos()
    {
        CySpringColliderVisualizer.CollidersEnabled = CySpringHairColliderGizmos;
        CySpringColliderVisualizer.BonesEnabled = CySpringHairColliderGizmos;

        if (_hairRuntimeCollisions == null)
            return;

        for (int i = 0; i < _hairRuntimeCollisions.Count; i++)
        {
            CySpringCollisionRuntimeData rd = _hairRuntimeCollisions[i];
            if (rd == null || rd.TargetTransform == null)
                continue;

            CySpringColliderVisualizer vis = rd.TargetTransform.GetComponent<CySpringColliderVisualizer>();
            if (vis != null)
                vis.Radius = rd.Radius;
        }
    }

    /// <summary>
    /// Une a cada hijo de una cadena de falda los nombres de colision que declara la raiz.
    /// La raiz nombra el volumen de su sector de cadera ademas de las capsulas del muslo; los
    /// hijos solo nombran las capsulas, asi que la tela podia plegarse hacia dentro en el
    /// gluteo. Con la herencia, todos los huesos de la cadena colisionan contra el mismo set.
    /// </summary>
    private static void PropagateClothCollisionNames(CySpringDataContainer container, bool onlySkirt = true)
    {
        if (container == null || container.springParam == null)
            return;

        for (int i = 0; i < container.springParam.Count; i++)
        {
            Gallop.CySpringParamDataElement spring = container.springParam[i];
            if (spring == null || spring._boneName == null || spring._collisionNameList == null)
                continue;

            if (onlySkirt && spring._boneName.IndexOf("MSkirt", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (spring._childElements == null)
                continue;

            for (int j = 0; j < spring._childElements.Count; j++)
            {
                Gallop.CySpringParamDataChildElement child = spring._childElements[j];
                if (child == null)
                    continue;

                if (child._collisionNameList == null)
                    child._collisionNameList = new List<string>();

                for (int k = 0; k < spring._collisionNameList.Count; k++)
                {
                    string name = spring._collisionNameList[k];
                    if (string.IsNullOrEmpty(name))
                        continue;
                    if (!child._collisionNameList.Contains(name))
                        child._collisionNameList.Add(name);
                }
            }
        }
    }

    /// <summary>
    /// Reconstruye SOLO el puente nativo de tela (falda y ropa) sin recrear el personaje.
    /// Permite aplicar en caliente datos de colision cambiados.
    /// </summary>
    public void ReloadClothPhysics()
    {
        CySpringDataContainer[] containers = GetComponentsInChildren<CySpringDataContainer>(true);
        var nativeContainers = new List<CySpringDataContainer>();

        for (int i = 0; i < containers.Length; i++)
        {
            CySpringDataContainer container = containers[i];
            if (container == null)
                continue;

            int kind = GuessCySpringContainerKind(container);
            if (kind == 1 || kind == 2)
                nativeContainers.Add(container);
        }

        if (nativeContainers.Count == 0)
        {
            Debug.LogWarning("[UmaContainerCharacter] ReloadClothPhysics: no hay contenedores nativos de ropa.");
            return;
        }

        Dictionary<string, Transform> cache = BuildCySpringTransformCache(transform, out HashSet<string> duplicates);

        Transform hip = FindCySpringTransform(transform, "Hip");
        if (hip == null && UpBodyBone != null)
            hip = UpBodyBone.transform;
        if (hip == null)
            hip = transform.Find("Position/Hip");
        if (hip == null)
            hip = transform;

        var slots = SplitCySpringContainers(nativeContainers);

        PropagateClothCollisionNames(slots.body);

        if (_cySpringController == null)
        {
            _cySpringOwner = new UmaViewerCySpringOwner(this);
            _cySpringController = CySpringController.AddController(gameObject, hip, _cySpringOwner);
        }

        if (_cySpringController == null)
        {
            Debug.LogError("[UmaContainerCharacter] ReloadClothPhysics: no se pudo crear el controller.");
            return;
        }

        _cySpringController.LoadFromDataContainers(
            cache, slots.head, slots.body, slots.bust, slots.tail, null, null, null,
            BodyScale, BodyScale, BodyScale);

        _cySpringController.Reset();
        _cySpringLoaded = true;
        LinkSkirtControllerToCySpring();

        if (CySpringWarmUpTime > 0.0f)
        {
            _cySpringController.ReserveWarmingUpCySpring(CySpringWarmUpTime);
            _cySpringController.TryUpdateWarmUp();
        }

    }

    private void LoadSkirtController(UmaDatabaseEntry bodyEntry)
    {
        if (IsMini || Body == null || bodyEntry == null)
            return;

        SkirtParamDataAsset skirtParam = LoadBestSkirtParamAsset(bodyEntry);
        if (skirtParam == null)
        {
            Debug.LogWarning($"[UmaContainerCharacter] No SkirtParamDataAsset found for {bodyEntry.Name}.");
            return;
        }

        _skirtController = GetComponent<SkirtController>();
        if (_skirtController == null)
            _skirtController = gameObject.AddComponent<SkirtController>();

        _skirtController.CharaId = CharaEntry != null ? CharaEntry.Id : 0;
        _skirtController.ModelScale = BodyScale > 0f ? BodyScale : 1f;
        _skirtController.Create(skirtParam);

        if (_cySpringController != null)
            LinkSkirtControllerToCySpring();
    }

    private SkirtParamDataAsset LoadBestSkirtParamAsset(UmaDatabaseEntry bodyEntry)
    {
        List<UmaDatabaseEntry> candidates = FindSkirtParamEntries(bodyEntry).ToList();
        if (candidates.Count == 0)
            return null;

        int charaId = CharaEntry != null ? CharaEntry.Id : 0;
        SkirtParamDataAsset best = null;
        int bestScore = -1;
        bool bestHasCharaOverride = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            UmaDatabaseEntry entry = candidates[i];
            if (entry == null)
                continue;

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry, false, true);
            if (bundle == null)
                continue;

            SkirtParamDataAsset asset = LoadSkirtParamAssetFromBundle(bundle, entry);
            if (asset == null)
            {
                Debug.LogWarning($"[UmaContainerCharacter] Skirt AB has no SkirtParamDataAsset: {entry.Name}");
                continue;
            }

            int score = ScoreSkirtParamAsset(asset);
            bool hasCharaOverride = charaId > 0 && asset.GetOverrideData(charaId) != null;

            if (score > bestScore || (score == bestScore && hasCharaOverride && !bestHasCharaOverride))
            {
                best = asset;
                bestScore = score;
                bestHasCharaOverride = hasCharaOverride;
            }
        }

        return best;
    }

    private IEnumerable<UmaDatabaseEntry> FindSkirtParamEntries(UmaDatabaseEntry bodyEntry)
    {
        if (Main == null || Main.AbChara == null || bodyEntry == null)
            yield break;

        HashSet<string> bodyTokens = GetSkirtBodyTokens(bodyEntry);
        if (bodyTokens.Count == 0)
            yield break;

        foreach (UmaDatabaseEntry entry in Main.AbChara)
        {
            if (entry == null || !entry.IsAssetBundle)
                continue;

            string assetName = Path.GetFileName(entry.Name);
            if (string.IsNullOrEmpty(assetName))
                continue;

            string lower = assetName.ToLowerInvariant();
            if (!lower.Contains("_skirt"))
                continue;

            foreach (string bodyToken in bodyTokens)
            {
                string expectedPrefix = "ast_" + bodyToken.ToLowerInvariant() + "_skirt";
                if (lower.StartsWith(expectedPrefix))
                {
                    yield return entry;
                    break;
                }
            }
        }
    }

    private HashSet<string> GetSkirtBodyTokens(UmaDatabaseEntry bodyEntry)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string prefabName = Path.GetFileName(bodyEntry.Name);
        if (!string.IsNullOrEmpty(prefabName) && prefabName.StartsWith("pfb_", StringComparison.OrdinalIgnoreCase))
        {
            string token = prefabName.Substring(4);
            AddBodyTokenVariants(result, token);
        }

        string directoryName = Path.GetFileName(Path.GetDirectoryName(bodyEntry.Name));
        AddBodyTokenVariants(result, directoryName);

        return result;
    }

    private void AddBodyTokenVariants(HashSet<string> result, string token)
    {
        if (result == null || string.IsNullOrEmpty(token))
            return;

        if (!token.StartsWith("bdy", StringComparison.OrdinalIgnoreCase))
            return;

        result.Add(token);

        string[] parts = token.Split('_');
        if (parts.Length >= 2)
            result.Add(parts[0] + "_" + parts[1]);
    }

    private SkirtParamDataAsset LoadSkirtParamAssetFromBundle(AssetBundle bundle, UmaDatabaseEntry entry)
    {
        if (bundle == null)
            return null;

        //(P1) ANTES se intentaba bundle.LoadAsset<SkirtParamDataAsset>(nombreDelAsset) y, si
        //fallaba, se cogía allAssets[0] a ciegas. La búsqueda por nombre NO puede funcionar:
        //el asset viene del juego con el GUID de script ORIGINAL, que no existe en este
        //proyecto, así que Unity no puede asociarlo a nuestro tipo y avisa
        //"No script asset for SkirtParamDataAsset". Además Cogear el primero a ciegas puede
        //elegir un asset que no corresponde a este cuerpo, y de ahí el "poco peso".
        //Ahora: se carga por TIPO y se puntúa el que más huesos de falda resuelva.
        SkirtParamDataAsset[] allAssets = bundle.LoadAllAssets<SkirtParamDataAsset>();

        if (allAssets == null || allAssets.Length == 0)
            return null;

        if (allAssets.Length == 1)
            return allAssets[0];

        SkirtParamDataAsset best = null;
        int bestScore = -1;

        for (int i = 0; i < allAssets.Length; i++)
        {
            SkirtParamDataAsset candidate = allAssets[i];
            if (candidate == null) continue;

            int score = ScoreSkirtParamAsset(candidate);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best != null ? best : allAssets[0];
    }

    private int ScoreSkirtParamAsset(SkirtParamDataAsset asset)
    {
        if (asset == null || asset._skirtBone == null || Body == null)
            return 0;

        int score = 0;
        Transform root = Body.transform;

        for (int i = 0; i < asset._skirtBone.Count; i++)
        {
            SkirtParamDataElement element = asset._skirtBone[i];
            if (element == null)
                continue;

            if (FindCySpringTransform(root, element._rootBoneName) != null &&
                FindCySpringTransform(root, element._childBoneName) != null)
            {
                score++;
            }
        }

        return score;
    }

    private (
        CySpringDataContainer head,
        CySpringDataContainer body,
        CySpringDataContainer bust,
        CySpringDataContainer tail
    ) SplitCySpringContainers(List<CySpringDataContainer> containers)
    {
        CySpringDataContainer head = null;
        CySpringDataContainer body = null;
        CySpringDataContainer bust = null;
        CySpringDataContainer tail = null;

        if (containers == null)
            return (head, body, bust, tail);

        for (int i = 0; i < containers.Count; i++)
        {
            CySpringDataContainer container = containers[i];

            if (container == null)
                continue;

            int kind = GuessCySpringContainerKind(container);

            switch (kind)
            {
                case 0:
                    if (head == null)
                        head = container;
                    else
                        Debug.LogWarning($"[UmaContainerCharacter] Extra head CySpringDataContainer ignored: {container.name}");
                    break;

                case 1:
                    if (body == null)
                        body = container;
                    else
                        Debug.LogWarning($"[UmaContainerCharacter] Extra body CySpringDataContainer ignored: {container.name}");
                    break;

                case 2:
                    if (bust == null)
                        bust = container;
                    else
                        Debug.LogWarning($"[UmaContainerCharacter] Extra bust CySpringDataContainer ignored: {container.name}");
                    break;

                case 3:
                    if (tail == null)
                        tail = container;
                    else
                        Debug.LogWarning($"[UmaContainerCharacter] Extra tail CySpringDataContainer ignored: {container.name}");
                    break;
            }
        }

        if (body == null && containers.Count == 1)
            body = containers[0];

        return (head, body, bust, tail);
    }

    /// <summary>
    /// (FIX cabello) Decide a qué parte (head=0, body=1, bust=2, tail=3) pertenece un
    /// contenedor. La fuente PRIMARIA son los HUESOS de sus resortes (prefijos del juego:
    /// `Sp_He_Hair`, `Sp_Hi_Tail`, `Sp_Ch_Bust`, `Sp_Hi_MSkirt`…). El nombre del
    /// GameObject/prefab NO basta: el pelo vive en `pfb_chr…_cloth00`, cuyo path contiene
    /// "cloth" y por eso la versión anterior lo casaba como "body" ANTES de mirar los
    /// huesos → el pelo se descartaba como "Extra body ignored" y quedaba RÍGIDO. Ahora se
    /// miran los huesos primero (autoritativos) y el path sólo sirve de apoyo si no hay
    /// huesos reconocibles.
    /// </summary>
    private int GuessCySpringContainerKind(CySpringDataContainer container)
    {
        if (container == null)
            return 1;

        // 1) Huesos: fuente autoritativa.
        if (container.springParam != null)
        {
            for (int i = 0; i < container.springParam.Count; i++)
            {
                //(Fase 0.5: fully-qualified. Hay DOS clases llamadas CySpringParamDataElement: la de
                //namespace Gallop (Gallop\Cyspring\CySpringParamDataElement.cs) y la del namespace
                //global (Gallop\StageScript\CySpring\CySpringParamDataElement.cs). Con 'using Gallop;'
                //gana la del namespace global, y container.springParam es List<Gallop.CySpring…> →
                //CS0029. container es CySpringDataContainer (Gallop), así que el tipo correcto es el
                //de Gallop. Sin este cambio: error CS0029 en la línea 848.)
                Gallop.CySpringParamDataElement element = container.springParam[i];

                if (element == null)
                    continue;

                int kind = GuessCySpringBoneKind(element._boneName);

                if (kind >= 0)
                    return kind;

                if (element._childElements == null)
                    continue;

                for (int j = 0; j < element._childElements.Count; j++)
                {
                    //(Fase 0.5: mismo choque que en la línea 848, una línea más abajo. Hay DOS
                    //clases CySpringParamDataChildElement: Gallop\Cyspring\… (namespace Gallop) y
                    //Gallop\StageScript\CySpring\… (namespace global). Con 'using Gallop;' gana la
                    //global, y element._childElements es List<Gallop.…> → CS0029. Sin este cambio:
                    //error CS0029 en la línea 869.)
                    Gallop.CySpringParamDataChildElement child = element._childElements[j];

                    if (child == null)
                        continue;

                    kind = GuessCySpringBoneKind(child._boneName);

                    if (kind >= 0)
                        return kind;
                }
            }
        }

        // 2) Sin huesos reconocibles: nombre del GameObject/prefab como apoyo.
        string path = GetTransformPath(container.transform).ToLowerInvariant();

        if (path.Contains("tail"))
            return 3;

        if (path.Contains("bust") || path.Contains("breast") || path.Contains("mune"))
            return 2;

        if (path.Contains("head") || path.Contains("hair") || path.Contains("ear"))
            return 0;

        if (path.Contains("body") || path.Contains("cloth") || path.Contains("skirt"))
            return 1;

        return 1;
    }

    private int GuessCySpringBoneKind(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return -1;

        string name = boneName.ToLowerInvariant();

        if (name.Contains("tail"))
            return 3;

        if (name.Contains("bust") || name.Contains("breast") || name.Contains("mune"))
            return 2;

        if (name.Contains("head") || name.Contains("hair") || name.Contains("ear"))
            return 0;

        if (name.Contains("skirt") || name.Contains("cloth") || name.Contains("body"))
            return 1;

        return -1;
    }

    private string GetTransformPath(Transform t)
    {
        if (t == null)
            return string.Empty;

        string path = t.name;
        Transform parent = t.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    public void SetDynamicBoneEnable(bool isOn)
    {
        if (IsMini)
            return;

        EnablePhysics = isOn;

        // (P1 — port del camino ORIGINAL) El original conmuta la física ventana a ventana
        // sobre el CySpringDataContainer, que a su vez activa los DynamicBone que creó.
        // Antes este método solo llegaba al puente nativo (CySpringController), que ya no
        // se usa para el pelo.
        if (cySpringDataContainers != null)
        {
            foreach (CySpringDataContainer cySpring in cySpringDataContainers)
            {
                //(P1) EnablePhysics solo afecta a los DynamicBone que creó el camino original
                //(pelo/orejas/cola). Los contenedores de ropa los lleva el puente nativo y se
                //gobiernan con SetEnableAllCySpringBones, más abajo.
                if (cySpring != null && cySpring.DynamicBones != null && cySpring.DynamicBones.Count > 0)
                    cySpring.EnablePhysics(isOn);
            }
        }

        if (_cySpringController != null)
        {
            _cySpringController.SetEnableAllCySpringBones(isOn);

            if (isOn)
                _cySpringController.Reset();
        }
    }

    public void ConfigureLivePhysics()
    {
        if (!IsLive || IsMini || cySpringDataContainers == null) return;

        // (P1 — port del camino ORIGINAL) En el live la física va con las DOS vías: los
        // DynamicBone del pelo/orejas/cola y el puente nativo de la ropa.
        EnablePhysics = true;
        SetDynamicBoneEnable(true);
    }

    public void ResetDynamicBone()
    {
        if (IsMini)
            return;

        // (P1 — port del camino ORIGINAL) Reset sobre los DynamicBone de cada contenedor.
        if (cySpringDataContainers != null)
        {
            foreach (CySpringDataContainer cySpring in cySpringDataContainers)
            {
                //(P1) ResetPhysics solo afecta a los DynamicBone (pelo/orejas/cola).
                if (cySpring != null && cySpring.DynamicBones != null && cySpring.DynamicBones.Count > 0)
                    cySpring.ResetPhysics();
            }
        }

        //(P1) La ropa (puente nativo) también se reinicia.
        if (_cySpringController != null)
            _cySpringController.Reset();
    }

    public void SetEyeTracking(bool isOn)
    {
        EnableEyeTracking = isOn;
    }

    public void SetFaceOverrideData(bool isOn)
    {
        FaceOverrideData?.SetEnable(isOn);
    }

    private void LateUpdate()
    {
        if (IsMini)
            return;

        AlterLateUpdateRuntime();
        AlterLateUpdatePostRuntime();
    }

    public void SetShaderParameterRuntime()
    {
        InitFaceLightRuntime();
        UpdateFaceLightRuntime();

        // 官方这里后面还会做：
        // EyeHighlightController.DefaultEyeMaterial()
        // RendererHolder.SetShaderKeyword()
        // CharaPartsHolder.SetHairCutOff()
        // 现在先不补，先修 FaceLight。
    }

    private void AlterLateUpdateRuntime()
    {
        // (DATAMINE) UN paso por frame con el dt del frame, igual que el juego
        // (CySpringUpdater.BeginSimulation -> CySpring.BeginSimulation(Time.deltaTime)).
        //
        // Motivo MEDIDO: es un solver Verlet y su energía viene del desplazamiento del hueso
        // respecto al frame anterior (d = PrevTargetPosition - TargetPosition), pero el ANCLA
        // (SelfPosition) solo cambia UNA vez por frame (al recoger la pose animada en Begin).
        // Si se meten varios sub-pasos por frame, los sub-pasos extra resuelven con el ancla
        // quieta: la restriccion de longitud + la rigidez devuelven la cadena a la pose de
        // reposo y la tela/pelo se queda RIGIDA (medido: huesos con localRotation constante).
        if (EnablePhysics && _cySpringLoaded && _cySpringController != null)
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0f)
                dt = Time.unscaledDeltaTime;
            dt = Mathf.Clamp(dt, 0.0f, 0.25f);

            _cySpringController.BeginSimulation(dt, false);
            _cySpringController.EndSimulation();
        }
    }

    private void AlterLateUpdatePostRuntime()
    {
        // (TEMP) Interruptor de gizmos, cada frame y barato (dos flags estaticos): asi el check del
        // Inspector enciende/apaga las esferas al momento, sin esperar a que cambie un valor.
        CySpringColliderVisualizer.CollidersEnabled = CySpringHairColliderGizmos;
        CySpringColliderVisualizer.BonesEnabled = CySpringHairColliderGizmos;

        // Ajuste EN CALIENTE de los colisores generados del pelo: si cambias radio, cantidad o solape
        // en el Inspector durante el Play, se recolocan al instante (y se reconstruye el array nativo).
        ApplyArmColliderLayout(false);
        // Grosor del pelo (radios cian) tambien en caliente.
        ApplyHairBoneRadiusLayout(false);

        // ESCAPE: tras la simulacion, sacar del interior de las esferas los huesos atrapados (el
        // motor no puede: reproyecta la punta al largo del hueso y la vuelve a meter).
        ApplyHairEscapeFix();

        if (CySpringHairEscapeDiagnose)
        {
            _escapeFrameCounter++;
            if (_escapeFrameCounter % 60 == 0)
            {
                if (_escapeCorrections > 0 || _escapeImpossible > 0)
                {
                    Debug.Log(
                        $"[PeloEscape] huesos rescatados={_escapeCorrections} " +
                        $"(sin salida posible={_escapeImpossible}) giro_max={_escapeMaxAngle * Mathf.Rad2Deg:F1}deg");
                }
                _escapeCorrections = 0;
                _escapeImpossible = 0;
                _escapeMaxAngle = 0.0f;
            }
        }
        // (INTENTO 10 / DIAG) ¿La fisica MUEVE el pelo o lo deja clavado en su reposo?
        // Logea (5 veces, cada 60 frames) el desvio maximo entre la rotacion escrita y el reposo
        // qg = ParentRotation*InitLocalRotation. Si el desvio es ~0, la fisica no aporta nada y la
        // pose visible ES el reposo del modelo.
        if (CySpringDiagnoseHair && CySpringHairViaNative && _cySpringController != null)
        {
            _diagFrameCounter++;
            if (_diagFrameCounter % 60 == 0 && _diagLogsDone < 5)
            {
                _diagLogsDone++;
                CySpring head = _cySpringController.GetSpringArray(0);
                CySpringRootBone[] roots = head != null ? head.RootBoneArray : null;
                if (roots != null)
                {
                    for (int i = 0; i < roots.Length; i++)
                    {
                        CySpringRootBone r = roots[i];
                        if (r == null) continue;

                        var bl = new List<CySpringBoneBase>();
                        bl.Add(r);
                        if (r.ChildBoneList != null) bl.AddRange(r.ChildBoneList);

                        float worst = 0f;
                        float worstLink = 0f;
                        string worstLinkName = "-";
                        for (int b = 0; b < bl.Count; b++)
                        {
                            CySpringBoneBase bo = bl[b];
                            if (bo == null || bo.GameObject == null) continue;
                            NativeClothWorking nw = bo.NativeState;
                            Quaternion rest = nw.ParentRotation * nw.InitLocalRotation;
                            float dev = Quaternion.Angle(rest, bo.GameObject.transform.rotation);
                            if (dev > worst) worst = dev;

                            // Invariante del motor: el ancla de un hueso de la cadena es la punta del
                            // padre -> SelfPosition(hijo) debe coincidir con TargetPosition(padre).
                            if (b > 0)
                            {
                                CySpringBoneBase prev = bl[b - 1];
                                if (prev != null)
                                {
                                    float link = Vector3.Distance(nw.SelfPosition, prev.NativeState.TargetPosition);
                                    if (link > worstLink)
                                    {
                                        worstLink = link;
                                        worstLinkName = bo.BoneName;
                                    }
                                }
                            }
                        }
                    }
                }

                // (TEMP) Mantener sincronizados los gizmos del radio propio de cada hueso.
                if (CySpringHairColliderGizmos)
                    UpdateHairBoneVisualizers();
            }
        }

        // 官方AlterLateUpdatePost 是 EndSimulation 后 UpdateBodyLightDir / UpdateFaceLight。
        // 现在先补脸部，身体光照后面再看 UpdateBodyLightDir。
        UpdateFaceLightRuntime();
    }

    private void InitFaceLightRuntime()
    {
        if (_faceLightInitialized && FaceRenderer != null && HeadToonBaseTransform != null)
            return;

        if (FaceRenderer == null)
        {
            FaceRenderer = FindRendererByName(transform, "M_Face");
        }

        if (FaceRenderer != null)
        {
            _faceMaterials = FaceRenderer.materials;

            FaceMaterial = null;
            foreach (var mat in _faceMaterials)
            {
                if (mat == null || mat.shader == null)
                    continue;

                string matName = mat.name.ToLower();
                string shaderName = mat.shader.name;

                if (shaderName.Contains("ToonFace") || matName.Contains("face"))
                {
                    FaceMaterial = mat;
                    break;
                }
            }
        }

        if (HeadToonBaseTransform == null)
        {
            // 官方 InitFaceLight 默认就是 GetHeadTransform，不是 Position_light。
            if (HeadBone != null)
            {
                HeadToonBaseTransform = HeadBone.transform;
            }
            else
            {
                var head = FindTransformByName(transform, "Head");
                if (head != null)
                {
                    HeadToonBaseTransform = head;
                }
                else if (FaceRenderer is SkinnedMeshRenderer smr && smr.rootBone != null)
                {
                    HeadToonBaseTransform = smr.rootBone;
                }
            }
        }

        _faceLightInitialized = FaceRenderer != null && HeadToonBaseTransform != null;
    }

    private void UpdateFaceLightRuntime()
    {
        if (IsMini)
            return;

        if (!_faceLightInitialized || FaceRenderer == null || HeadToonBaseTransform == null)
        {
            InitFaceLightRuntime();
        }

        if (FaceRenderer == null || HeadToonBaseTransform == null)
            return;

        if (_faceMaterials == null || _faceMaterials.Length == 0)
            _faceMaterials = FaceRenderer.materials;

        Vector3 pos = HeadToonBaseTransform.position;
        Vector3 up = HeadToonBaseTransform.up.normalized;
        Vector3 forward = HeadToonBaseTransform.forward.normalized;

        if (FlipFaceForward)
            forward = -forward;

        Vector3 faceCenter =
            pos +
            up * FaceCenterUpOffset +
            forward * FaceCenterForwardOffset;

        Matrix4x4 headMat = HeadToonBaseTransform.worldToLocalMatrix;

        foreach (var mat in _faceMaterials)
        {
            if (mat == null || mat.shader == null)
                continue;

            string matName = mat.name.ToLower();
            string shaderName = mat.shader.name;

            if (!shaderName.Contains("ToonFace") && !matName.Contains("face"))
                continue;

            mat.SetVector(ID_FaceCenterPos, faceCenter);
            mat.SetVector(ID_FaceUp, up);
            mat.SetVector(ID_FaceForward, forward);
            mat.SetMatrix(ID_FaceShadowHeadMat, headMat);
        }
    }

    private static Transform FindTransformByName(Transform root, string name)
    {
        if (root == null)
            return null;

        var list = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].name == name)
                return list[i];
        }

        return null;
    }

    private static Renderer FindRendererByName(Transform root, string name)
    {
        if (root == null)
            return null;

        var list = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].name == name)
                return list[i];
        }

        return null;
    }
    private void FixedUpdate()
    {
        if (IsMini) return;

        if (TrackTarget && EnableEyeTracking && !isAnimatorControl)
        {
            if (IK && !IK.enabled)
            {
                IK.enabled = true;
            }

            var finalRotation = FaceDrivenKeyTarget.GetEyeTrackRotation(TrackTarget.transform.position);
            FaceDrivenKeyTarget.SetEyeTrack(finalRotation);

            var cam = Camera.main;
            //LookAt Camera
            TrackTarget.position = Vector3.Lerp(TrackTarget.position, cam.transform.position, Time.fixedDeltaTime * 3);
        }
        else
        {
            if (IK && IK.enabled)
            {
                IK.enabled = false;
            }
        }

        if (isAnimatorControl)
        {
            FaceDrivenKeyTarget.ProcessLocator();
        }

        

        TearControllers.ForEach(a => a.UpdateOffset());

        //Apply UV Animation

        if (HeadShaderEffectData != null)
        {
            HeadShaderEffectData.updateUV(Time.fixedDeltaTime);
        }

        if (BodyShaderEffectData != null)
        {
            BodyShaderEffectData.updateUV(Time.fixedDeltaTime);
        }
    }

    public void UpBodyReset()
    {
        if (UpBodyBone)
        {
            UpBodyBone.transform.localPosition = UpBodyPosition;
            UpBodyBone.transform.localRotation = UpBodyRotation;
        }
    }

    public void CreateIK()
    {
        if (IsMini) return;
        var container = this;
        var animator = UmaAnimator;
        BipedRagdollReferences r = BipedRagdollReferences.FromAvatar(animator);
        BipedRagdollCreator.Options options = BipedRagdollCreator.AutodetectOptions(r);
        options.spine = true;

        var ik = container.gameObject.AddComponent<BipedIK>();
        ik.references.root = container.transform;
        ik.references.pelvis = r.hips;
        ik.references.spine = new Transform[] { r.spine, r.chest };
        ik.references.leftThigh = r.leftUpperLeg;
        ik.references.leftCalf = r.leftLowerLeg;
        ik.references.leftFoot = r.leftFoot;
        ik.references.rightThigh = r.rightUpperLeg;
        ik.references.rightCalf = r.rightLowerLeg;
        ik.references.rightFoot = r.rightFoot;
        ik.references.leftUpperArm = r.leftUpperArm;
        ik.references.leftForearm = r.leftLowerArm;
        ik.references.leftHand = r.leftHand;
        ik.references.rightUpperArm = r.rightUpperArm;
        ik.references.rightForearm = r.rightLowerArm;
        ik.references.rightHand = r.rightHand;
        ik.references.head = r.head;

        new List<IKSolver>(ik.solvers.ikSolvers).ForEach(i => i.IKPositionWeight = 0);
        new List<IKSolverLimb>(ik.solvers.limbs).ForEach(i => i.IKRotationWeight = 0);
        ik.solvers.lookAt.IKPositionWeight = 1;
        ik.solvers.lookAt.headWeight = 0.8f;
        ik.solvers.lookAt.bodyWeight = 0.25f;
        ik.solvers.lookAt.clampWeightHead = 0.62f;
        ik.solvers.lookAt.target = TrackTarget.transform;
        ik.solvers.lookAt.spineWeightCurve = new AnimationCurve(new Keyframe[2] { new Keyframe(0f, 1f), new Keyframe(1f, 0.3f) });
        IK = ik;
    }

    public FullBodyBipedIK CreatePoseIK()
    {
        var container = this;
        BipedRagdollReferences r = BipedRagdollReferences.FromAvatar(UmaAnimator);
        BipedReferences pr = new BipedReferences();
        var ik = container.gameObject.AddComponent<FullBodyBipedIK>();

        pr.root = r.root;
        pr.pelvis = r.hips;
        pr.spine = new Transform[] { r.spine, r.chest };
        pr.leftThigh = r.leftUpperLeg;
        pr.leftCalf = r.leftLowerLeg;
        pr.leftFoot = r.leftFoot;
        pr.rightThigh = r.rightUpperLeg;
        pr.rightCalf = r.rightLowerLeg;
        pr.rightFoot = r.rightFoot;
        pr.leftUpperArm = r.leftUpperArm;
        pr.leftForearm = r.leftLowerArm;
        pr.leftHand = r.leftHand;
        pr.rightUpperArm = r.rightUpperArm;
        pr.rightForearm = r.rightLowerArm;
        pr.rightHand = r.rightHand;
        pr.head = r.head;
        ik.SetReferences(pr, r.hips);

        foreach (var effector in ik.solver.effectors)
        {
            var handle = handleRoot.ChildHandles.Find(h => h.Owner.transform.parent == effector.bone && (h as UIHandleBone).Tags.Contains(BoneTags.IK));
            if (handle)
            {
                effector.target = handle.Owner.transform;
                effector.positionWeight = 1;
                effector.rotationWeight = 1;
            }
        }

        ik.enabled = false;
        return ik;
    }

    public void LoadTextures(UmaDatabaseEntry entry)
    {
        foreach (Texture2D tex2D in entry.GetAll<Texture2D>())
        {
            if (entry.Name.Contains("/mini/head"))
            {
                MiniHeadTextures.Add(tex2D, entry);
            }
            else if (entry.Name.Contains("/tail/"))
            {
                TailTextures.Add(tex2D, entry);
            }
            else if (entry.Name.Contains("bdy0"))
            {
                GenericBodyTextures.Add(tex2D, entry);
            }
            else if (entry.Name.Contains("_face") || entry.Name.Contains("_hair"))
            {
                if (IsMob)
                    MobHeadTextures.Add(tex2D, entry);
            }
        }
    }

    public void LoadBody(UmaDatabaseEntry entry)
    {
        Body = InstantiateEntry(entry, transform);
        UmaAnimator = Body.GetComponent<Animator>();

        if (IsMini)
        {
            UpBodyBone = Body.transform.Find("Position/Hip").gameObject;
        }
        else
        {
            UpBodyBone = Body.GetComponent<AssetHolder>()._assetTable["upbody_ctrl"] as GameObject;
        }

        LoadSkirtController(entry);

        if (IsGeneric)
        {
            TextureList textures = GenericBodyTextures;
            string costumeIdShort = VarCostumeIdShort,
                   costumeIdLong = VarCostumeIdLong,
                   height = VarHeight,
                   skin = VarSkin,
                   socks = VarSocks,
                   bust = VarBust;

            foreach (Renderer r in Body.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.materials)
                {
                    m.name = m.name.Replace(" (Instance)", "");
                    string mainTex = "", toonMap = "", tripleMap = "", optionMap = "", zekkenNumberTex = "";

                    if (IsMini)
                    {
                        m.SetTexture("_MainTex", textures.Load(0));
                    }
                    else
                    {

                        if (m.shader.name.Contains("Noline") && m.shader.name.Contains("TSER"))
                        {
                            var s = Builder.ShaderList.Find(a => a.name == m.shader.name.Replace("Noline", "")); //Generic costume shader need to change manually.
                            if (s)
                            {
                                m.shader = s;
                            }
                        }

                        //BodyAlapha's shader need to change manually.


                        //修改(载入通用服装ColorSet相关)
                        if (m.name.Contains("bdy"))
                        {
                            if (m.name.Contains("Alpha"))
                            {
                                m.shader = UmaAssetManager.BodyAlphaShader;
                            }
                            else
                            {

                                var areaTexCandidates = UmaViewerMain.Instance.AbChara.Where(a => a.Name.StartsWith(UmaDatabaseController.BodyPath + $"bdy{costumeIdShort}/textures") && a.Name.EndsWith("area")).ToList();
                                if (areaTexCandidates.Count > 0)
                                {
                                    foreach (var areaEntry in areaTexCandidates)
                                    {
                                        LoadTextures(areaEntry);
                                    }
                                    var defaultAreaTex = GenericBodyTextures.Load(t => t.name.Contains(costumeIdShort) && t.name.EndsWith("0_area"));
                                    if (defaultAreaTex != null)
                                    {
                                        m.SetTexture("_MaskColorTex", defaultAreaTex);
                                    }
                                    if (IsMob)
                                    {
                                        SetMaskColor(m, MobDressColor, true);
                                    }
                                    else
                                    {
                                        ApplyDressColorAndArea(m);
                                    }
                                }
                            }
                        }



                        // Indice de color del traje comun (0 = el de siempre). Antes iba fijo a "00",
                        // por eso solo se veia el primer color (p.ej. la camisa blanca del 0015).
                        string colorIndex = (VarColorIndex < 0 ? 0 : VarColorIndex).ToString("D2");

                        switch (costumeIdShort.Split('_')[0]) //costume ID
                        {
                            case "0001":
                                switch (r.sharedMaterials.ToList().IndexOf(m))
                                {
                                    case 0:
                                        mainTex = $"tex_bdy{costumeIdShort}_00_waku0_diff";
                                        toonMap = $"tex_bdy{costumeIdShort}_00_waku0_shad_c";
                                        tripleMap = $"tex_bdy{costumeIdShort}_00_waku0_base";
                                        optionMap = $"tex_bdy{costumeIdShort}_00_waku0_ctrl";
                                        break;
                                    case 1:
                                        mainTex = $"tex_bdy{costumeIdShort}_00_{skin}_{bust}_{socks.PadLeft(2, '0')}_diff";
                                        toonMap = $"tex_bdy{costumeIdShort}_00_{skin}_{bust}_{socks.PadLeft(2, '0')}_shad_c";
                                        tripleMap = $"tex_bdy{costumeIdShort}_00_0_{bust}_00_base";
                                        optionMap = $"tex_bdy{costumeIdShort}_00_0_{bust}_00_ctrl";
                                        break;
                                    case 2:
                                        int color = UnityEngine.Random.Range(0, 4);
                                        mainTex = $"tex_bdy0001_00_zekken{color}_{bust}_diff";
                                        toonMap = $"tex_bdy0001_00_zekken{color}_{bust}_shad_c";
                                        tripleMap = $"tex_bdy0001_00_zekken0_{bust}_base";
                                        optionMap = $"tex_bdy0001_00_zekken0_{bust}_ctrl";
                                        break;
                                }

                                zekkenNumberTex = $"tex_bdy0001_00_num{UnityEngine.Random.Range(1, 18):d2}";
                                break;
                            case "0003":
                                mainTex = $"tex_bdy{costumeIdShort}_00_{skin}_{bust}_diff";
                                toonMap = $"tex_bdy{costumeIdShort}_00_{skin}_{bust}_shad_c";
                                tripleMap = $"tex_bdy{costumeIdShort}_00_0_{bust}_base";
                                optionMap = $"tex_bdy{costumeIdShort}_00_0_{bust}_ctrl";
                                break;
                            case "0006":
                            case "0009":
                            case "0015":
                                // (_diff y _shad_c son los unicos que cambian de color; _base/_ctrl
                                //  solo existen en "00", asi que se quedan fijos.)
                                mainTex = $"tex_bdy{costumeIdLong}_{skin}_{bust}_{colorIndex}_diff";
                                toonMap = $"tex_bdy{costumeIdLong}_{skin}_{bust}_{colorIndex}_shad_c";
                                tripleMap = $"tex_bdy{costumeIdLong}_0_{bust}_00_base";
                                optionMap = $"tex_bdy{costumeIdLong}_0_{bust}_00_ctrl";
                                break;
                            default:
                                mainTex = $"tex_bdy{costumeIdLong}_{skin}_{bust}_diff";
                                toonMap = $"tex_bdy{costumeIdLong}_{skin}_{bust}_shad_c";
                                tripleMap = $"tex_bdy{costumeIdLong}_0_{bust}_base";
                                optionMap = $"tex_bdy{costumeIdLong}_0_{bust}_ctrl";
                                break;
                        }
                        m.SetTexture("_MainTex", textures.Load(t => t.name == mainTex));
                        m.SetTexture("_ToonMap", textures.Load(t => t.name == toonMap));
                        m.SetTexture("_TripleMaskMap", textures.Load(t => t.name == tripleMap));
                        m.SetTexture("_OptionMaskMap", textures.Load(t => t.name == optionMap));

                        if (!string.IsNullOrEmpty(zekkenNumberTex))
                            m.SetTexture("_ZekkenNumberTex", textures.Load(t => t.name == zekkenNumberTex));
                    }
                }
            }
        }
        else
        {
            foreach (Renderer r in Body.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.sharedMaterials)
                {
                    //BodyAlapha's shader need to change manually.
                    if (m.name.Contains("bdy") && m.name.Contains("Alpha"))
                    {
                        m.shader = UmaAssetManager.BodyAlphaShader;
                    }
                }
            }
        }

        var assetholder = Body.GetComponent<AssetHolder>();
        if (assetholder)
        {
            BodyShaderEffectData = assetholder._assetTable["chara_shader_effect"] as CharaShaderEffectData;
            if (BodyShaderEffectData)
            {
                BodyShaderEffectData.Initialize();
            }
        }
        InvalidateMirrorLayerCache();
    }

    public void LoadHead(UmaDatabaseEntry entry)
    {
        var textures = MobHeadTextures;
        GameObject head = InstantiateEntry(entry, transform);
        Head = head;
        RegisterMirrorHeadRenderers(head);

        AssetTable table = null;
        if (!IsMini)
        {
            table = Head.GetComponent<AssetHolder>()._assetTable;
        }

        //Some setting for Head
        EnableEyeTracking = UI.ModelSettings.EnableEyeTracking;

        foreach (Renderer r in head.GetComponentsInChildren<Renderer>())
        {
            if (!IsMini && r.name == "M_Face")
            {
                FaceRenderer = r;
            }
            foreach (Material m in r.materials)
            {
                m.name = m.name.Replace(" (Instance)", "");
                //only for mini umas
                if (head.name.Contains("mchr"))
                {
                    if (r.name.Contains("Hair"))
                    {
                        Hair = head;
                    }
                    if (r.name == "M_Face")
                    {
                        m.SetTexture("_MainTex", MiniHeadTextures.Load(t => t.name.Contains("face") && t.name.Contains("diff")));
                    }
                    if (r.name == "M_Cheek")
                    {
                        m.CopyPropertiesFromMaterial(Builder.TransMaterialCharas);
                        m.SetTexture("_MainTex", MiniHeadTextures.Load(t => t.name.Contains("cheek")));
                    }
                    if (r.name == "M_Mouth")
                    {
                        m.SetTexture("_MainTex", MiniHeadTextures.Load(t => t.name.Contains("mouth")));
                    }
                    if (r.name == "M_Eye")
                    {
                        m.SetTexture("_MainTex", MiniHeadTextures.Load(t => t.name.Contains("eye")));
                    }
                    if (r.name.StartsWith("M_Mayu_"))
                    {
                        m.SetTexture("_MainTex", MiniHeadTextures.Load(t => t.name.Contains("mayu")));
                    }
                }
                else
                {
                    if (IsMob)
                    {
                        if (m.name.EndsWith("eye"))
                        {
                            m.SetTexture("_MainTex", textures.LoadLast(t => t.name.Contains("_eye") && t.name.EndsWith("eye0")));
                            m.SetTexture("_High0Tex", textures.LoadLast(t => t.name.Contains("_eye") && t.name.EndsWith("hi00")));
                            m.SetTexture("_High1Tex", textures.LoadLast(t => t.name.Contains("_eye") && t.name.EndsWith("hi01")));
                            m.SetTexture("_High2Tex", textures.LoadLast(t => t.name.Contains("_eye") && t.name.EndsWith("hi02")));
                            m.SetTexture("_MaskColorTex", textures.LoadLast(t => t.name.Contains("_eye") && t.name.EndsWith("area")));
                            SetMaskColor(m, MobHeadColor, "eye", false);
                        }
                        if (m.name.EndsWith("face"))
                        {
                            m.SetTexture("_MainTex", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("diff")));
                            m.SetTexture("_ToonMap", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("shad_c")));
                            m.SetTexture("_TripleMaskMap", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("base")));
                            m.SetTexture("_OptionMaskMap", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("ctrl")));
                            m.SetTexture("_MaskColorTex", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("area") && !t.name.Contains("_eye")));
                            SetMaskColor(m, MobHeadColor, "mayu", true);
                        }
                        if (m.name.EndsWith("mayu"))
                        {
                            m.SetTexture("_MainTex", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("diff")));
                            m.SetTexture("_MaskColorTex", textures.LoadLast(t => t.name.Contains("_face") && t.name.EndsWith("area") && !t.name.Contains("_eye")));
                            SetMaskColor(m, MobHeadColor, "mayu", true);
                        }
                    }

                    //Glasses's shader need to change manually.
                    if (r.name.Contains("Hair") && r.name.Contains("Alpha"))
                    {
                        m.shader = UmaAssetManager.AlphaShader;
                    }

                    //Blush Setting
                    if (r.name.Contains("Cheek"))
                    {
                        r.gameObject.SetActive(false);
                        if (IsMob)
                        {
                            CheekTex_0 = MobHeadTextures.LoadLast(a => a.name.Contains("cheek0"));
                            CheekTex_1 = MobHeadTextures.LoadLast(a => a.name.Contains("cheek1"));
                        }
                        else
                        {
                            CheekTex_0 = table["cheek0"] as Texture;
                            CheekTex_1 = table["cheek1"] as Texture;
                        }
                    }

                    if (Main.AbList.TryGetValue("3d/chara/common/textures/tex_chr_tear00", out var tearEntry))
                    {
                        LoadedAssets.Add(tearEntry);
                        var ab = UmaAssetManager.LoadAssetBundle(tearEntry, true, false);
                        var tex = ab.LoadAsset<Texture>("tex_chr_tear00");
                        StaticTear_L = table["tearmesh_l"] as GameObject;
                        StaticTear_R = table["tearmesh_r"] as GameObject;
                        if (StaticTear_L && StaticTear_R)
                        {
                            StaticTear_L.GetComponent<Renderer>().material.mainTexture = tex;
                            StaticTear_R.GetComponent<Renderer>().material.mainTexture = tex;
                        }
                    }

                    switch (m.shader.name)
                    {
                        case "Gallop/3D/Chara/MultiplyCheek":
                            m.shader = UmaAssetManager.CheekShader; ;
                            break;
                        case "Gallop/3D/Chara/ToonFace/TSER":
                            m.shader = UmaAssetManager.FaceShader;
                            m.SetFloat("_CylinderBlend", 0.25f);
                            m.SetColor("_RimColor", new Color(0, 0, 0, 0));
                            break;
                        case "Gallop/3D/Chara/ToonEye/T":
                            m.shader = UmaAssetManager.EyeShader;
                            m.SetFloat("_CylinderBlend", 0.25f);
                            break;
                        case "Gallop/3D/Chara/ToonHair/TSER":
                            m.shader = UmaAssetManager.HairShader;
                            m.SetFloat("_CylinderBlend", 0.25f);
                            break;
                        case "Gallop/3D/Chara/ToonMayu":
                            m.shader = UmaAssetManager.EyebrowShader;
                            m.renderQueue += 1; //fix eyebrows disappearing sometimes
                            break;
                        default:
                            // m.shader = Shader.Find("Nars/UmaMusume/Body");
                            break;
                    }
                    if (!IsMini && r.name == "M_Face")
                    {
                        string matName = m.name.ToLower();
                        string shaderName = m.shader != null ? m.shader.name : "";

                        if (shaderName.Contains("ToonFace") || matName.Contains("face"))
                        {
                            FaceMaterial = m;
                        }
                    }

                }

                m.SetFloat("_StencilMask", CharaEntry.Id);
            }
        }

        //shader effect
        var assetholder = head.GetComponent<AssetHolder>();
        if (assetholder)
        {
            HeadShaderEffectData = assetholder._assetTable["chara_shader_effect"] as CharaShaderEffectData;
            if (HeadShaderEffectData)
            {
                HeadShaderEffectData.Initialize();
            }
        }
    }

    public void LoadHair(UmaDatabaseEntry entry)
    {
        GameObject hair = InstantiateEntry(entry, transform);
        Hair = hair;
        RegisterMirrorHeadRenderers(hair);
        var textures = MobHeadTextures;
        foreach (Renderer r in hair.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.materials)
            {
                m.name = m.name.Replace(" (Instance)", "");
                //Glasses's shader need to change manually.
                if (r.name.Contains("Hair") && r.name.Contains("Alpha"))
                {
                    m.shader = UmaAssetManager.AlphaShader;
                }

                if (m.name.EndsWith("_hair"))
                {
                    m.SetTexture("_MainTex", textures.Load(t => t.name.Contains("_hair") && t.name.EndsWith("diff")));
                    m.SetTexture("_ToonMap", textures.Load(t => t.name.Contains("_hair") && t.name.EndsWith("shad_c")));
                    m.SetTexture("_OptionMaskMap", textures.Load(t => t.name.Contains("_hair") && t.name.EndsWith("ctrl")));
                    m.SetTexture("_MaskColorTex", textures.Load(t => t.name.Contains("_hair") && t.name.EndsWith("area")));
                    SetMaskColor(m, MobHeadColor, "hair", true);
                    if (IsMob)
                    {
                        var cutoff = CharaData["hair_cutoff"].ToString();
                        m.SetFloat("_Cutoff", int.Parse(cutoff) / 10000f); //Not entirely correct, but effective
                        m.SetTexture("_TripleMaskMap", textures.Load(t => t.name.Contains("_hair") && t.name.EndsWith(CharaData["sex"].ToString() + "_base")));
                    }
                    else
                    {
                        m.SetTexture("_TripleMaskMap", textures.Load(t => t.name.Contains("_hair") && t.name.EndsWith("base")));
                    }
                }

                switch (m.shader.name)
                {
                    case "Gallop/3D/Chara/ToonHair/TSER":
                        m.shader = UmaAssetManager.HairShader;
                        m.SetFloat("_CylinderBlend", 0.25f);
                        break;
                    default:
                        // m.shader = Shader.Find("Nars/UmaMusume/Body");
                        break;
                }
            }
        }
    }



    //修改(载入专用尾巴相关)
    public void LoadExclusiveTail(UmaDatabaseEntry entry)
    {
        GameObject go = entry.Get<GameObject>();
        Tail = Instantiate(go, transform);
        InvalidateMirrorLayerCache();
    }



    public void LoadTail(UmaDatabaseEntry entry)
    {
        Tail = InstantiateEntry(entry, transform);
        var textures = TailTextures;
        foreach (Renderer r in Tail.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.materials)
            {
                m.name = m.name.Replace(" (Instance)", "");
                m.SetTexture("_MainTex", textures.Load(t => t.name.EndsWith("diff")));
                m.SetTexture("_ToonMap", textures.Load(t => t.name.Contains("shad")));
                m.SetTexture("_TripleMaskMap", textures.Load(t => t.name.Contains("base")));
                m.SetTexture("_OptionMaskMap", textures.Load(t => t.name.Contains("ctrl")));
                if (IsMob)
                {
                    SetMaskColor(m, MobHeadColor, "tail", true);
                }
            }
        }
        InvalidateMirrorLayerCache();
    }

    public void LoadTear(UmaDatabaseEntry entry)
    {
        GameObject go = entry.Get<GameObject>();
        if (go.name.EndsWith("000"))
        {
            TearPrefab_0 = go;
        }
        else if (go.name.EndsWith("001"))
        {
            TearPrefab_1 = go;
        }
    }



    //修改(载入通用服装ColorSet相关)
    public void RefreshDressColor()
    {
        foreach (var materialHelper in Materials)
        {
            var mat = materialHelper.Mat;
            if (mat == null) continue;

            if (mat.name.Contains("bdy") && !mat.name.Contains("Alpha"))
            {
                if (mat.HasProperty("_MaskColorTex"))
                {
                    if (IsMob)
                    {
                        SetMaskColor(mat, MobDressColor, true);
                    }
                    else
                    {
                        ApplyDressColorAndArea(mat);
                    }
                }
            }
        }
    }



    //修改(载入通用服装ColorSet相关)
    private void ApplyDressColorAndArea(Material mat)
    {
        var uma = Builder.CurrentUMAContainer;
        if (uma == null || string.IsNullOrEmpty(uma.VarCostumeIdShort)) return;

        string selectedId = UmaViewerUI.CurrentSelectedColorSetId;
        var parts = uma.VarCostumeIdShort.Split('_');
        if (parts.Length < 2) return;

        if (!int.TryParse(parts[0], out int bodyType)) return;
        if (!int.TryParse(parts[1], out int bodyTypeSub)) return;

        DataRow charaColor = null;
        var dressRow = UmaDatabaseController.Instance.DressData.FirstOrDefault(row =>
            int.Parse(row["body_type"].ToString()) == bodyType &&
            int.Parse(row["body_type_sub"].ToString()) == bodyTypeSub);

        if (dressRow != null && dressRow["body_setting"].ToString() == "4")
        {
            string dressId = dressRow["id"].ToString();
            var colorSetRows = UmaDatabaseController.Instance.CharaDressColor
                .Where(row => row["dress_id"].ToString() == dressId).ToList();

            charaColor = !string.IsNullOrEmpty(selectedId)
                ? colorSetRows.FirstOrDefault(row => row["id"].ToString() == selectedId)
                : null;
            if (charaColor == null && colorSetRows.Count > 0) charaColor = colorSetRows.First();
        }
        else
        {
            charaColor = !string.IsNullOrEmpty(selectedId)
                ? UmaDatabaseController.Instance.CharaDressColor.FirstOrDefault(row => row["id"].ToString() == selectedId)
                : null;
            if (charaColor == null) charaColor = UmaDatabaseController.Instance.CharaDressColor.FirstOrDefault();
        }

        if (charaColor == null) return;

        Texture2D areaTex = null;
        try
        {
            if (charaColor.Table.Columns.Contains("area_map_id"))
            {
                int areaMapId = Convert.ToInt32(charaColor["area_map_id"]);
                string suffix = areaMapId.ToString("D2") + "_area";
                areaTex = GenericBodyTextures.Load(t => t.name.EndsWith(suffix));
            }
        }
        catch { }

        if (areaTex != null) mat.SetTexture("_MaskColorTex", areaTex);
        SetMaskColor(mat, charaColor, false);
    }



    private void SetMaskColor(Material mat, DataRow colordata, bool IsMob)
    {
        mat.EnableKeyword("USE_MASK_COLOR");
        Color c1, c2, c3, c4, c5, c6, t1, t2, t3, t4, t5, t6;
        if (IsMob)
        {
            ColorUtility.TryParseHtmlString(colordata["color_r1"].ToString(), out c1);
            ColorUtility.TryParseHtmlString(colordata["color_r2"].ToString(), out c2);
            ColorUtility.TryParseHtmlString(colordata["color_g1"].ToString(), out c3);
            ColorUtility.TryParseHtmlString(colordata["color_g2"].ToString(), out c4);
            ColorUtility.TryParseHtmlString(colordata["color_b1"].ToString(), out c5);
            ColorUtility.TryParseHtmlString(colordata["color_b2"].ToString(), out c6);
            ColorUtility.TryParseHtmlString(colordata["toon_color_r1"].ToString(), out t1);
            ColorUtility.TryParseHtmlString(colordata["toon_color_r2"].ToString(), out t2);
            ColorUtility.TryParseHtmlString(colordata["toon_color_g1"].ToString(), out t3);
            ColorUtility.TryParseHtmlString(colordata["toon_color_g2"].ToString(), out t4);
            ColorUtility.TryParseHtmlString(colordata["toon_color_b1"].ToString(), out t5);
            ColorUtility.TryParseHtmlString(colordata["toon_color_b2"].ToString(), out t6);
        }
        else
        {


            //修改(载入通用服装ColorSet相关)
            ColorUtility.TryParseHtmlString(colordata["color_r1"].ToString(), out c1);
            ColorUtility.TryParseHtmlString(colordata["color_r2"].ToString(), out c2);
            ColorUtility.TryParseHtmlString(colordata["color_g1"].ToString(), out c3);
            ColorUtility.TryParseHtmlString(colordata["color_g2"].ToString(), out c4);
            ColorUtility.TryParseHtmlString(colordata["color_b1"].ToString(), out c5);
            ColorUtility.TryParseHtmlString(colordata["color_b2"].ToString(), out c6);
            ColorUtility.TryParseHtmlString(colordata["toon_color_r1"].ToString(), out t1);
            ColorUtility.TryParseHtmlString(colordata["toon_color_r2"].ToString(), out t2);
            ColorUtility.TryParseHtmlString(colordata["toon_color_g1"].ToString(), out t3);
            ColorUtility.TryParseHtmlString(colordata["toon_color_g2"].ToString(), out t4);
            ColorUtility.TryParseHtmlString(colordata["toon_color_b1"].ToString(), out t5);
            ColorUtility.TryParseHtmlString(colordata["toon_color_b2"].ToString(), out t6);


        }

        mat.SetColor("_MaskColorR1", c1);
        mat.SetColor("_MaskColorR2", c2);
        mat.SetColor("_MaskColorG1", c3);
        mat.SetColor("_MaskColorG2", c4);
        mat.SetColor("_MaskColorB1", c5);
        mat.SetColor("_MaskColorB2", c6);
        mat.SetColor("_MaskToonColorR1", t1);
        mat.SetColor("_MaskToonColorR2", t2);
        mat.SetColor("_MaskToonColorG1", t3);
        mat.SetColor("_MaskToonColorG2", t4);
        mat.SetColor("_MaskToonColorB1", t5);
        mat.SetColor("_MaskToonColorB2", t6);
    }

    private void SetMaskColor(Material mat, DataRow colordata, string prefix, bool hastoon)
    {
        mat.EnableKeyword("USE_MASK_COLOR");
        Color c1, c2, c3, c4, c5, c6, t1, t2, t3, t4, t5, t6;
        ColorUtility.TryParseHtmlString(colordata[$"{prefix}_color_r1"].ToString(), out c1);
        ColorUtility.TryParseHtmlString(colordata[$"{prefix}_color_r2"].ToString(), out c2);
        ColorUtility.TryParseHtmlString(colordata[$"{prefix}_color_g1"].ToString(), out c3);
        ColorUtility.TryParseHtmlString(colordata[$"{prefix}_color_g2"].ToString(), out c4);
        ColorUtility.TryParseHtmlString(colordata[$"{prefix}_color_b1"].ToString(), out c5);
        ColorUtility.TryParseHtmlString(colordata[$"{prefix}_color_b2"].ToString(), out c6);
        mat.SetColor("_MaskColorR1", c1);
        mat.SetColor("_MaskColorR2", c2);
        mat.SetColor("_MaskColorG1", c3);
        mat.SetColor("_MaskColorG2", c4);
        mat.SetColor("_MaskColorB1", c5);
        mat.SetColor("_MaskColorB2", c6);
        if (hastoon)
        {
            ColorUtility.TryParseHtmlString(colordata[$"{prefix}_toon_color_r1"].ToString(), out t1);
            ColorUtility.TryParseHtmlString(colordata[$"{prefix}_toon_color_r2"].ToString(), out t2);
            ColorUtility.TryParseHtmlString(colordata[$"{prefix}_toon_color_g1"].ToString(), out t3);
            ColorUtility.TryParseHtmlString(colordata[$"{prefix}_toon_color_g2"].ToString(), out t4);
            ColorUtility.TryParseHtmlString(colordata[$"{prefix}_toon_color_b1"].ToString(), out t5);
            ColorUtility.TryParseHtmlString(colordata[$"{prefix}_toon_color_b2"].ToString(), out t6);
            mat.SetColor("_MaskToonColorR1", t1);
            mat.SetColor("_MaskToonColorR2", t2);
            mat.SetColor("_MaskToonColorG1", t3);
            mat.SetColor("_MaskToonColorG2", t4);
            mat.SetColor("_MaskToonColorB1", t5);
            mat.SetColor("_MaskToonColorB2", t6);
        }
    }

    public void LoadPhysics(UmaDatabaseEntry entry)
    {
        if (!PhysicsContainer)
        {
            PhysicsContainer = new GameObject("PhysicsController");
            PhysicsContainer.transform.SetParent(transform);
        }
        InstantiateEntry(entry, PhysicsContainer.transform);
    }

    public void LoadAnimation(UmaDatabaseEntry entry)
    {
        if (UI.LiveTime)
        {
            return;
        }

        var aClip = entry.Get<AnimationClip>();

        if (UmaAnimator)
        {
            //Debug.Log("LiveTime" + UI.LiveTime.ToString());
            aClip.name = entry.Name; // Need a complete path to find dependencies
            LoadAnimation(aClip);
            return;
        }

        if (aClip.name.Contains("tear"))
        {
            return;
        }
    }

    private void LoadAnimation(AnimationClip clip)
    {
        if (clip.name.EndsWith("_s"))
        {
            OverrideController["clip_s"] = clip;
        }
        else if (clip.name.EndsWith("_e"))
        {
            OverrideController["clip_e"] = clip;
        }
        else if (clip.name.Contains("tail"))
        {
            if (IsMini) return;
            UpBodyReset();
            OverrideController["clip_t"] = clip;
            UmaAnimator.Play("motion_t", 1, 0);
        }
        else if (clip.name.EndsWith("_face"))
        {
            if (IsMini) return;
            LoadFaceAnimation(clip);
        }
        else if (clip.name.Contains("_ear"))
        {
            if (IsMini) return;
            LoadEarAnimation(clip);
        }
        else if (clip.name.EndsWith("_pos"))
        {
            if (IsMini) return;
            OverrideController["clip_p"] = clip;
            UmaAnimator.Play("motion_p", 2, 0);
        }
        else if (clip.name.EndsWith("_cam"))
        {
            Builder.SetPreviewCamera(clip);
        }
        else if (clip.name.Contains("_loop"))
        {
            UpBodyReset();
            if (isAnimatorControl && FaceDrivenKeyTarget)
            {
                FaceDrivenKeyTarget.ResetLocator();
                isAnimatorControl = false;
            }

            if (Main.AbList.TryGetValue($"{clip.name.Replace("/body", "/facial")}_face", out UmaDatabaseEntry entry))
            {
                LoadAnimation(entry);
            }

            if (Main.AbList.TryGetValue($"{clip.name.Replace("/body", "/facial")}_ear", out entry))
            {
                LoadAnimation(entry);
            }

            UmaDatabaseEntry motion_e = null, motion_s = null;
            if (Main.AbList.TryGetValue(clip.name.Replace("_loop", "_s"), out motion_s))
            {
                LoadAnimation(motion_s);
            }

            if (OverrideController["clip_2"].name.Contains("_loop"))
            {
                if (!OverrideController["clip_2"].name.Contains("hom_"))//home end animation not for interpolation
                {
                    if (Main.AbList.TryGetValue(OverrideController["clip_2"].name.Replace("_loop", "_e"), out motion_e))
                    {
                        LoadAnimation(motion_e);
                    }
                }
            }

            Builder.SetPreviewCamera(null);
            OverrideController["clip_1"] = OverrideController["clip_2"];
            OverrideController["clip_2"] = clip;
            UmaAnimator.Play("motion_1", 0, 0);
            UmaAnimator.SetTrigger((motion_s != null && motion_e != null) ? "next_e" : ((motion_s != null) ? "next_s" : "next"));
        }
        else
        {
            if (FaceDrivenKeyTarget)
            {
                FaceDrivenKeyTarget.ResetLocator();
                isAnimatorControl = false;
            }
            UpBodyReset();
            var pos_weight = UmaAnimator.GetLayerWeight(2);
            UmaAnimator.Rebind();
            UmaAnimator.SetLayerWeight(2, pos_weight); //keep position layer weight
            OverrideController["clip_2"] = clip;
            // If Cut-in, play immediately without state interpolation

            if (Main.AbList.TryGetValue($"{clip.name.Replace("/body", "/facial")}_face", out UmaDatabaseEntry facialMotion))
            {
                LoadAnimation(facialMotion);
            }

            if (Main.AbList.TryGetValue($"{clip.name.Replace("/body", "/facial")}_ear", out UmaDatabaseEntry earMotion))
            {
                LoadAnimation(earMotion);
            }

            if (Main.AbList.TryGetValue($"{clip.name.Replace("/body", "/camera")}_cam", out UmaDatabaseEntry cameraMotion))
            {
                LoadAnimation(cameraMotion);
            }
            else
            {
                Builder.SetPreviewCamera(null);
            }

            if (Main.AbList.TryGetValue($"{clip.name.Replace("/body", "/position")}_pos", out UmaDatabaseEntry posMotion))
            {
                LoadAnimation(posMotion);
            }
            else
            {
                OverrideController["clip_p"] = null;
            }

            if (IsMini)
            {
                Builder.SetPreviewCamera(null);
            }

            if (clip.name.Contains("crd") || clip.name.Contains("res_chr"))
            {

                if (clip.name.Contains("_cti_crd"))
                {
                    ResetDynamicBone();
                    var dir = Path.GetDirectoryName(clip.name).Replace("\\", "/");
                    string[] param = Path.GetFileName(clip.name).Split('_');
                    if (param.Length > 4)
                    {
                        int index = int.Parse(param[4]);
                        if (index == 1)
                        {
                            var cur = index + 1;
                            while (true)
                            {
                                var nextSearch = $"{dir}/{param[0]}_{param[1]}_{param[2]}_{param[3]}_{cur.ToString().PadLeft(2, '0')}";
                                if (Main.AbList.TryGetValue(nextSearch, out UmaDatabaseEntry result))
                                {
                                    UmaAssetManager.LoadAssetBundle(result);
                                    cur++;
                                }
                                else break;
                            }
                        }

                        index++;
                        var next = $"{dir}/{param[0]}_{param[1]}_{param[2]}_{param[3]}_{index.ToString().PadLeft(2, '0')}";
                        if (Main.AbList.TryGetValue(next, out UmaDatabaseEntry nextMotion))
                        {
                            var aevent = new AnimationEvent
                            {
                                time = clip.length * 0.99f,
                                stringParameter = (nextMotion != null ? nextMotion.Name : null),
                                functionName = (nextMotion != null ? "SetNextAnimationCut" : "SetEndAnimationCut")
                            };
                            clip.AddEvent(aevent);
                        }
                    }
                }

            }

            UmaAnimator.Play("motion_2", 0, 0);
        }
    }

    private void LoadFaceAnimation(AnimationClip clip)
    {
        if (clip.name.Contains("_s_"))
        {
            FaceOverrideController["clip_s"] = clip;
        }
        else if (clip.name.Contains("_e_"))
        {
            FaceOverrideController["clip_e"] = clip;
        }
        else if (clip.name.Contains("_loop"))
        {
            isAnimatorControl = true;
            FaceDrivenKeyTarget.ResetLocator();
            UmaDatabaseEntry motion_e = null;
            UmaDatabaseEntry motion_s = null;
            if (Main.AbList.TryGetValue(clip.name.Replace("_loop", "_s"), out motion_s))
            {
                LoadAnimation(motion_s);
            }

            if (FaceOverrideController["clip_2"].name.Contains("_loop"))
            {
                if (!FaceOverrideController["clip_2"].name.Contains("hom_"))//home end animation not for interpolation
                {
                    if (Main.AbList.TryGetValue(FaceOverrideController["clip_2"].name.Replace("_loop", "_e"), out motion_e))
                    {
                        LoadAnimation(motion_e);
                    }
                }
            }

            FaceOverrideController["clip_1"] = FaceOverrideController["clip_2"];
            FaceOverrideController["clip_2"] = clip;
            UmaFaceAnimator.Play("motion_1", 0, 0);
            UmaFaceAnimator.SetTrigger((motion_s != null && motion_e != null) ? "next_e" : ((motion_s != null) ? "next_s" : "next"));
        }
        else
        {
            isAnimatorControl = true;
            FaceDrivenKeyTarget.ResetLocator();
            FaceOverrideController["clip_2"] = clip;
            UmaFaceAnimator.Play("motion_2", 0, 0);
        }
    }

    private void LoadEarAnimation(AnimationClip clip)
    {
        if (clip.name.Contains("_s_"))
        {
            FaceOverrideController["clip_s_ear"] = clip;
        }
        else if (clip.name.Contains("_e_"))
        {
            FaceOverrideController["clip_e_ear"] = clip;
        }
        else if (clip.name.Contains("_loop"))
        {
            UmaDatabaseEntry motion_e = null;
            UmaDatabaseEntry motion_s = null;
            if (Main.AbList.TryGetValue(clip.name.Replace("_loop", "_s"), out motion_s))
            {
                LoadAnimation(motion_s);
            }

            if (FaceOverrideController["clip_2_ear"].name.Contains("_loop"))
            {
                if (!FaceOverrideController["clip_2_ear"].name.Contains("hom_"))//home end animation not for interpolation
                {
                    if (Main.AbList.TryGetValue(FaceOverrideController["clip_2_ear"].name.Replace("_loop", "_e"), out motion_e))
                    {
                        LoadAnimation(motion_e);
                    }
                }
            }

            FaceOverrideController["clip_1_ear"] = FaceOverrideController["clip_2_ear"];
            FaceOverrideController["clip_2_ear"] = clip;
            UmaFaceAnimator.Play("motion_1", 1, 0);
            UmaFaceAnimator.SetTrigger((motion_s != null && motion_e != null) ? "next_e_ear" : ((motion_s != null) ? "next_s_ear" : "next_ear"));
        }
        else
        {
            if (FaceOverrideController["clip_2"].name == "clip_2")
            {
                isAnimatorControl = true;
                FaceDrivenKeyTarget.ResetLocator();
            }
            FaceOverrideController["clip_2_ear"] = clip;
            UmaFaceAnimator.Play("motion_2", 1, 0);
        }
    }

    public void SetNextAnimationCut(string cutName)
    {
        UmaViewerMain.Instance.AbList.TryGetValue(cutName, out UmaDatabaseEntry asset);
        LoadAnimation(asset);
    }

    public void SetEndAnimationCut()
    {
        UI.AnimationSettings.Pause();
    }

    public void LoadFaceMorph(int id, string costumeId)
    {
        if (!Head) return;
        var locatorEntry = Main.AbList["3d/animator/drivenkeylocator"];
        LoadedAssets.Add(locatorEntry);
        var bundle = UmaAssetManager.LoadAssetBundle(locatorEntry);
        var locator = Instantiate(bundle.LoadAsset("DrivenKeyLocator"), transform) as GameObject;
        locator.name = "DrivenKeyLocator";

        var headBone = (GameObject)Head.GetComponent<AssetHolder>()._assetTable["head"];
        var eyeLocator_L = headBone.transform.Find("Eye_target_locator_L");
        var eyeLocator_R = headBone.transform.Find("Eye_target_locator_R");

        var mangaEntry = new List<UmaDatabaseEntry>()
        {
            Main.AbList["3d/effect/charaemotion/pfb_eff_chr_emo_eye_000"],
            Main.AbList["3d/effect/charaemotion/pfb_eff_chr_emo_eye_001"],
            Main.AbList["3d/effect/charaemotion/pfb_eff_chr_emo_eye_002"],
            Main.AbList["3d/effect/charaemotion/pfb_eff_chr_emo_eye_003"],
        };
        var mangaObjects = new List<GameObject>();
        mangaEntry.ForEach(entry =>
        {
            LoadedAssets.Add(entry);
            AssetBundle ab = UmaAssetManager.LoadAssetBundle(entry);
            var obj = ab.LoadAsset(Path.GetFileNameWithoutExtension(entry.Name)) as GameObject;
            obj.SetActive(false);

            var leftObj = Instantiate(obj, eyeLocator_L.transform);
            new List<Renderer>(leftObj.GetComponentsInChildren<Renderer>(true)).ForEach(a => {
                a.material.SetFloat("_StencilMask", id);
                a.material.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
                a.material.SetFloat("_StencilOp", (float)UnityEngine.Rendering.StencilOp.Keep);
                a.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            });
            LeftMangaObject.Add(leftObj);

            var RightObj = Instantiate(obj, eyeLocator_R.transform);
            if (RightObj.TryGetComponent<AssetHolder>(out var holder))
            {
                if (holder._assetTableValue["invert"] > 0)
                    RightObj.transform.localScale = new Vector3(-1, 1, 1);
            }
            new List<Renderer>(RightObj.GetComponentsInChildren<Renderer>(true)).ForEach(a => {
                a.material.SetFloat("_StencilMask", id);
                a.material.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
                a.material.SetFloat("_StencilOp", (float)UnityEngine.Rendering.StencilOp.Keep);
                a.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            });
            RightMangaObject.Add(RightObj);
        });

        var tearEntry = new List<UmaDatabaseEntry>() {
            Main.AbList["3d/chara/common/tear/tear000/pfb_chr_tear000"],
            Main.AbList["3d/chara/common/tear/tear001/pfb_chr_tear001"],
        };

        if (tearEntry.Count > 0)
        {
            tearEntry.ForEach(a => LoadTear(a));
        }

        if (TearPrefab_0 && TearPrefab_1)
        {
            var p0 = TearPrefab_0;
            var p1 = TearPrefab_1;
            var t = headBone.transform;
            TearControllers.Add(new TearController(CharaEntry.Id, t, Instantiate(p0, t), Instantiate(p1, t), 0, 1));
            TearControllers.Add(new TearController(CharaEntry.Id, t, Instantiate(p0, t), Instantiate(p1, t), 1, 1));
            TearControllers.Add(new TearController(CharaEntry.Id, t, Instantiate(p0, t), Instantiate(p1, t), 0, 0));
            TearControllers.Add(new TearController(CharaEntry.Id, t, Instantiate(p0, t), Instantiate(p1, t), 1, 0));
        }

        var firsehead = Head;
        var faceDriven = Instantiate(firsehead.GetComponent<AssetHolder>()._assetTable["facial_target"]) as FaceDrivenKeyTarget;

        //Need Instantiate or not?
        var earDriven = firsehead.GetComponent<AssetHolder>()._assetTable["ear_target"] as DrivenKeyTarget;
        var faceOverride = firsehead.GetComponent<AssetHolder>()._assetTable["face_override"] as FaceOverrideData;

        faceDriven._earTarget = earDriven._targetFaces;
        FaceDrivenKeyTarget = faceDriven;
        FaceDrivenKeyTarget.Container = this;
        FaceOverrideData = faceOverride;
        faceOverride?.SetEnable(UI.ModelSettings.EnableFaceOverride);
        faceDriven.DrivenKeyLocator = locator.transform;
        faceDriven.Initialize(UmaUtility.ConvertArrayToDictionary(firsehead.GetComponentsInChildren<Transform>()));

        var emotionDriven = ScriptableObject.CreateInstance<FaceEmotionKeyTarget>();
        emotionDriven.name = $"char{id}_{costumeId}_emotion_target";
        FaceEmotionKeyTarget = emotionDriven;
        emotionDriven.FaceDrivenKeyTarget = faceDriven;
        emotionDriven.FaceEmotionKey = UmaDatabaseController.Instance.FaceTypeData;
        emotionDriven.Initialize();
        // Facial attachments are explicit head renderers, including inactive manga/tears.
        RegisterMirrorHeadRenderers(headBone);
    }

    public void SetupBoneHandles()
    {
        if (IsLive) return;
        UIHandleCharacterRoot rootHandle = UIHandleCharacterRoot.CreateAsChild(this);

        var humanBones = GetHumanBones();
        var ikNames = new[] { "Hip", "Wrist_L", "Wrist_R", "Ankle_L", "Ankle_R" };
        foreach (var bone in humanBones)
        {
            List<BoneTags> tags = new List<BoneTags>() { BoneTags.Humanoid };
            if (bone.name.EndsWith("_L")) tags.Add(BoneTags.Left);
            if (bone.name.EndsWith("_R")) tags.Add(BoneTags.Right);

            if (ikNames.Any(x => x == bone.name))
            {
                var go = new GameObject(bone.name + "_IK_Handle");
                go.transform.parent = bone.transform;
                go.transform.localPosition = Vector3.zero;
                go.transform.rotation = bone.transform.rotation;
                go.transform.localScale = Vector3.one;
                var handle = UIHandleBone.CreateAsChild(go, new List<BoneTags>() { BoneTags.IK });
                rootHandle.ChildHandles.Add(handle);
            }

            if (bone.name != "Hip")
            {
                var handle = UIHandleBone.CreateAsChild(bone, tags).WithLineRenderer();
                rootHandle.ChildHandles.Add(handle);
            }
            else
            {
                var handle = UIHandleBone.CreateAsChild(bone, tags);
                rootHandle.ChildHandles.Add(handle);
            }
        }

        var wrist_L = humanBones.Find(b => b.name == "Wrist_L");
        foreach (var fingerBone in wrist_L.transform.GetComponentsInChildren<Transform>())
        {
            if (fingerBone.name.StartsWith("Index") || fingerBone.name.StartsWith("Middle") || fingerBone.name.StartsWith("Ring") || fingerBone.name.StartsWith("Pinky") || fingerBone.name.StartsWith("Thumb"))
            {
                List<BoneTags> tags = new List<BoneTags>() { BoneTags.Left, BoneTags.Finger };

                var handle = UIHandleBone.CreateAsChild(fingerBone, tags).SetScale(0.25f).WithLineRenderer();
                rootHandle.ChildHandles.Add(handle);
            }
        }

        var wrist_R = humanBones.Find(b => b.name == "Wrist_R");
        foreach (var fingerBone in wrist_R.transform.GetComponentsInChildren<Transform>())
        {
            if (fingerBone.name.StartsWith("Index") || fingerBone.name.StartsWith("Middle") || fingerBone.name.StartsWith("Ring") || fingerBone.name.StartsWith("Pinky") || fingerBone.name.StartsWith("Thumb"))
            {
                List<BoneTags> tags = new List<BoneTags>() { BoneTags.Right, BoneTags.Finger };

                var handle = UIHandleBone.CreateAsChild(fingerBone, tags).SetScale(0.25f).WithLineRenderer();
                rootHandle.ChildHandles.Add(handle);
            }
        }

        var allBones = SaveBones();
        foreach (var bone in allBones.Where(b => b.Tags.Contains(BoneTags.Dynamic)))
        {
            var handle = UIHandleBone.CreateAsChild(bone.Bone, bone.Tags).SetColor(Color.gray).SetScale(0.15f).WithLineRenderer();
            rootHandle.ChildHandles.Add(handle);
        }

        handleRoot = rootHandle;
    }

    public List<SerializableBone> SaveBones()
    {
        var boneList = new List<SerializableBone>();

        GatherSerializableBonesRecursive(Position, boneList, 0);

        return boneList;
    }

    private void GatherSerializableBonesRecursive(Transform current, List<SerializableBone> bones, int depth)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            GatherSerializableBonesRecursive(current.GetChild(i), bones, depth + 1);
        }

        //this can be optimized by keeping track if the bone is dynamic
        //and generating a list of tags beforehand
        //otherwise getComponentInParent() is called for every bone
        var bone = new SerializableBone(current, true);

        if (depth == 0)
        {
            //make it independent from character name
            bone.ParentName = "root";
        }

        bones.Add(bone);
    }

    public bool LoadBones(PoseData data, PoseLoadOptions options)
    {
        var currentBones = SaveBones();

        foreach (var bone in currentBones)
        {
            if (!options.Root && bone.ParentName == "root") continue;
            if (!options.Physics && bone.Tags.Contains(BoneTags.Dynamic)) continue;

            if (bone.Bone == null)
            {
                Debug.LogError($"Target bone {bone.Name} does not reference a bone in the scene.");
                continue;
            }

            var savedBone = data.Bones.FirstOrDefault(b => b.Name == bone.Name && b.ParentName == bone.ParentName);
            if (savedBone == null) continue;

            savedBone.Transform.ApplyTo(bone.Bone, options);
        }

        //could add a 'bone not found' threshold for returning false
        return true;
    }

    public List<Transform> GetHumanBones()
    {
        if (_humanoidBones == null)
        {
            var animator = UmaAnimator;

            List<Transform> list = new List<Transform>(animator.gameObject.GetComponentsInChildren<Transform>());

            _humanoidBones = new List<Transform>()
            {
                list.Find(t => t.name.Equals("Hip")),
                list.Find(t => t.name.Equals("Waist")),
                list.Find(t => t.name.Equals("Spine")),
                list.Find(t => t.name.Equals("Chest")),
                list.Find(t => t.name.Equals("Neck")),
                list.Find(t => t.name.Equals("Head")),

                list.Find(t => t.name.Equals("Shoulder_L")),
                list.Find(t => t.name.Equals("Arm_L")),
                list.Find(t => t.name.Equals("Elbow_L")),
                list.Find(t => t.name.Equals("Wrist_L")),

                list.Find(t => t.name.Equals("Shoulder_R")),
                list.Find(t => t.name.Equals("Arm_R")),
                list.Find(t => t.name.Equals("Elbow_R")),
                list.Find(t => t.name.Equals("Wrist_R")),

                list.Find(t => t.name.Equals("Thigh_L")),
                list.Find(t => t.name.Equals("Knee_L")),
                list.Find(t => t.name.Equals("Ankle_L")),
                list.Find(t => t.name.Equals("Toe_L")),

                list.Find(t => t.name.Equals("Thigh_R")),
                list.Find(t => t.name.Equals("Knee_R")),
                list.Find(t => t.name.Equals("Ankle_R")),
                list.Find(t => t.name.Equals("Toe_R"))
            };
        }

        return _humanoidBones.Where(b => b != null).ToList();
    }

    public List<SerializableMorph> SaveMorphs(bool changedOnly)
    {
        if (FaceDrivenKeyTarget == null)
        {
            return new List<SerializableMorph>();
        }

        if (changedOnly)
        {
            return FaceDrivenKeyTarget.AllMorphs.Where(morph => morph.weight != 0).Select(morph => new SerializableMorph(morph)).ToList();
        }
        else
        {
            return FaceDrivenKeyTarget.AllMorphs.Select(morph => new SerializableMorph(morph)).ToList();
        }
    }

    public bool LoadMorphs(PoseData data)
    {
        var currentMorphs = SaveMorphs(false);

        foreach (var morph in currentMorphs)
        {
            var savedMorph = data.Morphs.FirstOrDefault(b => b.Name == morph.Name);
            if (savedMorph == null)
            {
                morph.Morph.weight = 0;
            }
            else
            {
                savedMorph.ApplyTo(this, morph.Morph, false);
            }
        }

        this.FaceDrivenKeyTarget.ChangeMorph();

        return true;
    }

    private void CaptureInitialBodyPose()
    {
        InitBoneTransform =
            new Dictionary<Transform, (Vector3 localPosition, Quaternion localRotation, Vector3 localScale)>();

        // 覆盖所有可能被 Animator 驱动的模型节点，避免特殊动作残留在控制节点或辅助骨骼中。
        foreach (Transform modelTransform in GetComponentsInChildren<Transform>(true))
        {
            if (modelTransform == transform)
            {
                continue;
            }

            InitBoneTransform[modelTransform] = (
                modelTransform.localPosition,
                modelTransform.localRotation,
                modelTransform.localScale);
        }
    }

    public void ResetBodyPose()
    {
        if (InitBoneTransform == null)
        {
            return;
        }

        // 使用本地变换恢复，避免父节点缩放或旋转使子骨骼的世界坐标发生二次偏移。
        foreach (var pair in InitBoneTransform)
        {
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.localPosition = pair.Value.localPosition;
            pair.Key.localRotation = pair.Value.localRotation;
            pair.Key.localScale = pair.Value.localScale;
        }
    }

    public List<UmaDatabaseEntry> GetAssetsList()
    {
        return LoadedAssets
            .Concat(TailTextures.GetLoadedAssets())
            .Concat(GenericBodyTextures.GetLoadedAssets())
            .Concat(MiniHeadTextures.GetLoadedAssets())
            .Concat(MobHeadTextures.GetLoadedAssets())
            .Distinct().ToList();
    }

    private GameObject InstantiateEntry(UmaDatabaseEntry entry, Transform parent)
    {
        if (!LoadedAssets.Contains(entry))
        {
            foreach (var asset in UmaAssetManager.SearchAB(UmaViewerMain.Instance, entry))
            {
                if (!LoadedAssets.Contains(asset))
                {
                    LoadedAssets.Add(asset);
                }
            }
        }
        return Instantiate(entry.Get<GameObject>(), parent.transform);
    }
}
