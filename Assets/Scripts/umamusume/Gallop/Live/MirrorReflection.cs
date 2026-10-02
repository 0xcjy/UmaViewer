using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop
{
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class MirrorReflection : MonoBehaviour
{
    public enum Direction
    {
        Up,
        Forward,
        Right
    }

    private const string CAMERA_NAME = "Mirror Camera";
    private const string TEXTURE_NAME = "MirrorReflection Texture";
    private const float MIRROR_CAMERA_FOV_MIN = 1f;
    private const float MIRROR_CAMERA_FOV_MAX = 179f;
    private const float MIRROR_TEXTURE_SIZE_RATE_MIN = 0.01f;
    private const int MIRROR_CAMERA_DEPTH_OFFSET = -100;

    private static readonly Vector4 DEFAULT_DIST_POWER_VALUE = Vector4.zero;
    private static readonly Vector4 DEFAULT_DIST_MAP_TILE_OFFSET_VALUE = new Vector4(1f, 1f, 0f, 0f);

    [Header("镜面基础设置")]
    [SerializeField] private LayerMask _renderLayers = ~0;
    [SerializeField] private int _mirrorTextureSize = 1024;
    [SerializeField] private float _mirrorCliipPlaneOffset = 0.07f;
    [SerializeField] private Camera _baseCamera;
    [SerializeField] private float _mirrorReflectionRate = 0f;
    [SerializeField] private Vector4 _mirrorDistortionTileOffset = DEFAULT_DIST_MAP_TILE_OFFSET_VALUE;
    [SerializeField] private Vector4 _mirrorDistortionPower = DEFAULT_DIST_POWER_VALUE;
    [SerializeField] private Direction _direction = Direction.Up;
    [SerializeField] private Color _mirrorReflectionColor = Color.white;
    [SerializeField] private Color _backgroundColor = Color.black;
    [SerializeField] private bool _useBackgroundColor = false;

    [Header("材质贴图大小")]
    [SerializeField] private bool _useMirrorTextureScale = false;
    [SerializeField] private float _mirrorTextureScaleForBaseCamera = 1f;
    [SerializeField] private bool _isUseBaseCameraTextureSize = false;

    [Header("Runtime State")]
    [SerializeField] private bool _isEnabledMirrorCamera = true;
    [SerializeField] private bool _isEnabled = true;
    [SerializeField] private int _mirrorIndex = -1;
    [SerializeField] private bool _autoInitialize = true;
    [SerializeField] private bool _autoFindBaseCamera = true;
    [SerializeField] private bool _logDebug = false;

    [Header("Debug 和Fallback备选")]
    [SerializeField] private bool _legacyBuiltInFallback = false;

    [SerializeField, HideInInspector] private Transform _baseCameraTransform;
    [SerializeField, HideInInspector] private Transform _transform;
    [SerializeField, HideInInspector] private Camera _mirrorCamera;
    [SerializeField, HideInInspector] private Transform _mirrorCameraTransform;
    [SerializeField, HideInInspector] private RenderTexture _mirrorTexture;
    [SerializeField, HideInInspector] private int _oldMirrorTextureSize = -1;
    [SerializeField, HideInInspector] private Renderer _receivedMirrorMeshRenderer;
    [SerializeField, HideInInspector] private Material[] _materials;
    [SerializeField, HideInInspector] private LayerMask _finalRenderLayers = ~0;
    [SerializeField, HideInInspector] private int _objectLayer;
    [SerializeField, HideInInspector] private bool _isInitialized;
    [SerializeField, HideInInspector] private Vector4 _clipPlane;
    [SerializeField, HideInInspector] private Vector4 _initMirrorDistortionPower = DEFAULT_DIST_POWER_VALUE;
    [SerializeField, HideInInspector] private Vector4 _initMirrorDistortionTileOffset = DEFAULT_DIST_MAP_TILE_OFFSET_VALUE;
    [SerializeField, HideInInspector] private float _oldMirrorTextureScaleForBaseCamera = -1f;


    private Func<float> _getBaseCameraFovFactor;
    private Action<MirrorReflection> OnPreDraw;
    private Action<MirrorReflection> OnPostDraw;
    private event Action<ScriptableRenderContext, Camera> _beginCameraRenderingCallbacks;
    private event Action<ScriptableRenderContext, Camera> _endCameraRenderingCallbacks;

    private bool _isRenderingNow;
    private readonly Stack<int> _mirrorObjectLayers = new Stack<int>(4);
    internal Live.StageController LifecycleOwner { get; set; }

    private readonly UniversalRenderPipeline.SingleCameraRequest _singleCameraRequest =
        new UniversalRenderPipeline.SingleCameraRequest();

    private static readonly int PID_Color = Shader.PropertyToID("_Color");
    private static readonly int PID_ReflectionRate = Shader.PropertyToID("_ReflectionRate");
    private static readonly int PID_ReflectionTex = Shader.PropertyToID("_ReflectionTex");
    private static readonly int PID_DistMapST = Shader.PropertyToID("_DistMap_ST");
    private static readonly int PID_DistPower = Shader.PropertyToID("_DistPower");

    public Camera BaseCamera => _baseCamera;
    public Camera MirrorCamera => _mirrorCamera;
    public RenderTexture MirrorTexture => _mirrorTexture;
    public bool IsInitialized => _isInitialized;
    public bool IsUseBaseCameraTextureSize => _isUseBaseCameraTextureSize;
    public int MirrorIndex => _mirrorIndex;

    public float MirrorClipPlaneOffset
    {
        get => _mirrorCliipPlaneOffset;
        set => _mirrorCliipPlaneOffset = value;
    }

    public float MirrorReflectionRate
    {
        get => _mirrorReflectionRate;
        set
        {
            _mirrorReflectionRate = value;

            if (_baseCamera != null && _mirrorCamera != null && _isEnabledMirrorCamera)
                UpdateMirrorParams();
            else
                ApplyMaterialParams();
        }
    }

    public Vector4 MirrorDistortionTileOffset
    {
        get => _mirrorDistortionTileOffset;
        set
        {
            _mirrorDistortionTileOffset = value;
            ApplyMaterialParams();
        }
    }

    public Vector4 MirrorDistortionPower
    {
        get => _mirrorDistortionPower;
        set
        {
            _mirrorDistortionPower = value;
            ApplyMaterialParams();
        }
    }

    public Direction Dir
    {
        get => _direction;
        set => _direction = value;
    }

    public Color MirrorReflectionColor
    {
        get => _mirrorReflectionColor;
        set
        {
            _mirrorReflectionColor = value;
            ApplyMaterialParams();
        }
    }

    public Vector4 GetDefaultMirrorDistortionPower() => _initMirrorDistortionPower;
    public Vector4 GetDefaultMirrorDistortionMapTileOffset() => _initMirrorDistortionTileOffset;

    public void Reset()
    {
        _transform = transform;
        _receivedMirrorMeshRenderer = GetComponent<Renderer>();
        _objectLayer = gameObject.layer;
        _finalRenderLayers = _renderLayers;
    }

    private void Awake()
    {
        EnsureSelfReferences();



        if (_autoInitialize)
            TryAutoInitializeIfPossible();
    }

    private void OnEnable()
    {
        _isEnabled = true;

        if (_autoInitialize && !_isInitialized)
        {
            TryAutoInitializeIfPossible();
            return;
        }

        if (_isInitialized)
        {
            UpdateRenderTexture();
            UpdateMirrorTexture();
            UpdateMirrorParams();
        }
    }

    private void OnDisable()
    {
        _isEnabled = false;
        LifecycleOwner?.RestoreMirrorActorLayers(_mirrorCamera);
        RestoreMirrorObjectLayers();
        ClearMirrorMaterialBindingOnDisable();
    }

    private void LateUpdate()
    {
        if (!_isEnabled || !_isEnabledMirrorCamera)
            return;

        if (!_isInitialized)
        {
            if (_autoInitialize)
                TryAutoInitializeIfPossible();

            if (!_isInitialized)
                return;
        }

        if (_isRenderingNow)
            return;

        if (_baseCamera == null || _mirrorCamera == null || _baseCameraTransform == null)
            return;

        // Director submits once after the timeline and presentation camera pose settle.
    }

    private void OnDestroy()
    {
        LifecycleOwner?.ReleaseMirrorReflection(this);
        RestoreMirrorObjectLayers();
        ReleaseMirrorTexture();

        if (_mirrorCamera != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_mirrorCamera.gameObject);
            else Destroy(_mirrorCamera.gameObject);
#else
            Destroy(_mirrorCamera.gameObject);
#endif
        }
    }

    private void EnsureSelfReferences()
    {
        if (_transform == null)
            _transform = transform;

        if (_receivedMirrorMeshRenderer == null)
            _receivedMirrorMeshRenderer = GetComponent<Renderer>();

        _objectLayer = gameObject.layer;
        _finalRenderLayers = _renderLayers;
    }

    private bool IsUsableCamera(Camera cam)
    {
        if (cam == null)
            return false;

        if (_mirrorCamera != null && cam == _mirrorCamera)
            return false;

        if (!cam.enabled)
            return false;

        if (!cam.gameObject.activeInHierarchy)
            return false;

        return true;
    }

    private bool EnsureBaseCamera()
    {
        if (_baseCamera != null)
        {
            if (IsUsableCamera(_baseCamera))
            {
                _baseCameraTransform = _baseCamera.transform;
                return true;
            }

            _baseCamera = null;
            _baseCameraTransform = null;
        }

        if (!_autoFindBaseCamera)
            return false;

        if (IsUsableCamera(Camera.main))
        {
            _baseCamera = Camera.main;
            _baseCameraTransform = _baseCamera.transform;
            return true;
        }

        Camera[] all = Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (!IsUsableCamera(all[i]))
                continue;

            _baseCamera = all[i];
            _baseCameraTransform = _baseCamera.transform;
            return true;
        }

        return false;
    }

    private void TryAutoInitializeIfPossible()
    {
        // During live playback StageController is the sole initialization owner.
        if (Live.Director.instance != null || GetComponentInParent<Live.StageController>() != null)
            return;
        EnsureSelfReferences();

        if (!EnsureBaseCamera())
            return;

        Initialize(_baseCamera, _mirrorIndex, _isUseBaseCameraTextureSize);
    }

    public void Initialize(Camera baseCamera, int mirrorIndex = -1, bool isUseBaseCameraTextureSize = false)
    {
        LifecycleOwner?.PrepareMirrorReinitialization(this);
        RestoreMirrorObjectLayers();
        _isInitialized = false;

        EnsureSelfReferences();

        if (baseCamera != null)
        {
            _baseCamera = baseCamera;
            _baseCameraTransform = baseCamera.transform;
        }

        if (!EnsureBaseCamera())
        {
            Log("Initialize failed: base camera not found.");
            return;
        }

        if (_receivedMirrorMeshRenderer == null)
        {
            Log("Initialize failed: renderer missing.");
            return;
        }

        Material sharedMaterial = _receivedMirrorMeshRenderer.sharedMaterial;
        if (sharedMaterial == null &&
            (_receivedMirrorMeshRenderer.sharedMaterials == null || _receivedMirrorMeshRenderer.sharedMaterials.Length == 0))
        {
            Log("Initialize failed: no shared material.");
            return;
        }

        _mirrorIndex = mirrorIndex;
        _isUseBaseCameraTextureSize = isUseBaseCameraTextureSize;
        _transform = transform;
        _objectLayer = gameObject.layer;

        InitMaterials();
        CreateMirrorCamera();
        UpdateRenderTexture();
        UpdateMirrorTexture();
        UpdateMirrorParams();

        _isInitialized = true;
        LifecycleOwner?.CompleteMirrorReinitialization(this);
        Log($"Initialize success. mirrorIndex={_mirrorIndex}, useBaseCameraTextureSize={_isUseBaseCameraTextureSize}");
    }

    public void SetupBaseCamera(Camera baseCamera, Func<float> getFovFactorFunc)
    {
        if (baseCamera != null)
        {
            _baseCamera = baseCamera;
            _baseCameraTransform = baseCamera.transform;
        }
        else
        {
            EnsureBaseCamera();
        }

        _getBaseCameraFovFactor = getFovFactorFunc;
        UpdateMirrorParams();
    }

    public void ResetCullingMask()
    {
        _finalRenderLayers = _renderLayers;
        if (_mirrorCamera != null)
            _mirrorCamera.cullingMask = _finalRenderLayers;
    }

    public void AddCullingMask(int cullingMask)
    {
        _finalRenderLayers |= cullingMask;
        if (_mirrorCamera != null)
            _mirrorCamera.cullingMask = _finalRenderLayers;
    }

    public void RemoveCullingMask(int cullingMask)
    {
        _finalRenderLayers &= ~cullingMask;
        if (_mirrorCamera != null)
            _mirrorCamera.cullingMask = _finalRenderLayers;
    }

    public void AddDrawCallback(Action<MirrorReflection> preDraw, Action<MirrorReflection> postDraw)
    {
        OnPreDraw += preDraw;
        OnPostDraw += postDraw;
    }

    public void RemoveDrawCallback(Action<MirrorReflection> preDraw, Action<MirrorReflection> postDraw)
    {
        OnPreDraw -= preDraw;
        OnPostDraw -= postDraw;
    }

    public void AddBeginCameraRenderingCallback(Action<ScriptableRenderContext, Camera> callback)
    {
        _beginCameraRenderingCallbacks += callback;
    }

    public void RemoveBeginCameraRenderingCallback(Action<ScriptableRenderContext, Camera> callback)
    {
        _beginCameraRenderingCallbacks -= callback;
    }

    public void AddEndCameraRenderingCallback(Action<ScriptableRenderContext, Camera> callback)
    {
        _endCameraRenderingCallbacks += callback;
    }

    public void RemoveEndCameraRenderingCallback(Action<ScriptableRenderContext, Camera> callback)
    {
        _endCameraRenderingCallbacks -= callback;
    }

    public void SetMirrorObjectLayer(int layer)
    {
        if (!_isInitialized || layer < 0)
            return;
        _objectLayer = gameObject.layer;
        _mirrorObjectLayers.Push(_objectLayer);
        SetMirrorObjectLayerRecursively(transform, layer);
    }

    public void ResetMirrorObjectLayer()
    {
        if (_mirrorObjectLayers.Count == 0)
            return;
        _objectLayer = _mirrorObjectLayers.Pop();
        SetMirrorObjectLayerRecursively(transform, _objectLayer);
    }

    internal void RestoreMirrorObjectLayers()
    {
        while (_mirrorObjectLayers.Count > 0)
            ResetMirrorObjectLayer();
    }

    private static void SetMirrorObjectLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetMirrorObjectLayerRecursively(root.GetChild(i), layer);
    }

    public void SetActive(bool active)
    {
        // Native SetActive (0x19b7e10) gates on initialization and hides the receiver too.
        if (_isInitialized && gameObject.activeSelf != active)
            gameObject.SetActive(active);
    }

    public void SetLightMirrorShader(bool isEnable)
    {
        // Native RVA 0x19b7f90 writes CameraData.MirrorCasterQuality (0/1).
        // GallopRenderer replaces both forward queues with SimpleMirrorCaster
        // when quality is 1; renderer slot 1 is installed by LiveRenderSetup.
        if (_mirrorCamera != null)
            _mirrorCamera.GetUniversalAdditionalCameraData().SetRenderer(isEnable ? 1 : -1);
    }

    public void SetMirrorVisibleOfficialStyle(bool visible, bool pauseRenderWhenHidden = true)
    {
        if (visible)
        {
            _isEnabled = true;
            enabled = true;
            _isEnabledMirrorCamera = true;

            if (_isInitialized)
            {
                UpdateRenderTexture();
                UpdateMirrorTexture();
                UpdateMirrorParams();
            }
            else if (_autoInitialize)
            {
                TryAutoInitializeIfPossible();
            }

            return;
        }

        // �ر�ʱֻ�ѵ�ǰ���ʷ���ǿ��ѹ�� 0�����ⶳ�����һ֡������ʾ
        _mirrorReflectionRate = 0f;
        ApplyMaterialParams();

        if (pauseRenderWhenHidden)
            _isEnabledMirrorCamera = false;
    }

    public void RemakeMirrorTexture()
    {
        ReleaseMirrorTexture();
        UpdateRenderTexture();
        UpdateMirrorTexture();
        UpdateMirrorParams();
    }

    private void InitMaterials()
    {
        if (_receivedMirrorMeshRenderer == null)
            return;

        _materials = _receivedMirrorMeshRenderer.materials;
        if (_materials == null || _materials.Length == 0)
            return;

        bool foundDistPower = false;
        bool foundDistMapST = false;

        _initMirrorDistortionPower = DEFAULT_DIST_POWER_VALUE;
        _initMirrorDistortionTileOffset = DEFAULT_DIST_MAP_TILE_OFFSET_VALUE;

        for (int i = 0; i < _materials.Length; i++)
        {
            Material mat = _materials[i];
            if (mat == null)
                continue;

            if (!foundDistPower && mat.HasProperty(PID_DistPower))
            {
                _initMirrorDistortionPower = mat.GetVector(PID_DistPower);
                foundDistPower = true;
            }

            if (!foundDistMapST && mat.HasProperty(PID_DistMapST))
            {
                _initMirrorDistortionTileOffset = mat.GetVector(PID_DistMapST);
                foundDistMapST = true;
            }

            if (foundDistPower && foundDistMapST)
                break;
        }

        _mirrorDistortionPower = _initMirrorDistortionPower;
        _mirrorDistortionTileOffset = _initMirrorDistortionTileOffset;

        Log($"InitMaterials done. foundDistPower={foundDistPower}, foundDistMapST={foundDistMapST}");
    }

    private void CreateMirrorCamera()
    {
        if (_mirrorCamera != null)
        {
            _mirrorCameraTransform = _mirrorCamera.transform;
            return;
        }

        GameObject go = new GameObject(CAMERA_NAME, typeof(Camera));
        go.hideFlags = HideFlags.DontSave;

        _mirrorCamera = go.GetComponent<Camera>();
        _mirrorCameraTransform = go.transform;

        _mirrorCameraTransform.SetParent(_transform, true);
        _mirrorCameraTransform.position = _transform.position;
        _mirrorCameraTransform.rotation = _transform.rotation;
        _mirrorCameraTransform.localScale = Vector3.one;

        _mirrorCamera.allowHDR = false;
        _mirrorCamera.allowMSAA = false;
        _mirrorCamera.enabled = false;
        _mirrorCamera.depth = (_baseCamera != null ? _baseCamera.depth : 0f) + MIRROR_CAMERA_DEPTH_OFFSET;
        _mirrorCamera.clearFlags = CameraClearFlags.Color;
        _mirrorCamera.cullingMask = _finalRenderLayers;
        _mirrorCamera.stereoTargetEye = StereoTargetEyeMask.None;

        Log("CreateMirrorCamera done.");
    }

    private Vector2Int GetRenderTextureSize()
    {
        int refWidth = 0;
        int refHeight = 0;

        if (_baseCamera != null && _baseCamera.targetTexture != null)
        {
            refWidth = Mathf.Max(1, _baseCamera.targetTexture.width);
            refHeight = Mathf.Max(1, _baseCamera.targetTexture.height);
        }
        else if (_baseCamera != null)
        {
            refWidth = Mathf.Max(1, _baseCamera.pixelWidth);
            refHeight = Mathf.Max(1, _baseCamera.pixelHeight);
        }
        else
        {
            refWidth = Mathf.Max(1, Screen.width);
            refHeight = Mathf.Max(1, Screen.height);
        }

        if (_isUseBaseCameraTextureSize)
            return new Vector2Int(refWidth, refHeight);

        if (_useMirrorTextureScale)
        {
            float scale = Mathf.Max(MIRROR_TEXTURE_SIZE_RATE_MIN, _mirrorTextureScaleForBaseCamera);
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(refWidth * scale)),
                Mathf.Max(1, Mathf.RoundToInt(refHeight * scale))
            );
        }

        int longSide = Mathf.Max(1, _mirrorTextureSize);

        if (refWidth >= refHeight)
        {
            int h = Mathf.Max(1, Mathf.RoundToInt(longSide * (float)refHeight / refWidth));
            return new Vector2Int(longSide, h);
        }
        else
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(longSide * (float)refWidth / refHeight));
            return new Vector2Int(w, longSide);
        }
    }

    private void UpdateRenderTexture()
    {
        bool needRebuild =
            _mirrorTexture == null ||
            _oldMirrorTextureSize != _mirrorTextureSize ||
            !Mathf.Approximately(_oldMirrorTextureScaleForBaseCamera, _mirrorTextureScaleForBaseCamera);

        if (!needRebuild)
            return;

        ReleaseMirrorTexture();

        Vector2Int size = GetRenderTextureSize();
        _oldMirrorTextureScaleForBaseCamera = _mirrorTextureScaleForBaseCamera;

        _mirrorTexture = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
        {
            name = TEXTURE_NAME,
            hideFlags = HideFlags.DontSave,
            useMipMap = false,
            autoGenerateMips = false
        };

        _mirrorTexture.Create();

        if (_mirrorCamera != null)
            _mirrorCamera.targetTexture = _mirrorTexture;

        UpdateMirrorTexture();

        _oldMirrorTextureSize = _mirrorTextureSize;

        Log($"UpdateRenderTexture done. {size.x}x{size.y}");
    }

    private void SetMirrorTextureOnMaterials(Texture tex)
    {
        if (_materials == null)
            return;

        for (int i = 0; i < _materials.Length; i++)
        {
            Material mat = _materials[i];
            if (mat == null)
                continue;

            if (mat.HasProperty(PID_ReflectionTex))
                mat.SetTexture(PID_ReflectionTex, tex);
        }
    }

    private void ClearMirrorMaterialBindingOnDisable()
    {
        _mirrorReflectionRate = 0f;
        ApplyMaterialParams();
        SetMirrorTextureOnMaterials(null);

        if (_mirrorCamera != null)
            _mirrorCamera.targetTexture = null;
    }

    private void UpdateMirrorTexture()
    {
        SetMirrorTextureOnMaterials(_mirrorTexture);
    }

    private void ReleaseMirrorTexture()
    {
        SetMirrorTextureOnMaterials(null);

        if (_mirrorTexture == null)
            return;

        if (_mirrorCamera != null && _mirrorCamera.targetTexture == _mirrorTexture)
            _mirrorCamera.targetTexture = null;

        _mirrorTexture.Release();

#if UNITY_EDITOR
    if (!Application.isPlaying) DestroyImmediate(_mirrorTexture);
    else Destroy(_mirrorTexture);
#else
        Destroy(_mirrorTexture);
#endif

        _mirrorTexture = null;
    }

    public void UpdateMirrorParams()
    {
        if (_baseCamera == null || !_isEnabledMirrorCamera || _mirrorCamera == null)
            return;

        _mirrorCamera.clearFlags = CameraClearFlags.Color;
        _mirrorCamera.backgroundColor = _useBackgroundColor ? _backgroundColor : _baseCamera.backgroundColor;

        float fov = _baseCamera.fieldOfView;
        if (_getBaseCameraFovFactor != null)
        {
            try
            {
                fov *= _getBaseCameraFovFactor();
            }
            catch
            {
            }
        }

        _mirrorCamera.fieldOfView = Mathf.Clamp(fov, MIRROR_CAMERA_FOV_MIN, MIRROR_CAMERA_FOV_MAX);
        _mirrorCamera.nearClipPlane = _baseCamera.nearClipPlane;
        _mirrorCamera.farClipPlane = _baseCamera.farClipPlane;
        _mirrorCamera.orthographic = _baseCamera.orthographic;
        _mirrorCamera.orthographicSize = _baseCamera.orthographicSize;
        _mirrorCamera.targetTexture = _mirrorTexture;
        _mirrorCamera.cullingMask = _finalRenderLayers;

        if (_mirrorTexture != null && _mirrorTexture.height > 0)
            _mirrorCamera.aspect = (float)_mirrorTexture.width / _mirrorTexture.height;
        else
            _mirrorCamera.aspect = _baseCamera.aspect;

        ApplyMaterialParams();
    }

    private void ApplyMaterialParams()
    {
        if (_materials == null)
            return;

        for (int i = 0; i < _materials.Length; i++)
        {
            Material mat = _materials[i];
            if (mat == null)
                continue;

            if (mat.HasProperty(PID_ReflectionRate))
                mat.SetFloat(PID_ReflectionRate, _mirrorReflectionRate);

            if (mat.HasProperty(PID_Color))
                mat.SetColor(PID_Color, _mirrorReflectionColor);

            if (mat.HasProperty(PID_DistMapST))
                mat.SetVector(PID_DistMapST, _mirrorDistortionTileOffset);

            if (mat.HasProperty(PID_DistPower))
                mat.SetVector(PID_DistPower, _mirrorDistortionPower);
        }
    }

    private Vector3 GetMirrorNormal()
    {
        switch (_direction)
        {
            case Direction.Forward:
                return _transform.forward;
            case Direction.Right:
                return _transform.right;
            case Direction.Up:
            default:
                return _transform.up;
        }
    }

    private bool IsPointAbovePlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
    {
        return Vector3.Dot(planeNormal, (point - planePoint).normalized) > 0f;
    }

    private bool IsBaseCameraInFrontOfMirror()
    {
        if (_baseCameraTransform == null || _transform == null)
            return false;

        return IsPointAbovePlane(_baseCameraTransform.position, _transform.position, GetMirrorNormal());
    }

    private void UpdateProjectionMatrix()
    {
        if (_baseCamera == null || _baseCameraTransform == null || _mirrorCamera == null || _mirrorCameraTransform == null)
            return;

        Vector3 normal = GetMirrorNormal();
        Vector3 planePos = _transform.position;

        float d = -Vector3.Dot(normal, planePos) - _mirrorCliipPlaneOffset;
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 reflectionMat = Matrix4x4.zero;
        CalculateReflectionMatrix(ref reflectionMat, reflectionPlane.x, reflectionPlane.y, reflectionPlane.z, reflectionPlane.w);

        Vector3 reflectedBasePos = reflectionMat.MultiplyPoint(_baseCameraTransform.position);

        _mirrorCamera.worldToCameraMatrix = _baseCamera.worldToCameraMatrix * reflectionMat;
        _mirrorCamera.cullingMatrix = _baseCamera.cullingMatrix * reflectionMat;

        Vector3 offsetPlanePos = planePos + normal * _mirrorCliipPlaneOffset;
        Matrix4x4 mirrorWorldToCamera = _mirrorCamera.worldToCameraMatrix;
        Vector3 camSpacePoint = mirrorWorldToCamera.MultiplyPoint(offsetPlanePos);
        Vector3 camSpaceNormal = mirrorWorldToCamera.MultiplyVector(normal).normalized;
        _clipPlane = new Vector4(camSpaceNormal.x,camSpaceNormal.y,camSpaceNormal.z,-Vector3.Dot(camSpacePoint, camSpaceNormal));

        float oldBaseFov = _baseCamera.fieldOfView;

        try
        {
            _baseCamera.fieldOfView = _mirrorCamera.fieldOfView;
            _baseCamera.aspect = _mirrorCamera.aspect;

            Matrix4x4 projection = _baseCamera.projectionMatrix;
            CalculateObliqueMatrix(ref projection, _clipPlane);
            _mirrorCamera.projectionMatrix = projection;
        }
        finally
        {
            _baseCamera.fieldOfView = oldBaseFov;
            _baseCamera.ResetAspect();
        }

        _mirrorCameraTransform.position = reflectedBasePos;
        Vector3 baseEuler = _baseCameraTransform.eulerAngles;
        _mirrorCameraTransform.eulerAngles = new Vector3(0f, baseEuler.y, baseEuler.z);
    }

    private static float Sgn(float x)
    {
        if (x > 0f) return 1f;
        if (x < 0f) return -1f;
        return 0f;
    }
    // 根据裁剪平面修改投影矩阵,生成斜截投影矩阵
    private static void CalculateObliqueMatrix(ref Matrix4x4 projection, Vector4 clipPlane)
    {
        Vector4 q = projection.inverse * new Vector4(
            Sgn(clipPlane.x),
            Sgn(clipPlane.y),
            1f,
            1f
        );

        Vector4 c = clipPlane * (2f / Vector4.Dot(clipPlane, q));

        projection[2] = c.x - projection[3];
        projection[6] = c.y - projection[7];
        projection[10] = c.z - projection[11];
        projection[14] = c.w - projection[15];
    }
    //反射矩阵计算
    private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, float x, float y, float z, float w)
    {
        reflectionMat.m00 = 1f - 2f * x * x;
        reflectionMat.m01 = -2f * x * y;
        reflectionMat.m02 = -2f * x * z;
        reflectionMat.m03 = -2f * x * w;

        reflectionMat.m10 = -2f * y * x;
        reflectionMat.m11 = 1f - 2f * y * y;
        reflectionMat.m12 = -2f * y * z;
        reflectionMat.m13 = -2f * y * w;

        reflectionMat.m20 = -2f * z * x;
        reflectionMat.m21 = -2f * z * y;
        reflectionMat.m22 = 1f - 2f * z * z;
        reflectionMat.m23 = -2f * z * w;

        reflectionMat.m30 = 0f;
        reflectionMat.m31 = 0f;
        reflectionMat.m32 = 0f;
        reflectionMat.m33 = 1f;
    }

    private bool TrySubmitSingleCameraRequest()
    {
        if (_mirrorCamera == null || _mirrorTexture == null)
            return false;

        _singleCameraRequest.destination = _mirrorTexture;

        if (!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(_mirrorCamera, _singleCameraRequest))
        {
            Log("SingleCameraRequest is not supported by the active render pipeline.");
            return false;
        }

        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(_mirrorCamera, _singleCameraRequest);
        return true;
    }

    public void ForceRenderOnce()
    {
        if (!isActiveAndEnabled || !_isEnabled || !_isEnabledMirrorCamera) return;
        if (!_isInitialized)
            TryAutoInitializeIfPossible();

        if (!_isInitialized || _baseCamera == null || _mirrorCamera == null ||
            _receivedMirrorMeshRenderer == null || !_receivedMirrorMeshRenderer.enabled)
            return;

        if (_isRenderingNow)
            return;

        _isRenderingNow = true;
        bool oldInvert = GL.invertCulling;
        int receiverLayerDepth = _mirrorObjectLayers.Count;
        try
        {
            UpdateRenderTexture();
            UpdateMirrorParams();

            if (!IsBaseCameraInFrontOfMirror())
                return;

            UpdateProjectionMatrix();

            OnPreDraw?.Invoke(this);
            _beginCameraRenderingCallbacks?.Invoke(default, _mirrorCamera);

            GL.invertCulling = !oldInvert;
            if (!TrySubmitSingleCameraRequest())
                _mirrorCamera.Render();
        }
        catch (Exception e)
        {
            Log($"ForceRenderOnce failed: {e}");
        }
        finally
        {
            try
            {
                GL.invertCulling = oldInvert;
                if (_mirrorCamera != null)
                {
                    _mirrorCamera.ResetWorldToCameraMatrix();
                    _mirrorCamera.ResetProjectionMatrix();
                    _mirrorCamera.ResetCullingMatrix();
                }
            }
            finally
            {
                try
                {
                    OnPostDraw?.Invoke(this);
                }
                finally
                {
                    try
                    {
                        _endCameraRenderingCallbacks?.Invoke(default, _mirrorCamera);
                    }
                    finally
                    {
                        try
                        {
                            LifecycleOwner?.RestoreMirrorActorLayers(_mirrorCamera);
                        }
                        finally
                        {
                            try
                            {
                                // Preserve a parent ordinary-camera scope during a nested mirror draw.
                                while (_mirrorObjectLayers.Count > receiverLayerDepth)
                                    ResetMirrorObjectLayer();
                            }
                            finally
                            {
                                try
                                {
                                    GL.invertCulling = oldInvert;
                                }
                                finally
                                {
                                    _isRenderingNow = false;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void Log(string msg)
    {
        if (_logDebug)
            Debug.Log($"[MirrorReflection] {name}: {msg}", this);
    }

    public void SetBaseCamera(Camera cam)
    {
        _baseCamera = cam;
        _baseCameraTransform = cam != null ? cam.transform : null;
    }

    public void SetFovFactorGetter(Func<float> getter)
    {
        _getBaseCameraFovFactor = getter;
    }
}
}

