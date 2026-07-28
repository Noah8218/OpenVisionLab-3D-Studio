# Assimp pugixml Provenance - 2026-07-16

## Decision

The three compiler-read `pugixml` inputs in the fixed Open3D/Assimp candidate are traced from the public `zeux/pugixml` `v1.13` tag through the Assimp import and the fixed `v5.4.2` clean-build source.

This is a bounded source and delta result. It does not approve Open3D, Assimp, or pugixml for product distribution.

## Fixed Inputs

| Item | Fixed value |
| --- | --- |
| Official pugixml remote | `https://github.com/zeux/pugixml.git` |
| Upstream tag | `v1.13` |
| Upstream tag commit | `a0e064336317c9347a91224112af9933598714e9` |
| Official Assimp remote | `https://github.com/assimp/assimp.git` |
| Assimp import | `62cefd5b275628ff97a77d0cd9220e1c35794a3f` (`Update pugiXML library`) |
| Assimp current tag | `v5.4.2` / `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Follow-up Assimp change | `01231d0e6001f555c81dcfcc6c581fa5797ccac9` (`Add 2024 to copyright infos`) |
| Actual build-source directory | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/pugixml/src` |

The official release and source repository were checked directly at [pugixml v1.13](https://github.com/zeux/pugixml/releases/tag/v1.13) and [Assimp](https://github.com/assimp/assimp).

## Compiler Input Contract

The fixed build uses `PUGIXML_HEADER_ONLY`:

1. Assimp's `code/CMakeLists.txt` records `pugiconfig.hpp` and `pugixml.hpp` in `Pugixml_SRCS`.
2. The current `pugiconfig.hpp` defines `PUGIXML_HEADER_ONLY`.
3. `pugixml.hpp` then includes `pugixml.cpp` through `PUGIXML_SOURCE`.

The resulting effective source-input set is therefore:

```text
pugiconfig.hpp
pugixml.hpp
pugixml.cpp
```

`contrib/pugixml/CMakeLists.txt` still declares standalone target metadata `VERSION 1.9`. That is stale packaging metadata and is not used as the source-identity claim: the readme, fixed official tag, and source blobs establish `v1.13`. The verifier preserves this distinction so a future metadata or source change fails visibly instead of silently changing the SBOM claim.

## Source And Delta Evidence

| File | v1.13 blob | Assimp import blob | v5.4.2/build blob | v1.13 -> import normalized delta | import -> current normalized delta |
| --- | --- | --- | --- | --- | --- |
| `pugiconfig.hpp` | `88b2f2aee09f4752048fdfb5ecd093fbd55f65cf` | `9bf2efd39dc65020a21e0f7cfb0d1ee504b311b7` | `1a395690311ffd7c118ea16b1039a651e074707a` | `1/1` | `2/2` |
| `pugixml.cpp` | `c63645b67fd59acd53a4cbc725fc99da570b15a0` | `c63645b67fd59acd53a4cbc725fc99da570b15a0` | `6d6bd0edb210a00c63ae5e99de0cdade540fbc64` | `0/0` | `2/2` |
| `pugixml.hpp` | `050df154cc77124dd30cea09ab2ece270c4bd4bc` | `050df154cc77124dd30cea09ab2ece270c4bd4bc` | `fde6a4a862a6fc176a32d2f247b2c9a03fc1fbeb` | `0/0` | `2/2` |

The one normalized `pugiconfig.hpp` import change enables `PUGIXML_HEADER_ONLY`. The later `2/2` change for each file is the recorded 2024 copyright update. Each file has exactly the same one post-import path commit in the fixed range, `01231d0e6001f555c81dcfcc6c581fa5797ccac9`.

The verifier normalizes only CRLF to LF before calculating textual deltas. It retains raw file records and Git blob checks separately, so line-ending representation cannot be mistaken for a source-code modification.

## Repeatable Verification

The two official repositories must be present locally. The existing Assimp clone from the Kuba provenance task is reusable.

```powershell
git clone https://github.com/zeux/pugixml.git `
  artifacts/dependency-candidates/assimp-pugixml-provenance-20260716/official-pugixml

git clone https://github.com/assimp/assimp.git `
  artifacts/dependency-candidates/assimp-kubazip-provenance-20260716/official-assimp

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-pugixml-provenance.ps1 `
  -WorkingDirectory artifacts/dependency-candidates/assimp-pugixml-provenance-20260716/pugixml-provenance-run `
  -ReportPath artifacts/dependency-candidates/assimp-pugixml-provenance-20260716/pugixml-provenance.json
```

The recorded positive run reports:

```text
AssimpPugixmlProvenance|Pass|pugixml=a0e064336317c9347a91224112af9933598714e9|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=3|deltas=3|issues=0
```

Use a new `-WorkingDirectory` for every run. The verifier deliberately rejects an existing working directory to prevent stale artifacts from being reused.

The following controlled negative input must return exit code `1` and report the revision mismatch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-pugixml-provenance.ps1 `
  -WorkingDirectory artifacts/dependency-candidates/assimp-pugixml-provenance-20260716/pugixml-provenance-negative-run `
  -ReportPath artifacts/dependency-candidates/assimp-pugixml-provenance-20260716/pugixml-provenance-negative.json `
  -ExpectedPugixmlRevision 0000000000000000000000000000000000000000
```

## Claim Boundary

This evidence proves:

- fixed `v1.13` source identity for the three files;
- the bounded, CRLF-normalized Assimp import and post-import changes;
- exact current Assimp/build-source blobs; and
- the header-only source-input chain for this clean build.

It does not prove:

- an upstream release signature, complete upstream history, or maintainer identity;
- third-party notice completeness or license/legal disposition;
- binary reproducibility or runtime behavior; or
- Open3D product integration, Viewer/Runner parity, redistribution, or owner/legal approval.

Keep this evidence separate from the product Viewer and inspection reliability claims.
