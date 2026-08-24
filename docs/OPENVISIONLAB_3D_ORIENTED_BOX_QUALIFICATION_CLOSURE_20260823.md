# OpenVisionLab 3D OrientedBox3D Qualification

Date: 2026-08-23
Status: Complete
Backlog: `PL-0045` / `M-12`

## Operator and maintenance problem

`OrientedBox3D` already had schema `1.4`, numeric authoring, Viewer handles,
and an actual-pointer smoke. The reusable contract verifier covered only eight
box cases, Runner had no dedicated qualification route, and the current hosted
workflow did not require a complete OrientedBox subset. The available pointer
evidence was also historical rather than produced by the current Release
binary.

Runtime reassessment exposed two interaction-evidence defects. Leaving a box
handle could retain its cursor and Viewer status, and the smoke could assume a
normal state or a handle hit before the routed WPF hover event had actually
arrived. The latter occasionally sent the first drag to camera orbit instead
of the box.

## Product decision

Keep the existing owners and strengthen their observable contracts:

- `ToolRecipeSelectionContractVerification` remains the only schema and
  geometry assertion owner;
- `ToolRecipeOrientedBox3DGeometry` remains the production finite, positive,
  orthonormal, and right-handed geometry owner;
- Runner adds only a thin route to the shared verifier;
- the existing Viewer editor, eight handles, seven gestures, and explicit
  Apply/Cancel boundary remain unchanged;
- hover exit restores the prior status and Arrow cursor, while the smoke first
  establishes normal state and requires the routed hover mode before a drag.

No second geometry implementation, verifier framework, UI framework, schema
version, SDK algorithm, or downstream inspection consumer was added.

## Exact contract matrix

The shared report now requires these eleven named cases as an exact set:

| Contract group | Required cases |
| --- | --- |
| Valid persistence | schema `1.4` rotated right-handed box; current-schema acceptance; exact center/axes/half-extents save and reopen |
| Schema/payload rejection | schema `1.3`; mixed rectangle payload |
| Fail-closed geometry | zero axis; finite non-unit axis; parallel/non-orthogonal axes; left-handed basis; non-finite center/axis/extent; non-positive half-extent |

Success requires both independent report lines:

```text
OrientedBox3DContractVerification|PASS|cases=11|passed=11|failed=0
Result: Pass (32/32 checks)
```

The exact case-name set prevents a different passing case from silently
replacing a required case. Any `FAIL |` line, including fixture-cleanup
failure, now makes the shared verifier and the affected Workbench verifier
fail. Runner returns `0` on success, `5` on verifier failure, and `2` when the
required report path is absent. CI requires the Runner exit code and both exact
report lines; its Workbench and Shell routes also retain exact authoring,
round-trip, and pointer-option gates.

## Runtime interaction qualification

Affected control and states:

- Viewer `OrientedBox3D` outline and eight fixed-radius handles;
- normal, actual routed hover, actual pointer-down/up, pointer movement,
  mouse-leave recovery, Arrow/handle cursor recovery, and Viewer status
  recovery;
- Perspective move, X/Y/Z resize, and local-Y rotation; Top Y resize; Side
  collapsed-axis X resize;
- preserved selection identity, authored recipe, Preview/result state, and
  camera for every box gesture.

The SharpGL pointer surface has no separate visible keyboard-focus contract.
The smoke activates the Shell and verifies the Windows pointer target before
input. Existing Workbench verification separately covers explicit Apply,
global Esc/Cancel, validation error, reapply, Delete, save, and reopen.

The final current-build Release runs were repeated twice at each supported
layout. All four runs passed, and every gesture used its first routed-hover
targeting attempt:

| Layout | Size | Repetitions | Pointer result | Screenshot quality |
| --- | ---: | ---: | --- | --- |
| Compact | `1280 x 760` | `2/2` | seven gestures, three projections, eight handles, all interaction states true | accepted on attempt 1 in both runs |
| Wide | `1920 x 1040` | `2/2` | seven gestures, three projections, eight handles, all interaction states true | accepted on attempt 1 in both runs |

Visual inspection of the application-only captures confirmed that the box,
handles, Review bar, Viewer status, and bottom coordinate/status boundaries
are visible without overlap or unreachable content. Compact keeps its existing
responsive collapsed numeric editor and deliberate long-caption ellipsis;
Wide exposes the numeric editor. This slice does not claim localization
completion: remaining English Viewer/editor text stays owned by `M-19`.

The workstation reported exactly two independent monitors. The dynamically
selected smaller left monitor was `\\.\DISPLAY2`, logical bounds
`-1920,365,0,1445`. Runtime evidence records physical bounds
`-2400,456,0,1806`, working area `-2400,456,0,1746`, and an intersecting Shell
window for both sizes. Those logical/physical bounds record the current 125%
effective scaling. DPI 100%, 150%, 175%, and 200% were not available in this
run and remain unverified.

## Changed owners

- `src/OpenVisionLab.ThreeD.Data/Verification/ToolRecipeSelectionContractVerification.cs`
- `src/OpenVisionLab.ThreeD.Runner/Application/RunnerCommandRouter.cs`
- `src/OpenVisionLab.ThreeD.Shell/Verification/Workbench/InspectionWorkspaceSelectionVerification.cs`
- `src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.OrientedBox3D.cs`
- `src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.OrientedBox3DSmoke.cs`
- `src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.Viewport.cs`
- `.github/workflows/ci.yml`

## Verification and evidence

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0045-oriented-box-qualification`

| Gate | Current-tree result | Evidence |
| --- | --- | --- |
| 15-project Release build | Pass, `0` warnings / `0` errors | `final/build-release.txt` and `final-artifacts` |
| Runner OrientedBox subset | Pass, exact `11/11`; full `32/32` | `final/contracts/oriented-box-3d.txt` |
| Runner help / missing report | exit `0` / exit `2`, exact usage | `final/contracts/command-exit-codes.txt`, `runner-help.txt`, `runner-missing-report.txt` |
| Shell selection contracts | Pass, `32/32` | `final/contracts/tool-recipe-selections.txt` |
| Inspection Workspace | Pass, `67/67` | `final/contracts/inspection-workspace-selection.txt` |
| Shell option routing | Pass, `46/46` | `final/contracts/shell-smoke-command-line.txt` |
| Tool Recipe teaching | Pass, `51/51` | `final/contracts/tool-recipe-teaching.txt` |
| Standard test facade | Pass, `2/2` | `final/standard-tests-current-final/*.trx` |
| Code structure | Pass, `68/68` | `final/code-structure.txt` |
| NuGet health | Pass, 15 projects; vulnerable `0`, deprecated `0` | `final/nuget-package-health.txt` |
| Vision SDK package | Pass, `3.0.1-dev.20260823.crop.1`, source `7da6631e...`, expected SHA-256 | `final/vision-sdk-package.txt` |
| Compact actual-pointer repeat | Pass, `2/2` | `final/runtime/compact-routed-hover-final-1` and `-2` |
| Wide actual-pointer repeat | Pass, `2/2` | `final/runtime/wide-routed-hover-final-1` and `-2` |

The pre-change PL-0044 apphost baseline is retained at
`baseline-runtime/compact/pointer.txt`; it failed before a screenshot because
the Perspective C3D fit was unavailable. An intermediate final run is also
retained at `final/runtime/compact/pointer.txt`; all seven gestures ran, but it
exposed the smoke's unnormalized hover/leave assumption. A later retained run
at `final/runtime/compact-current-final/pointer.txt` exposed the computed-hit
versus routed-hover race. Neither failed record is presented as passing
evidence.

The workflow YAML was independently inspected and preserves valid PowerShell
block structure. A YAML parser package was not installed locally, and hosted
GitHub Actions was not executed. The source gate becomes hosted evidence only
after an explicitly authorized push and successful Actions run.

## Reusable checks

```powershell
dotnet $runner --verify-oriented-box-3d --report "$artifactDir\oriented-box-3d.txt"
```

Require exit `0`, the exact `11/11` subset line, and the exact `32/32` full
result. For actual UI evidence, run the current Release Shell apphost once at
each supported size with the same recipe and step:

```powershell
& $shellExe `
  --smoke-software-rendering `
  --tool-teaching-recipe 3D\Samples\ThicknessCouponV1\oriented-box-demo.ov3d-recipe.json `
  --tool-teaching-step step.oriented-box-authoring.01 `
  --smoke-oriented-box-pointer-report $pointerReport `
  --shell-smoke-leftmost `
  --shell-smoke-width $width `
  --shell-smoke-height $height `
  --shell-smoke-screenshot $screenshot `
  --shell-screenshot-quality-report $qualityReport
```

Require `1280/760` and `1920/1040` separately, all seven gesture rows, all
interaction-state booleans true, the stable PASS marker, accepted screenshot
quality, and `intersects=True`.

## Completion record

Status: Complete
Scope: Qualify the existing `OrientedBox3D` schema, fail-closed geometry,
Runner/CI completeness, and current Wide/Compact actual-pointer interaction
without adding a consumer or changing explicit Apply/Run behavior.
Acceptance criteria: schema/current-schema rotated round-trip and old/mixed
rejection -> Pass; zero/non-unit/parallel/left-handed/non-finite/non-positive
geometry rejection -> Pass; exact named Runner subset and CI gate -> Pass;
current Wide/Compact seven-gesture interaction and recovery -> Pass; current
proportional regressions and durable documentation -> Pass.
Verification: Release `0/0`; shared `32/32` with exact OrientedBox `11/11`;
Workbench `67/67`; Shell `46/46`; teaching `51/51`; standard tests `2/2`;
structure `68/68`; NuGet vulnerable/deprecated `0/0`; fixed SDK package pass;
current Wide/Compact actual-pointer repeats `2/2` each.
Evidence: this document, `.proofline/issues/PL-0045.json`, and the D-backed
evidence root above.
Boundary / next dependency: no new SDK algorithm, schema version, downstream
inspection consumer, calibrated measurement, release, or R0 claim. Hosted CI
and DPI 100/150/175/200 remain unverified. Next dependency-ready work is
`B-10`, followed by `E-13`.
