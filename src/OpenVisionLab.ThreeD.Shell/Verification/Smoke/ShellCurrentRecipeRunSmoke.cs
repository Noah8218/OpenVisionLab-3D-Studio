using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the Current Recipe Run Smoke preparation and explicit post-capture
/// ordered-run activation. MainWindow retains scenario selection, screenshot
/// orchestration, and failure/shutdown policy.
/// </summary>
internal sealed class ShellCurrentRecipeRunSmoke
{
    private readonly ShellMainWindowViewModel shell;
    private readonly Dispatcher dispatcher;
    private readonly Func<Button?> findRunButton;

    public ShellCurrentRecipeRunSmoke(
        ShellMainWindowViewModel shell,
        Dispatcher dispatcher,
        Func<Button?> findRunButton)
    {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.findRunButton = findRunButton ?? throw new ArgumentNullException(nameof(findRunButton));
    }

    public async Task<ShellCurrentRecipeRunSmokeResult> PrepareAsync(bool runSmoke)
    {
        if (!runSmoke)
        {
            return Success();
        }

        shell.IsValidateWorkspaceSelected = true;
        await dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);
        return Success();
    }

    public async Task<ShellCurrentRecipeRunSmokeResult> ExecuteAfterCaptureAsync(
        bool pressedSmoke,
        string? screenshotQualityReportPath)
    {
        if (!pressedSmoke)
        {
            return Success();
        }

        var workbench = shell.Workbench;
        if (!workbench.HasOrderedRunResult
            && !workbench.IsOrderedRunRunning)
        {
            var runButton = findRunButton();
            if (runButton is not { IsEnabled: true })
            {
                return Failure(
                    "Pressed current-recipe Run could not activate the enabled button after capture.");
            }

            if (runButton.Command?.CanExecute(runButton.CommandParameter) != true)
            {
                return Failure(
                    "Pressed current-recipe Run button command rejected activation after capture.");
            }

            runButton.Command.Execute(runButton.CommandParameter);
            if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath))
            {
                File.AppendAllLines(
                    Path.GetFullPath(screenshotQualityReportPath),
                [
                    "Activation|scope=CurrentRecipeRunPressed|mode=bound-command-after-held-capture"
                ]);
            }
        }

        var runDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (!workbench.HasOrderedRunResult
            && DateTimeOffset.UtcNow < runDeadline)
        {
            await Task.Delay(50);
        }

        return workbench.HasOrderedRunResult
            ? Success()
            : Failure(
                "Pressed current-recipe Run did not complete an ordered graph execution.");
    }

    private static ShellCurrentRecipeRunSmokeResult Success() =>
        new(null);

    private static ShellCurrentRecipeRunSmokeResult Failure(string message) =>
        new(message);
}

internal sealed record ShellCurrentRecipeRunSmokeResult(string? Failure)
{
    public bool Succeeded => Failure is null;
}
