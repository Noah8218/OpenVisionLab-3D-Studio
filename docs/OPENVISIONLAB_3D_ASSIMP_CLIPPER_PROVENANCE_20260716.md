# Assimp Clipper 6.4.2 Provenance - 2026-07-16

## Scope And Decision

This checkpoint verifies the fixed compiler-read Assimp Clipper component used by the separate-process Open3D registration candidate. It does not add a product dependency, authorize redistribution, or change Viewer or Runner behavior.

**Decision:** pass for the fixed official Clipper `6.4.2` release content, the recorded Assimp `aa1996` import state, the final `bb9101` ASCII-comment update, and current Assimp `v5.4.2` build-source identity.

The official release archive is not treated as a signed upstream VCS revision. The evidence proves fixed release content and the two recorded Assimp-side states. It does not assign individual final lines to a particular upstream author or close notice, binary reproducibility, or distribution gates.

## Fixed Evidence

| Input | Value |
| --- | --- |
| Official Clipper archive | `clipper_ver6.4.2.zip` from the official Clipper project |
| Archive SHA-256 | `a14320d82194807c4480ce59c98aa71cd4175a5156645c4e2b3edd330b930627` |
| Official archive `cpp/clipper.cpp` | `142,184` bytes, SHA-256 `5c642a3668311701f72572443aa42c1a981edb037298efc015166d9d90be0755` |
| Assimp Clipper import | `aa1996e1437777af62aac549d55591f1849f90de` (`Mosfet80 clipper update (#5220)`) |
| Import source blob | `d75974336b34975721598acceac797da15709d2f` |
| Latest Clipper path commit | `bb9101ae9eb2938cadfeadd4690bbdf910ca57f4` (`Eliminate non-ascii comments in clipper (#5480)`) |
| Current source blob | `c0a8565bb98568dcca4a5350ca52fa08152bea51` |
| Assimp tag | `v5.4.2` -> `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Official Clipper path history | `12` complete, unpaginated entries; `bb9101` directly precedes `aa1996` |
| Archive -> import delta | `7` additions / `6` deletions with end-of-line whitespace ignored |
| Import -> current-tag delta | `4` additions / `4` deletions with end-of-line whitespace ignored |

Ignored capture and replay artifacts are under `artifacts\dependency-candidates\assimp-clipper-provenance-20260716`.

## Verification Result

1. The official archive SHA-256 and the three relevant archive entries are fixed. `clipper.hpp` and `License.txt` match the build source after CRLF normalization.
2. The recorded raw source at `aa1996` hashes to its official Git blob. Its update from the official `6.4.2` archive contains the exact checked `7/6` delta, including the `InitEdge` value initialization and `BuildResult` loop-variable update.
3. The direct official import patch capture has SHA-256 `ff0c0a8959297d43f21f6f463aae47978532082fbf43de57024b781aae09120c` and contains both code-change sentinels.
4. The official Clipper path-history response begins with `bb9101` and its direct predecessor is `aa1996`; no later Clipper-path change is omitted by pagination.
5. The captured official `v5.4.2` raw source and the build input are raw-byte-identical and both hash to `c0a8565...`. The only `aa1996 -> v5.4.2` file delta is the official `bb9101` four-line ASCII comment change.
6. A controlled expected archive-to-import delta of `8/6` returns exit code `1`, writes a `Fail` report, and preserves the observed `7/6` delta. The verifier fails closed on a changed contract.

## Reproduction Checklist

The verifier is offline after the fixed official artifacts have been captured. It rejects missing or modified inputs rather than downloading replacement content silently.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-clipper-provenance.ps1 `
  -ReportPath artifacts\dependency-candidates\assimp-clipper-provenance-20260716\clipper-provenance-replay.json
```

- [x] Require the official Clipper `6.4.2` archive SHA-256 and all three fixed entry hashes.
- [x] Require official `v5.4.2` tag and commit evidence plus a complete, non-paginated Clipper path history.
- [x] Require `aa1996` and `bb9101` official file records and matching raw Git blobs.
- [x] Require archive-to-import `7/6` and import-to-current `4/4` source deltas.
- [x] Require current tag raw source to match the actual Open3D clean-build source input.
- [x] Require a controlled wrong-delta contract to fail with exit code `1`.
- [ ] Do not interpret this as a legal notice review, binary reproducibility result, or distribution approval.

## Claim Boundary

- Passed: fixed official `6.4.2` release content, fixed Assimp import/current source states, the two bounded source deltas, and current tag-to-build-source identity.
- Not passed: upstream VCS identity or signature, individual-line author attribution, other Assimp `contrib` provenance, final notice disposition, binary reproducibility, product integration, or distribution approval.

## Sources

- Clipper official project files: https://sourceforge.net/projects/polyclipping/files/
- Assimp `v5.4.2` tag: https://github.com/assimp/assimp/releases/tag/v5.4.2
- Assimp Clipper import commit: https://github.com/assimp/assimp/commit/aa1996e1437777af62aac549d55591f1849f90de
- Assimp ASCII comment commit: https://github.com/assimp/assimp/commit/bb9101ae9eb2938cadfeadd4690bbdf910ca57f4
- Assimp Clipper history at `v5.4.2`: https://github.com/assimp/assimp/commits/v5.4.2/contrib/clipper
