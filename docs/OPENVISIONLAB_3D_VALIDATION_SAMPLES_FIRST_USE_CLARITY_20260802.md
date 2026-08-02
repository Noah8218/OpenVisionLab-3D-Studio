# Validation Samples First-Use Clarity Closure

Date: 2026-08-02
Status: Complete

## Operator problem

The dedicated Validate `Samples` view mixed run-state filters, expected-role
assignment, issue navigation, and 3D comparison in one horizontal bar. At
`Compact 1280 x 760`, the role-assignment controls collapsed out of the useful
width while `Previous issue` and `Next issue` remained prominent before any
sample had run. The table labels `Role` and `State` also did not explain which
value was supplied by the operator and which value was produced by the recipe.

The operator needs the shortest safe workflow to be explicit:

```text
Add samples -> set the expected role -> Run sample set -> review results
```

## Completed behavior

- The navigation surface states that its radio choices switch review sections
  and that only `Run sample set` starts inspection.
- `Samples` leads with an English/Korean three-step workflow guide.
- The guide defines `Expected role` as the operator-provided answer and
  `Run state` as the recipe result. It also states that Held-out evidence is
  excluded from threshold tuning.
- Expected-role assignment has a dedicated row with checked Good, Bad, and
  Held-out controls and role counts. It no longer competes with status filters
  or issue navigation for one narrow row.
- The sample table uses `Expected` / `기대 역할` and `Run state` / `실행 상태`
  headers. Held-out is displayed consistently with its hyphen.
- Pending evidence now says `Not run · choose Run sample set` instead of using
  the obsolete `Run All` wording.
- Run-state filters appear only in `Run results` and `Failure analysis`.
- Previous/next issue commands appear only in `Failure analysis` when a real
  Fail or Error exists. They are not shown in the pre-run Samples workflow.
- Compact `Run results` uses the full evidence-pane width for the sample table.
  Compact `Failure analysis` uses that full width for the selected failed-step
  record; previous/next issue commands own sample traversal. Wide retains the
  two-pane sample/record review when enough width exists.
- Metrics and overlays are stacked in the selected-record detail surface so
  they remain readable in Compact. Secondary long names and evidence retain
  their full value in tooltips.
- Selecting a section, sample, role control, filter, or Viewer comparison
  remains non-executing. Preview, Publish, Run, and Validation contracts are
  unchanged.

## Semantic reference

| UI value | Owner | Meaning |
| --- | --- | --- |
| Good | Operator | This sample is expected to be accepted |
| Bad | Operator | This sample is expected to be rejected |
| Held-out | Operator | Final replay evidence; excluded from threshold tuning |
| Pending | Application | The current expected-role definition has not been run |
| Pass | Application | All supported recipe steps passed on this sample |
| Fail | Application | At least one inspection rule produced an out-of-tolerance result |
| Error | Application | Execution could not produce a valid inspection result |

## Acceptance criteria and evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| First-use workflow and owner/result distinction are visible before execution | Pass | Final Wide/Compact Samples captures |
| Expected-role controls remain reachable at Compact width | Pass | English/Korean Compact captures and role hover/checked capture |
| Issue navigation is absent before execution and present after real Fail/Error evidence | Pass | Docking verification `78/78`; pre-run Samples and post-run Failure captures |
| Compact required labels/actions do not overlap or clip | Pass | English/Korean `1280 x 760` Samples and Failure captures |
| Wide required labels/actions do not overlap or clip | Pass | English `1920 x 1040` Samples capture |
| Role assignment remains sidecar-only and non-executing | Pass | Validation Set verification `84` PASS lines |
| Section navigation remains presentation-only | Pass | Docking `78/78`; Inspection Workspace `63/63` |
| Current Release and structural boundaries remain valid | Pass | Release `0/0`; smoke options `28/28`; structure `29/29` |
| Fixed Human-owner R0 inputs match the current binary set | Pass | Wide and Compact `-ValidateOnly` reports |

## Theme and state matrix

The changed controls reuse the existing OpenVision semantic graphite roles and
implicit Button/ToggleButton/DataGrid styles; no framework-default or local
hard-coded control palette was introduced.

| State | Check |
| --- | --- |
| Normal / pending | Compact and Wide Samples captures |
| Pointer hover | Compact Good-role hover capture |
| Selected / checked | Samples navigation and Held-out role captures |
| Keyboard focus / selected failure | Failure section entered with native keyboard Space and captured from the actual EXE |
| Read-only results | Validation sample and selected-record DataGrids remain read-only |
| Pass / Fail semantic state | Post-run Failure capture shows existing pass/fail surfaces |
| Disabled during execution | Existing command `CanExecute` ownership remains unchanged; role/filter/sample mutations are blocked by `IsValidationSetRunning` |
| Popup / validation error | Not applicable; no popup or editable validation field was added |

## Verification

Commands were run from `C:\Git\OpenVisionLab-3D-Studio` with test `TEMP` and
`TMP` routed to
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\temp\20260802-validation-samples-clarity`:

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
- Workbench docking: `78/78`.
- Inspection Workspace: `63/63`.
- Validation Set: `84` PASS lines, process exit code `0`.
- Shell smoke command-line options: `28/28`.
- Code structure: `29/29`.
- Wide and Compact R0 `-ValidateOnly`: pass.
- `git diff --check`: pass; pre-existing line-ending warnings only.

The fixed Shell assembly SHA-256 is now
`172FD3DCE7B7A93CEEDA813E0FD28F1DFD5BD2D25EE231A4B5EB82FFE274F070`.

## Current-build visual evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260802-validation-samples-clarity`

The actual Release EXE windows were placed on the dynamically selected leftmost
monitor `\\.\DISPLAY2`, bounds `-1920,360,1920,1080`, working area
`-1920,360,1920,1040`. Every final capture rectangle intersects that monitor
and every final `PrintWindow` call succeeded.

Before:

- `before\exe-wide-validation-samples.png`
- `before\exe-compact-validation-samples.png`
- `before\exe-compact-validation-samples-ko.png`
- `before-printwindow-matrix.txt`

After:

- `after\exe-wide-validation-samples-en.png`
- `after\exe-compact-validation-samples-en.png`
- `after\exe-compact-validation-samples-ko.png`
- `after\exe-compact-validation-failures-en.png`
- `after\exe-compact-validation-failures-ko.png`
- `after\exe-compact-validation-role-hover-en.png`
- `after-printwindow-samples-final.txt`
- `after-printwindow-failures-final.txt`
- `after-printwindow-role-hover.txt`

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

This closure proves first-use semantic clarity and responsive integrity for the
Validation Samples and Failure Analysis surfaces. It does not prove human
usability, metrology, production readiness, or external camera/PLC/robot/cloud
integration. The product-owner unaided Wide/Compact R0 remains external for
`A-01` and Workspace v3 `8/8`.

The next owner-selected layout priority remains `A-12 Global current-source
quality state`. `J-12 Multiple-match result collection` remains deferred while
the layout-only continuation is active.

## Durable completion record

Status: Complete
Scope: Validation Samples meaning, expected-role assignment, run-state review, issue-navigation disclosure, and Compact failure-record layout
Acceptance criteria: all criteria above pass
Verification: Release `0/0`; docking `78/78`; workspace `63/63`; Validation Set `84` PASS; smoke `28/28`; structure `29/29`; Wide/Compact R0 `-ValidateOnly` pass
Evidence: `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260802-validation-samples-clarity`
Boundary / next dependency: Human-owner R0 remains external; continue the layout stream with A-12 unless the owner changes priority
