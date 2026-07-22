using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using System.IO;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// Deterministic smoke-only fixture for proving the live A2 -> A3 Publish ->
/// Plane Flatness two-role Viewer teaching path. It is not production or
/// physical-metrology evidence.
/// </summary>
internal static class PlaneFlatnessLiveA3PointerSmokeFixture
{
    private const int GridSize = 8;
    internal const string SourceEntityId = "source.live-a3-pointer";
    internal const string AffineEntityId = "fixture.live-a3.affine";
    internal const string CloudEntityId = "fixture.live-a3.cloud";
    internal const string HeightFieldEntityId = "derived.live-a3.height-field";
    internal const string RegridStepId = "step.live-a3.regrid";
    internal const string PlaneFlatnessStepId = "step.live-a3.plane-flatness";
    internal const string PointPairStepId = "step.live-a3.point-pair";
    internal const string GapFlushStepId = "step.live-a3.gap-flush";
    internal const string VolumeStepId = "step.live-a3.volume";
    internal const string CrossSectionStepId = "step.live-a3.cross-section";

    internal static (string RecipePath, string Summary) Prepare(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        var root = Path.GetFullPath(packageDirectory);
        Directory.CreateDirectory(root);

        var sourcePath = Path.Combine(root, "source.C3D");
        var recipePath = Path.Combine(root, "plane-flatness-live-a3.ov3d-recipe.json");
        var seed = C3DHeightFieldSnapshot.CreateForVerification(
            SourceEntityId,
            GridSize,
            GridSize,
            Enumerable.Range(0, GridSize * GridSize)
                .Select(index => SyntheticHeight(index / GridSize, index % GridSize))
                .ToArray());
        seed.SaveC3D(sourcePath);
        var snapshot = C3DHeightFieldSnapshot.LoadVerified(
            sourcePath,
            SourceEntityId,
            seed.Unit,
            seed.FrameId,
            seed.ByteLength,
            seed.ContentSha256,
            seed.Width,
            seed.Height);

        var cloud = CreatePublishedA2(snapshot);
        var profile = CreateProfile(cloud);
        var regridOnly = CreateDocument(snapshot, cloud, profile, [], includePlaneFlatness: false);
        var a3 = ToolRecipeRegridHeightFieldExecution.Execute(regridOnly, RegridStepId, cloud).Output
            ?? throw new InvalidDataException("The deterministic live A3 fixture did not produce a TransformedHeightField.");
        var binding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(a3);
        var selections = new[]
        {
            CreateSelection("selection.live-a3.reference-roi.initial", "Initial reference ROI", cloud, a3, binding),
            CreateSelection("selection.live-a3.measurement-roi.initial", "Initial measurement ROI", cloud, a3, binding),
            CreatePointPairSelection(cloud, a3, binding),
            CreateSelection("selection.live-a3.gap-first-roi.initial", "Initial Gap / Flush first ROI", cloud, a3, binding, new ToolRecipeGridRectangle(2, 1, 3, 2)),
            CreateSelection("selection.live-a3.gap-second-roi.initial", "Initial Gap / Flush second ROI", cloud, a3, binding, new ToolRecipeGridRectangle(2, 5, 3, 2)),
            CreateSelection("selection.live-a3.cross-section-row.initial", "Initial cross-section row segment", cloud, a3, binding, new ToolRecipeGridRectangle(3, 1, 1, 6))
        };
        var document = CreateDocument(snapshot, cloud, profile, selections, includePlaneFlatness: true);
        var validation = ToolRecipeValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(" ", validation.Errors));
        }

        ToolRecipeDocumentStore.Save(recipePath, document);
        var summary =
            $"Prepared deterministic live A3 pointer fixture | recipe={recipePath} | sourceSha256={snapshot.ContentSha256} | " +
            $"a2Sha256={cloud.ContentSha256} | a3Sha256={a3.ContentSha256} | boundary=synthetic-display-frame-only";
        return (recipePath, summary);
    }

    internal static C3DTransformedPointCloud CreatePublishedA2(string recipePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        var fullRecipePath = Path.GetFullPath(recipePath);
        var document = ToolRecipeDocumentStore.Load(fullRecipePath);
        var source = document.Source;
        var snapshot = C3DHeightFieldSnapshot.LoadVerified(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullRecipePath)!, source.Path)),
            source.Id,
            source.Unit,
            source.FrameId,
            source.ByteLength ?? throw new InvalidDataException("Fixture source byte length is missing."),
            source.ContentSha256 ?? throw new InvalidDataException("Fixture source SHA-256 is missing."),
            source.GridWidth ?? throw new InvalidDataException("Fixture source width is missing."),
            source.GridHeight ?? throw new InvalidDataException("Fixture source height is missing."));
        return CreatePublishedA2(snapshot);
    }

    private static ToolRecipeSelection CreateSelection(
        string id,
        string name,
        C3DTransformedPointCloud cloud,
        C3DTransformedHeightField a3,
        ToolRecipeSelectionSourceBinding binding,
        ToolRecipeGridRectangle? rectangle = null) =>
        new(
            id,
            name,
            ToolRecipeSelectionKinds.GridRectangle,
            cloud.RootSourceEntityId,
            a3.ReferenceFrameId,
            binding,
            rectangle ?? new ToolRecipeGridRectangle(0, 0, GridSize, GridSize),
            null,
            null);

    private static ToolRecipeSelection CreatePointPairSelection(
        C3DTransformedPointCloud cloud,
        C3DTransformedHeightField a3,
        ToolRecipeSelectionSourceBinding binding)
    {
        var populated = a3.Cells.Where(cell => cell.HasValue).ToArray();
        if (populated.Length < 2)
        {
            throw new InvalidDataException("Live A3 Point Pair fixture requires at least two populated cells.");
        }

        ToolRecipeSelectionPoint Point(int row, int column)
        {
            var cell = a3.Cells.Single(candidate => candidate.Row == row && candidate.Column == column && candidate.HasValue);
            var profile = a3.ReferenceGridProfile;
            var u = (column + 0.5d) * profile.PitchU;
            var v = (row + 0.5d) * profile.PitchV;
            return new ToolRecipeSelectionPoint(
                new ToolRecipeGridCellLocator("grid-cell", row, column),
                new ToolRecipeXyz(
                    profile.Origin.X + profile.UAxis.X * u + profile.VAxis.X * v + profile.HAxis.X * cell.Height,
                    profile.Origin.Y + profile.UAxis.Y * u + profile.VAxis.Y * v + profile.HAxis.Y * cell.Height,
                    profile.Origin.Z + profile.UAxis.Z * u + profile.VAxis.Z * v + profile.HAxis.Z * cell.Height),
                cell.Height);
        }

        return new ToolRecipeSelection(
            "selection.live-a3.point-pair.initial",
            "Initial point pair",
            ToolRecipeSelectionKinds.PointSet,
            cloud.RootSourceEntityId,
            a3.ReferenceFrameId,
            binding,
            null,
            [Point(populated[0].Row, populated[0].Column), Point(populated[^1].Row, populated[^1].Column)],
            null);
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot snapshot,
        C3DTransformedPointCloud cloud,
        C3DReferenceGridProfile profile,
        IReadOnlyList<ToolRecipeSelection> selections,
        bool includePlaneFlatness)
    {
        var steps = new List<ToolRecipeStep>
        {
            new("step.live-a3.solve", "fixture-affine-solve", "Synthetic affine prerequisite", 1,
                [snapshot.EntityId], AffineEntityId, []),
            new("step.live-a3.apply", "xyz-affine-apply", "Apply XYZ Affine", 2,
                [snapshot.EntityId, AffineEntityId], CloudEntityId, []),
            new(RegridStepId, "re-grid-height-map", "Re-grid Height Map", 1,
                [CloudEntityId], HeightFieldEntityId, profile.ToRecipeParameters())
        };
        if (includePlaneFlatness)
        {
            steps.Add(new ToolRecipeStep(
                PlaneFlatnessStepId,
                "plane-flatness",
                "Plane Flatness",
                3,
                [HeightFieldEntityId, selections[0].Id, selections[1].Id],
                "result.live-a3.plane-flatness",
                [
                    new ToolRecipeParameter("MaximumFlatness", "100"),
                    new ToolRecipeParameter("MinimumReferenceSampleCount", "3"),
                    new ToolRecipeParameter("MinimumMeasurementSampleCount", "3")
                ]));
            steps.Add(new ToolRecipeStep(
                PointPairStepId,
                "point-pair-dimensions",
                "Point Pair Dimensions",
                2,
                [HeightFieldEntityId, selections[2].Id],
                "result.live-a3.point-pair",
                [
                    new ToolRecipeParameter("ExpectedDistance", "0"),
                    new ToolRecipeParameter("DistanceTolerance", "100000"),
                    new ToolRecipeParameter("ExpectedPlanarWidth", "0"),
                    new ToolRecipeParameter("PlanarWidthTolerance", "100000"),
                    new ToolRecipeParameter("ExpectedElevationAngleDegrees", "0"),
                    new ToolRecipeParameter("ElevationAngleToleranceDegrees", "90")
                ]));
            steps.Add(new ToolRecipeStep(
                GapFlushStepId,
                "gap-flush",
                "Gap / Flush",
                3,
                [HeightFieldEntityId, selections[3].Id, selections[4].Id],
                "result.live-a3.gap-flush",
                [
                    new ToolRecipeParameter("ExpectedGap", "0"),
                    new ToolRecipeParameter("GapTolerance", "100000"),
                    new ToolRecipeParameter("ExpectedFlush", "0"),
                    new ToolRecipeParameter("FlushTolerance", "100000")
                ]));
            steps.Add(new ToolRecipeStep(
                VolumeStepId,
                "volume",
                "Volume",
                3,
                [HeightFieldEntityId, selections[0].Id, selections[1].Id],
                "result.live-a3.volume",
                [
                    new ToolRecipeParameter("ExpectedNetVolume", "0"),
                    new ToolRecipeParameter("VolumeTolerance", "100000")
                ]));
            steps.Add(new ToolRecipeStep(
                CrossSectionStepId,
                "cross-section-dimensions",
                "Cross-section Dimensions",
                2,
                [HeightFieldEntityId, selections[5].Id],
                "result.live-a3.cross-section",
                [
                    new ToolRecipeParameter("ExpectedWidth", "5"),
                    new ToolRecipeParameter("WidthTolerance", "100000"),
                    new ToolRecipeParameter("ExpectedHeightRange", "5"),
                    new ToolRecipeParameter("HeightTolerance", "100000")
                ]));
        }

        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Synthetic A3 Plane Flatness pointer teaching",
            new ToolRecipeSource(
                snapshot.EntityId,
                "Deterministic synthetic C3D",
                "C3D",
                snapshot.Unit,
                snapshot.FrameId,
                "source.C3D",
                snapshot.ByteLength,
                snapshot.ContentSha256,
                snapshot.Width,
                snapshot.Height),
            [],
            steps,
            selections);
    }

    private static C3DTransformedPointCloud CreatePublishedA2(C3DHeightFieldSnapshot snapshot)
    {
        var pairs = new[]
        {
            Pair("a", snapshot, 0, 0, SyntheticHeight(0, 0), 0.25, 0.25, SyntheticHeight(0, 0)),
            Pair("b", snapshot, 0, GridSize - 1, SyntheticHeight(0, GridSize - 1), GridSize - 0.75, 0.25, SyntheticHeight(0, GridSize - 1)),
            Pair("c", snapshot, GridSize - 1, 0, SyntheticHeight(GridSize - 1, 0), 0.25, GridSize - 0.75, SyntheticHeight(GridSize - 1, 0)),
            Pair("d", snapshot, GridSize - 1, GridSize - 1, SyntheticHeight(GridSize - 1, GridSize - 1), GridSize - 0.75, GridSize - 0.75, SyntheticHeight(GridSize - 1, GridSize - 1))
        };
        var correspondence = C3DLandmarkCorrespondenceSet.Create(
            "fixture.live-a3.correspondence",
            pairs,
            snapshot.EntityId,
            snapshot.RootSourceSha256,
            snapshot.Unit,
            snapshot.FrameId,
            "frame.synthetic-reference",
            "synthetic-unit",
            "OpenVisionLab deterministic live A3 pointer fixture",
            "R1",
            1e-12,
            4,
            4,
            0.1,
            0.1,
            "synthetic live A3 pointer fixture");
        var transform = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
                "step.live-a3.solve",
                AffineEntityId,
                correspondence,
                1e9,
                1e-9)).Output
            ?? throw new InvalidDataException("The deterministic live A3 fixture affine solve failed.");
        return C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput(
                "step.live-a3.apply",
                snapshot,
                transform,
                CloudEntityId)).Output
            ?? throw new InvalidDataException("The deterministic live A3 fixture affine application failed.");
    }

    private static C3DLandmarkCorrespondencePair Pair(
        string id,
        C3DHeightFieldSnapshot snapshot,
        int row,
        int column,
        double rawHeight,
        double referenceX,
        double referenceY,
        double referenceZ) =>
        new(
            id,
            id,
            snapshot.RootSourceSha256,
            column,
            rawHeight,
            row,
            $"reference.{id}",
            referenceX,
            referenceY,
            referenceZ);

    private static C3DReferenceGridProfile CreateProfile(C3DTransformedPointCloud cloud) =>
        C3DReferenceGridProfile.Create(
            cloud.ReferenceFrameId,
            cloud.ReferenceUnit,
            cloud.ReferenceProvenance,
            cloud.ReferenceRevision,
            new C3DReferenceGridVector(0.25, 0.25, 0),
            new C3DReferenceGridVector(1, 0, 0),
            new C3DReferenceGridVector(0, 1, 0),
            new C3DReferenceGridVector(0, 0, 1),
            1,
            1,
            GridSize,
            GridSize,
            0.8);

    private static double SyntheticHeight(int row, int column) =>
        10d + (row * 2d) + column + (row * column * 0.1d);
}
