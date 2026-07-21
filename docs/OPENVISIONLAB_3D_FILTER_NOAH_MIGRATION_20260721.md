# Filter Library-Noah Migration

Updated: 2026-07-21

Status: **Complete for deterministic software structure and regression evidence;
not a physical feature or metrology claim.**

## User goal

Move the remaining deterministic Median filter arithmetic to `Lib.ThreeD`
without weakening the existing C3D zero/missing rule, typed recipe adapter,
Viewer lifecycle, or Runner evidence.

## Current structure

- Current responsibility owner: Studio `C3DMedianFilterRule` owns both C3D
  adapter validation and the nested median-window calculation.
- Current call path: C3D snapshot -> Studio rule -> derived C3D snapshot.
- Current dependency direction: Studio-only numerical code.
- Current state/data owner: Data owns C3D bytes, identity, and the conversion
  of C3D zero/non-finite cells to `NaN`; Studio Tools owns recipe/lifecycle
  validation and currently owns the numerical loop.

## Intended new structure

- New responsibility owner: `Lib.ThreeD.DeterministicMedianFilterTool` owns
  only source-neutral finite/`NaN` grid median arithmetic.
- New call path: C3D snapshot -> Studio typed adapter -> Noah row-major values
  and kernel -> Noah result -> Studio derived C3D snapshot.
- New dependency direction: Studio Tools -> vendored `Lib.ThreeD`; Noah has no
  Studio/C3D/recipe/WPF dependency.
- New state/data owner: Data remains the only C3D-zero/missing and derived
  finite-zero boundary; Studio retains IDs, provenance, metrics, and lifecycle;
  Noah owns the numerical output values and changed-cell count.

## Preserved behavior

- Kernels are exactly `3`, `5`, or `7`.
- Missing (`NaN`) center cells remain `NaN`; missing neighbors are ignored.
- Windows use only in-bounds available neighbors.
- Median uses sorted finite values; even counts average the two middle values.
- The Data-layer finite-zero rejection remains unchanged after Noah returns.
- Filter remains preprocessing only and never makes measurement OK/NG.

## Structural conditions

1. Studio's Filter rule contains no nested cell/window/neighbor median loop or
   local sorting implementation and calls the Noah tool.
2. `Lib.ThreeD` has no Studio dependency and exposes the finite/`NaN`
   source-neutral contract.
3. C3D source validation, canonical derived snapshot/hash, finite-zero
   rejection, provenance, ToolResult, Preview/Publish, and Runner stay in
   Studio.

## Proof checks

- Noah smoke: spike removal plus missing-mask/border preservation: **Pass
  (`39/39`)**.
- Studio Filter Golden: existing `13/13` matrix, including finite-zero output
  rejection and deterministic output hash: **Pass**.
- Regression: Edge `13/13`, Line Fit `9/9`, package bridge `7/7`, and full
  Studio Debug build: **Pass**.
- Package: `Lib.ThreeD 2.7.4`, commit
  `5d06460c14b1edf390241b28511ce4997f70dc28`, SHA-256
  `BB44D30F8D3AB9C1CF528482CFA2A5A804D9222FFBAE258C765CEF2696EB2573`,
  and actual Runner DLL version: **Pass**.
- Structure: old Studio-loop symbols absent, new Noah call present, and no
  Noah Studio reference: **Pass**.

## Excluded

No UI change, new filter type, ROI filter, hole fill, mask interpolation,
calibration, or physical/metrology claim is introduced.
