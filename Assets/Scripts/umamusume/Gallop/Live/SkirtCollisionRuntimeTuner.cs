using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>Runtime tuning for hair, cloth, tail, and accessory spring motion.</summary>
    public sealed class SkirtCollisionRuntimeTuner : MonoBehaviour
    {
        private enum MotionCategory
        {
            Skirt,
            Other
        }

        private sealed class ParticleDefaults
        {
            public DynamicBone.Particle Particle;
            public MotionCategory Category;
            public float Damping;
            public float Elasticity;
            public float Stiffness;
            public float Inert;
        }

        private sealed class ColliderDefaults
        {
            public DynamicBoneCollider Collider;
            public float Radius;
        }

        private readonly List<ParticleDefaults> _particles = new List<ParticleDefaults>();
        private readonly HashSet<DynamicBone.Particle> _knownParticles = new HashSet<DynamicBone.Particle>();
        private readonly List<ColliderDefaults> _hipColliders = new List<ColliderDefaults>();
        private readonly HashSet<DynamicBoneCollider> _knownHipColliders = new HashSet<DynamicBoneCollider>();
        private readonly List<ColliderDefaults> _legColliders = new List<ColliderDefaults>();
        private readonly HashSet<DynamicBoneCollider> _knownLegColliders = new HashSet<DynamicBoneCollider>();
        private bool _visible;
        private bool _initialized;
        private float _dampingScale = 3f;
        private float _elasticityScale = 1.5f;
        private float _stiffnessScale = 2f;
        private float _inertScale = 1.1f;
        private float _hipRadiusScale = 0.2f;
        private float _legRadiusScale = 0.8f;
        private float _horizontalStiffness = 0.5f;
        private float _otherDampingScale = 3f;
        private float _otherElasticityScale = 1.5f;
        private float _otherStiffnessScale = 2f;
        private float _otherInertScale = 1.1f;
        private float _otherRigidity = 0.85f;
        private float _monitorBrightness = 1f;
        private float _monitorLightIntensity = 0.25f;
        private bool _showSkirtColliders;
        private bool _skirtBodyCollisionEnabled = true;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                _visible = !_visible;

            if (Director.instance == null || Director.instance.CharaContainerScript.Count == 0)
                return;

            bool added = false;
            bool addedCollider = false;
            foreach (UmaContainerCharacter character in Director.instance.CharaContainerScript)
            {
                if (character == null)
                    continue;

                foreach (DynamicBone dynamicBone in character.GetComponentsInChildren<DynamicBone>(true))
                {
                    MotionCategory category = GetMotionCategory(dynamicBone);
                    foreach (DynamicBone.Particle particle in dynamicBone.Particles)
                    {
                        if (!_knownParticles.Add(particle))
                            continue;

                        _particles.Add(new ParticleDefaults
                        {
                            Particle = particle,
                            Category = category,
                            Damping = particle.m_Damping,
                            Elasticity = particle.m_Elasticity,
                            Stiffness = particle.m_Stiffness,
                            Inert = particle.m_Inert
                        });
                        added = true;
                    }
                }

                foreach (DynamicBoneCollider collider in character.GetComponentsInChildren<DynamicBoneCollider>(true))
                {
                    if (IsHipSkirtCollider(collider) && _knownHipColliders.Add(collider))
                    {
                        _hipColliders.Add(new ColliderDefaults
                        {
                            Collider = collider,
                            Radius = collider.m_Radius
                        });
                        addedCollider = true;
                    }
                    if (IsLegSkirtCollider(collider) && _knownLegColliders.Add(collider))
                    {
                        _legColliders.Add(new ColliderDefaults
                        {
                            Collider = collider,
                            Radius = collider.m_Radius
                        });
                        addedCollider = true;
                    }
                }
            }

            _initialized = _particles.Count > 0;
            if (added || addedCollider)
                Apply();
        }

        private void OnGUI()
        {
            if (!_visible || !_initialized)
                return;

            GUILayout.BeginArea(new Rect(16, 16, 430, 720), GUI.skin.window);
            GUILayout.Label("Secondary Motion Tuning");

            bool changed = false;
            changed |= SliderRow("Damping", ref _dampingScale, 0.25f, 4f);
            changed |= SliderRow("Elasticity", ref _elasticityScale, 0f, 1.5f);
            changed |= SliderRow("Stiffness", ref _stiffnessScale, 0.25f, 2f);
            changed |= SliderRow("Inertia", ref _inertScale, 0f, 1.5f);
            changed |= SliderRow("Hip radius", ref _hipRadiusScale, 0.1f, 1.5f);
            changed |= SliderRow("Leg radius", ref _legRadiusScale, 0.5f, 1.5f);
            changed |= SliderRow("Horizontal stiffness", ref _horizontalStiffness, 0f, 1f);

            GUILayout.Space(5);
            GUILayout.Label("All non-skirt physics (relative to skirt base)");
            changed |= SliderRow("Other damping", ref _otherDampingScale, 0.25f, 4f);
            changed |= SliderRow("Other elasticity", ref _otherElasticityScale, 0f, 1.5f);
            changed |= SliderRow("Other stiffness", ref _otherStiffnessScale, 0.25f, 2f);
            changed |= SliderRow("Other inertia", ref _otherInertScale, 0f, 1.5f);
            changed |= SliderRow("Other rigidity", ref _otherRigidity, 0f, 1f);

            GUILayout.Space(5);
            GUILayout.Label("Live display");
            changed |= SliderRow("Screen brightness", ref _monitorBrightness, 0f, 1f);
            changed |= SliderRow("Screen glow", ref _monitorLightIntensity, 0f, 1f);

            if (GUILayout.Button("Restore defaults", GUILayout.Width(165)))
            {
                _dampingScale = 3f;
                _elasticityScale = 1.5f;
                _stiffnessScale = 2f;
                _inertScale = 1.1f;
                _hipRadiusScale = 0.2f;
                _legRadiusScale = 0.8f;
                _horizontalStiffness = 0.5f;
                _otherDampingScale = 3f;
                _otherElasticityScale = 1.5f;
                _otherStiffnessScale = 2f;
                _otherInertScale = 1.1f;
                _otherRigidity = 0.85f;
                _monitorBrightness = 1f;
                _monitorLightIntensity = 0.25f;
                changed = true;
            }

            if (changed)
                Apply();

            bool showColliders = GUILayout.Toggle(_showSkirtColliders, "Show skirt colliders");
            if (showColliders != _showSkirtColliders)
            {
                _showSkirtColliders = showColliders;
                foreach (SkirtCollisionVisualizer visualizer in Object.FindObjectsOfType<SkirtCollisionVisualizer>(true))
                    visualizer.SetVisible(_showSkirtColliders);
            }

            bool bodyCollisionEnabled = GUILayout.Toggle(_skirtBodyCollisionEnabled, "Enable skirt body collision");
            if (bodyCollisionEnabled != _skirtBodyCollisionEnabled)
            {
                _skirtBodyCollisionEnabled = bodyCollisionEnabled;
                SetSkirtBodyCollisionEnabled(_skirtBodyCollisionEnabled);
            }

            Director director = Director.instance;
            UmaContainerCharacter firstCharacter = director != null &&
                director.CharaContainerScript != null &&
                director.CharaContainerScript.Count > 0
                ? director.CharaContainerScript[0]
                : null;
            SkirtSurfaceCollisionSolver solver = firstCharacter != null
                ? firstCharacter.GetComponent<SkirtSurfaceCollisionSolver>() : null;
            GUILayout.Label(solver != null
                ? "Skirt rings: " + solver.RingCount + "  colliders: " + solver.ColliderCount +
                  "  contacts: " + solver.LastContactCount
                : "Skirt surface solver: not initialized");
            if (firstCharacter != null)
            {
                foreach (DynamicBoneCollider collider in firstCharacter.GetComponentsInChildren<DynamicBoneCollider>(true))
                {
                    if (!IsBodySkirtCollider(collider))
                        continue;

                    GUILayout.Label(collider.ColliderName + "  r=" + collider.m_Radius.ToString("0.000") +
                        " h=" + collider.m_Height.ToString("0.000"));
                }
            }
            GUILayout.Label("F8: close");
            GUILayout.EndArea();
        }

        private static bool SliderRow(string label, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(105));
            float updated = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(165));
            GUILayout.Label(updated.ToString("0.00") + "x", GUILayout.Width(55));
            GUILayout.EndHorizontal();

            bool changed = !Mathf.Approximately(value, updated);
            value = updated;
            return changed;
        }

        private void Apply()
        {
            foreach (ParticleDefaults defaults in _particles)
            {
                DynamicBone.Particle particle = defaults.Particle;
                if (particle == null)
                    continue;

                float categoryDamping = 1f;
                float categoryElasticity = 1f;
                float categoryStiffness = 1f;
                float categoryInert = 1f;
                if (defaults.Category == MotionCategory.Other)
                {
                    categoryDamping = _otherDampingScale;
                    categoryElasticity = _otherElasticityScale;
                    categoryStiffness = _otherStiffnessScale;
                    categoryInert = _otherInertScale;
                }

                particle.m_Damping = Mathf.Clamp01(defaults.Damping * _dampingScale * categoryDamping);
                particle.m_Elasticity = Mathf.Clamp01(defaults.Elasticity * _elasticityScale * categoryElasticity);
                particle.m_Stiffness = Mathf.Clamp01(defaults.Stiffness * _stiffnessScale * categoryStiffness);
                particle.m_Inert = Mathf.Clamp01(defaults.Inert * _inertScale * categoryInert);

                if (defaults.Category == MotionCategory.Other && _otherRigidity > 0f)
                {
                    // Multipliers alone cannot make a low-value source spring rigid.
                    // This is a direct, non-skirt constraint preset: high damping and
                    // stiffness, with limited rebound and body-motion lag.
                    particle.m_Damping = Mathf.Max(particle.m_Damping, Mathf.Lerp(0f, 0.9f, _otherRigidity));
                    particle.m_Stiffness = Mathf.Max(particle.m_Stiffness, Mathf.Lerp(0f, 0.98f, _otherRigidity));
                    particle.m_Elasticity = Mathf.Min(particle.m_Elasticity, Mathf.Lerp(1f, 0.05f, _otherRigidity));
                    particle.m_Inert = Mathf.Min(particle.m_Inert, Mathf.Lerp(1f, 0f, _otherRigidity));
                }
            }

            foreach (ColliderDefaults defaults in _hipColliders)
            {
                if (defaults.Collider != null)
                    defaults.Collider.m_Radius = defaults.Radius * _hipRadiusScale;
            }

            foreach (ColliderDefaults defaults in _legColliders)
            {
                if (defaults.Collider != null)
                    defaults.Collider.m_Radius = defaults.Radius * _legRadiusScale;
            }

            if (Director.instance == null)
                return;

            foreach (UmaContainerCharacter character in Director.instance.CharaContainerScript)
            {
                if (character == null)
                    continue;

                SkirtSurfaceCollisionSolver solver = character.GetComponent<SkirtSurfaceCollisionSolver>();
                if (solver != null)
                    solver.HorizontalStiffness = _horizontalStiffness;
            }

            foreach (StageMonitorDriver monitorDriver in Object.FindObjectsOfType<StageMonitorDriver>(true))
            {
                monitorDriver.SetBrightnessMultiplier(_monitorBrightness);
                monitorDriver.SetMonitorLightIntensity(_monitorLightIntensity);
            }
        }

        private void SetSkirtBodyCollisionEnabled(bool enabled)
        {
            foreach (UmaContainerCharacter character in Director.instance.CharaContainerScript)
            {
                if (character == null)
                    continue;

                foreach (DynamicBoneCollider collider in character.GetComponentsInChildren<DynamicBoneCollider>(true))
                {
                    if (IsBodySkirtCollider(collider))
                        collider.enabled = enabled;
                }

                SkirtSurfaceCollisionSolver solver = character.GetComponent<SkirtSurfaceCollisionSolver>();
                if (solver != null)
                    solver.enabled = enabled;
                character.ResetDynamicBone();
            }
        }

        private static bool IsBodySkirtCollider(DynamicBoneCollider collider)
        {
            if (collider == null || string.IsNullOrEmpty(collider.ColliderName) ||
                collider.ColliderName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (collider.ColliderName.IndexOf("Thigh", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            bool isSideHip = collider.ColliderName.EndsWith("_L", System.StringComparison.OrdinalIgnoreCase) ||
                collider.ColliderName.EndsWith("_R", System.StringComparison.OrdinalIgnoreCase);
            return collider.ColliderName.IndexOf("Hip", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                isSideHip && collider.m_Radius <= 0.25f;
        }

        private static bool IsHipSkirtCollider(DynamicBoneCollider collider)
        {
            return collider != null && collider.ColliderName != null &&
                collider.ColliderName.IndexOf("Hip", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                collider.ColliderName.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                (collider.ColliderName.EndsWith("_L", System.StringComparison.OrdinalIgnoreCase) ||
                 collider.ColliderName.EndsWith("_R", System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLegSkirtCollider(DynamicBoneCollider collider)
        {
            return collider != null && collider.ColliderName != null &&
                collider.ColliderName.IndexOf("Thigh", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                collider.ColliderName.IndexOf("MSkirt", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MotionCategory GetMotionCategory(DynamicBone dynamicBone)
        {
            string name = GetMotionName(dynamicBone);
            return dynamicBone != null && (dynamicBone.m_ContinuousCollision ||
                name.IndexOf("Skirt", System.StringComparison.OrdinalIgnoreCase) >= 0)
                ? MotionCategory.Skirt : MotionCategory.Other;
        }

        private static string GetMotionName(DynamicBone dynamicBone)
        {
            string name = dynamicBone != null && dynamicBone.m_Root != null
                ? dynamicBone.m_Root.name : string.Empty;
            for (Transform transform = dynamicBone != null ? dynamicBone.transform.parent : null;
                 transform != null; transform = transform.parent)
            {
                name += "/" + transform.name;
            }
            return name;
        }

    }
}
