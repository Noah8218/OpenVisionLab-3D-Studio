# Assimp kuba--zip Provenance (2026-07-16)

## Decision

The three CMake compiler inputs for Assimp's vendored `kuba--/zip` pass a fixed-source provenance check from the public [kuba--/zip repository](https://github.com/kuba--/zip) through Assimp `v5.4.2` to the clean Open3D build input.

This is not upstream-only byte identity. The accepted basis is official [kuba--/zip `v0.3.1`](https://github.com/kuba--/zip/tree/v0.3.1), followed by a bounded, explicit Assimp delta. The local CMake metadata reads `project(zip VERSION "0.3.0")`; it is build metadata, not sufficient evidence of the source revision and must not be used alone to claim the upstream source identity.

No product dependency, PCD loader, registration adapter, Viewer behavior, or release bundle is changed by this evidence.

## Scope

The build's `contrib/zip/CMakeLists.txt` declares exactly these compiler inputs:

```text
src/miniz.h
src/zip.h
src/zip.c
```

The verifier checks only those three inputs. It does not generalize to other Assimp `contrib` directories, `miniz` as an independently sourced component, notices, binary reproducibility, or distribution approval.

## Fixed Chain

| Stage | Fixed identity |
| --- | --- |
| Upstream remote | `https://github.com/kuba--/zip.git` |
| Upstream baseline | `v0.3.1` -> `550905d883b29f0b23e433fdb97f6299b628d4a9` |
| Assimp remote | `https://github.com/assimp/assimp.git` |
| Assimp import | [`83d7216726726a07e9e40f86cc2322b22fec11fa`](https://github.com/assimp/assimp/commit/83d7216726726a07e9e40f86cc2322b22fec11fa), `updated zip (#5499)` |
| Assimp current tag | [`v5.4.2`](https://github.com/assimp/assimp/tree/v5.4.2) -> `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Build input | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/zip/src` |

| File | kuba--/zip `v0.3.1` blob | Assimp import blob | Assimp `v5.4.2` and build blob |
| --- | --- | --- | --- |
| `miniz.h` | `cd86483184cfba1dd33c4db7c718965e20926c7a` | `f3b3456bdb93809f6b5b0b3ebba0e7e3f5a24a19` | `ad5850ce17d9449cc9356486dff73c8a566e1c46` |
| `zip.c` | `a35f86e34216003266f0497b108b5adfb23ef114` | `5b8955dba3ee65c52915fc4fe636d757200db124` | `deef56178b9139869e559cd2bb9d3d4182a0a8c4` |
| `zip.h` | `324904ca6c8d803c50f0365394a928fbddfba5b8` | `324904ca6c8d803c50f0365394a928fbddfba5b8` | `324904ca6c8d803c50f0365394a928fbddfba5b8` |

## Bounded Delta

The upstream checkout has CRLF representation for some text files while Assimp's source representation is LF. Git tree blobs remain the fixed source identity. The verifier converts only CRLF pairs to LF before calculating line deltas, so end-of-line representation cannot appear as a source change.

| File | `v0.3.1` -> Assimp import | Assimp import -> `v5.4.2` |
| --- | ---: | ---: |
| `miniz.h` | `6` additions / `1` deletion | `1` addition / `1` deletion |
| `zip.c` | `2` additions / `1` deletion | `7` additions / `3` deletions |
| `zip.h` | `0` additions / `0` deletions | `0` additions / `0` deletions |

The post-import Assimp path history is bounded to these commits:

| Commit | File | Delta |
| --- | --- | ---: |
| [`0d546b3d2edb5ae737c11971b26233f5a5316a43`](https://github.com/assimp/assimp/commit/0d546b3d2edb5ae737c11971b26233f5a5316a43) | `miniz.h` | `1/1` |
| [`8231d99a8547574bd9bc984c7c15702d71cf77e4`](https://github.com/assimp/assimp/commit/8231d99a8547574bd9bc984c7c15702d71cf77e4) | `zip.c` | `1/1` |
| [`dd1474e2801c6e0261e8a2d88fbe189789f76d14`](https://github.com/assimp/assimp/commit/dd1474e2801c6e0261e8a2d88fbe189789f76d14) | `zip.c` | `6/2` |

No post-import `zip.h` commit occurs before `v5.4.2`.

## Repeatable Verification

The ignored evidence directory must contain two public-origin clones with their `origin` URLs unchanged:

```text
artifacts/dependency-candidates/assimp-kubazip-provenance-20260716/upstream-kuba-zip
artifacts/dependency-candidates/assimp-kubazip-provenance-20260716/official-assimp
```

Use a fresh ignored working directory for every replay. The verifier refuses to reuse an existing directory.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-kubazip-provenance.ps1 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\kubazip-provenance-replay `
  -ReportPath artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\kubazip-provenance-replay.json
```

Accepted local replay:

```text
AssimpKubaZipProvenance|Pass|kuba=550905d883b29f0b23e433fdb97f6299b628d4a9|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=3|deltas=3|issues=0
```

Controlled fail-closed check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-kubazip-provenance.ps1 `
  -ExpectedKubaRevision 0000000000000000000000000000000000000000 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\kubazip-provenance-negative `
  -ReportPath artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\kubazip-provenance-negative.json
```

The controlled command exits `1` and records the Kuba tag revision mismatch.

## Claim Boundary

Passed:

- Fixed `kuba--/zip v0.3.1` Git tag, bounded Assimp delta, current `v5.4.2` source, CMake inputs, and actual clean-build source identity.
- Explicit post-import path history and line deltas for the three compiler inputs.
- Controlled wrong-baseline rejection.

Not passed:

- Git signature or owner identity, release-state proof, or complete upstream history.
- Independent upstream provenance for other Assimp components, including a separate `miniz` source audit.
- Notice disposition, binary reproducibility, redistribution approval, Open3D product adoption, or Viewer/Runner registration parity.
