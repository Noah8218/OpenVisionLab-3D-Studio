using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record SurfaceModelPreparationRequest(
    string ArtifactId,
    string Name,
    string SourceEntityId,
    string SourceContentSha256,
    string Unit,
    string FrameId,
    SurfaceModelPreparationParameters Parameters,
    SurfaceModelSymmetryDeclaration? Symmetry = null,
    SurfaceModelSurfaceSelectionRequest? SurfaceSelection = null);

public sealed record SurfaceModelSurfaceSelectionRequest(
    int[] ExplicitInternalSourceTriangleIndices,
    int[] ExplicitUnobservableSourceTriangleIndices,
    bool RemoveExactDuplicateTriangles);

/// <summary>
/// Strict product adapter from an imported triangle mesh to an identified
/// SurfaceModel. Library-Noah owns deterministic sampling, centroids, and
/// sample-normal calculation. Studio preserves source identity and artifacts.
/// </summary>
public static class SurfaceModelPreparation
{
    public static SurfaceModelArtifact Prepare(
        ImportedMesh mesh,
        SurfaceModelPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Parameters);

        if (request.Parameters.SamplingPolicy
            != SurfaceModelPreparationParameters
                .DeterministicTriangleCentroidSampling)
        {
            throw new InvalidDataException(
                "SurfaceModel preparation sampling policy is unsupported.");
        }

        var normalQuality = ImportedMeshNormalQualityAnalyzer.Create(
            mesh,
            request.Parameters.UnitNormalTolerance,
            request.Parameters.MinimumNormalAlignmentCosine);
        if (!normalQuality.IsUsable)
        {
            throw new InvalidDataException(
                $"SurfaceModel preparation requires dense, valid declared normals. "
                + normalQuality.Evidence);
        }

        var points = mesh.Positions
            .Select(ToPoint)
            .ToArray();
        var normals = mesh.Normals
            .Select(ToPoint)
            .ToArray();
        var triangles = new SurfaceModelTriangle[mesh.TriangleCount];
        for (var triangleIndex = 0;
             triangleIndex < mesh.TriangleCount;
             triangleIndex++)
        {
            var offset = triangleIndex * 3;
            triangles[triangleIndex] = new SurfaceModelTriangle(
                mesh.Indices[offset],
                mesh.Indices[offset + 1],
                mesh.Indices[offset + 2]);
        }

        var noahPoints = points
            .Select(LibraryNoahSurfaceMatching.Point)
            .ToArray();
        var noahTriangles = triangles
            .Select(triangle => new SurfaceModelTriangleInput(
                triangle.FirstPointIndex,
                triangle.SecondPointIndex,
                triangle.ThirdPointIndex))
            .ToArray();
        var retainedSourceTriangleIndices = Enumerable
            .Range(0, triangles.Length)
            .ToArray();
        SurfaceModelSurfaceSelection? surfaceSelection = null;
        if (request.SurfaceSelection is { } selectionRequest)
        {
            var selectionResult =
                new DeterministicModelSurfaceSelectionTool().Execute(
                    noahPoints,
                    noahTriangles,
                    new DeterministicModelSurfaceSelectionOptions
                    {
                        ExplicitInternalSourceTriangleIndices =
                            selectionRequest
                                .ExplicitInternalSourceTriangleIndices,
                        ExplicitUnobservableSourceTriangleIndices =
                            selectionRequest
                                .ExplicitUnobservableSourceTriangleIndices,
                        RemoveExactDuplicateTriangles =
                            selectionRequest.RemoveExactDuplicateTriangles
                    });
            if (!selectionResult.Success)
            {
                throw new InvalidDataException(selectionResult.Message);
            }

            retainedSourceTriangleIndices = selectionResult
                .RetainedSourceTriangleIndices
                .ToArray();
            surfaceSelection = new SurfaceModelSurfaceSelection(
                SurfaceModelSurfaceSelection
                    .ExactDuplicateAndExplicitExclusionPolicy,
                triangles.Length,
                selectionResult.ExplicitInternalSourceTriangleIndices
                    .ToArray(),
                selectionResult.ExplicitUnobservableSourceTriangleIndices
                    .ToArray(),
                selectionRequest.RemoveExactDuplicateTriangles,
                retainedSourceTriangleIndices.ToArray(),
                selectionResult.RemovedSurfaces
                    .Select(item => new SurfaceModelSurfaceRemoval(
                        item.SourceTriangleIndex,
                        RemovalReason(item.Reason),
                        item.DuplicateOfSourceTriangleIndex))
                    .ToArray());
        }

        var noahResult = new DeterministicSurfaceModelPreparationTool()
            .Execute(
                noahPoints,
                retainedSourceTriangleIndices
                    .Select(index => noahTriangles[index])
                    .ToArray(),
                normals.Select(LibraryNoahSurfaceMatching.Point).ToArray(),
                new DeterministicSurfaceModelPreparationOptions
                {
                    MaximumSampleCount =
                        request.Parameters.MaximumSampleCount
                });
        if (!noahResult.Success)
        {
            throw new InvalidDataException(noahResult.Message);
        }

        var samples = noahResult.Samples
            .Select(sample => new SurfaceModelSample(
                sample.Order,
                retainedSourceTriangleIndices[
                    sample.SourceTriangleIndex],
                ToPoint(sample.Position),
                ToPoint(sample.Normal)))
            .ToArray();

        return SurfaceModelArtifact.Create(
            request.ArtifactId,
            request.Name,
            request.SourceEntityId,
            request.SourceContentSha256,
            mesh.Format,
            request.Unit,
            request.FrameId,
            request.Parameters,
            points,
            triangles,
            normals,
            samples,
            request.Symmetry,
            surfaceSelection);
    }

    private static SurfaceModelPoint3 ToPoint(System.Numerics.Vector3 value) =>
        new(value.X, value.Y, value.Z);

    private static SurfaceModelPoint3 ToPoint(ThreeDPoint value) =>
        new(value.X, value.Y, value.Z);

    private static string RemovalReason(
        ModelSurfaceRemovalReason reason) =>
        reason switch
        {
            ModelSurfaceRemovalReason.ExplicitInternal =>
                SurfaceModelSurfaceSelection.ExplicitInternalReason,
            ModelSurfaceRemovalReason.ExplicitUnobservable =>
                SurfaceModelSurfaceSelection
                    .ExplicitUnobservableReason,
            ModelSurfaceRemovalReason.ExactDuplicate =>
                SurfaceModelSurfaceSelection.ExactDuplicateReason,
            _ => throw new InvalidDataException(
                "Library-Noah returned an unsupported model-surface removal reason.")
        };
}
