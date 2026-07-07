using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl : UserControl
{
    public static readonly DependencyProperty SidePanelsVisibleProperty =
        DependencyProperty.Register(
            nameof(SidePanelsVisible),
            typeof(bool),
            typeof(OpenVisionThreeDViewerControl),
            new PropertyMetadata(true, OnSidePanelsVisibleChanged));

    private const float FieldOfViewDegrees = 45.0f;
    private const string DefaultC3DSamplePath = @"3D\Thickness\Ori_20240116_094414.C3D";
    private const double DefaultC3DHeightDeviationTolerance = 1200.0;

    private readonly HeightGridPoint[] generatedPointCloud = CreateGeneratedPointCloud();
    private C3DHeightGrid? c3dSample;
    private string? smokeScreenshotPath;
    private string? smokeContractsPath;
    private string? smokeSaveRecipePath;
    private bool smokePublishResult;
    private int smokeExitCode;
    private readonly MainWindowViewModel viewModel = new();
    private bool isOrbiting;
    private bool isPanning;
    private string? smokePickTarget;
    private Point lastMousePosition;

    public OpenVisionThreeDViewerControl()
    {
        InitializeComponent();
        UpdateSidePanelsVisibility();
        DataContext = viewModel;
        c3dSample = LoadDefaultC3DSample();
        ConfigureC3DHeightDeviationRule();
        viewModel.PointCloudPointCount = generatedPointCloud.Length.ToString("N0", CultureInfo.InvariantCulture);
        SetC3DSampleStatus();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.CubeVisible)
                or nameof(MainWindowViewModel.PointCloudVisible)
                or nameof(MainWindowViewModel.C3DSampleVisible)
                or nameof(MainWindowViewModel.MeasurementVisible)
                or nameof(MainWindowViewModel.SelectedColorMode)
                or nameof(MainWindowViewModel.PointSize)
                or nameof(MainWindowViewModel.RecipePeakTolerance)
                or nameof(MainWindowViewModel.SelectedSelectionMode)
                or nameof(MainWindowViewModel.SelectionOverlayVisible)
                or nameof(MainWindowViewModel.ResultOverlayVisible)
                or nameof(MainWindowViewModel.ResultEntities))
            {
                if (args.PropertyName == nameof(MainWindowViewModel.RecipePeakTolerance))
                {
                    ConfigureC3DHeightDeviationRule();
                }

                RenderNow();
            }
            else if (args.PropertyName == nameof(MainWindowViewModel.SelectedRenderDensity))
            {
                ReloadDefaultC3DSample();
                RenderNow();
            }
        };

    }

    public bool SidePanelsVisible
    {
        get => (bool)GetValue(SidePanelsVisibleProperty);
        set => SetValue(SidePanelsVisibleProperty, value);
    }

    public MainWindowViewModel ViewModel => viewModel;

    private static void OnSidePanelsVisibleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((OpenVisionThreeDViewerControl)dependencyObject).UpdateSidePanelsVisibility();
    }

    private void UpdateSidePanelsVisibility()
    {
        if (LeftSidePanel is null || RightSidePanel is null)
        {
            return;
        }

        var visibility = SidePanelsVisible ? Visibility.Visible : Visibility.Collapsed;
        LeftSidePanel.Visibility = visibility;
        RightSidePanel.Visibility = visibility;
    }

    public void EnableSmokeFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        var smokeIndex = Array.IndexOf(args, "--smoke-screenshot");
        if (smokeIndex >= 0 && smokeIndex + 1 < args.Length)
        {
            smokeScreenshotPath = args[smokeIndex + 1];
        }

        ApplySmokeArguments(args);
        if (smokeScreenshotPath is not null)
        {
            Loaded += SmokeCaptureOnLoaded;
        }
    }

    private void ApplySmokeArguments(string[] args)
    {
        var densityIndex = Array.IndexOf(args, "--smoke-density");
        if (densityIndex >= 0 && densityIndex + 1 < args.Length)
        {
            viewModel.SelectedRenderDensity = args[densityIndex + 1];
        }

        var pointSizeIndex = Array.IndexOf(args, "--smoke-point-size");
        if (pointSizeIndex >= 0
            && pointSizeIndex + 1 < args.Length
            && double.TryParse(args[pointSizeIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointSize))
        {
            viewModel.PointSize = pointSize;
        }

        ApplySmokeTolerance(args);

        var sceneIndex = Array.IndexOf(args, "--smoke-scene");
        if (sceneIndex >= 0 && sceneIndex + 1 < args.Length && args[sceneIndex + 1].Equals("pointcloud", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.UsePointCloudSmokeScene();
        }

        var c3dIndex = Array.IndexOf(args, "--smoke-c3d");
        if (c3dIndex >= 0)
        {
            ApplySmokeC3D();
        }

        var actionIndex = Array.IndexOf(args, "--smoke-action");
        if (actionIndex >= 0 && actionIndex + 1 < args.Length)
        {
            ApplySmokeAction(args[actionIndex + 1]);
        }

        var selectionIndex = Array.IndexOf(args, "--smoke-selection");
        if (selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            ApplySmokeSelection(args[selectionIndex + 1]);
        }

        var overlayIndex = Array.IndexOf(args, "--smoke-overlay");
        if (overlayIndex >= 0 && overlayIndex + 1 < args.Length)
        {
            ApplySmokeOverlay(args[overlayIndex + 1]);
        }

        var ruleIndex = Array.IndexOf(args, "--smoke-rule");
        if (ruleIndex >= 0 && ruleIndex + 1 < args.Length)
        {
            ApplySmokeRule(args[ruleIndex + 1]);
        }

        var recipeIndex = Array.IndexOf(args, "--smoke-recipe");
        if (recipeIndex >= 0 && recipeIndex + 1 < args.Length)
        {
            ApplySmokeRecipe(args[recipeIndex + 1]);
        }

        ApplySmokeTolerance(args);

        var pickIndex = Array.IndexOf(args, "--smoke-pick");
        if (pickIndex >= 0 && pickIndex + 1 < args.Length)
        {
            smokePickTarget = args[pickIndex + 1].ToLowerInvariant();
        }

        var contractsIndex = Array.IndexOf(args, "--smoke-contracts");
        if (contractsIndex >= 0 && contractsIndex + 1 < args.Length)
        {
            smokeContractsPath = args[contractsIndex + 1];
        }

        var saveRecipeIndex = Array.IndexOf(args, "--smoke-save-recipe");
        if (saveRecipeIndex >= 0 && saveRecipeIndex + 1 < args.Length)
        {
            smokeSaveRecipePath = args[saveRecipeIndex + 1];
        }

        smokePublishResult = Array.IndexOf(args, "--smoke-publish-result") >= 0;
    }

    private void ApplySmokeTolerance(string[] args)
    {
        var toleranceIndex = Array.IndexOf(args, "--smoke-tolerance");
        if (toleranceIndex >= 0
            && toleranceIndex + 1 < args.Length
            && double.TryParse(args[toleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var tolerance))
        {
            viewModel.RecipePeakTolerance = tolerance;
        }
    }

    private void Viewport_OpenGLInitialized(object sender, OpenGLRoutedEventArgs args)
    {
        var gl = args.OpenGL;
        gl.ClearColor(0.08f, 0.10f, 0.13f, 1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
        gl.DepthFunc(OpenGL.GL_LEQUAL);
        gl.ShadeModel(OpenGL.GL_SMOOTH);
    }

    private void Viewport_Resized(object sender, OpenGLRoutedEventArgs args)
    {
        ConfigureProjection(args.OpenGL);
    }

    private void Viewport_OpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        var gl = args.OpenGL;
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

        ConfigureProjection(gl);
        ConfigureCamera(gl);
        DrawGrid(gl);
        DrawAxes(gl);

        if (viewModel.CubeVisible)
        {
            DrawCube(gl);
        }

        if (viewModel.PointCloudVisible)
        {
            DrawPointCloud(gl, generatedPointCloud);
        }

        if (viewModel.C3DSampleVisible && c3dSample is not null)
        {
            DrawC3DHeightGrid(gl);
        }

        if (viewModel.MeasurementVisible)
        {
            InspectionOverlayRenderer.DrawMeasurement(gl, viewModel.CubeVisible, viewModel.PointCloudVisible);
        }

        if (viewModel.SelectionOverlayVisible)
        {
            InspectionOverlayRenderer.DrawSelectionOverlay(gl, viewModel.SelectedSelectionMode);
        }

        if (viewModel.ResultOverlayVisible || viewModel.ResultEntities.Count > 0)
        {
            InspectionOverlayRenderer.DrawResultOverlay(gl, viewModel.C3DSampleVisible);
        }

        gl.Flush();
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        lastMousePosition = e.GetPosition(Viewport);

        var panRequested = e.ChangedButton == MouseButton.Middle || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (e.ChangedButton == MouseButton.Left && !panRequested)
        {
            if (TryPickCube(lastMousePosition, out var hit))
            {
                viewModel.SelectedEntity = "Generated Unit Cube";
                viewModel.PickCoordinate = CameraMath.FormatPoint(hit);
                viewModel.ViewerStatus = "Picked generated cube face";
            }
            else if (TryPickC3DPoint(lastMousePosition, out var c3dPoint))
            {
                viewModel.SelectedEntity = "C3D Height Grid";
                viewModel.PickCoordinate = FormatC3DPoint(c3dPoint);
                viewModel.ViewerStatus = "Picked C3D height-grid point";
            }
            else
            {
                viewModel.SelectedEntity = "(none)";
                viewModel.PickCoordinate = "(none)";
                viewModel.ViewerStatus = "No cube face under cursor";
            }
        }

        if (panRequested)
        {
            isPanning = true;
            Viewport.CaptureMouse();
            return;
        }

        if (e.ChangedButton is MouseButton.Left or MouseButton.Right)
        {
            isOrbiting = true;
            Viewport.CaptureMouse();
        }
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isOrbiting && !isPanning)
        {
            return;
        }

        var current = e.GetPosition(Viewport);
        var delta = current - lastMousePosition;
        lastMousePosition = current;

        if (isPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                isPanning = false;
                Viewport.ReleaseMouseCapture();
                return;
            }

            PanCamera(delta);
        }
        else
        {
            if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
            {
                isOrbiting = false;
                Viewport.ReleaseMouseCapture();
                return;
            }

            viewModel.YawDegrees += delta.X * 0.35;
            viewModel.PitchDegrees = Math.Clamp(viewModel.PitchDegrees - delta.Y * 0.35, -80.0, 80.0);
            viewModel.UpdateCameraStatus();
        }

        RenderNow();
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isOrbiting = false;
        isPanning = false;
        Viewport.ReleaseMouseCapture();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var zoomScale = e.Delta > 0 ? 0.88 : 1.14;
        viewModel.CameraDistance = Math.Clamp(viewModel.CameraDistance * zoomScale, 2.4, 20.0);
        viewModel.UpdateCameraStatus();
        RenderNow();
    }

    private void FitAll_Click(object sender, RoutedEventArgs e)
    {
        viewModel.FitAll();
        RenderNow();
    }

    private void FitSelection_Click(object sender, RoutedEventArgs e)
    {
        viewModel.FitSelection();
        RenderNow();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        viewModel.Reset();
        RenderNow();
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "artifacts", $"sharpgl_viewer_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        CaptureWindow(path);
    }

    private void OpenRecipe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ApplyHeightDeviationRecipe(dialog.FileName, isSmoke: false);
            RenderNow();
        }
    }

    private void SaveRecipe_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentRecipeWithDialog();
    }

    public void SaveCurrentRecipeWithDialog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            FileName = "c3d-height-deviation.recipe.json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            SaveCurrentHeightDeviationRecipe(dialog.FileName, isSmoke: false);
        }
    }

    public bool SaveCurrentRecipe(string path, bool isSmoke) => SaveCurrentHeightDeviationRecipe(path, isSmoke);

    private void PublishResult_Click(object sender, RoutedEventArgs e)
    {
        viewModel.PublishPreviewResult();
        RenderNow();
    }

    private async void SmokeCaptureOnLoaded(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(RenderNow);
        if (smokePickTarget == "cube")
        {
            ApplySmokePickCube();
            await Dispatcher.InvokeAsync(RenderNow);
        }
        else if (smokePickTarget == "c3d")
        {
            ApplySmokePickC3D();
            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokePublishResult)
        {
            viewModel.PublishPreviewResult();
            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokeSaveRecipePath is not null)
        {
            if (!SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
            {
                smokeExitCode = 1;
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokeContractsPath is not null)
        {
            WriteSceneContracts(smokeContractsPath);
        }

        await Task.Delay(900);
        CaptureWindow(smokeScreenshotPath!);
        await Task.Delay(100);
        Application.Current.Shutdown(smokeExitCode);
    }

    private void CaptureWindow(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        RenderNow();

        var width = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);

        viewModel.LastScreenshotPath = Path.GetFullPath(path);
        viewModel.ViewerStatus = "Screenshot captured";
    }

    private void ApplySmokeAction(string action)
    {
        if (action.Equals("fit-selection", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.FitSelection();
        }
        else if (action.Equals("pan", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Pan(-0.75, 0.35, 0.0);
        }
    }

    private void ApplySmokeSelection(string mode)
    {
        var selectionMode = mode.ToLowerInvariant() switch
        {
            "box" or "box-roi" => "Box ROI",
            "section" or "section-plane" => "Section Plane",
            _ => "Point"
        };

        viewModel.UseSelectionSmokeScene(selectionMode);
    }

    private void ApplySmokeOverlay(string overlay)
    {
        if (overlay.Equals("result", StringComparison.OrdinalIgnoreCase))
        {
            if (viewModel.C3DSampleVisible)
            {
                viewModel.UseC3DHeightDeviationRuleSmokeScene();
            }
            else
            {
                viewModel.UseResultSmokeScene();
            }
        }
    }

    private void ApplySmokeRule(string rule)
    {
        if (rule.Equals("height-deviation", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
        }
    }

    private void ApplySmokeRecipe(string path)
    {
        if (!ApplyHeightDeviationRecipe(path, isSmoke: true))
        {
            smokeExitCode = 1;
        }
    }

    private bool ApplyHeightDeviationRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = HeightDeviationRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);
            var result = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
                recipe.Source.EntityId,
                recipe.Source.Name,
                grid.Min,
                grid.Max,
                grid.Mean,
                grid.ValidSampleCount,
                recipe.Rule.PeakTolerance,
                recipe.Source.Unit));

            viewModel.SetC3DHeightDeviationPreview(result);
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
            viewModel.SetRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit, recipe.Rule.PeakTolerance);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke recipe" : "Recipe")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentHeightDeviationRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = ResolveCurrentRecipeSourcePath();
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new HeightDeviationRecipe(
                HeightDeviationRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                new HeightDeviationRecipeRule(viewModel.RecipePeakTolerance));

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke recipe save" : "Recipe save")} failed: {ex.Message}";
            return false;
        }
    }

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

        var defaultSample = FindDefaultC3DSamplePath();
        return defaultSample is not null ? Path.GetFullPath(defaultSample) : candidate;
    }

    private void ApplySmokeC3D()
    {
        if (c3dSample is null)
        {
            viewModel.ViewerStatus = "C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
    }

    private void ApplySmokePickCube()
    {
        viewModel.Reset();
        viewModel.CubeVisible = true;
        viewModel.PointCloudVisible = false;
        viewModel.SelectionOverlayVisible = false;
        viewModel.ResultOverlayVisible = false;
        viewModel.MeasurementVisible = true;
        viewModel.SelectedEntity = "Generated Unit Cube";
        viewModel.FitSelection();

        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickCube(center, out var hit))
        {
            viewModel.SelectedEntity = "Generated Unit Cube";
            viewModel.PickCoordinate = CameraMath.FormatPoint(hit);
            viewModel.ViewerStatus = "Smoke pick: generated cube";
        }
        else
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed";
        }
    }

    private void ApplySmokePickC3D()
    {
        if (c3dSample is null)
        {
            viewModel.ViewerStatus = "Smoke pick failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        var center = new Point(Math.Max(1.0, Viewport.ActualWidth) / 2.0, Math.Max(1.0, Viewport.ActualHeight) / 2.0);
        if (TryPickC3DPoint(center, out var point))
        {
            viewModel.SelectedEntity = "C3D Height Grid";
            viewModel.PickCoordinate = FormatC3DPoint(point);
            viewModel.ViewerStatus = "Smoke pick: C3D height grid";
        }
        else
        {
            viewModel.SelectedEntity = "(none)";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Smoke pick failed: C3D height grid";
        }
    }

    private void ConfigureProjection(OpenGL gl)
    {
        var width = Math.Max(1, (int)Viewport.ActualWidth);
        var height = Math.Max(1, (int)Viewport.ActualHeight);
        gl.Viewport(0, 0, width, height);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Perspective(FieldOfViewDegrees, (double)width / height, 0.1, 100.0);
    }

    private void ConfigureCamera(OpenGL gl)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.LoadIdentity();
        gl.LookAt(eye.X, eye.Y, eye.Z, target.X, target.Y, target.Z, 0.0, 1.0, 0.0);
    }

    private Vector3 GetCameraPosition()
    {
        return CameraMath.OrbitCameraPosition(
            GetCameraTarget(),
            viewModel.YawDegrees,
            viewModel.PitchDegrees,
            viewModel.CameraDistance);
    }

    private Vector3 GetCameraTarget()
    {
        return CameraMath.CameraTarget(viewModel.CameraTargetX, viewModel.CameraTargetY, viewModel.CameraTargetZ);
    }

    private void DrawGrid(OpenGL gl)
    {
        gl.LineWidth(1.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(0.25, 0.29, 0.36);

        for (var i = -5; i <= 5; i++)
        {
            gl.Vertex(i, -1.02, -5.0);
            gl.Vertex(i, -1.02, 5.0);
            gl.Vertex(-5.0, -1.02, i);
            gl.Vertex(5.0, -1.02, i);
        }

        gl.End();
    }

    private void DrawAxes(OpenGL gl)
    {
        gl.LineWidth(2.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(0.95, 0.25, 0.25);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(2.3, 0.0, 0.0);

        gl.Color(0.25, 0.85, 0.35);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(0.0, 2.3, 0.0);

        gl.Color(0.35, 0.55, 1.0);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(0.0, 0.0, 2.3);

        gl.End();
    }

    private void DrawCube(OpenGL gl)
    {
        gl.Begin(OpenGL.GL_QUADS);

        gl.Color(0.20, 0.62, 0.86);
        Quad(gl, (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1));

        gl.Color(0.14, 0.48, 0.75);
        Quad(gl, (1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1));

        gl.Color(0.95, 0.72, 0.32);
        Quad(gl, (-1, 1, 1), (1, 1, 1), (1, 1, -1), (-1, 1, -1));

        gl.Color(0.78, 0.46, 0.25);
        Quad(gl, (-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1));

        gl.Color(0.45, 0.72, 0.42);
        Quad(gl, (1, -1, 1), (1, -1, -1), (1, 1, -1), (1, 1, 1));

        gl.Color(0.36, 0.60, 0.36);
        Quad(gl, (-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1));

        gl.End();

        DrawCubeWire(gl);
    }

    private void DrawCubeWire(OpenGL gl)
    {
        gl.LineWidth(1.5f);
        gl.Color(1.0, 1.0, 1.0);
        gl.Begin(OpenGL.GL_LINES);

        Edge(gl, (-1, -1, -1), (1, -1, -1));
        Edge(gl, (1, -1, -1), (1, -1, 1));
        Edge(gl, (1, -1, 1), (-1, -1, 1));
        Edge(gl, (-1, -1, 1), (-1, -1, -1));
        Edge(gl, (-1, 1, -1), (1, 1, -1));
        Edge(gl, (1, 1, -1), (1, 1, 1));
        Edge(gl, (1, 1, 1), (-1, 1, 1));
        Edge(gl, (-1, 1, 1), (-1, 1, -1));
        Edge(gl, (-1, -1, -1), (-1, 1, -1));
        Edge(gl, (1, -1, -1), (1, 1, -1));
        Edge(gl, (1, -1, 1), (1, 1, 1));
        Edge(gl, (-1, -1, 1), (-1, 1, 1));

        gl.End();
    }

    private void DrawPointCloud(OpenGL gl, IReadOnlyList<HeightGridPoint> points)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);

        foreach (var point in points)
        {
            ApplyPointColor(gl, point);
            gl.Vertex(point.Position.X, point.Position.Y, point.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawPointCloudFrame(gl);
    }

    private void DrawC3DHeightGrid(OpenGL gl)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);

        foreach (var point in c3dSample!.Points)
        {
            ApplyPointColor(gl, point);
            gl.Vertex(point.Position.X, point.Position.Y, point.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawC3DFrame(gl);
    }

    private void DrawPointCloudFrame(OpenGL gl)
    {
        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(1.0, -1.18, -2.25);
        gl.Vertex(5.4, -1.18, -2.25);
        gl.Vertex(5.4, -1.18, 2.25);
        gl.Vertex(1.0, -1.18, 2.25);
        gl.End();
    }

    private void DrawC3DFrame(OpenGL gl)
    {
        var x = c3dSample!.XHalfExtent;
        var z = c3dSample.ZHalfExtent;

        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(-x, 0.0, -z);
        gl.Vertex(x, 0.0, -z);
        gl.Vertex(x, 0.0, z);
        gl.Vertex(-x, 0.0, z);
        gl.End();
    }

    private void ApplyPointColor(OpenGL gl, HeightGridPoint point)
    {
        var (r, g, b) = viewModel.SelectedColorMode switch
        {
            "Solid" => (0.62, 0.82, 1.0),
            "Deviation" => DeviationColor(point.DeviationScalar),
            _ => HeightColor(point.HeightScalar)
        };

        gl.Color(r, g, b);
    }

    private bool TryPickCube(Point screenPoint, out Vector3 hit)
    {
        hit = default;

        if (!viewModel.CubeVisible || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        if (!CameraMath.IntersectUnitCube(ray.origin, ray.direction, 1.0f, out var distance))
        {
            return false;
        }

        hit = ray.origin + ray.direction * distance;
        return true;
    }

    private bool TryPickC3DPoint(Point screenPoint, out HeightGridPoint hit)
    {
        hit = default;

        if (!viewModel.C3DSampleVisible || c3dSample is null || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        var bestDistance = float.PositiveInfinity;
        var maxDistance = Math.Max(0.12f, (float)viewModel.CameraDistance * 0.025f);

        foreach (var point in c3dSample.Points)
        {
            var toPoint = point.Position - ray.origin;
            var alongRay = Vector3.Dot(toPoint, ray.direction);
            if (alongRay < 0)
            {
                continue;
            }

            var closest = ray.origin + ray.direction * alongRay;
            var distance = Vector3.Distance(point.Position, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = point;
            }
        }

        return bestDistance <= maxDistance;
    }

    private (Vector3 origin, Vector3 direction) CreatePickRay(Point screenPoint)
    {
        return CameraMath.CreatePickRay(
            screenPoint,
            Viewport.ActualWidth,
            Viewport.ActualHeight,
            FieldOfViewDegrees,
            GetCameraPosition(),
            GetCameraTarget());
    }

    private void PanCamera(System.Windows.Vector delta)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        var movement = CameraMath.PanDelta(
            delta,
            Viewport.ActualHeight,
            FieldOfViewDegrees,
            viewModel.CameraDistance,
            target,
            eye);

        viewModel.Pan(movement.X, movement.Y, movement.Z);
    }

    private C3DHeightGrid? LoadDefaultC3DSample()
    {
        var path = FindDefaultC3DSamplePath();
        if (path is null)
        {
            return null;
        }

        try
        {
            return C3DHeightGrid.Load(path, viewModel.C3DMaxRenderedPoints);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static string? FindDefaultC3DSamplePath()
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, DefaultC3DSamplePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string ResolveRecipePath(string path, string recipeDirectory)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(recipeDirectory, path));
    }

    private void SetC3DSampleStatus()
    {
        if (c3dSample is null)
        {
            viewModel.C3DSamplePointCount = "(missing)";
            viewModel.C3DSampleSummary = $"Missing sample: {DefaultC3DSamplePath}";
            return;
        }

        viewModel.C3DSamplePointCount = c3dSample.Points.Length.ToString("N0", CultureInfo.InvariantCulture);
        viewModel.C3DSampleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{c3dSample.Width} x {c3dSample.Height} | rendered {c3dSample.Points.Length:N0} | density {viewModel.SelectedRenderDensity} | valid {c3dSample.ValidSampleCount:N0} | zero {c3dSample.ZeroSampleCount:N0} | min {c3dSample.Min:F3} | max {c3dSample.Max:F3}");
    }

    private void ReloadDefaultC3DSample()
    {
        c3dSample = LoadDefaultC3DSample();
        SetC3DSampleStatus();
        ConfigureC3DHeightDeviationRule();
    }

    private void ConfigureC3DHeightDeviationRule()
    {
        if (c3dSample is null)
        {
            return;
        }

        var result = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
            MainWindowViewModel.C3DEntityId,
            viewModel.RecipeSourceName,
            c3dSample.Min,
            c3dSample.Max,
            c3dSample.Mean,
            c3dSample.ValidSampleCount,
            viewModel.RecipePeakTolerance,
            viewModel.RecipeSourceUnit));

        viewModel.SetC3DHeightDeviationPreview(result);
    }

    private void WriteSceneContracts(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var lines = new List<string>
        {
            viewModel.SceneContractSummary,
            "SourceEntities"
        };

        lines.AddRange(viewModel.SourceEntities.Select(entity =>
            $"{entity.Id}|{entity.Kind}|unit={entity.Unit}|source={entity.SourcePath ?? "(generated)"}"));
        lines.Add("EntityLayers");
        lines.AddRange(viewModel.EntityLayers.Select(layer =>
            $"{layer.Id}|{layer.Kind}|visible={layer.IsVisible}|entities={string.Join(",", layer.EntityIds)}"));
        lines.Add("PreviewToolResult");
        var result = viewModel.PreviewToolResult;
        lines.Add($"{result.ToolName}|{result.Status}|metrics={result.Metrics.Count}|overlays={result.Overlays.Count}|message={result.Message}");
        lines.Add("PreviewMetrics");
        lines.AddRange(result.Metrics.Select(metric =>
            $"{metric.Name}|{metric.Kind}|value={metric.Value.ToString("F3", CultureInfo.InvariantCulture)}|unit={metric.Unit}|status={metric.Status?.ToString() ?? "(none)"}"));
        lines.Add("PreviewOverlays");
        lines.AddRange(result.Overlays.Select(overlay =>
            $"{overlay.Id}|{overlay.Kind}|label={overlay.Label}|status={overlay.Status?.ToString() ?? "(none)"}|source={overlay.SourceEntityId ?? "(none)"}"));
        lines.Add("ColorScaleLegend");
        lines.Add($"DeviationLegend|visible={viewModel.DeviationLegendVisible}|{viewModel.DeviationLegendStatus}|{viewModel.DeviationLegendPeak}|{viewModel.DeviationLegendTolerance}|{viewModel.DeviationLegendScale}");
        lines.Add("RenderControls");
        lines.Add($"PointSize|value={viewModel.PointSize.ToString("F1", CultureInfo.InvariantCulture)}");
        lines.Add($"RenderDensity|mode={viewModel.SelectedRenderDensity}|maxRenderedPoints={viewModel.C3DMaxRenderedPoints}|renderedC3DPoints={c3dSample?.Points.Length ?? 0}|summary={viewModel.RenderDensitySummary}");
        lines.Add("RecipeState");
        lines.Add($"RecipeTolerance|value={viewModel.RecipePeakTolerance.ToString("F3", CultureInfo.InvariantCulture)}|unit={viewModel.RecipeSourceUnit}");
        lines.Add($"RecipeSource|name={viewModel.RecipeSourceName}|path={viewModel.RecipeSourcePath}");
        lines.Add($"RecipeSave|summary={viewModel.RecipeSaveSummary}");
        lines.Add("PublishedResultEntities");
        lines.AddRange(viewModel.ResultEntities.Select(entity =>
            $"{entity.Id}|source={entity.SourceEntityId}|status={entity.Status}|metrics={entity.Metrics.Count}|overlays={entity.Overlays.Count}|message={entity.Message}"));
        lines.Add("PublishedMetrics");
        lines.AddRange(viewModel.ResultEntities.SelectMany(entity => entity.Metrics.Select(metric =>
            $"{entity.Id}|{metric.Name}|{metric.Kind}|value={metric.Value.ToString("F3", CultureInfo.InvariantCulture)}|unit={metric.Unit}|status={metric.Status?.ToString() ?? "(none)"}")));
        lines.Add("PublishedOverlays");
        lines.AddRange(viewModel.ResultEntities.SelectMany(entity => entity.Overlays.Select(overlay =>
            $"{entity.Id}|{overlay.Id}|{overlay.Kind}|label={overlay.Label}|status={overlay.Status?.ToString() ?? "(none)"}|source={overlay.SourceEntityId ?? "(none)"}")));

        File.WriteAllLines(path, lines);
    }

    private static string FormatC3DPoint(HeightGridPoint point) =>
        string.Create(CultureInfo.InvariantCulture, $"{CameraMath.FormatPoint(point.Position)} | raw {point.RawValue:F3}");

    private static void Quad(OpenGL gl, (double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) c, (double X, double Y, double Z) d)
    {
        gl.Vertex(a.X, a.Y, a.Z);
        gl.Vertex(b.X, b.Y, b.Z);
        gl.Vertex(c.X, c.Y, c.Z);
        gl.Vertex(d.X, d.Y, d.Z);
    }

    private static void Edge(OpenGL gl, (double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        gl.Vertex(a.X, a.Y, a.Z);
        gl.Vertex(b.X, b.Y, b.Z);
    }

    private static HeightGridPoint[] CreateGeneratedPointCloud()
    {
        const int columns = 55;
        const int rows = 41;
        var points = new HeightGridPoint[columns * rows];
        var index = 0;

        for (var row = 0; row < rows; row++)
        {
            var z = -2.0f + row * (4.0f / (rows - 1));
            for (var column = 0; column < columns; column++)
            {
                var localX = -2.2f + column * (4.4f / (columns - 1));
                var wave = 0.16 * Math.Sin(localX * 1.35) + 0.10 * Math.Cos(z * 1.8);
                var bump = 0.42 * Math.Exp(-((localX - 0.58) * (localX - 0.58) + (z + 0.32) * (z + 0.32)) / 0.32);
                var dent = -0.24 * Math.Exp(-((localX + 1.05) * (localX + 1.05) + (z - 0.88) * (z - 0.88)) / 0.24);
                var y = -0.70f + (float)(wave + bump + dent);
                var position = new Vector3(localX + 3.2f, y, z);
                var heightScalar = Clamp01((y + 1.05) / 0.86);
                var deviationScalar = Clamp01(Math.Abs(bump + dent) / 0.42);
                points[index++] = new HeightGridPoint(position, heightScalar, deviationScalar, y);
            }
        }

        return points;
    }

    private static (double R, double G, double B) HeightColor(double value)
    {
        var t = Clamp01(value);
        if (t < 0.5)
        {
            var local = t / 0.5;
            return (0.05, 0.35 + 0.55 * local, 0.95 - 0.30 * local);
        }

        var high = (t - 0.5) / 0.5;
        return (0.05 + 0.95 * high, 0.90 - 0.20 * high, 0.65 - 0.55 * high);
    }

    private static (double R, double G, double B) DeviationColor(double value)
    {
        var t = Clamp01(value);
        return (0.12 + 0.88 * t, 0.84 - 0.68 * t, 0.64 - 0.52 * t);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    private void RenderNow()
    {
        if (Viewport.IsLoaded)
        {
            Viewport.DoRender();
        }
    }

}
