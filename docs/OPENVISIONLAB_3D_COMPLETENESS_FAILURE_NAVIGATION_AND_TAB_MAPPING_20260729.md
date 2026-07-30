# Completeness failure navigation and repeated-Tab result mapping

Date: 2026-07-29

Status: Complete

Backlog scope: `H-08`, `H-10`

## Outcome

Completeness Grid results now have one explicit, view-only review surface:

- Previous failure and Next failure traverse failed cells in deterministic
  row-major result order and wrap at both ends.
- An all-pass result keeps its cell evidence visible but disables both failure
  navigation actions.
- Direct row selection and failure navigation select the same stable cell ID
  in the Workbench, Height Image, and 3D Viewer.
- Height Image uses a thicker selected outline, stronger fill, and `▶` label.
- 3D uses a white outer emphasis plus the original status-colored outline.
- Existing ordinary Thickness steps named exactly `Tab 1 Thickness` through
  `Tab 8 Thickness` are mapped by ordinal to the corresponding cell-result
  display name. Their step IDs, output entity IDs, ROI identities, parameters,
  and execution lifecycle are not changed.

Navigation is presentation state. It does not dirty or save the recipe and
does not invoke Preview, Publish, Run, or Validation Set.

## Ownership

- `ToolWorkbenchViewModel.CompletenessReview.cs` owns ordered review
  projection, explicit navigation, selected-cell state, and the read-only
  repeated-Tab identity mapping.
- `HeightImageViewerViewModel` and `MainWindowViewModel` own only the selected
  cell presentation state.
- Height Image and Viewer rendering consume the existing H-07 overlay
  descriptors; they do not duplicate cell decision policy.
- Core, Tools, and Runner H-02 through H-07 result contracts are unchanged.

## Acceptance evidence

The focused Workbench verification proves:

- all-pass output: four visible cells, zero failed cells, both navigation
  commands disabled;
- mixed output: two Pass and two Fail cells;
- initial selection: first failed cell in row-major order;
- Next, wrap, and Previous sequence:
  `r002.c001 -> r002.c002 -> r002.c001 -> r002.c002`;
- Height Image selected ID equals the Workbench selected ID;
- recipe dirty state, step ID, output entity ID, and output SHA remain
  unchanged throughout navigation;
- `Tab 1..8 Thickness` maps to cells 1..8 while all eight input step and output
  identities remain byte-for-byte unchanged.

The current Release wide and compact captures show the first failed cell
selected in both linked views and expose the same failure count/navigation
surface.

## Verification

Commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

dotnet run --no-build --project `
  src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Release -- `
  --verify-tool-height-measurement-workbench `
  artifacts\current\20260729-completeness-failure-navigation\height-measurement-workbench.txt

dotnet run --no-build --project `
  src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj `
  -c Release -- `
  --verify-c3d-completeness-grid `
  --report artifacts\current\20260729-completeness-failure-navigation\completeness-golden.txt

dotnet run --no-build --project `
  src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj `
  -c Release -- `
  --verify-display-viewmodel `
  artifacts\current\20260729-completeness-failure-navigation\display-viewmodel.txt

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts\verify-code-structure.ps1 `
  -ReportPath artifacts\current\20260729-completeness-failure-navigation\code-structure.txt

git diff --check
```

Results:

- Release build: `0` warnings, `0` errors;
- height measurement Workbench: `54/54`;
- Completeness golden: `23/23`;
- Inspection Workspace: `63/63`;
- Tool Recipe teaching: `28/28`;
- Artifact Navigator / Output Compare: `31/31`;
- Workbench docking: `33/33`;
- Shell smoke command line: `24/24`;
- Viewer display/projection: `103/103`;
- code structure: `17/17`;
- Wide and Compact screenshot quality: accepted on attempt `1`;
- `git diff --check`: pass; existing line-ending notices only.

## UI evidence

Evidence root:

- `artifacts/current/20260729-completeness-failure-navigation/`

Artifacts:

- `before-wide.png`: fresh current Release baseline captured before H-08/H-10;
  cell overlays exist, but there is no failed-cell review surface or selected
  cell emphasis.
- `after-wide.png`: current Release at `1920 x 1040`; the new review panel,
  failure `1/2`, and synchronized selected-cell emphasis are visible.
- `after-compact.png`: current Release at `1280 x 760`; navigation, four cell
  rows, and linked selected-cell emphasis remain usable.
- `after-wide-quality.txt`, `after-compact-quality.txt`: both accepted on
  attempt `1`.

All images contain only the application window.

## Completion record

Status: Complete

Scope: `H-08/H-10` explicit failed-cell Previous/Next navigation,
synchronized Height Image/3D selected-cell emphasis, zero-failure behavior,
and presentation-only `Tab 1..8 Thickness` identity mapping.

Acceptance criteria: deterministic row-major traversal -> pass; wrap behavior
-> pass; all-pass disabled state -> pass; same stable cell selected in linked
views -> pass; repeated-Tab names mapped without replacing ordinary Thickness
steps -> pass; recipe/output identities preserved -> pass; no implicit
execution or dirty-state mutation -> pass; current Release UI evidence ->
pass.

Verification: Release build `0/0`; Workbench `54/54`; Completeness golden
`23/23`; Workspace `63/63`; Viewer display `103/103`; related regression
groups and structure guard pass; Wide/Compact quality accepted.

Evidence:
`artifacts/current/20260729-completeness-failure-navigation/`.

Boundary / next dependency: `H-11/H-12` Good/Bad/Held-out Completeness
Validation Set examples and evidence-based threshold assistance are next.
`H-09` remains blocked by `E-11/G-12`. R0 owner replay, physical calibration,
and metrology remain external or unverified.
