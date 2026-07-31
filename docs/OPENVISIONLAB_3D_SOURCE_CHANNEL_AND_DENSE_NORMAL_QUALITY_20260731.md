# OpenVisionLab 3D Source Channel and Dense Normal Quality

Date: 2026-07-31

Status: Complete

Backlog scope: `B-11`, `B-16`

## Decision

OpenVisionLab uses commercial products as workflow references, not as visual
templates. This slice therefore does not copy GoPxL colors, theme, panel
proportions, labels, icons, assets, or screen topology. It advances an
OpenVisionLab-specific prerequisite: source data must state which inspection
channels actually exist before later tools consume them.

The product owner also authorized dependency-safe software development before
the unaided human R0. R0 still controls `A-01`, Workspace v3 `8/8`, and any
claim of unaided usability. It no longer blocks independent deterministic
source/model foundations that can be verified without owner interaction.

## Included scope

- one seven-entry source-channel catalog for C3D, GLB/STL mesh, and LAS/LAZ;
- preservation of decoded GLB `NORMAL` values through node transforms;
- preservation of ASCII and binary STL stored facet normals;
- preservation of LAS/LAZ intensity in sampled point records;
- format-driven LAS/LAZ RGB availability instead of array-shape inference;
- a WPF-neutral dense-normal quality report;
- fail-closed checks for density, finite values, non-zero length, unit length,
  index validity, triangle degeneracy, and triangle-winding alignment;
- explicit partial-normal presence so partial channels remain visible and
  invalid instead of being discarded or reported as absent;
- deterministic JSON projection and focused verification.

## Excluded scope

- calculated or repaired normals;
- a normal-map Viewer mode;
- SurfaceModel creation, sampling, matching, or pose search;
- UI or layout changes;
- physical calibration, traceability, uncertainty, or metrology claims.

## Source-channel contract

Every supported source returns exactly these entries:

1. Height
2. Intensity
3. Color
4. Depth
5. Normal
6. Confidence
7. Signal-to-noise ratio

Each entry is either `Available` with source-specific evidence or
`Unavailable` with a visible reason.

Current mappings are:

| Source | Available channels |
| --- | --- |
| Supported C3D height grid | Height |
| GLB/STL mesh | Color when decoded vertex color or base-color texture exists; Normal when at least one declared normal exists |
| LAS/LAZ | Intensity; Color only for LAS point formats that declare RGB |

XYZ positions are not re-labeled as Height or Depth. Viewer palette colors,
calculated face normals, and unsupported decoder fields are not promoted to
source channels.

## Dense-normal contract

`SourceNormalQualityReport` schema `1.0` records:

- source ID and format;
- position, triangle, and declared-normal counts;
- finite, non-zero, and unit-length normal counts;
- invalid indices and degenerate triangles;
- comparable, aligned, and reversed triangle corners;
- normal-length and alignment statistics;
- the applied tolerances and a fail-closed evidence statement.

`Valid` requires all of the following:

- at least one complete triangle;
- one declared normal for every position;
- every declared normal is finite and non-zero;
- every declared normal is unit length within `0.001`;
- every triangle index is valid;
- no triangle is degenerate;
- every referenced triangle corner is comparable;
- every corner normal aligns with triangle winding at cosine `>= 0.5`.

No missing, partial, reversed, zero, non-finite, non-unit, or degenerate input
is repaired. A source with no declared normals is `Unavailable`, not a
calculated success.

## Loader behavior

- GLB normals use the inverse-transpose node transform. Their original length
  is retained so a non-unit source normal cannot be normalized into a false
  pass. Mirrored transforms account for winding sign.
- A GLB containing some primitives with normals and some without retains a
  per-position presence mask.
- Binary STL repeats each stored facet normal across its three expanded
  vertices.
- ASCII STL retains each declared facet normal and records missing facets in
  the presence mask.
- LAS/LAZ sampled points retain their decoded `ushort` intensity. RGB
  availability follows LAS formats `2, 3, 5, 7, 8, 10`.

## Evidence

Artifacts:

- `artifacts/current/20260731-source-channel-normal-quality/source-channel-normal-quality-report.txt`
- `artifacts/current/20260731-source-channel-normal-quality/source-quality-workspace-report.txt`
- `artifacts/current/20260731-source-channel-normal-quality/code-structure-report.txt`
- `artifacts/current/20260731-source-channel-normal-quality/data-loading-matrix/matrix_smoke_summary_after.txt`

Verification:

| Check | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| Source channel + dense normal verifier | Pass, `26/26` |
| Existing Source Quality workspace | Pass, `18/18` |
| Full data-loading matrix | Pass, `128` checks / `0` failures |
| Code structure | Pass, `17/17` |
| Human R0 launcher `-ValidateOnly` | Pass, Wide and Compact; no application launched |

The controlled valid plane produces `4/4` dense unit normals and `6/6`
aligned corners. Reversed, partial, zero/non-finite, degenerate, and
incomplete-index fixtures fail closed. Public `Box.glb` passes with `24/24`
normals and `36/36` aligned corners. Public `Tetrahedron.stl` is retained
without repair and correctly fails because only `9/12` normals are unit length
and all `12/12` corners are reversed against triangle winding.

This slice changes no visible UI, layout, navigation, text, or interaction.
Fresh Wide/Compact screenshots are therefore not required for this closure;
the current UI evidence remains the 2026-07-31 first-use Authoring package.

## Completion record

Status: Complete

Scope: `B-11` actual source-channel catalog and `B-16` dense declared-normal
availability/consistency report for current C3D, GLB/STL, and LAS/LAZ sources.

Acceptance criteria:

- seven unique channels with explicit evidence -> Pass;
- unsupported channels remain unavailable and are never fabricated -> Pass;
- GLB and STL source normals are preserved -> Pass;
- missing and partial normals remain distinguishable -> Pass;
- known-valid normal fixture passes -> Pass;
- malformed/reversed fixtures fail closed -> Pass;
- existing source-quality and loading behavior remains valid -> Pass.

Verification: Release build `0/0`; focused `26/26`; Source Quality `18/18`;
loading matrix `128/128`; structure `17/17`; R0 Wide/Compact
`-ValidateOnly` Pass.

Evidence:
`artifacts/current/20260731-source-channel-normal-quality/`.

Boundary / next dependency: This does not add a Viewer normal diagnostic or
prove physical surface-normal accuracy. The next eligible software slice is
`J-01/J-03/J-04 SurfaceModel`; human-owner R0 remains separately required to
close `A-01` and Workspace v3 acceptance.
