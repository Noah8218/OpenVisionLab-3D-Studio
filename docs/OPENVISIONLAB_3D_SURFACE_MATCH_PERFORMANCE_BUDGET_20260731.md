# OpenVisionLab 3D Surface-Match Performance Budget

Date: 2026-07-31

Status: Complete for `K-11`

## Outcome

The Release Runner now owns a repeatable fixed-fixture surface-matching
performance gate. It measures a 256-sample identified SurfaceModel and
Prepared Scene under two authored search profiles while preserving the exact
execution and assessment identities across every repetition.

The operator problem is silent search-cost growth: wider authored rotation
ranges can multiply matching work even when the final pose and decision remain
correct. The commercial-workbench principle adapted here is explicit,
action-linked runtime evidence. OpenVisionLab implements that principle as an
independent Release verification contract, not as copied competitor UI,
terminology, theme, layout, or assets.

## Included scope

- Runner command
  `--verify-surface-match-performance-budget --report <path>`;
- Release-only execution, with Debug and other configurations rejected;
- one deterministic 256-triangle/256-sample synthetic model and exact
  256-sample scene;
- known pose of `18 degrees` yaw and `(15, -7, 3) mm` translation;
- `10` warm-up executions and `25` measured executions per profile;
- one bounded `11`-candidate profile and one broad `61`-candidate profile;
- wide translation limits so the broad profile exercises scoring work rather
  than rejecting most candidates before scoring;
- outer end-to-end Stopwatch timing plus the existing `pose-search`,
  `execution-artifact`, and `acceptance-evaluation` stage timings;
- min, median, nearest-rank p95, max, and every raw measured duration;
- exact repeated execution/assessment SHA-256 identity checks;
- exact Pass, coverage, RMSE, candidate-count, and recovered-pose checks.

## Fixed budgets

| Profile | Candidates | Median | P95 | Maximum |
| --- | ---: | ---: | ---: | ---: |
| bounded | 11 | `40 ms` | `80 ms` | `150 ms` |
| broad | 61 | `180 ms` | `350 ms` | `700 ms` |

These are regression ceilings for the recorded local Release fixture. They
are not production throughput requirements and do not imply equal timing on
different hardware.

## Current Release evidence

Environment: .NET `10.0.9`, Windows `10.0.19045`, x64, 12 logical processors,
workstation GC, high-resolution Stopwatch.

| Profile | Minimum | Median | P95 | Maximum | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| bounded, 11 candidates | `9.911 ms` | `11.344 ms` | `17.098 ms` | `17.846 ms` | Pass |
| broad, 61 candidates | `23.974 ms` | `34.849 ms` | `73.611 ms` | `73.629 ms` | Pass |

Both profiles recovered the exact documented pose, returned coverage `1.0`
with RMSE `1.2291427472082641E-14 mm`, and retained one execution hash and one
assessment hash across all `25` measured repetitions. The broad-profile
median also exceeded twice the bounded-profile median, proving that the matrix
exercised a materially larger workload.

## Verification

| Check | Result |
| --- | --- |
| Release solution rebuild | Pass, `0` warnings / `0` errors |
| Performance contract and budgets | Pass, `18/18` |
| Existing surface matching | Pass, `34/34` |
| Existing surface-match acceptance | Pass, `14/14` |
| Existing edge diagnostic/review | Pass, `20/20` with edge regression `21/21` |
| Code structure | Pass, `17/17` |
| Fixed R0 package | Pass, Wide/Compact `-ValidateOnly`; no application launched |

Reusable evidence:

- `artifacts/current/20260731-surface-match-performance-budget/surface-match-performance-budget.txt`;
- `artifacts/current/20260731-surface-match-performance-budget/regression/`;
- `artifacts/current/20260731-surface-match-performance-budget/r0-wide-validate-only.txt`;
- `artifacts/current/20260731-surface-match-performance-budget/r0-compact-validate-only.txt`.

No WPF, Viewer, visible text, navigation, docking, or layout changed in this
slice, so new UI screenshots and the Wide/Compact layout matrix are not
applicable. The fixed-package checks protect the current Release binary set;
they do not replace the product owner's unaided R0.

## Explicit boundaries

- Stopwatch observations never enter pose selection, coverage, acceptance,
  execution identity, or assessment identity.
- The fixture is deterministic synthetic evidence, not customer data.
- The ceilings are a local regression gate, not production throughput,
  cross-hardware equivalence, real-time scheduling, or capacity planning.
- No physical calibration, traceability, uncertainty, GR&R, or metrology claim
  is made.
- `M-17` remains open because its combined full-size Height Image and matching
  release matrix is broader than this matching-only gate.
- Camera, PLC, robot, cloud, and production-line integration remain out of
  scope.

## Closure record

Status: Complete

Scope: `K-11` Release-only fixed-fixture matching performance gate, two
candidate workloads, stage and outer timing matrix, deterministic identity
checks, and current fixed-package refresh.

Acceptance criteria: Release-only execution -> pass; fixed identified fixture
and two candidate profiles -> pass; `10` warm-ups plus `25` measurements ->
pass; median/p95/max budgets -> pass; exact pose/decision/coverage/RMSE and
stable identities -> pass; current matching/acceptance/edge/structure
regressions -> pass; current fixed package -> pass.

Verification: source baseline `c427837` plus the current K-11 working-tree
files; Release rebuild `0/0`; performance `18/18`; matching `34/34`;
acceptance `14/14`; edge diagnostic/review `20/20`; structure `17/17`; Wide
and Compact R0 `-ValidateOnly` passed without launching the application.

Evidence: this document and
`artifacts/current/20260731-surface-match-performance-budget/`.

Boundary / next dependency: human-owner R0 remains external for `A-01`.
`K-10` is the next dependency-ready software item; `K-04` remains blocked on
`B-12`, and `K-09` remains blocked on `J-12`.

1. `K-10 matching parameter experiment comparison with explicit Publish` | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `Human-owner Wide/Compact R0` | Prerequisite: product-owner unaided operation and evidence | Recommended model: none | Reasoning effort: none
