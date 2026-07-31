using System.Numerics;
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
    SurfaceModelPreparationParameters Parameters);

/// <summary>
/// Pure, deterministic conversion from an imported triangle mesh to an
/// identified SurfaceModel. Version 1 preserves all source geometry and
/// declared normals; removal of redundant/internal surfaces belongs to J-05.
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

        var sampleCount = Math.Min(
            request.Parameters.MaximumSampleCount,
            triangles.Length);
        var samples = new SurfaceModelSample[sampleCount];
        for (var sampleOrder = 0;
             sampleOrder < sampleCount;
             sampleOrder++)
        {
            var triangleIndex = SurfaceModelSampling.GetEvenTriangleIndex(
                sampleOrder,
                sampleCount,
                triangles.Length);
            var triangle = triangles[triangleIndex];
            var first = points[triangle.FirstPointIndex];
            var second = points[triangle.SecondPointIndex];
            var third = points[triangle.ThirdPointIndex];
            var centroid = new SurfaceModelPoint3(
                (first.X + second.X + third.X) / 3.0,
                (first.Y + second.Y + third.Y) / 3.0,
                (first.Z + second.Z + third.Z) / 3.0);
            var sampleNormal = Vector3.Normalize(
                mesh.Normals[triangle.FirstPointIndex]
                + mesh.Normals[triangle.SecondPointIndex]
                + mesh.Normals[triangle.ThirdPointIndex]);
            samples[sampleOrder] = new SurfaceModelSample(
                sampleOrder,
                triangleIndex,
                centroid,
                ToPoint(sampleNormal));
        }

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
            samples);
    }

    private static SurfaceModelPoint3 ToPoint(Vector3 value) =>
        new(value.X, value.Y, value.Z);
}
