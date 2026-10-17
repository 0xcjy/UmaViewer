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

    // (DATAMINE) El motor CySpring es de paso fijo con ramas 30/60 fps (ver docs del datamine
    // 03-algoritmo-motor.md): a 30 fps la fuerza se multiplica por 3 en vez de 1.5. Simular con
    // Time.deltaTime variable (p.ej. a 120 fps) da un comportamiento que el solver no espera.
    // -> Se simula en sub-pasos fijos de 1/60 (coherente con _targetCySpringFpsMode = 60).
    [Header("CySpring timing (datamine)")]
    [Tooltip("Paso fijo de simulacion CySpring (s). 1/60 = 60 fps (rama 60 del motor).")]
    public float CySpringFixedStep = 1.0f / 60.0f;
    [Tooltip("Sub-pasos maximos por frame (evita espirales si cae el fps).")]
    public int CySpringMaxSubSteps = 4;
    [Tooltip("Warm-up (s) al cargar: asienta la cadena antes de mostrarla (oficial: DEFAULT_WARMUPTIME). 0 = off.")]
    public float CySpringWarmUpTime = 1.0f;
    [Tooltip("Enrutar el contenedor de CABEZA (pelo) al camino NATIVO CySpring en vez de DynamicBone.")]
    public bool CySpringHairViaNative = true;

    private float _cySpringAccumulator;

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
                    matHlp.Toggle.Toggle.onValueChanged.AddListener((value) => { Debug.Log("e"); matHlp.ToggleMaterials(value); });
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

        // (P1) Física de ropa/pelo: DOS vías, una por tipo de prenda.
        //
        // - DynamicBone (camino del proyecto ORIGINAL): pelo, orejas y cola. Funciona SIN
        //   CySpringPlugin.dll. En el original la ROPA tampoco funcionaba por aquí.
        // - Puente nativo CySpringController + CySpringPlugin.dll (camino del fork): la falda
        //   y el resto de ropa. Es lo que arregló la ropa al añadir la DLL.
        //
        // Se reparten por tipo para que no se peleen por los mismos huesos: DynamicBone se
        // queda con head (pelo/orejas) y tail (cola); el puente nativo con body (falda) y bust.
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

        // (DIAG T012a) Banco de colisores realmente creado desde los datos. Sirve para ver si
        // EXISTE el colisor que falta (p. ej. un glúteo) o si un nombre no resuelve. Quitar
        // cuando T012a esté cerrada en Play Mode.
        Debug.Log(
            $"[CySpring][ColBank] {name}: dbColliders={dbColliders.Count} -> " +
            string.Join(",", dbColliders.Keys)
        );

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

        // (DATAMINE/P1) Warm-up oficial: el juego asienta la cadena antes de mostrarla
        // (CharaPropController.ReserveWarmingUpCySpring + TryUpdateWarmUp). Sin esto la pose
        // simulada arranca "clavada" en la de carga.
        if (CySpringWarmUpTime > 0.0f)
        {
            _cySpringController.ReserveWarmingUpCySpring(CySpringWarmUpTime);
            _cySpringController.TryUpdateWarmUp();
        }

        LinkSkirtControllerToCySpring();

        Debug.Log(
            "[UmaContainerCharacter] CySpring loaded. " +
            $"head={(slots.head ? slots.head.name : "null")}, " +
            $"body={(slots.body ? slots.body.name : "null")}, " +
            $"bust={(slots.bust ? slots.bust.name : "null")}, " +
            $"tail={(slots.tail ? slots.tail.name : "null")}"
        );
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

    /// <summary>
    /// Une a cada hijo de una cadena de falda los nombres de colision que declara la raiz.
    /// La raiz nombra el volumen de su sector de cadera ademas de las capsulas del muslo; los
    /// hijos solo nombran las capsulas, asi que la tela podia plegarse hacia dentro en el
    /// gluteo. Con la herencia, todos los huesos de la cadena colisionan contra el mismo set.
    /// </summary>
    private static void PropagateClothCollisionNames(CySpringDataContainer container)
    {
        if (container == null || container.springParam == null)
            return;

        for (int i = 0; i < container.springParam.Count; i++)
        {
            Gallop.CySpringParamDataElement spring = container.springParam[i];
            if (spring == null || spring._boneName == null || spring._collisionNameList == null)
                continue;

            if (spring._boneName.IndexOf("MSkirt", StringComparison.OrdinalIgnoreCase) < 0)
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

        Debug.Log($"[UmaContainerCharacter] ReloadClothPhysics OK (duplicados={duplicates.Count}).");
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

        Debug.Log(
            $"[UmaContainerCharacter] SkirtController loaded param={skirtParam.name}, " +
            $"charaId={_skirtController.CharaId}, body={bodyEntry.Name}");
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

            Debug.Log(
                $"[UmaContainerCharacter] Skirt candidate {asset.name}: " +
                $"score={score}, charaOverride={hasCharaOverride}, entry={entry.Name}");
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
        // (DATAMINE/P3) El motor es de paso FIJO (ramas 30/60 fps; ver datamine 03). En vez de
        // simular con el Time.deltaTime variable del render (aquí el target es 120 fps), se
        // acumula y se simulan sub-pasos fijos de CySpringFixedStep. Cada sub-paso va con
        // Begin+End pareados, que es exactamente el patrón de CySpringController.WarmUpCySpring.
        // La ROPA (y, si CySpringHairViaNative, el pelo) la lleva el puente nativo.
        if (EnablePhysics && _cySpringLoaded && _cySpringController != null)
        {
            float step = Mathf.Max(0.0001f, CySpringFixedStep);
            int maxSub = Mathf.Max(1, CySpringMaxSubSteps);

            _cySpringAccumulator += Mathf.Min(Time.deltaTime, 0.25f);

            int steps = 0;
            while (_cySpringAccumulator >= step && steps < maxSub)
            {
                _cySpringController.BeginSimulation(step, false);
                _cySpringController.EndSimulation();
                _cySpringAccumulator -= step;
                steps++;
            }

            // Si se agotó el cupo de sub-pasos, se descarta el retraso (no acumular backlog).
            if (steps >= maxSub)
                _cySpringAccumulator = 0.0f;
        }
    }

    private void AlterLateUpdatePostRuntime()
    {
        // (DATAMINE/P3) El EndSimulation va pareado con BeginSimulation en
        // AlterLateUpdateRuntime (sub-paso a sub-paso), así que aquí ya NO se cierra simulación.

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

        Debug.Log(
            $"[FaceLight] init={_faceLightInitialized}, " +
            $"FaceRenderer={(FaceRenderer ? FaceRenderer.name : "null")}, " +
            $"FaceMaterial={(FaceMaterial ? FaceMaterial.name : "null")}, " +
            $"HeadToonBase={(HeadToonBaseTransform ? HeadToonBaseTransform.name : "null")}"
        );
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
                                mainTex = $"tex_bdy{costumeIdLong}_{skin}_{bust}_{"00"}_diff";
                                toonMap = $"tex_bdy{costumeIdLong}_{skin}_{bust}_{"00"}_shad_c";
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
                        Debug.Log("Looking for texture " + mainTex);
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
                            Debug.Log(m.shader.name);
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
                        Debug.Log(m.shader.name);
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
