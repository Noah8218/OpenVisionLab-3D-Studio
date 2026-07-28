# Assimp Open3DGC Compiler-Input Provenance

Checked: 2026-07-16

## Decision

The fixed Open3D clean build's `29` compiler-read Open3DGC files now have an exact public carrier snapshot, Assimp import, fixed-tag delta, and clean-build identity chain.

The source-identity anchor is not a release tag. It is the public `KhronosGroup/glTF` branch `mesh-compression-open3dgc` at commit `7b61d5e065f98058fa12fadfec821546f486d960`. That branch snapshot is byte-identical to the `29` files added by Assimp commit `054820e6ffc03f1a914f2bc688d7f030cf01894b` (`[+] Added Open3DGC codec from KhronosGroup repository.`).

This is source-content and bounded Assimp-delta evidence only. It does not approve Open3D distribution, add a product dependency, prove binary reproducibility, or replace final notice/legal review.

## Fixed Identities

| Role | Value |
| --- | --- |
| Public carrier remote | `https://github.com/KhronosGroup/glTF.git` |
| Public carrier branch | `refs/remotes/origin/mesh-compression-open3dgc` |
| Public carrier snapshot | `7b61d5e065f98058fa12fadfec821546f486d960` |
| Assimp import | `054820e6ffc03f1a914f2bc688d7f030cf01894b` |
| Assimp fixed tag | `v5.4.2` |
| Assimp fixed tag commit | `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Clean-build input | `artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\Open3DGC` |
| Closure component | `Open3DGC`, `29` files, SHA-256 `bb290db0bc142ae99b6b07774091db1f07f29e820b544518c609d6559494f410` |

The Khronos branch's bundled Open3DGC README identifies the historical `amd/rest3d` location. That historical remote no longer resolves. The available author-hosted `kmammou/rest3d` repository is contextual source/copyright evidence only; it is not used here to claim the exact Assimp import revision. The exact imported content is the public Khronos snapshot above.

## Verified Chain

1. The Khronos branch reference resolves to the fixed snapshot.
2. Every one of the `29` mapped Khronos files has the same Git blob as the corresponding Assimp import file.
3. Assimp `v5.4.2` is the fixed commit and descendants include the import.
4. The current fixed tag changes exactly `16` of the `29` imported file paths. The verifier checks the complete ordered delta set.
5. Every current fixed-tag blob matches the actual clean-build Open3DGC input.
6. Assimp's CMake source list contains all `29` paths and enables `ASSIMP_IMPORTER_GLTF_USE_OPEN3DGC=1`.
7. The fixed compiler-read closure has the exact `29` ordered files and recorded canonical SHA-256.

The bounded `v5.4.2` delta paths are:

```text
o3dgcAdjacencyInfo.h
o3dgcArithmeticCodec.cpp
o3dgcArithmeticCodec.h
o3dgcBinaryStream.h
o3dgcCommon.h
o3dgcDynamicVector.h
o3dgcFIFO.h
o3dgcIndexedFaceSet.h
o3dgcSC3DMCDecoder.h
o3dgcSC3DMCDecoder.inl
o3dgcSC3DMCEncoder.inl
o3dgcTimer.h
o3dgcTriangleListEncoder.h
o3dgcTriangleListEncoder.inl
o3dgcVector.h
o3dgcVector.inl
```

## License Boundary

`o3dgcCommon.h` carries the 2013 Khaled Mammou / Advanced Micro Devices MIT permission notice. `o3dgcArithmeticCodec.h` and `o3dgcArithmeticCodec.cpp` separately carry the Amir Said / William A. Pearlman two-clause BSD redistribution notice.

The effective notice evidence for this compiler-read slice is therefore `MIT AND BSD-2-Clause`, not an MIT-only claim. This records source notices; it does not determine a complete packaged-notice bundle or provide legal advice.

## Reproduction

Fetch the historical public branch before running the verifier when the local clone lacks the tracking reference:

```powershell
git -C artifacts\dependency-candidates\assimp-open3dgc-intake-20260716\official-khronos-gltf fetch --filter=blob:none origin mesh-compression-open3dgc:refs/remotes/origin/mesh-compression-open3dgc
```

Use a new work directory for every run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-open3dgc-provenance.ps1 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-open3dgc-provenance-20260716\manual-work `
  -ReportPath artifacts\dependency-candidates\assimp-open3dgc-provenance-20260716\manual-report.json
```

Expected positive result:

```text
AssimpOpen3DGCProvenance|Pass|khronos=7b61d5e065f98058fa12fadfec821546f486d960|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=29|deltas=16|issues=0
```

A controlled wrong Khronos revision must fail closed with exit code `1`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-open3dgc-provenance.ps1 `
  -ExpectedKhronosRevision 0000000000000000000000000000000000 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-open3dgc-provenance-20260716\negative-work `
  -ReportPath artifacts\dependency-candidates\assimp-open3dgc-provenance-20260716\negative-report.json
```

## Claim Boundary

This evidence proves the fixed source-content path for the `29` compiler-read Open3DGC inputs only. It does not prove the historical AMD remote's availability, upstream signatures or release tagging, all Open3D dependencies, a complete final notice bundle, binary reproducibility, redistribution permission, product integration, metrology behavior, or Viewer/Runner parity.
