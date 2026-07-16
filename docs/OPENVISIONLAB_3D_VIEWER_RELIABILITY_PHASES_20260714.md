# OpenVisionLab 3D Viewer Reliability Phases

Updated: 2026-07-16

Status: current Viewer-reliability planning and claim-control source of truth. Use this document together with `OPENVISIONLAB_3D_PRODUCT_TARGET_AND_SELF_EVALUATION_20260711.md`; this document controls the order and exit gates for Viewer trust work.

## Decision

Viewer reliability is not one percentage. It has three different meanings that must not be combined:

1. **Phase 1 - Software and visual reliability:** the same supported file and user action produce stable display, interaction, evidence, and failure behavior.
2. **Phase 2 - Geometric and algorithm reliability:** different valid datasets, sampling patterns, and known transforms produce independently correct geometric results.
3. **Phase 3 - Physical and metrology reliability:** displayed and calculated values are traceable to calibrated physical units with uncertainty and independent metrology evidence.

Current status:

| Phase | Current decision | What that means |
| --- | --- | --- |
| Phase 1 | **Passed for the fixed supported scope locally and in current Windows CI** | The fixed Viewer matrix, host boundary, external interchange, deterministic full-query display-density, selected-point provenance, current-versus-next-Preview density state, hosted dual-capture, and mandatory real WPF pointer-input gates pass. |
| Phase 2 | **Not passed; current policy gates passed locally and in Windows CI** | Both fixed NIST physical instances, the Stanford published transform, the synthetic difficult-geometry matrix, and the runtime-neutral registration acceptance policy pass. Approved runtime/result mapping and Viewer/Runner registration parity remain open. |
| Phase 3 | **Blocked / unverified** | C3D physical mapping metadata, calibration provenance, uncertainty, repeated-scan evidence, and licensed metrology comparison are unavailable. |

The accurate current claim is:

> OpenVisionLab 3D has a repeatable fixed-scope engineering Viewer baseline and two independently cross-checked fixed nominal/actual inspection slices. It is not yet a general-purpose geometric validator or a calibrated metrology system.

## Phase 1 - Software And Visual Reliability

### Goal

Prove that supported Viewer workflows are deterministic, inspectable, hostable, and resistant to malformed input without confusing display sampling with measurement sampling.

### Exit Checklist

- [x] C3D, GLB, STL, LAS, and LAZ fixed positive/controlled-failure matrix passes in Viewer and Shell.
- [x] Orbit, pan, zoom, fit, visibility, picking, two-point measurement, overlays, legends, and essential Viewer HUD facts have current smoke evidence.
- [x] Source, Preview, and Published result entities remain separate; visibility changes never run Preview.
- [x] Missing and corrupt inputs fail in a controlled state; screenshot-quality gates reject invalid frames.
- [x] C3D display-frame mapping and external CloudCompare interchange pass for the fixed sample.
- [x] Fast/Balanced/Detailed nominal/actual display sampling changes independently while full-query metrics and published evidence remain identical.
- [x] Separate Viewer DLL, manifest, Host API, zero-`ProjectReference` BinaryHost, and Windows CI gates pass.
- [x] Picking a nominal/actual colored point shows its ordered query-point index, actual/query source IDs, signed deviation, unsigned distance, nearest nominal triangle ID, and tolerance status in standalone Viewer and Shell evidence. The original actual STL has no proven one-to-one vertex index, so the UI does not invent one.
- [x] Changing render density after a completed comparison clearly distinguishes the current displayed sample from the density selected for the next explicit Preview; it does not auto-run Preview.
- [x] Pointer-driven orbit/pan/zoom/pick regressions have repeatable automated input evidence rather than only command-driven smoke state.
- [x] A hosted smoke that requests both embedded Viewer and full-Shell evidence has one lifecycle owner, applies workflow actions once, captures both surfaces sequentially, and fails if either quality gate fails.

### Current Assessment

Phase 1 is **passed for the fixed supported scope locally and in the current Windows CI workflow**. This means the current supported data, display, host, evidence, failure, dual-capture, and pointer-interaction paths have current-source regression evidence. It does not establish arbitrary-data geometric correctness, calibrated physical accuracy, metrology certification, or portability to every Windows desktop/session configuration.

### Selected-Point Provenance Gate

Passed locally on 2026-07-14 for the fixed NIST identity-frame slice:

- `NominalActualDeviationSample` preserves the ordered query index, query position, closest nominal point, nearest source-triangle index, unsigned distance, signed deviation, and direct/robust sign path for display samples only. Full-query metrics remain independent of render sampling.
- `NominalActualComparisonViewModel` owns selected-point state, tolerance classification, presentation, and stale-state clearing. The View/OpenGL code-behind only performs the pointer ray test, draws the selected-point evidence, and bridges current state to the host.
- A real Balanced Viewer pointer-ray smoke selected query point `2,724,128` at `(6.270, 3.926, 4.603) mm`, signed deviation `-0.39734458923339844 mm`, unsigned distance `0.39734458923339844 mm`, nearest nominal triangle `725`, and `Below lower tolerance` status. The contract retains actual source `source.nist-overhang-x4-actual-part1` and query source `query.nist-overhang-x4-cloudcompare-vertices`.
- Standalone Viewer and full Shell screenshots pass the shared pixel-quality gate and show the same selected evidence in the Viewer HUD, Tool/Inspector, and linked state. The first Shell attempt that omitted the configured pick is preserved as rejected functional evidence; the Shell-only smoke path now invokes the same configured Viewer pointer selection before capture.
- Current evidence: `artifacts/nominal_actual_selected_point_20260714`. The executor/result golden passes `27/27`, ViewModel verification passes `65/65`, fixed Viewer/Shell regression passes `128/128`, and BinaryHost passes manifest `13/13`, outputs `12/12`, and Host API commands `3/3`.

### Display-Density State Gate

Passed locally on 2026-07-15 for the fixed NIST identity-frame slice:

- View first exposes `Current display` and `Next Preview` in the standalone density control, Viewer HUD, Viewer Inspector, Shell `Data & Layers`, Shell `Tool / Inspector`, and linked deviation summary. `NominalActualComparisonViewModel` owns the applied/next density names, budgets, summaries, and pending state; no Core/Data/Tools model change was required.
- Preview requests snapshot density and display budget together. Changing the global density after a published Balanced result leaves the current `59,487` samples and stride `71` unchanged, shows `Next Preview: Detailed`, retains the published result, and requires an explicit Preview.
- A separate Detailed Preview applies `145,639` samples and stride `29`. Fast/Balanced/Detailed and the pending transition retain one normalized measurement/published-evidence SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`.
- Current evidence: `artifacts/nominal_actual_density_state_20260715`. ViewModel verification passes `71` checks, nominal/actual golden passes `27/27`, the fixed Viewer/Shell matrix passes `128/128`, BinaryHost passes manifest `13/13`, outputs `12/12`, and Host API commands `3/3`, and the extended four-run density regression passes.

### Pointer-Input Gate

Passed locally on 2026-07-15 for the standalone Viewer and the same Viewer DLL hosted in the Shell:

- `--smoke-pointer-input-report` activates the visible WPF host for the smoke only, sends real Windows pointer input, and restores the original pointer position and topmost state afterward. The WPF/OpenGL code-behind remains the input bridge; existing ViewModel camera, pick, and selection properties are the acceptance state.
- Each host receives `MouseDown=3`, `MouseMove=12`, `MouseUp=3`, and `MouseWheel=1`. A left click selects the generated cube and records its coordinate plus `Cube pick` linked summary, a right-button drag changes yaw and pitch, a middle-button drag changes the camera target, and a positive wheel event reduces camera distance.
- Viewer and Shell each pass on two consecutive current-build runs. The two Viewer reports are byte-identical with SHA-256 `4D6C926DA834ED6AE017D98FEB84BCB043C1FA77AD3364A36D1B1EB842C7CF4E`; the two Shell reports are byte-identical with SHA-256 `2F2CBB688D8C3293C3176100CC6AE2D985BFF1A8F19DE840E77D98D72CCEA2A0`.
- The before and after Viewer/Shell screenshots pass the shared pixel-quality gate. The post-input Shell shows the same cube coordinate in the hosted HUD, Tool / Inspector context, and `Camera / Pick State`; this closes the stale linked-summary issue exposed by the first passing input run.
- Current evidence: `artifacts/pointer_input_regression_20260715`. The current-source build has zero warnings/errors, the fixed Viewer/Shell matrix passes `128/128`, and BinaryHost passes manifest `13/13`, outputs `12/12`, and Host API commands `3/3`.
- Windows Actions observation run `29378562022` at commit `7bebc62` records Viewer and Shell exit `0`, report presence, `pass=True`, routed events `3/12/3/1`, and successful pick/orbit/pan/zoom. Its authenticated artifact `8328811080` is `1,592,037` bytes with digest `sha256:90f4c9aae4ab5dee126ebfc59ea81d85006ef249bf9481d8107aa6677ec229f0`.
- Commit `8a841a6` made this a mandatory CI gate. Run `29378878976` passed the gate and every existing CI step; authenticated artifact `8328930089` is `1,593,122` bytes with digest `sha256:3179673b1d98406daaebc29bb1c4902e977bc9c49bf23a5d233e6dba5a5d8247` and repeats both host results with `Gate|mandatory=True`.

### Hosted Dual-Capture Gate

Passed locally on 2026-07-15 for quick C3D and the fixed full-resolution NIST nominal/actual workflow:

- The hosted Viewer no longer installs an independent shutdown handler. `OpenVisionLab.ThreeD.Shell` is the sole application-lifecycle owner, applies configured pick/pointer/publish/save actions once, captures the embedded Viewer, captures the full Shell, and then propagates the Viewer smoke exit code.
- The pre-fix full NIST command ended with process code `1` after about three minutes and produced zero capture artifacts. The fixed command completes with process code `0`; the embedded `411 x 380` Viewer and `1280 x 800` Shell captures both pass the shared quality gate on attempt 1.
- The same-run Viewer contract records `4,223,524` full-query points, query point `2,724,128`, `Published` state, result entity `result.nominal-actual-surface-deviation`, and smoke exit code `0`. Source, Preview, and Published result separation is unchanged.
- Current evidence: `artifacts/dual_capture_orchestration_20260715`. The current-source build has zero warnings/errors, standalone/hosted pointer reports retain their established SHA-256 values, the fixed Viewer/Shell matrix passes `128/128`, and BinaryHost passes manifest `13/13`, outputs `12/12`, and Host API commands `3/3`.
- Windows Actions runs `29378562022` and `29378878976` both pass the mandatory hosted Viewer/Shell dual-capture step. In the mandatory run, the hosted Viewer and full Shell are accepted on attempt 1 before the pointer gate runs.

### Next Work Order

1. Preserve the mandatory hosted dual-capture and Viewer/Shell pointer-input Windows CI gates as Phase 1 regression coverage.
2. Preserve the locally and Windows-CI-passed mandatory difficult-geometry workflow step and its required report assertions.
3. Keep registration acceptance blocked from product integration until the runtime/distribution prerequisites are resolved; when available, require correspondence count and fitness before RMSE and reject zero-correspondence success.

## Phase 2 - Geometric And Algorithm Reliability

### Goal

Prove that Viewer and Runner geometric results generalize beyond one fixed identity-frame dataset and remain correct under independently known transforms and materially different data conditions.

### Entry Prerequisites

- A second genuinely distinct measured/nominal pair with redistribution-safe or locally traceable provenance.
- An independently known non-identity rigid transform or alignment truth.
- External reference output from CloudCompare, ZEISS INSPECT, PolyWorks, Geomagic Control X, or another trusted implementation.

Do not spend implementation effort fabricating these prerequisites from the existing NIST derivative. Synthetic cases may test errors, but they cannot prove cross-data generalization.

### Exit Checklist

- [x] One fixed identity-frame NIST pair matches independent CloudCompare unsigned and robust-signed output within the declared tolerance.
- [x] A second distinct pair passes source identity, visible Viewer/Runner parity, and external aggregate-statistic comparison. NIST Part 2 preserves `3,965,430` full-query points, separate actual/nominal/query IDs and hashes, explicit Preview/Publish, recipe save/reopen, selected-point provenance, schema `1.2`, and `ViewerRunnerComparison|Matched` evidence.
- [x] A known non-identity transform passes translation and rotation plausibility plus point-level and aggregate expected output. The local Stanford gate covers `12` scans, `50,643` points, `36` checkpoints, full aggregate statistics, CloudCompare external transform parity at maximum difference `3.0913966692081019e-8`, and controlled tamper rejection; units and commercial use remain unavailable.
- [x] Duplicate vertices, non-finite normals, open surfaces, edge/vertex nearest hits, sparse/dense query sampling, and empty mesh/query no-data cases have local synthetic controlled outcomes. This does not replace registration zero-correspondence acceptance.
- [x] The runtime-neutral registration policy records correspondence count and fitness before RMSE and rejects zero-correspondence false success. Its local golden passes `20/20` with explicit units, scenario thresholds, and transform plausibility decisions.
- [ ] An approved registration runtime maps real source/target identity, correspondence count, fitness, RMSE, and transform into that policy and proves Viewer/Runner parity. Distribution and runtime approval remain blocked.
- [x] Each accepted NIST pair preserves actual/nominal/query hashes, units, frame, alignment, full-query/display-sample separation, recipe roundtrip, and Run Record identity.

### Difficult-Geometry Controlled Outcomes

Passed locally on 2026-07-15:

- Mesh-deviation verification passes `23/23`, adding deterministic coincident-triangle tie selection, non-finite stored-normal rejection, explicit open-surface winding semantics, robust vertex recovery, and empty-mesh rejection.
- Nominal/actual execution verification passes `29/29`, adding separate one-point and six-point full-query inputs with exact known-offset statistics plus zero-vertex query rejection.
- Open-surface signed values are explicitly local triangle-normal results, not closed-solid inside/outside classification. Query sampling remains point-weighted rather than generally invariant.
- The current-source build passes with zero warnings/errors and the fixed Viewer/Shell matrix remains `128/128`.
- Evidence and the complete audit matrix are in `OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md` and `artifacts/phase2_difficult_geometry_20260715`.
- The current workflow runs both verifiers as one mandatory fail-closed step, validates the exact passing headers and required case names, and uploads `artifacts/ci/phase2-difficult-geometry/**`. The exact step body and workflow YAML pass locally.
- Commit `0f89450` passed the mandatory step and every existing Windows workflow step in Actions run `29418511898`. Authenticated artifact `8344275224` is `3,725,380` bytes with digest `sha256:36ce274d5f1ffd09d2c4b27d1baec130f2ce2a81852291bed3cd7afb636e5021`; a fresh authenticated download matched that identity and passed all 11 selected Phase 2 report assertions.

Registration correspondence count, fitness, transform plausibility, and RMSE acceptance remain separate.

### Registration Acceptance Controlled Outcomes

Passed locally on 2026-07-15:

- `RegistrationAcceptanceRule` is runtime-neutral and adds no Open3D, PCD, or native dependency.
- Acceptance order is fixed as correspondence count -> fitness -> inlier RMSE -> rigid transform -> translation -> rotation. A failed earlier criterion leaves later criteria `NotRun`.
- The Runner golden passes `20/20`. It explicitly rejects `0 correspondence / RMSE 0`, insufficient correspondence/fitness, excessive RMSE, non-homogeneous/scaled/reflected transforms, scenario translation/rotation violations, malformed/non-finite evidence, unit mismatch, and invalid policy guards.
- Commit `13f143a` passed every Windows workflow step in Actions run `29454088343`; job `87483200712` completed the mandatory registration gate as step 15. Authenticated artifact `8358732707` matched `3,726,847` bytes and digest `sha256:fced1dde391124d89b761336c907957d597b73dfbecbdc9d2dff62f4bf18b9f7`, and its registration report preserves the `20/20`, zero-correspondence, and non-finite-transform evidence.
- This is a policy prerequisite, not registration recovery or product integration evidence. No real engine output reaches Viewer or Runner yet.

### Current Assessment

Phase 2 is **not passed**. NIST Part 2 carries source/provenance, independent CloudCompare signed/unsigned C2M, exact ordered-XYZ verification, full-query OpenVisionLab parity, current Viewer/Shell UI, explicit Preview/Publish, selected-point provenance, typed recipe save/reopen, schema `1.2`, and Viewer/Runner `Matched` evidence over `3,965,430` validation vertices. The separate Stanford gate passes the published non-identity transform at point and aggregate level with zero observed Python/Runner difference and `3.0913966692081019e-8` maximum CloudCompare difference, but remains local research-only evidence with unspecified units. The difficult-geometry controlled-outcome gate and runtime-neutral registration acceptance policy pass locally and in Windows CI. Phase 2 remains open because no approved registration runtime maps real engine output into the policy or proves Viewer/Runner parity. These fixed sources still do not establish arbitrary-mesh, arbitrary-sampling, arbitrary-sensor, registration-recovery, or metrology reliability. See `OPENVISIONLAB_3D_NIST_PART2_CLOUDCOMPARE_DEVIATION_BASELINE_20260715.md`, `OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`, `OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md`, `OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md`, and `OPENVISIONLAB_3D_REGISTRATION_ENGINE_PROTOTYPE_20260713.md`.

## Phase 3 - Physical And Metrology Reliability

### Goal

Connect Viewer coordinates and inspection values to traceable physical units, calibration identity, uncertainty assumptions, repeated observations, and independent metrology results.

### Entry Prerequisites

- C3D X/Z pitch, height scale/offset, units, axis directions, origins, and calibration identity.
- A traceable physical artifact or dataset with independently certified dimensions/deviations.
- Owner approval for any licensed metrology tool, calibration asset, or redistributable evidence.

### Exit Checklist

- [ ] A versioned mapping/calibration profile travels with source, recipe, Viewer evidence, Runner execution, and Run Record.
- [ ] Raw, model, display, and physical units are distinguishable and conversion roundtrips have known-answer tests.
- [ ] Measurement uncertainty assumptions and tolerance decision rules are recorded rather than implied.
- [ ] Repeated scans/runs quantify repeatability and reproducibility instead of proving only same-file determinism.
- [ ] At least one calibrated artifact matches an independent licensed metrology result within a declared acceptance budget.
- [ ] Release documentation states the exact supported calibration and environmental scope.

### Current Assessment

Phase 3 is **blocked and unverified**. Current `unitless`, `raw-height`, `model`, and source-provided `mm` labels must not be converted into calibrated or certified claims without the prerequisites above.

## Reliability Claims

| Claim | Allowed now? | Required wording or reason |
| --- | --- | --- |
| Fixed supported Viewer matrix is repeatable | Yes | State the sample matrix, build/commit identity, and evidence artifact. |
| Fixed C3D display-frame coordinates match independent interchange | Yes | Say `display-frame`; physical scale remains unverified. |
| Fixed NIST Part 1 and Part 2 identity-frame deviations match CloudCompare | Yes | State the part, query count, point/aggregate tolerance, threshold-boundary differences, and identity-frame limitation. Both have fixed visible product slices. |
| Any valid 3D model will produce the same result as commercial tools | No | Both NIST slices remain one nominal design and one XCT family. Stanford proves application of one supplied transform set, not registration recovery or arbitrary transforms. |
| C3D dimensions are physically calibrated | No | Phase 3 mapping/calibration metadata is missing. |
| Metrology-grade, certified, or production-ready | No | Uncertainty, calibrated artifact, repeated-scan, and licensed-tool gates are open. |

## Repeatable Evidence

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_reliability_phase1\pointer\viewer.png --smoke-screenshot-quality-report artifacts\viewer_reliability_phase1\pointer\viewer_quality.txt --smoke-contracts artifacts\viewer_reliability_phase1\pointer\viewer_contract.txt --smoke-pointer-input-report artifacts\viewer_reliability_phase1\pointer\viewer_pointer.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\viewer_reliability_phase1\pointer\shell.png --shell-screenshot-quality-report artifacts\viewer_reliability_phase1\pointer\shell_quality.txt --smoke-pointer-input-report artifacts\viewer_reliability_phase1\pointer\shell_pointer.txt
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1 -ArtifactDir artifacts\viewer_reliability_phase1\matrix -SkipBuild
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-nominal-actual-comparison --report artifacts\viewer_reliability_phase1\nominal_actual_golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-mesh-deviation --report artifacts\viewer_reliability_phase2\mesh_deviation_golden.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_reliability_phase1\viewer_selected.png --smoke-contracts artifacts\viewer_reliability_phase1\viewer_selected.txt --smoke-nominal-actual <actual.stl> <query.ply> <nominal.stl> --smoke-pick nominal-actual --smoke-publish-result
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-nist-nominal-actual-render-density.ps1 -ArtifactDir artifacts\viewer_reliability_phase1\render_density -SkipBuild
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-viewer-dll-host.ps1 -Configuration Debug -ArtifactDirectory artifacts\viewer_reliability_phase1\binary_host -NoRestore
```

The pointer commands require an interactive Windows desktop because they send real OS pointer input. The ignored NIST files are required for the render-density command. If either prerequisite is unavailable, report that gate as not revalidated rather than substituting an older artifact.

## Gate Update Template

```text
Gate:
Phase:
Decision: Pass | Partial | Fail | Blocked
Build/commit identity:
Input/source identity:
Independent reference:
Acceptance tolerance:
Viewer result:
Runner result:
Artifacts:
Known limitation:
Next prerequisite:
```

Update this document only when a gate changes decision, a prerequisite becomes available, or the allowed reliability claim changes.
