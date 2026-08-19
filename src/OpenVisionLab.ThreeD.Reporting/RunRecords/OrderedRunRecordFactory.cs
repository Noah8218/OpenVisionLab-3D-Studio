using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Reporting.RunRecords;

public static class OrderedRunRecordFactory
{
    public static InspectionRunRecord Create(
        OrderedRunRecordIdentity identity,
        ToolRecipeDocument document,
        ToolRecipeOrderedGraphExecutionResult execution,
        string viewerRunnerMatchState,
        InspectionRunArtifacts artifacts,
        InspectionRunEnvironment? executionEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(execution);

        var steps = ToolRecipeOrderedGraphRunRecordProjection.Create(
            document,
            execution);
        var source = new InspectionRunSource(
            document.Source.Id,
            identity.SourcePath,
            identity.SourceSha256,
            identity.SourceByteLength,
            document.Source.Unit);
        return new InspectionRunRecord(
            "1.9",
            identity.RunId,
            identity.RecordedAtUtc,
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                identity.RecipePath,
                identity.RecipeSha256),
            source,
            "Ordered Tool Recipe Replay",
            execution.Status,
            execution.Message,
            execution.Duration.TotalMilliseconds,
            steps.SelectMany(step => step.Metrics).ToArray(),
            steps.SelectMany(step => step.Overlays).ToArray(),
            viewerRunnerMatchState,
            artifacts)
        {
            ExecutionEnvironment = executionEnvironment,
            Steps = steps,
            SourceQualityEvidence = execution.SourceQuality is null
                ? InspectionRunSourceQualityEvidence.Unavailable(
                    "Source Quality was unavailable because the ordered source could not be analyzed.")
                : InspectionRunSourceQualityEvidence.Available(
                    source,
                    execution.SourceQuality),
            ThresholdCorrectionEvidence =
                ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                    identity.RecipePath,
                    document)
        };
    }
}
