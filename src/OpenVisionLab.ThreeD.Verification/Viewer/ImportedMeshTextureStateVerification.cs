using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ImportedMeshTextureStateVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer imported mesh texture state verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var mesh = ImportedMesh.CreateTriangleMesh(
            "mesh-a.glb",
            "Mesh A",
            "GLB",
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            [0, 1, 2]);
        var otherMesh = ImportedMesh.CreateTriangleMesh(
            "mesh-b.glb",
            "Mesh B",
            "GLB",
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            [0, 1, 2]);
        var state = new ImportedMeshTextureState();

        Check(
            "new state starts without a texture or failure",
            state.Source is null
                && state.TextureId == 0
                && !state.ReleasePending
                && state.UploadCount == 0
                && state.ReleaseCount == 0
                && state.ReleaseFailureCount == 0
                && !state.UploadFailed
                && state.UploadSummary == "texture none",
            $"id={state.TextureId}|pending={state.ReleasePending}|uploads={state.UploadCount}");

        state.RecordAllocation(17);
        Check(
            "allocation is retained until the context-bound delete completes",
            state.TextureId == 17
                && state.Source is null
                && !state.ReleasePending,
            $"id={state.TextureId}|pending={state.ReleasePending}");

        state.RecordUpload(mesh, 17, "uploaded 2x2 image/png");
        Check(
            "successful upload binds source identity and latest summary",
            state.MatchesSource(mesh)
                && !state.MatchesSource(otherMesh)
                && state.TextureId == 17
                && state.UploadCount == 1
                && !state.UploadFailed
                && state.UploadSummary == "uploaded 2x2 image/png",
            $"id={state.TextureId}|uploads={state.UploadCount}|summary={state.UploadSummary}");

        state.ResetForSourceChange();
        Check(
            "source replacement clears identity and schedules existing texture release",
            state.Source is null
                && state.TextureId == 17
                && state.ReleasePending
                && !state.UploadFailed
                && state.UploadSummary == "texture none",
            $"id={state.TextureId}|pending={state.ReleasePending}|summary={state.UploadSummary}");

        state.RecordRelease(succeeded: true);
        state.ClearAfterRelease();
        Check(
            "successful release clears the managed handle and source",
            state.TextureId == 0
                && state.Source is null
                && !state.ReleasePending
                && state.ReleaseCount == 1,
            $"id={state.TextureId}|pending={state.ReleasePending}|releases={state.ReleaseCount}");

        state.RecordUploadFailure("upload failed: unsupported format");
        Check(
            "failed upload blocks retry and preserves diagnostic text",
            state.UploadFailed
                && state.UploadSummary == "upload failed: unsupported format",
            $"failed={state.UploadFailed}|summary={state.UploadSummary}");

        state.RecordRelease(succeeded: false);
        Check(
            "release failure is counted independently",
            state.ReleaseCount == 1
                && state.ReleaseFailureCount == 1,
            $"releases={state.ReleaseCount}|releaseFailures={state.ReleaseFailureCount}");

        state.RecordAllocation(29);
        state.RecordUpload(mesh, 29, "uploaded 4x4 image/png");
        Check(
            "a later successful upload replaces stale failure state",
            state.MatchesSource(mesh)
                && state.TextureId == 29
                && state.UploadCount == 2
                && !state.UploadFailed
                && state.UploadSummary == "uploaded 4x4 image/png",
            $"id={state.TextureId}|uploads={state.UploadCount}|failed={state.UploadFailed}");

        state.ResetForOpenGLInitialization();
        Check(
            "OpenGL initialization clears transient texture state but keeps cumulative evidence",
            state.Source is null
                && state.TextureId == 0
                && !state.ReleasePending
                && !state.UploadFailed
                && state.UploadSummary == "texture none"
                && state.UploadCount == 2
                && state.ReleaseCount == 1
                && state.ReleaseFailureCount == 1,
            $"id={state.TextureId}|uploads={state.UploadCount}|releases={state.ReleaseCount}|releaseFailures={state.ReleaseFailureCount}");

        state.RecordAllocation(31);
        state.RecordUpload(mesh, 31, "uploaded 1x1 image/png");
        state.ClearManagedReferencesAfterDispose();
        Check(
            "dispose clears managed handles without erasing evidence",
            state.Source is null
                && state.TextureId == 0
                && !state.ReleasePending
                && state.UploadCount == 3
                && state.ReleaseCount == 1
                && state.ReleaseFailureCount == 1,
            $"id={state.TextureId}|uploads={state.UploadCount}|releases={state.ReleaseCount}");

        summary = $"Imported mesh texture state verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}
