# Controlled VTK 9.1.0 Release Rebuild - 2026-07-16

## Scope And Decision

This is a supply-chain evidence checkpoint for the separate-process Open3D registration candidate. It does not add VTK or Open3D to the product, approve redistribution, prove physical measurement accuracy, or prove historical prebuilt source-to-binary identity.

**Decision:** the exact Open3D VTK `9.1.0` source input and no-patch package recipe can produce a current, Release-only Windows candidate whose VTK package paths, Release target contract, CRT directives, and a direct link/run smoke match the legacy package contract. Historical reproduction remains open because the original Debug payload and its full historical build cache, commands, and environment are unavailable.

The follow-up same-source Open3D `0.19.0` build now proves that this VTK candidate is usable by the separate-process registration candidate. It is a local runtime-compatibility checkpoint, not VTK historical byte identity, product adoption, redistribution approval, or Viewer/Runner registration parity.

## Fixed Inputs

| Input | Evidence |
| --- | --- |
| VTK source | `VTK-9.1.0.tar.gz`, SHA-256 `8fed42f4f8f1eb8083107b68eaa9ad71da07110161a3116ad807f43e5ca5ce96` |
| VTK source commit | `285daeedd58eb890cb90d6e907d822eea3d2d092` |
| Open3D historical package recipe | `CMakeLists.txt` SHA-256 `c03a14d9785677ae4090b73b376cbaebde65c3fc92226d27eeeb0f474a9c37f5`; `vtk_build.cmake` SHA-256 `f6d2e37138a3768bf9ce4e0871bb5c913ce614db170c161b53481ad41726d328` |
| Legacy VTK package | `vtk_9.1_win.tar.gz`, `175,647,944` bytes, SHA-256 `6ee09115d23ec18d6d01d1e4c89fa236ec69406d8ba8cc1b8ec37c4123b93caa` |

## Controlled Build

The unmodified historical package recipe was configured with `STATIC_WINDOWS_RUNTIME=OFF` using:

| Setting | Value |
| --- | --- |
| CMake | `3.31.6` |
| Generator | `Visual Studio 17 2022`, x64 |
| Platform toolset | `v143` |
| C/C++ compiler | MSVC `19.44.35227.0`, `cl.exe` from `14.44.35207` |
| Windows SDK | `10.0.26100.0` targeting Windows `10.0.19045` |
| Build | Release only |

The external-project completion stamp exists. The package build log contains no error line and seven `C4819` source-encoding warnings. The resulting archive is `31,473,977` bytes with SHA-256 `a11a803164e4feef2ac4a223235a32c6ceed8f7a44a13c8103a9a1a0d907e09d`.

## Package Contract Results

| Check | Result |
| --- | --- |
| Release library count | `16` |
| Archive path comparison | All `1,139` candidate files are present in the legacy archive; candidate-only files: `0` |
| Legacy-only files | `17`: `VTK-targets-debug.cmake` plus 16 `d`-suffixed Debug libraries |
| Shared file SHA-256 matches | `1,107 / 1,139` |
| Shared file SHA-256 mismatches | `32`: 16 Release libraries, 13 generated headers, 3 generated CMake exports |
| Imported target count | `23` for both packages; `22 / 22` VTK target name/type/link/Release-library-leaf contracts match |
| Debug target locations | Missing by design in this Release-only candidate for the 16 static libraries |
| `/directives` comparison | `16 / 16` exact directive sets match: 15 C++ libraries use `MD_DynamicRelease` and the same `_MSC_VER=1900` marker; C-only `vtkkissfft` has neither marker |
| Direct CMake link/run smoke | `vtkSphereSource` executable built against only the candidate and reran with exit code `0` |

The twelve generated module-header differences normalize to the same content after removing a current-CMake `NOLINTNEXTLINE` line. The remaining generated header is `vtkConfigureDeprecated.h`; it records the actual configured compiler path, so a difference is expected.

## Correct Interpretation Of `_MSC_VER=1900`

The legacy package's generated `vtkConfigureDeprecated.h` records:

```text
C:/Program Files (x86)/Microsoft Visual Studio/2019/Enterprise/VC/Tools/MSVC/14.29.30133/bin/Hostx64/x64/cl.exe
```

The controlled candidate records its VS2022 `14.44.35207` compiler path. Despite that actual toolchain difference, both packages emit `/FAILIFMISMATCH:_MSC_VER=1900`. The installed VS2022 `yvals.h` deliberately contains:

```cpp
#pragma detect_mismatch("_MSC_VER", "1900")
```

Therefore that directive is an MSVC ABI-family marker, not an actual compiler-version measurement. It cannot establish a conflict with the documented VS2019 recipe or prove a VS2015 build. Microsoft documents binary compatibility across the v140 through v143 toolsets, with the final link performed by a toolset at least as new as the newest input: [C++ binary compatibility between Visual Studio versions](https://learn.microsoft.com/en-us/cpp/porting/binary-compat-2015-2017?view=msvc-170).

This corrects the earlier local claim that `VS2019` and `_MSC_VER=1900` contradicted one another. The marker no longer blocks provenance analysis. It also does not replace the missing historical CI log, full CMake cache, exact build commands, or proof that the legacy binary came from the recovered source tree.

## Remaining Limits And Next Evidence

1. The candidate intentionally omits all Debug libraries and `VTK-targets-debug.cmake`.
2. All 16 Release library hashes differ from the legacy prebuilt archive. The candidate is a current-toolchain compatibility/rebuild result, not byte-identical historical reproduction.
3. The exact legacy VS2019 `14.29.30133` toolset is not installed on this workstation, so an exact-toolchain rerun requires a separate controlled Windows environment.
4. A same-source `USE_SYSTEM_VTK=ON` Open3D Release build and registration probe now complete against this candidate. Product adoption, runner result mapping, Viewer/Runner registration parity, clean-host VC/OpenMP installation, notices, and owner/legal approval remain blocked.

## Reproducible Evidence

Ignored local evidence is under `artifacts/dependency-candidates/vtk-source-rebuild-20260716`:

```text
configure.txt
build-release.txt
controlled-rebuild-summary.json
controlled-rebuild-file-comparison.json
controlled-rebuild-directive-comparison.json
controlled-rebuild-target-comparison.json
build/vtk_9.1_win.tar.gz
link-smoke/CMakeLists.txt
link-smoke/main.cxx
link-smoke-build/Release/vtk_controlled_rebuild_link_smoke.exe
```

Keep this evidence isolated from product source and release bundles until the remaining distribution gates pass.

The follow-up Open3D candidate evidence is under `artifacts/o3d-vtk-candidate-20260716` and is summarized in `docs/OPENVISIONLAB_3D_OPEN3D_VTK_CANDIDATE_RUNTIME_20260716.md`.
