# Completeness Validation Set and threshold assistance closure

Date: 2026-07-29

Backlog scope: `H-11`, `H-12`, and `I-14`

## Outcome

Completeness Grid now participates in the existing evidence-based threshold
workflow without introducing a second assistant or bypassing explicit
execution.

- One controlled recipe replays two Good, two Bad, and one Held-out C3D
  sample.
- The authored `0.8 / 0 / 6 raw-height` policy produces real
  `Good=Pass`, `Bad=Fail`, and `HeldOut=Pass` results.
- Held-out observations remain visible but never enter candidate boundaries,
  ranking, confusion counts, or development decisions.
- Each sample contributes three policy-equivalent observations:
  `Minimum finite coverage`, `Minimum reference-relative mean`, and
  `Maximum reference-relative mean`.
- Every derived observation and candidate decision preserves the exact
  row-major cell locator, such as `r001.c002`.
- The shared threshold report contract is `2.1`.

## Candidate semantics

The assistant reduces each sample to the worst cell for each global policy
parameter. This prevents an average over healthy cells from hiding one
defective cell.

| Sample-level observation | Candidate rule | Typed recipe parameter |
| --- | --- | --- |
| Minimum finite coverage | Minimum | `MinimumFiniteCoverageRatio` |
| Minimum reference-relative mean | Minimum | `MinimumReferenceRelativeMeanRawHeight` |
| Maximum reference-relative mean | Maximum | `MaximumReferenceRelativeMeanRawHeight` |

The controlled fixture produces three supported zero-error candidates:

- minimum finite coverage `1`;
- minimum reference-relative mean `2 raw-height`;
- maximum reference-relative mean `4.5 raw-height`.

Other generic scalar candidates remain review-only and fail closed when no
explicit typed mapping exists.

## Explicit lifecycle

```text
Run labeled development + Held-out set
  -> Review candidate
  -> Cancel, or Apply to PropertyGrid draft only
  -> explicit development-only replay of the projected Completeness draft
  -> unlock separate explicit Held-out replay
```

Review and Cancel do not mutate the recipe or execute inspection. Candidate
Apply changes only the mapped PropertyGrid draft. For Completeness, Held-out
replay stays locked until the explicit development replay completes with no
Good/Bad expected-role mismatch. Thickness and Warpage retain their existing
contracts.

## UI result

The threshold error table now shows `Cell evidence` beside each exact sample
decision. Empty parameter, development, and Held-out evidence tables collapse
until they contain evidence. The Validation Set analysis height was increased
so the candidate and decision rows remain visible in the normal Wide layout.

Fresh current-Release captures:

- `artifacts/current/20260729-completeness-threshold-assistance/before-completeness-threshold-wide.png`;
- `artifacts/current/20260729-completeness-threshold-assistance/after-completeness-threshold-wide.png`;
- `artifacts/current/20260729-completeness-threshold-assistance/after-completeness-threshold-compact.png`.

## Verification

Commands actually run:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe `
  --verify-validation-set `
  artifacts/current/20260729-completeness-threshold-assistance/validation-set.txt

OpenVisionLab.ThreeD.Runner.exe `
  --verify-c3d-completeness-grid `
  --report artifacts/current/20260729-completeness-threshold-assistance/completeness-golden.txt

OpenVisionLab.ThreeD.Runner.exe `
  --labeled-validation-recipe <controlled-completeness-recipe> `
  --report artifacts/current/20260729-completeness-threshold-assistance/runner-completeness-evidence.json

OpenVisionLab.ThreeD.Shell.exe `
  --verify-inspection-workspace-selection <report>
OpenVisionLab.ThreeD.Shell.exe `
  --verify-recipe-manager-wpg <report>
OpenVisionLab.ThreeD.Shell.exe `
  --verify-workbench-docking <report>
OpenVisionLab.ThreeD.Shell.exe `
  --verify-shell-smoke-command-line <report>
powershell -File scripts/verify-code-structure.ps1 -ReportPath <report>
```

Results:

- Release build: `0` warnings, `0` errors;
- Validation Set: `82/82`;
- Completeness golden: `23/23`;
- Inspection Workspace: `63/63`;
- Recipe Manager / typed PropertyGrid: `37/37`;
- Workbench docking: `33/33`;
- Shell command-line options: `24/24`;
- code structure: `17/17`;
- production Runner: report schema `1.1`, threshold contract `2.1`,
  `57` candidates from `4` development samples, `1` Held-out excluded,
  `0` evidence warnings, and `8` exact mappings;
- Wide and Compact screenshot quality: accepted.

## Completion record

Status: Complete

Scope: Good/Bad/Held-out Completeness examples, worst-cell coverage/relative
height observations, exact sample/cell error evidence, three fail-closed
typed mappings, explicit Review/Cancel/draft Apply, development replay gate,
and separate Held-out replay.

Acceptance criteria: real Pass/Fail/Held-out replay -> pass; Held-out excluded
from candidate generation -> pass; exact sample/cell evidence -> pass;
supported coverage/lower-height/upper-height mappings -> pass; non-mutating
Review/Cancel -> pass; draft-only Apply -> pass; development and Held-out
replays separate -> pass; Runner parity -> pass; current UI evidence -> pass.

Verification: Release `0/0`; Validation Set `82/82`; Completeness `23/23`;
Inspection Workspace `63/63`; PropertyGrid `37/37`; docking `33/33`; Shell
options `24/24`; structure `17/17`; Runner contract `2.1`; capture quality
accepted.

Evidence:
`artifacts/current/20260729-completeness-threshold-assistance/`.

Boundary / next dependency: this proves deterministic software workflow on a
controlled native-grid fixture. It does not prove physical calibration,
production tolerance, certified metrology, detected-region routing, or a
real GPT correction transcript. The next eligible product slice is
`J-01/J-03/J-04 SurfaceModel preparation foundation`.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high
