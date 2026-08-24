# Changelog

This forward-looking log records notable user-visible changes from the current
`0.1.1-dev` development line onward. It does not claim that a version was
released; publication state is owned by the release and version policy.

## [Unreleased] - 0.1.1-dev

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
- Product version remains `0.1.1-dev`; no tag, release candidate, or public
  release is created by these changes.
