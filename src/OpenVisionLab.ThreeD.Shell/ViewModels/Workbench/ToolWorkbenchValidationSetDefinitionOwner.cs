using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Validation Set definition collection, sample order/role mutation,
/// dirty state, and the existing sidecar persistence boundary. Review,
/// threshold evidence, and explicit execution remain separate owners.
/// </summary>
internal sealed class ToolWorkbenchValidationSetDefinitionOwner :
    INotifyPropertyChanged
{
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string> getRecipeName;
    private readonly Func<string, string, string> localize;
    private readonly Action<bool> onDefinitionChanged;
    private readonly Action<bool> onDirtyChanged;
    private bool isValidationSetDefinitionDirty;

    public ToolWorkbenchValidationSetDefinitionOwner(
        Func<ToolRecipeDocument> createDocument,
        Func<string> getRecipeName,
        Func<string, string, string> localize,
        Action<bool> onDefinitionChanged,
        Action<bool> onDirtyChanged)
    {
        this.createDocument = createDocument
            ?? throw new ArgumentNullException(nameof(createDocument));
        this.getRecipeName = getRecipeName
            ?? throw new ArgumentNullException(nameof(getRecipeName));
        this.localize = localize
            ?? throw new ArgumentNullException(nameof(localize));
        this.onDefinitionChanged = onDefinitionChanged
            ?? throw new ArgumentNullException(nameof(onDefinitionChanged));
        this.onDirtyChanged = onDirtyChanged
            ?? throw new ArgumentNullException(nameof(onDirtyChanged));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ValidationSetSampleRow> Samples { get; } = [];

    public bool IsValidationSetDefinitionDirty =>
        isValidationSetDefinitionDirty;

    public void SetDefinitionDirty(bool value) =>
        SetDefinitionDirtyCore(value);

    public void SetValidationSetSources(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        var existingRoles = Samples.ToDictionary(
            sample => sample.SourcePath,
            sample => sample.Role,
            StringComparer.OrdinalIgnoreCase);
        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ReplaceSamples(paths.Select((path, index) => CreatePendingSample(
            index + 1,
            path,
            existingRoles.GetValueOrDefault(
                path,
                ToolRecipeValidationSampleRole.Good))));
        SetDefinitionDirtyCore(true);
        onDefinitionChanged(true);
    }

    public ValidationSetSampleRow? SetSelectedSampleRole(
        ValidationSetSampleRow? selected,
        string? value)
    {
        if (selected is null
            || !Enum.TryParse<ToolRecipeValidationSampleRole>(
                value,
                ignoreCase: true,
                out var role)
            || !Enum.IsDefined(role)
            || selected.Role == role)
        {
            return null;
        }

        var index = Samples.IndexOf(selected);
        if (index < 0)
        {
            return null;
        }

        var updated = selected with
        {
            Role = role,
            Status = "Pending",
            StatusText = localize("대기", "Pending"),
            Message = localize(
                "기대 역할이 변경됐습니다. '샘플 세트 실행'을 선택하세요.",
                "Expected role changed; choose Run sample set."),
            Duration = string.Empty,
            Steps = []
        };
        Samples[index] = updated;
        SetDefinitionDirtyCore(true);
        onDefinitionChanged(false);
        return updated;
    }

    public void ClearDefinition()
    {
        ReplaceSamples([]);
        SetDefinitionDirtyCore(true);
        onDefinitionChanged(true);
    }

    public void ReplaceExecutionResult(
        IEnumerable<ValidationSetSampleRow> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ReplaceSamples(samples);
    }

    public void RefreshLocalization(Func<ResultStatus, string> localizeStatus)
    {
        ArgumentNullException.ThrowIfNull(localizeStatus);
        for (var index = 0; index < Samples.Count; index++)
        {
            var sample = Samples[index];
            var status = Enum.TryParse<ResultStatus>(sample.Status, out var parsed)
                ? parsed
                : (ResultStatus?)null;
            var steps = sample.Steps.Select(step =>
            {
                var stepStatus = Enum.TryParse<ResultStatus>(step.Status, out var parsedStep)
                    ? parsedStep
                    : ResultStatus.Error;
                return step with
                {
                    StatusText = localizeStatus(stepStatus),
                    Metrics = step.Metrics.Select(metric => metric with
                    {
                        StatusText = Enum.TryParse<ResultStatus>(
                            metric.Status,
                            out var metricStatus)
                            ? localizeStatus(metricStatus)
                            : string.Empty
                    }).ToArray(),
                    Overlays = step.Overlays.Select(overlay => overlay with
                    {
                        StatusText = Enum.TryParse<ResultStatus>(
                            overlay.Status,
                            out var overlayStatus)
                            ? localizeStatus(overlayStatus)
                            : string.Empty
                    }).ToArray()
                };
            }).ToArray();
            Samples[index] = sample with
            {
                StatusText = status is null
                    ? localize("대기", "Pending")
                    : localizeStatus(status.Value),
                Steps = steps
            };
        }
    }

    public void SaveForRecipe(string recipePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        if (Samples.Count == 0)
        {
            var manifestPath =
                ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(
                    recipePath);
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            SetDefinitionDirtyCore(false);
            return;
        }

        var sourceHash = createDocument().Source.ContentSha256;
        if (string.IsNullOrWhiteSpace(sourceHash))
        {
            throw new InvalidDataException(
                "Validation Set roles cannot be saved without the identified recipe source SHA-256.");
        }

        ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
            recipePath,
            new ToolRecipeValidationSetDefinition(
                ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                getRecipeName(),
                sourceHash,
                Samples.Select((sample, index) =>
                    new ToolRecipeValidationSampleDefinition(
                        index + 1,
                        sample.SourcePath,
                        sample.Role)).ToArray()));
        SetDefinitionDirtyCore(false);
    }

    public bool LoadForRecipe(
        string recipePath,
        ToolRecipeDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentNullException.ThrowIfNull(document);
        var definition =
            ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath);
        if (definition is null
            || !string.Equals(
                definition.RecipeSourceSha256,
                document.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            ReplaceSamples([]);
            SetDefinitionDirtyCore(false);
            onDefinitionChanged(true);
            return false;
        }

        ReplaceSamples(definition.Samples
            .OrderBy(sample => sample.Order)
            .Select(sample => CreatePendingSample(
                sample.Order,
                sample.SourcePath,
                sample.Role,
                "저장된 역할을 불러왔습니다. 명시적 전체 실행을 기다립니다.",
                "Saved role loaded; waiting for explicit Run All.")));
        SetDefinitionDirtyCore(false);
        onDefinitionChanged(true);
        return true;
    }

    private void ReplaceSamples(IEnumerable<ValidationSetSampleRow> samples)
    {
        Samples.Clear();
        foreach (var sample in samples)
        {
            Samples.Add(sample);
        }
    }

    private ValidationSetSampleRow CreatePendingSample(
        int order,
        string sourcePath,
        ToolRecipeValidationSampleRole role,
        string? koreanMessage = null,
        string? englishMessage = null) =>
        new(
            order,
            sourcePath,
            role,
            "Pending",
            localize("대기", "Pending"),
            localize(
                koreanMessage ?? "미실행 · '샘플 세트 실행'을 선택하세요.",
                englishMessage ?? "Not run · choose Run sample set."),
            string.Empty,
            []);

    private void SetDefinitionDirtyCore(bool value)
    {
        if (isValidationSetDefinitionDirty == value)
        {
            return;
        }

        isValidationSetDefinitionDirty = value;
        OnPropertyChanged(nameof(IsValidationSetDefinitionDirty));
        onDirtyChanged(value);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
