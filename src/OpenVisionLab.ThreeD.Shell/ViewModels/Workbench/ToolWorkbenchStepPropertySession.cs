using System.ComponentModel;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchStepPropertySession : INotifyPropertyChanged
{
    public const string AdapterStatusPropertyName = "AdapterStatus";

    private object? draft;
    private string? draftStepId;
    private bool hasPendingChanges;
    private string status = "Select a typed tool to teach parameters. Apply XYZ Affine has a fixed no-parameter A2 contract.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? Draft
    {
        get => draft;
        private set
        {
            if (ReferenceEquals(draft, value))
            {
                return;
            }

            draft = value;
            OnPropertyChanged(nameof(Draft));
        }
    }

    public bool IsSupported => Draft is not null;

    public bool HasPendingChanges => hasPendingChanges;

    public string Status => status;

    public string GetAdapterStatus(ToolWorkbenchPipelineStepItem? step)
    {
        if (step is null)
        {
            return "No step selected";
        }

        return ToolWorkbenchStepPropertyAdapterCatalog.TryGetMappedNames(
            step.ToolId,
            out var mappedNames)
            ? FormatAdapterStatus(step, mappedNames)
            : "Partially supported - parameters are preserved read-only";
    }

    public void Refresh(ToolWorkbenchPipelineStepItem? step, string? newStatus = null)
    {
        draftStepId = step?.Id;
        object? nextDraft = null;
        if (step is not null)
        {
            ToolWorkbenchStepPropertyAdapterCatalog.TryCreateDraft(
                step.ToolId,
                step,
                out nextDraft);
        }

        Draft = nextDraft;

        SetState(
            false,
            newStatus ?? (Draft is null
                ? "This step is preserved, but no typed parameter editor is available yet."
                : "Parameters match the committed recipe. Editing does not run Preview or Publish."));
        OnPropertyChanged(nameof(IsSupported));
        OnPropertyChanged(AdapterStatusPropertyName);
    }

    public void MarkDirty() =>
        SetState(true, "Unapplied parameter changes. Apply or discard before changing recipe sessions.");

    /// <summary>
    /// Applies a bounded Filter preparation preset to the detached typed draft.
    /// The recipe step and all execution state remain unchanged until the normal
    /// parameter Apply command is used by the caller.
    /// </summary>
    public bool TryApplyFilterKernelPresetDraft(
        ToolWorkbenchPipelineStepItem step,
        int kernelSize,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.Id, draftStepId, StringComparison.Ordinal)
            || !string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            || Draft is not FilterStepProperties current)
        {
            message =
                "The selected step no longer matches the Filter preparation preset draft.";
            SetStatus(message);
            return false;
        }

        if (kernelSize is not (3 or 5 or 7))
        {
            message = "Filter preparation presets support kernel sizes 3, 5, and 7 only.";
            SetStatus(message);
            return false;
        }

        var next = new FilterStepProperties
        {
            Method = current.Method,
            KernelSize = kernelSize,
            MissingValuePolicy = current.MissingValuePolicy,
            BoundaryPolicy = current.BoundaryPolicy,
            UnmappedParameters = current.UnmappedParameters
        };
        if (!next.TryValidate(out message))
        {
            SetStatus(message);
            return false;
        }

        Draft = next;
        if (current.KernelSize == kernelSize)
        {
            SetState(
                false,
                $"Filter preparation preset {kernelSize} x {kernelSize} already matches the draft. No inspection ran.");
        }
        else
        {
            SetState(
                true,
                $"Filter preparation preset {kernelSize} x {kernelSize} applied to the draft only. Use normal Apply to change the recipe; Preview, Publish, and Run remain separate.");
        }

        message = Status;
        return true;
    }

    public bool TryApplyThresholdProposal(
        ToolWorkbenchPipelineStepItem step,
        ToolRecipeThresholdParameterProposal proposal,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(proposal);
        if (!string.Equals(step.Id, draftStepId, StringComparison.Ordinal)
            || !string.Equals(step.Id, proposal.StepId, StringComparison.Ordinal)
            || !string.Equals(step.ToolId, proposal.ToolId, StringComparison.Ordinal))
        {
            message =
                "The selected step no longer matches the reviewed threshold proposal.";
            SetStatus(message);
            return false;
        }

        var changes = proposal.Changes.ToDictionary(
            change => change.ParameterName,
            change => change.ProposedValue,
            StringComparer.Ordinal);
        switch (step.ToolId)
        {
            case "thickness":
            {
                var thickness = ThicknessStepProperties.From(step);
                if (!TryReadOptionalDouble(
                        changes,
                        "MinimumThickness",
                        thickness.MinimumThickness,
                        out var minimum,
                        out message)
                    || !TryReadOptionalDouble(
                        changes,
                        "MaximumThickness",
                        thickness.MaximumThickness,
                        out var maximum,
                        out message))
                {
                    SetStatus(message);
                    return false;
                }
                if (changes.Keys.Except(
                        ["MinimumThickness", "MaximumThickness"],
                        StringComparer.Ordinal).Any())
                {
                    message =
                        "The reviewed Thickness proposal contains an unsupported parameter.";
                    SetStatus(message);
                    return false;
                }

                thickness.MinimumThickness = minimum;
                thickness.MaximumThickness = maximum;
                if (!thickness.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                Draft = thickness;
                break;
            }
            case "warpage":
            {
                var warpage = WarpageStepProperties.From(step);
                if (!TryReadOptionalDouble(
                        changes,
                        "MaximumPeakToValley",
                        warpage.MaximumPeakToValley,
                        out var peakToValley,
                        out message)
                    || !TryReadOptionalDouble(
                        changes,
                        "MaximumRms",
                        warpage.MaximumRms,
                        out var rms,
                        out message))
                {
                    SetStatus(message);
                    return false;
                }
                if (changes.Keys.Except(
                        ["MaximumPeakToValley", "MaximumRms"],
                        StringComparer.Ordinal).Any())
                {
                    message =
                        "The reviewed Warpage proposal contains an unsupported parameter.";
                    SetStatus(message);
                    return false;
                }

                warpage.MaximumPeakToValley = peakToValley;
                warpage.MaximumRms = rms;
                if (!warpage.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                Draft = warpage;
                break;
            }
            case "completeness-grid":
            {
                var completeness = CompletenessGridStepProperties.From(step);
                if (!TryReadOptionalDouble(
                        changes,
                        "MinimumFiniteCoverageRatio",
                        completeness.MinimumFiniteCoverageRatio,
                        out var minimumCoverage,
                        out message)
                    || !TryReadOptionalDouble(
                        changes,
                        "MinimumReferenceRelativeMeanRawHeight",
                        completeness.MinimumReferenceRelativeMeanRawHeight,
                        out var minimumRelativeMean,
                        out message)
                    || !TryReadOptionalDouble(
                        changes,
                        "MaximumReferenceRelativeMeanRawHeight",
                        completeness.MaximumReferenceRelativeMeanRawHeight,
                        out var maximumRelativeMean,
                        out message))
                {
                    SetStatus(message);
                    return false;
                }
                if (changes.Keys.Except(
                        C3DCompletenessPresencePolicy.ParameterNames,
                        StringComparer.Ordinal).Any())
                {
                    message =
                        "The reviewed Completeness proposal contains an unsupported parameter.";
                    SetStatus(message);
                    return false;
                }

                completeness.MinimumFiniteCoverageRatio = minimumCoverage;
                completeness.MinimumReferenceRelativeMeanRawHeight =
                    minimumRelativeMean;
                completeness.MaximumReferenceRelativeMeanRawHeight =
                    maximumRelativeMean;
                if (!completeness.TryCreateContracts(
                        out _,
                        out _,
                        out message))
                {
                    SetStatus(message);
                    return false;
                }
                Draft = completeness;
                break;
            }
            default:
                message =
                    $"Tool '{step.ToolId}' has no typed threshold proposal adapter.";
                SetStatus(message);
                return false;
        }

        message =
            "Candidate values applied to the PropertyGrid draft only. Use the normal parameter Apply command to change the recipe.";
        SetState(true, message);
        return true;
    }

    public void SetStatus(string message)
    {
        status = message;
        OnPropertyChanged(nameof(Status));
    }

    internal void ResetDraftForSmoke(object value)
    {
        Draft = null;
        Draft = value;
    }

    public bool TryCreateParameterValues(
        ToolWorkbenchPipelineStepItem step,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        message = string.Empty;
        if (!string.Equals(step.Id, draftStepId, StringComparison.Ordinal))
        {
            message = "The selected step changed. Discard the draft and select the step again.";
            SetStatus(message);
            return false;
        }

        if (!ToolWorkbenchStepPropertyAdapterCatalog.TryCreateParameterValues(
                Draft,
                out values,
                out message))
        {
            SetStatus(message);
            return false;
        }

        return true;
    }

    public static bool IsSupportedTool(ToolWorkbenchPipelineStepItem step) =>
        ToolWorkbenchStepPropertyAdapterCatalog.IsSupported(step.ToolId);

    private static string FormatAdapterStatus(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlySet<string> mappedNames)
    {
        var unmappedCount = step.Parameters.Count(parameter => !mappedNames.Contains(parameter.Name));
        return unmappedCount == 0
            ? "Typed adapter ready"
            : $"Typed adapter ready | {unmappedCount} unmapped preserved";
    }

    private static bool TryReadOptionalDouble(
        IReadOnlyDictionary<string, string> changes,
        string parameterName,
        double currentValue,
        out double value,
        out string message)
    {
        if (!changes.TryGetValue(parameterName, out var text))
        {
            value = currentValue;
            message = string.Empty;
            return true;
        }
        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
            && double.IsFinite(value))
        {
            message = string.Empty;
            return true;
        }

        message =
            $"Threshold proposal parameter '{parameterName}' is not a finite number.";
        return false;
    }

    private void SetState(bool pending, string message)
    {
        hasPendingChanges = pending;
        status = message;
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(Status));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
