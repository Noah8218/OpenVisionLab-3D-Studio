# Assimp UTF8-CPP Provenance - 2026-07-16

## Decision

The four compiler-read UTF8-CPP headers in the fixed Open3D/Assimp candidate are source-identical from the official `nemtrif/utfcpp` `v3.2.3` tag through the Assimp update, fixed `v5.4.2`, and the clean-build source.

This is a fixed source-identity result for one header-only component. It does not approve Open3D, Assimp, or UTF8-CPP for product distribution.

## Fixed Inputs

| Item | Fixed value |
| --- | --- |
| Official UTF8-CPP remote | `https://github.com/nemtrif/utfcpp.git` |
| Upstream tag | `v3.2.3` |
| Upstream tag commit | `79835a5fa57271f07a90ed36123e30ae9741178e` |
| Official Assimp remote | `https://github.com/assimp/assimp.git` |
| Assimp update | `ce59d49dd9ce93ccf8585f78c70e58cb0e5d4961` (`update upf8 from 2.3.4 to 3.2.3`) |
| Assimp current tag | `v5.4.2` / `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Actual build-source directory | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/utf8cpp/source` |
| Compiler-read closure report | `artifacts/dependency-candidates/assimp-closure-20260716/assimp-closure.json` |

The official upstream source and tag are available at [UTF8-CPP](https://github.com/nemtrif/utfcpp) and [v3.2.3](https://github.com/nemtrif/utfcpp/releases/tag/v3.2.3).

## Source Identity

| File | Official v3.2.3 blob | Assimp update blob | Assimp v5.4.2/build blob |
| --- | --- | --- | --- |
| `utf8.h` | `82b13f59f983c57ea5bba18bcb58f836eaba8d5e` | same | same |
| `utf8/checked.h` | `512dcc2fbac82c55afb24f1ffc99d677b2a8e86a` | same | same |
| `utf8/core.h` | `34371ee31c8c3f48dc86c74991bc74230d08d3a7` | same | same |
| `utf8/unchecked.h` | `8fe83c9ecbc7eeffbf693bc8a50cd1833f816e82` | same | same |

The fixed Assimp post-update range from `ce59d49...` to `v5.4.2` has no history entries for these four paths. The verifier therefore requires every source, import, current-tag, and build-source blob to be identical and fails if a future tag introduces a changed path.

## Compiler Input Contract

Assimp's non-Hunter CMake path adds `../contrib/utf8cpp/source` to the include directories. The header chain is fixed as follows:

```text
utf8.h
  -> utf8/checked.h
       -> utf8/core.h
  -> utf8/unchecked.h
       -> utf8/core.h
```

The existing fixed compiler-read closure records exactly these four paths, count `4`, and file-set SHA-256:

```text
29a1bcc593f7b655228ea1deb6340c41047fbe2ea7a2d20b3c888057e3770c0c
```

The source tree also contains optional C++11/C++17 helpers, but they are not in this fixed compiler-read set. Do not claim that this four-file result proves the provenance of every file in UTF8-CPP.

## Repeatable Verification

The two official repositories must be present locally. The existing Assimp clone from the prior provenance tasks is reusable.

```powershell
git clone https://github.com/nemtrif/utfcpp.git `
  artifacts/dependency-candidates/assimp-utf8cpp-provenance-20260716/official-utf8cpp

git clone https://github.com/assimp/assimp.git `
  artifacts/dependency-candidates/assimp-kubazip-provenance-20260716/official-assimp

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-utf8cpp-provenance.ps1 `
  -WorkingDirectory artifacts/dependency-candidates/assimp-utf8cpp-provenance-20260716/utf8cpp-provenance-run `
  -ReportPath artifacts/dependency-candidates/assimp-utf8cpp-provenance-20260716/utf8cpp-provenance.json
```

The recorded positive run reports:

```text
AssimpUtf8CppProvenance|Pass|utf8cpp=79835a5fa57271f07a90ed36123e30ae9741178e|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=4|history=4|issues=0
```

Use a new `-WorkingDirectory` for every run. The verifier rejects an existing working directory to avoid stale evidence reuse.

The following controlled negative input must return exit code `1` and write a revision mismatch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-utf8cpp-provenance.ps1 `
  -WorkingDirectory artifacts/dependency-candidates/assimp-utf8cpp-provenance-20260716/utf8cpp-provenance-negative-run `
  -ReportPath artifacts/dependency-candidates/assimp-utf8cpp-provenance-20260716/utf8cpp-provenance-negative.json `
  -ExpectedUtf8CppRevision 0000000000000000000000000000000000000000
```

## Claim Boundary

This evidence proves:

- fixed official `v3.2.3` identity for the four compiler-read headers;
- exact Assimp update/current/build-source blob identity;
- an empty fixed-range post-update history for those paths; and
- the recorded CMake/header-chain/compiler-read input contract.

It does not prove:

- upstream release signature, complete upstream history, or maintainer identity;
- provenance for optional UTF8-CPP headers outside the four-file closure;
- third-party notice completeness or license/legal disposition;
- binary reproducibility or runtime behavior; or
- Open3D product integration, Viewer/Runner parity, redistribution, or owner/legal approval.

Keep this supply-chain evidence separate from Viewer, measurement, and physical-metrology claims.
