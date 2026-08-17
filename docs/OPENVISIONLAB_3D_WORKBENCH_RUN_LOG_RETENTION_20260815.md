# OpenVisionLab 3D Workbench Run-Log Retention

Date: 2026-08-15
Status: Complete
Issue: `PL-0008`

## Operator problem

The Workbench Session list previously retained every event for the lifetime of
the application. Long authoring, Preview, Run, and Validation sessions could
therefore grow this UI projection without a bound even though the same events
were already sent to the product's rolling `OVLog` files.

The two evidence surfaces were also not explained together. An operator could
not tell that the visible list was a recent in-memory view while the
Application Log files remained the durable rolling boundary.

## Implemented contract

- The production `AppendLog` path writes every event to `OVLog` first.
- The Workbench session projection then inserts the event newest-first and
  retains at most 3,000 entries by pruning only the oldest overflow.
- The localized Application Log caption states that memory/file distinction
  in English and Korean.
- The existing durable log configuration remains unchanged at 50 MB per file
  with 20 backups.
- The change does not start or alter Preview, Publish, Run, Validation, recipe,
  source, selection, or result state.

The policy stays with the existing Workbench ViewModel owner and reuses the
existing Application Log surface. No cache, service, export type, or storage
abstraction was introduced.

## Product principle

OpenVisionLab makes current state and the location of durable evidence clear by
stating exactly what remains in memory and where all Workbench events are
durably rolled.

## Verification

- Focused run-log retention and durable `OVLog`: `6/6`.
- Tool Recipe teaching: `35/35`.
- Validation Set: `84/84`.
- Recipe Manager + WPG: `40/40`.
- Shell smoke command line: `36/36`.
- Workbench docking and theme contracts: `82/82`.
- Logging integration: `4/4`.
- Code-structure guard: `29/29`.
- Debug and Release solution builds: `0` warnings, `0` errors.
- Current-build application evidence:
  - Wide `1920 x 1040` and Compact `1280 x 760`;
  - English and Korean;
  - caption visible without clipping, overlap, off-pane rendering,
    horizontal scrolling, or platform-default theme leakage;
  - all four retained captures accepted on the first quality-check attempt;
  - dynamically selected leftmost `\\.\DISPLAY2`, bounds
    `[-1920,365,1920,1080]`, with the Wide window intersecting it.
- Fixed-input R0 `-ValidateOnly`: Wide and Compact pass with refreshed hashes;
  neither command launches the application.
- GitHub Actions CI `#76` for implementation commit `e43bebb`: success.
- Proofline v2 validation, documentation-link/path checks, and
  `git diff --check`: pass.

Local verification evidence is stored physically at:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-run-log-retention\`

The first Tool Recipe verification from a copied binary-only folder failed
one check because that folder intentionally omitted the repository-owned
`recipes` template. The required repository-context rerun passed `35/35`;
this was a verification-fixture location boundary, not a product regression.

Hosted evidence:

`https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/runs/31872439326`

## Scope boundary

This slice bounds only the in-memory Workbench session projection. It does not
change the rolling-file size/count, add indefinite retention, add log export,
change application-log filters, alter recipe schemas, modify algorithms, or
claim production database/SPC behavior. Camera, PLC, robot, cloud, account,
deployment, and production-line control remain outside product scope.

Inventory remains `139 C / 17 P / 54 N / 9 E / 16 O`; this is maintenance of
an existing evidence workflow rather than a new capability.

## Next priorities

1. Product-owner unaided Wide and Compact R0 | Prerequisite: owner operation
   and observer record | Recommended model: none | Reasoning effort: none.
2. Establish a large-C3D memory/performance target | Prerequisite:
   representative maximum C3D input and accepted process-memory/load-time
   limits | Recommended model: none until the prerequisite exists | Reasoning
   effort: none.

No dependency-ready software item is selected after this closure. A newly
approved deterministic slice may be selected without treating automated
evidence as a substitute for R0.

## Completion record

```text
Status: Complete
Scope: Newest-3,000 Workbench session projection after durable OVLog routing, with localized operator-visible retention boundary
Acceptance criteria: production AppendLog cap and newest-first order -> pass; pruned events remain in flushed OVLog -> pass; localized memory/file boundary without execution side effects -> pass; affected regression, builds, structure, Wide/Compact UI, refreshed R0 inputs, documentation checks, and hosted CI -> pass
Verification: focused retention 6/6; Tool Recipe 35/35; Validation Set 84/84; Recipe Manager + WPG 40/40; Shell command line 36/36; Workbench docking 82/82; logging 4/4; structure 29/29; Debug/Release 0 warnings and 0 errors; Wide/Compact English/Korean screenshot quality accepted; R0 ValidateOnly Wide/Compact pass; GitHub Actions CI #76 success; Proofline v2, documentation, and git diff checks pass
Evidence: docs/OPENVISIONLAB_3D_WORKBENCH_RUN_LOG_RETENTION_20260815.md; .proofline/issues/PL-0008.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-run-log-retention/; https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/runs/31872439326
Boundary / next dependency: rolling-file policy and export formats unchanged; product-owner Wide/Compact R0 remains external; large-C3D redesign needs a representative maximum input and accepted budgets
```
