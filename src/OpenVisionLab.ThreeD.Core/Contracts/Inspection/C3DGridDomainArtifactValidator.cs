namespace OpenVisionLab.ThreeD.Core;

public sealed record C3DGridDomainArtifactValidityReport(
    string SchemaVersion,
    C3DGridDomainArtifactValidityState State,
    int CellCount,
    bool SourceIdentityShapeValid,
    bool SelectionIdentityShapeValid,
    bool MaskContentIdentityValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public bool IsValid => State == C3DGridDomainArtifactValidityState.Valid;
}

public enum C3DGridDomainArtifactValidityState
{
    Valid,
    Invalid
}

/// <summary>
/// Fail-closed validation for the authored source-grid domain contract. The
/// validator checks membership and lineage identities but does not reload the
/// source or infer physical coordinates.
/// </summary>
public static class C3DGridDomainArtifactValidator
{
    public static C3DGridDomainArtifactValidityReport Inspect(
        C3DGridDomainArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var errors = new List<string>();
        var cells = artifact.Cells ?? [];
        var sourceIdentityShapeValid = IsCanonicalSha256(artifact.SourceContentSha256)
            && IsCanonicalSha256(artifact.RootSourceSha256);
        if (!sourceIdentityShapeValid)
        {
            errors.Add("Grid-domain source content and root-source identities must be canonical SHA-256 values.");
        }

        var selectionIdentityShapeValid = IsCanonicalSha256(artifact.SelectionContentSha256);
        if (!selectionIdentityShapeValid)
        {
            errors.Add("Grid-domain selection identity must be a canonical SHA-256 value.");
        }

        if (artifact.SchemaVersion != C3DGridDomainArtifact.CurrentSchemaVersion)
        {
            errors.Add($"Unsupported grid-domain artifact schema '{artifact.SchemaVersion}'.");
        }

        RequireText(artifact.ArtifactId, "artifact ID", errors);
        RequireText(artifact.Name, "name", errors);
        RequireText(artifact.SourceEntityId, "source entity ID", errors);
        RequireText(artifact.SelectionId, "selection ID", errors);
        RequireText(artifact.Unit, "unit", errors);
        RequireText(artifact.FrameId, "frame ID", errors);
        if (artifact.CoordinateConvention != C3DGridDomainArtifact.CurrentCoordinateConvention)
        {
            errors.Add("Grid-domain coordinate convention is unsupported.");
        }

        var dimensionsValid = artifact.GridWidth > 0 && artifact.GridHeight > 0;
        if (!dimensionsValid)
        {
            errors.Add("Grid-domain artifact grid dimensions must be positive.");
        }
        else
        {
            try
            {
                _ = checked(artifact.GridWidth * artifact.GridHeight);
            }
            catch (OverflowException)
            {
                errors.Add("Grid-domain artifact grid dimensions overflow the cell index space.");
            }
        }

        var seen = new HashSet<(int Row, int Column)>();
        var previous = (-1, -1);
        foreach (var cell in cells)
        {
            if (cell is null)
            {
                errors.Add("Grid-domain artifact contains a null cell.");
                continue;
            }

            if (!dimensionsValid
                || cell.Row < 0 || cell.Row >= artifact.GridHeight
                || cell.Column < 0 || cell.Column >= artifact.GridWidth)
            {
                errors.Add($"Grid-domain cell ({cell.Row}, {cell.Column}) is outside the source grid.");
                continue;
            }

            if (!seen.Add((cell.Row, cell.Column)))
            {
                errors.Add($"Grid-domain artifact contains duplicate cell ({cell.Row}, {cell.Column}).");
            }

            if (previous.Item1 > cell.Row
                || previous.Item1 == cell.Row && previous.Item2 >= cell.Column)
            {
                errors.Add("Grid-domain cells must be unique and ordered row-major.");
            }

            previous = (cell.Row, cell.Column);
        }

        if (cells.Count == 0)
        {
            errors.Add("Grid-domain artifact must contain at least one covered cell.");
        }

        var maskContentIdentityValid = false;
        if (IsCanonicalSha256(artifact.MaskContentSha256) && dimensionsValid)
        {
            try
            {
                maskContentIdentityValid = string.Equals(
                    artifact.MaskContentSha256,
                    C3DGridDomainArtifact.CalculateMaskContentSha256(artifact),
                    StringComparison.Ordinal);
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                maskContentIdentityValid = false;
            }
        }

        if (!maskContentIdentityValid)
        {
            errors.Add("Grid-domain mask content identity is invalid.");
        }

        var contentIdentityValid = false;
        if (IsCanonicalSha256(artifact.ContentSha256))
        {
            try
            {
                contentIdentityValid = string.Equals(
                    artifact.ContentSha256,
                    C3DGridDomainArtifact.CalculateContentSha256(artifact),
                    StringComparison.Ordinal);
            }
            catch (Exception exception)
                when (exception is ArgumentException or InvalidDataException or OverflowException)
            {
                contentIdentityValid = false;
            }
        }

        if (!contentIdentityValid)
        {
            errors.Add("Grid-domain artifact content identity is invalid.");
        }

        var state = errors.Count == 0
            ? C3DGridDomainArtifactValidityState.Valid
            : C3DGridDomainArtifactValidityState.Invalid;
        return new(
            artifact.SchemaVersion ?? string.Empty,
            state,
            seen.Count,
            sourceIdentityShapeValid,
            selectionIdentityShapeValid,
            maskContentIdentityValid,
            contentIdentityValid,
            errors.ToArray(),
            state == C3DGridDomainArtifactValidityState.Valid
                ? "Authored row-major domain cells, source/selection lineage, mask identity, and artifact content identity are internally consistent; external source bytes are not reloaded."
                : "Grid-domain artifact rejected fail-closed; no source reinterpretation or membership repair was performed.");
    }

    private static void RequireText(string? value, string name, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Grid-domain {name} is required.");
        }
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
