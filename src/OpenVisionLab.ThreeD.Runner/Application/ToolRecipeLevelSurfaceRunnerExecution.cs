using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeLevelSurfaceRunnerExecution
{
    public static int Run(string recipePath, string stepId, string outputC3DPath, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var evaluation = ToolRecipeLevelSurfaceExecution.Execute(
                document, stepId, Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.Transform is null
                || evaluation.LevelFrame is null
                || evaluation.FrameChain is null)
            {
                throw new InvalidDataException($"Runner Level Surface failed: {evaluation.Result.Message}");
            }
            evaluation.Output.SaveC3D(outputC3DPath);
            var report = new
            {
                schemaVersion = "1.0",
                recipe = new { path = fullRecipePath, document.SchemaVersion, document.Name },
                step = new { id = stepId, toolId = "level-surface", status = evaluation.Result.Status.ToString() },
                output = new
                {
                    id = evaluation.Output.EntityId,
                    path = Path.GetFullPath(outputC3DPath),
                    evaluation.Output.ByteLength,
                    evaluation.Output.ContentSha256,
                    evaluation.Output.RootSourceSha256,
                    evaluation.Output.Width,
                    evaluation.Output.Height,
                    evaluation.Output.ValidCount,
                    evaluation.Output.MissingCount,
                    evaluation.Output.Minimum,
                    evaluation.Output.Maximum,
                    evaluation.Output.Mean,
                    evaluation.Output.Provenance
                },
                transform = new
                {
                    contractVersion = C3DLevelingTransform.ContractVersion,
                    id = evaluation.Transform.OutputEntityId,
                    evaluation.Transform.ContentSha256,
                    evaluation.Transform.RootSourceEntityId,
                    evaluation.Transform.RootSourceSha256,
                    evaluation.Transform.SourceUnit,
                    evaluation.Transform.SourceFrameId,
                    evaluation.Transform.ReferenceSampleCount,
                    evaluation.Transform.ReferenceResidualRms,
                    evaluation.Transform.ReferenceResidualPeakToValley,
                    evaluation.Transform.FittedSlopeX,
                    evaluation.Transform.FittedSlopeZ,
                    evaluation.Transform.FittedIntercept,
                    evaluation.Transform.TargetHeight,
                    matrix = evaluation.Transform.Matrix.Values,
                    referenceRegions = evaluation.Transform.ReferenceRegions
                },
                levelFrame = new
                {
                    contractVersion = C3DLevelFrameArtifact.ContractVersion,
                    id = evaluation.LevelFrame.OutputEntityId,
                    frameId = evaluation.LevelFrame.LevelFrameId,
                    evaluation.LevelFrame.ContentSha256,
                    evaluation.LevelFrame.RootSourceEntityId,
                    evaluation.LevelFrame.RootSourceSha256,
                    evaluation.LevelFrame.SourceUnit,
                    evaluation.LevelFrame.SourceFrameId,
                    evaluation.LevelFrame.LevelingTransformEntityId,
                    evaluation.LevelFrame.LevelingTransformContentSha256,
                    evaluation.LevelFrame.FittedSlopeX,
                    evaluation.LevelFrame.FittedSlopeZ,
                    evaluation.LevelFrame.FittedIntercept,
                    evaluation.LevelFrame.TargetHeight,
                    evaluation.LevelFrame.ReferenceSampleCount,
                    evaluation.LevelFrame.ReferenceResidualRms,
                    evaluation.LevelFrame.ReferenceResidualPeakToValley,
                    origin = new
                    {
                        evaluation.LevelFrame.Origin.X,
                        evaluation.LevelFrame.Origin.Y,
                        evaluation.LevelFrame.Origin.Z
                    },
                    uAxis = new
                    {
                        evaluation.LevelFrame.UAxis.X,
                        evaluation.LevelFrame.UAxis.Y,
                        evaluation.LevelFrame.UAxis.Z
                    },
                    vAxis = new
                    {
                        evaluation.LevelFrame.VAxis.X,
                        evaluation.LevelFrame.VAxis.Y,
                        evaluation.LevelFrame.VAxis.Z
                    },
                    hAxis = new
                    {
                        evaluation.LevelFrame.HAxis.X,
                        evaluation.LevelFrame.HAxis.Y,
                        evaluation.LevelFrame.HAxis.Z
                    },
                    matrix = evaluation.LevelFrame.SourceToFrame.Values,
                    referenceRegions = evaluation.LevelFrame.ReferenceRegions,
                    framePolicy = C3DLevelFrameArtifact.FramePolicy,
                    axisConvention = C3DLevelFrameArtifact.AxisConvention,
                    originPolicy = C3DLevelFrameArtifact.OriginPolicy
                },
                qualityEvidence = evaluation.QualityEvidence,
                frameChain = evaluation.FrameChain,
                result = evaluation.Result,
                outputReferenceSlopeX = evaluation.OutputReferenceSlopeX,
                outputReferenceSlopeZ = evaluation.OutputReferenceSlopeZ
            };
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(fullReportPath, JsonSerializer.Serialize(
                report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Leveled output: {Path.GetFullPath(outputC3DPath)}");
            Console.WriteLine($"Output SHA-256: {evaluation.Output.ContentSha256}");
            Console.WriteLine($"Leveling transform SHA-256: {evaluation.Transform.ContentSha256}");
            Console.WriteLine($"Frame chain SHA-256: {evaluation.FrameChain.ContentSha256}");
            return 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or OverflowException)
        {
            Console.Error.WriteLine($"Level Surface Runner failed: {exception.Message}");
            return 1;
        }
    }
}
