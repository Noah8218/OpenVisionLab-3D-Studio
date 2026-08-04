using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// WPF-neutral point used by the persisted SurfaceModel contract.
/// </summary>
public sealed record SurfaceModelPoint3(double X, double Y, double Z);

/// <summary>
/// Zero-based point indices for one oriented SurfaceModel triangle.
/// </summary>
public sealed record SurfaceModelTriangle(
    int FirstPointIndex,
    int SecondPointIndex,
    int ThirdPointIndex);

/// <summary>
/// One deterministic surface sample with an exact source-triangle locator.
/// </summary>
public sealed record SurfaceModelSample(
    int Order,
    int SourceTriangleIndex,
    SurfaceModelPoint3 Position,
    SurfaceModelPoint3 Normal);

/// <summary>
/// Explicit preparation parameters. Version 1 samples one centroid from each
/// deterministically selected triangle and never removes or repairs geometry.
/// </summary>
public sealed record SurfaceModelPreparationParameters(
    string SamplingPolicy,
    int MaximumSampleCount,
    double MinimumTriangleArea,
    double UnitNormalTolerance,
    double MinimumNormalAlignmentCosine)
{
    public const string DeterministicTriangleCentroidSampling =
        "deterministic-triangle-centroid-even-index-v1";
}

/// <summary>
/// Optional model-frame symmetry metadata for later pose-equivalence policy.
/// This declaration does not itself execute or alter matching.
/// </summary>
public sealed record SurfaceModelSymmetryDeclaration(
    string Kind,
    string Axis,
    int Order)
{
    public const string NoneKind = "none";
    public const string DiscreteRotationKind = "discrete-rotation";
    public const string NoAxis = "none";
    public const string XAxis = "x";
    public const string YAxis = "y";
    public const string ZAxis = "z";

    public static SurfaceModelSymmetryDeclaration None { get; } =
        new(NoneKind, NoAxis, 1);
}

public sealed record SurfaceModelSurfaceRemoval(
    int SourceTriangleIndex,
    string Reason,
    int? DuplicateOfSourceTriangleIndex);

/// <summary>
/// Persisted active-surface evidence. Source geometry remains intact while
/// downstream sampling, edge extraction, and overlays use the retained
/// source-triangle domain.
/// </summary>
public sealed record SurfaceModelSurfaceSelection(
    string Policy,
    int SourceTriangleCount,
    int[] ExplicitInternalSourceTriangleIndices,
    int[] ExplicitUnobservableSourceTriangleIndices,
    bool RemoveExactDuplicateTriangles,
    int[] RetainedSourceTriangleIndices,
    SurfaceModelSurfaceRemoval[] RemovedSurfaces)
{
    public const string ExactDuplicateAndExplicitExclusionPolicy =
        "exact-duplicate-and-explicit-source-triangle-exclusion-v1";
    public const string ExplicitInternalReason = "explicit-internal";
    public const string ExplicitUnobservableReason =
        "explicit-unobservable";
    public const string ExactDuplicateReason = "exact-duplicate";
}

/// <summary>
/// Identified, content-addressed nominal surface artifact. The source mesh,
/// prepared samples, unit, and coordinate frame remain explicit so downstream
/// matching cannot silently substitute unrelated geometry.
/// </summary>
public sealed record SurfaceModelArtifact(
    string SchemaVersion,
    string ArtifactId,
    string Name,
    string SourceEntityId,
    string SourceContentSha256,
    string SourceFormat,
    string Unit,
    string FrameId,
    string CoordinateConvention,
    SurfaceModelPreparationParameters Preparation,
    SurfaceModelPoint3[] Points,
    SurfaceModelTriangle[] Triangles,
    SurfaceModelPoint3[] Normals,
    SurfaceModelSample[] Samples,
    SurfaceModelSymmetryDeclaration? Symmetry,
    SurfaceModelSurfaceSelection? SurfaceSelection,
    string ContentSha256)
{
    public const string LegacySchemaVersion = "1.0";
    public const string SymmetrySchemaVersion = "1.1";
    public const string CurrentSchemaVersion = "1.2";
    public const string CurrentCoordinateConvention = "source-cartesian-xyz";

    public static SurfaceModelArtifact Create(
        string artifactId,
        string name,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceFormat,
        string unit,
        string frameId,
        SurfaceModelPreparationParameters preparation,
        IReadOnlyList<SurfaceModelPoint3> points,
        IReadOnlyList<SurfaceModelTriangle> triangles,
        IReadOnlyList<SurfaceModelPoint3> normals,
        IReadOnlyList<SurfaceModelSample> samples,
        SurfaceModelSymmetryDeclaration? symmetry = null,
        SurfaceModelSurfaceSelection? surfaceSelection = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(samples);

        var effectiveSymmetry = surfaceSelection is null
            ? symmetry
            : symmetry ?? SurfaceModelSymmetryDeclaration.None;
        var model = new SurfaceModelArtifact(
            surfaceSelection is not null
                ? CurrentSchemaVersion
                : effectiveSymmetry is null
                    ? LegacySchemaVersion
                    : SymmetrySchemaVersion,
            artifactId.Trim(),
            name.Trim(),
            sourceEntityId.Trim(),
            sourceContentSha256.Trim().ToUpperInvariant(),
            sourceFormat.Trim().ToUpperInvariant(),
            unit.Trim(),
            frameId.Trim(),
            CurrentCoordinateConvention,
            preparation,
            points.ToArray(),
            triangles.ToArray(),
            normals.ToArray(),
            samples.ToArray(),
            effectiveSymmetry,
            Copy(surfaceSelection),
            string.Empty);
        model = model with
        {
            ContentSha256 = CalculateContentSha256(model)
        };

        var validity = SurfaceModelArtifactValidator.Inspect(model);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"SurfaceModel is invalid: {string.Join(" ", validity.Errors)}");
        }

        return model;
    }

    public static string CalculateContentSha256(SurfaceModelArtifact model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(model.Preparation);
        ArgumentNullException.ThrowIfNull(model.Points);
        ArgumentNullException.ThrowIfNull(model.Triangles);
        ArgumentNullException.ThrowIfNull(model.Normals);
        ArgumentNullException.ThrowIfNull(model.Samples);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.SurfaceModel");
            writer.Write(model.SchemaVersion ?? string.Empty);
            writer.Write(model.ArtifactId ?? string.Empty);
            writer.Write(model.Name ?? string.Empty);
            writer.Write(model.SourceEntityId ?? string.Empty);
            writer.Write(
                (model.SourceContentSha256 ?? string.Empty)
                .ToUpperInvariant());
            writer.Write(
                (model.SourceFormat ?? string.Empty).ToUpperInvariant());
            writer.Write(model.Unit ?? string.Empty);
            writer.Write(model.FrameId ?? string.Empty);
            writer.Write(model.CoordinateConvention ?? string.Empty);
            writer.Write(model.Preparation.SamplingPolicy ?? string.Empty);
            writer.Write(model.Preparation.MaximumSampleCount);
            writer.Write(model.Preparation.MinimumTriangleArea);
            writer.Write(model.Preparation.UnitNormalTolerance);
            writer.Write(model.Preparation.MinimumNormalAlignmentCosine);
            if (model.SchemaVersion != LegacySchemaVersion)
            {
                writer.Write(model.Symmetry?.Kind ?? string.Empty);
                writer.Write(model.Symmetry?.Axis ?? string.Empty);
                writer.Write(model.Symmetry?.Order ?? 0);
            }

            if (model.SchemaVersion == CurrentSchemaVersion)
            {
                var selection = model.SurfaceSelection;
                writer.Write(selection?.Policy ?? string.Empty);
                writer.Write(selection?.SourceTriangleCount ?? 0);
                WriteIndices(
                    writer,
                    selection?.ExplicitInternalSourceTriangleIndices);
                WriteIndices(
                    writer,
                    selection?.ExplicitUnobservableSourceTriangleIndices);
                writer.Write(
                    selection?.RemoveExactDuplicateTriangles ?? false);
                WriteIndices(
                    writer,
                    selection?.RetainedSourceTriangleIndices);
                var removed = selection?.RemovedSurfaces;
                writer.Write(removed?.Length ?? 0);
                if (removed is not null)
                {
                    foreach (var item in removed)
                    {
                        writer.Write(item.SourceTriangleIndex);
                        writer.Write(item.Reason ?? string.Empty);
                        writer.Write(
                            item.DuplicateOfSourceTriangleIndex.HasValue);
                        if (item.DuplicateOfSourceTriangleIndex.HasValue)
                        {
                            writer.Write(
                                item.DuplicateOfSourceTriangleIndex.Value);
                        }
                    }
                }
            }

            writer.Write(model.Points.Length);
            foreach (var point in model.Points)
            {
                WritePoint(writer, point);
            }

            writer.Write(model.Triangles.Length);
            foreach (var triangle in model.Triangles)
            {
                writer.Write(triangle.FirstPointIndex);
                writer.Write(triangle.SecondPointIndex);
                writer.Write(triangle.ThirdPointIndex);
            }

            writer.Write(model.Normals.Length);
            foreach (var normal in model.Normals)
            {
                WritePoint(writer, normal);
            }

            writer.Write(model.Samples.Length);
            foreach (var sample in model.Samples)
            {
                writer.Write(sample.Order);
                writer.Write(sample.SourceTriangleIndex);
                WritePoint(writer, sample.Position);
                WritePoint(writer, sample.Normal);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WritePoint(
        BinaryWriter writer,
        SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.Z);
    }

    private static void WriteIndices(
        BinaryWriter writer,
        int[]? indices)
    {
        writer.Write(indices?.Length ?? 0);
        if (indices is not null)
        {
            foreach (var index in indices)
            {
                writer.Write(index);
            }
        }
    }

    private static SurfaceModelSurfaceSelection? Copy(
        SurfaceModelSurfaceSelection? selection) =>
        selection is null
            ? null
            : selection with
            {
                ExplicitInternalSourceTriangleIndices =
                    selection.ExplicitInternalSourceTriangleIndices.ToArray(),
                ExplicitUnobservableSourceTriangleIndices =
                    selection.ExplicitUnobservableSourceTriangleIndices.ToArray(),
                RetainedSourceTriangleIndices =
                    selection.RetainedSourceTriangleIndices.ToArray(),
                RemovedSurfaces = selection.RemovedSurfaces.ToArray()
            };
}

public static class SurfaceModelSurfaceDomain
{
    public static int[] GetRetainedSourceTriangleIndices(
        SurfaceModelArtifact model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.SurfaceSelection?.RetainedSourceTriangleIndices.ToArray()
               ?? Enumerable.Range(0, model.Triangles.Length).ToArray();
    }

    public static SurfaceModelTriangle[] GetRetainedTriangles(
        SurfaceModelArtifact model) =>
        GetRetainedSourceTriangleIndices(model)
            .Select(index => model.Triangles[index])
            .ToArray();
}

public static class SurfaceModelSampling
{
    public static int GetEvenTriangleIndex(
        int sampleOrder,
        int sampleCount,
        int triangleCount)
    {
        if (sampleCount <= 0
            || sampleCount > triangleCount
            || sampleOrder < 0
            || sampleOrder >= sampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleOrder),
                "SurfaceModel sample order/count must select a unique source triangle.");
        }

        return checked((int)(
            ((long)sampleOrder * 2L + 1L)
            * triangleCount
            / (sampleCount * 2L)));
    }
}
