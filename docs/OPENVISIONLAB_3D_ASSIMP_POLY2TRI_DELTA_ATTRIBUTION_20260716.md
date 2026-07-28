# Assimp Poly2Tri Delta Attribution - 2026-07-16

## Scope And Decision

This checkpoint attributes the current Assimp Poly2Tri tree delta at the **path-history** level. It is supply-chain evidence for the separate-process Open3D registration candidate only; it does not add a product dependency, approve redistribution, or change any Viewer behavior.

**Decision:** pass for fixed path-history coverage. The official `v5.4.2` tag resolves to `ddb74c2bbdee1565dda667e85f0c82a0588c8053`. Official GitHub history returns one complete, non-paginated page of `28` Poly2Tri path commits from `6bcdf989fb7331aab8fa3b1afe6a0740b1a4ec9b` back to the initial import `1ebd116dff21ca8e347676288bfda3056af18a8c`. The official initial/current trees have `15` Poly2Tri blobs each; `14` blobs differ, and every direct-delta path occurs in one or more of the `27` post-initial official commits.

This is not single-line blame. A final line can be affected by multiple commits, merge formatting, or a later revert. The evidence identifies the complete ordered commit set for each changed path and preserves the direct net tree delta; it does not assign each final line to exactly one commit.

## Fixed Evidence

| Input | Value |
| --- | --- |
| Initial Assimp Poly2Tri import | `1ebd116dff21ca8e347676288bfda3056af18a8c` |
| Assimp `v5.4.2` tag target | `ddb74c2bbdee1565dda667e85f0c82a0588c8053` |
| Latest Poly2Tri path commit before the tag | `6bcdf989fb7331aab8fa3b1afe6a0740b1a4ec9b` |
| Official history page | `28` commits, no `Link` pagination header |
| Initial/current official tree blobs | `15` / `15` |
| Direct changed paths | `14` |
| Direct net line delta | `1,407` additions, `1,328` deletions |
| Post-initial path commits | `27` |
| Changed paths without official post-initial history | `0` |
| History-page payload SHA-256 | `23b018111108ae9ee85be420b2f667d072c8b525e88a18d304278c99201ec48f` |
| Commit-detail summary SHA-256 | `3e6456464264a6f11def1489706af2b0c80fd7957dcc545120b89c40edf948de` |
| Final path-history ledger SHA-256 | `c69a223b6c8c95e8997caa13e37757f58b39cc70850b87bfd351eab8bae69dd2` |

The ignored source responses and reports are under `artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716`.

## Verification Result

1. The official tag-reference response maps `refs/tags/v5.4.2` directly to `ddb74c2...`; the tag commit's own one-file change is outside Poly2Tri, so the latest path-specific entry is correctly `6bcdf98...`.
2. The official GitHub path-history response contains `28` commits, begins with `6bcdf98...`, ends with `1ebd116...`, and has no pagination link. All `28` individual official commit responses match the captured response hashes and their summarized Poly2Tri file records.
3. Official Git Trees for initial and current commits are both non-truncated. The local shallow evidence repository's tree blob IDs match those two official tree responses exactly, so its direct `git diff` operates on the same source objects.
4. The direct initial-to-current comparison has `14` modified paths, no removed Poly2Tri paths, and `1,407` additions / `1,328` deletions. The same `14` paths are independently found by the official tree blob comparison.
5. All `14/14` direct-delta paths have at least one official post-initial commit in the captured path history. The ledger preserves every ordered SHA set rather than inferring an unverified single origin.
6. A controlled expected-additions change from `1,407` to `1,408` returns exit code `1`, writes a `Fail` report, and retains the observed direct delta. The verifier fails closed on a changed contract.

This closes the prior lack of current Poly2Tri **path-history** attribution. It does not change the separate fixed original-source content-equivalence result in `OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md`.

## Reproduction Checklist

The verifier is offline after the raw official API evidence is collected. It rejects missing, edited, incomplete, paginated, or mismatched input evidence rather than downloading a different history silently.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-assimp-poly2tri-delta-attribution.ps1 `
  -ReportPath artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\poly2tri-delta-attribution-replay.json
```

- [x] Require the official `v5.4.2` tag target and its current Git tree.
- [x] Require a non-truncated official initial/current tree pair and exact local blob-tree match.
- [x] Require one unpaginated `28`-commit path-history response from the latest path commit back to the initial import.
- [x] Verify all `28` raw commit response hashes and summarized Poly2Tri file records.
- [x] Require the fixed direct `14` path / `1,407` addition / `1,328` deletion contract.
- [x] Require all `14` direct-delta paths to have post-initial official-history coverage.
- [x] Require the controlled wrong-additions contract to fail with exit code `1`.
- [ ] Do not use this path-level ledger as a substitute for legal notice review or distribution approval.

## Claim Boundary

- Passed: fixed current-tag path history, official/local tree cross-check, direct net tree delta, and path-to-ordered-commit-set coverage.
- Not passed: final-line-to-single-commit blame, candidate Git-mirror ownership/signature, provenance for other vendored components, notice disposition, binary reproducibility, product integration, or distribution approval.

## Sources

- Assimp Poly2Tri history at `v5.4.2`: https://github.com/assimp/assimp/commits/v5.4.2/contrib/poly2tri
- Assimp `v5.4.2` tag reference: https://api.github.com/repos/assimp/assimp/git/ref/tags/v5.4.2
- Assimp `v5.4.2` commit: https://api.github.com/repos/assimp/assimp/commits/v5.4.2
- Assimp initial Poly2Tri import: https://github.com/assimp/assimp/commit/1ebd116dff21ca8e347676288bfda3056af18a8c
- GitHub Git Trees endpoint: https://docs.github.com/rest/git/trees#get-a-tree
