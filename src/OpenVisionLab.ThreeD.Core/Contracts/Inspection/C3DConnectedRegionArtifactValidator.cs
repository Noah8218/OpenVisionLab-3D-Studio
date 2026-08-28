namespace OpenVisionLab.ThreeD.Core;

public sealed record C3DConnectedRegionArtifactValidityReport(
    string SchemaVersion,
    C3DConnectedRegionArtifactValidityState State,
    int RegionCount,
    int CellCount,
    bool SourceIdentityShapeValid,
    bool MaskIdentityShapeValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public bool IsValid => State == C3DConnectedRegionArtifactValidityState.Valid;
}

public enum C3DConnectedRegionArtifactValidityState
{
    Valid,
    Invalid
}

/// <summary>
/// Fail-closed structural and content validation for a connected-region
/// artifact. Source and mask hashes are references; validating them here does
/// not silently reload or reinterpret external source bytes.
/// </summary>
public static class C3DConnectedRegionArtifactValidator
{
    public static C3DConnectedRegionArtifactValidityReport Inspect(
        C3DConnectedRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var errors = new List<string>();
        var regions = artifact.Regions ?? [];
        var cellCount = 0L;

        if (artifact.SchemaVersion != C3DConnectedRegionArtifact.CurrentSchemaVersion)
        {
            errors.Add($"Unsupported connected-region artifact schema '{artifact.SchemaVersion}'.");
        }

        RequireText(artifact.ArtifactId, "artifact ID", errors);
        RequireText(artifact.Name, "name", errors);
        RequireText(artifact.SourceEntityId, "source entity ID", errors);
        RequireText(artifact.Unit, "unit", errors);
        RequireText(artifact.FrameId, "frame ID", errors);
        RequireText(artifact.AreaUnit, "area unit", errors);

        var sourceIdentityShapeValid = IsCanonicalSha256(artifact.SourceContentSha256)
            && IsCanonicalSha256(artifact.RootSourceSha256);
        if (!sourceIdentityShapeValid)
        {
            errors.Add("Connected-region source content and root-source identities must be canonical SHA-256 values.");
        }

        var maskIdentityShapeValid = IsCanonicalSha256(artifact.MaskContentSha256);
        if (!maskIdentityShapeValid)
        {
            errors.Add("Connected-region mask identity must be a canonical SHA-256 value.");
        }

        if (artifact.GridWidth <= 0 || artifact.GridHeight <= 0)
        {
            errors.Add("Connected-region artifact grid dimensions must be positive.");
        }
        else
        {
            try
            {
                _ = checked(artifact.GridWidth * artifact.GridHeight);
            }
            catch (OverflowException)
            {
                errors.Add("Connected-region artifact grid dimensions overflow the cell index space.");
            }
        }

        if (artifact.Connectivity is not C3DConnectedRegionArtifact.FourConnectivity
            and not C3DConnectedRegionArtifact.EightConnectivity)
        {
            errors.Add("Connected-region connectivity must be Four or Eight.");
        }

        if (artifact.CoordinateConvention != C3DConnectedRegionArtifact.CurrentCoordinateConvention)
        {
            errors.Add("Connected-region coordinate convention is unsupported.");
        }

        if (!IsFinite(artifact.OriginX)
            || !IsFinite(artifact.OriginY)
            || !IsFinite(artifact.ColumnPitch)
            || !IsFinite(artifact.RowPitch)
            || artifact.ColumnPitch <= 0
            || artifact.RowPitch <= 0)
        {
            errors.Add("Connected-region coordinate origin and positive pitches must be finite.");
        }

        var expectedWidth = artifact.GridWidth;
        var expectedHeight = artifact.GridHeight;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            if (region is null)
            {
                errors.Add($"Connected-region region {regionIndex} is null.");
                continue;
            }

            if (region.Index != regionIndex)
            {
                errors.Add($"Connected-region region index {region.Index} is not ordered at position {regionIndex}.");
            }

            var cells = region.Cells ?? [];
            if (cells.Count == 0)
            {
                errors.Add($"Connected-region region {regionIndex} has no cells.");
            }

            if (region.SeedRow < 0
                || region.SeedRow >= expectedHeight
                || region.SeedColumn < 0
                || region.SeedColumn >= expectedWidth)
            {
                errors.Add($"Connected-region region {regionIndex} seed is outside the source grid.");
            }

            if (region.MinimumRow < 0
                || region.MinimumColumn < 0
                || region.MaximumRow < region.MinimumRow
                || region.MaximumColumn < region.MinimumColumn
                || region.MaximumRow >= expectedHeight
                || region.MaximumColumn >= expectedWidth)
            {
                errors.Add($"Connected-region region {regionIndex} bounds are outside the source grid.");
            }

            var seenCells = new HashSet<(int Row, int Column)>();
            var minimumRow = int.MaxValue;
            var minimumColumn = int.MaxValue;
            var maximumRow = int.MinValue;
            var maximumColumn = int.MinValue;
            var containsSeed = false;
            foreach (var cell in cells)
            {
                if (cell is null)
                {
                    errors.Add($"Connected-region region {regionIndex} contains a null cell.");
                    continue;
                }

                if (cell.Row < 0
                    || cell.Row >= expectedHeight
                    || cell.Column < 0
                    || cell.Column >= expectedWidth)
                {
                    errors.Add($"Connected-region region {regionIndex} contains a cell outside the source grid.");
                    continue;
                }

                if (!seenCells.Add((cell.Row, cell.Column)))
                {
                    errors.Add($"Connected-region region {regionIndex} contains a duplicate cell.");
                }

                minimumRow = Math.Min(minimumRow, cell.Row);
                minimumColumn = Math.Min(minimumColumn, cell.Column);
                maximumRow = Math.Max(maximumRow, cell.Row);
                maximumColumn = Math.Max(maximumColumn, cell.Column);
                containsSeed |= cell.Row == region.SeedRow
                    && cell.Column == region.SeedColumn;
            }

            if (cells.Count > 0
                && seenCells.Count > 0
                && (region.MinimumRow != minimumRow
                    || region.MinimumColumn != minimumColumn
                    || region.MaximumRow != maximumRow
                    || region.MaximumColumn != maximumColumn))
            {
                errors.Add($"Connected-region region {regionIndex} bounds do not match its cells.");
            }

            if (!containsSeed)
            {
                errors.Add($"Connected-region region {regionIndex} seed is not one of its cells.");
            }

            if (cellCount > int.MaxValue - seenCells.Count)
            {
                errors.Add("Connected-region artifact cell count exceeds the reportable range.");
                cellCount = int.MaxValue;
            }
            else
            {
                cellCount += seenCells.Count;
            }
            ValidateMetrics(region, regionIndex, errors);
        }

        var contentIdentityValid = false;
        if (IsCanonicalSha256(artifact.ContentSha256))
        {
            try
            {
                contentIdentityValid = string.Equals(
                    artifact.ContentSha256,
                    C3DConnectedRegionArtifact.CalculateContentSha256(artifact),
                    StringComparison.Ordinal);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      or InvalidDataException
                      or InvalidOperationException
                      or NullReferenceException
                      or OverflowException)
            {
                contentIdentityValid = false;
            }
        }

        if (!contentIdentityValid)
        {
            errors.Add("Connected-region artifact content identity is invalid.");
        }

        var state = errors.Count == 0
            ? C3DConnectedRegionArtifactValidityState.Valid
            : C3DConnectedRegionArtifactValidityState.Invalid;
        return new C3DConnectedRegionArtifactValidityReport(
            artifact.SchemaVersion ?? string.Empty,
            state,
            regions.Count,
            (int)Math.Min(cellCount, int.MaxValue),
            sourceIdentityShapeValid,
            maskIdentityShapeValid,
            contentIdentityValid,
            errors.ToArray(),
            state == C3DConnectedRegionArtifactValidityState.Valid
                ? "Typed connected-region cells, source/mask identities, geometry, and content identity are internally consistent; external source bytes are not reloaded."
                : "Connected-region artifact rejected fail-closed; no repair or source reinterpretation was performed.");
    }

    private static void ValidateMetrics(
        C3DConnectedRegionArtifactRegion region,
        int regionIndex,
        ICollection<string> errors)
    {
        var metrics = region.Metrics;
        if (metrics is null)
        {
            return;
        }

        if (metrics.CellCount != region.CellCount)
        {
            errors.Add($"Connected-region metrics cell count does not match region {regionIndex}.");
        }

        if (!IsFinite(metrics.Area)
            || metrics.Area < 0
            || !IsFinite(metrics.CenterX)
            || !IsFinite(metrics.CenterY)
            || metrics.Bounding is null)
        {
            errors.Add($"Connected-region metrics for region {regionIndex} are not finite.");
            return;
        }

        if (metrics.HasOrientation && !IsFinite(metrics.OrientationDegrees))
        {
            errors.Add($"Connected-region orientation for region {regionIndex} must be finite when present.");
        }

        var bounding = metrics.Bounding;
        if (bounding.MinimumRow != region.MinimumRow
            || bounding.MinimumColumn != region.MinimumColumn
            || bounding.MaximumRow != region.MaximumRow
            || bounding.MaximumColumn != region.MaximumColumn
            || !IsFinite(bounding.MinimumX)
            || !IsFinite(bounding.MinimumY)
            || !IsFinite(bounding.MaximumX)
            || !IsFinite(bounding.MaximumY)
            || bounding.MaximumX <= bounding.MinimumX
            || bounding.MaximumY <= bounding.MinimumY)
        {
            errors.Add($"Connected-region metric bounding for region {regionIndex} is inconsistent.");
        }
    }

    private static void RequireText(
        string? value,
        string name,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Connected-region {name} is required.");
        }
    }

    private static bool IsFinite(double value) => double.IsFinite(value);

    private static bool IsCanonicalSha256(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
