using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the WPF Surface Match collection and Published-evidence interaction
/// Smoke paths. MainWindow retains sequencing and failure/shutdown policy;
/// production evidence and selection state remain in the Workbench owner.
/// </summary>
internal sealed class ShellSurfaceMatchInteractionSmoke
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly Func<int, int, bool> setCursorPos;
    private readonly Dispatcher dispatcher;

    public ShellSurfaceMatchInteractionSmoke(
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView,
        Func<int, int, bool> setCursorPos,
        Dispatcher dispatcher)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.workbenchView = workbenchView ?? throw new ArgumentNullException(nameof(workbenchView));
        this.setCursorPos = setCursorPos ?? throw new ArgumentNullException(nameof(setCursorPos));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<ShellSurfaceMatchInteractionSmokeResult> RunAsync(
        bool collectionNavigationFocusHover,
        bool collectionDisabled,
        bool experimentFocusHover,
        bool collectionPopup,
        string? collectionPopupScreenshotPath)
    {
        if (collectionNavigationFocusHover)
        {
            var collection = workbench.SurfaceMatchCollection;
            var selectedMatchId = workbench.SelectedSurfaceMatchCollectionItem?.MatchId;
            var previousButton = FindButton("PreviousSurfaceMatchCollectionItem");
            if (collection is null
                || selectedMatchId is null
                || previousButton is null
                || !previousButton.IsEnabled
                || !previousButton.Focus())
            {
                return Failure(
                    "Previous Surface Match collection navigation button could not receive focus.");
            }

            var center = previousButton.PointToScreen(
                new Point(
                    previousButton.ActualWidth / 2.0,
                    previousButton.ActualHeight / 2.0));
            if (!setCursorPos(
                    (int)Math.Round(center.X),
                    (int)Math.Round(center.Y)))
            {
                return Failure(
                    "Previous Surface Match collection navigation button could not receive pointer hover.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            await Task.Delay(100);
            if (workbench.SurfaceMatchCollection?.ContentSha256 != collection.ContentSha256
                || workbench.SelectedSurfaceMatchCollectionItem?.MatchId != selectedMatchId)
            {
                return Failure(
                    "Focusing or hovering Surface Match navigation changed evidence or selection state.");
            }
        }

        if (collectionDisabled)
        {
            var collection = workbench.SurfaceMatchCollection;
            var selectedMatchId = workbench.SelectedSurfaceMatchCollectionItem?.MatchId;
            var selector = FindComboBox("SurfaceMatchCollectionSelector");
            if (collection is null || selectedMatchId is null || selector is null)
            {
                return Failure(
                    "Surface Match collection selector was unavailable for disabled-state capture.");
            }

            selector.SetCurrentValue(UIElement.IsEnabledProperty, false);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (selector.IsEnabled
                || workbench.SurfaceMatchCollection?.ContentSha256 != collection.ContentSha256
                || workbench.SelectedSurfaceMatchCollectionItem?.MatchId != selectedMatchId)
            {
                return Failure(
                    "Surface Match collection disabled-state capture changed evidence or selection state.");
            }
        }

        if (experimentFocusHover)
        {
            var publishedButton = FindButton("SurfaceMatchExperimentShowPublishedButton");
            if (publishedButton is null || !publishedButton.Focus())
            {
                return Failure(
                    "Surface Match Published comparison button could not receive keyboard focus.");
            }

            var center = publishedButton.PointToScreen(
                new Point(
                    publishedButton.ActualWidth / 2.0,
                    publishedButton.ActualHeight / 2.0));
            if (!setCursorPos(
                    (int)Math.Round(center.X),
                    (int)Math.Round(center.Y)))
            {
                return Failure(
                    "Surface Match Published comparison button could not receive pointer hover.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            await Task.Delay(100);
        }

        if (collectionPopup)
        {
            var collection = workbench.SurfaceMatchCollection;
            var selectedMatchId = workbench.SelectedSurfaceMatchCollectionItem?.MatchId;
            var selector = FindComboBox("SurfaceMatchCollectionSelector");
            if (collection is null
                || selectedMatchId is null
                || selector is null
                || !selector.Focus())
            {
                return Failure(
                    "Surface Match collection selector could not receive keyboard focus.");
            }

            selector.IsDropDownOpen = true;
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var center = selector.PointToScreen(
                new Point(
                    selector.ActualWidth / 2.0,
                    selector.ActualHeight / 2.0));
            if (!setCursorPos(
                    (int)Math.Round(center.X),
                    (int)Math.Round(center.Y + selector.ActualHeight * 2.5)))
            {
                return Failure(
                    "Surface Match collection popup item could not receive pointer hover.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            await Task.Delay(100);
            if (collectionPopupScreenshotPath is not null)
            {
                try
                {
                    selector.ApplyTemplate();
                    var popup = selector.Template.FindName("PART_Popup", selector) as Popup
                        ?? ShellSmokeArtifacts.FindVisualDescendants<Popup>(selector).FirstOrDefault();
                    if (popup?.Child is not FrameworkElement popupChild || !popup.IsOpen)
                    {
                        throw new InvalidOperationException(
                            "PART_Popup is closed or has no FrameworkElement child.");
                    }

                    popupChild.UpdateLayout();
                    var popupCapture = WpfScreenshotCapture.Capture(popupChild);
                    WpfScreenshotCapture.Save(
                        popupCapture.Bitmap,
                        collectionPopupScreenshotPath);
                    ShellSmokeArtifacts.WriteTextReport(
                        collectionPopupScreenshotPath + ".quality.txt",
                    [
                        "SurfaceMatchCollectionPopupScreenshot|" + popupCapture.Quality.Summary,
                        "Boundary|App-owned WPF popup child only; no desktop or unrelated application pixels."
                    ]);
                }
                catch (Exception exception)
                {
                    ShellSmokeArtifacts.WriteTextReport(
                        collectionPopupScreenshotPath + ".failure.txt",
                    [
                        "SurfaceMatchCollectionPopupScreenshot=FAIL",
                        exception.ToString()
                    ]);
                    return Failure(
                        "Surface Match collection popup app-only capture failed: "
                        + exception.Message);
                }
            }

            if (!selector.IsDropDownOpen
                || workbench.SurfaceMatchCollection?.ContentSha256 != collection.ContentSha256
                || workbench.SelectedSurfaceMatchCollectionItem?.MatchId != selectedMatchId)
            {
                return Failure(
                    "Opening the Surface Match collection selector changed evidence or selection state.");
            }

            await Task.Delay(2500);
        }

        return Success();
    }

    private Button? FindButton(string automationId) =>
        ShellSmokeArtifacts.FindVisualDescendants<Button>(workbenchView)
            .FirstOrDefault(button =>
                AutomationProperties.GetAutomationId(button) == automationId);

    private ComboBox? FindComboBox(string automationId) =>
        ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(workbenchView)
            .FirstOrDefault(comboBox =>
                AutomationProperties.GetAutomationId(comboBox) == automationId);

    private static ShellSurfaceMatchInteractionSmokeResult Success() =>
        new(null);

    private static ShellSurfaceMatchInteractionSmokeResult Failure(string message) =>
        new(message);
}

internal sealed record ShellSurfaceMatchInteractionSmokeResult(string? Failure)
{
    public bool Succeeded => Failure is null;
}
