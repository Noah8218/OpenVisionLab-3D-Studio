using System.IO;
using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class ViewerWorkbenchOverlayRendererVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer workbench overlay renderer verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: explicit Affine Apply/Re-grid render owner without a WPF control or active OpenGL context"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
            "viewer-overlay",
            "renderer-fixture",
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        void CheckFit(string label, ViewerWorkbenchOverlayRenderer renderer)
        {
            var positions = renderer.WorkbenchFitPositions;
            Check($"{label} fit bounds cover a full display box", positions.Count == 8
                && positions.All(point => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z)), $"corners={positions.Count}");
            const double yaw = -24, pitch = 42, fieldOfView = 45;
            foreach (var viewport in new[] { (Name: "Wide", Width: 590d, Height: 790d), (Name: "Compact", Width: 270d, Height: 480d) })
            {
                var aspect = viewport.Width / viewport.Height;
                var perspective = CameraMath.FitPositions(positions, yaw, pitch, fieldOfView, aspect);
                var eye = CameraMath.OrbitCameraPosition(perspective.Target, yaw, pitch, perspective.Distance);
                Check($"{label} {viewport.Name} perspective fits every corner", positions.All(position =>
                    CameraMath.TryProjectWorldPositionToScreen(position, viewport.Width, viewport.Height, fieldOfView, eye, perspective.Target, out var screen)
                    && screen.X >= 0 && screen.X <= viewport.Width && screen.Y >= 0 && screen.Y <= viewport.Height), $"distance={perspective.Distance:G6}");
                var orthographic = CameraMath.FitOrthographicPositions(positions, yaw, pitch, aspect);
                eye = CameraMath.OrbitCameraPosition(orthographic.Target, yaw, pitch, orthographic.Distance);
                Check($"{label} {viewport.Name} orthographic fits every corner", positions.All(position =>
                    CameraMath.TryProjectWorldPositionToOrthographicScreen(position, viewport.Width, viewport.Height, orthographic.Height, eye, orthographic.Target, out var screen)
                    && screen.X >= 0 && screen.X <= viewport.Width && screen.Y >= 0 && screen.Y <= viewport.Height), $"height={orthographic.Height:G6}");
            }
            var oldEye = CameraMath.OrbitCameraPosition(Vector3.Zero, yaw, pitch, 5.2);
            Check($"{label} old fallback distance reproduces clipping", positions.Any(position =>
                !CameraMath.TryProjectWorldPositionToScreen(position, 270, 480, fieldOfView, oldEye, Vector3.Zero, out var screen)
                || screen.X < 0 || screen.X > 270 || screen.Y < 0 || screen.Y > 480), "Negative control: distance5.2 is not a valid result fit.");
        }

        try
        {
            var fixture = PlaneFlatnessLiveA3PointerSmokeFixture.Prepare(root);
            var workbench = new ToolWorkbenchViewModel();
            var opened = workbench.TryOpenTeachingRecipe(fixture.RecipePath, out var openMessage);
            Check("deterministic overlay fixture opens", opened, openMessage);
            if (!opened)
            {
                throw new InvalidDataException(openMessage);
            }

            var publishedA2 = PlaneFlatnessLiveA3PointerSmokeFixture.CreatePublishedA2(fixture.RecipePath);
            var renderer = new ViewerWorkbenchOverlayRenderer(
                new ViewerWorkbenchOverlayCallbacks(
                    () => false,
                    () => true,
                    () => false,
                    () => 9.2,
                    () => 0,
                    () => 0,
                    _ => (Vector3.Zero, Vector3.UnitZ),
                    () => [],
                    () => new TeachingCaptureState(false, "", "", "", 0, [], false, false, "", 0),
                    () => null));

            Check("empty renderer has no result fit bounds", renderer.WorkbenchFitPositions.Count == 0, "corners=0");
            renderer.PrepareAffineApply(publishedA2);
            CheckFit("Affine", renderer);
            // Bounds of the owned 5x5 native reproducer after its existing display normalization.
            Vector3[] nativeBounds =
            [
                new(-3.9f, -2.925f, -3.9f), new(3.9f, -2.925f, -3.9f),
                new(-3.9f, 2.925f, -3.9f), new(3.9f, 2.925f, -3.9f),
                new(-3.9f, -2.925f, 3.9f), new(3.9f, -2.925f, 3.9f),
                new(-3.9f, 2.925f, 3.9f), new(3.9f, 2.925f, 3.9f)
            ];
            var nativeFit = CameraMath.FitPositions(nativeBounds, -24, 42, 45, 270d / 480d);
            var camera = new MainWindowViewModel();
            camera.SetWorkbenchAffineApply(publishedA2, isPublished: true);
            camera.SetWorkbenchResultScene(true);
            camera.ApplyC3DCameraFit(nativeFit.Target, nativeFit.Distance, useTopInspectionView: false, "Fit all visible entities");
            camera.ZoomCamera(1.25);
            Check("native Compact fit retains its zoom-out range", nativeFit.Distance > 20
                && Math.Abs(camera.CameraDistance - nativeFit.Distance * 1.25) < 1e-6, $"fit={nativeFit.Distance:G6}; zoom={camera.CameraDistance:G6}");
            camera.ZoomCamera(0.8);
            Check("native Compact zoom round trip preserves result and framing", Math.Abs(camera.CameraDistance - nativeFit.Distance) < 1e-6
                && ReferenceEquals(camera.WorkbenchAffineApply, publishedA2) && !camera.C3DSampleVisible, $"distance={camera.CameraDistance:G6}");
            Check(
                "Affine Apply preparation is owned by the renderer",
                renderer.HasManagedData,
                $"hasManagedData={renderer.HasManagedData}");
            renderer.ClearAffineApply();
            Check(
                "Affine Apply clear releases renderer state",
                !renderer.HasManagedData && renderer.WorkbenchFitPositions.Count == 0,
                $"hasManagedData={renderer.HasManagedData}");

            var document = ToolRecipeDocumentStore.Load(fixture.RecipePath);
            var publishedHeightField = ToolRecipeRegridHeightFieldExecution.Execute(
                    document,
                    PlaneFlatnessLiveA3PointerSmokeFixture.RegridStepId,
                    publishedA2)
                .Output;
            Check(
                "fixture creates a Re-grid Height Field for renderer preparation",
                publishedHeightField is not null,
                $"output={publishedHeightField?.OutputEntityId ?? "(none)"}");
            if (publishedHeightField is null)
            {
                throw new InvalidDataException("The renderer fixture did not create a Re-grid Height Field.");
            }

            renderer.PrepareRegridHeightField(publishedHeightField);
            CheckFit("Regrid", renderer);
            Check(
                "Re-grid preparation retains the exact output identity",
                ReferenceEquals(renderer.RegridHeightFieldRenderOutput, publishedHeightField)
                && renderer.HasManagedData,
                $"sameOutput={ReferenceEquals(renderer.RegridHeightFieldRenderOutput, publishedHeightField)}|hasManagedData={renderer.HasManagedData}");
            Check(
                "Re-grid picking fails closed when the viewport is unavailable",
                !renderer.TryPickRegridHeightFieldPoint(new Point(0, 0), out _),
                "viewport=0x0|pick=False");

            renderer.Clear();
            Check(
                "combined clear releases both overlay caches",
                !renderer.HasManagedData && renderer.RegridHeightFieldRenderOutput is null && renderer.WorkbenchFitPositions.Count == 0,
                $"hasManagedData={renderer.HasManagedData}|regridOutput={(renderer.RegridHeightFieldRenderOutput is null ? "null" : "present")}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        summary =
            $"Viewer workbench overlay renderer verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }
}
