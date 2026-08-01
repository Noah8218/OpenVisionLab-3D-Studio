# OpenVisionLab 3D Outlier Filter and Leveling Noah Migration

Date: 2026-08-01

Status: Complete

## Outcome

The active Remove Outlier Pixels and Level Surface calculations now belong to
committed Library-Noah source and are consumed through vendored
`Lib.ThreeD 2.8.2`. Studio no longer contains a second implementation of
either numerical kernel.

This is an ownership migration with preserved product behavior. It does not
change the recipe schema, PropertyGrid parameters, explicit Preview/Publish/
Run contract, source or ROI identity rules, Viewer presentation, or authored
acceptance meaning.

## Included scope

### DeterministicLocalMedianOutlierFilterTool

Library-Noah now owns:

- clipped available-neighbor selection for `3 x 3`, `5 x 5`, and `7 x 7`;
- finite-neighbor filtering and center exclusion;
- deterministic median calculation;
- the strict `absolute deviation > threshold` decision;
- minimum valid-neighbor support;
- a new output array with accepted outliers set to `NaN`;
- deterministic row-major outlier indices.

Studio retains exact C3D source/unit/scalar validation, authored parameter
parsing, mask hashing, derived-C3D identity and provenance, metrics, overlays,
and explicit lifecycle orchestration.

### LevelSurfaceTool

Library-Noah now owns:

- finite reference-cell collection from one or more source-neutral grid
  rectangles;
- de-duplication of overlapping reference cells while retaining per-region
  valid counts;
- the existing least-squares height-plane fit;
- raw-height residual RMS and peak-to-valley statistics;
- reference mean calculation;
- full-grid detrending to the reference mean;
- source grid and missing-mask preservation;
- the leveled reference-plane slope evidence.

Studio retains exact source and GridRectangle identity validation, reference
selection IDs, authored maximum-RMS acceptance, immutable
`C3DLevelingTransform` and derived-C3D construction, recipe lifecycle,
metrics, overlays, and Viewer routing. The limit comparison remains separate
from the Noah calculation, so a failed authored RMS gate still retains the
calculated transform evidence and produces no output C3D.

## Package provenance

| Item | Value |
| --- | --- |
| Noah worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Noah branch | `codex/surface-match-kernel` |
| Noah source commit | `3a2cbf8e7195d6f251dcafe6a9343b795d53fe79` |
| Package | `Lib.ThreeD 2.8.2` |
| Target | `netstandard2.0` |
| Vendored package | `third_party/LibraryNoah/Lib.ThreeD.2.8.2.nupkg` |
| SHA-256 | `EF397381CDD3344E3BAB7A7F29FF6124451DA6A1FCB1BC007B0BFDB284A0BFD7` |

The Noah worktree was clean after the commit. Packaging used the exact
committed source and recorded the same commit in NuGet repository metadata.
Studio does not use a cross-repository `ProjectReference`.

## Exact behavior parity

The focused Runner reports were captured before editing and again after the
vendored-package migration. The evidence-directory path line was excluded
because `before` and `after` are intentionally different directories; every
other line was compared exactly.

| Contract | Before | After | Comparable diff |
| --- | ---: | ---: | ---: |
| Remove Outlier Pixels golden | `9/9` | `9/9` | `0 / 14` lines |
| Level Surface golden | `9/9` | `9/9` | `0 / 14` lines |

Preserved identifiers include:

- outlier derived C3D SHA-256
  `08C7B173D30C9ADF0B83CCF7D37DF4A1B3C2B8A15A0D312E9BFAB24263C7DF0E`;
- outlier mask SHA-256
  `AE44FA864AD48A1ABF7FEC959137A84962F6E0A8E69D8C53B69F30FF44D3AD3E`;
- leveled C3D SHA-256
  `5BE202FAF610A7291CFD753837B2469A1C10A9F324A8216C4AB0D7CF8CE2A419`;
- leveling transform SHA-256
  `F2E47D4BC0C3CEB7746A5453501430D27D2016726D2F480920656580AA2BA265`;
- fitted slopes `0.7999617440359937` and `-0.39991923740931967`;
- reference RMS `0.014176899896671178` and unchanged RMS-gate failure.

## Verification

- Library-Noah Release build: `0` warnings, `0` errors.
- `Lib.Inspection.Smoke`: `78/78` pass, including three new focused Tool
  cases.
- Vendored package integrity: pass for ID, version, source commit, checksum,
  license entries, and `netstandard2.0` assembly.
- Studio Release build: `0` warnings, `0` errors.
- Studio Library-Noah bridge: `7/7` pass with `2.8.2` identity.
- Remove Outlier Pixels Runner golden: `9/9` pass.
- Level Surface Runner golden: `9/9` pass.
- Remove Outlier Pixels Workbench: `14/14` pass.
- Level Surface Workbench: `17/17` pass.
- Code structure: `23/23` pass; migration ledger is `15` debt and `13`
  reviewed Studio boundaries with no unclassified or expanded owner.
- Refreshed fixed R0 package: Wide and Compact `-ValidateOnly` pass; no
  application launched.

No visible UI or layout changed in this slice, so no UI screenshot matrix was
required. The Workbench verifiers still prove that Preview remains explicit,
Publish reuses the exact Preview without re-running, source state remains
immutable, and parameter Apply only marks the prior Preview stale.

## Evidence

The repository path is a junction to the required physical D-drive test root:

- logical: `artifacts/current/20260801-noah-outlier-leveling-migration/`;
- physical:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-outlier-leveling-migration\`.

The folder contains before/after Runner reports and fixtures, Studio and Noah
build/smoke reports, package verification, Workbench reports, structure
verification, both R0 validation-only reports, the exact package output, and
`parity-summary.txt`.

## Boundary and next dependency

This closure proves deterministic software ownership and parity only. It does
not prove physical calibration, metrology, production performance, or the
product owner's unaided Wide/Compact usability run. Human-owner R0 therefore
remains external and blocked on owner operation/evidence.

The next dependency-ready numerical migration is nominal/actual mesh
comparison and rigid-transform diagnostics. `J-12 Multiple-match result
collection` follows only after the decreasing Noah migration ledger reaches
that planned stage.

## Completion record

Status: Complete

Scope: local-median outlier filtering and height-field Level Surface numerical
ownership moved from Studio to committed, vendored Library-Noah Tools while
preserving the existing product contracts and exact deterministic outputs.

Acceptance criteria: both public sealed Noah Tools exist -> pass; exact Noah
source is committed before packing -> pass; Studio rules contain no duplicate
arithmetic -> pass; focused Runner output/mask/transform evidence is unchanged
-> pass; Workbench lifecycle parity remains intact -> pass; decreasing
migration ledger and no-new-owner guard pass -> pass.

Verification: Noah Release `0/0`, Smoke `78/78`, package integrity pass,
Studio Release `0/0`, bridge `7/7`, Runner `9/9 + 9/9`, Workbench `14/14 +
17/17`, structure `23/23`, and Wide/Compact R0 `-ValidateOnly` pass.

Evidence: this document and
`artifacts/current/20260801-noah-outlier-leveling-migration/`.

Boundary / next dependency: human-owner R0 still requires owner operation;
the next software priority is nominal/actual mesh comparison and rigid-
transform diagnostics in committed Library-Noah source.
