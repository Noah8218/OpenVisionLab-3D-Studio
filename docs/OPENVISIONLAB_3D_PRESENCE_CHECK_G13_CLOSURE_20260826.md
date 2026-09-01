# OpenVisionLab 3D G-13 Presence Check Closure

Date: 2026-08-26

Status: Complete for the bounded software slice

## Scope

G-13 adds one deterministic Presence Check over one explicitly authored,
source-bound `GridRectangle` feature in a C3D height field. The decision uses
inclusive finite-cell coverage and inclusive mean raw-height limits. A feature
with no finite mean is `Fail`; no replacement value is fabricated.

The route preserves the existing file-first action contract:

```text
source-bound C3D + GridRectangle + policy
  -> explicit Preview
  -> typed PresenceCheckResult evidence
  -> explicit Publish reusing the Preview instance
  -> explicit ordered Run through the Runner graph
  -> JSON / HTML / CSV Run Record evidence
```

This is a source/grid software contract. Raw height is reported in the source
unit and is not calibrated physical metrology. G-13 does not infer a mask,
rasterize `GridCircle` or `GridPolygon`, rerun G-11/G-12 presentation logic,
change the C3D source, or create a physical-area claim.

## Ownership and state flow

- `C3DPresenceCheckPolicy` and `C3DPresenceCheckOutput` in Core own the
  versioned policy, source/entity/content identity, unit/frame, exact feature
  rectangle, counts, coverage, nullable mean, decision, reason, and output
  hash.
- `C3DPresenceCheckRule` in Tools validates Studio identity and selection
  compatibility, then delegates finite-cell statistics to the existing public
  vendored `HeightMapRegionStatisticsTool`. It owns only the Studio policy and
  identity mapping.
- `ToolRecipeHeightMeasurementExecution` owns the `presence-check` recipe
  adapter. `ToolRecipeValidator` and the Core selection matrix require one
  source plus one ordered `GridRectangle` feature with exactly the three
  Presence Check parameters.
- `ToolRecipeOrderedGraphExecution` carries the same typed output into Run
  results. `ToolRecipeOrderedGraphRunRecordProjection` validates current-source
  identity, metrics, policy, decision, and hash without a second calculation.
- The existing Workbench catalog, PropertyGrid adapter, Preview/Publish
  lifecycle, Artifact Registry, Displayed Outputs, and Results projection
  expose `PresenceCheckResult` as evidence-only output. It is not presented as
  a synthetic 3D display or comparison artifact.
- `RunRecordWriter` retains the feature decision and statistics in JSON, HTML,
  and CSV, including one `presenceFeature` CSV row.

## Decision contract

| Item | Contract |
| --- | --- |
| Feature geometry | One explicit recipe-owned `GridRectangle` (`X=column`, `Z=row`) |
| Coverage | `finite cells / total cells`, inclusive minimum |
| Mean | Finite raw-height mean, inclusive minimum and maximum |
| Missing feature | No finite mean -> `Fail`, nullable mean remains absent |
| Identity | Output/input/root source IDs, source SHA-256, unit, frame, selection ID, exact rectangle |
| Output | `PresenceCheckResult`, stable `ContractVersion=1.0`, deterministic SHA-256 |
| Mutation | Source values, selection, recipe bytes, and Preview/Publish action boundaries remain unchanged |

## Focused acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| C1. Typed source-bound contract and deterministic feature hash | Pass | Runner golden `16/16`; source SHA `9AFE1D90A9FDF39D7C3F5EAF106002E77B1A9CD8DB98308FD4D1A4A482681E9C`; good output SHA `8005C3F98A38FD0983187C93337CF723161614BE67144D87C4428CBA87020779`. |
| C2. Good/missing, inclusive limits, and fail-closed invalid input | Pass | Runner golden proves good `Pass` with coverage `1`, mean `10`; missing `Fail` with coverage `0`, mean missing; partial coverage, invalid policy, malformed selection, and binding mismatch all fail closed. |
| C3. Recipe, Workbench, and explicit action contracts | Pass for bounded route | Workbench `14/14`; typed PropertyGrid, exact parameters, storage validation, Preview, Publish reuse, save/reopen, and new-recipe reset pass. Ordered graph Run is covered by the same Runner typed route. |
| C4. Run Record and export parity | Pass | Runner golden proves ordered projection reuses the typed instance and JSON/HTML/CSV preserve the same feature decision, identity, statistics, and hash. |
| C5. Build, tests, hygiene, and current-build runtime | Pass for this bounded slice | Release solution build `0` warnings/`0` errors; Release tests `10/10`; Runner `16/16`; Workbench `14/14`; `git diff --check` has no whitespace errors; actual Release EXE Wide/Compact screenshots are accepted and intersect the selected monitor. |

## Verification commands and evidence

All generated test, report, fixture, and screenshot files are physically under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-13\`.

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore -v:minimal`
  — `0` warnings, `0` errors.
- `dotnet test OpenVisionLab.ThreeDStudio.slnx -c Release --no-build -v:minimal`
  — `10` passed, `0` failed, `0` skipped.
- `OpenVisionLab.ThreeD.Runner.dll --verify-c3d-presence-check --report ...`
  — `Presence Check golden verification: PASS (16/16)`; report:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-13\c3d-presence-check-release.txt`.
- `OpenVisionLab.ThreeD.Shell.dll --verify-presence-check-workbench ...`
  — `PresenceCheckWorkbench|pass=True|checks=14/14`; report:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-13\presence-check-workbench-release.txt`.
- `git diff --check` — no whitespace errors. Git's line-ending normalization
  warnings are unrelated to whitespace failures.
- Actual current Release Shell EXE smoke used the existing recipe-owned
  Presence Check route with `--smoke-focus-selected-tool` and explicit
  measurement Preview:
  - Compact `1280x760`: accepted screenshot and monitor report at
    `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-13\ui\presence-check-1280x760.png`
    and `presence-check-1280x760-quality.txt`.
  - Wide `1920x1040`: accepted screenshot and monitor report at
    `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-13\ui\presence-check-1920x1040.png`
    and `presence-check-1920x1040-quality.txt`.

The current machine reported two independent monitors before the EXE launch.
The smaller left monitor was `\\.\DISPLAY2`; the captured DPI-aware window
rectangle intersected its logical bounds at `125%` DPI. The compact screenshot
shows `Step 01: Presence Check`, `Preview ready`, the applied feature rectangle
(`column 0, row 0, columns 2, rows 1`), and the evidence-only `Presence Check
Preview` with `mean raw height 10`, `finite coverage 100.0%`, `2 finite`, and
`0 missing cells`. The wide screenshot shows the same selected-tool surface
without required-text clipping or unexplained overlap.

## Durable boundaries

The following are intentionally outside this closure:

- polygon/circle-to-mask rasterization, threshold-to-mask authoring, and any
  inferred G-11/G-12 rerun;
- G-14 Fill Height, G-15 aggregate acceptance, and G-16 region dimensions;
- calibrated units, physical area, traceability, uncertainty, Gauge R&R,
  hardware, camera/lighting, PLC/I/O/robot, cloud, deployment, or production
  control;
- SDK package/source mutation. The existing public statistics tool was reused;
- owner R0, hosted CI, release/package/tag/publication/deployment, product
  version changes, commit/push, and PC restart;
- the full WPF visual qualification matrix: no G-13 XAML or shared style was
  changed, while current Wide/Compact EXE evidence at `125%` DPI is recorded.
  Alternate themes, `100%`, `150%`, `175%`, and `200%` DPI, held pointer-down
  state coverage, and the repository-wide UI performance baseline remain
  unverified.

## Completion record

```text
Status: Complete
Scope: G-13 explicit source-bound GridRectangle Presence Check with typed Core/Tools contracts, recipe/Workbench Preview and Publish route, ordered Runner/Run Record parity, JSON/HTML/CSV evidence, good/present and missing fixtures, and current Wide/Compact EXE evidence.
Acceptance criteria: C1 typed identity/metrics/hash -> pass; C2 inclusive coverage/raw-height and fail-closed missing/invalid inputs -> pass; C3 recipe/Workbench PresenceCheckResult and explicit Preview/Publish/Run route -> pass for bounded software surfaces; C4 ordered projection and JSON/HTML/CSV parity -> pass; C5 Release build/tests, focused checks, diff hygiene, and current Wide/Compact runtime -> pass for this bounded slice.
Verification: Release build 0/0; Release tests 10 passed/0 failed/0 skipped; Runner 16/16; Workbench 14/14; accepted actual EXE screenshots at 1280x760 and 1920x1040 with monitor intersection; git diff --check no whitespace errors.
Evidence: this document; .proofline/issues/PL-0053.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/G-13/.
Boundary / next dependency: G-14 Fill Height per region against a reference surface is the next dependency-ready software slice; full UI DPI/theme/performance qualification, owner R0, maximum-C3D limits, calibration, release, commit, push, version, package, deployment, and PC restart remain outside this closure.
```
