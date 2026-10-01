# master5 physics migration — 2026-09-20

User prioritized physics over render restoration; render goal paused.

## Migrated
- DynamicBone.cs copied byte-for-byte from master5: includes continuous segment collision and particle access used by skirt solver.
- SkirtSurfaceCollisionSolver, SkirtLegCollisionProxy, SkirtCollisionVisualizer copied byte-for-byte, with original metas.
- master5 CySpringDataContainer dynamic-physics methods merged into master7's existing data component. Kept its GUID, serialized fields, and compatibility methods required by remaining CySpring classes.
- CySpringCollisionData serialized fields exposed as in master5; added default constructor. Kept existing runtime APIs and GUID.
- UmaContainerCharacter.LoadPhysics uses master5 collider binding, DynamicBone setup and horizontal skirt surface solver. Removed old CySpring owner/setup helpers and Begin/EndSimulation calls; LoadBody no longer creates SkirtController. Only one backend drives bones on newly loaded characters.
- Master5 winner-outfit school-uniform thigh/hip collider fallback added to UmaViewerBuilder; other model-loading/render changes preserved.
- Physics enable/reset now use DynamicBone. Physics toggle also disables the added surface solver so it cannot reapply particle poses while physics is off.

## Validation
- Runtime assembly Roslyn compile passes, existing warnings only.
- Editor assembly compile passes against updated runtime DLL. Also fixed missing Gallop import in previously written render-capture helper, which otherwise blocked Editor compilation.
- Four copied solver files verified byte-identical to master5.
- Live visual validation NOT claimed: user's existing Unity Editor is playing an already-loaded Live. Do not terminate it or start another editor against the same project. Existing runtime components need recreation, not just domain reload.
- Added UmaViewer/Physics/Validate master5 migration menu. Reports DynamicBone count, surface rings, thigh colliders and presence of old CySpring/Skirt controllers; warns if character must be reloaded.

## Reload requirement
Stop Play Mode and start/load the character or Live again. The old runtime CySpring components cannot be safely converted by a script reload alone.

## Backups
Overwritten files preserved under tmp/physics-migration-backup, same relative paths. No entire master5 character/builder file was copied.

## Follow-up: oversized collision volumes (2026-09-20)
The first migration omitted master5 Live/SkirtCollisionRuntimeTuner.cs. Its Update/Apply
changes the authored collider radii after creation, so copying the solver alone was incomplete.

Added physics-only Master5LivePhysicsProfile, attached by ConfigureLivePhysics:
- Hip + Skirt + suffix _L/_R: original radius * 0.2 (case insensitive).
- Thigh + MSkirt: original radius * 0.8. Other colliders unchanged.
- Capsule height/center are NOT changed, matching master5 Apply exactly.
- Skirt particle damping/elasticity/stiffness/inertia multipliers: 3 / 1.5 / 2 / 1.1.
- Other particles also receive master5's additional multipliers and 0.85 rigidity clamp.
- Surface horizontal stiffness: 0.5.
- Scoped to Live, as in master5; no rendering controls imported.
- Original collider radii are stored on the character component and serialized for domain
  reloads. Repeated application uses original radii, not already-scaled values. Particle
  instances are tracked to prevent repeated tuning; newly rebuilt particles are discovered.

Validation: runtime and editor Roslyn compilation passed (runtime has existing warnings).
Added UmaViewer/Physics/Test master5 Live profile for collider name filters, five repeated
applications, unaffected collider/height, particle coefficients, and particle rebuild.
This menu test has NOT yet been executed in the user's running Unity Editor; compile is
not a runtime or visual pass. Validate master5 migration now reports missing profiles and
actual local radii, height, transform scale, and expected multipliers.
Reload Live after Unity imports scripts. Existing characters were created before this hook.
Visual skirt/body collision parity still requires replaying the affected outfit/motion.

## Performance follow-up
User confirmed physics fixed. The solver files now contain a bounded geometry-cache optimization; see PHYSICS_PERFORMANCE.md for changes, executed equivalence tests, and benchmark limits.
