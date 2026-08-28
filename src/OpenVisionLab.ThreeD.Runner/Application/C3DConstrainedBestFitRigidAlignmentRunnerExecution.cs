using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DConstrainedBestFitRigidAlignmentRunnerExecution
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
            var specification = JsonSerializer.Deserialize<C3DConstrainedBestFitRigidAlignmentRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath), options)
                ?? throw new InvalidDataException("Constrained best-fit rigid alignment Runner specification is empty.");
            var pairs = specification.Pairs?
                .Select(pair => new C3DConstrainedBestFitRigidAlignmentPair(
                    pair.SourcePointId,
                    pair.ReferencePointId,
                    pair.SourceX,
                    pair.SourceY,
                    pair.SourceZ,
                    pair.ReferenceX,
                    pair.ReferenceY,
                    pair.ReferenceZ))
                .ToArray()
                ?? throw new InvalidDataException("Constrained best-fit rigid alignment Runner specification has no pairs.");
            var evaluation = C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(
                new C3DConstrainedBestFitRigidAlignmentInput(
                    specification.StepId,
                    specification.OutputEntityId,
                    specification.SourceEntityId,
                    specification.SourceContentSha256,
                    specification.ReferenceEntityId,
                    specification.ReferenceContentSha256,
                    specification.SourceUnit,
                    specification.SourceFrameId,
                    specification.ReferenceUnit,
                    specification.ReferenceFrameId,
                    pairs,
                    specification.MaximumCorrespondenceCount,
                    specification.MinimumNormalizedLineSpread,
                    specification.ArithmeticResidualWarning));
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                throw new InvalidDataException($"Constrained best-fit rigid alignment Runner failed: {evaluation.Result.Message}");
            }

            var output = evaluation.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath,
            [
                "OpenVisionLab 3D Constrained Best-Fit Rigid Alignment Runner report",
                $"Specification|path={fullSpecificationPath}",
                $"Alignment|status={evaluation.Result.Status}|step={output.StepId}|output={output.OutputEntityId}|sha256={output.ContentSha256}",
                $"Source|entity={output.SourceEntityId}|sha256={output.SourceContentSha256}|unit={output.SourceUnit}|frame={output.SourceFrameId}",
                $"Reference|entity={output.ReferenceEntityId}|sha256={output.ReferenceContentSha256}|unit={output.ReferenceUnit}|frame={output.ReferenceFrameId}",
                $"Pose|matrix={string.Join(',', output.Pose.Values.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}",
                $"Diagnostics|pairs={output.Pairs.Count}|usedAll={output.UsedAllCorrespondences}|sourceSpread={output.SourceNormalizedLineSpread:R}|referenceSpread={output.ReferenceNormalizedLineSpread:R}|rmsResidual={output.RmsResidual:R}|maxResidual={output.MaximumResidual:R}|warning={output.ArithmeticResidualWarningExceeded}|countPolicy={output.CorrespondenceCountPolicy}|posePolicy={output.PoseConstraintPolicy}",
                $"Provenance|{output.Provenance}"
            ]);
            Console.WriteLine($"3D Constrained Best-Fit Rigid Alignment Runner: Pass ({output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException
                or OverflowException
                or JsonException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath,
            [
                "OpenVisionLab 3D Constrained Best-Fit Rigid Alignment Runner report",
                $"Error|{exception.Message}"
            ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}

internal sealed class C3DConstrainedBestFitRigidAlignmentRunnerSpecification
{
    public string StepId { get; set; } = "";
    public string OutputEntityId { get; set; } = "";
    public string SourceEntityId { get; set; } = "";
    public string SourceContentSha256 { get; set; } = "";
    public string ReferenceEntityId { get; set; } = "";
    public string ReferenceContentSha256 { get; set; } = "";
    public string SourceUnit { get; set; } = "";
    public string SourceFrameId { get; set; } = "";
    public string ReferenceUnit { get; set; } = "";
    public string ReferenceFrameId { get; set; } = "";
    public int MaximumCorrespondenceCount { get; set; }
    public double MinimumNormalizedLineSpread { get; set; }
    public double ArithmeticResidualWarning { get; set; }
    public List<C3DConstrainedBestFitRigidAlignmentRunnerPair> Pairs { get; set; } = [];
}

internal sealed class C3DConstrainedBestFitRigidAlignmentRunnerPair
{
    public string SourcePointId { get; set; } = "";
    public string ReferencePointId { get; set; } = "";
    public double SourceX { get; set; }
    public double SourceY { get; set; }
    public double SourceZ { get; set; }
    public double ReferenceX { get; set; }
    public double ReferenceY { get; set; }
    public double ReferenceZ { get; set; }
}
