using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public const string CubeEntityId = "source.generated-cube";
    public const string PointCloudEntityId = "source.generated-point-cloud";
    public const string C3DEntityId = "source.c3d-thickness";
    public const string SyntheticResultEntityId = "result.synthetic-height-deviation";

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
    private string pointCloudPointCount = "(pending)";
    private string c3DSamplePointCount = "(not loaded)";
    private string c3DSampleSummary = "C3D sample hidden";
    private string selectedSelectionMode = "Point";
    private string selectionSummary = "Point selection: generated point cloud peak";
    private bool selectionOverlayVisible = true;
    private bool resultOverlayVisible;
    private string resultSummary = "Result overlay hidden";
    private ToolResult previewToolResult = CreateNotRunToolResult();
    private IReadOnlyList<ResultEntity> resultEntities = [];
    private string publishedResultSummary = "Published result: none";
    private IReadOnlyList<EntityLayer> entityLayers = [];
    private string sceneContractSummary = "(pending)";
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
                RefreshSceneContracts();
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
                    "Section Plane" => "Section plane: viewer state only",
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
                PreviewToolResult = value ? CreateSyntheticHeightDeviationPreview() : CreateNotRunToolResult();
                ResultSummary = FormatToolResult(PreviewToolResult);
                ViewerStatus = value ? "Result overlay visible" : "Result overlay hidden";
                RefreshSceneContracts();
            }
        }
    }

    public string ResultSummary
    {
        get => resultSummary;
        set => SetField(ref resultSummary, value);
    }

    public ToolResult PreviewToolResult
    {
        get => previewToolResult;
        private set => SetField(ref previewToolResult, value);
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
        UsePointCloudSmokeScene();
        SelectionOverlayVisible = true;
        SelectedSelectionMode = mode;
        SelectedEntity = mode switch
        {
            "Box ROI" => "Box ROI",
            "Section Plane" => "Section Plane",
            _ => "Generated Point Cloud"
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
                "layer.preview.synthetic-height-deviation",
                "Preview: Synthetic Height Deviation",
                LayerKind.Preview,
                ResultOverlayVisible,
                [PointCloudEntityId]));
        }

        if (ResultEntities.Count > 0)
        {
            layers.Add(new EntityLayer(
                "layer.result.synthetic-height-deviation",
                "Published Synthetic Height Deviation",
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

    private static ResultEntity CreatePublishedResultEntity(ToolResult result) =>
        new(
            SyntheticResultEntityId,
            "Published Synthetic Height Deviation",
            PointCloudEntityId,
            result.Status,
            "Published from synthetic preview; source geometry remains unchanged.",
            result.Metrics,
            result.Overlays);

    private static string FormatToolResult(ToolResult result)
    {
        if (result.Status == ResultStatus.NotRun)
        {
            return "Result overlay hidden";
        }

        var metric = result.Metrics.First();
        return $"Preview: {result.ToolName}: {result.Status}\n{result.Message}\nMetric: {metric.Name} = {metric.Value:F3} {metric.Unit}\nOverlays: {result.Overlays.Count}";
    }

    private static string FormatPublishedResult(ResultEntity result)
    {
        var metric = result.Metrics.First();
        return $"{result.Name}: {result.Status}\nSource: {result.SourceEntityId}\nMetric: {metric.Name} = {metric.Value:F3} {metric.Unit}\nLayer: published result";
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
