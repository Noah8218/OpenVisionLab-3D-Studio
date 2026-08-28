using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightImageAlignmentRunnerExecution
{
    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            var specification = JsonSerializer.Deserialize<C3DHeightImageAlignmentRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                options) ?? throw new InvalidDataException("Height-image alignment Runner specification is empty.");

            var reference = LoadSource(specification.Reference, "reference");
            var moving = LoadSource(specification.Moving, "moving");
            var selection = new ToolRecipeGridRectangle(
                specification.TemplateRow,
                specification.TemplateColumn,
                specification.TemplateRowCount,
                specification.TemplateColumnCount);
            var evaluation = C3DHeightImageAlignmentAdapter.Evaluate(new C3DHeightImageAlignmentInput(
                specification.StepId,
                reference,
                moving,
                specification.SelectionId,
                selection,
                specification.OutputEntityId,
                specification.Mode,
                specification.SearchScoreMinimum,
                specification.AcceptanceScoreMinimumPercent,
                specification.MinimumCandidateMarginPercent,
                specification.AngleMinimumDegrees,
                specification.AngleMaximumDegrees,
                specification.AngleStepDegrees,
                specification.RansacReprojectionThreshold));
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                throw new InvalidDataException($"Height-image alignment Runner failed: {evaluation.Result.Message}");
            }

            var output = evaluation.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath, [
                "OpenVisionLab 3D Height Image Alignment Runner report",
                $"Specification|path={fullSpecificationPath}",
                $"Alignment|status={evaluation.Result.Status}|step={output.StepId}|mode={output.Mode}|output={output.OutputEntityId}|sha256={output.ContentSha256}",
                $"Reference|entity={output.ReferenceEntityId}|sha256={output.ReferenceContentSha256}|unit={output.Unit}|frame={output.FrameId}|selection={output.SelectionId}",
                $"Moving|entity={output.MovingEntityId}|sha256={output.MovingContentSha256}",
                $"Pose|translation={output.Pose.TranslationX:R},{output.Pose.TranslationY:R}|rotation={output.Pose.RotationDegrees:R}|scale={output.Pose.Scale:R}|center={output.Pose.CenterX:R},{output.Pose.CenterY:R}|bounds={output.Pose.BoundingX:R},{output.Pose.BoundingY:R},{output.Pose.BoundingWidth:R},{output.Pose.BoundingHeight:R}",
                $"Diagnostics|candidates={output.Diagnostics.CandidateCount}|best={output.Diagnostics.BestScorePercent:R}|second={output.Diagnostics.SecondScorePercent:R}|margin={output.Diagnostics.ScoreMarginPercent:R}|searchMin={output.Diagnostics.SearchScoreMinimum:R}|acceptMinPercent={output.Diagnostics.AcceptanceScoreMinimumPercent:R}|marginMinPercent={output.Diagnostics.MinimumCandidateMarginPercent:R}|angle={output.Diagnostics.AngleMinimumDegrees:R}..{output.Diagnostics.AngleMaximumDegrees:R}/{output.Diagnostics.AngleStepDegrees:R}",
                $"Provenance|{output.Provenance}"
            ]);
            Console.WriteLine($"3D Height Image Alignment Runner: Pass ({output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or OverflowException
                or JsonException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath, [
                "OpenVisionLab 3D Height Image Alignment Runner report",
                $"Error|{exception.Message}"
            ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static C3DHeightFieldSnapshot LoadSource(
        C3DHeightImageAlignmentRunnerSource source,
        string label)
    {
        if (source is null)
        {
            throw new InvalidDataException($"Height-image alignment {label} source is missing.");
        }

        return C3DHeightFieldSnapshot.LoadVerified(
            source.Path,
            source.EntityId,
            source.Unit,
            source.FrameId,
            source.ByteLength,
            source.ContentSha256,
            source.Width,
            source.Height);
    }
}

internal sealed class C3DHeightImageAlignmentRunnerSpecification
{
    public string StepId { get; set; } = "";
    public string SelectionId { get; set; } = "";
    public string OutputEntityId { get; set; } = "";
    public C3DHeightImageAlignmentMode Mode { get; set; }
    public C3DHeightImageAlignmentRunnerSource Reference { get; set; } = new();
    public C3DHeightImageAlignmentRunnerSource Moving { get; set; } = new();
    public int TemplateRow { get; set; }
    public int TemplateColumn { get; set; }
    public int TemplateRowCount { get; set; }
    public int TemplateColumnCount { get; set; }
    public double SearchScoreMinimum { get; set; }
    public double AcceptanceScoreMinimumPercent { get; set; }
    public double MinimumCandidateMarginPercent { get; set; }
    public int AngleMinimumDegrees { get; set; }
    public int AngleMaximumDegrees { get; set; }
    public double AngleStepDegrees { get; set; }
    public double RansacReprojectionThreshold { get; set; } = 3d;
}

internal sealed class C3DHeightImageAlignmentRunnerSource
{
    public string Path { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Unit { get; set; } = "";
    public string FrameId { get; set; } = "";
    public long ByteLength { get; set; }
    public string ContentSha256 { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}
