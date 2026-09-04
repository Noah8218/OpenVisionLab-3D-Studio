using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed record ShellValidationSetSmokeState(
    bool RunRequested,
    Task? ThresholdSelectionTask,
    Task? SectionSelectionTask)
{
    public static ShellValidationSetSmokeState Empty { get; } =
        new(false, null, null);
}

internal static class ShellValidationSetSmoke
{
    public static ShellValidationSetSmokeState Configure(
        string[] arguments,
        ToolWorkbenchViewModel workbench,
        Action activateValidationSet,
        Action expandEvidence,
        Action expandThresholds,
        Func<ValidationWorkspaceSection, Task> applySectionAsync,
        Func<ValidationWorkspaceSection, bool> isSectionActive)
        => Configure(
            new ShellCommandLineArguments(arguments),
            workbench,
            activateValidationSet,
            expandEvidence,
            expandThresholds,
            applySectionAsync,
            isSectionActive);

    internal static ShellValidationSetSmokeState Configure(
        ShellCommandLineArguments commandLine,
        ToolWorkbenchViewModel workbench,
        Action activateValidationSet,
        Action expandEvidence,
        Action expandThresholds,
        Func<ValidationWorkspaceSection, Task> applySectionAsync,
        Func<ValidationWorkspaceSection, bool> isSectionActive)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(workbench);
        ArgumentNullException.ThrowIfNull(activateValidationSet);
        ArgumentNullException.ThrowIfNull(expandEvidence);
        ArgumentNullException.ThrowIfNull(expandThresholds);
        ArgumentNullException.ThrowIfNull(applySectionAsync);
        ArgumentNullException.ThrowIfNull(isSectionActive);

        var recipePath = commandLine.GetValue("--smoke-validation-set-recipe");
        var sourceList = commandLine.GetValue("--smoke-validation-set-sources");
        if (string.IsNullOrWhiteSpace(recipePath)
            || string.IsNullOrWhiteSpace(sourceList))
        {
            return ShellValidationSetSmokeState.Empty;
        }

        if (!workbench.TryOpenTeachingRecipe(recipePath, out var message))
        {
            throw new InvalidDataException(
                $"Validation Set smoke recipe could not be opened: {message}");
        }

        var sources = sourceList
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToArray();
        workbench.SetValidationSetSources(sources);

        var requestedRoles = commandLine.GetValue("--smoke-validation-set-roles");
        if (!string.IsNullOrWhiteSpace(requestedRoles))
        {
            var roles = requestedRoles.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
            for (var index = 0;
                 index < roles.Length
                 && index < workbench.ValidationSetSamples.Count;
                 index++)
            {
                workbench.SelectedValidationSetSample =
                    workbench.ValidationSetSamples[index];
                workbench.SetValidationSampleRoleCommand.Execute(roles[index]);
            }
        }

        activateValidationSet();
        if (commandLine.HasFlag("--smoke-validation-set-expand-evidence"))
        {
            expandEvidence();
        }
        if (commandLine.HasFlag("--smoke-validation-set-expand-thresholds"))
        {
            expandThresholds();
        }

        var runRequested = commandLine.HasFlag("--smoke-validation-set-run");
        Task? thresholdSelectionTask = null;
        if (runRequested)
        {
            workbench.RunValidationSetCommand.Execute(null);
            var thresholdMetric = commandLine.GetValue("--smoke-validation-threshold-metric");
            var thresholdKind = commandLine.GetValue("--smoke-validation-threshold-kind");
            if (!string.IsNullOrWhiteSpace(thresholdMetric)
                || !string.IsNullOrWhiteSpace(thresholdKind))
            {
                thresholdSelectionTask =
                    SelectValidationThresholdCandidateAsync(
                        commandLine,
                        workbench,
                        thresholdMetric,
                        thresholdKind);
            }
            if (commandLine.HasFlag("--smoke-validation-set-open-compare"))
            {
                _ = OpenValidationSetComparisonAsync(workbench);
            }
        }

        Task? sectionSelectionTask = null;
        var requestedSection = commandLine.GetValue("--smoke-validation-section");
        if (Enum.TryParse<ValidationWorkspaceSection>(
                requestedSection,
                ignoreCase: true,
                out var validationSection)
            && Enum.IsDefined(
                typeof(ValidationWorkspaceSection),
                validationSection))
        {
            sectionSelectionTask = SelectValidationSectionAsync(
                workbench,
                validationSection,
                runRequested,
                applySectionAsync,
                isSectionActive);
        }

        return new(
            runRequested,
            thresholdSelectionTask,
            sectionSelectionTask);
    }

    private static async Task SelectValidationSectionAsync(
        ToolWorkbenchViewModel workbench,
        ValidationWorkspaceSection section,
        bool runRequested,
        Func<ValidationWorkspaceSection, Task> applySectionAsync,
        Func<ValidationWorkspaceSection, bool> isSectionActive)
    {
        while (workbench.IsValidationSetRunning
               || runRequested
               && !workbench.HasValidationThresholdAssistantAnalysis)
        {
            await Task.Delay(25);
        }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await applySectionAsync(section);
            if (isSectionActive(section))
            {
                return;
            }

            await Task.Delay(50);
        }
    }

    private static async Task SelectValidationThresholdCandidateAsync(
        ShellCommandLineArguments commandLine,
        ToolWorkbenchViewModel workbench,
        string? metric,
        string? kind)
    {
        while (workbench.IsValidationSetRunning
               || !workbench.HasValidationThresholdAssistantAnalysis)
        {
            await Task.Delay(25);
        }

        if (commandLine.HasFlag("--smoke-validation-threshold-assistant-disabled"))
        {
            workbench.SelectedValidationThresholdCandidate = null;
            return;
        }

        var candidate = workbench.ValidationThresholdCandidates.FirstOrDefault(
            item =>
                (string.IsNullOrWhiteSpace(metric)
                 || string.Equals(
                     item.MetricName,
                     metric,
                     StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(kind)
                    || string.Equals(
                        item.LimitKind,
                        kind,
                        StringComparison.OrdinalIgnoreCase)));
        if (candidate is null)
        {
            return;
        }

        workbench.SelectedValidationThresholdCandidate = candidate;
        var shouldPropose = commandLine.HasFlag("--smoke-validation-threshold-propose");
        var shouldReview = commandLine.HasFlag("--smoke-validation-threshold-review");
        var shouldApply = commandLine.HasFlag("--smoke-validation-threshold-apply");
        var shouldReplay = commandLine.HasFlag("--smoke-validation-threshold-replay-heldout");
        var shouldRevalidate = commandLine.HasFlag("--smoke-validation-threshold-revalidate-development");
        var manualValues = commandLine.GetValue("--smoke-validation-threshold-manual-values");
        if (shouldPropose
            && workbench.ProposeValidationThresholdCandidateCommand
                .CanExecute(null))
        {
            workbench.ProposeValidationThresholdCandidateCommand.Execute(null);
        }
        if ((shouldReview || shouldApply || shouldReplay)
            && workbench.ReviewValidationThresholdCandidateCommand
                .CanExecute(null))
        {
            workbench.ReviewValidationThresholdCandidateCommand.Execute(null);
        }
        if ((shouldApply || shouldReplay)
            && workbench.ApplyValidationThresholdCandidateCommand
                .CanExecute(null))
        {
            workbench.ApplyValidationThresholdCandidateCommand.Execute(null);
        }
        if (!string.IsNullOrWhiteSpace(manualValues)
            && workbench.SelectedStepPropertyDraft
                is ThicknessStepProperties thickness)
        {
            var values = manualValues
                .Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Select(value => value.Split(
                    '=',
                    2,
                    StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2)
                .ToDictionary(
                    parts => parts[0],
                    parts => double.Parse(
                        parts[1],
                        CultureInfo.InvariantCulture),
                    StringComparer.Ordinal);
            if (values.TryGetValue(
                    "MinimumThickness",
                    out var minimum))
            {
                thickness.MinimumThickness = minimum;
            }
            if (values.TryGetValue(
                    "MaximumThickness",
                    out var maximum))
            {
                thickness.MaximumThickness = maximum;
            }
            workbench.MarkSelectedStepParameterDraftDirty();
            if (!workbench.TryApplySelectedStepParameterDraft(
                    out var manualApplyMessage))
            {
                throw new InvalidDataException(
                    $"Threshold manual-value smoke Apply failed: {manualApplyMessage}");
            }
        }
        if (shouldRevalidate
            && workbench.RevalidateValidationThresholdCorrectionCommand
                .CanExecute(null))
        {
            await workbench.RevalidateValidationThresholdCorrectionAsync();
        }
        if (shouldReplay
            && workbench.ReplayValidationThresholdHeldOutCommand
                .CanExecute(null))
        {
            await workbench.ReplayValidationThresholdHeldOutAsync();
        }
    }

    private static async Task OpenValidationSetComparisonAsync(
        ToolWorkbenchViewModel workbench)
    {
        while (workbench.IsValidationSetRunning)
        {
            await Task.Delay(25);
        }

        if (workbench.OpenValidationSetComparisonCommand.CanExecute(null))
        {
            workbench.OpenValidationSetComparisonCommand.Execute(null);
        }
    }

}
