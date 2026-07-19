using System.Text.Json;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// A taught C3D grid ROI evaluated as raw-height residuals from one explicit
/// best-fit reference plane. Physical calibration is intentionally outside this recipe.
/// </summary>
public sealed record C3DWarpageRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    C3DWarpageStep Step)
{
    public const string SupportedRecipeType = "c3d-warpage";
    public const string BestFitInspectionRoiReferenceMode = "BestFitInspectionRoi";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static C3DWarpageRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<C3DWarpageRecipe>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Recipe is empty: {path}");
        Validate(recipe);
        return recipe;
    }

    public void Save(string path)
    {
        Validate(this);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    private static void Validate(C3DWarpageRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(recipe.Version))
        {
            throw new InvalidDataException("C3D Warpage recipe type and version are required.");
        }

        if (recipe.Source is null
            || string.IsNullOrWhiteSpace(recipe.Source.EntityId)
            || string.IsNullOrWhiteSpace(recipe.Source.Name)
            || string.IsNullOrWhiteSpace(recipe.Source.Path)
            || string.IsNullOrWhiteSpace(recipe.Source.Unit))
        {
            throw new InvalidDataException("C3D Warpage source entity, name, path, and unit are required.");
        }

        var step = recipe.Step ?? throw new InvalidDataException("C3D Warpage step is required.");
        if (string.IsNullOrWhiteSpace(step.Id)
            || string.IsNullOrWhiteSpace(step.SourceEntityId)
            || !step.SourceEntityId.Equals(recipe.Source.EntityId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(step.ReferenceId)
            || !step.ReferenceMode.Equals(BestFitInspectionRoiReferenceMode, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(step.Unit)
            || string.IsNullOrWhiteSpace(step.FrameId)
            || step.MinimumValidSamples < 3)
        {
            throw new InvalidDataException("C3D Warpage requires IDs, best-fit reference mode, unit, frame, and at least three valid samples.");
        }

        if (step.Roi is null
            || step.Roi.Row < 0
            || step.Roi.Column < 0
            || step.Roi.RowCount <= 0
            || step.Roi.ColumnCount <= 0)
        {
            throw new InvalidDataException("C3D Warpage ROI requires non-negative grid origin and positive row/column counts.");
        }

        if (step.Acceptance is null
            || !double.IsFinite(step.Acceptance.MaximumPeakToValley)
            || step.Acceptance.MaximumPeakToValley <= 0.0
            || step.Acceptance.MaximumRms is { } maximumRms
                && (!double.IsFinite(maximumRms) || maximumRms <= 0.0))
        {
            throw new InvalidDataException("C3D Warpage peak-to-valley and optional RMS limits must be finite and positive.");
        }
    }
}

public sealed record C3DWarpageStep(
    string Id,
    string SourceEntityId,
    string ReferenceId,
    string ReferenceMode,
    C3DGridRoi Roi,
    C3DWarpageAcceptance Acceptance,
    string Unit,
    string FrameId,
    int MinimumValidSamples = 3,
    bool Enabled = true);

public sealed record C3DWarpageAcceptance(double MaximumPeakToValley, double? MaximumRms = null);
