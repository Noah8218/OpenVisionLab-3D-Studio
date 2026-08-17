# OpenVisionLab 3D Advanced Semantic Theme Parity

Date: 2026-08-03

## Outcome

`A-16 Advanced workspace semantic-theme parity` is Complete. The Advanced
Data/Layers, Tool/Inspector, Evidence Workbench, and linked evidence surfaces
now use the existing OpenVisionLab graphite semantic resources instead of the
former white/platform-light palette.

This repair applies the OpenVisionLab requirement that one task surface use one
coherent visual system. It preserves the product's colors, terminology, docking
topology, controls, and bounded platform scope.

## Operator problem and root cause

The current Release baseline reproduced white Data/Layers, Tool/Inspector,
and Evidence Workbench surfaces in both supported layouts. Required text and
generated controls mixed light-theme colors with the surrounding graphite
Shell. Wide and Compact screenshot white-pixel ratios were `39.23%` and
`32.66%`.

`MainWindow.xaml` still owned four light content surfaces and `160` direct
light-palette text/background/border assignments. The shared TextBox and
ComboBox styles also replaced, rather than extended, the existing WPF UI dark
templates, so disabled and popup-generated ComboBox children could remain
platform-light.

## Implemented boundary

- Mapped the legacy light surface, divider, primary, secondary, muted,
  warning, fail, disabled, and viewport roles to existing `ThreeD.*` semantic
  resources.
- Set the Advanced dock owner to inherit `ThreeD.TextBrush` for generated and
  unqualified text children.
- Based shared TextBox and ComboBox styles on the installed WPF UI dark
  templates and retained OpenVision semantic normal, hover, keyboard-focus,
  disabled, read-only, validation-error, and popup states.
- Added focused Workbench verification that rejects the former light palette
  and checks the required semantic resources and generated-control state
  markers.
- Retained `#f59e0b` only for the Profile/Section scientific trace. It is data
  visualization, not application chrome or status meaning.

No new dependency, theme system, custom control template, inspection command,
or algorithm was added.

## Acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| Wide Advanced surfaces use the graphite theme | Pass | `after/wide-advanced-en.png` |
| Compact Advanced surfaces use the graphite theme | Pass | `after/compact-advanced-ko.png` |
| No legacy platform-light palette remains | Pass | Workbench `82/82`; semantic resources `10/10`; legacy list empty |
| Generated control states use product roles | Pass | control-state markers `9/9`; dock-tab markers `8/8` |
| Open ComboBox popup and focused selection remain dark | Pass | UI Automation `Collapsed -> Expanded`; `after/wide-advanced-combo-open-en.png` |
| Disabled/read-only controls remain legible | Pass | final Wide/Compact captures and shared style state matrix |
| Expanded/collapsed docking remains presentation-only | Pass | Workbench docking `82/82` |
| Required labels/actions do not overlap or clip | Pass | current Wide/Compact visual comparison |
| No unintended horizontal or nested scrollbars | Pass | current Wide/Compact visual comparison |
| Inspection behavior remains unchanged | Pass | Inspection Workspace `63/63`; Validation Set `84/84` |

The final white-pixel signal is supporting evidence only; visual inspection is
the acceptance gate:

| Layout | Before | After |
| --- | ---: | ---: |
| Wide `1920 x 1040` | `39.23%` | `0.55%` |
| Compact `1280 x 760` | `32.66%` | `0.56%` |

All final actual EXE windows intersected the single available and therefore
leftmost monitor `DISPLAY1`, bounds `0,0,1920,1080`.

## Verification

- Release solution: Pass, `0` warnings / `0` errors.
- Workbench docking and theme guard: Pass, `82/82`.
- Inspection Workspace: Pass, `63/63`.
- Validation Set: Pass, `84/84`.
- Shell command-line options: Pass, `28/28`.
- Code structure: Pass, `29/29`, zero numerical migration-debt files.
- Final Wide and Compact screenshot quality: accepted on attempt `1`.
- Advanced ComboBox popup: UI Automation `Collapsed -> Expanded`; selected
  value unchanged.
- Human-owner R0 fixed inputs: Wide and Compact `-ValidateOnly` Pass with Shell
  assembly SHA-256
  `BA0D7A168939067BF7BE4C9410BA985B81B2367F4A528CAAA90977F698FB0E48`.

Physical evidence root:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-a16-advanced-semantic-theme-parity\
```

`before` contains the true pre-edit current Release captures. `after` contains
the final normal and open-popup captures. `verification` contains the focused
reports, monitor rectangles, visual comparison, and R0 validation outputs.

## Completion record

Status: Complete

Scope: Advanced semantic-theme parity for Data/Layers, Tool/Inspector,
Evidence Workbench, linked evidence, and generated input/tab/popup states.

Acceptance criteria: Every criterion in the acceptance table passes.

Verification: Release `0/0`; Workbench `82/82`; Inspection Workspace `63/63`;
Validation Set `84/84`; command line `28/28`; structure `29/29`; final
Wide/Compact and open-popup evidence accepted; both R0 `-ValidateOnly` modes
pass.

Evidence: This document and the physical evidence root above.

Boundary / next dependency: Human-owner unaided Wide/Compact R0 remains
external for `A-01` and Workspace v3 `8/8`. The next dependency-ready layout
review is `C-13 Linked-view display-range consistency`.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high
