using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    public static class RenderUtils
    {
        public static Material GetMaterial(Renderer renderer)
        {
            if (renderer == null)
                return null;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
                return null;

            string originalName = sharedMaterial.name;

            Material material = renderer.material;
            if (material == null)
                return null;

            material.name = originalName;
            return material;
        }

        public static void CopyMaterial(
            Material source,
            Material destination,
            bool nameReplace = true)
        {
            if (source == null || destination == null)
                return;

            if (nameReplace)
                destination.name = source.name;

            if (destination.shader != source.shader)
                destination.shader = source.shader;

            destination.CopyPropertiesFromMaterial(source);
        }

        public static void Destroy(ref Material obj)
        {
            CoreUtils.Destroy(obj);
            obj = null;
        }
    }
}