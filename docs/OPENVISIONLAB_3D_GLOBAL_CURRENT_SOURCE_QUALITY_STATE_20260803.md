# OpenVisionLab 3D Global Current-Source Quality State

Date: 2026-08-03

## Outcome

`A-12 Global current-source quality state` is Complete. A loaded source now
shows one read-only quality status in the Job Bar beside recipe, source, and
alignment context. The status answers the operator's first source-trust
question without requiring the Source Quality support pane to remain open.

The implementation adapts the commercial workflow principle of keeping the
current task and its evidence linked. It retains OpenVisionLab terminology,
graphite semantic roles, layout, and explicit execution contracts; it does not
copy a competitor's theme, proportions, assets, or screen topology.

## Operator problem and selected design

The current-build baseline showed that a loaded Compact Authoring workspace
could hide the Source Quality pane behind an inactive support tab. Validate
and Results retained the source format and alignment state in the Job Bar but
did not expose the measured `84.5% valid / 15.5% missing` condition. An
operator therefore had to leave the current task context to answer whether
missing source cells were present.

The selected solution adds one compact text status between source context and
alignment:

- `Pass` uses the existing pass semantic roles;
- `Warning` is used when the current report has missing cells;
- `Error`, `Loading`, and `Unavailable` use the existing fail/info roles;
- the status is hidden when no source is loaded;
- its tooltip retains grid size, valid/missing counts and ratios, and the
  explicit read-only/no-Preview/no-Run boundary;
- the Source Quality workspace remains the detailed evidence owner.

The status is text-bearing rather than icon-only because the two ratios are
the decision evidence. It is not focusable or clickable and has no hover or
pressed command state. It has a stable automation ID, accessible name, and
help text.

## Included and excluded scope

Included:

- English and Korean summary/detail localization;
- loaded, empty, Pass, Warning, Loading, Error, and Unavailable presentation
  policy;
- Wide `1920 x 1040` and Compact `1280 x 760` Authoring, Validate, and Results
  placement;
- accessibility and source-quality non-mutation verification;
- a bounded Results-pane integrity repair exposed by the Compact matrix:
  `DockMinWidth=460` and width-aware wrapping for run-record commands.

Excluded:

- source-quality calculation changes;
- recipe, ROI, selection, Viewer, or inspection state mutation;
- automatic Preview, Publish, Run, or Validation;
- camera, PLC, robot, cloud, or production-line scope;
- the pre-existing Advanced-workspace light-surface theme leak recorded below.

## Acceptance and evidence matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| Loaded source quality is visible in Authoring | Pass | `after/wide-source-quality-en.png`; `after/compact-source-quality-ko.png` |
| Loaded quality remains visible in Validate and Results | Pass | `after/wide-validate-en.png`; `after/compact-validate-ko.png`; `after/wide-results-en.png`; `after/compact-results-ko.png` |
| Empty input does not show a stale quality status | Pass | `after/wide-empty-authoring-en.png`; `after/compact-empty-authoring-ko.png`; docking verification |
| Missing cells use the existing Warning role | Pass | actual `84.5% valid / 15.5% missing` Wide/Compact captures |
| A complete source uses the existing Pass role | Pass | final actual-EXE Completeness fixture render recorded in `verification/leftmost-monitor-placement.txt` |
| Status is accessible and read-only | Pass | docking `80/80`; Source Quality Wide/Compact smoke |
| Source-quality review changes no recipe, selection, log, or execution state | Pass | `viewOnly=true`, `recipeChanged=false`, `inspectionRun=false`, and unchanged boundary fields in both Source Quality reports |
| Compact Results commands remain visible and inside their pane | Pass | true before/after `compact-results-ko.png`; final after has no horizontally clipped required command |
| Current Release and structural boundaries remain valid | Pass | Release `0/0`; workspace `63/63`; Validation Set `84/84`; command line `28/28`; structure `29/29` |
| Fixed Human-owner R0 inputs match the current binary set | Pass | Wide and Compact `-ValidateOnly` reports |

All eight final screenshot-quality reports were accepted on their first
attempt. The changed Job Bar has no overlap, required-text clipping,
out-of-pane control, or horizontal/nested scrollbar at either supported size.

## Verification

Commands and focused checks:

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>
OpenVisionLab.ThreeD.Shell.exe --verify-inspection-workspace-selection <report>
OpenVisionLab.ThreeD.Shell.exe --verify-validation-set <report>
OpenVisionLab.ThreeD.Shell.exe --verify-shell-smoke-command-line <report>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-code-structure.ps1 -ReportPath <report>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
git diff --check
```

Results:

- Release build: `0` warnings, `0` errors.
- Workbench docking: `80/80`.
- Inspection Workspace: `63/63`.
- Validation Set: `84/84`.
- command-line smoke options: `28/28`.
- code structure: `29/29`, zero Studio numerical migration-debt files.
- Source Quality Wide and Compact: Pass; `84.489955%` valid,
  `15.510045%` missing; no recipe or execution mutation.
- Wide and Compact R0 fixed inputs: `-ValidateOnly` Pass.
- actual desktop EXE: rendered at `208,208` with size `1600 x 872` on the only
  available and therefore leftmost monitor `DISPLAY1` (`0,0,1920,1080`).

## Evidence

Physical root:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-a12-current-source-quality-layout\
```

The root contains true pre-edit captures under `before`, current final Release
captures and quality reports under `after`, and focused reports under
`verification`.

## Newly recorded follow-up

The pre-edit current-build Advanced audit exposed an existing theme defect in
both supported sizes: Data/Layers, Tool/Inspector, and Evidence Workbench
content surfaces render with white or platform-light backgrounds inside the
graphite application. The A-12 Job Bar status is not visible in Advanced and
did not cause this defect. It is deliberately not hidden or folded into this
closure.

Track it as `A-16 Advanced workspace semantic-theme parity`. The next task
must capture a fresh baseline, repair the owning shared/theme boundary rather
than adding one-off colors, and verify normal, selected, focus, disabled,
read-only, validation, tabbed, expanded/collapsed, English/Korean, Wide, and
Compact states.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

## Completion record

Status: Complete

Scope: Global read-only current-source quality status, localized evidence
summary/detail, empty-source hiding, accessible semantic states, and the
Compact Results command-integrity repair exposed by the same layout matrix.

Acceptance criteria: Every criterion in the acceptance matrix passes.

Verification: Release `0/0`; docking `80/80`; Inspection Workspace `63/63`;
Validation Set `84/84`; command line `28/28`; structure `29/29`; Source
Quality Wide/Compact Pass; eight accepted final captures; Wide/Compact R0
`-ValidateOnly` Pass; actual EXE on the current leftmost display.

Evidence: The physical evidence root above and this closure document.

Boundary / next dependency: Human-owner unaided Wide/Compact R0 remains
external for `A-01` and Workspace v3 `8/8`. `A-16` is the next layout defect;
`J-12` remains the deferred numerical backlog item.
