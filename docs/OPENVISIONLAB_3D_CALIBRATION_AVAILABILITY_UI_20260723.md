# Calibration capability availability UI — 2026-07-23

## Decision

Calibration Center must not present unfinished calibration capabilities as
usable product functions.

The current implemented and locally verified surface is:

- `Overview`
- `Repeatability` study load and explicit `Calculate`

The following roadmap capabilities remain visible for orientation, but are
disabled and labeled `준비 중 / Coming soon`:

- Height Calibration
- Sensor Alignment
- Calibration History
- Profile History evidence
- Sensor Transform evidence
- Profile Validate and Activate

Selecting an unavailable typed section through the ViewModel is ignored.
This prevents a caller from navigating into a blank placeholder even when it
does not use the WPF disabled state.

## UI contract

- The explorer and workspace tab strip use the same availability state.
- `준비 중 / Coming soon` labels and the explanatory tooltip are bilingual.
- The narrow inspector shows only the implemented `Calculate` action.
- Unimplemented Validate and Activate buttons are replaced by a clear
  bilingual status surface, avoiding both false affordance and label clipping.
- Docking, floating, current repeatability calculation, and explicit study
  loading remain unchanged.

## Verification

Current Debug evidence is stored in:

`artifacts/current/20260723-calibration-availability/`

It contains:

- `build.txt`: solution build, zero warnings and zero errors.
- `calibration-viewmodel-verification.txt`: section availability, current
  generic Recipe Workbench workspace contract, Korean/English availability
  strings, and repeatability workflow checks.
- `workbench-docking-verification.txt`: all `27/27` docking checks pass,
  including the four Calibration panes.
- `before-calibration-repeatability-ko.png`: actual-EXE baseline captured
  before the availability change.
- `after-calibration-repeatability-ko.png`: current actual-EXE Korean view.
- `after-calibration-repeatability-en.png`: current actual-EXE English view.
- `after-calibration-1280x760-ko.png` and
  `after-calibration-1280x760-en.png`: compact-resolution clipping review.
- matching screenshot-quality reports accepted on attempt 1.

## Claim boundary

This closes truthful navigation and visible capability-state handling. It does
not implement Height Calibration, Sensor Alignment, profile persistence,
profile activation, physical calibration, Gauge R&R, or metrology trust.
Those capabilities require separate typed contracts, algorithms, data, and
verification gates before their UI may become selectable.

## Completion record

Status: Complete

Scope: truthful availability and compact-layout handling for the existing
Calibration Center.

Acceptance criteria:

- Overview and Repeatability remain selectable: pass.
- unfinished sections and evidence views cannot be selected: pass.
- unfinished profile lifecycle actions are labeled rather than exposed as
  buttons: pass.
- Korean and English status labels render without horizontal clipping at the
  full work area and `1280 x 760`: pass.

Verification: Debug build `0` warnings / `0` errors; Calibration Center
ViewModel `70/70`; docking `27/27`; two verifier EXE runs and four actual-EXE
UI runs (Korean and English, full work area and `1280 x 760`) exited `0`;
the four after screenshots passed quality on attempt 1, with the separate
pre-change baseline retained for comparison.

Evidence: `artifacts/current/20260723-calibration-availability/`.

Boundary / next dependency: this does not implement any disabled calibration
capability. Enabling one requires its own typed workflow and evidence gate.
