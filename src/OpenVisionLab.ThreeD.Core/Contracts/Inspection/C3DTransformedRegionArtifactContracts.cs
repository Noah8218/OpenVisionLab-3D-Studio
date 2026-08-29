using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// One connected-region source-grid cell and its optional transformed point.
/// A null point preserves a missing raw-height sample without inventing a
/// coordinate.
/// </summary>
public sealed record C3DTransformedRegionCell(
    int Row,
    int Column,
    double? RawHeight,
    double? X,
    double? Y,
    double? Z)
{
    public bool HasFinitePoint =>
        RawHeight is { } rawHeight
        && double.IsFinite(rawHeight)
        && X is { } x
        && double.IsFinite(x)
        && Y is { } y
        && double.IsFinite(y)
        && Z is { } z
        && double.IsFinite(z);
}

/// <summary>
/// Immutable relationship between one typed connected-region artifact, its
/// exact source grid, and a published full-XYZ affine transform. The cell
/// membership stays in source-grid coordinates while finite cells carry the
/// corresponding reference-frame point.
/// </summary>
public sealed record C3DTransformedRegionArtifact(
    string SchemaVersion,
    string OutputEntityId,
    string SourceRegionArtifactId,
    string SourceRegionContentSha256,
    int SourceRegionIndex,
    int SourceRegionSeedRow,
    int SourceRegionSeedColumn,
    int SourceRegionMinimumRow,
    int SourceRegionMinimumColumn,
    int SourceRegionMaximumRow,
    int SourceRegionMaximumColumn,
    string SourceEntityId,
    string SourceContentSha256,
    string RootSourceSha256,
    string SourceUnit,
    string SourceFrameId,
    string SourceCoordinateConvention,
    string RegionCoordinateConvention,
    int SourceGridWidth,
    int SourceGridHeight,
    string TransformEntityId,
    string TransformContentSha256,
    string ReferenceFrameId,
    string ReferenceUnit,
    string ReferenceProvenance,
    string ReferenceRevision,
    string TransformPolicy,
    string MissingValuePolicy,
    IReadOnlyList<C3DTransformedRegionCell> Cells,
    string Provenance,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string TransformPolicyName =
        "SourceGridColumnRawHeightRowToReferenceFrame";
    public const string MissingValuePolicyName =
        "PreserveRegionCellsWithoutTransformedPoint";

    public int CellCount => Cells?.Count ?? 0;
    public int FiniteCellCount => Cells?.Count(cell => cell.HasFinitePoint) ?? 0;
    public int MissingCellCount => CellCount - FiniteCellCount;

    public static C3DTransformedRegionArtifact Create(
        string outputEntityId,
        C3DConnectedRegionArtifact sourceRegionArtifact,
        int sourceRegionIndex,
        string sourceEntityId,
        string sourceContentSha256,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string sourceCoordinateConvention,
        int sourceGridWidth,
        int sourceGridHeight,
        C3DAffineTransform3D transform,
        IReadOnlyList<C3DTransformedRegionCell> cells,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(sourceRegionArtifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCoordinateConvention);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var regionValidity = C3DConnectedRegionArtifactValidator.Inspect(sourceRegionArtifact);
        if (!regionValidity.IsValid)
        {
            throw new InvalidDataException(
                "Transformed region requires a valid ConnectedRegionArtifact: "
                + string.Join(" ", regionValidity.Errors));
        }

        if (sourceRegionIndex < 0
            || sourceRegionIndex >= sourceRegionArtifact.Regions.Count)
        {
            throw new InvalidDataException(
                $"Connected-region index {sourceRegionIndex} is outside the source artifact.");
        }

        var region = sourceRegionArtifact.Regions[sourceRegionIndex];
        if (!string.Equals(sourceEntityId, sourceRegionArtifact.SourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sourceContentSha256, sourceRegionArtifact.SourceContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rootSourceSha256, sourceRegionArtifact.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sourceUnit, sourceRegionArtifact.Unit, StringComparison.Ordinal)
            || !string.Equals(sourceFrameId, sourceRegionArtifact.FrameId, StringComparison.Ordinal)
            || sourceGridWidth != sourceRegionArtifact.GridWidth
            || sourceGridHeight != sourceRegionArtifact.GridHeight
            || !string.Equals(transform.RootSourceEntityId, sourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(transform.RootSourceSha256, sourceRegionArtifact.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(transform.SourceUnit, sourceUnit, StringComparison.Ordinal)
            || !string.Equals(transform.SourceFrameId, sourceFrameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Transformed region source and affine-transform identities are inconsistent.");
        }

        if (string.Equals(outputEntityId, sourceRegionArtifact.ArtifactId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputEntityId, transform.OutputEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputEntityId, sourceEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Transformed region output ID must differ from every input identity.");
        }

        var copiedCells = cells
            .Select(cell => cell with { })
            .ToArray();
        if (copiedCells.Length != region.Cells.Count)
        {
            throw new InvalidDataException(
                "Transformed region must preserve every source-region cell exactly once.");
        }

        for (var index = 0; index < copiedCells.Length; index++)
        {
            var cell = copiedCells[index];
            var expected = region.Cells[index];
            if (cell is null
                || cell.Row != expected.Row
                || cell.Column != expected.Column)
            {
                throw new InvalidDataException(
                    "Transformed region cells must preserve source-region order and coordinates.");
            }

            if (cell.Row < 0 || cell.Row >= sourceGridHeight
                || cell.Column < 0 || cell.Column >= sourceGridWidth)
            {
                throw new InvalidDataException(
                    "Transformed region cell is outside the source grid.");
            }

            var hasRaw = cell.RawHeight.HasValue;
            var hasPoint = cell.X.HasValue || cell.Y.HasValue || cell.Z.HasValue;
            if (hasRaw != (cell.X.HasValue && cell.Y.HasValue && cell.Z.HasValue))
            {
                throw new InvalidDataException(
                    "Transformed region cells must contain either a complete finite point or no point.");
            }

            if (hasRaw
                && (!cell.HasFinitePoint
                    || cell.RawHeight == 0d))
            {
                throw new InvalidDataException(
                    "Transformed region finite cells must contain finite non-zero C3D heights and coordinates.");
            }

            if (!hasRaw && hasPoint)
            {
                throw new InvalidDataException(
                    "Transformed region missing cells must not contain partial coordinates.");
            }
        }

        var artifact = new C3DTransformedRegionArtifact(
            CurrentSchemaVersion,
            outputEntityId.Trim(),
            sourceRegionArtifact.ArtifactId,
            sourceRegionArtifact.ContentSha256.ToUpperInvariant(),
            region.Index,
            region.SeedRow,
            region.SeedColumn,
            region.MinimumRow,
            region.MinimumColumn,
            region.MaximumRow,
            region.MaximumColumn,
            sourceEntityId.Trim(),
            sourceContentSha256.Trim().ToUpperInvariant(),
            rootSourceSha256.Trim().ToUpperInvariant(),
            sourceUnit.Trim(),
            sourceFrameId.Trim(),
            sourceCoordinateConvention.Trim(),
            sourceRegionArtifact.CoordinateConvention,
            sourceGridWidth,
            sourceGridHeight,
            transform.OutputEntityId,
            transform.ContentSha256.ToUpperInvariant(),
            transform.ReferenceFrameId,
            transform.ReferenceUnit,
            transform.ReferenceProvenance,
            transform.ReferenceRevision,
            TransformPolicyName,
            MissingValuePolicyName,
            copiedCells,
            provenance.Trim(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };

        var validity = C3DTransformedRegionArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Transformed region artifact is invalid: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        C3DTransformedRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DTransformedRegionArtifact");
        writer.Write(artifact.SchemaVersion ?? string.Empty);
        writer.Write(artifact.OutputEntityId ?? string.Empty);
        writer.Write(artifact.SourceRegionArtifactId ?? string.Empty);
        writer.Write((artifact.SourceRegionContentSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write(artifact.SourceRegionIndex);
        writer.Write(artifact.SourceRegionSeedRow);
        writer.Write(artifact.SourceRegionSeedColumn);
        writer.Write(artifact.SourceRegionMinimumRow);
        writer.Write(artifact.SourceRegionMinimumColumn);
        writer.Write(artifact.SourceRegionMaximumRow);
        writer.Write(artifact.SourceRegionMaximumColumn);
        writer.Write(artifact.SourceEntityId ?? string.Empty);
        writer.Write((artifact.SourceContentSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write((artifact.RootSourceSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write(artifact.SourceUnit ?? string.Empty);
        writer.Write(artifact.SourceFrameId ?? string.Empty);
        writer.Write(artifact.SourceCoordinateConvention ?? string.Empty);
        writer.Write(artifact.RegionCoordinateConvention ?? string.Empty);
        writer.Write(artifact.SourceGridWidth);
        writer.Write(artifact.SourceGridHeight);
        writer.Write(artifact.TransformEntityId ?? string.Empty);
        writer.Write((artifact.TransformContentSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write(artifact.ReferenceFrameId ?? string.Empty);
        writer.Write(artifact.ReferenceUnit ?? string.Empty);
        writer.Write(artifact.ReferenceProvenance ?? string.Empty);
        writer.Write(artifact.ReferenceRevision ?? string.Empty);
        writer.Write(artifact.TransformPolicy ?? string.Empty);
        writer.Write(artifact.MissingValuePolicy ?? string.Empty);
        var cells = artifact.Cells ?? [];
        writer.Write(cells.Count);
        foreach (var cell in cells)
        {
            ArgumentNullException.ThrowIfNull(cell);
            writer.Write(cell.Row);
            writer.Write(cell.Column);
            WriteNullable(writer, cell.RawHeight);
            WriteNullable(writer, cell.X);
            WriteNullable(writer, cell.Y);
            WriteNullable(writer, cell.Z);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteNullable(BinaryWriter writer, double? value)
    {
        writer.Write(value.HasValue);
        if (value is { } number)
        {
            writer.Write(number);
        }
    }
}

public sealed record C3DTransformedRegionArtifactValidityReport(
    string SchemaVersion,
    C3DTransformedRegionArtifactValidityState State,
    int CellCount,
    int FiniteCellCount,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public bool IsValid => State == C3DTransformedRegionArtifactValidityState.Valid;
}

public enum C3DTransformedRegionArtifactValidityState
{
    Valid,
    Invalid
}

public static class C3DTransformedRegionArtifactValidator
{
    public static C3DTransformedRegionArtifactValidityReport Inspect(
        C3DTransformedRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        if (artifact.SchemaVersion != C3DTransformedRegionArtifact.CurrentSchemaVersion)
        {
            errors.Add($"Unsupported transformed-region artifact schema '{artifact.SchemaVersion}'.");
        }

        RequireText(artifact.OutputEntityId, "output entity ID", errors);
        RequireText(artifact.SourceRegionArtifactId, "source region artifact ID", errors);
        RequireText(artifact.SourceEntityId, "source entity ID", errors);
        RequireText(artifact.SourceUnit, "source unit", errors);
        RequireText(artifact.SourceFrameId, "source frame ID", errors);
        RequireText(artifact.SourceCoordinateConvention, "source coordinate convention", errors);
        RequireText(artifact.RegionCoordinateConvention, "region coordinate convention", errors);
        RequireText(artifact.TransformEntityId, "transform entity ID", errors);
        RequireText(artifact.ReferenceFrameId, "reference frame ID", errors);
        RequireText(artifact.ReferenceUnit, "reference unit", errors);
        RequireText(artifact.ReferenceProvenance, "reference provenance", errors);
        RequireText(artifact.ReferenceRevision, "reference revision", errors);
        RequireText(artifact.TransformPolicy, "transform policy", errors);
        RequireText(artifact.MissingValuePolicy, "missing-value policy", errors);
        RequireText(artifact.Provenance, "provenance", errors);

        if (!IsCanonicalSha256(artifact.SourceRegionContentSha256))
        {
            errors.Add("Transformed-region source artifact identity must be a canonical SHA-256 value.");
        }
        if (!IsCanonicalSha256(artifact.SourceContentSha256)
            || !IsCanonicalSha256(artifact.RootSourceSha256)
            || !IsCanonicalSha256(artifact.TransformContentSha256))
        {
            errors.Add("Transformed-region source and transform identities must be canonical SHA-256 values.");
        }

        if (artifact.SourceRegionIndex < 0
            || artifact.SourceGridWidth <= 0
            || artifact.SourceGridHeight <= 0
            || artifact.SourceRegionSeedRow < 0
            || artifact.SourceRegionSeedRow >= artifact.SourceGridHeight
            || artifact.SourceRegionSeedColumn < 0
            || artifact.SourceRegionSeedColumn >= artifact.SourceGridWidth
            || artifact.SourceRegionMinimumRow < 0
            || artifact.SourceRegionMinimumColumn < 0
            || artifact.SourceRegionMaximumRow < artifact.SourceRegionMinimumRow
            || artifact.SourceRegionMaximumColumn < artifact.SourceRegionMinimumColumn
            || artifact.SourceRegionMaximumRow >= artifact.SourceGridHeight
            || artifact.SourceRegionMaximumColumn >= artifact.SourceGridWidth)
        {
            errors.Add("Transformed-region source bounds or grid dimensions are invalid.");
        }

        var cells = artifact.Cells ?? [];
        var seen = new HashSet<(int Row, int Column)>();
        foreach (var cell in cells)
        {
            if (cell is null)
            {
                errors.Add("Transformed-region cells cannot be null.");
                continue;
            }

            if (cell.Row < 0 || cell.Row >= artifact.SourceGridHeight
                || cell.Column < 0 || cell.Column >= artifact.SourceGridWidth)
            {
                errors.Add("Transformed-region cell is outside the source grid.");
            }
            if (!seen.Add((cell.Row, cell.Column)))
            {
                errors.Add("Transformed-region cells must be unique.");
            }

            var complete = cell.RawHeight.HasValue
                && cell.X.HasValue
                && cell.Y.HasValue
                && cell.Z.HasValue;
            var empty = !cell.RawHeight.HasValue
                && !cell.X.HasValue
                && !cell.Y.HasValue
                && !cell.Z.HasValue;
            if (!complete && !empty)
            {
                errors.Add("Transformed-region cells must be complete or empty.");
            }
            if (complete && !cell.HasFinitePoint)
            {
                errors.Add("Transformed-region finite cells must contain finite values.");
            }
        }

        var contentIdentityValid = IsCanonicalSha256(artifact.ContentSha256)
            && string.Equals(
                artifact.ContentSha256,
                C3DTransformedRegionArtifact.CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        if (!contentIdentityValid)
        {
            errors.Add("Transformed-region artifact content identity is invalid.");
        }

        var state = errors.Count == 0
            ? C3DTransformedRegionArtifactValidityState.Valid
            : C3DTransformedRegionArtifactValidityState.Invalid;
        return new C3DTransformedRegionArtifactValidityReport(
            artifact.SchemaVersion ?? string.Empty,
            state,
            cells.Count,
            cells.Count(cell => cell?.HasFinitePoint == true),
            contentIdentityValid,
            errors.ToArray(),
            state == C3DTransformedRegionArtifactValidityState.Valid
                ? "Typed source-region membership, transformed points, identities, and content hash are internally consistent."
                : "Transformed-region artifact rejected fail-closed; no source or transform was reinterpreted.");
    }

    private static void RequireText(
        string? value,
        string name,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Transformed-region {name} is required.");
        }
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
