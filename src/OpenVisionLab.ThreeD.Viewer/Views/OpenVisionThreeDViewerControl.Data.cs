using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
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
    public string? CurrentC3DSourcePath => c3dSample?.SourcePath;

    public string? CurrentViewerOnlySourcePath { get; private set; }

    public string? CurrentViewerOnlySourceFormat { get; private set; }

    /// <summary>
    /// Imports a verified mesh or point-cloud source into the Viewer only.
    /// The current recipe source and inspection lifecycle are not changed.
    /// </summary>
    public async Task<bool> LoadViewerOnlySourceAsync(
        string path,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        using var operation = sourceLoadOperations.Begin(cancellationToken);
        IProgress<double>? operationProgress = progress is null
            ? null
            : new Progress<double>(value =>
            {
                if (operation.IsCurrent)
                {
                    progress.Report(value);
                }
            });
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                if (operation.IsCurrent)
                {
                    viewModel.ViewerStatus = $"3D import failed; current source retained: file not found ({Path.GetFileName(fullPath)})";
                    RenderNow();
                }

                return false;
            }

            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            operationProgress?.Report(0.0);
            switch (extension)
            {
                case ".glb":
                case ".stl":
                {
                    var mesh = await Task.Run(
                        () => extension == ".glb"
                            ? GlbMesh.Load(fullPath, operation.Token, operationProgress)
                            : StlMesh.Load(fullPath, operation.Token, operationProgress),
                        operation.Token);
                    operation.Token.ThrowIfCancellationRequested();
                    operationProgress?.Report(90.0);
                    if (!sourceLoadOperations.TryApply(
                            operation,
                            () => ApplyViewerOnlyMesh(mesh, extension == ".glb" ? "GLB" : "STL")))
                    {
                        operation.Token.ThrowIfCancellationRequested();
                        return false;
                    }

                    if (!operation.IsCurrent)
                    {
                        return false;
                    }

                    break;
                }
                case ".las":
                case ".laz":
                {
                    var pointCloud = await LoadLazPointCloudAsync(
                        fullPath,
                        viewModel.LazMaxSampledPoints,
                        operation.Token,
                        operationProgress,
                        () => operation.IsCurrent && !operation.IsCancellationRequested);
                    operation.Token.ThrowIfCancellationRequested();
                    if (pointCloud is null)
                    {
                        return false;
                    }

                    if (!sourceLoadOperations.TryApply(
                            operation,
                            () => ApplyViewerOnlyPointCloud(
                                pointCloud,
                                extension == ".las" ? "LAS" : "LAZ")))
                    {
                        operation.Token.ThrowIfCancellationRequested();
                        return false;
                    }

                    if (!operation.IsCurrent)
                    {
                        return false;
                    }

                    break;
                }
                default:
                    throw new NotSupportedException($"The '{extension}' format is not available in 3D Import.");
            }

            operationProgress?.Report(100.0);
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!operation.IsCurrent || !operation.IsExternalCancellationRequested)
            {
                return false;
            }

            viewModel.ViewerStatus = "3D import cancelled; current source retained.";
            RenderNow();

            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException or FormatException or OverflowException)
        {
            if (operation.IsCurrent)
            {
                viewModel.ViewerStatus = $"3D import failed; current source retained: {exception.Message}";
                RenderNow();
            }

            return false;
        }
    }

    private void ApplyViewerOnlyMesh(ImportedMesh mesh, string format)
    {
        ResetImportedMeshTextureUpload();
        importedMesh = mesh;
        selectedImportedMeshPoint = null;
        selectedImportedMeshTriangleIndex = null;
        selectedImportedMeshSurfaceNormal = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        viewModel.ClearTwoPointMeasurement();
        SetGlbSampleStatus();
        viewModel.UseGlbSmokeScene();
        CurrentViewerOnlySourcePath = Path.GetFullPath(mesh.SourcePath);
        CurrentViewerOnlySourceFormat = format;
        viewModel.ViewerStatus = $"{format} imported for Viewer only; recipe source unchanged: {Path.GetFileName(mesh.SourcePath)}";
        RenderNow();
    }

    private void ApplyViewerOnlyPointCloud(LazPointCloud pointCloud, string format)
    {
        lazSourceState.SetPointCloud(pointCloud);
        selectedLazPoint = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        viewModel.ClearTwoPointMeasurement();
        SetLazSampleStatus();
        viewModel.UseLazPointSmokeScene();
        viewModel.SelectedEntity = $"{Path.GetFileName(pointCloud.SourcePath)} ({format})";
        viewModel.SelectionSummary = $"Point selection: {format} sampled point cloud";
        CurrentViewerOnlySourcePath = Path.GetFullPath(pointCloud.SourcePath);
        CurrentViewerOnlySourceFormat = format;
        viewModel.ViewerStatus = $"{format} imported for Viewer only; recipe source unchanged: {Path.GetFileName(pointCloud.SourcePath)}";
        RenderNow();
    }

    public bool TryGetCurrentC3DSourceBinding(
        string path,
        out ToolRecipeSelectionSourceBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (c3dSample is null
            || !string.Equals(
                Path.GetFullPath(c3dSample.SourcePath),
                fullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            binding = null!;
            return false;
        }

        binding = ToolRecipeSelectionSourceBindingVerifier.FromHeightGrid(c3dSample);
        return true;
    }

    public C3DSourceLoadPerformance? LastC3DSourceLoadPerformance { get; private set; }

    public C3DSourceApplyPerformance? LastC3DSourceApplyPerformance { get; private set; }

    /// <summary>
    /// Loads a C3D source for recipe teaching. This only changes Viewer source
    /// state; it does not configure, preview, publish, or run an inspection.
    /// </summary>
    public bool LoadC3DSource(string path)
    {
        using var operation = sourceLoadOperations.Begin();
        try
        {
            LastC3DSourceLoadPerformance = null;
            var fullPath = C3DSourceLoadPreparation.GetExistingPath(path);
            var loaded = C3DHeightGrid.Load(fullPath, viewModel.C3DMaxRenderedPoints);
            return sourceLoadOperations.TryApply(
                operation,
                () => ApplyLoadedC3DSource(loaded, fullPath));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (operation.IsCurrent)
            {
                viewModel.ViewerStatus = $"C3D source load failed: {exception.Message}";
                RenderNow();
            }

            return false;
        }
    }

    public async Task<bool> LoadC3DSourceAsync(
        string path,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        using var operation = sourceLoadOperations.Begin(cancellationToken);
        IProgress<double>? operationProgress = progress is null
            ? null
            : new Progress<double>(value =>
            {
                if (operation.IsCurrent)
                {
                    progress.Report(value);
                }
            });
        try
        {
            var prepared = await C3DSourceLoadPreparation.LoadAsync(
                path,
                viewModel.C3DMaxRenderedPoints,
                operation.Token,
                operationProgress);
            operation.Token.ThrowIfCancellationRequested();
            if (!operation.IsCurrent)
            {
                return false;
            }

            var fullPath = prepared.FullPath;
            var applied = false;
            var applyMilliseconds = 0.0;
            applied = sourceLoadOperations.TryApply(
                operation,
                () =>
                {
                    var applyStart = Stopwatch.GetTimestamp();
                    ApplyLoadedC3DSource(
                        prepared.Grid,
                        fullPath,
                        prepared.RenderProxy,
                        prepared.Positions);
                    applyMilliseconds = Stopwatch.GetElapsedTime(applyStart).TotalMilliseconds;
                });
            if (!applied)
            {
                operation.Token.ThrowIfCancellationRequested();
                return false;
            }

            if (!operation.IsCurrent)
            {
                return false;
            }

            LastC3DSourceLoadPerformance = new C3DSourceLoadPerformance(
                prepared.Grid.LoadPerformance,
                prepared.TopologyMilliseconds,
                prepared.PositionsMilliseconds,
                prepared.WorkerMilliseconds,
                applyMilliseconds)
            {
                ApplyDetail = LastC3DSourceApplyPerformance
            };
            operationProgress?.Report(100.0);
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!operation.IsCurrent || !operation.IsExternalCancellationRequested)
            {
                return false;
            }

            viewModel.ViewerStatus = "C3D source load cancelled; current source retained.";
            RenderNow();

            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (operation.IsCurrent)
            {
                viewModel.ViewerStatus = $"C3D source load failed; current source retained: {exception.Message}";
                RenderNow();
            }

            return false;
        }
    }

    private void ApplyLoadedC3DSource(
        C3DHeightGrid loaded,
        string fullPath,
        C3DHeightGridRenderProxy? preparedRenderProxy = null,
        Vector3[]? preparedPositions = null)
    {
        var totalStart = Stopwatch.GetTimestamp();
        var displayListBuildCountBefore = c3dDisplayListBuildCount;
        var sourceStateMilliseconds = 0.0;
        var clearStateMilliseconds = 0.0;
        var sampleStatusMilliseconds = 0.0;
        var sceneMilliseconds = 0.0;
        var displayMilliseconds = 0.0;
        var alignmentMilliseconds = 0.0;
        var statusMilliseconds = 0.0;
        var finalRenderMilliseconds = 0.0;
        c3dSourceApplyRenderRequestCount = 0;
        c3dSourceApplySuppressedRenderRequestCount = 0;
        c3dSourceApplyRenderExecutionCount = 0;
        c3dSourceApplyRenderExecutionMilliseconds = 0.0;
        c3dSourceApplyActive = true;
        c3dSourceApplyRenderSuppressed = true;
        try
        {
            var stageStart = Stopwatch.GetTimestamp();
            c3dSample = loaded;
            CurrentViewerOnlySourcePath = null;
            CurrentViewerOnlySourceFormat = null;
            if (preparedRenderProxy is not null && preparedPositions is not null)
            {
                c3dRenderProxyCache.Set(loaded, preparedRenderProxy);
                c3dRenderPositionCache.Set(
                    preparedRenderProxy,
                    ModelTransform.Identity,
                    preparedPositions);
                c3dGpuReleasePending = c3dGpuBuffers is not null;
                c3dGpuBufferKey = null;
                c3dGpuFailedKey = null;
                c3dDisplayListKey = null;
                c3dInteractionDisplayListKey = null;
                pendingC3DDisplayListBuildReason = "source-applied";
            }
            ResetInteractionWireframeLodForSourceChange(sourceApplied: true);
            sourceStateMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
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
            clearStateMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
            SetC3DSampleStatus();
            sampleStatusMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
            viewModel.UseC3DSmokeScene();
            sceneMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
            viewModel.Display.ResetC3DHeightGridGeometryStyle();
            displayMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
            viewModel.SetC3DAlignment(
                ModelTransform.Identity,
                "C3D grid-index scalar frame",
                Path.GetFileNameWithoutExtension(fullPath));
            TryFitCurrentC3D(
                useTopInspectionView: true,
                "C3D source fitted to top inspection view");
            alignmentMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
            viewModel.ViewerStatus = $"C3D source loaded for teaching: {Path.GetFileName(fullPath)}";
            statusMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;

            stageStart = Stopwatch.GetTimestamp();
            c3dSourceApplyRenderSuppressed = false;
            RenderNow();
            finalRenderMilliseconds = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
        }
        finally
        {
            c3dSourceApplyRenderSuppressed = false;
            c3dSourceApplyActive = false;
            LastC3DSourceApplyPerformance = new C3DSourceApplyPerformance(
                sourceStateMilliseconds,
                clearStateMilliseconds,
                sampleStatusMilliseconds,
                sceneMilliseconds,
                displayMilliseconds,
                alignmentMilliseconds,
                statusMilliseconds,
                finalRenderMilliseconds,
                Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds,
                c3dSourceApplyRenderRequestCount,
                c3dSourceApplySuppressedRenderRequestCount,
                c3dSourceApplyRenderExecutionCount,
                c3dSourceApplyRenderExecutionMilliseconds,
                c3dDisplayListBuildCount - displayListBuildCountBefore,
                lastC3DDisplayListBuildReason);
        }
    }

    /// <summary>
    /// Clears stale C3D geometry when a teaching recipe has no trusted source.
    /// This does not modify the authored recipe.
    /// </summary>
    public void ClearC3DTeachingSource(string status)
    {
        using var operation = sourceLoadOperations.Begin();
        sourceLoadOperations.TryApply(
            operation,
            () =>
            {
                ResetInteractionWireframeLodForSourceChange(sourceApplied: false);
                c3dSample = null;
                ClearTeachingSelectionsForSourceChange();
                planeFlatnessEvaluation = null;
                planeReferenceMeasurement = null;
                ClearWarpageTransientInspectionState();
                SetC3DSampleStatus();
                viewModel.UseEmptyTeachingScene(status);
                RenderNow();
            });
    }

    /// <summary>
    /// Displays a verified, same-grid C3D workbench result without changing
    /// the authored recipe source or clearing recipe-owned selections.
    /// </summary>
    public bool ShowC3DWorkbenchResult(string path, string label)
    {
        using var operation = sourceLoadOperations.Begin();
        try
        {
            var fullPath = Path.GetFullPath(path);
            var loaded = C3DHeightGrid.Load(fullPath, viewModel.C3DMaxRenderedPoints);
            return sourceLoadOperations.TryApply(
                operation,
                () =>
                {
                    c3dSample = loaded;
                    SetC3DSampleStatus();
                    viewModel.UseC3DSmokeScene();
                    viewModel.SetC3DAlignment(ModelTransform.Identity, "C3D grid-index scalar frame", label);
                    TryFitCurrentC3D(
                        useTopInspectionView: true,
                        "Workbench result fitted to top inspection view");
                    viewModel.ViewerStatus = $"Workbench display: {label}";
                    RenderNow();
                });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (operation.IsCurrent)
            {
                viewModel.ViewerStatus = $"Workbench result display failed: {exception.Message}";
                RenderNow();
            }

            return false;
        }
    }

    private C3DHeightGrid? LoadDefaultC3DSample()
    {
        var path = ViewerSamplePathLocator.Find(DefaultC3DSamplePath);
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
        var path = ViewerSamplePathLocator.Find(DefaultGlbSamplePath);
        return path is null ? null : LoadGlbSample(path);
    }

    private LazPointCloudMetadata? LoadDefaultLazSample()
    {
        var path = ViewerSamplePathLocator.Find(DefaultLazSamplePath);
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
        importedMeshTextureReleasePending |= importedMeshTextureId != 0;
        importedMeshTextureSource = null;
        importedMeshTextureUploadFailed = false;
        importedMeshTextureUploadSummary = "texture none";
    }

    private void ReleaseImportedMeshTexture(OpenGL gl)
    {
        if (importedMeshTextureId != 0)
        {
            try
            {
                gl.DeleteTextures(1, [importedMeshTextureId]);
                importedMeshTextureReleaseCount++;
            }
            catch
            {
                importedMeshTextureReleaseFailureCount++;
                throw;
            }
        }

        importedMeshTextureId = 0;
        importedMeshTextureSource = null;
        importedMeshTextureReleasePending = false;
    }

    private LazPointCloudMetadata? LoadLazSample(string path)
    {
        lazPointCloud = null;
        lazSceneTransform = default;
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
            SetLazSceneTransform(metadata);
            viewModel.SetLazSampleSource(path, Path.GetFileNameWithoutExtension(path));
            return metadata;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            viewModel.LazSamplePointCount = "(unsupported)";
            viewModel.LazSampleSummary = $"Unsupported or corrupt LAZ/LAS: {ex.Message}";
            lazSceneTransform = default;
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
            lazSceneTransform = default;
            return null;
        }

        try
        {
            var loadResult = lazPointCloudLoadCoordinator.Load(candidate, maxSampledPoints);
            if (loadResult.PointCloud is not { } pointCloud)
            {
                return null;
            }

            if (loadResult.Reused)
            {
                lazPointCloudCacheHitCount++;
            }
            else
            {
                lazPointCloudDecodeCount++;
            }

            SetLoadedLazPointCloudTelemetry(pointCloud, loadResult.LoadMilliseconds, loadResult.Reused);
            return pointCloud;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            viewModel.LazSamplePointCount = "(unsupported)";
            viewModel.LazSampleSummary = $"Unsupported or corrupt LAZ/LAS point decode: {ex.Message}";
            viewModel.ClearLazSamplingTelemetry("LAZ/LAS sampling: load failed");
            lazSceneTransform = default;
            return null;
        }
    }

    private async Task<LazPointCloud?> LoadLazPointCloudAsync(
        string path,
        int maxSampledPoints,
        CancellationToken externalCancellationToken = default,
        IProgress<double>? externalProgress = null,
        Func<bool>? isCurrent = null)
    {
        var candidate = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        var sourceName = Path.GetFileName(candidate);
        var sampleLimit = Math.Max(2, maxSampledPoints);
        if (!File.Exists(candidate))
        {
            if (isCurrent?.Invoke() ?? true)
            {
                viewModel.FailLazPointCloudLoad(sourceName, "file not found");
            }

            return null;
        }

        lazPointCloudLoadRequestCount++;
        if (isCurrent?.Invoke() ?? true)
        {
            viewModel.BeginLazPointCloudLoad(sourceName);
        }

        try
        {
            externalProgress?.Report(0.0);
            var result = await lazPointCloudLoadCoordinator.LoadAsync(
                candidate,
                sampleLimit,
                externalCancellationToken,
                new Progress<double>(value =>
                {
                    if (!(isCurrent?.Invoke() ?? true))
                    {
                        return;
                    }

                    lazPointCloudProgressUpdateCount++;
                    lazPointCloudLastProgress = Math.Clamp(value, 0.0, 100.0);
                    viewModel.ReportLazPointCloudLoadProgress(sourceName, lazPointCloudLastProgress);
                    externalProgress?.Report(lazPointCloudLastProgress);
                    CaptureLazProgressSmokeScreenshotIfRequested();
                }),
                isCurrent);
            if (result is not { } loadResult)
            {
                return null;
            }

            if (loadResult.WasCanceled)
            {
                lazPointCloudCancellationCount++;
                if (isCurrent?.Invoke() ?? true)
                {
                    viewModel.CancelLazPointCloudLoad(sourceName);
                }

                return null;
            }

            if (loadResult.PointCloud is not { } pointCloud)
            {
                return null;
            }

            if (loadResult.Reused)
            {
                lazPointCloudCacheHitCount++;
                externalProgress?.Report(100.0);
            }
            else
            {
                lazPointCloudDecodeCount++;
            }

            SetLoadedLazPointCloudTelemetry(pointCloud, loadResult.LoadMilliseconds, loadResult.Reused);
            return pointCloud;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (isCurrent?.Invoke() ?? true)
            {
                viewModel.FailLazPointCloudLoad(sourceName, ex.Message);
            }

            return null;
        }
    }

    private async Task ReloadCurrentLazPointCloudAsync()
    {
        if (lazPointCloud is null)
        {
            return;
        }

        using var operation = sourceLoadOperations.Begin();
        var sourcePath = lazPointCloud.SourcePath;
        var reloaded = await LoadLazPointCloudAsync(
            sourcePath,
            viewModel.LazMaxSampledPoints,
            operation.Token,
            isCurrent: () => operation.IsCurrent && !operation.IsCancellationRequested);
        operation.Token.ThrowIfCancellationRequested();
        if (reloaded is null)
        {
            return;
        }

        sourceLoadOperations.TryApply(
            operation,
            () =>
            {
                lazSourceState.SetPointCloud(reloaded);
                selectedLazPoint = null;
                lazTwoPointFirst = null;
                lazTwoPointSecond = null;
                viewModel.ClearTwoPointMeasurement();
                SetLazSampleStatus();
                viewModel.SelectionSummary = "Point selection: reset after point-cloud density change";
                viewModel.MeasurementSummary = "Distance and height delta: reset after point-cloud density change";
                viewModel.PickCoordinate = "(none)";
                viewModel.ViewerStatus = $"Point cloud re-sampled: {viewModel.SelectedRenderDensity}";
                RenderNow();
            });
    }

    private void SetLoadedLazPointCloudTelemetry(
        LazPointCloud pointCloud,
        double loadMilliseconds,
        bool reused)
    {
        SetLazSceneTransform(pointCloud.Metadata);
        viewModel.SetLazSampleSource(pointCloud.SourcePath, Path.GetFileNameWithoutExtension(pointCloud.SourcePath));
        viewModel.SetLazSamplingTelemetry(
            pointCloud.DecodedPointCount,
            pointCloud.SampledPoints.Length,
            pointCloud.SampleStride,
            loadMilliseconds);
        viewModel.CompleteLazPointCloudLoad(Path.GetFileName(pointCloud.SourcePath), loadMilliseconds, reused);
    }

    private void CaptureLazProgressSmokeScreenshotIfRequested()
    {
        if (smokeLazProgressScreenshotCaptured
            || smokeLazProgressScreenshotPath is null
            || lazPointCloudLastProgress is <= 0.0 or >= 100.0
            || !IsLoaded)
        {
            return;
        }

        smokeLazProgressScreenshotCaptured = true;
        UpdateLayout();
        CaptureWindow(smokeLazProgressScreenshotPath);
    }

    private void SetLazSceneTransform(LazPointCloudMetadata metadata)
    {
        lazSceneTransform = LazSceneTransform.FromMetadata(metadata);
        var corners = lazSceneTransform.CreateBoundsCorners(metadata);
        var min = corners[0];
        var max = corners[0];
        foreach (var corner in corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        viewModel.SetLazSampleBounds(min, max);
    }

    private void SetC3DSampleStatus()
    {
        ResetC3DGridHoverForSourceChange();
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
        viewModel.SetC3DDisplayCapabilities(
            renderProxy.HasSurface,
            SourceChannelCatalogAnalyzer.CreateForC3DHeightGrid());
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
        var normalQuality = importedMesh is { } normalMesh
            ? ImportedMeshNormalQualityAnalyzer.Create(normalMesh)
            : null;
        viewModel.SetImportedMeshDisplayCapabilities(
            importedMesh is { } mesh && (mesh.HasVertexColors || mesh.HasBaseColorTexture),
            importedMesh is { } importedMeshValue
                ? SourceChannelCatalogAnalyzer.CreateForImportedMesh(importedMeshValue)
                : null,
            normalQuality);

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
        var normalSummary = normalQuality is { } report
            ? $"normals {report.State} {report.NormalCount:N0}/{report.PositionCount:N0} | normal evidence {report.Evidence}"
            : "normals unavailable";
        viewModel.GlbSampleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileName(importedMesh.SourcePath)} | format {importedMesh.Format} | vertices {importedMesh.Positions.Length:N0} | triangles {importedMesh.TriangleCount:N0} | {colorSummary} | {textureSummary} | {normalSummary} | bounds {FormatVector(importedMesh.Min)} to {FormatVector(importedMesh.Max)}");
        viewModel.SetGlbSampleSource(importedMesh.SourcePath, Path.GetFileNameWithoutExtension(importedMesh.SourcePath), importedMesh.Format);
        viewModel.SetGlbSampleBounds(importedMesh.Min, importedMesh.Max);
    }

    private void SetLazSampleStatus()
    {
        viewModel.SetLazDisplayCapabilities(
            lazPointCloud?.HasRgb == true,
            lazPointCloud?.HasIntensity == true,
            lazPointCloud is { } pointCloud
                ? SourceChannelCatalogAnalyzer.CreateForLazPointCloud(pointCloud)
                : null);

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
            importedMeshTextureUploadCount++;
            importedMeshTextureUploadSummary = string.Create(
                CultureInfo.InvariantCulture,
                $"uploaded {texture.Width}x{texture.Height} {importedMesh.BaseColorTexture.MimeType}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or NotSupportedException)
        {
            ReleaseImportedMeshTexture(gl);
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
        var pixels = C3DHeightMapRasterizer.CreatePixels(c3dSample, pixelWidth, pixelHeight);

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
            "Thickness Coupon v1",
            rowIndex,
            samples.Length,
            min,
            max,
            mean,
            C3DSectionProfilePathBuilder.Build(samples, min, max));
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

    private void ReloadDefaultC3DSample()
    {
        var sourcePath = c3dSample?.SourcePath ?? ViewerSamplePathLocator.Find(DefaultC3DSamplePath);
        var pointPairStep = viewModel.CreatePointPairDimensionsRecipeStep();
        var restoreThicknessPreview = viewModel.ThicknessVisible;
        var restoreWarpagePreview = viewModel.WarpageVisible;
        var restorePointPairPreview = viewModel.PointPairDimensionsVisible;
        var restoreFlatnessPreview = viewModel.PlaneFlatnessVisible;
        var restoreVolumePreview = viewModel.VolumeVisible;
        var restoreCrossSectionPreview = viewModel.CrossSectionVisible;
        try
        {
            c3dSample = c3dSample is not null
                ? c3dSample.WithMaxRenderedPoints(viewModel.C3DMaxRenderedPoints)
                : string.IsNullOrWhiteSpace(sourcePath)
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
        else if (c3dSample is not null)
        {
            HeightDeviationRuleCoordinator.ApplyToViewModel(
                viewModel,
                c3dSample,
                viewModel.RecipeSourceName,
                viewModel.RecipePeakTolerance,
                viewModel.RecipeSourceUnit);
        }
    }

    private void DecreaseC3DHeightColorMaximum_Click(object sender, RoutedEventArgs e) =>
        viewModel.ShiftC3DHeightColorMaximum(-1);

    private void IncreaseC3DHeightColorMaximum_Click(object sender, RoutedEventArgs e) =>
        viewModel.ShiftC3DHeightColorMaximum(1);

    private void DecreaseC3DHeightColorMinimum_Click(object sender, RoutedEventArgs e) =>
        viewModel.ShiftC3DHeightColorMinimum(-1);

    private void IncreaseC3DHeightColorMinimum_Click(object sender, RoutedEventArgs e) =>
        viewModel.ShiftC3DHeightColorMinimum(1);

    private void ResetC3DHeightColorRange_Click(object sender, RoutedEventArgs e) =>
        viewModel.ResetC3DHeightColorRange();

}

public sealed record C3DSourceLoadPerformance(
    C3DHeightGridLoadPerformance Grid,
    double TopologyMilliseconds,
    double PositionsMilliseconds,
    double WorkerMilliseconds,
    double ApplyMilliseconds)
{
    public C3DSourceApplyPerformance? ApplyDetail { get; init; }
}

public sealed record C3DSourceApplyPerformance(
    double SourceStateMilliseconds,
    double ClearStateMilliseconds,
    double SampleStatusMilliseconds,
    double SceneMilliseconds,
    double DisplayMilliseconds,
    double AlignmentMilliseconds,
    double StatusMilliseconds,
    double FinalRenderMilliseconds,
    double TotalMilliseconds,
    int RenderRequestCount,
    int SuppressedRenderRequestCount,
    int RenderExecutionCount,
    double RenderExecutionMilliseconds,
    int DisplayListBuildCount,
    string LastDisplayListBuildReason);
