# Message Dialog and Recipe Save UX

Date: 2026-07-22
Status: Complete for implementation and current-build evidence

## Decision

OpenVisionLab 3D Studio uses the existing company WPF message-dialog design
instead of stock `System.Windows.MessageBox`. The authoritative reference is:

`C:\Git\Labelling_Application\OpenVisionLab\Library\OpenVisionLab.Wpf.MessageDialogs`

The vendored 3D project is:

`src\OpenVisionLab.Wpf.MessageDialogs`

The public dialog API, modal ownership, button sets, keyboard behavior,
severity kinds, and expandable technical-details affordance are retained.
The bounded product adaptations are:

- target .NET 10 so the project belongs to the current Studio solution;
- obtain standard button and details labels from `OpenVisionLab.Localization`;
- use the 3D Studio navy/teal visual tokens rather than the source
  application's red-accent styling;
- reference the library through alias `OvlMessageDialogs` so its preserved
  `OpenVisionLab.Wpf.MessageDialogs` namespace cannot shadow third-party
  `Wpf.Ui` XAML types.

This is a source-authoritative port with explicit compatibility adaptations,
not a second custom message-box framework.

## User-facing contract

- Korean UI displays Korean title, instruction, standard buttons, and the
  `상세 정보` affordance.
- English UI displays the corresponding English strings.
- Raw exception text, file-system detail, typed entity IDs, and schema text
  may appear inside expandable technical details; they are evidence, not the
  primary operator instruction.
- A recipe with no inspection step can execute Save or Save As as an editable
  draft. It cannot Preview or Run until input and inspection-step validation
  passes.
- Save errors are reserved for file-system or malformed structural data; they
  do not instruct the operator to fabricate a first inspection step.
- Dialog display never runs Preview, Publish, or inspection.

Localization template for future dialogs:

```csharp
ShowStudioDialog(
    WpfMessageDialogKind.Warning,
    "ThreeD.Dialog.Feature.Title",
    "한국어 제목",
    "English title",
    "ThreeD.Dialog.Feature.Message",
    "운영자가 수행할 다음 행동을 설명하는 한국어 문장입니다.",
    "The matching English operator instruction.",
    technicalDetails);
```

New message paths must use this shared helper or the shared dialog project.
Do not add new `System.Windows.MessageBox.Show` calls.

## Verification

- `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"`
  passed with 0 warnings and 0 errors.
- Recipe Center/WPG/message localization verification passed `24/24`.
- Workbench docking verification passed `25/25`.
- Tool recipe teaching verification passed `18/18`.
- source search found zero stock `MessageBox.Show`, `MessageBoxResult`,
  `MessageBoxButton`, or `MessageBoxImage` references under `src`.
- Korean and English Recipe Center and message-dialog captures passed the
  screenshot-quality check on attempt 1.

Evidence is under:

`artifacts/current/20260722-message-dialog-localization/`

The owner's attached screenshot with the English stock Save warning is the
immediate before evidence. The `after-ko` and `after-en` captures are from the
current Debug build after the port.

## Completion record

```text
Status: Complete
Scope: Shared WPF message-dialog port and Shell message migration/localization; no algorithm, recipe schema, or Run Record change.
Acceptance criteria: Current build passes; Shell uses no stock MessageBox; Korean and English captures are readable and accepted. The original zero-step Save restriction is superseded by the empty-recipe lifecycle correction.
Verification: build 0/0; Recipe Center 24/24; docking 25/25; recipe teaching 18/18; stock-message search 0; four screenshot-quality reports accepted on attempt 1.
Evidence: artifacts/current/20260722-message-dialog-localization/ and this document.
Boundary / next dependency: Owner first-use replay remains external evidence. Technical exception detail is not translated. Trusted real four-landmark acquisition is still required for the next alignment/metrology gate.
```

## Superseding lifecycle correction

The original zero-step Save restriction was a product-contract error. Recipe
creation must be possible before an inspection step is authored. The current
contract, implementation, and actual-EXE create/reopen evidence are recorded in
`docs/OPENVISIONLAB_3D_EMPTY_RECIPE_LIFECYCLE_20260722.md`.
