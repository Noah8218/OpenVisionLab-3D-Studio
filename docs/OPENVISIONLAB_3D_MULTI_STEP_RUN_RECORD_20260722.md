# Ordered Multi-step Run Record

Date: 2026-07-22
Status: Complete for the bounded A3 -> supported measurement sequence

> Historical compatibility checkpoint: schema `1.3` and this bounded
> eight-step path remain supported. Schema `1.4` general sequential graph
> reporting supersedes this document as the latest coverage checkpoint; see
> `OPENVISIONLAB_3D_GENERAL_GRAPH_RUN_RECORD_20260723.md`.

## Outcome

The bounded ordered executor now writes one durable Run Record for the full
executed sequence instead of reducing the run to one selected measurement.
Schema `1.3` adds optional `Steps`; the existing optional `Step` remains the
single-step schema `1.2` compatibility path.

```text
explicit Published A2
  -> Re-grid Height Map (A3)
  -> Thickness
  -> Warpage
  -> Plane Flatness
  -> Point Pair Dimensions
  -> Gap / Flush
  -> Volume
  -> Cross-section Dimensions
  -> one JSON / HTML / CSV Run Record
```

Every recorded step carries authored recipe index, stable step/tool identity,
ordered input entity IDs, output entity ID, status, message, elapsed time,
metrics, and overlays. A measurement tolerance `Fail` contributes to the
overall result but does not erase later completed step evidence.

## Shell UI

The default Tool Workbench's dockable `Pipeline / Validation` panel includes a
bilingual `Run Record` tab. It shows the schema, ordered-step count, overall
state, tool, per-step state, typed route, and first key metric. It is read-only
and does not Preview, Publish, edit, or rerun the recipe.

## Compatibility

- Ordered sequences emit schema `1.3`, `Steps[8]`, and leave legacy `Step`
  empty.
- Existing single-step execution still emits schema `1.2` and its existing
  `Step`; it does not fabricate `Steps`.
- The current Shell reads the optional shape, so older schema `1.0`, `1.1`,
  and `1.2` records remain valid when `Steps` is absent.
- Product version, Viewer Host API, manifest, and recipe schema are unchanged.

## Acceptance record

```text
Status: Complete
Scope: One durable JSON/HTML/CSV record and one current docked Shell view for the bounded A3 -> seven-measurement execution.
Acceptance criteria:
- exact ordered A3 plus seven-measurement identities -> pass (8/8)
- typed input/output routes, metrics, overlays, elapsed time -> pass
- tolerance Fail retained while later steps remain recorded -> pass
- JSON, HTML, and CSV carry the same ordered identities -> pass
- current Korean and English Workbench show the same 8-step record -> pass
- legacy single-step schema 1.2 Step contract remains -> pass
Verification:
- dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -> 0 warnings, 0 errors
- Runner --verify-artifact-owned-roi-runner with JSON/HTML/CSV -> 21/21
- Cross-section single-step Runner -> schema 1.2 with Step and without Steps
- Shell screenshot quality -> Korean and English accepted on attempt 1
Evidence: artifacts/verification/20260722-multi-step-run-record/
Boundary / next dependency: This is not an arbitrary graph executor, batch/multi-piece history, automatic feature detection, or physical/metrology evidence. Execution still begins from an explicit trusted Published A2. Real four-landmark A1/A2 replay requires distinct trusted acquisition/provenance/reference-grid evidence.
```

## Reusable checks

1. Preserve authored order and stable typed entity IDs.
2. Treat tolerance Fail as evidence, not an execution exception.
3. Stop and record Error for invalid routing or adapter failure.
4. Keep JSON, HTML, CSV, and Shell projections derived from the same record.
5. Never infer physical units or calibration from display-frame fixture values.
