using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop
{
    /// <summary>
    /// 平面镜反射。live10149 上 1 个实例，宿主是 <c>mirror_a</c>。
    ///
    /// 按原签名重建（bundle MonoScript: ClassName=MirrorReflection, Namespace=Gallop,
    /// AssemblyName=umamusume），所以下面的字段由 Unity 从 bundle 直接反序列化，不是猜的。
    /// 字段名逐字照抄 TypeTree —— 包括原作的拼写错误 `_mirrorCliipPlaneOffset`，
    /// 改成正确拼写反而收不到数据。
    ///
    /// **接收端是查出来的，不是猜的**：`mtl_env_live10149_mirror000` 用
    /// `Cygames/MirrorAndShadow/ReceiveMirror`，该 shader 只有 4 个属性 ——
    /// `_MainTex` / `_ReflectionTex` / `_Color` / `_ReflectionRate`，
    /// 而组件字段 `_mirrorReflectionColor`、`_mirrorReflectionRate` 与后两者逐字对应。
    ///
    /// live10149 实测值：textureSize=256、reflectionRate=1、reflectionColor=白、
    /// renderLayers=12544、useBackgroundColor=1、backgroundColor=透明黑、
    /// clipPlaneOffset=0、baseCamera=null（回落当前直播摄像机）。
    ///
    /// ── 没有依据、因此没有实现的部分 ──
    ///
    ///   `_direction`        枚举，值为 0，**语义未知**。IL2CPP 元数据取不到（见 CLAUDE.md），
    ///                       所以无法知道它是不是在选镜面法线轴。这里不使用它，
    ///                       改为**从网格自身的法线推导镜面**——那是几何事实，不是猜测。
    ///   `_mirrorDistortion*`  属于 ReceiveDistortionMirror 变体，本材质用的是普通版，不参与。
    ///   `_useMirrorTextureScale` / `_mirrorTextureScaleForBaseCamera`   语义未知，未使用。
    /// </summary>
    public class MirrorReflection : MonoBehaviour
    {
        // ↓ 名字必须与 bundle TypeTree 逐字一致（含原作拼写错误）
        [SerializeField] private LayerMask _renderLayers;
        [SerializeField] private int _mirrorTextureSize = 256;
        [SerializeField] private float _mirrorCliipPlaneOffset;
        [SerializeField] private Camera _baseCamera;
        [SerializeField] private float _mirrorReflectionRate = 1f;
        [SerializeField] private Vector4 _mirrorDistortionTileOffset;
        [SerializeField] private Vector4 _mirrorDistortionPower;
        [SerializeField] private int _direction;
        [SerializeField] private Color _mirrorReflectionColor = Color.white;
        [SerializeField] private Color _backgroundColor;
        [SerializeField] private bool _useBackgroundColor;
        [SerializeField] private bool _useMirrorTextureScale;
        [SerializeField] private float _mirrorTextureScaleForBaseCamera = 1f;

        private Camera _mirrorCamera;
        private RenderTexture _reflectionTexture;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private bool _rendering;
        private bool _logged;

        private static readonly int kReflectionTex = Shader.PropertyToID("_ReflectionTex");
        private static readonly int kReflectionRate = Shader.PropertyToID("_ReflectionRate");
        private static readonly int kColor = Shader.PropertyToID("_Color");

        public void ApplyTimeline(bool valid, bool enabled, float reflectionRate)
        {
            if (!valid) return;
            _mirrorReflectionRate = Mathf.Max(0f, reflectionRate);
            this.enabled = enabled;
            if (_renderer != null)
            {
                _block ??= new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(_block);
                _block.SetFloat(kReflectionRate, _mirrorReflectionRate);
                _renderer.SetPropertyBlock(_block);
            }
        }

        // URP 下不能用 Camera.Render()（SRP 会直接报错、什么都不画），
        // 必须挂 beginCameraRendering 拿到 ScriptableRenderContext 再 RenderSingleCamera。
        private void OnEnable()  => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (_mirrorCamera != null) { DestroyImmediate(_mirrorCamera.gameObject); _mirrorCamera = null; }
            if (_reflectionTexture != null) { DestroyImmediate(_reflectionTexture); _reflectionTexture = null; }
        }

        private Camera ResolveBaseCamera()
        {
            if (_baseCamera != null) return _baseCamera;
            if (Gallop.Live.Director.instance != null)
            {
                var t = Gallop.Live.Director.instance.MainCameraTransform;
                if (t != null)
                {
                    var c = t.GetComponent<Camera>();
                    if (c != null) return c;
                }
            }
            return Camera.main;
        }

        /// <summary>
        /// 镜面法线。`_direction` 的枚举含义拿不到，所以不用它，改从网格法线推导 ——
        /// 平面镜的网格法线是确定的几何量，比猜枚举可靠。取不到网格时回落 transform.up。
        /// </summary>
        private Vector3 ResolveNormal()
        {
            var mf = GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh != null && mesh.normals != null && mesh.normals.Length > 0)
                return transform.TransformDirection(mesh.normals[0]).normalized;
            return transform.up;
        }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_rendering) return;                       // 防止镜中镜递归
            if (cam == _mirrorCamera) return;

            _renderer ??= GetComponent<Renderer>();
            if (_renderer == null) return;

            Camera baseCam = ResolveBaseCamera();
            if (baseCam == null || cam != baseCam) return;

            EnsureResources(baseCam);
            if (_mirrorCamera == null || _reflectionTexture == null) return;

            Vector3 normal = ResolveNormal();
            Vector3 pos = transform.position;
            float d = -Vector3.Dot(normal, pos) - _mirrorCliipPlaneOffset;
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, d);

            Matrix4x4 reflection = CalculateReflectionMatrix(plane);

            _mirrorCamera.transform.position = reflection.MultiplyPoint(baseCam.transform.position);
            _mirrorCamera.transform.rotation = baseCam.transform.rotation;
            _mirrorCamera.worldToCameraMatrix = baseCam.worldToCameraMatrix * reflection;

            // 斜投影：近裁剪面贴合镜面，避免映出镜子背后的东西
            Vector4 clipPlane = CameraSpacePlane(_mirrorCamera, pos, normal, 1f);
            _mirrorCamera.projectionMatrix = baseCam.CalculateObliqueMatrix(clipPlane);

            _mirrorCamera.fieldOfView = baseCam.fieldOfView;
            _mirrorCamera.nearClipPlane = baseCam.nearClipPlane;
            _mirrorCamera.farClipPlane = baseCam.farClipPlane;

            _rendering = true;
            GL.invertCulling = true;                     // 镜像后绕序反了
#pragma warning disable CS0618
            UniversalRenderPipeline.RenderSingleCamera(ctx, _mirrorCamera);
#pragma warning restore CS0618
            GL.invertCulling = false;
            _rendering = false;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetTexture(kReflectionTex, _reflectionTexture);
            _block.SetFloat(kReflectionRate, _mirrorReflectionRate);
            _block.SetColor(kColor, _mirrorReflectionColor);
            _renderer.SetPropertyBlock(_block);

            if (!_logged)
            {
                _logged = true;
                Debug.Log($"[MirrorReflection] '{name}' size={_mirrorTextureSize} rate={_mirrorReflectionRate} " +
                          $"layers={(int)_renderLayers} baseCam={(baseCam != null ? baseCam.name : "<null>")} " +
                          $"normal={normal} | _direction={_direction}（枚举语义未知，未使用）");
            }
        }

        private void EnsureResources(Camera baseCam)
        {
            int size = Mathf.Max(16, _mirrorTextureSize);
            if (_reflectionTexture == null || _reflectionTexture.width != size)
            {
                if (_reflectionTexture != null) DestroyImmediate(_reflectionTexture);
                _reflectionTexture = new RenderTexture(size, size, 24) { name = $"MirrorRT_{name}" };
                _reflectionTexture.Create();
            }

            if (_mirrorCamera == null)
            {
                var go = new GameObject($"MirrorCamera_{name}") { hideFlags = HideFlags.HideAndDontSave };
                _mirrorCamera = go.AddComponent<Camera>();
                _mirrorCamera.enabled = false;           // 只手动 Render()
            }

            _mirrorCamera.targetTexture = _reflectionTexture;
            _mirrorCamera.cullingMask = _renderLayers;
            if (_useBackgroundColor)
            {
                _mirrorCamera.clearFlags = CameraClearFlags.SolidColor;
                _mirrorCamera.backgroundColor = _backgroundColor;
            }
            else
            {
                _mirrorCamera.clearFlags = baseCam.clearFlags;
                _mirrorCamera.backgroundColor = baseCam.backgroundColor;
            }
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector4 p)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            return m;
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sign)
        {
            Vector3 offsetPos = pos + normal * _mirrorCliipPlaneOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(offsetPos);
            Vector3 cnormal = m.MultiplyVector(normal).normalized * sign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }
    }
}
