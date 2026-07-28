# Legacy 3D Teaching Video Review — 2026-07-22

## Purpose and boundary

The owner supplied two historical company-project recordings as workflow references only:

- `Thickness_Teaching.mp4`: 2552 x 1388, 336.85 seconds, H.264
- `Warpage_Teaching.mp4`: 2552 x 1388, 290.97 seconds, H.264

OpenVisionLab 3D Studio must not reproduce their visual design, product identity, control arrangement, or proprietary implementation. This review extracts only general inspection-workflow lessons and maps them onto the current GoPxL-inspired typed tool-chain direction.

Review evidence is stored under `artifacts/reference-review/20260722-legacy-teaching-videos/`, including periodic contact sheets and selected full-size frames.

## Apply

1. **Overview plus detail remains synchronized.** The recordings keep a full 3D overview and a detailed height-map view visible while teaching. OpenVisionLab should preserve linked selection and camera-independent overview/detail evidence through dockable Viewer and Tool Lab views.
2. **The selected tree node owns the visible overlay and properties.** Choosing an alignment edge, intersection, Thickness instance, or Warpage instance immediately focuses its ROI, parameters, and result. This supports the current Artifact Navigator -> Step Parameters -> Viewer overlay contract.
3. **Repeated inspection instances are first-class.** Multiple Thickness and Warpage ROIs appear as individually selectable instances. OpenVisionLab should represent these as repeated ordered recipe steps with stable IDs, not as one tool containing a hidden list.
4. **ROI and numeric evidence are shown together.** Rectangles, fitted lines/intersections, and numeric labels stay on the data. Current Preview overlays should keep the same evidence relationship while preserving explicit Preview and Publish.
5. **Per-step and overall status are both visible.** The recordings show row-level OK/NG and a final overall status. OpenVisionLab should retain step status in the pipeline and calculate overall recipe status from published typed results.
6. **Visibility and edit protection matter.** Per-object visibility/lock affordances reduce accidental ROI changes. OpenVisionLab should provide typed visibility and teaching-lock state where it materially prevents mistakes.

## Apply with a different UI/UX

1. **Historical `Align / Thickness / Warpage` top-level groups become a generic tool chain.** OpenVisionLab must not imply that only two measurements exist. Alignment, re-grid, feature, and measurement tools remain ordered typed steps with explicit inputs and outputs.
2. **Fixed four-pane layout becomes dockable task views.** The useful overview/detail/properties/results roles remain, but every view follows the current dockable-view contract and can be rearranged for 1920 x 1080 or 1280 x 760.
3. **Immediate visual feedback remains, automatic inspection execution does not.** Parameter or ROI edits may update transient teaching overlays, but Preview and Publish remain explicit actions.
4. **Large OK/NG presentation becomes compact status plus evidence navigation.** The operator must see status quickly without losing the results table, failed metric, tolerance, and associated overlay.
5. **Historical coordinate labels become typed provenance.** Displayed positions are useful only when the source entity, frame, unit, owner output, and content identity are retained in the recipe and run evidence.

## Exclude

1. The historical product name, icons, color palette, window chrome, exact panel geometry, and control styling.
2. A fixed algorithm taxonomy limited to `Align`, `Thickness`, and `Warpage`.
3. Oversized OK/NG text that consumes the evidence panel and hides diagnostic context.
4. Ambiguous icon-only controls without tooltips, accessible names, or visible state.
5. Implicit re-execution while moving an ROI or editing a parameter.
6. A flat object list that does not expose typed input/output lineage, stale evidence, or publish state.
7. Physical-unit or metrology claims derived only from the historical screen labels.

## Resulting product rules

- Keep the GoPxL-style `typed input -> ordered tool -> typed output` recipe model.
- Keep explicit Preview and Publish.
- Add repeated measurement steps through stable recipe-step identities.
- Keep overview, detail, parameters, and results synchronized but dockable.
- Keep overlay labels concise and selectable; the result row must navigate back to its ROI/evidence.
- Preserve source/frame/unit/hash ownership for every taught ROI.
- Treat the videos as UX reference evidence only, not algorithm or metrology validation.

## Immediate implementation influence

The current Plane Flatness gate will prove the same general operator loop without copying the historical UI:

1. Publish a synthetic transformed height field (A3) through the normal Re-grid tool lifecycle.
2. Select the Plane Flatness step in the ordered pipeline.
3. Teach a reference ROI and measurement ROI with two separate Viewer pointer captures.
4. Persist stable selection IDs, rectangles, and exact A3 owner/hash/frame/unit bindings.
5. Save and reopen the recipe without automatically running inspection.

## Implemented checkpoint

The immediate gate above now passes in one current-build Shell session. The
normal Re-grid lifecycle publishes a deterministic synthetic A3 with content
SHA-256
`715350CE3B7194010B4AEAB58B69C001922032250DACA3C9010F532EF0C6151B`.
Two real Viewer pointer captures replace the ordered Plane Flatness Reference
and Measurement ROIs, both remain owned by that exact A3, and schema `1.3`
save/reopen preserves the role order and rectangles. Preview remains `NotRun`
and the result collection remains unchanged throughout teaching.

This checkpoint implements the historical workflow lesson of synchronized
step/overlay/parameter ownership without copying the historical layout or
making Thickness and Warpage product modes. Evidence is under
`artifacts/verification/20260722-plane-flatness-live-a3-pointer/`.

```text
Status: Complete
Scope: Historical workflow review plus one live A3 Plane Flatness two-role teaching loop
Acceptance criteria: two real pointer ROIs -> pass; exact A3 ownership -> pass; save/reopen -> pass; no implicit inspection -> pass
Verification: Shell smoke PASS; generic measurement Workbench 23/23; current Debug build 0 warnings/errors
Evidence: artifacts/reference-review/20260722-legacy-teaching-videos and artifacts/verification/20260722-plane-flatness-live-a3-pointer
Boundary / next dependency: UX reference and synthetic display-frame evidence only; real A1 and physical/metrology trust require trusted four-landmark reference data
```
