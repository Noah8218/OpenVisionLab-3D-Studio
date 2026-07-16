# Assimp Poly2Tri Content Lineage - 2026-07-16

## Scope And Decision

This is a supply-chain evidence checkpoint for the separate-process Open3D registration candidate. It does not add Assimp or Open3D to the product, approve redistribution, prove a binary is reproducible, or change the Viewer.

**Decision:** the fixed original Poly2Tri Mercurial revision and the candidate Git commit are now directly content-equivalent. The official Google Code source archive was checked out at Mercurial `5de9623d6a500d8b0ad3126a48957c5152c15ad2`; a `core.autocrlf=false` Git archive at `greenm01/poly2tri@99927efa011013154460ca4cb06bcd64d4768edb` matches all `35/35` ordinal source paths, lengths, and raw-byte SHA-256 values. This closes the fixed original-revision-to-candidate-content identity checkpoint.

The candidate Git mirror has no release tag, signature, or independently verified ownership/mirroring record. The pass therefore proves source-content equivalence to the recovered official revision, not that the Git commit itself is an authenticated official revision identifier. Assimp-side modifications from the initial import to current `v5.4.2`, broader vendored-component provenance, notices, binary reproducibility, and distribution approval remain separate gates.

## Fixed Evidence

| Input | Value |
| --- | --- |
| Assimp initial Poly2Tri import | `1ebd116dff21ca8e347676288bfda3056af18a8c` |
| Assimp initial import statement | `+ add poly2tri to assimp repository.` |
| Initial Assimp patch blob | `f8d38b909b0f676dd4b401d4b9cee33b076e0449` |
| Patch-recorded source revision | Mercurial `5de9623d6a50`, dated 2011-08-08 |
| Candidate Git mirror | `https://github.com/greenm01/poly2tri.git` |
| Candidate content-equivalent commit | `99927efa011013154460ca4cb06bcd64d4768edb` |
| Candidate commit metadata | `2011-08-08T22:26:41-04:00`, `fixed NULL` |
| Mirror inventory | `206` commits, `0` tags |
| Official Google Code project | `poly2tri`, repository type `hg`, source present |
| Official original Mercurial commit | `5de9623d6a500d8b0ad3126a48957c5152c15ad2`, Mason Green, `2011-08-08T22:26:41-04:00`, `fixed NULL` |
| Official source archive | `1,091,419` bytes, SHA-256 `02092826bf5c539ed5a904386a2439eb608cc4d1d008adc7034ae3a2230a05bb` |
| Archive VCS evidence | `175` file entries, including `140` `.hg` metadata entries |
| Artifact-local Mercurial runtime | `mercurial 7.2.3`, wheel SHA-256 `262383e7290f6a83a6c6dff4b40b727fd5f0f4c7dfd79fa45e2e1ca0b45531b1` |
| Candidate Git archive SHA-256 | `bdafe3a0502725096aa3ad4ca22bb360d49e53188bb625d4ac182900f330d8d0` |
| Equal raw canonical manifest SHA-256 | `c8a0845fb300289b219e3bf06d07180c4d33ca18609741b6513f72aad29622e7` |
| Assimp current source | `v5.4.2`, commit `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |

Ignored report: `artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\poly2tri-origin-identity.json`.

## Verification Result

1. The official Google Code archive SHA-256 matches the fixed value before use. Its extracted Mercurial repository has `200` revisions; the target is revision `190` with the expected author, date, and `fixed NULL` message.
2. With the user's approval, Mercurial `7.2.3` was downloaded only under the ignored evidence folder and loaded through the existing Python `3.11` process. No global package, PATH, registry, product project, or release bundle was changed.
3. `hg archive --type files --rev 5de962...` writes `.hg_archival.txt` whose `node` is the complete expected Mercurial revision. The generated metadata file is excluded from source comparison.
4. `git -c core.autocrlf=false archive --format=zip 99927...` exports the Git objects directly, avoiding workstation checkout line-ending policy.
5. The two archive exports contain exactly `35/35` ordinal paths, with `0` missing, `0` extra, `0` raw-byte mismatches, and `0` CRLF-to-LF normalized mismatches. Both raw canonical manifests equal `c8a0845fb300289b219e3bf06d07180c4d33ca18609741b6513f72aad29622e7`.
6. A controlled invalid candidate revision returns exit code `1`, writes a `Fail` report, and records `fatal: not a tree object`, proving the verifier does not silently compare an arbitrary Git tree.
7. The initial Assimp patch still identifies the two original import changes, while direct initial-import-to-`v5.4.2` comparison bounds the later tree delta to `14` files, `1,407` insertions, and `1,328` deletions. The separate official path-history gate maps all `14` paths to ordered post-initial Assimp commit sets; it intentionally does not claim single-line blame.

The fixed original source can therefore be cited as content-equivalent to the candidate Git commit. The current Assimp Poly2Tri tree contains that initial documented import plus later Assimp-side changes. The fixed build's actual source content remains independently covered by `OPENVISIONLAB_3D_ASSIMP_SOURCE_SNAPSHOT_IDENTITY_20260716.md`.

## Reproduction Checklist

Use a new, empty ignored working directory for each replay; the verifier deliberately refuses to delete or reuse an existing directory.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-poly2tri-origin.ps1 `
  -WorkingDirectory artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\poly2tri-origin-replay `
  -ReportPath artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\poly2tri-origin-replay.json
```

- [x] Verify the official source-archive SHA-256 before reading the Mercurial repository.
- [x] Require the complete expected Mercurial revision and Git revision identifiers.
- [x] Require `.hg_archival.txt` to name the expected original revision.
- [x] Require `35/35` ordinal paths with zero missing, extra, raw-byte, and CRLF-to-LF-normalized mismatches.
- [x] Require equal raw and normalized canonical manifest SHA-256 values.
- [x] Require a controlled invalid revision to fail with exit code `1` and a `Fail` report.
- [x] Preserve the `14` file / `1,407` insertion / `1,328` deletion current-Assimp delta evidence.
- [x] Require the separate official `28`-commit path-history ledger to cover all `14` direct-delta paths.
- [ ] Obtain owner/legal acceptance before treating this content evidence as sufficient for notices or redistribution.

## Remaining Requirements

1. Preserve the path-level ledger boundary; do not convert it into unproven final-line-to-single-commit blame.
2. Resolve independent upstream revision/modification evidence for the remaining enabled Assimp vendored components.
3. Complete BoringSSL source-to-binary/toolchain evidence only after explicit owner approval for its required tools.
4. Produce reviewed third-party notices and obtain owner/legal approval before any Open3D distribution decision.

Do not promote this checkpoint to Git-mirror ownership authentication, full third-party notice evidence, binary reproducibility, product integration, or distribution approval.

## Sources

- Assimp Poly2Tri history at `v5.4.2`: https://github.com/assimp/assimp/commits/v5.4.2/contrib/poly2tri
- Initial Assimp import commit: https://github.com/assimp/assimp/commit/1ebd116dff21ca8e347676288bfda3056af18a8c
- Example Assimp-side Poly2Tri correction: https://github.com/assimp/assimp/commit/02fc5effbaec2f0463b7fa2eb983f5930d97e3ea
- Candidate Git mirror: https://github.com/greenm01/poly2tri
- Google Code Archive schema and source-archive protocol: https://code.google.com/archive/schema
- Mercurial `7.2.3` package metadata: https://pypi.org/project/mercurial/7.2.3/
