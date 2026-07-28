# OpenVisionLab 3D Viewer and Runtime Localization Checkpoint

Date: 2026-07-24
Status: Complete

## Product decision

OpenVisionLab 3D Studio remains a local, sensor-neutral, rule-based 3D
inspection recipe workbench:

`3D input -> typed tools -> explicit teaching/Preview -> Publish -> validation/Run -> evidence`

This checkpoint completes the shared Viewer display slice left open by the
2026-07-23 fixed-label localization work. It does not add an algorithm, change
recipe validation, or broaden the product into camera, PLC, cloud,
production-line, physical-calibration, or metrology scope.

## Implemented scope

- Added Viewer localization bindings backed by the existing
  `OpenVisionLanguageService`.
- Localized the Viewer toolbar, geometry display list, HUD toggle, right-drag
  guidance, View menu, and matching right-click context commands.
- Localized the orientation label, measurement HUD headings, coordinate-frame
  explanation, pick state, transform/alignment/mapping display, performance
  summary, and bottom camera/model status.
- Localized display-only geometry, color-map, and render-density values in
  Workbench and Expert without changing their stored values.
- Localized the Expert header state and selected comparison/evidence summary
  labels while keeping the raw evidence payload intact.
- Added language-change notifications so the visible Viewer and Shell summary
  bindings refresh without reloading data or executing inspection.

## Contract boundary

Localization is a presentation adapter only. The following remain
authoritative and unchanged:

- geometry, color-map, and density identifiers;
- typed input/output entity IDs and tool IDs;
- recipe JSON and saved parameter values;
- coordinate symbols and numeric transform values;
- algorithm contracts and Preview/Publish/Run behavior;
- raw Runner report text and persisted evidence payloads.

This checkpoint therefore means that the shared Viewer and selected runtime
summary surfaces are bilingual. It does not mean that every technical string,
tool description, persisted record, or third-party control value is
translated.

## Visual result

The Korean `1920 x 1040` Workbench capture shows translated Viewer controls,
HUD, orientation, readiness, and camera/model status without clipping. The
Korean Expert capture additionally shows translated geometry/color/density
display values and comparison summary labels. English remains unchanged at
`1280 x 760`. All four actual-EXE quality reports were accepted on attempt 1.

Before evidence:

- `artifacts/current/20260724-viewer-runtime-localization/before/workbench-ko-1920x1040.png`
- `artifacts/current/20260724-viewer-runtime-localization/before/workbench-en-1280x760.png`

After evidence:

- `artifacts/current/20260724-viewer-runtime-localization/after/workbench-ko-1920x1040-final.png`
- `artifacts/current/20260724-viewer-runtime-localization/after/workbench-en-1280x760-final.png`
- `artifacts/current/20260724-viewer-runtime-localization/after/expert-ko-1920x1040-final.png`
- `artifacts/current/20260724-viewer-runtime-localization/after/expert-en-1280x760-final.png`

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Debug solution build | Pass, 0 warnings / 0 errors | `artifacts/current/20260724-viewer-runtime-localization/build-after-final.txt` |
| Viewer display/runtime localization | Pass, 92/92 | `artifacts/current/20260724-viewer-runtime-localization/viewer-display-localization-verification-final.txt` |
| Docking contracts | Pass, 27/27 | `artifacts/current/20260724-viewer-runtime-localization/workbench-docking-verification.txt` |
| Focused Viewer current-source capture | Accepted on attempt 1 | `artifacts/current/20260724-viewer-runtime-localization/after/viewer-focused-final.*` |
| Workbench and Expert, Korean/English actual EXE | Four captures accepted on attempt 1 | `artifacts/current/20260724-viewer-runtime-localization/after/*-final.*` |

Commands used:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Debug -p:Platform="Any CPU"

dotnet run --project "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj" `
  -c Debug --no-build -- `
  --smoke-c3d thickness `
  --verify-display-viewmodel `
  "artifacts\current\20260724-viewer-runtime-localization\viewer-display-localization-verification-final.txt"

dotnet run --project "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj" `
  -c Debug --no-build -- `
  --verify-workbench-docking `
  "artifacts\current\20260724-viewer-runtime-localization\workbench-docking-verification.txt"
```

## Completion record

Status: Complete
Scope: Shared Viewer controls/HUD/runtime display plus selected Shell
comparison/evidence display localization in separate Korean and English
states.
Acceptance criteria: Display values translate without changing stored
contracts; language changes refresh visible bindings; Debug build and focused
verifiers pass; representative current actual-EXE captures show no structural
clipping.
Verification: Debug build `0/0`; Viewer display/runtime `92/92`; docking
`27/27`; four actual-EXE Workbench/Expert screenshot-quality reports accepted
on attempt 1.
Evidence: `artifacts/current/20260724-viewer-runtime-localization/`.
Boundary / next dependency: Raw Runner reports, recipes, IDs, numeric
contracts, and other technical payloads remain intentionally untranslated.
The next product evidence gate is an unaided owner first-recipe replay. The
owner-approved scoped UI score remains `85/100` until that replay; physical
calibration and metrology remain unverified.
