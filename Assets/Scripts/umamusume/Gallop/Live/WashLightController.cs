using System;
using System.Runtime.InteropServices;
using Gallop.Live.Cutt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.StaticVariableDefine.Live
{
    //WashLight投影网格使用的固定顶点、UV和索引数据
    public static class WashLightController
    {
        // 地面投影网格顶点
        public static readonly Vector3[] PROJECTION_MESH_VERTEX_ARRAY =
        {
            new Vector3(-0.5f, 0.0f, -0.5f),
            new Vector3( 0.5f, 0.0f, -0.5f),
            new Vector3(-0.5f, 0.0f,  0.5f),
            new Vector3( 0.5f, 0.0f,  0.5f),
        };
        // 相机投影网格顶点,左右顺序与地面投影相反
        public static readonly Vector3[] CAMERA_PROJECTION_MESH_VERTEX_ARRAY =
        {
            new Vector3( 0.5f, 0.0f, -0.5f),
            new Vector3(-0.5f, 0.0f, -0.5f),
            new Vector3( 0.5f, 0.0f,  0.5f),
            new Vector3(-0.5f, 0.0f,  0.5f),
        };
        // 投影纹理 UV
        public static readonly Vector2[] PROJECTION_MESH_UV_ARRAY =
        {
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
        };

        public static readonly int[] PROJECTION_MESH_INDEX_ARRAY =
        {
            0, 2, 1,
            2, 3, 1,
        };
    }
}

namespace Gallop.Live
{

    public class WashLightController : MonoBehaviour
    {
        private const int RECTANGLE_VERTEX_NUM = 4;
        private const float FADEOUT_LENGTH_MIN = 0.001f;
        private const float PROJECTION_CAMERA_BLANK = 0.01f;
        private const int FRUSTUM_PLANE_NEAR_CLIP_INDEX = 4;

        private static readonly int PropBlinkLightColor = Shader.PropertyToID("_BlinkLightColor");
        private static readonly int PropCutoffHeight = Shader.PropertyToID("_CutoffHeight");
        private static readonly int PropFadeoutHeightStart = Shader.PropertyToID("_FadeoutHeightStart");
        private static readonly int PropFadeoutHeightEnd = Shader.PropertyToID("_FadeoutHeightEnd");
        private static readonly int PropFadeoutHeightLength = Shader.PropertyToID("_FadeoutHeightLength");
        private static readonly int PropProjectorColorPower = Shader.PropertyToID("_ProjectorColorPower");

        [SerializeField] private float _distance;

        [Header("Fadeout")]
        [SerializeField] private bool _isEnabledFadeout;
        [SerializeField] private float _fadeoutHeightStart;
        [SerializeField] private float _fadeoutHeightEnd;

        [Header("Projection")]
        [SerializeField] private GameObject _projectionObject;

        [Header("Floor Projection")]
        [SerializeField] private bool _isEnabledProjection;
        [SerializeField] private Texture _projectionTexture;
        [SerializeField] private float _projectionSide = 0.1f;
        [SerializeField] private float _projectionHeight;
        [SerializeField] private bool _isEnabledProjectionFadeout;

        [SerializeField] private float _projectionFadeoutForwardStart;
        [SerializeField] private float _projectionFadeoutForwardEnd;
        [SerializeField] private float _projectionFadeoutBackwardStart;
        [SerializeField] private float _projectionFadeoutBackwardEnd;
        [SerializeField] private float _projectionFadeoutLeftStart;
        [SerializeField] private float _projectionFadeoutLeftEnd;
        [SerializeField] private float _projectionFadeoutRightStart;
        [SerializeField] private float _projectionFadeoutRightEnd;

        [Header("Camera Projection")]
        [SerializeField] private bool _isEnabledCameraProjection;
        [SerializeField] private Texture _cameraProjectionTexture;
        [SerializeField] private float _cameraProjectionSide = 0.1f;
        [SerializeField] private float _cameraProjectionColorPowerRateBorderAngle = 30.0f;
        [SerializeField] private float _cameraProjectionColorPowerRateMin = 0.5f;
        [SerializeField] private Gallop.LiveDefine.LightBlendMode _cameraProjectionLightBlendMode = Gallop.LiveDefine.LightBlendMode.SoftAddition;

        private Transform _cachedTransform;
        private int _raycastLayerMask;
        private Material _washLightMaterial;

        private bool _isEnabledRaycastChara;
        private float _raycastDistance;
        private Ray _ray;
        private Ray[] _vertexRayArray = new Ray[RECTANGLE_VERTEX_NUM];

        private Vector3[] _floorProjectionVertexOffsetArray = new Vector3[RECTANGLE_VERTEX_NUM];
        private Plane _projectionPlane;
        private Transform _projectionTransform;
        private Mesh _projectionMesh;
        private Material _projectionMaterial;
        private MeshRenderer _projectionMeshRenderer;
        private Vector3[] _projectionVertexRayHitPositionArray;
        private Vector3[] _projectionMeshVertexArray;
        private float _projectionColorPower;
        private Vector3 _projectionPlanePosition = Vector3.zero;
        private Vector3 _projectionPlaneNormal = Vector3.up;

        private Vector3[] _cameraProjectionVertexOffsetArray = new Vector3[RECTANGLE_VERTEX_NUM];
        private float _cameraProjectionColorPower = 0.5f;
        private float _cameraProjectionColorPowerRateBorder;
        private float _toCameraDegree;
        private float _cameraProjectionColorPowerAngleLerpFactor;

        private bool _isInitializedProjection;
        private bool _isInitialized;

        public float Distance
        {
            get => _distance;
            set => _distance = value;
        }

        public bool IsEnabledFadeout
        {
            get => _isEnabledFadeout;
            set => _isEnabledFadeout = value;
        }

        public float FadeoutHeightStart
        {
            get => _fadeoutHeightStart;
            set => _fadeoutHeightStart = value;
        }

        public float FadeoutHeightEnd
        {
            get => _fadeoutHeightEnd;
            set => _fadeoutHeightEnd = value;
        }

        public GameObject ProjectionObject
        {
            get => _projectionObject;
            set => _projectionObject = value;
        }

        public bool IsEnabledProjection
        {
            get => _isEnabledProjection;
            set => _isEnabledProjection = value;
        }

        public Texture ProjectionTexture
        {
            get => _projectionTexture;
            set => _projectionTexture = value;
        }

        public float ProjectionSide
        {
            get => _projectionSide;
            set
            {
                _projectionSide = value;
                if (_floorProjectionVertexOffsetArray != null)
                    SetupFloorProjectionVertexOffsetArray();
            }
        }

        public float ProjectionHeight
        {
            get => _projectionHeight;
            set => _projectionHeight = value;
        }

        public bool IsEnabledProjectionFadeout
        {
            get => _isEnabledProjectionFadeout;
            set => _isEnabledProjectionFadeout = value;
        }

        public float ProjectionFadeoutForwardStart
        {
            get => _projectionFadeoutForwardStart;
            set => _projectionFadeoutForwardStart = value;
        }

        public float ProjectionFadeoutForwardEnd
        {
            get => _projectionFadeoutForwardEnd;
            set => _projectionFadeoutForwardEnd = value;
        }

        public float ProjectionFadeoutBackwardStart
        {
            get => _projectionFadeoutBackwardStart;
            set => _projectionFadeoutBackwardStart = value;
        }

        public float ProjectionFadeoutBackwardEnd
        {
            get => _projectionFadeoutBackwardEnd;
            set => _projectionFadeoutBackwardEnd = value;
        }

        public float ProjectionFadeoutLeftStart
        {
            get => _projectionFadeoutLeftStart;
            set => _projectionFadeoutLeftStart = value;
        }

        public float ProjectionFadeoutLeftEnd
        {
            get => _projectionFadeoutLeftEnd;
            set => _projectionFadeoutLeftEnd = value;
        }

        public float ProjectionFadeoutRightStart
        {
            get => _projectionFadeoutRightStart;
            set => _projectionFadeoutRightStart = value;
        }

        public float ProjectionFadeoutRightEnd
        {
            get => _projectionFadeoutRightEnd;
            set => _projectionFadeoutRightEnd = value;
        }

        public bool IsEnabledCameraProjection
        {
            get => _isEnabledCameraProjection;
            set => _isEnabledCameraProjection = value;
        }

        public Texture CameraProjectionTexture
        {
            get => _cameraProjectionTexture;
            set => _cameraProjectionTexture = value;
        }

        public float CameraProjectionSide
        {
            get => _cameraProjectionSide;
            set
            {
                _cameraProjectionSide = value;
                if (_cameraProjectionVertexOffsetArray != null)
                    SetupCameraProjectionVertexOffsetArray();
            }
        }

        public float CameraProjectionColorPowerRateBorderAngle
        {
            get => _cameraProjectionColorPowerRateBorderAngle;
            set
            {
                _cameraProjectionColorPowerRateBorderAngle = value;
                _cameraProjectionColorPowerRateBorder = Cosf(_cameraProjectionColorPowerRateBorderAngle);
            }
        }

        public float CameraProjectionColorPowerRateMin
        {
            get => _cameraProjectionColorPowerRateMin;
            set => _cameraProjectionColorPowerRateMin = value;
        }

        public LiveDefine.LightBlendMode CameraProjectionLightBlendMode
        {
            get => _cameraProjectionLightBlendMode;
            set => _cameraProjectionLightBlendMode = value;
        }

        public Transform CachedTransform => _cachedTransform;
        public float CameraProjectionColorPowerRateBorder => _cameraProjectionColorPowerRateBorder;
        public float ToCameraDegree => _toCameraDegree;
        public float CameraProjectionColorPowerAngleLerpFactor => _cameraProjectionColorPowerAngleLerpFactor;
        public bool IsInitialized => _isInitialized;

        private void OnDestroy()
        {
            DestroyInternal();
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            _cachedTransform = transform;

            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
                _washLightMaterial = renderer.material;

            _raycastLayerMask =
                GetCullingLayer(1) |
                GetCullingLayer(14) |
                GetCullingLayer(15) |
                GetCullingLayer(16);

            InitializeProjection();

            _isInitialized = true;
        }

        private void InitializeProjection()
        {
            if (_isInitializedProjection)
                return;

            if (!_isEnabledProjection && !_isEnabledCameraProjection)
                return;

            if (_projectionObject != null)
            {
                _projectionTransform = _projectionObject.transform;
            }
            else
            {   
                // 未指定投影对象时，默认使用第一个子对象
                if (_cachedTransform == null)
                    throw new NullReferenceException(nameof(_cachedTransform));

                if (_cachedTransform.childCount < 1)
                    return;

                _projectionTransform = _cachedTransform.GetChild(0);
                if (_projectionTransform == null)
                    throw new NullReferenceException(nameof(_projectionTransform));

                _projectionObject = _projectionTransform.gameObject;
            }

            if (_projectionObject == null)
                throw new NullReferenceException(nameof(_projectionObject));

            // Native InitializeProjection calls GallopGameObjectExtensions.Initialize
            // before replacing the child mesh (RVA 0x1aab830 -> 0x1c16ba0).
            _projectionTransform.localPosition = Vector3.zero;
            _projectionTransform.localRotation = Quaternion.identity;
            _projectionTransform.localScale = Vector3.one;

            MeshFilter meshFilter = _projectionObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                return;

            _projectionMesh = CreateProjectionMesh();
            meshFilter.mesh = _projectionMesh;

            _projectionMeshRenderer = _projectionObject.GetComponent<MeshRenderer>();
            if (_projectionMeshRenderer == null)
                return;

            _projectionMaterial = _projectionMeshRenderer.material;
            if (_projectionMaterial == null)
                throw new NullReferenceException(nameof(_projectionMaterial));

            _projectionMaterial.SetColor(PropBlinkLightColor, Color.clear);

            _projectionVertexRayHitPositionArray = new Vector3[RECTANGLE_VERTEX_NUM];
            _projectionMeshVertexArray = new Vector3[RECTANGLE_VERTEX_NUM];

            InitializeFloorProjection();

            if (_isEnabledCameraProjection)
            {
                SetupCameraProjectionVertexOffsetArray();
                _cameraProjectionColorPowerRateBorder = Cosf(_cameraProjectionColorPowerRateBorderAngle);
            }

            _isInitializedProjection = true;
        }

        private void InitializeFloorProjection()
        {
            if (!_isEnabledProjection)
                return;

            SetupFloorProjectionVertexOffsetArray();

            _projectionPlanePosition.y = _projectionHeight;

            Vector3 normal = NormalizeSafe(_projectionPlaneNormal, Vector3.up);
            _projectionPlane = new Plane(normal, _projectionPlanePosition);
        }

        private void InitializeCameraProjection()
        {
            if (!_isEnabledCameraProjection)
                return;

            SetupCameraProjectionVertexOffsetArray();
            _cameraProjectionColorPowerRateBorder = Cosf(_cameraProjectionColorPowerRateBorderAngle);
        }

        public void AlterLateUpdate(Vector3 cameraPosition, Plane[] frustumPlanes)
        {
            if (!_isInitialized)
                return;

            if (!enabled)
                return;

            RaycastCharaModel(_isEnabledRaycastChara, _distance, _raycastDistance);

            if (_isEnabledFadeout && _washLightMaterial != null)
            {
                if (_washLightMaterial.HasProperty(PropFadeoutHeightStart))
                    _washLightMaterial.SetFloat(PropFadeoutHeightStart, _fadeoutHeightStart);

                if (_washLightMaterial.HasProperty(PropFadeoutHeightEnd))
                    _washLightMaterial.SetFloat(PropFadeoutHeightEnd, _fadeoutHeightEnd);

                if (_washLightMaterial.HasProperty(PropFadeoutHeightLength))
                {
                    float length = Mathf.Max(_fadeoutHeightStart - _fadeoutHeightEnd, FADEOUT_LENGTH_MIN);
                    _washLightMaterial.SetFloat(PropFadeoutHeightLength, length);
                }
            }

            UpdateFloorProjection();
            UpdateCameraProjection(cameraPosition, frustumPlanes);
        }

        public void SetUpdateInfo(ref WashLightUpdateInfo updateInfo)
        {
            if (!updateInfo.IsAllSettings)
            {
                _isEnabledRaycastChara = updateInfo.IsEnabledRaycast;
                _raycastDistance = updateInfo.RaycastDistance;
            }

            _cameraProjectionSide = updateInfo.CameraProjectionSide;
            _cameraProjectionColorPower = updateInfo.CameraProjectionColorPower;

            SetupCameraProjectionVertexOffsetArray();
        }
        private void RaycastCharaModel(bool enable, float rayDistance, float checkDistance)
        {
            if (rayDistance < 0.000001f)
                return;

            if (!enable)
                return;

            if (checkDistance < 0.000001f)
                return;

            if (checkDistance > rayDistance)
                checkDistance = rayDistance;

            if (_cachedTransform == null)
                throw new NullReferenceException(nameof(_cachedTransform));

            _ray.origin = _cachedTransform.position;
            _ray.direction = NormalizeSafe(_cachedTransform.up, Vector3.up);

            _cachedTransform.localScale = Vector3.one;

            if (Physics.Raycast(_ray, out RaycastHit hit, checkDistance, _raycastLayerMask))
            {
                Vector3 localScale = _cachedTransform.localScale;
                float rate = Mathf.Clamp01(hit.distance / rayDistance);
                _cachedTransform.localScale = localScale * rate;
            }
        }

        private void UpdateFloorProjection()
        {
            if (!_isInitializedProjection || !_isEnabledProjection)
                return;

            if (!_isEnabledFadeout && _washLightMaterial != null)
            {
                if (_washLightMaterial.HasProperty(PropCutoffHeight))
                    _washLightMaterial.SetFloat(PropCutoffHeight, _projectionHeight);
            }

            SetupProjectionRays(_floorProjectionVertexOffsetArray);

            bool visible = TryProjectRaysToPlane(_projectionPlane, true);
            if (_projectionMeshRenderer == null)
                throw new NullReferenceException(nameof(_projectionMeshRenderer));

            _projectionMeshRenderer.enabled = visible;

            if (!_projectionMeshRenderer.enabled)
                return;

            if (_projectionMesh == null)
                throw new NullReferenceException(nameof(_projectionMesh));

            _projectionMesh.SetVertices(_projectionMeshVertexArray);
            _projectionMesh.RecalculateNormals();
            _projectionMesh.RecalculateBounds();

            if (_projectionMaterial == null)
                throw new NullReferenceException(nameof(_projectionMaterial));

            _projectionMaterial.mainTexture = _projectionTexture;

            _projectionColorPower = 1.0f;

            if (_isEnabledProjectionFadeout)
            {
                float avgX = 0.0f;
                float avgZ = 0.0f;

                for (int i = 0; i < _projectionVertexRayHitPositionArray.Length; i++)
                {
                    Vector3 p = _projectionVertexRayHitPositionArray[i];
                    avgX += p.x;
                    avgZ += p.z;
                }

                float count = _projectionVertexRayHitPositionArray.Length;
                avgX /= count;
                avgZ /= count;

                float zPower = CalcProjectionFade(
                    avgZ,
                    _projectionFadeoutBackwardEnd,
                    _projectionFadeoutBackwardStart,
                    _projectionFadeoutForwardStart,
                    _projectionFadeoutForwardEnd
                );

                float xPower = CalcProjectionFade(
                    avgX,
                    _projectionFadeoutRightEnd,
                    _projectionFadeoutRightStart,
                    _projectionFadeoutLeftStart,
                    _projectionFadeoutLeftEnd
                );

                _projectionColorPower = Mathf.Min(zPower, xPower);
            }

            _projectionMaterial.SetFloat(PropProjectorColorPower, _projectionColorPower);
        }

        private void UpdateCameraProjection(Vector3 cameraPosition, Plane[] frustumPlanes)
        {
            if (!_isInitializedProjection || !_isEnabledCameraProjection)
                return;

            if (_projectionMeshRenderer == null)
                throw new NullReferenceException(nameof(_projectionMeshRenderer));

            if (_projectionMeshRenderer.enabled)
                return;

            if (_cameraProjectionColorPower < 0.000001f)
            {
                _projectionMeshRenderer.enabled = false;
                return;
            }

            if (frustumPlanes == null || frustumPlanes.Length <= FRUSTUM_PLANE_NEAR_CLIP_INDEX)
                throw new IndexOutOfRangeException(nameof(frustumPlanes));

            SetupProjectionRays(_cameraProjectionVertexOffsetArray);

            Plane nearClipPlane = frustumPlanes[FRUSTUM_PLANE_NEAR_CLIP_INDEX];

            bool visible = TryProjectRaysToPlane(nearClipPlane, false);
            _projectionMeshRenderer.enabled = visible;

            if (!_projectionMeshRenderer.enabled)
                return;

            if (_projectionMesh == null)
                throw new NullReferenceException(nameof(_projectionMesh));

            _projectionMesh.SetVertices(_projectionMeshVertexArray);
            _projectionMesh.RecalculateNormals();
            _projectionMesh.RecalculateBounds();

            if (_projectionMaterial == null)
                throw new NullReferenceException(nameof(_projectionMaterial));

            _projectionMaterial.mainTexture = _cameraProjectionTexture != null
                ? _cameraProjectionTexture
                : _projectionTexture;

            float colorPower = _cameraProjectionColorPower;
            _cameraProjectionColorPowerAngleLerpFactor = _cameraProjectionColorPowerRateMin;

            if (_cachedTransform == null)
                throw new NullReferenceException(nameof(_cachedTransform));

            Vector3 toCamera = cameraPosition - _cachedTransform.position;
            toCamera = NormalizeSafe(toCamera, Vector3.up);

            float dot = Vector3.Dot(toCamera, _cachedTransform.up);
            _toCameraDegree = dot;

            if (dot > _cameraProjectionColorPowerRateBorder)
            {
                float lerp = (dot - _cameraProjectionColorPowerRateBorder) /(1.0f - _cameraProjectionColorPowerRateBorder);

                lerp = Mathf.Clamp01(lerp);

                _cameraProjectionColorPowerAngleLerpFactor =_cameraProjectionColorPowerRateMin + lerp * (1.0f - _cameraProjectionColorPowerRateMin);
            }

            _projectionMaterial.SetFloat(PropProjectorColorPower,colorPower * _cameraProjectionColorPowerAngleLerpFactor);

            TrySetLightBlendModeMaterialProperty(_cameraProjectionLightBlendMode, _projectionMaterial);
        }

        private void SetupFloorProjectionVertexOffsetArray()
        {
            Vector3[] src = Gallop.StaticVariableDefine.Live.WashLightController.PROJECTION_MESH_VERTEX_ARRAY;

            if (_floorProjectionVertexOffsetArray == null)
                throw new NullReferenceException(nameof(_floorProjectionVertexOffsetArray));

            for (int i = 0; i < _floorProjectionVertexOffsetArray.Length; i++)
                _floorProjectionVertexOffsetArray[i] = src[i] * _projectionSide;
        }

        private void SetupCameraProjectionVertexOffsetArray()
        {
            Vector3[] src = Gallop.StaticVariableDefine.Live.WashLightController.CAMERA_PROJECTION_MESH_VERTEX_ARRAY;

            if (_cameraProjectionVertexOffsetArray == null)
                throw new NullReferenceException(nameof(_cameraProjectionVertexOffsetArray));

            for (int i = 0; i < _cameraProjectionVertexOffsetArray.Length; i++)
                _cameraProjectionVertexOffsetArray[i] = src[i] * _cameraProjectionSide;
        }

        private static Mesh CreateProjectionMesh()
        {
            Mesh mesh = new Mesh();

            mesh.SetVertices(Gallop.StaticVariableDefine.Live.WashLightController.PROJECTION_MESH_VERTEX_ARRAY);
            mesh.SetUVs(0, Gallop.StaticVariableDefine.Live.WashLightController.PROJECTION_MESH_UV_ARRAY);
            mesh.SetIndices(
                Gallop.StaticVariableDefine.Live.WashLightController.PROJECTION_MESH_INDEX_ARRAY,
                MeshTopology.Triangles,
                0
            );

            mesh.MarkDynamic();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public bool TryGetProjectionMeshRenderer(out MeshRenderer meshRenderer)
        {
            meshRenderer = _projectionMeshRenderer;
            return meshRenderer != null;
        }

        private void DestroyInternal()
        {
            if (_washLightMaterial != null)
            {
                DestroyRuntimeObject(_washLightMaterial);
                _washLightMaterial = null;
            }

            if (_projectionMesh != null)
            {
                DestroyRuntimeObject(_projectionMesh);
                _projectionMesh = null;
            }

            if (_projectionMaterial != null)
            {
                DestroyRuntimeObject(_projectionMaterial);
                _projectionMaterial = null;
            }

            _isInitializedProjection = false;
            _isInitialized = false;
        }

        private void SetupProjectionRays(Vector3[] vertexOffsetArray)
        {
            if (_vertexRayArray == null)
                throw new NullReferenceException(nameof(_vertexRayArray));

            if (vertexOffsetArray == null)
                throw new NullReferenceException(nameof(vertexOffsetArray));

            if (_cachedTransform == null)
                throw new NullReferenceException(nameof(_cachedTransform));

            Vector3 origin = _cachedTransform.position;
            Vector3 up = _cachedTransform.up;
            Quaternion rotation = _cachedTransform.rotation;

            for (int i = 0; i < _vertexRayArray.Length; i++)
            {
                Vector3 offset = rotation * vertexOffsetArray[i];
                Vector3 direction = NormalizeSafe(up + offset, Vector3.up);
                _vertexRayArray[i] = new Ray(origin, direction);
            }
        }

        private bool TryProjectRaysToPlane(Plane plane, bool limitDistance)
        {
            if (_vertexRayArray == null)
                throw new NullReferenceException(nameof(_vertexRayArray));

            if (_projectionVertexRayHitPositionArray == null)
                throw new NullReferenceException(nameof(_projectionVertexRayHitPositionArray));

            if (_projectionMeshVertexArray == null)
                throw new NullReferenceException(nameof(_projectionMeshVertexArray));

            if (_projectionTransform == null)
                throw new NullReferenceException(nameof(_projectionTransform));

            Vector3 normal = plane.normal;
            float distance = plane.distance;

            for (int i = 0; i < _vertexRayArray.Length; i++)
            {
                Ray ray = _vertexRayArray[i];

                float denom = Vector3.Dot(ray.direction, normal);
                float eps = Mathf.Max(Mathf.Abs(denom) * 0.000001f, Mathf.Epsilon * 8.0f);

                if (Mathf.Abs(denom) < eps)
                    return false;

                float t = -(Vector3.Dot(ray.origin, normal) + distance) / denom;

                if (t <= 0.0f)
                    return false;

                if (limitDistance && t >= _distance)
                    return false;

                Vector3 worldHit = ray.GetPoint(t);
                _projectionVertexRayHitPositionArray[i] = worldHit;
                _projectionMeshVertexArray[i] = _projectionTransform.InverseTransformPoint(worldHit);
            }

            return true;
        }

        private static float CalcProjectionFade(float value,float lowerEnd,float lowerStart,float upperStart,float upperEnd)
        {
            float power = 0.0f;

            if (value >= lowerEnd)
            {
                if (value < lowerStart)
                {
                    power = (value - lowerEnd) / Mathf.Max(lowerStart - lowerEnd, FADEOUT_LENGTH_MIN);
                }
                else
                {
                    power = 1.0f;

                    if (value >= upperStart)
                    {
                        power = 0.0f;

                        if (value < upperEnd)
                            power = 1.0f - ((value - upperStart) / Mathf.Max(upperEnd - upperStart, FADEOUT_LENGTH_MIN));
                    }
                }
            }

            return power;
        }

        private static Vector3 NormalizeSafe(Vector3 value, Vector3 fallback)
        {
            float length = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z);

            if (length <= 0.00001f)
                return fallback;

            return value / length;
        }

        private static float Cosf(float degree)
        {
            return Mathf.Cos(degree * Mathf.Deg2Rad);
        }

        private static int GetCullingLayer(int layer)
        {
            return 1 << layer;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        private static void TrySetLightBlendModeMaterialProperty(LiveDefine.LightBlendMode blendMode,Material material)
        {
            if (material == null)
                return;

            switch (blendMode)
            {
                case LiveDefine.LightBlendMode.Addition:
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)BlendMode.One);
                    break;

                case LiveDefine.LightBlendMode.Multiply:
                case LiveDefine.LightBlendMode.Multiply0:
                    material.SetInt("_SrcBlend", (int)BlendMode.DstColor);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                    break;

                case LiveDefine.LightBlendMode.SoftAddition:
                    material.SetInt("_SrcBlend", (int)BlendMode.OneMinusDstColor);
                    material.SetInt("_DstBlend", (int)BlendMode.One);
                    break;

                case LiveDefine.LightBlendMode.AlphaBlend:
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    break;

                case LiveDefine.LightBlendMode.Multiply2x:
                    material.SetInt("_SrcBlend", (int)BlendMode.DstColor);
                    material.SetInt("_DstBlend", (int)BlendMode.SrcColor);
                    break;
            }
        }
    }
}