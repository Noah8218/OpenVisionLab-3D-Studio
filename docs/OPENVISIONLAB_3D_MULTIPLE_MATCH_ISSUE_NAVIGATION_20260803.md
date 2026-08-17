# OpenVisionLab 3D Multiple-Match Issue Navigation Closure

Date: 2026-08-03

Status: Complete

Backlog item: `K-09 Multiple-match issue navigation`

## Outcome

Operators can review the retained `J-12` Surface Match collection in order with
explicit `Previous` and `Next` actions. Both actions select the same typed item
owned by the existing retained-result selector and route that item's immutable
execution and assessment evidence to the Viewer.

Navigation is intentionally non-wrapping:

- the first result disables `Previous`;
- an interior result enables both directions;
- the last result disables `Next`.

The selector remains available for direct access. The selected match ID remains
available through the selector tooltip, while the compact label uses the stable
order, match-ID suffix, and decision for scanning.

## Operator problem and design boundary

The `J-12` selector proved that retained matches could be selected without
execution, but a dropdown-only path made ordered result review unnecessarily
slow. The product principle is explicit current result and
next safe action. The implementation uses OpenVisionLab terminology, existing
graphite theme resources, and existing arrow symbols.

This slice changes presentation only. It does not:

- execute Surface Match, Preview, Publish, Run, or Validation;
- mutate recipe steps, parameters, source routing, output state, or candidate
  state;
- persist the currently viewed collection item;
- wrap from the last result to the first or vice versa;
- add filtering, symmetry policy, acquisition metadata, or matching arithmetic;
- change `Lib.ThreeD 2.8.9`, the collection schema, or the Library-Noah
  ownership boundary.

## Structural proof

### Structural changes confirmed

- Before: `SelectedSurfaceMatchCollectionItem` already owned the current
  presentation selection, but the ComboBox was its only operator entry point.
- After: two bounded `RelayCommand` instances move to an adjacent item and set
  that same property. No second selected-index field, navigation service,
  collection wrapper, or persistence contract was added.
- Evidence:
  `ToolWorkbenchViewModel.SurfaceMatchCollection.cs` and focused Workbench
  checks `10/10`.

### Call path

- Old path: selector -> `SelectedSurfaceMatchCollectionItem` ->
  `ApplySurfaceMatchCollectionSelection` -> existing Viewer display request.
- New path: selector or Previous/Next command -> the same
  `SelectedSurfaceMatchCollectionItem` -> the same apply method -> the same
  Viewer display request.
- Evidence: exact linked execution and assessment hashes switch with each
  command; selector and navigation command states remain synchronized.

### Responsibility split

- Core/Data/Tools/Library-Noah responsibilities are unchanged.
- Shell ViewModel owns adjacent-item presentation policy and command state.
- Shell View owns localized labels, existing semantic button styling,
  tooltips, automation names, and purposeful `ArrowLeft24`/`ArrowRight24`
  symbols.
- Viewer continues to display the selected immutable evidence and owns no
  navigation policy.

### Dependency and state flow

The dependency direction remains Shell presentation -> existing typed Core
evidence. The retained collection and selected item remain the only evidence
and presentation owners. No arithmetic moved into Studio and the Noah migration
ledger remains `0` debt with `31` reviewed boundaries.

## Verification

| Gate | Result |
| --- | --- |
| Studio Release build | Pass, `0` warnings / `0` errors |
| Multiple Surface Match Runner | `14/14` |
| K-09 Workbench navigation | `10/10` |
| Current-input single-match Workbench/Runner parity | `14/14` |
| Docking | `82/82` |
| Inspection Workspace | `64/64` |
| Validation Set | `84/84` |
| Shell command line | `31/31` |
| Structure/Noah ownership | `29/29`, `0` debt, `31` reviewed boundaries |
| Human-owner R0 Wide `-ValidateOnly` | Pass |
| Human-owner R0 Compact `-ValidateOnly` | Pass |
| `git diff --check` before documentation closure | Pass |

The focused navigation verification proves first/last command boundaries,
exact adjacent evidence routing, selector/command synchronization, unchanged
collection identity and recipe state, invalid-ID fail-closed behavior, and
command disablement after evidence clear.

## UI evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-k09-multiple-match-issue-navigation\`

True current-Release pre-edit baselines:

- `before\wide-before-navigation.png`;
- `before\compact-korean-before-navigation.png`.

Final current-Release captures:

- `after\wide-second-previous-focus-next-disabled.png`;
- `after\compact-korean-second-previous-focus-next-disabled.png`;
- `after\compact-english-first-previous-disabled-next-enabled.png`;
- `after\window-monitor-placement.txt`.

Wide `1920 x 1040` and Compact `1280 x 760` were restored to the dynamically
selected leftmost monitor `\\.\DISPLAY1`, bounds `(0, 0)-(1920, 1080)`, before
capture. The final screenshots contain no unexplained overlap, required-label
clipping, out-of-pane controls, or unintended horizontal/nested scroll bars.
English and Korean labels, first/last disabled states, keyboard focus, pointer
hover, and selected result states use the existing semantic dark-theme button,
icon, tooltip, and accessibility system. Compact `Flow`, `Tools`, and
`Selected` remain the pre-existing intentional adaptive tab labels.

## Inventory and next dependency

`K-09` moved from New to Complete. Inventory at this checkpoint was
`132 C / 17 P / 61 N / 9 E / 16 O` (`235` total).

Superseding status (2026-08-03): `F-13` is Complete and the current inventory
is `133 C / 17 P / 60 N / 9 E / 16 O`. The next bounded,
dependency-ready item is:

`1. J-13 Symmetry-aware pose equivalence | Recommended model: gpt-5.6-sol | Reasoning effort: high`

Pose-equivalence arithmetic must be implemented in committed Library-Noah
source first.
Human-owner Wide/Compact R0 remains an external acceptance task and is not
replaced by the automated checks above.

## Completion record

Status: Complete

Scope: Non-wrapping Previous/Next navigation over the existing immutable J-12
retained Surface Match collection, synchronized with the existing selector and
Viewer evidence route, with localized themed controls and current-build UI
evidence.

Acceptance criteria: Previous/Next select adjacent retained results through one
selection owner -> pass; first/last boundaries disable the unavailable command
without wrapping -> pass; selection remains presentation-only and preserves
recipe, collection, output, and candidate state -> pass; selector remains
synchronized -> pass; current Release Wide/Compact theme and layout evidence
passes -> pass; focused and regression verification remains green -> pass.

Verification: Release `0/0`; Runner `14/14`; K-09 Workbench `10/10`;
current-input single-match parity `14/14`; docking `82/82`; Inspection
Workspace `64/64`; Validation Set `84/84`; command line `31/31`; structure
`29/29`; Wide/Compact R0 `-ValidateOnly` pass.

Evidence: This document and the D-backed
`20260803-k09-multiple-match-issue-navigation` evidence root.

Boundary / next dependency: This proves deterministic presentation behavior
for the controlled two-result fixture. It does not prove human usability,
real-part robustness, symmetry-aware pose equivalence, acquisition direction,
calibrated metrology, cross-hardware performance, or production readiness. Human-owner R0
remains external. `F-13`, `J-13`, and `J-05` are now Complete; the next
dependency-ready item is `J-07 Model key-point artifact and debug overlay`,
implemented in committed Library-Noah first.
