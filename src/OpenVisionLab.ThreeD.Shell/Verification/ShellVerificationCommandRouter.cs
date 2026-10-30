using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Viewer.Verification;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellVerificationCommandRouter
{
    private delegate bool ReportVerifier(string reportPath, out string summary);

    // This order and Shell's blank-path pass-through are existing CLI contracts.
    // UI verification stays in this assembly; add registrations without moving suite owners.
    private static readonly (string Option, ReportVerifier Verify)[] Commands =
    [
        ("--verify-source-quality-workspace", SourceQualityWorkspaceVerification.Verify),
        ("--verify-path-input", PathInputVerification.Verify),
        ("--verify-recipe-manager-wpg", RecipeManagerWpgVerification.Verify),
        ("--verify-settings-field-presentation", RecipeManagerWpgVerification.VerifyFieldPresentation),
        ("--verify-height-measurement-cancellation", HeightMeasurementCancellationVerification.Verify),
        ("--verify-height-measurement-snapshot", HeightMeasurementSnapshotVerification.Verify),
        ("--verify-source-load-cancellation", ShellSourceLoadCancellationVerification.Verify),
        ("--verify-source-reload", ShellSourceReloadVerification.Verify),
        ("--verify-opened-recipe-source", ShellSourceReloadVerification.VerifyOpenedRecipeSource),
        ("--verify-tool-recipe-selections", ToolRecipeSelectionContractVerification.Verify),
        ("--verify-workbench-docking", ToolWorkbenchDockingVerification.Verify),
        ("--verify-modern-tool-settings", ToolWorkbenchDockingVerification.VerifyModernToolSettings),
        ("--verify-tool-settings-layout", ToolSettingsLayoutVerification.Verify),
        ("--verify-kernel-settings-restoration", ToolWorkbenchDockingVerification.VerifyKernelSettingsRestoration),
        ("--verify-kernel-editing-lifecycle", ToolWorkbenchDockingVerification.VerifyKernelEditingLifecycle),
        ("--verify-numeric-display", NumericDisplayVerification.Verify),
        ("--verify-c3d-texture", C3DTexturePresentationVerification.Verify),
        ("--verify-c3d-texture-popup", C3DTexturePresentationVerification.VerifyPopup),
        ("--verify-texture-selection-identity", C3DTexturePresentationVerification.VerifySelectionIdentity),
        ("--verify-texture-layout-reattach", C3DTexturePresentationVerification.VerifyLayoutReattach),
        ("--verify-number-summary-display", NumericDisplayVerification.VerifyReadOnlyFormatting),
        ("--verify-measurement-number-text", NumericDisplayVerification.VerifyMeasurementText),
        ("--verify-selected-tool-input-choice", SelectedToolInputChoiceVerification.Verify),
        ("--verify-render-context-option", ShellStartupVerification.VerifyRenderContextOption),
        ("--verify-workbench-language", ToolWorkbenchDockingVerification.VerifyLanguageSwitch),
        ("--verify-workbench-control-states", ToolWorkbenchDockingVerification.VerifyControlStates),
        ("--verify-layout-fixture-bounds", ToolWorkbenchDockingVerification.VerifyLayoutFixtureBounds),
        ("--verify-teaching-activity-layout", ToolWorkbenchDockingVerification.VerifyActivityLayout),
        ("--verify-recipe-flow", ToolWorkbenchDockingVerification.VerifyRecipeFlow),
        ("--verify-common-header", ToolWorkbenchDockingVerification.VerifyCommonHeader),
        ("--verify-common-header-layout", ToolWorkbenchDockingVerification.VerifyCommonHeaderLayout),
        ("--verify-inspection-execution-bar", ToolWorkbenchDockingVerification.VerifyInspectionExecutionBar),
        ("--verify-viewer-workspace-presentation", ViewerWorkspacePresentationVerification.Verify),
        ("--verify-viewer-pane-restoration", ViewerWorkspacePresentationVerification.VerifyPaneRestoration),
        ("--verify-height-legend-keyboard", ViewerWorkspacePresentationVerification.VerifyLegendKeyboard),
        ("--verify-legend-input-focus", ViewerWorkspacePresentationVerification.VerifyLegendInputFocus),
        ("--verify-profile-footer", ViewerWorkspacePresentationVerification.VerifyProfileFooter),
        ("--verify-common-state-keyboard-accessibility", CommonStateKeyboardAccessibilityVerification.Verify),
        ("--verify-c3d-height-profile", C3DHeightProfileVerification.Verify),
        ("--verify-c3d-height-distribution", C3DHeightDistributionVerification.Verify),
    ];

    public static bool IsVerificationRequest(string[] args) =>
        args.Any(argument => argument.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase));

    public static void Run(string[] args, Action<int> shutdownApplication)
    {
        foreach (var command in Commands)
        {
            var index = Array.FindIndex(args, argument => argument.Equals(command.Option, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                Console.WriteLine($"{command.Option} requires a report path.");
                shutdownApplication(2);
                return;
            }

            var passed = command.Verify(args[index + 1], out var summary);
            Console.WriteLine(summary);
            shutdownApplication(passed ? 0 : 1);
            return;
        }

        Console.WriteLine($"Unsupported Shell verification option: {string.Join(' ', args)}");
        shutdownApplication(2);
    }
}
