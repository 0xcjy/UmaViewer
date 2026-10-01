using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // ProjectorFeature.Create 0x1a39af0: event 300, all queues, CommonOpaque.
    // CustomProjector.DrawProjector 0x1a13d10: recull each authored projector and
    // redraw receiving geometry with its material, not a volume mesh/decal or a Light cookie.
    public sealed class ProjectorRenderPass : ScriptableRenderPass, IDisposable
    {
        private static readonly ShaderTagId ReceiverTag = new ShaderTagId("Projector");
        private static readonly ShaderTagId[] OutlineTags =
        {
            default, new ShaderTagId("BlendProjector"), new ShaderTagId("AddProjector"),
            new ShaderTagId("MulProjector")
        };
        private readonly Plane[] _planes = new Plane[6];
        private IReadOnlyList<CustomProjector> _projectors;
        private IReadOnlyList<MirrorBallProjector> _mirrorBallProjectors;
        private static readonly int OutlineLightMode = Shader.PropertyToID("_OutlineLightModeTag");

        public ProjectorRenderPass() { renderPassEvent = RenderPassEvent.AfterRenderingOpaques; }

        // Call from AddRenderPasses; this pass targets the currently bound camera color/depth.
        public bool Setup(IReadOnlyList<CustomProjector> projectors,
            IReadOnlyList<MirrorBallProjector> mirrorBallProjectors = null)
        {
            _projectors = projectors;
            _mirrorBallProjectors = mirrorBallProjectors;
            if (projectors != null)
                for (int i = 0; i < projectors.Count; i++)
                    if (projectors[i] != null && projectors[i].isActiveAndEnabled) return true;
            if (mirrorBallProjectors != null)
                for (int i = 0; i < mirrorBallProjectors.Count; i++)
                    if (mirrorBallProjectors[i] != null && mirrorBallProjectors[i].isActiveAndEnabled) return true;
            return false;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if ((_projectors == null && _mirrorBallProjectors == null) || camera == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Gallop.Projector");
            try
            {
                for (int i = 0; i < (_projectors?.Count ?? 0); i++)
                {
                    CustomProjector source = _projectors[i];
                    if (source == null || !source.isActiveAndEnabled || source.Material == null ||
                        (camera.cullingMask & (1 << source.gameObject.layer)) == 0) continue;
                    int layerMask = camera.cullingMask & (source.OverrideIgnoreLayer
                        ? (int)source.OverrideLayerMask : ~(int)source.IgnoreLayer);
                    if (layerMask == 0 || !camera.TryGetCullingParameters(out var parameters)) continue;
                    source.OnPreRender?.Invoke();
                    try
                    {
                        source.CalculateMatrices(out var cullingMatrix, out var projectorMatrix, out var clipMatrix);
                        GeometryUtility.CalculateFrustumPlanes(cullingMatrix, _planes);
                        parameters.cullingOptions = CullingOptions.None;
                        parameters.reflectionProbeSortingCriteria = ReflectionProbeSortingCriteria.None;
                        parameters.cullingMask = unchecked((uint)layerMask);
                        // Native ExecuteCulling uses the projector's localToWorld matrix here,
                        // while its six explicit culling planes come from the calculated frustum.
                        parameters.cullingMatrix = source.transform.localToWorldMatrix;
                        parameters.cullingPlaneCount = 6;
                        for (int p = 0; p < 6; p++) parameters.SetCullingPlane(p, _planes[p]);
                        CullingResults receivers = context.Cull(ref parameters);
                        cmd.SetGlobalMatrix(ShaderManager.GetPropertyId(ShaderManager.PropertyId.unity_Projector), projectorMatrix);
                        cmd.SetGlobalMatrix(ShaderManager.GetPropertyId(ShaderManager.PropertyId.unity_ProjectorClip), clipMatrix);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        DrawingSettings drawing = CreateDrawingSettings(ReceiverTag, ref renderingData, SortingCriteria.CommonOpaque);
                        drawing.perObjectData = PerObjectData.None;
                        drawing.overrideMaterial = source.Material;
                        FilteringSettings filter = new FilteringSettings(RenderQueueRange.all, layerMask);
                        for (int pass = 0; pass < source.Material.passCount; pass++)
                        {
                            drawing.overrideMaterialPassIndex = pass;
                            context.DrawRenderers(receivers, ref drawing, ref filter);
                        }
                        // Native optional receiver-owned outline pass (no override material).
                        if (!source.Material.HasProperty(OutlineLightMode)) continue;
                        int outline = source.Material.GetInt(OutlineLightMode);
                        if (outline <= 0 || outline >= OutlineTags.Length) continue;
                        drawing = CreateDrawingSettings(OutlineTags[outline], ref renderingData, SortingCriteria.CommonOpaque);
                        drawing.perObjectData = PerObjectData.None;
                        cmd.SetGlobalTexture(ShaderManager.GetPropertyId(ShaderManager.PropertyId._ProjectorTex),
                            source.Material.GetTexture(ShaderManager.GetPropertyId(ShaderManager.PropertyId._MainTex)));
                        cmd.SetGlobalColor(ShaderManager.GetPropertyId(ShaderManager.PropertyId._ProjectorColor),
                            source.Material.GetColor(ShaderManager.GetPropertyId(ShaderManager.PropertyId._Color)));
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        context.DrawRenderers(receivers, ref drawing, ref filter);
                    }
                    finally { source.OnPostRender?.Invoke(); }
                }
                for (int i = 0; i < (_mirrorBallProjectors?.Count ?? 0); i++)
                {
                    MirrorBallProjector source = _mirrorBallProjectors[i];
                    if (source == null || !source.isActiveAndEnabled || source.Material == null ||
                        (camera.cullingMask & (1 << source.gameObject.layer)) == 0) continue;
                    int layerMask = camera.cullingMask & (source.OverrideIgnoreLayer
                        ? (int)source.OverrideLayerMask : ~(int)source.IgnoreLayer);
                    if (layerMask == 0 && !source.ProjectionToCharacterModelOnly) continue;
                    DrawingSettings drawing = CreateDrawingSettings(ReceiverTag, ref renderingData, SortingCriteria.CommonOpaque);
                    drawing.perObjectData = PerObjectData.None;
                    source.Draw(context, cmd, camera, ref drawing, layerMask);
                }
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose() { _projectors = null; _mirrorBallProjectors = null; }
    }
}
