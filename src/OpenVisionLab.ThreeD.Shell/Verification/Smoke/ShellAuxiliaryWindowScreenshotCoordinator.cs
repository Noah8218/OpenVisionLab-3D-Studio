namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed record ShellAuxiliaryWindowScreenshotRequest
{
    public string? ViewerPopoutScreenshotPath { get; init; }
    public string? ViewerPopoutQualityReportPath { get; init; }
    public string? RecipeManagerScreenshotPath { get; init; }
    public string? RecipeManagerQualityReportPath { get; init; }
    public bool FirstRecipeCreatePressed { get; init; }
    public string? MessageDialogScreenshotPath { get; init; }
    public string? MessageDialogQualityReportPath { get; init; }
    public bool MessageDialogPrimaryPressed { get; init; }
}

internal sealed class ShellAuxiliaryWindowScreenshotCallbacks
{
    public required Func<string, string?, Task<bool>> CaptureViewerPopout { get; init; }
    public required Func<string, string?, bool, Task<bool>> CaptureRecipeManager { get; init; }
    public required Action<string?> AppendRecipeManagerMonitorEvidence { get; init; }
    public required Func<string, string?, bool, Task<bool>> CaptureMessageDialog { get; init; }
}

/// <summary>
/// Owns the order and failure policy for optional auxiliary Shell Smoke
/// screenshots. Concrete Window lookup, WPF capture, and monitor inspection are
/// supplied by MainWindow callbacks.
/// </summary>
internal sealed class ShellAuxiliaryWindowScreenshotCoordinator
{
    private readonly ShellAuxiliaryWindowScreenshotCallbacks callbacks;

    public ShellAuxiliaryWindowScreenshotCoordinator(
        ShellAuxiliaryWindowScreenshotCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public async Task<string?> CaptureAsync(
        ShellAuxiliaryWindowScreenshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ViewerPopoutScreenshotPath is { } viewerPopoutPath
            && !await callbacks.CaptureViewerPopout(
                viewerPopoutPath,
                request.ViewerPopoutQualityReportPath))
        {
            return "Viewer pop-out screenshot remained unavailable, blank, or invalid after 3 attempts.";
        }

        if (request.RecipeManagerScreenshotPath is { } recipeManagerPath
            && !await callbacks.CaptureRecipeManager(
                recipeManagerPath,
                request.RecipeManagerQualityReportPath,
                request.FirstRecipeCreatePressed))
        {
            return "Recipe Manager screenshot remained blank or invalid after 3 attempts.";
        }

        if (request.RecipeManagerScreenshotPath is not null)
        {
            callbacks.AppendRecipeManagerMonitorEvidence(
                request.RecipeManagerQualityReportPath);
        }

        if (request.MessageDialogScreenshotPath is { } messageDialogPath
            && !await callbacks.CaptureMessageDialog(
                messageDialogPath,
                request.MessageDialogQualityReportPath,
                request.MessageDialogPrimaryPressed))
        {
            return "Message dialog screenshot remained blank or invalid after 3 attempts.";
        }

        return null;
    }
}
