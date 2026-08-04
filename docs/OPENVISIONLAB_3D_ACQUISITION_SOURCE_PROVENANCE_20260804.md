# Acquisition/source provenance and limitation notes - 2026-08-04

Status: Complete

Follow-on update: `K-04` is now Complete. The optional source provenance record
also carries structured SensorToScene direction when explicitly supplied. See
`OPENVISIONLAB_3D_ACQUISITION_DIRECTION_AND_EDGE_ORIENTATION_20260804.md` for
the current direction contract, Viewer evidence, and verification. Historical
statements below that describe K-04 as next or blocked record the B-12 closure
checkpoint and are superseded by that document.

Backlog item: `B-12`

## Outcome

OpenVisionLab 3D Studio now keeps one explicit acquisition/source provenance
contract with each recipe source. An operator can declare whether acquisition
evidence is available, record the evidence text, and record what is unknown or
limited. The contract survives recipe save and reopen.

The feature is source metadata, not an inspection algorithm. Editing or
resetting the draft does not change the recipe. Only **Apply provenance**
changes the source descriptor, and Apply does not start Preview, Publish, Run,
or Validation.

## Included and excluded scope

Included:

- explicit `Available` or `Unavailable` state;
- required evidence text and required limitation notes;
- one coherent editor in the existing Source Quality workspace;
- explicit Apply and reset-to-applied actions;
- recipe JSON save/reopen round trip;
- legacy recipe fallback when the new field is absent;
- source-scoped reset when a different source is selected;
- Korean and English operator text.

Excluded:

- camera or sensor integration;
- camera pose, calibration, or acquisition-viewpoint inference;
- structured viewpoint/direction vectors (`K-04`);
- reflective/transparent/textureless limitation flags (`B-17`);
- automatic inspection or validation execution.

## Contract

`ToolRecipeSource.AcquisitionProvenance` is an optional, backward-compatible
field in the current recipe schema. A present contract is:

| Field | Rule |
| --- | --- |
| `state` | `Available` or `Unavailable` only |
| `evidence` | Required, non-blank operator/import evidence |
| `limitationNotes` | Required, non-blank known limitation or missing-information text |

Example:

```json
{
  "source": {
    "id": "source.c3d.height-map",
    "format": "C3D",
    "unit": "raw-height",
    "frameId": "frame.c3d-grid-index",
    "acquisitionProvenance": {
      "state": "Unavailable",
      "evidence": "Acquisition provenance was explicitly unavailable in the delivered source package.",
      "limitationNotes": "Viewpoint, direction, sensor pose, calibration, and capture conditions are unavailable."
    }
  }
}
```

An older recipe with no `acquisitionProvenance` field remains valid. The UI
shows a localized unavailable fallback without dirtying that recipe. The
fallback is stored only after the operator explicitly applies it.

## Operator workflow

1. Select the source card and open **Source Quality**.
2. In **Acquisition/source provenance**, choose **Evidence available** or
   **Evidence unavailable**.
3. Record the evidence and the known limitations. Do not convert assumptions
   into evidence.
4. Review the boundary note: the text does not prove or infer camera pose,
   calibration, or acquisition viewpoint.
5. Choose **Apply provenance**. Until this action, the values are only a draft.
6. Save the recipe. Reopening restores the exact applied state and text.

**Reset to applied** discards only the current draft. Selecting a different
source installs a new unavailable default and does not reuse the previous
source's evidence.

## Ownership and execution boundary

- Core owns the WPF-neutral recipe contract and validation.
- Shell Workbench owns source-scoped state, dirty state, save/reopen routing,
  and the explicit Apply callback.
- Source Quality owns the transient editor draft and presentation.
- No numerical or geometric algorithm was added, so no Library-Noah change is
  required.
- Provenance does not route into the Viewer, edge scoring, matching, or any
  inspection executor in this slice.

## Verification

Source baseline: repository `HEAD` `4da01ef5c098f524018323cafdb1204660224f50`
plus the preserved, uncommitted Lib.ThreeD 2.9.0 promotion and this B-12 work.

| Gate | Result |
| --- | --- |
| Release solution build | Pass, 0 warnings / 0 errors |
| B-12 focused contract/save/reopen/no-execution | Pass, `14/14` |
| Existing Source Quality workspace | Pass, `18/18` |
| Inspection Workspace / Workbench docking | Pass, `64/64` and `82/82` |
| Validation Set | Pass, `84/84` |
| Shell smoke command line | Pass, `33/33` |
| Code structure | Pass, `29/29` |
| Changed public-document local links | Pass, `13` checked / `0` missing |
| Working-tree whitespace | Pass, `git diff --check` |
| Current EXE Wide / Compact Source Quality smoke | Pass; recipe unchanged and inspection not run |
| Screenshot quality | Pass on first attempt for both supported sizes |

The focused verifier proves:

- draft typing and reset leave recipe dirty state and execution evidence
  unchanged;
- Apply stores exact Available evidence and marks only the recipe dirty;
- Available and explicit Unavailable contracts save and reopen exactly;
- save/reopen logs contain no Preview, Publish, Run, Validate, or Validation
  category;
- changing sources does not leak source-specific evidence;
- a legacy missing field is readable, unavailable, and clean;
- blank present evidence or limitation notes fail storage validation.

## Current-build UI evidence

All actual EXE captures used the dynamically selected leftmost monitor:
`DISPLAY2`, bounds `-1920,365,1920 x 1080`.

Before:

- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance\before\wide-source-quality-en.png`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance\before\compact-source-quality-expanded-ko.png`

After:

- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance\after\wide-source-quality-en.png`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance\after\compact-source-quality-ko.png`

Supported graphite-theme states:

- normal selected Unavailable and disabled/no-change actions;
- keyboard focus plus required-evidence validation warning;
- Available pending draft with enabled Apply hover;
- open ComboBox popup with Available/Unavailable choices.

State captures are under:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance\after\states
```

Visual review found no white/default-control leak, popup-theme mismatch,
required-label clipping, overlap, horizontal scrolling, or Viewer regression.
Compact uses the existing vertical scroll path, so quality details remain
reachable below the provenance card.

The Wide, Compact, validation-focus, Available-hover, and open-popup full-window
captures passed the full-window screenshot-quality gate on the first attempt.
The separate `19080`-pixel popup-only crop records the generic heuristic as
`acceptable=False` because the intended dark popup has a `15.24%` black-pixel
ratio; that full-window-oriented heuristic is not used as the popup theme gate.
The popup crop and its surrounding accepted window were visually reviewed
together.

## Corrected verification prerequisites

- The first build command used the nonexistent
  `OpenVisionLab.ThreeD.sln`; the actual solution is
  `OpenVisionLab.ThreeDStudio.sln`. The corrected Release build passed.
- The first parallel regression batch caused two verification processes to
  contend for the shared localization catalog. Both affected checks passed
  when rerun sequentially. This was a test-orchestration prerequisite, not a
  product failure.
- One UI-automation attempt selected a TextBlock instead of the ComboBox item;
  it did not invoke Apply. The final keyboard path selected the actual
  `Available` item, enabled Apply, and still left Apply uninvoked.

## Evidence root

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance
```

Principal reports:

- `verification/source-acquisition-provenance-final.txt`
- `verification/source-quality-final.txt`
- `verification/inspection-workspace-final.txt`
- `verification/workbench-docking-final.txt`
- `verification/validation-set-final.txt`
- `verification/shell-command-line-final.txt`
- `verification/code-structure-final.txt`
- `verification/release-build-final.log`

## Durable closure

```text
Status: Complete
Scope: B-12 explicit acquisition/source provenance state, evidence, limitations, Apply/reset, save/reopen, legacy fallback, and source isolation
Acceptance criteria: Available/Unavailable contract -> pass; exact save/reopen -> pass; no automatic execution -> pass; legacy compatibility -> pass; Wide/Compact/theme integrity -> pass
Verification: Release build 0 warnings/0 errors; focused 14/14; Source Quality 18/18; workspace 64/64; docking 82/82; Validation Set 84/84; command line 33/33; structure 29/29; public-doc links and diff check; current EXE Wide/Compact and state captures
Evidence: D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance
Boundary / next dependency: no camera, calibration, inferred viewpoint, or edge orientation; B-12 now unblocks K-04
```

## Next priority

1. `K-04 Acquisition viewpoint/direction metadata for edge orientation` | Recommended model: `gpt-5.6-sol` | Reasoning effort: high

K-04 must build on this explicit Available/Unavailable source contract and
must remain operator-authored or imported evidence. It must not infer a camera
viewpoint from geometry.
