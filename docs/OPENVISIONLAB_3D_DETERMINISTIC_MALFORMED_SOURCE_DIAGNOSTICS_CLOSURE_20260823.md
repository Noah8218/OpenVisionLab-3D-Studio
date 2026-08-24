# OpenVisionLab 3D Deterministic Malformed-Source Diagnostics Closure

Date: 2026-08-23

Issue: `PL-0046`

Backlog item: `B-10`

Status: Complete

## Outcome

OpenVisionLab now exposes one deterministic, persisted grid-integrity result
for each current C3D Source Quality report. Operators can distinguish valid
implicit grid structure from a rejected malformed source and can review the
same evidence in Source Quality, Results, Run Records, exports, and a privacy-
safe support bundle without rerunning source analysis.

The operator problem was that coverage and missing-height evidence did not say
whether the sample grid itself was structurally coherent. The product response
keeps source trust visible and read-only: diagnostics explain the current
input, while Preview, Publish, Run, recipe state, and source selection retain
their existing explicit contracts.

## Contract

`SourceQualityReport` current schema is `1.1`. It requires one
`GridDiagnostics` payload with these four checks in this exact order:

1. `Topology`;
2. `LocatorMonotonicity`;
3. `DuplicateLocator`;
4. `CoordinateFiniteness`.

Each check carries a typed `Pass` or `Error`, affected count, first affected
sample/row/column/component when applicable, and stable evidence text. The
aggregate state is derived from the checks. Core validation fails closed when
the order, count, state, location tuple, component, or declared/observed/unique
count relationship contradicts the payload. In particular:

- a passing topology requires declared = observed = unique;
- duplicate state and affected count must match observed - unique;
- sample ordinal must be inside the observed range;
- row, column, ordinal, and component location fields are paired;
- an out-of-grid first locator is representable only with a topology error;
- current schema `1.1` requires diagnostics, while legacy schema `1.0` must
  omit them.

The legacy `1.0` JSON compatibility fixture remains byte-stable at SHA-256
`E2176611372E01F26A8208A9C7C09154209A8DB50BA4774A1F4DA6670B9F82A2`.

## C3D Format Boundary

The supported C3D file contract is an 8-byte width/height header followed by
one row-major float32 height per declared cell. It has no explicit locator or
XYZ coordinate payload. A structurally valid C3D therefore receives an
implicit row-major locator sequence; the software does not reinterpret a
height value as a coordinate.

Raw height `0`, `NaN`, or infinity remains a missing-coverage sample under the
existing Source Quality policy. It is not a coordinate-finiteness error.
Explicit non-monotonic, duplicate-locator, and non-finite XYZ failures are
proven through the source-neutral explicit-grid analyzer seam and SDK Tool,
not fabricated from C3D height bytes.

Malformed C3D topology is rejected before a report replaces the current
source, using these stable reasons:

| Reason | Meaning |
| --- | --- |
| `HeaderIncomplete` | The 8-byte grid header is incomplete. |
| `DimensionsNonPositive` | Width or height is not positive. |
| `CellCountOverflow` | Declared dimensions exceed supported cell-count or byte-length range. |
| `PayloadLengthMismatch` | Actual byte length does not match the declared dimensions. |

## Numerical Ownership Refactor Proof

The first implementation placed grid counting, locator ordering, duplicate
detection, and coordinate-finiteness arithmetic in a Data analyzer. The
structure guard correctly identified that as new Studio numerical ownership.

The final structure is:

```text
C3D Source Quality analysis / explicit diagnostic fixture
  -> SourceQualityGridDiagnosticsAnalyzer (Studio Data adapter)
  -> OpenVisionLab.Vision3D.FeatureExtraction.GridDiagnosticsTool
  -> typed GridDiagnosticsResult
  -> Core SourceQualityGridDiagnostics validation and report composition
```

| Proof dimension | Before | After |
| --- | --- | --- |
| Calculation owner | Studio Data analyzer | public sealed SDK `GridDiagnosticsTool` |
| Call path | Source Quality -> Data arithmetic -> Core report | Source Quality -> thin Data adapter -> vendored SDK Tool -> Core validation/report |
| Dependency direction | Studio owned reusable calculation | Studio Data depends on the fixed SDK package; SDK has no Studio dependency |
| Studio responsibility | Calculation plus mapping | typed input/enum/result mapping, C3D implicit-format policy, Core contract validation, and one product-specific implicit-coordinate message |
| Removed coupling | locator/count/finiteness arithmetic in Studio | no locator/count/finiteness calculation remains in the adapter |

The SDK source is committed as
`8be38403d0d00698431d7ffa4de60a63289672c6`. Studio consumes only the vendored
`OpenVisionLab.Vision3D 3.0.1-dev.20260823.grid-diagnostics.1` package with
SHA-256
`964A543C007687ED93F2AFEC682245A76C61DA2AE42EC9B786FB8CC27BED976C`.
The decreasing migration baseline remains at zero debt and now registers the
Data adapter as the 35th reviewed boundary with a zero numerical-signal
ceiling.

## Operator And Evidence Surfaces

- Source Quality shows four localized single-column diagnostic cards with
  explicit state text, icon, semantic border/background, and wrapped evidence.
- The global status and title-bar quality badge include the aggregate grid
  state. Wide Korean and Compact English retain the complete text without
  trimming; navigation remains view-only and does not edit or execute.
- The actual WPF Error row resolves the semantic Error resources and retains a
  276-character coordinate-finiteness explanation without clipping.
- Ordered Shell and Runner records retain the exact report instance or exact
  identified report evidence. JSON, HTML, CSV, Shell/Runner text, Results, and
  the privacy-safe support ZIP project all four checks without source
  reanalysis.
- Contradictory current diagnostic payloads and source/report identity
  mismatches fail closed before inspection.
- Asynchronous malformed C3D load retains the previous source, clears the load
  state, and reports the exact expected reason. It does not replace the active
  source with a partial result.

## Verification

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0046-source-topology-diagnostics`

| Check actually run | Result | Reusable evidence |
| --- | --- | --- |
| Full 15-project Release solution build | `0` warnings / `0` errors | `final-current/gates/full-release-build.txt` |
| Source Quality report verifier | `22/22` | `final-current/focused-current-after-provenance/source-quality-report.txt` |
| Source Quality workspace and actual WPF Error row | `28/28` | `final-current/focused-current-after-provenance/source-quality-workspace.txt` |
| Surface Match Run Record export regression | `25/25` | `final-current/focused-current-after-provenance/surface-match-run-record-export.txt` |
| Completeness JSON/HTML/CSV regression | `31/31` | `final-current/focused-current-after-provenance/c3d-completeness-grid.txt` |
| Shell ordered Run regression | `16/16` | `final-current/focused-current-after-provenance/current-recipe-ordered-run.txt` |
| Privacy-safe support bundle regression | `15/15` | `final-current/focused-current-after-provenance/privacy-safe-support-bundle.txt` |
| Shell smoke command-line contract | `47/47` | `final-current/focused-current-after-provenance/shell-smoke-command-line.txt` |
| Standard .NET test facade | `2/2` | `final-current/standard-tests/` |
| Vision SDK smoke | `173/173`, including implicit and malformed explicit grid diagnostics | `sdk-boundary/sdk-smoke.txt` |
| Package-only SDK consumer | Pass | `sdk-boundary/package-consumer-run.txt` |
| Studio SDK adapter/provenance regression | `26/26` | `final-current/gates/vision-sdk-3d.txt` |
| Fixed Vision SDK package boundary | Pass | `final-current/gates/vision-sdk-package.txt` |
| Studio structure/decreasing migration guard | `68/68`, zero debt, 35 reviewed boundaries | `final-current/gates/code-structure.txt` |
| NuGet health | 15 projects, vulnerable `0`, deprecated `0` | `final-current/gates/nuget-package-health.txt` |
| Actual malformed-load EXE smoke | Pass; previous source retained, exact `CellCountOverflow` text matched, load state cleared | `final-current/runtime/async-failure-exe/async-c3d-failure.txt` |
| Wide Korean / Compact English Source Quality EXE | Pass; 4/4 cards and complete badge visible, screenshot quality attempt 1 | `final-current/runtime/source-quality-ui-current/` |

The EXE evidence used the dynamically selected leftmost monitor and verified
window intersection. Runtime UI evidence was captured at the workstation's
current 125% scaling. DPI 100%, 150%, 175%, and 200% were not exercised and
remain unverified.

## Boundaries

- This is deterministic software diagnostics for the supported C3D contract;
  it is not calibration, certified metrology, Gauge R&R, or production
  approval.
- A representative maximum C3D input and accepted memory/load-time limits were
  not supplied, so this does not close the separate large-C3D qualification.
- Product-owner unaided Wide/Compact R0 remains deferred and was not replaced
  by automation.
- Hosted CI was not run for this uncommitted Studio tree. No Studio commit,
  push, version change, release package, tag, or release was created.
- The SDK source commit exists locally so the package has reproducible source;
  it was not pushed.
- Camera, lighting, PLC, robot, cloud, account, and production-line control
  remain outside this scope.

## Completion Record

```text
Status: Complete
Scope: PL-0046/B-10 schema-1.1 deterministic grid diagnostics, fail-closed malformed C3D reasons and payload validation, SDK numerical ownership, visible Source Quality/Results state, exact exports, and previous-source-retaining asynchronous failure
Acceptance criteria: four ordered typed diagnostics -> pass; schema 1.1 current and schema 1.0 exact compatibility -> pass; contradictory payloads fail closed -> pass; C3D implicit locator/missing-height boundary remains truthful -> pass; UI and persisted exports expose exact evidence without execution/reanalysis -> pass; malformed async load retains the previous source with exact reason -> pass; SDK owns reusable calculation and Studio migration debt remains zero -> pass
Verification: Release 0/0; Source Quality 22/22; workspace 28/28; export 25/25; Completeness 31/31; ordered Run 16/16; privacy 15/15; Shell options 47/47; standard tests 2/2; SDK smoke 173/173; package consumer pass; structure 68/68; fixed package pass; NuGet 15 projects / vulnerable 0 / deprecated 0; Wide/Compact EXE and malformed-load EXE pass at current 125% scaling
Evidence: docs/OPENVISIONLAB_3D_DETERMINISTIC_MALFORMED_SOURCE_DIAGNOSTICS_CLOSURE_20260823.md; .proofline/issues/PL-0046.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260823-pl0046-source-topology-diagnostics/
Boundary / next dependency: R0, hosted CI, 100/150/175/200% DPI, maximum-C3D performance, physical metrology, Studio commit/push/release, and SDK push remain outside this closure; E-13 is the next dependency-ready inventory item
```
