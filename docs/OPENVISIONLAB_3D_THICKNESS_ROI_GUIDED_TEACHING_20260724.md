# Thickness ROI guided teaching - 2026-07-24

## Decision

The owner could not discover how to load a 3D Map and teach a Thickness ROI
without guidance. The implementation therefore keeps the existing typed
recipe and Viewer-capture contracts, but makes the required operator sequence
explicit in Step Parameters:

1. select `Capture ROI` or `Replace ROI`;
2. click two opposite grid corners in the 3D Viewer;
3. select `Apply ROI / selection`;
4. set the typed tolerance parameters;
5. invoke `Preview` explicitly.

Teaching changes recipe geometry only. It must not invoke Preview, Publish, or
Run.

## Implemented UI

- A Thickness-only bilingual teaching card explains the full sequence.
- The selection requirement is shown as
  `Thickness measurement ROI · 2 grid corners`.
- Existing generic selection commands and the active capture ribbon now switch
  between Korean and English.
- Capture, remove, reuse, undo, cancel, and apply controls use the existing
  WPF UI icon set where the icon improves scanning.
- Language changes refresh computed Workbench guidance as well as direct
  localization bindings.
- No recipe JSON, typed entity ID, parameter contract, algorithm, or explicit
  Preview/Publish/Run behavior changed.

## Current Thickness boundary

The current `Thickness` adapter evaluates scalar height values within one
recipe-owned `GridRectangle` and reports mean, range, minimum, maximum, and
valid-sample evidence against authored limits. It is not yet a calibrated
two-surface physical-thickness algorithm. The UI must not imply otherwise.

## Verification

- Release build: pass, `0` warnings / `0` errors.
- Generic height measurement Workbench: pass, `28/28`.
- Teaching capture ViewModel: pass, `18/18`.
- Actual Release EXE replacement-capture entry:
  - selected step: `step.thickness.01`;
  - existing schema/selection: `1.2`, `1`;
  - result: capture active at `0/2`;
  - recipe dirty state, selection count, route, Preview, and result entities
    remained unchanged;
  - result: pass.
- Korean and English actual-EXE screenshot quality: accepted on attempt 1.
- Korean active-capture screenshot quality at `1920 x 1040`: accepted on
  attempt 1.

Evidence:

- `artifacts/current/20260724-thickness-roi-guided-teaching/before-thickness-step-ko.png`
- `artifacts/current/20260724-thickness-roi-guided-teaching/after-thickness-step-ko.png`
- `artifacts/current/20260724-thickness-roi-guided-teaching/after-thickness-step-en.png`
- `artifacts/current/20260724-thickness-roi-guided-teaching/after-thickness-capture-active-ko.png`
- `artifacts/current/20260724-thickness-roi-guided-teaching/actual-exe-thickness-replace-capturing.txt`
- `artifacts/current/20260724-thickness-roi-guided-teaching/release-build-final.txt`
- `artifacts/current/20260724-thickness-roi-guided-teaching/tool-height-measurement-workbench-final.txt`
- `artifacts/current/20260724-thickness-roi-guided-teaching/teaching-capture-viewmodel-final.txt`

## Completion record

Status: Complete

Scope: discoverable bilingual Thickness ROI teaching entry and capture-ribbon
workflow in the existing generic Inspection Recipe Workbench.

Acceptance criteria:

- the operator sequence is visible in Step Parameters: pass;
- ROI capture/replace and capture-ribbon commands are bilingual: pass;
- existing recipe and explicit execution boundaries remain unchanged: pass;
- the current Release EXE enters Thickness replacement capture without running
  inspection: pass;
- fresh before/after and active-state UI evidence exists: pass.

Verification: Release build `0/0`, Workbench `28/28`, teaching capture `18/18`,
actual EXE replacement capture pass, three current after-captures accepted on
attempt 1.

Evidence: this document and
`artifacts/current/20260724-thickness-roi-guided-teaching/`.

Boundary / next dependency: the broader unaided owner first-recipe replay must
restart on this updated EXE. Physical thickness requires a separately designed
two-surface/calibrated measurement contract and evidence; it is not proved by
this UI checkpoint.
