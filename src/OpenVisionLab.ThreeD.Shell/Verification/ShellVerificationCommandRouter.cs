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
        ("--verify-recipe-manager-wpg", RecipeManagerWpgVerification.Verify),
        ("--verify-height-measurement-cancellation", HeightMeasurementCancellationVerification.Verify),
        ("--verify-height-measurement-snapshot", HeightMeasurementSnapshotVerification.Verify),
        ("--verify-source-load-cancellation", ShellSourceLoadCancellationVerification.Verify),
        ("--verify-source-reload", ShellSourceReloadVerification.Verify),
        ("--verify-tool-recipe-selections", ToolRecipeSelectionContractVerification.Verify),
        ("--verify-workbench-docking", ToolWorkbenchDockingVerification.Verify),
        ("--verify-viewer-workspace-presentation", ViewerWorkspacePresentationVerification.Verify),
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
