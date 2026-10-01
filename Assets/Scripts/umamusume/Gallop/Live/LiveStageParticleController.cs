using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live
{
    // Stage consumers from master5 Director.OnParticleUpdate/OnParticleGroupUpdate.
    // Native Flash.UpdateParticle is a separate simulator and remains owned by LiveFlashController.
    public sealed class LiveStageParticleController : IDisposable
    {
        private readonly Dictionary<string, List<ParticleSystem>> particles =
            new Dictionary<string, List<ParticleSystem>>(StringComparer.Ordinal);

        public LiveStageParticleController(Transform stageRoot)
        {
            if (stageRoot == null) return;
            foreach (var particle in stageRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                string name = particle.gameObject.name;
                if (!particles.TryGetValue(name, out var list))
                {
                    list = new List<ParticleSystem>();
                    particles.Add(name, list);
                }
                list.Add(particle);
            }
        }

        public void Update(LiveTimelineParticleData group, float emissionRate)
        {
            if (group == null || !particles.TryGetValue(group.name, out var list)) return;
            foreach (var particle in list)
            {
                if (particle == null || !particle.gameObject.activeInHierarchy) continue;
                var emission = particle.emission;
                emission.rateOverTime = emissionRate;
            }
        }

        // Vector2.x = FlickerDarkRate; y = FlickerLightRate, matching master5's MinMaxCurve.
        public void UpdateGroup(LiveTimelineParticleGroupData group, Vector2 emissionRange)
        {
            if (group == null || !particles.TryGetValue(group.name, out var list)) return;
            foreach (var particle in list)
            {
                if (particle == null || !particle.gameObject.activeInHierarchy) continue;
                var emission = particle.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRange.x, emissionRange.y);
            }
        }

        public void Dispose()
        {
            // Stage owns these ParticleSystems; this controller owns only the lookup cache.
            particles.Clear();
        }
    }
}
