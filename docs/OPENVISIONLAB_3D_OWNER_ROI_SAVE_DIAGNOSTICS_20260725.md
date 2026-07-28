# Owner ROI deletion, Measurement creation, recipe-save, and diagnostics correction - 2026-07-25

## Status

Status: Complete

Scope: correct the owner-reported Thickness flow so each ordered ROI can be
deleted directly, a current Reference-only Thickness route enables creation of
the Measurement ROI, incomplete authored recipes can be saved as drafts, and
the existing Dev-derived logging DLL records enough structured evidence to
diagnose these actions from the file log.

## Reopened evidence

The earlier dual-surface checkpoint was reopened by owner use. Fresh
current-Release evidence reproduced three practical defects:

1. the dual-ROI cards hid the generic selection section that owned the only
   Delete action, while neither ordered card exposed Delete;
2. every two-input Thickness route was interpreted as the old one-ROI
   Measurement route, so a current-schema `HeightField -> Reference ROI`
   route was misread and Measurement capture remained unavailable;
3. storage validation required an execution-complete step, so a legitimate
   incomplete teaching draft could not be saved.

The existing in-memory Workbench session log also did not write its teaching
actions to the application log file, so the disabled/rejected state was not
reconstructable afterward.

Fresh before evidence:

```text
artifacts/current/20260725-owner-roi-save-diagnostics/before-dual-roi-no-delete-1280x760.png
```

## Corrected behavior

- Reference ROI and Measurement ROI cards each expose a familiar Delete icon
  and bilingual `Remove selection` action.
- Selecting an authored ROI in the Viewer synchronizes both the owning
  Inspection Flow step and its Reference/Measurement role.
- Current schema `1.3` distinguishes a Reference-only Thickness draft from a
  schema `1.2` legacy one-ROI Thickness. The former enables Measurement
  capture; the latter still preserves its existing ROI as Measurement and
  upgrades without losing it when Reference is taught.
- Deleting Measurement retains Reference and immediately enables a new
  Measurement capture.
- Deleting Reference retains the other ROI as a reusable, unrouted selection
  where an ordered hole cannot be represented. Thickness retains its supported
  legacy Measurement route.
- `ValidateForStorage` accepts incomplete step routes as explicit drafts and
  records a warning. Strict `Validate`, Preview, Publish, and Run still require
  the full tool contract.
- The dual-surface template is promoted from schema `1.2` to `1.3`; no existing
  recipe schema is silently reinterpreted.
- Teaching actions do not invoke Preview, Publish, or Run.

## Persistent diagnostics

The 3D solution already contained the Dev logging projects and output DLLs:

```text
src/OpenVisionLab.Logging/OpenVisionLab.Logging.csproj
src/OpenVisionLab.Logging.Controls/OpenVisionLab.Logging.Controls.csproj
src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.Logging.dll
src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.Logging.Controls.dll
```

The Workbench session sink now also writes through `OVLog`. Stable
`key=value` diagnostics cover:

- capture start: step, tool, role, selection ID, kind, required points, and
  whether geometry already existed;
- apply rejection: role and exact rejection reason;
- apply success: role, selection ID, rectangle, ordered input route, and the
  no-execution boundary;
- deletion: role, selection ID, rectangle, and remaining route;
- save request: path, dirty state, step count, and selection count;
- save rejection/failure and save success.

The default Release log is:

```text
src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/Log/OpenVisionLab-ALL.log
```

The docked `Run Log -> Application Log` view retains the existing Open-folder
support from `OpenVisionLab.Logging.Controls`.

## Dev logging source-to-target map

The requested Dev logging component was already present before this
checkpoint, so no second DLL/project copy was added.

| Dev source | 3D target | Observed relation |
| --- | --- | --- |
| `Library/OpenVisionLab.Logging/OpenVisionLab.Logging.csproj` | `src/OpenVisionLab.Logging/OpenVisionLab.Logging.csproj` | SHA-256 identical |
| `Library/OpenVisionLab.Logging/LogEnums.cs` | `src/OpenVisionLab.Logging/LogEnums.cs` | SHA-256 identical |
| `Library/OpenVisionLab.Logging/Model/RuntimeLogStream.cs` | `src/OpenVisionLab.Logging/Model/RuntimeLogStream.cs` | SHA-256 identical |
| `Library/OpenVisionLab.Logging/OVLog.cs` | `src/OpenVisionLab.Logging/OVLog.cs` | existing 3D extension adds `Flush` and `Shutdown` |
| `Library/OpenVisionLab.Logging/log4net.config` | `src/OpenVisionLab.Logging/log4net.config` | existing 3D configuration uses stable rolling filenames under `Log` |

Equivalence status: the used `OVLog.Write`, runtime stream, categories, project
identity, and file sink are confirmed. Byte-for-byte equivalence of the whole
Dev bundle is not claimed because the 3D target intentionally contained the
pre-existing shutdown and rolling-file extensions. This checkpoint did not
modify the logging library sources; it connected Workbench activity to the
already built DLL.

## Verification

Current Release evidence:

- solution build: pass, `0` warnings / `0` errors;
- generic height measurement Workbench: pass, `42/42`;
- logging integration: pass, `4/4`;
- recipe selection contracts: pass, `17/17`;
- recipe teaching: pass, `27/27`;
- docking: pass, `28/28`;
- Recipe Center/WPG: pass, `28/28`;
- teaching capture ViewModel: pass, `20/20`;
- actual Release Viewer pointer flow: pass; Measurement ROI deleted, recreated
  with real pointer input, explicitly applied, and saved in one run;
- actual saved route:
  `source.c3d.height-map; selection.reference-roi.01; selection.thickness-01.measurement-roi`;
- Preview/result references remained unchanged throughout teaching;
- current `1280 x 760` pointer-flow screenshot and current maximized Delete-button
  screenshot quality: accepted on attempt 1;
- tool-add refresh duplication removed: at the historical `1920 x 1040`
  viewport the three-run medians are `2.869 ms` tool selection, `140.890 ms`
  add, `58.234 ms` selected-step refresh, and `112.892 ms` UI apply. One run
  marginally exceeded the old add/UI budgets (`153.468/158.239 ms`), so this
  remains fixed local timing evidence rather than a broader guarantee.

Evidence folder:

```text
artifacts/current/20260725-owner-roi-save-diagnostics/
```

Key evidence:

```text
before-dual-roi-no-delete-1280x760.png
after-dual-roi-delete-final.png
after-measurement-create-apply-save-final-1280x760.png
actual-measurement-create-apply-save-final.txt
actual-measurement-create-apply-save.cancel-pointer.txt
actual-measurement-created-final.ov3d-recipe.json
height-measurement-release-final.txt
logging-release-final.txt
recipe-selections-release-final.txt
recipe-teaching-release-final.txt
docking-release-final.txt
recipe-manager-wpg-release-final.txt
teaching-capture-release-final.txt
workbench-response-1920x1040-1.txt
workbench-response-1920x1040-2.txt
workbench-response-1920x1040-3.txt
```

## Boundary / next dependency

This proves the corrected local Release software flow and durable diagnostics.
It does not prove physical calibration, uncertainty, traceable metrology,
arbitrary hardware, or production-line integration. The next product evidence
gate remains the owner's unaided first-recipe replay on this updated Release
EXE.

## Completion record

Status: Complete

Scope: ordered ROI Delete actions, current/legacy two-input Thickness role
disambiguation, Measurement ROI create/recreate, incomplete-draft save, and
persistent Workbench diagnostic logging.

Acceptance criteria:

- each ordered ROI has direct Delete: pass;
- Reference-only current Thickness enables Measurement ROI: pass;
- actual pointer Measurement creation and Apply succeed: pass;
- incomplete recipe saves while Preview/Run remain blocked: pass;
- saved recipe reopens and complete route persists: pass;
- log alone records delete, capture, apply, route, save request, and success:
  pass.

Verification: Release build `0/0`; focused checks `42/42`, `4/4`, `17/17`,
`27/27`, `28/28`, `28/28`, and `20/20`; actual Release pointer/apply/save
smoke; current screenshot quality accepted on attempt 1.

Evidence: this document,
`artifacts/current/20260725-owner-roi-save-diagnostics/`, and the current
Release `OpenVisionLab-ALL.log`.

Boundary / next dependency: owner unaided replay on the updated EXE.
