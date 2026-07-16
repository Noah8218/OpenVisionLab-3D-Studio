# Open3D Windows Distribution Audit

Checked: 2026-07-16

## Decision

Do not add the official Open3D `0.19.0` Windows development binaries to an OpenVisionLab product or Viewer release bundle.

The package is valid for the current local registration prototype, but it is not distribution-ready evidence. The inspected binary package contains no license, notice, SBOM, dependency lock, CMake cache, or compile-command file. Its public CMake export identifies Eigen and the dynamic oneTBB dependency, while the monolithic `Open3D.dll` contains additional private/static components that the package does not enumerate. A same-tag local source build now provides reproducible configuration, recovered and independent clean build/install evidence, complete install hashes, and a smaller runtime. A CycloneDX `1.6` candidate now records 27 direct components plus 25 observed support/transitive components, exact archive and license hashes where available, and three Open3D-side modifications. Assimp's fixed-build compiled closure, archive-to-build source snapshot, and source-backed candidate notice manifest now cover Assimp core plus all 12 compiler-read closure components without collapsing Open3DGC's MIT and BSD-2-Clause notice sources. The miniz build header's MIT and Unlicense source text is verified, but an official reachable-object audit intentionally leaves original raw-byte identity `Unresolved`. BoringSSL and VTK binary/toolchain reproducibility, the final notice bundle, REDIST/legal disposition, product integration impact analysis, and owner/legal approval remain unresolved. Exact attribution and redistribution obligations therefore cannot be reconstructed reliably enough for publication.

**2026-07-16 correction:** the prior claim that the documented VS2019 workflow conflicts with `_MSC_VER=1900` is withdrawn. The legacy archive's `vtkConfigureDeprecated.h` records VS2019 MSVC `14.29.30133`, while the current VS2022 STL header intentionally emits the same marker. A current no-patch Release candidate now proves VTK package-contract compatibility, and a same-source `USE_SYSTEM_VTK=ON` Open3D build proves local registration-runtime compatibility. Neither result proves historical byte identity or distribution readiness. See `docs/OPENVISIONLAB_3D_VTK_CONTROLLED_REBUILD_20260716.md` and `docs/OPENVISIONLAB_3D_OPEN3D_VTK_CANDIDATE_RUNTIME_20260716.md`.

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

`docs/open3d-0.19.0-nongui-windows-candidate.cdx.json` is a schema-valid CycloneDX `1.6` direct-evidence candidate, not a complete distribution manifest. It contains 52 component records: 27 direct components reduced from 28 external-project references and 25 observed support/transitive components. All 23 downloaded component-archive hashes and all three exact oneMKL wheel hashes match; the root source archive, 50 unique license evidence hashes, and all 3 recorded Open3D-side modification hashes also match local files. Four unresolved-provenance records deliberately keep the distribution gate open.

BoringSSL is now tied to full upstream commit `edfe4133d28c5e39d4fce6a2554f3e2b4cafc9bd`. The local Open3D `v0.19.0` `build_boringssl.ps1` and `boringssl.cmake` match the official tagged files byte-for-byte with SHA-256 `f2f24801a5e69b7dd294332afbbb4270e62c71c71ab03322d82511f8561ce50e` and `8555bd0e4476c8cb015fd0fdc96d356c47d8afd8962cf36c522c1ab5cc205bf2`. The build script checks out that commit, builds Release and Debug `ssl`/`crypto`, packages the outputs, and contains no patch command. The cached 168-entry Windows AMD64 archive is `6,265,404` bytes and matches Open3D's declared SHA-256 `fd538d545990a4657ee2b22c444e0baf61edaa1609f84cdfc9217659c44988c4`. This resolves exact source and documented recipe only; the historical compiler/toolchain, independent binary reproduction, and archive-to-source modification equivalence remain unproven.

The 2026-07-16 controlled-rebuild preflight confirms that the local VS2022 developer environment exposes CMake and MSVC `14.44.35207`, but the official script's Windows prerequisites `perl`, `go`, and `nasm` are absent. No substitute recipe, partial build, or system installation was attempted. `docs/OPENVISIONLAB_3D_BORINGSSL_CONTROLLED_REBUILD_PLAN_20260716.md` records the fixed inputs, approved-run prerequisite, two-run archive/topology/directive/link comparison, and nonclaims. This is a precise blocked preflight, not BoringSSL binary reproducibility evidence.

Assimp official tag `v5.4.2` resolves to commit `ddb74c2bbdee1565dda667e85f0c82a0588c8053`, while the extracted project and runtime both report `5.4.1`. The CMake-selected official ZIP is `55,769,830` bytes with SHA-256 `03e38d123f6bf19a48658d197fd09c9a69db88c076b56a476ab2da9f5eb87dcc`; its `2,940` files match all `2,940` build-source paths, lengths, and SHA-256 values with zero differences, and the generated update/patch commands are empty. The fixed clean Release project, compiler dependency tracking, and object tree agree on `232/232` Assimp source/object mappings and `15/15` bundled-zlib mappings. The actual compiler-read closure has 11 `contrib` directories represented by 12 SBOM records. A two-run-identical registry probe reports 48 importers and 22 exporters; M3D and C4D are absent. Removing `zlibstatic.lib` from the probe link yields 13 zlib-related unresolved diagnostics and final `LNK1120`. This closes the fixed source snapshot and compiled closure, Poly2Tri's fixed original-source content identity/current path history, and Clipper's fixed release/import/current-tag provenance, not other vendored snapshots or final legal disposition. See `docs\OPENVISIONLAB_3D_ASSIMP_SOURCE_SNAPSHOT_IDENTITY_20260716.md`, `docs\OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md`, `docs\OPENVISIONLAB_3D_ASSIMP_POLY2TRI_DELTA_ATTRIBUTION_20260716.md`, and `docs\OPENVISIONLAB_3D_ASSIMP_CLIPPER_PROVENANCE_20260716.md`.

Poly2Tri original-source content and current path-history identity now pass. The official Google Code source archive SHA-256 `02092826bf5c539ed5a904386a2439eb608cc4d1d008adc7034ae3a2230a05bb` was queried with artifact-local Mercurial `7.2.3`; its original revision `5de9623d6a500d8b0ad3126a48957c5152c15ad2` and the candidate Git commit `greenm01/poly2tri@99927efa011013154460ca4cb06bcd64d4768edb` export `35/35` equal ordinal files and raw bytes. The equal raw canonical manifest SHA-256 is `c8a0845fb300289b219e3bf06d07180c4d33ca18609741b6513f72aad29622e7`, and an invalid candidate revision fails closed. Official `v5.4.2` tree/history evidence then records 15/15 initial/current blobs, 14 direct changed paths, a 1,407/1,328 net line delta, 28 complete official history entries, and 14/14 path coverage by 27 post-initial commits. This proves fixed original source content and path-level history only: the candidate mirror has no verified signature/ownership mapping, final-line blame remains intentionally unclaimed, and final notice disposition remains open. See `docs\OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md`, `docs\OPENVISIONLAB_3D_ASSIMP_POLY2TRI_DELTA_ATTRIBUTION_20260716.md`, and both `scripts\verify-assimp-poly2tri-*.ps1` verifiers.

Clipper `6.4.2` fixed release/import/current-tag provenance now passes. The official Clipper archive SHA-256 is `a14320d82194807c4480ce59c98aa71cd4175a5156645c4e2b3edd330b930627`; it records an exact `7/6` archive-to-Assimp-import delta at `aa1996e1437777af62aac549d55591f1849f90de` and a bounded `4/4` import-to-current delta at `bb9101ae9eb2938cadfeadd4690bbdf910ca57f4`. The captured `v5.4.2` tag source and actual clean-build source are raw-byte-identical at Git blob `c0a8565bb98568dcca4a5350ca52fa08152bea51`. This does not prove an upstream VCS signature, individual-line authorship, notice scope, binary reproducibility, or distribution approval. See `docs\OPENVISIONLAB_3D_ASSIMP_CLIPPER_PROVENANCE_20260716.md` and `scripts\verify-assimp-clipper-provenance.ps1`.

stb_image `v2.29` fixed upstream-commit/current-build provenance now passes. Captured upstream tags and releases are empty, so the fixed basis is upstream commit `0bc88af4de5fb022db643c2d8e549a0927749354`, not a release tag. Its source, Assimp `3ff7851ff9ad3004bb934fedaf657ffad0572573`, the `v5.4.2` tag source, and the actual clean-build input are raw-byte-identical at SHA-256 `c54b15a689e6a1f32c75e2ec23afa442e3e0e37e894b73c1974d08679b20dd5c` and blob `a632d543510ebf4410f124369b07a303e1d096d6`. CMake and `Assimp.cpp` evidence confirm that the header is compiler read. This does not prove an upstream release/tag, complete upstream history, Assimp wrapper semantics, notice scope, binary reproducibility, or distribution approval. See `docs\OPENVISIONLAB_3D_ASSIMP_STB_PROVENANCE_20260716.md` and `scripts\verify-assimp-stb-provenance.ps1`.

The official Open3D `v0.19.0` oneMKL recipe matches the audited local file byte-for-byte. Its exact `mkl-include`, `mkl-devel`, and `mkl-static` `2024.1.0` Windows wheel hashes and RECORD integrity pass. Their 191 payload files minus the recipe's 12 `*_dll.lib` exclusions match all 179 archive files and all 179 installed files; the four observed Release link inputs are present and wheel-attributed. Two normalized reassemblies are byte-identical and retain the original payload-set SHA-256. This closes fixed payload provenance, not the undocumented original ZIP container metadata or final Intel notice/legal disposition.

The Open3D-hosted VTK archive now resolves to exact VTK `9.1.0` source commit `285daeedd58eb890cb90d6e907d822eea3d2d092`, the exact source archive hash, and a no-patch Open3D package recipe. All `1,156/1,156` archive files match the clean install. Fourteen explicit Open3D Release link inputs expand through the installed package graph to 20 reachable VTK targets, all 16 packaged Release static libraries, and seven exact child components; all 8 packaged license files match VTK source. The documented Windows recipe uses Visual Studio 16 2019, but all 30 C++ static libraries record `_MSC_VER=1900` and dynamic Release/Debug CRT directives. The asset predates its Open3D adoption commit and no retained Actions run resolves the contradiction, so exact historical compiler invocation and source-to-binary reproducibility remain open.

The controlled 2026-07-16 Release rebuild confirms the interpretation above: the legacy `vtkConfigureDeprecated.h` carries VS2019 MSVC `14.29.30133`; a current VS2022 v143 rebuild produces all 16 legacy Release path names, 22/22 VTK import-target contracts, matching CRT/directive sets, and a passing direct link/run smoke. The candidate is `31,473,977` bytes with SHA-256 `a11a803164e4feef2ac4a223235a32c6ceed8f7a44a13c8103a9a1a0d907e09d`; it intentionally lacks the 16 Debug libraries and differs in every Release library hash. A same-source Open3D `0.19.0` Release build against that candidate now completes locally, matches all 873 clean-install paths, has the same 29 dynamic dependencies and 16,000 ordinal/name exports, and preserves DemoICP plus 33-run robustness behavior against the independent clean build. This removes marker-based toolchain conflict and Open3D runtime compatibility as blockers, but does not prove historical byte identity or full command/environment reproducibility. See `docs/OPENVISIONLAB_3D_VTK_CONTROLLED_REBUILD_20260716.md` and `docs/OPENVISIONLAB_3D_OPEN3D_VTK_CANDIDATE_RUNTIME_20260716.md`.

See `docs/OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md` for scope, method, validation, and blockers. Do not generate final notices from this candidate while other Assimp vendored-component revision/modification provenance, BoringSSL and VTK binary/toolchain reproducibility, and the oneMKL source-versus-wheel license-text disposition remain open.

**2026-07-16 kuba--zip correction:** the preceding Assimp closure summary is superseded for this bounded component. CMake's `0.3.0` version metadata is not source identity. The three compiler inputs pass from public `kuba--/zip v0.3.1` commit `550905d883b29f0b23e433fdb97f6299b628d4a9`, through Assimp import `83d7216726726a07e9e40f86cc2322b22fec11fa`, to current `v5.4.2` and the actual clean-build files. CRLF-normalized deltas and the complete fixed-range post-import history cover one `miniz.h` and two `zip.c` commits. This is bounded source/delta evidence only, not release-state proof, upstream signatures, independent miniz provenance, notice scope, binary reproducibility, or distribution approval. See `docs\OPENVISIONLAB_3D_ASSIMP_KUBAZIP_PROVENANCE_20260716.md` and `scripts\verify-assimp-kubazip-provenance.ps1`.

**2026-07-16 pugixml correction:** Assimp's three effective header-only inputs now pass from public `zeux/pugixml v1.13` commit `a0e064336317c9347a91224112af9933598714e9`, through import `62cefd5b275628ff97a77d0cd9220e1c35794a3f`, to `v5.4.2` and the actual clean-build files. `pugixml.cpp` and `pugixml.hpp` match the import exactly after CRLF normalization; `pugiconfig.hpp` has the one explicit header-only configuration delta. The only fixed-range follow-up is the `2/2` per-file 2024 copyright update. The standalone CMake `VERSION 1.9` value is stale metadata, not the source version. This resolves one bounded source/delta and compiler-input-chain slice only; upstream signature/history, notice scope, binary reproducibility, distribution approval, product integration, and Viewer/Runner parity remain open. See `docs\OPENVISIONLAB_3D_ASSIMP_PUGIXML_PROVENANCE_20260716.md` and `scripts\verify-assimp-pugixml-provenance.ps1`.

**2026-07-16 UTF8-CPP correction:** the fixed four-header compiler-read subset now passes from public `nemtrif/utfcpp v3.2.3` commit `79835a5fa57271f07a90ed36123e30ae9741178e`, through Assimp update `ce59d49dd9ce93ccf8585f78c70e58cb0e5d4961`, to `v5.4.2` and the actual clean-build source. All four blobs are identical and their fixed post-update path history is empty. The non-Hunter include directory, header chain, and existing four-file compiler-read closure are checked. This resolves fixed source identity for only that four-file subset, not optional UTF8-CPP headers, upstream signature/history, notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_UTF8CPP_PROVENANCE_20260716.md` and `scripts\verify-assimp-utf8cpp-provenance.ps1`.

**2026-07-16 MiniZip correction:** the fixed four-file compiler-read subset now passes from public `madler/zlib v1.3.1` commit `51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf`, through Assimp update `64d88276ef7117c09165e468dbb9acd999e324ac`, to `v5.4.2` and the actual clean-build source. All four blobs are identical and their fixed post-update path history is empty. Although CMake lists `crypt.h`, source-defined `NOUNCRYPT` precedes the conditional include in `unzip.c`, so it is not preprocessed in this build and is correctly outside the closure. This resolves only zlib-contrib identity for `ioapi.c`, `ioapi.h`, `unzip.c`, and `unzip.h`, not complete MiniZip/Info-ZIP/`crypt.h` provenance, upstream signature/history, notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_MINIZIP_PROVENANCE_20260716.md` and `scripts\verify-assimp-minizip-provenance.ps1`.

**2026-07-16 miniz correction:** the fixed one-file compiler-read subset now passes from public `kuba--/zip v0.3.1` commit `550905d883b29f0b23e433fdb97f6299b628d4a9`, through Assimp PR `#5499`, merge `83d7216726726a07e9e40f86cc2322b22fec11fa`, the one `0d546b3...` post-merge change, `v5.4.2`, and the actual clean-build input. The PR baseline is byte-identical to Kuba; the fixed PR deltas are `2/0` and `4/1`, and the merge-to-current delta is `1/1`. PR head and merge have identical content but no asserted Git ancestry. A separate full official `richgel999/miniz` reachable-object audit sees neither fixed raw blob in `1,368` current objects and records `OriginIdentityStatus=Unresolved`; this does not disprove a historical derivation. The build header contains both observed MIT and Unlicense text, which supports the candidate source-text expression only. This resolves only the Kuba-to-Assimp source/delta/current-build slice, not independent original `richgel999/miniz` source identity/history, notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_MINIZ_PROVENANCE_20260716.md`, `docs\OPENVISIONLAB_3D_ASSIMP_MINIZ_ORIGIN_BOUNDARY_20260716.md`, and both corresponding verifier scripts.

**2026-07-16 OpenDDL Parser correction:** the fixed `13`-file compiler-read subset now passes from public `kimkulling/openddl-parser v0.5.1` commit `ffad343385f550b933c7e498e9bd0a861605102c`, through Assimp baseline `bc7ef58b4947a01f4f7163b47b96ca273473d7eb`, two fixed Assimp deltas, `v5.4.2`, and the actual clean-build source. Eleven inputs remain exact; `OpenDDLCommon.h` changes `12/15` and `OpenDDLParser.cpp` changes `3/1`. The shared static `0.4.0` string is source metadata, not the upstream-tag identity. This resolves only the fixed source/delta/current-build slice, not upstream signature/history, notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_OPENDDL_PROVENANCE_20260716.md` and `scripts\verify-assimp-openddl-provenance.ps1`.

**2026-07-16 zlib correction:** the fixed `25`-file zlib core subset now passes from public `madler/zlib v1.2.13` commit `04f42ceca40f73e2978b50e93806c2a18c1281fc`, through Assimp update `8741da2036cba41cf55fd5805e7a9730a70d2a3a`, `v5.4.2`, and the actual clean-build source. All fixed source blobs match and every checked source path has empty post-import history. The build also generates `zconf.h`; that effective generated header is recorded but intentionally outside the upstream source-identity subset. This resolves only the fixed source/delta/current-build slice, not upstream signature/history, generated-header provenance, notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_ZLIB_PROVENANCE_20260716.md` and `scripts\verify-assimp-zlib-provenance.ps1`.

**2026-07-16 RapidJSON correction:** the fixed `29`-header subset now passes from public `Tencent/rapidjson` post-`v1.1.0` commit `676d99db96e2108724e62342a47e28c8e991ed3b`, through Assimp update `4a3e0e46ac45867c8c8fac9cbcdee3bc30e99f92`, `v5.4.2`, and the actual clean-build source. The `v1.1.0` tag is an ancestor only; it is not substituted for source identity. The Assimp update changes 16 paths from its first parent, and every checked header has empty post-import history. This resolves only the fixed source/delta/current-build slice, not a release-tag identity for the post-tag snapshot, upstream signature/history, notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_RAPIDJSON_PROVENANCE_20260716.md` and `scripts\verify-assimp-rapidjson-provenance.ps1`.

**2026-07-16 Open3DGC correction:** the `29` compiler-read Open3DGC files now pass from public `KhronosGroup/glTF` `mesh-compression-open3dgc` snapshot `7b61d5e065f98058fa12fadfec821546f486d960`, through Assimp import `054820e6ffc03f1a914f2bc688d7f030cf01894b`, to the fixed `v5.4.2` clean-build input. The exact current Assimp delta is bounded to `16` named paths. The source notices require `MIT AND BSD-2-Clause`, because the core is MIT and the arithmetic-codec header/source use BSD-2-Clause text. This resolves only the public carrier-snapshot/import/delta/current-build slice, not historical AMD remote availability, upstream signature/release evidence, final notice scope, binary reproducibility, distribution approval, product integration, or Viewer/Runner parity. See `docs\OPENVISIONLAB_3D_ASSIMP_OPEN3DGC_PROVENANCE_20260716.md` and `scripts\verify-assimp-open3dgc-provenance.ps1`.

## Microsoft Runtime

The local Visual Studio 2022 redist tree provides the required VC143 CRT and OpenMP files, and `vc_redist.x64.exe` is `25,635,768` bytes for the installed `14.44.35211.0` toolset. This installer is not part of either the `141,536,768`-byte official-package total or the `58,520,064`-byte source-built total.

If the candidate is ever distributed:

1. Treat the latest supported Microsoft `vc_redist.x64.exe` as an installer prerequisite instead of copying arbitrary runtime DLLs beside the application.
2. Verify that the distributor has a valid Visual Studio license and follows the applicable Visual Studio license terms and REDIST list.
3. Record prerequisite detection, installer version, hash, unattended command, restart behavior, and clean-host evidence.
4. Do not redistribute debug or `debug_nonredist` files.

Microsoft recommends central VC Redistributable deployment so runtime security and servicing updates remain independent from the application package.

`scripts/verify-open3d-runtime-prerequisites.ps1` now provides a research-only preflight for the separate-process candidate. It requires an explicit minimum runtime version, records the three adjacent probe files and their hashes, rejects any adjacent copy of the four x64 VC/OpenMP runtime DLLs, records the system DLL versions/hashes, checks the x64 VC runtime registry version, and returns exit code `1` with a report when evidence is missing, too old, or bundled beside the probe.

Current-host command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-open3d-runtime-prerequisites.ps1 `
  -RuntimeDirectory artifacts\o3d-clean\probe-build\Release `
  -MinimumRuntimeVersion 14.44.35211.0 `
  -ReportPath artifacts\open3d-runtime-prerequisites_20260716\current-host.txt
```

The current machine passes with installed x64 runtime `14.51.36247.0`, which is newer than the recorded source-build minimum `14.44.35211.0`. A controlled empty system directory and missing registry path return exit code `1` and identify all missing prerequisites; a one-file adjacent `VCOMP140.dll` fixture also returns exit code `1` while the system evidence remains valid. This closes local preflight inventory and controlled diagnostic behavior only. It does not prove an actual clean host, installer execution, restart behavior, servicing, redistribution rights, or product integration.

On 2026-07-16 the Microsoft official latest-x64 permalink resolved to a signed `vc_redist.x64.exe` version `14.51.36247.0`, size `18,731,856` bytes, SHA-256 `843068991daaa1f73ad9f6239bce4d0f6a07a51f18c37ea2a867e9beca71295c`, and `Valid` Microsoft Corporation Authenticode signature. Ignored installer identity evidence is under `artifacts/dependency-candidates/microsoft-vc-redist-20260716`; the installer is not committed or included in a product bundle. Microsoft documents `/install`, `/quiet`, `/norestart`, and `/log` options.

The isolated Windows Sandbox execution described in `docs/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md` now passes the technical clean-host test: pre-install preflight exits `1` with `system=0/4` and no successful probe report, the exact reviewed installer exits `0` without restart, post-install preflight exits `0` with `system=4/4`, and the fixed `0 -> 1` probe JSON matches its baseline after removing only `elapsedMilliseconds`. This closes clean-host prerequisite behavior for that fixed candidate only; it does not approve redistribution or product integration.

`scripts/stage-open3d-clean-host-evidence-bundle.ps1` creates the runbook's self-contained staging payload after validating the fixed candidate, input, baseline, and verifier hashes. A local smoke stages nine files with a manifest and no VC/OpenMP sidecars; a one-byte `Open3D.dll` mutation is rejected before an output directory is created. This proves staging integrity behavior only, not clean-host installation or distribution readiness.

## Distribution Gate

Open3D product distribution remains blocked until all items are checked:

- [x] Exact Open3D source tag commit, source archive hash, and non-GUI Windows configure options are recorded; the configuration regenerates `Open3D.sln` locally.
- [x] A recovered Release source build/install completes, its 873 installed paths, sizes, and hashes are recorded, and the source-built probe matches official output in the 33-run robustness matrix.
- [x] An independent clean single-shot Release build completes with a preserved warning inventory and no interrupted-output recovery; its install contract and registration behavior match the recovered and official runtimes.
- [ ] Enabled static and dynamic dependencies, versions, source URLs, licenses, modifications, and hashes are captured in SPDX or CycloneDX form. A schema-valid direct-evidence candidate exists; the fixed Assimp core plus 12-component compiler closure now has a source-backed candidate notice manifest (`13` entries, `125` compiler paths, `15` notice records) in addition to the bounded source provenance checks. Independent original miniz raw-byte identity remains intentionally `Unresolved` despite observed source-text evidence; BoringSSL and VTK binary/toolchain reproducibility, final notice review, and the remaining non-Assimp dependency evidence still prevent this gate from passing.
- [x] The VTK marker ambiguity is resolved: `vtkConfigureDeprecated.h` identifies the legacy VS2019 `14.29.30133` compiler and a current no-patch Release rebuild reproduces the Release package contract. A same-source `USE_SYSTEM_VTK=ON` Open3D candidate then completed a local Release install and preserved the clean build's 29 dependency, 16,000 export, DemoICP, controlled-failure, and 33-run robustness contracts. Historical byte identity, a Debug rebuild, and full historical build commands/environment remain open.
- [ ] `THIRD-PARTY-NOTICES.txt` is generated from that exact dependency manifest, not from binary string guesses. The source-backed Assimp closure candidate is a verified input, not the final notice file or a legal review.
- [ ] The release bundle includes Open3D MIT text, oneTBB Apache-2.0 text and applicable notices, and every enabled dependency's required attribution.
- [ ] Microsoft VC/OpenMP prerequisite handling follows the applicable REDIST terms and passes on a clean Windows host. The fixed candidate now has technical clean-host evidence; REDIST terms review and redistribution approval remain open.
- [x] A clean host without the prerequisite fails with a controlled diagnostic; installation followed by the same fixed `0 -> 1` registration probe passes. Windows Sandbox evidence records `system=0/4` before installation, `system=4/4` after the reviewed installer, and exact normalized probe parity.
- [ ] Viewer DLL, Shell, Runner, installer, update, and uninstall impacts are measured independently from the existing SharpGL bundle.
- [ ] Owner/legal approval is recorded before any public or commercial binary publication.

Until this gate passes, keep all Open3D binaries under ignored `artifacts/`, exclude them from CI/release assets, and do not add them to the Viewer DLL manifest.

## Sources Checked

- Open3D `0.19.0` MIT license: https://github.com/isl-org/Open3D/blob/v0.19.0/LICENSE
- Open3D `0.19.0` third-party inventory: https://github.com/isl-org/Open3D/tree/v0.19.0/3rdparty
- Open3D dependency build logic: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/find_dependencies.cmake
- Open3D `v0.19.0` BoringSSL Windows build/package script: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/boringssl/build_boringssl.ps1
- Open3D `v0.19.0` BoringSSL archive definition: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/boringssl/boringssl.cmake
- BoringSSL exact upstream commit: https://github.com/google/boringssl/commit/edfe4133d28c5e39d4fce6a2554f3e2b4cafc9bd
- Assimp `v5.4.2` release: https://github.com/assimp/assimp/releases/tag/v5.4.2
- Assimp `v5.4.2` project options: https://github.com/assimp/assimp/blob/v5.4.2/CMakeLists.txt
- Assimp `v5.4.2` importer/contrib build wiring: https://github.com/assimp/assimp/blob/v5.4.2/code/CMakeLists.txt
- Open3D `v0.19.0` oneMKL wheel/package recipe: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/mkl/mkl.cmake
- PyPI `mkl-include 2024.1.0` release metadata: https://pypi.org/pypi/mkl-include/2024.1.0/json
- PyPI `mkl-devel 2024.1.0` release metadata: https://pypi.org/pypi/mkl-devel/2024.1.0/json
- PyPI `mkl-static 2024.1.0` release metadata: https://pypi.org/pypi/mkl-static/2024.1.0/json
- Open3D `v0.19.0` VTK package recipe: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/vtk/vtk_build.cmake
- Open3D VTK archive wrapper and Windows runtime selection: https://github.com/isl-org/Open3D/blob/v0.19.0/3rdparty/vtk/CMakeLists.txt
- Open3D VTK package workflow: https://github.com/isl-org/Open3D/blob/v0.19.0/.github/workflows/vtk_packages.yml
- Open3D VTK archive release and hashes: https://github.com/isl-org/open3d_downloads/releases/tag/vtk
- Open3D VTK package-adoption commit: https://github.com/isl-org/Open3D/commit/405622a0acdaf70432236c0fadeeaa945e4d3e3c
- VTK `9.1.0` release source: https://github.com/Kitware/VTK/releases/tag/v9.1.0
- Microsoft `_MSC_VER` compiler-version guidance: https://learn.microsoft.com/en-us/cpp/overview/compiler-versions?view=msvc-170
- oneTBB `2021.12.0` Apache-2.0 license: https://github.com/uxlfoundation/oneTBB/blob/v2021.12.0/LICENSE.txt
- Microsoft Visual Studio 2022 redistribution list: https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution
- Microsoft Visual C++ redistribution guidance: https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files?view=msvc-170
- Microsoft latest supported Visual C++ Redistributable downloads and compatibility rules: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170
