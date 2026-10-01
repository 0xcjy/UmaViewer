# Physics performance pass — 2026-09-20

## Scope
User confirmed physics is correct after the master5 runtime profile fix. Preserve its
collider radii, sampling counts, update rate, solver ordering, and positional constraints.
Rendering restoration remains out of scope.

## Observed code costs (not a captured Live CPU profile)
- DynamicBoneCollider.Collide recomputed lossyScale and TransformPoint on every probe.
  Continuous skirt collision tests particle endpoints, three segment probes, and 4–8
  swept probes, plus repeat endpoint tests after correction. Surface samples repeat this.
- Surface solver repeated collider name/radius filtering inside span/panel inner loops.
- The newly migrated Live profile allocated component arrays and predicate closures on
  its 0.5-second discovery pass, and rebuilt motion-category strings even for known particles.

## Changes
- Added lazy world-geometry cache for a single collision batch. Same sphere/capsule
  projection routines are used; no approximation or removed collision checks.
- Each DynamicBone batch ends before ApplyParticlesToTransforms. Surface solver has its
  own batch, ending before pose writes. No cross-frame or cross-chain reuse, since those
  writes may move colliders. This scope is main-thread-only, not a worker-thread API.
- The original uncached path remains available for live A/B comparison through menu
  UmaViewer/Physics/Toggle collision geometry cache. Default is enabled.
- Surface filters colliders once per solve; chain scale read once per chain rather than depth.
- Live profile discovery reuses component lists, removes capturing Find predicates, and
  only computes motion category when discovering new particles.
- Added Live.SkirtSurfaceCollision profiler marker; Live.DynamicBone already existed.

## Executed validation
- Runtime and Editor assemblies compile; existing runtime warnings only.
- Used a separate minimal Unity 2022.3.62f1 batch-mode test project under
  tmp/physics-cache-test-project. Did not close/restart the user's master7 Editor.
- 24,576 deterministic point comparisons passed across sphere/capsule, inside/outside,
  all axes, translated/rotated/nonuniform/negative-scale transforms. Also tested nested
  cache scopes and fresh geometry after motion between batches and outside a batch.
- Synthetic microbenchmark, 1,048,576 queries per path (two measurement orders):
  uncached 792.87 ms; cached 102.02 ms; ratio 7.77x for this collision-query workload.
  NOT a Live frame-time speedup. Full animation, transform writes, surface solve, rendering,
  and real collider sharing are not represented by this synthetic ratio.
- Evidence: tmp/physics-performance-baseline/cache-test-result.txt and cache-test-unity.log.
- Menu to rerun: UmaViewer/Physics/Test collision cache equivalence.

## CySpring native investigation
CySpringNative.cs imports CySpringPlugin, not cyspring.dll. Both binaries are x64 and
export NativeClothUpdate, NativeClothSkirtUpdate, NativeSkirtUpdate.

master7 Assets/Plugins/CySpringPlugin.dll:
  3,838,976 bytes, SHA256 BE84FAF0CC3CCCCEDA8BDBCA23530E0BEB2049BDCB9ADD0FF7E9ECAAAEEB135F
Reference UmaViewer_Data/Plugins/x86_64/CySpringPlugin.dll:
  3,869,184 bytes, SHA256 C4ADA7CDD8A80D268601715AEBB52CA0A2A235FD3CE189459B30AAE598269D2A

DLL existence/export names do not prove a successful runtime invocation or correct ABI.
Different hashes do not prove incompatibility. Old skirt clipping has NOT been attributed
to a specific DLL failure. Do not replace DLLs or re-enable the old backend without tracing
binding, structure layouts/strides, collision flags/indices, and skirt linking. The current
working solver does not call this native backend.
Native export evidence: tmp/physics-performance-baseline/native-exports.json.

## Next decision: native kernel vs further managed optimization
First compare the same Live, outfit, camera and time range with geometry cache off/on.
Capture Live.DynamicBone and Live.SkirtSurfaceCollision, whole CPU frame time, allocations,
and spikes; distinguish physics-bound from rendering-bound frames.
If physics remains expensive, proposed native design is a batched flat-array solver, not
one P/Invoke per particle/collider: gather geometry/poses on main thread, solve a batch,
write poses back once. Port the current verified constraints including continuous and
surface collision; retain managed fallback and compare particle results. Independent
characters are the first candidate parallel units; interacting skirt chains cannot simply
be parallelized without respecting their constraint dependencies.
No native DLL has been built or installed in this pass. No actual Live FPS gain is claimed.

## Backups
Pre-optimization DynamicBone.cs, DynamicBoneCollider.cs, SkirtSurfaceCollisionSolver.cs
are under tmp/physics-performance-baseline. Earlier master5 migration byte-identical
claims refer to their state before this optimization pass.
