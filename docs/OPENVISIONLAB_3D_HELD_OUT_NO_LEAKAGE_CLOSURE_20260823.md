# OpenVisionLab 3D Good/Bad/Held-out No-leakage Qualification

Date: 2026-08-23
Status: Complete
Backlog: `PL-0043` / `M-14`

## Operator and maintenance problem

Validation Set already separated Good and Bad development evidence from
Held-out evidence. The current verifier proved that Held-out samples were
visible, marked `IncludedInDevelopment=false`, recorded as excluded, and
absent from candidate decisions. It did not directly prove the stronger
counterfactual: changing only Held-out content and identity must leave every
development suggestion unchanged.

Without that comparison, a future accidental use of Held-out data in a hidden
boundary or tie-break could evade the visible zero-decision assertion.

## Product decision

Keep `ToolRecipeThresholdCandidateAnalyzer` and the existing Validation Set
verifier as the single owners. Add one adversarial fixture and one focused
check; add no product algorithm, duplicate verifier, framework, or CI command.

The development inputs remain:

| Role | Mean raw-height evidence |
| --- | ---: |
| Good | `2` |
| Good | `4` |
| Bad | `-10` |
| Bad | `20` |

The ordinary Held-out input is `3`. The alternate is `1,000,000`, producing a
different C3D SHA-256. Only the excluded Held-out identity may change. The
serialized development fingerprint contains candidate identities and order,
limits, counts, exact sample decisions, warnings, and typed evidence warnings.
It must remain identical.

## Verification

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0043-heldout-no-leakage`

| Gate | Current-tree result |
| --- | --- |
| Focused Shell Release build | Pass, `0` warnings / `0` errors |
| 15-project Release build | Pass, `0` warnings / `0` errors |
| Validation Set | Pass, `87/87`; exactly 87 PASS and zero FAIL lines |
| Counterfactual Held-out check | Pass; identity changed, development fingerprint unchanged, candidates `48` |
| Direct Runner labeled evidence | Pass; samples `5`, development `4`, Held-out `1`, candidates `48`, decisions `192`, Held-out decisions `0` |
| Standard test facade | Pass, `2/2` |
| NuGet health | Pass, 15 projects; vulnerable `0`, deprecated `0` |
| Code structure | Pass, `68/68` |
| Vision SDK package | Pass, source `7da6631e...`, expected SHA-256 |

Controlled evidence identities:

- ordinary Held-out C3D:
  `D9384A7B5A032D28E952E8742619EA224F2763FC5B5B3C431DC895544AA93C3B`;
- alternate extreme Held-out C3D:
  `C8E5E3B630377A08B1320123AD7F62A21BC3209F28876227755033902630B9FC`;
- final Validation Set report:
  `3E90679954A803C51C19EF03EC88F60FE2A49F7A41B7A0B67AE5F0DBF56720BC`;
- final Runner JSON:
  `B49EFDBE8D3349C7CF1BA03213825618C13099AEB32EC285C3E5AD7FEF7E6E31`.

The existing hosted workflow already invokes `--verify-validation-set`. The
verifier succeeds only when all exactly 87 cases pass, so no duplicate CI step
or report parser was added.

## Reusable check

```powershell
dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Release -- --verify-validation-set "$artifactDir\validation-set.txt"
```

For future no-leakage changes, keep development inputs fixed, change only the
Held-out value and identity, and compare the full development fingerprint.
Checking only that Held-out decisions are absent is insufficient.

## Completion record

Status: Complete
Scope: Add one counterfactual Held-out fixture to the existing Validation Set
suite and qualify the Good/Bad/Held-out split against hidden suggestion
leakage.
Acceptance criteria: roles and development flags -> Pass; exact development
and Held-out counts -> Pass; alternate Held-out identity changes -> Pass;
complete candidate/warning/decision fingerprint unchanged -> Pass; current
Runner and existing CI ownership retained -> Pass; proportional current-tree
checks -> Pass.
Verification: focused/full Release `0/0`; Validation Set `87/87`; Runner
`5/4/1/48/192/0`; standard facade `2/2`; NuGet vulnerable/deprecated `0/0`;
structure `68/68`; fixed Vision SDK package identity/checksum pass.
Evidence: this document, `.proofline/issues/PL-0043.json`, and the D-backed
evidence root above.
Boundary / next dependency: no UI, product analysis, schema, recipe,
Preview/Publish/Run, version, R0, release, or physical-metrology behavior
changed. Hosted CI itself was not executed locally.
