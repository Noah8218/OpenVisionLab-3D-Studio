# OpenVisionLab 3D Nominal Comparison and Transform Diagnostics Noah Migration

Date: 2026-08-01

Status: Complete

## Outcome

Nominal/actual mesh comparison and registration-transform diagnostics now use
public sealed Library-Noah Tools. Studio no longer owns their distance,
closest-point, sign-recovery, streaming-statistics, or matrix-diagnostic
arithmetic.

The migration preserves the existing Studio result, acceptance, identity,
artifact, and lifecycle contracts. It does not add a new UI or change an
operator workflow.

## Ownership boundary

Library-Noah now owns:

- `TriangleMeshDistanceTool`: deterministic triangle BVH construction,
  nearest-point distance, closest-point evidence, and direct/robust signed
  distance recovery;
- `NominalActualMeshComparisonTool`: streamed actual-point evaluation,
  tolerance counts, display sampling, Welford statistics, and sign-recovery
  counts;
- `RigidTransformDiagnosticsTool`: homogeneous-row error, rotation
  orthogonality error, determinant/unit error, translation magnitude, and
  rotation angle.

Studio retains:

- source path, declared unit/frame, source/artifact identity, and SHA-256
  validation;
- STL/PLY loading and strict conversion to source-neutral Noah contracts;
- canonical Studio reports, retained evidence, localized status, and progress
  adaptation;
- authored registration acceptance limits, ordered Pass/Fail/Error policy,
  evidence linkage, recipe lifecycle, and explicit execution orchestration.

`TriangleMeshDistanceIndex` remains as a compatibility adapter for existing
Studio callers. It delegates every numerical query to Noah and preserves the
existing empty-mesh error contract.

## Fixed package provenance

| Item | Value |
| --- | --- |
| Package | `Lib.ThreeD 2.8.3` |
| Exact source commit | `4420c40d3179edc7703cfef6e0ea53ac898f8f3f` |
| Target | `netstandard2.0` |
| SHA-256 | `63F70F92354257E6E2975753BC17A76118478CB6AB0C77EB487C09F5A50F0C39` |
| Vendored package | `third_party/LibraryNoah/Lib.ThreeD.2.8.3.nupkg` |

The package was built from committed source in the clean
`C:\Git\Library-Noah-surface-match-kernel` worktree. No cross-repository
`ProjectReference` is used.

## Observable parity

The same focused Studio reports were captured before and after the migration.
Line-by-line comparison is exact:

| Report | Before lines | After lines | Differences |
| --- | ---: | ---: | ---: |
| Mesh deviation | 25 | 25 | 0 |
| Nominal/actual comparison | 31 | 31 | 0 |
| Registration acceptance | 23 | 23 | 0 |

This proves parity for the controlled fixtures and report contracts. It does
not prove physical calibration, metrology accuracy, arbitrary-mesh robustness,
or production performance.

## Verification

- Library-Noah Release build: `0` warnings, `0` errors.
- Library-Noah Smoke: `81/81` pass, including three new Tool cases.
- Studio Release build: `0` warnings, `0` errors.
- Mesh deviation verification: `23/23` pass.
- Nominal/actual comparison verification: `29/29` pass.
- Registration acceptance verification: `20/20` pass.
- Library-Noah package/bridge: package integrity pass and `7/7` bridge pass.
- Code structure: `24/24` pass; decreasing ledger is now `12` migration-debt
  files and `16` reviewed Studio boundaries.
- Fixed R0 package: Wide `1920 x 1040` and Compact `1280 x 760`
  `-ValidateOnly` both pass; no application was launched.

Evidence is retained under:

- `artifacts/current/20260801-noah-nominal-registration-migration/`.

## Boundary and next dependency

Human-owner unaided Wide/Compact R0 remains external and is not replaced by
the automated package validation. `J-12 Multiple-match` remains behind the
decreasing numerical-ownership ledger; any new matching arithmetic must be
implemented in committed Noah first.

The next dependency-ready ownership slice is the height-map inspection and
preparation family: Completeness Grid, height-grid/distribution summaries,
region statistics/reference-grid reconstruction, and then their dependent
dual-surface/height-deviation rules. This keeps the first visible inspection
workflow on the same Tool-only architecture before adding multiple-match
collection behavior.

## Completion record

Status: Complete

Scope: nominal/actual mesh comparison, triangle-distance queries, and rigid
registration-transform diagnostics migrated to public committed Noah Tools;
Studio product contracts and acceptance policy preserved.

Acceptance criteria: exact committed package provenance; no duplicated Studio
arithmetic in the three migrated owners; focused observable parity; package,
bridge, structure, build, and fixed R0 validation all pass.

Verification: Library-Noah `0/0` and `81/81`; Studio `0/0`; focused Runner
`23/23`, `29/29`, and `20/20`; bridge `7/7`; structure `24/24`; exact report
diff `0`; both `-ValidateOnly` modes pass.

Evidence: this document and
`artifacts/current/20260801-noah-nominal-registration-migration/`.

Boundary / next dependency: no UI, metrology, arbitrary-data, or human-usability
claim is included; continue the remaining `12`-file ledger with the height-map
inspection/preparation Tool family before `J-12`.
