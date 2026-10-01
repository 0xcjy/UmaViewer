using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;

namespace Gallop.Live
{
    internal sealed class MirrorBallProjectionRuntime : IDisposable
    {
        private sealed class Binding
        {
            public MirrorBallProjector projector;
            public Vector3 position, scale, axis;
            public Quaternion rotation;
            public Vector2 uvOffset, uvScale;
            public float radius, falloff, speed, value, appTime, feather;
            public bool enabled, loop, cube, onlyCharacter, backside, overrideIgnore;
            public LayerMask overrideMask;
            public readonly HashSet<int> missingTextures = new HashSet<int>();
        }
        private readonly Dictionary<int, Binding> _bindings = new Dictionary<int, Binding>();
        private readonly List<MirrorBallProjector> _projectors = new List<MirrorBallProjector>();
        public IReadOnlyList<MirrorBallProjector> Projectors => _projectors;

        public void Initialize(Transform stageRoot, Func<int, Transform> characterResolver)
        {
            Dispose();
            var stageRenderers = stageRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var projector in stageRoot.GetComponentsInChildren<MirrorBallProjector>(true))
            {
                int hash = FNVHash.Generate(projector.name);
                if (_bindings.ContainsKey(hash)) throw new InvalidOperationException("Duplicate MirrorBallProjector name: " + projector.name);
                if (!projector.Initialize())
                {
                    Debug.LogError("MirrorBallProjector '" + projector.name + "' has no authored material/shader.", projector);
                    continue;
                }
                Transform transform = projector.transform;
                var binding = new Binding
                {
                    projector = projector, enabled = projector.enabled,
                    position = transform.localPosition, rotation = transform.localRotation, scale = transform.localScale,
                    axis = projector.MirrorBallRotationAxis, radius = projector.MirrorBallProjectionRadius,
                    falloff = projector.MirrorBallFallOffPower, speed = projector.MirrorBallLoopRotationSpeed,
                    value = projector.MirrorBallLoopRotationValue, appTime = projector.AppTime,
                    uvOffset = projector.MirrorBallUVOffset, uvScale = projector.MirrorBallUVScale,
                    loop = projector.MirrorBallIsLoopRotation, cube = projector.MirrorBallUseCubeMap,
                    onlyCharacter = projector.ProjectionToCharacterModelOnly, backside = projector.BacksideOff,
                    feather = projector.BacksideFeather, overrideIgnore = projector.OverrideIgnoreLayer,
                    overrideMask = projector.OverrideLayerMask
                };
                _bindings.Add(hash, binding);
                _projectors.Add(projector);
                foreach (var renderer in stageRenderers) projector.AddTargetRenderer(renderer, false);
                // StageController.SetupLightProjection registers all twenty character positions.
                for (int position = 0; position < 20; position++)
                {
                    Transform character = characterResolver?.Invoke(position);
                    if (character == null) continue;
                    foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                        projector.AddTargetRenderer(renderer, true);
                }
                projector.enabled = false;
            }
        }

        public bool Apply(ref LiveLightProjectionTimeline.UpdateInfo info, Func<int, Texture> textures,
            Func<int, Texture> mirrorTextures)
        {
            if (info.group == null || !_bindings.TryGetValue(info.group.nameHash, out var binding)) return false;
            var projector = binding.projector;
            var a = info.current;
            if (a == null) { projector.enabled = false; return true; }
            var b = info.next ?? a;
            float t = info.progress;
            Transform transform = projector.transform;
            transform.localPosition = Vector3.LerpUnclamped(a.Position, b.Position, t);
            // Native explicitly excludes MirrorBall projectors from character attachment.
            transform.localRotation = Quaternion.Lerp(a.Rotation, b.Rotation, t);
            transform.localScale = Vector3.LerpUnclamped(a.Scale, b.Scale, t);
            projector.OverrideIgnoreLayer = a.OverrideIgnoreLayer;
            projector.OverrideLayerMask = a.OverrideLayerMask;
            projector.ProjectionToCharacterModelOnly = a.ProjectionToCharacterModelOnly;
            projector.MirrorBallProjectionRadius = Mathf.LerpUnclamped(a.MirrorBallProjectionRadius, b.MirrorBallProjectionRadius, t);
            projector.MirrorBallFallOffPower = Mathf.LerpUnclamped(a.MirrorBallFallOffPower, b.MirrorBallFallOffPower, t);
            projector.MirrorBallRotationAxis = a.MirrorBallRotateAxis;
            projector.MirrorBallLoopRotationValue = Mathf.LerpUnclamped(a.MirrorBallRotateValue, b.MirrorBallRotateValue, t);
            projector.MirrorBallIsLoopRotation = a.MirrorBallIsLoopRotation;
            projector.MirrorBallLoopRotationSpeed = Mathf.LerpUnclamped(a.MirrorBallLoopRotationSpeed, b.MirrorBallLoopRotationSpeed, t);
            projector.MirrorBallUVOffset = Vector2.LerpUnclamped(a.MirrorBallUVOffset, b.MirrorBallUVOffset, t);
            projector.MirrorBallUVScale = Vector2.LerpUnclamped(a.MirrorBallUVScale, b.MirrorBallUVScale, t);
            projector.MirrorBallUseCubeMap = a.MirrorBallUseCubeMap;
            projector.AppTime = info.time;
            projector.BacksideOff = a.BacksideOff;
            projector.BacksideFeather = Mathf.LerpUnclamped(a.MirrorBallBacksideFeather, b.MirrorBallBacksideFeather, t);
            Material material = projector.Material;
            material.SetColor(ShaderManager.GetPropertyId(ShaderManager.PropertyId._ProjectorMulColor0), info.color);
            material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._ProjectorColorPower),
                Mathf.LerpUnclamped(a.ColorPower, b.ColorPower, t));
            LiveDefine.TrySetLightBlendModeMaterialProperty((LiveDefine.LightBlendMode)a.LightBlendMode, material);
            Func<int, Texture> resolver = info.group.ContentType == 0 ? textures :
                info.group.ContentType == 1 ? mirrorTextures : null;
            if (a.IsEnable)
            {
                Texture texture = resolver?.Invoke(a.TextureId);
                if (texture == null)
                {
                    projector.enabled = false;
                    if (binding.missingTextures.Add(a.TextureId))
                        Debug.LogError("MirrorBallProjector '" + projector.name + "' is missing authored ContentType " +
                            info.group.ContentType + " texture " + a.TextureId + ".", projector);
                    return true;
                }
                material.SetTexture(ShaderManager.GetPropertyId(ShaderManager.PropertyId._LightTex), texture);
            }
            projector.enabled = a.IsEnable;
            return true;
        }

        public void Dispose()
        {
            foreach (var b in _bindings.Values)
            {
                var p = b.projector;
                if (p == null) continue;
                p.Release();
                p.transform.localPosition = b.position;
                p.transform.localRotation = b.rotation;
                p.transform.localScale = b.scale;
                p.enabled = b.enabled;
                p.MirrorBallRotationAxis = b.axis;
                p.MirrorBallProjectionRadius = b.radius;
                p.MirrorBallFallOffPower = b.falloff;
                p.MirrorBallLoopRotationSpeed = b.speed;
                p.MirrorBallLoopRotationValue = b.value;
                p.AppTime = b.appTime;
                p.MirrorBallUVOffset = b.uvOffset;
                p.MirrorBallUVScale = b.uvScale;
                p.MirrorBallIsLoopRotation = b.loop;
                p.MirrorBallUseCubeMap = b.cube;
                p.ProjectionToCharacterModelOnly = b.onlyCharacter;
                p.BacksideOff = b.backside;
                p.BacksideFeather = b.feather;
                p.OverrideIgnoreLayer = b.overrideIgnore;
                p.OverrideLayerMask = b.overrideMask;
            }
            _bindings.Clear();
            _projectors.Clear();
        }
    }
}
