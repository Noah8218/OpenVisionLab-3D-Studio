using System.IO;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// View-free post-capture evidence policy for the Shell Smoke route.
/// Visual-tree inspection and monitor/DPI measurement stay behind explicit
/// callbacks owned by MainWindow; this type only orders optional report lines.
/// </summary>
internal sealed class ShellSmokeScreenshotEvidenceCallbacks
{
    public required Action<string?> AppendWindowMonitorEvidence { get; init; }
    public required Action<string> AppendValidationThresholdEvidence { get; init; }
    public required Action<string, string?> AppendPreparationPresetEvidence { get; init; }
}

internal sealed record ShellSmokeScreenshotEvidenceRequest
{
    public string? QualityReportPath { get; init; }
    public string? ViewerPresentationCameraLinkSummary { get; init; }
    public bool AppendValidationThresholdEvidence { get; init; }
    public string? IntegrationExchangeEvidenceLine { get; init; }
    public string? PreparationPresetAssistantMode { get; init; }
}

internal sealed class ShellSmokeScreenshotEvidenceCoordinator
{
    private readonly ShellSmokeScreenshotEvidenceCallbacks callbacks;
    private readonly Action<string, IEnumerable<string>> appendLines;

    public ShellSmokeScreenshotEvidenceCoordinator(
        ShellSmokeScreenshotEvidenceCallbacks callbacks,
        Action<string, IEnumerable<string>> appendLines)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        this.appendLines = appendLines ?? throw new ArgumentNullException(nameof(appendLines));
    }

    public void Append(ShellSmokeScreenshotEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reportPath = request.QualityReportPath;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            if (request.ViewerPresentationCameraLinkSummary is { } cameraSummary)
            {
                AppendLine(reportPath, cameraSummary);
            }
        }

        callbacks.AppendWindowMonitorEvidence(reportPath);
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            if (request.AppendValidationThresholdEvidence)
            {
                callbacks.AppendValidationThresholdEvidence(reportPath);
            }

            if (request.IntegrationExchangeEvidenceLine is { } integrationEvidence)
            {
                AppendLine(reportPath, integrationEvidence);
            }
        }

        if (request.PreparationPresetAssistantMode is { } preparationState)
        {
            callbacks.AppendPreparationPresetEvidence(
                preparationState,
                request.QualityReportPath);
        }
    }

    private void AppendLine(string reportPath, string line) =>
        appendLines(Path.GetFullPath(reportPath), [line]);
}
