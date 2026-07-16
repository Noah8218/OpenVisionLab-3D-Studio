# Assimp stb_image v2.29 Provenance - 2026-07-16

## Scope And Decision

This checkpoint verifies the single compiler-read Assimp `stb_image.h` input used by the separate-process Open3D registration candidate. It does not add a product dependency, approve redistribution, or change Viewer or Runner behavior.

**Decision:** pass for a fixed official upstream commit, the Assimp source-update commit, the current Assimp `v5.4.2` tag source, and the actual Open3D clean-build input. All four files are raw-byte-identical.

The `stb_image.h` header identifies itself as `v2.29`, but the captured official `nothings/stb` tags and releases endpoints both return an empty unpaginated array. This is therefore commit-based source identity, not an official release/tag identity. The upstream history response is paginated and is deliberately not used as a complete history claim.

## Fixed Evidence

| Input | Value |
| --- | --- |
| Fixed upstream commit | `0bc88af4de5fb022db643c2d8e549a0927749354` (`stb_image: optimizations`) |
| Upstream/blob SHA | `a632d543510ebf4410f124369b07a303e1d096d6` |
| Source SHA-256 | `c54b15a689e6a1f32c75e2ec23afa442e3e0e37e894b73c1974d08679b20dd5c` |
| Assimp source-update commit | `3ff7851ff9ad3004bb934fedaf657ffad0572573` (`updated STBIMAGElib (#5500)`) |
| Assimp update record | `173` additions / `175` deletions, resulting blob `a632d543...` |
| Assimp tag | `v5.4.2` -> `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Assimp `stb_image.h` history | `4` complete, unpaginated entries; latest `3ff7851...` |
| Captured upstream tags/releases | `0` / `0`, both without `Link` pagination headers |
| Compiler-use checks | CMake source list, `STB_IMAGE_IMPLEMENTATION`, and `StbCommon.h` include all pass |

Ignored capture and replay artifacts are under `artifacts\dependency-candidates\assimp-stb-provenance-20260716`.

## Verification Result

1. The official upstream commit record for `0bc88af...` names `stb_image.h` with result blob `a632d543...`.
2. The recorded Assimp `3ff7851...` file record has the same blob. Its captured raw file, the raw `v5.4.2` tag file, and the actual clean-build input all have the same SHA-256 and Git blob.
3. Assimp's fixed path history has four entries, begins with `3ff7851...`, ends at `1b37b74...`, and has no pagination header. Thus the current tag source did not receive a later `contrib/stb/stb_image.h` path change after the recorded update.
4. The actual Assimp CMake source list includes `../contrib/stb/stb_image.h`; `Assimp.cpp` instantiates the header with `STB_IMAGE_IMPLEMENTATION`; and `StbCommon.h` includes the same header.
5. The source header preserves its `v2.29` and public-domain identification. It is not a claim that a separately published `v2.29` tag or release exists.
6. A controlled all-zero expected source SHA returns exit code `1` and records four source-identity failures. The verifier fails closed when the fixed content contract changes.

## Reproduction Checklist

The verifier is offline after the fixed official artifacts have been captured. It rejects altered or missing evidence rather than downloading replacement content silently.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-stb-provenance.ps1 `
  -ReportPath artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-provenance-replay.json
```

- [x] Require exact raw source SHA-256 and Git blob equality across upstream, Assimp update, tag, and build input.
- [x] Require fixed official upstream and Assimp commit records with the expected file blob.
- [x] Require the fixed Assimp tag and non-paginated path history.
- [x] Record the captured absence of upstream tags/releases without converting it into a release claim.
- [x] Require CMake, implementation, and wrapper source-use evidence.
- [x] Require a controlled wrong source SHA to fail with exit code `1`.
- [ ] Do not treat this as an upstream release signature, complete upstream history, notice review, binary reproducibility, or distribution approval.

## Claim Boundary

- Passed: fixed upstream commit-to-current-build byte identity and the local compiler-use path for `stb_image.h`.
- Not passed: an official upstream release/tag, full upstream history, Assimp wrapper/prefix semantics, other Assimp `contrib` provenance, final notices, binary reproducibility, product integration, or distribution approval.

## Sources

- stb upstream commit: https://github.com/nothings/stb/commit/0bc88af4de5fb022db643c2d8e549a0927749354
- stb repository: https://github.com/nothings/stb
- Assimp stb source update: https://github.com/assimp/assimp/commit/3ff7851ff9ad3004bb934fedaf657ffad0572573
- Assimp stb history at `v5.4.2`: https://github.com/assimp/assimp/commits/v5.4.2/contrib/stb/stb_image.h
- Assimp `v5.4.2` tag: https://github.com/assimp/assimp/releases/tag/v5.4.2
