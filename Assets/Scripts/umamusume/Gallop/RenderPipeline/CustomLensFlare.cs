using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Flare host of the live stage lights (`*flarelight*`). The asset it points at (LensFlareData)
    /// is the in-house copy of Unity's legacy Flare: one texture atlas plus a list of elements
    /// strung out along the line that runs from this object through the screen centre (Position 0 =
    /// this object, 1 = screen centre, values above 1 go past the centre).
    ///
    /// URP never renders `UnityEngine.LensFlare`, so each element is drawn here as one camera-facing
    /// quad that samples its own slice of the atlas (UVs baked into the mesh, tint per element via a
    /// property block). The per-frame state (on/off, colour, brightness, fade speed) is written by
    /// `RecoveredLensFlareController`, the port of the native `UnityLensFlareController`
    /// (AlterUpdate / UpdateInfo), so the engine's rules live in a single place.
    ///
    /// The serialized field names are part of the bundle layout: they must not be renamed.
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomLensFlare : MonoBehaviour
    {
        private const string kShaderName = "Hidden/UmaViewer/LensFlareAdditive";
        private static readonly int sColorId = Shader.PropertyToID("_Color");

        /// <summary>Global brightness multiplier, for A/B against a reference capture.</summary>
        public static float RuntimeStrength = 1f;

        /// <summary>
        /// The built-in flare is hidden by any collider between the flare and the camera. The
        /// stage's own lamp colliders sit at the flare source itself, so the raycast is opt-in
        /// (it always ignores <see cref="IgnoreLayers"/> either way).
        /// </summary>
        public static bool RuntimeOcclusion = false;

        // ---- Serialized fields of the authored flare host (do not rename) ----
        public LensFlareData Flare;
        public float Brightness = 1f;
        public float FadeSpeed = 1f;
        public Color Color = Color.white;
        public LayerMask IgnoreLayers;
        public bool IsDirectional;

        // ---- Render tuning (absent from the authored assets, so the initializers apply) ----
        [Header("Render")]
        [Tooltip("Fraction of the screen height covered by an element whose Size is 1. " +
                 "The authored assets use Size 3 and 6, so 0.02 draws them at 6% and 12% of the " +
                 "screen height. This is the one knob to calibrate against a reference capture: " +
                 "the flare asset stores the element size in an arbitrary unit.")]
        public float elementSizeScale = 0.02f;
        [Tooltip("Scale an element grows from while it is fading in (Zoom elements only).")]
        public float zoomMinScale = 0.35f;

        private sealed class ElementQuad
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public LensFlareElement Element;
            public MaterialPropertyBlock Properties;
        }

        private readonly List<ElementQuad> _elements = new List<ElementQuad>();
        private LensFlareData _builtData;
        private Transform _root;
        private Light _light;
        private bool _builtLight;
        private int _layer = -1;
        private bool _enabled = true;
        private float _fade;

        private static Shader sShader;
        private static bool sShaderMissing;
        private static readonly Dictionary<LensFlareData, Mesh[]> sMeshCache = new Dictionary<LensFlareData, Mesh[]>();
        private static readonly Dictionary<Texture, Material> sMaterialCache = new Dictionary<Texture, Material>();

        // ---- State written by RecoveredLensFlareController ----

        /// <summary>
        /// Native `UnityLensFlareController.UpdateInfo`: the timeline hands the flare its tint,
        /// brightness and fade speed, and turns it on or off. Positioning is applied by the caller
        /// (it owns the authored local position and the group fallback).
        /// </summary>
        public void ApplyTimeline(bool enabled, bool overridePosition, Vector3 offset,
            Color color, float brightness, float fadeSpeed)
        {
            _enabled = enabled;
            // The authored tint carries alpha 0 by convention (it is not opacity), and the timeline
            // colour replaces it, exactly like LensFlare.color did natively.
            Color = color;
            Brightness = Mathf.Max(0f, brightness);
            FadeSpeed = Mathf.Max(0f, fadeSpeed);
        }

        // ---- Rendering ----

        private void OnDisable()
        {
            // The controller deactivates the host when the timeline says so: take the quads with it.
            ClearElements();
        }

        private void OnDestroy()
        {
            ClearElements();
        }

        private void LateUpdate()
        {
            if (!EnsureElements())
            {
                SetAllQuadsActive(false);
                return;
            }

            Camera camera = ResolveCamera();
            if (camera == null)
            {
                SetAllQuadsActive(false);
                return;
            }

            int layer = ResolveLayer(camera);
            if (_layer != layer)
            {
                _layer = layer;
                SetRootLayer(_layer);
            }

            // On screen and in front of the camera, like the flare renderer required.
            Vector3 viewport = camera.WorldToViewportPoint(transform.position);
            bool visible = viewport.z > 0f &&
                           viewport.x >= 0f && viewport.x <= 1f &&
                           viewport.y >= 0f && viewport.y <= 1f;
            if (visible && RuntimeOcclusion && IsOccluded(camera))
                visible = false;

            float target = (_enabled && Brightness > 0f && visible) ? 1f : 0f;
            _fade = Mathf.MoveTowards(_fade, target, Mathf.Max(0.0001f, FadeSpeed) * Time.deltaTime);

            Vector3 sourceScreen = camera.WorldToScreenPoint(transform.position);
            if (_fade <= 0.001f || sourceScreen.z <= 0.0001f)
            {
                SetAllQuadsActive(false);
                return;
            }

            // The authored tints carry alpha 0 by convention: only RGB counts.
            float tintR = Color.r;
            float tintG = Color.g;
            float tintB = Color.b;
            float tintBrightness = Mathf.Max(0f, Brightness) * RuntimeStrength;

            float screenHeight = camera.pixelHeight;
            Vector2 center = new Vector2(camera.pixelWidth * 0.5f, screenHeight * 0.5f);
            // Size 1 covers `elementSizeScale` of the screen height; ScreenToWorldPoint wants a
            // world size, and a pixel at that depth measures this much:
            float worldPerScreenHeight = 2f * sourceScreen.z * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Quaternion cameraRotation = camera.transform.rotation;
            bool useLightColor = HasLightColor();

            for (int i = 0; i < _elements.Count; i++)
            {
                ElementQuad quad = _elements[i];
                LensFlareElement element = quad.Element;
                if (element == null)
                {
                    SetQuadActive(quad, false);
                    continue;
                }

                // "Fade": the element fades in and out with the flare instead of popping.
                float elementFade = element.Fade ? _fade : (visible ? 1f : 0f);
                if (elementFade <= 0.001f)
                {
                    SetQuadActive(quad, false);
                    continue;
                }

                Vector2 elementScreen = Vector2.LerpUnclamped(sourceScreen, center, element.Position);
                float size = Mathf.Max(0f, element.Size) * elementSizeScale * worldPerScreenHeight;
                // "Zoom": the element scales up while it becomes visible.
                if (element.Zoom)
                    size *= Mathf.Lerp(zoomMinScale, 1f, elementFade);
                if (size <= 0f)
                {
                    SetQuadActive(quad, false);
                    continue;
                }

                Quaternion rotation = cameraRotation;
                if (element.Rotate)
                {
                    // "Rotate": the bottom of the element always faces the screen centre.
                    Vector2 direction = elementScreen - center;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                    rotation *= Quaternion.AngleAxis(angle, Vector3.forward);
                }

                Transform quadTransform = quad.Transform;
                quadTransform.SetPositionAndRotation(
                    camera.ScreenToWorldPoint(new Vector3(elementScreen.x, elementScreen.y, sourceScreen.z)),
                    rotation);
                quadTransform.localScale = new Vector3(size, size, 1f);

                Color elementColor = element.Color;
                float lightR = 1f, lightG = 1f, lightB = 1f;
                if (element.UseLightColor && useLightColor)
                {
                    // The authored hosts have no Light component, so this stays neutral and the
                    // lamp colour keeps coming from the controller (it folds the renderer's
                    // _BlinkLightColor / _MulColor0 * _ColorPower * 2 into the timeline colour).
                    lightR = _light.color.r;
                    lightG = _light.color.g;
                    lightB = _light.color.b;
                }

                float strength = tintBrightness * elementFade;
                Color finalColor = new Color(
                    elementColor.r * tintR * lightR * strength,
                    elementColor.g * tintG * lightG * strength,
                    elementColor.b * tintB * lightB * strength,
                    1f);

                quad.Renderer.GetPropertyBlock(quad.Properties);
                quad.Properties.SetColor(sColorId, finalColor);
                quad.Renderer.SetPropertyBlock(quad.Properties);
                SetQuadActive(quad, true);
            }
        }

        // ---- Element construction ----

        private bool EnsureElements()
        {
            if (Flare == null || !Flare.HasTexture || Flare.ElementCount == 0)
            {
                // Missing authored resources stay missing: never invent a flare.
                if (_builtData != null)
                    ClearElements();
                return false;
            }

            if (_builtData == Flare)
                return _elements.Count > 0;

            ClearElements();

            Mesh[] meshes = GetElementMeshes(Flare);
            Material material = GetMaterial(Flare.Texture);
            if (meshes == null || material == null)
                return false;

            var rootObject = new GameObject("LensFlareQuads_" + name);
            _root = rootObject.transform;

            // World-space quads: keeping them outside the host avoids inheriting its (possibly
            // scaled) transform, which would distort the per-element screen size.
            _root.SetParent(null, false);
            _layer = -1;

            for (int i = 0; i < Flare.ElementCount; i++)
            {
                LensFlareElement element = Flare.ElementArray[i];
                if (element == null)
                    continue;

                var quadObject = new GameObject("flare_" + name + "_" + i);
                quadObject.transform.SetParent(_root, false);

                var filter = quadObject.AddComponent<MeshFilter>();
                filter.sharedMesh = meshes[i];

                var renderer = quadObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;

                quadObject.SetActive(false);
                _elements.Add(new ElementQuad
                {
                    Transform = quadObject.transform,
                    Renderer = renderer,
                    Element = element,
                    Properties = new MaterialPropertyBlock()
                });
            }

            CacheLight();
            _builtData = Flare;
            return _elements.Count > 0;
        }

        private void ClearElements()
        {
            _elements.Clear();
            _builtData = null;
            _layer = -1;
            if (_root != null)
            {
                if (Application.isPlaying)
                    Destroy(_root.gameObject);
                else
                    DestroyImmediate(_root.gameObject);
                _root = null;
            }
        }

        private void CacheLight()
        {
            if (_builtLight)
                return;
            _builtLight = true;
            _light = GetComponent<Light>();
        }

        private bool HasLightColor()
        {
            CacheLight();
            return _light != null;
        }

        private bool IsOccluded(Camera camera)
        {
            Vector3 target = Vector3.Lerp(camera.transform.position, transform.position, 0.98f);
            int mask = ~(int)IgnoreLayers;
            return Physics.Linecast(camera.transform.position, target, mask, QueryTriggerInteraction.Ignore);
        }

        private void SetRootLayer(int layer)
        {
            if (_root == null)
                return;
            Transform[] children = _root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                children[i].gameObject.layer = layer;
        }

        private int ResolveLayer(Camera camera)
        {
            int layer = gameObject.layer;
            if ((camera.cullingMask & (1 << layer)) == 0)
                layer = 0;
            return layer;
        }

        private static Camera ResolveCamera()
        {
            Gallop.Live.Director director = Gallop.Live.Director.instance;
            if (director != null && director.MainRenderCamera != null)
                return director.MainRenderCamera;

            return Camera.main;
        }

        private void SetAllQuadsActive(bool active)
        {
            for (int i = 0; i < _elements.Count; i++)
                SetQuadActive(_elements[i], active);
        }

        private static void SetQuadActive(ElementQuad quad, bool active)
        {
            if (quad == null || quad.Transform == null)
                return;
            GameObject quadObject = quad.Transform.gameObject;
            if (quadObject.activeSelf != active)
                quadObject.SetActive(active);
        }

        // ---- Shared render resources ----

        private static Mesh[] GetElementMeshes(LensFlareData data)
        {
            if (sMeshCache.TryGetValue(data, out Mesh[] cached) &&
                cached != null && cached.Length == data.ElementCount && cached[0] != null)
                return cached;

            PruneSharedResources();

            var meshes = new Mesh[data.ElementCount];
            for (int i = 0; i < meshes.Length; i++)
            {
                LensFlareElement element = data.ElementArray[i];
                Rect uv = new Rect(0f, 0f, 1f, 1f);
                if (element != null)
                    data.TryGetImageUvRect(element.ImageIndex, out uv);
                meshes[i] = BuildQuadMesh(uv, data.Texture);
            }

            sMeshCache[data] = meshes;
            return meshes;
        }

        private static Mesh BuildQuadMesh(Rect uv, Texture source)
        {
            var mesh = new Mesh
            {
                name = "LensFlareQuad_" + (source != null ? source.name : "none") + "_" + uv.x + "_" + uv.y,
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(uv.xMin, uv.yMin),
                new Vector2(uv.xMin, uv.yMax),
                new Vector2(uv.xMax, uv.yMax),
                new Vector2(uv.xMax, uv.yMin)
            };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetMaterial(Texture texture)
        {
            if (texture == null)
                return null;

            if (sMaterialCache.TryGetValue(texture, out Material cached) && cached != null)
                return cached;

            if (sShader == null && !sShaderMissing)
            {
                sShader = Shader.Find(kShaderName);
                if (sShader == null)
                {
                    sShaderMissing = true;
                    Debug.LogWarning($"[CustomLensFlare] shader no encontrado: {kShaderName}");
                }
            }
            if (sShader == null)
                return null;

            PruneSharedResources();

            var material = new Material(sShader)
            {
                name = "mat_lensflare_" + texture.name,
                hideFlags = HideFlags.DontSave
            };
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);
            material.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
            material.renderQueue = (int)RenderQueue.Transparent + 100;
            sMaterialCache[texture] = material;
            return material;
        }

        // The static caches live across stage loads; a new stage destroys the previous bundle
        // assets, which turns their keys into destroyed objects. Drop those entries (and their
        // meshes/materials) whenever a cache miss happens.
        private static void PruneSharedResources()
        {
            var deadData = new List<LensFlareData>();
            foreach (KeyValuePair<LensFlareData, Mesh[]> entry in sMeshCache)
                if (entry.Key == null)
                    deadData.Add(entry.Key);
            for (int i = 0; i < deadData.Count; i++)
            {
                if (sMeshCache.TryGetValue(deadData[i], out Mesh[] meshes) && meshes != null)
                    for (int m = 0; m < meshes.Length; m++)
                        if (meshes[m] != null)
                            DestroyShared(meshes[m]);
                sMeshCache.Remove(deadData[i]);
            }

            var deadTextures = new List<Texture>();
            foreach (KeyValuePair<Texture, Material> entry in sMaterialCache)
                if (entry.Key == null)
                    deadTextures.Add(entry.Key);
            for (int i = 0; i < deadTextures.Count; i++)
            {
                if (sMaterialCache.TryGetValue(deadTextures[i], out Material material) && material != null)
                    DestroyShared(material);
                sMaterialCache.Remove(deadTextures[i]);
            }
        }

        private static void DestroyShared(Object shared)
        {
            if (shared == null)
                return;
            if (Application.isPlaying)
                Destroy(shared);
            else
                DestroyImmediate(shared);
        }

        // ---- Diagnostics ----

        /// <summary>
        /// Prints how many flare hosts exist, how many resolved their authored asset and how many
        /// are actually drawn. Used to check the live stage from the console.
        /// </summary>
        public static void LogRuntimeDiagnostics(Camera camera)
        {
            CustomLensFlare[] flares = FindObjectsOfType<CustomLensFlare>(true);
            int active = 0, withData = 0, quads = 0, drawn = 0, fadedIn = 0, occlusionOn = 0;
            string sample = string.Empty;

            for (int i = 0; i < flares.Length; i++)
            {
                CustomLensFlare flare = flares[i];
                if (flare == null)
                    continue;

                if (flare.gameObject.activeInHierarchy)
                    active++;
                if (flare.Flare != null)
                    withData++;
                if (flare.Flare != null && flare.Flare.Texture != null && RuntimeOcclusion)
                    occlusionOn++;
                int drawnHere = 0;
                for (int q = 0; q < flare._elements.Count; q++)
                {
                    quads++;
                    if (flare._elements[q].Transform != null && flare._elements[q].Transform.gameObject.activeSelf)
                        drawnHere++;
                }
                drawn += drawnHere;
                if (flare._fade > 0.5f)
                    fadedIn++;
                if (sample.Length == 0 && flare.Flare != null)
                    sample = flare.name + ":elements=" + flare.Flare.ElementCount +
                             ":layout=" + flare.Flare.TextureLayout +
                             ":brightness=" + flare.Brightness.ToString("0.###") +
                             ":quads=" + flare._elements.Count +
                             ":drawn=" + drawnHere +
                             ":fade=" + flare._fade.ToString("0.###");
            }

            Debug.Log($"[CustomLensFlare] hosts={flares.Length} active={active} withData={withData} " +
                      $"quads={quads} drawn={drawn} fadedIn={fadedIn} occlusion={occlusionOn}" +
                      (camera != null ? $" camera={camera.name}" : string.Empty) +
                      (sample.Length > 0 ? " sample=" + sample : string.Empty));
        }
    }
}
