# OpenVisionLab 3D Completeness Known-cell Golden Qualification

Date: 2026-08-23
Status: Complete
Backlog: `PL-0042` / `M-15`

## Operator and maintenance problem

The product already had a deterministic Completeness golden verifier, but the
canonical inventory still classified the known-cell suite as New. Hosted CI
also treated any zero-exit report as sufficient and did not prove that all
current cases ran. That gap could hide a partial or stale passing report.

## Product decision

Keep the existing Runner verifier as the single contract owner. Do not add a
second framework, repeat its assertions, or change Completeness execution.
Qualify the current `30/30` matrix and make the existing CI step require its
exact complete header:

```text
C3DCompletenessGridGoldenVerification|PASS|cases=30|passed=30|failed=0
```

## Qualified contract

The controlled `8 x 8` source produces four stable row-major cells:

| Cell | Region row,column,size | Coverage | Missing | Relative height | Decision |
| --- | --- | ---: | ---: | ---: | --- |
| `r001.c001` | `2,0,2x2` | `1` | `0` | `2` | Pass |
| `r001.c002` | `2,2,2x2` | `0.75` | `1` | `4` | Fail |
| `r002.c001` | `4,0,2x2` | `0.5` | `2` | `-2` | Pass |
| `r002.c002` | `4,2,2x2` | `0` | `4` | missing | Fail |

The inclusive policy therefore produces exactly two passing and two failing
cells, and the all-missing cell fails closed. The suite additionally proves:

- exact reference mean and stable cell ordering;
- immutable source identity;
- equal direct, repeated, adapter, and ordered Runner content identities;
- exact schema `1.9` JSON, readable HTML, and structured CSV child rows;
- rejection of missing or malformed current cell evidence;
- readability of legacy schema `1.8` evidence;
- all-pass, all-missing, invalid-policy, and out-of-footprint boundaries.

## Verification and evidence

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0042-completeness-known-cell-golden`

The baseline current verifier passed `30/30` before the CI/documentation
change. Its controlled identities were:

- source SHA-256:
  `634CEA27B3483D51173145884D78B88A313D596D048358F14456B0312AAB0042`;
- evidence-only output SHA-256:
  `C535D7C8DF40C585E5A22EBF5594D48768A89A20DF257A82DE6F3E75752BED6C`;
- policy output SHA-256:
  `1B051233FFCCC65FD72A4CB50299C629C8BCE7929E7AC4CA3CA3F33653DBF8CE`.

| Gate | Current-tree result |
| --- | --- |
| 15-project Release build | Pass, `0` warnings / `0` errors |
| Completeness golden | Pass, `30/30`; all 12 selected exact contract lines present |
| Standard test facade | Pass, `2/2` |
| NuGet health | Pass, 15 projects; vulnerable `0`, deprecated `0` |
| Code structure | Pass, `68/68` |
| Vision SDK package | Pass, source `7da6631e...`, expected SHA-256 |
| Inventory and documentation checks | Pass, `147 C / 47 N`, link target present |

The first isolated build attempt used one shared `BaseIntermediateOutputPath`
for every project and failed because their generated assets collided. The
correct repository-supported `--artifacts-path` invocation then restored and
built the same current tree successfully; both logs are retained. This
qualification does not claim a new algorithm, UI runtime review, hosted-CI
execution, physical metrology, or release acceptance.

## Reusable check

Run the existing command and require both a zero exit and the exact header:

```powershell
dotnet run --no-build --project $runnerProject -c Release -- `
  --verify-c3d-completeness-grid `
  --report "$artifactDir\completeness-grid.txt"
```

## Completion record

Status: Complete
Scope: Qualify the existing Completeness known-cell golden suite and close the
partial-report gap in its existing CI step.
Acceptance criteria: exact four-cell matrix -> Pass; inclusive decisions and
aggregate -> Pass; deterministic/source/export/compatibility contracts ->
Pass; exact CI complete-report gate -> Pass; current-tree proportional checks
-> Pass.
Verification: Release `0/0`; Completeness `30/30`; standard facade `2/2`;
NuGet vulnerable/deprecated `0/0`; structure `68/68`; fixed Vision SDK package
identity/checksum pass; inventory and documentation checks pass.
Evidence: this document, `.proofline/issues/PL-0042.json`, and the D-backed
evidence root above.
Boundary / next dependency: no product or verifier assertion changed; hosted
CI, owner R0, physical metrology, and large-C3D qualification are outside this
closure.
