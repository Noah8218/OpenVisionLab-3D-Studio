using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void HandleFitAllCommand()
    {
        var fitted = viewModel.IsTopOrthographicView
            ? TryFitCurrentC3DOrthographic("Top view fitted to all C3D data")
            : TryFitCurrentC3D(useTopInspectionView: false, "Fit all C3D height grid");
        if (!fitted)
        {
            viewModel.FitAll();
        }

        RenderNow();
    }

    private void HandleTopViewCommand()
    {
        if (TryFitCurrentC3DOrthographic(
                "Top orthographic view · X=column, Z=row · view only"))
        {
            RenderNow();
        }
    }

    private void HandlePerspectiveViewCommand()
    {
        viewModel.RestorePerspectiveView(
            "Perspective view restored · recipe and inspection unchanged");
        RenderNow();
    }

    private void HandleFitRoiCommand()
    {
        if (!TryGetVisibleTeachingGridRectangle(out var selectionId, out var rectangle))
        {
            viewModel.ViewerStatus = "Fit ROI requires a selected Reference or Measurement ROI";
            return;
        }

        var lastRow = rectangle.Row + rectangle.RowCount - 1;
        var lastColumn = rectangle.Column + rectangle.ColumnCount - 1;
        var rawHeight = GetTeachingGridRectangleDisplayRawHeight(selectionId, rectangle);
        Vector3[] positions =
        [
            CreateC3DGridDisplayPosition(rectangle.Row, rectangle.Column, rawHeight),
            CreateC3DGridDisplayPosition(rectangle.Row, lastColumn, rawHeight),
            CreateC3DGridDisplayPosition(lastRow, lastColumn, rawHeight),
            CreateC3DGridDisplayPosition(lastRow, rectangle.Column, rawHeight)
        ];
        var aspect = Math.Max(1.0, Viewport.ActualWidth) / Math.Max(1.0, Viewport.ActualHeight);
        if (viewModel.IsTopOrthographicView)
        {
            var fit = CameraMath.FitOrthographicPositions(
                positions,
                yawDegrees: 0.0,
                pitchDegrees: 90.0,
                aspect,
                padding: 1.30);
            viewModel.ApplyTopOrthographicFit(
                fit.Target,
                fit.Height,
                fit.Distance,
                "Top view fitted to selected ROI · view only");
        }
        else
        {
            var fit = CameraMath.FitPositions(
                positions,
                viewModel.YawDegrees,
                viewModel.PitchDegrees,
                FieldOfViewDegrees,
                aspect,
                padding: 1.30);
            viewModel.ApplyC3DCameraFit(
                fit.Target,
                fit.Distance,
                useTopInspectionView: false,
                "Perspective view fitted to selected ROI · view only");
        }

        RenderNow();
    }

    private void HandleFitSelectionCommand()
    {
        if (viewModel.SelectedEntity != "C3D Height Grid"
            || !TryFitCurrentC3D(useTopInspectionView: false, "Fit selected C3D height grid"))
        {
            viewModel.FitSelection();
        }

        RenderNow();
    }

    private bool TryFitCurrentC3D(bool useTopInspectionView, string status)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            return false;
        }

        var renderProxy = GetC3DRenderProxy();
        var positions = GetC3DRenderPositions(renderProxy);
        var yaw = useTopInspectionView ? 0.0 : viewModel.YawDegrees;
        var pitch = useTopInspectionView ? 80.0 : viewModel.PitchDegrees;
        var fit = CameraMath.FitPositions(
            positions,
            yaw,
            pitch,
            FieldOfViewDegrees,
            Math.Max(1.0, Viewport.ActualWidth) / Math.Max(1.0, Viewport.ActualHeight));
        viewModel.ApplyC3DCameraFit(fit.Target, fit.Distance, useTopInspectionView, status);
        return true;
    }

    private bool TryFitCurrentC3DOrthographic(string status)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            return false;
        }

        var renderProxy = GetC3DRenderProxy();
        var positions = GetC3DRenderPositions(renderProxy);
        var fit = CameraMath.FitOrthographicPositions(
            positions,
            yawDegrees: 0.0,
            pitchDegrees: 90.0,
            Math.Max(1.0, Viewport.ActualWidth) / Math.Max(1.0, Viewport.ActualHeight));
        viewModel.ApplyTopOrthographicFit(fit.Target, fit.Height, fit.Distance, status);
        return true;
    }

    private void HandleResetCommand()
    {
        viewModel.RecipeOutputEnabled = true;
        viewModel.Reset();
        RenderNow();
    }

    private void HandleScreenshotCommand()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "artifacts", $"sharpgl_viewer_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        CaptureWindow(path);
    }

    private void HandleOpenRecipeCommand()
    {
        if (!recipeLoadWorkflow.CanStartOpen())
        {
            return;
        }

        var path = recipeDialogHost.SelectRecipeToOpen();
        if (path is not null)
        {
            StartOpenRecipeRequest(path);
        }
    }

    private void StartOpenRecipeRequest(string path)
        => recipeLoadWorkflow.StartOpen(path, isSmoke: false, cancellationToken: viewerLifetimeToken);

    private void HandleSaveRecipeCommand()
    {
        SaveCurrentRecipeWithDialog();
    }

    private void HandleApplyRoiAlignmentCommand()
    {
        ApplyRoiReferenceAlignment();
    }

    public void SaveCurrentRecipeWithDialog()
    {
        var savePlan = recipeSaveWorkflow.ResolveCurrentRecipeSavePlan();
        var path = recipeDialogHost.SelectRecipeToSave(savePlan.DefaultFileName);

        if (path is not null)
        {
            SaveCurrentRecipe(path, isSmoke: false);
        }
    }

    public bool SaveCurrentRecipe(string path, bool isSmoke) =>
        recipeSaveWorkflow.Save(path, isSmoke);

    // Legacy public entry point; the session owns ROI policy and state.
    public bool ApplyRoiReferenceAlignment() => roiEditingSession.ApplyRoiReferenceAlignment();

    private void HandlePublishResultCommand()
    {
        hostOperations.PublishCurrentPreviewResult();
        RenderNow();
    }

    private void CaptureWindow(string path)
    {
        RenderNow();
        var result = WpfScreenshotCapture.Capture(this);
        WpfScreenshotCapture.Save(result.Bitmap, path);

        viewModel.LastScreenshotPath = Path.GetFullPath(path);
        viewModel.ViewerStatus = "Screenshot captured";
    }

    private bool ApplyRecipeFile(string path, bool isSmoke) =>
        recipeLoadWorkflow.Apply(path, isSmoke);

    private bool SetRecipeLoadFailure(string label, Exception exception)
    {
        if (IsDisposed)
        {
            return false;
        }

        var message = $"{label} failed: {exception.Message}";
        SetRecipeValidationWarning(message);
        viewModel.ViewerStatus = message;
        return false;
    }

    private void SetRecipeValidationOk() => viewModel.SetRecipeValidationSummary("Validation: OK");

    private void SetRecipeValidationWarning(string warning) => viewModel.SetRecipeValidationSummary(warning);

    private string ResolveCurrentRecipeSourcePath()
    {
        var candidate = viewModel.RecipeSourcePath;
        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.GetFullPath(candidate);
        }

        if (File.Exists(candidate))
        {
            return candidate;
        }

        var defaultSample = samplePathResolver.Resolve(DefaultC3DSamplePath);
        return defaultSample is not null ? Path.GetFullPath(defaultSample) : candidate;
    }

}
