# Assimp miniz Original-Origin Boundary - 2026-07-16

## Decision

The fixed Assimp compiler input remains proven only from public `kuba--/zip v0.3.1` through the bounded Assimp changes to the clean build. A separate audit of the current official `richgel999/miniz` reachable Git object set passes, but it does not establish original upstream byte identity.

The audit records `OriginIdentityStatus = Unresolved`. It found neither the Kuba baseline `miniz.h` blob nor the actual clean-build `miniz.h` blob in the full, non-shallow official repository's current reachable object set. This is evidence of non-observation in that bounded object set, not evidence that no historical relationship ever existed.

The clean-build header contains both the existing Unlicense and MIT source-text markers. That supports retaining the candidate manifest's source-text expression `MIT OR Unlicense`; it is not a final license interpretation, notice decision, legal approval, or distribution approval.

## Fixed Evidence

| Item | Fixed value |
| --- | --- |
| Official miniz remote | `https://github.com/richgel999/miniz.git` |
| Official repository state | Full, non-shallow clone; no partial-clone filter |
| Legacy tag | `v114` / `48605fb1bd5662effe13b789d866981225e71256` |
| Legacy `miniz.c` blob | `ac3d93569f4f2b5683c639aa6b55db9c64894425` |
| Modern tag | `3.0.2` / `293d4db1b7d0ffee9756d035b9ac6f7431ef8492` |
| Modern `miniz.c` blob | `1968d62b8f99b897cbe639a422c7775d3271b9a8` |
| Modern `miniz.h` blob | `2f86380ad42f6aa9ae3a90bd50bf1928431a351f` |
| Kuba tag | `v0.3.1` / `550905d883b29f0b23e433fdb97f6299b628d4a9` |
| Kuba baseline `miniz.h` blob | `cd86483184cfba1dd33c4db7c718965e20926c7a` |
| Assimp clean-build `miniz.h` blob | `ad5850ce17d9449cc9356486dff73c8a566e1c46` |
| Assimp clean-build SHA-256 | `bba5c196415bda01b460d2dd8be779189bb228c2802b5b61dd20eae5c3921b06` |
| Compiler-read closure | `contrib/zip/src/miniz.h` only |
| Closure file-set SHA-256 | `ccc0dd6eef59502e6d9aa1774b21ce1e2038d8448baf6d28bdf878d868a5a67c` |

The full official clone enumerated `1,368` currently reachable objects. Neither the Kuba baseline blob nor the clean-build blob was among them. `v114` is not an ancestor of `3.0.2`.

## Interpreting the Source Text

The public Kuba source describes its zip library as using the miniz `v3.0.2` API. Its imported `miniz.h`, however, identifies `miniz.c 3.0.0`, a 2013 update marker, and `MZ_VERSION "11.0.2"`. The official `v114` source instead identifies `miniz.c v1.14`, a 2012 update marker, and `MZ_VERSION "9.1.14"`. The official `3.0.2` revision also has different `miniz.c` and `miniz.h` blobs from both the Kuba baseline and the actual build input.

These facts rule out treating the fixed Kuba-to-Assimp path as direct raw-byte proof of either named official tag. They do not rule out an unrecorded historical derivation, a transformed import, or source history outside the current official reachable refs.

## Repeatable Verification

Use a full clone. Do not use `--filter=blob:none`, because enumerating all reachable objects must not trigger deferred blob downloads during the audit.

```powershell
git clone --no-checkout https://github.com/richgel999/miniz.git `
  artifacts/dependency-candidates/assimp-miniz-origin-boundary-20260716/official-miniz-full

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-miniz-provenance.ps1 `
  -ReportPath artifacts/dependency-candidates/assimp-miniz-provenance-20260716/miniz-provenance.json

powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-miniz-origin-boundary.ps1 `
  -MinizProvenanceReportPath artifacts/dependency-candidates/assimp-miniz-provenance-20260716/miniz-provenance.json `
  -ReportPath artifacts/dependency-candidates/assimp-miniz-origin-boundary-20260716/miniz-origin-boundary.json
```

The positive report must contain:

```text
AssimpMinizOriginBoundary|Pass|origin=Unresolved|reachableMatches=0|issues=0
```

Use an incorrect official legacy revision to check fail-closed behavior:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  scripts/verify-assimp-miniz-origin-boundary.ps1 `
  -MinizProvenanceReportPath artifacts/dependency-candidates/assimp-miniz-provenance-20260716/miniz-provenance.json `
  -ReportPath artifacts/dependency-candidates/assimp-miniz-origin-boundary-20260716/miniz-origin-boundary-negative.json `
  -ExpectedMinizLegacyRevision 0000000000000000000000000000000000000000
```

This command must return exit code `1` and record the expected legacy-tag revision mismatch.

## Claim Boundary

This evidence proves:

- the official remote, tag revisions, tag blobs, non-shallow state, and current reachable-object set used for the audit;
- that neither fixed Kuba nor fixed clean-build raw blob is directly reachable from that current official object set;
- that the two official tag revisions do not have the asserted ancestor relationship;
- that the current build header contains both observed Unlicense and MIT source-text markers; and
- deterministic positive and controlled negative audit behavior.

It does not prove:

- original upstream byte identity, complete history, ownership, authorship, or a historical derivation path for the Assimp build input;
- that the official repository has never contained a matching blob outside its current reachable refs;
- a legal interpretation of the combined source text or final third-party-notice completeness;
- binary reproducibility, redistribution, owner/legal approval, or Open3D product integration; or
- Viewer, Runner, inspection, calibration, or physical-metrology behavior.

Keep this unresolved provenance boundary separate from both the candidate notice manifest and all product reliability claims.
