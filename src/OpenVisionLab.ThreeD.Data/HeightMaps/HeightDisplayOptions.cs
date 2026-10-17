using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>SDK measurement values and limits captured by one explicit execution; never recipe/display persistence.</summary>
public sealed class HeightMeasurementColorSnapshot
{
    private readonly double[] values;

    public HeightMeasurementColorSnapshot(string inputEntityId, string inputContentSha256, string executionIdentity,
        int width, int height, IReadOnlyList<double> values, double lowerLimit, double upperLimit, string unit, string toolName)
    {
        if (width <= 0 || height <= 0 || values.Count != checked(width * height)
            || !double.IsFinite(lowerLimit) || !double.IsFinite(upperLimit) || lowerLimit > upperLimit)
            throw new ArgumentException("Measurement colour dimensions or executed limits are invalid.");
        ArgumentException.ThrowIfNullOrWhiteSpace(inputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionIdentity);
        InputEntityId = inputEntityId;
        InputContentSha256 = inputContentSha256;
        ExecutionIdentity = executionIdentity;
        Width = width;
        Height = height;
        this.values = values.ToArray();
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        ToolName = toolName;
    }

    public string InputEntityId { get; }
    public string InputContentSha256 { get; }
    public string ExecutionIdentity { get; }
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<double> Values => values;
    public double LowerLimit { get; }
    public double UpperLimit { get; }
    public string Unit { get; }
    public string ToolName { get; }
    public bool MatchesSource(string sha256, int width, int height) => Width == width && Height == height
        && string.Equals(InputContentSha256, sha256, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Display preferences only. Manual values and measurement limits are never persisted here.</summary>
public sealed record HeightDisplayProfile(HeightDisplayOptions Options, IReadOnlyDictionary<string, string> MapModes, string RecipeContext)
{
    public IReadOnlyDictionary<string, HeightTextureImageSettings>? Textures { get; init; }
}

/// <summary>A full-frame, top-left image association; source SHA-256 is the dictionary key.</summary>
public sealed record HeightTextureImageSettings(int GridWidth, int GridHeight, string ImagePath, string ImageSha256, bool IsVisible)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => GridWidth > 0 && GridHeight > 0 && !string.IsNullOrWhiteSpace(ImagePath)
        && System.IO.Path.IsPathFullyQualified(ImagePath) && ImageSha256 is { Length: 64 } && ImageSha256.All(Uri.IsHexDigit);
}

public sealed record HeightDisplayOptions
{
    [System.Text.Json.Serialization.JsonIgnore]
    public HeightMeasurementColorSnapshot? Measurement { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRangeDragActive { get; init; }
    public string RangeMode { get; init; } = "auto";
    public bool IsLinked { get; init; } = true;
    public bool KeepTeachingRange { get; init; }
    public bool KeepValidationRange { get; init; } = true;
    public bool KeepInspectionRange { get; init; } = true;
    public bool HighlightOutside { get; init; }
    public string ColorStyle { get; init; } = "continuous";
    public int BandCount { get; init; } = 10;
    public string Palette { get; init; } = "Height";
    public bool IsCollapsed { get; init; }

    public bool KeepsRange(string workspace) => workspace switch
    {
        "validate" => KeepValidationRange,
        "inspect" => KeepInspectionRange,
        _ => KeepTeachingRange
    };

    public static bool IsStoredMapMode(KeyValuePair<string, string> entry) =>
        (entry.Key.StartsWith("teach|", StringComparison.Ordinal) || entry.Key.StartsWith("validate|", StringComparison.Ordinal)
            || entry.Key.StartsWith("inspect|", StringComparison.Ordinal))
        && entry.Key.Length > entry.Key.IndexOf('|') + 1 && entry.Value is "robust" or "roi";

    public HeightDisplayOptions Sanitize(bool forPersistence = false) => this with
    {
        Measurement = forPersistence ? null : Measurement,
        IsRangeDragActive = !forPersistence && IsRangeDragActive,
        RangeMode = RangeMode is "auto" or "robust" or "roi" || !forPersistence && RangeMode == "manual" ? RangeMode : "auto",
        ColorStyle = ColorStyle is "continuous" or "bands" or "tolerance" ? ColorStyle : "continuous",
        BandCount = BandCount is 5 or 8 or 10 or 16 or 20 ? BandCount : 10,
        Palette = Palette is "Height" or "Grayscale" or "Thermal" or "ColorVisionAssist" or "Solid" ? Palette : "Height"
    };
}
