using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    // Native MirrorBallProjector 0x1a2fdb0 / 0x1a30950 and ProjectorModuleCutoff 0x1a3b830.
    // Uses the original resource's MirrorBallProjector shader, including its spherical projection,
    // cubemap, animated rotation, falloff and backside clipping. No planar substitute shader.
    [DisallowMultipleComponent]
    public sealed class MirrorBallProjector : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [SerializeField] private LayerMask _ignoreLayer;
        public float MirrorBallProjectionRadius = 20f;
        public float MirrorBallFallOffPower = 1f;
        public bool MirrorBallIsLoopRotation;
        public float MirrorBallLoopRotationSpeed;
        public float MirrorBallLoopRotationValue;
        public Vector3 MirrorBallRotationAxis = Vector3.up;
        public Vector2 MirrorBallUVOffset;
        public Vector2 MirrorBallUVScale = Vector2.one;
        public bool MirrorBallUseCubeMap;
        public float AppTime;
        public bool OverrideIgnoreLayer { get; set; }
        public LayerMask OverrideLayerMask { get; set; }
        public bool ProjectionToCharacterModelOnly { get; set; }
        public bool BacksideOff { get; set; }
        public float BacksideFeather { get; set; }
        public Action OnPreRender { get; set; }
        public Action OnPostRender { get; set; }
        public Material Material => _material;
        public LayerMask IgnoreLayer => _ignoreLayer;

        private sealed class Target
        {
            public Renderer renderer;
            public bool character;
            public Material[] materials, sources;
            public int[] cutoffTypes;
        }
        private struct LayerState { public GameObject gameObject; public int layer; }
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<LayerState> _layerStates = new List<LayerState>();
        private readonly HashSet<Transform> _targetTransforms = new HashSet<Transform>();
        private Material _originalMaterial;
        private readonly Plane[] _planes = new Plane[6];
        private static readonly int CutoffType = Shader.PropertyToID("_CutoffType");
        private static readonly int CutoffTex = Shader.PropertyToID("_CutoffTex");
        private static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BacksideOffId = Shader.PropertyToID("_ProjectorBacksideOff");
        private static readonly int BacksideFeatherId = Shader.PropertyToID("_MirrorBallBacksideFeather");

        public bool Initialize()
        {
            if (_originalMaterial != null) return true;
            if (_material == null || _material.shader == null) return false;
            _originalMaterial = _material;
            _material = new Material(_originalMaterial) { name = _originalMaterial.name, hideFlags = HideFlags.DontSave };
            return true;
        }

        // Native CreateDrawInfo accepts a receiver only when every material explicitly advertises
        // a valid CutoffType (-1..5); ordinary Projector-tag receivers remain in DrawRenderers.
        public void AddTargetRenderer(Renderer renderer, bool character)
        {
            if (renderer == null) return;
            foreach (var existing in _targets) if (existing.renderer == renderer) return;
            Material[] sources = renderer.sharedMaterials;
            if (sources.Length == 0) return;
            int[] types = new int[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null || !sources[i].HasProperty(CutoffType)) return;
                types[i] = sources[i].GetInt(CutoffType);
                if (types[i] < -1 || types[i] > 5) return;
            }
            var target = new Target { renderer = renderer, character = character, sources = sources,
                materials = new Material[sources.Length], cutoffTypes = types };
            for (int i = 0; i < sources.Length; i++)
                target.materials[i] = new Material(_material) { hideFlags = HideFlags.DontSave };
            _targets.Add(target);
            foreach (Transform child in renderer.GetComponentsInChildren<Transform>(true))
                if (_targetTransforms.Add(child)) _layerStates.Add(new LayerState { gameObject = child.gameObject });
        }

        private void ApplyParameters()
        {
            Vector3 position = transform.position;
            _material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallPosWS), new Vector4(position.x, position.y, position.z, 0f));
            _material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallRotateAxis), new Vector4(MirrorBallRotationAxis.x, MirrorBallRotationAxis.y, MirrorBallRotationAxis.z, 0f));
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallRotateValue), MirrorBallLoopRotationValue);
            Vector3 scale = transform.localScale;
            _material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallScale), new Vector4(scale.x, scale.y, scale.z, 0f));
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallProjectionRadius), MirrorBallProjectionRadius);
            // Serialized inspector property says Falloff; the actual compiled shader uniform is FallOff.
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallFallOffPower), MirrorBallFallOffPower);
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallIsLoopRotation), MirrorBallIsLoopRotation ? 1f : 0f);
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallLoopRotationSpeed), MirrorBallLoopRotationSpeed);
            _material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallUVOffset), new Vector4(MirrorBallUVOffset.x, MirrorBallUVOffset.y, 0f, 0f));
            _material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallUVScale), new Vector4(MirrorBallUVScale.x, MirrorBallUVScale.y, 0f, 0f));
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MirrorBallUseCubeMap), MirrorBallUseCubeMap ? 1f : 0f);
            _material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._AppTime), AppTime);
            // Current shader/resource additions absent from the older binary key layout.
            _material.SetFloat(BacksideOffId, BacksideOff ? 1f : 0f);
            _material.SetFloat(BacksideFeatherId, BacksideFeather);
        }

        public void Draw(ScriptableRenderContext context, CommandBuffer cmd, Camera camera,
            ref DrawingSettings drawing, int layerMask)
        {
            if (_material == null || !camera.TryGetCullingParameters(out var parameters)) return;
            int hiddenLayer = GraphicSettings.GetLayer(GraphicSettings.LayerIndex.LayerNO_VISIBLE);
            if (_targets.Count != 0 && hiddenLayer < 0)
                throw new InvalidOperationException("MirrorBall cutoff receivers require the authored NO_VISIBLE Unity layer.");
            ApplyParameters();
            OnPreRender?.Invoke();
            try
            {
                for (int i = 0; i < _layerStates.Count; i++)
                {
                    var state = _layerStates[i];
                    if (state.gameObject == null) continue;
                    state.layer = state.gameObject.layer;
                    _layerStates[i] = state;
                    state.gameObject.layer = hiddenLayer;
                }
                parameters.cullingOptions = CullingOptions.None;
                parameters.cullingMask = unchecked((uint)(hiddenLayer >= 0 ? layerMask & ~(1 << hiddenLayer) : layerMask));
                CullingResults receivers = context.Cull(ref parameters);
                GeometryUtility.CalculateFrustumPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, _planes);
                if (!ProjectionToCharacterModelOnly)
                {
                    drawing.overrideMaterial = _material;
                    var filter = new FilteringSettings(RenderQueueRange.all, layerMask);
                    for (int pass = 0; pass < _material.passCount; pass++)
                    {
                        drawing.overrideMaterialPassIndex = pass;
                        context.DrawRenderers(receivers, ref drawing, ref filter);
                    }
                }
                foreach (Target target in _targets)
                {
                    Renderer renderer = target.renderer;
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        (ProjectionToCharacterModelOnly && !target.character) ||
                        !GeometryUtility.TestPlanesAABB(_planes, renderer.bounds)) continue;
                    for (int submesh = 0; submesh < target.materials.Length; submesh++)
                    {
                        Material material = target.materials[submesh];
                        Material receiver = target.sources[submesh];
                        material.CopyPropertiesFromMaterial(_material);
                        int type = target.cutoffTypes[submesh];
                        int textureProperty = type == 3 ? CutoffTex : MainTex;
                        bool needsCutoff = type == 2 || type == 3;
                        bool supported = receiver != null &&
                            ((type != 2 && type != 3 && type != 4 && type != 5) ||
                            (receiver.HasProperty(textureProperty) && (!needsCutoff || receiver.HasProperty(Cutoff))));
                        material.SetInt(CutoffType, supported ? type : -1);
                        if (supported && (type == 2 || type == 3 || type == 4 || type == 5))
                        {
                            material.SetTexture(CutoffTex, receiver.GetTexture(textureProperty));
                            if (needsCutoff) material.SetFloat(Cutoff, receiver.GetFloat(Cutoff));
                        }
                        cmd.DrawRenderer(renderer, material, submesh, 0);
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            finally
            {
                foreach (var state in _layerStates)
                    if (state.gameObject != null) state.gameObject.layer = state.layer;
                OnPostRender?.Invoke();
            }
        }

        public void Release()
        {
            foreach (var target in _targets)
                foreach (var material in target.materials) CoreUtils.Destroy(material);
            _targets.Clear();
            _layerStates.Clear();
            _targetTransforms.Clear();
            if (_originalMaterial == null) return;
            CoreUtils.Destroy(_material);
            _material = _originalMaterial;
            _originalMaterial = null;
        }
        private void OnDestroy() { Release(); }
    }
}
