using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public bool LoadInspectionTaskRecipe(string recipeFileName)
    {
        var path = samplePathResolver.Resolve(Path.Combine("recipes", recipeFileName));
        if (path is null)
        {
            viewModel.ViewerStatus = $"Inspection task recipe was not found: {recipeFileName}";
            return false;
        }

        return ApplyRecipeFile(path, isSmoke: false);
    }

    private bool TryHandleWarpageRoiPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.WarpageRoiSelectionMode)
        {
            return false;
        }

        if (!TryPickC3DPoint(screenPoint, out var point) || c3dSample is null)
        {
            viewModel.SelectedEntity = "C3D Warpage ROI";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Warpage ROI pick missed C3D height grid";
            return true;
        }

        viewModel.SetWarpageRoiFromCenter(point.Row, point.Column, c3dSample.Height, c3dSample.Width);
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private void DrawWarpageRoi(OpenGL gl)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null || !viewModel.WarpageConfigured)
        {
            return;
        }

        var roi = viewModel.CreateWarpageRecipeStep().Roi;
        if (!IsC3DGridRoiInside(roi, c3dSample))
        {
            return;
        }

        var height = c3dSample.Mean;
        var lastRow = roi.Row + roi.RowCount - 1;
        var lastColumn = roi.Column + roi.ColumnCount - 1;
        var topLeft = CreateC3DGridDisplayPosition(roi.Row, roi.Column, height);
        var topRight = CreateC3DGridDisplayPosition(roi.Row, lastColumn, height);
        var bottomRight = CreateC3DGridDisplayPosition(lastRow, lastColumn, height);
        var bottomLeft = CreateC3DGridDisplayPosition(lastRow, roi.Column, height);

        gl.LineWidth(3.0f);
        gl.Color(0.96, 0.36, 0.72);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.End();

        gl.PointSize(7.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.78, 0.16);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void ClearWarpageTransientInspectionState()
    {
        planeFlatnessEvaluation = null;
        planeReferenceMeasurement = null;
        twoPointFirst = null;
        twoPointSecond = null;
        roiEditingSession.Reset();
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearPlaneReferenceMeasurement();
        viewModel.ClearRoiStepMeasurement();
    }

}
