# Open3D Windows Distribution Audit

Checked: 2026-07-13

## Decision

Do not add the official Open3D `0.19.0` Windows development binaries to an OpenVisionLab product or Viewer release bundle.

The package is valid for the current local registration prototype, but it is not distribution-ready evidence. The inspected binary package contains no license, notice, SBOM, dependency lock, CMake cache, or compile-command file. Its public CMake export identifies Eigen and the dynamic oneTBB dependency, while the monolithic `Open3D.dll` contains additional private/static components that the package does not enumerate. A same-tag local source build now provides reproducible configuration, recovered and independent clean build/install evidence, complete install hashes, and a smaller runtime. A CycloneDX `1.6` candidate now records 27 direct components plus 6 observed support/transitive components, exact archive and license hashes where available, and three Open3D-side modifications. It is intentionally incomplete because Assimp `contrib` closure and exact prebuilt BoringSSL/MKL/VTK provenance remain unresolved. The audit also still lacks a final notice bundle, Microsoft prerequisite deployment proof, clean-host evidence, product integration impact analysis, and owner/legal approval. Exact attribution and redistribution obligations therefore cannot be reconstructed reliably enough for publication.

This is an engineering compliance audit, not legal advice. Commercial distribution requires owner/legal approval after the evidence gate below passes.

## Binary Package Evidence

```text
Package: open3d-devel-windows-amd64-0.19.0.zip
Bytes: 69,318,048
SHA-256: a39a205e4d9db029e2a98313acae20d8cc8e3f60f89437ee4f3f00cdc231dc87
Extracted files: 1,095
Extracted bytes: 223,575,567
License / notice / copying / SBOM files: 0
CMakeCache / compile_commands / dependency-lock files: 0
```

The registration probe's adjacent native runtime remains:

| File | Bytes | Audit note |
| --- | ---: | --- |
| `open3d-registration-probe.exe` | 70,656 | Requires Microsoft VC runtime |
| `Open3D.dll` | 141,146,112 | Monolithic Open3D binary; exact private/static component manifest absent |
| `tbb12.dll` | 320,000 | File version `2021.12.0`; oneTBB is Apache-2.0 |
| **Adjacent total** | **141,536,768** | Excludes Microsoft runtime prerequisite |

`dumpbin` records `MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`, and `VCOMP140.dll` in addition to Windows system libraries. The current machine has x64 VC runtime `14.51.36247.00`, so successful local execution is not clean-machine deployment evidence.

Binary inspection also finds Filament, Assimp, libjpeg, and libpng markers plus `IPPCODE`/`IPPDATA` sections. This proves that the DLL contains more than the public Eigen/oneTBB CMake surface, but it does not prove the complete enabled-component list or each component version. Do not turn these markers into an inferred SBOM.

## Source Comparison

The same-tag source archive was inspected separately:

```text
Package: Open3D v0.19.0 source ZIP
Bytes: 50,486,918
SHA-256: a7744e3900fbf93c7f3d5828c93a489f2c7b90deb1eb30d94c2b591f0ed38b69
Source files: 2,260
Third-party license files under 3rdparty/: 42
```

Open3D itself uses MIT and requires the copyright and permission notice to accompany copies or substantial portions. The official `3rdparty/README.md` lists dependencies under multiple license families, including MIT, BSD, Apache-2.0, MPL-2.0, zlib/libpng, Curl, Intel, and other terms. The source tree includes component license files for Assimp, Eigen, Filament, IPP, MKL, libjpeg-turbo, libpng, Qhull, VTK, ZeroMQ, and others. This list describes possible source dependencies; without the binary build configuration it is not proof that every item is linked into the Windows release DLL.

The bundled `tbb12.dll` version matches oneTBB `2021.12.0`. Its Apache-2.0 distribution conditions require the license and applicable attribution/NOTICE handling. Those files are not present in the inspected Open3D binary package.

## Source Configure Reproduction

The official `v0.19.0` tag resolves to commit `1e7b17438687a0b0c1e5a7187321ac7044afe275`. The source archive hash above and the following non-GUI Windows configuration are now fixed as the candidate build input:

```powershell
$cmake = 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
& $cmake -S artifacts\dependency-candidates\open3d-license-audit-0.19.0\source\Open3D-0.19.0 `
  -B artifacts\dependency-candidates\open3d-source-build-0.19.0\build-minimal `
  -G 'Visual Studio 17 2022' -A x64 `
  -DBUILD_SHARED_LIBS=ON -DSTATIC_WINDOWS_RUNTIME=OFF `
  -DBUILD_GUI=OFF -DBUILD_WEBRTC=OFF -DBUILD_JUPYTER_EXTENSION=OFF `
  -DBUILD_PYTHON_MODULE=OFF -DBUILD_EXAMPLES=OFF -DBUILD_UNIT_TESTS=OFF -DBUILD_BENCHMARKS=OFF `
  -DBUILD_CUDA_MODULE=OFF -DBUILD_SYCL_MODULE=OFF -DBUILD_ISPC_MODULE=OFF `
  -DWITH_IPP=OFF -DWITH_OPENMP=ON `
  -DBUILD_LIBREALSENSE=OFF -DBUILD_AZURE_KINECT=OFF `
  -DBUILD_TENSORFLOW_OPS=OFF -DBUILD_PYTORCH_OPS=OFF -DBUNDLE_OPEN3D_ML=OFF `
  -DDEVELOPER_BUILD=OFF
```

Local configuration evidence:

```text
CMake: 3.31.6 (Visual Studio bundled)
Generator: Visual Studio 17 2022, x64
Compiler: MSVC 19.44.35227.0
Windows SDK: 10.0.26100.0 targeting Windows 10.0.19045
First configure / generate: 41.9 / 7.5 seconds
Second independent configure / generate: 35.1 / 3.4 seconds
Selected option comparison: 20 options, 0 differences
Solution project comparison: 109 / 109 projects, 0 differences
Result: Open3D.sln created independently in both build directories
Evidence: artifacts/dependency-candidates/open3d-source-build-0.19.0/configure-minimal.log
Evidence: artifacts/dependency-candidates/open3d-source-build-0.19.0/configure-minimal-repeat.log
Evidence: artifacts/dependency-candidates/open3d-source-build-0.19.0/configure-reproducibility.json
```

The options remove GUI, WebRTC, Python, examples, tests, benchmarks, CUDA, SYCL, ISPC, IPP, and sensor integrations. They do not produce a registration-only dependency graph. The generated configuration still builds Assimp, curl, Eigen, fmt, GLEW, GLFW, JPEG, jsoncpp, liblzf, msgpack, nanoflann, PNG, Qhull, TBB, TinyGLTF, tinyobjloader, VTK, ZeroMQ, and other helpers from source, while using OpenGL and OpenMP from the system. NASM was not installed, so CMake warns that libjpeg-turbo performance may be reduced.

This passes source identity and configure reproducibility. The broad dependency graph confirms that source configuration alone does not justify adopting Open3D as a small registration runtime. The following build evidence narrows runtime and binary behavior but does not close attribution or clean-host deployment.

## Source Release Build Evidence

The fixed configuration first completed a Release build and `INSTALL` after interrupted-output recovery. Early overlapping build processes left five 0-byte Embree compile objects; those generated objects were removed and rebuilt without modifying Open3D source. A subsequent single-process incremental `INSTALL` returned exit code `0`, reported the install tree up to date, and found zero 0-byte Embree compile objects.

```text
Install root: artifacts/o3d/i
Install files: 873
Install bytes: 88,977,375
DLLs: 2
Libraries: 2
Headers: 801
CMakeCache SHA-256: d73019329d0b00ee3dce00b6712d9db4ee6c01e443044a3780db02783bedf952
Install manifest JSON SHA-256: c6006e8955a6f226c936ba40b1d335a2b7731661be2bc646343cff72833e7b7a
Incremental verification log SHA-256: b2106f07629d413091e9651442c65a824c92d8d9a46bb17a0490561ac147d172
```

| Source-built runtime file | Bytes | SHA-256 |
| --- | ---: | --- |
| `open3d-registration-probe.exe` | 70,656 | `5214ee9e4b9664e34f6e28e29673ba64dcc7caf21cf913022efa1470ebbc8d3e` |
| `Open3D.dll` | 58,120,704 | `4d8cd9ea3bb1310851f8a942fb2f21bb8313bf182e29e993e856b0a7ad842d5e` |
| `tbb12.dll` | 328,704 | `4014138f76a813cda99570c238b7e34489969782c8b5896f851c0ec0ce877388` |
| **Adjacent total** | **58,520,064** | n/a |

The source-built adjacent runtime is 83,016,704 bytes smaller than the official-package probe bundle. `dumpbin /DEPENDENTS` identifies `tbb12.dll` as the only adjacent non-system DLL; VC143 CRT and OpenMP remain installed prerequisites. Build diagnostics include missing NASM with reduced libjpeg-turbo performance, a libzmq `/LTCG` recommendation, and Open3D DLL export `LNK4286` warnings.

Behavioral evidence is stronger than a successful link alone. The source-built probe matches the official binary exactly, excluding elapsed time, in all 33 runs of the 11-case robustness matrix and is deterministic in 11/11 cases. Current pair `0 -> 1` DemoICP output also matches in 3/3 runs. Both runtimes reject pair `1 -> 2` with exit code `1` and no report because `cloud_bin_2.pcd` contains 771 non-finite normals; its older successful metrics predate the input guard and are not current pass evidence.

An independent clean directory then configured once and completed one uninterrupted, single-process Release `INSTALL` invocation in 3,387.699 seconds with exit code `0`. Its persistent log has 56 compiler-warning lines, 152 linker-warning lines, 29 build CMake warning headers, 5 configure CMake warning headers, and zero actual error lines. There are zero 0-byte Embree compile objects; the only zero-byte `*.obj` path is Assimp's intentional invalid-model test fixture.

The clean install repeats all 873 paths and 88,977,375 bytes. Manifest readback has zero missing or mismatched entries. Compared with the recovered install, 871 hashes match and only the rebuilt `Open3D.dll` and `tbb12.dll` differ. Their PE timestamps and SHA-256 values differ, but sizes, export ordinal/name contracts, and dependency lists match. The clean three-file runtime remains 58,520,064 bytes:

| Clean runtime file | Bytes | SHA-256 |
| --- | ---: | --- |
| `open3d-registration-probe.exe` | 70,656 | `55b88b951070773df103dc98ec8641b43e7a80205228c1426c46a2155b13bc8f` |
| `Open3D.dll` | 58,120,704 | `53062a532951e85612a724dad3908f0587a247874e93248ddfef6f8ecd150712` |
| `tbb12.dll` | 328,704 | `10e38577698271acbee58b77d1b936b17f85e6f1bab282feea779d70787d4e9d` |
| **Adjacent total** | **58,520,064** | n/a |

The clean probe matches the official binary in 33/33 robustness runs, is deterministic in 11/11 cases, and reproduces the same 5/11 predeclared outcomes. It also matches current `0 -> 1` DemoICP output in 3/3 runs and returns the same controlled `1 -> 2` non-finite-normal failure.

Ignored evidence:

```text
artifacts/o3d/install-summary.txt
artifacts/o3d/install-manifest.json
artifacts/o3d/install-manifest.csv
artifacts/o3d/open3d-dependents.txt
artifacts/o3d/incremental-install-verification.log
artifacts/o3d/probe-results/robustness-source-build/source-build-parity-summary.json
artifacts/o3d/probe-results/demo-current/pair-0-to-1-parity.json
artifacts/o3d/probe-results/demo-current/pair-1-to-2-controlled-failure-parity.json
artifacts/o3d/probe-results/demo-current/pcd-nonfinite-audit.txt
artifacts/o3d-clean/configure.log
artifacts/o3d-clean/build-release-install.log
artifacts/o3d-clean/build-log-audit.txt
artifacts/o3d-clean/install-manifest.json
artifacts/o3d-clean/install-manifest-verification.txt
artifacts/o3d-clean/binary-contract-comparison.json
artifacts/o3d-clean/probe-results/robustness/clean-build-parity-overall.json
artifacts/o3d-clean/probe-results/demo-current/pair-0-to-1-clean-parity.json
artifacts/o3d-clean/probe-results/demo-current/pair-1-to-2-clean-controlled-failure-parity.json
```

## Candidate SBOM Evidence

`docs/open3d-0.19.0-nongui-windows-candidate.cdx.json` is a schema-valid CycloneDX `1.6` direct-evidence candidate, not a complete distribution manifest. It contains 33 component records: 27 direct components reduced from 28 external-project references and 6 observed support/transitive components. All 23 downloaded component-archive hashes match the clean build cache exactly; the root source archive, 35 unique license evidence hashes, and all 3 recorded modification hashes also match local files. Eight unresolved-provenance records deliberately keep the distribution gate open.

See `docs/OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md` for scope, method, validation, and blockers. Do not generate final notices from this candidate while Assimp and prebuilt BoringSSL/MKL/VTK closure remains unresolved.

## Microsoft Runtime

The local Visual Studio 2022 redist tree provides the required VC143 CRT and OpenMP files, and `vc_redist.x64.exe` is `25,635,768` bytes for the installed `14.44.35211.0` toolset. This installer is not part of either the `141,536,768`-byte official-package total or the `58,520,064`-byte source-built total.

If the candidate is ever distributed:

1. Treat the latest supported Microsoft `vc_redist.x64.exe` as an installer prerequisite instead of copying arbitrary runtime DLLs beside the application.
2. Verify that the distributor has a valid Visual Studio license and follows the applicable Visual Studio license terms and REDIST list.
3. Record prerequisite detection, installer version, hash, unattended command, restart behavior, and clean-host evidence.
4. Do not redistribute debug or `debug_nonredist` files.

Microsoft recommends central VC Redistributable deployment so runtime security and servicing updates remain independent from the application package.

## Distribution Gate

Open3D product distribution remains blocked until all items are checked:

- [x] Exact Open3D source tag commit, source archive hash, and non-GUI Windows configure options are recorded; the configuration regenerates `Open3D.sln` locally.
- [x] A recovered Release source build/install completes, its 873 installed paths, sizes, and hashes are recorded, and the source-built probe matches official output in the 33-run robustness matrix.
- [x] An independent clean single-shot Release build completes with a preserved warning inventory and no interrupted-output recovery; its install contract and registration behavior match the recovered and official runtimes.
- [ ] Enabled static and dynamic dependencies, versions, source URLs, licenses, modifications, and hashes are captured in SPDX or CycloneDX form. A schema-valid direct-evidence candidate exists, but unresolved Assimp and prebuilt BoringSSL/MKL/VTK provenance prevent this gate from passing.
- [ ] `THIRD-PARTY-NOTICES.txt` is generated from that exact dependency manifest, not from binary string guesses.
- [ ] The release bundle includes Open3D MIT text, oneTBB Apache-2.0 text and applicable notices, and every enabled dependency's required attribution.
- [ ] Microsoft VC/OpenMP prerequisite handling follows the applicable REDIST terms and passes on a clean Windows host.
- [ ] A clean host without the prerequisite fails with a controlled diagnostic; installation followed by the same registration probe passes.
- [ ] Viewer DLL, Shell, Runner, installer, update, and uninstall impacts are measured independently from the existing SharpGL bundle.
- [ ] Owner/legal approval is recorded before any public or commercial binary publication.

Until this gate passes, keep all Open3D binaries under ignored `artifacts/`, exclude them from CI/release assets, and do not add them to the Viewer DLL manifest.

## Sources Checked

- Open3D `0.19.0` MIT license: https://github.com/isl-org/Open3D/blob/v0.19.0/LICENSE
- Open3D `0.19.0` third-party inventory: https://github.com/isl-org/Open3D/tree/v0.19.0/3rdparty
- Open3D dependency build logic: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/find_dependencies.cmake
- oneTBB `2021.12.0` Apache-2.0 license: https://github.com/uxlfoundation/oneTBB/blob/v2021.12.0/LICENSE.txt
- Microsoft Visual Studio 2022 redistribution list: https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution
- Microsoft Visual C++ redistribution guidance: https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files?view=msvc-170
