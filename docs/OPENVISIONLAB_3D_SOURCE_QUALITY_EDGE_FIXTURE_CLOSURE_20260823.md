# OpenVisionLab 3D SourceQualityReport Edge Fixture Closure

Date: 2026-08-23

Backlog item: `M-09`

Proofline issue: `PL-0040`

Status: Complete

## Scope

The existing Runner-owned `--verify-source-quality-report` command now owns
the missing finite-value and malformed-C3D regression matrix. This slice adds
no production validation abstraction, schema change, UI, recipe behavior, or
new test framework.

## Acceptance Evidence

- The pre-change command passed its existing `13/13` baseline.
- The current command passes `18/18`.
- Signed finite heights `[-4, -1, 2, 5]` retain exact valid/missing ratios,
  minimum `-4`, maximum `5`, mean `0.5`, and two-bin distribution `[2, 2]`.
- Existing zero and non-finite missing-value, all-missing, mask, channel,
  distribution, serialization, and invalid-bin cases remain passing.
- An incomplete header, zero width, and declared 2 x 2 payload with only three
  cells each fail with `InvalidDataException`.
- `Int32.MaxValue x Int32.MaxValue` dimensions fail with `OverflowException`
  before allocation or report construction.
- Every malformed `.c3d` fixture is written beside the D-backed report and
  deleted in `finally`; the evidence root contained zero transient C3D files
  after verification.
- `.github/workflows/ci.yml` runs the same existing command after build and
  rejects a nonzero exit or a report without
  `SourceQualityReportVerification|Pass|cases=18|passed=18|failed=0`.

## Verification

All local outputs are physically under:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0040-source-quality-edge-fixtures
```

Executed checks:

```text
Focused Runner Release restore/build: Pass, 0 warnings / 0 errors
SourceQualityReport baseline: Pass, 13/13
SourceQualityReport current final: Pass, 18/18
Transient C3D cleanup: Pass, 0 files remain
Release solution build: Pass, 15 projects, 0 warnings / 0 errors
Standard MTP facade: Pass, 2/2
NuGet package health: Pass, 15 projects, vulnerable 0 / deprecated 0
Code structure: Pass, 68/68
Vision SDK package boundary: Pass
```

One final focused `--no-restore` build against a new empty artifacts path first
failed with `NETSDK1004` because that path had no `project.assets.json`. After
running the required restore into the same D-backed path, the focused Release
build passed with zero warnings and zero errors. This setup retry is retained
in `focused-final-build.log` and `focused-final-restore.log`.

Primary reusable evidence:

- `source-quality-edge-fixtures-final.txt`
- `source-quality-report.json`
- `standard-tests/standard-verifier-facade.trx`
- `nuget-package-health.txt`
- `code-structure-report.txt`
- `vision-sdk-package-report.txt`

## Durable Closure

Status: Complete

Scope: `M-09` finite/missing and malformed C3D SourceQualityReport fixtures on
the existing Runner verifier, plus hosted-workflow completeness enforcement.

Acceptance criteria: finite/missing semantics -> pass `18/18`; malformed
topology rejection -> pass with exact exception types; D-backed generation and
cleanup -> pass with zero transient C3D files; CI source gate -> present and
requires the exact complete marker; proportional repository checks -> pass.

Verification: focused Release build and verifier, full Release solution build,
standard test facade, NuGet health, structure guard, and fixed Vision SDK
package guard all passed from the current working tree.

Evidence: the D-backed evidence root above and `.proofline/issues/PL-0040.json`.

Boundary / next dependency: this is local verifier and CI-source evidence; it
does not claim a hosted CI run, product UI behavior, maximum-C3D performance,
physical metrology, human-owner R0, release qualification, or publication.
