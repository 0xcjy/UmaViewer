using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Gallop
{
    //æŽ§åˆ¶è£™æ‘†éª¨éª¼çš„ç¢°æ’žä¿®æ­£ã€è§’åº¦æ›´æ–°ä»¥åŠä¸ŽCySpringçš„è”åŠ¨
    public class SkirtController : MonoBehaviour
    {

        public NativeSkirtWorking[] NativeWorkingArray;

        public NativeSkirtArg NativeArg;

        public bool IsEnableSkirt
        {
            get { return _isEnableSkirt; }
            set { _isEnableSkirt = value; }
        }

        public int CharaId { get; set; }
        public float ModelScale { get; set; }
        public bool IsGeneralDress { get; set; }

        private bool _isScaleCollisionEnabled = true;

        [SerializeField] private SkirtParamDataAsset _paramDataAsset;
        [SerializeField] private Transform _center;
        [SerializeField] private Transform _kneeL;
        [SerializeField] private Transform _kneeR;
        [SerializeField] private Transform _ankleL;
        [SerializeField] private Transform _ankleR;

        [SerializeField] private float _kneeColliderRadius = 0.06f;
        [SerializeField] private float _ankleColliderRadius = 0.06f;
        [SerializeField] private float _influenceAngle = 30.0f;
        [SerializeField] private float _influenceMaxAngle = 90.0f;

        private bool _isOffsetAngleTargetEndNode=true;
        private float _defaultKneeColliderRadius;
        private float _defaultAnkleColliderRadius;

        [SerializeField] private SkirtData[] _skirtDataArray;

        // Read-only accessors used by the PMX skirt-physics export tooling.
        public SkirtData[] SkirtDataArray
        {
            get { return _skirtDataArray; }
        }

        public Transform CenterBone
        {
            get { return _center; }
        }

        public Transform KneeLBone
        {
            get { return _kneeL; }
        }

        public Transform KneeRBone
        {
            get { return _kneeR; }
        }

        public Transform AnkleLBone
        {
            get { return _ankleL; }
        }

        public Transform AnkleRBone
        {
            get { return _ankleR; }
        }

        private Transform _cachedTransform;
        private bool _isInitialized;

        private bool _isEnableSkirt = true;
        private Func<bool> _isEnableCySpringFunc;

        // (FIX ancla del plano de falda) Nodo cuyo espacio usa el solver. Tiene que ser el MISMO
        // owner de CySpring (el nodo "Position"), porque `UpdatePrevousPosture` lo deja en
        // (0,0,0)/identidad durante el step y ahi es donde estan las posiciones de hueso contra
        // las que se compara el plano. Antes se usaba el contenedor (Chara_XXXX_00), que NO se
        // pone a cero: el plano quedaba desplazado por la posicion del personaje. Medido en el
        // live 1001 (contenedor en x=8): el panel izquierdo recibia un empuje de 8,47 m y el
        // frontal ninguno. Con el ancla correcta, el empuje es 0,02-0,03 m (el radio del hueso).
        [SerializeField] private Transform _rootAnchor;

        public void SetRootAnchor(Transform anchor)
        {
            if (anchor != null)
                _rootAnchor = anchor;
        }

        private static readonly Vector3 SkirtNormal = Vector3.up;
        private static readonly Vector3 RotAxis = Vector3.left;

        public float KneeColliderRadius
        {
            get { return _kneeColliderRadius; }
        }

        public float AnkleColliderRadius
        {
            get { return _ankleColliderRadius; }
        }

        public float InfluenceAngle
        {
            get { return _influenceAngle; }
        }

        public float InfluenceMaxAngle
        {
            get { return _influenceMaxAngle; }
        }


        private Transform CachedTransform
        {
            get
            {
                if (_cachedTransform == null)
                    _cachedTransform = transform;
                return _cachedTransform;
            }
        }

        public SkirtController()
        {
            CharaId = -1;
            ModelScale = 1.0f;

            _kneeColliderRadius = 0.06f;
            _ankleColliderRadius = 0.06f;
            _influenceAngle = 30.0f;
            _influenceMaxAngle = 90.0f;

            _isOffsetAngleTargetEndNode = true;
            _isEnableSkirt = true;
        }

        private void Reset()
        {
            ResolveBasicBones();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                ResolveBasicBones();
        }

        private void Awake()
        {
            if (!_isInitialized)
            {
                if (_paramDataAsset != null)
                    Create(_paramDataAsset);
                else
                    ResolveBasicBones();
            }
        }

        

        public void Create(SkirtParamDataAsset data)
        {
            _paramDataAsset = data;

            if (ModelScale <= 0.0f)
                ModelScale = 1.0f;

            ApplyParamAsset(data);

            ResolveBasicBones();

            _defaultKneeColliderRadius = _kneeColliderRadius;
            _defaultAnkleColliderRadius = _ankleColliderRadius;

            ScaleCollision();
            EnsureNativeWorkingArray();
            InitializeSkirtData();
            InitializeNativeWorkingStaticFlags();

            // La referencia de reposo (Init*) se captura AQUI, en el bind, igual que hace la
            // fisica de CySpring: si se capturase en el primer frame de juego la pose podria ser
            // cualquiera (los personajes que entran a mitad de cancion ya estan girados/posados).
            _UpdateSkirtArg();

            _isInitialized = true;
            _isEnableSkirt = true;
        }

        private void ScaleCollision()
        {
            if (!_isScaleCollisionEnabled)
                return;

            float scale = ModelScale;

            float knee = (_defaultKneeColliderRadius * scale) * 1000.0f;
            _kneeColliderRadius = Math.Floor(knee) / 1000.0f;

            float ankle = (_defaultAnkleColliderRadius * scale) * 1000.0f;
            _ankleColliderRadius = Math.Floor(ankle) / 1000.0f;
        }

        public void CreateLinkCySpring(CySpringRootBone[] rootBoneList, Func<bool> isEnableCySpringFunc)
        {
            if (rootBoneList == null || _skirtDataArray == null)
            {
                _isEnableCySpringFunc = isEnableCySpringFunc;
                return;
            }

            EnsureNativeWorkingArray();

            for (int i = 0; i < _skirtDataArray.Length; i++)
            {
                if (_skirtDataArray[i] != null)
                    _skirtDataArray[i].CySpringRoot = null;
            }

            for (int r = 0; r < rootBoneList.Length; r++)
            {
                CySpringRootBone rootBone = rootBoneList[r];

                if (rootBone == null)
                    continue;

                rootBone.LinkSkirtIndex = -1;

                if (rootBone.ChildBoneList == null || rootBone.ChildBoneList.Count < 1)
                    continue;

                if (rootBone.GameObject == null)
                    continue;

                CySpringBoneBase firstChild = rootBone.ChildBoneList[0];

                if (firstChild == null)
                    continue;

                Vector3 rootPos = rootBone.GameObject.transform.position;

                for (int s = 0; s < _skirtDataArray.Length; s++)
                {
                    SkirtData skirtData = _skirtDataArray[s];

                    if (skirtData == null)
                        continue;

                    if (skirtData.SkirtRoot == null || skirtData.SkirtChild == null)
                        continue;

                    if (!string.Equals(skirtData.SkirtRoot.name, rootBone.BoneName, StringComparison.Ordinal))
                        continue;

                    if (!string.Equals(skirtData.SkirtChild.name, firstChild.BoneName, StringComparison.Ordinal))
                        continue;

                    skirtData.CySpringRoot = rootBone;
                    rootBone.LinkSkirtIndex = s;

                    float offsetAngle = CalcSkirtOffsetAngle(rootBone, rootPos);
                    skirtData.OffsetAngle = offsetAngle;

                    if (NativeWorkingArray != null && s < NativeWorkingArray.Length)
                    {
                        NativeSkirtWorking work = NativeWorkingArray[s];
                        work.OffsetAngle = offsetAngle;
                        NativeWorkingArray[s] = work;
                    }

                    break;
                }
            }

            _isEnableCySpringFunc = isEnableCySpringFunc;
        }

        private float CalcSkirtOffsetAngle(CySpringRootBone rootBone, Vector3 rootPos)
        {
            if (rootBone == null || rootBone.ChildBoneList == null || rootBone.ChildBoneList.Count < 1)
                return 0.0f;

            float maxRad = 0.0f;
            int count = rootBone.ChildBoneList.Count;

            CySpringBoneBase first = rootBone.ChildBoneList[0];
            if (first != null && first.GameObject != null)
            {
                float d = Vector3.Distance(first.GameObject.transform.position, rootPos);

                if (d > 0.0001f)
                {
                    float v = Mathf.Clamp(rootBone.CollisionRadius / d, -1.0f, 1.0f);
                    maxRad = Mathf.Asin(v);
                }
            }

            if (_isOffsetAngleTargetEndNode)
            {
                if (count > 1)
                {
                    CySpringBoneBase last = rootBone.ChildBoneList[count - 1];
                    CySpringBoneBase prev = rootBone.ChildBoneList[count - 2];

                    if (last != null && prev != null && last.GameObject != null)
                    {
                        float d = Vector3.Distance(last.GameObject.transform.position, rootPos);

                        if (d > 0.0001f)
                        {
                            float v = Mathf.Clamp(prev.CollisionRadius / d, -1.0f, 1.0f);
                            maxRad = Mathf.Asin(v);
                        }
                    }
                }
            }
            else
            {
                for (int i = 1; i < count; i++)
                {
                    CySpringBoneBase bone = rootBone.ChildBoneList[i];
                    CySpringBoneBase prev = rootBone.ChildBoneList[i - 1];

                    if (bone == null || prev == null || bone.GameObject == null)
                        continue;

                    float d = Vector3.Distance(bone.GameObject.transform.position, rootPos);

                    if (d <= 0.0001f)
                        continue;

                    float v = Mathf.Clamp(prev.CollisionRadius / d, -1.0f, 1.0f);
                    float rad = Mathf.Asin(v);

                    if (rad > maxRad)
                        maxRad = rad;
                }
            }

            return maxRad * Mathf.Rad2Deg;
        }

        public void UpdateScale(float scale)
        {
            ModelScale = scale;

            if (_isScaleCollisionEnabled)
            {
                float knee = (_defaultKneeColliderRadius * ModelScale) * 1000.0f;
                _kneeColliderRadius = Gallop.Math.Floor(knee) / 1000.0f;

                float ankle = (_defaultAnkleColliderRadius * ModelScale) * 1000.0f;
                _ankleColliderRadius = Gallop.Math.Floor(ankle) / 1000.0f;
            }

            NativeArg.KneeColliderRadius = _kneeColliderRadius;
            NativeArg.AnkleColliderRadius = _ankleColliderRadius;

            _UpdateSkirtArg();
        }

        public void UpdateSkirtArgForCySpring()
        {
            if (!_isInitialized)
                return;

            if (_skirtDataArray == null)
                return;

            _UpdateSkirtArg();
        }

        private void _UpdateSkirtArg()
        {
            NativeArg.RootPos = _ResolveRootPos();

            if (_center != null)
                NativeArg.CenterPos = _center.position;
            else
                // Sin hueso "centro", usar el propio personaje. Dejarlo a (0,0,0) hace que el
                // solver evalue los planos contra el origen del mundo (la falda se levanta hacia
                // el origen y empeora con la distancia).
                NativeArg.CenterPos = NativeArg.RootPos;

            if (_kneeL != null)
                NativeArg.KneeLPos = _kneeL.position;

            if (_kneeR != null)
                NativeArg.KneeRPos = _kneeR.position;

            if (_ankleL != null)
                NativeArg.AnkleLPos = _ankleL.position;

            if (_ankleR != null)
                NativeArg.AnkleRPos = _ankleR.position;

            NativeArg.KneeColliderRadius = _kneeColliderRadius;
            NativeArg.AnkleColliderRadius = _ankleColliderRadius;

            NativeArg.InfluenceAngle = _influenceAngle;
            NativeArg.InfluenceMaxAngle = _influenceMaxAngle;

            if (_skirtDataArray == null)
                return;

            EnsureNativeWorkingArray();

            Quaternion parentRotation = _GetParentRotation();

            for (int i = 0; i < _skirtDataArray.Length; i++)
            {
                SkirtData skirtData = _skirtDataArray[i];

                if (skirtData == null)
                    continue;

                if (skirtData.SkirtRoot == null || skirtData.SkirtChild == null)
                    continue;

                if (NativeWorkingArray == null || i >= NativeWorkingArray.Length)
                    continue;

                Quaternion initWorldRotation = parentRotation * skirtData.RootInitRotation;

                NativeSkirtWorking work = NativeWorkingArray[i];

                // El juego recalcula estos tres campos EN CADA FRAME (decompilado
                // _UpdateSkirtArg: get_position del hijo + Quaternion_op_Multiply con el eje
                // estatico escribiendo en el working). Capturarlos una sola vez en el bind es
                // incorrecto: hay que refrescarlos cada frame.
                work.SkirtRootPos = skirtData.SkirtRoot.position;

                work.SkirtInitChildPos =
                    work.SkirtRootPos +
                    initWorldRotation * skirtData.ChildInitLocalPosition;

                work.SkirtInitNormal = initWorldRotation * SkirtNormal;
                work.RotationAxis = initWorldRotation * RotAxis;

                work.OffsetAngle = skirtData.OffsetAngle;

                NativeWorkingArray[i] = work;
            }
            
        }

        private Transform _ResolveRigRoot()
        {
            return transform.Find("Position");
        }

        /// <summary>
        /// De donde sale NativeArg.RootPos. El plano de falda nativo se coloca con
        /// `SkirtNormalPos = SkirtRootPos - RootPos` y se compara contra posiciones de hueso que,
        /// durante el step, estan en el espacio del owner (`UpdatePrevousPosture` lo deja en el
        /// origen). Por eso RootPos sale del ancla del owner, que vale 0 en ese momento: asi
        /// `SkirtNormalPos` cae exactamente sobre la raiz de la falda. Leyendolo del contenedor
        /// (que no se pone a cero) el plano quedaba desplazado por la posicion del personaje
        /// (medido en el live 1001: hasta 8,47 m de empuje en un panel).
        /// </summary>
        private Vector3 _ResolveRootPos()
        {
            Transform anchor = _rootAnchor != null ? _rootAnchor : _ResolveRigRoot();

            return anchor != null ? anchor.position : Vector3.zero;
        }

        public void UpdateSkirt()
    {
        if (!_isInitialized)
            return;

        if (!_isEnableSkirt)
            return;

        if (_skirtDataArray == null)
            return;

        if (NativeWorkingArray == null)
            return;

        bool hasUpdateSelf = false;

        int count = _skirtDataArray.Length;

        // 1. ç¬¬ä¸€è½®ï¼šå®˜æ–¹å…ˆåˆ¤æ–­å“ªäº› skirt ç”± SkirtController è‡ªå·±æ›´æ–°
        for (int i = 0; i < count; i++)
        {
            SkirtData skirtData = _skirtDataArray[i];

            if (skirtData == null)
                continue;

            skirtData.IsUpdate = false;

            if (skirtData.SkirtRoot == null)
                continue;

            if (skirtData.SkirtChild == null)
                continue;

            if (!IsUpdateSelf(skirtData))
                continue;

            // å®˜æ–¹ï¼šå…ˆè¿˜åŽŸskirt rootçš„åˆå§‹localRotation
            skirtData.SkirtRoot.localRotation = skirtData.RootInitRotation;

            skirtData.IsUpdate = true;
            hasUpdateSelf = true;

            if ((uint)i < (uint)NativeWorkingArray.Length)
            {
                NativeSkirtWorking work = NativeWorkingArray[i];

                // å®˜æ–¹è¿™é‡Œå†™çš„æ˜¯-1.0f,ä¸æ˜¯ -360.0f
                work.Evaluation = -1.0f;

                NativeWorkingArray[i] = work;
            }
        }

        //åªæœ‰æœ‰self updateæ—¶æ‰æ›´æ–°skirt arg
        if (!hasUpdateSelf)
            return;

        _UpdateSkirtArg();

        //ç¬¬äºŒè½®ï¼šå¯¹ IsUpdateçš„skirtè°ƒ NativeSkirtUpdate
        for (int i = 0; i < count; i++)
        {
            SkirtData skirtData = _skirtDataArray[i];

            if (skirtData == null)
                continue;

            if (!skirtData.IsUpdate)
                continue;

            if (skirtData.SkirtRoot == null)
                continue;

            if (skirtData.SkirtChild == null)
                continue;

            if ((uint)i >= (uint)NativeWorkingArray.Length)
                continue;

            // å®˜æ–¹æ˜¯ä¼  NativeWorkingArray[i] çš„åœ°å€,ä¸æ˜¯å¤åˆ¶ work åˆ°ä¸´æ—¶å˜é‡
            CySpringNative.UpdateSkirtNativePluginOne(NativeWorkingArray,i,ref NativeArg);
        }

        //ç¬¬ä¸‰è½®ï¼šEvaluation > 0æ‰æ—‹è½¬ skirt root
        for (int i = 0; i < count; i++)
        {
            SkirtData skirtData = _skirtDataArray[i];

            if (skirtData == null)
                continue;

            if (!skirtData.IsUpdate)
                continue;

            if (skirtData.SkirtRoot == null)
                continue;

            if (skirtData.SkirtChild == null)
                continue;

            if ((uint)i >= (uint)NativeWorkingArray.Length)
                continue;

            NativeSkirtWorking work = NativeWorkingArray[i];

            if (work.Evaluation > 0.0f)
            {
                // Decompilado del juego (SkirtController.UpdateSkirt @0x7ffffc34df70):
                //   Transform.Rotate(transform, &ejeEstatico, evaluation, /*Space*/ 1)   -> Space.World
                // El eje es la constante estatica (Vector3.left), NO el RotationAxis del working.
                // El visor lo hacia en Space.Self, y por eso el giro caia en un eje distinto al
                // calculado por el solver (la falda se torcia).
                skirtData.SkirtRoot.Rotate(
                    RotAxis,
                    work.Evaluation,
                    Space.World);
            }
        }
    }

        public void SetSkirtExpressionBoneForRaceRun(bool isEnable)
        {
            if (_skirtDataArray == null)
                return;

            for (int i = 0; i < _skirtDataArray.Length; i++)
            {
                SkirtData skirtData = _skirtDataArray[i];

                if (skirtData == null)
                    continue;

                if (skirtData.IsIgnoreRaceRun)
                    skirtData.IsUpdate = isEnable;
                else
                    skirtData.IsUpdate = true;
            }
        }

        private bool IsUpdateSelf(SkirtData skirtData)
        {
            if (skirtData == null)
                return false;

            if (!_isEnableSkirt)
                return false;

            if (!skirtData.IsUpdate)
                return false;

            if (skirtData.SkirtRoot == null)
                return false;

            if (skirtData.CySpringRoot == null)
                return true;

            if (_isEnableCySpringFunc == null)
                return false;

            return !_isEnableCySpringFunc();
        }

        private static Transform _GetBone(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;

            int childCount = root.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform found = _GetBone(root.GetChild(i), name);

                if (found != null)
                    return found;
            }

            return null;
        }

        private Quaternion _GetParentRotation()
        {
            if (_skirtDataArray != null)
            {
                for (int i = 0; i < _skirtDataArray.Length; i++)
                {
                    SkirtData skirtData = _skirtDataArray[i];

                    if (skirtData == null)
                        continue;

                    if (skirtData.SkirtRoot == null || skirtData.SkirtChild == null)
                        continue;

                    if (skirtData.SkirtRootParent != null)
                        return skirtData.SkirtRootParent.rotation;
                }
            }

            return Quaternion.identity;
        }

        

        private void EnsureNativeWorkingArray()
        {
            int count = _skirtDataArray != null ? _skirtDataArray.Length : 0;

            if (count <= 0)
            {
                NativeWorkingArray = null;
                return;
            }

            if (NativeWorkingArray == null || NativeWorkingArray.Length != count)
            {
                NativeWorkingArray = new NativeSkirtWorking[count];
            }
        }

        private void InitializeSkirtData()
        {
            if (_skirtDataArray == null)
                return;

            for (int i = 0; i < _skirtDataArray.Length; i++)
            {
                SkirtData skirtData = _skirtDataArray[i];

                if (skirtData == null)
                    continue;

                if (skirtData.SkirtRoot != null)
                {
                    skirtData.RootInitRotation = skirtData.SkirtRoot.localRotation;
                    skirtData.SkirtRootParent = skirtData.SkirtRoot.parent;
                }
                else
                {
                    skirtData.RootInitRotation = Quaternion.identity;
                    skirtData.SkirtRootParent = null;
                }

                if (skirtData.SkirtChild != null)
                    skirtData.ChildInitLocalPosition = skirtData.SkirtChild.localPosition;
                else
                    skirtData.ChildInitLocalPosition = Vector3.zero;

                skirtData.CySpringRoot = null;
                skirtData.IsUpdate = true;
            }
        }

        private void InitializeNativeWorkingStaticFlags()
        {
            if (_skirtDataArray == null)
                return;

            EnsureNativeWorkingArray();

            for (int i = 0; i < _skirtDataArray.Length; i++)
            {
                SkirtData skirtData = _skirtDataArray[i];

                if (skirtData == null)
                    continue;

                if (NativeWorkingArray == null || i >= NativeWorkingArray.Length)
                    continue;

                NativeSkirtWorking work = NativeWorkingArray[i];

                work.IsCheckRightKnee = skirtData.IsCheckRightLeg ? 1 : 0;
                work.IsCheckLeftKnee = skirtData.IsCheckLeftLeg ? 1 : 0;

                work.IsCheckRightAnkle = (_ankleR != null && skirtData.IsCheckRightLeg) ? 1 : 0;
                work.IsCheckLeftAnkle = (_ankleL != null && skirtData.IsCheckLeftLeg) ? 1 : 0;

                work.Evaluation = 0.0f;
                work.OffsetAngle = skirtData.OffsetAngle;

                NativeWorkingArray[i] = work;
            }
        }

        private void ResolveBasicBones()
        {
            Transform root = CachedTransform;

            // "Hip" es el nombre real del rig de Uma. Si `_center` queda a null, `_UpdateSkirtArg`
            // no escribe `NativeArg.CenterPos` y se queda en (0,0,0): la falda evalua sus planos
            // contra el ORIGEN DEL MUNDO, asi que la esquina que apunta al origen se levanta y
            // cuanto mas lejos esta el personaje, peor (el slot del centro salia bien).
            if (_center == null)
                _center = FindFirstBone(root, "center", "Center", "J_Bip_C_Hips", "J_Bip_C_Pelvis", "Hips", "Pelvis", "Hip", "Waist");

            if (_kneeL == null)
                _kneeL = FindFirstBone(root, "knee_L", "Knee_L", "LeftKnee", "J_Bip_L_LowerLeg", "J_Bip_L_Knee", "Bip001 L Calf");

            if (_kneeR == null)
                _kneeR = FindFirstBone(root, "knee_R", "Knee_R", "RightKnee", "J_Bip_R_LowerLeg", "J_Bip_R_Knee", "Bip001 R Calf");

            if (_ankleL == null)
                _ankleL = FindFirstBone(root, "ankle_L", "Ankle_L", "LeftAnkle", "J_Bip_L_Foot", "J_Bip_L_Ankle", "Bip001 L Foot");

            if (_ankleR == null)
                _ankleR = FindFirstBone(root, "ankle_R", "Ankle_R", "RightAnkle", "J_Bip_R_Foot", "J_Bip_R_Ankle", "Bip001 R Foot");
        }

        private Transform FindFirstBone(Transform root, params string[] names)
        {
            if (root == null || names == null)
                return null;

            for (int i = 0; i < names.Length; i++)
            {
                Transform found = _GetBone(root, names[i]);

                if (found != null)
                    return found;
            }

            return null;
        }

        private void ApplyParamAsset(SkirtParamDataAsset data)
        {
            if (data == null)
                return;

            if (!string.IsNullOrEmpty(data._centerBoneName))
                _center = _GetBone(CachedTransform, data._centerBoneName);

            if (!string.IsNullOrEmpty(data._kneeLBoneName))
                _kneeL = _GetBone(CachedTransform, data._kneeLBoneName);

            if (!string.IsNullOrEmpty(data._kneeRBoneName))
                _kneeR = _GetBone(CachedTransform, data._kneeRBoneName);

            if (!string.IsNullOrEmpty(data._ankleLBoneName))
                _ankleL = _GetBone(CachedTransform, data._ankleLBoneName);

            if (!string.IsNullOrEmpty(data._ankleRBoneName))
                _ankleR = _GetBone(CachedTransform, data._ankleRBoneName);

            _kneeColliderRadius = data._kneeColliderRadius;
            _ankleColliderRadius = data._ankleColliderRadius;
            _influenceAngle = data._influenceAngle;
            _influenceMaxAngle = data._influenceMaxAngle;

            _isOffsetAngleTargetEndNode = data.IsOffsetAngleTargetEndNode;
            _isScaleCollisionEnabled = data.IsScaleCollision;

            if (CharaId >= 0)
            {
                SkirtParamDataOverrideCollision overrideData = data.GetOverrideData(CharaId);

                if (overrideData != null)
                {
                    _kneeColliderRadius = overrideData.KneeCollisionSize;
                    _ankleColliderRadius = overrideData.AnkleCollisionSize;
                }
            }

            if (data._skirtBone != null)
            {
                List<SkirtData> list = new List<SkirtData>();

                for (int i = 0; i < data._skirtBone.Count; i++)
                {
                    SkirtParamDataElement element = data._skirtBone[i];

                    if (element == null)
                        continue;

                    SkirtData skirtData = new SkirtData();

                    skirtData.SkirtRoot = _GetBone(CachedTransform, element._rootBoneName);
                    skirtData.SkirtChild = _GetBone(CachedTransform, element._childBoneName);

                    skirtData.IsCheckRightLeg = element._isCheckRightLeg;
                    skirtData.IsCheckLeftLeg = element._isCheckLeftLeg;
                    skirtData.IsIgnoreRaceRun = element.IsIgnoreRaceRun;

                    if (skirtData.SkirtRoot != null && skirtData.SkirtChild != null)
                    {
                        list.Add(skirtData);
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning(
                    "[SkirtController] Missing skirt bone root=" +
                    element._rootBoneName +
                    ", child=" +
                    element._childBoneName
                );
            }
#endif
                }

                _skirtDataArray = list.ToArray();
            }
        }

        private SkirtData[] ConvertToSkirtDataArray(object source)
        {
            if (source == null)
                return null;

            SkirtData[] direct = source as SkirtData[];
            if (direct != null)
                return direct;

            List<SkirtData> list = new List<SkirtData>();

            IEnumerable enumerable = source as IEnumerable;
            if (enumerable == null)
                return null;

            foreach (object item in enumerable)
            {
                if (item == null)
                    continue;

                SkirtData skirtData = ConvertToSkirtData(item);

                if (skirtData != null)
                    list.Add(skirtData);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        private SkirtData ConvertToSkirtData(object item)
        {
            SkirtData direct = item as SkirtData;
            if (direct != null)
                return direct;

            SkirtData data = new SkirtData();

            data.SkirtRoot = ReadTransformFromObject(item,
                "SkirtRoot",
                "skirtRoot",
                "_skirtRoot",
                "Root",
                "root",
                "RootBone",
                "rootBone",
                "RootName",
                "rootName"
            );

            data.SkirtChild = ReadTransformFromObject(item,
                "SkirtChild",
                "skirtChild",
                "_skirtChild",
                "Child",
                "child",
                "ChildBone",
                "childBone",
                "ChildName",
                "childName"
            );

            data.IsCheckRightLeg = ReadBoolFromObject(item, data.IsCheckRightLeg,
                "IsCheckRightLeg",
                "isCheckRightLeg",
                "_isCheckRightLeg",
                "CheckRightLeg",
                "checkRightLeg"
            );

            data.IsCheckLeftLeg = ReadBoolFromObject(item, data.IsCheckLeftLeg,
                "IsCheckLeftLeg",
                "isCheckLeftLeg",
                "_isCheckLeftLeg",
                "CheckLeftLeg",
                "checkLeftLeg"
            );

            data.IsIgnoreRaceRun = ReadBoolFromObject(item, data.IsIgnoreRaceRun,
                "IsIgnoreRaceRun",
                "isIgnoreRaceRun",
                "_isIgnoreRaceRun",
                "IgnoreRaceRun",
                "ignoreRaceRun"
            );

            data.OffsetAngle = ReadFloatFromObject(item, data.OffsetAngle,
                "OffsetAngle",
                "offsetAngle",
                "_offsetAngle"
            );

            return data;
        }

        private static bool TryGetMemberValue(object obj, out object value, params string[] names)
        {
            value = null;

            if (obj == null || names == null)
                return false;

            Type type = obj.GetType();

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];

                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                );

                if (field != null)
                {
                    value = field.GetValue(obj);
                    return true;
                }

                PropertyInfo prop = type.GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                );

                if (prop != null && prop.CanRead)
                {
                    value = prop.GetValue(obj, null);
                    return true;
                }
            }

            return false;
        }

        private void TryCopyTransform(object src, ref Transform dst, params string[] names)
        {
            object value;

            if (!TryGetMemberValue(src, out value, names))
                return;

            Transform t = value as Transform;
            if (t != null)
            {
                dst = t;
                return;
            }

            Component c = value as Component;
            if (c != null)
            {
                dst = c.transform;
                return;
            }

            string boneName = value as string;
            if (!string.IsNullOrEmpty(boneName))
            {
                Transform found = _GetBone(CachedTransform, boneName);

                if (found != null)
                    dst = found;
            }
        }

        private void TryCopyFloat(object src, ref float dst, params string[] names)
        {
            object value;

            if (!TryGetMemberValue(src, out value, names))
                return;

            try
            {
                dst = Convert.ToSingle(value);
            }
            catch
            {
            }
        }

        private void TryCopyBool(object src, ref bool dst, params string[] names)
        {
            object value;

            if (!TryGetMemberValue(src, out value, names))
                return;

            try
            {
                dst = Convert.ToBoolean(value);
            }
            catch
            {
            }
        }

        private Transform ReadTransformFromObject(object src, params string[] names)
        {
            object value;

            if (!TryGetMemberValue(src, out value, names))
                return null;

            Transform t = value as Transform;
            if (t != null)
                return t;

            Component c = value as Component;
            if (c != null)
                return c.transform;

            string boneName = value as string;
            if (!string.IsNullOrEmpty(boneName))
                return _GetBone(CachedTransform, boneName);

            return null;
        }

        private bool ReadBoolFromObject(object src, bool defaultValue, params string[] names)
        {
            object value;

            if (!TryGetMemberValue(src, out value, names))
                return defaultValue;

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private float ReadFloatFromObject(object src, float defaultValue, params string[] names)
        {
            object value;

            if (!TryGetMemberValue(src, out value, names))
                return defaultValue;

            try
            {
                return Convert.ToSingle(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        [Serializable]
        public class SkirtData
        {
            public Transform SkirtRoot;
            public Transform SkirtChild;
            public bool IsCheckRightLeg;
            public bool IsCheckLeftLeg;
            public bool IsIgnoreRaceRun;
            public float OffsetAngle;

            [NonSerialized] public Quaternion RootInitRotation;
            [NonSerialized] public Vector3 ChildInitLocalPosition;
            [NonSerialized] public CySpringRootBone CySpringRoot;
            [NonSerialized] public bool IsUpdate;
            [NonSerialized] public Transform SkirtRootParent;

            public SkirtData()
            {
                RootInitRotation = Quaternion.identity;
                ChildInitLocalPosition = Vector3.zero;
                CySpringRoot = null;
                IsUpdate = true;
                SkirtRootParent = null;
            }
        }

        private static string NormalizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            return name
                .Replace("(Clone)", string.Empty)
                .Trim();
        }

        private static bool SameBone(Transform t, string boneName)
        {
            if (t == null || string.IsNullOrEmpty(boneName))
                return false;

            return string.Equals(
                NormalizeBoneName(t.name),
                NormalizeBoneName(boneName),
                StringComparison.Ordinal
            );
        }
    }
}


