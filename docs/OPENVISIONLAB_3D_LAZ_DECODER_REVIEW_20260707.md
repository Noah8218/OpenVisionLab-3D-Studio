# OpenVisionLab 3D LAZ Decoder Review

Checked: 2026-07-07

This note narrows the LAZ task after adding point-cloud rendering to the Viewer. The current repository proves LAZ header metadata, bounds-frame rendering, a headless XYZ/RGB decode probe, and Viewer sampled RGB point rendering.

## Current Repo Evidence

- Current sample: `3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz`.
- Current Viewer metadata smoke state: `decoder=metadata-only`.
- Current Viewer point smoke state: `decoder=points-decoded`.
- Current Runner probe state: `decoder=points-decoded`.
- Last recorded metadata: LAS 1.2, raw point format `130`, logical point format `2`, LASzip VLR present, 2,155,617 points, 26-byte point records, bounds X `-57.914..59.170`, Y `-92.749..82.339`, Z `-1.028..2.539`.
- Point format 2 carries RGB in the LAS point record layout, so the useful Viewer target is XYZ plus RGB, not XYZ-only.
- Local CLI check found no installed `laszip`, `las2txt`, `lasinfo`, `las2las`, or `pdal` command.
- Local NuGet cache check found no existing LAS/LAZ/PDAL package.
- NuGet sources are available: `nuget.org` and Visual Studio offline packages.
- `Unofficial.laszip.netstandard` has been verified against the sample through `OpenVisionLab.ThreeD.Runner --laz-probe`.

## External Source Check

- [LASzip](https://laszip.org/) is the core LAZ compression/decompression family and supports reading compressed LAZ directly without first expanding to a separate LAS file.
- [PDAL readers.las](https://pdal.org/en/stable/stages/readers.las.html) supports LAS and LAZ, but compressed LAZ support depends on a PDAL build with LASzip or LAZperf. Local `pdal` is not installed, so PDAL is a tool/runtime dependency decision rather than a ready path.
- [NuGet LASzip](https://www.nuget.org/packages/LASzip) exists as a .NET package for LAS/LAZ point-cloud parsing, but the current package-search output shows low download count relative to Aardvark packages.
- [NuGet Unofficial.laszip.netstandard](https://www.nuget.org/packages/Unofficial.laszip.netstandard/) targets .NET Standard 2.0 and is the smallest first candidate to prototype without a native CLI tool.
- [NuGet Aardvark.Data.Points.LasZip](https://www.nuget.org/packages/Aardvark.Data.Points.LasZip/) is a higher-level LAS/LAZ parser package that depends on `Unofficial.laszip.netstandard`; use it only if the lower-level package makes the prototype too much custom code.

Local package search evidence:

```text
dotnet package search LASzip --format json --take 10

nuget.org candidates:
- LASzip 0.7.0
- Unofficial.laszip.netstandard 5.6.2
- Aardvark.Data.Points.LasZip 5.6.2
- Unofficial.laszip.net 2.2.0
- LasZipNetStandard 1.1.0
```

## Decision

Do not add PDAL or LAStools as the product path yet. They are useful developer tools, but they would make the desktop app depend on an external installation or vendored native binaries.

The isolated managed prototype now lives in `src/OpenVisionLab.ThreeD.Data`:

1. Use `Unofficial.laszip.netstandard`.
2. Keep `Aardvark.Data.Points.LasZip` as a fallback only if future files expose missing behavior.
3. Keep the Viewer project free of direct LASzip package references; Viewer should consume `LazPointCloud` sampled points from `OpenVisionLab.ThreeD.Data`.

## Prototype Acceptance Checklist

- Decode `xyzrgb_manuscript.laz` without mutating or expanding the source file in place. Done by Runner probe.
- Read scaled XYZ coordinates and RGB for at least one sampled render set. Done by Runner probe.
- Confirm decoded coordinate bounds match the metadata bounds within a small tolerance. Done by Runner probe.
- Downsample for Viewer rendering instead of drawing all 2,155,617 points by default. Done in `LazPointCloud.SampledPoints`.
- Record a smoke contract with at least:

```text
LAZ|loaded=True|decoder=points-decoded|decodedPoints=2155617|sampledPoints=<n>|rgb=True|boundsMatch=True
```

- Render a current-build screenshot such as `artifacts/laz_points_after.png`. Done by Viewer `--smoke-laz-points`.
- Keep source geometry and result overlays separate.

## Next Smallest Task

Extend LAZ point-cloud inspection with picking/selection and add shell-hosted LAZ point screenshot smoke.
