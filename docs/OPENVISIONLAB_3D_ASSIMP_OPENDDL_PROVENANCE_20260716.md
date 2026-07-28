# Assimp OpenDDL Parser Provenance - 2026-07-16

## Decision

All `13` compiler-read OpenDDL Parser inputs in the fixed Open3D/Assimp candidate are source-identical to public `kimkulling/openddl-parser v0.5.1`, through the fixed Assimp baseline, then the two documented Assimp changes, current `v5.4.2`, and the clean-build source.

The static `0.4.0` string read by the vendored CMake file is present in the upstream `v0.5.1` source too. It is metadata inside `OpenDDLParser.cpp`, not the authoritative source-tag identity.

## Fixed Inputs

| Item | Fixed value |
| --- | --- |
| OpenDDL Parser remote | `https://github.com/kimkulling/openddl-parser.git` |
| Upstream tag | `v0.5.1` |
| Upstream tag revision | `ffad343385f550b933c7e498e9bd0a861605102c` |
| Assimp remote | `https://github.com/assimp/assimp.git` |
| Assimp baseline | `bc7ef58b4947a01f4f7163b47b96ca273473d7eb` (`bump openddl-parser to v0.5.1`) |
| Assimp common-header change | `7cbf4c4136bf9884fad408e6e388b10ba3ace635` |
| Assimp parser change | `081cae6a950204ced52f5ca09b78fe7446286967` |
| Assimp current tag | `v5.4.2` / `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Actual build-source directory | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/openddlparser` |
| Compiler-read closure | `13` files / SHA-256 `85156d0fafb1ade930ed3e330e69841a2e68aa48bfb29f46e604fe290bc6e3f1` |

## Source and Delta Chain

The upstream tag and Assimp baseline match in all `13/13` compiler-read blobs. Eleven files remain identical through Assimp `v5.4.2` and the clean build. Only two fixed Assimp changes remain:

| File | Assimp change | Delta from v0.5.1 baseline | Current/build blob |
| --- | --- | --- | --- |
| `include/openddlparser/OpenDDLCommon.h` | `7cbf4c4136bf9884fad408e6e388b10ba3ace635` | `12/15` | `4b92d1406f353917788bc76fcef0c3fbde62eb3e` |
| `code/OpenDDLParser.cpp` | `081cae6a950204ced52f5ca09b78fe7446286967` | `3/1` | `3d7dce45ec5267687afbe4d502cfe5033f57046b` |

The version marker is `0.4.0` at the upstream tag, Assimp baseline, and current source. Do not treat it as a contradiction to, or replacement for, the fixed upstream `v0.5.1` tag.

## Repeatable Verification

```powershell
git clone https://github.com/kimkulling/openddl-parser.git `
  artifacts/dependency-candidates/assimp-openddl-intake-20260716/official-openddl-parser

git clone https://github.com/assimp/assimp.git `
  artifacts/dependency-candidates/assimp-miniz-intake-20260716/official-assimp-full

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-openddl-provenance.ps1 `
  -ReportPath artifacts/dependency-candidates/assimp-openddl-provenance-20260716/openddl-provenance.json
```

The positive report must contain:

```text
AssimpOpenDDLProvenance|Pass|upstream=ffad343385f550b933c7e498e9bd0a861605102c|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=13|deltas=2|issues=0
```

Use an incorrect upstream revision to check fail-closed behavior:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-openddl-provenance.ps1 `
  -ReportPath artifacts/dependency-candidates/assimp-openddl-provenance-20260716/openddl-provenance-negative.json `
  -ExpectedUpstreamRevision 0000000000000000000000000000000000000000
```

This command must return exit code `1` and record the upstream revision mismatch.

## Claim Boundary

This evidence proves:

- exact upstream `v0.5.1` to Assimp baseline identity for every compiler-read input;
- the two fixed Assimp source deltas to the current tag and clean-build inputs;
- the current CMake declarations and 13-file compiler-read closure; and
- a fail-closed check for an incorrect upstream revision.

It does not prove:

- upstream release signature, complete upstream history, or maintainer identity;
- third-party notice completeness or license/legal disposition;
- binary reproducibility or runtime behavior; or
- Open3D product integration, Viewer/Runner parity, redistribution, or owner/legal approval.

Keep this supply-chain result separate from Viewer, inspection, and physical-metrology reliability claims.
