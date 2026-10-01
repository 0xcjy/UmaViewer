# Live render restoration — 2026-09-20

## Goal (still active)
Restore the Live appearance of `D:/Projects/UmaTools/UmaViewer` in master7. Full visual parity is NOT established.

## Reference identity
Installed GameAssembly.dll SHA256:
`D678A0713257AF45A6CD499854A7C8A80CB5999F5CB79E320FD12E315B28B78D`
Matches master5 `tmp/reference-inventory-20260905.json`. Its previous D:/Download reference path therefore does not invalidate the GameAssembly evidence. Other files have not yet been re-hashed.

Reuse master5 `tmp/render-{analysis,call-map,disassembly}.txt`, `tmp/render-pipeline-api.txt`, runtime metadata and unpacked DLL. The original on-disk metadata was only 21 bytes; repeating a naive Cpp2IL run is not the next useful step.

## Baseline findings
- PostImageEffectFeature and DofDiffusionBloomOverlayPass were empty MonoBehaviours.
- GallopImageEffect only mapped Bloom into a non-global Volume without adding a collider. Diffusion never rendered via that adapter.
- Director cached the first effect camera without checking camera changes.
- Active UMA URP asset has HDR disabled. Do not silently change this before comparing reference buffer formats.
- master5 is not a finished solution: latest character GlobalLight writes were disabled after an A/B isolated washout, and several passes remain unimplemented. Do not copy its Director/config wholesale.

## Changes
- Reused master5 recovered Bloom/DOF/Diffusion pass and minimal dependencies; kept master7 existing metas. Added CameraData and GallopImageEffectParameter with source metas.
- Removed the uncompiled historical block from PostImageEffectFeature and dependencies on master5 LiveRenderConfig. Rendering enabled with a serialized switch for A/B.
- Feature only runs during play on the active Director presentation camera; excludes non-game/monitor/reflection cameras.
- Replaced Volume adapter with direct timeline -> recovered parameter routing. Explicit DecideDrawType is essential: Setup deliberately does not select a mode.
- Shared runtime state resets on subsystem initialization, Live initialize and Director destruction. No old camera LateUpdate re-publishes stale timeline values.
- Added Editor LiveRenderSetup.Install (idempotent, uses AssetDatabase); ran it successfully to register the feature in UMAUniversalRenderPipelineAsset_Renderer.
- Added LiveRenderSetup.Validate contract checks.
- Added tools/check_live_compile.ps1 from master5, fixed its extra-source discovery to respect nested asmdef/asmref boundaries. The unmodified script incorrectly included disabled Gallop.Legacy sources and reported duplicate types; that was a harness error, not a baseline project error.

## Verification
- Baseline and migrated runtime assembly Roslyn compilation pass (existing warnings only).
- Full Unity 2022.3.62f1 batch import/compile/install completed with exit 0: tmp/live-render-install.log.
- Unity batch validation completed with exit 0: tmp/live-render-validation.log; look for [LiveRenderValidation] PASS.
- Checks: idempotent registration, default None bypass, Bloom mode, unscaled intensity, DiffusionBloom mode, disabled-key reset, fresh shared state.
- `-nographics` checks cannot prove shader execution or pixel changes. NO rendered-image improvement claimed yet.
- Original overwritten source backups: tmp/live-render-baseline (including Director/GallopImageEffect and initial compile log).

## Next critical actions
1. Create/adapt a minimal Live capture harness. master5 Assets/Editor/LiveCaptureAutomation.cs and Assets/Scripts/umamusume/Gallop/Live/LiveFrameCapture.cs exist but depend on its expanded Director/config; do not copy blindly.
2. Run a real graphics device, load shader.a and a deterministic Live/characters/frame. Capture feature off/on and ensure recovered enqueue/execute/material diagnostics accompany output images.
3. Audit actual source/temporary RT formats, pass indices and source bindings against reverse evidence before tuning brightness.
4. Add missing DOF timeline dispatch/focus and other tracks only with reference-backed mappings. Current transplant supports backend modes but Director only routes existing Bloom/Diffusion track.
5. Investigate stage/lens flare/GlobalLight separately; do not mask lighting errors by lowering Bloom or disabling global light.

## Commands
```powershell
& tools/check_live_compile.ps1
# Launch helper processes with Start-Process -WindowStyle Hidden.
# Unity args for validation:
# -batchmode -nographics -projectPath D:/Projects/UmaTools/UmaViewer-master7 -executeMethod LiveRenderSetup.Validate -quit -logFile D:/Projects/UmaTools/UmaViewer-master7/tmp/live-render-validation.log
```

## Resumed after confirmed physics repair — 2026-09-20
Target images are actual-game video extracts under master5/captures/reference, NOT images
rendered by master5. Inspected live1001-dof-cuts-20260906/frame_001740.png and its manifest:
1920x1080, 60 fps, Live1001 / stage10102, cast 1068_00,1006_00,1030_00, then 1003_00.
The video offset is 1.5 seconds; named frame indices are timeline frame indices, not raw
video frame indices. Do not compare arbitrary cast/costume or unaligned camera cuts.

Found master7 missing even the serialized postEffectDOFKeys field and DOF timeline dispatch.
Added master5 DOF key schema (adapted abstract dataType), worksheet field, interpolated
DOF dispatch after camera and Bloom updates, and Director routing into the existing pass.
Disabled/missing tracks clear DOF instead of retaining the previous camera's state.
Focus uses master5's screenshot-tested inference: bit17=metric focal distance; otherwise
bit16=current camera look-at; otherwise character-mask head average. Failed positional
focus disables DOF instead of focusing at the common one-metre default. These flag names
are NOT proven reverse-engineered semantics. See master5/docs/LIVE_DOF_TIMELINE_2026-09-06.md.
Dedicated multi-camera DOF tracks, flags 18/19, and exact original interpolation semantics
remain unverified; main-track integration is not complete game parity.
The renderer now explicitly requests URP depth when the selected mode needs it.

Capture harness updated to reference cast and unique costume 00, 1920x1080, frames
1710/1732/1740/1755/3000; records focus/camera/actual cast diagnostics. Added opt-in menu
UmaViewer/Rendering/Capture current Live post-effects off-on. This operates on a loaded
Live and does not close the user's Editor. Off/on refers to the entire recovered feature,
not DOF alone. No automatic capture has been initiated in the user's existing session.
Standalone executeMethod capture still exits its own batch Editor after completion.

Validation: runtime and Editor Roslyn compilation passed. Extended state-contract checks
for composed DOF mode/depth/focus, not yet executed in Unity. No new rendered images or
pixel-equivalence result is claimed. PostFilm/color correction/other tracks are still pending.
Physics files untouched in this rendering pass.

## DOF full-screen blur follow-up — 2026-09-20
User reported intermittent full-frame blur after enabling DOF. Found a concrete depth
routing bug: the recovered Parameter.IsDepthTexture only covers temporary-disable and
overlay conditions; the URP adapter incorrectly treated it as the full depth requirement.
Ordinary DOF without overlays therefore did not necessarily request depth. Fixed the URP
adapter to request depth for all five DOF modes in addition to overlay depth requirements.
Updated contract checks to assert the adapter property, not the recovered property.

Also tracked which Camera owns the main DOF track and the cached look-at. Bypass DOF
(not Bloom/Diffusion) when rendering a different camera whose dedicated DOF track is not
implemented. Reject missing/nonfinite/behind-near/out-of-range positional focus, and
nonpositive normalized metric focus before it enters the CoC calculations. No replacement
focus distance or arbitrary blur multiplier is introduced. Transition warnings include
Live time, camera, key frame and attributes for diagnosis.

Runtime and Editor Roslyn builds pass. These changes address verified code defects, not
proof that every reported blurry frame is fixed. No screenshot validation was performed.
Native flag semantics, mult-camera tracks and shader/depth-buffer correctness remain to
be verified; multi-camera bypass is a conservative partial implementation, not parity.

## Startup blur isolation and playback time — 2026-09-20
- Added a runtime uGUI seconds readout in the existing right-hand progress-bar margin:
  current seconds / total seconds, one decimal, updated in LateUpdate including scrubbing.
  Text does not intercept pointer events; it follows the existing bottom-panel animation.
- Added `DOF: OFF` / `DOF: ON (test)` button in the left-hand margin. DOF defaults OFF
  at subsystem registration. This is a diagnostic safety bypass, NOT a DOF parity fix.
- GallopImageEffect applies the user gate after copying timeline parameters, disabling
  both DOF flags and selecting a non-DOF draw path without mutating authored parameters.
  Bloom/Diffusion settings are preserved. Clicking re-applies the active camera adapter
  immediately, including while paused; subsequent timeline updates honor the same gate.
- Runtime and Editor Roslyn compilation passed (runtime has existing warnings).
  No in-Editor UI or rendered-image validation performed. Re-enter Live after Unity
  imports the scripts so Awake creates the controls. If startup blur remains with DOF
  off, next inspect Diffusion/Bloom composition instead of assuming a focus error.

## DOF source-parameter focus-mode regression fixed — 2026-09-20

Compared master5 and master7, including the actual parameter types rather than only
matching assignment statements. The recovered render pass is identical except for its
logging interval; the important difference is the timeline-to-renderer adapter:

- master5 writes `DofDiffusionBloomOverlayPass.Parameter` directly. Its focal-position,
  focal-transform and focal-distance setters only write values.
- master7 writes `DofDiffusionBloomOverlayParam` before `GallopImageEffect` copies it.
  Those setters ALSO select Position, Transform and Point, respectively.
- The migrated Director selected the intended mode FIRST, then assigned position,
  null transform and finally focal distance. Thus ALL otherwise valid look-at/character
  keys became Point keys. `Setup` faithfully copied the wrong mode to the renderer.
- live1001's dumped frame-0 key in master5/tmp/dof-keys-1001.json has attribute 0x50000,
  focalPoint=1, focalSize=30 and spread=1.75. It is a look-at key under the existing
  inferred flag mapping, not an instruction to focus one metre away. The previous
  user's `target=point, focus=0.2m, near=0.8` log is consistent with this exact defect.

Fix: assign the resolved DofFocalType LAST, after all three focus-value setters. Keep
existing focus validity guards, units, blur strength and native preparation formulas.
No additional arbitrary focal-distance clamp or blur attenuation was introduced.
DOF is enabled by default again on a new Play session; the ON(test)/OFF A/B button
remains. Bloom/Diffusion and physics are unchanged by this fix.

### Executed regression validation

Added `Assets/Editor/LiveDofFocusRegression.cs`, available outside Play Mode from
`UmaViewer/Rendering/Validate DOF focus routing`. It invokes the production Director
callback, GallopImageEffect adapter and PrepareDofParam rather than reproducing the
production assignment logic in the test. Restores global runtime state afterward.

Executed with Unity 2022.3.62f1 in `tmp/dof-focus-test-project` (a separate minimal
project loading the actual compiled runtime and dependencies). The user's open editor
was not stopped or restarted.

- Before fix: failed with `expected Position, got Point`; log `tmp/dof-focus-before.log`.
- After fix: exit 0; log `tmp/dof-focus-after.log`. Passed startup camera look-at,
  metric-distance focus, switching back at a camera cut, missing target bypass,
  URP depth requirement, prepared focal depth/sharp interval, DOF-only toggle and
  re-enabling while paused.
- Synthetic fixture results through actual PrepareDofParam: positional target 25m
  produces focus 25m / sharpTo 40m, authored metric distance 8m produces focus 7.2m
  with near=0.8 (existing native formula), and subsequent positional target produces
  focus 4m. These are test-fixture values, NOT captured Live camera measurements.
- Runtime and Editor Roslyn compiles pass, with existing runtime warnings only.

This establishes and repairs a concrete migration regression, but does not constitute
pixel validation against the game. Full-scene captures, multi-camera DOF tracks and
native flag semantics remain separate work. The previous default-OFF mitigation is
superseded by this fix; a manually toggled OFF state can still be enabled with the UI.

## 2026-09-20 — foreground DOF sharp interval (live1001, ~50s)

The reference game image `master5/captures/reference/live1001-dof-cuts-20260906/frame_003000.png`
has sharp supporting performers, while `master5/captures/runs/20260906_131230_343_146781/frame_003000.png`
already has the same foreground overblur reported in master7. Copying master5's backend is not a fix.

Evidence:
- Key2963: quality=5 (background + foreground), focalSize=30m, foregroundSize=1,
  smoothness=.5, character mask=7. Capture focal depth=40.81587m; supporting performer
  depths include 29.23675m, 31.04587m, 34.40935m. Previous focus-mode fix remains intact.
- Extracted local `shader` bundle with UnityPy; disassembled the actual D3D bytecode
  using Windows D3DDisassemble (no shader-source reconstruction assumed).
  PostDiffusionDofBloom_Rich pass10 COCBGFG uses fragment blob19. Constants:
  cb0[140]=_CurveParams; cb0[164].y=_dofForegroundSize; cb0[23]=_ZBufferParams.
  Its alpha is max(saturate((curve.z-depth)*curve.x)*foregroundSize,
  saturate((depth-curve.z)*curve.y)), clamped to 1. Both sides use curve.z.
- Native PrepareDofParam evidence matches recovered C#: curve.z=(focus+size/2)/(far-near).
  Consequently this binary/backend + current game shader combination has no sharp band
  on the foreground side: those 29–34m supporting performers receive full foreground CoC.
  This proves the local contract mismatch; it does NOT prove the current game's own
  CPU implementation uses the same contract as the older reference binary.

Correction:
- Add `_GallopDofFocalStart=max(0,(focus-size/2)/(far-near))` in PrepareDofParam.
- New Resources shader `RenderPipeline/LiveDofCoC` uses that near edge only for foreground.
  Retain original background edge/slope, foreground strength, depth linearization,
  depth UV scroll, source RGB, blur and composition. It replaces ONLY CoC generation
  in the three modern quality5 paths (Dof, DofBloom, DiffusionDofBloom).
- Background-only mode still invokes the official shader. Old DOF remains untouched.
  Material is cached per pass and disposed with the pass; no additional blit/RT.
- This is a reference-guided compatibility correction, not a claim of byte-identical
  native reconstruction. No timestamp/song/character-specific runtime override.

Validation:
- Runtime Roslyn compile and Editor compile passed.
- Isolated Unity 2022.3.62f1 GPU run, production PrepareDofParam -> BlitDofCoc -> shader
  -> floating-point readback: 80 samples passed, exit0 (`tmp/dof-foreground-gpu.log`).
  Includes normal/reversed Z, zero-width sharp band, foreground strength0, preserved RGB.
  With frame3000 parameters: CoC at 5m=1, 15m=.5306, 25m=.0410,
  29.24/31.05/34.41/40.82m=0, 65m=.4475, 90m=1.
- Test menu: `UmaViewer/Rendering/Validate DOF foreground (GPU)` outside Play Mode.
- Still requires visual confirmation in the user's running Live around 49–53 seconds;
  synthetic GPU readback does not establish a full-frame match (depth coverage/UVs,
  transparent stage props, lighting and other post effects remain separate concerns).

## 2026-09-20 — PostFilm color timeline / native Overlay integration

Scope: restore the three main-sheet color PostFilm tracks without modifying the
accepted physics or DOF algorithms. This is not a claim that all remaining effects
or full-frame game parity have been achieved.

Evidence and implementation:
- Live1001 main camera has 162/72/12 keys in postFilmKeys/postFilm2Keys/postFilm3Keys.
  They were not serialized/dispatched in master7. Added the list wrapper, worksheet
  fields, timeline evaluation and Director -> existing ScreenOverlay forwarding.
- Key field order follows the local game bundle TypeTree (including BlinkLight fields).
- Reference binary LiveTimelineKeyPostFilmData.isInverseVignette RVA 0x1af62d0 tests
  attribute 0x100000. 231/246 inspected keys use it. This selects inverse-vignette
  shader passes, NOT depth disable. Native SetupPostFilmUpdateDataInfo RVA 0x1ae70c0
  confirms raw power, signed depthPower, linear parameter interpolation and direct
  RollAngle forwarding. The color layout is All=(0,0,0,0), TopBottom=(0,0,1,1),
  LeftRight=(0,1,0,1), Four=(0,1,2,3).
- Unlike master5's approximation, no arbitrary power multiplier/cap or new vignette
  shader is used. The existing official PostBlit_Rich/ScreenOverlay chain owns blend
  math, depth mask, inverse pass and three-layer composition.
- Evaluates in cached objects. Missing, disabled and pre-first-key tracks reset to
  None; seeks use the existing key lookup/interpolation. A zero/invalid scale falls
  back to 1 to prevent invalid reciprocal shader coordinates.
- No additional renderer feature or duplicate full-screen postprocessing chain.

Validation:
- tools/check_live_compile.ps1: runtime compilation passed (existing warnings only).
- LivePostFilmRegression timeline PASS in isolated Unity 2022.3.62f1: worksheet JSON,
  four color mappings, unclamped power, signed depth, exact cuts, reverse seek,
  absent/disabled tracks, movie bypass and zero-scale guard.
- Actual locally extracted game PostBlit_Rich shader GPU readback PASS:
  Add (0.2,0.3,0.4)+(0.4,0.2,0.1)*0.5=(0.4,0.4,0.45);
  explicit COLOR_ONLY on/off produced the same result, so the recovered keyword
  adapter was deliberately left unchanged. First/later inverse passes both changed
  the spatial mask; ordered Add then Lerp returned (0.2,0.2,0.225).
  Log: tmp/postfilm-final-gpu.log, Unity exited successfully.
- The current game shader's _PostFilmIsWithoutDepth=1 suppresses the color contribution
  in the tested Add variant. Keep native IsEnableDepth=true; depthPower=0 is sufficient
  for these synthetic color tests. Do not reinterpret inverseVignette as this flag.
- CPU menu: UmaViewer/Rendering/Validate PostFilm timeline. GPU test is deliberately
  batch-mode-only; UMA_POSTFILM_TEST_BUNDLE must point to the user's locally extracted
  shader bundle. No proprietary shader bundle is copied into Assets.

Remaining limits / visual acceptance:
- Re-enter Live after compilation so the added worksheet fields deserialize anew.
- UV movie tracks conservatively bypass (movie texture/time ownership is not wired).
  BlinkLight-synchronized colors currently use authored key colors, and loop metadata
  is not executed here. All inspected Live1001 tracks are color mode with no loops.
- Uses the main camera worksheet; dedicated multi-camera PostFilm tracks remain pending.
- Actual game screenshot parity, Bloom/diffusion strength, sun shafts, lens flares and
  other effects still need separate inspection. GPU tests validate this adapter, not
  all effects or an entire real Live frame.
- Combined isolated regression also passed after the PostFilm integration:
  LivePostFilmRegression CPU/GPU, LiveDofFocusRegression and
  LiveDofForegroundRegression (80 GPU samples), with successful Unity exit.
  Log: tmp/post-effects-regression.log.

## 2026-09-20: radial blur restoration (not DOF)

Implemented:
- Deserialize radialBlurKeys using the shipped TypeTree field order; reuse existing
  timeline interpolation and playback filtering. Director publishes a value-type
  sample after PostFilm; absent/off/pre-first/disabled tracks clear the effect.
- RadialBlurPass uses the locally loaded Cygames/ImageEffects/RadialBlur shader,
  not a substitute blur. Native OnRadialBlur RVA 0x1a3d340 evidence gives five
  blur/composite pairs (0/1, 2/3, 4/5, 6/7, 8/9), direct downsample divisors,
  ping-pong iterations, original full-resolution source for final composition,
  signed 0.0001 area-difference guard, raw strength, normalized depth distances,
  rectangle bounds and expansion by blend length. Roll uses -degrees*Deg2Rad.
- Same feature owns SourceSetup -> Dof/Diffusion/Bloom/Overlay -> Radial -> FinalBlit.
  DOF setup failure no longer suppresses an independently valid radial pass.
  Registration evidence: native RegisterPass accesses the DOF/tilt/radial fields
  in that order around RVA 0x1a36379 / 0x1a364ac / 0x1a365a0.
- Lazy material lifetime, matching active presentation camera only, explicit depth
  request, keyword reset, release of temporary textures, descriptor format retained.
  Malformed downsample/iteration values are guarded; iterations capped at 32 (actual
  Live1001 uses 1/2, divisors 2/3/4). Physics/DOF algorithms were not modified.

Evidence and tests:
- tmp/radial-native.txt, radial-param.txt, radial-execute.txt, feature-register.txt,
  radial-program-*.txt are local disassembly evidence; none are new bundled assets.
- Actual Live1001: 28 keys, onset frame729 (12.15s), mode2 power0.5 depthBack38;
  off frame988; mode1 impact frame3965 (66.083s). Runtime compile passed.
- Assets/Editor/LiveRadialBlurRegression.cs tests worksheet loading, discrete
  cuts/seeking, missing/disabled tracks, pass mapping, signed area guard, depth
  normalization, rectangle expansion and parameter copying. Optional locally
  exported UMA_RADIAL_TEST_KEYS fixture validates all 7901 frames of Live1001.
- Isolated Unity GPU test uses actual game shader and production Render method:
  zero-strength identity; all five modes change a stripe pattern with finite output;
  multiple iterations/downsampling; normal and reversed-Z near-depth protection
  (maximum error 5.96e-8), far-depth blur, rectangle overriding depth protection,
  and stale keyword/uniform reset. Shader bundle supplied only by environment path
  UMA_POSTFILM_TEST_BUNDLE, never copied into Assets.
- Combined suite PASS: radial CPU/real keys/GPU, PostFilm CPU/GPU, DOF focus,
  DOF foreground (80 GPU samples). tmp/radial-final-regression.log, Unity successful
  exit. The minimal isolated test project still logs existing Burst runtime folder
  and DOTween module warnings/errors on import; no test or C# compilation failures.

Known limitations (do NOT claim complete visual parity):
- Depth bit16 is inferred from real track data; original key accessors were not
  present in the inspected reference binary. Bits17/18 are unresolved: frame5989
  contains bit18 and a nonzero rectangle without bit17. To avoid shipping a guessed
  activation rule, timeline rectangle enable/expand are currently false. The native
  rendering implementations are present and GPU tested, ready for verified mapping.
- Dedicated multicamera radial tracks and complete real-Live capture comparison
  remain pending. Synthetic shader tests are not whole-frame acceptance.
- Newly added serialized fields require re-entering Live after editor compilation.
- Restore process must leave the user's main Unity editor running; tests above use
  only tmp/dof-focus-test-project and do not restart the main editor.
- Follow-up source-ownership audit: radial-only setup uses the current configured
  render event rather than a stale DOF pass event. For events >550, radial reacquires
  camera color after DOF's direct writeback; radial output always enqueues FinalBlit.
  This closes the late-event missing-writeback/resource-retention case without
  modifying the DOF algorithm. Runtime compile rerun passed.
- A thread heartbeat named "Live 渲染持续还原" (id live) was enabled at the user's
  request for continued unattended work, every 30 minutes. It should read this
  document before progressing and notify only meaningful results or required action.

## 2026-09-20 heartbeat: Bloom / diffusion shader-contract audit

Scope and result:
- Audited local native CreateBloomTexture (RVA 0x1a18aa0) and
  OnRenderImageDiffusionFastBloom (RVA 0x1a1c8b0), together with the locally
  extracted FastBloom, PostBloom_Rich and PostDiffusionBloom_Rich DXBC.
- No evidence from this stage justifies changing the current Bloom strength or
  replacing diffusion with a generic blur. Runtime rendering, physics and DOF
  algorithms are unchanged. This is a verification stage, not a visual upgrade.
- Positive-radius Bloom deliberately uses a neutral first reduction (threshold=0,
  intensity=1). FastBloom pass2 subtracts threshold per tap; pass3 applies intensity.
  Both nine-tap kernels have weights .225, +/- .15, +/- .11, +/- .075,
  +/- .0525, summing to 1. Applying threshold/intensity in all three passes would
  incorrectly suppress highlights. Current production ordering matches this.
- Zero-radius Bloom pass1 performs max(average4(source)-threshold,0)*intensity.
- Bloom composition is Add: S+B*w, or Screen: 1-(1-S)*(1-B*w), selected by
  _BloomIsScreenBlend. There is no saturate in these native shader expressions;
  tests therefore include source RGB >1 and intensity >1, not only LDR colors.
- Diffusion first composites Bloom into C, then computes component-wise
  max(C,max(0,C*C+R-R*C*C-threshold)), where R is the separately blurred RGB.
  Color controls follow in brightness -> saturation -> contrast order; luminance
  coefficients are (.2125,.7154,.0721), contrast pivots around .5. This is not a
  simple interpolation between sharp and blurry images.
- Uniform mapping confirmed: _ColorParam=(bright,saturation,contrast,threshold).
  Existing adapter matches; no arbitrary gamma conversion or artistic multiplier
  was added based solely on screenshot impressions.

Persistent validation:
- Added Assets/Editor/LiveBloomDiffusionRegression.cs (isolated batch mode only).
  Uses actual shipped shaders from UMA_POSTFILM_TEST_BUNDLE, not replacement
  shaders; restores touched shader globals, destroys RTs/materials, unloads bundle.
- 24 extraction checks (zero-radius and two-axis paths), 9 Add/Screen composition
  checks including on/off transition, 27 diffusion/color-order checks all PASS.
  Expected values are independently calculated from DXBC, RGB tolerance .00015.
- tools/check_live_compile.ps1 passed; tmp/bloom-audit-compile.log.
- Combined isolated suite passed and Unity exited successfully:
  tmp/bloom-audit-regression.log. Includes the new 60 shader-contract checks,
  radial 7901 actual-key timeline samples and GPU suite, PostFilm timeline/GPU,
  DOF focus and 80 foreground GPU samples. Existing minimal-project import/Burst
  warnings remain outside this audit; do not call the entire console error-free.
- Local-only evidence: tmp/create-bloom-audit.txt, tmp/diffusion-bloom-audit.txt,
  tmp/FastBloom-pass*-audit.txt, tmp/PostBloom_Rich-pass*-audit.txt,
  tmp/PostDiffusionBloom_Rich-pass*-audit.txt; extraction helper
  tmp/disassemble_bloom_audit.py. No proprietary bundle was added to Assets.

Limits / next work:
- These GPU tests verify shader equations using constant-color HDR fixtures; they
  do NOT execute the full production ScriptableRenderContext chain, validate blur
  radius on a real scene, check complete camera/RT ownership, or demonstrate
  screenshot parity. Real Live captures and spatial-response validation remain.
- No new claim about radial key bits17/18: their timeline mapping remains disabled.
- Do not retune established physics/DOF to compensate for unmeasured differences.
  Next useful work is real-Live capture plumbing or remaining-light-effect evidence.
- Main Unity editor and unrelated physics-test editor were left running. Isolated
  validation completed; no isolated Unity test remains running at this checkpoint.

## 2026-09-20 heartbeat: missing Live1176 color curves, verified LUT foundation

Actual-asset inventory (local TypeTrees, not inferred from screenshots):
- Live1001 has no colorCorrectionDataLists or preColorCorrectionDataLists.
- Live1093 has one color-correction group with three keys (0,157,8287), all disabled.
- Live1176 has one group with SIX ENABLED simple/non-selective keys at frames
  0,836,3819,5294,5597,7066. Master7 previously had no matching worksheet field,
  so these authored RGB curves could not survive deserialization or affect rendering.
  This establishes a missing effect for Live1176; it does not explain Live1001 gaps.
- Live1176 also has authored distance/height fog and tilt-shift keys. A generic
  bloom-strength increase is not a substitute for restoring these independent tracks.

Evidence and implementation (preparatory, NOT enabled in the render chain yet):
- Added LiveTimelineColorCorrectionData.cs and the worksheet colorCorrectionDataLists
  field. Unlike master5's placeholder, the key inherits LiveTimelineKey, not
  LiveTimelineKeyWithInterpolate: the actual keys contain NO interpolateType,
  curve or easingType fields. No guessed cross-key interpolation was introduced.
- Added ColorCorrectionCurveLut.cs: a reusable, disposable 256x4 linear ARGB32
  no-mipmap texture, R/G/B grayscale rows at y=.125/.375/.625 and an unused zero row.
  Texture/pixel storage is reused on update; there is no per-frame allocation loop.
- Native ColorCorrectionPass evidence: GetCurveTime RVA 0x1a10820 uses i/255;
  FloatToByte RVA 0x1a107e0 clamps then truncates(value*255), NOT rounds;
  MakeGrayColor RVA 0x1a10ad0 replicates this byte into RGB with alpha255;
  SetupInstance RVA 0x1a10e70 allocates 256x4 texture format5, mip=false, linear=true;
  UpdateTextureParameter RVA 0x1a11770 clamps each source/target curve sample,
  blends samples before byte quantization, and fills row offsets0/256/512.
  RIP constants verified at VA 0x182291b64=1 and 0x182291d90=255.
- The shipped ColorCorrectionCurvesSimple shader samples those three rows and then
  applies saturation about luminance (.2126729,.7151522,.072175), preserving alpha.
  PostFilm mask power0 gives full correction, not disabled correction. Mask variants,
  advanced depth curves, selective correction and stencil integration remain pending.
- No render pass or timeline callback was registered in this stage. This deliberately
  avoids guessing the ordering relative to prepare-screen/mask passes. Runtime visual
  output, physics and DOF algorithms remain unchanged. Do not describe this as a
  completed color-correction restoration.

Validation:
- Assets/Editor/LiveColorCorrectionRegression.cs reads actual six-key Live1176 fixture
  through JsonUtility and verifies key count, cut frames, enable flags and authored
  AnimationCurve tangents. It tests byte truncation, row packing, pre-quantization
  blending, texture reuse/disposal/recreation, plus 54 actual shipped-shader GPU
  checks (six keys x three saturations x three input colors, including out-of-range
  HDR channels and nonopaque alpha).
- First test run exposed a TEST ORACLE error: Texture2D.GetPixelBilinear's CPU lookup
  did not match D3D's normalized texel-center sampling. Replaced the oracle with
  explicit x=u*256-.5, clamped indices and exact row lookup. Did not weaken the
  tolerance or alter native LUT construction to make the test pass.
- Runtime compile passed: tmp/color-lut-compile.log. Corrected full isolated suite
  passed: tmp/color-lut-regression2.log, Unity exit successful. Includes color LUT54,
  Bloom/diffusion60, radial real-key7901+GPU, PostFilm, DOF focus+foreground80.
- Actual fixture is local-only tmp/color1176-worksheet.json, supplied via
  UMA_COLOR_TEST_KEYS; shader bundle still only UMA_POSTFILM_TEST_BUNDLE, not Assets.
- Local evidence: tmp/color-curve-utils.txt, color-gray.txt, color-update-texture.txt,
  color-setup-instance.txt, ColorCorrectionCurvesSimple-pass0-audit.txt,
  color-native-types.txt and register-full-color-audit.txt. Investigation helpers:
  tmp/inspect-color-correction.py, inventory-camera-tracks.py, disassemble-color-audit.py.
  PostFilmInspect/Program.cs now filters ColorCorrection; radial version preserved
  as Program.radial-backup.txt. Native metadata lacks original color timeline methods.

Next concrete work:
- Trace native RegisterPass color-correction ownership/order and its PrepareScreenMakeup
  masks before registering a simple-mode pass. Decode ApplyParameter/Execute and
  selective/advanced bypass conditions. Use six real Live1176 keys as a regression.
- The new helper is ready to be reused by that pass; do not rebuild LUT every frame
  when the selected discrete key has not changed. Main-track/simple mode can be
  delivered separately from advanced/multicamera paths once ordering is established.
- Full Live captures remain absent. No screenshot parity claim; no changes to the
  confirmed physics/DOF or the unresolved radial rectangle flags. Main editor kept
  running, and isolated validation has finished with no test process left active.

## 2026-09-20 heartbeat: color-pass order/RT evidence and spatial LUT regression

Native dispatch and ownership evidence (not merely feature field order):
- Extended tmp/register-full-color-audit.txt through RVA 0x1a38670. The previous
  0x23b0-byte dump ended inside RegisterPass; that was not the full function.
- Updated the local LibCpp2IL inspection to resolve actual native field offsets
  via GetFieldOffsetFromIndex; tmp/color-native-offsets.txt now records them.
  PostImageEffectFeature fields: DOF +0x50, radial +0x60, exposure +0x80,
  color correction +0x88, prepare-last +0x90, grading +0x98.
- Normal RegisterPass path loads DOF at RVA 0x1a364ac, radial at 0x1a36694,
  exposure at 0x1a36a64, color at 0x1a36b5b, prepare-last at 0x1a36c52.
  Color's reflected setup invocation is 0x1a36c1f, conditional successful append
  is 0x1a36c3e. This establishes conditional registration after DOF/radial/exposure
  and BEFORE prepare-last/grading, rather than guessing from metadata field order.
- Separate feature IsSimpleMode (Parameter +0x7e0, NOT color correction mode0)
  branches at 0x1a36063 to 0x1a370b8: color/grading-only registration path.
  Do not confuse the two meanings of simple mode or assume all passes always run.
- ColorCorrectionPass.Setup RVA 0x1a11570 reads Parameter.ColorCorrection +0x5d0,
  checks enable, updates LUT, and sets its OWN RenderPassEvent from +0x6c8.
  Its nested Parameter.Default RVA 0x1a243c0 writes event550 at 0x1a245de and
  clears MaskType/MaskPower. Event is not inherited from DOF in the native pass.
- Execute RVA 0x1a0fcd0 explicitly compares event with550 at 0x1a10109:
  <=550 hands the temporary result back via Feature.SetSourceRT at 0x1a101c5;
  >550 blits to the camera then releases its temporary (0x1a10167/0x1a10177).
  A future integration must preserve preceding radial output and handle late-event
  camera ownership; blindly copying radial's ResetSourceRT would discard its result.
- Evidence files: tmp/color-setup-dispatch.txt, color-execute-audit.txt,
  color-default-audit.txt, color-native-offsets.txt, register-full-color-audit.txt.
  These are local binary analyses, not proof that a complete scene already matches.

New spatial GPU test found an actual LUT defect missed by constant swatches:
- Extended Assets/Editor/LiveColorCorrectionRegression.cs with 257x9 independent
  RGB ramps, varying alpha and HDR endpoints, using all six real Live1176 curves.
  Tests curves alone and actual PostBloom_Rich -> ColorCorrectionCurvesSimple
  composition for Add/Screen and saturations0/1/1.4, not CPU-only simulated passes.
- Default Texture2D anisotropy was1; this project's current quality level uses
  ForceEnable. Under that setting a data-LUT lookup acquired spatial dependence
  on neighboring scene colors. Example real-key frame0: red input .003125 should
  map to0, but default anisotropy yielded .002655089 in the shipped GPU shader.
  Disabling anisotropy on the LUT alone fixed the discrepancy without changing
  curves, shader math, QualitySettings, or numeric tolerance.
- ColorCorrectionCurveLut now explicitly sets anisoLevel=0. This is a local
  data-texture sampling safeguard justified by the GPU result; it is NOT claimed
  to be an additional assignment recovered from native SetupInstance. Native
  SetupInstance doesn't show this explicit override in the inspected path.
- Test forces AnisotropicFiltering.ForceEnable and restores the prior setting in
  finally. It asserts the production LUT opts out; no test-only override hides
  the production setting. RGB tolerance stays .0015 and alpha is checked too.
- The first chain oracle incorrectly assumed Bloom writes alpha1. Local DXBC
  confirms PostBloom_Rich Add/Screen operates on xyzw, including alpha. Corrected
  the independent oracle; the curves pass preserves that composed alpha. Runtime
  Bloom/DOF code was NOT changed to accommodate the test.

Verification:
- tools/check_live_compile.ps1 PASS, tmp/color-spatial-compile.log.
- Final combined isolated GPU run PASS, tmp/color-spatial-final.log:
  124902 RGBA pixel checks and7058 samples demonstrating that moving curves before
  additive Bloom changes the result, plus previous54 curve swatches, Bloom60,
  radial7901 timeline samples/GPU, PostFilm timeline/GPU, focus/foreground80.
- Earlier failing diagnostic logs retained: color-spatial-regression.log,
  color-spatial-regression2.log, color-spatial-aniso0.log. Do not mistake these
  for the final result. No tolerance loosening or shader retuning was used.
- These remain small GPU fixtures, not full production render-context tests or
  real-Live screenshot parity. Color correction still has NO runtime timeline
  callback/pass registration; do not tell the user it is already visible.
- Next: implement main-camera simple/nonselective color dispatch with cut/seek/reset
  tests and native ordering/RT handoff now established. Authored blendCurve timing,
  advanced/depth/selective modes, masks/stencil, and multicamera require more evidence.
  Full real-Live capture work remains outstanding. Physics, DOF and unresolved
  radial rectangle timeline flags were not changed; main editors left running.
- Final isolated editor exited successfully (return code0); no isolated test process remains at this checkpoint.

## 2026-09-20 — Simple color correction connected to runtime Live chain

Scope and evidence:
- Implements the simple/nonselective/unmasked main-camera subset established by
  the previous native RegisterPass/Setup/Execute audit (see preceding section).
  Color keeps native default event550. Default ordering is DOF/Bloom/overlay ->
  radial -> color -> final camera resolve. This is no longer just a LUT fixture.
- New LiveColorCorrectionTimeline evaluates one authored group with discrete cuts.
  Uses active camera worksheet; absent/disabled tracks, pre-first time, invalid time,
  advanced/selective/masked keys or multiple groups bypass rather than guess.
  No blendCurve time interpolation was invented. Its meaning remains unresolved.
- LiveTimelineControl dispatches sampled state and Director subscribes/unsubscribes
  beside radial. Runtime reset and parameter shallow-copy include color state.
  Feature global IsEnable defaults true, so color readiness is independent of DOF
  readiness. Existing global rendering/active presentation camera gates still apply.

Implementation:
- New SimpleColorCorrectionPass owns a reusable linear RGB LUT and native kind159
  material. Curve-reference changes (cuts, seeks, re-enable) rebuild LUT; stable
  curves and saturation-only changes avoid re-upload. Disposal releases LUT/material.
  Authored curves are treated immutable; editing curve keys in-place at runtime is
  not supported by this cache contract.
- Production CommandBuffer draw explicitly sets LUT/saturation/mask-power0, disables
  mask keywords, sets neutral stencil, then unbinds LUT. It preserves prior source
  instead of discarding radial output. Unsupported/missing shader bypasses the pass.
- Color-only source/final passes run at550. For custom existing-effect events later
  than550, a color resolve at550 restores camera ownership before late DOF/radial;
  this avoids losing correction when radial resets source, or retaining a stale temp.
  GetColorChainSchedule exposes the actual production scheduling policy to tests.
- Fixed compiler namespace collision (Gallop.Math vs System.Math) during integration.
  Physics and DOF algorithms, Bloom constants and radial bit17/18 flags unchanged.

Verification:
- tools/check_live_compile.ps1 PASS: tmp/color-integration-final-compile.log.
- tmp/color-integration-gpu.log PASS: timeline six-key cut/backseek/cache/disposal,
  54 GPU swatches and124902 spatial RGBA checks now invoke the PRODUCTION
  SimpleColorCorrectionPass.Render command-buffer helper, not a direct-only Blit.
  Tests poison prior global uniforms and check LUT global unbinding after each draw.
- tmp/color-integration-final-gpu.log PASS, successful batch exit: adds21 combinations
  of DOF/radial/color readiness and events500/550/600. This checks source ownership
  using a stable-order model, NOT a full actual URP camera/render-context execution.
  Disabled-list, invalid-mode, multi-group, reset and cache tests also pass.
- Both combined runs also pass Bloom/diffusion60, radial7901 real timeline samples
  plus GPU, PostFilm timeline/GPU, DOF focus and foreground80 GPU regressions.
- Final parameter-copy assertion rerun PASS: tmp/color-integration-copy-gpu.log;
  successful batch exit with all combined regressions. No concurrent isolated runs;
  main Unity and physics editors untouched.

Limitations / next work:
- Full scene Live1176 screenshot comparison has NOT been run. Fixture success is
  not proof of matching the real game, actual camera orientation, or complete URP
  RT handoff. Existing real reference captures still need matching timeline/camera.
- Live1001 lacks a color group; do not expect this effect to change that track.
  Live1093 authored color keys are disabled. Live1176 is the useful enabled fixture.
- Unsupported color depth/selective/masks/multiple groups, blendCurve semantics,
  fog/tilt-shift, and radial rectangle flags remain separate evidence work.
- Next priority: real-Live isolated render-context/capture validation of the expanded
  chain; avoid claiming complete restoration or tuning against synthetic swatches.

## 2026-09-25 — Resume and close reference-capture contract validation

Recovered the interrupted Sep20 capture-preparation work and verified its final
isolated run completed successfully. No Unity process (main or isolated) was
running at this checkpoint; no editor was stopped or restarted.

Changes already present, now verified and recorded:
- LiveRenderCapture now accepts UMA_LIVE_CAPTURE_REFERENCE pointing specifically
  to a reference manifest.json, plus optional comma-separated
  UMA_LIVE_CAPTURE_FRAMES restricted to that manifest's timeline frames.
- LiveCapturePlan uses real manifest song/stage/cast/costume/fps/resolution data.
  This enables Live1176 preparation instead of always loading Live1001. Identity
  mismatches and frames beyond the loaded song duration fail explicitly.
  The manifest's video extraction offset is NOT applied again to timeline seeks.
- Saved capture plan and batch-exit intent survive the initial play-mode domain
  reload via SessionState. Start is batch-only; interactive use remains explicit
  Capture current Live. Neither entry point was invoked in the user's editor.
- Capture requires60 distinct camera-render frames at the requested timeline time
  (within0.1 timeline frame), with shader readiness and expected feature state.
  Duplicate callbacks, stale time, camera changes and unavailable shader state
  cannot satisfy settling. This prevents labeling an old rendered frame with a
  newly requested time. It does not establish deterministic cloth/particle history.
- Output includes capture-plan.json and per-frame requested/actual time diagnostics.
  Off/on still toggles the recovered feature as a whole, not all renderer features.
- New editor-only files: LiveCaptureContract.cs and LiveCaptureContractRegression.cs.
  Physics, DOF and rendering algorithms were not modified by this stage.

Verification actually completed (not pending):
- tools/check_live_compile.ps1 PASS, tmp/capture-contract-compile.log.
- First editor compilation detected ambiguous Gallop.Math/System.Math in the
  capture harness; fixed by qualifying System.Math.Max. Failed diagnostic is
  tmp/capture-contract-gpu.log; do not use it as final validation.
- tmp/capture-contract-final-gpu.log, Sep20 20:44 local: successful batch exit.
  Includes compilation of LiveRenderCapture/LiveRenderSetup and the new contract,
  actual Live1001 and1176 reference manifest parsing, identity/subset/reload checks,
  stale-frame/duplicate/readiness gate checks; all PASS.
- Same final run: color21 schedule combinations,54 swatches and124902 spatial
  pixels; Bloom/diffusion60; radial7901 samples andGPU; PostFilm timeline/GPU;
  DOF focus and foreground80 GPU samples all PASS. No new code changed after that
  successful run during this resume, so a redundant GPU run was not started.

Still outstanding:
- NO real full-scene Live was loaded or captured in this stage. The minimal GPU
  project lacks full Assets/resources and is only a regression harness.
- Next stage needs a separate full-asset isolated project and a local asset-loading
  path that does not access account/token configuration. Do not run Start with
  -quit: it launches asynchronous play-mode capture and exits itself on completion.
- Suggested first reference: master5/captures/reference/
  live1176-content-aligned-20260912/manifest.json, small subset308,868,3848 to cover
  authored color changes before expanding the full sequence. Compare actual cast,
  camera and time diagnostics before treating pixel differences as effect defects.
- Actual URP render-context handoff, camera orientation and real-game screenshot
  parity remain unverified. This checkpoint improves trustworthy capture plumbing;
  it is not a new claim of visual parity or a measured improvement in appearance.

## 2026-09-25: overbright-frame composition audit

- Traced authored Bloom/Diffusion worksheet key -> interpolation in
  LiveTimelineControl.SetupBloomDiffusionUpdateInfo -> Director callback ->
  GallopImageEffect.ApplyBloomParameter -> the active camera's shared
  PostImageEffectFeature.RuntimeParameter. Enabled flags and GraphicSettings
  choose one DofDiffusionBloomOverlay mode. Missing/disabled/no-current-key
  worksheet paths currently skip update; the matching game's reset behavior
  remains unproven, so these paths were not retuned or cleared speculatively.
- In DofDiffusionBloomOverlayPass.Execute, the Bloom/Diffusion/DOF modes are
  mutually exclusive switch cases. CreateBloomTexture applies the extraction
  once and hands its _Bloom RT to the selected composite. ScreenOverlayRender
  uses the Bloom-containing base or first-film pass only for layer1; layers2/3
  ping-pong the previous result through filmPass2nd, which does not re-sample
  _Bloom. The temporary result is handed to the feature at event <=550 and
  copied (not additively blended) to camera color by FinalBlit; late-event
  output writes camera color directly. The configured renderer asset lists
  exactly one Gallop post-effect feature and no URP Bloom Volume is created by
  GallopImageEffect. This excludes a second Bloom *in the inspected main-camera
  path*, not every possible bright scene source or untested camera transition.
- Added isolated actual-shader/production-Render.Blit RGB checks for first
  Bloom+Add film, followed by two Lerp film passes without another Bloom sample.
  Expected values: (.45,.5,.6), (.225,.25,.3), (.1125,.125,.15).
  Capture-side diagnostics now include Bloom/Diffusion enable flags, weight,
  radius, blend, diffusion controls and all three film modes/powers beside the
  time/camera/cast identity. These fields are not proof of pixel parity.
- tools/check_live_compile.ps1 passed (pre-existing warnings only). Isolated
  D3D11 batch test tmp/bloom-layer-d3d11.log exited0: capture contract,
  color/LUT, Bloom/diffusion, radial timeline/GPU, PostFilm timeline/GPU, DOF
  focus and 80 foreground GPU samples all passed. First isolated compile
  found a test-only missing namespace (fixed). A -nographics run used the Null
  device and yielded invalid GPU pixels; neither failed run is acceptance.
  Only tmp/dof-focus-test-project was run, and the user's main editor was not
  stopped/restarted. The main project lacks a Git metadata directory here.
- No strength, physics, DOF, or unresolved radial-rectangle flags changed.
  A full-asset isolated Live1176 off/on capture aligned to the master5
  reference manifest has NOT run: the minimal test project contains no Live
  scene/cast/stage resources. Do not claim the reported overbright frames are
  fixed. Next, capture matching real Live frames and compare camera/cast/time
  and these runtime parameters before interpreting luminance differences.

## 2026-09-25: post-film variant and Bloom-off follow-up

- Rechecked the four shipped Rich combination shaders directly from their D3D11
  fragment DXBC with `tmp/audit_postfilm_variants.py`. For every inspected
  ColorOnly, movie/mask, vignette, monochrome and inverse-vignette variant,
  the first film pass has one more *sampled texture register* than its paired
  second/third film pass. This includes PostDiffusionDofBloom_Rich passes
  11/12 versus 13/14. Its pass 8 base combination lists `_Bloom`, `_MainTex`
  and `_TapLowBackground`; the film-pass serialized `m_NameIndices` is
  incomplete, so register counts alone do NOT establish each register's name.
  Exact slot/name mapping remains unverified. All these official overlay passes
  have target blend Src=One/Dst=Zero, i.e. overwrite, not hardware additive
  framebuffer blending. The runtime PostFilmBlit uses first-pass output followed
  by second-pass ping-pong; it does not invoke the base pass as an extra step.
- In the Bloom-off DiffusionDofBloom branch, Execute queues a black `_Bloom`
  binding before the selected switch, and OnRenderImageDiffusionDofBloom does
  not replace it when `IsEnableBloom` is false. Its first film pass is still
  selected when layer1 is valid. This excludes one obvious previous-frame
  texture leak by code inspection and the isolated shader fixture below; the
  complete production render-context path has not been exercised by that test.
- Expanded `Assets/Editor/LiveBloomDiffusionRegression.cs` to put a bright
  texture in `_Bloom`, explicitly replace it with black, and check the first
  DiffusionDofBloom film pass. The fixture sets neutral diffusion parameters
  and restores `_TapLowBackground` plus other global state. Production shader
  and effect strengths were not modified. `tools/check_live_compile.ps1`
  passed; the isolated Editor assembly also compiled from its response file.
- The isolated Unity D3D11 process PID 21560 again stopped before creating its
  requested log, with CPU unchanged. Only that isolated process was ended; the
  user's editor PID 15476 was not touched. Consequently the new GPU assertion
  could not run in that sandboxed attempt. A subsequent approved, isolated
  D3D11 run did produce `tmp/bloom-disabled-gpu-approved.log` and exited
  successfully: the new Bloom-disabled DiffusionDofBloom first-film check,
  Bloom extraction/composition, three-layer PostFilm, radial, color and DOF
  GPU regressions passed. Only the user's editor PID 15476 remains. This
  validates the tested shader branch, NOT a full production frame.
- No full-asset isolated Live1176 frame exists yet. The reference manifest and
  PNGs (candidate frames 308, 868, 3848) are available in master5, but a
  legitimate parity check needs the matching cast/stage/song, timeline frame,
  camera and effect diagnostics from a separate full-asset capture first.
  Static evidence narrows duplicate composition in the inspected main-camera
  path; it cannot rule out another camera/feature, the authored Bloom intensity,
  HDR source values, color transforms or incorrect runtime RT handoff in an
  actual overbright frame. Do not lower strengths or claim parity based on this.

## 2026-09-25: confirmed active URP HDR baseline mismatch (evidence-based)

- `ProjectSettings/GraphicsSettings.asset` selects the GUID `be7a0efb84976e141aca289da4fd7832`, which resolves to `Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset.asset` in master7.
- The same asset GUID is active in master5. A line-by-line comparison of that asset found exactly one difference: master5 has `m_SupportsHDR: 1`, while master7 had `m_SupportsHDR: 0`.
- The Live prefab cameras already have HDR enabled (`m_HDR: 1`), and the inspected active asset is the one requiring depth/opaque textures and MSAA 4. Therefore the project-level HDR mismatch was a concrete render-baseline difference, not a Bloom strength guess.
- Changed only the active asset's `m_SupportsHDR` from `0` to `1`. No Bloom, DOF, physics, radial-rectangle flags, or shader strengths were changed.
- `tools/check_live_compile.ps1` had already passed before this asset-only change; it will be rerun after the change. A full Live frame comparison is still required. The open user Unity editor was not stopped or restarted.
- Interpretation remains conditional until a real Live capture is taken: this aligns master7's serialized baseline with master5, but does not by itself prove the overbright frame is fixed or prove complete game parity.

## 2026-09-25: continued from Claude session — character Toon property-block ordering

- Read the Claude session `b9c624ba-2c4a-4708-9d8a-320cb91d7634.jsonl` through its final 403 interruption. Its last useful direction was to verify the active URP/Cy baseline rather than tune effects from screenshots.
- Compared the corresponding `Director.cs` code in master5 and master7. Master5 explicitly uses `Renderer.GetPropertyBlock -> modify -> SetPropertyBlock` for GlobalLight and later character color tracks. Master7's Claude-edited code created a fresh block for each handler, set GlobalLight values, then let BgColor1 replace the whole block later in the same frame; it also wrote directional-light values through `renderer.materials` every update.
- Changed master7 to reuse one character MPB, call `GetPropertyBlock` before each handler update, and write `_UseOriginalDirectionalLight` / `_OriginalDirectionalLightDir` into the same MPB. This preserves rim/light values across timeline handlers and avoids per-frame material instantiation. This is a source-level alignment with master5, not a screenshot-based intensity adjustment.
- `tools/check_live_compile.ps1` passes after the change with only pre-existing warnings.
- Full Live visual validation remains pending; this change should be judged with a real Live capture, especially the formerly overbright frames. Physics, DOF, Bloom strengths, and radial blur rectangle flags were not changed.

## 2026-09-25: real-frame rollback after HDR test produced catastrophic clipping

- The user tested the HDR baseline change and supplied a real Unity Game view at approximately 23.3 seconds. The frame showed near-total white/clipped highlights, severe chroma expansion and loss of scene detail. This is a decisive negative result for enabling HDR in the current master7 runtime.
- Immediately reverted `Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset.asset` to `m_SupportsHDR: 0`.
- Also reverted the experimental Director MPB ordering change to the pre-test state so the user returns to the last known visual baseline rather than mixing two unvalidated changes. The MPB alignment remains a source-level hypothesis for a later isolated A/B test, not an active production change.
- `tools/check_live_compile.ps1` passes after the rollback with only the existing warnings.
- Conclusion: master5's serialized HDR flag cannot be copied directly into master7. The custom post chain or its intermediate RT/tone mapping differs at runtime; HDR must remain disabled until the actual target format, shader expectations and final conversion path are traced.

## 2026-09-25: bounded target preload evidence and phase inventories

- Added phase-only opt-in `bundle_inventory`, per-bundle `bundle_index`, and loaded-scene `scene_inventory`; counts distinguish opened handles, AssetBundle catalog entries, observed `LoadAsset` returns, and instantiated scene components. `GetAllAssetNames()` only enumerates catalog candidates. No physical, DOF, HDR, shader, or post-effect parameters were altered.
- Hash-verified target `GameAssembly` + metadata pointer lookup finds `Gallop.ResourcePath.GetCyalumeScorePath` at RVA `0x19ba070`. In `Director.GetLivePreloadEntries` the direct call at RVA `0x1a5c18f` feeds `AddByKey` at `0x1a5c19f`. Metadata literal: `Live/MusicScores/m{0:0000}/m{0:0000}_cyalume`. Master7 now conditionally adds that key to its existing preload list. Whether the key exists for a particular Live and how many dependencies it expands to need a real session log.
- Corrected attribution of earlier nearby native calls: `GetWorkSheetBySheetIndexAndVariationId`, `UmaAssetManager.LoadAssetBundle`, `AssetBundle.LoadAsset` and `PartEntry..ctor` were listed as targets to investigate, but are outside the bounded `GetLivePreloadEntries` function and must not be claimed as its direct callees.
- C# compile and a synthetic JSONL summary fixture passed. Real Live1176 full-load/playback log and same-frame visual parity remain unavailable, so this is an evidence/coverage increment, not a claim of visible quality improvement.
- Isolated D3D11 shader/DOF regression `tmp/stage1-inventory-gpu-20260925.log` completed with Unity batch exit code 0. It does not exercise the new full-Live preload/inventory path and does not establish real-frame parity.

### 2026-09-25 — Live resource root registration

The compiled viewer's progress denominator is expanded Bundle requests, not assets inside Bundles. Disassembly of `LiveResourceRegister.RegisterDownload` (RVA 0x1ab98b0) establishes that it registers the Director's roots **plus** each selected character's body/head/tail folders and face runtime resources before preloading. master7 used only the Director roots. `LiveResourceRegister.cs` now adds only metadata-present entries for the selected characters, with a `resource_roots` diagnostic category. This is a structural preload correction, not a cosmetic progress-counter adjustment. `check_live_compile.ps1` passed; no real Live session has verified resulting counts or visual impact. See `docs/LIVE_RENDER_PLAN.md` for exact reverse-engineering evidence, limitations and next measurements.

## 2026-09-26: per-root preload accounting (no rendering change)

Cross-checked the GLM progress disassembly against target `SearchAB` RVA 0x1b5d6a0 and master7's loop. The denominator counts dependency-expanded bundle requests per root, including repeated dependencies and cache hits, not assets inside a bundle. Target optional audio is omitted only when its `IsAssetReady -> TryGetReadablePath` test fails; that test includes pending/integrity handling and cannot be replaced with a naive `File.Exists`. Master7 does not yet implement that omission. Its extra laser-dependency fallback also remains unverified for the current song.

Added opt-in `preload_root` JSONL records and a summary of the 20 largest contributors with an accounting check against the progress denominator. `tools/check_live_compile.ps1` passed (existing warnings only); a synthetic JSONL summary assertion passed. This diagnostic-only change neither loads more resources nor affects physics, DOF or post-processing. There is still no real Live diagnostics session or full-frame comparison, so the actual gap to the binary viewer remains unmeasured. Run Live1176 in the already-open main Editor and compare root counts and repeated requests before making another loading or rendering change.

### Real Live preload validation: sessions 20260925-170518 and 20260925-170638

- The first opt-in real session is song 1176, stage 10148, 14 selected characters. It completed preload with 1,207 distinct roots (18 Director + 1,189 character roots), 1,388 unique dependency-expanded entries and **1,757 request attempts / UI target**. The per-root sum is exactly 1,757; 369 are repeated names across expansions. Outcomes: 1,373 bundle opens, 14 non-bundle loads, 370 handle reuses; no recorded missing files, load failures or exceptions in this preload. Count units are requests, not 1,757 distinct assets.
- A second real session is song 1001, stage 10102, 18 selected characters: 1,300 roots (20 Director + 1,280 character), 1,495 unique expanded entries, **1,882 request attempts / UI target**; 387 repeats. Outcomes: 1,473 bundle opens, 16 non-bundle loads, 393 handle reuses; no recorded preload failure. This independently confirms the request-count arithmetic, not target-binary parity.
- The Live1176 `preload_callback` inventory reports 2,690 loaded handles, which includes handles that were loaded before the counted preload; do not mislabel this snapshot as 2,690 opens by this session. Its sampled playback ran through frame 1830 (~30.5s), with `CameraTimeline1` and the `DiffusionDofBloom` mode in sampled frames. It does not cover reference frame 868 exactly or frame 3848 (~64s), nor does a pass log prove pixel equivalence.
- Result: the former two-digit progress was substantially due to missing selected-character resource roots, now corrected structurally. The counts are in the same *order of magnitude* as the user's observation of the binary viewer, but there is no exact same-config binary numerator/denominator or matched-frame visual test yet. Stage 1 resource-count measurement and stage 2 root-accounting portions have real evidence; full visual/render-chain acceptance remains open. Do not raise Bloom or change physics/DOF based on this count alone.
- Next checkpoint: obtain a target-viewer load count for song 1176/stage 10148 with identical 14-character selections, then capture master7 at frames 868 and 3848 and inspect actual active camera/RT/pass sequence and output. Keep registration as-is unless those measurements reveal a specific missing resource class.

## 2026-09-26: actual pass execution tracing (diagnostic-only)

The previous `render_pass` data only proves renderer enqueue. An additional opt-in `render_execute` record now marks command-buffer submission for SourceSetup, DOF/Diffusion/Bloom, RadialBlur, SimpleColorCorrection, ColorEarlyResolve and FinalBlit. It records per-camera/frame RT ownership transitions, without reading back pixels. Default is off unless `tmp/enable-live-pass-trace` (plus ordinary diagnostics) or the matching environment flag is set. The target disassembly confirms DOF's conditional `SetSourceRT` before command-buffer execution (RVA 0x1a19b90, call at VA 0x181a1a661); target RegisterPass has conditional exposure/grading branches that master7 does not yet implement. No runtime pass trace or matched screenshot has yet been collected. Compile passed; existing physics, DOF, HDR state and post-effect strengths remain untouched. See `docs/LIVE_RENDER_PLAN.md` for the acquisition plan and limits.

- The `render_pass` record now also includes URP's actual `cameraData.postProcessEnabled` flag (`urpPostProcessing`). The serialized Live prefab has `m_RenderPostProcessing: 0` on its camera entries, but only a same-frame runtime record can establish whether that remained off; do not infer URP Bloom absence from the prefab alone.

### Real Live1176 session 20260925-172955

The latest log confirms 1,803 bundle-request attempts and no preload failures, with URP built-in post-processing disabled on all 543 sampled pass-enqueue frames. It contains no per-pass execution records, because the optional trace marker was not enabled during that session. The marker has now been enabled for the next run and the target-frame execution window widened to ?6 Live frames to accommodate actual Editor playback cadence. No visual change or duplicate custom Bloom conclusion follows from this log alone. See `docs/LIVE_RENDER_PLAN.md`.

### 2026-09-26: real submitted pass chains validated (session 20260925-173827-227)

- Real song1176/stage10148/14 slots produced **34 render_execute records across 8 camera/Unity frames**. At Live frames 302,308,867,865,870,874 the submitted chain is SourceSetup -> DofDiffusionBloomOverlay -> SimpleColorCorrection -> FinalBlit. At frames 3845,3850 it is SourceSetup -> DofDiffusionBloomOverlay -> RadialBlur -> SimpleColorCorrection -> FinalBlit. Revisits around 868 have different Unity frames and must not be mistaken for duplicate passes within one frame.
- All 8 groups match their scheduled pass counts (4 or 5), contain no repeated recorded step, have identical preceding output/current input RT identifiers, and end at the initial camera RT. All 279 enqueue records have URP built-in post-processing disabled. This is evidence against whole-pass repetition or lost radial output **in these sampled frames only**. Exact reference frames868/3848 and pixel luminance were not captured.
- Added execution-chain reporting to `tools/summarize_live_diagnostics.py`, grouping by Unity frame AND camera, checking counts, adjacent RT continuity and camera return. Validated against the actual 34 records and a deliberately broken-handoff fixture. Saved complete report to `tmp/live-diagnostics-20260925-173827-227-summary.json`. No runtime renderer changes in this round; no Unity compile/GPU regression needed for the offline reporting change.
- Continued native internal audit: `OnRenderImageDiffusionDofBloom` calls BlurBlt at VA0x181a1c240, DiffusionFilterProcess at0x181a1c2ae, conditionally CreateBloomTexture at0x181a1c3a7, and PostFilmBlit at0x181a1c5c4; the source follows this outline. Native PostFilmBlit submits/clears at0x181a4cfb3/0x181a4cfc9 and0x181a4d192/0x181a4d19c. The current source likewise clears after these internal submissions, so the outer submission does not by itself prove repeated Bloom commands. Internal shader variants/parameters and pixel output still require verification; this is not visual parity.
- Next investigation can proceed using the recovered assets/binary: trace Bloom/Diffusion/PostFilm enable flags and numerical parameters at these timeline points, and verify actual Shader pass selection and intermediate texture formats. There is no reason to request another identical outer-chain playback run. A future visual capture should include intermediate images and parameter evidence together.


## 2026-09-26: autonomous continuation, provenance audit and corrections

This entry supersedes the overly broad exclusions in the preceding sampling notes.

### Corrections to prior conclusions
- `monitorCameraEnabled` was populated from `MonitorCameraSettings.IsEnabledTextureWidthRate`. It was NOT camera activation. Renamed the field to `monitorTextureWidthRateEnabled`; old logs cannot exclude monitor capture/compositing.
- `live_frame` formerly required an exact frame multiple of 30. Playback skipping those frames produced extremely sparse samples. Sampling now records the first observed frame in each 30-frame bucket (plus exact target frames), including backward bucket transitions. It still is not an exhaustive per-frame activation trace.
- Particle/Light counts are scoped to Director children, not the entire scene. The earlier session had 104 particles at 1/4 seconds, zero from 7 seconds in the observed samples. Neither result excludes particles elsewhere.
- PostFilm key counts prove track presence, not valid/enabled layers at a given time. The null camera targetTexture label likewise does not prove absence of SetTargetBuffers.

### Work performed without another user playback
- Exported all Bloom and three PostFilm tracks directly from the local game's son1176_Camera typetree to `tmp/live1176-effect-source.json` using master5's read-only asset utility. No Config.json was read.
- Added `tools/audit_live_bloom_source.py`: checks nine numerical fields against held-key intervals, uses the NEXT key's interpolation mode, skips curves instead of approximating them, and filters song identity. Six existing live_frame records in session20260926-005328-470 match all nine fields within 0.0001. Saved `tmp/live1176-bloom-source-comparison.json`. Injected-value mismatch and wrong-song negative controls pass.
- At frame3848 the bounding keys are3819/3972; next-key interpolation is None. Asset key3819 has threshold0.949999988, intensity6.690000057, blur2, weight1, diffusion threshold0.075000003, brightness/saturation/contrast1. Thus these sampled numbers were not invented intensity settings. This does NOT establish target-viewer evaluation parity, intermediate image correctness or absence of internal double composition.
- BloomBlendMode on this key is0 (Screen); master7 maps0 to `_BloomIsScreenBlend=1`. Added the runtime enum name to future diagnostic snapshots. Existing logs do not measure this uniform's actual draw-time GPU value.
- At the same interval PostFilm layer1 mode5/power4.159999847, layer2 mode5/power0.059999999; layer3 mode0 (disabled), although it retains a nonzero stored power. Counts alone were misleading.
- Replaced DofDiffusionBloomOverlayPass's reflection-based PostFilm dispatch with the concrete public typed method/ref arguments. Native direct-call evidence includes VA0x181a1c5c4 to0x181a4cad0. Retained null/error fallback and existing command ordering. Removes boxing/method discovery, NOT a claim of brighter/darker visual improvement.
- Removed an accidental literal-backslash-n diagnostic insertion from the disabled LegacyCompatibility timeline file. Active timeline accessors are retained.
- Investigated the missing movie/blend-factor transfer in Parameter.Setup but did NOT change it without target nested-method evidence; the renderer disassembly file does not contain this nested Setup implementation. Current color-only timeline bypasses UV movie tracks, so wiring texture fields alone would not restore movie rendering.

### Tests and remaining boundary
- `tools/check_live_compile.ps1`: passed (existing warnings).
- Isolated project, refreshed umamusume.dll: PostFilm timeline/GPU passed; DOF focus passed; foreground DOF80 GPU samples passed; Bloom/Diffusion24 extraction +9 Bloom +27 diffusion cases passed, including existing Bloom-once layer checks. These tests are NOT an end-to-end execution of the changed typed wrapper in a real Live scene.
- Initial PostFilm GPU invocation lacked UMA_POSTFILM_TEST_BUNDLE and failed its precondition. Retried with existing local tmp/postfilm-shaders.unity3d and passed. No shader assets downloaded.
- Added an explicit user-invoked menu `UmaViewer/Rendering/Capture current Live diagnostic frames (308,868,3848)`. It uses the actual currently loaded song/stage/cast, unlike the reference menu's default song1001 preset. It delegates to the existing settled off/on capture and restores playback/render-feature state afterwards. It is a same-session diagnostic capture, NOT a matched game-reference comparison. Not auto-invoked on the user's Editor.
- New capture entry compiled in the isolated Editor. Capture-contract test initially lacked its two reference-manifest environment paths; retry uses existing master5 live1001/live1176 manifests.
- Physics, DOF math, HDR state, Bloom/Diffusion strengths and radial unknown bits unchanged. Main Unity not closed/restarted/controlled.
- Next actual acquisition: in the loaded Live1176 choose the new diagnostic menu once. It saves six off/on images plus parameters/identity under captures/render-smoke. This is the first pixel-input/output checkpoint, not another full-song counter-only run. It does not yet capture individual internal Bloom textures. Before using game-reference images, verify exact identity and alignment.

- Final capture-contract retry: PASS, exit0 (tmp/continuation-capture-contract-retry.log). New menu was compiled; actual interactive capture remains unexecuted. All isolated Unity instances exited; main Editor remained running.


## 2026-09-26: capture identity failure fixed; session011553 examined
- User invoked diagnostic capture, but no captures/render-smoke directory was created. Editor exception points to LiveCapturePlan.Validate from BeginCurrentLiveCapture line106: Invalid reference character/costume identity. Builder retains full generic costume IDs with underscores; capture validation incorrectly allowed exactly one separator for the entire character+costume string.
- LiveCapturePlan.ParseCharacterIdentity now splits only at the FIRST underscore, validates numeric nonempty costume components, and retains the complete costume. Both validation and isolated automatic character selection use the same parser (the old selection also truncated costumes). Exact cast comparison remains strict; no costume normalization/guessing.
- Regression covers short and generic costume identities, invalid/empty/path-like values, serialization roundtrip, and rejection of a different costume. Runtime compile passed; isolated Editor compiled all changed editor sources and LiveCaptureContractRegression.Run passed, exit0 (tmp/capture-identity-regression.log). This verifies parsing, not successful real scene capture. No physics/DOF/render strength changes.
- Session live-diagnostics-20260926-011553-248 provides309 live_frame snapshots, showing the new sampling is substantially less sparse. Offline held-key audit compares300 snapshots:299 match all nine Bloom/Diffusion values; the sole mismatch is frame0 with initial/default parameters. This is not evidence by itself that the default was actually drawn, and it is retained in the report instead of silently excluded. Nine remaining frame snapshots are outside this held-key comparison coverage.
-30 submitted pass records form7 camera/frame groups; the existing chain checker reports no issues for these sampled outer chains. Still no pixel capture and no exclusion of internal composition faults.
- Correction to preceding note: PostFilm key3819 layer1 raw power4.16 is NOT the evaluated power at frame3848. The next key3874 selects Curve interpolation; actual sampled frame3844 power0.7138 and frame3870 power0.3087. Bloom's next key differs and selects None. Do not apply Bloom's held-key reasoning to PostFilm. At frame3844 recorded BloomBlendMode=Screen, film validity True|True|False, corroborating layer3 disabled in that sample.
- Required next user action: after editor script compilation, load Live1176 and invoke the same diagnostic capture menu. Successful completion must log [LiveRenderCapture] Completed and save six PNGs; an ordinary complete-song playback does not substitute for this capture. Main Editor was not controlled; only isolated verification was launched.


## 2026-09-26: successful real capture and off-screen preview UX
- Latest session captures/render-smoke/20260926_092541_878 completed all six PNGs and per-frame metadata (308/868/3848 off/on). Earlier incomplete folder is not the accepted set. User's "No cameras rendering" screenshot is explained by capture assigning the sole display camera's targetTexture to an off-screen RT; timeline is deliberately held for the 60-render stability gate. No Editor pause is requested by this tool. Completion is confirmed in Editor log, not inferred from the image.
- Added a utility preview showing the captured RT, target frame, off/on state and saved count, plus cancel/restore button and menu. Closing preview cancels. Added cleanup before script reload and guard against starting during delayed restoration. Game view itself still has no display-target camera during capture; this deliberately avoids injecting a second camera/composite pass into measurement. Preview UI has only compile validation, not interactive verification.
- Runtime compile passed; isolated Editor compiled the changed capture script; capture-contract regression passed exit0 in tmp/capture-preview-regression.log. No renderer/physics/DOF math changes; no need for another real acquisition solely to validate preview.
- Created comparison.png and pixel-summary.json in the successful capture folder. All source PNGs are1920x1080. Post-effects visibly add blue coloration/glow; pre-post frame868 already contains extensive white stage panels. Near-white(all RGB>=250) percentages off/on:308=6.92/4.08,868=24.46/18.34,3848=12.70/7.03. At868 any-channel>=250 rises30.43->36.54 percent. These are encoded output pixel counts, NOT linear luminance or proof of overexposure. White area alone cannot diagnose repeated Bloom.
- Off/on toggles the entire custom feature including DOF/color/film, not Bloom alone. Current real cast includes full0002_00_00 costumes; do not claim parity to reference manifests with different cast/costumes. Need internal-effect attribution and matched reference before changing strengths.


## 2026-09-26: captured-film attribution and native PostFilm/BlinkLight color synchronization

- The accepted real in-project capture is `captures/render-smoke/20260926_092541_878`: six 1920x1080 off/on frames at 308/868/3848, not an identical character/costume configuration to the compiled game's reference. The capture tool deliberately redirects the sole display camera offscreen, temporarily showing "No cameras rendering" in Game; completion was logged and six images saved. No new capture is required merely for the earlier preview repair.
- Added `Assets/Editor/LiveCapturedFilmAttribution.cs`, a **film-only** GPU experiment on SHA-256 pinned real *pre-post* PNGs, using the shipped `PostBlit_Rich` passes 1/3 and checking the later-pass arithmetic against `PostDiffusionDofBloom_Rich` pass 13. The three tracks' authored `AnimationCurve`s produce all nine captured runtime powers within 0.0002. Four counterfactual **raw hardware depth samples**, ARGB32 ping-pong, ARGBFloat pre-clamp probes, and three omit-one-layer renders per frame generated 72 outputs. Initial report: `captures/render-smoke/20260926_092541_878/film-attribution/report.json`; reproducible input generator: `tools/prepare_live_film_attribution.py` (rebuilt input exactly equals the original fixture). `tmp/film-attribution-gpu.log` exited 0. This is **not** captured scene depth, final Bloom/DOF/color correction, or game-reference parity.
- In that counterfactual at rawDepth=0, frame868 layer2 authored Add puts 19.65% of pixels above 1 in at least one float RGB channel; after clamped layer2, layer3 Add puts 46.93% above 1 before its output clamp. Thus film arithmetic alone can contribute clipping **without Bloom**; it is not proof of actual overexposure or grounds to turn down Bloom blindly. The visual sheet is `captures/render-smoke/20260926_092541_878/film-attribution/contact-depth0.png`.
- Native `SetupPostFilmUpdateDataInfo` RVA `0x1ae70c0` (`tmp/postfilm-setup-update-audit.txt`) checks current-key sync bits `0x200000/0x400000/0x800000/0x1000000` for corners 0..3 (getter RVAs `0x1af7c30/0x1af6370/0x1af62e0/0x1af6330`). It fetches the current StageController BlinkLight RGB by key name/hash/container (`TryGetPostFilmBlinkLightColorRGB` RVA `0x1ae8420`, StageController lookup `0x1a8f110`), applies **the PostFilm key's** `BlinkLightBrightnessPower` and `IsAdjustedBlinkLightColor` (`ApplyOfficialBlinkLightColorRGB` RVA `0x1add210`), copies RGB only, and preserves each authored alpha (`CopyPostFilmColorRGB` RVA `0x1adf9f0`). Crucially, during interpolation **a next key with that corner's sync bit holds the current corner** rather than lerping to the next key's authored color. Previous master7 code always lerped and never resolved live BlinkLight RGB. This is a real source-backed omission, not an aesthetic adjustment.
- Implemented optional raw-color resolver and native sync/next-sync hold in `LivePostFilmTimeline`, bound it to the stage's existing `StageBlinkLightDriver` in `LiveTimelineControl` without an every-frame scene search or clock-order rewrite. Missing lookup falls back to authored current RGB; per-layer `none|resolved|missing` and color0 RGB are available in opt-in `LiveRuntimeDiagnostics`. The capture metadata now saves `filmBlinkLookup` and all three `filmColor0` RGBA. The dedicated user-facing menu `UmaViewer/Rendering/Capture current Live BlinkFilm diagnostic frames (900,1240,3900)` targets authored sync intervals for Live1176; its resulting identity and metadata must still be checked. These frames are **not** covered by the prior six PNGs.
- The original 3848 capture is in a **next-key sync transition**: even without dynamic Blink RGB, holding the current authored layer1 color changes the film-only rawDepth=0 output. Old vs new frame3848 layer3 encoded RGB absolute mean errors were ~13.68/8.69/0; frame308 and 868 outputs unchanged. New report and old/new side-by-side: `captures/render-smoke/20260926_092541_878/film-attribution-sync-v2/`; new GPU test exited 0 (`tmp/film-attribution-sync-v2-gpu.log`). This is a pre-Bloom film counterfactual, not evidence that the complete real Live frame now matches game reference.
- Native `DofDiffusionBloomOverlayPass.Allocate` RVA `0x1a16b50` explicitly requests ARGBHalf (format 7), whereas `RenderTextureHandle.GetTemporaryRT` RVA `0x1a47440` takes the default-format overload: **mixed internal formats are intentional**; disabling camera HDR is not a reason to force all intermediates to ARGB32. `ScreenOverlayRender.Parameter.Setup` RVA `0x1a46820` omits ColorBlendFactor/movie-info fields, as does the current implementation; native `Director.UpdatePostFilm` RVA `0x1a6aea0` sets the factor in its own path. Do not invent a Setup copy based on field names alone.
- Validation: `tools/check_live_compile.ps1` passed with only pre-existing warnings. Isolated Unity 2022.3.62f1 GPU/fixture regressions passed: PostFilm sync, nine film powers/72 counterfactual PNGs, 80 DOF foreground samples, DOF focus, Bloom/Diffusion, ColorCorrection, capture contracts (`tmp/postfilm-blink-sync-gpu.log`, `tmp/postfilm-sync-LiveDofForegroundRegression-Run.log`, `tmp/postfilm-sync-LiveDofFocusRegression-Run.log`, `tmp/postfilm-sync-LiveBloomDiffusionRegression-RunGpu.log`, `tmp/postfilm-sync-LiveColorCorrectionRegression-RunGpu.log`, `tmp/postfilm-capture-metadata-contract.log`). An intermediate Bloom invocation initially failed only because its required shader-bundle environment variable was absent; it passed on retry. **No complete Live scene ran in the isolated project.** Main Unity Editor PID22128 was neither closed nor restarted.
- Remaining: verify the stage driver actually resolves `pfb_env_live10148_blinklight_dummy_b` at sync frames and whether its LateUpdate cache is one frame behind the native StageController snapshot; do **not** reorder callbacks just by intuition. Real synced RGB and full-frame parity need an actual capture at the sync window with the above lookup/color metadata. Match character/stage/camera identity before making any visual claim. Bit17/18 radial rectangular flags remain unconfirmed and disabled. Physics, DOF algorithm, HDR switch, Bloom/PostFilm strengths unchanged.
- The existing `tmp/live-diagnostics-20260926-011553-248.jsonl` has a real `instance_create` for `pfb_env_live10148_blinklight_dummy_b` (two renderers). This supports its asset being present, **not** yet successful `StageBlinkLightDriver.TryGetCurrentBlinkColor` lookup or correct clock freshness. The new capture status distinguishes that runtime lookup from mere asset presence.

## 2026-09-26: BlinkFilm 真实帧取证与 PostFilm 单独归因

- 新会话 `captures/render-smoke/20260926_101523_651` 的计划和六张 PNG/元数据完整：Live1176、舞台10148、1920×1080、14 人、帧 900/1240/3900。`filmBlinkLookup` 分别为 `none|resolved|resolved`、`resolved|resolved|none`、`resolved|none|resolved`。因此原先“资源实例存在但不知灯色是否查到”的问题，在这些静止稳定帧已证实查到；**不代表播放中的转换帧时钟一致**。源数据 `tmp/live1176-effect-source.json`：900 层2/3 当前 key 均 bit21+bit20 且亮度1，实测 Color0 同为 (0.172549,0.466667,0.819608)；1240 层1亮度0故 Color0黑、层2仍用相同 RGB；3900 层1 Color0=(0.570755,0.733868,1)，层3 Color0=(0.194057,0.249515,0.34)=同一原始 RGB×0.34，**但层3 mode=None 不参与输出**。以上与实际 key 的同步位/亮度一致。
- `D:\Projects\UmaTools\UmaViewer-master5\captures\reference\live1176-content-aligned-20260912\manifest.json` 虽也有 10148/14人/1920×1080/60fps，身份却是 `1062_50,1067_02,1068_02,1006_00×11`，而本次是 `1086_0002_00_00,1034_0002_00_00,…`。它没有这三个目标帧（最近如 888/918 和 3898/3918），另外的 live1176 参考有18人，**不可据此做同身份、同相机逐像素一致性结论**。
- `captures/render-smoke/20260926_101523_651/blink-film-capture-pixel-summary.json` 与 `blink-film-capture-comparison.png`：off→on 的编码 RGB 均值分别由 (61.96,65.66,60.56)→(77.27,94.50,113.92)，(54.69,59.60,54.07)→(71.79,94.64,124.16)，(52.49,56.72,53.07)→(53.75,65.56,72.23)。这包含所有后处理和 DOF，不是 Bloom-only，也不是线性光照值。
- 扩充 `tools/prepare_live_film_attribution.py` 及 `Assets/Editor/LiveCapturedFilmAttribution.cs`：只从捕获元数据里亮度=1、未调整且当前 key 的 Color0 同步成功的层恢复原始舞台 RGB，再用官方 Shader 和真实预后处理图像复演三层；GPU 检查全部九个功率及九个 Color0 与真实元数据误差小于0.0002，官方 film variant parity 通过。输出 `film-attribution-blink/report.json`、72 张层/省略层图、`real-vs-film-depth0-contact.png`，隔离测试 `tmp/blink-film-attribution-gpu.log` exit0。rawDepth=0 仅**假设深度**，不代表本次 Live 的真实深度/叠加位置。视觉上 900/1240 的蓝色罩层在 Film-only 实验已显著出现，**不能直接归咎 Bloom 重复叠加**；on 帧仍比 Film-only 更发光/模糊，不能据此排除 Bloom/DOF/扩散误差。
- 静态次序复核：`LiveTimelineControl.AlterLateUpdate` 当前明确在 `AlterUpdate_PostFilm` 后才调用 `AlterUpdate_BlinkLight`；原生 RVA 0x1ac9460（GLM 报告 §10.3）亦列 PostFilm 在 BlinkLight 之前。报告同时将 master7 描述为 Blink→PostFilm 的那句话与实际代码冲突，不能以此为依据盲目重排。StageBlinkLightDriver 的 LateUpdate 缓存与原生 StageController 时钟的边界仍需过渡帧/运行时证据；静止60次渲染只能排除稳定帧 lookup missing。
- 增加独立的 **off/noFilm/on** 诊断菜单（900/1240/3900）：在仅该主相机的 `beginCameraRendering` 与 `endCameraRendering` 间暂时把三层 PostFilmMode 置 None，保留 DOF/Bloom/Diffusion/ColorCorrection；离开该相机或异常结束立即还原原始结构体。原有 off/on 菜单行为不变。`on−noFilm` 能在完整后处理输入上归因 Film，`noFilm−off` 归因其它链条（**不是 Bloom-only**）；目前尚未采集三态真实 Live，故没有新渲染改善结论。编译 `tmp/film-isolation-compile.log` 通过，隔离 Editor 脚本编译/采集契约 `tmp/film-isolation-contract.log` exit0；主 Editor PID22128 未控制/重启，物理/景深/强度未修改。
- 下一关键动作：等待三态菜单在主 Editor 同配置 Live 上运行，核对各帧九张 PNG 的身份/稳定门禁及 noFilm metadata，再定位过亮在 Film 还是其它链条。若想与游戏逐像素比较，还需另行制作**相同角色服装及准确帧/时钟**的原版参考；不以不匹配视频作“改善”的依据。


### 2026-09-26: three-state capture scope audit (no Live visual claim)

- Audited local URP 14 source: `beginCameraRendering` precedes `renderer.AddRenderPasses`; the renderer then runs `Setup`/`Execute` and calls `context.Submit()` before `endCameraRendering`. `PostImageEffectFeature.AddRenderPasses` binds its `CameraData.Parameter` to the shared runtime object; `DofDiffusionBloomOverlayPass.Execute` reads the same object's overlay modes. Thus camera-scoped `noFilm` mode suppression reaches pass selection and pixel rendering, rather than only a late metadata snapshot. This is a code-path argument, not a full-Live GPU observation.
- Hardened `Assets/Editor/LiveRenderCapture.cs`: `noFilm` stability gate now requires a matching begin/end camera suppression for *each* settled render; a missing callback cannot silently produce a mislabeled `noFilm` PNG. Restoration rewrites only the three `PostFilmMode` fields, preserving other timeline-owned parameters written by callbacks. Cancellation still restores before releasing the RT; on/off paths are unchanged. Suppressed mode is restored before metadata is written, so the metadata's `film1/2/3` fields continue describing the original authored modes; `filmIsolation=true` identifies the temporary render-only override.
- Added isolated `LiveCaptureContractRegression` assertions for all three overlay validity gates, retained strengths, nested suppression rejection, preservation of a concurrent Bloom field update and idempotent restoration. `tools/check_live_compile.ps1` passed (existing warnings); isolated Unity batch test exited successfully in `tmp/film-isolation-scope-contract.log`. The isolated test does **not** establish that all nine full-Live PNGs are distinct or visually correct.
- Next hard boundary: one actual Live1176/stage10148/14-cast run of `UmaViewer -> Rendering -> Capture current Live off/noFilm/on (900,1240,3900)`. Compare the nine PNGs and identity/`filmIsolation` metadata; interpret `on - noFilm` as film contribution only if the same-frame experiment succeeds. `noFilm - off` includes the entire remaining DOF/Bloom/Diffusion/ColorCorrection chain, not Bloom alone. Do not change effect strengths from unmatched reference images.


### 2026-09-26: Live1093 user-observed stage occlusion, haze and floor flicker

- The user prefers the **middle Film-only** panel of `captures/render-smoke/20260926_101523_651/film-attribution-blink/real-vs-film-depth0-contact.png`. That panel is a counterfactual official-Shader replay of pre-post frames with `rawDepth=0`, not a real full-Live post chain. Preserve this preference as a visual target, but do not turn off Bloom/DOF or dial brightness by eye on this basis.
- The two user images show our Live1093/stage10137 at roughly 20.1 s with a conspicuous orange semi-transparent surface over the front of the stage and blown-out highlights; the real-game example includes warm shafts/scattering and readable character detail. Their character outfits, camera clock and exact frame are not proven matched, so this is a strong *failure report*, not pixel-parity evidence. The user also reports persistent floor flicker consistent with depth fighting; the actual pair of triangles/materials is not yet identified.
- Existing live diagnostic `tmp/live-diagnostics-20260926-024319-599.jsonl` identifies stage10137 instances `alpha_mask`, `alpha_mask_side_stage`, `sunset`, `main000`, `horizon`, multiple blink lights. At frame1202 (20.027 s), the timeline reports BloomIntensity=7.36, diffusionBright=1 and PostFilm modes Add/Lerp/Add. These are authored/runtime values, **not** evidence that Bloom or one particular mask caused the orange overlay. `multiCameraActive=0` there. The diagnostic has no renderer-level geometry/material state, so it cannot identify the front surface or overlapping floor pair.
- Found a concrete defect in our *ground-panel exclusivity*, independently of the user's unidentified floor mesh: `StageBlinkLightDriver.LateUpdate` previously called `EnforceGroundPanelExclusive` **before** `ApplyToRootCached` for every cached root, so the losing panels were immediately re-enabled in the same frame. Also, `ApplyRootOffCached` set renderers enabled and usually only blackened MPB colors, which can still write depth. Moved exclusion after the per-root updates and set `forceRenderingOff=true` for non-selected ground-panel renderers. This affects only `_blinklight_ground_panel` groups; it is a candidate fix, **not yet a verified resolution** of the user's stage floor z-fighting. Other coplanar meshes can still conflict.
- Added a one-time, opt-in read-only stage-surface snapshot to `LiveRuntimeDiagnostics.RecordFrame` near frames 1190?1220: renderer hierarchy/name, mesh, material/shader, queue, ZTest/ZWrite when exposed, active/render-off flags, world bounds, plus bounded candidates for simultaneously visible thin coplanar renderers. It uses `sharedMaterials` and does not alter scene/material state. The next naturally occurring diagnostic Live1093 session can reveal the orange-mask state and candidate floor pair **without requiring the off/noFilm/on nine-image capture**. An identical-bounds candidate is not proof of coincident triangles; confirm mesh coordinates and material pass before moving or hiding other surfaces.
- `tools/check_live_compile.ps1` passed after both changes (`tmp/stage-ground-panel-compile.log`). In isolated `tmp/dof-focus-test-project`, copied the validated assembly and ran `LiveDofFocusRegression.Run` with Unity batch exit0, `tmp/stage-ground-dof-gpu.log`. This does not test full Live1093 rendering or verify the new ground-panel behavior in the loaded stage. Main Unity Editor was not closed/restarted; physical simulation and DOF algorithm were untouched.
- Remaining evidence path: obtain renderer-level snapshot on a *natural* next playback, distinguish `alpha_mask[_side_stage]` from `sunset` and translucent material render queues/depth tests, identify floor pair rather than offsetting the stage. Separately inspect actual Live1093 SunShafts/LightShafts/VolumeLight keys and the native pass texture handoff; the reference's haze is not license to add a generic fake beam. Do not ask the user to re-run a capture solely for this note; they explicitly declined another operation now.
- Added an isolated Unity Editor regression `tmp/dof-focus-test-project/Assets/Editor/StageGroundPanelRegression.cs`: two synthetic cached ground-panel roots sharing a mesh, distinct update frames, and a direct call to the exclusion method. `tmp/stage-ground-panel-regression.log` exits 0 and verifies the losing renderer has `forceRenderingOff=true` while the latest is not suppressed. This validates the corrected exclusion action, **not** the real Live1093 floor mesh identity, timeline order under gameplay, or the original game's rendering intent.
- Native disassembly corroborates an explicit `StageController.InitializeSunShaftsControl` (RVA `0x1a863b0`) and `SunShaftsPass.Execute` (RVA `0x1a4fe30`) pipeline with viewport projection, temporary RTs and source-RT handoff; master7 currently has serialized `sunShaftsSettings` but no attached equivalent pass. The next diagnostic `live_frame` now logs whether SunShafts settings exist, authored power/intensity/blur and indirect shaft texture count, without synthesizing new beams. No assertion yet that those settings are active for Live1093.


## 2026-09-26: Live1093 natural playback follow-up ? stage surface evidence

- Parsed the completed `tmp/live-diagnostics-20260926-032402-136.jsonl` session (song1093, stage10137, 51 sampled Live frames). The one-shot surface snapshot is at Live frame1201 / 20.022 s and contains 378 renderer/material records. Camera position was (7.145, 1.151, 12.183), FOV33.94; no active monitor camera in the sampled frame. Bloom intensity7.36, diffusion disabled, PostFilm Add/Lerp/Add powers1.4915/0.5720/0.0079. These are runtime state readings, **not** visual attribution of the blown-out pixels.
- **Correction to the prior floor hypothesis:** no `_blinklight_ground_panel` name occurs anywhere in this session's resource/instance/surface records. Thus the earlier ground-panel exclusivity fix is valid for stages using that family but cannot explain the reported Live1093 floor flicker. Do not cite it as a Live1093 visual repair.
- Only one coplanar-bounds candidate was emitted: `pfb_env_live10137_main000(Clone)/mirror000` versus `/shadow`. Both are enabled/visible, y=0.001 m, queue3000; mirror bounds X62.38/Z21.85, shadow X16.05/Z0.424 with XZ overlap. The pair consists of **different meshes** (`ReceiveMirror`, `Shadow`), so matching AABBs do not prove overlapping triangles or actual depth competition; it may be an intended reflection/shadow overlay. Main stage `/ground_top`, `/ground_front`, `/bg_ground` have tall AABBs and were deliberately excluded by the planar-bounds filter. They are not yet cleared as suspects. No arbitrary floor offset or renderer suppression has been applied.
- The stage includes transparent `light004_sunset_glow` (shader `LightBlinkBlend`, queue3000, bounds center (0,3.565,4.75), size (20.575,7.13,16)) and several side sunset planes. `alpha_mask` meshes use `Hidden/Gallop/3D/Live/Stage/Alpha`, queue3000. An AABB and shader name are insufficient to identify the orange foreground image; the supplied real-game example is not the same camera/character identity as the Unity screenshot.
- Live1093's static timeline settings have `sunShaftsSettings != null`, intensity0.5, **sunPower0**, blurRadius5/2 iterations; indirect shaft texture count0. This establishes data presence, not an active original SunShafts pass or a cause of the real game's visible haze. Avoid synthesizing fog/shafts based on the screenshot alone.
- Extended the opt-in, one-shot stage snapshot with effective material `_SrcBlend`/`_DstBlend`, `_ReflectionRate`/bound texture, `_Color`, sorting order and **world-space horizontal-triangle height histogram** for named ground/mirror/shadow meshes. This separates a tall floor AABB from true horizontal triangles and tests whether the mirror is actually bound; it does not alter rendered pixels. The next natural Live1093 playback can fill these missing observations. `tools/check_live_compile.ps1` succeeded; isolated Unity `LiveDofFocusRegression.Run` passed in `tmp/stage-surface-material-dof.log` (exit0). Main Editor PID22128 was not restarted; physics and DOF algorithms unchanged. A real same-camera image/controlled renderer attribution remains necessary before a visual fix can be claimed.


## 2026-09-26: Live1093 natural playback follow-up ? stage surface evidence

- Parsed `tmp/live-diagnostics-20260926-032402-136.jsonl` (song1093, stage10137; 51 sampled Live frames). The surface snapshot is at frame1201 / 20.022 s and has 378 material records. Camera (7.145,1.151,12.183), FOV33.94; sampled Bloom intensity7.36, diffusion off, PostFilm Add/Lerp/Add powers1.4915/0.5720/0.0079. These values do not prove the source of clipped pixels.
- Correction: `_blinklight_ground_panel` does not occur anywhere in this session's resource/instance/surface records. The earlier ground-panel exclusion fix cannot explain Live1093 floor flicker, though it may help stages with those panels.
- The sole thin/coplanar-bounds candidate is `pfb_env_live10137_main000(Clone)/mirror000` versus `/shadow`, both visible with bounds at y=0.001 m, queue3000. They have different meshes/shaders (`ReceiveMirror`, `Shadow`): common bounds do not prove triangle overlap or depth fighting and may be intended layering. The `/ground_top`, `/ground_front`, `/bg_ground` meshes have tall bounds and were excluded by the previous planar-AABB heuristic; they are not cleared.
- A transparent `light004_sunset_glow` (LightBlinkBlend, queue3000) and several side sunset planes exist. The `alpha_mask` meshes use a separate Alpha shader. Bounds/shader names do not identify which one makes the orange foreground area. The two supplied screenshots are not same-camera/character pixel references.
- Timeline has `sunShaftsSettings` intensity0.5, **sunPower0**, blurRadius5/iterations2; indirect shaft texture count0. Presence is not evidence of an active pass at this frame. Do not synthesize haze or tune Bloom from this data alone.
- Added one-shot diagnostic material source/destination blend, reflection rate/texture, color, sorting order and world-space horizontal-triangle-height histograms for named floor/mirror/shadow meshes. The added sampling is read-only; later natural playback can distinguish true horizontal overlap and mirror binding. `tools/check_live_compile.ps1` passed; isolated Unity DOF regression passed in `tmp/stage-surface-material-dof.log` (exit0). Main Editor was not restarted; physical/DOF algorithms unchanged. No complete same-camera visual parity claimed.
- Additional isolated `StageSurfaceHeightRegression.Run` passed (`tmp/stage-surface-height-regression.log`, exit0): horizontal world-space 1 mm triangle counted while a sloped triangle was excluded. It tests the probe, not the actual stage geometry.
