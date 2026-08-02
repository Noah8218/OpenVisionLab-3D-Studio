# Validate Dock And Compact Layout Integrity Closure

Date: 2026-08-01
Status: Complete

## Operator problem

The current-build Validate workspace exposed all seven validation/support dock
tabs at once. At `Compact 1280 x 760`, required tab names were reduced to
fragments such as `Pi`, `Ou`, and `Se`; the validation action row and required
table headers also competed with a redundant summary. Wide mode showed the
same tab-density problem at a larger scale.

The operator needs the active validation task, its sample actions, and the 3D
evidence to remain readable without losing explicitly requested support panes.

## Completed scope

- The Validate stage presents one focused `Validate` tab by default.
- An explicitly requested support pane is presented beside `Validate`; the
  current verified case is `Session Log`.
- Presentation filtering keeps the AvalonDock content model alive. It does not
  call `Hide`, `Show`, close, recreate, or persist a different dock model.
- Visible Validate tabs use a fixed readable width. Hidden validation support
  tab containers have zero width, zero margin, no hit testing, and no keyboard
  focus.
- Compact Validate restores or clamps the evidence/viewer ratio so the sample
  actions and required table headers remain readable while the Viewer remains
  the larger region.
- The dedicated Validate presentation removes redundant title/summary text,
  shows Add/Clear actions only when idle, and shows Cancel only while a
  validation run is active.
- File and evidence values may use ellipsis as secondary identifiers, with the
  complete value retained in a tooltip. Required headers use `File`, `State`,
  `Duration`, and `Evidence` (`파일`, `상태`, `실행 시간`, `실행 근거`).
- Command-line fixture setup now applies the explicitly requested workbench pane
  after Validation Set composition, matching the visible operator request.
- Advanced restores the complete legacy support-tab set.

## Acceptance criteria and evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Default Validate exposes one readable task tab | Pass | Docking verification `78/78`; Wide/Compact captures show `Validate` only |
| Explicit support request exposes exactly one additional readable tab | Pass | Docking verification and `exe-compact-validate-session.png` show `Validate` plus `Session Log` |
| Compact actions and required labels are reachable and not clipped | Pass | English and Korean `1280 x 760` current-build captures |
| Wide layout has no required-label clipping or unintended horizontal/nested scrolling | Pass | English `1920 x 1040` current-build capture |
| Viewer and Displayed Outputs tabs remain available and the Viewer remains dominant | Pass | All after captures and compact ratio verification |
| Advanced restores the full support set | Pass | Docking verification `78/78` |
| Stage/pane presentation does not run Preview, Publish, Run, or Validation and does not dirty recipe state | Pass | Docking verification `78/78`; Inspection Workspace `63/63`; Validation Set `84` PASS lines |
| Current Release and structural boundaries remain valid | Pass | Release `0` warnings / `0` errors; smoke options `28/28`; structure `29/29` |
| Fixed Human-owner R0 inputs match the current binaries | Pass | Wide and Compact `-ValidateOnly` reports pass all nine SHA-256 inputs |

## Verification

Commands run from `C:\Git\OpenVisionLab-3D-Studio` with test `TEMP` and `TMP`
routed to
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\temp\20260801-layout-integrity-audit`:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>
OpenVisionLab.ThreeD.Shell.exe --verify-inspection-workspace-selection <report>
OpenVisionLab.ThreeD.Shell.exe --verify-validation-set <report>
OpenVisionLab.ThreeD.Shell.exe --verify-shell-smoke-command-line <report>

powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-code-structure.ps1 -ReportPath <report>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
git diff --check
```

Final results:

- Release build: `0` warnings, `0` errors.
- Docking workspace: `78/78`.
- Inspection Workspace selection boundary: `63/63`.
- Validation Set ordered graph: `84` PASS lines, process exit code `0`.
- Shell smoke command-line options: `28/28`.
- Code structure: `29/29`, including `0` Noah migration-debt files and `30`
  reviewed Studio boundaries.
- R0 Wide `1920 x 1040` and Compact `1280 x 760`: both `-ValidateOnly` pass.
- `git diff --check`: pass.

## Current-build visual evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-layout-integrity-audit`

The actual EXE windows were placed on the dynamically selected leftmost monitor
`\\.\DISPLAY2`, bounds `-1920,360,1920,1080`, working area
`-1920,360,1920,1040`. Every recorded window rectangle intersects that monitor
and every `PrintWindow` call succeeded.

Before:

- `before\exe-wide-validate-loaded.png`
- `before\exe-compact-validate-loaded.png`
- `before\exe-compact-validate-loaded-ko.png`
- `before-printwindow-matrix-part1.txt`
- `before-printwindow-matrix-part2.txt`

After:

- `after\exe-wide-validate.png`
- `after\exe-compact-validate.png`
- `after\exe-compact-validate-ko.png`
- `after\exe-compact-validate-session.png`
- `after-printwindow-matrix.txt`

Reports:

- `release-build-final.txt`
- `docking-final.txt`
- `inspection-workspace-final.txt`
- `validation-set-final.txt`
- `shell-smoke-options-final.txt`
- `code-structure-final.txt`
- `r0-wide-validate-only.txt`
- `r0-compact-validate-only.txt`

## Boundary and next dependency

This closure proves a bounded Validate layout and dock-presentation correction.
It does not prove human usability, metrology, production readiness, camera/PLC/
robot/cloud integration, or `J-12 Multiple-match result collection`.

The product-owner unaided Wide/Compact R0 remains the external acceptance task
for `A-01` and Workspace v3 `8/8`. Missing R0 evidence does not block the next
dependency-ready layout-only slice. Numerical work remains deferred until the
product owner changes the layout-only priority.

## Durable completion record

Status: Complete
Scope: Validate-stage dock focus, Compact readability, state-aware actions, and explicit support-pane presentation
Acceptance criteria: all criteria above pass
Verification: Release `0/0`; docking `78/78`; workspace `63/63`; Validation Set `84` PASS; smoke `28/28`; structure `29/29`; Wide/Compact R0 `-ValidateOnly` pass
Evidence: `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-layout-integrity-audit`
Boundary / next dependency: Human-owner R0 remains external; continue only with the next evidence-backed layout problem unless the owner changes priority
