using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal sealed record CompletenessValidationVerificationFixture(
    string RootPath,
    string RecipePath,
    ToolRecipeDocument Document,
    IReadOnlyList<ToolRecipeValidationSampleInput> Samples);

internal static class CompletenessValidationVerificationFixtureFactory
{
    public static CompletenessValidationVerificationFixture Create(
        string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        Directory.CreateDirectory(rootPath);

        var taughtPath = Path.Combine(rootPath, "completeness-taught.C3D");
        var samples = new[]
        {
            CreateSample(rootPath, "completeness-good-low.C3D", [2, 3, 4, 2.5], 0, ToolRecipeValidationSampleRole.Good),
            CreateSample(rootPath, "completeness-good-high.C3D", [2.5, 3.5, 4.5, 3], 0, ToolRecipeValidationSampleRole.Good),
            CreateSample(rootPath, "completeness-bad-low.C3D", [-5, 3, 8, 2], 2, ToolRecipeValidationSampleRole.Bad),
            CreateSample(rootPath, "completeness-bad-high.C3D", [-4, 3, 7, 2], 1, ToolRecipeValidationSampleRole.Bad),
            CreateSample(rootPath, "completeness-held-out.C3D", [2.2, 3.2, 4.2, 2.8], 0, ToolRecipeValidationSampleRole.HeldOut),
        };
        WriteHeightField(taughtPath, [2.2, 3.2, 4.2, 2.8], 0);

        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(
            taughtPath);
        var sourceInfo = new FileInfo(taughtPath);
        var source = new ToolRecipeSource(
            "source.validation.completeness",
            "Completeness taught source",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            taughtPath,
            sourceInfo.Length,
            binding.ContentSha256,
            binding.GridWidth,
            binding.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.validation.completeness.reference",
            "Completeness Reference ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(0, 0, 1, 4),
            null,
            null);
        var inspection = new ToolRecipeSelection(
            "selection.validation.completeness.inspection",
            "Completeness Inspection Grid ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(2, 0, 4, 4),
            null,
            null);
        var profile = new C3DCompletenessGridProfile(
            2,
            2,
            2,
            2,
            2,
            2,
            C3DCompletenessCellShape.GridRectangle);
        var policy = new C3DCompletenessPresencePolicy(0.8, 0, 6);
        var step = new ToolRecipeStep(
            "step.validation.completeness",
            "completeness-grid",
            "Completeness Grid",
            3,
            [source.Id, reference.Id, inspection.Id],
            "result.validation.completeness",
            profile.ToRecipeParameters()
                .Concat(policy.ToRecipeParameters())
                .ToArray());
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Completeness Validation Set fixture",
            source,
            [],
            [step],
            [reference, inspection]);
        var recipePath = Path.Combine(
            rootPath,
            "completeness-threshold-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, document);
        ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
            recipePath,
            new ToolRecipeValidationSetDefinition(
                ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                document.Name,
                document.Source.ContentSha256!,
                samples.Select((sample, index) =>
                    new ToolRecipeValidationSampleDefinition(
                        index + 1,
                        sample.SourcePath,
                        sample.Role)).ToArray()));

        return new CompletenessValidationVerificationFixture(
            rootPath,
            recipePath,
            document,
            samples);
    }

    private static ToolRecipeValidationSampleInput CreateSample(
        string rootPath,
        string fileName,
        IReadOnlyList<double> relativeCellHeights,
        int missingCellsInSecondCell,
        ToolRecipeValidationSampleRole role)
    {
        var path = Path.Combine(rootPath, fileName);
        WriteHeightField(path, relativeCellHeights, missingCellsInSecondCell);
        return new ToolRecipeValidationSampleInput(path, role);
    }

    private static void WriteHeightField(
        string path,
        IReadOnlyList<double> relativeCellHeights,
        int missingCellsInSecondCell)
    {
        if (relativeCellHeights.Count != 4
            || missingCellsInSecondCell is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeCellHeights));
        }

        const int width = 6;
        const int height = 6;
        var values = Enumerable.Repeat(10d, width * height).ToArray();
        var cells = new[]
        {
            new ToolRecipeGridRectangle(2, 0, 2, 2),
            new ToolRecipeGridRectangle(2, 2, 2, 2),
            new ToolRecipeGridRectangle(4, 0, 2, 2),
            new ToolRecipeGridRectangle(4, 2, 2, 2),
        };
        for (var index = 0; index < cells.Length; index++)
        {
            Fill(
                values,
                width,
                cells[index],
                10d + relativeCellHeights[index]);
        }
        for (var index = 0; index < missingCellsInSecondCell; index++)
        {
            var rowOffset = index / 2;
            var columnOffset = index % 2;
            values[(2 + rowOffset) * width + 2 + columnOffset] = double.NaN;
        }

        C3DHeightFieldSnapshot.CreateForVerification(
            "source.validation.completeness",
            width,
            height,
            values).SaveC3D(path);
    }

    private static void Fill(
        double[] values,
        int width,
        ToolRecipeGridRectangle rectangle,
        double value)
    {
        for (var row = rectangle.Row;
             row < rectangle.Row + rectangle.RowCount;
             row++)
        for (var column = rectangle.Column;
             column < rectangle.Column + rectangle.ColumnCount;
             column++)
        {
            values[row * width + column] = value;
        }
    }
}
