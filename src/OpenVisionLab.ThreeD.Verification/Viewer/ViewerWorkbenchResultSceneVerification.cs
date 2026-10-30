using System.IO;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerWorkbenchResultSceneVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed)>();
        void Check(string name, bool passed) => checks.Add((name, passed));
        var viewModel = new MainWindowViewModel();
        viewModel.UseResultSmokeScene();
        viewModel.CubeVisible = true;
        viewModel.PointCloudVisible = true;
        viewModel.GlbSampleVisible = true;
        viewModel.LazSampleVisible = true;
        viewModel.C3DSampleVisible = true;
        viewModel.YawDegrees = 23;
        viewModel.PitchDegrees = 41;
        viewModel.CameraDistance = 7;
        viewModel.CameraTargetX = 2;
        viewModel.CameraTargetY = 3;
        viewModel.CameraTargetZ = 4;
        viewModel.SetWorkbenchResultScene(true);
        Check("standalone result excludes default cube and generated points", !viewModel.CubeVisible && !viewModel.PointCloudVisible);
        Check("standalone result excludes unrelated imported and C3D geometry", !viewModel.GlbSampleVisible && !viewModel.LazSampleVisible && !viewModel.C3DSampleVisible);
        Check("standalone result excludes sample measurements and overlays", !viewModel.MeasurementVisible && !viewModel.SelectionOverlayVisible && !viewModel.ResultOverlayVisible && !viewModel.SampleResultOverlayVisible);
        Check("standalone result preserves orbit, zoom and pan", CameraUnchanged());
        viewModel.SetWorkbenchResultScene(true);
        Check("repeated output refresh preserves the camera", CameraUnchanged());
        viewModel.CubeVisible = true;
        viewModel.PointCloudVisible = true;
        viewModel.SetWorkbenchResultScene(false);
        Check("overlay mode retains its existing source context", viewModel.C3DSampleVisible && viewModel.CubeVisible && viewModel.PointCloudVisible);
        Check("overlay mode also preserves the camera", CameraUnchanged());
        summary = $"Workbench result scene: {(checks.All(item => item.Passed) ? "PASS" : "FAIL")} ({checks.Count(item => item.Passed)}/{checks.Count})";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(item => $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name}").Prepend(summary));
        return checks.All(item => item.Passed);

        bool CameraUnchanged() => viewModel.YawDegrees == 23 && viewModel.PitchDegrees == 41 && viewModel.CameraDistance == 7
            && viewModel.CameraTargetX == 2 && viewModel.CameraTargetY == 3 && viewModel.CameraTargetZ == 4;
    }
}
