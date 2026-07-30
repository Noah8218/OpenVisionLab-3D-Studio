# OpenVisionLab 3D Completeness Results and Overlays

Date: 2026-07-29

Status: Complete

Backlog scope: `H-05`, `H-06`, `H-07`

## Outcome

Completeness Grid now turns the existing H-02/H-03/H-04 cell evidence into
one deterministic presence inspection result:

```text
authored inclusive coverage and reference-relative height limits
  -> exact Pass/Fail for every stable cell
  -> passed/failed cell counts
  -> aggregate Pass only when every cell passes
  -> the same coordinate-true green/red overlay in Height Image and 3D
```

The existing seven-parameter H-02 recipe remains readable and executes as
evidence-only `Warning`. A decision policy is enabled only when all three
additional parameters are present:

- `MinimumFiniteCoverageRatio`, inclusive and constrained to `0..1`;
- `MinimumReferenceRelativeMeanRawHeight`, inclusive;
- `MaximumReferenceRelativeMeanRawHeight`, inclusive.

A partial policy group, inverted height limits, or out-of-range coverage
fails closed. A cell without a finite mean always fails when a policy exists.

## Ownership

- Core owns `C3DCompletenessPresencePolicy`, cell decisions, aggregate fields,
  and stable `C3DCompletenessCellOverlay` descriptors.
- Tools owns the deterministic threshold comparisons, fail-closed missing
  behavior, child-to-aggregate rule, metrics, overlays, and content identity.
- Workbench owns typed PropertyGrid authoring and explicit Preview/Publish.
- Height Image and Viewer only render the shared descriptors; they do not
  calculate decisions.
- Ordered graph and production Runner consume the same Tools result.

Editing a policy or ROI does not Preview, Publish, Run, or save automatically.
Source cells and the authored source identity remain unchanged.

## Controlled fixture

The `8 x 8` fixture uses policy:

```text
MinimumFiniteCoverageRatio = 0.5
MinimumReferenceRelativeMeanRawHeight = -3
MaximumReferenceRelativeMeanRawHeight = 3
```

| Cell | Coverage | Relative mean | Decision |
| --- | ---: | ---: | --- |
| `r001.c001` | `1.00` | `2` | Pass |
| `r001.c002` | `0.75` | `4` | Fail |
| `r002.c001` | `0.50` | `-2` | Pass |
| `r002.c002` | `0.00` | missing | Fail |

Result: `2` passed, `2` failed, aggregate `Fail`, and `4` overlays.

The policy output SHA-256 is:

```text
1B051233FFCCC65FD72A4CB50299C629C8BCE7929E7AC4CA3CA3F33653DBF8CE
```

The evidence-only H-02 output remains:

```text
C535D7C8DF40C585E5A22EBF5594D48768A89A20DF257A82DE6F3E75752BED6C
```

An independent all-valid fixture proves `4` passed, `0` failed, aggregate
`Pass`. A separate all-missing Inspection Grid fixture proves `0` passed,
`4` failed, aggregate `Fail`. The mixed fixture proves partial failure and an
individual all-missing cell that fails closed.

## Verification

Commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Runner.exe `
  --verify-c3d-completeness-grid `
  --report artifacts/current/20260729-completeness-results-overlays/golden-report.txt

OpenVisionLab.ThreeD.Shell.exe `
  --verify-tool-height-measurement-workbench `
  artifacts/current/20260729-completeness-results-overlays/workbench-report.txt

OpenVisionLab.ThreeD.Runner.exe `
  --tool-recipe <policy-fixture-recipe> `
  --source <controlled-c3d> `
  --report <runner-report> `
  --run-record <json> `
  --html-report <html> `
  --csv-report <csv>
```

Results:

- Release build: `0` warnings, `0` errors;
- Completeness golden: `23/23`;
- height measurement Workbench: `50/50`;
- Tool Recipe selections: `29/29`;
- Inspection Workspace: `63/63`;
- Tool Recipe teaching: `28/28`;
- Recipe Manager/PropertyGrid: `37/37`;
- Workbench docking: `33/33`;
- Artifact Navigator/Output Compare: `31/31`;
- Shell smoke options: `24/24`;
- code structure: `17/17`;
- production Runner: `Fail`, `1/1` step, `11` metrics, `4` overlays, exact
  output SHA parity;
- Wide and Compact current-Release screenshot quality: accepted on attempt
  `1`;
- `git diff --check`: pass after whitespace correction.

## UI evidence

- `before-wide.png`: fresh current H-02 Release baseline captured before this
  implementation; the output is evidence-only and has no colored cell result.
- `after-wide.png`: current Release, `1920 x 1040`, linked 3D and Height Image;
  both show the same two green Pass and two red Fail cells.
- `after-compact.png`: current Release, `1280 x 760`; aggregate Fail,
  passed/failed counts, and both linked overlay surfaces remain visible.

All captures contain only the application window.

Evidence root:

- `artifacts/current/20260729-completeness-results-overlays/`

## Completion record

Status: Complete

Scope: `H-05/H-06/H-07` authored presence policy, deterministic per-cell
Pass/Fail, passed/failed counts, all-children aggregate result, stable
coordinate-true Height Image and 3D overlays, and Workbench/Runner parity.

Acceptance criteria: inclusive typed policy -> pass; previous evidence-only
recipe compatibility -> pass; exact child decisions -> pass; all-missing cell
fails closed -> pass; aggregate equals child statuses -> pass; stable overlay
identity and geometry -> pass; linked 2D/3D rendering -> pass; all-valid and
partial-failure fixtures -> pass; production Runner parity -> pass; source
immutability and explicit lifecycle -> pass.

Verification: Release build `0/0`; golden `23/23`; Workbench `50/50`; focused
regressions and code structure all pass; production Runner reports `2` Pass,
`2` Fail, aggregate `Fail`, `4` overlays, and SHA
`1B051233FFCCC65FD72A4CB50299C629C8BCE7929E7AC4CA3CA3F33653DBF8CE`;
Wide/Compact capture quality accepted.

Evidence:
`artifacts/current/20260729-completeness-results-overlays/`, including policy
recipe, C3D fixtures, golden/Workbench/regression reports, Runner text/JSON/
HTML/CSV, and before/after captures.

Boundary / next dependency: failed-cell navigation (`H-08`), mapping repeated
Tab names to cell results (`H-10`), Validation Set examples (`H-11`), and
Completeness threshold assistance (`H-12`) are not part of this slice.
Detected/oriented-region routing (`H-09`) is blocked by `E-11/G-12`.
Physical calibration, production tolerance, and certified metrology remain
external or unverified. The next eligible product slice is
`H-08/H-10 failed-cell review and repeated-Tab result mapping`.
