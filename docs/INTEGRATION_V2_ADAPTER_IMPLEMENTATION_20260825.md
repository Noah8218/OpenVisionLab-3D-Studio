# Integration v2 adapter implementation — 2026-08-25

Status: Complete for the 3D Studio transaction/result-correlation slice.

## Scope

- `OpenVisionLab.ThreeD.Reporting` now consumes the fixed v2 contracts package
  and reads/writes canonical v2 Handoff, Acknowledgement, and Result messages.
- `ThreeDIntegrationExchange.PublishCompletedResult` requires an accepted
  Acknowledgement and a Run Record containing
  `InspectionRunIntegrationContext`.
- The Run Record is compared field-for-field with the Handoff for project,
  project schema, sequence, step, camera, acquisition, frame, unit, modality,
  input kind, input SHA-256, recipe SHA-256, and consumer build.
- `Pass`, `Fail`, and `Warning` map to `Pass`, `Ng`, and `Indeterminate`; `Ng`
  is never treated as an execution error. Missing context, wrong identity, or
  tampered artifacts fail closed with a correlation or artifact error.
- Result artifacts are staged under the transaction and published through an
  atomic message-file move. No recipe import or inspection execution is
  triggered by exchange operations. Transaction artifacts and the selected Run
  Record source reject symbolic-link/reparse-point traversal.

## Fixed dependency

Package: `OpenVisionLab.Integration.Contracts` `0.2.0-alpha.1`
SHA-256: `35BBA1D2462C99188C8B1BF155FEADD01A6A366252CF7A8C32AA4A8512A4C11B`

The same package and hash are vendored in the Machine Studio consumer.

## Verification

- Reporting tests: 4/4.
- Existing 3D data tests: 2/2.
- Release solution build: 0 warnings, 0 errors.
- Shell integration ViewModel verification: PASS, 16 checks, exit code 0.
- Test/build outputs: `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\integration-v2-tests-final2`
  and `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\integration-v2-full-build`.
- Shell verification report:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\integration-v2-shell-verification\integration-view-model.txt`.

## Boundary

This slice establishes the 3D Reporting adapter and exact Run Record gate. It
does not make the exchange auto-run a recipe, import into the active workspace,
or connect to PLC, robot, MES, cloud, or physical-camera systems. Runtime UI
visual-state/DPI coverage was not expanded because no visual control or layout
was changed.
