using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class AlignedPointRepeatabilityStudyExecution
{
    public static int Run(string studyPath, string reportPath)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(studyPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

            var loaded = AlignedPointRepeatabilityStudyLoader.Load(studyPath);
            var evaluation = AlignedPointRepeatabilityRule.Evaluate(loaded.Input);
            WriteEvaluationReport(reportPath, loaded, evaluation);
            Console.WriteLine($"Aligned point repeatability study: {evaluation.Result.Status}");
            return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException
                                          or JsonException
                                          or ArgumentException
                                          or OverflowException)
        {
            TryWriteErrorReport(reportPath, studyPath, exception);
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void WriteEvaluationReport(
        string reportPath,
        LoadedAlignedPointRepeatabilityStudy loaded,
        AlignedPointRepeatabilityEvaluation evaluation)
    {
        var input = loaded.Input;
        var acceptance = input.Acceptance;
        var lines = new List<string>
        {
            $"AlignedPointRepeatabilityStudyRun|{evaluation.Result.Status}|decision={evaluation.Decision}|runs={evaluation.RunCount}|correspondences={evaluation.CorrespondenceCount}|failing={evaluation.FailingCorrespondenceCount}",
            $"Study|path={Clean(loaded.Study.Path)}|bytes={loaded.Study.ByteLength}|sha256={loaded.Study.Sha256}|id={Clean(input.StudyId)}|measurementDefinition={Clean(input.MeasurementDefinitionId)}|referenceRoi={Clean(input.ReferenceRoiId)}|unit={Clean(input.Unit)}|frame={Clean(input.FrameId)}|alignmentReference={Clean(input.AlignmentReferenceId)}|correspondenceDefinition={Clean(input.CorrespondenceDefinitionId)}",
            acceptance is null
                ? "Acceptance|configured=False"
                : $"Acceptance|configured=True|minimumRuns={acceptance.MinimumRunCount}|minimumCorrespondences={acceptance.MinimumCorrespondenceCount}|maximumSampleStandardDeviation={Format(acceptance.MaximumSampleStandardDeviation)}|maximumRange={Format(acceptance.MaximumRange)}|unit={Clean(input.Unit)}",
            "SourceEvidence"
        };

        lines.AddRange(loaded.Sources.Select(source =>
            $"Source|run={Clean(source.RunId)}|entity={Clean(source.SourceEntityId)}|name={Clean(source.Name)}|path={Clean(source.Path)}|bytes={source.ByteLength}|sha256={source.Sha256}"));
        lines.Add("MappingEvidence");
        lines.AddRange(loaded.Mappings.Select(mapping =>
            $"Mapping|run={Clean(mapping.RunId)}|entity={Clean(mapping.SourceEntityId)}|name={Clean(mapping.Name)}|path={Clean(mapping.Path)}|bytes={mapping.ByteLength}|sha256={mapping.Sha256}|alignmentMethod={Clean(mapping.AlignmentMethodId)}|alignmentEvidence={Clean(mapping.AlignmentEvidenceId)}"));
        lines.Add(InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true));
        lines.Add(InspectionContractText.MetricsMarker);
        lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add("PointEvaluations");
        lines.AddRange(evaluation.PointEvaluations.Select(point =>
            $"Point|id={Clean(point.ReferencePoint.CorrespondenceId)}|alignedX={Format(point.ReferencePoint.AlignedX)}|alignedY={Format(point.ReferencePoint.AlignedY)}|alignedZ={Format(point.ReferencePoint.AlignedZ)}|runs={point.RunCount}|mean={Format(point.Mean)}|minimum={Format(point.Minimum)}|maximum={Format(point.Maximum)}|sampleStandardDeviation={Format(point.SampleStandardDeviation)}|sixSigmaSpread={Format(point.SixSigmaSpread)}|range={Format(point.Range)}|status={point.Status}|sampleStandardDeviationPassed={point.SampleStandardDeviationPassed}|rangePassed={point.RangePassed}"));
        lines.Add("ClaimBoundary|physicalCalibration=False|gaugeRr=False|mappingDerivationVerified=False|viewerLinkedSelection=False");

        WriteLines(reportPath, lines);
    }

    private static void TryWriteErrorReport(string reportPath, string studyPath, Exception exception)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                return;
            }

            WriteLines(reportPath,
            [
                $"AlignedPointRepeatabilityStudyRun|Error|study={Clean(studyPath)}",
                $"Error|type={exception.GetType().Name}|message={Clean(exception.Message)}",
                "ClaimBoundary|physicalCalibration=False|gaugeRr=False|mappingDerivationVerified=False|viewerLinkedSelection=False"
            ]);
        }
        catch (Exception writeException) when (writeException is IOException
                                                or UnauthorizedAccessException
                                                or ArgumentException)
        {
            Console.Error.WriteLine($"Could not write aligned point repeatability error report: {writeException.Message}");
        }
    }

    private static void WriteLines(string reportPath, IReadOnlyList<string> lines)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
    }

    private static string Format(double value) =>
        double.IsFinite(value)
            ? value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)
            : "(pending)";

    private static string Clean(string? value) =>
        InspectionContractText.Clean(value ?? InspectionContractText.Missing);
}
