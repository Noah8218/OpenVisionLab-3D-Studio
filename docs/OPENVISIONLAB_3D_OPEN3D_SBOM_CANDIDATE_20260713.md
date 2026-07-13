# Open3D Candidate SBOM Evidence

Checked: 2026-07-13

## Decision

`open3d-0.19.0-nongui-windows-candidate.cdx.json` is a CycloneDX `1.6` candidate for the independently clean Open3D `0.19.0` non-GUI Windows x64 Release build. It is useful dependency evidence, but it is not a distribution approval or a complete third-party notice source.

Keep Open3D outside the product dependency graph until the unresolved transitive provenance, Microsoft prerequisite, clean-host, product impact, and owner/legal gates in `OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md` pass.

## Scope

The candidate contains:

- one metadata subject for Open3D source commit `1e7b17438687a0b0c1e5a7187321ac7044afe275`;
- 27 direct component records derived from 28 Release external-project references, with Qhull's C and C++ projects represented as one component;
- 6 directly observed build/transitive records: DirectX-Headers, DirectXMath, VTK kissfft, VTK pugixml, VTK KWSys, and libzmq's Windows wepoll implementation;
- source/prebuilt archive hashes where an exact downloaded archive exists;
- local license-file hashes and explicit Open3D modifications where observed;
- an explicit dependency graph and unresolved-provenance properties.

It intentionally excludes inactive source-tree dependencies and does not infer components from binary strings. Microsoft VC143 CRT and OpenMP are deployment prerequisites, not resolved SBOM components, until the clean-host prerequisite gate is designed.

## Evidence Method

1. Parse `artifacts/o3d-clean/b/cpp/open3d/Open3D.vcxproj` for Release x64 link inputs and project references.
2. Confirm external-project completion stamps and `USE_SYSTEM_*` values from the clean CMake build.
3. Hash the exact archives in the Open3D `3rdparty_downloads` cache and the exact license files in the source/build trees.
4. Record Open3D patches and compatibility copies separately from upstream archive hashes.
5. Preserve unresolved provenance instead of converting an archive name or license directory into an unsupported completeness claim.

Observed build contract:

```text
Release final link inputs: 60
External project references: 28
Candidate direct components: 27
Observed support/transitive components: 6
Candidate component records: 33
Open3D.dll adjacent non-system runtime: tbb12.dll
```

## Recorded Modifications

| Component | Open3D-side change | SHA-256 |
| --- | --- | --- |
| JsonCpp `1.9.4` | `0001-optional-CXX11-ABI-and-MSVC-runtime.patch` | `227c717971e444a76d67c51e71179204543294fe08ae3a4eeafb945effdb361c` |
| zlib `1.2.13` | `0001-patch-zlib-to-enable-unzip.patch` | `43dff318c107f2751ab81ee8057891bdbf3dd54a315ec5028eab1d19e7648506` |
| UVAtlas `may2022` | Copy Open3D's compatibility `sal.h` into the UVAtlas source | `8d1acb5784aeb2a3fb0bafe5ed9a0854cabdffe0009f693a947e5164e35adb06` |

## Unresolved Items

- Assimp is downloaded from tag `v5.4.2`, but its extracted project declares `5.4.1`. The exact enabled `contrib` closure and notices are not yet reduced to compiled objects.
- The Open3D-hosted BoringSSL `edfe413` prebuilt archive lacks a locally proven exact upstream commit, build recipe, and modification record.
- The Open3D-hosted MKL archive documents assembly from Intel pip packages, but the input wheel hashes and archive reconstruction are not recorded.
- The Open3D-hosted VTK `9.1` prebuilt archive exposes linked VTK/kissfft/pugixml/KWSys libraries and licenses, but not exact upstream revisions, build recipe, modification record, or a proven complete transitive closure.
- libzmq `4.3.3` is statically linked under LGPLv3 terms with the ZeroMQ static-linking exception, and its Windows build includes BSD-2-Clause wepoll. Final notices must preserve both.
- `THIRD-PARTY-NOTICES.txt` must not be generated as final evidence until these gaps are closed.

## Validation

The candidate should pass all of the following before it is updated in a future session:

```powershell
$bom = Get-Content -Raw docs\open3d-0.19.0-nongui-windows-candidate.cdx.json | ConvertFrom-Json
if ($bom.specVersion -ne '1.6' -or $bom.components.Count -ne 33) { exit 1 }
$refs = @($bom.metadata.component.'bom-ref') + @($bom.components.'bom-ref')
if (($refs | Sort-Object -Unique).Count -ne $refs.Count) { exit 1 }
$dependencyRefs = @($bom.dependencies.ref)
if (@($refs | Where-Object { $_ -notin $dependencyRefs }).Count -ne 0) { exit 1 }
```

Validate the JSON against the official CycloneDX `1.6` JSON schema rather than accepting JSON parse success as schema evidence. The reference schema is maintained in the [CycloneDX specification repository](https://github.com/CycloneDX/specification/tree/1.6/schema).

Current verification result:

```text
cyclonedx-1.6-schema=PASS
components=33
dependencies=34
component-archive-hashes=23/23 exact-set-match
root-source-archive-hash=PASS
license-evidence-hashes=35/35 unique/all-found
modification-hashes=3/3 exact-set-match
unresolved-provenance-records=8
```

Ignored local evidence:

```text
artifacts/o3d-clean/open3d-release-link-evidence.json
artifacts/o3d-clean/sbom-schema-validation.txt
artifacts/o3d-clean/sbom-candidate-verification.txt
artifacts/o3d-sbom-schema/bom-1.6.schema.json
```

## Gate Effect

This closes only the first-pass inventory evidence task. The distribution-gate item for a complete SPDX/CycloneDX dependency manifest remains unchecked because the candidate still records unresolved provenance and transitive scope. The next distribution work should resolve one uncertainty at a time, beginning with the exact Assimp compiled importer/contrib closure or the VTK prebuilt provenance decision; neither is a reason to integrate Open3D into the product now.
