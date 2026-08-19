using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

internal static class HeightDeviationRuleCoordinator
{
    public static ToolResult CreatePreviewResult(
        C3DHeightGrid grid,
        string sourceName,
        double peakTolerance,
        string sourceUnit)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);

        return HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
            MainWindowViewModel.C3DEntityId,
            sourceName,
            grid.Min,
            grid.Max,
            grid.Mean,
            grid.ValidSampleCount,
            peakTolerance,
            sourceUnit));
    }

    public static void ApplyToViewModel(
        MainWindowViewModel viewModel,
        C3DHeightGrid grid,
        string sourceName,
        double peakTolerance,
        string sourceUnit)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);

        viewModel.SetC3DHeightDeviationPreview(
            CreatePreviewResult(grid, sourceName, peakTolerance, sourceUnit));
    }
}
