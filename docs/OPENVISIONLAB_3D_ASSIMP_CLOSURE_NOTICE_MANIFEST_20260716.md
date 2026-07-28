# Assimp Closure Notice Manifest Candidate - 2026-07-16

## Decision

The fixed clean Open3D Release build now has a deterministic, source-backed
candidate notice manifest for Assimp core and the actual compiler-read Assimp
`contrib` closure. It is an engineering evidence artifact, not a final
`THIRD-PARTY-NOTICES.txt`, legal interpretation, redistribution decision, or
product dependency approval.

`scripts\verify-assimp-closure-notice-manifest.ps1` requires all of the
following to agree before it emits `Pass`:

1. The fixed Assimp `v5.4.2` closure report and its `232/232` Release source/
   object mapping.
2. A fresh archive-to-build source snapshot report with `2,940/2,940` files
   and no missing, extra, or modified paths.
3. The CycloneDX `1.6` candidate's component, license, and source-evidence
   properties.
4. Current clean-build hashes for every candidate notice source file.

The manifest keeps each source notice reference separate. In particular,
Open3DGC has one MIT source notice plus two BSD-2-Clause arithmetic-codec
notice sources; it is not represented as MIT-only.

## Fixed Candidate

| Component | Compiler scope | SPDX expression | Source notice records |
| --- | ---: | --- | ---: |
| Assimp | `232` Release sources | `BSD-3-Clause` | 1 |
| Clipper | 2 | `BSL-1.0` | 1 |
| Open3DGC | 29 | `MIT AND BSD-2-Clause` | 3 |
| OpenDDL Parser | 13 | `MIT` | 1 |
| Poly2Tri | 12 | `BSD-3-Clause` | 1 |
| pugixml | 3 | `MIT` | 1 |
| RapidJSON | 29 | `MIT` | 1 |
| stb_image | 1 | `MIT OR Unlicense` | 1 |
| MiniZip | 4 | `Zlib` | 1 |
| UTF8-CPP | 4 | `BSL-1.0` | 1 |
| kuba--zip | 2 | `Unlicense` | 1 |
| miniz | 1 | `MIT OR Unlicense` | 1 |
| zlib | 25 | `Zlib` | 1 |

The fixed 12-component closure contains `125` compiler-read paths. Together
with Assimp core, the candidate has `13` entries and `15` individual source
notice records.

## Current Verification

| Check | Result |
| --- | --- |
| Assimp archive to clean-build source snapshot | Pass: `2,940/2,940`, no differences |
| Candidate manifest | Pass: 13 entries, 12 closure components, 125 compiler paths, 15 notice records |
| Deterministic notice contract SHA-256 | `ce51a50d6852cb3229c6406d85e7bb181a296006d0a72bae934959883babc43c` |
| Controlled wrong archive SHA-256 | Fail report and exit code `1` |

The current ignored reports are:

- `artifacts\dependency-candidates\assimp-source-snapshot-20260716\assimp-source-snapshot-post-manifest.json`
- `artifacts\dependency-candidates\assimp-closure-notice-manifest-20260716\assimp-closure-notice-manifest-second.json`
- `artifacts\dependency-candidates\assimp-closure-notice-manifest-20260716\assimp-closure-notice-manifest-negative.json`

The negative report contains two independent archive-SHA mismatch findings:
one from the closure and one from the source-snapshot report. It proves that a
stale or substituted Assimp archive identity is not silently accepted.

## Reproduction

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-source-snapshot.ps1 `
  -ReportPath artifacts\dependency-candidates\assimp-source-snapshot-20260716\assimp-source-snapshot.json

powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-closure-notice-manifest.ps1 `
  -SourceSnapshotReportPath artifacts\dependency-candidates\assimp-source-snapshot-20260716\assimp-source-snapshot.json `
  -ReportPath artifacts\dependency-candidates\assimp-closure-notice-manifest-20260716\assimp-closure-notice-manifest.json
```

- [ ] Require both commands to return exit code `0`.
- [ ] Require `Status=Pass`, `13` entries, `125` compiler-read paths, and `15`
  source notice records.
- [ ] Preserve the JSON manifest with the fixed build evidence.
- [ ] Do not turn the JSON directly into a release notice file without an
  owner/legal review of the broader Open3D dependency graph.

## Claim Boundary

This candidate proves only that the fixed Assimp core plus compiler-read
closure has stable, source-hash-backed notice references that agree with the
current CycloneDX candidate. It does not prove that these references form a
complete product-distribution notice set, decide license obligations, resolve
independent original miniz provenance, reproduce BoringSSL or VTK binaries,
approve Microsoft REDIST handling, add Open3D to the Viewer, or establish
Viewer/Runner registration parity.

## Related Evidence

- `docs\OPENVISIONLAB_3D_ASSIMP_SOURCE_SNAPSHOT_IDENTITY_20260716.md`
- `docs\OPENVISIONLAB_3D_ASSIMP_OPEN3DGC_PROVENANCE_20260716.md`
- `docs\OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md`
- `docs\OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`
