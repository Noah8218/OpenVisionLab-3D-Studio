namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal enum ShellSmokeScreenshotTargetKind
{
    Window,
    Button,
    RecipeHealthNavigation
}

internal sealed record ShellSmokeScreenshotTarget(
    ShellSmokeScreenshotTargetKind Kind,
    string? AutomationId,
    string? Scope);

/// <summary>
/// Immutable command-line Smoke screenshot target policy. The selector knows
/// precedence only; Window lookup, pointer/focus, and capture remain in the
/// WPF View.
/// </summary>
internal sealed record ShellSmokeScreenshotTargetRequest
{
    public bool Import3DDataPressed { get; init; }
    public bool ValidationThresholdAssistantPressed { get; init; }
    public bool ViewerToolbarPressed { get; init; }
    public bool ViewerPresentationPressed { get; init; }
    public bool RecipeHealthNavigationPressed { get; init; }
    public bool SupportBundlePressed { get; init; }
    public bool CurrentRecipeRunPressed { get; init; }
    public bool IntegrationExchangePressed { get; init; }
    public string? IntegrationExchangeAutomationId { get; init; }
    public string? IntegrationExchangeScope { get; init; }
    public string? PreparationPresetAssistantMode { get; init; }
}

internal static class ShellSmokeScreenshotTargetSelector
{
    public static ShellSmokeScreenshotTarget? Select(
        ShellSmokeScreenshotTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Import3DDataPressed)
        {
            return Button("Import3DData", "Import3DDataPressed");
        }
        if (request.ValidationThresholdAssistantPressed)
        {
            return Button(
                "ProposeValidationThresholdButton",
                "ValidationThresholdAssistantProposePressed");
        }
        if (request.ViewerToolbarPressed)
        {
            return Button("ViewerFitAll", "ViewerToolbarPressed");
        }
        if (request.ViewerPresentationPressed)
        {
            return Button(
                "ViewerCameraLink",
                "ViewerPresentationCameraLinkPressed");
        }
        if (request.RecipeHealthNavigationPressed)
        {
            return new(
                ShellSmokeScreenshotTargetKind.RecipeHealthNavigation,
                null,
                "RecipeHealthNavigationPressed");
        }
        if (request.SupportBundlePressed)
        {
            return Button(
                "PrivacySafeSupportBundleButton",
                "PrivacySafeSupportBundlePressed");
        }
        if (request.CurrentRecipeRunPressed)
        {
            return Button("RunCurrentRecipeButton", "CurrentRecipeRunPressed");
        }
        if (request.IntegrationExchangePressed)
        {
            return new(
                ShellSmokeScreenshotTargetKind.Button,
                request.IntegrationExchangeAutomationId,
                request.IntegrationExchangeScope);
        }
        if (request.PreparationPresetAssistantMode?.Equals(
                "apply-pressed",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return Button(
                "ApplyPreparationPresetDraft",
                "PreparationPresetAssistantApplyDraftPressed");
        }

        return null;
    }

    private static ShellSmokeScreenshotTarget Button(
        string automationId,
        string scope) =>
        new(ShellSmokeScreenshotTargetKind.Button, automationId, scope);
}
