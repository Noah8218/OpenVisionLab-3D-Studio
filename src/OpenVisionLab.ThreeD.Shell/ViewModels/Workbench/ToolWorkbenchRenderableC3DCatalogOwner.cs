using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the neutral, read-only catalog of C3D files that are safe to render.
/// The owner consumes an explicit snapshot of Artifact Projection, execution
/// output, and Validation Set state; it never calls a Preview, Publish, Run, or
/// Validation action and never mutates the supplied state.
/// </summary>
internal sealed class ToolWorkbenchRenderableC3DCatalogOwner
{
    private static readonly string[] PreparationToolOrder =
    [
        "filter",
        "remove-outlier-pixels",
        "domain-mask",
        "level-surface",
        "roi-crop"
    ];

    public IReadOnlyList<ToolWorkbenchRenderableC3DTarget> Targets { get; private set; } = [];

    public void Rebuild(ToolWorkbenchRenderableC3DCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var targets = new List<ToolWorkbenchRenderableC3DTarget>();
        if (TryProjectSource(snapshot) is { } source)
        {
            targets.Add(source);
        }

        var validationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in snapshot.ValidationSamples.OrderBy(sample => sample.Order))
        {
            var id = $"validation.sample.{sample.Order}";
            if (sample.Order <= 0
                || !validationIds.Add(id)
                || !IsC3DFile(sample.C3DPath))
            {
                continue;
            }

            targets.Add(new ToolWorkbenchRenderableC3DTarget(
                id,
                sample.DisplayName,
                sample.Contract,
                sample.State,
                sample.C3DPath,
                sample.Detail,
                true,
                true,
                null,
                null));
        }

        foreach (var toolId in PreparationToolOrder)
        {
            var matches = snapshot.PreparationOutputs
                .Where(output => string.Equals(output.ToolId, toolId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1 || TryProjectPreparation(snapshot, matches[0]) is not { } preparation)
            {
                continue;
            }

            targets.Add(preparation);
        }

        Targets = targets;
    }

    public ToolWorkbenchRenderableC3DTarget? GetTarget(string? id) =>
        Targets.FirstOrDefault(target =>
            string.Equals(target.Id, id, StringComparison.OrdinalIgnoreCase));

    private static ToolWorkbenchRenderableC3DTarget? TryProjectSource(
        ToolWorkbenchRenderableC3DCatalogSnapshot snapshot)
    {
        var source = snapshot.Source;
        var artifact = snapshot.Artifacts.FirstOrDefault(item =>
            string.Equals(item.Id, source.Id, StringComparison.OrdinalIgnoreCase));
        var binding = source.Binding;
        if (!source.IsReady
            || binding is null
            || !string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(binding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || artifact is null
            || artifact.NodeKind != "Source"
            || IsUnavailableState(artifact.State)
            || !artifact.HasContentHash
            || string.IsNullOrWhiteSpace(source.ContentSha256)
            || !string.Equals(source.ContentSha256, binding.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.RootSourceId, source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.Unit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(artifact.FrameId, source.FrameId, StringComparison.Ordinal)
            || !MatchesC3DFile(
                source.C3DPath,
                binding.GridWidth,
                binding.GridHeight,
                8L + (long)binding.GridWidth * binding.GridHeight * sizeof(float),
                source.ContentSha256))
        {
            return null;
        }

        return new ToolWorkbenchRenderableC3DTarget(
            source.Id,
            source.DisplayName,
            artifact.Contract,
            artifact.State,
            source.C3DPath,
            artifact.Detail,
            true,
            true,
            null,
            null);
    }

    private static ToolWorkbenchRenderableC3DTarget? TryProjectPreparation(
        ToolWorkbenchRenderableC3DCatalogSnapshot snapshot,
        ToolWorkbenchRenderableC3DPreparationSnapshot preparation)
    {
        var output = preparation.Output;
        var artifact = output is null
            ? null
            : snapshot.Artifacts.FirstOrDefault(item =>
                string.Equals(item.Id, output.EntityId, StringComparison.OrdinalIgnoreCase));
        if (!preparation.IsCurrent
            || preparation.IsStale
            || output is null
            || !output.IsDerived
            || artifact is null
            || artifact.PipelineStep is not { } step
            || !string.Equals(step.ToolId, preparation.ToolId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.OutputEntityId, output.EntityId, StringComparison.OrdinalIgnoreCase)
            || artifact.NodeKind is not ("HeightField" or "FilteredHeightField" or "LeveledHeightField")
            || IsUnavailableState(artifact.State)
            || !artifact.HasContentHash
            || !string.Equals(artifact.ContentSha256, output.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.RootSourceId, snapshot.Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.Unit, snapshot.Source.Unit, StringComparison.Ordinal)
            || !string.Equals(artifact.FrameId, snapshot.Source.FrameId, StringComparison.Ordinal)
            || !string.Equals(output.Unit, snapshot.Source.Unit, StringComparison.Ordinal)
            || !string.Equals(output.FrameId, snapshot.Source.FrameId, StringComparison.Ordinal)
            || !string.Equals(output.RootSourceSha256, snapshot.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !MatchesC3DFile(
                preparation.C3DPath,
                output.Width,
                output.Height,
                output.ByteLength,
                output.ContentSha256)
            || !QualityMatchesSource(artifact.PreparationQualityDelta, artifact, output, snapshot.Source))
        {
            return null;
        }

        return new ToolWorkbenchRenderableC3DTarget(
            artifact.Id,
            artifact.DisplayName,
            artifact.Contract,
            artifact.State,
            preparation.C3DPath!,
            artifact.Detail,
            false,
            true,
            artifact.PreparationQualityDelta,
            preparation.ToolId);
    }

    private static bool QualityMatchesSource(
        SourceQualityDelta? quality,
        ToolWorkbenchArtifactItem artifact,
        C3DHeightFieldSnapshot output,
        ToolWorkbenchRenderableC3DSourceSnapshot source)
    {
        if (quality is null)
        {
            return true;
        }

        return string.Equals(quality.SourceEntityId, source.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(quality.SourceContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(quality.DerivedEntityId, artifact.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(quality.DerivedContentSha256, output.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(quality.SourceRootSourceSha256, quality.DerivedRootSourceSha256, StringComparison.OrdinalIgnoreCase)
            && quality.SourceIdentityRetained;
    }

    private static bool IsUnavailableState(string? state) =>
        string.Equals(state, "Stale", StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, "Disabled", StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, "Source required", StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, "Needs repair", StringComparison.OrdinalIgnoreCase);

    private static bool IsC3DFile(string? path) =>
        TryInspectC3DFile(path, null, null, null, null);

    private static bool MatchesC3DFile(
        string? path,
        int expectedWidth,
        int expectedHeight,
        long expectedByteLength,
        string expectedContentSha256) =>
        TryInspectC3DFile(
            path,
            expectedWidth,
            expectedHeight,
            expectedByteLength,
            expectedContentSha256);

    private static bool TryInspectC3DFile(
        string? path,
        int? expectedWidth,
        int? expectedHeight,
        long? expectedByteLength,
        string? expectedContentSha256)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !string.Equals(Path.GetExtension(path), ".c3d", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            if (stream.Length < 8)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[8];
            stream.ReadExactly(header);
            var width = BinaryPrimitives.ReadInt32LittleEndian(header);
            var height = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
            if (width <= 0
                || height <= 0
                || stream.Length != 8L + (long)width * height * sizeof(float)
                || expectedWidth is not null && width != expectedWidth
                || expectedHeight is not null && height != expectedHeight
                || expectedByteLength is not null && stream.Length != expectedByteLength)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedContentSha256))
            {
                return true;
            }

            stream.Position = 0;
            var actualContentSha256 = Convert.ToHexString(SHA256.HashData(stream));
            return string.Equals(actualContentSha256, expectedContentSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed record ToolWorkbenchRenderableC3DCatalogSnapshot(
    ToolWorkbenchRenderableC3DSourceSnapshot Source,
    IReadOnlyList<ToolWorkbenchArtifactItem> Artifacts,
    IReadOnlyList<ToolWorkbenchRenderableC3DPreparationSnapshot> PreparationOutputs,
    IReadOnlyList<ToolWorkbenchRenderableC3DValidationSampleSnapshot> ValidationSamples);

internal sealed record ToolWorkbenchRenderableC3DSourceSnapshot(
    string Id,
    string DisplayName,
    string Format,
    string Unit,
    string FrameId,
    string C3DPath,
    string ContentSha256,
    string Detail,
    bool IsReady,
    ToolRecipeSelectionSourceBinding? Binding);

internal sealed record ToolWorkbenchRenderableC3DPreparationSnapshot(
    string ToolId,
    C3DHeightFieldSnapshot? Output,
    string? C3DPath,
    bool IsCurrent,
    bool IsStale,
    bool IsPublished);

internal sealed record ToolWorkbenchRenderableC3DValidationSampleSnapshot(
    int Order,
    string C3DPath,
    string DisplayName,
    string Contract,
    string State,
    string Detail);

internal sealed record ToolWorkbenchRenderableC3DTarget(
    string Id,
    string DisplayName,
    string Contract,
    string State,
    string C3DPath,
    string Detail,
    bool IsSource,
    bool IsDisplayable,
    SourceQualityDelta? PreparationQualityDelta,
    string? PreparationToolId);
