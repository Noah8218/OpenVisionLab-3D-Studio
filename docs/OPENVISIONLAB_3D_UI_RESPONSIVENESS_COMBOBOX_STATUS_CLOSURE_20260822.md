# UI Responsiveness, ComboBox, and Bottom Status Closure

Date: 2026-08-22
Issue: `PL-0031`
Status: Complete

Correction note: the first PL-0031 closure inspected `ComboBox` opening-tag
dimensions but did not inspect style setters or the rendered glyph bounds. The
product-owner screenshot then demonstrated that the Wide `English` selection
was still clipped at the lower text edge. PL-0031 was reopened; the earlier
visual-completion evidence was invalidated and replaced by the checks and
current-build evidence below.

## Operator problem and bounded scope

The product-owner review reported an apparent freeze when changing Korean to
English, clipped or temporarily blank ComboBox values, slow UI motion, and an
unclear lower boundary in the maximized Shell. This slice covers localization
refresh, every app-owned XAML ComboBox, the two explicit popup animations, and
one global bottom status surface.

The operator loop remains load -> source quality -> teach -> explicit Preview
-> explicit Publish -> explicit Run -> evidence -> save/reopen. Language,
layout, ComboBox presentation, and the bottom status do not execute inspection
or mutate recipe, source, ROI, result, or Viewer selection state. Camera, PLC,
robot, cloud, account, and production-line control remain out of scope.

## Confirmed cause and correction

`ThreeDLocalization` exposes 538 localized properties. One language change
previously raised 538 individual `PropertyChanged` events. Several Workbench
presentation subscribers intentionally listen without a property filter, so a
single switch rebuilt palette choices, Viewer candidates, validation summaries,
and diagnostics hundreds of times. Replacing selected-item collections during
that fan-out explains both the apparent pause and the temporarily blank values
seen in the supplied image.

The correction raises WPF's supported empty-name all-properties notification
once. Filtered subscribers now accept that notification. The full
`ShellMainWindowViewModel`, including its Workbench subscribers, measured
`8.39 ms` for the final current Release Korean/English switch, with one localization
notification and preserved palette and auxiliary-view selections. Because the
corrected operation is below the 500 ms focused budget, a transient busy
overlay would flicker rather than help. The persistent bottom status instead
reports `Language applied · <elapsed> ms` after a user-initiated switch.

## Complete ComboBox inventory

The inventory counts opening `ComboBox` elements and excludes property
elements such as `ComboBox.ItemTemplate`. The corrected audit separately
inspects every `ComboBox` and `ComboBoxItem` style owner, including setters
that are not present on the opening tag, and lays out the actual Wide,
Compact, and popup language text.

| XAML owner | Count |
| --- | ---: |
| `OpenVisionLab.Logging.Controls/View/LogPanelView.xaml` | 2 |
| `OpenVisionLab.ThreeD.Shell/MainWindow.xaml` | 3 |
| `Views/Calibration/CalibrationCenterView.xaml` | 1 |
| `Views/Recipe/RecipeManagerView.xaml` | 1 |
| `Views/Shell/StudioNavigationRailView.xaml` | 1 |
| `Views/Workbench/HeightImageViewerView.xaml` | 1 |
| `Views/Workbench/OutputCompareView.xaml` | 3 |
| `Views/Workbench/RecipePipelineReviewView.xaml` | 1 |
| `Views/Workbench/SelectedToolWorkspaceView.xaml` | 2 |
| `Views/Workbench/SourceQualityWorkspaceView.xaml` | 2 |
| `Views/Workbench/ToolInspectorView.xaml` | 3 |
| `Views/Workbench/ViewerWorkspacePopoutWindow.xaml` | 1 |
| `Views/Workbench/ViewerWorkspaceView.xaml` | 1 |
| `OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.xaml` | 5 |
| **Total** | **27** |

The corrected audit found four `ComboBox` style owners and three
`ComboBoxItem` style owners. It also found the missed 25 px `Height` setter in
the logging panel. That setter is now 30 px, and all four control styles plus
all three item styles use centered content and ideal text formatting. Pixel
snapping/layout rounding was removed from ComboBox text owners because the
125% display scale can round fractional glyph metrics into the lower edge.
The five former 24/25/27 px local declarations remain normalized, and the
Height Image palette remains 108 px wide. The final source audit reports 27
controls, zero undersized tag dimensions, zero undersized style setters, and
no unsafe ComboBox or ComboBoxItem style owner.

## Motion and lower-boundary audit

The only explicit app-owned XAML popup animations were Viewer `Slide` and log
panel `Fade`; both were removed. A current search finds no `PopupAnimation`,
`Storyboard`, `BeginStoryboard`, or `DoubleAnimation` in app XAML. This does
not claim that framework or operating-system composition has no intrinsic
motion; it proves that this source tree no longer requests decorative XAML
animation.

`MainWindow` now reserves a fixed 30 px semantic bottom status row with a top
divider. The left side identifies the current inspection stage and next route;
the right side shows operation status, including language-switch completion.
Both fields trim safely and use polite accessibility live regions.

## Verification and UI state matrix

| Gate | Result |
| --- | --- |
| Release build | Pass, 0 warnings / 0 errors |
| Workbench docking and UI contract | Pass, 95/95 |
| Language notification | Pass, 1 empty-name all-properties event |
| Full ViewModel language-switch time | Pass, 8.39 ms (< 500 ms) |
| ComboBox source and style inventory | Pass, 27 controls / 4 control styles / 3 item styles / 0 unsafe |
| Rendered English glyph bounds | Pass; Wide `English` 49 x 18.6, Compact `EN` 18 x 18.6, popup `English` 44.8 x 18.6; no clip |
| Explicit XAML animation audit | Pass, 0 |
| Wide 1920 x 1040 English | Pass; full `English` selection and bottom status visible |
| Compact 1280 x 760 English | Pass; full compact `EN` selection and bottom status visible |
| Open language popup | Pass; current Release visual review shows complete `한국어`/`English` and `한`/`EN`; deterministic popup text layout has no clip |
| Height Image editable values | Pass; non-empty `95` and `125` values rendered and applied as view-only state |
| Disabled semantics | Pass; language selector resolves shared disabled surface/text brushes |
| Recipe/execution boundary | Pass; Height Image smoke reports recipe unchanged and inspection not run |

Commands actually run:

```powershell
dotnet build src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -c Release --nologo --disable-build-servers

src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Shell.exe `
  --verify-workbench-docking <D-backed-report>
```

The corrected actual-window smokes used the current Release EXE, English UI,
dynamic leftmost-monitor selection, and `--shell-smoke-width/height` for Wide
and Compact. The selected monitor was
`bounds=-2400,456,0,1806`; both window rectangles intersected it.

Evidence root:

```text
D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0031-combobox-horizontal-reopen/
```

Key files:

- `before/owner-compact-language-clipping.png`
- `before/wide-english-current.png`
- `before/compact-english-current.png`
- `after/wide-english.png`
- `after/wide-english.txt`
- `after/compact-english.png`
- `after/compact-english.txt`
- `after/workbench-docking-verification.txt`

The product-owner image is retained as the reopening evidence. Current Release
Wide and Compact captures are accepted on the first quality attempt, and the
rendered-text verifier checks the selection and popup `TextBlock` objects
rather than inferring legibility from bindings or control dimensions.

### PL-0033 split-boundary visual follow-up

A later product-owner screenshot exposed a separate visual collision beside
the auxiliary Viewer selector that the PL-0031 captures did not exercise. In
Wide vertical split, a redundant leading `Document24` glyph began on the
selected auxiliary-slot boundary and only half of the glyph was visible.
PL-0033 first removed that glyph, but its first follow-up then made a second
incorrect visual claim: it inferred the adjacent palette value as `높이` from
the binding/localization context even though the rendered lower strokes did
not make the first syllable independently legible. The product-owner crop
invalidated that evidence and reopened PL-0033.

The confirmed second cause was the leaf `Height="30"` on the Wpf.Ui-based
Height Image palette selector. It overrode the shared `MinHeight` contract and
prevented the template from taking the 36.62 device-independent pixels needed
at the available 125% display scale. Removing that fixed height makes the
selected `높이` and all three popup items (`높이`, `회색조`, `열화상`) complete.
The full 27-control source inventory found four other fixed-height ComboBoxes
(language, first-recipe starter, and two Source Quality selectors); their fixed
heights were also removed, leaving zero ComboBox `Height` declarations while
retaining the shared 30 px minimum and centered item/content contracts.

Current Release Korean and English Wide/Compact captures pass. The Height
palette actual-input smoke covers normal, hover, held pointer-down, focus,
open popup, keyboard selection, and mouse-leave recovery; its UI-to-ViewModel
selection round trip restores the original value and preserves the view-only
boundary. The first-recipe selected value, Source Quality selected values and
popup items, Wide `English`, and Compact `En` are also legible. Actual monitor
evidence is 125%; 100%, 150%, 175%, and 200% remain unverified because those
monitor scales were unavailable. Preserve `.proofline/issues/PL-0033.json`
and the D-backed `20260822-pl0033-height-combobox-reopen` evidence root.

## Durable completion record

```text
Status: Complete
Scope: One-event language refresh, 27-ComboBox geometry audit/correction, removal of two explicit popup animations, and a persistent Shell bottom status boundary
Acceptance criteria: C1 one bounded language notification and preserved selections -> pass (8.39 ms); C2 all 27 ComboBoxes, 4 control-style owners, 3 item-style owners, and rendered Wide/Compact/popup English text -> pass; C3 explicit app XAML animations -> 0; C4 Wide/Compact bottom boundary -> pass; C5 build, focused verification, actual EXE evidence, documentation, ledger, and diff hygiene -> pass
Verification: Release build 0 warnings/0 errors; Workbench docking 95/95; Wide and Compact actual Release EXE smokes exit 0 with acceptable screenshots; rendered selection/popup glyph checks pass
Evidence: .proofline/issues/PL-0031.json; this document; D-backed evidence root above
Boundary / next dependency: this is deterministic software and UI evidence, not the product-owner unaided Wide/Compact R0 or a human-usability/release acceptance claim; no commit, push, version, or release was performed
```
