# BoringSSL Controlled Rebuild Plan - 2026-07-16

## Scope And Boundary

This plan prepares a controlled current-toolchain rebuild of the BoringSSL package consumed by the separate-process Open3D `0.19.0` candidate. It is not permission to distribute BoringSSL/Open3D, add a product dependency, change the Viewer or Runner, or claim historical byte reproduction.

The goal is narrower: preserve the official Open3D Windows recipe, build the fixed BoringSSL source on a recorded current host after approved prerequisites are available, and compare package topology and static-library link contracts with Open3D's fixed prebuilt archive.

## Fixed Inputs

| Input | Fixed identity |
| --- | --- |
| BoringSSL source | Commit `edfe4133d28c5e39d4fce6a2554f3e2b4cafc9bd` |
| Open3D build script | `build_boringssl.ps1`, SHA-256 `f2f24801a5e69b7dd294332afbbb4270e62c71c71ab03322d82511f8561ce50e` |
| Open3D archive definition | `boringssl.cmake`, SHA-256 `8555bd0e4476c8cb015fd0fdc96d356c47d8afd8962cf36c522c1ab5cc205bf2` |
| Fixed prebuilt archive | `boringssl_edfe413_win_amd64.tar.gz`, size `6,265,404`, SHA-256 `fd538d545990a4657ee2b22c444e0baf61edaa1609f84cdfc9217659c44988c4` |

The recorded official script builds Release and Debug `ssl` and `crypto`, copies `include`, and packages the output. It has no patch command.

## Current Environment Preflight

Observed on 2026-07-16, before any installation:

| Requirement | Observed state | Decision |
| --- | --- | --- |
| Visual Studio 2022 developer environment | Available | Usable current-toolchain candidate only |
| MSVC x64 compiler | `14.44.35207` | Record if a build is approved; not historical-toolchain evidence |
| CMake | Available from VS2022 | Record exact version during the run |
| Git | Available | Record exact version during the run |
| Strawberry Perl / compatible `perl` | Missing from developer command path | Blocked |
| Go | Missing from developer command path | Blocked |
| NASM | Missing from developer command path | Blocked |

The official script names Perl, Go, and NASM as Windows prerequisites. Do not install them or run a substitute recipe without explicit owner approval. A partial build or a manually edited copy of the script is not evidence for this plan.

Ignored preflight evidence is under `artifacts/dependency-candidates/boringssl-controlled-rebuild-preflight-20260716`, including the exact developer-command-path output and a machine-readable summary. It records no prerequisite installation, clone, build, archive comparison, or distribution claim.

## Approved-Run Checklist

1. Record explicit owner approval for the three prerequisite tool installations and their applicable licenses.
2. Install only the approved tools, then record absolute paths, versions, installer/source identities, and SHA-256 values under a new ignored artifact root such as `artifacts/boringssl-controlled-rebuild-YYYYMMDD`.
3. Revalidate the four fixed inputs above before cloning or compiling.
4. Run the byte-matched Open3D script unchanged from an artifact-local working directory under a recorded VS developer command environment. Preserve standard output/error and the exact command line.
5. Record the cloned source `HEAD`, tree identity, CMake cache/generator, MSVC version, all prerequisite versions, CPU architecture, and build elapsed time.
6. Compare candidate and fixed archives after extraction:
   - complete relative path set;
   - every copied header hash;
   - Release/Debug `ssl.lib` and `crypto.lib` size/hash;
   - `dumpbin /directives` contract for each static library;
   - controlled link smoke using the same named libraries.
7. Repeat the candidate build once under the same recorded environment. Report byte identity only if the candidate archives actually match; otherwise report the exact structural and link-contract differences.
8. Keep all inputs and outputs under ignored `artifacts/`. Do not add the candidate archive or libraries to product, CI, release assets, Viewer manifests, or notices.

## Expected Command Shape

After prerequisites are approved and present on `PATH`, invoke the unmodified artifact-local script through a recorded VS developer environment:

```powershell
$command = 'call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64 && powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\path\to\artifact\build_boringssl.v0.19.0.ps1'
& cmd.exe /d /c $command
```

Use the actual approved Visual Studio path rather than assuming this workstation's path. The script creates an archive named `boringssl_edfe413_win_amd64.tar.gz` when `PROCESSOR_ARCHITECTURE=AMD64`.

## Acceptance And Nonclaims

A successful controlled build can establish current-toolchain package topology, header content, static-library directive, and link compatibility evidence. It cannot by itself establish the historical Open3D archive's compiler/toolchain, byte identity, source-to-binary equivalence, redistribution rights, final notices, registration-product integration, Viewer/Runner parity, or physical metrology accuracy.

If a prerequisite installation, build, topology check, directive comparison, or link smoke fails, preserve the evidence and keep the BoringSSL provenance gate open.
