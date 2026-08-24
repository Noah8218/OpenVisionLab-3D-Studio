# OpenVisionLab 3D Studio Project Analysis

Date: 2026-08-22

Update: 2026-08-23 — follow-up slices through `PL-0044` are closed. The master
backlog remains authoritative for the resulting inventory and next priority.

Status: Complete for the recorded repository audit and its dependency-ready
follow-up slices through `PL-0044`

Authority boundary: this dated document is analysis and completion evidence.
`OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md` remains
the sole owner of the current capability inventory and development queue.

## 1. Scope And Method

The owner requested an end-to-end review of the repository documentation and
actual source, followed by durable documentation and the start of development
on confirmed deficiencies. The audit baseline was clean `main` commit
`f725ad9`. The review covered:

- Git state and the latest fifteen commits;
- all solution and project files, project references, target frameworks, and
  package dependencies;
- the complete `docs` file inventory, document titles and status markers, all
  current authority documents, and task-relevant architecture, data, Viewer,
  MVVM, SDK, verification, and release documents;
- startup, source loading, rendering, teaching, Preview, Publish, Run,
  reporting, save, and reopen source paths;
- 3D formats, data models, Viewer capabilities, algorithms, memory ownership,
  asynchronous execution, cancellation, resource lifetime, UI workflow, and
  verification infrastructure.

The repository contained about `995` files and `252` documentation files
(`248` Markdown, `3` JSON, and `1` GIF) at the audit baseline. Every document
was inventoried before selection. Current authority and linked task documents
were read in full; dated historical evidence was evaluated through its full
inventory, title/status scan, and targeted content search rather than treating
every old completion statement as current authority.

Private market research, vendor comparisons, and supplied-media analysis are
excluded from this tracked report under the repository's independent design
boundary.

## 2. Current Product Definition

OpenVisionLab 3D Studio is a local, file-first, deterministic 2.5D/3D
rule-based inspection workbench for identified height fields, point clouds,
and triangle meshes. The supported operator contract is:

```text
Load
-> Source Quality
-> Teach
-> explicit Preview
-> explicit Publish
-> explicit Run
-> Evidence
-> Save / Reopen
```

The Viewer is a synchronized teaching, measurement, comparison, and evidence
surface. It is not the whole product. Current evidence does not establish a
camera-acquisition product, PLC or robot controller, cloud service, certified
metrology system, Gauge R&R result, or production approval.

The canonical maturity inventory remains:

| Classification | Count |
| --- | ---: |
| Complete | 149 |
| Partial | 16 |
| New | 45 |
| External prerequisite | 9 |
| Out of scope | 16 |
| Total | 235 |

Inspection Workspace v3 remains `7/8`; Inspection Workbench v4 is `3/3`.
The frozen first-release Phase 1 package and automated gates pass, while
product-owner unaided Wide and Compact R0 remains the release-acceptance gate.

## 3. Solution And Architecture

The solution contains fifteen projects:

| Layer | Projects | Responsibility |
| --- | --- | --- |
| Product UI | `ThreeD.Shell`, `ThreeD.Viewer`, `ThreeDStudio` | Workbench composition, WPF/OpenGL Viewer, standalone host |
| Presentation | `ThreeD.Presentation`, `ThreeD.Docking.Controls`, `Wpf.MessageDialogs` | shared commands, AvalonDock surfaces, dialogs |
| Application and domain | `ThreeD.Core`, `ThreeD.Tools` | stable contracts, Studio adapters, recipe and inspection policy |
| Data and evidence | `ThreeD.Data`, `ThreeD.Reporting`, `ThreeD.Runner` | source decoding, Run Record composition, headless replay |
| Infrastructure | `Localization`, `Logging`, `Logging.Controls` | language and durable logging |
| Standard verification facade | `ThreeD.Data.Tests` | `dotnet test` discovery over selected existing public verifiers |

The primary projects target .NET 10; Windows/WPF hosts use Windows-specific
targets. The C# language version is not explicitly pinned and therefore uses
the selected SDK default. OpenCV/OpenCvSharp is not used. Principal packages
are `OpenVisionLab.Vision3D 3.0.1-dev.20260823.crop.1`, `SharpGL.WPF 3.1.1`,
`Unofficial.laszip.netstandard 5.6.2`, AvalonDock, WPF-UI, LiveCharts, and
log4net.

Reusable numerical, geometric, feature-extraction, matching, and inspection
algorithms belong to the vendored OpenVisionLab Vision SDK. Studio owns source
identity, frame/unit validation, recipe policy, Preview/Publish/Run lifecycle,
evidence composition, persistence, and UI. The current structure guard proves
this boundary without adding a speculative Clean Architecture layer.

```text
ThreeD.Shell
  -> ViewModels and lifecycle/request/evidence/layout coordinators
  -> ThreeD.Viewer WPF/OpenGL bridge
  -> ThreeD.Tools adapters and policy
  -> ThreeD.Data loaders and identified models
  -> OpenVisionLab.Vision3D numerical tools
  -> ThreeD.Reporting and ThreeD.Runner evidence/replay
```

`PL-0039` adds one conventional .NET 10 MTP/xUnit v3 project that directly
wraps two existing public Data verifiers. Verification remains primarily
hosted inside Runner, Shell, and Viewer product assemblies and invoked through
custom command routers and PowerShell gates; the facade does not duplicate or
migrate that catalog.

`PL-0040` closes `M-09` through that existing catalog: the Runner-owned
SourceQualityReport verifier now passes `18/18` for signed finite values,
missing-value semantics, and malformed C3D topology, while the hosted workflow
requires the complete report marker. It adds no production validation layer or
second fixture framework.

`PL-0041` closes `M-11` by extending the existing Inspection Workspace
verifier to `67/67`. The 3D and Height Image adapters retain one shared
selection boundary, distinct role changes publish once, repeated identities
publish zero additional changes, and recipe/execution state remains stable.

`PL-0042` closes `M-15` by qualifying the existing Runner-owned Completeness
verifier at `30/30`. The current suite already owns the exact four-cell metric
and decision matrix, deterministic direct/ordered parity, source immutability,
and schema `1.9` export validation; the hosted workflow now also requires its
complete report header. No duplicate test framework or product behavior was
added.

`PL-0043` closes `M-14` by extending the existing Validation Set verifier from
`86/86` to `87/87`. One counterfactual fixture changes only Held-out content
and identity while the complete development candidate, limit, order, warning,
confusion, and exact decision fingerprint remains unchanged. The existing
hosted command and shared analyzer remain the owners; no second test framework
or product behavior was added.

`PL-0044` closes `M-13` by qualifying the four existing Runner suites for
exactly the current Prepare catalog. Median Filter, Remove Outlier Pixels,
Level Surface, and ROI/Crop now record identical successful source path,
length, SHA-256, and retained values/counts before and after execution while
preserving a separate deterministic derived output and root provenance. The
existing CI preparation step owns the four-report evidence gate; Transform is
excluded and no product behavior or duplicate verifier framework was added.

## 4. Application Flow

The actual Shell startup and primary C3D flow are:

```text
App.OnStartup
-> ShellVerificationCommandRouter
-> MainWindow
-> ShellMainWindowViewModel
   -> ToolWorkbenchViewModel / Results / CalibrationCenterViewModel
-> lifecycle, display, teaching, request, evidence, and layout coordinators
-> recipe/layout restore and Viewer/source synchronization
-> LoadC3DSourceRequested
-> ShellWorkbenchLifecycleController.LoadC3DSourceRequested
-> Viewer.LoadC3DSourceAsync
-> C3DHeightGrid and render proxy
-> Source Quality and typed teaching selections
-> tool-specific Preview owner
-> OpenVisionLab Vision SDK Execute(...)
-> Preview overlay and metrics
-> explicit Publish
-> ordered Shell Run or Runner replay
-> schema 1.9 JSON / HTML / CSV / Results
-> save and reopen
```

`PL-0038` adds one always-reachable Shell Import surface for the five proven
decoders. C3D follows recipe-source binding; GLB, STL, LAS, and LAZ are
explicitly Viewer-only and preserve recipe/execution state. `.gltf` external
resources and formats without a verified decoder remain unadvertised.

## 5. 3D Data And Format Findings

| Format/data | Actual support | Important boundary |
| --- | --- | --- |
| C3D height field | Import, identified derived save, sampled ASCII PLY export | little-endian width/height plus float cells; zero/non-finite missing; physical calibration is not inferred |
| GLB 2.0 | Triangle mesh import with positions, optional normals/colors/UV, embedded base-color texture, node transforms and instancing | bounded whole-file load; no general `.gltf` external-resource path |
| STL | Binary little-endian and invariant-culture ASCII import with normals | bounded whole-file load; strict validation and 1,000,000-triangle ceiling |
| LAS/LAZ | XYZ, intensity, and supported RGB point formats | audit baseline found synchronous full decode; `PL-0034` now provides cancellable/progress Data loading and off-UI-thread latest-wins Viewer loading with exact-budget reuse |
| PLY | strict binary float XYZ verification reader and sampled ASCII Viewer export | not a general application importer |
| PNG | screenshot, preview, and GLB texture decode | not a height/depth input format |
| OBJ/PCD/XYZ/TIFF/RAW | no current general importer | do not advertise as implemented |

The inspection source and rendering sample are deliberately separate. Display
density does not change measurement input. C3D Viewer height scaling is a
display transform and is not a physical-unit calibration.

## 6. Viewer Capability Matrix

| Capability | Status | Recorded implementation level |
| --- | --- | --- |
| SharpGL rendering | Implemented | fixed-function paths plus C3D VBO/IBO and display-list fallback |
| Perspective camera | Implemented | orbit, pan, zoom, target, fit, reset |
| Orthographic camera | Partial | Top orthographic is a product command; full front/side preset set is absent |
| Coordinate triad and grid | Implemented | persistent orientation and reference surfaces |
| Background | Partial | fixed graphite background; no operator color setting |
| Point size | Implemented | bounded display setting |
| Lighting | Not Found | no OpenGL light/material system |
| Shading/material | Partial | smooth color interpolation and GLB base texture; no general material system |
| Color mapping | Implemented | Source, Solid, Grayscale, Height, Thermal, Deviation, RGB as supported by source |
| Selection/picking | Implemented | C3D, point cloud, mesh triangle/surface normal, typed teaching selections |
| Measurement | Implemented | two-point and inspection-specific result overlays |
| ROI | Implemented in Viewer | GridRectangle, OrientedBox3D, profile and related teaching; downstream Crop output remains partial |
| Clipping | Not Found | Section Plane is a profile/teaching surface, not render clipping |
| Screenshot | Implemented | WPF capture with quality report |
| Performance controls | Partial | density budgets and telemetry exist; maximum supported inputs are not qualified |

Fast/Balanced/Detailed display budgets are `25,000/55,000/140,000` C3D
points, `25,000/50,000/150,000` LAS/LAZ points, and
`25,000/60,000/180,000` imported-mesh triangles.

## 7. Algorithm Findings

Implemented deterministic families include:

- median filtering, local-median outlier removal, and surface leveling;
- height-difference edge extraction;
- two-point line, three-point plane, 3D line fit, line intersection, and
  landmark correspondence;
- XYZ affine solve/apply and deterministic height re-grid;
- bounded rigid surface matching, multiple-match collection, coverage,
  diagnostic edges, symmetry equivalence, and acquisition direction;
- thickness, warpage, plane flatness, point-pair dimensions, gap/flush,
  volume, cross-section dimensions, and completeness grid inspection;
- source summaries, distribution/region statistics, repeatability statistics,
  labeled evidence statistics, and deterministic threshold analysis.

Most Workbench Preview owners use `Task.Run`, `CancellationToken`, running
state, stale-result invalidation, and `CanExecute` guards. Results retain typed
artifacts, identities, hashes, metrics, overlays, and Run Record evidence.

Not implemented as complete product tools are general voxel downsampling,
smoothing, mesh repair/decimation, segmentation,
clustering/blob analysis, general normal estimation, and general ICP. Existing
affine and bounded surface-pose workflows must not be relabeled as general ICP.

## 8. WPF And MVVM Findings

Recent ownership work established concrete lifecycle, execution, state, and
session owners. Event subscriptions and cancellation sources are generally
paired with unload/dispose paths. Heavy inspection Preview work is normally
off the UI thread, and `async void` is mainly restricted to WPF event
boundaries.

Large composition roots remain:

- Shell `MainWindow.xaml.cs`;
- `ToolWorkbenchViewModel` and its Validation Set projection;
- Viewer `MainWindowViewModel`;
- Viewer teaching, rendering, and docking code-behind.

File size alone is not a defect and partial files are not architecture
boundaries. Future extraction is justified only where a responsibility owns
independent state, cancellation, dependencies, change cadence, and a focused
test seam. WPF/OpenGL rendering and pointer bridges should remain View-owned;
durable workflow policy should not move back into code-behind.

## 9. Performance And Stability Findings

### High: C3D peak memory is not bounded

`C3DHeightFieldSnapshot` reads the complete file and creates a `double[]`.
`C3DHeightImageFrame` copies values with `ToArray()` and creates a BGRA bitmap.
The Viewer also retains decoded and sampled/GPU representations. A large load
can therefore overlap raw bytes, `float[]`, multiple `double[]`, bitmap bytes,
render topology, and GPU buffers. The repository correctly leaves the
large-C3D gate blocked until a representative maximum input and accepted
memory/load-time limits exist.

### High: LAS/LAZ decode can block the UI

`LazPointCloud.Load` visits the entire decoded point count synchronously.
Retained memory is sampled, but latency is still O(N), density changes can
repeat the scan, and the Viewer call has no cancellation token.

Status update: `PL-0034` closes this interactive Viewer defect with a
compatible cancellable/progress Data overload, off-UI-thread latest-wins
loading, visible progress, current-sample retention, and exact
source-and-budget reuse. Full decode remains O(N) for a new source/budget and
is not a maximum-input performance claim. See section 17.

### High: imported mesh texture lifetime

The audit confirmed that GLB/STL replacement cleared a generated OpenGL
texture ID without `DeleteTextures`. `PL-0030` is the first implemented audit
follow-up and is recorded in section 15.

### Medium and lower findings

- `[Complete: PL-0035]` GLB/STL now reject over-limit whole files and declared
  geometry/texture ranges before unsafe allocation. See sections 19-20.
- no general processing timeout policy exists; cancellation should remain the
  first recovery mechanism and timeouts should be added only to owned,
  bounded operations;
- best-effort language, dialog, and log-retention cleanup contains empty
  catches that reduce diagnostics;
- no general recipe/result Undo/Redo exists beyond bounded teaching undo;
- no general file drag-and-drop workflow was found.

No current evidence establishes a P0 data-corruption or deterministic crash
defect. Potential maximum-input OOM is retained as a high unqualified risk,
not promoted to a confirmed critical failure without a representative input.

## 10. UX And Product Findings

Strong user-facing behavior includes first-use recipe/source/task setup,
explicit workflow stages, linked Viewer and evidence, contextual Add/teach/
repair routes, safe step removal, source/result comparison, bilingual UI,
docking, sample recipes, Run history, and privacy-safe support export.

Material gaps are the C3D-only primary Open dialog, distributed access to
other implemented decoders, absent drag-and-drop and general Undo/Redo,
limited large-data guidance, synchronous LAS/LAZ loading, and incomplete
calibration profile/physical evidence workflows.

The Calibration Center implements offline software repeatability views. Height
calibration, sensor alignment, profile lifecycle, traceability, uncertainty,
Gauge R&R, and production tolerance remain unavailable or externally blocked.

## 11. Documentation Versus Implementation

| Documented area | Source-grounded result | Judgment |
| --- | --- | --- |
| C3D Load/Teach/Run | complete loader, Workbench, Viewer, Runner, and evidence path | Implemented |
| GLB/STL/LAS/LAZ loading | one exact-format Import action, off-UI-thread decode, progress/cancel, Viewer-only marker, and recipe/execution non-mutation | Implemented operator workflow (`PL-0038`) |
| ROI/Crop | SDK-owned exact crop, immutable origin/mask/identity, Preview/Publish, compatible later-tool teaching, save/reopen, Viewer/Runner evidence | Implemented for bounded HeightField contract (`PL-0037`) |
| Surface Match | SDK, Workbench, Viewer evidence, and Runner parity exist | Implemented for bounded contract |
| General ICP | no general implementation found | Not Found |
| Calibration | repeatability software exists; physical calibration and traceability do not | Partial |
| General Undo/Redo | only bounded teaching undo found | Not Found |
| Run Record schema 1.9 | exact JSON/HTML/CSV and completeness cell evidence exist | Implemented |
| Privacy-safe support ZIP | bounded sanitized bundle and manifest exist | Implemented |
| Large C3D | render sampling exists; maximum decode/inspection budget is not proven | Blocked |
| First-release Phase 1 | the prior frozen package/CI predates PL-0037 and is historical; a new exact commit/package/CI plus owner R0 are required | Incomplete |

Actual source and current verification take precedence over older completion
language. The root README should continue distinguishing a decoder or fixed
Viewer path from a normal integrated operator workflow.

## 12. OpenVisionLab Ecosystem Position

The appropriate long-term role is:

```text
3D file or future approved acquisition adapter
-> OpenVisionLab 3D Studio source qualification and inspection authoring
-> OpenVisionLab Vision SDK numerical tools
-> typed result, ROI, dataset, and Run Record artifacts
-> future Labeling Studio dataset exchange
-> future Machine Studio versioned recipe/Runner handoff
```

Vision SDK integration is real. Labeling Studio and Machine Studio runtime
integration are not currently implemented. Future exchange should use stable
source/frame/unit/artifact identities and versioned manifests rather than UI
selection state. Camera, PLC, robot, cloud, and production-line integration
require an explicit product-direction decision and are not implied by this
ecosystem position.

## 13. Product Scorecard

| Area | Score / 10 | Basis |
| --- | ---: | --- |
| Architecture | 8.0 | strong SDK and execution/evidence boundaries |
| Code quality | 7.2 | strong validation with large composition roots and mixed verifier hosting |
| 3D capability | 6.5 | useful height/mesh/point inspection with important preparation gaps |
| Performance | 6.2 | render budgets exist; peak memory and full point decode remain risks |
| Stability | 7.0 | good validation/lifetime patterns with confirmed texture and large-input gaps |
| UI/UX | 7.5 | coherent inspection workflow with import, cancel, undo, and large-data gaps |
| Testing | 7.3 | broad custom verification plus a thin two-test standard facade; no maximum-input proof |
| Documentation | 8.5 | strong contracts/evidence with high historical volume |
| Developer experience | 7.4 | typed C# API, Runner, and initial standard discovery; most gates remain custom |
| Product value | 7.8 | differentiated deterministic file-first inspection workflow |
| Open-source readiness | 8.0 | public workflow, legal, sample, and privacy boundaries are mature |
| Commercial competitiveness | 4.8 | acquisition, calibrated metrology, scale, and deployment gaps |
| Average | 7.2 | capable internal/open developer product; industrial claims require more evidence |

## 14. Prioritized Development Findings

1. Freeze and qualify a new exact Studio commit/package/hosted-CI identity,
   then run product-owner unaided Wide/Compact R0. Prerequisite: explicit owner
   release-candidate approval; recommended model: none until approval;
   reasoning effort: none.
2. Supply a representative maximum C3D plus accepted peak-memory and load-time
   limits. Prerequisite: owner data and limits. Recommended model: none until
   supplied; reasoning effort: none.
3. `[Complete: PL-0036]` Share one source-scoped decoded C3D snapshot and
   remove avoidable full-size copies. Closure:
   `OPENVISIONLAB_3D_SHARED_C3D_SOURCE_SNAPSHOT_CLOSURE_20260823.md`.
4. `[Complete: PL-0034]` Make LAS/LAZ decode asynchronous and cancellable,
   with progress and bounded sample reuse. Closure:
   `OPENVISIONLAB_3D_LAZ_RESPONSIVE_LOAD_CLOSURE_20260822.md`.
5. `[Complete: PL-0035]` Add explicit GLB/STL allocation guardrails and
   actionable errors. C3D maximum-input qualification remains separately
   blocked on the owner prerequisite. Closure:
   `OPENVISIONLAB_3D_IMPORTED_MESH_ALLOCATION_GUARDRAILS_20260822.md`.
6. `[Complete: PL-0037]` Complete ROI/Crop as a typed Vision SDK tool, Studio
   adapter, explicit Preview/Publish path, compatible later-tool input, Runner
   execution, and immutable evidence. Closure:
   `OPENVISIONLAB_3D_ROI_CROP_TYPED_PREPARATION_CLOSURE_20260823.md`.
7. `[Complete: PL-0038]` Expose only proven decoders through one coherent
   Import surface with progress, cancellation, and truthful limitations.
   Closure: `OPENVISIONLAB_3D_COHERENT_IMPORT_SURFACE_CLOSURE_20260823.md`.
8. `[Complete: PL-0039]` Add a thin conventional test facade over selected
   existing verifiers without creating a new testing architecture. Closure:
   `OPENVISIONLAB_3D_STANDARD_TEST_FACADE_CLOSURE_20260823.md`.
9. `[Complete: PL-0040]` Extend the existing SourceQualityReport verifier with
   malformed and edge-case C3D fixtures and enforce its complete result in CI.
   Closure:
   `OPENVISIONLAB_3D_SOURCE_QUALITY_EDGE_FIXTURE_CLOSURE_20260823.md`.
10. `[Complete: PL-0041]` Prove atomic 3D/Height Image selection and repeated-
    selection suppression on the existing Inspection Workspace verifier.
    Closure:
    `OPENVISIONLAB_3D_CROSS_VIEW_SELECTION_ATOMICITY_CLOSURE_20260823.md`.
11. `[Complete: PL-0042]` Qualify the existing Completeness known-cell golden
    suite and require its exact complete report in CI. Closure:
    `OPENVISIONLAB_3D_COMPLETENESS_KNOWN_CELL_GOLDEN_CLOSURE_20260823.md`.
12. `[Complete: PL-0043]` Prove Good/Bad/Held-out no-leakage with an extreme
    alternate Held-out value/identity and an unchanged complete development
    fingerprint. Closure:
    `OPENVISIONLAB_3D_HELD_OUT_NO_LEAKAGE_CLOSURE_20260823.md`.
13. `[Complete: PL-0044]` Qualify all four current Prepare tools for exact
    source-file/object immutability, separate derived identity, deterministic
    parity, and one four-report CI evidence gate. Closure:
    `OPENVISIONLAB_3D_PREPARATION_SOURCE_IMMUTABILITY_CLOSURE_20260823.md`.
14. Complete `M-12` OrientedBox3D schema/geometry/pointer/Runner coverage.
    Recommended model: `gpt-5.6-sol`; reasoning effort: `medium`.
15. Complete `B-10` deterministic malformed-source diagnostics. Recommended
    model: `gpt-5.6-sol`; reasoning effort: `medium`.
16. Complete `E-13` supported selection kind/role declaration and fail-closed
    matrix. Recommended model: `gpt-5.6-sol`; reasoning effort: `medium`.
17. Define physical calibration, traceability, uncertainty, and Gauge R&R
    evidence only after calibration artifacts and repeated hardware/operator
   data are available. Recommended model: none until prerequisites exist;
   reasoning effort: none.

## 15. First Development Slice — PL-0030

Operator problem: replacing textured GLB meshes during a long Viewer session
could accumulate GPU texture resources because reset discarded the texture ID
without deleting it.

Product principle: rendering resources belong to the WPF/OpenGL bridge and
must be released without changing source identity, recipe state, inspection
execution, or visible UI.

Implemented boundary:

- replacement retains the previous texture ID and marks it for release;
- the next active OpenGL draw calls `DeleteTextures` before a replacement can
  upload;
- a failed upload deletes any texture ID generated by that attempt;
- OpenGL context initialization forgets prior-context IDs rather than trying
  to delete them in the new context;
- a focused smoke reloads the same textured GLB inside one context and records
  upload/release counts.

Verification:

- focused Viewer and standalone host Release builds: zero warnings and errors;
- full Release solution build: zero warnings and errors;
- structure guard: `67/67`;
- actual textured-GLB EXE smoke: `textureUploads=2`, `textureReleases=1`, exit
  `0`;
- Windows reported two monitors; the EXE was placed on leftmost
  `\\.\DISPLAY2` at `[-1920,365,1920,1080]`, and its
  `[-1920,365,1280,760]` window intersected that monitor;
- rendered screenshot inspection showed the textured mesh, grid, and Viewer
  chrome without a blank frame.

Evidence:

- `../.proofline/issues/PL-0030.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260822-pl0030-imported-mesh-texture-lifetime\`.

No visible UI, layout, recipe, measurement, algorithm, source/result,
Preview/Publish/Run, version, package, or frozen `c1b49ec` artifact changed.
This source change is post-freeze development and does not qualify a new
release candidate.

## 16. Completion Record

```text
Status: Complete
Scope: f725ad9 repository analysis plus PL-0030 imported-mesh OpenGL texture lifetime correction
Acceptance criteria: source/document implementation distinction recorded -> pass; confirmed deficiencies and development order recorded -> pass; previous texture deleted before replacement upload -> pass; failed upload cleanup and context boundary -> pass; focused EXE regression and proportional build/structure checks -> pass
Verification: Viewer Release 0 warnings/0 errors; standalone host Release 0 warnings/0 errors; full solution Release 0 warnings/0 errors; structure 67/67; textured GLB actual EXE reload exit 0 with uploads=2/releases=1; selected monitor/window intersection verified
Evidence: this document; .proofline/issues/PL-0030.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0030-imported-mesh-texture-lifetime/
Boundary / next dependency: owner Wide/Compact R0 remains external; large-C3D work remains blocked on a representative maximum input and accepted memory/load-time limits; no physical calibration, Gauge R&R, production, camera, PLC, robot, cloud, commit, push, or release claim is made
```

## 17. Second Development Slice — PL-0034

The LAS/LAZ audit finding is complete for the bounded interactive Viewer
scope. The loader retains its synchronous compatibility surface and adds
cancellation plus monotonic progress. Viewer recipe and density transitions
run decode outside the UI thread, apply only the latest successful request,
retain the prior sample on cancellation/failure, and cache completed exact
source-and-budget samples. A passive localized toolbar status exposes progress
without changing recipe or execution state.

Current Release verification uses the 2,155,617-point compressed public LAZ.
Wide/Compact normal completion, Compact in-flight progress, an overlapping
Detailed-to-Balanced cancellation, and an exact-budget cache hit all pass at
the available 125% scale. The detailed implementation, checks, and boundaries
are in `OPENVISIONLAB_3D_LAZ_RESPONSIVE_LOAD_CLOSURE_20260822.md` and
`.proofline/issues/PL-0034.json`.

## 18. PL-0034 Completion Record

```text
Status: Complete
Scope: PL-0034 asynchronous cancellable/progress LAS/LAZ Viewer loading with latest-request application and exact source-and-budget reuse
Acceptance criteria: loader parity/progress/cancellation -> pass; current point-cloud retention and no stale apply -> pass; visible localized progress -> pass; cache reuse -> pass; current focused/build/runtime/UI/structure checks -> pass
Verification: Release 0 warnings/0 errors; source-channel 29/29; Viewer display 111/111; Shell options 42/42; structure 67/67; current actual EXE Wide/Compact and race/cache smokes exit 0
Evidence: docs/OPENVISIONLAB_3D_LAZ_RESPONSIVE_LOAD_CLOSURE_20260822.md; .proofline/issues/PL-0034.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0034-laz-responsive-load/
Boundary / next dependency: R0 is deferred, not complete; new-source decode remains O(N); large-C3D remains blocked on representative input and accepted budgets; no maximum-input SLA or physical-metrology claim
```

## 19. Third Development Slice — PL-0035

Imported GLB/STL files now fail closed before unbounded whole-file, decoded
accessor, expanded geometry, or embedded-texture allocation. The bounded Data
implementation uses a 512 MiB file ceiling, 3,000,000 GLB accessor/vertex and
index ceilings, a 256 MiB embedded-texture ceiling, exact bufferView/BIN range
validation, and the existing 1,000,000-triangle STL ceiling during preflight or
ASCII parsing.

The focused suite retains public GLB, binary STL, and ASCII STL behavior while
small hostile declarations and sparse over-limit files return actionable
`InvalidDataException` messages. Detailed limits, checks, and boundaries are
in `OPENVISIONLAB_3D_IMPORTED_MESH_ALLOCATION_GUARDRAILS_20260822.md` and
`.proofline/issues/PL-0035.json`.

## 20. PL-0035 Completion Record

```text
Status: Complete
Scope: PL-0035 bounded GLB/STL whole-file, declared geometry, buffer range, embedded texture, and STL triangle allocations
Acceptance criteria: GLB allocation declarations fail before allocation -> pass; embedded texture fails before copy -> pass; STL file and triangle ceilings fail before unsafe growth -> pass; valid import compatibility -> pass; focused/build/structure checks -> pass
Verification: Shell Release 0 warnings/0 errors; source-channel/import 35/35; full solution Release 0 warnings/0 errors; structure 67/67
Evidence: docs/OPENVISIONLAB_3D_IMPORTED_MESH_ALLOCATION_GUARDRAILS_20260822.md; .proofline/issues/PL-0035.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0035-imported-mesh-guardrails/
Boundary / next dependency: visible malformed-import Viewer UI was source-inspected but not runtime-tested; R0 remains deferred; maximum C3D qualification remains blocked on representative input and accepted budgets
```

## 21. Fourth Development Slice — PL-0036

The active Workbench source now has one decoded C3D snapshot lifetime. The
existing `ToolWorkbenchSourceSession` owns the asynchronous task, verifies the
current source binding before sharing, and supplies the same immutable snapshot
to Source Quality and Height Image. Source or binding replacement clears both
the task and a stale Height Image.

The lower data path no longer retains a whole-file byte array while decoding:
`C3DHeightFieldSnapshot` incrementally hashes and parses with one fixed buffer.
`C3DHeightImageFrame` retains the snapshot's read-only value memory rather than
copying the complete decoded array. Detailed ownership proof, verification,
and limitations are in
`OPENVISIONLAB_3D_SHARED_C3D_SOURCE_SNAPSHOT_CLOSURE_20260823.md` and
`.proofline/issues/PL-0036.json`.

## 22. PL-0036 Completion Record

```text
Status: Complete
Scope: PL-0036 active Workbench decoded C3D snapshot sharing, binding verification, streaming source decode, stale Height Image clearing, and zero-copy Height Image raw values
Acceptance criteria: one source-session task/reference -> pass; Source Quality/Height Image shared identity -> pass; stale binding/clear behavior -> pass; exact streaming decode identity/values -> pass; focused/build/structure gates -> pass
Verification: full solution Release 0 warnings/0 errors; shared snapshot/Source Quality 24/24; Inspection Workspace/Height Image 64/64; C3D profile 14/14; distribution 26/26; structure 67/67
Evidence: docs/OPENVISIONLAB_3D_SHARED_C3D_SOURCE_SNAPSHOT_CLOSURE_20260823.md; .proofline/issues/PL-0036.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260823-pl0036-shared-c3d-snapshot/
Boundary / next dependency: maximum-input memory/load-time qualification remains blocked; R0 remains deferred; frozen R0 package unchanged; no physical metrology, commit, push, package, or release claim
```
