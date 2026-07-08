using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public const string CubeEntityId = "source.generated-cube";
    public const string PointCloudEntityId = "source.generated-point-cloud";
    public const string C3DEntityId = "source.c3d-thickness";
    public const string GlbEntityId = "source.imported-mesh";
    public const string LazEntityId = "source.public-laz-manuscript";
    public const string SyntheticResultEntityId = "result.synthetic-height-deviation";
    public const string C3DHeightDeviationResultEntityId = "result.c3d-height-deviation";
    public const string LazTwoPointResultEntityId = "result.laz-two-point-measurement";
    private const double DefaultLazExpectedDistance = 116.919;
    private const double DefaultLazDistanceTolerance = 0.010;
    private const double DefaultLazExpectedHeightDelta = -0.624;
    private const double DefaultLazHeightDeltaTolerance = 0.010;

    private bool cubeVisible = true;
    private bool pointCloudVisible = true;
    private bool c3DSampleVisible;
    private bool glbSampleVisible;
    private bool lazSampleVisible;
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
    private string glbSampleTriangleCount = "(not loaded)";
    private string glbSampleSummary = "Imported mesh hidden";
    private string glbSampleName = "Public GLB Box";
    private string glbSampleSourcePath = @"3D\PublicSamples\glTF\Box.glb";
    private string importedMeshFormat = "GLB";
    private Vector3 importedMeshFitCenter = Vector3.Zero;
    private double importedMeshFitDistance = 5.2;
    private string lazSamplePointCount = "(not loaded)";
    private string lazSampleSummary = "LAZ/LAS metadata hidden";
    private string lazSampleName = "Public LAZ/LAS Point Cloud";
    private string lazSampleSourcePath = @"3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz";
    private Vector3 lazFitCenter = Vector3.Zero;
    private double lazFitDistance = 220.0;
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
    private string recipeParameterSummary = "Transform: identity | ROI: auto regions";
    private string recipeValidationSummary = string.Empty;
    private string recipeRoiMode = "Auto";
    private double recipeRoiLeftCenterX = -1.653;
    private double recipeRoiLeftCenterZ;
    private double recipeRoiLeftHalfWidth = 0.992;
    private double recipeRoiLeftHalfDepth = 2.500;
    private double recipeRoiRightCenterX = 1.653;
    private double recipeRoiRightCenterZ;
    private double recipeRoiRightHalfWidth = 0.992;
    private double recipeRoiRightHalfDepth = 2.500;
    private int recipeRoiMaxSampledPoints = 55000;
    private ToolResult previewToolResult = CreateNotRunToolResult();
    private ToolResult? c3dHeightDeviationPreview;
    private IReadOnlyList<SourceEntity> sourceEntities = [];
    private IReadOnlyList<ResultEntity> resultEntities = [];
    private string publishedResultSummary = "Published result: none";
    private IReadOnlyList<EntityLayer> entityLayers = [];
    private string sceneContractSummary = "(pending)";
    private ModelTransform c3DModelTransform = ModelTransform.Identity;
    private string transformSummary = "Transform: Identity | T(0.000, 0.000, 0.000) | R(0.0, 0.0, 0.0) | S 1.000";
    private string alignmentSummary = "Alignment: Not aligned | source frame";
    private string coordinateMappingSummary = "Mapping: source = aligned | raw-height retained";
    private string alignmentWorkflowSummary = "ROI alignment: not applied";
    private bool hudDetailsVisible = true;
    private bool deviationLegendVisible;
    private string deviationLegendStatus = "Status: inactive";
    private string deviationLegendPeak = "Peak: none";
    private string deviationLegendTolerance = "Tolerance: none";
    private string deviationLegendScale = "Scale: mean to peak deviation";
    private bool pointCloudColorLegendVisible;
    private string pointCloudColorLegendTitle = "Point Cloud Height Scale";
    private string pointCloudColorLegendLow = "Low: not loaded";
    private string pointCloudColorLegendHigh = "High: not loaded";
    private string pointCloudColorLegendScale = "Scale: source Z minimum to maximum";
    private double lazHeightMinimum = double.NaN;
    private double lazHeightMaximum = double.NaN;
    private string lazHeightUnit = "source-z";
    private bool heightMapVisible;
    private ImageSource? heightMapImageSource;
    private string heightMapSummary = "Height map: not loaded";
    private string heightMapRange = "Range: not loaded";
    private int heightMapPixelWidth;
    private int heightMapPixelHeight;
    private bool sectionProfileVisible;
    private string sectionProfileSummary = "Profile: not loaded";
    private string sectionProfileRange = "Range: not loaded";
    private string sectionProfilePathData = "M 0,30 L 240,30";
    private int sectionProfileSampleCount;
    private bool twoPointMeasurementVisible;
    private string twoPointMeasurementSummary = "Two-point: pick P1 and P2 on the C3D height grid.";
    private string twoPointMeasurementDetails = "Distance and height delta: pending";
    private string lazTwoPointAcceptanceSummary = "LAZ/LAS acceptance: pending";
    private double lazTwoPointExpectedDistance = DefaultLazExpectedDistance;
    private double lazTwoPointDistanceTolerance = DefaultLazDistanceTolerance;
    private double lazTwoPointExpectedHeightDelta = DefaultLazExpectedHeightDelta;
    private double lazTwoPointHeightDeltaTolerance = DefaultLazHeightDeltaTolerance;
    private Vector3? lazTwoPointPreviewFirst;
    private Vector3? lazTwoPointPreviewSecond;
    private string lazTwoPointPreviewHeightUnit = "source-z-units";
    private double twoPointDistance = double.NaN;
    private double twoPointDeltaX = double.NaN;
    private double twoPointDeltaY = double.NaN;
    private double twoPointDeltaZ = double.NaN;
    private double twoPointRawHeightDelta = double.NaN;
    private bool roiStepMeasurementVisible;
    private string roiStepMeasurementSummary = "ROI step: compare two C3D regions.";
    private string roiStepMeasurementDetails = "Left/right ROI height delta: pending";
    private string roiStepEditSummary = "ROI edit: auto regions; click left ROI then right ROI.";
    private string roiStepSelectionMode = "Auto";
    private double roiStepLeftRawMean = double.NaN;
    private double roiStepRightRawMean = double.NaN;
    private double roiStepRawHeightDelta = double.NaN;
    private double roiStepModelHeightDelta = double.NaN;
    private int roiStepLeftPointCount;
    private int roiStepRightPointCount;
    private string performanceSummary = "Performance: waiting for first frame";
    private double viewportFps = double.NaN;
    private double viewportDrawMilliseconds = double.NaN;
    private string lazSamplingSummary = "LAZ/LAS sampling: not loaded";
    private double lazLoadMilliseconds = double.NaN;
    private double lazSamplePercent = double.NaN;
    private int lazSampleStride;
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
        SourceEntities = CreateSourceEntities(C3DModelTransform, GlbSampleName, GlbSampleSourcePath, LazSampleName, LazSampleSourcePath);

        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public string[] ColorModes { get; } = ["Solid", "Height", "RGB", "Deviation"];

    public string[] RenderDensityModes { get; } = ["Fast", "Balanced", "Detailed"];

    public string[] SelectionModes { get; } = ["Point", "Two Point Measure", "ROI Step Compare", "Box ROI", "Section Plane"];

    public string CoordinateFrameSummary { get; } = "Right-handed | Y-up height | X red | Y green | Z blue";

    public bool HudDetailsVisible
    {
        get => hudDetailsVisible;
        set
        {
            if (SetField(ref hudDetailsVisible, value))
            {
                OnPropertyChanged(nameof(ImportedMeshHudDetailsVisible));
                OnPropertyChanged(nameof(LazHudDetailsVisible));
            }
        }
    }

    public bool ImportedMeshHudDetailsVisible => HudDetailsVisible && GlbSampleVisible;

    public bool LazHudDetailsVisible => HudDetailsVisible && LazSampleVisible;

    public IReadOnlyList<SourceEntity> SourceEntities
    {
        get => sourceEntities;
        private set => SetField(ref sourceEntities, value);
    }

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

    public ModelTransform C3DModelTransform
    {
        get => c3DModelTransform;
        private set => SetField(ref c3DModelTransform, value);
    }

    public string TransformSummary
    {
        get => transformSummary;
        private set => SetField(ref transformSummary, value);
    }

    public string AlignmentSummary
    {
        get => alignmentSummary;
        private set => SetField(ref alignmentSummary, value);
    }

    public string CoordinateMappingSummary
    {
        get => coordinateMappingSummary;
        private set => SetField(ref coordinateMappingSummary, value);
    }

    public string AlignmentWorkflowSummary
    {
        get => alignmentWorkflowSummary;
        private set => SetField(ref alignmentWorkflowSummary, value);
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

    public bool GlbSampleVisible
    {
        get => glbSampleVisible;
        set
        {
            if (SetField(ref glbSampleVisible, value))
            {
                ViewerStatus = value ? $"{ImportedMeshFormat} mesh visible" : $"{ImportedMeshFormat} mesh hidden";
                OnPropertyChanged(nameof(ImportedMeshHudDetailsVisible));
                RefreshSceneContracts();
            }
        }
    }

    public bool LazSampleVisible
    {
        get => lazSampleVisible;
        set
        {
            if (SetField(ref lazSampleVisible, value))
            {
                ViewerStatus = value ? "LAZ/LAS point cloud visible" : "LAZ/LAS point cloud hidden";
                OnPropertyChanged(nameof(LazHudDetailsVisible));
                RefreshPointCloudColorLegend();
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
            var requestedMode = value is not null && ColorModes.Contains(value) ? value : "Height";
            var mode = requestedMode == "Deviation" && !C3DSampleVisible && !ResultOverlayVisible
                ? (LazSampleVisible ? "RGB" : "Solid")
                : requestedMode;

            var changed = SetField(ref selectedColorMode, mode);
            if (changed || mode != requestedMode)
            {
                ViewerStatus = mode == requestedMode
                    ? $"Point cloud color mode: {mode}"
                    : $"Point cloud color mode: {mode} (Deviation requires an active result)";
                if (changed)
                {
                    RefreshPointCloudColorLegend();
                }
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

    public int LazMaxSampledPoints => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 150000,
        _ => 50000
    };

    public int ImportedMeshMaxRenderedTriangles => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 180000,
        _ => 60000
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

    public string GlbSampleTriangleCount
    {
        get => glbSampleTriangleCount;
        set => SetField(ref glbSampleTriangleCount, value);
    }

    public string GlbSampleSummary
    {
        get => glbSampleSummary;
        set => SetField(ref glbSampleSummary, value);
    }

    public string GlbSampleName
    {
        get => glbSampleName;
        private set => SetField(ref glbSampleName, value);
    }

    public string GlbSampleSourcePath
    {
        get => glbSampleSourcePath;
        private set => SetField(ref glbSampleSourcePath, value);
    }

    public string ImportedMeshFormat
    {
        get => importedMeshFormat;
        private set => SetField(ref importedMeshFormat, value);
    }

    public string ImportedMeshLayerLabel => $"{ImportedMeshFormat} Mesh: {GlbSampleName}";

    public string LazSamplePointCount
    {
        get => lazSamplePointCount;
        set => SetField(ref lazSamplePointCount, value);
    }

    public string LazSampleSummary
    {
        get => lazSampleSummary;
        set => SetField(ref lazSampleSummary, value);
    }

    public string LazSampleName
    {
        get => lazSampleName;
        private set => SetField(ref lazSampleName, value);
    }

    public string LazSampleSourcePath
    {
        get => lazSampleSourcePath;
        private set => SetField(ref lazSampleSourcePath, value);
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
                    "Two Point Measure" => TwoPointMeasurementSummary,
                    "ROI Step Compare" => RoiStepMeasurementDetails,
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

    public string RecipeParameterSummary
    {
        get => recipeParameterSummary;
        private set => SetField(ref recipeParameterSummary, value);
    }

    public string RecipeValidationSummary
    {
        get => recipeValidationSummary;
        private set => SetField(ref recipeValidationSummary, value);
    }

    public double RecipeTransformTranslateX
    {
        get => C3DModelTransform.TranslateX;
        set => SetRecipeTransform(C3DModelTransform with { TranslateX = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformTranslateY
    {
        get => C3DModelTransform.TranslateY;
        set => SetRecipeTransform(C3DModelTransform with { TranslateY = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformTranslateZ
    {
        get => C3DModelTransform.TranslateZ;
        set => SetRecipeTransform(C3DModelTransform with { TranslateZ = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformRotateXDegrees
    {
        get => C3DModelTransform.RotateXDegrees;
        set => SetRecipeTransform(C3DModelTransform with { RotateXDegrees = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformRotateYDegrees
    {
        get => C3DModelTransform.RotateYDegrees;
        set => SetRecipeTransform(C3DModelTransform with { RotateYDegrees = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformRotateZDegrees
    {
        get => C3DModelTransform.RotateZDegrees;
        set => SetRecipeTransform(C3DModelTransform with { RotateZDegrees = CoerceFinite(value, 0.0) });
    }

    public double RecipeTransformScale
    {
        get => C3DModelTransform.Scale;
        set => SetRecipeTransform(C3DModelTransform with { Scale = Math.Max(0.001, CoerceFinite(value, 1.0)) });
    }

    public string RecipeRoiMode
    {
        get => recipeRoiMode;
        private set => SetField(ref recipeRoiMode, string.IsNullOrWhiteSpace(value) ? "Auto" : value);
    }

    public double RecipeRoiLeftCenterX
    {
        get => recipeRoiLeftCenterX;
        set
        {
            if (SetField(ref recipeRoiLeftCenterX, CoerceFinite(value, recipeRoiLeftCenterX)))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiLeftCenterZ
    {
        get => recipeRoiLeftCenterZ;
        set
        {
            if (SetField(ref recipeRoiLeftCenterZ, CoerceFinite(value, recipeRoiLeftCenterZ)))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiLeftHalfWidth
    {
        get => recipeRoiLeftHalfWidth;
        set
        {
            if (SetField(ref recipeRoiLeftHalfWidth, Math.Max(0.0001, CoerceFinite(value, recipeRoiLeftHalfWidth))))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiLeftHalfDepth
    {
        get => recipeRoiLeftHalfDepth;
        set
        {
            if (SetField(ref recipeRoiLeftHalfDepth, Math.Max(0.0001, CoerceFinite(value, recipeRoiLeftHalfDepth))))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiRightCenterX
    {
        get => recipeRoiRightCenterX;
        set
        {
            if (SetField(ref recipeRoiRightCenterX, CoerceFinite(value, recipeRoiRightCenterX)))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiRightCenterZ
    {
        get => recipeRoiRightCenterZ;
        set
        {
            if (SetField(ref recipeRoiRightCenterZ, CoerceFinite(value, recipeRoiRightCenterZ)))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiRightHalfWidth
    {
        get => recipeRoiRightHalfWidth;
        set
        {
            if (SetField(ref recipeRoiRightHalfWidth, Math.Max(0.0001, CoerceFinite(value, recipeRoiRightHalfWidth))))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public double RecipeRoiRightHalfDepth
    {
        get => recipeRoiRightHalfDepth;
        set
        {
            if (SetField(ref recipeRoiRightHalfDepth, Math.Max(0.0001, CoerceFinite(value, recipeRoiRightHalfDepth))))
            {
                RefreshRecipeParameterSummary();
            }
        }
    }

    public int RecipeRoiMaxSampledPoints
    {
        get => recipeRoiMaxSampledPoints;
        private set => SetField(ref recipeRoiMaxSampledPoints, Math.Max(1, value));
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

    public bool PointCloudColorLegendVisible
    {
        get => pointCloudColorLegendVisible;
        private set => SetField(ref pointCloudColorLegendVisible, value);
    }

    public string PointCloudColorLegendTitle
    {
        get => pointCloudColorLegendTitle;
        private set => SetField(ref pointCloudColorLegendTitle, value);
    }

    public string PointCloudColorLegendLow
    {
        get => pointCloudColorLegendLow;
        private set => SetField(ref pointCloudColorLegendLow, value);
    }

    public string PointCloudColorLegendHigh
    {
        get => pointCloudColorLegendHigh;
        private set => SetField(ref pointCloudColorLegendHigh, value);
    }

    public string PointCloudColorLegendScale
    {
        get => pointCloudColorLegendScale;
        private set => SetField(ref pointCloudColorLegendScale, value);
    }

    public bool HeightMapVisible
    {
        get => heightMapVisible;
        private set => SetField(ref heightMapVisible, value);
    }

    public ImageSource? HeightMapImageSource
    {
        get => heightMapImageSource;
        private set => SetField(ref heightMapImageSource, value);
    }

    public string HeightMapSummary
    {
        get => heightMapSummary;
        private set => SetField(ref heightMapSummary, value);
    }

    public string HeightMapRange
    {
        get => heightMapRange;
        private set => SetField(ref heightMapRange, value);
    }

    public int HeightMapPixelWidth
    {
        get => heightMapPixelWidth;
        private set => SetField(ref heightMapPixelWidth, value);
    }

    public int HeightMapPixelHeight
    {
        get => heightMapPixelHeight;
        private set => SetField(ref heightMapPixelHeight, value);
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

    public bool TwoPointMeasurementVisible
    {
        get => twoPointMeasurementVisible;
        private set => SetField(ref twoPointMeasurementVisible, value);
    }

    public string TwoPointMeasurementSummary
    {
        get => twoPointMeasurementSummary;
        private set => SetField(ref twoPointMeasurementSummary, value);
    }

    public string TwoPointMeasurementDetails
    {
        get => twoPointMeasurementDetails;
        private set => SetField(ref twoPointMeasurementDetails, value);
    }

    public string LazTwoPointAcceptanceSummary
    {
        get => lazTwoPointAcceptanceSummary;
        private set => SetField(ref lazTwoPointAcceptanceSummary, value);
    }

    public double LazTwoPointExpectedDistance
    {
        get => lazTwoPointExpectedDistance;
        set
        {
            if (SetField(ref lazTwoPointExpectedDistance, CoerceFinite(value, DefaultLazExpectedDistance)))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double LazTwoPointDistanceTolerance
    {
        get => lazTwoPointDistanceTolerance;
        set
        {
            var tolerance = Math.Max(0.0, CoerceFinite(value, DefaultLazDistanceTolerance));
            if (SetField(ref lazTwoPointDistanceTolerance, tolerance))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double LazTwoPointExpectedHeightDelta
    {
        get => lazTwoPointExpectedHeightDelta;
        set
        {
            if (SetField(ref lazTwoPointExpectedHeightDelta, CoerceFinite(value, DefaultLazExpectedHeightDelta)))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double LazTwoPointHeightDeltaTolerance
    {
        get => lazTwoPointHeightDeltaTolerance;
        set
        {
            var tolerance = Math.Max(0.0, CoerceFinite(value, DefaultLazHeightDeltaTolerance));
            if (SetField(ref lazTwoPointHeightDeltaTolerance, tolerance))
            {
                RefreshLazTwoPointAcceptanceState();
            }
        }
    }

    public double TwoPointDistance
    {
        get => twoPointDistance;
        private set => SetField(ref twoPointDistance, value);
    }

    public double TwoPointDeltaX
    {
        get => twoPointDeltaX;
        private set => SetField(ref twoPointDeltaX, value);
    }

    public double TwoPointDeltaY
    {
        get => twoPointDeltaY;
        private set => SetField(ref twoPointDeltaY, value);
    }

    public double TwoPointDeltaZ
    {
        get => twoPointDeltaZ;
        private set => SetField(ref twoPointDeltaZ, value);
    }

    public double TwoPointRawHeightDelta
    {
        get => twoPointRawHeightDelta;
        private set => SetField(ref twoPointRawHeightDelta, value);
    }

    public bool RoiStepMeasurementVisible
    {
        get => roiStepMeasurementVisible;
        private set => SetField(ref roiStepMeasurementVisible, value);
    }

    public string RoiStepMeasurementSummary
    {
        get => roiStepMeasurementSummary;
        private set => SetField(ref roiStepMeasurementSummary, value);
    }

    public string RoiStepMeasurementDetails
    {
        get => roiStepMeasurementDetails;
        private set => SetField(ref roiStepMeasurementDetails, value);
    }

    public string RoiStepEditSummary
    {
        get => roiStepEditSummary;
        private set => SetField(ref roiStepEditSummary, value);
    }

    public string RoiStepSelectionMode
    {
        get => roiStepSelectionMode;
        private set => SetField(ref roiStepSelectionMode, value);
    }

    public double RoiStepLeftRawMean
    {
        get => roiStepLeftRawMean;
        private set => SetField(ref roiStepLeftRawMean, value);
    }

    public double RoiStepRightRawMean
    {
        get => roiStepRightRawMean;
        private set => SetField(ref roiStepRightRawMean, value);
    }

    public double RoiStepRawHeightDelta
    {
        get => roiStepRawHeightDelta;
        private set => SetField(ref roiStepRawHeightDelta, value);
    }

    public double RoiStepModelHeightDelta
    {
        get => roiStepModelHeightDelta;
        private set => SetField(ref roiStepModelHeightDelta, value);
    }

    public int RoiStepLeftPointCount
    {
        get => roiStepLeftPointCount;
        private set => SetField(ref roiStepLeftPointCount, value);
    }

    public int RoiStepRightPointCount
    {
        get => roiStepRightPointCount;
        private set => SetField(ref roiStepRightPointCount, value);
    }

    public string PerformanceSummary
    {
        get => performanceSummary;
        private set => SetField(ref performanceSummary, value);
    }

    public double ViewportFps
    {
        get => viewportFps;
        private set => SetField(ref viewportFps, value);
    }

    public double ViewportDrawMilliseconds
    {
        get => viewportDrawMilliseconds;
        private set => SetField(ref viewportDrawMilliseconds, value);
    }

    public string LazSamplingSummary
    {
        get => lazSamplingSummary;
        private set => SetField(ref lazSamplingSummary, value);
    }

    public double LazLoadMilliseconds
    {
        get => lazLoadMilliseconds;
        private set => SetField(ref lazLoadMilliseconds, value);
    }

    public double LazSamplePercent
    {
        get => lazSamplePercent;
        private set => SetField(ref lazSamplePercent, value);
    }

    public int LazSampleStride
    {
        get => lazSampleStride;
        private set => SetField(ref lazSampleStride, value);
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
        else if (GlbSampleVisible)
        {
            FitGlbCamera();
        }
        else if (LazSampleVisible)
        {
            FitLazCamera();
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
        else if ((SelectedEntity == "Public GLB Mesh" || SelectedEntity == $"{ImportedMeshFormat} Mesh") && GlbSampleVisible)
        {
            FitGlbCamera();
            ViewerStatus = $"Fit selected {ImportedMeshFormat} mesh";
        }
        else if ((SelectedEntity == "Public LAZ/LAS Metadata" || SelectedEntity == "Public LAZ/LAS Point Cloud") && LazSampleVisible)
        {
            FitLazCamera();
            ViewerStatus = "Fit selected LAZ bounds";
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
        HudDetailsVisible = true;
        ClearC3DLinkedViews();
        CubeVisible = false;
        MeasurementVisible = false;
        ResultOverlayVisible = false;
        C3DSampleVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = false;
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
        var keepCurrentC3DScene = (mode == "Section Plane" || mode == "ROI Step Compare") && C3DSampleVisible;
        if (!keepCurrentC3DScene)
        {
            UsePointCloudSmokeScene();
        }

        SelectionOverlayVisible = true;
        SelectedSelectionMode = mode;
        SelectedEntity = mode switch
        {
            "Box ROI" => "Box ROI",
            "ROI Step Compare" => "ROI Step Compare",
            "Section Plane" => "Section Plane",
            _ => "Generated Point Cloud"
        };
        SelectionSummary = mode switch
        {
            "Section Plane" when SectionProfileVisible => SectionProfileSummary,
            "Section Plane" => "Section plane: profile not loaded",
            "Two Point Measure" => TwoPointMeasurementSummary,
            "ROI Step Compare" => RoiStepMeasurementDetails,
            "Box ROI" => "Box ROI: viewer state only",
            _ => "Point selection: generated point cloud peak"
        };
        ViewerStatus = $"Smoke scene: {mode}";
    }

    public void UseC3DSmokeScene()
    {
        HudDetailsVisible = true;
        CubeVisible = false;
        PointCloudVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = false;
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

    public void UseGlbSmokeScene()
    {
        HudDetailsVisible = false;
        var meshLabel = ImportedMeshDisplayName();
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        LazSampleVisible = false;
        GlbSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = meshLabel;
        PickCoordinate = "(not picked)";
        SelectionSummary = $"Mesh selection: {ImportedMeshFormat} mesh loaded";
        MeasurementSummary = $"{ImportedMeshFormat} mesh loaded; measurement tools pending";
        SelectedColorMode = "Solid";
        YawDegrees = 38.0;
        PitchDegrees = 26.0;
        FitGlbCamera();
        ViewerStatus = $"Smoke scene: {ImportedMeshFormat} mesh";
        UpdateCameraStatus();
    }

    public void UseGlbFailureScene(string summary)
    {
        HudDetailsVisible = true;
        var meshLabel = ImportedMeshDisplayName();
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        LazSampleVisible = false;
        GlbSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = meshLabel;
        PickCoordinate = "(load failed)";
        SelectionSummary = summary;
        MeasurementSummary = $"{ImportedMeshFormat} load failed; see Viewer status and contract output.";
        SelectedColorMode = "Solid";
    }

    public void UseLazSmokeScene()
    {
        HudDetailsVisible = false;
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "Public LAZ/LAS Metadata";
        PickCoordinate = "(metadata only)";
        SelectionSummary = "Point selection: LAZ/LAS metadata loaded";
        MeasurementSummary = "LAZ header loaded; compressed point records not decoded yet";
        SelectedColorMode = "Solid";
        YawDegrees = 34.0;
        PitchDegrees = 34.0;
        FitLazCamera();
        ViewerStatus = "Smoke scene: public LAZ/LAS metadata";
        UpdateCameraStatus();
    }

    public void UseLazFailureScene(string summary)
    {
        HudDetailsVisible = true;
        ClearC3DLinkedViews();
        CubeVisible = false;
        PointCloudVisible = false;
        C3DSampleVisible = false;
        GlbSampleVisible = false;
        LazSampleVisible = true;
        MeasurementVisible = false;
        SelectionOverlayVisible = false;
        ResultOverlayVisible = false;
        SelectedEntity = "Public LAZ/LAS Point Cloud";
        PickCoordinate = "(load failed)";
        SelectionSummary = summary;
        MeasurementSummary = "LAZ/LAS load failed; see Viewer status and contract output.";
        SelectedColorMode = "Solid";
    }

    public void UseLazPointSmokeScene()
    {
        UseLazSmokeScene();
        SelectedEntity = "Public LAZ/LAS Point Cloud";
        PickCoordinate = "(sampled points)";
        SelectionSummary = "Point selection: LAZ/LAS sampled point cloud";
        MeasurementSummary = "LAZ/LAS point decode loaded; XYZ/RGB sampled points rendered.";
        SelectedColorMode = "RGB";
        ViewerStatus = "Smoke scene: public LAZ/LAS point cloud";
    }

    private void ClearC3DLinkedViews()
    {
        ClearHeightMap();
        ClearSectionProfile();
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

    public void SetTwoPointMeasurementStart(Vector3 point, float rawValue) =>
        SetTwoPointMeasurementStart(point, rawValue, "raw-height");

    public void SetTwoPointMeasurementStart(Vector3 point, float heightValue, string heightUnit)
    {
        TwoPointMeasurementVisible = true;
        TwoPointDistance = double.NaN;
        TwoPointDeltaX = double.NaN;
        TwoPointDeltaY = double.NaN;
        TwoPointDeltaZ = double.NaN;
        TwoPointRawHeightDelta = double.NaN;
        var valueLabel = heightUnit == "raw-height"
            ? $"raw {heightValue:F3}"
            : $"height {heightValue:F3} {heightUnit}";
        TwoPointMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1: {FormatVector(point)} | {valueLabel}");
        TwoPointMeasurementDetails = "Pick P2 to measure distance and height delta.";
        SelectionSummary = TwoPointMeasurementSummary;
        MeasurementSummary = TwoPointMeasurementDetails;
        ViewerStatus = "Two-point P1 set";
    }

    public void SetTwoPointMeasurement(Vector3 first, float firstRaw, Vector3 second, float secondRaw) =>
        SetTwoPointMeasurement(first, firstRaw, second, secondRaw, "raw-height");

    public void SetTwoPointMeasurement(Vector3 first, float firstHeight, Vector3 second, float secondHeight, string heightUnit)
    {
        var delta = second - first;
        var distance = delta.Length();
        var heightDelta = secondHeight - firstHeight;
        var heightDeltaLabel = heightUnit == "raw-height"
            ? $"height delta {heightDelta:F3} raw-height"
            : $"height delta {heightDelta:F3} {heightUnit}";

        TwoPointMeasurementVisible = true;
        TwoPointDistance = distance;
        TwoPointDeltaX = delta.X;
        TwoPointDeltaY = delta.Y;
        TwoPointDeltaZ = delta.Z;
        TwoPointRawHeightDelta = heightDelta;
        TwoPointMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1 {FormatVector(first)} -> P2 {FormatVector(second)}");
        TwoPointMeasurementDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Distance {distance:F3} model | dX {delta.X:F3}, dY {delta.Y:F3}, dZ {delta.Z:F3} | {heightDeltaLabel}");
        SelectionSummary = TwoPointMeasurementDetails;
        MeasurementSummary = TwoPointMeasurementDetails;
        LazTwoPointAcceptanceSummary = "LAZ/LAS acceptance: pending";
        ViewerStatus = "Two-point measurement updated";
    }

    public void SetLazTwoPointMeasurementPreview(Vector3 first, Vector3 second, double heightDelta, string heightUnit)
    {
        activePreviewLayerId = "layer.preview.laz-two-point-measurement";
        activePreviewLayerName = "Preview: LAZ/LAS Two Point Measurement";
        activePreviewSourceEntityId = LazEntityId;
        activeResultEntityId = LazTwoPointResultEntityId;
        activeResultEntityName = "Published LAZ/LAS Two Point Measurement";
        lazTwoPointPreviewFirst = first;
        lazTwoPointPreviewSecond = second;
        lazTwoPointPreviewHeightUnit = string.IsNullOrWhiteSpace(heightUnit) ? "source-z-units" : heightUnit;
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        RefreshLazTwoPointAcceptanceState();
        ViewerStatus = "LAZ/LAS two-point result preview ready";
    }

    public void ClearTwoPointMeasurement()
    {
        TwoPointMeasurementVisible = false;
        TwoPointDistance = double.NaN;
        TwoPointDeltaX = double.NaN;
        TwoPointDeltaY = double.NaN;
        TwoPointDeltaZ = double.NaN;
        TwoPointRawHeightDelta = double.NaN;
        TwoPointMeasurementSummary = "Two-point: pick P1 and P2 on the C3D height grid.";
        TwoPointMeasurementDetails = "Distance and height delta: pending";
        lazTwoPointPreviewFirst = null;
        lazTwoPointPreviewSecond = null;
        LazTwoPointAcceptanceSummary = "LAZ/LAS acceptance: pending";
        if (SelectedSelectionMode == "Two Point Measure")
        {
            SelectionSummary = TwoPointMeasurementSummary;
        }
    }

    public void SetRoiStepSelectionPending(string summary, string details, string selectionMode)
    {
        RoiStepMeasurementVisible = true;
        RoiStepSelectionMode = selectionMode;
        RoiStepLeftPointCount = 0;
        RoiStepRightPointCount = 0;
        RoiStepLeftRawMean = double.NaN;
        RoiStepRightRawMean = double.NaN;
        RoiStepRawHeightDelta = double.NaN;
        RoiStepModelHeightDelta = double.NaN;
        RoiStepMeasurementSummary = summary;
        RoiStepMeasurementDetails = details;
        RoiStepEditSummary = "ROI edit: click right ROI center to finish comparison.";
        SelectionSummary = details;
        MeasurementSummary = details;
        ViewerStatus = "ROI step left ROI set";
    }

    public void SetRoiStepMeasurement(int leftPointCount, double leftRawMean, double leftModelMeanY, int rightPointCount, double rightRawMean, double rightModelMeanY, string selectionMode)
    {
        var rawDelta = rightRawMean - leftRawMean;
        var modelDelta = rightModelMeanY - leftModelMeanY;

        RoiStepMeasurementVisible = true;
        RoiStepSelectionMode = selectionMode;
        RoiStepLeftPointCount = leftPointCount;
        RoiStepRightPointCount = rightPointCount;
        RoiStepLeftRawMean = leftRawMean;
        RoiStepRightRawMean = rightRawMean;
        RoiStepRawHeightDelta = rawDelta;
        RoiStepModelHeightDelta = modelDelta;
        RoiStepMeasurementSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"ROI step: L {leftPointCount:N0} pts vs R {rightPointCount:N0} pts");
        RoiStepMeasurementDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Mean raw L {leftRawMean:F3}, R {rightRawMean:F3} | step {rawDelta:F3} raw-height | model dY {modelDelta:F3}");
        RoiStepEditSummary = selectionMode == "Interactive"
            ? "ROI edit: interactive L/R centers selected; click again to start a new pair."
            : "ROI edit: auto regions; click left ROI then right ROI.";
        SelectionSummary = RoiStepMeasurementDetails;
        MeasurementSummary = RoiStepMeasurementDetails;
        ViewerStatus = "ROI step comparison updated";
    }

    public void ClearRoiStepMeasurement(string details = "Left/right ROI height delta: pending")
    {
        RoiStepMeasurementVisible = false;
        RoiStepLeftPointCount = 0;
        RoiStepRightPointCount = 0;
        RoiStepLeftRawMean = double.NaN;
        RoiStepRightRawMean = double.NaN;
        RoiStepRawHeightDelta = double.NaN;
        RoiStepModelHeightDelta = double.NaN;
        RoiStepMeasurementSummary = "ROI step: compare two C3D regions.";
        RoiStepMeasurementDetails = details;
        RoiStepEditSummary = "ROI edit: auto regions; click left ROI then right ROI.";
        RoiStepSelectionMode = "Auto";
        if (SelectedSelectionMode == "ROI Step Compare")
        {
            SelectionSummary = RoiStepMeasurementDetails;
        }
    }

    public void SetC3DAlignment(ModelTransform transform, string alignmentName, string referenceName)
    {
        C3DModelTransform = transform;
        SourceEntities = CreateSourceEntities(transform, GlbSampleName, GlbSampleSourcePath, LazSampleName, LazSampleSourcePath);
        TransformSummary = $"Transform: {FormatModelTransform(transform)}";
        AlignmentSummary = $"Alignment: {alignmentName} | reference {referenceName}";
        CoordinateMappingSummary = ModelTransformIsIdentity(transform)
            ? "Mapping: source = aligned | raw-height retained"
            : "Mapping: source -> aligned display coordinates | raw-height retained";
        ViewerStatus = $"Alignment state: {alignmentName}";
        RefreshSceneContracts();
        NotifyRecipeTransformProperties();
        RefreshRecipeParameterSummary();
    }

    public void SetGlbSampleSource(string sourcePath, string sourceName, string format = "GLB")
    {
        ImportedMeshFormat = string.IsNullOrWhiteSpace(format) ? "GLB" : format.Trim().ToUpperInvariant();
        GlbSampleSourcePath = sourcePath;
        GlbSampleName = string.IsNullOrWhiteSpace(sourceName) ? ImportedMeshDisplayName() : sourceName;
        OnPropertyChanged(nameof(ImportedMeshLayerLabel));
        SourceEntities = CreateSourceEntities(C3DModelTransform, GlbSampleName, GlbSampleSourcePath, LazSampleName, LazSampleSourcePath);
        RefreshSceneContracts();
    }

    public void SetGlbSampleBounds(Vector3 min, Vector3 max)
    {
        importedMeshFitCenter = (min + max) * 0.5f;
        var radius = Math.Max(0.001, Vector3.Distance(min, max) * 0.5);
        importedMeshFitDistance = Math.Clamp(radius / Math.Tan(Math.PI / 8.0) * 1.7, 0.35, 12000.0);
    }

    public void SetLazSampleSource(string sourcePath, string sourceName)
    {
        LazSampleSourcePath = sourcePath;
        LazSampleName = string.IsNullOrWhiteSpace(sourceName) ? "Public LAZ/LAS Point Cloud" : sourceName;
        SourceEntities = CreateSourceEntities(C3DModelTransform, GlbSampleName, GlbSampleSourcePath, LazSampleName, LazSampleSourcePath);
        RefreshSceneContracts();
    }

    public void SetLazSampleBounds(Vector3 min, Vector3 max)
    {
        lazFitCenter = (min + max) * 0.5f;
        var radius = Math.Max(1.0, Vector3.Distance(min, max) * 0.5);
        lazFitDistance = Math.Clamp(radius / Math.Tan(Math.PI / 8.0) * 1.35, 80.0, 12000.0);
    }

    public void SetLazHeightRange(double minimum, double maximum, string unit)
    {
        lazHeightMinimum = CoerceFinite(minimum, double.NaN);
        lazHeightMaximum = CoerceFinite(maximum, double.NaN);
        lazHeightUnit = string.IsNullOrWhiteSpace(unit) ? "source-z" : unit;
        PointCloudColorLegendTitle = "Point Cloud Height Scale";
        PointCloudColorLegendLow = double.IsFinite(lazHeightMinimum)
            ? string.Create(CultureInfo.InvariantCulture, $"Low: {lazHeightMinimum:F3} {lazHeightUnit}")
            : "Low: not loaded";
        PointCloudColorLegendHigh = double.IsFinite(lazHeightMaximum)
            ? string.Create(CultureInfo.InvariantCulture, $"High: {lazHeightMaximum:F3} {lazHeightUnit}")
            : "High: not loaded";
        PointCloudColorLegendScale = "Scale: source Z min to max";
        RefreshPointCloudColorLegend();
    }

    public void SetRecipeRoiStepEdit(
        string mode,
        double leftCenterX,
        double leftCenterZ,
        double leftHalfWidth,
        double leftHalfDepth,
        double rightCenterX,
        double rightCenterZ,
        double rightHalfWidth,
        double rightHalfDepth,
        int maxSampledPoints)
    {
        RecipeRoiMode = mode;
        SetField(ref recipeRoiLeftCenterX, CoerceFinite(leftCenterX, recipeRoiLeftCenterX), nameof(RecipeRoiLeftCenterX));
        SetField(ref recipeRoiLeftCenterZ, CoerceFinite(leftCenterZ, recipeRoiLeftCenterZ), nameof(RecipeRoiLeftCenterZ));
        SetField(ref recipeRoiLeftHalfWidth, Math.Max(0.0001, CoerceFinite(leftHalfWidth, recipeRoiLeftHalfWidth)), nameof(RecipeRoiLeftHalfWidth));
        SetField(ref recipeRoiLeftHalfDepth, Math.Max(0.0001, CoerceFinite(leftHalfDepth, recipeRoiLeftHalfDepth)), nameof(RecipeRoiLeftHalfDepth));
        SetField(ref recipeRoiRightCenterX, CoerceFinite(rightCenterX, recipeRoiRightCenterX), nameof(RecipeRoiRightCenterX));
        SetField(ref recipeRoiRightCenterZ, CoerceFinite(rightCenterZ, recipeRoiRightCenterZ), nameof(RecipeRoiRightCenterZ));
        SetField(ref recipeRoiRightHalfWidth, Math.Max(0.0001, CoerceFinite(rightHalfWidth, recipeRoiRightHalfWidth)), nameof(RecipeRoiRightHalfWidth));
        SetField(ref recipeRoiRightHalfDepth, Math.Max(0.0001, CoerceFinite(rightHalfDepth, recipeRoiRightHalfDepth)), nameof(RecipeRoiRightHalfDepth));
        RecipeRoiMaxSampledPoints = maxSampledPoints;
        RefreshRecipeParameterSummary();
    }

    public void SetAlignmentWorkflowSummary(string summary)
    {
        AlignmentWorkflowSummary = string.IsNullOrWhiteSpace(summary) ? "ROI alignment: not applied" : summary;
    }

    public void SetRecipeValidationSummary(string summary)
    {
        RecipeValidationSummary = string.IsNullOrWhiteSpace(summary) || summary == "Validation: OK" ? string.Empty : summary;
    }

    private void SetRecipeTransform(ModelTransform transform)
    {
        SetC3DAlignment(transform, "Manual recipe alignment", RecipeSourceName);
    }

    public void SetRenderPerformance(double fps, double drawMilliseconds)
    {
        ViewportFps = fps;
        ViewportDrawMilliseconds = drawMilliseconds;
        PerformanceSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Performance: {fps:F1} fps | draw {drawMilliseconds:F2} ms | C3D points {C3DSamplePointCount}");
    }

    public void SetLazSamplingTelemetry(ulong decodedPointCount, int sampledPointCount, int sampleStride, double loadMilliseconds)
    {
        var percent = decodedPointCount > 0
            ? sampledPointCount / (double)decodedPointCount * 100.0
            : 0.0;
        LazLoadMilliseconds = CoerceFinite(loadMilliseconds, double.NaN);
        LazSamplePercent = percent;
        LazSampleStride = Math.Max(0, sampleStride);
        LazSamplingSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"LAZ/LAS sampling: load {LazLoadMilliseconds:F0} ms | sampled {sampledPointCount:N0}/{decodedPointCount:N0} ({percent:F2}%) | stride {LazSampleStride} | density {SelectedRenderDensity}");
    }

    public void ClearLazSamplingTelemetry(string summary = "LAZ/LAS sampling: not loaded")
    {
        LazLoadMilliseconds = double.NaN;
        LazSamplePercent = double.NaN;
        LazSampleStride = 0;
        LazSamplingSummary = string.IsNullOrWhiteSpace(summary) ? "LAZ/LAS sampling: not loaded" : summary;
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
        ViewerStatus = $"Result published: {resultEntity.Name}";
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

    public void ZoomCamera(double zoomScale)
    {
        var minimumDistance = GlbSampleVisible
            ? Math.Max(0.05, importedMeshFitDistance * 0.02)
            : 2.4;
        var maximumDistance = GlbSampleVisible
            ? Math.Max(20.0, importedMeshFitDistance * 2.5)
            : LazSampleVisible
                ? Math.Max(20.0, lazFitDistance * 2.5)
                : 20.0;
        CameraDistance = Math.Clamp(CameraDistance * zoomScale, minimumDistance, maximumDistance);
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

    private void FitGlbCamera()
    {
        SetCameraTarget(importedMeshFitCenter.X, importedMeshFitCenter.Y, importedMeshFitCenter.Z);
        CameraDistance = importedMeshFitDistance;
    }

    private void FitLazCamera()
    {
        SetCameraTarget(lazFitCenter.X, lazFitCenter.Y, lazFitCenter.Z);
        CameraDistance = lazFitDistance;
    }

    private void RefreshSceneContracts()
    {
        var layers = new List<EntityLayer>
        {
            new EntityLayer("layer.source.generated-cube", "Generated Unit Cube", LayerKind.Source, CubeVisible, [CubeEntityId]),
            new EntityLayer("layer.source.generated-point-cloud", "Generated Point Cloud", LayerKind.Source, PointCloudVisible, [PointCloudEntityId]),
            new EntityLayer("layer.source.c3d-thickness", "C3D Thickness Sample", LayerKind.Source, C3DSampleVisible, [C3DEntityId]),
            new EntityLayer("layer.source.imported-mesh", GlbSampleName, LayerKind.Source, GlbSampleVisible, [GlbEntityId]),
            new EntityLayer("layer.source.public-laz-manuscript", "Public LAZ/LAS Point Cloud", LayerKind.Source, LazSampleVisible, [LazEntityId])
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
            var resultLayer = CreatePublishedResultLayer(ResultEntities);
            layers.Add(new EntityLayer(
                resultLayer.Id,
                resultLayer.Name,
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

    private void RefreshLazTwoPointAcceptanceState()
    {
        if (activeResultEntityId != LazTwoPointResultEntityId
            || !TwoPointMeasurementVisible
            || !double.IsFinite(TwoPointDistance)
            || !double.IsFinite(TwoPointRawHeightDelta))
        {
            return;
        }

        var distanceStatus = Math.Abs(TwoPointDistance - LazTwoPointExpectedDistance) <= LazTwoPointDistanceTolerance
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var heightStatus = Math.Abs(TwoPointRawHeightDelta - LazTwoPointExpectedHeightDelta) <= LazTwoPointHeightDeltaTolerance
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var status = distanceStatus == ResultStatus.Pass && heightStatus == ResultStatus.Pass
            ? ResultStatus.Pass
            : ResultStatus.Fail;

        LazTwoPointAcceptanceSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"LAZ/LAS acceptance: {status} | distance {TwoPointDistance:F3} vs {LazTwoPointExpectedDistance:F3} +/- {LazTwoPointDistanceTolerance:F3} model | height delta {TwoPointRawHeightDelta:F3} vs {LazTwoPointExpectedHeightDelta:F3} +/- {LazTwoPointHeightDeltaTolerance:F3} {lazTwoPointPreviewHeightUnit}");

        if (lazTwoPointPreviewFirst is { } first && lazTwoPointPreviewSecond is { } second)
        {
            PreviewToolResult = CreateLazTwoPointMeasurementPreview(
                first,
                second,
                TwoPointRawHeightDelta,
                lazTwoPointPreviewHeightUnit,
                distanceStatus,
                heightStatus,
                status);
            ResultSummary = FormatToolResult(PreviewToolResult);
        }

        RefreshSceneContracts();
    }

    private ToolResult CreateLazTwoPointMeasurementPreview(
        Vector3 first,
        Vector3 second,
        double heightDelta,
        string heightUnit,
        ResultStatus distanceStatus,
        ResultStatus heightStatus,
        ResultStatus status)
    {
        var delta = second - first;
        var distance = delta.Length();

        return new ToolResult(
            "LAZ/LAS Two Point Measurement",
            status,
            status == ResultStatus.Pass
                ? "Preview within configured tolerance; source point cloud is unchanged and no result is published."
                : "Preview exceeds configured tolerance; source point cloud is unchanged and no result is published.",
            TimeSpan.Zero,
            [
                new Metric("Distance", MetricKind.Length, distance, "model", distanceStatus),
                new Metric("Delta X", MetricKind.Length, delta.X, "model", ResultStatus.Pass),
                new Metric("Delta Y", MetricKind.Length, delta.Y, "model", ResultStatus.Pass),
                new Metric("Delta Z", MetricKind.Length, delta.Z, "model", ResultStatus.Pass),
                new Metric("Source Z height delta", MetricKind.Length, heightDelta, heightUnit, heightStatus)
            ],
            [
                new Overlay("overlay.laz-two-point-line", OverlayKind.Polyline, "LAZ/LAS two-point distance line", distanceStatus, LazEntityId),
                new Overlay("overlay.laz-two-point-height-marker", OverlayKind.Marker, "LAZ/LAS source-Z height delta marker", heightStatus, LazEntityId)
            ]);
    }

    private ResultEntity CreatePublishedResultEntity(ToolResult result) =>
        new(
            activeResultEntityId,
            activeResultEntityName,
            activePreviewSourceEntityId,
            result.Status,
            "Published from preview; source geometry remains unchanged.",
            result.Metrics,
            result.Overlays);

    private static (string Id, string Name) CreatePublishedResultLayer(IReadOnlyList<ResultEntity> results)
    {
        var firstResult = results[0];
        return firstResult.Id switch
        {
            C3DHeightDeviationResultEntityId => ("layer.result.c3d-height-deviation", "Published C3D Height Deviation"),
            LazTwoPointResultEntityId => ("layer.result.laz-two-point-measurement", "Published LAZ/LAS Two Point Measurement"),
            SyntheticResultEntityId => ("layer.result.synthetic-height-deviation", "Published Synthetic Height Deviation"),
            _ => ($"layer.{firstResult.Id}", firstResult.Name)
        };
    }

    private string ImportedMeshDisplayName() =>
        ImportedMeshFormat == "GLB" ? "Public GLB Mesh" : $"{ImportedMeshFormat} Mesh";

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

    private void RefreshPointCloudColorLegend()
    {
        PointCloudColorLegendVisible = LazSampleVisible
            && SelectedColorMode == "Height"
            && double.IsFinite(lazHeightMinimum)
            && double.IsFinite(lazHeightMaximum)
            && lazHeightMaximum > lazHeightMinimum;
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

    public void SetLazRecipeLoaded(string recipePath, string sourceName, string sourcePath)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Recipe: {Path.GetFileName(recipePath)}\nSource: {sourceName}\nLAZ/LAS acceptance: editable");
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        SetLazSampleSource(sourcePath, sourceName);
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

    public void SetHeightMap(ImageSource imageSource, int sourceWidth, int sourceHeight, int renderedPoints, double min, double max, double mean, int pixelWidth, int pixelHeight)
    {
        HeightMapVisible = true;
        HeightMapImageSource = imageSource;
        HeightMapPixelWidth = pixelWidth;
        HeightMapPixelHeight = pixelHeight;
        HeightMapSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Height map: {sourceWidth} x {sourceHeight} C3D | rendered {renderedPoints:N0} points");
        HeightMapRange = string.Create(
            CultureInfo.InvariantCulture,
            $"Range: min {min:F3}, max {max:F3}, mean {mean:F3} raw-height");
    }

    public void ClearHeightMap()
    {
        HeightMapVisible = false;
        HeightMapImageSource = null;
        HeightMapPixelWidth = 0;
        HeightMapPixelHeight = 0;
        HeightMapSummary = "Height map: not loaded";
        HeightMapRange = "Range: not loaded";
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
        "Fast" => "Fast: up to 25,000 C3D points / 25,000 LAZ/LAS points / 25,000 mesh triangles",
        "Detailed" => "Detailed: up to 140,000 C3D points / 150,000 LAZ/LAS points / 180,000 mesh triangles",
        _ => "Balanced: up to 55,000 C3D points / 50,000 LAZ/LAS points / 60,000 mesh triangles"
    };

    private static IReadOnlyList<SourceEntity> CreateSourceEntities(
        ModelTransform c3DTransform,
        string glbName,
        string glbSourcePath,
        string lazName,
        string lazSourcePath) =>
    [
        new SourceEntity(CubeEntityId, "Generated Unit Cube", EntityKind.Mesh, "unitless", null, ModelTransform.Identity),
        new SourceEntity(PointCloudEntityId, "Generated Point Cloud", EntityKind.PointCloud, "unitless", null, ModelTransform.Identity),
        new SourceEntity(C3DEntityId, "C3D Thickness Sample", EntityKind.HeightGrid, "raw-height", @"3D\Thickness\Ori_20240116_094414.C3D", c3DTransform),
        new SourceEntity(GlbEntityId, glbName, EntityKind.Mesh, "unitless", glbSourcePath, ModelTransform.Identity),
        new SourceEntity(LazEntityId, lazName, EntityKind.PointCloud, "source-units", lazSourcePath, ModelTransform.Identity)
    ];

    private static string FormatModelTransform(ModelTransform transform) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"T({transform.TranslateX:F3}, {transform.TranslateY:F3}, {transform.TranslateZ:F3}) | R({transform.RotateXDegrees:F1}, {transform.RotateYDegrees:F1}, {transform.RotateZDegrees:F1}) | S {transform.Scale:F3}");

    private static bool ModelTransformIsIdentity(ModelTransform transform) =>
        transform.TranslateX == 0.0
        && transform.TranslateY == 0.0
        && transform.TranslateZ == 0.0
        && transform.RotateXDegrees == 0.0
        && transform.RotateYDegrees == 0.0
        && transform.RotateZDegrees == 0.0
        && transform.Scale == 1.0;

    private void RefreshRecipeSummary()
    {
        RecipeSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Recipe: {recipeFileName}\nSource: {RecipeSourceName}\nTolerance: {RecipePeakTolerance:F3} {RecipeSourceUnit}");
    }

    private void RefreshRecipeParameterSummary()
    {
        RecipeParameterSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Transform T({RecipeTransformTranslateX:F3}, {RecipeTransformTranslateY:F3}, {RecipeTransformTranslateZ:F3}) R({RecipeTransformRotateXDegrees:F1}, {RecipeTransformRotateYDegrees:F1}, {RecipeTransformRotateZDegrees:F1}) S {RecipeTransformScale:F3}\nROI {RecipeRoiMode}: L({RecipeRoiLeftCenterX:F3}, {RecipeRoiLeftCenterZ:F3}) R({RecipeRoiRightCenterX:F3}, {RecipeRoiRightCenterZ:F3})");
    }

    private static string FormatVector(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

    private static double CoerceFinite(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    private void NotifyRecipeTransformProperties()
    {
        OnPropertyChanged(nameof(RecipeTransformTranslateX));
        OnPropertyChanged(nameof(RecipeTransformTranslateY));
        OnPropertyChanged(nameof(RecipeTransformTranslateZ));
        OnPropertyChanged(nameof(RecipeTransformRotateXDegrees));
        OnPropertyChanged(nameof(RecipeTransformRotateYDegrees));
        OnPropertyChanged(nameof(RecipeTransformRotateZDegrees));
        OnPropertyChanged(nameof(RecipeTransformScale));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
