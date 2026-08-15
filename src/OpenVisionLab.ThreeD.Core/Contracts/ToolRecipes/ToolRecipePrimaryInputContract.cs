namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Owns the primary typed-input rule shared by measurement authoring and recipe validation.
/// Auxiliary recipe-owned selections remain governed by each tool's existing contract.
/// </summary>
public static class ToolRecipePrimaryInputContract
{
    private static readonly HashSet<string> HeightFieldTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "thickness",
        "warpage",
        "completeness-grid"
    };

    private static readonly HashSet<string> TransformedHeightFieldTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "plane-flatness",
        "point-pair-dimensions",
        "gap-flush",
        "volume",
        "cross-section-dimensions"
    };

    public static bool TryGetRequiredContract(string? toolId, out string requiredContract)
    {
        if (toolId is not null && HeightFieldTools.Contains(toolId))
        {
            requiredContract = "HeightField";
            return true;
        }

        if (toolId is not null && TransformedHeightFieldTools.Contains(toolId))
        {
            requiredContract = "TransformedHeightField";
            return true;
        }

        requiredContract = string.Empty;
        return false;
    }

    public static bool IsCompatible(string? toolId, string? artifactContract)
    {
        if (!TryGetRequiredContract(toolId, out var requiredContract)
            || string.IsNullOrWhiteSpace(artifactContract))
        {
            return false;
        }

        var primaryContract = artifactContract.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        if (string.Equals(requiredContract, "TransformedHeightField", StringComparison.Ordinal))
        {
            return string.Equals(primaryContract, requiredContract, StringComparison.Ordinal);
        }

        return primaryContract is "HeightField"
            or "RawHeightField"
            or "FilteredHeightField"
            or "LeveledHeightField"
            or "TransformedHeightField"
            || string.Equals(
                artifactContract,
                "SourceC3D / RawHeightField",
                StringComparison.Ordinal);
    }

    public static string GetProducedContract(string? toolId) => toolId?.ToLowerInvariant() switch
    {
        "filter" or "remove-outlier-pixels" => "FilteredHeightField",
        "level-surface" => "LeveledHeightField",
        "roi-crop" => "HeightField",
        "re-grid-height-map" => "TransformedHeightField",
        "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions"
            or "gap-flush" or "volume" or "cross-section-dimensions" => "MeasurementResult",
        "completeness-grid" => "CompletenessGridMetrics",
        _ => string.Empty
    };
}
