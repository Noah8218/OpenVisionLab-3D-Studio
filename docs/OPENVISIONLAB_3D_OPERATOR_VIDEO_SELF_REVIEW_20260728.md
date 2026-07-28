# OpenVisionLab 3D Operator Video Self-Review

> Data-safety correction, 2026-07-28: source-specific historical media from
> captured company data is retired. The current README GIF and Wide dual-ROI
> replay were recaptured with `Synthetic Thickness Coupon v1` at
> `1280 x 840`. Current evidence is under
> `artifacts/current/20260728-synthetic-thickness-ui-replay/`. Do not restore
> older captured-source screenshots or GIFs from Git history.

> Superseding correction, 2026-07-28: the two P0 findings in this historical
> review are closed by schema `1.5` step-owned dual-ROI routing and corrected
> capture completion order. Current Wide and Compact Release replay completes
> Reference + Measurement, Preview readiness, Ctrl+S, and reopen. Read
> `docs/OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md` and use
> `artifacts/current/20260728-dual-roi-role-preservation/` as the current
> after evidence. The Compact precision, gesture-instruction, and `E-09`
> OrientedBox3D findings below remain open.

Date: 2026-07-28

Status: Complete for the requested current-product review. The review found
blocking dual-ROI defects; it does not declare the product workflow complete.

## Scope

The current Release WPF application was operated through the same external
input boundary available to an operator:

- UI Automation was used only to locate visible controls;
- `user32` pointer down/move/up generated real mouse clicks and drags;
- `SendKeys` generated Enter, Escape, and Ctrl+S;
- FFmpeg `gdigrab` recorded the visible desktop and cursor;
- the exact
  `synthetic-thickness-coupon-v1.C3D` source was used;
- source recipes were copied into the evidence folder before mutation.

The replay was not an in-process ViewModel command test. It also was not an
unaided first-time human replay, physical calibration, or metrology evidence.

## Evidence

| Replay | Media | Result |
| --- | --- | --- |
| Wide Thickness ROI | `01-wide-thickness-roi-replay.mp4`, 1920 x 1040, 46.13 s | Reference ROI delete, fresh drag, Review, and Enter Apply passed; Measurement ROI continuation remained disabled. |
| OrientedBox3D numeric authoring | `02-oriented-box-numeric-replay.mp4`, 1920 x 1040, 55.00 s | Invalid axes disabled Apply; valid edit, Apply, Ctrl+S, reopen, and exact name restoration passed. |
| Compact Thickness ROI | `03-compact-thickness-roi-replay.mp4`, 1280 x 760, 33.13 s | Fresh Reference ROI drag, Review, and Enter Apply passed at compact width. |
| README preview | `docs/assets/openvisionlab-3d-roi-workflow.gif`, 1,283,380 bytes | Synthetic-only `960 x 520`, 28-second ROI workflow excerpt; under the 10 MiB gate. |

Preserve the full evidence under
`artifacts/current/20260728-operator-video-self-review/`, including JSONL
timelines, contact sheets, the media probe, environment identity, and the
application-log excerpt.

## What passed

1. Top orthographic and linked Height Image opened in both Wide and Compact.
2. Deleting Measurement first and Reference second produced two visible
   `Missing` states.
3. A real Height Image pointer drag created a `499 x 292` Reference
   GridRectangle.
4. Lifecycle feedback progressed through `Drawing -> Review -> Applied`.
5. Enter applied the candidate without Preview or Run.
6. OrientedBox3D rejected a non-orthogonal axis, accepted corrected values,
   saved through Ctrl+S, and restored `README demo volume` after reopen.
7. The three MP4 files decode as H.264/YUV420P at 15 fps, and the README GIF
   was visually checked at its beginning, middle, and end.

## Confirmed defects

### P0 - deleting Reference first collapses Measurement identity

With both Thickness selections applied, deleting Reference left the route as
`source; measurement`. Starting Reference capture then reused the Measurement
selection:

```text
role=reference | selection=selection.tab-01.measurement-roi | existing=True
```

The role resolver currently derives Reference and Measurement from list
positions. Removing one item compacts the list and promotes the remaining
selection into the wrong role. The workaround used for the successful video
was Measurement delete first, then Reference delete. That workaround is not
acceptable as a product contract.

Required correction:

- preserve two explicit role slots independently of list compaction;
- deleting Reference must leave Measurement identified as Measurement;
- deleted role state, overlay, command readiness, and recipe route must update
  atomically;
- add focused tests for delete Reference only, delete Measurement only, both
  orders, redraw, save/reopen, and existing schema 1.3 recipes.

### P0 - Height Image Apply leaves Measurement drawing disabled

The fresh Reference ROI reached Applied and was routed as
`source; reference`, but `Draw or redraw Measurement ROI` remained disabled.
The final Wide timeline records:

```text
measurement-draw-readiness-after-reference-apply:
enabled=False; expected=True; passed=False
```

The visible summary knew the Measurement ROI was missing, so the remaining
failure is consistent with stale capture/command state across the linked
Height Image and Workbench owners. This blocks completing a new Thickness
dual-ROI workflow without restarting or another workaround.

Required correction:

- end the shared capture session exactly once after Height Image Apply;
- refresh both ROI command `CanExecute` states after the applied selection is
  routed;
- prove `Reference Apply -> Measurement Draw` with actual pointer input in
  Wide and Compact;
- prove Preview becomes ready only after the second valid ROI is applied.

### P1 - 2D and 3D ROI gestures use different mental models

Height Image creation is press-drag-release. The 3D surface workflow is taught
as two grid points. Two ordinary clicks on Height Image produce a `1 x 1`
candidate rather than the intended rectangle. The current helper text is not
enough to prevent this error during unaided use.

Required correction:

- state `Drag from one corner to the opposite corner` beside the active role;
- change the cursor and show a rubber-band rectangle while dragging;
- reject or explicitly warn on accidental `1 x 1` ROI when the selected tool
  requires a usable surface sample region;
- keep the same Review/Apply lifecycle after either input gesture.

### P1 - compact precision remains weak

At 1280 x 760, the Height Image was usable but the demonstrated drag occupied
only about `18 x 31` screen pixels. The dual Viewer composition and dense
Selected Tool panel leave little precision for Tab-level authoring.

Required correction:

- provide a one-action `Focus Height Image for ROI` mode or temporary
  full-height teaching surface;
- restore the previous layout after Apply/Cancel;
- retain visible role, lifecycle, and Apply/Cancel while focused.

### P1 - OrientedBox3D remains numerically invisible

Numeric validation and persistence worked, but no box outline, faces, axes, or
handles appeared in the 3D Viewer. An operator cannot verify that center,
orientation, and half-extents describe the intended volume. This directly
confirms backlog item `E-09`.

Required correction:

- render selected/unselected OrientedBox3D wireframes in the declared frame;
- distinguish X/Y/Z axes and selected state;
- add center, rotate, and extent handles with fixed screen-space hit targets;
- keep transient editing separate from Apply and recipe mutation;
- synchronize numeric edits and Viewer handles without running inspection.

### P2 - one automation/accessibility identifier is not exposed

`OrientedBox3DEditor` is assigned to a WPF `Border`, but that element is not
present in the UI Automation Control tree. Visible edit fields could still be
operated by bounded location lookup. Move the semantic AutomationId to an
accessible container or expose a custom peer before relying on it as a stable
operator-test contract.

## User-centered assessment

| Area | Assessment |
| --- | --- |
| Overall composition | The dominant Viewer plus Selected Tool direction is correct and materially clearer than the earlier journey-card layout. |
| State feedback | Missing, Drawing, Review, and Applied are visible and understandable when synchronization is correct. |
| Learnability | The Height Image drag gesture and 3D two-point gesture are insufficiently distinguished. |
| Recovery | Delete and redraw are discoverable, but role collapse makes the most obvious recovery sequence unsafe. |
| Compact operation | Controls remain reachable, but precise Tab ROI authoring is too constrained. |
| 3D region confidence | GridRectangle is visible in linked views; OrientedBox3D is not yet visually verifiable. |
| Persistence | OrientedBox3D Apply, Ctrl+S, and reopen passed. |
| Production readiness | Not ready for unaided new dual-ROI Thickness authoring until both P0 defects are fixed and replayed. |

## Implementation order

1. Fix explicit dual-ROI role identity and both delete orders.
2. Fix shared capture completion and Measurement command readiness after
   Reference Apply.
3. Replay the complete Reference + Measurement + Preview + Save + reopen
   flow in Wide and Compact.
4. Implement `E-09` OrientedBox3D Viewer outline and pointer handles.
5. Improve focused Height Image ROI authoring and gesture instruction.
6. Ask the owner to perform R0 unaided replay only after steps 1-3 pass.

## Completion record

```text
Status: Complete
Scope: Current Release external-input operation, three videos, timelines, visual self-review, and README GIF.
Acceptance criteria: Wide/Compact current UI captured; OrientedBox save/reopen captured; media verified; findings grounded in timeline/log/video; README asset produced.
Verification: Release EXE operation; actual pointer/keyboard input; ffprobe H.264/YUV420P/15 fps checks; contact-sheet and GIF frame review; PowerShell parser check.
Evidence: artifacts/current/20260728-synthetic-thickness-ui-replay/,
artifacts/current/20260728-synthetic-thickness-coupon/, and
docs/assets/openvisionlab-3d-roi-workflow.gif.
Boundary / next dependency: Product dual-ROI workflow remains incomplete because Reference-first delete breaks role identity and Measurement Draw remains disabled after fresh Reference Apply. R0 and physical metrology remain unverified.
```
