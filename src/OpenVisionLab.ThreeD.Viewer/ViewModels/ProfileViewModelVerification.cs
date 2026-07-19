using System.IO;
using System.Numerics;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public static class ProfileViewModelVerification
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
            Check("Profile remains display-only", ReferenceEquals(initialPreview, viewModel.PreviewToolResult) && ReferenceEquals(initialResults, viewModel.ResultEntities) && Equals(initialPointPair, viewModel.CreatePointPairDimensionsRecipeStep()), $"preview={viewModel.PreviewToolResult.Status}|results={viewModel.ResultEntities.Count}");

            viewModel.ClearProfile();
            Check("Profile clear removes live display state", !viewModel.ProfileVisible && viewModel.ProfileValidSampleCount == 0 && viewModel.ProfileMissingSampleCount == 0, viewModel.ProfileSummary);

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
