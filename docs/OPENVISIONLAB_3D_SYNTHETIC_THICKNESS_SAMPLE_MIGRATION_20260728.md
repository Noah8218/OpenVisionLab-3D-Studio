# Public-safe Synthetic Thickness Sample Migration

Date: 2026-07-28

## Decision

The previously used Thickness C3D originated from non-public company work.
It and all source-specific derived identities are therefore not acceptable as
public demo or regression material.

The public replacement is `Synthetic Thickness Coupon v1`, a wholly fictional
inspection coupon. AI image generation was used only to choose a generic
non-sensitive visual layout. The C3D values, missing-cell mask, ROI locations,
recipe, and expected measurements are generated deterministically by code.

## Package

`3D/SyntheticValidation/ThicknessCouponV1/`

- `ai-concept.png`: visual ideation; never used as numeric input.
- `synthetic-thickness-coupon-v1.C3D`: deterministic `1280 x 840` C3D.
- `source-height-preview.png`: generated height-map and ROI review image.
- `ground-truth.json`: provenance, hashes, coverage, ROI coordinates, and
  eight expected signed separations.
- `inspection-recipe.ov3d-recipe.json`: schema `1.5`, eight Thickness steps,
  16 artifact-owned ROI selections, and explicit dual-ROI role routing.
- `README.md`: reproduction and interpretation guide.

Generator:

```powershell
python scripts/generate-synthetic-thickness-coupon.py `
  --output 3D/SyntheticValidation/ThicknessCouponV1
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
| C3D SHA-256 | `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937` |
| Invalid-map bytes | `134,400` |
| Invalid-map SHA-256 | `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358` |
| Height Image pixel SHA-256 | `D6B402B870622F25C73C10C6D312DF1BB8EC837BC3EFC7A9B5BA8FB8EF432C4A` |
| Unit | `synthetic-height-unit` |
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
  --tool-recipe 3D/SyntheticValidation/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json `
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
- `synthetic-height-unit` proves software behavior only. It is not millimetres,
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
- historical Actions artifact cleanup -> fail, 57 pre-sanitization artifacts
  remain unexpired.

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

Boundary / next dependency: the repository owner must authorize deletion of
the 57 historical Actions artifacts and authenticate an account capable of
that deletion. The owner must also submit a GitHub sensitive-data cleanup
request for the still-addressable old objects. Preserve the one artifact whose
head is the sanitized root. Forks and third-party clones require separate
coordination; existing local clones must be replaced with a fresh clone. Local
untracked files remain untouched.
