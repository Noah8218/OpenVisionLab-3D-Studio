# Assimp MiniZip Provenance - 2026-07-16

## Decision

The four compiler-read MiniZip files in the fixed Open3D/Assimp candidate are source-identical to the `contrib/minizip` snapshot in official `madler/zlib v1.3.1`, through the Assimp update, fixed `v5.4.2`, and the clean-build source.

This is a fixed source-identity result for a four-file zlib contrib subset. It is not a complete origin history for the independent MiniZip project or for every file in Assimp's `contrib/unzip` directory.

## Fixed Inputs

| Item | Fixed value |
| --- | --- |
| Official zlib remote | `https://github.com/madler/zlib.git` |
| zlib tag | `v1.3.1` |
| zlib tag commit | `51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf` |
| Official Assimp remote | `https://github.com/assimp/assimp.git` |
| Assimp update | `64d88276ef7117c09165e468dbb9acd999e324ac` (`updated minizip to last version`) |
| Assimp current tag | `v5.4.2` / `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Actual build-source directory | `artifacts/o3d-clean/b/assimp/src/ext_assimp/contrib/unzip` |
| Compiler-read closure report | `artifacts/dependency-candidates/assimp-closure-20260716/assimp-closure.json` |

The official zlib source documents MiniZip as an experimental contribution under `contrib/minizip`; the fixed tag is available at [zlib v1.3.1](https://github.com/madler/zlib/releases/tag/v1.3.1).

## Source Identity

| File | zlib v1.3.1 blob | Assimp update blob | Assimp v5.4.2/build blob |
| --- | --- | --- | --- |
| `ioapi.c` | `782d32469ae5d5dc3515b9e589737c3b0c661dda` | same | same |
| `ioapi.h` | `a2d2e6e60d9250b048d50320a60dccc9d99e0264` | same | same |
| `unzip.c` | `ea05b7d62a07f6ada2cb5a7723f398d9f44a8822` | same | same |
| `unzip.h` | `5cfc9c6274e75e32ae79f5d51a18fbd874f62711` | same | same |

The fixed Assimp post-update range from `64d88276...` to `v5.4.2` has no history entries for these four paths. The verifier requires all source, import, current-tag, and build-source blobs to stay identical.

## Compiler Input Contract

Assimp's non-Hunter `unzip_SRCS` source group lists five paths, including `crypt.h`. The fixed compiler-read closure records only these four files:

```text
contrib/unzip/ioapi.c
contrib/unzip/ioapi.h
contrib/unzip/unzip.c
contrib/unzip/unzip.h
```

Their recorded file-set SHA-256 is:

```text
2e24ca3bfc05768d96770aa57d73843c01c0c3abd7acd3e3643b4db4177dc19f
```

This is intentional for the fixed source: `unzip.c` defines `NOUNCRYPT` before the later conditional `crypt.h` include, preventing that header from being preprocessed. The verifier checks the CMake source group, non-Hunter include directory, source-defined `NOUNCRYPT` ordering, `unzip.c -> unzip.h -> ioapi.h` chain, and the closure record.

`crypt.h` is therefore not covered by this four-file provenance result. Its independent Info-ZIP-related origin and any encryption-capable build remain separate scope.

## Repeatable Verification

The two official repositories must be present locally. The existing Assimp clone from the prior provenance tasks is reusable.

```powershell
git clone https://github.com/madler/zlib.git `
  artifacts/dependency-candidates/assimp-minizip-intake-20260716/official-zlib

git clone https://github.com/assimp/assimp.git `
  artifacts/dependency-candidates/assimp-kubazip-provenance-20260716/official-assimp

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-minizip-provenance.ps1 `
  -WorkingDirectory artifacts/dependency-candidates/assimp-minizip-provenance-20260716/minizip-provenance-run `
  -ReportPath artifacts/dependency-candidates/assimp-minizip-provenance-20260716/minizip-provenance.json
```

The recorded positive run reports:

```text
AssimpMiniZipProvenance|Pass|zlib=51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf|assimp=ddb74c2bbdee1565dda667e85f0c82a0588c8053|files=4|history=4|issues=0
```

Use a new `-WorkingDirectory` for every run. The verifier rejects an existing directory so stale evidence cannot be reused.

The following controlled negative input must return exit code `1` and record a zlib revision mismatch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-minizip-provenance.ps1 `
  -WorkingDirectory artifacts/dependency-candidates/assimp-minizip-provenance-20260716/minizip-provenance-negative-run `
  -ReportPath artifacts/dependency-candidates/assimp-minizip-provenance-20260716/minizip-provenance-negative.json `
  -ExpectedZlibRevision 0000000000000000000000000000000000000000
```

## Claim Boundary

This evidence proves:

- fixed zlib `v1.3.1` `contrib/minizip` identity for the four compiler-read files;
- exact Assimp update/current/build-source blob identity;
- an empty fixed-range Assimp post-update history for those paths; and
- the source-defined `NOUNCRYPT` reason that excludes `crypt.h` from this build's compiler-read closure.

It does not prove:

- complete original MiniZip, Info-ZIP, or `crypt.h` provenance;
- upstream release signature, complete upstream history, or maintainer identity;
- third-party notice completeness or license/legal disposition;
- binary reproducibility or runtime behavior; or
- Open3D product integration, Viewer/Runner parity, redistribution, or owner/legal approval.

Keep this supply-chain result separate from Viewer, inspection, and physical-metrology reliability claims.
