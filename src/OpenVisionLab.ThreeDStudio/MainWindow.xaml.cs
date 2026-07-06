using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenVisionLab.ThreeDStudio.ViewModels;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeDStudio;

public partial class MainWindow : Window
{
    private const float CubeHalfSize = 1.0f;
    private const float FieldOfViewDegrees = 45.0f;

    private readonly GeneratedPoint[] pointCloud = CreateGeneratedPointCloud();
    private readonly string? smokeScreenshotPath;
    private readonly MainWindowViewModel viewModel = new();
    private bool isOrbiting;
    private bool isPanning;
    private Point lastMousePosition;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PointCloudPointCount = pointCloud.Length.ToString("N0", CultureInfo.InvariantCulture);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.CubeVisible)
                or nameof(MainWindowViewModel.PointCloudVisible)
                or nameof(MainWindowViewModel.MeasurementVisible)
                or nameof(MainWindowViewModel.SelectedColorMode)
                or nameof(MainWindowViewModel.SelectedSelectionMode)
                or nameof(MainWindowViewModel.SelectionOverlayVisible))
            {
                RenderNow();
            }
        };

        var args = Environment.GetCommandLineArgs();
        var smokeIndex = Array.IndexOf(args, "--smoke-screenshot");
        if (smokeIndex >= 0 && smokeIndex + 1 < args.Length)
        {
            smokeScreenshotPath = args[smokeIndex + 1];
            var sceneIndex = Array.IndexOf(args, "--smoke-scene");
            if (sceneIndex >= 0 && sceneIndex + 1 < args.Length && args[sceneIndex + 1].Equals("pointcloud", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.UsePointCloudSmokeScene();
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

            Loaded += SmokeCaptureOnLoaded;
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
            DrawPointCloud(gl);
        }

        if (viewModel.MeasurementVisible)
        {
            DrawMeasurement(gl);
        }

        if (viewModel.SelectionOverlayVisible)
        {
            DrawSelectionOverlay(gl);
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
                viewModel.PickCoordinate = FormatPoint(hit);
                viewModel.ViewerStatus = "Picked generated cube face";
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

    private async void SmokeCaptureOnLoaded(object sender, RoutedEventArgs e)
    {
        await Dispatcher.InvokeAsync(RenderNow);
        await Task.Delay(900);
        CaptureWindow(smokeScreenshotPath!);
        await Task.Delay(100);
        Application.Current.Shutdown(0);
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
        var target = GetCameraTarget();
        var yaw = DegreesToRadians(viewModel.YawDegrees);
        var pitch = DegreesToRadians(viewModel.PitchDegrees);
        var x = viewModel.CameraDistance * Math.Cos(pitch) * Math.Sin(yaw);
        var y = viewModel.CameraDistance * Math.Sin(pitch);
        var z = viewModel.CameraDistance * Math.Cos(pitch) * Math.Cos(yaw);
        return target + new Vector3((float)x, (float)y, (float)z);
    }

    private Vector3 GetCameraTarget()
    {
        return new Vector3((float)viewModel.CameraTargetX, (float)viewModel.CameraTargetY, (float)viewModel.CameraTargetZ);
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

    private void DrawMeasurement(OpenGL gl)
    {
        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.95, 0.22);
        gl.Begin(OpenGL.GL_LINES);

        gl.Vertex(-1.0, 1.35, 1.15);
        gl.Vertex(1.0, 1.35, 1.15);
        gl.Vertex(-1.0, 1.23, 1.15);
        gl.Vertex(-1.0, 1.47, 1.15);
        gl.Vertex(1.0, 1.23, 1.15);
        gl.Vertex(1.0, 1.47, 1.15);

        gl.End();
    }

    private void DrawPointCloud(OpenGL gl)
    {
        gl.PointSize(3.0f);
        gl.Begin(OpenGL.GL_POINTS);

        foreach (var point in pointCloud)
        {
            ApplyPointColor(gl, point);
            gl.Vertex(point.Position.X, point.Position.Y, point.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawPointCloudFrame(gl);
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

    private void ApplyPointColor(OpenGL gl, GeneratedPoint point)
    {
        var (r, g, b) = viewModel.SelectedColorMode switch
        {
            "Solid" => (0.62, 0.82, 1.0),
            "Deviation" => DeviationColor(point.DeviationScalar),
            _ => HeightColor(point.HeightScalar)
        };

        gl.Color(r, g, b);
    }

    private void DrawSelectionOverlay(OpenGL gl)
    {
        switch (viewModel.SelectedSelectionMode)
        {
            case "Box ROI":
                DrawBoxRoi(gl);
                break;
            case "Section Plane":
                DrawSectionPlane(gl);
                break;
            default:
                DrawSelectionPoint(gl);
                break;
        }
    }

    private void DrawSelectionPoint(OpenGL gl)
    {
        var point = new Vector3(3.78f, -0.28f, -0.32f);
        gl.PointSize(11.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.25, 0.25);
        gl.Vertex(point.X, point.Y, point.Z);
        gl.End();
        gl.PointSize(1.0f);

        gl.LineWidth(2.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(1.0, 1.0, 0.25);
        gl.Vertex(point.X - 0.18, point.Y, point.Z);
        gl.Vertex(point.X + 0.18, point.Y, point.Z);
        gl.Vertex(point.X, point.Y - 0.18, point.Z);
        gl.Vertex(point.X, point.Y + 0.18, point.Z);
        gl.Vertex(point.X, point.Y, point.Z - 0.18);
        gl.Vertex(point.X, point.Y, point.Z + 0.18);
        gl.End();
    }

    private void DrawBoxRoi(OpenGL gl)
    {
        var min = new Vector3(2.35f, -1.10f, -1.05f);
        var max = new Vector3(4.45f, -0.10f, 0.95f);
        gl.LineWidth(2.5f);
        gl.Color(1.0, 0.74, 0.18);
        gl.Begin(OpenGL.GL_LINES);
        BoxEdge(gl, min.X, min.Y, min.Z, max.X, min.Y, min.Z);
        BoxEdge(gl, max.X, min.Y, min.Z, max.X, min.Y, max.Z);
        BoxEdge(gl, max.X, min.Y, max.Z, min.X, min.Y, max.Z);
        BoxEdge(gl, min.X, min.Y, max.Z, min.X, min.Y, min.Z);
        BoxEdge(gl, min.X, max.Y, min.Z, max.X, max.Y, min.Z);
        BoxEdge(gl, max.X, max.Y, min.Z, max.X, max.Y, max.Z);
        BoxEdge(gl, max.X, max.Y, max.Z, min.X, max.Y, max.Z);
        BoxEdge(gl, min.X, max.Y, max.Z, min.X, max.Y, min.Z);
        BoxEdge(gl, min.X, min.Y, min.Z, min.X, max.Y, min.Z);
        BoxEdge(gl, max.X, min.Y, min.Z, max.X, max.Y, min.Z);
        BoxEdge(gl, max.X, min.Y, max.Z, max.X, max.Y, max.Z);
        BoxEdge(gl, min.X, min.Y, max.Z, min.X, max.Y, max.Z);
        gl.End();
    }

    private void DrawSectionPlane(OpenGL gl)
    {
        const double x = 3.25;
        const double y0 = -1.20;
        const double y1 = 0.20;
        const double z0 = -2.15;
        const double z1 = 2.15;

        gl.LineWidth(2.0f);
        gl.Color(0.96, 0.30, 0.86);
        gl.Begin(OpenGL.GL_LINES);
        for (var i = 0; i <= 8; i++)
        {
            var z = z0 + (z1 - z0) * i / 8.0;
            gl.Vertex(x, y0, z);
            gl.Vertex(x, y1, z);
            var y = y0 + (y1 - y0) * i / 8.0;
            gl.Vertex(x, y, z0);
            gl.Vertex(x, y, z1);
        }
        gl.End();
    }

    private bool TryPickCube(Point screenPoint, out Vector3 hit)
    {
        hit = default;

        if (!viewModel.CubeVisible || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
        {
            return false;
        }

        var ray = CreatePickRay(screenPoint);
        if (!IntersectUnitCube(ray.origin, ray.direction, out var distance))
        {
            return false;
        }

        hit = ray.origin + ray.direction * distance;
        return true;
    }

    private (Vector3 origin, Vector3 direction) CreatePickRay(Point screenPoint)
    {
        var width = (float)Math.Max(1.0, Viewport.ActualWidth);
        var height = (float)Math.Max(1.0, Viewport.ActualHeight);
        var x = (float)(2.0 * screenPoint.X / width - 1.0);
        var y = (float)(1.0 - 2.0 * screenPoint.Y / height);

        var eye = GetCameraPosition();
        var view = Matrix4x4.CreateLookAt(eye, GetCameraTarget(), Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)DegreesToRadians(FieldOfViewDegrees),
            width / height,
            0.1f,
            100.0f);

        Matrix4x4.Invert(view * projection, out var inverseViewProjection);

        var near = Vector3.Transform(new Vector3(x, y, 0.0f), inverseViewProjection);
        var far = Vector3.Transform(new Vector3(x, y, 1.0f), inverseViewProjection);
        return (near, Vector3.Normalize(far - near));
    }

    private void PanCamera(System.Windows.Vector delta)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        var forward = Vector3.Normalize(target - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var worldPerPixel = 2.0 * viewModel.CameraDistance * Math.Tan(DegreesToRadians(FieldOfViewDegrees) / 2.0) / Math.Max(1.0, Viewport.ActualHeight);
        var movement = right * (float)(-delta.X * worldPerPixel) + up * (float)(delta.Y * worldPerPixel);

        viewModel.Pan(movement.X, movement.Y, movement.Z);
    }

    private static bool IntersectUnitCube(Vector3 origin, Vector3 direction, out float distance)
    {
        distance = 0;
        var min = new Vector3(-CubeHalfSize, -CubeHalfSize, -CubeHalfSize);
        var max = new Vector3(CubeHalfSize, CubeHalfSize, CubeHalfSize);
        var tMin = 0.0f;
        var tMax = float.PositiveInfinity;

        for (var axis = 0; axis < 3; axis++)
        {
            var axisOrigin = GetAxis(origin, axis);
            var axisDirection = GetAxis(direction, axis);
            var axisMin = GetAxis(min, axis);
            var axisMax = GetAxis(max, axis);

            if (Math.Abs(axisDirection) < 0.00001f)
            {
                if (axisOrigin < axisMin || axisOrigin > axisMax)
                {
                    return false;
                }

                continue;
            }

            var t1 = (axisMin - axisOrigin) / axisDirection;
            var t2 = (axisMax - axisOrigin) / axisDirection;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax)
            {
                return false;
            }
        }

        distance = tMin;
        return true;
    }

    private static float GetAxis(Vector3 vector, int axis) => axis switch
    {
        0 => vector.X,
        1 => vector.Y,
        _ => vector.Z
    };

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

    private static void BoxEdge(OpenGL gl, double x1, double y1, double z1, double x2, double y2, double z2)
    {
        gl.Vertex(x1, y1, z1);
        gl.Vertex(x2, y2, z2);
    }

    private static string FormatPoint(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static GeneratedPoint[] CreateGeneratedPointCloud()
    {
        const int columns = 55;
        const int rows = 41;
        var points = new GeneratedPoint[columns * rows];
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
                points[index++] = new GeneratedPoint(position, heightScalar, deviationScalar);
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

internal readonly record struct GeneratedPoint(Vector3 Position, double HeightScalar, double DeviationScalar);
