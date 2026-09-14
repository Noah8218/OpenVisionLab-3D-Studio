namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed record ShellSmokeScreenshotCaptureRequest
{
    public string? ScreenshotPath { get; init; }
    public string? QualityReportPath { get; init; }
    public ShellSmokeScreenshotTargetRequest Target { get; init; } = new();
    public string? ViewerPresentationCameraLinkSummary { get; init; }
    public bool AppendValidationThresholdEvidence { get; init; }
    public string? IntegrationExchangeEvidenceLine { get; init; }
    public string? PreparationPresetAssistantMode { get; init; }
}

internal sealed class ShellSmokeScreenshotCaptureCallbacks
{
    public required Func<string, string, string?, string, Task<bool>> CaptureButtonPressed { get; init; }
    public required Func<string, string?, Task<bool>> CaptureRecipeHealthNavigation { get; init; }
    public required Func<string, string?, string, Task<bool>> CaptureWindow { get; init; }
    public required Action<ShellSmokeScreenshotEvidenceRequest> AppendEvidence { get; init; }
}

internal sealed record ShellSmokeScreenshotCaptureResult(bool Succeeded, string? Failure);

/// <summary>
/// Owns Shell Smoke screenshot target dispatch, capture failure policy, and
/// post-capture evidence handoff. Window and visual-tree mechanics remain
/// behind explicit callbacks composed by ShellSmokeScenarioRunner and
/// ShellSmokeArtifacts.
/// </summary>
internal sealed class ShellSmokeScreenshotCaptureCoordinator
{
    private readonly ShellSmokeScreenshotCaptureCallbacks callbacks;

    public ShellSmokeScreenshotCaptureCoordinator(
        ShellSmokeScreenshotCaptureCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public async Task<ShellSmokeScreenshotCaptureResult> CaptureAsync(
        ShellSmokeScreenshotCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ScreenshotPath is null)
        {
            return new ShellSmokeScreenshotCaptureResult(true, null);
        }

        var target = ShellSmokeScreenshotTargetSelector.Select(request.Target);
        var succeeded = target switch
        {
            { Kind: ShellSmokeScreenshotTargetKind.Button } button =>
                await callbacks.CaptureButtonPressed(
                    button.AutomationId!,
                    request.ScreenshotPath,
                    request.QualityReportPath,
                    button.Scope!),
            { Kind: ShellSmokeScreenshotTargetKind.RecipeHealthNavigation } =>
                await callbacks.CaptureRecipeHealthNavigation(
                    request.ScreenshotPath,
                    request.QualityReportPath),
            _ => await callbacks.CaptureWindow(
                request.ScreenshotPath,
                request.QualityReportPath,
                "Shell")
        };
        if (!succeeded)
        {
            return new ShellSmokeScreenshotCaptureResult(
                false,
                "Shell screenshot remained blank or invalid after 3 attempts.");
        }

        callbacks.AppendEvidence(
            new ShellSmokeScreenshotEvidenceRequest
            {
                QualityReportPath = request.QualityReportPath,
                ViewerPresentationCameraLinkSummary = request.ViewerPresentationCameraLinkSummary,
                AppendValidationThresholdEvidence = request.AppendValidationThresholdEvidence,
                IntegrationExchangeEvidenceLine = request.IntegrationExchangeEvidenceLine,
                PreparationPresetAssistantMode = request.PreparationPresetAssistantMode
            });
        return new ShellSmokeScreenshotCaptureResult(true, null);
    }
}
