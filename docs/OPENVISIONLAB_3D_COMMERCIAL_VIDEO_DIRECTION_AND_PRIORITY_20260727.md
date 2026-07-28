# Commercial 3D Video Direction and Development Priority

Date: 2026-07-27

Status: Complete for the 11 supplied-video review, current-source capability
mapping, and durable priority decision

## Executive decision

OpenVisionLab 3D Studio should be developed as:

> A local, file-first, deterministic 2.5D/3D rule-based inspection workbench
> that turns identified height fields, point clouds, and meshes into teachable
> regions, explicit inspection results, repeatable validation, and durable
> evidence.

It should not become a general sensor-management or factory-integration
platform in the current phase.

The common lesson from GoPxL, SICK Nova, MVTec HALCON/MERLIC, Zivid Studio,
and Photoneo PhoXi Control is not merely that a 3D Viewer needs more buttons.
Their useful inspection workflow is:

```text
source identity and data-quality review
  -> 2D height/depth/intensity map for fast region teaching
  -> 3D surface for height, occlusion, pose, and volume review
  -> typed preprocessing, feature, alignment, and measurement steps
  -> explicit parameter/threshold decision
  -> good/bad sample replay
  -> visible per-region and per-step evidence
  -> saved recipe/job and repeatable result record
```

OpenVisionLab already has a strong local recipe/execution foundation and the
new Inspection Workspace v3. The next work should extend that foundation in
the order recorded below. It should not restart the Shell or rewrite the
existing C3D loader, recipe, Runner, Viewer, ROI editing, or measurement
adapters.

The exhaustive implementation inventory, release-train order, dependency
graph, evidence gates, per-item status, and first 20-item executable queue are
maintained in:

- `docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`

This document remains the source-by-source commercial-video analysis. The
master backlog is the execution source of truth when selecting a development
item.

## Scope and evidence

All 11 supplied media files under `C:\Git\GoPxL_Video\3D` were checked with
`ffprobe`. Ten have English subtitles. The short Zivid Capture Assistant clip
has no subtitle and was assessed from its complete visible sequence and
on-screen text. Cleaned English subtitle files were used where both cleaned
and word-level/original variants exist; the alternate files are supporting
transcripts, not separate videos.

| # | Video | Duration | Resolution | SHA-256 prefix |
| ---: | --- | ---: | ---: | --- |
| 1 | `GoPxL GUI - Walk Through.mp4` | 244.874 s | 3840 x 2160 | `6D538DA81AFCAB18` |
| 2 | `3D_01_SICK_Nova_3D_Overview.mp4` | 417.654 s | 1920 x 1080 | `3005C2899D3D4A41` |
| 3 | `3D_02_SICK_Nova_3D_Presence_Inspection.mp4` | 234.514 s | 1920 x 1080 | `419F524D0B2223CC` |
| 4 | `3D_03_MVTec_HALCON_Surface_Based_Matching_Introduction.mp4` | 315.654 s | 1920 x 1080 | `8161E2C4E8DC1A4D` |
| 5 | `3D_04_HALCON_Optimize_3D_Surface_Matching_Data.mp4` | 416.234 s | 1920 x 1080 | `96218612FEDB9864` |
| 6 | `3D_05_HALCON_Edge_Supported_Surface_Matching.mp4` | 267.014 s | 1920 x 1080 | `6815FA20473740F8` |
| 7 | `3D_06_HALCON_Stereo_Surface_Reconstruction.mp4` | 461.634 s | 1920 x 1080 | `1D38A89A0E646278` |
| 8 | `3D_07_Zivid_Studio_First_Point_Cloud.m4v` | 198.323 s | 1920 x 1080 | `B0AE9971D5AFFBB7` |
| 9 | `3D_08_Photoneo_PhoXi_Control_First_Scan.mp4` | 283.533 s | 1920 x 1080 | `EF55FCED4D88F5EB` |
| 10 | `3D_09_MVTec_MERLIC_Height_Image_Fill_Inspection.mp4` | 306.554 s | 1920 x 1080 | `19BFAED5B145A470` |
| 11 | `3D_10_Zivid_Studio_Capture_Assistant.m4v` | 62.771 s | 1920 x 1080 | `B4B012B2723099E9` |

Four representative frames per video and one contact sheet per video are
preserved under:

- `artifacts/current/20260727-commercial-video-direction/`

The review is limited to the behavior visible or stated in these files. It
does not claim to cover every feature in the current commercial products.

## Video-by-video analysis

### 1. GoPxL GUI Walk Through

Observed:

- a stable left responsibility rail separates Manage, System, Inspect,
  Connect, and Report;
- jobs, backup/restore, support bundles, alignment, scan settings, tools,
  industrial outputs, health, measurements, and performance are not mixed
  into one page;
- the Tools page is the inspection-authoring home, while measurement and
  execution performance remain visible reporting surfaces.

Lesson for OpenVisionLab:

- keep recipe authoring, result evidence, validation, and diagnostics as
  recognizable responsibilities;
- keep the current compact Inspection Workspace centered on the selected
  tool instead of restoring many permanent workflow cards;
- expose per-step timing and result evidence in the existing lower
  Problems/Messages/Performance/Validation area.

Current mapping:

- the Inspection Workspace, Recipe Chain, Selected Tool, Run Record,
  Validation Set, logging, and performance evidence provide a strong local
  foundation;
- sensor discovery, alignment hardware, industrial protocols, HMI, and
  system health are intentionally out of scope.

### 2. SICK Nova 3D Overview

Observed:

- Configure and Run are visibly different operating states;
- a top-down 2D intensity/height representation is used for fast ROI
  teaching, while one action switches to an interactive 3D view;
- the operator rotates, pans, zooms, resets, changes height colors, hides the
  floor, and adjusts the displayed height range;
- Blob Finder is added as a tool, its ROI is taught on a stopped good sample,
  and result limits convert the measured count into Pass/Fail;
- free-running samples are then used to validate the configuration.

Lesson for OpenVisionLab:

- preserve explicit Preview and Run rather than copying free-running hidden
  execution;
- make a full-size coordinate-true height image a first-class teaching
  surface, synchronized with the 3D surface;
- keep limits and current result evidence beside the selected tool.

Current mapping:

- true Top orthographic, Perspective, Surface default, height coloring,
  explicit Preview/Run, ROI editing, parameters, and result limits exist;
- the current linked `Height Map` is a small read-only preview, not an
  interactive 2D teaching workspace;
- connected-component/blob inspection is not implemented.

### 3. SICK Nova 3D Presence Inspection

Observed:

- a good sample is acquired first;
- Blob Region Finder creates an oriented bounding region whose output is
  reused by a downstream Completeness Check;
- the 2D view teaches the footprint and the 3D side view verifies the
  vertical extent;
- the completeness tool defines a cell grid and judges height/coverage per
  cell;
- good and bad samples feed an `Estimate thresholds` action, after which the
  operator can still edit and explicitly apply the values;
- replay visibly distinguishes a complete and an incomplete part.

Lesson for OpenVisionLab:

- typed output chaining should include region artifacts, not only numeric
  measurements;
- an actual volume ROI needs a vertical extent and 3D review;
- repeated geometry and actual per-cell presence judgment are separate
  responsibilities;
- suggested thresholds must show their sample evidence and require explicit
  operator application.

Current mapping:

- typed artifacts, automatic compatible routing, dual ROI Thickness,
  `4 x 2` repeat authoring, Validation Set, and explicit Apply are useful
  foundations;
- `GridRectangle` is only an X=column/Z=row height-field footprint;
- `OrientedBox3D`, completeness/cell-occupancy inspection, connected regions,
  and good/bad threshold estimation are not implemented.

### 4. HALCON Surface-Based Matching Introduction

Observed:

- a CAD/object model is sampled into a surface model;
- model and scene are checked for usable points and consistent normals;
- matching returns a pose and a score;
- the model is transformed by the returned pose and overlaid in the scene;
- the score is explained as visible surface coverage, so an occluded object
  may have a valid maximum below one;
- debug views expose scene samples, model samples, and key points.

Lesson for OpenVisionLab:

- a future matcher needs explicit model-preparation, model identity, scene
  identity, pose, score semantics, and overlay evidence;
- a generic label such as `confidence` is insufficient unless its physical
  meaning is stated;
- normals and key points are inspection evidence, not hidden implementation
  details.

Current mapping:

- nominal/actual comparison, landmark correspondence, affine solve/apply,
  transformed point clouds, and result overlays are adjacent foundations;
- there is no general surface-model training, pose search, surface-coverage
  score, or matching-debug workspace.

### 5. HALCON Optimize 3D Surface Matching Data

Observed:

- model preparation removes internal, redundant, and unobservable surfaces;
- symmetry and allowed rotations constrain the search;
- scene preparation checks XYZ mapping, invalid zero pixels, noise, and
  background points;
- median filtering, height thresholds, saved-background subtraction, 3D
  distance, region growing, and reduced domains improve robustness and
  runtime;
- false positives, multiple matches, score, and elapsed time are reviewed
  together.

Lesson for OpenVisionLab:

- source quality and preprocessing must become an explicit typed stage;
- model and scene preparation require different contracts;
- search constraints and runtime evidence belong with a future matcher;
- filters should create identifiable derived artifacts and never silently
  modify the source.

Current mapping:

- source SHA/grid identity, finite/missing counts, height distribution,
  Median Filter, ROI/Crop catalog entry, transformed-field coverage,
  collisions, and explicit derived artifacts are partial foundations;
- there is no unified source-quality report, invalid-pixel map, background
  model/subtraction, model-surface cleanup, symmetry contract, or pose-range
  constraint.

### 6. HALCON Edge-Supported Surface Matching

Observed:

- surface-only matching can return a plausible false background match;
- trained 3D edges add a second independent score;
- surface score and edge score remain separate;
- XYZ mapping, invalid/duplicate points, model edge extraction, acquisition
  viewpoint, and normal/edge directions are debugged interactively;
- calibrated intensity or additional cameras can be fused later.

Lesson for OpenVisionLab:

- advanced matching must expose multiple evidence components instead of one
  unexplained aggregate score;
- acquisition viewpoint and normal direction are part of data validity;
- 2D/3D fusion is a later extension, not a reason to weaken the current
  deterministic 3D foundation.

Current mapping:

- Height Difference Edge, 3D Line Fit, Line Intersection, diagnostics, and
  separate feature artifacts exist;
- they do not constitute an edge-supported surface matcher;
- viewpoint-aware normals, duplicate-point diagnostics, and multimodal
  registration are not implemented.

### 7. HALCON Stereo Surface Reconstruction

Observed:

- two or more calibrated cameras produce disparity from image
  correspondences;
- texture, camera viewpoint, triangulation angle, calibration, and projector
  pattern determine reconstruction completeness and depth accuracy;
- a tight 3D bounding box reduces runtime and noise;
- persisted disparity and score images support parameter tuning;
- pairwise reconstruction favors matching input, while fusion improves
  noise/outlier suppression and visual closure;
- shiny, transparent, or textureless objects are called out as limitations.

Lesson for OpenVisionLab:

- reconstructed 3D input needs provenance and quality limitations, not just a
  file path;
- bounding volume, invalid/disparity maps, coverage, and acquisition
  limitations should be visible when the source provides them;
- a stereo reconstruction engine is not required for the current local
  inspection target.

Current mapping:

- file identity, frame/unit fields, coverage, missing cells, and bounds exist
  in several current contracts;
- camera setup, disparity, reconstruction score, fusion, and acquisition
  provenance are absent and remain outside the current implementation scope.

### 8. Zivid Studio First Point Cloud

Observed:

- the main view switches among colored/texture point cloud, 2D color map,
  and depth map;
- Manual Mode exposes exposure and filters;
- Assisted Mode accepts a maximum capture time, analyzes the scene, captures,
  and produces multiple frames/settings;
- the suggested settings can be returned to Manual Mode, pruned, refined,
  and later transferred to the SDK.

Lesson for OpenVisionLab:

- an assistant should propose a bounded, reviewable draft and then return the
  operator to ordinary editable parameters;
- alternative diagnostic maps should share one source identity;
- assisted behavior must expose inputs, suggestions, and final explicit
  acceptance.

Current mapping:

- typed PropertyGrid drafts, Apply/Discard, source/result identity, color
  modes, Filter, and explicit Preview provide a compatible interaction model;
- live capture, exposure, frame selection, and SDK export are intentionally
  absent.

### 9. Photoneo PhoXi Control First Scan

Observed:

- device discovery shows connection state before a device is selected;
- connection changes the UI into Settings, Structure/output maps, Viewer, and
  device-information responsibilities;
- Trigger Scan and Free Run are distinct;
- `Set` applies temporary settings, while `Set and Store` persists a profile;
- the Viewer can inspect normals and color schemes, and scans can be saved in
  different formats.

Lesson for OpenVisionLab:

- temporary draft state and persisted recipe state must remain visibly
  distinct;
- source maps and diagnostic views should be selectable without changing the
  recipe;
- connection and acquisition status are useful lessons only if a later scope
  decision adds sensor adapters.

Current mapping:

- detached PropertyGrid drafts, dirty/saved state, Save/Save As, display-only
  Viewer modes, local source load, and export evidence are present;
- device discovery, Trigger/Free Run, sensor profiles, and raw output-map
  configuration are out of scope.

### 10. MERLIC Height Image Fill Inspection

Observed:

- invalid disparity pixels are visible and can be removed with an outlier
  filter;
- a tilted height image is leveled by drawing ROIs on the reference surface;
- gray range is scaled for later 2D tools, or disparity is converted to a
  metric height image;
- the moving box is aligned before fill inspection;
- two compartment ROIs are trained with good and bad samples;
- processing exposes per-region result, confidence, and an all-regions
  accepted result.

Lesson for OpenVisionLab:

- height-image preparation, leveling, alignment, and inspection should be one
  readable chain;
- good/bad evidence is more useful than arbitrary default tolerances;
- per-region evidence must remain inspectable even when an aggregate result
  exists.

Current mapping:

- Median Filter, datum/plane fitting, affine/re-grid pipeline, dual ROI
  measurement, Validation Set, per-step metrics/overlays, and aggregate Run
  status are partial foundations;
- the current height image is not a full teaching surface;
- outlier-mask authoring, a typed Level Surface tool, sample-class training,
  per-region confidence semantics, and threshold suggestion are missing.

### 11. Zivid Studio Capture Assistant

Observed from the complete 62.771-second visible sequence:

- the workflow is reduced to `Connect to camera -> Select capture mode ->
  Adjust settings -> Capture`;
- Assisted Mode analyzes the scene and produces suggested frame/exposure
  settings;
- Zivid Studio is positioned for evaluation, while the SDK is the later
  application path.

Lesson for OpenVisionLab:

- assistants should be short, task-specific, and end in an ordinary editable
  recipe;
- the relevant pattern is `analyze -> propose -> review -> explicit apply`,
  not camera control itself.

Current mapping:

- the selected-tool workspace, typed drafts, Validation Set, and explicit
  Apply/Preview can host a future threshold/data-preparation assistant;
- camera connection and capture remain out of scope.

## Current capability map

The state labels mean:

- **Implemented**: present in current source with focused verification;
- **Partial**: adjacent capability exists, but the commercial workflow
  requirement is not complete;
- **Missing**: no current typed product contract was found;
- **Out of scope**: deliberately excluded from the current local product.

| Capability | State | Current evidence or boundary |
| --- | --- | --- |
| Identified local C3D/file input | Implemented | path, byte length, SHA-256, grid identity, frame/unit fields, last-recipe restore |
| Dominant interactive 3D surface | Implemented | Surface default, height colors, Perspective/Top, fit, picking, HUD, split/pop-out |
| Ordered typed recipe and artifacts | Implemented | schema `1.3`, Recipe Chain, selected inputs/outputs, Artifact Registry, Runner |
| Explicit execution lifecycle | Implemented | parameter Apply/Discard, ROI Apply/Cancel, explicit Preview/Publish/Run |
| X/Z height-field ROI | Implemented | `GridRectangle`, role-aware editing, numeric geometry, same-ID replacement |
| Full-size interactive height image | Implemented | native-grid Height Image supports Fit/1:1/zoom/pan, palette/range, linked hover, and reusable split/pop-out presentation |
| 2D/3D synchronized ROI editing | Implemented | shared ID/geometry/role colors, actual-pointer draw/move/resize, Review/Apply/Cancel/Delete, and reversible Height Image edit focus pass current evidence |
| True XYZ/volume ROI | Missing | no `OrientedBox3D`; display-only ROI Y position is not geometry or measurement extent |
| Median denoise | Implemented | typed Filter creates `FilteredHeightField` and preserves the missing mask |
| Source-quality dashboard and invalid map | Partial | valid/missing counts and height histogram exist; no unified preflight or invalid-pixel canvas |
| Level surface and background preparation | Partial | plane/affine/re-grid primitives exist; no selected typed Level Surface or background-subtraction workflow |
| Deterministic measurement set | Implemented | Thickness, Warpage, Flatness, Point Pair, Gap/Flush, Volume, Cross-section, datum deviation |
| Repeated Tab geometry authoring | Implemented | `4 x 2` display-only review then eight ordinary dual-ROI Thickness steps |
| Presence/completeness cell inspection | Missing | repeat authoring does not calculate cell presence, height, or coverage |
| Sample-set replay and failure analysis | Implemented | Validation Set, filters, issue navigation, per-step metrics/overlays |
| Good/bad threshold teaching | Missing | Validation Set does not label training roles or suggest/apply thresholds |
| Feature/datum chain | Implemented | edges, lines, intersection, landmarks, affine solve/apply, re-grid |
| General surface matching | Missing | no surface-model artifact, pose search, coverage score, or matcher |
| Matching preparation/debug | Missing | no normals/keypoints/symmetry/pose-range/edge-score matcher diagnostics |
| Nominal/actual comparison | Implemented for fixed local evidence | fixed-scope point/mesh deviation and durable evidence; not a general surface matcher |
| Run evidence and local export | Implemented | JSON/HTML/CSV Run Record, history/open/export, Viewer/Runner parity evidence |
| Physical metrology trust | Unverified | calibration capability does not prove traceability, uncertainty, GR&R, or production tolerance |
| Sensor capture and factory integration | Out of scope | camera, PLC, robot, fieldbus, HMI, cloud, and plant management remain excluded |

## Product maturity without a misleading single percentage

Use separate denominators:

- Inspection Workspace v3 implementation is `7/8` bounded slices (`87.5%`);
  only the owner's unaided exact-source replay remains.
- The local deterministic inspection foundation is operational: loading,
  recipe authoring, explicit execution, measurements, Validation Set, Runner,
  and Run Record all have focused evidence.
- Linked Height Image/3D teaching is implemented for `GridRectangle`;
  commercial-style source preparation and true XYZ region teaching remain
  partial or missing.
- Presence/completeness and evidence-based threshold teaching are missing.
- General surface matching and its diagnostics are missing.
- Live acquisition/factory-platform completeness is not a target and must not
  be included in a product-completeness percentage.
- Physical calibration and metrology credibility remain unverified rather
  than assigned a percentage.

## Prioritized development list

The owner's unaided Inspection Workspace v3 replay is a prerequisite, not
another implementation slice:

```text
Prerequisite: owner at the running current Release application
Gate: complete the documented 12-step exact-source workflow without guidance
Action while unavailable: do not spend model tokens repeating implementation
```

After that gate, proceed in this order.

### P0-A. Source Quality and Preparation contract

Outcome:

- one source-bound `SourceQualityReport` records grid/point count,
  finite/missing ratio, height range/distribution, invalid-cell mask identity,
  frame/unit/provenance, and available diagnostic channels;
- the Workbench shows the report before measurement teaching;
- the report never runs inspection or changes the source;
- later filters consume the identified source and create separate outputs.

First acceptance:

- the exact Synthetic Thickness Coupon v1 C3D shows a coordinate-true invalid/valid mask plus the
  current histogram and identity;
- Viewer and headless verification agree on counts and SHA-256;
- unsupported normals, confidence, or acquisition fields remain explicitly
  unavailable rather than fabricated.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P0-B. Full-size linked Height Image workspace

Outcome:

- add a reusable height-image Viewer slot backed by the exact C3D grid;
- support pan, zoom, Fit, height palette/range, and optional invalid-mask
  overlay;
- synchronize selected step, Reference/Measurement ROI, hover coordinate,
  role color, move, resize, Delete, Review, Apply, and Cancel between the
  height image and 3D surface;
- keep Top orthographic as the 3D top view; do not rename it as the height
  image.

First acceptance:

- one Thickness ROI can be drawn and corrected in the height image, inspected
  in Perspective, and explicitly applied with the same selection ID;
- view changes do not dirty or execute the recipe;
- ROI edits remain recipe-only until explicit Preview.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P1-A. Typed `OrientedBox3D` selection

Outcome:

- add a new recipe selection kind with center, three orthonormal axes, and
  half extents in a declared frame/unit;
- provide top/side/perspective handles and exact numeric editing;
- keep `GridRectangle` schema and behavior unchanged;
- only tools that declare `OrientedBox3D` support can consume it.

First acceptance:

- save/reopen and Runner round-trip one oriented volume ROI;
- its vertical Y extent is visually obvious in 3D and numerically editable;
- changing view-only GridRectangle overlay Y never changes this volume;
- legacy schema `1.3` recipes remain compatible.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P1-B. Evidence-based threshold assistant

Outcome:

- let Validation Set samples be assigned explicit `Good`, `Bad`, or `Held
  out` roles without changing their C3D bytes;
- calculate per-step metric distributions and candidate thresholds;
- show a confusion/error table and the exact samples supporting a suggestion;
- require explicit Apply to update the PropertyGrid draft, then explicit
  Preview/Run;
- prove the selected threshold on held-out samples.

First acceptance:

- one existing Thickness or Warpage step has at least one good, one bad, and
  one held-out sample;
- suggestion evidence is reproducible;
- Cancel leaves the recipe unchanged;
- Apply changes only the parameter draft until ordinary parameter Apply.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P1-C. Completeness / cell-occupancy inspection

Outcome:

- add one deterministic tool that divides a taught region into rows and
  columns;
- calculate finite coverage and a height statistic per cell against an
  explicit reference/threshold;
- expose per-cell Pass/Fail overlays plus failed-cell count and aggregate
  result;
- reuse repeat-grid geometry concepts without treating eight Thickness steps
  as a completeness algorithm.

First acceptance:

- the Synthetic Thickness Coupon v1 Tab layout or a deterministic synthetic fixture produces
  named cell results with known expected Pass/Fail;
- every failed cell is selectable and visible in both height image and 3D;
- Workbench and Runner results match.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P2-A. Typed height-field preparation steps

Implement only as evidence requires:

1. invalid/outlier mask handling;
2. Level Surface from explicit reference ROIs;
3. Reduce Domain/Mask;
4. saved-background subtraction.

Every step must preserve the source, emit a typed derived height field, record
valid/missing changes, and keep Preview/Publish explicit. Do not add a generic
filter collection without one sample and one measurable acceptance gate per
step.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P2-B. Surface-matching foundation

Outcome:

- introduce identified `SurfaceModel`, scene input, model-preparation result,
  pose result, and overlay contracts;
- validate points, normals, units, frames, and model/scene identity before
  search;
- define the score as visible/model surface coverage or another explicit
  metric;
- return pose, score components, runtime, and transformed-model overlay;
- prove one deterministic synthetic or redistributable fixture in Workbench
  and Runner.

This is a new algorithm/product slice. Do not present landmark affine solving
or nominal/actual deviation as general surface matching.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

### P3. Matching optimization and debug evidence

Only after P2-B passes:

- model key points and sampled surfaces;
- normal and viewpoint diagnostics;
- symmetry and bounded rotation/search constraints;
- background/mask integration;
- separate surface and 3D-edge scores;
- false-positive and multiple-match review;
- performance budget and failure reasons.

Intensity or extra-camera fusion remains later and requires a separate
calibration/scope decision.

Recommended model: `gpt-5.6-sol`

Reasoning effort: `high`

## Explicitly deferred or rejected

Do not implement these from the supplied videos in the current phase:

- camera discovery, exposure, trigger, free run, frame acquisition, or sensor
  SDK export;
- stereo calibration/reconstruction and projector control;
- PLC, robot, Ethernet/IP, Profinet, Modbus, ASCII, HMI, cloud, accounts, or
  plant health;
- automatic Preview/Run caused by ROI, parameter, selection, visibility, or
  Viewer changes;
- an opaque AI anomaly trainer;
- a free-form node graph;
- changing `GridRectangle` or its display-only Y position into a volume ROI;
- claiming calibrated thickness or metrology without physical evidence.

## Dependency sequence

```text
Owner unaided Workspace v3 replay
  -> P0-A Source Quality contract
  -> P0-B linked Height Image workspace
  -> P1-A OrientedBox3D
  -> P1-B threshold assistant
  -> P1-C completeness
  -> P2-A additional preparation tools
  -> P2-B surface matching foundation
  -> P3 matching optimization/debug
```

P1-B can begin after P0-A if it uses existing GridRectangle tools. P1-C
depends on P0-B for practical evidence review. P2-B depends on P0-A and should
not block the near-term height-field inspection workflow.

## Durable handoff rules

Future chats must:

1. read this document after `AGENTS.md` and the current next-session handoff;
2. preserve the current Inspection Workspace v3, explicit execution
   lifecycle, and rule-based product identity;
3. check the owner replay prerequisite before selecting implementation;
4. select the first incomplete priority above whose dependencies pass;
5. define one typed input/output contract, one sample, one failure case, and
   Workbench/Runner evidence before implementation;
6. update this document only when evidence changes a priority, capability
   state, or scope boundary.

## Verification

Performed for this documentation checkpoint:

- `git status --short` and `git log --oneline -5`;
- `ffprobe` duration, resolution, codec, and byte metadata for all `11/11`
  video files;
- SHA-256 identity check for all supplied media and subtitle files;
- complete cleaned subtitle review for the ten subtitled videos;
- full visual sequence and on-screen text review for the subtitle-free Zivid
  Capture Assistant clip;
- `44` fresh representative frame extracts and `11` contact sheets;
- current handoff, GoPxL redirection, Inspection Workspace v3, repeat-grid,
  product-target, commercial-gap, and Viewer-research document review;
- current source search for recipe selection kinds, tool catalog, Height Map,
  data-quality evidence, Validation Set, and absent commercial capability
  types.

No product code or UI behavior was changed by this research checkpoint, so a
build or UI before/after capture is not a meaningful acceptance requirement.

## Completion record

Status: Complete

Scope: All 11 supplied commercial 3D videos reviewed individually; current
OpenVisionLab implementation classified as Implemented, Partial, Missing, or
Out of scope; product identity, dependency order, acceptance-oriented
priorities, and explicit non-goals recorded.

Acceptance criteria:

- video inventory and source identity -> pass, `11/11`;
- per-video workflow and product lesson -> pass, `11/11`;
- current-source capability mapping -> pass, code and current documents
  checked;
- prioritized implementation list with dependencies and acceptance evidence
  -> pass;
- future-chat continuation route -> pass, this document is referenced by
  `AGENTS.md`, the product target, and the next-session handoff.

Verification: Metadata, subtitles, fresh frames/contact sheets, current docs,
and current source searches listed above completed without a missing supplied
video.

Evidence:

- this document;
- `artifacts/current/20260727-commercial-video-direction/`;
- the exact local media under `C:\Git\GoPxL_Video\3D`.

Boundary / next dependency: The research and roadmap are complete. Product
implementation remains gated first by the owner's unaided current Release
replay. Physical calibration, traceability, uncertainty, GR&R, and production
tolerances remain external prerequisites for certified measurement claims.
