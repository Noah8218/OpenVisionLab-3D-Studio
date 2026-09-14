using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the command-line Smoke scenario for selecting a Workbench tool and
/// inspecting its parameter surface. Production tool state remains in the
/// Workbench ViewModel; this type owns only Smoke sequencing and visual lookup.
/// </summary>
internal sealed class ShellToolSelectionSmoke
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly Dispatcher dispatcher;

    public ShellToolSelectionSmoke(
        ToolWorkbenchViewModel workbench,
        ToolRecipeWorkbenchView workbenchView,
        Dispatcher dispatcher)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.workbenchView = workbenchView ?? throw new ArgumentNullException(nameof(workbenchView));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<string?> RunAsync(
        string? toolId,
        bool expandParameters,
        bool focusParameterSearch)
    {
        if (!string.IsNullOrWhiteSpace(toolId))
        {
            var tool = workbench.Tools.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, toolId, StringComparison.OrdinalIgnoreCase));
            if (tool is null)
            {
                return $"Smoke tool '{toolId}' was not found in the Workbench catalog.";
            }

            workbenchView.ActivateToolLibraryPane();
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            var toolList = ShellSmokeArtifacts.FindVisualDescendants<ListBox>(workbenchView)
                .FirstOrDefault(list => string.Equals(list.Name, "AllToolsList", StringComparison.Ordinal));
            if (toolList is null)
            {
                return "Tool Library list was not found for double-click Smoke.";
            }

            var initialStepCount = workbench.PipelineSteps.Count;
            toolList.SelectedItem = tool;
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var doubleClickArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent,
            };
            toolList.RaiseEvent(doubleClickArgs);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var selectedStep = workbench.SelectedPipelineStep;
            var added = workbench.PipelineSteps.Count == initialStepCount + 1
                && selectedStep is not null
                && string.Equals(selectedStep.ToolId, tool.Id, StringComparison.Ordinal);
            Console.WriteLine(
                $"ToolLibraryDoubleClick|handled={doubleClickArgs.Handled}|selected={string.Equals(workbench.SelectedTool?.Id, tool.Id, StringComparison.Ordinal)}|added={added}|steps={initialStepCount}->{workbench.PipelineSteps.Count}");
            if (!doubleClickArgs.Handled || !added)
            {
                return "Tool Library double-click did not invoke AddSelectedToolCommand exactly once.";
            }
        }

        if (expandParameters)
        {
            workbenchView.ActivateSelectedToolPane();
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            var parametersExpander = ShellSmokeArtifacts.FindVisualDescendants<Expander>(workbenchView)
                .FirstOrDefault(expander =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(expander)
                    == "SelectedToolParametersExpander");
            if (parametersExpander is null)
            {
                return "Selected Tool parameters expander was not found.";
            }

            parametersExpander.IsExpanded = true;
        }

        if (focusParameterSearch)
        {
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            var parameterSearch = ShellSmokeArtifacts.FindVisualDescendants<TextBox>(workbenchView)
                .FirstOrDefault(textBox =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(textBox)
                    == "RecipeStepPropertySearch");
            if (parameterSearch is null || !parameterSearch.Focus())
            {
                return "Selected Tool parameter search could not receive keyboard focus.";
            }
        }

        return null;
    }
}
