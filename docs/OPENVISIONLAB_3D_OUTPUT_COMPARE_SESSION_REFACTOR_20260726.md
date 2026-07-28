# OpenVisionLab 3D Output Compare Session Refactor

Date: 2026-07-26

Status: Complete

## Scope

Move session-only Output Compare candidate, A/B/C pin, and summary state out of
the large `ToolWorkbenchViewModel` state surface without changing its public
binding contract or any recipe, Preview, Publish, Run, or Validation Set
behavior.

Source state: commit `18b72ca` plus the current preserved working tree.

## Responsibility change

| Before | After |
|---|---|
| `ToolWorkbenchViewModel` owns three pin fields and candidate replacement state | `ToolWorkbenchOutputCompareSession` owns candidates, pins, pin preservation, lookup, and summaries |
| Workbench builds candidates and performs session-state replacement | Workbench only builds candidates from recipe/artifact/validation state and passes them to the session |
| Output Compare state has no focused test seam | `ToolArtifactNavigatorVerification` directly verifies session replacement and pin preservation |

The Workbench facade still exposes `CompareCandidates`,
`CompareSlotA/B/CArtifactId`, summaries, and `GetCompareCandidate` so existing
XAML and commands do not need a broad rewrite.

## Preserved boundaries

- Candidate discovery remains in Workbench because it depends on the artifact
  registry, current source, Filter output, and Validation Set samples.
- Viewer creation and C3D loading remain in `OutputCompareView`; they require
  WPF and OpenGL.
- The session never changes recipe routing and never invokes Preview, Publish,
  Run, or Validation Set execution.
- No event bus, interface, factory, or service container was added.

## Structural proof

- The old `compareSlotAArtifactId`, `compareSlotBArtifactId`, and
  `compareSlotCArtifactId` fields are absent from all
  `ToolWorkbenchViewModel*.cs` files.
- `SetCompareSlot` and `DescribeCompareSlot` are no longer Workbench-owned.
- Candidate replacement, lookup, pin preservation, and summary generation call
  `ToolWorkbenchOutputCompareSession`.
- Focused session verification proves a pinned artifact survives candidate
  replacement and receives the refreshed state summary.

## Verification

- Release solution build: `0` warnings, `0` errors.
- Artifact Navigator / Output Compare: `25/25`.
- Validation Set integration: `24/24`.
- Workbench docking: `28/28`.
- Current Release Output Compare screenshot: quality accepted on attempt `1`;
  black ratio `0.0487`, white ratio `0.3441`, luminance `0..255`.

Evidence:

- `artifacts/current/20260726-output-compare-session-refactor/artifact-navigator.txt`
- `artifacts/current/20260726-output-compare-session-refactor/validation-set.txt`
- `artifacts/current/20260726-output-compare-session-refactor/workbench-docking.txt`
- `artifacts/current/20260726-output-compare-session-refactor/output-compare-after.png`
- `artifacts/current/20260726-output-compare-session-refactor/output-compare-after-quality.txt`

A true pre-edit capture was not taken because the next boundary was selected
after orientation. The closest historical baseline is
`artifacts/current/20260723-output-compare-usable-default/after-output-compare-1920x1040.png`.
It is not presented as current-source before evidence. Visual comparison shows
the same functional contract—source pinned in A and empty reversible B/C
slots—while unrelated layout and Viewer presentation changed in later
checkpoints.

## Closure record

Status: Complete

Scope: Output Compare candidates, pins, pin preservation, lookup, and summaries
are owned by a focused session behind the existing Workbench facade.

Acceptance criteria: new session owns state -> pass; old Workbench fields and
state methods absent -> pass; public XAML/API contract preserved -> pass;
candidate replacement retains pins -> pass; current integrations remain green
-> pass.

Verification: Release build `0/0`; Artifact Navigator `25/25`; Validation Set
`24/24`; docking `28/28`; current Release screenshot quality pass.

Evidence: `artifacts/current/20260726-output-compare-session-refactor/`.

Boundary / next dependency: this proves the structural state-owner change and
current software behavior only. It does not prove the owner's unaided
first-recipe replay or physical calibration/metrology.
