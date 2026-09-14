using System.IO;
using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Models;
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
            "D:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio",
            "refactor-audit-20260909",
            "pl0437-viewer-overlay",
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

            renderer.PrepareAffineApply(publishedA2);
            Check(
                "Affine Apply preparation is owned by the renderer",
                renderer.HasManagedData,
                $"hasManagedData={renderer.HasManagedData}");
            renderer.ClearAffineApply();
            Check(
                "Affine Apply clear releases renderer state",
                !renderer.HasManagedData,
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
                !renderer.HasManagedData && renderer.RegridHeightFieldRenderOutput is null,
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
