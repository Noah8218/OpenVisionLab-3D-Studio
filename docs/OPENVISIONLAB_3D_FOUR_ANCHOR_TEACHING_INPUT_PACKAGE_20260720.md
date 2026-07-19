# OpenVisionLab 3D Four-Anchor Teaching Input Package

Updated: 2026-07-20
Status: **Ready for operator input - not an executable recipe**

## Purpose and boundary

This is the operator handoff for converting the existing two-corner teaching
template into one real Landmark Correspondence v1 recipe. It deliberately
contains no sample reference coordinates, no inferred threshold, and no
synthetic CornerAnchor. Do not import this document as a recipe.

The current starting template is
`recipes/c3d-xyz-affine-teaching-template.ov3d-teach.json`. It is schema
`1.1` and supplies only `UpperLeftCorner` and `LowerRightCorner`; it remains
readable as a teaching scaffold but is not a runnable v1 correspondence
recipe.

The resulting recipe is allowed to create **correspondence evidence only**.
It must not calculate or apply XYZ Affine, re-grid, Thickness, or Warpage.

## Fixed measured-source identity

| Field | Required value |
| --- | --- |
| Recipe source path | `3D/Warpage/Ori_20240116_094430.C3D` |
| Source entity | `source.c3d.height-map` |
| Recorded source SHA-256 | `79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C` |
| Recorded grid | `1301 x 1967` |
| Source unit / frame | `raw-height` / `frame.c3d-grid-index` |
| Source convention | `column-rawHeight-row` |

If the source bytes, grid, unit, or frame differ, start a new recipe from the
actual acquisition. Do not reuse any row from this packet.

## Required operator inputs

Fill every cell from the actual fixture or nominal authority. Source XYZ is
not hand-entered: it is resolved from the current **Published** Line
Intersection `CornerAnchor` output when Preview runs.

| # | Published source `CornerAnchor` output ID | Source role | Reference landmark ID | Reference X | Reference Y | Reference Z | Evidence that it is a real fixture feature |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| 1 | `<enter output ID>` | `<enter role>` | `<enter ID>` | `<enter>` | `<enter>` | `<enter>` | `<drawing/CAD/fixture evidence>` |
| 2 | `<enter output ID>` | `<enter role>` | `<enter ID>` | `<enter>` | `<enter>` | `<enter>` | `<drawing/CAD/fixture evidence>` |
| 3 | `<enter output ID>` | `<enter role>` | `<enter ID>` | `<enter>` | `<enter>` | `<enter>` | `<drawing/CAD/fixture evidence>` |
| 4 | `<enter output ID>` | `<enter role>` | `<enter ID>` | `<enter>` | `<enter>` | `<enter>` | `<drawing/CAD/fixture evidence>` |

The four source IDs, source roles, source coordinates, reference IDs, and
reference XYZ values must all be distinct. At least one point on **each** side
must be genuinely off the other three points' plane. Four visually separate
top-surface corners are not sufficient for the full-XYZ transform.

| Descriptor field | Operator value |
| --- | --- |
| Reference frame ID | `<enter exact frame ID>` |
| Reference unit | `<enter unit>` |
| Reference provenance | `<drawing, CAD, fixture, or controlled-measurement identifier>` |
| Reference revision | `<enter revision>` |
| `MinimumNormalizedTetrahedronVolume` | `<explicit number: 0 < value < 1>` |

The threshold is dimensionless and must be explicitly selected by the owner;
it cannot be inferred from the C3D image or display scale.

## Teaching procedure

1. Open the starting recipe and complete four independent branches of
   `Filter -> Height Difference Edge -> 3D Line Fit -> Line Intersection`.
   Publish each Line Intersection result as a current `CornerAnchor`.
2. Open **Landmark Correspondence Tool Lab**. Add exactly four rows using the
   published source output IDs and the supplied reference IDs/XYZ values.
3. Enter the single descriptor's frame, unit, provenance, revision, and
   normalized-volume threshold. The Tool Lab promotes the recipe to schema
   `1.2`; the Landmark Correspondence step must have one input: the generated
   `landmark-correspondence-set` selection.
4. Use **Preview**. It must report source rank `4/4`, reference rank `4/4`,
   and both normalized volumes strictly above the taught threshold. Any error
   is a data/teaching error; do not bypass it by editing the source or lowering
   the requirement without a new owner decision.
5. Use **Publish**, then **Save**. Reopen the saved recipe and Preview again
   before headless validation.

## Required acceptance evidence

The real fixture gate passes only when all of the following are recorded from
the same saved recipe and C3D source:

- the recipe validates as schema `1.2` with exactly four correspondence rows;
- every row resolves to a current Published `CornerAnchor` produced by a
  Line Intersection step from the same source identity;
- Preview and Publish show one identical correspondence SHA-256, source and
  reference ranks `4/4`, and both normalized volumes above the chosen limit;
- save/reopen preserves the descriptor and all four rows; and
- the headless Runner exits `0` and writes the same correspondence SHA-256.

Run the last check only after the actual source branches and reference inputs
are saved:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug -- `
  --tool-teaching-landmark-correspondence "<saved-recipe.ov3d-teach.json>" `
  --tool-teaching-step "step.landmark-correspondence.01" `
  --report "artifacts\verification\four-anchor\runner-report.txt"
```

An exit code other than `0`, a missing report, a rank below `4`, or a
normalized volume at or below the taught minimum is a failed gate. It is not
an affine result and does not authorize the XYZ Affine implementation.

## Handoff package

Provide the completed table, source acquisition/C3D, authority for the
reference coordinates, and the saved schema `1.2` recipe. The next work is a
real Runner parity check; only after it passes may the separately designed XYZ
Affine tool be considered.

See `docs/OPENVISIONLAB_3D_LANDMARK_CORRESPONDENCE_TYPED_ADAPTER_DESIGN_20260719.md`
for the typed contract and claim boundary.
