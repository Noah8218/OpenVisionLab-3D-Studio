# Assimp miniz Provenance - 2026-07-16

## Decision

The single compiler-read `miniz` input in the fixed Open3D/Assimp candidate passes a bounded source chain from public `kuba--/zip v0.3.1`, through the reviewed Assimp pull request and merge/current changes, to the clean-build source.

This is not independent provenance for the original `richgel999/miniz` source. The public Kuba README describes its zip library as layered over the miniz `v3.0.2` API, but the source identity verified here starts at the public Kuba `v0.3.1` tag and does not collapse that relationship into an unproven upstream byte-identity claim.

The separate official-history boundary audit records exactly what is known about that remaining claim: neither the Kuba baseline nor the actual build raw blob appears in the current reachable object set of a full, non-shallow official `richgel999/miniz` clone, but that non-observation does not prove that a historical relationship never existed. Its outcome is deliberately `Unresolved`. See `docs/OPENVISIONLAB_3D_ASSIMP_MINIZ_ORIGIN_BOUNDARY_20260716.md`.

## Fixed Inputs

| Item | Fixed value |
| --- | --- |
| Kuba remote | `https://github.com/kuba--/zip.git` |
| Kuba tag | `v0.3.1` |
| Kuba tag revision | `550905d883b29f0b23e433fdb97f6299b628d4a9` |
| Assimp remote | `https://github.com/assimp/assimp.git` |
| Assimp PR | `#5499` / `refs/remotes/origin/pr-5499` |
| Assimp PR head | `afef86519689ce64c992610e5ae3b76fdf222edf` |
| Assimp merge | `83d7216726726a07e9e40f86cc2322b22fec11fa` |
| Assimp current tag | `v5.4.2` / `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Actual build input | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/zip/src/miniz.h` |
| Compiler-read closure | `contrib/zip/src/miniz.h` only |
| Closure file-set SHA-256 | `ccc0dd6eef59502e6d9aa1774b21ce1e2038d8448baf6d28bdf878d868a5a67c` |

The pull-request ref is required because the merge commit retains the same final `miniz.h` content but does not retain the PR head as a Git ancestor. The verification therefore proves content identity across that boundary, not branch ancestry.

## Source and Delta Chain

| Step | Revision | `miniz.h` Git blob | Evidence |
| --- | --- | --- | --- |
| Kuba baseline | `v0.3.1` / `550905d...` | `cd86483184cfba1dd33c4db7c718965e20926c7a` | Public fixed tag |
| Assimp PR baseline | `7d4a5c7af3951557717c0bbc9630f67e5eeb28e9` | `cd86483184cfba1dd33c4db7c718965e20926c7a` | Exact content identity with Kuba baseline |
| PR macro update | `3ff60401a7172514ec026f6746b55ce766ad8433` | `ee9a2899bda9d3fee94fffb308b40c09af1fa36b` | `2/0` lines from PR baseline |
| PR header update | `8dac9e7581f3baf8cb710432ebf69e1257a776aa` | `f3b3456bdb93809f6b5b0b3ebba0e7e3f5a24a19` | `4/1` lines from macro update |
| PR head and merge | `afef865...` / `83d721...` | `f3b3456bdb93809f6b5b0b3ebba0e7e3f5a24a19` | Zero content delta across PR-head-to-merge boundary |
| Post-merge change/current tag/build | `0d546b3...` / `v5.4.2` | `ad5850ce17d9449cc9356486dff73c8a566e1c46` | `1/1` line change; tag and clean build match |

The clean-build input has SHA-256 `bba5c196415bda01b460d2dd8be779189bb228c2802b5b61dd20eae5c3921b06`.

## Repeatable Verification

The Assimp clone must contain the public PR ref before the verifier runs.

```powershell
git clone https://github.com/kuba--/zip.git `
  artifacts/dependency-candidates/assimp-kubazip-provenance-20260716/upstream-kuba-zip

git clone https://github.com/assimp/assimp.git `
  artifacts/dependency-candidates/assimp-miniz-intake-20260716/official-assimp-full

git -C artifacts/dependency-candidates/assimp-miniz-intake-20260716/official-assimp-full `
  fetch origin refs/pull/5499/head:refs/remotes/origin/pr-5499

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-miniz-provenance.ps1 `
  -ReportPath artifacts/dependency-candidates/assimp-miniz-provenance-20260716/miniz-provenance.json
```

The positive report must contain:

```text
AssimpMinizProvenance|Pass|kuba=550905d883b29f0b23e433fdb97f6299b628d4a9|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|blobs=10|issues=0
```

Use an incorrect expected Kuba revision to check fail-closed behavior:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-miniz-provenance.ps1 `
  -ReportPath artifacts/dependency-candidates/assimp-miniz-provenance-20260716/miniz-provenance-negative.json `
  -ExpectedKubaRevision 0000000000000000000000000000000000000000
```

This command must return exit code `1` and record the revision mismatch.

## Claim Boundary

This evidence proves:

- exact `kuba--/zip v0.3.1` to Assimp PR baseline content identity for the compiler-read input;
- the fixed Assimp PR and post-merge deltas to the current `v5.4.2` and clean-build input;
- the actual CMake source group and one-file compiler-read closure; and
- a fail-closed check for an incorrect Kuba revision.

It does not prove:

- independent original `richgel999/miniz` source identity, release, complete history, or ownership;
- the absence of a historical relationship merely because current official reachable refs do not contain either fixed raw blob;
- PR-head Git ancestry to the Assimp merge commit;
- third-party notice completeness or license/legal disposition;
- binary reproducibility or runtime behavior; or
- Open3D product integration, Viewer/Runner parity, redistribution, or owner/legal approval.

Keep this supply-chain result separate from Viewer, inspection, and physical-metrology reliability claims.
