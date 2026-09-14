using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchFirstRecipeEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)!;
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench first-recipe event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: property/create/browse event routing and disposal"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var root = Path.Combine(
            reportDirectory,
            "first-recipe-coordinator");
        var folderPath = Path.Combine(root, "recipes");
        var sourcePath = Path.Combine(root, "source.C3D");
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(sourcePath, string.Empty);

        var owner = new ToolWorkbenchFirstRecipeSetupOwner(
            Path.Combine(root, "recent-recipes.json"),
            () => null,
            () => "recipe",
            () => sourcePath,
            () => null,
            () => Array.Empty<ToolRecipeSelection>(),
            () => "source-1",
            _ => null,
            () => { });
        var propertyChangedCount = 0;
        var createCount = 0;
        var browseFolderCount = 0;
        var browseSourceCount = 0;
        var coordinator = new ToolWorkbenchFirstRecipeEventCoordinator(
            owner,
            _ => propertyChangedCount++,
            () => createCount++,
            () => browseFolderCount++,
            () => browseSourceCount++);

        try
        {
            owner.BeginFirstRecipeSetup();
            owner.FirstRecipeFolderPath = folderPath;
            owner.FirstRecipeSourcePath = sourcePath;
            Check(
                "PropertyChanged reaches the Workbench callback",
                propertyChangedCount > 0,
                $"propertyChangedCount={propertyChangedCount}");

            owner.BrowseFirstRecipeFolderCommand.Execute(null);
            owner.BrowseFirstRecipeSourceCommand.Execute(null);
            Check(
                "Both browse requests reach their callbacks",
                browseFolderCount == 1 && browseSourceCount == 1,
                $"folder={browseFolderCount}; source={browseSourceCount}");

            Check(
                "Create command remains valid at the original setup boundary",
                owner.CreateFirstRecipeCommand.CanExecute(null),
                $"isValid={owner.IsFirstRecipeSetupValid}");
            owner.CreateFirstRecipeCommand.Execute(null);
            Check(
                "Create request reaches the Workbench callback",
                createCount == 1,
                $"createCount={createCount}");

            coordinator.Dispose();
            coordinator.Dispose();
            var propertyChangedAfterDispose = propertyChangedCount;
            var createAfterDispose = createCount;
            var browseFolderAfterDispose = browseFolderCount;
            var browseSourceAfterDispose = browseSourceCount;
            owner.FirstRecipeName = "after-dispose";
            owner.BrowseFirstRecipeFolderCommand.Execute(null);
            owner.BrowseFirstRecipeSourceCommand.Execute(null);
            owner.CreateFirstRecipeCommand.Execute(null);
            Check(
                "Dispose is idempotent and suppresses every later callback",
                propertyChangedCount == propertyChangedAfterDispose
                && createCount == createAfterDispose
                && browseFolderCount == browseFolderAfterDispose
                && browseSourceCount == browseSourceAfterDispose,
                $"property={propertyChangedCount}; create={createCount}; folder={browseFolderCount}; source={browseSourceCount}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            coordinator.Dispose();
            owner.Dispose();
        }

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchFirstRecipeEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
