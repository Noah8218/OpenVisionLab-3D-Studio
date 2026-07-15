# OpenVisionLab 3D Product Target And Self Evaluation

Updated: 2026-07-15

Status: current product-direction source of truth. Older market reviews remain useful as history, but this document controls current priorities when they conflict.

Viewer reliability phases and claim limits are governed by `docs/OPENVISIONLAB_3D_VIEWER_RELIABILITY_PHASES_20260714.md`.

## Executive Decision

OpenVisionLab 3D Studio should target an explainable, local, rule-based 3D inspection recipe workbench for height maps, point clouds, and meshes.

The product workflow is:

```text
Load measured 3D data and optional nominal data
  -> define units, coordinate frame, references, and ROIs
  -> add ordered inspection steps
  -> Preview explicitly
  -> review metrics, tolerance state, and 2D/3D overlays
  -> Publish an explicit result entity/layer
  -> save the recipe
  -> replay the same recipe in the headless Runner
  -> review a durable run record and report
```

Current maturity is **early inspection workbench MVP**. No repository-backed percentage is used.

- Viewer Foundation v1: **passed for the current fixed sample matrix**.
- C3D map fidelity: **display frame passed for the fixed Thickness sample; physical scale unverified**.
- Inspection Recipe v1: **baseline passed for five independent typed C3D slices: numeric-reference-ROI plane flatness, explicit-cell point-pair dimensions, explicit two-region Gap/Flush, explicit reference/measurement-ROI Volume, and exact-row Cross-section Dimensions**.
- Release candidate: **Viewer bundle prerelease `v0.1.0-rc.1` is published at commit `ac57687`; local, Windows CI, public archive/manifest hash, downloaded-bundle BinaryHost, and `Matched` Viewer/Runner gates pass**.
- Current development identity: **post-RC source builds, manifests, and Run Records identify as `0.1.1-dev`; no corresponding tag or package is published**.
- Nominal/actual inspection: **one fixed identity-frame full-query Viewer/Shell Preview and Publish, typed recipe save/reopen, headless Runner replay, schema `1.2` Run Record, and Viewer/Runner `Matched` gate pass; second-pair generalization, non-identity alignment, uncertainty, and metrology certification remain incomplete**.
- Production integration: **intentionally out of scope**.

Passing Viewer Foundation v1 does not mean the viewer is production-complete. It means rendering, camera, visibility, picking, selection, measurement/result overlays, color modes, Shell hosting, and screenshot evidence are stable enough to protect as a regression baseline while inspection workflow development begins.

The reliability decision is phase-specific: Phase 1 has passed for the fixed supported scope locally and in the current Windows CI workflow, including Foundation, selected-point provenance, current-versus-next-Preview density-state clarity, hosted dual-capture, and mandatory real WPF pointer input in standalone Viewer and hosted Shell. This does not generalize to every Windows session or arbitrary geometry. Phase 2 geometric generalization is not passed; Phase 3 physical/metrology reliability is blocked and unverified. Do not convert those decisions into one product-reliability percentage.

NIST AMMT `Overhang Part X4` is the first corresponding local external measured/nominal baseline. Its `9 x 5 x 5 mm` nominal STL loads correctly with 2,904 triangles, while the Part 1 XCT surface is a distinct 8,560,096-triangle binary STL above the current Viewer limit. The independent CloudCompare 2.13.2 deviation prerequisite passes in the NIST-provided 3-2-1 part frame over all 4,223,524 CloudCompare-unique measured vertices: unsigned mean/std are `0.192040211` / `0.208181684 mm`, signed mean/std are `0.0124131265` / `0.282957542 mm`, XYZ is preserved exactly, and independent binary-PLY verification agrees within `1e-6 mm`. OpenVisionLab streams the original source and matches the fixed query's unsigned/robust-signed output within `1e-6 mm`. Viewer and Shell run all 4,223,524 query points, classify `548,207` below, `2,990,143` within, and `685,174` above `[-0.3, 0.3] mm`, and publish a separate result entity/layer. Fast/Balanced/Detailed Viewer runs render `24,992` / `59,487` / `145,639` signed display samples while preserving normalized measurement/published-evidence SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`. A completed Balanced result now remains explicitly labelled `Current display: Balanced | 59,487 | stride 71` after Detailed is selected, while `Next Preview: Detailed` states that explicit Preview is required; the subsequent Detailed Preview becomes current with `145,639` samples and stride `29` without changing that evidence hash. The typed recipe reopens with stable actual/nominal/query identities and hashes, and Runner produces the same expected `Fail` status and statistics with `ViewerRunnerComparison|Matched`; schema `1.2` JSON plus HTML/CSV preserve step and execution identity. The current executor/result verification passes `27/27`, ViewModel verification passes `71` checks, and a real Balanced pointer-ray smoke exposes ordered query index, actual/query source IDs, signed/unsigned deviation, nearest nominal triangle, and tolerance state in Viewer and Shell. Separate real OS pointer smokes route deterministic click/orbit/pan/zoom events through both hosts and produce byte-identical repeat reports per host. The maximum UI contract-statistic delta from independently parsed CloudCompare output remains `1.3381639552001445e-7 mm`. This closes one fixed identity-frame product slice and the locally/current-Windows-CI-validated fixed-scope Phase 1 Viewer gate, not other sampling/topology cases, semantic mismatch between non-empty declared unit/frame and source truth, non-identity alignment, physical uncertainty, metrology certification, or redistribution approval. See `docs/OPENVISIONLAB_3D_MEASURED_NOMINAL_SAMPLE_REVIEW_20260714.md`, `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md`, `docs/OPENVISIONLAB_3D_NIST_NOMINAL_ACTUAL_END_TO_END_20260714.md`, and `docs/OPENVISIONLAB_3D_VIEWER_RELIABILITY_PHASES_20260714.md`.

Phase 2 evidence progressed on 2026-07-15 without changing the phase decision. NIST Overhang X4 Part 2 is a separately manufactured and measured source with `8,040,658` triangles, SHA-256 `0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032`, and bounds distinct from Part 1. The exact Part 1 CloudCompare 2.13.2 executable extracted `3,965,430` ordered validation vertices; independent PLY verification preserves exact XYZ, and OpenVisionLab unsigned/robust-signed full-query parity passes with maximum difference `7.1853447186631669e-7 mm`, zero material sign mismatches, one explicitly equivalent float-epsilon zero, and 100% signed coverage. The visible Part 2 workflow now preserves distinct actual/query IDs, explicit Preview/Publish, `59,186` Balanced display samples independent of all `3,965,430` measured points, selected-point provenance, typed recipe save/reopen, schema `1.2`, and `ViewerRunnerComparison|Matched`. Stanford Drill separately supplies 12 real range scans and 11 published non-identity translation/quaternion transforms; its local research-only gate matches independent Python/Runner and CloudCompare transform output and rejects a controlled tamper. Phase 2 remains not passed because difficult-geometry controlled outcomes and the accepted registration path remain open. See `docs/OPENVISIONLAB_3D_NIST_PART2_CLOUDCOMPARE_DEVIATION_BASELINE_20260715.md`, `docs/OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`, and `docs/OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md`.

The measured/nominal workbench View, ViewModel, shared Model, Preview, Publish, recipe roundtrip, and Runner checkpoints now pass in that order. Core owns typed comparison and result contracts, Data owns ordered PLY parsing, Tools owns the numerical executor and typed recipe, the ViewModels own durable workflow/input/published state, and the View/code-behind boundary is limited to binding, rendering, file/CLI smoke setup, and event bridging. Current-source end-to-end evidence is under `artifacts/nominal_actual_publish_20260714`.

The source-aware Viewer display-settings slice has separately completed its View, ViewModel, Viewer-local Model, C3D Geometry Style rendering, local deterministic performance, and C3D Grayscale/Thermal Color Map checkpoints. Typed source, Geometry Style, and Color Map identifiers plus immutable `ViewerDisplaySettingsSnapshot` define the effective display-state contract; SharpGL consumes that snapshot for C3D Points, Wireframe, Surface, and Surface + Edges. The display proxy triangulates only complete stride-adjacent source cells, leaves holes open, uses reduced display-only grid overlays, and remains separate from source-cell picking and measurement. Static C3D geometry uses an OpenGL display list; result-owned Plane Flatness Deviation colors bypass it. Two final 31-frame Fast/Balanced/Detailed runs pass `24/24` cases on the recorded local GTX 1060 3GB machine, with minimum observed FPS of `46.786`, `32.574`, and `18.352` respectively. The fixed Balanced 33,761-point C3D sample also completes 90-frame Grayscale and Thermal smokes at `75.303 FPS / 5.272 ms` and `37.049 FPS / 10.438 ms`. Commit `3136ebe` adds a mandatory direct LUT gate, and Windows Actions run `29409271743` passes both color captures/contracts plus all existing gates. These are fixed-sample behavior claims; the performance figures remain local, not cross-machine guarantees. Display changes remain outside inspection Preview, Publish, recipe fingerprints, and Host API v1.0. Preserve `docs/OPENVISIONLAB_3D_C3D_GEOMETRY_STYLE_PERFORMANCE_20260715.md`, `docs/OPENVISIONLAB_3D_C3D_COLOR_MAPS_20260715.md`, and their artifact folders as evidence sources. GLB/STL geometry switching, manual ranges, legends, inversion, and physical color calibration remain open.

Current-source revalidation on 2026-07-12 confirmed the gate with `artifacts/viewer_validation_20260712/matrix_smoke_summary_after.txt`: 129 loader, display, pick, measurement, color, density, Shell-hosting, contract, and controlled-failure checks passed with no failures. C3D-specific detailed display, point picking, two-point distance/height evidence, the 10/10 mapping golden suite, a 66,212-point zero-error .NET PLY roundtrip, independent Python recalculation, and Open3D 0.19.0 re-save comparison also passed in the Viewer display frame. This closes the current fixed-scope Viewer validation; physical calibration and licensed metrology comparison remain separate blocked trust gates.

Full-resolution external-viewer validation on 2026-07-13 strengthened trust gate T3. Stable portable CloudCompare 2.13.2 independently loaded and re-saved all `1,653,562` current-source C3D vertices with unchanged order/RGB and maximum coordinate-component drift `5.00000001e-7` Viewer units. Its C2C mean/std are `4.91657e-7` / `1.49337e-7`, and independently reconstructed selected-cell distance, width, model-height delta, raw-height delta, and signed elevation angle pass the display-frame tolerances. This is interchange and derived-value evidence, not physical calibration, uncertainty, certified metrology, or ZEISS/PolyWorks equivalence. See `docs/OPENVISIONLAB_3D_CLOUDCOMPARE_PARITY_20260713.md`.

Release-candidate revalidation on 2026-07-13 confirmed `0.1.0-rc.1` at commit `ac57687` in Windows Actions run `29198517611`. Build, binary-only Viewer Host, Viewer/Shell screenshot quality, six algorithm/map golden suites, actual C3D PLY roundtrip, independent Python mapping, and artifact upload passed. The uploaded Viewer manifest and schema `1.1` Cross-section Run Record carry the same clean commit and product/Host API identity, and the Viewer/Runner state is `Matched`. GitHub prerelease `v0.1.0-rc.1` publishes the complete Viewer dependency ZIP with SHA-256 `b9a9b6d002f507da63da32934d93bf6e8deaff2d7c1b00ff70a6f36d6b784a83`; this is not a stable, calibrated, or full-application release.

Post-RC development identity changed to `0.1.1-dev` on 2026-07-14 so current-source Viewer manifests and Run Records cannot be confused with the published RC evidence. This is a source/build identity only; no tag, release asset, stable promotion, or Host API change was made.

The same current working tree passed a local pre-push regression on 2026-07-14. `artifacts/local_ci_pre_push_20260714/local_ci_summary.txt` records a zero-warning/error build; an eight-project NuGet audit with zero vulnerable or deprecated packages; Plane `9/9`, Point Pair `9/9`, Gap/Flush `8/8`, Volume `9/9`, Cross-section `9/9`, and C3D Map `10/10` golden cases; five typed Run Record identities and two legacy `Step=null` cases; `Matched` Cross-section Viewer/Runner output; independent .NET/Python map checks; a `0.1.1-dev` BinaryHost with Host API `1.0`; and accepted Viewer, BinaryHost, and Shell schema `1.0`/`1.1`/`1.2` screenshots.

Windows Actions run `29302323300` then passed at commit `e704f6f` with every job step green, including the new LF recipe-byte and recorded-SHA gate. Authenticated artifact `8298975554` is `1,323,767` bytes with digest `sha256:70935ecfb48978cc20abeda446b62fd0ba8d67fb29809a932b122b7a77fa5d00`. Its clean Windows Viewer manifest and Run Record identify product `0.1.1-dev`, Host API `1.0`, manifest schema `1.0`, Run Record schema `1.2`, and commit `e704f6f`; typed Cross-section step identity, Pass status, five metrics, three overlays, and Viewer/Runner `Matched` state pass. The executed recipe's raw SHA-256 is the same LF-stable `f9355976ebd179f20719e20d24736a6f61d8b6711e98bad4b543ced1ae279666` locally and remotely, and the selected local/remote business/evidence payload matches exactly. This is post-RC development evidence, not a packaged release, physical calibration, or metrology certification.

A fresh public-asset acceptance run on 2026-07-13 independently downloaded that ZIP and `SHA256SUMS.txt`, matched the archive hash, and used the BinaryHost verifier to enforce all 13 manifest file paths, sizes, and SHA-256 values before build. The zero-`ProjectReference` Host passed with 12/12 required outputs, C3D render/pick, and an accepted first-attempt screenshot (`blackRatio=0.0045`, `whiteRatio=0.3578`, luminance `0..255`); a 4/4 rejection matrix blocked outside-bundle, missing, wrong-size, and same-size hash-mismatched entries before Host build. This proves package integrity and hostability for the tested Windows/.NET 10 environment, not physical measurement accuracy or broad host compatibility.

Host API v1.0 consumer acceptance on 2026-07-13 passed against both that public RC bundle and a current-source bundle. The zero-`ProjectReference` BinaryHost records a C3D `HostState` snapshot, nonzero `HostStateChanged` events, all three view-command invocations, and a successfully parsed `c3d-height-deviation` recipe saved through `IOpenVisionThreeDViewerHost.SaveRecipe`. A controlled missing-recipe run records `smokeExitCode=1` and now returns process exit code `1`, closing an external-host failure-propagation gap.

Registration-engine research on 2026-07-13 accepted Open3D `DemoICPPointClouds` as a probe-only alignment golden candidate, not calibrated or nominal/actual evidence. An 11-case x 3-run robustness characterization is deterministic but matches only 5 predeclared outcomes: high noise misses the translation limit, partial/combined cases miss strict RMSE limits despite small known-transform errors, a medium initial error converges unexpectedly, and a distant initial error proves that zero correspondences can be reported with RMSE `0`. The recorded non-GUI Open3D `0.19.0` source commit now passes both the recovered build and an independent clean single-shot Release build/install. The clean 873-file, 88,977,375-byte install has the same paths and sizes as the recovered install, 871 identical file hashes, and two rebuilt DLLs with matching export/dependency contracts but different PE timestamps and hashes. Its 58,520,064-byte three-file probe runtime matches the official Windows binary in all 33 robustness runs and all three current `0 -> 1` DemoICP runs. Current hardened evidence remains narrower than the earlier report: both runtimes reject pair `1 -> 2` because `cloud_bin_2.pcd` contains 771 non-finite normals, so its older successful metrics are historical pre-hardening evidence. This proves a reproducible separate-process technical boundary and an explicit correspondence/fitness guard requirement, not product adoption. A schema-valid 33-component CycloneDX candidate records the directly proven dependency set, exact available archive/license hashes, and three modifications. Distribution remains blocked by unresolved Assimp and prebuilt BoringSSL/MKL/VTK provenance, final notices, Microsoft VC/OpenMP and clean-host evidence, product integration impact, and owner/legal approval; Viewer/Runner parity remains open. The .NET `PclNET 0.8.3` path remains rejected, and no product dependency, PCD loader, or fixed sample was added. `docs/OPENVISIONLAB_3D_REGISTRATION_ENGINE_PROTOTYPE_20260713.md`, `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`, and `docs/OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md` record the decision.

Windows Actions run `29216983045` passed this Host API consumer gate at commit `95dd8da` together with all Shell/Runner/golden/map checks. Evidence artifact `8266920376` is `1,167,342` bytes with digest `sha256:254145a80071df39f88d4c199372d1c30c64057f6b931062de4c8dfbdc476c16`.

Windows Actions run `29215566528` revalidated the hardened gate at commit `c50d196`. BinaryHost, Shell screenshot quality, Runner/golden/map checks, actual C3D roundtrip, independent Python mapping, and artifact upload all passed. Evidence artifact `8266449434` has digest `sha256:230b5607524e668ed47f59d85e08514bace873e631f676bb44a32282d2eb4c65`.

Windows Actions run `29288595132` revalidated the current Viewer trust baseline at commit `cebdc8f` on 2026-07-14. The existing BinaryHost, Shell screenshot-quality, Runner/golden/map, actual-map, PLY-signature, and artifact gates passed together with the new stride-aligned independent Python point-pair calculation. Evidence artifact `8294167228` is `1,167,597` bytes with digest `sha256:485c6bbcfb0389ed2af2584eb9dfb359365fd95927bcfb3e3b2ccd4342d9b7bc`.

Windows Actions run `29297655730` passed the NuGet package-health gate at commit `6779881` on 2026-07-14. The separate four-case verifier self-test and live audit passed for all eight solution projects with zero vulnerable or deprecated direct/transitive packages, and every existing BinaryHost, Shell screenshot-quality, Runner, golden, map, and upload step remained green. Evidence artifact `8297372590` is `1,168,807` bytes with digest `sha256:66a3a2650a720aa8810ca4a433f73f08d97053122f77750f740455e6b9385fde`; a fresh authenticated download matched the digest and contained both parseable raw JSON responses plus the zero-finding summary.

## Evidence Checked

Local checks performed on 2026-07-11:

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`: passed with zero warnings and zero errors.
- Viewer fitted-plane smoke: `artifacts/self_eval_viewer_plane_20260711.png` and `artifacts/self_eval_viewer_plane_20260711.txt`.
- Shell hosted-viewer smoke: `artifacts/self_eval_shell_plane_20260711.png`.
- Runner replay: `artifacts/self_eval_runner_c3d_20260711.txt`; the configured sample intentionally reports `Fail` because peak deviation exceeds tolerance.
- Analytic plane/flatness verification: `artifacts/plane_flatness_golden_after.txt`; exact plane coefficients, signed extrema, flatness, RMS, Pass/Fail thresholds, and six controlled error paths pass.
- Point-pair dimensions evidence: `artifacts/viewer_dimensions_after.*`, `artifacts/viewer_dimensions_reopen_after.*`, `artifacts/runner_point_pair_dimensions_after.txt`, and `artifacts/shell_dimensions_after.png`; the saved source cells replay the same distance, XZ width, signed elevation angle, and status.
- Analytic point-pair verification: `artifacts/point_pair_dimensions_golden_after.txt`; a known `(3,4,4)` vector, signed descending angle, tolerance failure, and six controlled invalid-input paths pass (`9/9`).
- Gap/Flush evidence: `artifacts/viewer_gap_flush_after.*`, `artifacts/viewer_gap_flush_reopen_after.*`, `artifacts/runner_gap_flush_after.txt`, and `artifacts/shell_gap_flush_after.png`; Viewer and Runner match signed gap `1.322` model, signed flush `243.544` raw-height, sample counts, and Pass status.
- Analytic Gap/Flush verification: `artifacts/gap_flush_golden_after.txt`; signed separation/overlap, independent tolerance failures, empty region, non-finite statistics, invalid tolerance, and missing-unit cases pass (`8/8`).
- Volume evidence: `artifacts/viewer_volume_after.*`, `artifacts/viewer_volume_reopen_after.*`, `artifacts/runner_volume_after.txt`, and `artifacts/shell_volume_steps_after.png`; Viewer and Runner match above `0.874`, below `0.972`, signed net `-0.098 model^3`, sample counts, and Pass status.
- Analytic Volume verification: `artifacts/volume_golden_after.txt`; exact above/below/net integration, signed acceptance, insufficient/empty samples, invalid area/tolerance, non-finite measurement, and missing-unit cases pass (`9/9`).
- Cross-section evidence: `artifacts/viewer_cross_section_after.*`, `artifacts/viewer_cross_section_reopen_after.*`, `artifacts/runner_cross_section_after.txt`, and `artifacts/shell_cross_section_steps_after.png`; Viewer and Runner match row `983`, columns `200..1100`, `836` valid samples, width `4.247 model`, raw-height range `1708.232`, and Pass status.
- Analytic Cross-section verification: `artifacts/cross_section_golden_after.txt`; exact width/range, independent tolerance failures, invalid selectors, insufficient/non-finite/out-of-range samples, invalid tolerance, and missing-unit cases pass (`9/9`).
- C3D map fidelity: `artifacts/map_fidelity/c3d_map_fidelity_golden.txt` passes `10/10`; the full-resolution point-only audit roundtrips all 1,653,562 valid points with zero .NET XYZ/RGB error; an independent Python implementation reports maximum coordinate error `2.37e-7` and RGB error `0`; the local PNG identifies the unflipped source orientation; Microsoft 3D Viewer independently renders the same major shape; Open3D 0.19.0 preserves the sampled `66,212` points/RGB within `5e-6`; and CloudCompare 2.13.2 preserves all full-resolution points/RGB within `5.00000001e-7` Viewer units while also passing C2C and selected point-pair metric checks.
- Current data matrix: C3D, GLB, STL, LAS, and LAZ with positive and controlled-failure paths.
- Current architecture: separate Core, Data, Tools, Viewer, Docking.Controls, Shell, Runner, and app-host projects.
- Fixed NIST nominal/actual end-to-end evidence: `artifacts/nominal_actual_publish_20260714` records the Part 1 accepted Viewer/Shell, recipe reopen, `ViewerRunnerComparison|Matched`, and schema `1.2` baseline. `artifacts/nist_part2_visible_20260715` records the Part 2 before/after identity correction, accepted Viewer/Shell, selected point, recipe reopen, Runner `Matched`, schema `1.2` JSON/HTML/CSV, `27/27` executor/result, `71` ViewModel checks, `128/128` fixed matrix, BinaryHost, and current pointer input. `artifacts/pointer_input_regression_20260715` records byte-identical repeated local pointer reports. Windows Actions runs `29378562022` and `29378878976` prove hosted dual-capture and Viewer/Shell pointer input in the current runner image; the second run enforces pointer input as a mandatory gate and preserves authenticated artifact digest `sha256:3179673b1d98406daaebc29bb1c4902e977bc9c49bf23a5d233e6dba5a5d8247`.

Current-source Viewer revalidation performed on 2026-07-12:

- Build: passed with zero warnings and zero errors.
- Fixed matrix: `artifacts/viewer_validation_20260712/matrix_smoke_summary_after.txt` records 129 passes and zero failures.
- C3D interaction evidence: `artifacts/viewer_validation_20260712/c3d_detailed_pick.png`, `c3d_detailed_pick.txt`, `c3d_two_point.png`, and `c3d_two_point.txt`.
- C3D numerical evidence: `c3d_map_golden.txt`, `c3d_map_dotnet.txt`, and `c3d_map_python.txt` in the same artifact folder.
- External runtime evidence: Open3D 0.19.0 preserved all 66,212 sampled vertices and RGB; ASCII re-save maximum coordinate drift was `5e-6`, passing the documented `1e-5` external-writer tolerance.
- Full-resolution external runtime evidence: CloudCompare 2.13.2 preserved all 1,653,562 ordered vertices and RGB at `1e-6`, reported sub-micro-unit C2C statistics, and preserved the fixed recipe point-pair display-frame metrics. Evidence is recorded under `artifacts/map_fidelity_cloudcompare_20260713` and summarized in `docs/OPENVISIONLAB_3D_CLOUDCOMPARE_PARITY_20260713.md`.

The plane/flatness, point-pair-dimensions, Viewer, and Evidence Workbench baseline is published in commit `718792e`. The C3D map-fidelity update is evaluated by the current-build evidence listed above.

## Commercial Product Findings

Official product material was checked on 2026-07-11.

| Product | Commercial pattern | Direction for OpenVisionLab 3D |
| --- | --- | --- |
| ZEISS INSPECT | Parametric inspection steps are traceable, repeatable, editable, dependency-aware, and reusable as templates. It also connects nominal/actual color maps, GD&T, reporting, and trend analysis. | Make the inspection plan and its dependencies first-class. A result must explain which source, reference, ROI, parameters, and earlier step produced it. |
| PolyWorks Inspector | Uses reusable inspection projects, explicit sequences, feature/datum/best-fit alignment, measured-to-nominal color maps, dimensional controls, multipiece review, and certified math. | Treat coordinate/reference definition as part of the recipe, show real ordered steps, and keep Viewer/Runner results deterministic. Do not claim metrology-grade accuracy without validation. |
| Geomagic Control X | Focuses on scan-to-CAD or scan-to-scan comparison, visual scripting, repeated automated inspection, annotations, dimensions, and understandable reports. | Add nominal/actual comparison only after basic references and recipe steps are stable. Preserve learned inspection intent in recipes rather than UI-only state. |
| LMI Gocator | Chains masks/ROIs into surface tools such as plane, flatness, dimensions, holes, volume, gap, and flush, then applies thresholds for decisions. | Build small, inspectable ROI-based tools with explicit inputs, metrics, overlays, and tolerances. `Reference Plane + Flatness` is the correct first complete surface tool. |
| Cognex VisionPro 3D | Provides reference-plane height, volume, cross-section, alignment, and graphical application flow for industrial 3D data. | Prioritize reference-relative height, volume, cross-section, and ordered tool flow before broad CAD or AI features. |

The common commercial lesson is not the number of tools. It is the complete chain:

```text
reference -> alignment -> tool input -> measurement -> tolerance -> visual evidence -> replay -> report
```

## Target Position

### Target Users

- Vision and automation engineers developing offline 3D inspection recipes.
- Quality engineers reviewing 3D measurements and evidence without needing a full CAD metrology suite.
- Developers extending transparent rule-based tools and validating Viewer/Runner parity.

### Product Differentiators

- Local-first and sensor-neutral for imported height maps, point clouds, and meshes.
- A separately reusable SharpGL Viewer with inspection facts visible inside the Viewer itself.
- Explicit source/result separation and explicit Preview/Publish behavior.
- Human-readable evidence contracts plus a headless Runner for deterministic replay.
- Inspectable rule algorithms and small end-to-end tool slices instead of hidden automatic tuning.
- Future LLM assistance may draft recipe steps only after the recipe schema and validators are stable; it must never bypass validation or explicit Preview/Run/Publish.

### Explicit Non-Goals For The Current Product Phase

- Full CAD kernel, broad STEP/IGES/PMI import, or standards-complete GD&T.
- Scanner/camera acquisition, robot programming, PLC/I/O, production HMI, or line control.
- Enterprise data lake, cloud collaboration, account management, or plant-wide SPC platform.
- AI defect training or automatic recipe tuning before rule-based evidence is trustworthy.
- Claims of calibrated or certified metrology accuracy without units, calibration, uncertainty, and algorithm-validation evidence.

## Capability Scorecard

Scale: `0` absent, `1` prototype, `2` working MVP, `3` operational baseline, `4` commercial-mature. Scores are directional and are not combined into a marketing percentage.

| Capability | Current | Evidence | Main gap |
| --- | ---: | --- | --- |
| Data loading and 3D display | 3 | C3D, GLB, STL, LAS/LAZ fixed matrix; render density and controlled loader failures. | Clip/crop workflow, broader formats, and out-of-core scale are not yet operational. |
| Camera, picking, selection, overlays | 3 | Orbit/pan/zoom/fit, point/mesh picks, ROI/section, measurement and result overlays, Viewer HUD, and repeatable real WPF click/orbit/pan/zoom regression in Viewer and Shell. | Remote CI portability of foreground Windows input and broader interaction scenarios remain unverified. |
| Reference and alignment | 2 | Transform state, translation-only Align From ROI, fitted C3D height-field plane, and numeric recipe-owned reference ROI. | No interactive plane ROI, 3-point frame, plane-derived rotation, 3-2-1, or best-fit. |
| Measurement toolbox | 2 | Two-point, height delta, ROI step, section/profile, height map, fitted-plane distance, ROI-reference flatness, explicit-cell dimensions, Gap/Flush, Volume, Cross-section, and one fixed full-query actual-to-nominal surface deviation. | Automatic feature-based dimensions, area, physical/calibrated volume, broader nominal comparison, and edge-detected gap remain incomplete. |
| Recipe and inspection-step model | 2 | Typed flatness, point-pair-dimensions, Gap/Flush, Volume, Cross-section, and nominal/actual slices with stable step/source/reference IDs, save/reopen, explicit Preview/Publish, Runner replay, and Shell evidence. | The slices use tool-specific recipe families; there is no proven multi-step dependency executor. |
| Runner and evidence parity | 2 | Headless replay, contract comparison, screenshots, result layers, Shell history/snapshot views, schema `1.2` JSON/HTML/CSV, fixed NIST actual/nominal/query identity, and `Matched` output. | Repeatability is still one validated pair; the second physical candidate has no external baseline, Viewer/Runner parity, batch, or general ordered multi-step replay contract. |
| Nominal/actual comparison | 2 | Fixed NIST actual/nominal/query identities, source-provided frame/alignment, exact point-to-triangle deviation, robust signs, zero-centred color map, tolerances, Preview/Publish, recipe roundtrip, and Runner parity. | A second physical candidate and transform-truth candidate exist, but neither has passed the second-pair/non-identity execution gates; arbitrary sampling/topology, uncertainty, and metrology remain open. |
| Reporting and multipiece review | 2 | Runner TXT, per-run JSON, human-readable HTML metric table, CSV metric export, and Shell artifact commands. | No PDF, database, retention/signing, multi-piece table, trends, statistics, or SPC. |
| Metrology assurance | 1 | Deterministic smoke values, explicit raw/model units in selected paths, analytic plane/flatness, point-pair, Gap/Flush, Volume, and Cross-section golden suites, plus a C3D display-frame golden/neutral-PLY roundtrip baseline. | Formal physical mapping contract, calibration provenance, uncertainty, calibrated external datasets, licensed metrology comparison, feature-fitting validation, and broader independent algorithm validation. |
| Architecture and maintainability | 2 | Separate Viewer/Shell/Core/Data/Tools/Runner boundaries; MVVM direction; CI build. | Viewer code-behind remains large, recipe logic is tool-specific, and automated unit/integration tests are limited. |

## Gate Decision

### Viewer Foundation v1: Passed

The current fixed matrix demonstrates the contracts originally required for the viewer gate:

- reliable display of representative height-grid, mesh, and point-cloud samples;
- camera control and fit behavior;
- object/layer visibility;
- picking and selection;
- measurement and result overlays;
- color modes and legends;
- standalone Viewer and docked Shell hosting;
- screenshot and contract smoke evidence;
- controlled loader failures.

Future viewer changes must preserve this baseline, but routine development should no longer add viewer-only features without an inspection workflow need.

The 2026-07-12 current-source revalidation closes this fixed-scope Viewer gate. It does not close physical calibration, out-of-core scale, gesture automation, or independent commercial metrology validation.

### Inspection Recipe v1: Current Gate

The next release target is one complete reusable inspection plan, not another isolated smoke-only measurement.

Required acceptance scenario:

1. Load the C3D sample.
2. Define a reference plane from an operator-selected ROI or three valid points.
3. Add a flatness/deviation step with explicit units and tolerance.
4. Preview without mutating the source entity.
5. Show plane normal, sample count, RMS, min/max signed deviation, flatness, status, and deviation overlay.
6. Publish a separate result entity/layer explicitly.
7. Save the reference and tool as recipe steps with stable IDs and input references.
8. Reopen the recipe and reproduce the same Viewer result.
9. Run the same recipe headlessly and match metrics/status against the Viewer contract.
10. Show the actual ordered steps and the resulting run record in Shell.

Inspection Recipe v1 passes only when all ten items have current build and smoke evidence.

Status on 2026-07-11: the baseline passes for `recipes/c3d-plane-flatness.recipe.json` using a numeric operator-configured reference ROI. Current evidence is `artifacts/viewer_flatness_after.*`, `artifacts/viewer_flatness_reopen_after.*`, `artifacts/runner_flatness_after.txt`, and `artifacts/shell_flatness_after.png`. This does not validate calibrated accuracy or a general multi-step graph.

Algorithm hardening status: `artifacts/plane_flatness_golden_after.txt` passes an analytic plane with known signed offsets and controlled invalid-reference/input cases. This validates the current plane/flatness mathematics against known answers, but not calibration, uncertainty, or external metrology software.

Second typed-slice status on 2026-07-11: `recipes/c3d-point-pair-dimensions.recipe.json` passes explicit Preview/Publish, source-cell recipe save/reopen, Viewer/Runner parity, Shell step evidence, and render-density-independent source-cell resolution. `artifacts/point_pair_dimensions_golden_after.txt` passes `9/9` known-answer and controlled-error cases. This measures two selected C3D cells; it does not perform edge detection, line/circle fitting, CAD dimensions, or GD&T.

Third typed-slice status on 2026-07-12: `recipes/c3d-gap-flush.recipe.json` passes explicit Preview/Publish, two-region recipe save/reopen, Viewer/Runner parity, Shell step evidence, and a fixed 140,000-point measurement budget independent from display density. `artifacts/gap_flush_golden_after.txt` passes `8/8` signed known-answer and controlled-error cases. Gap is the signed aligned-X distance between facing ROI edges; Flush is right-minus-left mean raw height. These remain unitless/raw-height results, not calibrated physical seam measurements.

Fourth typed-slice status on 2026-07-12: `recipes/c3d-volume.recipe.json` passes explicit Preview/Publish, reference-plane and measurement-ROI recipe save/reopen, Viewer/Runner parity, Shell step evidence, and `9/9` analytic/error golden cases. Its above/below/net values remain uncalibrated display-frame `model^3`, not physical volume.

Fifth typed-slice status on 2026-07-12: `recipes/c3d-cross-section-dimensions.recipe.json` passes explicit Preview/Publish, exact source-row/column-range recipe save/reopen, Viewer/Runner parity, Shell step evidence, and `9/9` analytic/error golden cases. It does not perform automatic feature finding or calibrated physical dimensioning.

## Development Priorities

### P0: C3D Map Fidelity - Display Baseline Done, Physical Profile Next

- Preserve the passed source-grid orientation, mapping golden cases, and neutral PLY coordinate/color roundtrip.
- Obtain X/Z pitch, height scale/offset, source/display units, axis directions, and calibration identity from the C3D producer or official format contract.
- Store those values in an explicit mapping profile. Keep the current normalization as a named uncalibrated display profile and never silently relabel it as physical units.
- Repeat the same neutral-file bounds/coordinate comparison in an independent metrology tool before making accuracy claims.

### P0: Reference Plane + Flatness End-To-End Slice - Baseline Done

- View: reference mode/ROI or three-point selection, flatness parameters, and visible step placement.
- ViewModel: commands, selection validation, tolerance state, metric/result state, and step summary.
- Model/Tools: fitted reference result and flatness evaluation using the smallest shared step shape required by this tool.
- Evidence: overlay, result layer, recipe save/reopen, Runner parity, Viewer/Shell screenshots, and contract checks.
- Do not build a speculative workflow engine first; let this first complete step define the minimum reusable contract.

### P1: Real Inspection Plan In Shell - Single-Step Evidence Baseline Done

- Actual flatness and point-pair recipe step rows show enabled state, source/reference inputs, status, and Viewer/Runner evidence.
- Multi-step order, dependencies, blocked-step state, and one combined recipe remain unproven.
- Keep Preview, Publish, Save, and Run explicit commands.

### P1: Basic Surface Measurement Set

Add one complete tool at a time in this order:

1. Flatness and signed deviation to selected plane. Baseline done for a numeric reference ROI.
2. Explicit-cell width/distance/signed elevation angle. Baseline done; automatic feature extraction remains out of scope.
3. Gap/flush or two-region step height. Explicit-region baseline done.
4. Volume above/below a reference plane. Explicit height-field ROI baseline done; physical calibration remains blocked.
5. Cross-section dimensions. Exact source-row/range baseline done; automatic feature finding remains out of scope.

Each tool requires Viewer/Shell UI, metrics, overlay, tolerance, recipe persistence, Runner replay, and evidence before the next tool starts.

### P2: Nominal/Actual Inspection v1 - Fixed Baseline Done

- Distinguish measured and nominal entities.
- Add explicit alignment strategy and transform evidence.
- Implement measured-to-nominal point/mesh deviation and a signed color map.
- Start with one local mesh/point-cloud pair; do not add a CAD kernel first.

End-to-end status on 2026-07-15: both NIST Overhang X4 Part 1 and Part 2 have verified CloudCompare signed/unsigned C2M references in the documented 3-2-1 part frame. For both physical instances, source identity, fixed-frame algorithm parity, generic View/ViewModel/shared execution, real signed-color Preview, separate Publish result, typed recipe save/reopen, Runner replay, schema `1.2` Run Record, and current Viewer/Shell evidence pass. Stanford Drill separately passes application of its published non-identity transform at point and aggregate level. The next executable gate is the smallest missing controlled difficult-geometry case after auditing existing goldens; do not generalize these fixed-source results to arbitrary registration or metrology.

### P2: Durable Run Record And Report

- Define a serializable run record containing recipe identity, source identity/hash, time, status, metrics, artifact paths, and Viewer/Runner match state.
- Generate simple JSON plus HTML or CSV before considering PDF or enterprise reporting.
- Add batch/trend views only after multiple real runs use the same stable record.

Status on 2026-07-12: schema `1.1` baseline done for one real Cross-section run. `artifacts/run_record_identity_20260712` preserves the matched Pass result plus product/Host API versions, Git commit/tree state, .NET runtime, OS, and architecture in JSON/HTML/CSV; current Shell remains compatible with schema `1.0`. Broader multi-run reporting remains deferred.

Repeatability status on 2026-07-14: authenticated artifacts from Windows Actions runs `29297655730` and `29297867087` contain independent Cross-section records for the same recipe/source. After excluding only Run ID, UTC time, elapsed milliseconds, and Git commit, the complete remaining JSON payload is identical with SHA-256 `59ab1baf854ef23da98bdf7e977a3fd69d9675e81f0efedb99dbc7f5be1cd2d8`; both records have Pass status, five identical metrics, three identical overlays, and `Matched` Viewer/Runner state. This strengthens deterministic same-source replay but is not multi-piece or batch evidence. Those schema `1.1` records identify the legacy `c3d-height-deviation` document family through `RecipeType` and the executed Cross-section tool through `ToolName`; schema `1.2` closes that historical ambiguity with the first-class `Step` object described below.

Step-identity status on 2026-07-14: schema `1.2` adds an optional first-class `Step` object while preserving older-minor deserialization. The fixed Cross-section run records stable step `step.c3d-cross-section-dimensions`, source `source.c3d-thickness`, and reference `reference.c3d-row-range` consistently in JSON, HTML, and CSV under `artifacts/run_record_step_identity_20260714`; status remains Pass and Viewer/Runner remains Matched. Plane Flatness, Volume, Point Pair, and Gap/Flush use their existing validated recipe IDs. Legacy Height Deviation and LAZ recipe `1.0` documents have no stable step ID, so their records leave `Step` empty rather than inventing identity. This closes the single-step traceability gap but does not prove a general ordered multi-step executor.

Viewer deployment status on 2026-07-12: binary boundary proven locally and in Windows Actions run `29195744796`. `samples/OpenVisionLab.ThreeD.Viewer.BinaryHost` has no project reference, builds from the published Viewer bundle, and its generated EXE directly passes C3D render/pick smoke with all required runtime dependencies; the CI evidence artifact was uploaded successfully.

Shell evidence status on 2026-07-12: local full Shell C3D capture was accepted on attempt 1 with black ratio `0.0609`, white ratio `0.6215`, luminance `0..255`, and `1,024,000` sampled pixels. Windows Actions run `29196380343` passed the Shell quality and release identity steps and uploaded the expanded CI evidence artifact.

### P3: Metrology Credibility

- Make model/display/source units and conversions explicit in every measurement path.
- Preserve the passed synthetic golden baselines for plane/flatness (`9/9`), point-pair distance/angle (`9/9`), Gap/Flush (`8/8`), Volume (`9/9`), Cross-section (`9/9`), and C3D display mapping (`10/10`). The next credibility gap is physical mapping/calibration provenance, uncertainty, calibrated external datasets, and independent feature-fitting/metrology validation.
- Record algorithm version, sample policy, calibration provenance, and uncertainty assumptions.
- Do not use terms such as certified, calibrated, or metrology-grade until independently justified.

## Engineering Direction

- Preserve the Viewer as a separate SharpGL library and preserve the docking/WPF-UI ownership boundaries.
- For visible work, follow View -> ViewModel -> Model. View contains bindings, commands, behaviors, and converters; code-behind remains only the UI/OpenGL/OS bridge.
- Build vertical inspection slices. A new tool is incomplete without parameters, validation, metrics, overlay, tolerance status, recipe persistence, Runner parity, and evidence.
- Keep source geometry immutable. Preview and published result geometry remain separate.
- Use stable step IDs and explicit entity/reference inputs. Never depend on display names or implicit active-selection state during replay.
- Keep measurement sampling independent from render density.
- Keep source-grid fidelity, Viewer display-frame fidelity, and calibrated physical fidelity as separate gates. Screenshots support numerical evidence but do not replace it.
- Make invalid references, insufficient points, unit mismatch, degenerate fits, and missing inputs controlled result states rather than unhandled exceptions.
- Prefer known synthetic truth before adding another public sample or external geometry dependency.
- Update this document and `AGENTS.md` when a gate passes or the product target changes.

## Official Sources Checked

- ZEISS INSPECT parametric concept: https://www.zeiss.com/metrology/en/software/zeiss-inspect/features/parametrics.html
- ZEISS INSPECT Optical 3D: https://www.zeiss.com/metrology/en/software/zeiss-inspect/zeiss-inspect-optical-3d.html
- PolyWorks Inspector: https://www.polyworks.com/en-us/products/polyworks-inspector
- Geomagic Control X: https://hexagon.com/products/geomagic-control-x
- Geomagic Control X automated inspection: https://hexagon.com/products/geomagic-control-x/automated-inspection
- LMI Gocator emulator scenarios and built-in tools: https://lmi3d.com/testing-purpose/
- LMI Surface Mask and Flatness workflow: https://lmi3d.com/blog/introducing-surface-masking/
- Cognex VisionPro 3D-A5000 tools: https://www.cognex.com/products/machine-vision/3d-machine-vision-systems/3d-a5000-series-area-scan/software
- CloudCompare cloud-to-cloud distance: https://www.cloudcompare.org/doc/wiki/index.php?title=Cloud-to-Cloud_Distance
- Open3D geometry file I/O: https://www.open3d.org/docs/latest/tutorial/geometry/file_io.html
