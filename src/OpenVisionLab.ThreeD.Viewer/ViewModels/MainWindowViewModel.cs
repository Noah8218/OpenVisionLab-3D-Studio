using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public const string CubeEntityId = "source.generated-cube";
    public const string PointCloudEntityId = "source.generated-point-cloud";
    public const string C3DEntityId = "source.c3d-thickness";
    public const string SyntheticResultEntityId = "result.synthetic-height-deviation";
    public const string C3DHeightDeviationResultEntityId = "result.c3d-height-deviation";

    private bool cubeVisible = true;
    private bool pointCloudVisible = true;
    private bool c3DSampleVisible;
    private bool measurementVisible = true;
    private string selectedEntity = "Generated Unit Cube";
    private string pickCoordinate = "(none)";
    private string lastScreenshotPath = "(none)";
    private string viewerStatus = "Ready: generated cube and point cloud loaded";
    private string bottomStatus = "Model units: unitless | Camera: orbit | Source/result separation: source only";
    private string measurementSummary = "Cube width: 2.000 model units\nExpected center: (0.000, 0.000, 0.000)";
    private string selectedColorMode = "Height";
    private double pointSize = 2.0;
    private string selectedRenderDensity = "Balanced";
    private string renderDensitySummary = FormatRenderDensitySummary("Balanced");
    private string pointCloudPointCount = "(pending)";
    private string c3DSamplePointCount = "(not loaded)";
    private string c3DSampleSummary = "C3D sample hidden";
    private string selectedSelectionMode = "Point";
    private string selectionSummary = "Point selection: generated point cloud peak";
    private bool selectionOverlayVisible = true;
    private bool resultOverlayVisible;
    private string resultSummary = "Result overlay hidden";
    private string recipeSummary = "Recipe: current C3D height deviation\nSource: C3D Thickness Sample\nTolerance: 1200.000 raw-height";
    private string recipeFileName = "(current)";
    private string recipeSourceName = "C3D Thickness Sample";
    private string recipeSourcePath = @"3D\Thickness\Ori_20240116_094414.C3D";
    private string recipeSourceUnit = "raw-height";
    private double recipePeakTolerance = 1200.0;
    private string recipeSaveSummary = "Recipe save: not saved";
    private ToolResult previewToolResult = CreateNotRunToolResult();
    private ToolResult? c3dHeightDeviationPreview;
    private IReadOnlyList<ResultEntity> resultEntities = [];
    private string publishedResultSummary = "Published result: none";
    private IReadOnlyList<EntityLayer> entityLayers = [];
    private string sceneContractSummary = "(pending)";
    private bool deviationLegendVisible;
    private string deviationLegendStatus = "Status: inactive";
    private string deviationLegendPeak = "Peak: none";
    private string deviationLegendTolerance = "Tolerance: none";
    private string deviationLegendScale = "Scale: mean to peak deviation";
    private bool sectionProfileVisible;
    private string sectionProfileSummary = "Profile: not loaded";
    private string sectionProfileRange = "Range: not loaded";
    private string sectionProfilePathData = "M 0,30 L 240,30";
    private int sectionProfileSampleCount;
    private string activePreviewLayerId = "layer.preview.synthetic-height-deviation";
    private string activePreviewLayerName = "Preview: Synthetic Height Deviation";
    private string activePreviewSourceEntityId = PointCloudEntityId;
    private string activeResultEntityId = SyntheticResultEntityId;
    private string activeResultEntityName = "Published Synthetic Height Deviation";
    private double cameraTargetX = 2.05;
    private double cameraTargetY = -0.25;
    private double cameraTargetZ;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        SourceEntities =
        [
            new SourceEntity(CubeEntityId, "Generated Unit Cube", EntityKind.Mesh, "unitless", null, ModelTransform.Identity),
            new SourceEntity(PointCloudEntityId, "Generated Point Cloud", EntityKind.PointCloud, "unitless", null, ModelTransform.Identity),
            new SourceEntity(C3DEntityId, "C3D Thickness Sample", EntityKind.HeightGrid, "raw-height", @"3D\Thickness\Ori_20240116_094414.C3D", ModelTransform.Identity)
        ];

        RefreshSceneContracts();
    }

    public string[] ColorModes { get; } = ["Solid", "Height", "Deviation"];

    public string[] RenderDensityModes { get; } = ["Fast", "Balanced", "Detailed"];

    public string[] SelectionModes { get; } = ["Point", "Box ROI", "Section Plane"];

    public IReadOnlyList<SourceEntity> SourceEntities { get; }

    public IReadOnlyList<EntityLayer> EntityLayers
    {
        get => entityLayers;
        private set => SetField(ref entityLayers, value);
    }

    public string SceneContractSummary
    {
        get => sceneContractSummary;
        private set => SetField(ref sceneContractSummary, value);
    }

    public bool CubeVisible
    {
        get => cubeVisible;
        set
        {
            if (SetField(ref cubeVisible, value))
            {
                ViewerStatus = value ? "Cube layer visible" : "Cube layer hidden";
                RefreshSceneContracts();
            }
        }
    }

    public bool PointCloudVisible
    {
        get => pointCloudVisible;
        set
        {
            if (SetField(ref pointCloudVisible, value))
            {
                ViewerStatus = value ? "Point cloud layer visible" : "Point cloud layer hidden";
                RefreshSceneContracts();
            }
        }
    }

    public bool C3DSampleVisible
    {
        get => c3DSampleVisible;
        set
        {
            if (SetField(ref c3DSampleVisible, value))
            {
                ViewerStatus = value ? "C3D sample visible" : "C3D sample hidden";
                if (ResultOverlayVisible)
                {
                    ApplyActivePreviewResult();
                }
                else
                {
                    RefreshSceneContracts();
                }
            }
        }
    }

    public bool MeasurementVisible
    {
        get => measurementVisible;
        set
        {
            if (SetField(ref measurementVisible, value))
            {
                MeasurementSummary = value
                    ? "Cube width: 2.000 model units\nExpected center: (0.000, 0.000, 0.000)"
                    : "Measurement overlay hidden";
                ViewerStatus = value ? "Measurement overlay visible" : "Measurement overlay hidden";
            }
        }
    }

    public string MeasurementSummary
    {
        get => measurementSummary;
        set => SetField(ref measurementSummary, value);
    }

    public string SelectedColorMode
    {
        get => selectedColorMode;
        set
        {
            if (SetField(ref selectedColorMode, value))
            {
                ViewerStatus = $"Point cloud color mode: {value}";
            }
        }
    }

    public double PointSize
    {
        get => pointSize;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 6.0);
            if (SetField(ref pointSize, clamped))
            {
                ViewerStatus = string.Create(CultureInfo.InvariantCulture, $"Point size: {clamped:F1}px");
            }
        }
    }

    public string SelectedRenderDensity
    {
        get => selectedRenderDensity;
        set
        {
            var mode = RenderDensityModes.Contains(value) ? value : "Balanced";
            if (SetField(ref selectedRenderDensity, mode))
            {
                RenderDensitySummary = FormatRenderDensitySummary(mode);
                ViewerStatus = $"Render density: {mode}";
            }
        }
    }

    public string RenderDensitySummary
    {
        get => renderDensitySummary;
        private set => SetField(ref renderDensitySummary, value);
    }

    public int C3DMaxRenderedPoints => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 140000,
        _ => 55000
    };

    public string PointCloudPointCount
    {
        get => pointCloudPointCount;
        set => SetField(ref pointCloudPointCount, value);
    }

    public string C3DSamplePointCount
    {
        get => c3DSamplePointCount;
        set => SetField(ref c3DSamplePointCount, value);
    }

    public string C3DSampleSummary
    {
        get => c3DSampleSummary;
        set => SetField(ref c3DSampleSummary, value);
    }

    public string SelectedSelectionMode
    {
        get => selectedSelectionMode;
        set
        {
            if (SetField(ref selectedSelectionMode, value))
            {
                SelectionSummary = value switch
                {
                    "Box ROI" => "Box ROI: viewer state only",
                    "Section Plane" => SectionProfileVisible ? SectionProfileSummary : "Section plane: profile not loaded",
                    _ => "Point selection: generated point cloud peak"
                };
                ViewerStatus = $"Selection mode: {value}";
            }
        }
    }

    public string SelectionSummary
    {
        get => selectionSummary;
        set => SetField(ref selectionSummary, value);
    }

    public bool SelectionOverlayVisible
    {
        get => selectionOverlayVisible;
        set
        {
            if (SetField(ref selectionOverlayVisible, value))
            {
                ViewerStatus = value ? "Selection overlay visible" : "Selection overlay hidden";
            }
        }
    }

    public bool ResultOverlayVisible
    {
        get => resultOverlayVisible;
        set
        {
            if (SetField(ref resultOverlayVisible, value))
            {
                if (value)
                {
                    ApplyActivePreviewResult();
                }
                else
                {
                    ResetActivePreviewIdentity();
                    PreviewToolResult = CreateNotRunToolResult();
                    ResultSummary = FormatToolResult(PreviewToolResult);
                    RefreshSceneContracts();
                }

                ViewerStatus = value ? "Result overlay visible" : "Result overlay hidden";
            }
        }
    }

    public string ResultSummary
    {
        get => resultSummary;
        set => SetField(ref resultSummary, value);
    }

    public string RecipeSummary
    {
        get => recipeSummary;
        set => SetField(ref recipeSummary, value);
    }

    public string RecipeSourceName
    {
        get => recipeSourceName;
        set
        {
            if (SetField(ref recipeSourceName, string.IsNullOrWhiteSpace(value) ? "C3D Thickness Sample" : value))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public string RecipeSourcePath
    {
        get => recipeSourcePath;
        set
        {
            if (SetField(ref recipeSourcePath, string.IsNullOrWhiteSpace(value) ? @"3D\Thickness\Ori_20240116_094414.C3D" : value))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public string RecipeSourceUnit
    {
        get => recipeSourceUnit;
        set
        {
            if (SetField(ref recipeSourceUnit, string.IsNullOrWhiteSpace(value) ? "raw-height" : value))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public double RecipePeakTolerance
    {
        get => recipePeakTolerance;
        set
        {
            var clamped = double.IsFinite(value) ? Math.Max(0.001, value) : 1200.0;
            if (SetField(ref recipePeakTolerance, clamped))
            {
                RefreshRecipeSummary();
            }
        }
    }

    public string RecipeSaveSummary
    {
        get => recipeSaveSummary;
        private set => SetField(ref recipeSaveSummary, value);
    }

    public ToolResult PreviewToolResult
    {
        get => previewToolResult;
        private set
        {
            if (SetField(ref previewToolResult, value))
            {
                RefreshDeviationLegend(value);
            }
        }
    }

    public IReadOnlyList<ResultEntity> ResultEntities
    {
        get => resultEntities;
        private set => SetField(ref resultEntities, value);
    }

    public string PublishedResultSummary
    {
        get => publishedResultSummary;
        private set => SetField(ref publishedResultSummary, value);
    }

    public string SelectedEntity
    {
        get => selectedEntity;
        set => SetField(ref selectedEntity, value);
    }

    public string PickCoordinate
    {
        get => pickCoordinate;
        set => SetField(ref pickCoordinate, value);
    }

    public string LastScreenshotPath
    {
        get => lastScreenshotPath;
        set => SetField(ref lastScreenshotPath, value);
    }

    public string ViewerStatus
    {
        get => viewerStatus;
        set => SetField(ref viewerStatus, value);
    }

    public string BottomStatus
    {
        get => bottomStatus;
        set => SetField(ref bottomStatus, value);
    }

    public double YawDegrees { get; set; } = 38.0;

    public double PitchDegrees { get; set; } = 24.0;

    public double CameraDistance { get; set; } = 9.2;

    public double CameraTargetX
    {
        get => cameraTargetX;
        set => SetField(ref cameraTargetX, value);
    }

    public double CameraTargetY
    {
        get => cameraTargetY;
        set => SetField(ref cameraTargetY, value);
    }

    public double CameraTargetZ
    {
        get => cameraTargetZ;
        set => SetField(ref cameraTargetZ, value);
    }

    public bool DeviationLegendVisible
    {
        get => deviationLegendVisible;
        private set => SetField(ref deviationLegendVisible, value);
    }

    public string DeviationLegendStatus
    {
        get => deviationLegendStatus;
        private set => SetField(ref deviationLegendStatus, value);
    }

    public string DeviationLegendPeak
    {
        get => deviationLegendPeak;
        private set => SetField(ref deviationLegendPeak, value);
    }

    public string DeviationLegendTolerance
    {
        get => deviationLegendTolerance;
        private set => SetField(ref deviationLegendTolerance, value);
    }

    public string DeviationLegendScale
    {
        get => deviationLegendScale;
        private set => SetField(ref deviationLegendScale, value);
    }

    public bool SectionProfileVisible
    {
        get => sectionProfileVisible;
        private set => SetField(ref sectionProfileVisible, value);
    }

    public string SectionProfileSummary
    {
        get => sectionProfileSummary;
        private set => SetField(ref sectionProfileSummary, value);
    }

    public string SectionProfileRange
    {
        get => sectionProfileRange;
        private set => SetField(ref sectionProfileRange, value);
    }

    public string SectionProfilePathData
    {
        get => sectionProfilePathData;
        private set => SetField(ref sectionProfilePathData, value);
    }

    public int SectionProfileSampleCount
    {
        get => sectionProfileSampleCount;
        private set => SetField(ref sectionProfileSampleCount, value);
    }

    public void FitAll()
    {
        if (C3DSampleVisible)
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 13.2;
        }
        else if (PointCloudVisible && CubeVisible)
        {
            SetCameraTarget(2.05, -0.25, 0.0);
            CameraDistance = 9.2;
        }
        else if (PointCloudVisible)
        {
            SetCameraTarget(3.2, -0.70, 0.0);
            CameraDistance = 7.2;
        }
        else
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 5.2;
        }

        ViewerStatus = "Fit all visible entities";
        UpdateCameraStatus();
    }

    public void FitSelection()
    {
        if (SelectedEntity == "C3D Height Grid" && C3DSampleVisible)
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 13.2;
            ViewerStatus = "Fit selected C3D height grid";
        }
        else if (SelectedEntity == "Generated Point Cloud" && PointCloudVisible)
        {
            SetCameraTarget(3.2, -0.70, 0.0);
            CameraDistance = 7.2;
            ViewerStatus = "Fit selected point cloud";
        }
        else
        {
            SetCameraTarget(0.0, 0.0, 0.0);
            CameraDistance = 5.2;
            ViewerStatus = "Fit selected cube";
        }

        UpdateCameraStatus();
    }

    public void Reset()
    {
        YawDegrees = 38.0;
        PitchDegrees = 24.0;
        CameraDistance = 9.2;
        SetCameraTarget(2.05, -0.25, 0.0);
        SelectedEntity = "Generated Unit Cube";
        PickCoordinate = "(none)";
        ViewerStatus = "Camera reset";
        UpdateCameraStatus();
    }

    public void UsePointCloudSmokeScene()
    {
        CubeVisible = false;
        MeasurementVisible = false;
        ResultOverlayVisible = false;
        C3DSampleVisible = false;
        PointCloudVisible = true;
        SelectedColorMode = "Height";
        SelectedEntity = "Generated Point Cloud";
        PickCoordinate = "(none)";
        CameraDistance = 7.2;
        SetCameraTarget(3.2, -0.70, 0.0);
        ViewerStatus = "Smoke scene: generated point cloud";
        UpdateCameraStatus();
    }

    public void UseSelectionSmokeScene(string mode)
    {
        var keepCurrentC3DScene = mode == "Section Plane" && C3DSampleVisible;
        if (!keepCurrentC3DScene)
        {
            UsePointCloudSmokeScene();
        }

        SelectionOverlayVisible = true;
        SelectedSelectionMode = mode;
        SelectedEntity = mode switch
        {
            "Box ROI" => "Box ROI",
            "Section Plane" => "Section Plane",
            _ => "Generated Point Cloud"
        };
        SelectionSummary = mode switch
        {
            "Section Plane" when SectionProfileVisible => SectionProfileSummary,
            "Section Plane" => "Section plane: profile not loaded",
            "Box ROI" => "Box ROI: viewer state only",
            _ => "Point selection: generated point cloud peak"
        };
        ViewerStatus = $"Smoke scene: {mode}";
    }

    public void UseC3DSmokeScene()
    {
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "C3D Height Grid";
        PickCoordinate = "(none)";
        MeasurementSummary = "C3D sample loaded; no measurement tool published";
        SelectionSummary = "Selection overlay hidden";
        SelectedColorMode = "Height";
        YawDegrees = 34.0;
        PitchDegrees = 52.0;
        CameraDistance = 13.2;
        SetCameraTarget(0.0, 0.0, 0.0);
        ViewerStatus = "Smoke scene: C3D height grid";
        UpdateCameraStatus();
    }

    public void UseResultSmokeScene()
    {
        UsePointCloudSmokeScene();
        MeasurementVisible = true;
        SelectionOverlayVisible = true;
        ResultOverlayVisible = true;
        SelectedSelectionMode = "Box ROI";
        SelectedEntity = "Result Overlay";
        PickCoordinate = "(viewer-only sample)";
        ViewerStatus = "Smoke scene: result overlay";
    }

    public void UseC3DHeightDeviationRuleSmokeScene()
    {
        UseC3DSmokeScene();
        MeasurementVisible = false;
        MeasurementSummary = "C3D rule preview uses raw height statistics.";
        ResultOverlayVisible = true;
        SelectedColorMode = "Deviation";
        SelectedEntity = "C3D Height Deviation Rule";
        PickCoordinate = "(sample-backed rule)";
        ViewerStatus = "Smoke scene: C3D height deviation rule";
    }

    public void SetC3DHeightDeviationPreview(ToolResult result)
    {
        c3dHeightDeviationPreview = result;
        if (ResultOverlayVisible && C3DSampleVisible)
        {
            ApplyActivePreviewResult();
        }
    }

    public bool PublishPreviewResult()
    {
        if (PreviewToolResult.Status == ResultStatus.NotRun)
        {
            ViewerStatus = "No preview result to publish";
            return false;
        }

        var resultEntity = CreatePublishedResultEntity(PreviewToolResult);
        ResultEntities = [resultEntity];
        PublishedResultSummary = FormatPublishedResult(resultEntity);
        ViewerStatus = "Published synthetic result layer";
        RefreshSceneContracts();
        return true;
    }

    public void Pan(double deltaX, double deltaY, double deltaZ)
    {
        CameraTargetX += deltaX;
        CameraTargetY += deltaY;
        CameraTargetZ += deltaZ;
        ViewerStatus = "Camera panned";
        UpdateCameraStatus();
    }

    public void UpdateCameraStatus()
    {
        BottomStatus = $"Model units: unitless | Camera: yaw {YawDegrees:F1}, pitch {PitchDegrees:F1}, distance {CameraDistance:F2}, target ({CameraTargetX:F2}, {CameraTargetY:F2}, {CameraTargetZ:F2})";
    }

    private void SetCameraTarget(double x, double y, double z)
    {
        CameraTargetX = x;
        CameraTargetY = y;
        CameraTargetZ = z;
    }

    private void RefreshSceneContracts()
    {
        var layers = new List<EntityLayer>
        {
            new EntityLayer("layer.source.generated-cube", "Generated Unit Cube", LayerKind.Source, CubeVisible, [CubeEntityId]),
            new EntityLayer("layer.source.generated-point-cloud", "Generated Point Cloud", LayerKind.Source, PointCloudVisible, [PointCloudEntityId]),
            new EntityLayer("layer.source.c3d-thickness", "C3D Thickness Sample", LayerKind.Source, C3DSampleVisible, [C3DEntityId])
        };

        if (PreviewToolResult.Status != ResultStatus.NotRun)
        {
            layers.Add(new EntityLayer(
                activePreviewLayerId,
                activePreviewLayerName,
                LayerKind.Preview,
                ResultOverlayVisible,
                [activePreviewSourceEntityId]));
        }

        if (ResultEntities.Count > 0)
        {
            var isC3DResult = ResultEntities.Any(entity => entity.Id == C3DHeightDeviationResultEntityId);
            layers.Add(new EntityLayer(
                isC3DResult ? "layer.result.c3d-height-deviation" : "layer.result.synthetic-height-deviation",
                isC3DResult ? "Published C3D Height Deviation" : "Published Synthetic Height Deviation",
                LayerKind.Result,
                true,
                ResultEntities.Select(entity => entity.Id).ToArray()));
        }

        EntityLayers = layers;

        var sourceLayerCount = EntityLayers.Count(layer => layer.Kind == LayerKind.Source);
        var visibleSourceLayerCount = EntityLayers.Count(layer => layer.Kind == LayerKind.Source && layer.IsVisible);
        var previewLayerCount = EntityLayers.Count(layer => layer.Kind == LayerKind.Preview);
        SceneContractSummary =
            $"Source entities: {SourceEntities.Count} | Source layers: {sourceLayerCount} | Visible source layers: {visibleSourceLayerCount} | Preview layers: {previewLayerCount} | Published results: {ResultEntities.Count}";
    }

    private static ToolResult CreateNotRunToolResult() =>
        new(
            "Synthetic Height Deviation Preview",
            ResultStatus.NotRun,
            "No preview result is active.",
            TimeSpan.Zero,
            [],
            []);

    private static ToolResult CreateSyntheticHeightDeviationPreview() =>
        new(
            "Synthetic Height Deviation Preview",
            ResultStatus.Warning,
            "Preview only; source geometry is unchanged and no result is published.",
            TimeSpan.Zero,
            [
                new Metric("Synthetic peak deviation", MetricKind.Deviation, 0.42, "unitless", ResultStatus.Warning),
                new Metric("Preview overlay count", MetricKind.Count, 3, "count", ResultStatus.Warning)
            ],
            [
                new Overlay("overlay.synthetic-pass-band", OverlayKind.Box, "PASS tolerance band", ResultStatus.Pass, PointCloudEntityId),
                new Overlay("overlay.synthetic-profile", OverlayKind.Polyline, "Preview profile line", ResultStatus.Warning, PointCloudEntityId),
                new Overlay("overlay.synthetic-fail-markers", OverlayKind.Marker, "FAIL marker cluster", ResultStatus.Fail, PointCloudEntityId)
            ]);

    private ResultEntity CreatePublishedResultEntity(ToolResult result) =>
        new(
            activeResultEntityId,
            activeResultEntityName,
            activePreviewSourceEntityId,
            result.Status,
            "Published from preview; source geometry remains unchanged.",
            result.Metrics,
            result.Overlays);

    private void ApplyActivePreviewResult()
    {
        if (C3DSampleVisible && c3dHeightDeviationPreview is not null)
        {
            activePreviewLayerId = "layer.preview.c3d-height-deviation";
            activePreviewLayerName = "Preview: C3D Height Deviation Rule";
            activePreviewSourceEntityId = C3DEntityId;
            activeResultEntityId = C3DHeightDeviationResultEntityId;
            activeResultEntityName = "Published C3D Height Deviation";
            PreviewToolResult = c3dHeightDeviationPreview;
        }
        else
        {
            ResetActivePreviewIdentity();
            PreviewToolResult = CreateSyntheticHeightDeviationPreview();
        }

        ResultSummary = FormatToolResult(PreviewToolResult);
        RefreshSceneContracts();
    }

    private void ResetActivePreviewIdentity()
    {
        activePreviewLayerId = "layer.preview.synthetic-height-deviation";
        activePreviewLayerName = "Preview: Synthetic Height Deviation";
        activePreviewSourceEntityId = PointCloudEntityId;
        activeResultEntityId = SyntheticResultEntityId;
        activeResultEntityName = "Published Synthetic Height Deviation";
    }

    private void RefreshDeviationLegend(ToolResult result)
    {
        if (activePreviewSourceEntityId != C3DEntityId || result.Status == ResultStatus.NotRun)
        {
            DeviationLegendVisible = false;
            DeviationLegendStatus = "Status: inactive";
            DeviationLegendPeak = "Peak: none";
            DeviationLegendTolerance = "Tolerance: none";
            DeviationLegendScale = "Scale: mean to peak deviation";
            return;
        }

        var peak = result.Metrics.FirstOrDefault(metric => metric.Name == "Peak absolute deviation");
        var tolerance = result.Metrics.FirstOrDefault(metric => metric.Name == "Peak tolerance");
        var unit = peak?.Unit ?? tolerance?.Unit ?? "raw-height";
        var statusText = result.Status switch
        {
            ResultStatus.Pass => "Status: Pass | within tolerance",
            ResultStatus.Fail => "Status: Fail | above tolerance",
            ResultStatus.Warning => "Status: Warning | review tolerance",
            ResultStatus.Error => "Status: Error | invalid result",
            _ => $"Status: {result.Status}"
        };

        DeviationLegendStatus = statusText;
        DeviationLegendPeak = peak is null
            ? "Peak: none"
            : string.Create(CultureInfo.InvariantCulture, $"Peak: {peak.Value:F3} {unit}");
        DeviationLegendTolerance = tolerance is null
            ? "Tolerance: none"
            : string.Create(CultureInfo.InvariantCulture, $"Tolerance: +/- {tolerance.Value:F3} {unit}");
        DeviationLegendScale = "Scale: 0 = mean, 1 = peak deviation";
        DeviationLegendVisible = true;
    }

    private static string FormatToolResult(ToolResult result)
    {
        if (result.Status == ResultStatus.NotRun)
        {
            return "Result overlay hidden";
        }

        var metric = result.Metrics.FirstOrDefault(metric => metric.Status is not null) ?? result.Metrics.First();
        return $"Preview: {result.ToolName}: {result.Status}\n{result.Message}\nMetric: {metric.Name} = {metric.Value:F3} {metric.Unit}\nOverlays: {result.Overlays.Count}";
    }

    private static string FormatPublishedResult(ResultEntity result)
    {
        var metric = result.Metrics.First();
        return $"{result.Name}: {result.Status}\nSource: {result.SourceEntityId}\nMetric: {metric.Name} = {metric.Value:F3} {metric.Unit}\nLayer: published result";
    }

    public void SetRecipeLoaded(string recipePath, string sourceName, string sourcePath, string unit, double peakTolerance)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSourceName = sourceName;
        RecipeSourcePath = sourcePath;
        RecipeSourceUnit = unit;
        RecipePeakTolerance = peakTolerance;
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        RefreshRecipeSummary();
    }

    public void SetRecipeSaved(string recipePath)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSaveSummary = $"Recipe saved: {Path.GetFullPath(recipePath)}";
        RefreshRecipeSummary();
    }

    public void SetSectionProfile(string sourceName, int rowIndex, int sampleCount, double min, double max, double mean, string pathData)
    {
        SectionProfileVisible = sampleCount > 1;
        SectionProfileSampleCount = sampleCount;
        SectionProfileSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Profile: {sourceName} center section | row {rowIndex} | samples {sampleCount}");
        SectionProfileRange = string.Create(
            CultureInfo.InvariantCulture,
            $"Range: min {min:F3}, max {max:F3}, mean {mean:F3} raw-height");
        SectionProfilePathData = string.IsNullOrWhiteSpace(pathData) ? "M 0,30 L 240,30" : pathData;

        if (SelectedSelectionMode == "Section Plane")
        {
            SelectionSummary = SectionProfileSummary;
        }
    }

    public void ClearSectionProfile()
    {
        SectionProfileVisible = false;
        SectionProfileSampleCount = 0;
        SectionProfileSummary = "Profile: not loaded";
        SectionProfileRange = "Range: not loaded";
        SectionProfilePathData = "M 0,30 L 240,30";

        if (SelectedSelectionMode == "Section Plane")
        {
            SelectionSummary = "Section plane: profile not loaded";
        }
    }

    private static string FormatRenderDensitySummary(string mode) => mode switch
    {
        "Fast" => "Fast: up to 25,000 C3D points",
        "Detailed" => "Detailed: up to 140,000 C3D points",
        _ => "Balanced: up to 55,000 C3D points"
    };

    private void RefreshRecipeSummary()
    {
        RecipeSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Recipe: {recipeFileName}\nSource: {RecipeSourceName}\nTolerance: {RecipePeakTolerance:F3} {RecipeSourceUnit}");
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
