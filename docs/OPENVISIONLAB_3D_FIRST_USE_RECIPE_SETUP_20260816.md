# OpenVisionLab 3D First-use Recipe Setup

Date: 2026-08-16
Status: Complete

## Outcome

`PL-0013` replaces the fragmented New Recipe -> Save As -> Open source ->
select task route with one explicit first-use setup in Recipe Center. Before
creation, the operator can see and edit:

- recipe name;
- recipe folder and exact target file;
- one C3D source;
- an optional `Empty recipe` or compatible `Thickness measurement` starter;
- whether the confirmed setup should be remembered.

`Create recipe` remains explicit. A remembered setup is restored visibly and
can be edited or reset. Missing folders or sources are explained in place and
disable Create. Reset returns to safe defaults and clears the saved setup.

## Product boundary

The operator problem was excessive context switching before teaching could
begin. The product requirement is to keep related first-use
configuration and its next action in one coherent surface. The implementation
uses OpenVisionLab names, theme resources, typed input routing, lifecycle, and
layout.

This remains a local, file-first deterministic inspection workflow. It does
not add camera, acquisition, PLC, robot, cloud, account, deployment, or
production-line behavior.

## Lifecycle and persistence contract

- Opening, editing, restoring, or resetting setup does not create or load a
  recipe, add a step, or invoke Preview, Publish, Run, or Validation.
- Confirmation first loads the selected C3D into the Viewer, creates and saves
  the named recipe, binds the stable source, and optionally inserts one typed
  Thickness starter through the existing compatible Add route.
- Source and result geometry remain separate. No result replaces the source.
- The reusable setup is stored beside recent-recipe configuration as
  `first-recipe-setup.json` only after a confirmed Create with Remember
  selected. Restored values remain visible, validated, editable, and
  resettable.
- Stale restored paths do not trigger work or silently fall back.

## Implementation owners

- `ToolWorkbenchViewModel.FirstRecipeUx.cs`: setup state, validation,
  persistence, reset, and typed optional starter.
- `RecipeManagerView.xaml`: themed, keyboard-reachable setup surface.
- `MainWindow.xaml.cs`: explicit confirmed creation and C3D Viewer loading.
- `ShellRecipeLifecycleSmoke.cs`: actual-EXE save/reopen contract.
- `ThreeDLocalization.cs`: English/Korean operator copy.

## Verification

Evidence root:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-pl0013-first-use-setup\`

- Recipe Manager + WPG: Pass `49/49`. Covers all fields, exact target,
  no-action draft/restore/reset, confirmed Thickness creation, save/reopen,
  stale-path explanation, disabled Create, and English/Korean copy.
- Tool Recipe teaching: Pass `46/46`.
- Workbench docking: Pass `84/84`.
- Shell smoke options: Pass `39/39`.
- Debug and Release builds: `0` warnings, `0` errors.
- Actual current Release EXE empty lifecycle: Pass with zero steps.
- Actual current Release EXE Thickness lifecycle: Pass with one `thickness`
  step, `source.c3d.height-map` routing, matching Viewer source, saved recipe,
  hidden setup, and clean state.
- Human-owner R0 package validation: Wide and Compact `-ValidateOnly` Pass on
  dynamically selected leftmost `DISPLAY2`; all nine hashes match and no
  application launches.

## UI evidence and comparison

Before:

- `before\wide-1920x1040-en.png`
- `before\compact-1280x760-en.png`

After:

- `after\final-wide-1920x1040-en-valid.png`
- `after\final-compact-1280x760-ko-stale-disabled.png`
- `after\final-compact-1280x760-en-create-pressed.png`
- `after\compact-1280x760-en-starter-popup-computer-use.png`
- `after\compact-1280x760-en-name-focus-input.png`

The before state exposes only the New Recipe command. The after state keeps
identity, location, source, starter, exact target, validation, remember/reset,
and the explicit Create action in one bounded panel. Wide and Compact,
English/Korean, non-empty focused input, open starter popup, stale/disabled,
and held pressed states remain inside the selected monitor with no clipped
required text or platform-light theme leak.

The pressed-state smoke first attempts OS pointer injection and a routed mouse
event. This DPI host required a test-only `ButtonBase` held-state fallback to
capture `IsPressed`; therefore the image proves the rendered held-state theme,
not end-to-end hardware injection fidelity. A separate Computer Use session
verified the real ComboBox popup and keyboard focus path in the actual EXE.

## Maturity reassessment

Evidence-bounded workflow judgment:

| Area | Before | After | Basis |
|---|---:|---:|---|
| First-use efficiency | 6.5/10 | 8.5/10 | Four contexts consolidated; confirmed setup restores visibly and safely |
| Operator authoring readiness | 7.4/10 | 7.8/10 | Compatible Add, contextual teaching, health navigation, and first-use creation now align |

These are qualitative product-workflow judgments, not telemetry, release
acceptance, certified usability, production approval, or physical-metrology
claims. `PL-0012` stale search context and `PL-0014` language-popup correction
remain open, and product-owner unaided Wide/Compact R0 remains external.

## Completion record

```text
Status: Complete
Scope: Complete PL-0013 one-surface recipe identity, folder, C3D source, optional compatible starter, confirmed remembered setup, stale validation, and reset behavior
Acceptance criteria: all four inputs visible before explicit Create -> pass; confirmed setup save/reload/reopen with no restore action -> pass; stale paths explained and Create disabled -> pass; Reset returns to safe defaults without action -> pass; Wide/Compact English/Korean focus/popup/disabled/pressed states remain themed, reachable, and on selected monitor -> pass
Verification: Debug and Release 0/0; Recipe Manager + WPG 49/49; Tool Recipe teaching 46/46; Workbench docking 84/84; Shell smoke options 39/39; actual Release EXE empty and Thickness save/reopen pass; Wide/Compact R0 -ValidateOnly pass; git diff --check is the final repository gate
Evidence: this document, .proofline/issues/PL-0013.json, and D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0013-first-use-setup/
Boundary / next dependency: product-owner unaided Wide/Compact R0 remains external; PL-0012 search-context correction is the next deterministic software priority
```
