# Open3D Candidate SBOM Evidence

Checked: 2026-07-16

## Decision

`open3d-0.19.0-nongui-windows-candidate.cdx.json` is a CycloneDX `1.6` candidate for the independently clean Open3D `0.19.0` non-GUI Windows x64 Release build. It is useful dependency evidence, but it is not a distribution approval or a complete third-party notice source.

The fixed Assimp core plus compiler-read `contrib` closure now has a separate source-backed candidate notice manifest. It verifies `13` entries, `125` compiler-read paths, and `15` source notice records against the closure, archive-to-build snapshot, CycloneDX component properties, and current source hashes. It is an input to a future reviewed notice bundle, not that bundle itself. See `docs\OPENVISIONLAB_3D_ASSIMP_CLOSURE_NOTICE_MANIFEST_20260716.md`.

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

The fixed clean Release build downloads official Assimp tag `v5.4.2` at commit `ddb74c2bbdee1565dda667e85f0c82a0588c8053`; both its project and runtime report `5.4.1`. The CMake-selected `v5.4.2.zip` archive now matches the extracted clean-build source tree exactly: `2,940/2,940` ordinal paths, lengths, and SHA-256 values match with zero missing, extra, or modified files, and the generated update/patch commands are empty. The generated project, compiler tracking, and object tree agree on all `232/232` Assimp sources. Bundled zlib agrees on `15/15` source/object mappings. A probe linked against both static libraries reports 48 importers and 22 exporters identically in two runs; M3D and C4D are absent. Omitting `zlibstatic.lib` produces 13 zlib-related unresolved-symbol diagnostics and final `LNK1120`, proving that edge in the actual link contract. See `docs\OPENVISIONLAB_3D_ASSIMP_SOURCE_SNAPSHOT_IDENTITY_20260716.md`.

| Compiler-read component | Version identity used | Files | License |
| --- | --- | ---: | --- |
| Clipper | `6.4.2`, fixed release/import/current-tag provenance | 2 | BSL-1.0 |
| Open3DGC | fixed Khronos `mesh-compression-open3dgc` snapshot/import/current-build provenance | 29 | MIT AND BSD-2-Clause |
| OpenDDL Parser | fixed `v0.5.1` tag/current-build identity; static `0.4.0` source metadata | 13 | MIT |
| Poly2Tri | Assimp `v5.4.2` modified vendored snapshot | 12 | BSD-3-Clause |
| pugixml | fixed `v1.13` tag plus header-only configuration/delta/current build | 3 | MIT |
| RapidJSON | fixed post-`v1.1.0` public snapshot/current-build identity | 29 | MIT |
| stb_image | `2.29`, fixed upstream-commit/current-build identity | 1 | MIT OR Unlicense |
| MiniZip | fixed `zlib v1.3.1` `contrib/minizip` four-file/current-build identity; source-defined `NOUNCRYPT` | 4 | Zlib |
| UTF8-CPP | fixed `v3.2.3` tag/current-build identity for four compiler-read headers | 4 | BSL-1.0 |
| kuba--zip | CMake `0.3.0`; fixed `v0.3.1` tag plus bounded Assimp delta/current build | 2 | Unlicense |
| miniz | fixed `kuba--/zip v0.3.1` to Assimp/current-build identity; original raw-byte identity unresolved after official reachable-object audit; MIT and Unlicense source text observed | 1 | MIT OR Unlicense |
| zlib | fixed `v1.2.13` core/current-build identity; generated `zconf.h` outside source subset | 25 | Zlib |

These records plus the archive-to-build source comparison close the actual fixed-build compile closure and source-snapshot identity. Fixed original-source/current path-history identity for Poly2Tri, fixed release/import/current-tag provenance for Clipper, fixed Khronos Open3DGC snapshot/import/current-build provenance, fixed upstream-commit/current-build identity for stb_image, fixed `v1.13`/header-only/current-build provenance for pugixml, fixed `v3.2.3` four-header/current-build identity for UTF8-CPP, fixed `zlib v1.3.1` four-file/current-build identity for MiniZip, fixed `kuba--/zip v0.3.1` to current-build identity for miniz, fixed OpenDDL Parser `v0.5.1` to current-build identity, fixed zlib `v1.2.13` core/current-build identity, and fixed RapidJSON post-`v1.1.0` public snapshot/current-build identity are also resolved separately. The separate original miniz boundary audit documents non-observation in current official reachable refs but leaves original byte identity `Unresolved`; final notice review remains outside this claim.

Poly2Tri original-source content and current path-history identity now pass. The official Google Code source archive at SHA-256 `02092826bf5c539ed5a904386a2439eb608cc4d1d008adc7034ae3a2230a05bb` was queried with artifact-local Mercurial `7.2.3`; `hg archive` at the patch-referenced `5de9623d6a500d8b0ad3126a48957c5152c15ad2` and `git -c core.autocrlf=false archive` at `greenm01/poly2tri@99927efa011013154460ca4cb06bcd64d4768edb` match `35/35` ordinal paths and raw bytes. Their raw canonical manifest is `c8a0845fb300289b219e3bf06d07180c4d33ca18609741b6513f72aad29622e7`; an invalid Git revision fails closed. Official Assimp `v5.4.2` then supplies a complete 28-entry path history, 15/15 official/local blob-tree agreement, 14 direct changed paths, 1,407/1,328 net line delta, and 14/14 coverage by 27 post-initial commits. This resolves fixed original-revision content equivalence and current path-level history, not Git-mirror ownership/signature, final-line blame, final notices, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md`, `docs\OPENVISIONLAB_3D_ASSIMP_POLY2TRI_DELTA_ATTRIBUTION_20260716.md`, and both `scripts\verify-assimp-poly2tri-*.ps1` verifiers.

Clipper fixed provenance now passes for the compiler-read `6.4.2` component. The official archive SHA-256 `a14320d82194807c4480ce59c98aa71cd4175a5156645c4e2b3edd330b930627`, Assimp import blob `d75974336b34975721598acceac797da15709d2f`, latest path blob `c0a8565bb98568dcca4a5350ca52fa08152bea51`, and current build input form a checked chain. Archive-to-import is `7/6`, import-to-current is `4/4`, and a controlled wrong-delta contract fails closed. This resolves fixed source content and bounded Assimp changes, not an upstream VCS signature, individual-line attribution, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_CLIPPER_PROVENANCE_20260716.md` and `scripts\verify-assimp-clipper-provenance.ps1`.

stb_image fixed provenance now passes for the compiler-read `v2.29` header. The captured upstream tags/releases endpoints are empty, so source identity is bound to upstream commit `0bc88af4de5fb022db643c2d8e549a0927749354` rather than a release. Its source, Assimp update `3ff7851ff9ad3004bb934fedaf657ffad0572573`, tag source, and build input share SHA-256 `c54b15a689e6a1f32c75e2ec23afa442e3e0e37e894b73c1974d08679b20dd5c` and blob `a632d543510ebf4410f124369b07a303e1d096d6`. CMake, implementation, and wrapper use are verified. This resolves exact source bytes and local compiler use, not an upstream release/tag, complete history, wrapper semantics, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_STB_PROVENANCE_20260716.md` and `scripts\verify-assimp-stb-provenance.ps1`.

kuba--zip fixed provenance now passes for `miniz.h`, `zip.c`, and `zip.h`, the three CMake compiler inputs. The CMake `0.3.0` version is not taken as a source revision: public `kuba--/zip v0.3.1` commit `550905d883b29f0b23e433fdb97f6299b628d4a9`, Assimp import `83d7216726726a07e9e40f86cc2322b22fec11fa`, `v5.4.2`, and the clean build input are bound through fixed tree blobs. CRLF-normalized source deltas and the complete fixed-range post-import `miniz.h`/`zip.c` commits are verified. This resolves one bounded source/delta slice, not release-state proof, upstream signatures, independent miniz provenance, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_KUBAZIP_PROVENANCE_20260716.md` and `scripts\verify-assimp-kubazip-provenance.ps1`.

pugixml fixed provenance now passes for the three effective header-only inputs. Public `zeux/pugixml v1.13` commit `a0e064336317c9347a91224112af9933598714e9`, Assimp import `62cefd5b275628ff97a77d0cd9220e1c35794a3f`, current `v5.4.2`, and the clean build input are tied by fixed tree blobs. CRLF-normalized import deltas are `1/1` for the retained `PUGIXML_HEADER_ONLY` configuration and `0/0` for the upstream implementation/header; the only fixed-range follow-up is the `2/2` copyright update for each file. CMake `VERSION 1.9` is standalone metadata, not the source identity. This resolves one bounded source/delta and input-chain slice, not upstream signature/history, notice scope, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_PUGIXML_PROVENANCE_20260716.md` and `scripts\verify-assimp-pugixml-provenance.ps1`.

UTF8-CPP fixed provenance now passes for the four compiler-read headers. Public `nemtrif/utfcpp v3.2.3` commit `79835a5fa57271f07a90ed36123e30ae9741178e`, the Assimp update, current `v5.4.2`, and the clean build input share exact fixed blobs. The fixed post-update path history is empty, while the non-Hunter include directory, header include chain, and closure file-set SHA-256 are checked. This resolves four-header fixed source identity only, not optional UTF8-CPP headers, upstream signature/history, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_UTF8CPP_PROVENANCE_20260716.md` and `scripts\verify-assimp-utf8cpp-provenance.ps1`.

MiniZip fixed provenance now passes for the four compiler-read zlib-contrib files. Public `madler/zlib v1.3.1` commit `51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf`, Assimp update `64d88276ef7117c09165e468dbb9acd999e324ac`, current `v5.4.2`, and the clean build input share exact fixed blobs. The fixed post-update path history is empty. CMake lists `crypt.h`, but `unzip.c` source-defines `NOUNCRYPT` before the conditional include, so the existing four-file compiler-read closure correctly excludes it. This resolves only `ioapi.c`, `ioapi.h`, `unzip.c`, and `unzip.h` from zlib's `contrib/minizip`, not complete MiniZip/Info-ZIP/`crypt.h` provenance, upstream signature/history, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_MINIZIP_PROVENANCE_20260716.md` and `scripts\verify-assimp-minizip-provenance.ps1`.

miniz fixed provenance now passes for the one compiler-read header from public `kuba--/zip v0.3.1`. The Kuba tag, Assimp PR baseline, PR deltas, merge content, post-merge change, current `v5.4.2`, and clean build input are tied by fixed blobs, numstats, CMake, and closure evidence. PR head and merge content match, but Git ancestry across that merge boundary is intentionally not claimed. The separate full official reachable-object audit sees neither the fixed Kuba nor build raw blob and deliberately records original byte identity as `Unresolved`; both MIT and Unlicense source-text markers are observed in the build header. This resolves only the fixed Kuba-to-Assimp/current-build source chain, not independent original `richgel999/miniz` source identity/history, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_MINIZ_PROVENANCE_20260716.md`, `docs\OPENVISIONLAB_3D_ASSIMP_MINIZ_ORIGIN_BOUNDARY_20260716.md`, and both corresponding verifier scripts.

OpenDDL Parser fixed provenance now passes for all `13` compiler-read inputs. Public `kimkulling/openddl-parser v0.5.1`, the Assimp baseline, and the clean build have an exact fixed chain; only two fixed Assimp changes modify the current source. The `0.4.0` version string is present at the upstream tag itself and is metadata rather than tag identity. This resolves only the fixed source/delta/current-build slice, not upstream signature/history, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_OPENDDL_PROVENANCE_20260716.md` and `scripts\verify-assimp-openddl-provenance.ps1`.

zlib core fixed provenance now passes for all `25` compiler-read source files. Public `madler/zlib v1.2.13`, the Assimp update, current `v5.4.2`, and the clean build share exact fixed blobs, and the checked post-import source-path history is empty. Assimp generates `zconf.h` for the build; that generated effective header is explicitly outside the upstream source-identity subset. This resolves only the fixed source/delta/current-build slice, not upstream signature/history, generated-header provenance, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_ZLIB_PROVENANCE_20260716.md` and `scripts\verify-assimp-zlib-provenance.ps1`.

RapidJSON fixed provenance now passes for all `29` compiler-read headers. Public `v1.1.0` is an ancestor, while the exact source identity is post-tag commit `676d99...`; that snapshot, Assimp update `4a3e0e...`, current `v5.4.2`, and the clean build share exact fixed blobs. The Assimp update changes 16 headers from its first parent, and the checked post-import history is empty. This resolves only the fixed source/delta/current-build slice, not an official release-tag claim for the post-tag snapshot, upstream signature/history, notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_RAPIDJSON_PROVENANCE_20260716.md` and `scripts\verify-assimp-rapidjson-provenance.ps1`.

Open3DGC fixed provenance now passes for all `29` compiler-read files. Public `KhronosGroup/glTF` `mesh-compression-open3dgc` snapshot `7b61d5e...`, Assimp import `054820e6...`, fixed `v5.4.2`, and the clean build are tied by exact blobs. The fixed current Assimp delta has exactly `16` named paths. The core MIT notice and two arithmetic-codec BSD-2-Clause notices are both recorded, so this component is not MIT-only. This resolves only the public carrier-snapshot/import/delta/current-build slice, not historical AMD remote availability, upstream signature/release proof, final notices, binary reproducibility, or distribution. See `docs\OPENVISIONLAB_3D_ASSIMP_OPEN3DGC_PROVENANCE_20260716.md` and `scripts\verify-assimp-open3dgc-provenance.ps1`.

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

- Assimp's fixed-build compile closure, archive-to-build snapshot, and source-backed candidate notice manifest now cover Assimp core plus all 12 compiler-read `contrib` components. The manifest preserves `15` separate source notice records, including Open3DGC's MIT and BSD-2-Clause split. The Poly2Tri candidate Git mirror still has no verified ownership/signature mapping; final-line blame remains intentionally unclaimed. The original miniz raw-byte boundary is audited but deliberately remains `Unresolved`; final notice review remains unresolved.
- kuba--zip fixed tag plus bounded-Assimp-delta/current-build provenance is resolved for its three compiler inputs. The independent miniz boundary audit cannot be promoted to original provenance because current official reachable refs may be incomplete; other vendored components, notices, binary reproducibility, and distribution remain unresolved.
- BoringSSL now resolves to upstream commit `edfe4133d28c5e39d4fce6a2554f3e2b4cafc9bd`, and the official Open3D `v0.19.0` Windows build/package script is recorded byte-for-byte. A 2026-07-16 controlled-rebuild preflight finds VS2022/CMake/MSVC `14.44.35207` but no Perl, Go, or NASM on the developer command path. The official script explicitly requires those tools, so no substitute or partial build was attempted. `docs/OPENVISIONLAB_3D_BORINGSSL_CONTROLLED_REBUILD_PLAN_20260716.md` fixes the approved-run comparison contract. The prebuilt archive's historical compiler/toolchain, independent binary reproduction, and archive-to-source modification equivalence remain unresolved.
- oneMKL input and payload provenance is resolved for this fixed archive. Final notice review must explicitly reconcile the different Open3D-source and wheel license texts; the canonical ZIP does not claim the undocumented original container metadata.
- VTK `9.1.0` source identity, documented no-patch package recipe, archive/install payload, seven-child target closure, and 8/8 license matches are resolved. The marker interpretation is resolved; exact historical compiler invocation and source-to-binary reproducibility remain unresolved.
- VTK marker interpretation is resolved: `_MSC_VER=1900` is an ABI-family marker, not evidence that contradicts the recorded VS2019 compiler path. The controlled Release rebuild and a same-source `USE_SYSTEM_VTK=ON` Open3D runtime check provide package and local runtime compatibility evidence; exact historical byte reproduction, Debug reconstruction, and final distribution approval remain unresolved.
- libzmq `4.3.3` is statically linked under LGPLv3 terms with the ZeroMQ static-linking exception, and its Windows build includes BSD-2-Clause wepoll. Final notices must preserve both.
- The Assimp closure candidate manifest is now verified, but `THIRD-PARTY-NOTICES.txt` must not be generated as final evidence until these gaps are closed and the full enabled dependency graph is reviewed.

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

This closes the first-pass inventory, BoringSSL exact-source/documented-recipe, fixed-build Assimp compiled closure, fixed Poly2Tri, Clipper, Open3DGC, stb_image, kuba--zip, pugixml, UTF8-CPP, MiniZip, miniz, OpenDDL Parser, zlib core, and RapidJSON provenance subtasks, fixed oneMKL wheel/payload provenance, VTK source/recipe/payload/transitive-closure subtasks, and the technical clean-host VC/OpenMP prerequisite gate. The distribution-gate item for a complete SPDX/CycloneDX dependency manifest remains unchecked because independent original miniz provenance, BoringSSL and VTK binary/toolchain reproducibility, final notice scope, and owner/legal review are still incomplete. The next technical task after explicit prerequisite-tool approval is the controlled BoringSSL rebuild; it is not a reason to integrate Open3D into the product now.
