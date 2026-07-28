# OpenVisionLab 3D Filter Typed Adapter v1 Design

Updated: 2026-07-19

Status: Owner-approved design. The bounded v1 implementation passes the local
gates recorded in `OPENVISIONLAB_3D_FILTER_TYPED_ADAPTER_20260719.md`.

## Work Contract

### User goal

Turn the already taught `Filter` row into the first understandable 3D
inspection-pipeline adapter, while preserving the completed teaching workflow
and keeping algorithm execution explicit.

### Non-negotiable requirements

- The product remains a general 3D inspection recipe workbench, not a
  thickness/warpage-only application.
- `Teach`, parameter editing, layer visibility, and selection changes never run
  the filter.
- `Preview`, `Run`, and `Publish` remain explicit actions.
- The first filter must not fabricate missing surface data or claim physical
  accuracy.
- The UI and implementation use OpenVisionLab-owned layout, terminology,
  contracts, fixtures, and visual assets.

### Included scope

- one C3D raw-height Median filter;
- typed input, parameter, output, provenance, and error contracts;
- selected-step Properties UI and execution-state behavior;
- Shell/Viewer/Runner ownership and parity plan; and
- synthetic Golden, fixed-sample, UI, and CI acceptance gates.

### Excluded scope

- Gaussian, mean, bilateral, morphology, or hole-filling filters;
- ROI filtering, because `ROI / Crop` is already a separate pipeline tool;
- height-difference edge, line fit, intersection, correspondence, affine, and
  re-grid execution;
- point-cloud neighborhood filtering or mesh smoothing;
- calibration, physical units, uncertainty, metrology, and performance claims.

## Evidence and Product Fit

The current 3D catalog already declares:

```text
Filter
  input:  HeightField
  output: FilteredHeightField
  Method: Median
  Kernel: 3 x 3
```

The current `ToolRecipeDocument` persists ordered entity routing and named
parameters, but `ToolWorkbenchViewModel` intentionally never invokes an
algorithm. The current C3D Data boundary exposes the complete source map as a
row-major `double[]`, mapping zero and non-finite cells to `NaN`.

The 2D OpenVisionLab reference provides three useful rules:

1. input layer, output layer, filter method, and method-specific parameters
   are visible in the PropertyGrid;
2. parameter changes require an explicit Preview; and
3. Filter prepares data for a downstream tool and does not make the final
   OK/NG decision.

The 3D design keeps those workflow rules but does not copy the 2D UI or assume
that image border and pixel-intensity behavior is correct for a height field.

## Owner Decision Proposed for v1

| Decision | Proposed v1 value | Reason |
| --- | --- | --- |
| Scalar being filtered | C3D `raw-height` only | The first edge tools consume height transitions. Grid X/Z and the source frame must remain stable. |
| Method | `Median` only | It removes isolated height spikes without introducing Gaussian/bilateral weighting and missing-value semantics before real data proves they are needed. |
| Kernel | `3`, `5`, or `7`, default `3` | Small bounded choices are teachable, deterministic, and sufficient for the first Golden matrix. |
| Missing cells | `PreserveMask` | Zero and non-finite source cells stay missing; Filter never fills a hole. |
| Boundary | `AvailableNeighbors` | At source edges the window is clipped to available cells; no reflected or repeated synthetic samples are introduced. |
| ROI | none | The explicit `ROI / Crop` step owns regional restriction. |
| Acceptance | none | Successful execution is shown as `Completed - preprocessing`; downstream measurement tools own Pass/Fail. |

These six fixed choices intentionally remove Method, Border, Missing-value, and
ROI controls from the Basic UI. They remain visible as read-only execution
facts in the expanded Evidence/Advanced surface.

## Teaching UI

The existing `Project Explorer | 3D View | Properties` layout is retained.
Selecting an authored Filter row shows this compact Properties content:

```text
Step 01: Filter
--------------------------------------------------
Input
  HeightField       source.c3d.height-map

Filter
  Method            Median (v1)
  Kernel size       [ 3 x 3 ] [ 5 x 5 ] [ 7 x 7 ]
  Missing cells     Preserve source holes

Output
  FilteredHeightField
  derived.filtered-height.01

Evidence
  Taught - ready for Preview
  Filter changes raw-height only; source grid/frame stay unchanged.
```

UI rules:

- Kernel presets are mutually exclusive buttons or one compact combo, not
  free-text input.
- The existing top workflow owns `Preview`, `Run`, and `Publish`; no duplicate
  execution button is added inside Properties.
- Editing Kernel, input, or output marks an existing preview `Stale` but does
  not recalculate it.
- An explicit Preview may display the temporary filtered surface and label it
  `Preview output - not published`; recipe input routing remains unchanged.
- Project Explorer exposes source and preview/published output as separate
  entities. Creating an output never silently changes a later step input.
- Source/filtered split comparison remains a later Viewer slice; v1 needs only
  an explicit source/output entity switch and `Return to source` action.

## State Contract

| State | Meaning | Allowed next action |
| --- | --- | --- |
| `Taught` | Step and parameters exist; no adapter output exists. | Validate or Preview. |
| `Ready` | Typed preflight succeeded. | Preview. |
| `Preview running` | Explicit Preview is executing off the UI thread. | Cancel. |
| `Preview ready` | Temporary output and diagnostics exist. | Inspect, Publish, or edit. |
| `Preview stale` | Source identity, input, output, or Kernel changed. | Preview again. |
| `Published` | Current Preview was explicitly accepted as the step output entity. | Save recipe or Run. |
| `Error` | Typed preflight or execution rejected the request. | Correct the reported field/source and retry. |

Filter execution success is presented as `Completed - preprocessing`, not as a
measurement OK/NG. If the existing Run Record requires `ResultStatus.Pass`,
the message must explicitly state that this means execution succeeded and no
acceptance rule was evaluated.

## Typed Contract

The authored recipe continues to use schema `1.1`. The Filter step uses these
canonical parameter names:

```json
{
  "id": "step.filter.01",
  "toolId": "filter",
  "toolName": "Filter",
  "inputEntityIds": ["source.c3d.height-map"],
  "outputEntityId": "derived.filtered-height.01",
  "parameters": [
    { "name": "Method", "value": "Median" },
    { "name": "KernelSize", "value": "3" },
    { "name": "MissingValuePolicy", "value": "PreserveMask" },
    { "name": "BoundaryPolicy", "value": "AvailableNeighbors" }
  ]
}
```

The current display string `Kernel = 3 x 3` is migrated to
`KernelSize = 3` when the adapter is implemented. The parser must reject
unknown names, duplicate names, unsupported values, even kernels, and silent
numeric normalization.

### Typed input

```text
C3DMedianFilterInput
  stepId
  sourceEntityId
  sourcePath / byteLength / contentSha256
  width / height
  unit / frameId / scalarMeaning=raw-height
  row-major values (finite non-zero => value, missing => NaN)
  KernelSize = 3 | 5 | 7
```

### Typed output

```text
C3DMedianFilterOutput
  outputEntityId / rootSourceEntityId
  width / height / unit / frameId / scalarMeaning=raw-height
  row-major filtered values with the source missing mask preserved
  validCount / missingCount / changedCount
  min / max / mean
  canonical outputContentSha256
  provenance: toolId, contractVersion, sourceSha256, parameters
```

The canonical output hash is calculated over width, height, and IEEE-754
row-major output values with a single declared byte order. It is evidence for
Viewer/Runner parity, not a physical calibration claim.

## Numerical Definition

For each source grid cell:

1. If the center cell is missing, the output cell is `NaN`.
2. Otherwise, clip the square Kernel window to source bounds.
3. Discard missing neighbor cells.
4. Sort the remaining finite values.
5. Use the middle value for an odd count; for an even count, use the arithmetic
   mean of the two middle values calculated in `double`.
6. Preserve the original width, height, unit, frame, row/column mapping, and
   missing mask.

No X, Y, grid locator, transform, affine, or re-grid value is modified. The
implementation may optimize the median only after profiling; the reference
definition above remains authoritative.

## Preflight and Error Rules

Preview and Run fail closed when any of these conditions is present:

- input entity does not resolve to one earlier `HeightField`;
- source format is not C3D, source bytes/hash changed, or dimensions/byte
  length no longer match;
- unit, frame, or scalar meaning is empty or differs from the taught source;
- the source contains no valid finite non-zero height;
- Method is not exactly `Median`;
- KernelSize is not `3`, `5`, or `7`;
- MissingValuePolicy or BoundaryPolicy differs from the fixed v1 contract;
- output ID is empty, equals the input ID, duplicates another recipe identity,
  or does not match the declared `FilteredHeightField` output; or
- cancellation occurs before a complete output snapshot exists.

A failed or cancelled Preview never publishes a partial output entity.

## Explicit Execution Boundary

```text
Teach / parameter edit / layer visibility
  -> validate authored state only

Preview selected Filter
  -> verify exact source bytes and typed parameters
  -> execute Median
  -> create temporary preview entity and diagnostics
  -> do not rewrite input routing or save the recipe

Publish selected Filter
  -> require the current non-stale preview
  -> promote it to the declared output entity

Run recipe
  -> preflight the full requested chain first
  -> reject unsupported downstream taught rows before partial execution
  -> execute the same Filter rule used by Preview and Runner
```

## Ownership Boundary

| Layer | Owns | Does not own |
| --- | --- | --- |
| `Core` | Existing recipe/entity identities and structural validation. | C3D file reads or median calculation. |
| `Data` | Same-byte C3D identity verification, complete row-major height values, immutable derived height-field snapshot/hash. | Filter parameters or acceptance. |
| `Tools` | Strict Filter parameter parsing, typed input/output, Median reference algorithm, diagnostics. | WPF state or file dialogs. |
| Shell ViewModel | Selected step, explicit Preview/Cancel/Publish commands, stale state, entity routing, progress. | Pointer rendering or duplicate numerical implementation. |
| Viewer | Source/preview/published entity display and clear Preview labeling. | Recipe mutation or implicit execution. |
| `Runner` | Headless use of the same typed adapter and output/provenance report. | A second Filter implementation. |

The first adapter does not introduce an interface/factory with one
implementation. A direct `toolId == filter` dispatch is sufficient; extract a
shared adapter abstraction only when the second executable taught tool proves
the common shape.

## Verification and Acceptance Gates

### Numerical Golden

1. constant surface remains unchanged;
2. isolated center spike is removed;
3. a sharp step edge remains stable under the declared median rule;
4. zero/NaN/infinity cells preserve the exact missing mask;
5. source-border windows follow `AvailableNeighbors`, including even valid
   sample counts;
6. Kernel sizes 3, 5, and 7 produce the declared answers;
7. all invalid parameters and source-identity mutations fail closed;
8. repeated execution is byte/hash deterministic; and
9. a fixed C3D sample produces equal Viewer and Runner output hashes and
   diagnostics.

### UI and workflow

- inactive, Ready, Preview-running, Preview-ready, stale, Published, cancelled,
  and Error states are ViewModel verified;
- parameter editing and visibility changes produce zero Preview/Run requests;
- explicit Preview does not change the step input entity;
- unsupported downstream tools block full Run before Filter execution;
- current-source before/after captures at `1280 x 760` show the selected Filter
  Properties and a clearly labeled preview output;
- Viewer right-drag pan, short right-click menu, Profile, height distribution,
  docking, and BinaryHost regressions remain green; and
- the full solution builds with zero warnings/errors and the new mandatory
  Filter Golden runs in local and Windows CI.

## Risks and Deferred Decisions

- The current `ReadHeightMapValues()` checks dimensions and byte length but does
  not re-hash the source after load. The adapter must use a same-byte verified
  Data read before any execution claim.
- A naive per-cell sort is the reference implementation. Preview must run off
  the UI thread and be cancellable; optimization requires measured evidence.
- Gaussian and bilateral filters remain deferred until a real source shows
  Median cannot provide stable downstream edge evidence and their missing-value
  and scale semantics are separately approved.
- This Filter does not establish physical scale, edge accuracy, registration,
  thickness, warpage, calibration, or metrology trust.

## Approval Checkpoint

Owner decision on 2026-07-19: all four decisions were approved:

1. Filter v1 processes `raw-height` only and preserves grid XYZ/frame mapping.
2. Median-only with Kernel `3/5/7` is sufficient for the first adapter.
3. Missing cells remain missing and source-border windows use only available
   valid neighbors.
4. ROI remains a separate `ROI / Crop` step.

The bounded implementation and its current-build evidence are recorded in
`OPENVISIONLAB_3D_FILTER_TYPED_ADAPTER_20260719.md`.

## Completion Record

```text
Status: Complete
Scope: Filter typed-adapter UI, numerical, ownership, execution-boundary, and verification design only.
Acceptance criteria:
  - Current 3D teaching and typed-rule boundaries reviewed: Pass.
  - Relevant 2D Filter PropertyGrid and explicit Preview behavior reviewed: Pass.
  - Minimal v1 parameters, missing/border behavior, UI states, typed input/output, error rules, and Goldens recorded: Pass.
  - Owner decisions required before implementation are explicit: Pass.
Verification: Source/doc review and consistency searches only; no production code or visible UI changed.
Evidence: docs/OPENVISIONLAB_3D_FILTER_TYPED_ADAPTER_DESIGN_20260719.md
Boundary / next dependency: Owner confirmation of the four Approval Checkpoint decisions.
```
