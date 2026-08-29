using System.Text.Json;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

/// <summary>
/// Local reader/writer for the cross-modal projection sidecar. It mirrors the
/// Machine Studio contract without introducing a cross-repository project
/// reference or changing the released v2 contracts package.
/// </summary>
public static class ThreeDCoordinateProjectionContract
{
    public const string SchemaVersion = "1.0";
    public const string ProfileArtifactRole = "coordinate-projection-profile";
    public const string ProfileArtifactId = "coordinate-projection-profile";
    public const string ResultEvidenceRole = "coordinate-projection-result";
    public const string ResultEvidenceArtifactId = "coordinate-projection-result";
    public const string MappingKind = "normalized-linear";
    public const string ImageUnit = "px";
    public const string ImageOrigin = "top-left";
    public const string GridUnit = "raw-height";
    public const string GridFrameId = "frame.c3d-grid-index";
    public const string GridOrigin = "top-left";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static ThreeDCoordinateProjectionProfile ReadProfile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var profile = JsonSerializer.Deserialize<ThreeDCoordinateProjectionProfile>(
                           File.ReadAllText(Path.GetFullPath(path)),
                           JsonOptions)
                       ?? throw new InvalidDataException("Coordinate projection profile is empty.");
        Validate(profile);
        return profile;
    }

    public static TwoDIntegrationRunRecordDocument ReadTwoDRunRecord(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var record = JsonSerializer.Deserialize<TwoDIntegrationRunRecordDocument>(
                         File.ReadAllText(Path.GetFullPath(path)),
                         JsonOptions)
                     ?? throw new InvalidDataException("The 2D Run Record is empty.");
        if (record.SourceImageWidth <= 1 || record.SourceImageHeight <= 1
            || record.Steps is null)
        {
            throw new InvalidDataException(
                "The 2D Run Record does not contain source dimensions and steps.");
        }

        return record;
    }

    public static string SerializeResult(ThreeDCoordinateProjectionResult result)
    {
        ValidateResult(result);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    public static ThreeDCoordinateProjectionResult ReadResult(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var result = JsonSerializer.Deserialize<ThreeDCoordinateProjectionResult>(
                         File.ReadAllText(Path.GetFullPath(path)),
                         JsonOptions)
                     ?? throw new InvalidDataException("Coordinate projection result is empty.");
        ValidateResult(result);
        return result;
    }

    public static void Validate(ThreeDCoordinateProjectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!string.Equals(profile.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(profile.ProjectionId))
        {
            throw new InvalidDataException("Coordinate projection profile identity is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(profile.TwoDTransactionId)
            && (!Guid.TryParse(profile.TwoDTransactionId, out var transactionId)
                || transactionId == Guid.Empty))
        {
            throw new InvalidDataException(
                "The coordinate projection profile TwoD transaction identity is invalid.");
        }

        ArgumentNullException.ThrowIfNull(profile.Image);
        if (profile.Image.Width <= 1
            || profile.Image.Height <= 1
            || !string.Equals(profile.Image.Unit, ImageUnit, StringComparison.Ordinal)
            || !string.Equals(profile.Image.Origin, ImageOrigin, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Coordinate projection image dimensions, unit, and origin are invalid.");
        }

        ArgumentNullException.ThrowIfNull(profile.Grid);
        if (!string.Equals(profile.Grid.Unit, GridUnit, StringComparison.Ordinal)
            || !string.Equals(profile.Grid.FrameId, GridFrameId, StringComparison.Ordinal)
            || !string.Equals(profile.Grid.Origin, GridOrigin, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Coordinate projection grid unit, frame, and origin are invalid.");
        }

        ArgumentNullException.ThrowIfNull(profile.Mapping);
        if (!string.Equals(profile.Mapping.Kind, MappingKind, StringComparison.Ordinal)
            || !double.IsFinite(profile.Mapping.ScaleX)
            || !double.IsFinite(profile.Mapping.ScaleY)
            || profile.Mapping.ScaleX == 0.0
            || profile.Mapping.ScaleY == 0.0
            || !double.IsFinite(profile.Mapping.OffsetX)
            || !double.IsFinite(profile.Mapping.OffsetY))
        {
            throw new InvalidDataException(
                "Coordinate projection mapping must be normalized-linear with finite non-zero scale.");
        }
    }

    public static (double X, double Y) MapImageToGrid(
        ThreeDCoordinateProjectionProfile profile,
        double imageX,
        double imageY,
        int gridWidth,
        int gridHeight)
    {
        Validate(profile);
        ValidateCoordinate(imageX, nameof(imageX));
        ValidateCoordinate(imageY, nameof(imageY));
        ValidateGridDimensions(gridWidth, gridHeight);
        return (
            profile.Mapping.OffsetX
                + imageX / (profile.Image.Width - 1)
                * (gridWidth - 1)
                * profile.Mapping.ScaleX,
            profile.Mapping.OffsetY
                + imageY / (profile.Image.Height - 1)
                * (gridHeight - 1)
                * profile.Mapping.ScaleY);
    }

    public static (double X, double Y) MapGridToImage(
        ThreeDCoordinateProjectionProfile profile,
        double gridX,
        double gridY,
        int gridWidth,
        int gridHeight)
    {
        Validate(profile);
        ValidateCoordinate(gridX, nameof(gridX));
        ValidateCoordinate(gridY, nameof(gridY));
        ValidateGridDimensions(gridWidth, gridHeight);
        return (
            (gridX - profile.Mapping.OffsetX)
                / ((gridWidth - 1) * profile.Mapping.ScaleX)
                * (profile.Image.Width - 1),
            (gridY - profile.Mapping.OffsetY)
                / ((gridHeight - 1) * profile.Mapping.ScaleY)
                * (profile.Image.Height - 1));
    }

    public static bool ProfilesMatch(
        ThreeDCoordinateProjectionProfile expected,
        ThreeDCoordinateProjectionProfile actual) =>
        string.Equals(expected.ProjectionId, actual.ProjectionId, StringComparison.Ordinal)
        && expected.Image == actual.Image
        && expected.Grid == actual.Grid
        && expected.Mapping == actual.Mapping;

    private static void ValidateResult(ThreeDCoordinateProjectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!string.Equals(result.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(result.ProjectionId)
            || !Guid.TryParse(result.TwoDTransactionId, out var twoDTransactionId)
            || twoDTransactionId == Guid.Empty
            || !Guid.TryParse(result.ThreeDTransactionId, out var threeDTransactionId)
            || threeDTransactionId == Guid.Empty
            || result.ImageWidth <= 1
            || result.ImageHeight <= 1
            || result.GridWidth <= 1
            || result.GridHeight <= 1
            || result.TwoDToThreeD is null
            || result.ThreeDToTwoD is null)
        {
            throw new InvalidDataException("Coordinate projection result identity or dimensions are invalid.");
        }

        ValidatePoints(result.TwoDToThreeD);
        ValidatePoints(result.ThreeDToTwoD);
    }

    private static void ValidatePoints(IEnumerable<ThreeDProjectedCoordinate> points)
    {
        foreach (var point in points)
        {
            if (point is null
                || string.IsNullOrWhiteSpace(point.Direction)
                || string.IsNullOrWhiteSpace(point.Id)
                || !double.IsFinite(point.ImageX)
                || !double.IsFinite(point.ImageY)
                || !double.IsFinite(point.GridX)
                || !double.IsFinite(point.GridY)
                || point.SampledHeight is { } height && !double.IsFinite(height))
            {
                throw new InvalidDataException("Coordinate projection contains an invalid point.");
            }
        }
    }

    private static void ValidateGridDimensions(int gridWidth, int gridHeight)
    {
        if (gridWidth <= 1 || gridHeight <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gridWidth),
                "Projection requires a grid wider and taller than one cell.");
        }
    }

    private static void ValidateCoordinate(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Coordinate must be finite.");
        }
    }
}

public sealed record ThreeDCoordinateProjectionProfile(
    string SchemaVersion,
    string ProjectionId,
    string? TwoDTransactionId,
    ThreeDCoordinateProjectionImage Image,
    ThreeDCoordinateProjectionGrid Grid,
    ThreeDCoordinateProjectionMapping Mapping);

public sealed record ThreeDCoordinateProjectionImage(
    int Width,
    int Height,
    string Unit,
    string Origin);

public sealed record ThreeDCoordinateProjectionGrid(
    string Unit,
    string FrameId,
    string Origin);

public sealed record ThreeDCoordinateProjectionMapping(
    string Kind,
    double ScaleX,
    double ScaleY,
    double OffsetX,
    double OffsetY);

public sealed record ThreeDCoordinateProjectionResult(
    string SchemaVersion,
    string ProjectionId,
    string TwoDTransactionId,
    string ThreeDTransactionId,
    string Outcome,
    string TwoDRunId,
    string ThreeDRunId,
    int ImageWidth,
    int ImageHeight,
    int GridWidth,
    int GridHeight,
    IReadOnlyList<ThreeDProjectedCoordinate> TwoDToThreeD,
    IReadOnlyList<ThreeDProjectedCoordinate> ThreeDToTwoD,
    DateTimeOffset RecordedAtUtc);

public sealed record ThreeDProjectedCoordinate(
    string Direction,
    string Id,
    string Kind,
    string Label,
    double ImageX,
    double ImageY,
    double GridX,
    double GridY,
    double? SampledHeight,
    string SampleStatus,
    string InspectionStatus);

public sealed record TwoDIntegrationRunRecordDocument(
    string SchemaVersion,
    string RunId,
    DateTimeOffset RecordedAtUtc,
    string SourceRelativePath,
    string SourceSha256,
    long SourceByteLength,
    string RecipeRelativePath,
    string RecipeSha256,
    string Outcome,
    string Message,
    double TotalMilliseconds,
    IReadOnlyList<TwoDIntegrationStepRecordDocument> Steps)
{
    public int SourceImageWidth { get; init; }
    public int SourceImageHeight { get; init; }
}

public sealed record TwoDIntegrationStepRecordDocument(
    int Index,
    string Name,
    string ToolType,
    string Status,
    bool ToolSuccess,
    bool AcceptancePassed,
    string Message,
    double ElapsedMilliseconds,
    IReadOnlyDictionary<string, double> Metrics)
{
    public IReadOnlyList<TwoDIntegrationOverlayDocument> Overlays { get; init; } = [];
}

public sealed record TwoDIntegrationOverlayDocument(
    string Kind,
    string Label,
    double BoundsX,
    double BoundsY,
    double BoundsWidth,
    double BoundsHeight,
    double CenterX,
    double CenterY,
    double StartX,
    double StartY,
    double EndX,
    double EndY,
    double Angle,
    int PointCount,
    IReadOnlyList<TwoDIntegrationOverlayPointDocument> Points);

public sealed record TwoDIntegrationOverlayPointDocument(double X, double Y);
