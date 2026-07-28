# OpenVisionLab 3D Dual-ROI Role Preservation

Date: 2026-07-28

Status: Complete for the documented software workflow

## Outcome

The blocking dual-ROI findings from the actual operator-video review are
closed:

- deleting Reference no longer reinterprets the surviving Measurement ROI as
  Reference;
- deleting Measurement retains Reference and immediately permits a new
  Measurement capture;
- a fresh Height Image Reference Apply ends capture exactly once and
  immediately enables Measurement Draw;
- save/reopen restores both semantic roles and their selection identities;
- ROI authoring still does not invoke Preview, Publish, Run, or Validation
  Set.

The correction applies to the existing dual-region tools:

- Thickness;
- Plane Flatness;
- Gap/Flush, where first/second are the visible role labels;
- Volume.

## Ownership decision

The semantic role belongs to the inspection step, not to the reusable
selection. A `GridRectangle` can be routed to different steps, so encoding
`reference` or `measurement` into the selection itself would create stale
cross-step meaning.

Recipe schema `1.5` therefore adds optional step-owned
`ToolRecipeDualRoiRouting`:

```text
ToolRecipeStep
  InputEntityIds:
    first typed input
    first region when present
    second region when present
  DualRoiRouting:
    FirstRegionSelectionId
    SecondRegionSelectionId
```

A missing role is stored as `null`. This permits the storage-valid incomplete
route `source; measurement` while preserving that selection as the second
role. Strict Preview/Run validation still requires the complete ordered route
`source; reference; measurement`.

## Compatibility

| Recipe schema | Read behavior | Write behavior after dual-ROI edit |
| --- | --- | --- |
| `1.0`-`1.2` | Existing legacy rules remain unchanged, including legacy one-ROI Thickness interpretation. | Promoted to current schema when structured selection authoring requires it. |
| `1.3` | Artifact-owned selections remain valid. Complete dual-ROI roles are inferred from ordered inputs. | Promoted to `1.5`; explicit role routing is written. |
| `1.4` | OrientedBox3D and all existing artifact-owned recipes remain valid. Complete dual-ROI roles are inferred from ordered inputs. | Promoted to `1.5`; explicit role routing is written. |
| `1.5` | Explicit role routing is preferred. A missing first role does not collapse the second role. | Role metadata and ordered input route are saved together. |

The validator rejects:

- dual-role metadata in schemas older than `1.5`;
- the same selection in both roles;
- role IDs that do not reference declared `GridRectangle` selections;
- route order that disagrees with the role metadata;
- role metadata on a non-dual-ROI tool.

## Capture-state correction

The previous Apply order advanced to Measurement while the shared capture was
still active. The command was correctly disabled at that instant, but no
second role-command refresh occurred when capture later ended.

The corrected order is:

```text
Persist selection and route
  -> end shared capture
  -> advance active role
  -> refresh dual-ROI commands and workspace projection
```

This keeps one explicit Apply boundary and avoids a duplicate selection,
execution, or command-specific workaround.

## Actual operator replay

The current Release EXE was operated through the real WPF window. UI
Automation resolves moving controls, `user32` supplies ROI pointer
down/move/up, and `SendKeys` supplies Enter and Ctrl+S after restoring the
application foreground window.

| Scenario | Evidence |
| --- | --- |
| Wide `1920 x 1040` | Reference and Measurement each reached `Drawing -> Review -> Applied`; Measurement Draw was enabled immediately after Reference Apply; Ctrl+S and reopen restored both as Applied. |
| Compact `1280 x 760` | The same complete lifecycle and save/reopen passed. The Height Image surface was only `119 x 183`, so precision remains a separate P1 UX issue. |
| Execution boundary | Preview became enabled only after both ROIs were Applied and was not invoked. Run all remained disabled and was not invoked. |
| Media | H.264/YUV420P, 15 fps, current Wide/Compact videos and contact sheets; updated README GIF is 1,437,729 bytes. |

Historical before evidence remains in
`artifacts/current/20260728-operator-video-self-review/`. Current after
evidence is in
`artifacts/current/20260728-dual-roi-role-preservation/`.

## Verification

```text
Release build                         Pass, 0 warnings / 0 errors
Tool Recipe selections               Pass, 29/29
Height measurement Workbench         Pass, 46/46
Inspection Workspace / Height Image  Pass, 61/61
Tool Recipe teaching                 Pass, 28/28
Recipe Manager / PropertyGrid        Pass, 37/37
Workbench docking                    Pass, 33/33
Thickness repeat-grid                Pass, 20/20
Artifact Navigator                   Pass, 31/31
Artifact-owned ordered Runner        Pass, 18/18
Code structure                       Pass, 17/17
PowerShell parser                    Pass, 0 errors
Operator video replay                Pass
```

## Remaining user-centered risks

1. `E-09` OrientedBox3D still needs a visible Viewer outline and pointer
   handles. Numeric persistence alone is not spatially reviewable.
2. Compact Height Image authoring needs a focused or temporarily maximized
   teaching surface.
3. Height Image drag and 3D two-point creation need more explicit,
   mode-specific gesture instruction.
4. R0 remains the owner's unaided exact-source replay.
5. Physical calibration, metrology, production tolerances, and uncertainty
   remain unverified.

## Completion record

```text
Status: Complete
Scope: Step-owned dual-ROI role identity, both delete orders, Reference Apply -> Measurement Draw, schema 1.5 persistence, current Release Wide/Compact pointer replay, and save/reopen.
Acceptance criteria: Surviving role never collapses; both delete orders recover; Height Image Reference Apply enables Measurement; both roles reach Applied; Ctrl+S/reopen preserves roles; no implicit inspection execution.
Verification: Release build; focused Core/Data/Shell/Runner suites; structure/parser checks; actual WPF pointer/keyboard video replay; media/contact-sheet review.
Evidence: docs/OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md; artifacts/current/20260728-dual-roi-role-preservation/; docs/assets/openvisionlab-3d-roi-workflow.gif.
Boundary / next dependency: Does not close Compact precision, gesture instruction, E-09 OrientedBox3D Viewer editing, R0 owner replay, or physical metrology.
```
