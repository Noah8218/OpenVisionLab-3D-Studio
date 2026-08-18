# Standard Per-step Timing Evidence Closure

Date: 2026-08-18
Issue: `PL-0019` / `L-09`
Status: Complete

## Outcome

Run Record schema `1.7` now exposes one observational stage-timing contract
across ordered preparation/inspection and Surface Match reporting. It reuses
already observed durations, never reruns an algorithm, and does not change a
content identity or acceptance decision.

| Execution source | Stable stage IDs | Timing source |
| --- | --- | --- |
| Ordered recipe step | `tool-execution` | Existing `ToolResult.Elapsed` |
| Surface Match | `pose-search`, `execution-artifact`, `acceptance-evaluation` | Persisted `SurfaceMatchRuntimeReport` |

Each available timing value carries schema, state, clock, message, finite
non-negative total milliseconds, and finite non-negative stage milliseconds.
Stage totals must match the observed total. Missing Surface Match runtime is
explicitly `Unavailable`; an incompatible execution identity fails closed.

## Product And UX Behavior

- JSON, HTML, CSV, Runner, and Shell Results use the same timing values.
- Legacy records without timing remain readable and show `Unavailable`.
- The Results step table adds `Execution time` beside status and evidence.
- Compact Results removes a redundant detail line, reduces evidence-card
  spacing, and uses proportional columns so the first step row and all five
  meanings remain visible without horizontal scrolling.
- Preview, Publish, Run, Validation, editing, opening, and layout contracts are
  unchanged; restoration or reporting does not execute inspection.

## Verification

- Release solution build and final Shell Release rebuild: `0` warnings, `0`
  errors in each build.
- Current-recipe ordered Run: `13/13`.
- Surface Match Run Record export: `22/22`.
- Artifact-owned ordered Runner: `19/19`.
- Run Record history and legacy fallback: `12/12`.
- Workbench docking/theme: `87/87`.
- Shell smoke command line: `40/40`.
- Code structure and ownership guard: `29/29`.
- Actual Release EXE Wide `1920 x 1040` and Compact `1280 x 760`: screenshot
  quality accepted on attempt 1; both windows intersect the dynamically
  selected leftmost `DISPLAY2`.
- Refreshed fixed-input Wide and Compact R0 `-ValidateOnly`: pass; no
  application launched.

The true pre-edit screenshot was not captured. The closest reproducible
baseline is the PL-0016 Wide Results image at
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0016-shell-ordered-thickness-run\after\wide-results-run-record-latest.jpg`.
Current after evidence is under
`C:\Users\USER\AppData\Local\Temp\OpenVisionLab-3D-Studio\20260818-pl0019-standard-stage-timing\`.
The required D: root and the repository `artifacts` junction were read-only in
this sandbox, so this task used the permitted system temporary directory and
recorded that storage fallback instead of claiming D-backed output.

## Operator Review Checklist

1. Open a current Run Record in Results.
2. Confirm each ordered row shows state, execution time, and evidence together.
3. Hover a truncated timing or evidence value and confirm the full tooltip.
4. Open a legacy record and confirm timing says `Unavailable` rather than zero.
5. Export JSON, HTML, and CSV and compare stage IDs and millisecond values.

## Maturity And Next Priority

The evidence-bounded authoring-readiness judgment remains `8.6/10`. Timing
visibility improves diagnosis but does not provide human usability acceptance,
physical metrology, a hardware-independent performance SLA, or production
approval.

1. Product-owner unaided Wide/Compact R0 | Prerequisite: owner operation and observer record | Recommended model: none | Reasoning effort: none.
2. `L-10 Source Quality evidence in Run Record` | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`.
3. Large-C3D memory/performance target | Prerequisite: representative maximum C3D and accepted process-memory/load-time limits | Recommended model: none | Reasoning effort: none.

## Closure Record

```text
Status: Complete
Scope: PL-0019 shared observational stage timing for ordered steps and persisted Surface Match evidence, JSON/HTML/CSV/Runner/Results projection, legacy handling, Compact Results density, and refreshed R0 package
Acceptance criteria: C1 shared finite non-negative clocked contract excluded from identity/acceptance -> pass; C2 ordered existing elapsed projection without extra execution -> pass; C3 persisted Surface Match projection with unavailable/mismatch boundaries -> pass; C4 JSON/HTML/CSV/Runner/Results parity and legacy readability -> pass; C5 focused verification, Release, Wide/Compact EXE, no-auto-run, and R0 ValidateOnly -> pass
Verification: Release 0/0; ordered Run 13/13; Surface Match 22/22; artifact-owned Runner 19/19; Run Record history 12/12; docking/theme 87/87; Shell options 40/40; structure 29/29; Wide/Compact actual EXE quality and monitor intersection pass; R0 Wide/Compact ValidateOnly pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_STANDARD_STAGE_TIMING_CLOSURE_20260818.md; .proofline/issues/PL-0019.json; C:/Users/USER/AppData/Local/Temp/OpenVisionLab-3D-Studio/20260818-pl0019-standard-stage-timing/
Boundary / next dependency: owner R0 remains external; synthetic/raw-height evidence is not calibrated metrology; timing is observational rather than a production SLA; D-backed output was unavailable in the sandbox; L-10 is the selected next dependency-ready software priority
```
