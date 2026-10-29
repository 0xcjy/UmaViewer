# Complete skirt solver map (instruction by instruction) + switchboard

> Working document. Complements `FISICA_CYSPRING_LIVE.md` (attempts 11 and 15–16).
> **None of this is committed.**
>
> Document rule: every step carries a number (F/G/N/P/X) and the **switch** of the temporary
> `SkirtSolverDiag` component that turns it off (field `A1`, `B2`…). With every switch ON the
> behaviour is exactly today's. They are turned off one at a time.

---

## 0. How to test (protocol)

1. **Stop** Play Mode (recompiling in Play leaves `_springArray = null`; see the other doc's notes).
2. Unity recompiles; check `read_console` for errors.
3. Play → load live 1001 with character **1013**, fixed camera, leave it ≥ 8 s (until
   `_isLiveSetup`).
4. Select the character in the hierarchy (`Chara_1013_XX`) → `SkirtSolverDiag` is added there at
   runtime. The checkboxes in sections **A**, **B** and **C** apply **immediately**. The ones in
   **D** require reloading the character.
5. Before touching anything: `ContextMenu → Dump del plano de falda (una vez)` on each character to
   be compared (one from the centre and one lateral). That dump is the objective evidence.
6. One switch at a time; note down "no change" / "better" / "worse".

The dump prints, per panel: `root.world`, `root.sim` (= `root.world − Position.position`),
`w.SkirtRootPos`, `diff`, `w.ChildPos`, `w.Normal`, `w.Axis`, the four `IsCheck*`, `Evaluation` and
`OffsetAngle`. And additionally, for each hooked skirt chain (`LinkSkirtIndex >= 0`), what the
**native** code has recorded: `nw.SkirtKneeNormal`, `nw.SkirtNormalPos`, `IsCheckSkirtKnee` and
**`delta = |SkirtNormalPos − working.SkirtRootPos|`**.

**Prediction to check first** (main hypothesis): in the **live** `delta` ≈ magnitude of the
container's position (**≈ 0.9–1.0 m**), and in **`Version2`** `delta` ≈ **0**. If it holds, the
culprit is located. (Careful: `SkirtNormalPos` is only written on frames where
`Evaluation > −360`; if it comes out `(0,0,0)` and `IsCheckSkirtKnee = 0`, there was no plane on that
frame.)

---

## 1. The whole flow, in order

### PHASE F — Load / bind (once per character)

| # | Instruction | Where | What it does | If it is wrong |
| --- | --- | --- | --- | --- |
| F01 | `LoadBestSkirtParamAsset(bodyEntry)` → `FindSkirtParamEntries` + `ScoreSkirtParamAsset` + `GetOverrideData(CharaId)` | `UmaContainerCharacter` | picks the body's `SkirtParamDataAsset` (and its per-character override) | radii/angles from another body; too many or too few panels |
| F02 | `LoadSkirtController` **is postponed** if `Position` does not exist (`_pendingSkirtParam`) and is created in `CreatePendingSkirtController` | idem | avoids capturing the pose half-built | impossible rest pose → skirt floating |
| F03 | `_skirtController.CharaId / ModelScale` | idem | per-character override and collision scaling | unscaled radii |
| F04 | `Create(param)` → `ApplyParamAsset` | `SkirtController` | reads `_centerBoneName`, `_kneeLBoneName/R`, `_ankleLBoneName/R`, radii, `_influenceAngle`, `_influenceMaxAngle`, `IsOffsetAngleTargetEndNode`, `IsScaleCollision`, `OverrideCollisionList`, `_skirtBone[]` | **if a name does not exist in the rig → bone `null` → `NativeArg.*Pos` stays (0,0,0)**: phantom collider at the origin |
| F05 | `ResolveBasicBones()` (only fills the ones still `null`) | idem | `center`/`Hip`/`Waist`, knees, ankles | same as above |
| F06 | `_defaultKnee/AnkleColliderRadius = _knee/AnkleColliderRadius` | idem | keeps the asset's value | — | — |
| F07 | `ScaleCollision()`: `floor(radius * ModelScale * 1000)/1000` | idem | per-body scaling | wrong radii → plane too close/far | **A8** |
| F08 | `EnsureNativeWorkingArray()`: `new NativeSkirtWorking[nPanels]` | idem | allocates the array passed to the native | — | — |
| F09 | `InitializeSkirtData()`: `RootInitRotation = SkirtRoot.localRotation`, `SkirtRootParent = SkirtRoot.parent`, `ChildInitLocalPosition = SkirtChild.localPosition`, `CySpringRoot = null`, `IsUpdate = true` | idem | **the skirt's rest pose** | half-built pose → constant tilt | — |
| F10 | `InitializeNativeWorkingStaticFlags()`: `IsCheckLeft/RightKnee`, `IsCheckLeft/RightAnkle`, `Evaluation = 0`, `OffsetAngle = data` | idem | which colliders each panel evaluates | evaluates knees that are not touching | **B2** |
| F11 | `_UpdateSkirtArg()` (first pass, outside the step) | idem | fills `NativeArg` + the four `Vector3` per panel | — | — |
| F12 | `LoadPhysics`: `PropagateClothCollisionNames(slots.body)` | `UmaContainerCharacter` | copies the panel root's collision names to its children (`Col_B_Hip_MSkirt_*`) | the glute/hip pops out from behind | **D2** |
| F13 | `LoadPhysics`: `LoadFromDataContainers(...)` → `CySpring.Bind` per part (`Head/Body/Bust/Tail`) | `CySpringController`/`Cyspring` | builds chains, `NativeArray`, colliders, `ParentWorkIndex` | — | — |
| F14 | initial `PostSpring`/`PrevFinalRotation` + `_hipPrePosition` | `Cyspring`/`CySpringController` | previous state for the first frame | — | — |
| F15 | `LinkSkirtControllerToCySpring()` (only if `CySpringEnableSkirtSolver`) | `UmaContainerCharacter` | `CreateLinkSkirtController` → `bodySpring.SetSkirtController` | **without this the skirt is plain cloth (no knee plane)** | inspector toggle |
| F16 | `CreateLinkCySpring(bodySpring.RootBoneArray, () => _isPlaying)` | `SkirtController` | matches `SkirtRoot.name`/`SkirtChild.name` against the CySpring roots, writes `rootBone.LinkSkirtIndex = s` and `work.OffsetAngle` | no match → `LinkSkirtIndex = -1` → **it never enters the native skirt branch** | — |
| F17 | `CalcSkirtOffsetAngle(rootBone, rootPos)` = `asin(CollisionRadius / distance)` (end node if `_isOffsetAngleTargetEndNode`) | idem | per-panel angular bias | plane rotated too much/too little | **D1** |
| F18 | Warm-up (`ReserveWarmingUpCySpring` + `TryUpdateWarmUp`) | `UmaContainerCharacter` | settles the chain before showing it | starts frozen in the load pose | **D3** |

### PHASE G — Per simulation step (60 Hz, `AlterLateUpdateRuntime`)

| # | Instruction | Where | What it does | If it is wrong |
| --- | --- | --- | --- | --- |
| G01 | `EnablePhysics` + `_cySpringLoaded` + `_cySpringController != null` | `UmaContainerCharacter.AlterLateUpdateRuntime` | gate for everything | "disable physics" does nothing → **check here when testing** |
| G02 | fixed-step accumulator (`TargetCySpringFPS`, max 4 steps) | idem | Verlet cadence | rigid or accelerated chain |
| G03 | `BeginSimulation(dt)` → `_ownerTransformPosition/Rotation/LocalScale` ← current state | `CySpringController` | stores the owner's pose | — |
| G04 | `UpdatePrevousPosture()`: `owner.position = 0`, `owner.rotation = identity`, normalised scale | idem | **puts the solver's space at the origin** | it is the reason everything else must live in that space |
| G05 | `OnReset()` | idem | resets the native state if needed | — |
| G06 | `_hipMoveDistance = dist(Hip.position, _hipPrePosition)` | idem | energy from hip displacement | skirt feels "light"/exaggerated |
| G07 | `_rootBoneParentWork.GatherPosture()` → `NativeRootParentWork[i] = world pos/rot` of each parent | `Cyspring` | **parent positions in the owner's space** (because the owner is already at 0) | — |
| G08 | `GatherSpring(dt, scale, legacyScale)` per part → `GatherRootSpring` → `UpdateNativeOldTransform` (`SelfPosition = parentWorld + parentRot*localPos*bodyScale`, `ParentRotation`) + `SaveWorldRotation` (→ `AnimationRotation`) | `Cyspring`/`CyspringRootBone`/`CySpringBoneBase` | gathers the animated pose = **solver input** | — |
| G09 | **`_skirtController.UpdateSkirtArgForCySpring()`** | `CySpringController` | calls `_UpdateSkirtArg` again with the owner already at zero | **this is where `NativeArg.RootPos` comes from the container (which is NOT zeroed)** | **A1…A9, B1, B2** |
| G10 | `UpdateSpringThread()` → per root: `spring.UpdateSpring(hipMove, timescale, springRate, is60FPS)` | `Cyspring` | a single native step per root | — |
| G11 | `UpdateNativeCloth(..., root.LinkSkirtIndex, _skirtController, ..., bCollisionSwitch = true, ...)` | `CySpringNative` | if `LinkSkirtIndex >= 0 && skirtCtrl != null && skirtCtrl.IsEnableSkirt` → **skirt branch**; otherwise → plain cloth | — | **B1 / B3** |

### PHASE N — Inside the native plugin (skirt chains only)

| # | Instruction | Where | What it does | If it is wrong |
| --- | --- | --- | --- | --- |
| N01 | `working.Evaluation = -360` on every bone of the chain | `NativeClothSkirtUpdate` | "no value" | — |
| N02 | `NativeSkirtUpdate(working[i], arg)` per panel: up to 4 `Eval` (left knee, left ankle, right knee, right ankle) depending on `IsCheck*` | idem | computes the angle to open by | **B2** |
| N03 | Each `Eval`: `colPos = arg.KneeLPos − arg.RootPos` (the native subtracts `RootPos` from **knee/ankle and from `CenterPos`**) | `NativeSkirtUpdate` | moves everything into `RootPos`-relative coordinates | — |
| N04 | `Eval`: `depth = dot(C − child, N) + r`; `ang = acos(dot(P,u)/|P|)`; attenuation by `InfluenceAngle/MaxAngle` with `v2 = C − CenterPos`; keeps the **maximum** | `EvalSkirtCollisionAngle` | angle in degrees | with a wrong `CenterPos` everything is discarded (stays at −360) | **A2/A7/A9** |
| N05 | `if (Evaluation > -360)`: `half = (Evaluation − OffsetAngle)*0.5`; `q = quat(axis*sin, cos)`; `normal = q * SkirtInitNormal * conj(q)` | `NativeClothSkirtUpdate` | the plane's normal | **A5/A6/A7** |
| N06 | `SkirtNormalPos = SkirtRootPos − arg.RootPos`, copied to **every** bone with `IsCheckSkirtKnee = 1` | idem | **the plane's point, in the step's space** | **A1 (the key point)** |
| N07 | `SolveBone` per bone: Verlet + stiffness + drag + gravity + wind + limits | `CySpring_SolveBone` | the normal cloth chain | — |
| N08 | `ResolveBoneCollisions` (spheres/capsules/planes `Col_*` from the asset) | idem | thigh, hip, body | **B3** |
| N09 | Skirt block: `d = dot(p − SkirtNormalPos, SkirtKneeNormal)`; if `d < CollisionRadius` → `p += normal*(radius − d)` and **re-project to the bone's length** | idem (`SolveBone`) | keeps the skirt outside the knee | a displaced `SkirtNormalPos` pushes **the whole** chain |
| N10 | Back to the loop: final rotation per bone (`FinalRotation`) | idem | result | — |

### PHASE P — Write-back to the bone

| # | Instruction | Where | What it does | If it is wrong |
| --- | --- | --- | --- | --- |
| P01 | `EndSimulation()` waits for the thread, `PostSpring` per part (inline) | `CySpringController` | — | — |
| P02 | `spring.PostSpring()` → per root `PostAllSpring()` → `CySpringBoneBase.PostSpring()`: `finalRotation = native.FinalRotation`; `BlendRate<1` → `Lerp(PrevFinalRotation, …)` + `ApplyLimitRotation`; **`transform.rotation = finalRotation`** | `Cyspring`/`CySpringBoneBase` | **this is where the skirt becomes visible** | with this OFF, the skirt stays as the animation left it | **C1** |
| P03 | owner transform restored (position/rotation/scale) | `CySpringController` | puts the rig back in place | — |
| P04 | `_hipPrePosition = Hip.position` | idem | for the next step's `hipMoveDistance` | — |

### PHASE X — What the viewer does **NOT** do (and why it does not matter)

| # | Thing | Why it is not the problem |
| --- | --- | --- |
| X01 | `SkirtController.UpdateSkirt()` (the root rotation by `Evaluation`) **is never called**: its only call site is `CySpringUpdater`, which is not in any scene or prefab (checked by GUID + `m_MethodName`). | **It is not a defect**: `IsUpdateSelf()` returns `!_isEnableCySpringFunc()`, and that func is `() => _isPlaying`. That is, in the official game that method is the **fallback path for when CySpring is OFF**. With physics on it does nothing. That is why the `Space.Self → Space.World` change of attempt 15.3 could not change anything (and why the "legs solved" of that moment was a placebo). |
| X02 | Wind (`CySpringEnableWind = false`) | with wind OFF the skirt still tilted (measured, attempt 15.1) |
| X03 | Skirt root rotation from the animation | `PostSpring` overwrites the rotation of every bone in the chain; the animation only enters as `AnimationRotation` (G08) |

---

## 2. Main hypothesis — **CONFIRMED AND FIXED** (2026-10-29 23:05)

`NativeArg.RootPos` is read from the **container** (`Chara_XXXX_00`), but **everything else** in the
step lives in the **owner**'s space (`Position`), which `UpdatePrevousPosture` leaves at `(0,0,0)`
(G04):

- `SelfPosition` of each bone → G08, in that space.
- `SkirtRootPos`, `SkirtInitChildPos` → positions of bones under `Position`, in that space.
- `RootPos` → `CachedTransform.position` = **container, which is not zeroed**.

And the native places the plane with `SkirtNormalPos = SkirtRootPos − RootPos` (N06) and compares it
against the step's `p` (N09). So the knee plane ends up **displaced by exactly the container's
position** (on the order of 1 m), in a fixed world direction:

- the skirt bones are *on the other side* of the plane → `d << radius` → big push → **the skirt
  lifts / inflates** (attempt 11's symptom);
- the push is `+SkirtKneeNormal`, which is **per panel** → different panels deform differently →
  **one corner points at the stage centre** (attempt 15's symptom);
- since the error is a **world** vector, the affected corner depends on the character's *yaw* →
  "only on non-central slots";
- in **`Version2` the container is at the origin** → `RootPos ≈ 0` → the plane lands where it should
  → **the skirt comes out right**, which is exactly what was just measured with the saved pose;
- and `Evaluation` (the angle) comes out right anyway, because `Eval` subtracts `RootPos` from
  everything (N03/N04), so it is translation-invariant: that is why the 5–26° of attempt 16.4 were
  "reasonable".

Corollary: `nativeArg` **cannot be patched** at runtime: `_UpdateSkirtArg` (G09) rewrites `RootPos`,
`CenterPos`, knees and ankles on every step. Any earlier test that touched `NativeArg` from outside
(e.g. the "+5 m on `CenterPos`" of attempt 15.5) **never reached the engine**. That is why the
switches now live inside `_UpdateSkirtArg` itself.

### The measurement that closed it (same as `FISICA_CYSPRING_LIVE.md`, attempt 18)

Live 1001, 18 characters, same frame: `delta = |SkirtNormalPos − working.SkirtRootPos|` was
**exactly** the container's position (8.000 m / 7.000 m / 4.801 m / 0.705 m / 0.087 m depending on the
slot). With the character at x=8, panels L/BL/BR/B received pushes of **4–8.5 m** and the ones facing
the other way received **none**; with the correct `RootPos`, all 11 panels sit at `dmin ≈ 0.00` with a
push of 0.02–0.03 m.

**Fix** (already in code, default): `RootPos` = anchor of the CySpring **owner** (`Position`, which is
0 during the step), passed from `BindCySpringOwnerAnchor`. Switch **A1** now only serves to reproduce
the bug (`Contenedor`).

---

## 3. Recommended bisection order

> **Status**: round 1 (`A1 → Cero` / `RigPosition`) was the culprit and **is already applied as the
> default value**. The rest of the table stays as a reference in case another skirt defect shows up.

| Round | Switch | What it rules out | If the skirt gets fixed… |
| --- | --- | --- | --- |
| 1 | **A1 → `Cero`** | the plane's displacement | **culprit confirmed** (do not continue) |
| 2 | **A1 → `RigPosition`** | idem, with the engine's real anchor | confirms it and gives the "correct" long-term variant |
| 3 | **B1 OFF** | the whole skirt branch (plane + evaluation) | the problem is the plane/`Evaluation` |
| 4 | **B2 OFF** | only the evaluation (no plane) | tells "displaced plane" apart from "bad angle" |
| 5 | **C1 OFF** | the write-back to the bone | the deformation comes from the animation track, not the solver |
| 6 | **B3 OFF** | all collisions of the chain | the problem is a capsule (`Col_*`) from the asset |
| 7 | **A5 / A6 OFF** | the "world" plane normal and axis | the axis/normal computed from the pose do not match |
| 8 | **A3 ON** | passing `SkirtRootPos` into the owner's space | confirms the space split (see N03) |
| 9 | **A4 OFF** | the reconstructed `SkirtInitChildPos` | the reconstruction does not match the real pose |
| 10 | **A7 / D1** | `OffsetAngle` | per-panel angular bias |
| 11 | **A2, A8, A9, D2, D3** | the rest | (unlikely: already ruled out with data) |

If **A1 = Cero** fixes it, the definitive fix is one line:

```csharp
// SkirtController._UpdateSkirtArg()
// RootPos must be in the solver owner's space (which is 0 during the step).
NativeArg.RootPos = _rootAnchor != null ? _rootAnchor.position : Vector3.zero;
```

with `_rootAnchor` = the same `transform.Find("Position")` that `BindCySpringOwnerAnchor` already
uses.

---

## 4. Cleanup (when the problem is closed)

**Done** on 2026-10-29: `SkirtSolverDiag` and every `(DIAG …)` block were removed from
`SkirtController`, `UmaContainerCharacter`, `Cyspring` and `CySpringController`; the four real fixes
(skirt `RootPos` anchor, `IsWorld` plane position, per-step env refresh, plus the deferred bind) stay.
The local pose tool (`Assets/Editor/PoseSnapshotTool.cs`) is now listed in `.git/info/exclude`.

The original checklist, for reference:

1. Delete `Assets/Scripts/Diagnostics/SkirtSolverDiag.cs` **and its `.meta`**.
2. Remove from `SkirtController.cs` the blocks marked `(DIAG A*)` / `(DIAG B*)`, the `_diag` field,
   `SetDiag`, `_ResolveRootPos`, `_ResolveRigRoot`, and keep the final fix (one line).
3. Remove from `UmaContainerCharacter.cs` the `(DIAG D*)` blocks and `GetOrAdd`.
4. Remove from `Cyspring.cs` the `(DIAG B3)` and `(DIAG C1)` blocks.
5. Delete `docs/FALDA_SOLVER_MAPA_COMPLETO.md` or move the useful parts into
   `FISICA_CYSPRING_LIVE.md`.
