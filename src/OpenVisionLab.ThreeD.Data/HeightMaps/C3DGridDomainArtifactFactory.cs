using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Maps one validated recipe-owned GridPolygon to the source-bound authored
/// domain artifact. Rasterization remains Core-owned; this factory only adds
/// source lineage and the typed artifact identity.
/// </summary>
public static class C3DGridDomainArtifactFactory
{
    public static C3DGridDomainArtifact Create(
        string artifactId,
        string name,
        C3DHeightFieldSnapshot source,
        ToolRecipeSelection selection)
        => Create(artifactId, name, source, selection, CancellationToken.None);

    public static C3DGridDomainArtifact Create(
        string artifactId,
        string name,
        C3DHeightFieldSnapshot source,
        ToolRecipeSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridPolygon, StringComparison.Ordinal)
            || selection.GridPolygon is null)
        {
            throw new InvalidDataException(
                $"Selection '{selection.Id}' must contain one GridPolygon payload.");
        }

        ValidateSelectionBinding(source, selection);
        var rasterization = ToolRecipeGridPolygonRasterizer.Rasterize(
            selection.GridPolygon,
            source.Width,
            source.Height,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!rasterization.IsValid)
        {
            throw new InvalidDataException(
                $"GridPolygon rasterization failed: {string.Join(" ", rasterization.Errors)}");
        }
        if (rasterization.Cells.Count == 0)
        {
            throw new InvalidDataException(
                "GridPolygon domain must cover at least one source-grid cell center.");
        }

        var cells = new C3DGridDomainArtifactCell[rasterization.Cells.Count];
        for (var index = 0; index < cells.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cell = rasterization.Cells[index];
            cells[index] = new C3DGridDomainArtifactCell(cell.Row, cell.Column);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var artifact = C3DGridDomainArtifact.Create(
            artifactId,
            name,
            source.EntityId,
            source.ContentSha256,
            source.RootSourceSha256,
            selection.Id,
            C3DGridDomainArtifact.CalculateSelectionContentSha256(selection),
            source.Unit,
            source.FrameId,
            source.Width,
            source.Height,
            cells);
        cancellationToken.ThrowIfCancellationRequested();
        return artifact;
    }

    private static void ValidateSelectionBinding(
        C3DHeightFieldSnapshot source,
        ToolRecipeSelection selection)
    {
        if (!source.IsDerived
            && !string.Equals(selection.RootSourceId, source.EntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"GridPolygon selection '{selection.Id}' root source does not match '{source.EntityId}'.");
        }

        if (source.IsDerived && string.IsNullOrWhiteSpace(selection.RootSourceId))
        {
            throw new InvalidDataException(
                $"GridPolygon selection '{selection.Id}' derived source root identity is missing.");
        }

        var binding = selection.SourceBinding
            ?? throw new InvalidDataException(
                $"GridPolygon selection '{selection.Id}' source binding is required.");
        if (!string.Equals(binding.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || binding.GridWidth != source.Width
            || binding.GridHeight != source.Height
            || !string.Equals(selection.FrameId, source.FrameId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"GridPolygon selection '{selection.Id}' is stale for source '{source.EntityId}'.");
        }

        if (source.IsDerived)
        {
            if (!string.Equals(binding.Format, "HeightField", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(binding.OwnerEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(binding.RootSourceContentSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(binding.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(binding.FrameId, source.FrameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"GridPolygon selection '{selection.Id}' derived HeightField binding is stale.");
            }
        }
        else if (!string.Equals(binding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.RootSourceId, source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(binding.OwnerEntityId)
            || !string.IsNullOrWhiteSpace(binding.RootSourceContentSha256)
            || !string.IsNullOrWhiteSpace(binding.Unit)
            || !string.IsNullOrWhiteSpace(binding.FrameId))
        {
            throw new InvalidDataException(
                $"GridPolygon selection '{selection.Id}' raw C3D binding is not source-bound.");
        }
    }
}
