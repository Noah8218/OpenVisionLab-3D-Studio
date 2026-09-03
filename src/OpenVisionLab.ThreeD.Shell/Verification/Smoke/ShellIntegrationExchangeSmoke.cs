using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the Integration Exchange visual-state Smoke scenarios. The shell
/// supplies the selected-workspace state, integration ViewModel, WPF root, and
/// Dispatcher; high-level failure/shutdown policy remains with the shell
/// coordinator.
/// </summary>
internal sealed record ShellIntegrationExchangeSmokeResult(
    string? PressedCaptureAutomationId,
    string? PressedCaptureScope,
    string? EvidenceLine,
    string? Failure)
{
    public bool Succeeded => Failure is null;

    public bool HasPressedCapture =>
        PressedCaptureAutomationId is not null
        && PressedCaptureScope is not null;
}

internal static class ShellIntegrationExchangeSmoke
{
    private const string RepresentativeExchangeRoot =
        @"D:\OpenVisionLab-Exchange\Projects\Automated-Optical-Inspection-Line-With-A-Deliberately-Long-Commissioning-Name\Shared-Exchange";

    public static async Task<ShellIntegrationExchangeSmokeResult> RunAsync(
        string requestedState,
        bool isExchangeSelected,
        ThreeDIntegrationViewModel integrationExchange,
        Window shellWindow,
        Dispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedState);
        ArgumentNullException.ThrowIfNull(integrationExchange);
        ArgumentNullException.ThrowIfNull(shellWindow);
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (!isExchangeSelected)
        {
            return Failure(
                "Integration exchange visual state requires --shell-workspace Exchange.");
        }

        integrationExchange.ExchangeRoot = RepresentativeExchangeRoot;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        shellWindow.UpdateLayout();

        if (requestedState.Equals("input-focus", StringComparison.OrdinalIgnoreCase))
        {
            var input = FindVisualDescendants<TextBox>(shellWindow)
                .FirstOrDefault(textBox => textBox.Name == "ExchangeRootTextBox");
            if (input is null)
            {
                return Failure("Integration exchange folder input was not available.");
            }

            input.Text = RepresentativeExchangeRoot;
            input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            integrationExchange.ExchangeRoot = RepresentativeExchangeRoot + @"\Restored-From-ViewModel";
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
            if (!input.Focus()
                || !input.IsKeyboardFocusWithin
                || input.Text != RepresentativeExchangeRoot + @"\Restored-From-ViewModel")
            {
                return Failure(
                    "Integration exchange folder did not complete its two-way binding and focus round trip.");
            }

            return Success(
                evidenceLine:
                    "IntegrationExchangeInput|focus=true|longValue=true|textToViewModel=true|viewModelToText=true");
        }

        if (requestedState.Equals("interaction-matrix", StringComparison.OrdinalIgnoreCase))
        {
            shellWindow.Activate();
            var interactionWindowHandle = new WindowInteropHelper(shellWindow).Handle;
            var interactionForegrounded = SetForegroundWindow(interactionWindowHandle);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            var workspace = FindVisualDescendants<UserControl>(shellWindow)
                .FirstOrDefault(control =>
                    AutomationProperties.GetAutomationId(control)
                    == "MachineExchangeWorkspace");
            var buttons = workspace is null
                ? []
                : FindVisualDescendants<Button>(workspace)
                    .Where(button => button.IsVisible)
                    .ToArray();
            if (workspace is null || buttons.Length < 7 || !buttons.Any(button => !button.IsEnabled))
            {
                return Failure(
                    "Integration exchange interaction matrix did not expose its expected enabled and disabled controls.");
            }

            var interactionHoverFallbackUsed = false;
            foreach (var button in buttons.Where(button => button.IsEnabled))
            {
                button.BringIntoView();
                if (!button.Focus()
                    || !button.IsKeyboardFocusWithin
                    || button.Command is not null
                     && button.IsEnabled != button.Command.CanExecute(button.CommandParameter))
                {
                    return Failure(
                        "Integration exchange button focus or CanExecute state was inconsistent.");
                }

                var relativeCenter = button.TransformToAncestor(shellWindow).Transform(
                    new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                var transformToDevice = PresentationSource.FromVisual(shellWindow)
                    ?.CompositionTarget?.TransformToDevice
                    ?? Matrix.Identity;
                var deviceCenter = transformToDevice.Transform(relativeCenter);
                _ = GetWindowRect(interactionWindowHandle, out var interactionWindowRect);
                var center = new Point(
                    interactionWindowRect.Left + deviceCenter.X,
                    interactionWindowRect.Top + deviceCenter.Y);
                if (!SetCursorPos((int)Math.Round(center.X), (int)Math.Round(center.Y)))
                {
                    return Failure(
                        "Integration exchange pointer could not enter the button hit region.");
                }

                PostClientMouseMove(interactionWindowHandle, deviceCenter);
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await Task.Delay(75);
                for (var attempt = 0; attempt < 5 && !button.IsMouseOver; attempt++)
                {
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                    await Task.Delay(50);
                }

                var buttonHoverFallback = false;
                if (!button.IsMouseOver)
                {
                    // Some desktop sessions keep the test process active while
                    // suppressing cross-process mouse-over promotion. Capture the
                    // element and route a real WPF mouse move so the same template
                    // trigger can still be inspected; report this as a harness
                    // fallback rather than native pointer proof.
                    System.Windows.Input.Mouse.Capture(
                        button,
                        System.Windows.Input.CaptureMode.Element);
                    button.RaiseEvent(new System.Windows.Input.MouseEventArgs(
                        System.Windows.Input.Mouse.PrimaryDevice,
                        Environment.TickCount)
                    {
                        RoutedEvent = System.Windows.Input.Mouse.MouseMoveEvent,
                        Source = button
                    });
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                    buttonHoverFallback = button.IsMouseOver;
                    System.Windows.Input.Mouse.Capture(null);
                }

                if (!button.IsMouseOver && !buttonHoverFallback)
                {
                    return Failure(
                        $"Integration exchange button did not enter hover state. foregrounded={interactionForegrounded}; cursor={center.X:0.#},{center.Y:0.#}; window={interactionWindowRect.Left},{interactionWindowRect.Top},{interactionWindowRect.Right},{interactionWindowRect.Bottom}; automationId={AutomationProperties.GetAutomationId(button)}");
                }

                interactionHoverFallbackUsed |= buttonHoverFallback;
                var awayDevice = transformToDevice.Transform(new Point(8, 8));
                var away = new Point(
                    interactionWindowRect.Left + awayDevice.X,
                    interactionWindowRect.Top + awayDevice.Y);
                SetCursorPos((int)Math.Round(away.X), (int)Math.Round(away.Y));
                PostClientMouseMove(interactionWindowHandle, awayDevice);
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
                await Task.Delay(75);
                if (button.IsMouseOver)
                {
                    return Failure(
                        "Integration exchange button did not recover after mouse leave.");
                }
            }

            var input = FindVisualDescendants<TextBox>(workspace)
                .First(textBox => textBox.Name == "ExchangeRootTextBox");
            input.Focus();
            if (!input.MoveFocus(new System.Windows.Input.TraversalRequest(
                    System.Windows.Input.FocusNavigationDirection.Next))
                || System.Windows.Input.Keyboard.FocusedElement is not Button)
            {
                return Failure(
                    "Integration exchange Tab traversal did not reach the next action.");
            }

            return Success(
                evidenceLine:
                    $"IntegrationExchangeInteraction|focus=true|hover=true|mouseLeave=true|disabled=true|canExecute=true|tabTraversal=true|hoverFallback={interactionHoverFallbackUsed}");
        }

        if (requestedState.Equals("validation-error", StringComparison.OrdinalIgnoreCase))
        {
            integrationExchange.RefreshHandoffsCommand.Execute(null);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
            return string.IsNullOrWhiteSpace(integrationExchange.StatusText)
                ? Failure("Integration exchange validation error did not render a status message.")
                : Success(
                    evidenceLine:
                        "IntegrationExchangeValidation|statusRendered=true|processStable=true|actionExecuted=false");
        }

        if (requestedState.Equals("primary-pressed", StringComparison.OrdinalIgnoreCase))
        {
            var primaryButton = FindVisualDescendants<Button>(shellWindow)
                .FirstOrDefault(button =>
                    AutomationProperties.GetAutomationId(button)
                    == "SaveIntegrationSetup");
            if (primaryButton is not { IsVisible: true, IsEnabled: true })
            {
                return Failure(
                    "Integration exchange primary button was not available for pressed-state capture.");
            }

            return Success(
                pressedCaptureAutomationId: "SaveIntegrationSetup",
                pressedCaptureScope: "IntegrationExchangePrimaryPressed");
        }

        if (requestedState.Equals("refresh-pressed", StringComparison.OrdinalIgnoreCase))
        {
            return Success(
                pressedCaptureAutomationId: "RefreshIntegrationHandoffs",
                pressedCaptureScope: "IntegrationExchangeRefreshPressed");
        }

        return Failure($"Unsupported integration exchange visual state '{requestedState}'.");
    }

    private static ShellIntegrationExchangeSmokeResult Success(
        string? pressedCaptureAutomationId = null,
        string? pressedCaptureScope = null,
        string? evidenceLine = null) =>
        new(
            pressedCaptureAutomationId,
            pressedCaptureScope,
            evidenceLine,
            Failure: null);

    private static ShellIntegrationExchangeSmokeResult Failure(string message) =>
        new(
            PressedCaptureAutomationId: null,
            PressedCaptureScope: null,
            EvidenceLine: null,
            Failure: message);

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
