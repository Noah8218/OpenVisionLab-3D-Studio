using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// A recipe-owned, source-bound projection of one detected connected region.
/// The cells remain in source-grid coordinates so later tools can consume the
/// exact selected footprint without mutating or copying the source height field.
/// </summary>
public sealed record C3DEditableRegionArtifact(
    string SchemaVersion,
    string ArtifactId,
    string Name,
    string SourceConnectedRegionArtifactId,
    string SourceConnectedRegionContentSha256,
    string SourceEntityId,
    string SourceContentSha256,
    string RootSourceSha256,
    string MaskContentSha256,
    string Unit,
    string FrameId,
    int GridWidth,
    int GridHeight,
    string CoordinateConvention,
    double OriginX,
    double OriginY,
    double ColumnPitch,
    double RowPitch,
    string AreaUnit,
    int RegionIndex,
    C3DConnectedRegionArtifactRegion Region,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentCoordinateConvention =
        C3DConnectedRegionArtifact.CurrentCoordinateConvention;

    public IReadOnlyList<C3DConnectedRegionArtifactCell> Cells => Region.Cells;
    public C3DConnectedRegionArtifactBounding Bounding => new(
        Region.MinimumRow,
        Region.MinimumColumn,
        Region.MaximumRow,
        Region.MaximumColumn,
        OriginX + Region.MinimumColumn * ColumnPitch,
        OriginY + Region.MinimumRow * RowPitch,
        OriginX + Region.MaximumColumn * ColumnPitch,
        OriginY + Region.MaximumRow * RowPitch);

    public static C3DEditableRegionArtifact Create(
        string artifactId,
        string name,
        C3DConnectedRegionArtifact source,
        int regionIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);

        var region = source.Regions?.SingleOrDefault(candidate => candidate.Index == regionIndex)
            ?? throw new InvalidDataException(
                $"Connected-region artifact does not contain region index {regionIndex}.");
        var artifact = new C3DEditableRegionArtifact(
            CurrentSchemaVersion,
            artifactId.Trim(),
            name.Trim(),
            source.ArtifactId,
            source.ContentSha256,
            source.SourceEntityId,
            source.SourceContentSha256,
            source.RootSourceSha256,
            source.MaskContentSha256,
            source.Unit,
            source.FrameId,
            source.GridWidth,
            source.GridHeight,
            CurrentCoordinateConvention,
            source.OriginX,
            source.OriginY,
            source.ColumnPitch,
            source.RowPitch,
            source.AreaUnit,
            region.Index,
            new C3DConnectedRegionArtifactRegion(
                region.Index,
                region.SeedRow,
                region.SeedColumn,
                (region.Cells ?? [])
                    .Select(cell => new C3DConnectedRegionArtifactCell(cell.Row, cell.Column))
                    .ToArray(),
                region.MinimumRow,
                region.MinimumColumn,
                region.MaximumRow,
                region.MaximumColumn,
                region.Metrics),
            string.Empty);
        artifact = artifact with { ContentSha256 = CalculateContentSha256(artifact) };
        var validity = C3DEditableRegionArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Editable-region artifact is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    public static string CalculateContentSha256(C3DEditableRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DEditableRegionArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.ArtifactId ?? string.Empty);
            writer.Write(artifact.Name ?? string.Empty);
            writer.Write(artifact.SourceConnectedRegionArtifactId ?? string.Empty);
            writer.Write((artifact.SourceConnectedRegionContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(artifact.SourceEntityId ?? string.Empty);
            writer.Write((artifact.SourceContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write((artifact.RootSourceSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write((artifact.MaskContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(artifact.Unit ?? string.Empty);
            writer.Write(artifact.FrameId ?? string.Empty);
            writer.Write(artifact.GridWidth);
            writer.Write(artifact.GridHeight);
            writer.Write(artifact.CoordinateConvention ?? string.Empty);
            writer.Write(artifact.OriginX);
            writer.Write(artifact.OriginY);
            writer.Write(artifact.ColumnPitch);
            writer.Write(artifact.RowPitch);
            writer.Write(artifact.AreaUnit ?? string.Empty);
            writer.Write(artifact.RegionIndex);
            writer.Write(artifact.Region.SeedRow);
            writer.Write(artifact.Region.SeedColumn);
            writer.Write(artifact.Region.MinimumRow);
            writer.Write(artifact.Region.MinimumColumn);
            writer.Write(artifact.Region.MaximumRow);
            writer.Write(artifact.Region.MaximumColumn);
            writer.Write(artifact.Region.Cells.Count);
            foreach (var cell in artifact.Region.Cells)
            {
                writer.Write(cell.Row);
                writer.Write(cell.Column);
            }
            writer.Write(artifact.Region.Metrics is not null);
            if (artifact.Region.Metrics is { } metrics)
            {
                writer.Write(metrics.CellCount);
                writer.Write(metrics.Area);
                writer.Write(metrics.CenterX);
                writer.Write(metrics.CenterY);
                writer.Write(metrics.HasOrientation);
                writer.Write(metrics.OrientationDegrees);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record C3DEditableRegionArtifactValidityReport(
    string SchemaVersion,
    C3DEditableRegionArtifactValidityState State,
    int CellCount,
    bool SourceIdentityShapeValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public bool IsValid => State == C3DEditableRegionArtifactValidityState.Valid;
}

public enum C3DEditableRegionArtifactValidityState
{
    Valid,
    Invalid
}

public static class C3DEditableRegionArtifactValidator
{
    public static C3DEditableRegionArtifactValidityReport Inspect(
        C3DEditableRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var sourceIdentityShapeValid = IsSha256(artifact.SourceConnectedRegionContentSha256)
            && IsSha256(artifact.SourceContentSha256)
            && IsSha256(artifact.RootSourceSha256)
            && IsSha256(artifact.MaskContentSha256);
        if (!sourceIdentityShapeValid)
        {
            errors.Add("Editable-region source identities must be canonical SHA-256 values.");
        }

        if (artifact.SchemaVersion != C3DEditableRegionArtifact.CurrentSchemaVersion)
        {
            errors.Add($"Unsupported editable-region artifact schema '{artifact.SchemaVersion}'.");
        }
        RequireText(artifact.ArtifactId, "artifact ID", errors);
        RequireText(artifact.Name, "name", errors);
        RequireText(artifact.SourceConnectedRegionArtifactId, "source connected-region artifact ID", errors);
        RequireText(artifact.SourceEntityId, "source entity ID", errors);
        RequireText(artifact.Unit, "unit", errors);
        RequireText(artifact.FrameId, "frame ID", errors);
        RequireText(artifact.AreaUnit, "area unit", errors);
        if (artifact.GridWidth <= 0 || artifact.GridHeight <= 0)
        {
            errors.Add("Editable-region grid dimensions must be positive.");
        }
        if (artifact.CoordinateConvention != C3DEditableRegionArtifact.CurrentCoordinateConvention)
        {
            errors.Add("Editable-region coordinate convention is unsupported.");
        }
        if (!double.IsFinite(artifact.OriginX)
            || !double.IsFinite(artifact.OriginY)
            || !double.IsFinite(artifact.ColumnPitch)
            || !double.IsFinite(artifact.RowPitch)
            || artifact.ColumnPitch <= 0
            || artifact.RowPitch <= 0)
        {
            errors.Add("Editable-region origin and positive pitches must be finite.");
        }
        var region = artifact.Region;
        if (region is null)
        {
            errors.Add("Editable-region selected region is required.");
        }
        else
        {
            if (region.Index != artifact.RegionIndex)
            {
                errors.Add("Editable-region selected region index does not match the artifact index.");
            }
            var cells = region.Cells ?? [];
            if (cells.Count == 0)
            {
                errors.Add("Editable-region selected region must contain at least one cell.");
            }
            var seen = new HashSet<(int Row, int Column)>();
            var minimumRow = int.MaxValue;
            var minimumColumn = int.MaxValue;
            var maximumRow = int.MinValue;
            var maximumColumn = int.MinValue;
            foreach (var cell in cells)
            {
                if (cell is null
                    || cell.Row < 0 || cell.Row >= artifact.GridHeight
                    || cell.Column < 0 || cell.Column >= artifact.GridWidth)
                {
                    errors.Add("Editable-region selected cells must stay inside the source grid.");
                    continue;
                }
                if (!seen.Add((cell.Row, cell.Column)))
                {
                    errors.Add("Editable-region selected cells must be unique.");
                }
                minimumRow = Math.Min(minimumRow, cell.Row);
                minimumColumn = Math.Min(minimumColumn, cell.Column);
                maximumRow = Math.Max(maximumRow, cell.Row);
                maximumColumn = Math.Max(maximumColumn, cell.Column);
            }
            if (seen.Count > 0
                && (region.MinimumRow != minimumRow
                    || region.MinimumColumn != minimumColumn
                    || region.MaximumRow != maximumRow
                    || region.MaximumColumn != maximumColumn))
            {
                errors.Add("Editable-region bounds must match the selected cells.");
            }
        }

        var contentIdentityValid = IsSha256(artifact.ContentSha256)
            && string.Equals(
                artifact.ContentSha256,
                C3DEditableRegionArtifact.CalculateContentSha256(artifact),
                StringComparison.Ordinal);
        if (!contentIdentityValid)
        {
            errors.Add("Editable-region content identity is invalid.");
        }

        var state = errors.Count == 0
            ? C3DEditableRegionArtifactValidityState.Valid
            : C3DEditableRegionArtifactValidityState.Invalid;
        return new(
            artifact.SchemaVersion,
            state,
            artifact.Region?.Cells?.Count ?? 0,
            sourceIdentityShapeValid,
            contentIdentityValid,
            errors,
            state == C3DEditableRegionArtifactValidityState.Valid
                ? $"Editable region {artifact.RegionIndex} is source-bound with {artifact.Region?.Cells?.Count ?? 0:N0} exact cell(s)."
                : "Editable-region artifact failed closed validation.");
    }

    private static void RequireText(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Editable-region {label} is required.");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 }
        && value.All(Uri.IsHexDigit)
        && value == value.ToUpperInvariant();
}
