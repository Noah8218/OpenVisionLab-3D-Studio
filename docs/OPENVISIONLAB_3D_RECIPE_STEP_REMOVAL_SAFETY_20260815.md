# OpenVisionLab 3D Recipe-Step Removal Safety

> Current-priority note: the bounded Workbench run-log item identified below
> was completed later on 2026-08-15 as `PL-0008`. This document retains the
> recipe-step removal outcome and its then-current handoff boundary.

Date: 2026-08-15
Status: Complete
Issue: `PL-0007`

## Operator problem

The Workbench previously removed the selected authored step immediately. The
same action could also remove teaching selections that became unused, but the
operator was not shown that impact first. The command predicate checked only
whether a step was selected, so it did not fail closed during Preview or
Validation Set execution.

This matters because the product has no general Undo contract for recipe
structure. A single mistaken click could therefore destroy authored work.

## Implemented contract

- Remove now opens the existing OpenVisionLab themed message dialog.
- The dialog names the selected step and reports how many teaching selections
  would become unused and be removed, including their names.
- Cancel is the default and preserves the selected step, all selections, the
  selected identity, dirty state, and Run Log.
- Confirm removes the exact requested step and only selections no remaining
  step uses.
- The command is unavailable while any tool Preview, whole-recipe Run-backed
  Preview, Surface Match experiment, or Validation Set execution is active.
- The confirmation path rechecks the execution state and selected stable step
  ID immediately before mutation, so a stale request fails closed.
- Requesting, cancelling, or displaying the confirmation does not invoke
  Preview, Publish, Run, or Validation.
- The existing `--shell-smoke-leftmost` verifier now converts physical monitor
  coordinates to WPF device-independent coordinates, preventing partial
  off-screen placement on a scaled left monitor.

The ViewModel owns the mutation policy and impact calculation. `MainWindow`
owns only localized confirmation presentation. This keeps the existing
explicit-action contract and does not add a new service or abstraction.

## Commercial lesson retained

Commercial workbenches such as GoPxL make consequential actions and their
next effect explicit. OpenVisionLab adapts that workflow principle by showing
the exact local step and selection impact before mutation. It does not copy a
competitor screen, terminology, color system, icon artwork, topology, or code.

## Verification

- Recipe Manager + WPG focused verification: `40/40`.
- Shell smoke command-line verification: `35/35`.
- Debug and Release solution builds: `0` warnings, `0` errors.
- Code-structure guard: `29/29`.
- Current-build application evidence:
  - Wide `1920 x 1040` and Compact `1280 x 760` baseline and after captures;
  - English and Korean confirmation content;
  - normal and held primary-button pointer-down states;
  - capture-quality acceptance for every retained image;
  - dynamic leftmost monitor and actual window intersection record.
- Fixed-input R0 `-ValidateOnly`: Wide and Compact pass after refreshing all
  current Release hashes; no application is launched by validation-only mode.
- Proofline v2 validation and `git diff --check`: pass.

Local verification evidence is stored physically at:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-recipe-step-removal-safety\`

## Scope boundary

This slice protects selected recipe-step deletion only. It does not introduce
general Undo/Redo, add confirmation to every ROI/reference deletion path, or
change recipe schemas, algorithms, Viewer rendering, docking, source/result
separation, or Preview/Publish/Run semantics. Bounded Workbench run-log
retention remains the next dependency-ready maintenance item.

Product-owner unaided Wide and Compact R0 remains an external acceptance gate
for `A-01`, Workspace v3 `8/8`, and any human-usability or release-acceptance
claim. Automated evidence in this closure does not replace it.

## Completion record

```text
Status: Complete
Scope: Impact-aware confirmation and execution-state guard for selected recipe-step removal
Acceptance criteria: confirmation before mutation -> pass; Cancel preserves authored state -> pass; exact step and orphan-selection removal after confirm -> pass; active execution blocks and fails closed -> pass; no implicit execution -> pass; Wide/Compact localized and pointer-state UI evidence -> pass
Verification: Recipe Manager + WPG 40/40; Shell command line 35/35; Debug/Release 0 warnings and 0 errors; structure 29/29; Wide/Compact and English/Korean screenshot quality accepted; refreshed Wide/Compact R0 ValidateOnly pass; Proofline v2 and git diff checks pass
Evidence: docs/OPENVISIONLAB_3D_RECIPE_STEP_REMOVAL_SAFETY_20260815.md; .proofline/issues/PL-0007.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-recipe-step-removal-safety/
Boundary / next dependency: no general Undo/Redo or other deletion-path expansion; bounded Workbench run-log retention next; product-owner Wide/Compact R0 remains external
```
