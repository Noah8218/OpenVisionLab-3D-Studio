# OpenVisionLab 3D Owner UI Acceptance Protocol

Updated: 2026-07-20
Status: Manual owner evidence required; this protocol does not itself accept the UI gate.

## Purpose and boundary

Use this protocol to decide whether the UI/UX `80/100` gate may be accepted
after G1 through G7. It tests only the local 3D recipe-workbench experience:
source, selected route, typed input/output, explicit authoring action, Viewer,
and validation feedback.

It does not test an algorithm result, sensor, camera, PLC, robot, affine
execution, calibration, physical units, or metrology.

## Preconditions

Run from `C:\Git\OpenVisionLab-3D-Studio` on the exact source being reviewed.

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Debug -p:Platform="Any CPU" --no-restore -v:q
dotnet run --no-build --project "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj" -c Debug -- --verify-tool-recipe-teaching artifacts\ui\owner-acceptance\tool-recipe-teaching-report.txt
dotnet run --no-build --project "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj" -c Debug -- --verify-workbench-docking artifacts\ui\owner-acceptance\workbench-docking-report.txt
dotnet run --no-build --project "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj" -c Debug -- --verify-artifact-navigator artifacts\ui\owner-acceptance\artifact-navigator-report.txt
powershell -ExecutionPolicy Bypass -File scripts\verify-workbench-keyboard-readiness.ps1 -ReportPath artifacts\ui\owner-acceptance\keyboard-ui-automation-report.txt
```

All four reports must pass before a manual result is recorded. The keyboard
script validates the current 100% host only; it does not replace the actual
150% DPI or first-time-operator sections below.

Current local readiness evidence (2026-07-20):
`scripts/verify-workbench-keyboard-readiness.ps1` passed `5/5` at the host's
actual `100%` scale. It found the current Shell window, the compatible Filter
candidate, and its separate Add control; keyboard `Enter` preserved the
pipeline count for candidate selection and increased it by exactly one only
for explicit Add. The test process was not saved.

Current-source refresh evidence (2026-07-20): the exact precondition commands
were rerun after a Debug build with `0` warnings and `0` errors. Tool Recipe
Teaching passed `18/18`, Workbench Docking passed `25/25`, Artifact Navigator
passed `24/24`, and keyboard readiness passed `5/5`. The current-build Korean
`1920 x 1080`, Korean `1280 x 760`, and English `1920 x 1080` Shell captures
all passed screenshot quality on attempt one. The reports and captures are in
`artifacts/ui/20260720-owner-acceptance-refresh/`. The reference-width visual
review confirmed the visible compatible `Add`, next-missing-input explanation,
and separate English step-title/state rows. This automatic refresh does not
record a `150%` DPI or first-time-operator result.

Actual high-DPI technical evidence (2026-07-20): the current Debug Shell main
window was opened on the operator's configured display and Windows
`GetDpiForWindow` returned `144 DPI` (`150%`). After a fresh Debug build with
`0` warnings and `0` errors, Tool Recipe Teaching passed `18/18`, Workbench
Docking passed `25/25`, Artifact Navigator passed `24/24`, and keyboard
readiness passed `5/5` in that environment. The Korean `1280 x 760` logical
Workbench and English `1920 x 1080` logical Workbench both passed screenshot
quality on attempt one; visual review retained the compatible `Add`,
next-missing-input explanation, and separate English step-title/state rows
without overlap. Evidence is in
`artifacts/ui/20260720-owner-acceptance-150dpi/`. This proves the technical
high-DPI render check, but does not invent the remaining first-time-operator
result or the owner's decision record.

## A. Keyboard-only review

Start the Shell from Visual Studio (`F5`) or with the reviewed teaching recipe
open. Use `Tab`, `Shift+Tab`, `Enter`, `Space`, and `Esc` only; do not use the
mouse for this section.

| Check | Pass condition | Result / note |
| --- | --- | --- |
| Visible focus | The focused control can be identified while moving through the header, Toolbox, compatible candidate, candidate `Add`, Viewer controls, Step Parameters, and Problems. |  |
| Candidate selection | Focus a compatible candidate and press `Enter`. Only Toolbox selection changes; no Pipeline row is added and no Preview/Run/Publish state changes. |  |
| Explicit candidate add | Focus the separate `Add` action and press `Enter`. Exactly one taught row is added with the displayed candidate input; Preview/Run/Publish remain uninvoked. Discard or create a new unsaved recipe after this check. |  |
| Blocked-state explanation | Focus the `Next missing input` message. Its tooltip/name gives the full contract when its visible text is truncated. |  |
| Pane navigation | Keyboard focus can enter and leave the left, center, right, and lower docked panes without trapping the operator. |  |

## B. Actual high-DPI review

Do not change the system scale automatically. On the operator display, set
Windows display scaling to `150%`, restart Visual Studio and the Shell, then
record the actual display resolution and scale below.

| Check | Pass condition | Result / note |
| --- | --- | --- |
| Main Workbench | No control overlap; source, selected route, compatible `Add`, next-missing-input reason, Viewer, Step Parameters, and Problems remain identifiable. |  |
| Compact Workbench | At the closest `1280 x 760` logical work area available, the visible compatible row still exposes its `Add` action and the next-missing-input reason. |  |
| English | The English Step Parameters title and its state badge are both legible; neither is hidden by the other. |  |
| Overflow behavior | Long technical IDs may ellipsize only where a tooltip or accessible name exposes the complete value. |  |

Display scale: ____%<br>
Display resolution: ____ x ____<br>
Windows / monitor description: ____________________

## C. First-time operator review

Ask an engineer who did not implement this UI to use the default Tool
Workbench without explanation. Give only this task:

> Identify the current source, the selected inspection step, its typed input
> and output, the reason it cannot run yet, and the next safe action. Then
> select a compatible tool without changing the recipe.

| Check | Pass condition | Result / note |
| --- | --- | --- |
| Source and route | The operator identifies the source and selected step from the Toolbox / Entities surface. |  |
| Typed chain | The operator identifies the input/output from the selected-route card or Step Parameters without opening a secondary Tool Lab. |  |
| Next action | The operator finds the compatible candidate and the next-missing-input explanation. |  |
| Safety boundary | The operator states that candidate selection alone does not add, connect, Preview, Run, or Publish. |  |
| Time and confusion | Record elapsed time and any panel, label, or state that caused confusion. |  |

Elapsed time: ____ minutes<br>
Observed confusion / corrective issue: ____________________

## Decision record

| Decision | Owner | Date | Evidence folder | Notes |
| --- | --- | --- | --- | --- |
| Pass / revise / reject |  |  | `artifacts/ui/owner-acceptance/` |  |

Accept the UI/UX gate only when all automated preconditions and all manual
checks pass, with no unresolved overlap, focus trap, hidden action, or
unexpected mutation/execution. A `revise` or `reject` result keeps algorithm
work paused and should name one concrete UI issue before another change.
