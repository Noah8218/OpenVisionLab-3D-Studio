# Assimp RapidJSON Provenance

Date: 2026-07-16

## Purpose

This document records a bounded compiler-input provenance check for the
RapidJSON headers used by the fixed Open3D `0.19.0` clean Release build through
Assimp `v5.4.2`. It is evidence for the fixed source/delta/current-build slice
only. It is not a binary-reproducibility, notice, redistribution,
product-integration, or Viewer/Runner-parity claim.

## Fixed Inputs

| Item | Fixed value |
| --- | --- |
| Official upstream | `https://github.com/Tencent/rapidjson.git` |
| Latest release tag present at intake | `v1.1.0` / `f54b0e47a08782a6131cc3d60f94d038fa6e0a51` |
| Actual public source snapshot | `676d99db96e2108724e62342a47e28c8e991ed3b` |
| Snapshot date and subject | 2024-03-08, `fix Visual Studio 2022 ... C5232` |
| Assimp upstream | `https://github.com/assimp/assimp.git` |
| Assimp import boundary | `4a3e0e46ac45867c8c8fac9cbcdee3bc30e99f92` (`#5501`) |
| Assimp fixed tag | `v5.4.2` / `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Clean build source | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/rapidjson/include` |
| Closure component | `rapidjson`, `29` headers, SHA-256 `f9f5aec8c411fb5af185f0a148d1615539ca9837f3a05fa992df0cef315006fb` |

The `v1.1.0` tag is an ancestor of the actual source snapshot, but it is not
the source-identity claim. The official RapidJSON repository has no release tag
at `676d99...`; therefore this evidence records that exact public post-tag
commit rather than incorrectly describing the current Assimp headers as an
unmodified `v1.1.0` release.

## Checked Source Set

The fixed header-only closure contains 29 paths beneath `rapidjson/`, including
document/parser/writer/schema headers, error headers, and internal support
headers. The verifier checks every path at four points:

1. Official RapidJSON commit `676d99...`.
2. Assimp merge/update commit `4a3e0e...`.
3. Assimp `v5.4.2`.
4. The fixed clean-build source directory.

All 29 Git blobs and exported/build-file hashes agree. The Assimp update changes
16 RapidJSON header paths relative to its first parent, and no checked path
changes after the update through `v5.4.2`.

Assimp's CMake code adds `../contrib/rapidjson/include`, defines
`RAPIDJSON_HAS_STDSTRING=1`, declares the
`ASSIMP_RAPIDJSON_NO_MEMBER_ITERATOR` option, and defines
`RAPIDJSON_NOMEMBERITERATORCLASS` when that option is enabled. The verifier
checks those source-use contract markers alongside the existing closure report.

## Reproduction

Run from the repository root with a previously unused working directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-rapidjson-provenance.ps1 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-rapidjson-provenance-20260716\manual-work `
  -ReportPath artifacts\dependency-candidates\assimp-rapidjson-provenance-20260716\manual-report.json
```

Expected positive output:

```text
AssimpRapidJsonProvenance|Pass|rapidjson=676d99db96e2108724e62342a47e28c8e991ed3b|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=29|history=29|issues=0
```

The post-fix local report is
`artifacts/dependency-candidates/assimp-rapidjson-provenance-20260716/rapidjson-provenance-rerun.json`.

## Fail-Closed Check

Passing an incorrect expected baseline revision records a failure and returns
exit code `1`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-rapidjson-provenance.ps1 `
  -ExpectedRapidJsonBaselineRevision 0000000000000000000000000000000000000000 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-rapidjson-provenance-20260716\negative-work `
  -ReportPath artifacts\dependency-candidates\assimp-rapidjson-provenance-20260716\negative-report.json
```

The controlled local negative run returned `1` with one recorded issue.

## Claim Boundary

This evidence establishes all of the following for this fixed build:

- The public `v1.1.0` tag and the separate post-tag public snapshot resolve to
  the recorded commits, with the tag an ancestor of the snapshot.
- All 29 fixed headers are byte-identical at the public snapshot, Assimp import,
  Assimp `v5.4.2`, and clean-build source locations.
- Each fixed header has no post-import Assimp history through `v5.4.2`.
- Assimp's include path, relevant definitions, and the compiler-read closure
  agree on the 29-header subset.

It does not establish an official release tag for the post-tag snapshot,
upstream release-signature or ownership identity, complete RapidJSON-history
provenance, third-party notice disposition, byte-identical binary reproduction,
redistribution approval, product adoption, inspection metric correctness, or
physical metrology accuracy.
