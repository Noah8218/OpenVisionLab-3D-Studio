# Public-safe Synthetic Thickness Sample Migration

Date: 2026-07-28

Current tracking: `PL-0003` owns the remaining GitHub object cleanup. On
2026-08-05, an authenticated owner/admin audit deleted all 57 retired-lineage
Actions artifacts and preserved all 14 sanitized-lineage artifacts, including
the sanitized-root artifact. GitHub Support ticket `#4633618` (`Clear Cached
Views`) is now Open, while the former `main` commit remains addressable by old
SHA in both the parent repository and fork network. Closure therefore depends
on GitHub processing the ticket and a fresh object-access check.

## Decision

The previously used Thickness C3D originated from non-public company work.
It and all source-specific derived identities are therefore not acceptable as
public demo or regression material.

The public replacement is `Thickness Coupon v1`, a wholly fictional
inspection coupon. AI image generation was used only to choose a generic
non-sensitive visual layout. The C3D values, missing-cell mask, ROI locations,
recipe, and expected measurements are generated deterministically by code.

## Package

`3D/Samples/ThicknessCouponV1/`

- `ai-concept.png`: visual ideation; never used as numeric input.
- `thickness-coupon-v1.C3D`: deterministic `1280 x 840` C3D.
- `source-height-preview.png`: generated height-map and ROI review image.
- `ground-truth.json`: provenance, hashes, coverage, ROI coordinates, and
  eight expected signed separations.
- `inspection-recipe.ov3d-recipe.json`: schema `1.5`, eight Thickness steps,
  16 artifact-owned ROI selections, and explicit dual-ROI role routing.
- `README.md`: reproduction and interpretation guide.

Generator:

```powershell
python scripts/generate-thickness-coupon-sample.py `
  --output 3D/Samples/ThicknessCouponV1
```

## AI concept prompt

The concept requested a fictional rectangular calibration coupon with exactly
eight raised rounded pads in a `4 x 2` grid, a narrow reference ledge beside
each measurement plateau, cross-shaped channels, chamfered corners, three
asymmetric fiducials, and a cyan-to-yellow height-map treatment. It explicitly
forbade branding, serial numbers, text, people, machinery, and resemblance to
a commercial part.

The generated concept is an appearance reference only. Diffusion output is
not a metrology source and contributes no height value to the C3D.

## Deterministic source contract

| Field | Value |
| --- | --- |
| Grid | `1280 x 840` |
| Cells | `1,075,200` |
| Valid | `908,436` (`84.5%`) |
| Missing | `166,764` (`15.5%`) |
| C3D bytes | `4,300,808` |
| C3D SHA-256 | `D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8` |
| Invalid-map bytes | `134,400` |
| Invalid-map SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |
| Height Image pixel SHA-256 | `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087` |
| Unit | `raw-height` |
| Frame | `frame.c3d-grid-index` |

The datum is an affine height plane. Each reference ROI samples that plane and
each measurement ROI samples a parallel plateau.

| Pad | Expected signed separation | Acceptance |
| --- | ---: | ---: |
| 1 | 8 | 7.75–8.25 |
| 2 | 12 | 11.75–12.25 |
| 3 | 16 | 15.75–16.25 |
| 4 | 20 | 19.75–20.25 |
| 5 | 10 | 9.75–10.25 |
| 6 | 14 | 13.75–14.25 |
| 7 | 18 | 17.75–18.25 |
| 8 | 22 | 21.75–22.25 |

## Verification evidence

Commands:

```powershell
dotnet run --no-build `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj `
  -c Release -- `
  --tool-recipe 3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json `
  --report artifacts/current/20260728-synthetic-thickness-coupon/runner.txt `
  --run-record artifacts/current/20260728-synthetic-thickness-coupon/run-record.json `
  --html-report artifacts/current/20260728-synthetic-thickness-coupon/run-report.html `
  --csv-report artifacts/current/20260728-synthetic-thickness-coupon/run-report.csv `
  --expect-status Pass
```

Result: production Runner `Pass (8/8 steps)`. Measured means are within
float32 rounding distance of all eight authored values.

The same artifact folder contains the Source Quality JSON and Height Image
probe. They prove source hash, native mapping, coverage, invalid-map parity,
and display-pixel identity.

## Data-safety boundary

- Do not reintroduce the retired company-derived source, filename, hash,
  screenshots, GIF, ROI coordinates, or source-specific statistics.
- Local untracked copies are outside this migration and must not be deleted
  without explicit owner instruction.
- A new commit that deletes a file does not remove it from older Git commits.
  The remote feature branch requires a separately approved history rewrite or
  branch replacement before its old blobs are actually removed.
- `raw-height` proves software behavior only. It is not millimetres,
  calibrated depth, certified thickness, or production metrology.

## Remote Git exposure audit

Read-only inspection on 2026-07-28 found that a normal cleanup commit is not
enough:

| Reachable ref | Retired content still reachable |
| --- | --- |
| `origin/main` | two captured C3D path aliases sharing one `10,236,276`-byte blob, plus two captured PNG aliases |
| `origin/codex/3d-workbench-line-fit` | the same captured C3D/PNG blobs plus the source-specific generated recipe, generator script, historical document path, and old README GIF |
| `v0.1.0-rc.1` | the same two captured C3D and PNG aliases |

The owner approved a coordinated sanitized-root replacement. On 2026-07-28,
both public branches were force-updated with exact `--force-with-lease`
expectations:

| Updated ref | Sanitized root |
| --- | --- |
| `refs/heads/main` | `6936af87a80f29f43c8e27ce34abf55c37dd8f97` |
| `refs/heads/codex/3d-workbench-line-fit` | `6936af87a80f29f43c8e27ce34abf55c37dd8f97` |

The retired `refs/tags/v0.1.0-rc.1` tag was deleted locally and from `origin`.
The sanitized root has no parent, contains 716 files, and passed both the
retired-path scan and retired-identifier/hash scan with zero findings.

This invalidates previous commit IDs, clones, links, and comparisons. Removing
reachable refs does not prove immediate physical deletion from GitHub caches,
forks, Actions artifacts, or third-party clones. If the retired blobs must be
made inaccessible by old object URL as well, the repository owner must request
GitHub sensitive-data cleanup and separately audit those external copies.

## Post-push external retention audit

The post-push audit distinguishes a clean public tip from full remote erasure:

- `origin/main` and `origin/codex/3d-workbench-line-fit` both resolve to
  `1c90b8374adb7a66c35355fd1cfd6734df272b0c`.
- Both tips contain 716 files and pass the retired-path and
  retired-identifier/hash scans with zero findings.
- `git ls-remote --tags origin` returns no tags and the GitHub Releases API
  returns no releases.
- The GitHub commit API still returns HTTP `200` for the former `main` tip.
  Git ref replacement therefore has not yet made every old object URL
  inaccessible.
- The Actions API reports 58 unexpired artifacts: one belongs to the new
  sanitized root and 57 belong to historical commits.
- The historical workflow generated a PLY from the retired C3D and then
  uploaded `artifacts/ci/**`; the 57 historical artifacts must therefore be
  treated as potentially derived from the retired source until deleted or
  independently inspected.

## PL-0003 authenticated re-audit and artifact cleanup

The 2026-08-05 audit authenticated as the repository owner with admin and push
permission. It inspected GitHub metadata only; no Actions archive or retired
sample content was downloaded or redistributed.

- The current remote has two branch heads, no tags, no releases, and no pull
  requests.
- The Actions API returned 71 unexpired artifacts before cleanup. An ancestry
  check against sanitized root
  `6936af87a80f29f43c8e27ce34abf55c37dd8f97` classified exactly 57 as retired
  lineage and 14 as sanitized lineage.
- All 57 retired-lineage artifact deletions returned HTTP `204`. The
  authenticated post-delete inventory contains 14 artifacts, with zero target
  artifacts remaining and zero preserved artifacts missing.
- Sanitized-root artifact `8678950407` remains present. The other 13
  sanitized-lineage artifacts also remain present.
- The former `main` tip still returns HTTP `200` by commit SHA. The one public
  fork has a sanitized `main` head and no tags, but the same old SHA remains
  addressable through the fork network.
- GitHub Support ticket `#4633618`, `Clear Cached Views`, was submitted through
  the authenticated Virtual Agent flow and is Open. The request identifies the
  dangling commit and asks GitHub to clear cached/internal references without
  deleting the repository or its sanitized history. No private sample content
  was uploaded.
- Immediately after submission, the old commit endpoints in both the parent
  repository and fork network still returned HTTP `200`; this is an expected
  pending state, not proof that GitHub has completed the cleanup.

Reusable evidence is stored at
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260805-pl0003-github-retention\`.

## Completion record

Status: Blocked

Scope: Public-safe synthetic C3D, ground truth, preview, eight-step Thickness
recipe, current-tree source defaults, legacy recipe inputs, CI map probe,
README media, and current-tree documentation.

Acceptance criteria:

- fictional, non-captured source -> pass;
- reproducible `1280 x 840` C3D and recorded identity -> pass;
- eight independent dual-ROI Thickness steps -> pass;
- production Runner replay -> pass, `8/8`;
- current-tree documentation/media sanitization -> pass, sensitive identifier
  scan `0`;
- fresh synthetic-only README GIF -> pass, Wide actual UI replay and
  save/reopen;
- reachable remote branch history replacement -> pass, both public heads now
  descend from the parentless sanitized root;
- retired release tag removal -> pass, tag absent from local and remote refs;
- old GitHub object URL inaccessible -> fail, former commit API response is
  HTTP `200`;
- historical Actions artifact cleanup -> pass, 57/57 deletions returned HTTP
  `204`, post-delete target count `0`;
- sanitized-lineage artifact preservation -> pass, 14/14 remain, including the
  sanitized-root artifact;
- GitHub Support cleanup request -> pass, ticket `#4633618` is Open;
- resulting object-access outcome -> fail/pending, GitHub has not yet completed
  the request and both checked old-commit endpoints still return HTTP `200`.

Verification: deterministic regeneration; Release build `0/0`; generic Tool
Recipe Runner `8/8`; repeat authoring `20/20`; recipe selections `29/29`;
height measurement `46/46`; teaching `28/28`; structure `17/17`; Source
Quality and Height Image probes; current Wide actual UI ROI replay,
Ctrl+S/save/reopen; README local-link check.

Evidence: `artifacts/current/20260728-synthetic-thickness-coupon/`, parentless
root `6936af87a80f29f43c8e27ce34abf55c37dd8f97`, post-rewrite audit tip
`1c90b8374adb7a66c35355fd1cfd6734df272b0c`, post-push
`git ls-remote`/tree/content scans, and read-only GitHub commit, release, and
Actions artifact API audits.

PL-0003 current evidence: authenticated before/after inventories, the exact
57-item delete target set, 57 deletion responses, the 14-item preserve set,
SHA-256 evidence manifest, Support request draft, and ticket `#4633618` record under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260805-pl0003-github-retention\`.

Boundary / next dependency: GitHub Support processes open ticket `#4633618`;
after GitHub reports completion, recheck and record the parent and fork-network
object-access outcome. Forks and third-party clones remain
separately owned external copies; existing local clones must be replaced with
a fresh clone. Local untracked files remain untouched.
