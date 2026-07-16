# Assimp Source Snapshot Identity - 2026-07-16

## Scope And Decision

This is a supply-chain evidence checkpoint for the separate-process Open3D registration candidate. It does not add Assimp or Open3D to the product, approve redistribution, prove a binary is reproducible, or change Viewer behavior.

**Decision:** the fixed clean Open3D Release build's `ext_assimp` source directory is an exact content match for the official Assimp `v5.4.2` ZIP selected by the generated CMake ExternalProject metadata. This closes only the archive-to-build-source snapshot link.

It does **not** establish that each `contrib` directory is an unmodified copy of its independent upstream repository, recover each vendored component's original commit, approve notices, or close Open3D distribution.

## Fixed Inputs

| Input | Value |
| --- | --- |
| Official archive URL selected by CMake | `https://github.com/assimp/assimp/archive/refs/tags/v5.4.2.zip` |
| Archive path | `artifacts\dependency-candidates\open3d-license-audit-0.19.0\source\Open3D-0.19.0\3rdparty_downloads\assimp\v5.4.2.zip` |
| Archive bytes | `55,769,830` |
| Archive SHA-256 | `03e38d123f6bf19a48658d197fd09c9a69db88c076b56a476ab2da9f5eb87dcc` |
| Build source directory | `artifacts\o3d-clean\b\assimp\src\ext_assimp` |
| CMake URL metadata | `artifacts\o3d-clean\b\assimp\src\ext_assimp-stamp\ext_assimp-urlinfo.txt` |
| CMake update / patch commands | Empty in `ext_assimp-update-info.txt` and `ext_assimp-patch-info.txt` |

The generated download script verifies the fixed SHA-256 before extraction. The generated extraction script removes the destination, extracts the one archive root, and renames that root to `ext_assimp`; no update or patch command was configured afterward.

## Verification Method

`scripts\verify-assimp-source-snapshot.ps1` performs the following checks without extracting or changing the source tree:

1. Validate the archive SHA-256 against the fixed expected value.
2. Open the ZIP and require exactly one root directory, no backslash paths, no duplicate paths, and no empty, `.` or `..` path segments.
3. Compare every ZIP file to the build source with ordinal path matching, uncompressed length, and per-file SHA-256.
4. Emit canonical ordered content-manifest SHA-256 values for both trees and require equality.
5. Return exit code `1` with a JSON report for any source, archive, path, hash, count, or content mismatch.

The output report contains at most the requested number of difference examples while retaining full mismatch counts.

## Current Result

| Check | Result |
| --- | --- |
| Archive files | `2,940` |
| Build source files | `2,940` |
| Missing files | `0` |
| Extra files | `0` |
| Modified files | `0` |
| Canonical content-manifest SHA-256, archive and source | `faddad7cde8ae1956c96616e6fecaf2151feef9fa3159712f497f3ff3dae5eb7` |
| Result | Pass |

The current report is ignored local evidence at `artifacts\dependency-candidates\assimp-source-snapshot-20260716\assimp-source-snapshot.json`. It records matching canonical content-manifest hashes for the archive and build source.

A separate forward-slash ZIP fixture with the same one-file path but different archive/source content returned exit code `1`, `Status=Fail`, and `ModifiedFileCount=1`. This verifies the content-mismatch rejection path without changing the actual Assimp archive or build source.

## Reproduction Checklist

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-source-snapshot.ps1 `
  -ReportPath artifacts\dependency-candidates\assimp-source-snapshot-20260716\assimp-source-snapshot.json
```

- [ ] Confirm the archive path and SHA-256 match the fixed inputs above.
- [ ] Run the verifier against the exact clean-build `ext_assimp` directory.
- [ ] Require `Status=Pass`, `2940/2940` files, and zero missing, extra, and modified paths.
- [ ] Preserve the JSON report with the related clean-build evidence.
- [ ] Do not promote this checkpoint to individual vendored-component revision, modification-delta, binary-reproducibility, or distribution evidence.

## Claim Boundary And Remaining Work

The completed Assimp compiler/object/runtime closure and this snapshot identity check establish what source content the fixed build used. The unresolved work is narrower but still material for distribution:

1. Independently map each remaining compiler-read vendored component to its upstream revision and document any Assimp-side delta. Poly2Tri fixed original-source content and current path-history identity now pass in `OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md` and `OPENVISIONLAB_3D_ASSIMP_POLY2TRI_DELTA_ATTRIBUTION_20260716.md`; Git-mirror ownership/signature and final-line blame remain out of scope. Start the next smallest component with Clipper `6.4.2`.
2. Resolve BoringSSL current-toolchain reproduction after explicit owner approval to install its required `perl`, `go`, and `nasm` tools.
3. Complete final third-party notice, REDIST, owner/legal, product-integration, real result-mapping, and Viewer/Runner parity gates.

See `docs\OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md` and `docs\OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md` for the broader blocked distribution decision.

## Sources

- Assimp `v5.4.2` tag: https://github.com/assimp/assimp/tree/v5.4.2
- Assimp `v5.4.2` archive selected by the CMake ExternalProject: https://github.com/assimp/assimp/archive/refs/tags/v5.4.2.zip
- Open3D `v0.19.0` dependency build logic: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/find_dependencies.cmake
