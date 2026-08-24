# OpenVisionLab 3D Preparation Source-Immutability Qualification

Date: 2026-08-23
Status: Complete
Backlog: `PL-0044` / `M-13`

## Operator and maintenance problem

The current Prepare catalog already contained four typed tools and each tool
already had a Runner golden verifier. Their successful paths did not uniformly
record the input C3D path, byte length, and SHA-256 both before and after
execution. Hosted CI also invoked only the Median Filter golden directly.

That left a regression gap: a preparation adapter could accidentally modify its
source file or retained source snapshot while still producing a plausible
derived result, or one of the four existing suites could silently disappear
from the preparation CI gate.

## Product decision

Keep the four existing Runner verifiers and the existing typed-preparation CI
step as the only owners. Strengthen those suites without adding a product
algorithm, SDK algorithm, verifier framework, dependency, schema, recipe
behavior, or UI surface.

The qualified inventory is exactly the current Prepare category:

| Tool ID | Display name | Runner total |
| --- | --- | ---: |
| `filter` | Median Filter | `13/13` |
| `remove-outlier-pixels` | Remove Outlier Pixels | `9/9` |
| `level-surface` | Level Surface | `9/9` |
| `roi-crop` | ROI / Crop | `6/6` |

`Apply XYZ Affine` and `Re-grid Height Map` remain Transform tools and are not
part of this qualification.

Each included success path now proves:

- the same exact source path is re-read after execution;
- source byte length and SHA-256 are unchanged before and after;
- retained source values and valid/missing counts remain unchanged where the
  suite owns the source object;
- the output has a separate entity and path, is marked derived, has a
  64-character SHA-256, and retains the pre-execution source SHA-256 as
  `RootSourceSha256`;
- the deterministic repeat, adapter, ordered, transform/mask, or saved-output
  parity already owned by that tool remains intact.

The suites deliberately do not require output bytes to differ from source
bytes. A valid no-op preparation may retain identical encoded content while
still creating a separately identified derived result.

## Controlled identities

| Tool | Source bytes | Source SHA-256 before and after | Derived output entity | Output SHA-256 |
| --- | ---: | --- | --- | --- |
| Median Filter | `44` | `6055F66BF8F0FDF0B07163566D65B3FDFE80631AEE53E99E777BF37F8BB67419` | `derived.filtered.01` | `AF4F754125BD88EE8AD27DA0129A2555883BC403ACBB1F7F46EAA11DE018B563` |
| Remove Outlier Pixels | `488` | `FAE710BB1886C2D406F66A507D9B45866D42C184C70F31CE9E7DF9724A5415FC` | `derived.outlier-removed.01` | `08C7B173D30C9ADF0B83CCF7D37DF4A1B3C2B8A15A0D312E9BFAB24263C7DF0E` |
| Level Surface | `776` | `D08AA4FE4377C0CC2A6A43210E98EC8A5E8815374311BA33D1CC40C1861EED52` | `derived.leveled-height.01` | `5BE202FAF610A7291CFD753837B2469A1C10A9F324A8216C4AB0D7CF8CE2A419` |
| ROI / Crop | `128` | `4C160D9032BA9D59C3253CD31695D59B7FB085521801FBB6D7813F13D1E813DC` | `derived.roi-crop.01` | `603FD00EC3C2FABE802D8CCEBAD94CBAD51661B3336FC20298A982ADA3C0CF6D` |

## Verification

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0044-preparation-source-immutability`

| Gate | Current-tree result |
| --- | --- |
| Focused Runner Release build | Pass, `0` warnings / `0` errors |
| 15-project Release build | Pass, `0` warnings / `0` errors |
| Median Filter Runner | Pass, `13/13` |
| Remove Outlier Pixels Runner | Pass, `9/9` |
| Level Surface Runner | Pass, `9/9` |
| ROI / Crop Runner | Pass, `6/6` |
| Preparation completeness aggregate | Pass, `PreparationSourceImmutabilityVerification\|PASS\|tools=4\|passed=4\|failed=0` |
| Affected Workbench regressions | Pass, `14/14`, `17/17`, and `19/19` |
| Tool Recipe teaching regression | Pass, `51/51` |
| Standard test facade | Pass, `2/2` |
| NuGet health | Pass, 15 projects; vulnerable `0`, deprecated `0` |
| Code structure | Pass, `68/68` |
| Vision SDK package | Pass, source `7da6631e...`, expected SHA-256 |

The first isolated full-build attempt used `--no-restore` against a new
`final-artifacts` path and correctly returned `NETSDK1004` because that path had
no `project.assets.json`. Restore plus build succeeded, and a subsequent exact
`--no-restore` Release build passed with zero warnings and zero errors.

The hosted workflow now runs all four existing commands and requires each
complete count marker plus the new source-identity and derived-output evidence
markers. A missing, partial, or reverted tool report cannot produce the 4/4
aggregate. Hosted GitHub Actions itself was not executed locally.

No product or UI source changed for this slice. Therefore no new EXE UI,
Wide/Compact, theme, DPI, pointer, or screenshot evidence is claimed.

## Reusable check

```powershell
dotnet $runner --verify-c3d-filter --report "$artifactDir\median-filter.txt"
dotnet $runner --verify-c3d-remove-outliers --report "$artifactDir\remove-outliers.txt"
dotnet $runner --verify-c3d-level-surface --report "$artifactDir\level-surface.txt"
dotnet $runner --verify-c3d-roi-crop --report "$artifactDir\roi-crop.txt"
```

Require the exact per-tool totals above, each tool's source-identity evidence,
and the exact four-tool aggregate. A command exit code alone is insufficient.

## Completion record

Status: Complete
Scope: Qualify exactly the four current typed Prepare tools for successful
source-file and retained-source immutability while preserving separate derived
output identity and existing deterministic parity.
Acceptance criteria: exact four-tool inventory and Transform exclusion -> Pass;
source path/length/SHA and retained values/counts unchanged -> Pass; separate
derived entity/path/hash/root provenance -> Pass; exact `13/13`, `9/9`, `9/9`,
`6/6` and CI 4/4 evidence gate -> Pass; proportional current-tree checks ->
Pass.
Verification: focused/full Release `0/0`; four Runner suites `13/13`, `9/9`,
`9/9`, `6/6`; Workbench `14/14`, `17/17`, `19/19`; teaching `51/51`; standard
facade `2/2`; NuGet vulnerable/deprecated `0/0`; structure `68/68`; fixed
Vision SDK package identity/checksum pass.
Evidence: this document, `.proofline/issues/PL-0044.json`, and the D-backed
evidence root above.
Boundary / next dependency: no product algorithm, SDK package, UI, schema,
recipe, Preview/Publish/Run, version, R0, release, or physical-metrology
behavior changed. Hosted CI remains unverified until an authorized push.
