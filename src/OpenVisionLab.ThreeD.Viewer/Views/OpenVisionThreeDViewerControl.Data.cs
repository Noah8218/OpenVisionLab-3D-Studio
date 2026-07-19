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
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public string? CurrentC3DSourcePath => c3dSample?.SourcePath;

    /// <summary>
    /// Loads a C3D source for recipe teaching. This only changes Viewer source
    /// state; it does not configure, preview, publish, or run an inspection.
    /// </summary>
    public bool LoadC3DSource(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("C3D source was not found.", fullPath);
            }

            c3dSample = C3DHeightGrid.Load(fullPath, viewModel.C3DMaxRenderedPoints);
            ClearTeachingSelectionsForSourceChange();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            ClearWarpageTransientInspectionState();
            viewModel.ClearThicknessPreview();
            viewModel.ClearWarpagePreview();
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            SetC3DSampleStatus();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(
                ModelTransform.Identity,
                "C3D grid-index scalar frame",
                Path.GetFileNameWithoutExtension(fullPath));
            viewModel.ViewerStatus = $"C3D source loaded for teaching: {Path.GetFileName(fullPath)}";
            RenderNow();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"C3D source load failed: {exception.Message}";
            RenderNow();
            return false;
        }
    }

    /// <summary>
    /// Clears stale C3D geometry when a teaching recipe has no trusted source.
    /// This does not modify the authored recipe.
    /// </summary>
    public void ClearC3DTeachingSource(string status)
    {
        c3dSample = null;
        ClearTeachingSelectionsForSourceChange();
        planeFlatnessEvaluation = null;
        planeReferenceMeasurement = null;
        ClearWarpageTransientInspectionState();
        SetC3DSampleStatus();
        viewModel.UseEmptyTeachingScene(status);
        RenderNow();
    }

    /// <summary>
    /// Displays a verified, same-grid C3D workbench result without changing
    /// the authored recipe source or clearing recipe-owned selections.
    /// </summary>
    public bool ShowC3DWorkbenchResult(string path, string label)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            c3dSample = C3DHeightGrid.Load(fullPath, viewModel.C3DMaxRenderedPoints);
            SetC3DSampleStatus();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(ModelTransform.Identity, "C3D grid-index scalar frame", label);
            viewModel.ViewerStatus = $"Workbench display: {label}";
            RenderNow();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"Workbench result display failed: {exception.Message}";
            RenderNow();
            return false;
        }
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

    private ImportedMesh? LoadDefaultGlbSample()
    {
        var path = FindDefaultGlbSamplePath();
        return path is null ? null : LoadGlbSample(path);
    }

    private LazPointCloudMetadata? LoadDefaultLazSample()
    {
        var path = FindDefaultLazSamplePath();
        return path is null ? null : LoadLazSample(path);
    }

    private ImportedMesh? LoadGlbSample(string path)
    {
        ResetImportedMeshTextureUpload();
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "GLB");
        if (!File.Exists(candidate))
        {
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = $"Missing GLB sample: {path}";
            return null;
        }

        try
        {
            var mesh = GlbMesh.Load(candidate);
            viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "GLB");
            return mesh;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            viewModel.GlbSampleTriangleCount = "(unsupported)";
            viewModel.GlbSampleSummary = $"Unsupported or corrupt GLB: {ex.Message}";
            return null;
        }
    }

    private ImportedMesh? LoadStlSample(string path)
    {
        ResetImportedMeshTextureUpload();
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "STL");
        if (!File.Exists(candidate))
        {
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = $"Missing STL sample: {path}";
            return null;
        }

        try
        {
            var mesh = StlMesh.Load(candidate);
            viewModel.SetGlbSampleSource(path, Path.GetFileNameWithoutExtension(path), "STL");
            return mesh;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or OverflowException)
        {
            viewModel.GlbSampleTriangleCount = "(unsupported)";
            viewModel.GlbSampleSummary = $"Unsupported or corrupt STL: {ex.Message}";
            return null;
        }
    }

    private void ResetImportedMeshTextureUpload()
    {
        importedMeshTextureSource = null;
        importedMeshTextureId = 0;
        importedMeshTextureUploadFailed = false;
        importedMeshTextureUploadSummary = "texture none";
    }

    private LazPointCloudMetadata? LoadLazSample(string path)
    {
        lazPointCloud = null;
        lazViewerOrigin = default;
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
        if (!File.Exists(candidate))
        {
            viewModel.LazSamplePointCount = "(missing)";
            viewModel.LazSampleSummary = $"Missing LAZ/LAS sample: {path}";
            return null;
        }

        try
        {
            var metadata = LazPointCloudMetadata.Load(candidate);
            SetLazViewerOrigin(metadata);
            viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
            return metadata;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            viewModel.LazSamplePointCount = "(unsupported)";
            viewModel.LazSampleSummary = $"Unsupported or corrupt LAZ/LAS: {ex.Message}";
            lazViewerOrigin = default;
            return null;
        }
    }

    private LazPointCloud? LoadLazPointCloud(string path) => LoadLazPointCloud(path, viewModel.LazMaxSampledPoints);

    private LazPointCloud? LoadLazPointCloud(string path, int maxSampledPoints)
    {
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
        if (!File.Exists(candidate))
        {
            viewModel.LazSamplePointCount = "(missing)";
            viewModel.LazSampleSummary = $"Missing LAZ/LAS sample: {path}";
            lazViewerOrigin = default;
            return null;
        }

        try
        {
            var loadStart = Stopwatch.GetTimestamp();
            var pointCloud = LazPointCloud.Load(candidate, Math.Max(2, maxSampledPoints));
            var loadMilliseconds = Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds;
            SetLazViewerOrigin(pointCloud.Metadata);
            viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
            viewModel.SetLazSamplingTelemetry(
                pointCloud.DecodedPointCount,
                pointCloud.SampledPoints.Length,
                pointCloud.SampleStride,
                loadMilliseconds);
            return pointCloud;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            viewModel.LazSamplePointCount = "(unsupported)";
            viewModel.LazSampleSummary = $"Unsupported or corrupt LAZ/LAS point decode: {ex.Message}";
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: load failed");
            lazViewerOrigin = default;
            return null;
        }
    }

    private void ReloadCurrentLazPointCloud()
    {
        if (lazPointCloud is null)
        {
            return;
        }

        var sourcePath = lazPointCloud.SourcePath;
        var reloaded = LoadLazPointCloud(sourcePath);
        if (reloaded is null)
        {
            viewModel.UseLazFailureScene(viewModel.LazSampleSummary);
            return;
        }

        lazPointCloud = reloaded;
        lazSample = reloaded.Metadata;
        selectedLazPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        viewModel.ClearTwoPointMeasurement();
        SetLazSampleStatus();
        viewModel.SelectionSummary = "Point selection: reset after point-cloud density change";
        viewModel.MeasurementSummary = "Distance and height delta: reset after point-cloud density change";
        viewModel.PickCoordinate = "(none)";
        viewModel.ViewerStatus = $"Point cloud re-sampled: {viewModel.SelectedRenderDensity}";
    }

    private void SetLazViewerOrigin(LazPointCloudMetadata metadata)
    {
        lazViewerOrigin = (
            (metadata.MinX + metadata.MaxX) * 0.5,
            (metadata.MinY + metadata.MaxY) * 0.5,
            (metadata.MinZ + metadata.MaxZ) * 0.5);
        var corners = GetLazBoundsCorners(metadata);
        var min = corners[0];
        var max = corners[0];
        foreach (var corner in corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        viewModel.SetLazSampleBounds(min, max);
    }

    private static string ReadRecipeType(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("recipeType", out var recipeType)
            ? recipeType.GetString() ?? throw new InvalidDataException($"Recipe type is empty: {path}")
            : throw new InvalidDataException($"Recipe type is missing: {path}");
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

    private static string? FindDefaultGlbSamplePath()
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, DefaultGlbSamplePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string? FindDefaultLazSamplePath()
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, DefaultLazSamplePath);
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
        ResetProfileIfSourceChanged();
        if (c3dSample is null)
        {
            InvalidateC3DRenderProxy();
            viewModel.SetC3DDisplayCapabilities(surfaceGeometryAvailable: false);
            viewModel.C3DSamplePointCount = "(missing)";
            viewModel.C3DSampleSummary = $"Missing sample: {DefaultC3DSamplePath}";
            viewModel.ClearC3DHeightDistribution();
            viewModel.ClearHeightMap();
            viewModel.ClearSectionProfile();
            return;
        }

        var renderProxy = GetC3DRenderProxy();
        viewModel.SetC3DDisplayCapabilities(renderProxy.HasSurface);
        viewModel.C3DSamplePointCount = c3dSample.Points.Length.ToString("N0", CultureInfo.InvariantCulture);
        viewModel.C3DSampleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{c3dSample.Width} x {c3dSample.Height} | rendered {c3dSample.Points.Length:N0} | density {viewModel.SelectedRenderDensity} | valid {c3dSample.ValidSampleCount:N0} | zero {c3dSample.ZeroSampleCount:N0} | min {c3dSample.Min:F3} | max {c3dSample.Max:F3}");
        viewModel.SetC3DHeightDistribution(
            c3dSample.HeightDistribution,
            c3dSample.ContentSha256);
        UpdateHeightMapFromC3D();
        UpdateSectionProfileFromC3D();
    }

    private void SetGlbSampleStatus()
    {
        viewModel.SetImportedMeshDisplayCapabilities(
            importedMesh is { } mesh && (mesh.HasVertexColors || mesh.HasBaseColorTexture));

        if (importedMesh is null)
        {
            viewModel.GlbSampleTriangleCount = "(missing)";
            viewModel.GlbSampleSummary = $"Missing sample: {DefaultGlbSamplePath}";
            return;
        }

        viewModel.GlbSampleTriangleCount = importedMesh.TriangleCount.ToString("N0", CultureInfo.InvariantCulture);
        var colorSummary = importedMesh.HasVertexColors
            ? $"vertex colors {importedMesh.VertexColors.Length:N0}"
            : "vertex colors none";
        var textureSummary = importedMesh.HasBaseColorTexture
            ? $"texture {importedMesh.BaseColorTexture!.MimeType} {importedMesh.BaseColorTexture.Bytes.Length:N0} bytes | texcoords {importedMesh.TextureCoordinates.Length:N0}"
            : "texture none";
        viewModel.GlbSampleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileName(importedMesh.SourcePath)} | format {importedMesh.Format} | vertices {importedMesh.Positions.Length:N0} | triangles {importedMesh.TriangleCount:N0} | {colorSummary} | {textureSummary} | bounds {FormatVector(importedMesh.Min)} to {FormatVector(importedMesh.Max)}");
        viewModel.SetGlbSampleSource(importedMesh.SourcePath, Path.GetFileNameWithoutExtension(importedMesh.SourcePath), importedMesh.Format);
        viewModel.SetGlbSampleBounds(importedMesh.Min, importedMesh.Max);
    }

    private void SetLazSampleStatus()
    {
        viewModel.SetLazDisplayCapabilities(lazPointCloud?.HasRgb == true);

        if (lazSample is null)
        {
            viewModel.LazSamplePointCount = "(missing)";
            viewModel.LazSampleSummary = $"Missing sample: {DefaultLazSamplePath}";
            viewModel.SetLazHeightRange(double.NaN, double.NaN, "source-z");
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: not loaded");
            return;
        }

        viewModel.LazSamplePointCount = lazSample.PointCount.ToString("N0", CultureInfo.InvariantCulture);
        if (lazPointCloud is null)
        {
            viewModel.LazSampleSummary = $"{lazSample.FormatSummary()} | metadata only; point rendering pending";
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: metadata only");
        }
        else
        {
            viewModel.LazSamplePointCount = string.Create(
                CultureInfo.InvariantCulture,
                $"{lazPointCloud.DecodedPointCount:N0} / sampled {lazPointCloud.SampledPoints.Length:N0}");
            viewModel.LazSampleSummary = string.Create(
                CultureInfo.InvariantCulture,
                $"{Path.GetFileName(lazPointCloud.SourcePath)} | decoded {lazPointCloud.DecodedPointCount:N0} | sampled {lazPointCloud.SampledPoints.Length:N0} | density {viewModel.SelectedRenderDensity} | load {viewModel.LazLoadMilliseconds:F0} ms | sample {viewModel.LazSamplePercent:F2}% | RGB {lazPointCloud.HasRgb} | bounds match {lazPointCloud.BoundsMatch}");
        }

        viewModel.SetLazSampleSource(lazSample.SourcePath, Path.GetFileNameWithoutExtension(lazSample.SourcePath));
        viewModel.SetLazHeightRange(lazSample.MinZ, lazSample.MaxZ, "source-z");
    }

    private bool EnsureImportedMeshTexture(OpenGL gl)
    {
        if (importedMesh is null || !importedMesh.HasBaseColorTexture)
        {
            return false;
        }

        if (ReferenceEquals(importedMeshTextureSource, importedMesh))
        {
            return importedMeshTextureId != 0;
        }

        if (importedMeshTextureUploadFailed)
        {
            return false;
        }

        try
        {
            var texture = DecodeTexture(importedMesh.BaseColorTexture!.Bytes);
            var ids = new uint[1];
            gl.GenTextures(1, ids);
            importedMeshTextureId = ids[0];
            gl.BindTexture(GlTexture2D, importedMeshTextureId);
            gl.TexParameter(GlTexture2D, GlTextureMinFilter, (int)GlLinear);
            gl.TexParameter(GlTexture2D, GlTextureMagFilter, (int)GlLinear);
            gl.TexParameter(GlTexture2D, GlTextureWrapS, (int)GlRepeat);
            gl.TexParameter(GlTexture2D, GlTextureWrapT, (int)GlRepeat);
            gl.PixelStore(GlUnpackAlignment, 1);
            gl.TexImage2D(
                GlTexture2D,
                0,
                GlRgba,
                texture.Width,
                texture.Height,
                0,
                GlBgra,
                GlUnsignedByte,
                texture.Pixels);
            importedMeshTextureSource = importedMesh;
            importedMeshTextureUploadSummary = string.Create(
                CultureInfo.InvariantCulture,
                $"uploaded {texture.Width}x{texture.Height} {importedMesh.BaseColorTexture.MimeType}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or NotSupportedException)
        {
            importedMeshTextureUploadFailed = true;
            importedMeshTextureUploadSummary = $"upload failed: {ex.Message}";
            return false;
        }
    }

    private static (int Width, int Height, byte[] Pixels) DecodeTexture(byte[] encodedImage)
    {
        using var stream = new MemoryStream(encodedImage);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
        {
            throw new InvalidOperationException("Texture image has no frames.");
        }

        BitmapSource source = decoder.Frames[0];
        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        }

        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return (source.PixelWidth, source.PixelHeight, pixels);
    }

    private void UpdateHeightMapFromC3D()
    {
        if (c3dSample is null || c3dSample.Points.Length == 0)
        {
            viewModel.ClearHeightMap();
            return;
        }

        const int pixelWidth = 240;
        const int pixelHeight = 72;
        var pixels = new byte[pixelWidth * pixelHeight * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 31;
            pixels[index + 1] = 24;
            pixels[index + 2] = 17;
            pixels[index + 3] = 255;
        }

        foreach (var point in c3dSample.Points)
        {
            var x = (point.Position.X + c3dSample.XHalfExtent) / Math.Max(0.0001f, c3dSample.XHalfExtent * 2.0f);
            var z = (point.Position.Z + c3dSample.ZHalfExtent) / Math.Max(0.0001f, c3dSample.ZHalfExtent * 2.0f);
            var column = (int)Math.Round(Math.Clamp(x, 0.0f, 1.0f) * (pixelWidth - 1));
            var row = (int)Math.Round(Math.Clamp(z, 0.0f, 1.0f) * (pixelHeight - 1));
            PaintHeightMapPixel(pixels, pixelWidth, pixelHeight, column, row, point.HeightScalar);
        }

        var bitmap = BitmapSource.Create(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            pixelWidth * 4);
        bitmap.Freeze();

        viewModel.SetHeightMap(
            bitmap,
            c3dSample.Width,
            c3dSample.Height,
            c3dSample.Points.Length,
            c3dSample.Min,
            c3dSample.Max,
            c3dSample.Mean,
            pixelWidth,
            pixelHeight);
    }

    private static void PaintHeightMapPixel(byte[] pixels, int pixelWidth, int pixelHeight, int column, int row, double heightScalar)
    {
        var (r, g, b) = HeightMapColor(heightScalar);
        for (var y = Math.Max(0, row - 1); y <= Math.Min(pixelHeight - 1, row + 1); y++)
        {
            for (var x = Math.Max(0, column - 1); x <= Math.Min(pixelWidth - 1, column + 1); x++)
            {
                var index = (y * pixelWidth + x) * 4;
                pixels[index] = b;
                pixels[index + 1] = g;
                pixels[index + 2] = r;
                pixels[index + 3] = 255;
            }
        }
    }

    private static (byte R, byte G, byte B) HeightMapColor(double value)
    {
        var (r, g, b) = C3DPointMapPalette.Height(value);
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private void UpdateSectionProfileFromC3D()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ClearSectionProfile();
            return;
        }

        var centerZ = c3dSample.Points.MinBy(point => Math.Abs(point.Position.Z)).Position.Z;
        var samples = c3dSample.Points
            .Where(point => Math.Abs(point.Position.Z - centerZ) < 0.0005f)
            .OrderBy(point => point.Position.X)
            .ToArray();

        if (samples.Length < 2)
        {
            viewModel.ClearSectionProfile();
            return;
        }

        var min = samples.Min(point => point.RawValue);
        var max = samples.Max(point => point.RawValue);
        var mean = samples.Average(point => point.RawValue);
        var rowIndex = EstimateProfileRowIndex(centerZ);
        viewModel.SetSectionProfile(
            "C3D Thickness Sample",
            rowIndex,
            samples.Length,
            min,
            max,
            mean,
            BuildSectionProfilePath(samples, min, max));
    }

    private int EstimateProfileRowIndex(float z)
    {
        if (c3dSample is null || c3dSample.ZHalfExtent <= 0.0f)
        {
            return 0;
        }

        var normalized = (z + c3dSample.ZHalfExtent) / (c3dSample.ZHalfExtent * 2.0f);
        return (int)Math.Round(Math.Clamp(normalized, 0.0f, 1.0f) * (c3dSample.Height - 1));
    }

    private static string BuildSectionProfilePath(IReadOnlyList<HeightGridPoint> samples, double min, double max)
    {
        const double chartWidth = 240.0;
        const double chartHeight = 54.0;
        const double padding = 3.0;
        var span = Math.Max(0.001, max - min);
        var stride = Math.Max(1, (int)Math.Ceiling(samples.Count / 80.0));
        var reduced = samples.Where((_, index) => index % stride == 0).ToList();
        if (reduced[^1] != samples[^1])
        {
            reduced.Add(samples[^1]);
        }

        var builder = new StringBuilder();
        for (var index = 0; index < reduced.Count; index++)
        {
            var sample = reduced[index];
            var x = reduced.Count == 1 ? 0.0 : chartWidth * index / (reduced.Count - 1);
            var y = padding + (1.0 - ((sample.RawValue - min) / span)) * (chartHeight - padding * 2.0);
            builder.Append(index == 0 ? "M " : " L ");
            builder.Append(x.ToString("F1", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(y.ToString("F1", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private void ReloadDefaultC3DSample()
    {
        var sourcePath = c3dSample?.SourcePath ?? FindDefaultC3DSamplePath();
        var pointPairStep = viewModel.CreatePointPairDimensionsRecipeStep();
        var restoreThicknessPreview = viewModel.ThicknessVisible;
        var restoreWarpagePreview = viewModel.WarpageVisible;
        var restorePointPairPreview = viewModel.PointPairDimensionsVisible;
        var restoreFlatnessPreview = viewModel.PlaneFlatnessVisible;
        var restoreVolumePreview = viewModel.VolumeVisible;
        var restoreCrossSectionPreview = viewModel.CrossSectionVisible;
        try
        {
            c3dSample = string.IsNullOrWhiteSpace(sourcePath)
                ? null
                : C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            c3dSample = null;
            viewModel.ViewerStatus = $"C3D render-density reload failed: {ex.Message}";
        }

        twoPointFirst = null;
        twoPointSecond = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearRoiStepMeasurement();
        SetC3DSampleStatus();
        if (c3dSample is not null && restoreThicknessPreview)
        {
            PreviewC3DThickness();
        }
        else if (c3dSample is not null && restoreWarpagePreview)
        {
            PreviewC3DWarpage();
        }
        else if (c3dSample is not null && pointPairStep is not null)
        {
            try
            {
                var first = c3dSample.ReadPoint(pointPairStep.First.Row, pointPairStep.First.Column);
                var second = c3dSample.ReadPoint(pointPairStep.Second.Row, pointPairStep.Second.Column);
                SetTwoPointMeasurement(first, second, updatePointPairReferences: false);
                if (restorePointPairPreview)
                {
                    PreviewC3DPointPairDimensions();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
            {
                viewModel.InvalidatePointPairDimensionsPreview($"C3D reload invalidated point references: {ex.Message}");
            }
        }
        else if (restoreCrossSectionPreview)
        {
            PreviewC3DCrossSection();
        }
        else if (restoreVolumePreview)
        {
            PreviewC3DVolume();
        }
        else if (restoreFlatnessPreview)
        {
            PreviewC3DPlaneFlatness();
        }
        else
        {
            ConfigureC3DHeightDeviationRule();
        }
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

}
