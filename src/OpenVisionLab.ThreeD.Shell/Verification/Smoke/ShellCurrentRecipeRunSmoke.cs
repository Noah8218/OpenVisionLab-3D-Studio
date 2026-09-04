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

    public Task<ShellCurrentRecipeRunSmokeResult> PrepareAsync(bool runSmoke) =>
        PrepareAsync(runSmoke, CancellationToken.None);

    internal async Task<ShellCurrentRecipeRunSmokeResult> PrepareAsync(
        bool runSmoke,
        CancellationToken cancellationToken)
    {
        if (!runSmoke)
        {
            return Success();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Canceled();
        }

        shell.IsValidateWorkspaceSelected = true;
        if (!await YieldDispatcherAsync(
                dispatcher,
                DispatcherPriority.Render,
                cancellationToken))
        {
            return Canceled();
        }
        return Success();
    }

    public Task<ShellCurrentRecipeRunSmokeResult> ExecuteAfterCaptureAsync(
        bool pressedSmoke,
        string? screenshotQualityReportPath) =>
        ExecuteAfterCaptureAsync(
            pressedSmoke,
            screenshotQualityReportPath,
            CancellationToken.None);

    internal async Task<ShellCurrentRecipeRunSmokeResult> ExecuteAfterCaptureAsync(
        bool pressedSmoke,
        string? screenshotQualityReportPath,
        CancellationToken cancellationToken)
    {
        if (!pressedSmoke)
        {
            return Success();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Canceled();
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

            if (cancellationToken.IsCancellationRequested)
            {
                return Canceled();
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
            if (!await DelayAsync(TimeSpan.FromMilliseconds(50), cancellationToken))
            {
                return Canceled();
            }
        }

        return workbench.HasOrderedRunResult
            ? Success()
            : Failure(
                "Pressed current-recipe Run did not complete an ordered graph execution.");
    }

    private static ShellCurrentRecipeRunSmokeResult Success() =>
        new(null);

    private static ShellCurrentRecipeRunSmokeResult Canceled() =>
        new(null)
        {
            IsCanceled = true
        };

    private static ShellCurrentRecipeRunSmokeResult Failure(string message) =>
        new(message);

    private static async Task<bool> YieldDispatcherAsync(
        Dispatcher dispatcher,
        DispatcherPriority priority,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await dispatcher.InvokeAsync(() => { }, priority);
            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}

internal sealed record ShellCurrentRecipeRunSmokeResult(string? Failure)
{
    public bool IsCanceled { get; init; }

    public bool Succeeded => Failure is null && !IsCanceled;
}
