using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ProfileViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D height-profile ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        try
        {
            var viewModel = new MainWindowViewModel();
            Check("Profile is an explicit selection mode", viewModel.SelectionModes.Contains(MainWindowViewModel.ProfileSelectionMode), string.Join(",", viewModel.SelectionModes));
            Check("Profile command requires C3D", !viewModel.ProfileCommand.CanExecute(null), viewModel.C3DSampleVisible.ToString());

            viewModel.UseC3DSmokeScene();
            Check("Profile command is enabled for C3D", viewModel.ProfileCommand.CanExecute(null), viewModel.C3DSampleVisible.ToString());
            var requestCount = 0;
            viewModel.ProfileViewRequested += (_, _) => requestCount++;
            var initialPreview = viewModel.PreviewToolResult;
            var initialResults = viewModel.ResultEntities;
            var initialPointPair = viewModel.CreatePointPairDimensionsRecipeStep();
            viewModel.ProfileCommand.Execute(null);
            Check("Profile command selects mode and requests dock view", requestCount == 1 && viewModel.SelectedSelectionMode == MainWindowViewModel.ProfileSelectionMode, $"requests={requestCount}|mode={viewModel.SelectedSelectionMode}");

            viewModel.SetProfileStart(4, 7, new Vector3(1.0f, 2.0f, 3.0f), 105.0f);
            Check("P1 state is visible without fabricating P2", viewModel.ProfileVisible && viewModel.ProfileValidSampleCount == 1 && viewModel.ProfileEndpointSummary.Contains("P2: not set", StringComparison.Ordinal), viewModel.ProfileEndpointSummary);

            viewModel.SetProfile(
                4,
                7,
                new Vector3(1.0f, 2.0f, 3.0f),
                105.0f,
                9,
                15,
                new Vector3(5.0f, 4.0f, 3.0f),
                111.0f,
                validSampleCount: 9,
                missingSampleCount: 1,
                minimum: 101.0,
                maximum: 113.0,
                mean: 107.0,
                pathData: "M 0,30 L 240,10");
            Check("P1-P2 metrics and chart state update", viewModel.ProfileValidSampleCount == 9 && viewModel.ProfileMissingSampleCount == 1 && viewModel.ProfilePathData == "M 0,30 L 240,10" && viewModel.ProfileSummary.Contains("ΔH 6.000", StringComparison.Ordinal), $"{viewModel.ProfileSummary}|{viewModel.ProfileRange}");

            viewModel.SetProfileLinkedCursor(
                row: 7,
                column: 11,
                rawHeight: 107.0,
                sampleIndex: 4,
                sampleCount: 9,
                minimum: 101.0,
                maximum: 113.0);
            Check(
                "Exact profile cursor maps to the sampled chart coordinate",
                viewModel.ProfileLinkedCursorVisible
                && Math.Abs(viewModel.ProfileLinkedCursorX - 120.0) < 0.001
                && Math.Abs(viewModel.ProfileLinkedCursorY - 27.0) < 0.001
                && viewModel.ProfileLinkedCursorSummary.Contains("(7,11)", StringComparison.Ordinal)
                && viewModel.ProfileLinkedCursorSummary.Contains("5/9", StringComparison.Ordinal),
                $"visible={viewModel.ProfileLinkedCursorVisible}|x={viewModel.ProfileLinkedCursorX:R}|y={viewModel.ProfileLinkedCursorY:R}|summary={viewModel.ProfileLinkedCursorSummary}");

            viewModel.SetProfileLinkedCursorUnavailable();
            Check(
                "Off-trace cursor has no fabricated profile marker",
                !viewModel.ProfileLinkedCursorVisible
                && viewModel.ProfileLinkedCursorSummary.Contains("outside", StringComparison.Ordinal),
                viewModel.ProfileLinkedCursorSummary);
            Check("Profile remains display-only", ReferenceEquals(initialPreview, viewModel.PreviewToolResult) && ReferenceEquals(initialResults, viewModel.ResultEntities) && Equals(initialPointPair, viewModel.CreatePointPairDimensionsRecipeStep()), $"preview={viewModel.PreviewToolResult.Status}|results={viewModel.ResultEntities.Count}");

            viewModel.ClearProfile();
            Check("Profile clear removes live display state", !viewModel.ProfileVisible && viewModel.ProfileValidSampleCount == 0 && viewModel.ProfileMissingSampleCount == 0 && !viewModel.ProfileLinkedCursorVisible && viewModel.ProfileLinkedCursorSummary.Contains("unavailable", StringComparison.Ordinal), viewModel.ProfileSummary);


            var sourcePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "profile-gap.c3d");
            using (var writer = new BinaryWriter(File.Create(sourcePath)))
            {
                writer.Write(5);
                writer.Write(4);
                for (var index = 0; index < 20; index++) writer.Write(index == 6 ? float.NaN : index + 1f);
            }
            var source = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 4);
            var profile = viewModel.HeightProfile;
            viewModel.ProfileSourceCommand.Execute(source);
            viewModel.ProfileActionCommand.Execute(ViewerHeightProfileAction.Begin);
            Check("Empty begin displays no fabricated graph", profile.IsPicking && viewModel.ProfileVisible && viewModel.ProfilePathData.Length == 0, viewModel.ProfilePathData);
            var a = source.ReadPoint(0, 0);
            var b = source.ReadPoint(3, 4);
            Check("Invalid first cells are ignored", !profile.Pick(a with { Row = -1 }) && !profile.Pick(a with { RawValue = 99 }) && profile.First is null, profile.First.ToString() ?? "none");
            Check("A is exact and B must be distinct", profile.Pick(a) && !profile.Pick(a) && profile.IsPicking && profile.Second is null, viewModel.ProfileEndpointSummary);
            Check("B finishes the original full-grid trace", profile.Pick(b) && profile.HasEndpoints && !profile.IsPicking && profile.ExpectedSampleCount == 5 && profile.Samples.Length == 4, $"samples={profile.Samples.Length}/{profile.ExpectedSampleCount}");
            Check("SDK distance uses grid cells and raw height", Math.Abs(profile.GridDistance - 5) < 1e-9 && Math.Abs(profile.HeightDelta - 19) < 1e-9, $"XY={profile.GridDistance};H={profile.HeightDelta}");
            Check("Missing cells break graph and cursor", viewModel.ProfilePathData.Split(" M ", StringSplitOptions.RemoveEmptyEntries).Length == 2 && profile.GetTracePoint(0.25) is null && profile.GetTracePoint(1) == b, viewModel.ProfilePathData);
            Check("Invalid moves preserve both endpoints", !profile.Move(2, b with { Row = -1 }) && !profile.Move(1, a with { Row = 1, Column = 1, RawValue = float.NaN }) && !profile.Move(2, a) && profile.First == a && profile.Second == b, viewModel.ProfileEndpointSummary);
            profile.SelectEndpoint(1);
            Check("Nudge into a missing cell is rejected", !profile.Nudge(1, 1) && profile.First == a, viewModel.ProfileEndpointSummary);
            profile.Swap();
            Check("Swap reverses trace and signed height", profile.First == b && profile.Second == a && Math.Abs(profile.GridDistance - 5) < 1e-9 && Math.Abs(profile.HeightDelta + 19) < 1e-9 && profile.Samples[0] == b, viewModel.ProfileEndpointSummary);
            profile.ToggleCollapsed();
            profile.Begin();
            profile.Pick(source.ReadPoint(0, 4));
            profile.Cancel();
            Check("Cancel restores the previous pair and collapsed state", profile.First == b && profile.Second == a && profile.IsCollapsed && !profile.IsPicking, viewModel.ProfileEndpointSummary);
            profile.Attach(C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0));
            Check("Render density cannot change measurement identity", profile.First == b && profile.Second == a && profile.HasEndpoints, profile.SourceSha256 ?? "none");
            profile.Begin();
            profile.Attach(null);
            profile.Cancel();
            Check("Source replacement removes backup and stale measurements", !profile.IsVisible && !profile.HasEndpoints && !profile.IsPicking && profile.Samples.Length == 0 && !viewModel.ProfileVisible, viewModel.ProfileSummary);
            viewModel.ProfileSourceCommand.Execute(source);
            profile.Begin();
            profile.Pick(a);
            profile.Pick(b);
            viewModel.SetPointerModeCommand.Execute("Move");
            Check("Arrow edits are disabled outside measure mode", !viewModel.ProfileActionCommand.CanExecute(ViewerHeightProfileAction.Left), viewModel.SelectedSelectionMode);
            Check("Profile actions never execute or edit inspection", ReferenceEquals(initialPreview, viewModel.PreviewToolResult) && ReferenceEquals(initialResults, viewModel.ResultEntities) && Equals(initialPointPair, viewModel.CreatePointPairDimensionsRecipeStep()), $"preview={viewModel.PreviewToolResult.Status}");
            Check("Orbit center rejects non-finite targets", !viewModel.OrbitHereCommand.CanExecute(new Vector3(float.NaN, 0, 0)) && viewModel.OrbitHereCommand.CanExecute(a.Position), "finite camera-only target");
            viewModel.OrbitHereCommand.Execute(a.Position);
            Check("Orbit center updates only camera", viewModel.CameraTargetX == a.Position.X && viewModel.CameraTargetY == a.Position.Y && viewModel.CameraTargetZ == a.Position.Z && ReferenceEquals(initialPreview, viewModel.PreviewToolResult), "camera and inspection");
            summary = $"Height-profile ViewModel verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Height-profile ViewModel verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}
