using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRegridHeightFieldGoldenVerification
{
    private const string SourceId = "source.regrid.fixture";
    private const string SourceUnit = "raw-height";
    private const string SourceFrame = "frame.c3d-grid-index";

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("explicit-grid-missing-cell-and-publish-coverage", VerifyProjectionAndCoverage),
            Check("deterministic-collision-winner-and-canonical-hash", VerifyCollisionAndDeterminism),
            Check("out-of-bounds-and-reference-identity-rejected", VerifyRejectedInput),
            Check("typed-recipe-adapter-and-incomplete-publish-gate", VerifyRecipeAdapterAndCoverageBlock)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath,
        [
            $"C3DRegridHeightFieldGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|input=Published-TransformedPointCloud-plus-typed-ReferenceGridProfile|output=TransformedHeightField|assignment=PlanarNearestCellCenter|collision=NearestCenterThenSourceLocator|bounds=RejectPreview|holes=PreserveMissing|measurement=excluded",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D Re-grid Height Map golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyProjectionAndCoverage()
    {
        var cloud = CreateCloud();
        var evaluation = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput(
            "step.regrid", cloud, CreateProfile(cloud, 0.75), "derived.height-field"));
        var output = evaluation.Output;
        var pass = evaluation.Result.Status == ResultStatus.Pass && output is not null
            && output.RowCount == 2 && output.ColumnCount == 2
            && output.PopulatedCellCount == 3 && output.MissingCellCount == 1
            && Nearly(output.CoverageRatio, 0.75) && output.MeetsMinimumCoverage
            && Nearly(output.Cells[0].Height, 10) && Nearly(output.Cells[1].Height, 20) && Nearly(output.Cells[2].Height, 30)
            && !output.Cells[3].HasValue && output.Cells[3].SourceRow == -1;
        return (pass, $"status={evaluation.Result.Status};cells={output?.PopulatedCellCount}/{output?.Cells.Count};coverage={output?.CoverageRatio:G17};missing={output?.MissingCellCount}");
    }

    private static (bool Passed, string Evidence) VerifyCollisionAndDeterminism()
    {
        var cloud = CreateCloud(
        [
            new C3DTransformedPoint(0, 0, 0, 0.25, 0.50, 20),
            new C3DTransformedPoint(0, 1, 0, 0.25, 0.50, 30),
            new C3DTransformedPoint(1, 1, 0, 0.75, 0.50, 90)
        ]);
        var profile = CreateProfile(cloud, 1.0, 1, 1);
        var first = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput("step.regrid", cloud, profile, "derived.height-field"));
        var second = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput("step.regrid", cloud, profile, "derived.height-field"));
        var output = first.Output;
        var pass = first.Result.Status == ResultStatus.Pass && second.Result.Status == ResultStatus.Pass
            && output is not null && second.Output is not null && output.ContentSha256 == second.Output.ContentSha256
            && output.CollisionCount == 2 && output.Cells[0].SourceRow == 0 && output.Cells[0].SourceColumn == 0 && Nearly(output.Cells[0].Height, 20);
        return (pass, $"first={first.Output?.ContentSha256};second={second.Output?.ContentSha256};collisions={output?.CollisionCount};winner={output?.Cells[0].SourceRow},{output?.Cells[0].SourceColumn}");
    }

    private static (bool Passed, string Evidence) VerifyRejectedInput()
    {
        var cloud = CreateCloud([new C3DTransformedPoint(0, 0, 0, 2.0, 0.0, 1.0)]);
        var outOfBounds = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput("step.regrid", cloud, CreateProfile(cloud, 0.0, 1, 1), "derived.height-field"));
        var wrongIdentity = C3DReferenceGridProfile.Create(
            "frame.other", "fixture-unit", "fixture reference", "R1",
            new C3DReferenceGridVector(0, 0, 0), new C3DReferenceGridVector(1, 0, 0), new C3DReferenceGridVector(0, 1, 0), new C3DReferenceGridVector(0, 0, 1),
            1, 1, 1, 1, 0);
        var identity = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput("step.regrid", cloud, wrongIdentity, "derived.height-field"));
        var pass = outOfBounds.Result.Status == ResultStatus.Error && outOfBounds.Result.Message.Contains("half-open", StringComparison.OrdinalIgnoreCase)
            && identity.Result.Status == ResultStatus.Error && identity.Result.Message.Contains("exactly matches", StringComparison.OrdinalIgnoreCase);
        return (pass, $"outOfBounds={outOfBounds.Result.Status}:{outOfBounds.Result.Message};identity={identity.Result.Status}:{identity.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyRecipeAdapterAndCoverageBlock()
    {
        var cloud = CreateCloud();
        var profile = CreateProfile(cloud, 1.0);
        var recipe = CreateRecipe(cloud, profile);
        var prepared = ToolRecipeRegridHeightFieldExecution.TryPrepare(recipe, "step.regrid", cloud, out var input, out var message);
        var evaluation = ToolRecipeRegridHeightFieldExecution.Execute(recipe, "step.regrid", cloud);
        var pass = prepared && input is not null && evaluation.Output is not null
            && evaluation.Result.Status == ResultStatus.Warning && !evaluation.Output.MeetsMinimumCoverage
            && Nearly(evaluation.Output.CoverageRatio, 0.75)
            && ToolRecipeValidator.Validate(recipe).IsValid;
        return (pass, $"prepared={prepared}:{message};status={evaluation.Result.Status};coverage={evaluation.Output?.CoverageRatio:G17};publishEligible={evaluation.Output?.MeetsMinimumCoverage}");
    }

    private static C3DTransformedPointCloud CreateCloud(IReadOnlyList<C3DTransformedPoint>? points = null)
    {
        var snapshot = C3DHeightFieldSnapshot.CreateForVerification(SourceId, 3, 2, [1d, 4d, 2d, 3d, 5d, 6d], SourceUnit, SourceFrame);
        var transform = CreatePublishedTransform(snapshot);
        var transformed = points ??
        [
            new C3DTransformedPoint(0, 0, 1, 0.10, 0.10, 10),
            new C3DTransformedPoint(0, 1, 4, 1.10, 0.10, 20),
            new C3DTransformedPoint(1, 0, 3, 0.10, 1.10, 30)
        ];
        return C3DTransformedPointCloud.Create(
            "derived.transformed-point-cloud", snapshot.EntityId, snapshot.RootSourceSha256, snapshot.Unit, snapshot.FrameId,
            C3DAffineApplyRule.SourceCoordinateConvention, snapshot.Width, snapshot.Height, transform, transformed, "regrid golden transformed cloud");
    }

    private static C3DReferenceGridProfile CreateProfile(C3DTransformedPointCloud cloud, double minimumCoverage, int rows = 2, int columns = 2) =>
        C3DReferenceGridProfile.Create(
            cloud.ReferenceFrameId, cloud.ReferenceUnit, cloud.ReferenceProvenance, cloud.ReferenceRevision,
            new C3DReferenceGridVector(0, 0, 0), new C3DReferenceGridVector(1, 0, 0), new C3DReferenceGridVector(0, 1, 0), new C3DReferenceGridVector(0, 0, 1),
            1, 1, rows, columns, minimumCoverage);

    private static ToolRecipeDocument CreateRecipe(C3DTransformedPointCloud cloud, C3DReferenceGridProfile profile) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Re-grid fixture",
            new ToolRecipeSource(SourceId, "Fixture C3D", "C3D", SourceUnit, SourceFrame, "fixture.c3d", 99, cloud.RootSourceSha256, cloud.SourceGridWidth, cloud.SourceGridHeight),
            [],
            [
                new ToolRecipeStep("step.fixture.solve", "fixture-affine-solve", "Fixture solve", 1, [SourceId], cloud.AffineTransformEntityId, []),
                new ToolRecipeStep("step.fixture.apply", "xyz-affine-apply", "Apply XYZ Affine", 2, [SourceId, cloud.AffineTransformEntityId], cloud.OutputEntityId, []),
                new ToolRecipeStep("step.regrid", "re-grid-height-map", "Re-grid Height Map", 1, [cloud.OutputEntityId], "derived.height-field", profile.ToRecipeParameters())
            ],
            []);

    private static C3DAffineTransform3D CreatePublishedTransform(C3DHeightFieldSnapshot snapshot)
    {
        var locators = new[] { (Row: 0, Column: 0), (Row: 0, Column: 1), (Row: 1, Column: 0), (Row: 1, Column: 1) };
        var pairs = locators.Select((locator, index) =>
        {
            var raw = snapshot.Values.Span[locator.Row * snapshot.Width + locator.Column];
            return new C3DLandmarkCorrespondencePair(
                $"derived.fixture.corner.{index}", "Corner anchor", snapshot.RootSourceSha256,
                locator.Column, raw, locator.Row, $"fixture.corner.{index}", locator.Column, raw, locator.Row);
        }).ToArray();
        var correspondence = C3DLandmarkCorrespondenceSet.Create(
            "derived.fixture.correspondence", pairs, snapshot.EntityId, snapshot.RootSourceSha256,
            snapshot.Unit, snapshot.FrameId, "frame.fixture-reference", "fixture-unit", "fixture reference", "R1",
            1e-12, 4, 4, 0.1, 0.1, "regrid golden fixture");
        var solve = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput("step.fixture.solve", "derived.fixture.affine", correspondence, 1000, 1e-12));
        return solve.Output ?? throw new InvalidDataException($"Re-grid fixture could not publish A1: {solve.Result.Message}");
    }

    private static bool Nearly(double actual, double expected) => Math.Abs(actual - expected) <= 1e-10;
    private static (string Name, bool Passed, string Evidence) Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try { var result = verify(); return (name, result.Passed, result.Evidence); }
        catch (Exception exception) { return (name, false, $"unexpected {exception.GetType().Name}: {exception.Message}"); }
    }
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
