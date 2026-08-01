# OpenVisionLab 3D Surface Match Library-Noah Migration

Date: 2026-08-01

Status: Complete

## Outcome

The deterministic Surface Match pose-search and one-way unique-nearest
coverage arithmetic now belongs to `Library-Noah` and is consumed by Studio
through the committed, vendored `Lib.ThreeD 2.8.0` package. Studio retains
product identity, unit/frame validation, recipe policy, acceptance,
Preview/Publish/Run orchestration, evidence composition, and UI.

This closes the numerical-ownership prerequisite recorded after `K-10`. It
does not add a new matching feature or change the operator workflow.

## Exact source and package

| Item | Value |
| --- | --- |
| Noah source worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Noah branch | `codex/surface-match-kernel` |
| Noah commit | `7d1ad8721ca7aed9efa2a17beaa36409d7dbd718` |
| Commit subject | `feat: add deterministic surface matching kernel` |
| Package | `Lib.ThreeD 2.8.0` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.0.nupkg` |
| SHA-256 | `7378C02ABDED9C02F1448CDF80577B00A7AD99E78BC2B722E341DD7513CE754C` |

The package was built after the Noah commit, carries that exact commit in its
repository metadata, and matches the restored NuGet global-cache package byte
for byte. The unrelated dirty `C:\Git\Library-Noah` checkout was inspected
read-only and was not changed.

## Responsibility boundary

Library-Noah owns:

- finite ordered XYZ sample validation needed by the numerical kernel;
- deterministic Euler/translation candidate generation and ordering;
- centroid-based translation for each rotation candidate;
- bounded translation and candidate-budget rejection;
- one-way model-to-scene unique-nearest correspondence selection;
- matched count, coverage, RMSE, correspondence distances, and raw pose;
- deterministic best-candidate ranking.

Studio owns:

- `SurfaceModel` and `PreparedScene` identities and source lineage;
- unit, frame, source, artifact, and authored-parameter validation;
- strict conversion to/from Noah source-neutral contracts;
- canonical Studio artifacts, hashes, timing reports, and persistence;
- acceptance thresholds and typed Pass/Fail/Rejected interpretation;
- explicit Preview, Publish, Run, and Validation lifecycle;
- Viewer overlays and evidence presentation.

`RigidSurfacePoseSearch` and `SurfaceCoverageScorer` remain public compatibility
entry points, but contain validation and adapter code only. The structure gate
rejects reintroduction of their former rotation, centroid, distance, or
claimed-scene arithmetic.

## Observable-behavior preservation

The controlled known-pose result remained byte-identical before and after the
migration:

```text
known-pose-result.json SHA-256
4D214BA3684162407332A69D95155C7FF7D780CC7C8B277795DB028619408B5F
```

The fixture still recovers the documented `30 degree` yaw and
`(10, -4, 2)` translation. Runner and Workbench retain exact pose, coverage,
assessment, overlay, and execution identities. Acceptance remains separate
from raw matching evidence, and surface and diagnostic edge scores remain
separate.

## Verification

Primary commands actually run:

```powershell
dotnet build Lib.Common.sln -c Release -m:1
dotnet run --project Lib.Inspection.Smoke/Lib.Inspection.Smoke.csproj -c Release --no-build
dotnet pack Lib.ThreeD/Lib.ThreeD.csproj -c Release --no-build -p:PackageVersion=2.8.0 -p:RepositoryCommit=7d1ad8721ca7aed9efa2a17beaa36409d7dbd718
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU" -m:1 -t:Rebuild
python scripts/verify-nuget-package-health.py --solution OpenVisionLab.ThreeDStudio.slnx --report <D-drive-report> --json-directory <D-drive-json>
powershell -File scripts/verify-library-noah-package.ps1 -ReportPath <D-drive-report>
powershell -File scripts/verify-code-structure.ps1 -ReportPath <D-drive-report>
powershell -File scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
powershell -File scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
```

Focused Runner and Shell verification commands produced the reports named in
the evidence section below.

Library-Noah:

- committed-source Release build: `0` warnings, `0` errors;
- full `Lib.Inspection.Smoke`: `69/69`;
- new kernel cases cover known pose, `4/5` occlusion, no-match translation,
  and candidate-budget rejection;
- packed package ID/version/target/commit/license metadata verified.

Studio:

- Release Rebuild: `0` warnings, `0` errors;
- vendored package metadata/hash: Pass;
- restored NuGet cache hash equals vendored hash: Pass;
- NuGet health: `12` projects, `0` vulnerable, `0` deprecated;
- Studio Library-Noah bridge/package behavior: `7/7`;
- Surface matching foundation: `34/34`;
- Surface Match acceptance: `14/14`;
- Surface-edge matching: `21/21`;
- Surface-edge diagnostics/review: `20/20`;
- SurfaceModel regression: `22/22`;
- fixed performance matrix: `18/18`;
- Workbench/Runner parity: `23/23`;
- Inspection Workspace: `63/63`;
- Workbench docking: `76/76`;
- Validation Set: `84/84`;
- code structure: `18/18`;
- refreshed human-owner R0 package: Wide and Compact `-ValidateOnly` Pass.

The observed fixed-machine timing after migration was
`5.086/7.740/8.106 ms` median/p95/max for the bounded 11-candidate profile and
`17.262/21.982/23.942 ms` for the broad 61-candidate profile. These values are
observational regression evidence only; they are not a cross-hardware or
production-throughput claim.

The first R0 validation attempt correctly failed because an incremental build
had left the Shell EXE timestamp older than the newest source. A full Release
Rebuild refreshed the binary set; hashes were recalculated rather than the
stale-source guard being bypassed, and both validation-only modes then passed.

## UI and benchmark boundary

This slice changes no UI, UX, layout, visible text, docking, theme, or
responsive behavior. New before/after screenshots are therefore not
applicable. Existing GoPxL-derived principles remain workflow guidance only;
no competitor theme, assets, proportions, topology, or naming were copied.

## Evidence

Logical repository path:

- `artifacts/current/20260801-surface-match-noah-migration/`

Physical test-artifact path:

- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-surface-match-noah-migration\`

The repository path is a verified directory junction to the D-drive location.
`noah/` contains committed-source build, smoke, pack, and package evidence;
`studio/` contains package, matching, acceptance, edge, performance, parity,
workspace, docking, validation, NuGet, structure, and R0 reports.

## Completion record

Status: Complete

Scope: Existing Surface Match pose-search and unique-nearest coverage
arithmetic migrated to committed `Library-Noah`; exact package vendored and
consumed through strict Studio adapters.

Acceptance criteria: Noah is the numerical owner -> Pass; exact source commit
is built, smoked, packed, and recorded -> Pass; vendored package/checksum and
restore cache agree -> Pass; Studio observable artifacts and
Runner/Workbench parity are preserved -> Pass; R0 fixed binary package is
refreshed -> Pass.

Verification: Noah Release `0/0` and Smoke `69/69`; Studio Release Rebuild
`0/0`; package/bridge `7/7`; matching `34/34`; acceptance `14/14`; edge
`21/21`; edge review `20/20`; SurfaceModel `22/22`; performance `18/18`;
parity `23/23`; Inspection Workspace `63/63`; docking `76/76`; Validation Set
`84/84`; structure `18/18`; NuGet `12/0/0`; Wide/Compact R0
`-ValidateOnly` Pass.

Evidence: this document and
`artifacts/current/20260801-surface-match-noah-migration/`.

Boundary / next dependency: Human-owner unaided Wide/Compact R0 remains
external for `A-01` and human-usability acceptance. `J-12 Multiple-match
result collection` is the next dependency-ready matching slice. It must extend
the committed Noah package for any new matching arithmetic before Studio adds
typed collection, identity, persistence, evidence, and UI orchestration. This
closure does not prove arbitrary convergence, symmetry handling, physical
calibration, metrology, cross-hardware performance, or production readiness.
