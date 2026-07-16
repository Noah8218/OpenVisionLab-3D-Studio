# Open3D Candidate SBOM Evidence

Checked: 2026-07-16

## Decision

`open3d-0.19.0-nongui-windows-candidate.cdx.json` is a CycloneDX `1.6` candidate for the independently clean Open3D `0.19.0` non-GUI Windows x64 Release build. It is useful dependency evidence, but it is not a distribution approval or a complete third-party notice source.

Keep Open3D outside the product dependency graph until the unresolved transitive provenance, Microsoft prerequisite, clean-host, product impact, and owner/legal gates in `OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md` pass.

## Scope

The candidate contains:

- one metadata subject for Open3D source commit `1e7b17438687a0b0c1e5a7187321ac7044afe275`;
- 27 direct component records derived from 28 Release external-project references, with Qhull's C and C++ projects represented as one component;
- 25 directly observed support/transitive records: DirectX-Headers, DirectXMath, seven exact VTK child components, libzmq wepoll, 12 compiler-proven Assimp `contrib` records, and the three exact PyPI wheel inputs used to assemble the oneMKL archive;
- source/prebuilt archive hashes where an exact downloaded archive exists;
- local license-file hashes and explicit Open3D modifications where observed;
- an explicit dependency graph and unresolved-provenance properties.

It intentionally excludes inactive source-tree dependencies and does not infer components from binary strings. Microsoft VC143 CRT and OpenMP are deployment prerequisites, not resolved SBOM components, until the clean-host prerequisite gate is designed.

## Evidence Method

1. Parse `artifacts/o3d-clean/b/cpp/open3d/Open3D.vcxproj` for Release x64 link inputs and project references.
2. Confirm external-project completion stamps and `USE_SYSTEM_*` values from the clean CMake build.
3. Hash the exact archives in the Open3D `3rdparty_downloads` cache and the exact license files in the source/build trees.
4. Record Open3D patches and compatibility copies separately from upstream archive hashes.
5. Reduce Assimp with its Release `vcxproj`, compiler dependency `tlog`, source-to-object map, built libraries, and runtime importer/exporter registry.
6. Reduce oneMKL with the official tagged recipe, exact PyPI wheel identities and RECORD files, archive/install payload hashes, and observed Release link inputs.
7. Reduce VTK with its exact `9.1.0` source, Open3D package recipe, archive/install payload identity, installed CMake target graph, actual Open3D Release link inputs, packaged library directives, and exact child-component license files.
8. Preserve unresolved provenance instead of converting an archive name or license directory into an unsupported completeness claim.

Observed build contract:

```text
Release final link inputs: 60
External project references: 28
Candidate direct components: 27
Observed support/transitive components: 25
Candidate component records: 52
Open3D.dll adjacent non-system runtime: tbb12.dll
```

## Assimp Compiled Closure

The fixed clean Release build downloads official Assimp tag `v5.4.2` at commit `ddb74c2bbdee1565dda667e85f0c82a0588c8053`; both its project and runtime report `5.4.1`. The generated project, compiler tracking, and object tree agree on all `232/232` Assimp sources. Bundled zlib agrees on `15/15` source/object mappings. A probe linked against both static libraries reports 48 importers and 22 exporters identically in two runs; M3D and C4D are absent. Omitting `zlibstatic.lib` produces 13 zlib-related unresolved-symbol diagnostics and final `LNK1120`, proving that edge in the actual link contract.

| Compiler-read component | Version identity used | Files | License |
| --- | --- | ---: | --- |
| Clipper | `6.4.2` | 2 | BSL-1.0 |
| Open3DGC | Assimp `v5.4.2` vendored snapshot | 29 | MIT |
| OpenDDL Parser | `0.4.0` | 13 | MIT |
| Poly2Tri | Assimp `v5.4.2` modified vendored snapshot | 12 | BSD-3-Clause |
| pugixml | `1.13`, header-only mode | 3 | MIT |
| RapidJSON | `1.1.0`, compiled include subset | 29 | MIT |
| stb_image | `2.29` | 1 | MIT OR Unlicense |
| MiniZip | `1.1`, `NOUNCRYPT` | 4 | Zlib |
| UTF8-CPP | Assimp `v5.4.2` vendored snapshot | 4 | BSL-1.0 |
| kuba--zip | `0.3.0` | 2 | Unlicense |
| miniz | compiled header reports `3.0.0` | 1 | MIT OR Unlicense |
| zlib | `1.2.13` | 25 | Zlib |

These records close the actual fixed-build compile closure. Exact independent upstream revisions and Assimp-side modification deltas for the vendored snapshots, especially Poly2Tri, remain outside this claim.

## oneMKL Payload Provenance

The official Open3D `v0.19.0` `mkl.cmake` is byte-identical to the audited local recipe at SHA-256 `6d50fb7ec09eb482d26c46511af353067285980adb08d7f5ce24c2e3433f2ce1`. It downloads three Windows AMD64 wheels with `--no-deps` and removes dynamic `*_dll.lib` import libraries before packaging:

| Wheel | Bytes | SHA-256 | RECORD | Selected payload |
| --- | ---: | --- | ---: | ---: |
| `mkl_include-2024.1.0-py2.py3-none-win_amd64.whl` | 1,259,074 | `99743a54abb8e2e440b097e3333fb939414f63aa06404f069a287a1cc68b9509` | 149/149 | 145/145 |
| `mkl_devel-2024.1.0-py2.py3-none-win_amd64.whl` | 15,190,087 | `7a01a43194101984f6fe02ef73ea17771675a3c56a47c435bb09a44c330ae2dc` | 32/32 | 16/28; 12 `*_dll.lib` files excluded |
| `mkl_static-2024.1.0-py2.py3-none-win_amd64.whl` | 220,751,472 | `d30476fb142ea72d7030a3be8a4b30fa4f63107cf6713bf9c5947f3a3256c627` | 22/22 | 18/18 |

The 191-file wheel payload union minus those exact 12 exclusions yields all 179 Open3D archive files. Every archive file and every installed copy matches its wheel source; the payload-set SHA-256 is `20e01cb0fbf223eac87cc34ec74db24009afebe6ac3cb183feb518d7f2a758b5`. The four observed Release link inputs (`mkl_intel_ilp64.lib`, `mkl_core.lib`, `mkl_sequential.lib`, and `mkl_tbb_thread.lib`) all come from `mkl-static`. Two canonical reassemblies with fixed order, timestamp, permissions, and compression are byte-identical at `223,567,525` bytes and SHA-256 `cd6ee11a7fc67092266c397a23eb323cc60035f72503bb0ef370c8811a698f4f`; both reproduce the original payload set. Their container hash intentionally differs from Open3D's ZIP because its packaging metadata and tool invocation are undocumented.

The Open3D source license file (`15,591` bytes, SHA-256 `c2a11e99bbb46a34565a04d28280a386f90d9b1fdd5db49c29336571224f9e25`) and all three wheel license files (`4,105` bytes, SHA-256 `7721633d0ddff43fae25ebfd405f8166a0ce730cbcec44f2f3ad9d5eb8ac9a6f`) are not byte-identical. Payload provenance is closed, but final Intel notice selection and legal approval are not.

## VTK Source And Package Closure

The fixed Open3D archive is `vtk_9.1_win.tar.gz`, `175,647,944` bytes, SHA-256 `6ee09115d23ec18d6d01d1e4c89fa236ec69406d8ba8cc1b8ec37c4123b93caa`. Open3D's package recipe fixes VTK source `9.1.0` at SHA-256 `8fed42f4f8f1eb8083107b68eaa9ad71da07110161a3116ad807f43e5ca5ce96`; the official VTK tag peels to commit `285daeedd58eb890cb90d6e907d822eea3d2d092`. The package recipe applies no patch command and archives only `vtk/include`, `vtk/lib`, and `vtk/share`.

The archive has `1,195` members: `1,156` files, 39 directories, and `1,315,696,847` uncompressed file bytes. Every archive file matches the clean Open3D install, `1,156/1,156`, and the canonical ordered payload-set SHA-256 is `aae498f1dea80dde26731dddf15484dd0fd4de38cafa4066455087579cf46a02`. The final Open3D Release project names 14 VTK libraries. The installed VTK CMake graph expands those starts to 20 reachable VTK targets, including all 16 packaged Release static libraries and seven third-party child components. `vtkCommonComputationalGeometry` and `vtkfmt` are in the package graph but are not explicit final Open3D link inputs; the SBOM records archive/build closure, not proof that every static-library object entered `Open3D.dll`.

| Child component | Exact identity | Installed role | License |
| --- | --- | --- | --- |
| ExprTk | `2.71`, commit `19348fd0fde5e545279ad87b7ab7d26066c1f4a1` | `VTK::exprtk` interface target | MIT |
| fmt | `8.0.1`, commit `0b94b400970a4ef95f73970d1f720fbc8817c2f1` | `VTK::fmt`, Release and Debug static libraries | MIT |
| kissfft | 2021-04-29 snapshot, commit `3c952a293ae7b2dfd773db9eff76ceb9bff58dc6` | explicit Release link input | BSD-3-Clause |
| KWIML | 2018-02-01 snapshot, commit `a079afc646f46b81686676bec91fb0a8e3799e4a` | `VTK::kwiml` interface target | BSD-3-Clause |
| pugixml | `1.11.4`, commit `02ffda89cb14f98f9ddde60ebca64ab434b6f877` | explicit Release link input | MIT |
| utf8cpp | `2.3.4`, commit `925e289e86ae5465991485a3d4aaaeff94652018` | `VTK::utf8` interface target | BSL-1.0 |
| KWSys | 2021-09-15 snapshot, commit `2c4a83304e08c3e5e17d400315d6f1253f1f7bbc` | explicit Release link input | BSD-3-Clause |

All eight packaged VTK/child license files match the corresponding VTK `9.1.0` source files byte-for-byte. This closes source identity, the documented no-patch recipe, fixed archive payload, installed target/component closure, and packaged license provenance.

It does not close historical binary reproducibility. The official workflow says `windows-2019`, `Visual Studio 16 2019`, x64, dynamic CRT, and Release plus Debug. In contrast, all 30 C++ static libraries record `_MSC_VER=1900`; Release records `MD_DynamicRelease`, Debug records `MDd_DynamicDebug`, and the two C-only kissfft libraries have no MSVC mismatch directive. Microsoft documents `_MSC_VER=1900` as the Visual Studio 2015 compiler generation. The asset predates Open3D adoption commit `405622a0acdaf70432236c0fadeeaa945e4d3e3c`, and no retained Actions run exposes the exact compiler invocation. The workflow is therefore a documented recipe, not proof of how this asset was compiled.

**2026-07-16 correction:** the `_MSC_VER=1900` directive is not a compiler-version conflict. The same marker is emitted by local VS2022 `yvals.h`; the legacy package's generated `vtkConfigureDeprecated.h` directly records Visual Studio 2019 MSVC `14.29.30133`. A no-patch current Release rebuild matches all legacy Release path names, 22/22 VTK target contracts, all 16 directive sets, and a link/run smoke, while omitting Debug and retaining different library hashes. Historical byte identity and all distribution gates remain open. See `docs/OPENVISIONLAB_3D_VTK_CONTROLLED_REBUILD_20260716.md`.

## Recorded Modifications

| Component | Open3D-side change | SHA-256 |
| --- | --- | --- |
| JsonCpp `1.9.4` | `0001-optional-CXX11-ABI-and-MSVC-runtime.patch` | `227c717971e444a76d67c51e71179204543294fe08ae3a4eeafb945effdb361c` |
| zlib `1.2.13` | `0001-patch-zlib-to-enable-unzip.patch` | `43dff318c107f2751ab81ee8057891bdbf3dd54a315ec5028eab1d19e7648506` |
| UVAtlas `may2022` | Copy Open3D's compatibility `sal.h` into the UVAtlas source | `8d1acb5784aeb2a3fb0bafe5ed9a0854cabdffe0009f693a947e5164e35adb06` |

## Unresolved Items

- Assimp's fixed-build compile closure is now reduced to objects and runtime registration. Exact upstream revisions/modification deltas for its vendored snapshots and final notice review remain unresolved.
- BoringSSL now resolves to upstream commit `edfe4133d28c5e39d4fce6a2554f3e2b4cafc9bd`, and the official Open3D `v0.19.0` Windows build/package script is recorded byte-for-byte. A 2026-07-16 controlled-rebuild preflight finds VS2022/CMake/MSVC `14.44.35207` but no Perl, Go, or NASM on the developer command path. The official script explicitly requires those tools, so no substitute or partial build was attempted. `docs/OPENVISIONLAB_3D_BORINGSSL_CONTROLLED_REBUILD_PLAN_20260716.md` fixes the approved-run comparison contract. The prebuilt archive's historical compiler/toolchain, independent binary reproduction, and archive-to-source modification equivalence remain unresolved.
- oneMKL input and payload provenance is resolved for this fixed archive. Final notice review must explicitly reconcile the different Open3D-source and wheel license texts; the canonical ZIP does not claim the undocumented original container metadata.
- VTK `9.1.0` source identity, documented no-patch package recipe, archive/install payload, seven-child target closure, and 8/8 license matches are resolved. The marker interpretation is resolved; exact historical compiler invocation and source-to-binary reproducibility remain unresolved.
- VTK marker interpretation is resolved: `_MSC_VER=1900` is an ABI-family marker, not evidence that contradicts the recorded VS2019 compiler path. The controlled Release rebuild and a same-source `USE_SYSTEM_VTK=ON` Open3D runtime check provide package and local runtime compatibility evidence; exact historical byte reproduction, Debug reconstruction, and final distribution approval remain unresolved.
- libzmq `4.3.3` is statically linked under LGPLv3 terms with the ZeroMQ static-linking exception, and its Windows build includes BSD-2-Clause wepoll. Final notices must preserve both.
- `THIRD-PARTY-NOTICES.txt` must not be generated as final evidence until these gaps are closed.

## Validation

The candidate should pass all of the following before it is updated in a future session:

```powershell
$bom = Get-Content -Raw docs\open3d-0.19.0-nongui-windows-candidate.cdx.json | ConvertFrom-Json
if ($bom.specVersion -ne '1.6' -or $bom.components.Count -ne 52) { exit 1 }
$refs = @($bom.metadata.component.'bom-ref') + @($bom.components.'bom-ref')
if (($refs | Sort-Object -Unique).Count -ne $refs.Count) { exit 1 }
$dependencyRefs = @($bom.dependencies.ref)
if (@($refs | Where-Object { $_ -notin $dependencyRefs }).Count -ne 0) { exit 1 }
$dependencyTargets = @($bom.dependencies.dependsOn | Where-Object { $_ })
if (@($dependencyTargets | Where-Object { $_ -notin $refs }).Count -ne 0) { exit 1 }
```

Validate the JSON against the official CycloneDX `1.6` JSON schema rather than accepting JSON parse success as schema evidence. The reference schema is maintained in the [CycloneDX specification repository](https://github.com/CycloneDX/specification/tree/1.6/schema).

Current verification result:

```text
cyclonedx-1.6-schema=PASS
components=52
dependencies=53
component-archive-hashes=23/23 exact-set-match
root-source-archive-hash=PASS
license-evidence-records=54/54 exact-hash-match
license-evidence-unique-hashes=50
modification-hashes=3/3 exact-set-match
boringssl-full-commit=PASS
boringssl-open3d-build-script-tag-match=PASS
boringssl-open3d-cmake-tag-match=PASS
boringssl-prebuilt-archive-hash=PASS
assimp-release-source-object-mappings=232/232
assimp-bundled-zlib-source-object-mappings=15/15
assimp-contrib-directories/components=11/12
assimp-runtime-registry=48 importers/22 exporters/two-run-identical
assimp-missing-zlib-link=13 unresolved diagnostics/LNK1120
mkl-wheel-identities-and-record-integrity=3/3
mkl-archive-and-install-payload-hashes=179/179
mkl-release-link-inputs=4/4
mkl-canonical-reassembly=2/2 byte-identical
vtk-source-archive-hash=PASS
vtk-archive-install-payload-hashes=1156/1156
vtk-open3d-explicit-link-inputs=14
vtk-reachable-targets/static-libraries/child-components=20/16/7
vtk-source-license-matches=8/8
vtk-library-directives=32/32; 30 C++ _MSC_VER=1900; MD/MDd
unresolved-provenance-records=4
```

Ignored local evidence. The unsuffixed SBOM reports preserve the 33-component archive/hash baseline; the `-assimp-closure` reports preserve the 45-component Assimp checkpoint, the MKL reports preserve the 48-component checkpoint, and the VTK reports validate the current 52-component graph:

```text
artifacts/o3d-clean/open3d-release-link-evidence.json
artifacts/o3d-clean/sbom-schema-validation.txt
artifacts/o3d-clean/sbom-candidate-verification.txt
artifacts/o3d-clean/sbom-schema-validation-assimp-closure.txt
artifacts/o3d-clean/sbom-candidate-verification-assimp-closure.txt
artifacts/o3d-sbom-schema/bom-1.6.schema.json
artifacts/dependency-candidates/boringssl-provenance-20260716/boringssl-provenance.txt
artifacts/dependency-candidates/assimp-closure-20260716/assimp-closure.json
artifacts/dependency-candidates/assimp-closure-20260716/assimp-registry-run1.txt
artifacts/dependency-candidates/assimp-closure-20260716/assimp-registry-run2.txt
artifacts/dependency-candidates/assimp-closure-20260716/assimp-missing-zlib-link.txt
artifacts/dependency-candidates/mkl-provenance-20260716/mkl-provenance.json
artifacts/dependency-candidates/mkl-provenance-20260716/mkl-canonical-reassembly.json
artifacts/dependency-candidates/mkl-provenance-20260716/mkl-sbom-crosscheck.txt
artifacts/dependency-candidates/vtk-provenance-20260716/vtk-provenance.json
artifacts/dependency-candidates/vtk-provenance-20260716/vtk-archive-payload.json
artifacts/dependency-candidates/vtk-provenance-20260716/vtk-library-directives.json
artifacts/dependency-candidates/vtk-provenance-20260716/vtk-sbom-crosscheck.txt
artifacts/dependency-candidates/vtk-provenance-20260716/target-probe-build/vtk-targets.txt
```

## Gate Effect

This closes the first-pass inventory, BoringSSL exact-source/documented-recipe, fixed-build Assimp compiled closure, fixed oneMKL wheel/payload provenance, VTK source/recipe/payload/transitive-closure subtasks, and the technical clean-host VC/OpenMP prerequisite gate. The distribution-gate item for a complete SPDX/CycloneDX dependency manifest remains unchecked because Assimp vendored provenance, BoringSSL and VTK binary/toolchain reproducibility, final notice scope, and owner/legal review are still incomplete. The next technical task after explicit prerequisite-tool approval is the controlled BoringSSL rebuild; it is not a reason to integrate Open3D into the product now.
