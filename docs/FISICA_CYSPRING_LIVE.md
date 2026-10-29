# CySpring physics in the Live (hair / cloth / skirt / tail)

> Working document so we don't repeat attempts that were already tried. One block per attempt, in
> chronological order. Anything marked as **measured** was checked in Play Mode with the MCP for
> Unity; anything marked as **unverified** must not be taken as good.

---

## Attempt 1 — 2026-10-29 (start of the session)

**Problem description**
The viewer's physics was a pile of workarounds (DynamicBone + invented colliders and radii) that did
not reproduce the game. Goal: use only the game's real engine (`CySpringPlugin.dll`) with the data
from the assets, without a layer of our own.

**Proposed solution**
Remove the DynamicBone path from the runtime entirely and leave `CySpringDataContainer` as a passive
data container, with `CySpringController` as the sole owner of the simulation.

**Files edited**
- `Assets/Scripts/UmaContainerCharacter.cs` (~39 hack methods removed, ~2,900 lines;
  `LoadPhysics`, `ReloadClothPhysics`, `SetDynamicBoneEnable`, `ResetDynamicBone`,
  `ConfigureLivePhysics` rewritten on top of `_cySpringController`).
- `Assets/Scripts/umamusume/Gallop/Cyspring/CySpringDataContainer.cs` (−482 lines of the DynamicBone
  path).
- `Assets/Scripts/umamusume/Gallop/Cyspring/Cyspring.cs` (diagnostics cleanup).
- Removed: `Assets/Scripts/CySpringTestCollider.cs`, `CySpringHairProbeVisualizer.cs` (+ `.meta`).

**Did it solve the problem**
**Partially.** Measured in live 1001 (18 characters): 0 without a controller, 0 without chains;
hair 10–15 roots / 28–44 bones / 15–23 colliders, cloth 12–26 / 47–55 / 20–24, tail 1 / 5 / 2;
0 console errors. But **visually the result is still wrong** (see attempt 5).

**New problems introduced**
- The public names `SetDynamicBoneEnable` / `ResetDynamicBone` are kept on purpose (the
  `Version2.unity` scene has them wired by name, and `UISettingsModel` and the exporter use them):
  they are misleading names, but necessary.
- `PropagateClothCollisionNames` (skirt) is kept: it is a leftover from the old path, it is not in
  the game.

---

## Attempt 2 — 2026-10-29 (phase 2: environment collisions)

**Problem description**
Characters go through the floor/set pieces with hair, cloth and tail: the cutt's environment
collision track was missing (`CharaCollisionDataList`, key type `CharaCollision`).

**Proposed solution**
Port the official timeline path (not the `StageController` route, which is dead code: its getter has
no call site in the dump):
- collider class `CySpringCollisionComponent` (namespace `Gallop`),
- key/group `LiveTimelineCharaCollisionData` + field in the worksheet,
- `AlterUpdate_CharaCollision` / `UpdateCharaCollisionModelData` /
  `UpdateCharaCollisionModel` logic in `LiveTimelineControl`,
- accessors `ApplyEnvCySpringCollisionTo{Head,Body,Tail}` in `UmaContainerCharacter`.

**Files edited**
- **New** `Assets/Scripts/umamusume/Gallop/Cyspring/CySpringCollisionComponent.cs`.
- **New** `Assets/Scripts/umamusume/Gallop/Live/Cutt/LiveTimelineDataList/LiveTimelineCharaCollisionData.cs`.
- `Assets/Scripts/umamusume/Gallop/Cyspring/CySpringCollisionRuntimeData.cs`
  (`SetDefaultRadius` / `SetDefaultOffset` / `SetDefaultOffset2`).
- `Assets/Scripts/umamusume/Gallop/Live/Cutt/LiveTimeLine/LiveTimelineControl.cs` (+271 lines).
- `Assets/Scripts/umamusume/Gallop/Live/Cutt/LiveTimelineWorkSheet.cs` (`CharaCollisionDataList` field).
- `Assets/Scripts/UmaContainerCharacter.cs` (accessors).

**Did it solve the problem**
**No (in practice nothing was applied).** It compiles with 0 errors and the data exists, but it was
discovered later that the distribution to the characters found nobody (see attempt 4).

**New problems introduced**
- **Possible regression in live 1081**: it is the only one of the tested lives with an environment
  track, and in it the **tail lifts** (planes at y≈1.10/1.15 pushing upwards). Not confirmed whether
  it is the game's data or a wrong anchor (see attempt 5).

---

## Attempt 3 — 2026-10-29 (ParentWork was not being refreshed)

**Problem description**
In the game, `CySpringController.ApplyEnvCySpringCollisonTo{Head,Body,Tail}` does **two** things:
`CySpring.ApplyEnvCollision(...)` and then `CySpringController.SetupSpringParentWork(this)`.
The viewer only did the first one. When the native array is rebuilt, `InitNativeCollisions` leaves
`ParentWorkIndex = 0` on **every** collision → the engine resolves against the wrong parent
(pushes that don't exist and collisions that don't brake).

**Proposed solution**
Append `SetupSpringParentWork()` to the end of `CySpringController.ApplyEnvCollision(Parts, array)`
and keep the head array in `_envColArray` (same as the game).

**Files edited**
- `Assets/Scripts/umamusume/Gallop/Cyspring/CySpringController.cs`.

**Did it solve the problem**
**Unverified** (at that time the distribution to characters still did not work, attempt 4).

**New problems introduced**
- None known. Cost: `SetupSpringParentWork` runs every time the environment track's keyframe changes
  (not every frame), so the cost is acceptable.

---

## Attempt 4 — 2026-10-29 (character ↔ slot map)

**Problem description**
`UpdateCharaCollisionModel` looked for the character with
`director.charaObjs[i].GetComponentInChildren<UmaContainerCharacter>(true)`, but the
`UmaContainerCharacter`s **do not hang from the slot**: they are root objects (`Chara_XXXX_00`,
`parent=null`). Result: no environment collision was ever applied.

**Proposed solution**
Use `Director.CharaContainerScript[i]`, which is the list the rest of the live uses to index by
position (facial, lipsync), and which `Director.SetupCharacterLocator` maps to the same index.

**Files edited**
- `Assets/Scripts/umamusume/Gallop/Live/Cutt/LiveTimeLine/LiveTimelineControl.cs`.

**Did it solve the problem**
**Not verified in a live** (pending the test with 1001 + character 1013).

**New problems introduced**
- If the live leaves gaps between slots, `CharaContainerScript` is compacted on load and the index
  may not match the bit in `LiveCharaPositionFlag`. This is a **pre-existing** limitation of the
  viewer (it affects facial and lipsync the same way), not a new one.

---

## Attempt 5 — 2026-10-29 16:51 (diagnosis: anchor and engine space) — **no code changes**

**Problem description**
The user's real symptoms (live, characters animating): hair goes through **cloth, arms, shoulders and
neck**; when turning in place it barely moves, but **when walking/moving it gets exaggerated and
"rises"**.

**Measurements (live 1001 and 1081, Play Mode)**
```
container (anchor used by the engine) = (0.000, -0.965,  0.000)   ← _ownerTransform / _previousPos
actual rig (node "Position")          = (0.000,  0.235, -2.866)

bone (world)       = (0.057, 1.487, 4.119)
engine SelfPosition = (0.038, 1.539, 0.041)     ← 4 m off
```

bone↔collider penetration (only its own indices): 3–13 cm
chain stretching (distance / InitBoneDistance): up to 28 %

The live moves the rig with the `Position` node, but `CySpringController` anchors on the container.
All the native computation (collision positions, `Distance`/`Normal` of the environment plane,
displacement forces) ends up referenced to an origin that is not the character's.

**Ruled out with data (do not repeat these hypotheses)**
- Native structs: `NativeClothCollision` (0x4C), `NativeClothWorking` (0x168) and
  `NativeRootParentWork` (0x20) **match** `tools\cyspring-datamine\docs\02-abi-y-estructuras.md`.
  `NativeRootParentWork` is indexed with `ParentWorkIndex * 0x20` (**no** +1).
- The `ParentWork`s **are** refreshed every frame and match the bone to the centimetre once the
  character is already placed (the 3.4 m offset seen at the beginning was a startup transient).
- There is no double driver (DynamicBone removed), nor `CySpringUpdater` in the scene.
- `_autoUpdateInLateUpdate = false` is correct: the simulation is triggered by
  `UmaContainerCharacter.AlterLateUpdateRuntime` (one step per frame).
- `CurrentCharaPosition` follows the container (which is **not** the rig).
- Hair **only** declares head/torso colliders → going through the skirt is the game's behaviour, not
  a bug.

**Proposed solution (pending application)**
Anchor the controller on the **rig root** instead of the container, in `LoadPhysics` and
`ReloadClothPhysics`:
```csharp
Transform rigRoot = transform.Find("Position");   // fallback: transform
_cySpringController.SetOwner(rigRoot.gameObject, hip, _cySpringOwner);
```
Verification: in 1001 with character 1013, the first bone's `SelfPosition` must match its world
position (±1 cm) and penetration must drop from 3–13 cm to ~0. Then repeat on 1081 for the tail.

**Files edited**
- None (diagnosis phase).

**Did it solve the problem**
Diagnosis: **identifies the cause** (anchor/space), it does not fix it yet.

**New problems introduced**
- None.

---

## Attempt 6 — 2026-10-29 17:10 (anchor the solver to the rig root)

**Problem description**
The engine works with the owner placed at the ORIGIN during the simulation
(`UpdatePrevousPosture` sets `_ownerTransform.position = Vector3.zero` and `EndSimulation` restores
it). If the owner is the container `Chara_XXXX_00`, the rig (child, ~2.9 m away) does **not** end up
at the origin and all the native computation (collisions, planes, displacement forces) is done in a
displaced space.

**Proposed solution**
Anchor on the rig root (`Position`) and, since that node is created by the loader **after**
`LoadPhysics`, resolve it lazily in `AlterLateUpdateRuntime` (once). While it does not exist, the
fallback is the `Hip`, which is also self-consistent (the same transform from which `_previousPos` is
taken is zeroed).

**Files edited**
- `Assets/Scripts/UmaContainerCharacter.cs`:
  - `BindCySpringOwnerAnchor(controller, hip)` (new) and the call after creating/retrieving the
    controller in `LoadPhysics` and `ReloadClothPhysics`.
  - fields `_cySpringAnchorResolved` / `_cySpringHip`.
  - deferred re-bind at the start of `AlterLateUpdateRuntime`.

**Did it solve the problem**
**Partially / not confirmed visually.** Verified in live 1001: `owner=Position` on every character
(previously `owner=Hip` or the container). `DynamicRatio = 1` (pure physics), `IsSkip = 0`,
`FinalRotation` = rotation written to the bone (the solver does command). Penetration measured against
real colliders (radius ≤ 0.35): there are still 6–20 cm in some strands.

**New problems introduced**
- None detected (0 compilation errors; the 5 console `NullReferenceException`s are pre-existing).

### Corrections to earlier interpretations (do not repeat them!)
- The **3.3–5.8 m offset** between `_rootParentWork` and the bone **is NOT a bug**: it is the effect
  of putting the owner at the origin during the simulation step. Measuring `pw` vs `pt` outside the
  frame tells you nothing.
- The **"28–68 % chain stretching"** was a BADLY taken metric: `InitBoneDistance` is stored **on the
  parent bone** (`parentNative.InitBoneDistance = distance(parent→child)`), and the last bone **copies**
  the parent's value. Comparing `arr[b].InitBoneDistance` with `dist(b-1, b)` measures something else.
- `DynamicRatio = 1`, `MoveSpringApplyRate` and `Stiffness/Drag/Gravity` arrive correctly from the
  asset.

---

## Attempt 7 — 2026-10-29 17:40 (is the rest pose captured before animating?)

**Problem description**
With **Enable Physics off** the hair is still wrong (it only moves a little). Measured on
`Chara_1013_02` with the scene stopped: two samples 110 frames apart give **the same** rotation on
the hair bone and on the hip, and its `Animation` is `enabled=False / playing=False`.
That is: it is not only the solver; the starting pose is already wrong.

**Proposed solution (hypothesis)**
The viewer captures the spring's rest pose when **instantiating** the character (`InitLocalRotation`,
`BoneAxis`, `FirstLocalPosition`, `FirstPosition`, `InitBoneDistance`). In the live the first animated
pose arrives later, so the spring would chase the prefab's pose forever.

Test: hot re-bind with the live already animating (`ReloadClothPhysics()` on the 18 characters, live
1001, t≈4.7 s).

**Files edited**
- None (Play Mode test).

**Did it solve the problem**
**Pending visual confirmation** (0 console errors after the re-bind).
If it fixes it: the definitive fix is a **lazy bind** (capture the rest pose on the character's first
already-animated frame, not on instantiate).

**New problems introduced**
- None detected.

---

## Attempt 8 — 2026-10-29 18:00 (lazy re-bind) — **REVERTED by default at 18:30**

**Problem description**
Attempt 7 could only be tested hot, and that **is not good enough**: the live is loaded by the user
with character 1013, so any change made at runtime does not exist in their session. It has to be
implemented as code so they can test it.

**Proposed solution**
Re-bind the physics **once**, as soon as the live has posed the character (`_liveCurrentTime > 0.2 s`
and `_isLiveSetup`), reusing `ReloadClothPhysics()`.

**Files edited**
- `Assets/Scripts/UmaContainerCharacter.cs`:
  - `public bool CySpringRebindOnAnimatedPose` (now **false** by default),
  - `private bool _cySpringPoseRebound;`, `IsAnimatedPoseReadyForCySpring()`,
  - re-bind block in `AlterLateUpdateRuntime`.

**Did it solve the problem**
**NO: it swapped one symptom for another.** Measured with the user watching:
- Teeth: the back hair stops going through the arm (apparent improvement).
- Cost: the **tail** goes from pointing down (`dir≈(0,-0.82,-0.58)`) to pointing **up**
  (`dir=(0,1,0)`), because the rest pose is captured on a random frame of the song (t≈0.2 s) instead
  of the model's neutral pose.
- The captured rest defines the direction the solver chases (`dir == aim`), so it "pushes" the hair
  away from the arm: the improvement was an artefact, not a correction.

**New problems introduced**
- Tail rotated upwards (regression). **Disabled by default** to go back to the previous state.

### Lesson (important)
The rest pose must be captured in the model's **neutral pose** (on load), not on an animation frame.
If the lazy bind is ever revisited, it must look for a neutral frame, not a fixed time.

---

## Attempt 9 — 2026-10-29 18:50 (deep verification + tools)

**Problem description**
Two doubts remained: (1) are the colliders scaled per character (the user's hypothesis)? (2) does the
live's animation move the hair bones? And there was no way to compare the SAME frame with and without
physics without being able to pose the character by hand in `Version2`.

**Verifications (all measured in Play Mode, character 1013_02)**
| Check | Result |
| --- | --- |
| Per-character collider scaling | **It does exist** in the game: `ratio = lossyScale.y / legacyScale` (legacyScale = BodyScale); `radius = default * ratio`, `offset = default * ratio`, `localPos = default / legacyScale`. In 1013 `ratio = 1.0000` → not the cause |
| collider `localPos` | **Real discrepancy**: it stays at the raw offset (0.7400) instead of `offset/BodyScale` (0.7482). ~1 % here; it would matter with BodyScale ≪ 1 |
| Collider lists per bone (asset vs runtime) | **Identical 1:1**. `Hair4_*` only carries `Col_Arm_L_Hair` + `Col_Chest_Hair_B` → no elbow **by data** (same as the game) |
| Capsules (arm/elbow) | Well formed (0.16–0.25 m, valid `pwi`) |
| `BoneAxis` / `AimVector` | **Correct**: `aim(b) == deltaRest(b+1)` exactly ("aim → child" convention) |
| Chain lengths | Exact (`dist / InitBoneDistance = 1.00`) |
| Mesh ↔ bones | Same objects (`matchedSpring = 31`) |
| `ParentWorkIndex` / `ParentRotation` | Correct (`Head` / `Tail_Ctrl`) |
| Does the animation move the hair? | **Yes**: `AnimationRotation` of `Sp_He_Hair4_C_00` changes with the live's time |

**Conclusion**: collision, geometry, axes, scaling and data are faithful to the game. What still needs
to be discriminated is whether the look (hair "floating") comes from the rest pose or from the solver,
and that requires comparing the same frame with and without physics.

**Tools added**
- `Assets/Editor/PoseSnapshotTool.cs` (LOCAL, not committed): `Tools > UmaViewer > 1. Guardar pose
  de huesos (live)` writes `Poses/<character>_pose.json` with the `localRotation` of every bone
  (with an index chain to resolve them later); `2. Aplicar pose de huesos (Version2)` re-applies it
  on the character loaded in Edit Mode (where CySpring does not simulate).
- `Assets/Scripts/Settings/UISettingsModel.cs`: eye tracking now starts **disabled**
  (`_enableEyeTracking = false` + an `Awake` that forces the toggle).

**Did it solve the problem**
Tools: yes (compiles with 0 errors). The diagnosis: it narrows the problem down, it does not close it.

**New problems introduced**
- None known.

---

## Attempt 10 — 2026-10-29 19:20 (**reading the culprit**: the bind was done with a half-built model)

**Problem description**
Hair floated with physics on and the physics ON/OFF difference was minimal.

**Measurement (scene loaded, physics active)**
```
                            dist(actual, rest)   dist(rest, head)   dist(actual, head)
Sp_He_Hair2_L_00               1.225 m                1.202 m              0.120 m
Sp_He_Hair4_C_00               1.304 m                1.219 m              0.106 m
Sp_Ch_Bust0_L_00 (body)        0.139 m                0.192 m              0.154 m
```
The **body** bones had moved 0.14 m (how much the character moved) and the **hair** bones
**1.2–1.3 m**: the rest pose had been captured when the hair was **not yet mounted on the head** (in
`LoadPhysics` the `Position` node does not exist yet). The solver dragged the hair towards that
impossible "rest" (1.2 m from the head) → floating.

**Proposed solution**
Postpone the bind: if the `Position` node does not exist when `LoadPhysics()` is called, set
`_cySpringBindPending` and bind on the first `AlterLateUpdateRuntime` where the rig is complete.

**Files edited**
- `Assets/Scripts/UmaContainerCharacter.cs` (`LoadPhysics` + deferred bind block in
  `AlterLateUpdateRuntime` + `_cySpringBindPending` field).

**Did it solve the problem**
**YES (confirmed by the user)**: "now it looks MUCH better in Version2 and in the live". Tail fine,
strands fine, back hair with minimal overlap with the arm.

**New problems introduced**
- None known.

---

## Attempt 11 — 2026-10-29 19:35 (same cause in the skirt)

**Problem description**
The skirt/shorts lifts and "floats" on some movements.

**Cause**: `SkirtController` (the skirt's own solver) did its `Create(skirtParam)` during the body
load, with the same problem: if the model is not mounted, it captures the rest pose half-built.

**Proposed solution**
If `Position` does not exist in `LoadSkirtController`, store the `SkirtParamDataAsset` in
`_pendingSkirtParam` and create it in the deferred bind (`CreatePendingSkirtController()` right
before `LoadPhysics()`).

**Files edited**
- `Assets/Scripts/UmaContainerCharacter.cs` (`LoadSkirtController`, `CreatePendingSkirtController`,
  `_pendingSkirtParam` field, call in the deferred block).

**Did it solve the problem**
**Pending verification by the user.**

**New problems introduced**
- None known.

---

## Attempt 12 — 2026-10-29 20:10 (solver cadence)

**Problem**: the hair looked "light"/exaggerated; it was suspected that the solver ran at 30 Hz.
**Fact**: `NativeClothUpdate` is calibrated for fixed steps (`GetTargetCySpringFPS` = 30, or 60 with
`_targetCySpringFpsMode == 1`). `AlterLateUpdateRuntime` was changed to a **fixed step** with an
accumulator (60 Hz, max 4 steps/frame) instead of feeding it every render frame (120 fps).
**Result**: **no perceptible difference** (the mode was already 60 Hz and the project already ran at
~60 effective frames). It is kept because it is the correct thing to do, but it was not the hair's
problem.

## Attempt 13 — 2026-10-29 20:20 (EyeTrack off)

User request: `Canvas/UI/Panels/Settings/.../ModelSettings/EyeTrack` must start disabled.
`UISettingsModel._enableEyeTracking = false` + an `Awake()` that forces `EnableEyeTracking = false`
(in case the scene had it ticked). New characters take the value on load
(`UmaContainerCharacter:1952`). **Done and compiled**.

## Attempt 14 — 2026-10-29 20:35 (deferred bind: HAIR SOLVED)

**Problem**: hair and side tails floated; measured: hair bones **1.2 m** away from the head in the
rest pose (body bones only 0.14 m). The rest pose was captured when the model was **not mounted**
yet.
**Done**: `LoadPhysics()` is postponed (`_cySpringBindPending`) until `Position` exists **and** the
number of `CySpringDataContainer`s is stable for **2 frames** (`IsModelReadyForCySpringBind`).
**Result**: **SOLVED** (confirmed by the user: "much better in Version2 and in the live"; tail and
strands fine, minimal overlap with the arm).

## Attempt 15 — 2026-10-29 21:00 (skirt: LEGS SOLVED, tilt still open)

**Symptom**: the skirt tilts (the corner pointing at the centre) and the legs went through it.

Steps and results (all measured/verified with the user):
1. **Wind** as the cause → **RULED OUT**: with `EnableWind=false`, `hasWindArray=[F,F,F]` and
   `windControlData=[0,0,0]` the skirt still tilted. (`CySpringEnableWind` is left as false by
   default: the viewer does not apply the live's `charaWind` track, so the asset's wind would blow
   constantly; implementing that track is pending.)
2. **`SkirtController._center` was NULL** → `NativeArg.CenterPos` stayed at `(0,0,0)` (the world
   origin). Fixed: `"Hip"`/`"Waist"` added to the name list + fallback to `RootPos`.
   Verified: `_center=Hip` on all of them.
3. **Rotation axis**: `UpdateSkirt` did `Rotate(RotAxis, Evaluation, Space.Self)`. Decompiled from the
   game: `Transform.Rotate(static axis, Evaluation, Space.World)`. Corrected to **`Space.World`**
   (+ a guard in case the axis came out degenerate: `Transform.Rotate` with a zero axis threw
   `ArgumentException` and **aborted the whole loop**, leaving the legs going through).
   → **Legs SOLVED** (confirmed).
4. **Capturing the `Init*` only once at bind** (my "double rotation" hypothesis) → **WRONG and
   reverted**: the decompiled `_UpdateSkirtArg` recalculates
   `SkirtInitChildPos`/`SkirtInitNormal`/`RotationAxis` **every frame**.
5. **Offsetting `CenterPos` by +5 m in X** (the user's test to see whether the tilt followed that
   value) → **no change** ⇒ `CenterPos` does not cause the tilt (it only affects the attenuation).

**Open**: the skirt's tilt on non-central slots.

## Attempt 16 — 2026-10-29 21:30 (final rule-outs)

1. **Position/rotation at bind time**: physics **and** skirt were bound with the character at the
   centre and unrotated (like slot 1) and then put back in place → **no change** ⇒ it is not the load.
   (It did not take effect at first because of a bug of mine: the skirt was created BEFORE the
   forcing; the order was fixed and re-tested.)
2. **Duplicate names** (every character is called the same; do they resolve to the first one?):
   **RULED OUT** by instanceID in the collisions (all from the character itself) and in the skirt
   data (see 3).
3. **Objects shared between characters**: `_skirtDataArray`, `NativeWorkingArray`, `_center`,
   `CachedTransform` and the `SkirtRoot` bones have **different instanceIDs** per character; only
   `_paramDataAsset` is shared (correct, it is read-only). **RULED OUT**.
4. **`Evaluation`**: reasonable values (5–26°) with `OffsetAngle=3.4` ⇒ the solver computes a normal
   angle; the problem is in the data/pose it is applied to.

**Next proposed step (not done)**: instrument per frame, for a centre character and a lateral one,
`Evaluation`, `OffsetAngle`, `RotationAxis` and the resulting `localRotation` of the skirt root;
compare on the SAME frame.

---

## Attempt 17 — 2026-10-29 22:20 (complete solver map + switchboard)

**New user data (key)**: the live's pose saved with `Tools > 1. Guardar pose` and applied in
`Version2` **with physics OFF (option 3)** reproduces the inflated/tilted skirt; applied **with
physics ON (option 2)** the skirt looks right again. And disabling physics **in the live** changed
nothing (not confirmed that it was really disabled).

**Finding A (corrects attempt 15.3)**: `SkirtController.UpdateSkirt()` is **never called** in the
viewer (its only call site is `CySpringUpdater`, which is not in any scene/prefab; checked by GUID and
by `m_MethodName`). **It is not a defect**: `IsUpdateSelf()` returns `!_isEnableCySpringFunc()` and
that func is `() => _isPlaying`, so in the official game that method is the **fallback path for when
CySpring is off**. With physics on it does nothing ⇒ the `Space.Self → Space.World` change of 15.3
could not change anything, and the "legs SOLVED" of that moment must be considered **inconclusive**.

**Finding B (main candidate cause)**: the native skirt plane is placed with
`SkirtNormalPos = SkirtRootPos − arg.RootPos` and compared against the step's bone positions
(`CySpring_SolveBone`). All those positions are in the space of the CySpring **owner**, which
`UpdatePrevousPosture` leaves at the origin; but `NativeArg.RootPos` is read from
`CachedTransform.position` = **the container** (`Chara_XXXX_00`), which is **not** zeroed. The plane
ends up displaced ~1 m in a fixed world direction ⇒ the whole chain pushed (skirt lifts) and, since
the normal is per panel, **one corner points at the centre**; and in `Version2` (container at the
origin) it works. It matches every symptom and the pose test.

**Finding C (method)**: `_UpdateSkirtArg()` runs on **every step** and rewrites `RootPos`,
`CenterPos`, knees and ankles ⇒ **patching `NativeArg` at runtime does not work** (it never reaches
the native). That explains why the "+5 m on `CenterPos`" of attempt 15.5 changed nothing. And `Eval`
subtracts `RootPos` from everything, so the **angle** comes out right even when the **plane point** is
wrong (that is why 5–26° were normal).

**Tool added**: `Assets/Scripts/Diagnostics/SkirtSolverDiag.cs` (LOCAL, not committed): a component
with 15 switches that reproduce/disable each step of the skirt solver (native arguments, collisions,
write-back to the bone, bind) + `ContextMenu → Dump del plano de falda` with `RootPos`, container vs
`Position` and, per panel, `SkirtRootPos`, `diff`, normal, axis, `IsCheck*`, `Evaluation`,
`OffsetAngle`. Instruction-by-instruction map in **`docs/FALDA_SOLVER_MAPA_COMPLETO.md`**.

**First switch to test**: `A1 rootPosMode = Cero`.

**Files edited**: `SkirtController.cs`, `UmaContainerCharacter.cs`, `Cyspring.cs`, new
`Assets/Scripts/Diagnostics/SkirtSolverDiag.cs`.

**Did it solve the problem**: pending verification by the user.

**New problems introduced**: none known (with every switch ON the behaviour is the usual one).
⚠️ It is a **temporary** tool: it is removed once the problem is closed.

---

## Attempt 18 — 2026-10-29 23:05 (skirt: SOLVED — the plane's anchor was the container)

**User confirmation (earlier)**: the live's pose applied in `Version2` with physics OFF shows the
defect; with physics ON it looks right. That is: the bad skirt **comes out of the live's solver**, not
out of the animation.

**Cause**: `NativeArg.RootPos` was read from the **container** (`Chara_XXXX_00`) while everything else
in the step (bone positions, `SkirtRootPos`, `CenterPos`, knees, ankles) is in the space of the
CySpring **owner** (`Position`), which `UpdatePrevousPosture` leaves at `(0,0,0)`. The native places
the plane with `SkirtNormalPos = SkirtRootPos − RootPos` and compares it against the bones'
`SelfPosition` ⇒ the plane came out displaced by **exactly** the character's position.

**Measured** (live 1001, 18 characters, same frame):

| character | container | `RootPos` | `SkirtNormalPos` | `w.SkirtRootPos` | DELTA |
| --- | --- | --- | --- | --- | --- |
| 1087 | (8.00, 0, 0.08) | same | (−8.02, 1.00, −0.15) | (−0.02, 1.00, −0.07) | **8.000 m** |
| 1006 | (−7.00, …) | same | (7.01, 0.99, …) | (0.01, …) | **7.000 m** |
| 1011 | (4.80, …) | same | (−4.82, …) | (−0.02, …) | **4.801 m** |
| 1026 | (0.70, …) | same | (−0.72, …) | (−0.02, …) | **0.705 m** |
| 1090/1096/1023 | (0.00, −0.09, 0) | same | (~0, 1.09, …) | (~0, 1.00, …) | **0.087 m** |

And the **per-panel push** for the character at x=8 (what was visible), with the old mode:

| panel | normal | `dmin` | push |
| --- | --- | --- | --- |
| F, FL, FR, FLL, FRR, R, MSkirt1_L | x > 0 | +0.7 … +9.0 | **none** |
| **L** | (−0.76, 0.09, 0.65) | −4.01 | **4.03 m** |
| **BL** | (−1.00, −0.05, −0.05) | −8.45 | **8.47 m** |
| **BR** | (0.03, 0.27, −0.96) | −3.35 | **3.37 m** |
| **B** | (−0.68, 0.10, −0.73) | −8.28 | **8.30 m** |

That is: **half of the skirt (the half facing the stage centre) received pushes measured in metres and
the other half received none** — the "corner pointing at the centre" that was lifting. The further
from the centre the slot, the worse; and the centre (9 cm) looked fine.

With the correct `RootPos`, the SAME measurement gives `dmin ≈ 0.00` on all 11 panels and pushes of
**0.02–0.03 m** (the bone's radius): the plane touches where it should.

**Fix applied (code)**
- `SkirtController._ResolveRootPos()` → by default the **owner's anchor** (`_rootAnchor`), and if
  there is no anchor, `Position`, and if not, `(0,0,0)`. **Never** the container.
- `SkirtController.SetRootAnchor(Transform)` + `_rootAnchor` field.
- `UmaContainerCharacter.BindCySpringOwnerAnchor(...)` → `_skirtController.SetRootAnchor(rigRoot)`,
  the same node that is passed to `SetOwner` (so the skirt's anchor and the engine's cannot diverge).

**Verified in Play Mode, compiled code (0 errors)**: live 1001, 17 characters with a skirt, all
`DELTA = 0.000`, **0 panels out of place**. Pending the user's confirmation with other
characters/costumes.

**New problems introduced**: none known.

---

## Attempt 19 — 2026-10-29 23:50 (1081: environment collisions — SOLVED, two bugs)

**Symptom**: in live **1081** (the first one tested with the `CharaCollision` track) hair and tail
exploded and the skirt flared. User's key observation: turning the env collisions off fixed
**hair and tail**, and the skirt was broken only in **some** characters (the tail was up in exactly
the same ones).

**Bug 1 — where the plane is placed** (`CySpringCollisionRuntimeData.UpdateNativeCollision`, `Plane`):
`Distance` was computed from `rootParentWorkArray[ParentWorkIndex].WorldPosition`, i.e. from a
**character bone**, which is only right for a plane hanging from a bone. The cutt's env planes are
**stage objects** (`IsWorld = true`, parented to the Director). Measured: the authored floor at
y = −0.27/−0.11 reached the native as **Distance = 1.32** (the chest bone), and with `Normal = +Y`
that pushes everything below 1.32 m upwards. Fix: for `IsWorld`, use the collider's own world
position, minus `characterPosition` (the owner, which is 0 during the step).

**Bug 2 — when it is refreshed** (`CySpring.GatherSpring`): env collisions were only updated when
`isUpdateScale` was true, so `Distance` stayed frozen from the moment the native array was rebuilt,
with `characterPosition = 0`. That is why it only broke **some** characters: the plane is a
half-space in the step's space, so any character whose owner is not at y≈0 gets it at the wrong
height. In 1081:

| group | owner y | collider | `Distance` before | `Distance` after | skirt max above hip (before → after) |
| --- | --- | --- | --- | --- | --- |
| 9 chars | 0.00 | EnvCollision0 (y −0.27) | −0.27 | −0.27 | −0.04 → −0.08…+0.03 |
| 6 chars | 0.20 | EnvCollision1 (y −0.11) | −0.11 | −0.31 | −0.03 → −0.07…+0.02 |
| **3 chars** | **1.20** (raised platform) | EnvCollision2 (y 1.10) | **1.10** | **−0.10** | **+0.19 → 0.00 / −0.11 / −0.06** |

Fix: `UpdateEnvNativeCollisions()` on every `GatherSpring` (1–3 colliders per character, negligible).

**What the environment track actually is**: half-space planes with `Normal = +Y`, i.e. **floors**
(stage + raised platform). They only act on cloth that goes **below** them, so with the correct
value they do nothing in normal choreography: measured at the end of 1081, **0 of 1638 bones pushed**
and the closest bone 53 cm above the plane — exactly why it looks identical with them on or off.
Keep them on: they are the safety net for crouching/kneeling/floor moves and for the platform cuts.

**Verified**: live 1081 with env collisions ON, 18 characters — all three groups now in the same
band (skirt spread 0.15–0.32, no bone above the hip), user confirmed "Solucionado".

**New problems introduced**: none known.

---

## Method notes (so we don't repeat mistakes)

- ⚠️ **Unity's console has "Collapse" enabled**: 18 identical `Debug.Log`s show up as **one** line.
  That led us to conclude (wrongly) that the code was not running. To count for real, include
  something different in each log (position, index…). `read_console` accepts `filter_text`.
- **Every character in the live has the same name** (`Chara_1013_XX`): any comparison by name is
  ambiguous; use **instanceID**.
- `SkirtController`'s `NativeWorkingArray`/`NativeArg` are **public** → when reading them through
  reflection you must include `BindingFlags.Public`.
- The live takes ~10 s to reach `_isLiveSetup`; and the autoloader reports "the slider is not
  advancing" even though the live does advance (it reads the bar's value, not `_liveCurrentTime`).

- **Play Mode and recompiling do not mix**: recompiling with Play active leaves the character with
  `_springArray = null` + `_initialized = true` (no physics and no console error). You must
  Stop → Play → reload.
- The autoloader (`..\OWNproject\AI_UnityHUB_autoloader.py`) reports **"the Slider is not advancing"**
  in a misleading way: it reads the bar's value, which the Director updates with
  `SetValueWithoutNotify`. The live **does** advance (`_liveCurrentTime` 32.8 → 35.2). Check with
  `Director._liveCurrentTime`, not with the slider.
- If the live freezes, it is `sliderControl.is_Touched = true`: clearing `is_Touched` and `is_Outed`
  resumes it.
- Calling the MCP with the JSON as a `powershell -File` argument loses the quotes (parse error) and
  `python -` through a *pipe* waits for EOF (20 min hangs). Use a `.py` in `%TEMP%` and a `.cs` probe
  file.
- Short lives: **1001 (~2 min)** for tests, with character **1013**. The lives with an environment
  track are only: 1034, 1081, 1082, 1095, 1151 and 1177.

## Working tree state (uncommitted)

11 files: 2 new (`CySpringCollisionComponent.cs`, `LiveTimelineCharaCollisionData.cs`),
11 modified/deleted, **+503 / −3,597**. No `commit` or `push` has been done (not requested).
The diagnostic probes live only in `%TEMP%\opencode`; there is nothing temporary in the repo.
