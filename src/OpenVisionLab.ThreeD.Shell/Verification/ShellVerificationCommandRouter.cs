using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellVerificationCommandRouter
{
    public static bool IsVerificationRequest(string[] args) =>
        args.Any(argument => argument.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase));

    public static void Run(string[] args)
    {
        const string sourceQualityWorkspaceVerificationOption =
            "--verify-source-quality-workspace";
        var sourceQualityWorkspaceVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                sourceQualityWorkspaceVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (sourceQualityWorkspaceVerificationIndex >= 0)
        {
            if (sourceQualityWorkspaceVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{sourceQualityWorkspaceVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = SourceQualityWorkspaceVerification.Verify(
                args[sourceQualityWorkspaceVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string toolRecipeSelectionsVerificationOption = "--verify-tool-recipe-selections";
        const string workbenchDockingVerificationOption = "--verify-workbench-docking";
        const string c3dHeightProfileVerificationOption = "--verify-c3d-height-profile";
        const string c3dHeightDistributionVerificationOption = "--verify-c3d-height-distribution";
        const string recipeManagerWpgVerificationOption = "--verify-recipe-manager-wpg";
        const string viewerWorkspacePresentationVerificationOption =
            "--verify-viewer-workspace-presentation";
        var recipeManagerWpgVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(recipeManagerWpgVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (recipeManagerWpgVerificationIndex >= 0)
        {
            if (recipeManagerWpgVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{recipeManagerWpgVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = RecipeManagerWpgVerification.Verify(
                args[recipeManagerWpgVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var toolRecipeSelectionsVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(toolRecipeSelectionsVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (toolRecipeSelectionsVerificationIndex >= 0)
        {
            if (toolRecipeSelectionsVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{toolRecipeSelectionsVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolRecipeSelectionContractVerification.Verify(
                args[toolRecipeSelectionsVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var workbenchDockingVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(workbenchDockingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (workbenchDockingVerificationIndex >= 0)
        {
            if (workbenchDockingVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{workbenchDockingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolWorkbenchDockingVerification.Verify(
                args[workbenchDockingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var viewerWorkspacePresentationVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                viewerWorkspacePresentationVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (viewerWorkspacePresentationVerificationIndex >= 0)
        {
            if (viewerWorkspacePresentationVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{viewerWorkspacePresentationVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ViewerWorkspacePresentationVerification.Verify(
                args[viewerWorkspacePresentationVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string commonStateKeyboardAccessibilityVerificationOption =
            "--verify-common-state-keyboard-accessibility";
        var commonStateKeyboardAccessibilityVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                commonStateKeyboardAccessibilityVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (commonStateKeyboardAccessibilityVerificationIndex >= 0)
        {
            if (commonStateKeyboardAccessibilityVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{commonStateKeyboardAccessibilityVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = CommonStateKeyboardAccessibilityVerification.Verify(
                args[commonStateKeyboardAccessibilityVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var c3dHeightProfileVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(c3dHeightProfileVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (c3dHeightProfileVerificationIndex >= 0)
        {
            if (c3dHeightProfileVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{c3dHeightProfileVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = C3DHeightProfileVerification.Verify(
                args[c3dHeightProfileVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var c3dHeightDistributionVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(c3dHeightDistributionVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (c3dHeightDistributionVerificationIndex >= 0)
        {
            if (c3dHeightDistributionVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{c3dHeightDistributionVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = C3DHeightDistributionVerification.Verify(
                args[c3dHeightDistributionVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        Console.WriteLine($"Unsupported Shell verification option: {string.Join(' ', args)}");
        Shutdown(2);
    }

    private static void Shutdown(int exitCode) =>
        System.Windows.Application.Current.Shutdown(exitCode);
}
