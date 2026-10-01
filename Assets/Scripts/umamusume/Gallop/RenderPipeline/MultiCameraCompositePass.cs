using Gallop.Live;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // Execute after URP14's final blit (AfterRendering + 1), not on an uninitialized post-FX
    // temporary. Each auxiliary camera has its own actual output attachment.
    public sealed class MultiCameraCompositePass : ScriptableRenderPass
    {
        private MultiCamera _camera;

        public MultiCameraCompositePass()
        {
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRendering + 3);
        }

        public bool Setup(MultiCamera camera)
        {
            _camera = camera;
            return camera != null && camera.isActiveAndEnabled && camera.Composite != null &&
                camera.Composite.isActiveAndEnabled && camera.FinalComposite != null && camera.FinalComposite.isActiveAndEnabled;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_camera == null || !_camera.isActiveAndEnabled || _camera.Composite == null ||
                !_camera.Composite.isActiveAndEnabled || _camera.FinalComposite == null ||
                !_camera.FinalComposite.isActiveAndEnabled || renderingData.cameraData.camera != _camera.GetCamera() ||
                _camera.CaptureTexture == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("MultiCameraComposite");
            try
            {
                _camera.Composite.AfterRenderingCallback(cmd, _camera.CaptureTexture);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
    }

    public sealed class MultiCameraFinalCompositePass : ScriptableRenderPass
    {
        private MultiCameraFinalComposite _composite;

        public MultiCameraFinalCompositePass()
        {
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRendering - 1);
        }

        public bool Setup(MultiCameraFinalComposite composite)
        {
            _composite = composite;
            return composite != null && composite.isActiveAndEnabled;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_composite == null || !_composite.isActiveAndEnabled || renderingData.cameraData.camera != _composite.RenderCamera) return;
            var colorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (colorTarget == null) return;
            RenderTargetIdentifier destination = colorTarget.nameID;
            CommandBuffer cmd = CommandBufferPool.Get("MultiCameraFinalComposite");
            try
            {
                _composite.AfterRenderingCallback(cmd, destination, renderingData.cameraData.IsCameraProjectionMatrixFlipped());
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
    }
}
