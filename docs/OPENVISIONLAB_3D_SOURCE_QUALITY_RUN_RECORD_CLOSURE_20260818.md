# OpenVisionLab 3D Source Quality Run Record Closure

Date: 2026-08-18
Status: Complete
Proofline issue: `PL-0020`
Backlog item: `L-10`

## Outcome

Run Record schema `1.8` now carries the exact identified Source Quality report
used by the ordered inspection. The operator can see source quality state,
grid size, valid and missing ratios in Results and can inspect the complete
identity, invalid-cell mask, coordinate, provenance, and channel evidence.
JSON, HTML, CSV, Shell text, and Runner text preserve the same evidence.

This closes the gap where inspection status and timing were retained but the
quality of the source used for that decision was not visible in the result
record.

## Product Contract

- Shell reuses the already loaded `SourceQualityReport`; it does not load the
  source or run the quality analyzer a second time.
- Runner creates the report from the one source snapshot already loaded for
  ordered execution.
- Source entity, byte length, content/root SHA-256, grid, unit, and frame must
  match. A mismatched supplied report fails before any inspection step runs.
- Legacy and non-raw A2 routes remain readable with explicit `Unavailable`
  evidence instead of invented quality.
- Source Quality remains evidence only. It does not change recipe parameters,
  tool routing, Preview, Publish, Run status, or deterministic result identity.

## Operator Evidence

The Results Run Record card now keeps two independent evidence rows:

1. Source Quality state and concise grid/coverage summary.
2. Threshold-correction state and its existing details.

The final Compact summary is visible without clipping as
`4 × 4 · 유효 100.0% · 누락 0.0%`. Full Source Quality evidence remains
available through the row tooltip and accessibility help text. The card uses
the existing semantic Pass, Warning, Fail, panel, divider, and text brushes;
no new platform-default control style was introduced.

Actual Release EXE evidence:

- Wide `1920 x 1040`:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0020-source-quality-run-record\after\after-wide-results.png`
- Compact `1280 x 760`:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0020-source-quality-run-record\after\after-compact-results.png`
- Closest reproducible baseline before the Results change:
  the matching `before\before-wide-results.png` and
  `before\before-compact-results.png` files under the same evidence root.

Both final captures passed screenshot quality on attempt 1. The application
window intersected the dynamically selected leftmost monitor reported as
`monitorBounds=-2400,456,0,1806`; final rectangles were reported as
`windowRect=-2400,456,0,1756` for Wide and
`windowRect=-2400,456,-800,1406` for Compact.

## Verification

All test-only files and reports are physically under:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0020-source-quality-run-record\`

Current checks:

| Check | Result |
| --- | --- |
| Release solution build | Pass, 0 warnings / 0 errors |
| Shell ordered Run | Pass, 15/15 |
| Run Record history and legacy fallback | Pass, 12/12 |
| Source Quality workspace regression | Pass, 18/18 |
| Artifact-owned A2 compatibility | Pass, 22/22 |
| General 27-step Runner export | Pass, 21/21 |
| Surface Match prepared-scene export | Pass, 23/23 |
| Workbench docking/theme | Pass, 87/87 |
| Shell smoke options | Pass, 40/40 |
| Code structure and ownership | Pass, 29/29 |

An actual Runner CLI replay of the 4×4 Thickness fixture produced schema
`1.8`, status `Pass`, Source Quality SHA-256
`A79D5EA32773AD6D07702BCD6E9323F16A74024A7BAD33FADEE8709787906B98`,
and invalid-cell-mask SHA-256
`291D040929C1BF8CF515B7A4B5553A3BF03812BEF0D241AC1737DFED669603EB`.
The same mask identity was found in the Runner text, JSON, HTML, and CSV.

The refreshed nine-input human-owner R0 package passes Wide and Compact
`-ValidateOnly`; automated validation does not replace the owner's unaided
run.

## Boundaries

- The 4×4 and synthetic fixtures prove deterministic software identity and
  presentation, not physical calibration, metrology, Gauge R&R, or production
  approval.
- Source Quality is not yet consumed by compatible-tool suggestions. That is
  the separate `B-13` backlog item.
- Product-owner unaided Wide/Compact R0 remains required for `A-01`, Workspace
  v3 `8/8`, and human-usability or release-acceptance claims.

## Durable Completion Record

```text
Status: Complete
Scope: Complete PL-0020/L-10 exact Source Quality evidence across ordered execution, schema 1.8 Run Record, JSON/HTML/CSV/Shell/Runner text, Results, legacy/unavailable handling, fail-closed mismatch, and refreshed R0 package
Acceptance criteria: exact identified report retained -> pass; Shell and Runner avoid a second source load/execution -> pass; export and Results parity -> pass; mismatch fails before inspection and legacy/non-raw routes are explicit Unavailable -> pass; Release/focused/Wide/Compact/R0 verification -> pass
Verification: Release 0/0; ordered Run 15/15; history 12/12; Source Quality 18/18; A2 compatibility 22/22; general Runner 21/21; Surface Match 23/23; docking 87/87; Shell options 40/40; structure 29/29; actual Runner text/JSON/HTML/CSV mask parity; Wide/Compact EXE quality and monitor intersection; R0 Wide/Compact ValidateOnly
Evidence: docs/OPENVISIONLAB_3D_SOURCE_QUALITY_RUN_RECORD_CLOSURE_20260818.md; .proofline/issues/PL-0020.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260818-pl0020-source-quality-run-record/
Boundary / next dependency: owner R0 remains external; synthetic raw-height is not metrology; L-12 Completeness per-cell result export is the selected next dependency-ready software priority
```
