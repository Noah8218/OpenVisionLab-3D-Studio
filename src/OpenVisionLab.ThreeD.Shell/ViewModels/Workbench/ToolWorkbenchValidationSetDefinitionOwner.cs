using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
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
    private ToolRecipeValidationSampleDefinition[] savedSamples = [];

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
        var existingSamples = Samples.ToDictionary(
            sample => sample.SourcePath,
            sample => sample,
            StringComparer.OrdinalIgnoreCase);
        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.SequenceEqual(Samples.Select(sample => sample.SourcePath), StringComparer.OrdinalIgnoreCase)) return;

        ReplaceSamples(paths.Select((path, index) => existingSamples.TryGetValue(path, out var previous)
            ? CreatePendingSample(index + 1, path, previous.DevelopmentRole) with { Expectation = previous.EffectiveExpectation, Usage = previous.EffectiveUsage }
            : CreatePendingSample(index + 1, path, ToolRecipeValidationSampleRole.HeldOut) with { Expectation = ToolRecipeValidationExpectation.Unset, Usage = ToolRecipeValidationUsage.Adjustment }));
        RefreshDefinitionDirty();
        onDefinitionChanged(true);
    }

    public ValidationSetSampleRow? SetSelectedSampleRole(
        ValidationSetSampleRow? selected,
        string? value)
    {
        if (selected is null || value is null) return null;
        var expectation = selected.EffectiveExpectation;
        var usage = selected.EffectiveUsage;
        switch (value.ToLowerInvariant())
        {
            case "good": expectation = ToolRecipeValidationExpectation.Ok; break;
            case "bad": expectation = ToolRecipeValidationExpectation.Ng; break;
            case "unset": expectation = ToolRecipeValidationExpectation.Unset; break;
            case "heldout": usage = selected.IsHeldOut ? ToolRecipeValidationUsage.Adjustment : ToolRecipeValidationUsage.HeldOut; break;
            case "adjustment": usage = ToolRecipeValidationUsage.Adjustment; break;
            default: return null;
        }
        if (selected.EffectiveExpectation == expectation && selected.EffectiveUsage == usage) return null;

        var index = Samples.IndexOf(selected);
        if (index < 0)
        {
            return null;
        }

        var updated = selected with
        {
            Role = ToolRecipeValidationSampleDefinition.ResolveDevelopmentRole(expectation, usage),
            Expectation = expectation,
            Usage = usage
        };
        Samples[index] = updated;
        RefreshDefinitionDirty();
        onDefinitionChanged(false);
        return updated;
    }

    public void ClearDefinition()
    {
        ReplaceSamples([]);
        RefreshDefinitionDirty();
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

    public void SaveForRecipe(string recipePath, bool markClean = true)
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

            if (markClean) SetDefinitionDirtyCore(false);
            return;
        }

        var document = createDocument();
        var sourceHash = document.Source.ContentSha256;
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
                        sample.DevelopmentRole) { Expectation = sample.EffectiveExpectation, Usage = sample.EffectiveUsage }).ToArray())
            {
                RecipeContentSha256 = File.Exists(recipePath) ? HashFile(recipePath) : null,
                CalculationContextSha256 = ToolRecipeValidationSetDefinitionStore.CreateCalculationContextSha256(document)
            });
        if (markClean) SetDefinitionDirtyCore(false);
    }

    public bool LoadForRecipe(
        string recipePath,
        ToolRecipeDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        return LoadDefinition(document, ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath));
    }

    public bool LoadDefinition(ToolRecipeDocument document, ToolRecipeValidationSetDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(document);
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
                "미검증",
                "Not validated") with { Expectation = sample.EffectiveExpectation, Usage = sample.EffectiveUsage }));
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
                koreanMessage ?? "미검증",
                englishMessage ?? "Not validated"),
            string.Empty,
            []);

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private ToolRecipeValidationSampleDefinition[] CaptureSampleDefinitions() => Samples.Select((sample, index) =>
        new ToolRecipeValidationSampleDefinition(index + 1, sample.SourcePath, sample.DevelopmentRole)
        { Expectation = sample.EffectiveExpectation, Usage = sample.EffectiveUsage }).ToArray();

    private void RefreshDefinitionDirty() => SetDefinitionDirtyCore(!CaptureSampleDefinitions().SequenceEqual(savedSamples));

    private void SetDefinitionDirtyCore(bool value)
    {
        if (!value) savedSamples = CaptureSampleDefinitions();
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
