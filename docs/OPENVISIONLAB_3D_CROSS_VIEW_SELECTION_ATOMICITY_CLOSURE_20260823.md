# OpenVisionLab 3D Cross-View Selection Atomicity Closure

Date: 2026-08-23

Backlog item: `M-11`

Proofline issue: `PL-0041`

Status: Complete

## Scope

The existing `--verify-inspection-workspace-selection` command now proves the
shared 3D Viewer and Height Image ROI selection boundary as one deterministic
regression matrix. The slice changes no product selection implementation,
WPF layout/style/text, recipe, schema, execution contract, or test framework.

## Acceptance Evidence

- The pre-change Inspection Workspace command passed its existing `64/64`
  baseline.
- The current command passes `67/67`.
- A simulated 3D Viewer adapter request selects Reference through the existing
  `SelectPipelineStepForSelection(...)` boundary and publishes exactly one
  `SelectionChanged` event.
- Repeating that selection with upper-case identity succeeds without another
  event and retains exactly one authored Reference selection.
- Selecting Measurement through the actual Height Image ROI pointer request
  publishes exactly one additional event and synchronizes workspace role,
  selected recipe identity, and active Height Image overlay.
- Repeating the same Height Image selection adds no event.
- The complete round trip retains both authored selections and their geometry,
  dirty state, input route, step state, Preview state, and measurement output.
- Existing CI still owns the same Workbench command and now rejects a report
  without `Result: Pass (67/67 checks)`.

## Verification

All local outputs are physically under:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0041-cross-view-selection-atomicity
```

Executed checks at the focused checkpoint:

```text
Inspection Workspace baseline: Pass, 64/64
Focused Shell Release restore/build: Pass, 0 warnings / 0 errors
Inspection Workspace current: Pass, 67/67
Release solution build: Pass, 15 projects, 0 warnings / 0 errors
Standard MTP facade: Pass, 2/2
NuGet package health: Pass, 15 projects, vulnerable 0 / deprecated 0
Code structure: Pass, 68/68
Vision SDK package boundary: Pass
Monitor topology: two monitors; left smaller DISPLAY2 selected
Verifier mode: headless command route; no WPF window created
```

Primary evidence:

- `baseline-inspection-workspace-selection.txt`
- `baseline-monitor.txt`
- `focused-build.log`
- `inspection-workspace-selection.txt`
- `inspection-workspace-selection-final.txt`
- `inspection-workspace-selection-console.log`
- `solution-build.log`
- `standard-tests/`
- `nuget-package-health.txt`
- `code-structure-report.txt`
- `vision-sdk-package-report.txt`

## UI Evidence Boundary

No XAML, style, template, visible text, pointer adapter, or production
selection code changed. This closure therefore establishes the `M-11`
headless regression suite, not a new runtime visual or human-usability claim.
The dated C-09/C-10 Wide/Compact pointer evidence remains historical.

## Durable Closure

Status: Complete

Scope: `M-11` exact cross-view selection-change counts, repeated-selection
suppression, identity/cardinality parity, and non-execution invariants on the
existing Inspection Workspace verifier and CI command.

Acceptance criteria: distinct 3D/Height Image selections -> one atomic change
each; identical/case-varied repeats -> zero additional changes; recipe and
execution state -> unchanged; existing CI command -> exact complete-report
gate; proportional repository verification -> current evidence recorded.

Verification: baseline and focused results above, plus the final repository
checks recorded in `.proofline/issues/PL-0041.json`.

Evidence: the D-backed evidence root above and `.proofline/issues/PL-0041.json`.

Boundary / next dependency: no fresh WPF visual-state evidence was required
because no product UI changed. This does not establish owner R0, maximum-C3D
performance, physical metrology, release qualification, hosted CI success, or
publication.
