# Truthful alignment status summary

Date: 2026-08-06

Status: Complete

## Scope

This correction makes the Studio header describe the actual alignment stage
already present in the recipe and that step's current state. It closes
`PL-0005` without changing alignment arithmetic, recipe execution, layout,
theme resources, or the explicit Preview/Publish/Run contract.

## Operator-visible contract

The summary selects the most downstream present stage. If a recipe contains
more than one instance of that stage, the last ordered instance owns the
summary.

| Present recipe stage | Header format |
| --- | --- |
| Re-grid Height Map | `A3 Re-grid Height Map | <actual State>` |
| Apply XYZ Affine | `A2 Apply XYZ Affine | <actual State>` |
| XYZ Affine Solve | `A1 XYZ Affine Solve | <actual State>` |
| Legacy XYZ Affine Transform | `Legacy XYZ Affine Transform | <actual State>` |
| None | `Alignment not taught` |

Examples of `<actual State>` include `Waiting for upstream`, `Preview ready`,
and `Published`. A `State` property change raises only the
`AlignmentStatusSummary` presentation notification. It does not invoke
Preview, Publish, Run, Validation, or another product action.

## Before and after evidence

The current teaching template contains a legacy transform and an A3 Re-grid
Height Map whose state is `Waiting for upstream`.

| Layout | Before | After | Result |
| --- | --- | --- | --- |
| Wide `1920 x 1040` | `before-wide.png` reported `Legacy affine scaffold taught, not calculated` | `after-wide.png` reports `A3 Re-grid Height Map | Waiting for upstream` | Pass |
| Compact `1280 x 760` | `before-compact.png` reported the same stale legacy text | `after-compact.png` reports `A3 Re-grid Height Map | Waiting for upstream` | Pass |

All four application-only captures passed the built-in screenshot-quality
gate. Both after captures used the actual Release EXE on dynamically selected
leftmost monitor `\\.\DISPLAY2`, bounds `[-1920,365,1920,1080]`; actual
window bounds were `[-1920,365,1920,1040]` and
`[-1920,365,1280,760]`. The header remained readable in the existing graphite
theme, with no overlap, new clipping, platform-default control, or workflow
change observed. No control template or interactive visual state changed in
this text-only correction.

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-alignment-status-summary\`

## Verification

- Debug solution build: `0` warnings, `0` errors.
- Release solution build: `0` warnings, `0` errors.
- Tool Recipe teaching verification: `35/35`, including none, legacy, A1,
  A2, A3, state-change notification, and no-action checks.
- The same verifier already runs through
  `.github/workflows/ci.yml --verify-tool-recipe-teaching`; hosted CI remains
  unavailable until a future authorized commit and push.
- Wide and Compact shell and Re-grid Tool Lab screenshot-quality reports:
  accepted on attempt 1.
- Refreshed nine-input human-owner R0 Wide and Compact `-ValidateOnly`:
  passed; neither command launched the application.
- Release Shell assembly SHA-256:
  `613022257925EBE4EC2612C7ADDD0508116DBF7CCAA79E688333056E76CB27F8`.

## Completion record

```text
Status: Complete
Scope: Replace the obsolete alignment header text with the most downstream actual A1/A2/A3/legacy step state and refresh it on State changes
Acceptance criteria: downstream stage/state precedence -> pass; presentation notification without Preview/Publish/Run -> pass; focused and solution verification -> pass; current-build Wide/Compact before/after evidence -> pass
Verification: Debug 0/0; Release 0/0; Tool Recipe teaching 35/35; four after screenshot quality reports accepted; R0 Wide/Compact ValidateOnly pass
Evidence: this document; .proofline/issues/PL-0005.json; D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-alignment-status-summary\
Boundary / next dependency: hosted CI requires a future authorized commit/push; product-owner unaided Wide/Compact R0 remains external; the next software correction is release-policy reconciliation
```
