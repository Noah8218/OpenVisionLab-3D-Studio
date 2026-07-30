using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeLabeledValidationRunnerExecution
{
    public static int Run(string recipePath, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var definition =
                ToolRecipeValidationSetDefinitionStore.LoadForRecipe(
                    fullRecipePath)
                ?? throw new InvalidDataException(
                    "The recipe has no Validation Set role manifest. Save the labeled set in Workbench first.");
            if (!string.Equals(
                    definition.RecipeSourceSha256,
                    document.Source.ContentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The Validation Set role manifest belongs to a different recipe source identity.");
            }

            var execution = ToolRecipeValidationSetExecution.Execute(
                document,
                definition.Samples
                    .OrderBy(sample => sample.Order)
                    .Select(sample => new ToolRecipeValidationSampleInput(
                        sample.SourcePath,
                        sample.Role))
                    .ToArray());
            var evidence =
                ToolRecipeLabeledEvidenceAnalyzer.Analyze(document, execution);
            var thresholdCandidates =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    execution);
            var report = new
            {
                schemaVersion = "1.1",
                contractVersion = evidence.ContractVersion,
                recipe = new
                {
                    path = fullRecipePath,
                    document.SchemaVersion,
                    document.Name,
                    sourceSha256 = document.Source.ContentSha256
                },
                validationSet = new
                {
                    manifestPath =
                        ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(
                            fullRecipePath),
                    definition.SchemaVersion,
                    definition.RecipeSourceSha256,
                    sampleCount = definition.Samples.Count
                },
                execution,
                evidence,
                thresholdCandidates,
                thresholdMappings =
                    ToolRecipeThresholdCandidateParameterMapper
                        .SupportedMappings
            };
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Converters = { new JsonStringEnumConverter() },
                        WriteIndented = true
                    }));

            Console.WriteLine(evidence.Message);
            Console.WriteLine(thresholdCandidates.Message);
            Console.WriteLine($"Labeled evidence report: {fullReportPath}");
            return execution.Status == ResultStatus.Error ? 1 : 0;
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
            Console.Error.WriteLine(
                $"Labeled Validation Runner failed: {exception.Message}");
            return 1;
        }
    }
}
