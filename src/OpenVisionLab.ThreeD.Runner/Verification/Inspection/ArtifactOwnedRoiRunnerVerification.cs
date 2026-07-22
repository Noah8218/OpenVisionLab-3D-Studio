using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ArtifactOwnedRoiRunnerVerification
{
    public static int Run(string reportPath)
    {
        var checks = new List<(string Name, bool Passed, string Evidence)>();
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", nameof(ArtifactOwnedRoiRunnerVerification), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var cloud = CreateCloud();
            var profile = CreateProfile(cloud);
            var baseDocument = CreateDocument(cloud, profile, null, includeMeasurement: false);
            var expectedA3 = ToolRecipeRegridHeightFieldExecution.Execute(baseDocument, "step.regrid", cloud).Output
                ?? throw new InvalidDataException("Synthetic A3 fixture did not produce a height field.");
            var binding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(expectedA3);
            var measurementSelection = new ToolRecipeSelection(
                "selection.transformed.roi", "Transformed measurement ROI", ToolRecipeSelectionKinds.GridRectangle,
                cloud.RootSourceEntityId, expectedA3.ReferenceFrameId, binding,
                new ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            var referenceSelection = measurementSelection with
            {
                Id = "selection.transformed.reference-roi",
                Name = "Transformed reference ROI"
            };
            var pointPairSelection = new ToolRecipeSelection(
                "selection.transformed.point-pair", "Transformed point pair", ToolRecipeSelectionKinds.PointSet,
                cloud.RootSourceEntityId, expectedA3.ReferenceFrameId, binding, null,
                [
                    new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", 0, 0), new ToolRecipeXyz(0.5, 0.5, 10), 10),
                    new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", 1, 1), new ToolRecipeXyz(1.5, 1.5, 17), 17)
                ], null);
            var gapFirstSelection = measurementSelection with
            {
                Id = "selection.transformed.gap-first-roi",
                Name = "Gap / Flush first ROI",
                GridRectangle = new ToolRecipeGridRectangle(0, 0, 2, 1)
            };
            var gapSecondSelection = measurementSelection with
            {
                Id = "selection.transformed.gap-second-roi",
                Name = "Gap / Flush second ROI",
                GridRectangle = new ToolRecipeGridRectangle(0, 1, 2, 1)
            };
            var document = CreateDocument(
                cloud, profile, [referenceSelection, measurementSelection, pointPairSelection, gapFirstSelection, gapSecondSelection],
                includeMeasurement: true, includeWarpage: true, includePlaneFlatness: true, includePointPair: true, includeGapFlush: true);

            var validation = ToolRecipeValidator.Validate(document);
            checks.Add(("artifact-owned schema and route", validation.IsValid, string.Join(" / ", validation.Errors)));

            var direct = ToolRecipeHeightMeasurementExecution.Execute(document, "step.thickness", expectedA3);
            var directWarpage = ToolRecipeHeightMeasurementExecution.Execute(document, "step.warpage", expectedA3);
            var directFlatness = ToolRecipeHeightMeasurementExecution.Execute(document, "step.plane-flatness", expectedA3);
            var directPointPair = ToolRecipeHeightMeasurementExecution.Execute(document, "step.point-pair", expectedA3);
            var directGapFlush = ToolRecipeHeightMeasurementExecution.Execute(document, "step.gap-flush", expectedA3);
            var ordered = ToolRecipeTransformedHeightFieldMeasurementSequence.Execute(
                document, "step.regrid", "step.thickness", cloud);
            var orderedAll = ToolRecipeTransformedHeightFieldMeasurementSequence.ExecuteOrdered(
                document, "step.regrid", cloud);
            checks.Add(("legacy single measurement sequence remains compatible",
                direct.Output is not null && ordered.Output is not null
                && direct.Output.ContentSha256 == ordered.Output.Measurement.ContentSha256
                && ordered.Output.HeightField.ContentSha256 == expectedA3.ContentSha256,
                $"direct={direct.Output?.ContentSha256};runner={ordered.Output?.Measurement.ContentSha256};a3={ordered.Output?.HeightField.ContentSha256}"));
            checks.Add(("ordered Runner executes all supported measurements in authored order",
                orderedAll.Output is not null
                && orderedAll.Output.Measurements.Select(item => item.StepId).SequenceEqual(["step.thickness", "step.warpage", "step.plane-flatness", "step.point-pair", "step.gap-flush"])
                && orderedAll.Output.HeightField.ContentSha256 == expectedA3.ContentSha256,
                orderedAll.Output is null
                    ? orderedAll.Result.Message
                    : string.Join(",", orderedAll.Output.Measurements.Select(item => $"{item.RecipeIndex}:{item.StepId}"))));
            checks.Add(("direct adapters and ordered Runner output hashes match",
                direct.Output is not null && directWarpage.Output is not null && orderedAll.Output is not null
                && directFlatness.Output is not null && directPointPair.Output is not null && directGapFlush.Output is not null
                && orderedAll.Output.Measurements[0].Output.ContentSha256 == direct.Output.ContentSha256
                && orderedAll.Output.Measurements[1].Output.ContentSha256 == directWarpage.Output.ContentSha256
                && orderedAll.Output.Measurements[2].Output.ContentSha256 == directFlatness.Output.ContentSha256
                && orderedAll.Output.Measurements[3].Output.ContentSha256 == directPointPair.Output.ContentSha256
                && orderedAll.Output.Measurements[4].Output.ContentSha256 == directGapFlush.Output.ContentSha256,
                $"thickness={direct.Output?.ContentSha256};warpage={directWarpage.Output?.ContentSha256};flatness={directFlatness.Output?.ContentSha256};pointPair={directPointPair.Output?.ContentSha256};gapFlush={directGapFlush.Output?.ContentSha256}"));
            checks.Add(("Plane Flatness consumes ordered reference and measurement ROIs",
                directFlatness.Output is not null
                && directFlatness.Output.SelectionId == "selection.transformed.reference-roi;selection.transformed.roi"
                && directFlatness.Result.Metrics.Any(metric => metric.Name == "Flatness"),
                directFlatness.Output is null ? directFlatness.Result.Message : directFlatness.Output.EvidenceSummary));
            checks.Add(("Point Pair reconstructs full XYZ from A3 and preserves ordered selection",
                directPointPair.Output is not null
                && directPointPair.Output.SelectionId == pointPairSelection.Id
                && Approximately(directPointPair.Result.Metrics.Single(metric => metric.Name == "3D distance").Value, Math.Sqrt(51d))
                && Approximately(directPointPair.Result.Metrics.Single(metric => metric.Name == "Planar width").Value, Math.Sqrt(2d))
                && Approximately(directPointPair.Result.Metrics.Single(metric => metric.Name == "Height-axis delta").Value, 7d),
                directPointPair.Output is null ? directPointPair.Result.Message : directPointPair.Output.EvidenceSummary));
            checks.Add(("Gap / Flush consumes ordered artifact ROIs and reference-grid U/H axes",
                directGapFlush.Output is not null
                && directGapFlush.Output.SelectionId == $"{gapFirstSelection.Id};{gapSecondSelection.Id}"
                && Approximately(directGapFlush.Result.Metrics.Single(metric => metric.Name == "Signed gap").Value, 0d)
                && Approximately(directGapFlush.Result.Metrics.Single(metric => metric.Name == "Signed flush").Value, 2.5d),
                directGapFlush.Output is null ? directGapFlush.Result.Message : directGapFlush.Output.EvidenceSummary));
            checks.Add(("failed tolerance does not suppress later measurement evidence",
                orderedAll.Output is not null
                && orderedAll.Result.Status == ResultStatus.Fail
                && orderedAll.Output.Measurements[0].Output.Result.Status == ResultStatus.Fail
                && orderedAll.Output.Measurements[1].Output.Result.Status == ResultStatus.Pass,
                orderedAll.Output is null
                    ? orderedAll.Result.Message
                    : $"overall={orderedAll.Result.Status};first={orderedAll.Output.Measurements[0].Output.Result.Status};second={orderedAll.Output.Measurements[1].Output.Result.Status}"));

            var recipePath = Path.Combine(root, "artifact-owned.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var reopened = ToolRecipeDocumentStore.Load(recipePath);
            var reopenedBindings = reopened.Selections!.Select(item => item.SourceBinding).ToArray();
            checks.Add(("artifact-owned ROI save and reopen",
                reopenedBindings.Length == 5
                && reopenedBindings.All(item => ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(binding, item))
                && reopened.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion,
                $"schema={reopened.SchemaVersion};selections={reopenedBindings.Length};owner={reopenedBindings[0].OwnerEntityId};hash={reopenedBindings[0].ContentSha256}"));

            var wrongOwner = ReplaceBinding(document, binding with { OwnerEntityId = "derived.wrong-owner" });
            checks.Add(("wrong owner rejected", !ToolRecipeValidator.Validate(wrongOwner).IsValid,
                string.Join(" / ", ToolRecipeValidator.Validate(wrongOwner).Errors)));

            var wrongHash = ReplaceBinding(document, binding with { ContentSha256 = new string('A', 64) });
            var wrongHashEvaluation = ToolRecipeHeightMeasurementExecution.Execute(wrongHash, "step.thickness", expectedA3);
            checks.Add(("wrong artifact hash rejected", wrongHashEvaluation.Output is null && wrongHashEvaluation.Result.Status == ResultStatus.Error,
                wrongHashEvaluation.Result.Message));

            var wrongGrid = ReplaceBinding(document, binding with { GridWidth = binding.GridWidth + 1 });
            var wrongGridEvaluation = ToolRecipeHeightMeasurementExecution.Execute(wrongGrid, "step.thickness", expectedA3);
            checks.Add(("wrong artifact grid rejected", wrongGridEvaluation.Output is null && wrongGridEvaluation.Result.Status == ResultStatus.Error,
                wrongGridEvaluation.Result.Message));

            var wrongOrder = document with { Steps = [document.Steps[^1], .. document.Steps.Take(document.Steps.Count - 1)] };
            var wrongOrderEvaluation = ToolRecipeTransformedHeightFieldMeasurementSequence.Execute(
                wrongOrder, "step.regrid", "step.thickness", cloud);
            checks.Add(("out-of-order Runner request rejected", wrongOrderEvaluation.Output is null && wrongOrderEvaluation.Result.Status == ResultStatus.Error,
                wrongOrderEvaluation.Result.Message));

            var unsupported = document with
            {
                Steps = [.. document.Steps, new ToolRecipeStep(
                    "step.review", "overlay-control-review", "Overlay / Control Review", 1,
                    ["result.warpage"], "result.review", [])]
            };
            var unsupportedEvaluation = ToolRecipeTransformedHeightFieldMeasurementSequence.ExecuteOrdered(
                unsupported, "step.regrid", cloud);
            checks.Add(("unsupported downstream tool rejected",
                unsupportedEvaluation.Output is null && unsupportedEvaluation.Result.Status == ResultStatus.Error,
                unsupportedEvaluation.Result.Message));

            var noMeasurementEvaluation = ToolRecipeTransformedHeightFieldMeasurementSequence.ExecuteOrdered(
                baseDocument, "step.regrid", cloud);
            checks.Add(("missing downstream measurement rejected",
                noMeasurementEvaluation.Output is null && noMeasurementEvaluation.Result.Status == ResultStatus.Error,
                noMeasurementEvaluation.Result.Message));

            var duplicateOutput = document with
            {
                Steps = [.. document.Steps.Take(document.Steps.Count - 1), document.Steps[^1] with { OutputEntityId = "result.thickness" }]
            };
            var duplicateOutputEvaluation = ToolRecipeTransformedHeightFieldMeasurementSequence.ExecuteOrdered(
                duplicateOutput, "step.regrid", cloud);
            checks.Add(("duplicate output identity rejected",
                duplicateOutputEvaluation.Output is null && duplicateOutputEvaluation.Result.Status == ResultStatus.Error,
                duplicateOutputEvaluation.Result.Message));
        }
        catch (Exception exception)
        {
            checks.Add(("unexpected exception", false, $"{exception.GetType().Name}: {exception.Message}"));
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }

        var passed = checks.Count(check => check.Passed);
        var success = checks.Count > 0 && passed == checks.Count;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath,
        [
            $"ArtifactOwnedRoiRunnerVerification|{(success ? "Pass" : "Fail")}|checks={checks.Count}|passed={passed}|failed={checks.Count - passed}",
            "Boundary|sequence begins with one explicit Published A2 TransformedPointCloud and supports only A3 followed by Thickness/Warpage/Plane Flatness/Point Pair Dimensions/Gap-Flush; A1/A2 production, arbitrary graphs, automatic seam detection, and physical metrology are excluded",
            .. checks.Select(check => $"{(check.Passed ? "PASS" : "FAIL")}|{check.Name}|{Clean(check.Evidence)}")
        ]);
        Console.WriteLine($"Artifact-owned ROI ordered Runner verification: {(success ? "Pass" : "Fail")} ({passed}/{checks.Count})");
        return success ? 0 : 5;
    }

    private static ToolRecipeDocument ReplaceBinding(ToolRecipeDocument document, ToolRecipeSelectionSourceBinding binding) =>
        document with { Selections = document.Selections!.Select(selection => selection with { SourceBinding = binding }).ToArray() };

    private static ToolRecipeDocument CreateDocument(
        C3DTransformedPointCloud cloud,
        C3DReferenceGridProfile profile,
        IReadOnlyList<ToolRecipeSelection>? selections,
        bool includeMeasurement,
        bool includeWarpage = false,
        bool includePlaneFlatness = false,
        bool includePointPair = false,
        bool includeGapFlush = false)
    {
        var steps = new List<ToolRecipeStep>
        {
            new("step.fixture.solve", "fixture-affine-solve", "Fixture solve", 1, [cloud.RootSourceEntityId], cloud.AffineTransformEntityId, []),
            new("step.fixture.apply", "xyz-affine-apply", "Apply XYZ Affine", 2, [cloud.RootSourceEntityId, cloud.AffineTransformEntityId], cloud.OutputEntityId, []),
            new("step.regrid", "re-grid-height-map", "Re-grid Height Map", 1, [cloud.OutputEntityId], "derived.height-field", profile.ToRecipeParameters())
        };
        if (includeMeasurement)
        {
            steps.Add(new ToolRecipeStep(
                "step.thickness", "thickness", "Thickness", 2,
                ["derived.height-field", selections!.Single(item => item.Id == "selection.transformed.roi").Id], "result.thickness",
                [new("MinimumThickness", "9"), new("MaximumThickness", "16"), new("MinimumValidSampleCount", "4")]));
        }
        if (includeWarpage)
        {
            steps.Add(new ToolRecipeStep(
                "step.warpage", "warpage", "Warpage", 2,
                ["derived.height-field", selections!.Single(item => item.Id == "selection.transformed.roi").Id], "result.warpage",
                [new("MaximumPeakToValley", "100"), new("MaximumRms", "100"), new("MinimumValidSampleCount", "3")]));
        }
        if (includePlaneFlatness)
        {
            steps.Add(new ToolRecipeStep(
                "step.plane-flatness", "plane-flatness", "Plane Flatness", 3,
                ["derived.height-field", "selection.transformed.reference-roi", "selection.transformed.roi"], "result.plane-flatness",
                [new("MaximumFlatness", "100"), new("MinimumReferenceSampleCount", "3"), new("MinimumMeasurementSampleCount", "3")]));
        }
        if (includePointPair)
        {
            steps.Add(new ToolRecipeStep(
                "step.point-pair", "point-pair-dimensions", "Point Pair Dimensions", 2,
                ["derived.height-field", "selection.transformed.point-pair"], "result.point-pair",
                [
                    new("ExpectedDistance", "0"), new("DistanceTolerance", "100"),
                    new("ExpectedPlanarWidth", "0"), new("PlanarWidthTolerance", "100"),
                    new("ExpectedElevationAngleDegrees", "0"), new("ElevationAngleToleranceDegrees", "90")
                ]));
        }
        if (includeGapFlush)
        {
            steps.Add(new ToolRecipeStep(
                "step.gap-flush", "gap-flush", "Gap / Flush", 3,
                ["derived.height-field", "selection.transformed.gap-first-roi", "selection.transformed.gap-second-roi"], "result.gap-flush",
                [
                    new("ExpectedGap", "0"), new("GapTolerance", "0.000001"),
                    new("ExpectedFlush", "2.5"), new("FlushTolerance", "0.000001")
                ]));
        }
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Artifact-owned ROI Runner fixture",
            new ToolRecipeSource(cloud.RootSourceEntityId, "Fixture C3D", "C3D", "raw-height", "frame.c3d-grid-index", "fixture.c3d", 99, cloud.RootSourceSha256, cloud.SourceGridWidth, cloud.SourceGridHeight),
            [], steps, selections ?? []);
    }

    private static C3DTransformedPointCloud CreateCloud()
    {
        var snapshot = C3DHeightFieldSnapshot.CreateForVerification(
            "source.artifact-roi", 2, 2, [10d, 12d, 14d, 17d], "raw-height", "frame.c3d-grid-index");
        var pairs = new[]
        {
            new C3DLandmarkCorrespondencePair("a", "a", snapshot.RootSourceSha256, 0, 10, 0, "ra", 0, 0, 0),
            new C3DLandmarkCorrespondencePair("b", "b", snapshot.RootSourceSha256, 1, 12, 0, "rb", 1, 0, 0),
            new C3DLandmarkCorrespondencePair("c", "c", snapshot.RootSourceSha256, 0, 14, 1, "rc", 0, 1, 0),
            new C3DLandmarkCorrespondencePair("d", "d", snapshot.RootSourceSha256, 1, 17, 1, "rd", 1, 1, 1)
        };
        var correspondence = C3DLandmarkCorrespondenceSet.Create(
            "fixture.correspondence", pairs, snapshot.EntityId, snapshot.RootSourceSha256,
            snapshot.Unit, snapshot.FrameId, "frame.fixture", "fixture-unit", "synthetic fixture", "R1",
            1e-12, 4, 4, 0.1, 0.1, "artifact ROI fixture");
        var transform = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
            "step.fixture.solve", "fixture.affine", correspondence, 1e9, 1e-9)).Output
            ?? throw new InvalidDataException("Synthetic affine fixture failed.");
        var points = new[]
        {
            new C3DTransformedPoint(0, 0, 10, 0.25, 0.25, 10),
            new C3DTransformedPoint(0, 1, 12, 1.25, 0.25, 12),
            new C3DTransformedPoint(1, 0, 14, 0.25, 1.25, 14),
            new C3DTransformedPoint(1, 1, 17, 1.25, 1.25, 17)
        };
        return C3DTransformedPointCloud.Create(
            "fixture.cloud", snapshot.EntityId, snapshot.RootSourceSha256, snapshot.Unit, snapshot.FrameId,
            C3DAffineApplyRule.SourceCoordinateConvention, snapshot.Width, snapshot.Height, transform, points, "artifact ROI fixture cloud");
    }

    private static C3DReferenceGridProfile CreateProfile(C3DTransformedPointCloud cloud) =>
        C3DReferenceGridProfile.Create(
            cloud.ReferenceFrameId, cloud.ReferenceUnit, cloud.ReferenceProvenance, cloud.ReferenceRevision,
            new C3DReferenceGridVector(0, 0, 0), new C3DReferenceGridVector(1, 0, 0),
            new C3DReferenceGridVector(0, 1, 0), new C3DReferenceGridVector(0, 0, 1),
            1, 1, 2, 2, 1);

    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private static bool Approximately(double actual, double expected) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-9;
}
