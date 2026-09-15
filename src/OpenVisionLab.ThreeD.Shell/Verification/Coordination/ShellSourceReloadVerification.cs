using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellSourceReloadVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureRoot = Path.Combine(reportDirectory, $"source-reload-{Guid.NewGuid():N}");
        var lines = new List<string>
        {
            "OpenVisionLab 3D same-path C3D reload verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary|Shell admits the existing source only when the current file identity still matches the Viewer binding; a changed or unreadable file is reloaded through the existing Viewer owner.",
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Check(
                "verification-runs-on-sta",
                Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                Thread.CurrentThread.GetApartmentState().ToString());

            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "same-path.c3d");
            var original = CreateSource([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            original.SaveC3D(sourcePath);
            var sameByteLength = new FileInfo(sourcePath).Length;

            using var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var viewModel = new ShellMainWindowViewModel(
                recentRecipesPath: Path.Combine(fixtureRoot, "recent-recipes.json"));
            using var coordinator = new ShellWorkbenchSourceLoadCoordinator(
                new ShellSourceFileDialogService(() => throw new InvalidOperationException("The dialog is not used by this verification.")),
                viewer,
                viewModel,
                new ShellWorkbenchSourceLoadCallbacks
                {
                    ShowLoadSourceFailure = message => lines.Add($"INFO | load-failure-callback | {message}"),
                });

            var initialLoad = coordinator.LoadC3DSource(sourcePath);
            var currentBindingAvailable = viewer.TryGetCurrentC3DSourceBinding(sourcePath, out var initialBinding);
            Check(
                "initial-source-load-establishes-viewer-binding",
                initialLoad && currentBindingAvailable,
                $"loaded={initialLoad};binding={currentBindingAvailable};sha={initialBinding?.ContentSha256 ?? "(none)"}");

            var sameBytesShortcut = coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            Check(
                "same-path-and-same-bytes-uses-existing-binding",
                sameBytesShortcut,
                $"shortcut={sameBytesShortcut};fileLength={new FileInfo(sourcePath).Length}");

            var sameContent = CreateSource([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            sameContent.SaveC3D(sourcePath);
            var sameBytesAfterRewrite = coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            Check(
                "same-path-byte-identical-rewrite-remains-current",
                sameBytesAfterRewrite,
                $"shortcut={sameBytesAfterRewrite};fileLength={new FileInfo(sourcePath).Length};sameLength={new FileInfo(sourcePath).Length == sameByteLength}");

            var changedContent = CreateSource([101, 102, 103, 104, 105, 106, 107, 108, 109]);
            changedContent.SaveC3D(sourcePath);
            var changedFileLength = new FileInfo(sourcePath).Length;
            var changedPathRequiresReload = !coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            Check(
                "same-path-different-content-invalidates-shortcut",
                changedPathRequiresReload && changedFileLength == sameByteLength,
                $"reloadRequired={changedPathRequiresReload};changedLength={changedFileLength};originalLength={sameByteLength}");

            var reloaded = coordinator.LoadC3DSource(sourcePath);
            var reloadedBindingAvailable = viewer.TryGetCurrentC3DSourceBinding(sourcePath, out var reloadedBinding);
            var diskBindingAfterReload = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            Check(
                "changed-source-reloads-through-existing-viewer-owner",
                reloaded
                && reloadedBindingAvailable
                && initialBinding is not null
                && !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(initialBinding, reloadedBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(reloadedBinding, diskBindingAfterReload),
                $"reloaded={reloaded};binding={reloadedBindingAvailable};oldSha={initialBinding?.ContentSha256 ?? "(none)"};newSha={reloadedBinding?.ContentSha256 ?? "(none)"}");

            var retainedBinding = reloadedBinding;
            File.WriteAllBytes(sourcePath, [0, 1, 2]);
            var unreadableSourceRequiresReload = !coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            var failedReload = !coordinator.LoadC3DSource(sourcePath);
            var retainedAfterFailure = viewer.TryGetCurrentC3DSourceBinding(sourcePath, out var bindingAfterFailure)
                && retainedBinding is not null
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(retainedBinding, bindingAfterFailure);
            Check(
                "unreadable-replacement-fails-closed-and-retains-current-viewer-source",
                unreadableSourceRequiresReload && failedReload && retainedAfterFailure,
                $"reloadRequired={unreadableSourceRequiresReload};failedReload={failedReload};retained={retainedAfterFailure};status={viewer.HostState.ViewerStatus}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected | {exception}");
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellSourceReload|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static C3DHeightFieldSnapshot CreateSource(IReadOnlyList<double> values) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            "source.same-path-reload",
            3,
            3,
            values,
            "fixture-unit",
            "frame.raw");
}
