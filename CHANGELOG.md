# Changelog

This forward-looking log records notable user-visible changes from the current
`0.4.6-dev` development line onward. It does not claim that a version was
released; publication state is owned by the release and version policy.

## [Unreleased] - 0.4.8-dev

### Added

- Added authenticated TCP transaction transfer for the 3D integration
  workspace, including runtime build identity and explicit listener/client
  controls.
- Preserved the file-first lifecycle: receiving bytes does not acknowledge,
  load a recipe, run an inspection, or publish a Result automatically.

### Changed

- Isolated cancellable C3D source path validation, decode, render-topology, and
  CPU preparation timing from the WPF Viewer control into a WPF-neutral
  Loading owner without changing the public load, recipe, raw-value, or
  explicit Preview/Publish/Run contracts.

- Isolated teaching-capture source identity validation and C3D
  GridRectangle/GridCircle/GridPolygon point preparation from the WPF Viewer
  control into a WPF-neutral owner without changing the public Viewer,
  Selection, recipe, or explicit Preview/Publish/Run contracts.

- Isolated Surface Match evidence validation, display-frame mapping,
  correspondence projection, and optional edge render-snapshot preparation
  from the WPF Viewer control into a Core/Data-only owner without changing the
  public Viewer or recipe/Run Record contracts.

- Isolated Current Recipe Run Smoke workspace preparation, bound-command
  activation, ordered-result waiting, and activation evidence from
  `MainWindow` into an explicit Shell verification owner without changing
  recipe, Run Record, or Preview/Publish/Run contracts.

- Extracted Viewer workspace presentation/layout Smoke orchestration from
  `MainWindow` into a dedicated Shell verification owner without changing
  Viewer workspace or workflow contracts.

- Isolated Surface Match collection navigation, disabled-state, Published
  focus/hover, popup opening, and popup evidence capture from `MainWindow` into
  an explicit Shell verification owner without changing recipe or Run Record
  contracts.
- Isolated Validation Threshold Assistant Smoke dock preparation, visual lookup,
  coordinate projection, and evidence formatting from `MainWindow` into an
  explicit Shell verification owner without changing recipe or Run Record
  contracts.
- Isolated Preparation Preset Assistant Smoke configuration, popup capture,
  visual lookup, and evidence formatting from `MainWindow` into an explicit
  Shell verification owner without changing recipe or Run Record contracts.
- Expanded the public documentation map with deterministic integration,
  verification, and workflow evidence while preserving the software-only
  product boundary.

### Added

- Added one first-use Recipe Center for recipe identity, C3D source, compatible
  starter selection, remembered setup, and explicit reset.
- Added contextual tool setup, compatible input routing, recipe-health counts,
  and direct navigation to incomplete requirements.
- Added safe same-grid Thickness variants and an explicit saved-recipe ordered
  Run that writes reviewable Results evidence.
- Added Top-view GridRectangle teaching with exact live row, column, width, and
  height before Apply.
- Added schema `1.6` GridCircle teaching with center/boundary drawing, exact
  center-row, center-column, and radius editing, explicit Apply/Cancel, and
  save/reopen/Runner parity without an implicit inspection consumer.
- Added persistent selected `X / Y / Z` and C3D raw-height status in the Viewer.
- Added Run Record stage timing, exact Source Quality evidence, and exact
  Completeness per-cell JSON, HTML, and CSV results.
- Added an explicit privacy-safe support ZIP with a manifest, sanitized recipe,
  bounded session-log excerpt, source identity, Source Quality evidence, and
  current result while excluding raw 3D bytes and private workstation data by
  default.
- Added typed ROI/Crop preparation with an immutable smaller HeightField,
  preserved missing cells and source-grid origin, explicit Preview/Publish,
  compatible later-tool teaching, and ordered Runner replay.
- Added one localized 3D Import action for exact C3D, GLB, STL, LAS, and LAZ
  files, with progress/cancel, C3D recipe binding, and truthful Viewer-only
  GLB/STL/LAS/LAZ state that preserves the recipe and current view on failure.
- Added four deterministic Source Quality grid-integrity checks—topology,
  locator order, duplicate locators, and coordinate finiteness—with localized
  Source Quality/Results state and exact JSON, HTML, CSV, text, and privacy-
  safe support-bundle evidence.
- Added an explicit source-bound GridRectangle Presence Check with inclusive
  finite-coverage and raw-height limits, fail-closed missing-feature behavior,
  Workbench Preview/Publish, ordered Runner/Run Record parity, and JSON, HTML,
  and CSV feature evidence.
- Added deterministic connected-region detection and metrics with explicit
  source-bound masks, Four/Eight connectivity, Workbench evidence, and Runner
  golden verification.
- Added bounded 2D height-image alignment over immutable reference and moving
  C3D snapshots, with explicit software pose and acceptance evidence.
- Added deterministic rigid-pose preparation from ordered point pairs and
  constrained all-correspondence best-fit alignment with residual evidence.
- Added bounded height-field background removal/subtraction and nearest-distance
  point-cloud filtering, each with separate derived-output identity and replay
  evidence.
- Added selected connected-region component preparation that preserves exact
  source-grid cells and missing values in a separate derived raw-height output.
- Added a typed HeightMap integration exchange and consumer smoke path with
  explicit acknowledgement, result publication, and Run Record evidence.
- Added bounded selected-region transform propagation from a raw C3D source,
  exact connected-region artifact, and published affine transform into a
  separate immutable result with deterministic membership, missing preservation,
  JSON identity, and fail-closed metadata guards.

### Improved

- Added one explicit per-tool selection kind/role matrix so unsupported,
  undeclared, and wrong-cardinality selection routes fail before execution,
  while incomplete drafts remain saveable for explicit repair.
- Qualified `OrientedBox3D` schema/current-schema round-trip and fail-closed
  geometry through an exact named 11-case Runner/CI subset, refreshed repeated
  Wide/Compact seven-gesture pointer evidence, and restored the Viewer cursor
  and status after leaving a box handle.
- Qualified all four current Prepare tools—Median Filter, Remove Outlier
  Pixels, Level Surface, and ROI/Crop—for exact source-file/value immutability,
  separate deterministic derived-output identity and root-source provenance,
  with one exact four-report CI evidence gate. Transform tools are unchanged.
- Hardened Validation Set no-leakage verification so changing only a Held-out
  sample's value and identity cannot alter development candidates, limits,
  ranking, confusion counts, warnings, or exact sample decisions.
- Qualified the existing 30-case Completeness known-cell golden suite and made
  CI reject an incomplete report without changing product execution behavior.
- Improved linked 3D/Height Image ROI selection verification with exact atomic
  event counts, repeated-selection suppression, and recipe/execution
  invariants on the existing Workbench CI path.
- Improved SourceQualityReport regression coverage with signed finite-height
  statistics, malformed C3D topology fixtures, cleanup checks, and a complete-
  report CI gate on the existing Runner verifier.
- Improved malformed C3D handling with stable header, dimension, overflow, and
  payload-length reasons while retaining the previous source after an
  asynchronous load failure. Contradictory current diagnostic payloads now
  fail closed.
- Moved reusable grid-diagnostic calculation into the committed and vendored
  OpenVisionLab Vision SDK `GridDiagnosticsTool`; Studio retains only C3D
  format policy, typed adaptation, contract validation, and evidence/UI
  composition.
- Improved contributor verification with one .NET 10 `dotnet test` facade for
  two existing headless Data verifiers and a zero-test-resistant CI gate.
- Improved Tool Library search-context recovery and language-selector popup
  readability in Wide and Compact layouts.
- Improved Results density while retaining timing, source quality, status, and
  evidence identity.

### Compatibility

- Current generic Tool Recipes use schema `1.6`; earlier `1.0` through `1.5`
  forms retain their bounded meanings and cannot contain `GridCircle`.
- Current ordered and Surface Match Run Records use schema `1.9`.
- Current Source Quality reports use schema `1.1`; legacy schema `1.0` remains
  readable only without grid diagnostics and retains its exact compatibility
  fixture identity.
- Schema `1.8` and older optional-field records remain readable under their
  documented compatibility boundaries.
- Product version is `0.4.3-dev`; no tag, release candidate, or public
  release is created by these changes.
