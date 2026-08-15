# OpenVisionLab 3D Recipe Health Navigation

Date: 2026-08-15
Status: Complete software-slice evidence for `PL-0011`

## Operator Problem And Commercial Lesson

The seventeen-step EXE-authored study recipe exceeded the Compact Flow
viewport. The former global requirement badge did not identify which steps or
requirements needed attention, so an operator had to scan the chain and move
between panels.

The adopted commercial principle is current-task clarity: summarize the
recipe, expose the exact next requirement, and keep configuration, Viewer, and
evidence linked. The implementation keeps OpenVisionLab terminology, graphite
theme, explicit Preview/Publish/Run actions, and file-first identity. It does
not copy GoPxL layout, artwork, names, proportions, or code.

## Delivered Contract

- Flow shows exact, mutually exclusive counts for `Ready`, `Needs input`,
  `Needs selection`, `Needs parameters`, `Stale Preview`, and `Published`.
- Classification uses the deterministic precedence `Published -> Stale
  Preview -> Needs input -> Needs selection -> Needs parameters -> Ready` so
  every step contributes to exactly one count.
- The selected health item shows the stable owning step and exact input,
  selection, or parameter requirement.
- `Previous` and `Next` select and reveal the owning Flow row without wrapping.
  The final requirement disables `Next`; the first disables `Previous`.
- Navigation is unavailable while execution or an in-progress teaching or
  parameter draft could make a presentation-only jump unsafe.
- Navigation does not invoke Preview, Publish, Run, change recipe/source/result
  state, create or remove a layer, or alter the active input selection.
- All new text is localized in English and Korean and uses the existing
  semantic theme and WPF UI control styles.

## Verification

| Gate | Current result | Evidence |
| --- | --- | --- |
| Recipe health and no-mutation regression | `46/46` | `reports/tool-recipe-teaching.txt` |
| Workbench layout/theme integration | `84/84` | `reports/workbench-docking.txt` |
| Shell smoke option parsing | `37/37` | `reports/shell-smoke-options.txt` |
| Current Release build | `0` warnings, `0` errors | `reports/release-build-final.txt` |
| Fixed R0 input validation | Wide pass; Compact pass; neither launched the app | `reports/r0-wide-validate-only.txt`, `reports/r0-compact-validate-only.txt` |
| Current Release EXE UI | Wide English, Compact English/Korean, last requirement, and held native pointer-down accepted | `after/` and quality reports |

All physical evidence is under:

`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0011-recipe-health-navigation/`

The EXE was dynamically placed on the smaller left monitor, `DISPLAY2`, with
bounds `[-1920,365,1920,1080]`. The observed Compact window rectangle was
`[-1920,365,-640,1125]` (`1280 x 760`) and intersected that monitor. The
seventeenth requirement was reachable through navigation and scrolled into
view. The held pointer-down capture verified the actual `IsPressed` state and
showed no white or platform-default flash.

The refreshed fixed package hashes are recorded in
`OPENVISIONLAB_3D_HUMAN_OWNER_R0_EXECUTION_20260729.md` and
`scripts/start-human-owner-r0.ps1`.

## Boundaries

- This closes the `PL-0011` software scope. Automated observation and
  `-ValidateOnly` do not replace the product owner's unaided Wide/Compact R0.
- The study recipes remain workflow evidence. This work does not claim
  calibrated physical measurement, Gauge R&R, certified metrology, or
  production approval.
- Camera, lighting, PLC, robot, cloud, account, deployment, and production-line
  control remain outside product scope.
- `PL-0013` owns first-use recipe/source/task consolidation, `PL-0012` owns
  Tool Library search context, and `PL-0014` owns the language-popup theme and
  bounds defect.

## Completion Record

```text
Status: Complete
Scope: PL-0011 exact recipe-health projection, localized non-wrapping requirement navigation, Flow reveal, and presentation-only safety
Acceptance criteria: six exact mutually exclusive counts -> pass; Previous/Next reveals exact owner and requirement without wrapping or mutation -> pass; seventeen-step Wide/Compact review has reachable actions and no clipped required text -> pass
Verification: Debug and Release builds 0 warnings/0 errors; Tool Recipe teaching 46/46; Workbench docking 84/84; Shell smoke options 37/37; current Release EXE Wide/Compact English/Korean on DISPLAY2; last-requirement and held pointer-down captures accepted; fixed Wide/Compact -ValidateOnly pass
Evidence: this document; .proofline/issues/PL-0011.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0011-recipe-health-navigation/
Boundary / next dependency: product-owner unaided Wide/Compact R0 remains external; PL-0013 is the next deterministic software priority
```
