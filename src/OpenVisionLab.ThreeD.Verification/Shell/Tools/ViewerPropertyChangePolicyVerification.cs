using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class ViewerPropertyChangePolicyVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer property-change policy verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var none = ViewerViewModelPropertyChangePolicy.Classify(null);
        Check("null property is ignored", none == ViewerViewModelPropertyChangeEffects.None, Describe(none));

        var legend = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.DeviationLegendVisible));
        Check(
            "deviation legend maps only to legend refresh",
            legend == ViewerViewModelPropertyChangeEffects.UpdateDeviationLegendVisibility,
            Describe(legend));

        var pointCloudLegend = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.PointCloudColorLegendVisible));
        Check(
            "point-cloud legend maps only to legend refresh",
            pointCloudLegend == ViewerViewModelPropertyChangeEffects.UpdatePointCloudColorLegendVisibility,
            Describe(pointCloudLegend));

        var display = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.PointSize));
        Check(
            "display property requests render",
            display == ViewerViewModelPropertyChangeEffects.Render,
            Describe(display));

        var peakTolerance = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.RecipePeakTolerance));
        Check(
            "peak tolerance refreshes the rule and render",
            Has(peakTolerance, ViewerViewModelPropertyChangeEffects.Render)
            && Has(peakTolerance, ViewerViewModelPropertyChangeEffects.ApplyHeightDeviationRule)
            && !Has(peakTolerance, ViewerViewModelPropertyChangeEffects.ReloadRenderDensity),
            Describe(peakTolerance));

        var transform = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.C3DModelTransform));
        Check(
            "model transform covers ROI, reference plane, flatness, and render",
            Has(transform, ViewerViewModelPropertyChangeEffects.Render)
            && Has(transform, ViewerViewModelPropertyChangeEffects.UpdateRoiStepMeasurement)
            && Has(transform, ViewerViewModelPropertyChangeEffects.FitReferencePlane)
            && Has(transform, ViewerViewModelPropertyChangeEffects.InvalidatePlaneFlatness),
            Describe(transform));

        var density = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.SelectedRenderDensity));
        Check(
            "render density selects the reload branch",
            density == ViewerViewModelPropertyChangeEffects.ReloadRenderDensity,
            Describe(density));

        var roi = ViewerViewModelPropertyChangePolicy.Classify(
            nameof(MainWindowViewModel.RecipeRoiLeftCenterX));
        Check(
            "recipe ROI edit selects parameter synchronization",
            Has(roi, ViewerViewModelPropertyChangeEffects.SyncRecipeRoiParameters)
            && Has(roi, ViewerViewModelPropertyChangeEffects.Render)
            && !Has(roi, ViewerViewModelPropertyChangeEffects.ReloadRenderDensity),
            Describe(roi));

        Check(
            "recipe ROI classification covers both sides",
            ViewerViewModelPropertyChangePolicy.IsRecipeRoiEditProperty(
                nameof(MainWindowViewModel.RecipeRoiRightHalfDepth))
            && !ViewerViewModelPropertyChangePolicy.IsRecipeRoiEditProperty(
                nameof(MainWindowViewModel.RecipePeakTolerance)),
            "left/right=True|unrelated=False");

        Check(
            "host map preserves public C3D visibility name",
            ViewerHostStatePropertyMap.Map("C3DSampleVisible")
                == nameof(ViewerHostState.C3DSampleVisible),
            ViewerHostStatePropertyMap.Map("C3DSampleVisible") ?? "(null)");
        Check(
            "host map exposes imported mesh visibility",
            ViewerHostStatePropertyMap.Map("GlbSampleVisible")
                == nameof(ViewerHostState.GlbSampleVisible),
            ViewerHostStatePropertyMap.Map("GlbSampleVisible") ?? "(null)");
        Check(
            "host map exposes point-cloud visibility",
            ViewerHostStatePropertyMap.Map("LazSampleVisible")
                == nameof(ViewerHostState.LazSampleVisible),
            ViewerHostStatePropertyMap.Map("LazSampleVisible") ?? "(null)");
        Check(
            "host map preserves selection name",
            ViewerHostStatePropertyMap.Map("SelectedSelectionMode")
                == nameof(ViewerHostState.SelectionMode),
            ViewerHostStatePropertyMap.Map("SelectedSelectionMode") ?? "(null)");
        Check(
            "host map groups selection summary",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SelectionSummary))
                == nameof(ViewerHostState.Selection),
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SelectionSummary)) ?? "(null)");
        Check(
            "host map exposes selection overlay visibility",
            ViewerHostStatePropertyMap.Map("SelectionOverlayVisible")
                == nameof(ViewerHostState.SelectionOverlayVisible),
            ViewerHostStatePropertyMap.Map("SelectionOverlayVisible") ?? "(null)");
        Check(
            "host map exposes Nominal/Actual input presence",
            ViewerHostStatePropertyMap.Map("NominalActualInput")
                == nameof(ViewerHostState.HasNominalActualInput),
            ViewerHostStatePropertyMap.Map("NominalActualInput") ?? "(null)");
        Check(
            "host map exposes nested Nominal/Actual state",
            ViewerHostStatePropertyMap.Map("NominalActual.State")
                == nameof(ViewerHostState.NominalActualState),
            ViewerHostStatePropertyMap.Map("NominalActual.State") ?? "(null)");
        Check(
            "host map groups Nominal/Actual evidence display state",
            ViewerHostStatePropertyMap.Map("NominalActual.InputsReady")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.EvidenceSummary")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.StateSummary")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.DirectionSummary")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.CurrentDisplaySamplingSummary")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.NextPreviewSamplingSummary")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.DisplaySamplingChangePending")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.ProgressPercent")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.DistributionVisible")
                == nameof(ViewerHostState.NominalActualDisplay)
            && ViewerHostStatePropertyMap.Map("NominalActual.DistributionSummary")
                == nameof(ViewerHostState.NominalActualDisplay),
            $"inputs={ViewerHostStatePropertyMap.Map("NominalActual.InputsReady") ?? "(null)"}|evidence={ViewerHostStatePropertyMap.Map("NominalActual.EvidenceSummary") ?? "(null)"}|state={ViewerHostStatePropertyMap.Map("NominalActual.StateSummary") ?? "(null)"}|progress={ViewerHostStatePropertyMap.Map("NominalActual.ProgressPercent") ?? "(null)"}|distribution={ViewerHostStatePropertyMap.Map("NominalActual.DistributionSummary") ?? "(null)"}");
        Check(
            "host map groups C3D thickness evidence display state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ThicknessVisible))
                == nameof(ViewerHostState.ThicknessEvidence)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ThicknessValidSampleCount))
                == nameof(ViewerHostState.ThicknessEvidence),
            $"visible={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ThicknessVisible)) ?? "(null)"}|samples={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ThicknessValidSampleCount)) ?? "(null)"}");
        Check(
            "host map groups height profile state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ProfileSummary))
                == nameof(ViewerHostState.Profile)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ProfileLinkedCursorMarkerTop))
                == nameof(ViewerHostState.Profile),
            $"summary={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ProfileSummary)) ?? "(null)"}|marker={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ProfileLinkedCursorMarkerTop)) ?? "(null)"}");
        Check(
            "host map groups section profile state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SectionProfileSummary))
                == nameof(ViewerHostState.SectionProfile)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SectionProfilePathData))
                == nameof(ViewerHostState.SectionProfile),
            $"summary={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SectionProfileSummary)) ?? "(null)"}|path={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SectionProfilePathData)) ?? "(null)"}");
        Check(
            "host map groups source sample display state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.LazSampleName))
                == nameof(ViewerHostState.SourceSamples)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.LazSampleSummary))
                == nameof(ViewerHostState.SourceSamples)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.GlbSampleName))
                == nameof(ViewerHostState.SourceSamples)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.GlbSampleSummary))
                == nameof(ViewerHostState.SourceSamples),
            $"lazName={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.LazSampleName)) ?? "(null)"}|lazSummary={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.LazSampleSummary)) ?? "(null)"}|meshName={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.GlbSampleName)) ?? "(null)"}|meshSummary={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.GlbSampleSummary)) ?? "(null)"}");
        Check(
            "host map groups scene display state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SceneContractSummary))
                == nameof(ViewerHostState.SceneDisplay)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.TransformSummary))
                == nameof(ViewerHostState.SceneDisplay)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.AlignmentSummary))
                == nameof(ViewerHostState.SceneDisplay)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.CoordinateMappingSummary))
                == nameof(ViewerHostState.SceneDisplay)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.C3DSamplePointCount))
                == nameof(ViewerHostState.SceneDisplay)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.LazSamplingSummary))
                == nameof(ViewerHostState.SceneDisplay),
            "scene=" + (ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SceneContractSummary)) ?? "(null)")
            + "|transform=" + (ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.TransformSummary)) ?? "(null)")
            + "|alignment=" + (ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.AlignmentSummary)) ?? "(null)")
            + "|mapping=" + (ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.CoordinateMappingSummary)) ?? "(null)")
            + "|c3d=" + (ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.C3DSamplePointCount)) ?? "(null)")
            + "|laz=" + (ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.LazSamplingSummary)) ?? "(null)"));
        Check(
            "host map groups presentation state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.HudDetailsVisible))
                == nameof(ViewerHostState.Presentation)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.C3DHeightColorRangeRevision))
                == nameof(ViewerHostState.Presentation),
            $"hud={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.HudDetailsVisible)) ?? "(null)"}|range={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.C3DHeightColorRangeRevision)) ?? "(null)"}");
        Check(
            "host map groups ViewerPresentationBar display state",
            ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SelectedColorMode))
                == nameof(ViewerHostState.Presentation)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ResultOverlayVisible))
                == nameof(ViewerHostState.Presentation)
            && ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.MeasurementVisible))
                == nameof(ViewerHostState.Presentation),
            $"color={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.SelectedColorMode)) ?? "(null)"}|result={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.ResultOverlayVisible)) ?? "(null)"}|measurement={ViewerHostStatePropertyMap.Map(nameof(MainWindowViewModel.MeasurementVisible)) ?? "(null)"}");
        Check(
            "unknown host notification is suppressed",
            ViewerHostStatePropertyMap.Map("UnknownProperty") is null,
            "mapped=(null)");

        var hostViewModel = new MainWindowViewModel
        {
            C3DSampleVisible = true,
            SelectedEntity = "C3D Height Map",
            SelectedSelectionMode = "Point",
            PickCoordinate = "pick",
            MeasurementSummary = "measure",
            ResultSummary = "result",
            RecipeSummary = "recipe",
            ViewerStatus = "status",
            HudDetailsVisible = true,
            GlbSampleVisible = true,
            LazSampleVisible = true,
            SelectionOverlayVisible = true,
            SelectedColorMode = "Height"
        };
        var sourceState = new ViewerHostSourceState("c3d-path", "mesh-path", "GLB");
        hostViewModel.ViewerStatus = "status";
        var hostNotifications = new List<ViewerHostStateChangedEventArgs>();
        var hostDisposed = false;
        var hostStateCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(hostViewModel, sourceState),
            () => hostDisposed,
            hostNotifications.Add);
        var hostState = hostStateCoordinator.Current;
        Check(
            "host state coordinator snapshots ViewModel display state",
            hostState.C3DSampleVisible
            && hostState.ActiveEntity == "C3D Height Map"
            && hostState.SelectionMode == "Point"
            && hostState.PickCoordinate == "pick"
            && hostState.MeasurementSummary == "measure"
            && hostState.ResultSummary == "result"
            && hostState.RecipeSummary == "recipe"
            && hostState.ViewerStatus == "status"
            && hostState.GlbSampleVisible
            && hostState.LazSampleVisible
            && hostState.SelectionOverlayVisible
            && hostState.Presentation.HudDetailsVisible
            && !hostState.Presentation.C3DHeightDistributionVisible
            && hostState.Presentation.C3DHeightColorRangeAuto
            && hostState.Presentation.C3DHeightDistributionSourceSha256 == "not loaded"
            && hostState.Presentation.AvailableColorMaps.Count > 0
            && hostState.Presentation.SelectedColorMap == "Height"
            && !hostState.Presentation.ResultOverlayVisible
            && hostState.Presentation.MeasurementVisible
            && hostState.NominalActualState == ViewerNominalActualState.NoInputs
            && !hostState.HasNominalActualInput
            && hostState.NominalActualDisplay == ViewerHostNominalActualDisplayState.Empty
            && hostState.ThicknessEvidence == ViewerHostThicknessEvidenceState.Empty
            && hostState.Profile == ViewerHostProfileState.Empty
            && hostState.SectionProfile == ViewerHostSectionProfileState.Empty
            && hostState.SourceSamples == ViewerHostSourceSamplesState.Empty
            && hostState.SceneDisplay.SceneContractSummary == hostViewModel.SceneContractSummary
            && hostState.SceneDisplay.TransformSummary == hostViewModel.TransformSummary
            && hostState.SceneDisplay.AlignmentSummary == hostViewModel.AlignmentSummary
            && hostState.SceneDisplay.CoordinateMappingSummary == hostViewModel.CoordinateMappingSummary
            && hostState.SceneDisplay.C3DSamplePointCount == hostViewModel.C3DSamplePointCount
            && hostState.SceneDisplay.LazSamplingSummary == hostViewModel.LazSamplingSummary
            && hostState.Selection.Summary == hostViewModel.SelectionSummary
            && hostState.CoordinateFrameSummary == hostViewModel.CoordinateFrameSummary,
            $"activeEntity={hostState.ActiveEntity}|selectionMode={hostState.SelectionMode}|viewerStatus={hostState.ViewerStatus}|hud={hostState.Presentation.HudDetailsVisible}|distribution={hostState.Presentation.C3DHeightDistributionVisible}|auto={hostState.Presentation.C3DHeightColorRangeAuto}|source={hostState.Presentation.C3DHeightDistributionSourceSha256}|maps={hostState.Presentation.AvailableColorMaps.Count}|selectedMap={hostState.Presentation.SelectedColorMap}|result={hostState.Presentation.ResultOverlayVisible}|measurement={hostState.Presentation.MeasurementVisible}|nominalActualState={hostState.NominalActualState}|hasNominalActualInput={hostState.HasNominalActualInput}|profile={hostState.Profile.Visible}|frame={hostState.CoordinateFrameSummary}");
        hostViewModel.SetProfileStart(4, 7, new System.Numerics.Vector3(1.0f, 2.0f, 3.0f), 105.0f);
        var profileState = ViewerHostStateProjection.From(hostViewModel, sourceState).Profile;
        Check(
            "host state projects profile display snapshot",
            profileState.Visible
            && profileState.ValidSampleCount == 1
            && profileState.MissingSampleCount == 0
            && profileState.EndpointSummary.Contains("P2: not set", StringComparison.Ordinal)
            && profileState.PathData == "M 0,30 L 240,30",
            $"visible={profileState.Visible}|valid={profileState.ValidSampleCount}|missing={profileState.MissingSampleCount}|endpoint={profileState.EndpointSummary}|path={profileState.PathData}");
        hostViewModel.SetSectionProfile("section", 2, 3, 0.5, 2.5, 1.5, "M 0,30 L 240,12");
        var sectionProfileState = ViewerHostStateProjection.From(hostViewModel, sourceState).SectionProfile;
        Check(
            "host state projects section profile display snapshot",
            sectionProfileState.Visible
            && sectionProfileState.SampleCount == 3
            && sectionProfileState.Summary.Contains("section", StringComparison.Ordinal)
            && sectionProfileState.Range.Contains("min 0.500", StringComparison.Ordinal)
            && sectionProfileState.PathData == "M 0,30 L 240,12",
            $"visible={sectionProfileState.Visible}|samples={sectionProfileState.SampleCount}|summary={sectionProfileState.Summary}|range={sectionProfileState.Range}|path={sectionProfileState.PathData}");
        hostViewModel.SetLazSampleSource("laz-path", "Sample LAZ");
        hostViewModel.LazSampleSummary = "Point cloud summary";
        hostViewModel.SetGlbSampleSource("mesh-path", "Sample GLB", "GLB");
        hostViewModel.GlbSampleSummary = "Mesh summary";
        var sourceSamplesState = ViewerHostStateProjection.From(hostViewModel, sourceState).SourceSamples;
        Check(
            "host state projects source sample display snapshot",
            sourceSamplesState.PointCloudName == "Sample LAZ"
            && sourceSamplesState.PointCloudSummary == "Point cloud summary"
            && sourceSamplesState.MeshName == "Sample GLB"
            && sourceSamplesState.MeshSummary == "Mesh summary",
            $"pointCloud={sourceSamplesState.PointCloudName}|pointSummary={sourceSamplesState.PointCloudSummary}|mesh={sourceSamplesState.MeshName}|meshSummary={sourceSamplesState.MeshSummary}");
        var sourceSampleNotifications = new List<ViewerHostStateChangedEventArgs>();
        var sourceSampleCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(hostViewModel, sourceState),
            () => false,
            sourceSampleNotifications.Add);
        sourceSampleCoordinator.Notify(nameof(MainWindowViewModel.LazSampleSummary));
        Check(
            "host state coordinator publishes source sample display changes",
            sourceSampleNotifications.Count == 1
            && sourceSampleNotifications[0].PropertyName == nameof(ViewerHostState.SourceSamples)
            && sourceSampleNotifications[0].State.SourceSamples == sourceSamplesState,
            $"count={sourceSampleNotifications.Count}|property={sourceSampleNotifications.FirstOrDefault()?.PropertyName ?? "(none)"}|mesh={sourceSampleNotifications.FirstOrDefault()?.State.SourceSamples.MeshName ?? "(none)"}");
        var sceneDisplayState = ViewerHostStateProjection.From(hostViewModel, sourceState).SceneDisplay;
        Check(
            "host state projects scene display snapshot",
            sceneDisplayState.SceneContractSummary == hostViewModel.SceneContractSummary
            && sceneDisplayState.TransformSummary == hostViewModel.TransformSummary
            && sceneDisplayState.AlignmentSummary == hostViewModel.AlignmentSummary
            && sceneDisplayState.CoordinateMappingSummary == hostViewModel.CoordinateMappingSummary
            && sceneDisplayState.C3DSamplePointCount == hostViewModel.C3DSamplePointCount
            && sceneDisplayState.LazSamplingSummary == hostViewModel.LazSamplingSummary,
            $"scene={sceneDisplayState.SceneContractSummary}|transform={sceneDisplayState.TransformSummary}|alignment={sceneDisplayState.AlignmentSummary}|mapping={sceneDisplayState.CoordinateMappingSummary}|c3d={sceneDisplayState.C3DSamplePointCount}|laz={sceneDisplayState.LazSamplingSummary}");
        var sceneDisplayNotifications = new List<ViewerHostStateChangedEventArgs>();
        var sceneDisplayCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(hostViewModel, sourceState),
            () => false,
            sceneDisplayNotifications.Add);
        sceneDisplayCoordinator.Notify(nameof(MainWindowViewModel.TransformSummary));
        Check(
            "host state coordinator publishes scene display changes",
            sceneDisplayNotifications.Count == 1
            && sceneDisplayNotifications[0].PropertyName == nameof(ViewerHostState.SceneDisplay)
            && sceneDisplayNotifications[0].State.SceneDisplay == sceneDisplayState,
            $"count={sceneDisplayNotifications.Count}|property={sceneDisplayNotifications.FirstOrDefault()?.PropertyName ?? "(none)"}|transform={sceneDisplayNotifications.FirstOrDefault()?.State.SceneDisplay.TransformSummary ?? "(none)"}");
        Check(
            "host state projects source and selection DTOs",
            hostState.Sources == sourceState
            && hostState.Selection == new ViewerHostSelectionState(
                "C3D Height Map",
                "Point",
                "pick",
                true)
            {
                Summary = hostViewModel.SelectionSummary
            },
            $"c3d={hostState.Sources.CurrentC3DSourcePath}|viewerOnly={hostState.Sources.CurrentViewerOnlySourcePath}|selection={hostState.Selection.SelectionMode}|summary={hostState.Selection.Summary}");
        hostStateCoordinator.Notify(nameof(MainWindowViewModel.ViewerStatus));
        Check(
            "host state coordinator publishes known property changes",
            hostNotifications.Count == 1
            && hostNotifications[0].PropertyName == nameof(ViewerHostState.ViewerStatus)
            && hostNotifications[0].State.ViewerStatus == "status",
            $"count={hostNotifications.Count}|property={hostNotifications.FirstOrDefault()?.PropertyName ?? "(none)"}");
        hostViewModel.SetThicknessPreview(
            new C3DThicknessEvaluation(
                new ToolResult(
                    "C3D Thickness",
                    ResultStatus.Pass,
                    "pass",
                    TimeSpan.Zero,
                    [new Metric("Mean", MetricKind.Length, 1.25, "model", ResultStatus.Pass)],
                    []),
                HasMeasurement: true,
                PackageResultStatus: "Pass",
                PackageErrorCode: "None",
                Roi: new C3DGridRoi(1, 2, 3, 4),
                Mean: 1.25,
                Minimum: 0.50,
                Maximum: 2.00,
                Range: 1.50,
                ValidSampleCount: 8,
                BelowLowerLimitCount: 1,
                AboveUpperLimitCount: 0));
        var thicknessEvidenceState = ViewerHostStateProjection.From(hostViewModel, sourceState).ThicknessEvidence;
        Check(
            "host state projects C3D thickness evidence display snapshot",
            thicknessEvidenceState.Visible
            && thicknessEvidenceState.Summary.Contains("mean 1.250", StringComparison.Ordinal)
            && thicknessEvidenceState.Details.Contains("row 1, column 2", StringComparison.Ordinal)
            && Math.Abs(thicknessEvidenceState.Mean - 1.25) < 0.001
            && Math.Abs(thicknessEvidenceState.MinimumMeasured - 0.50) < 0.001
            && Math.Abs(thicknessEvidenceState.MaximumMeasured - 2.00) < 0.001
            && Math.Abs(thicknessEvidenceState.Range - 1.50) < 0.001
            && thicknessEvidenceState.ValidSampleCount == 8,
            $"visible={thicknessEvidenceState.Visible}|mean={thicknessEvidenceState.Mean:G6}|minimum={thicknessEvidenceState.MinimumMeasured:G6}|maximum={thicknessEvidenceState.MaximumMeasured:G6}|range={thicknessEvidenceState.Range:G6}|samples={thicknessEvidenceState.ValidSampleCount}");
        var thicknessNotifications = new List<ViewerHostStateChangedEventArgs>();
        var thicknessCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(hostViewModel, sourceState),
            () => false,
            thicknessNotifications.Add);
        thicknessCoordinator.Notify(nameof(MainWindowViewModel.ThicknessSummary));
        Check(
            "host state coordinator publishes C3D thickness evidence changes",
            thicknessNotifications.Count == 1
            && thicknessNotifications[0].PropertyName == nameof(ViewerHostState.ThicknessEvidence)
            && thicknessNotifications[0].State.ThicknessEvidence == thicknessEvidenceState,
            $"count={thicknessNotifications.Count}|property={thicknessNotifications.FirstOrDefault()?.PropertyName ?? "(none)"}|samples={thicknessNotifications.FirstOrDefault()?.State.ThicknessEvidence.ValidSampleCount.ToString() ?? "(none)"}");
        var sectionNotifications = new List<ViewerHostStateChangedEventArgs>();
        var sectionCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(hostViewModel, sourceState),
            () => false,
            sectionNotifications.Add);
        sectionCoordinator.Notify(nameof(MainWindowViewModel.SectionProfileSummary));
        Check(
            "host state coordinator publishes section profile changes",
            sectionNotifications.Count == 1
            && sectionNotifications[0].PropertyName == nameof(ViewerHostState.SectionProfile)
            && sectionNotifications[0].State.SectionProfile == sectionProfileState,
            $"count={sectionNotifications.Count}|property={sectionNotifications.FirstOrDefault()?.PropertyName ?? "(none)"}|samples={sectionNotifications.FirstOrDefault()?.State.SectionProfile.SampleCount.ToString() ?? "(none)"}");
        hostViewModel.ConfigureNominalActualComparison(
            new NominalActualComparisonInput(
                "step.nominal-actual",
                new NominalActualFileIdentity(
                    "source.actual",
                    "Actual",
                    "actual.stl",
                    100,
                    new string('A', 64)),
                new NominalActualFileIdentity(
                    "source.nominal",
                    "Nominal",
                    "nominal.stl",
                    200,
                    new string('B', 64)),
                new NominalActualFileIdentity(
                    "source.query",
                    "Query",
                    "query.ply",
                    300,
                    new string('C', 64)),
                "model",
                "frame.fixture",
                "alignment.fixture",
                -0.3,
                0.3));
        var configuredHostState = ViewerHostStateProjection.From(hostViewModel, sourceState);
        Check(
            "host state projects Nominal/Actual lifecycle",
            configuredHostState.HasNominalActualInput
            && configuredHostState.NominalActualState == ViewerNominalActualState.InputsReady
            && configuredHostState.NominalActualDisplay.InputsReady
            && configuredHostState.NominalActualDisplay.EvidenceSummary == "No comparison evidence."
            && configuredHostState.NominalActualDisplay.StateSummary == "Status: Inputs ready"
            && configuredHostState.NominalActualDisplay.DirectionSummary == "Direction: Actual to Nominal"
            && configuredHostState.NominalActualDisplay.CurrentDisplaySamplingSummary == "Current display: no comparison result"
            && configuredHostState.NominalActualDisplay.NextPreviewSamplingSummary == "Next Preview: Balanced | up to 60,000 points"
            && !configuredHostState.NominalActualDisplay.DisplaySamplingChangePending
            && configuredHostState.NominalActualDisplay.ProgressPercent == 0.0
            && !configuredHostState.NominalActualDisplay.DistributionVisible
            && configuredHostState.NominalActualDisplay.DistributionSummary == "Deviation distribution: not available",
            $"state={configuredHostState.NominalActualState}|hasInput={configuredHostState.HasNominalActualInput}|inputsReady={configuredHostState.NominalActualDisplay.InputsReady}|evidence={configuredHostState.NominalActualDisplay.EvidenceSummary}|summary={configuredHostState.NominalActualDisplay.StateSummary}|direction={configuredHostState.NominalActualDisplay.DirectionSummary}|current={configuredHostState.NominalActualDisplay.CurrentDisplaySamplingSummary}|next={configuredHostState.NominalActualDisplay.NextPreviewSamplingSummary}|pending={configuredHostState.NominalActualDisplay.DisplaySamplingChangePending}|progress={configuredHostState.NominalActualDisplay.ProgressPercent:G6}|distributionVisible={configuredHostState.NominalActualDisplay.DistributionVisible}|distribution={configuredHostState.NominalActualDisplay.DistributionSummary}");
        hostStateCoordinator.Notify("NominalActualInput");
        hostStateCoordinator.Notify("NominalActual.State");
        hostStateCoordinator.Notify("NominalActual.InputsReady");
        hostStateCoordinator.Notify("NominalActual.EvidenceSummary");
        Check(
            "host state coordinator publishes Nominal/Actual changes",
            hostNotifications.Count == 5
            && hostNotifications[1].PropertyName == nameof(ViewerHostState.HasNominalActualInput)
            && hostNotifications[1].State.HasNominalActualInput
            && hostNotifications[2].PropertyName == nameof(ViewerHostState.NominalActualState)
            && hostNotifications[2].State.NominalActualState == ViewerNominalActualState.InputsReady
            && hostNotifications[3].PropertyName == nameof(ViewerHostState.NominalActualDisplay)
            && hostNotifications[3].State.NominalActualDisplay.InputsReady
            && hostNotifications[4].PropertyName == nameof(ViewerHostState.NominalActualDisplay)
            && hostNotifications[4].State.NominalActualDisplay.EvidenceSummary == "No comparison evidence.",
            $"count={hostNotifications.Count}|input={hostNotifications.ElementAtOrDefault(1)?.PropertyName ?? "(none)"}|state={hostNotifications.ElementAtOrDefault(2)?.State.NominalActualState.ToString() ?? "(none)"}|display={hostNotifications.ElementAtOrDefault(3)?.PropertyName ?? "(none)"},{hostNotifications.ElementAtOrDefault(4)?.PropertyName ?? "(none)"}");
        var nominalActualDisplayNotifications = new List<ViewerHostStateChangedEventArgs>();
        var nominalActualDisplayCoordinator = new ViewerHostStateCoordinator(
            () => ViewerHostStateProjection.From(hostViewModel, sourceState),
            () => false,
            nominalActualDisplayNotifications.Add);
        nominalActualDisplayCoordinator.Notify("NominalActual.StateSummary");
        nominalActualDisplayCoordinator.Notify("NominalActual.ProgressPercent");
        nominalActualDisplayCoordinator.Notify("NominalActual.DistributionVisible");
        nominalActualDisplayCoordinator.Notify("NominalActual.DistributionSummary");
        Check(
            "host state coordinator publishes Nominal/Actual display changes",
            nominalActualDisplayNotifications.Count == 4
            && nominalActualDisplayNotifications.All(notification =>
                notification.PropertyName == nameof(ViewerHostState.NominalActualDisplay))
            && nominalActualDisplayNotifications[0].State.NominalActualDisplay.StateSummary == "Status: Inputs ready"
            && nominalActualDisplayNotifications[1].State.NominalActualDisplay.ProgressPercent == 0.0
            && !nominalActualDisplayNotifications[2].State.NominalActualDisplay.DistributionVisible
            && nominalActualDisplayNotifications[3].State.NominalActualDisplay.DistributionSummary == "Deviation distribution: not available",
            $"count={nominalActualDisplayNotifications.Count}|properties={string.Join(",", nominalActualDisplayNotifications.Select(notification => notification.PropertyName))}|summary={nominalActualDisplayNotifications.FirstOrDefault()?.State.NominalActualDisplay.StateSummary ?? "(none)"}|progress={nominalActualDisplayNotifications.ElementAtOrDefault(1)?.State.NominalActualDisplay.ProgressPercent.ToString("G6") ?? "(none)"}|distributionVisible={nominalActualDisplayNotifications.ElementAtOrDefault(2)?.State.NominalActualDisplay.DistributionVisible.ToString() ?? "(none)"}|distribution={nominalActualDisplayNotifications.ElementAtOrDefault(3)?.State.NominalActualDisplay.DistributionSummary ?? "(none)"}");
        hostStateCoordinator.Notify("UnknownProperty");
        Check(
            "host state coordinator suppresses unknown properties",
            hostNotifications.Count == 5,
            $"count={hostNotifications.Count}");
        hostDisposed = true;
        hostStateCoordinator.Notify(nameof(MainWindowViewModel.ViewerStatus));
        Check(
            "host state coordinator suppresses disposed notifications",
            hostNotifications.Count == 5,
            $"count={hostNotifications.Count}");
        Check(
            "presentation bar adapter forwards Host state and mutations",
            VerifyPresentationBarAdapter(),
            "Host-backed adapter forwards two-way display changes and releases its event subscription");
        Check(
            "height profile adapter projects Host state and releases subscription",
            VerifyHeightProfileAdapter(out var profileAdapterDetail),
            profileAdapterDetail);

        summary = $"Viewer property-change policy verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static bool Has(
        ViewerViewModelPropertyChangeEffects effects,
        ViewerViewModelPropertyChangeEffects expected) =>
        (effects & expected) != 0;

    private static string Describe(ViewerViewModelPropertyChangeEffects effects) =>
        effects.ToString();

    private static bool VerifyPresentationBarAdapter()
    {
        var host = new PresentationBarFakeHost();
        using var adapter = new ViewerPresentationBarViewModel(host);
        var propertyChangedCount = 0;
        adapter.PropertyChanged += (_, _) => propertyChangedCount++;

        var initialCount = propertyChangedCount;
        adapter.SelectedColorMap = "Solid";
        adapter.SelectionOverlayVisible = true;
        adapter.ResultOverlayVisible = true;
        adapter.MeasurementVisible = true;
        adapter.DecreaseHeightMinimumCommand.Execute(null);
        adapter.IncreaseHeightMinimumCommand.Execute(null);
        adapter.DecreaseHeightMaximumCommand.Execute(null);
        adapter.IncreaseHeightMaximumCommand.Execute(null);
        adapter.ResetHeightColorRangeCommand.Execute(null);
        var forwarded = host.SelectedColorMap == "Solid"
            && host.SelectionOverlayVisible
            && host.ResultOverlayVisible
            && host.MeasurementVisible
            && host.MinimumDirections.SequenceEqual([-1, 1])
            && host.MaximumDirections.SequenceEqual([-1, 1])
            && host.ResetCount == 1
            && propertyChangedCount > initialCount;

        var countBeforeDispose = propertyChangedCount;
        adapter.Dispose();
        host.RaiseStateChanged(nameof(ViewerHostState.Presentation));
        return forwarded
            && !adapter.DecreaseHeightMinimumCommand.CanExecute(null)
            && propertyChangedCount == countBeforeDispose;
    }

    private static bool VerifyHeightProfileAdapter(out string detail)
    {
        var host = new PresentationBarFakeHost();
        using var adapter = new HeightProfileViewModel(host);
        var propertyChangedCount = 0;
        adapter.PropertyChanged += (_, _) => propertyChangedCount++;
        var initialCount = propertyChangedCount;
        var initialProjection = !adapter.ProfileVisible
            && adapter.ProfileSummary == host.HostState.Profile.Summary
            && adapter.ProfilePathData == host.HostState.Profile.PathData;

        host.SetProfile(host.HostState.Profile with
        {
            Visible = true,
            Summary = "P1-P2 profile | distance 2.000 viewer",
            PathData = "M 0,30 L 240,10",
            LinkedCursorVisible = true,
            LinkedCursorX = 120.0,
            LinkedCursorY = 27.0
        });
        var updatedProjection = adapter.ProfileVisible
            && adapter.ProfileSummary.Contains("P1-P2", StringComparison.Ordinal)
            && adapter.ProfilePathData == "M 0,30 L 240,10"
            && adapter.ProfileLinkedCursorVisible
            && Math.Abs(adapter.ProfileLinkedCursorX - 120.0) < 0.001
            && Math.Abs(adapter.ProfileLinkedCursorY - 27.0) < 0.001
            && propertyChangedCount > initialCount;
        var updatedSummary = adapter.ProfileSummary;
        var updatedPath = adapter.ProfilePathData;

        var countBeforeDispose = propertyChangedCount;
        adapter.Dispose();
        host.SetProfile(host.HostState.Profile with { Summary = "ignored after dispose" });
        var postDisposeChangeCount = propertyChangedCount;
        var passed = initialProjection
            && updatedProjection
            && postDisposeChangeCount == countBeforeDispose;
        detail = $"initial={initialProjection};updated={updatedProjection};changes={postDisposeChangeCount};beforeDispose={countBeforeDispose};summary={updatedSummary};path={updatedPath}";
        return passed;
    }

    private sealed class PresentationBarFakeHost : IOpenVisionThreeDViewerHost
    {
        private ViewerHostState state = new(
            C3DSampleVisible: true,
            ActiveEntity: "C3D",
            SelectionMode: "Point",
            PickCoordinate: "",
            MeasurementSummary: "",
            ResultSummary: "",
            RecipeSummary: "",
            ViewerStatus: "Ready",
            CoordinateFrameSummary: "Scene")
        {
            SelectionOverlayVisible = false,
            Profile = new ViewerHostProfileState(
                Visible: false,
                Summary: "Profile: choose P1 and P2 on the C3D height grid.",
                EndpointSummary: "P1: not set | P2: not set",
                Range: "Height range: pending",
                PathData: "M 0,30 L 240,30",
                ValidSampleCount: 0,
                MissingSampleCount: 0,
                LinkedCursorVisible: false,
                LinkedCursorX: 0.0,
                LinkedCursorY: 0.0,
                LinkedCursorMarkerLeft: 0.0,
                LinkedCursorMarkerTop: 0.0,
                LinkedCursorSummary: "Linked cursor: unavailable until P1–P2 trace is ready."),
            Presentation = new ViewerHostPresentationState(
                HudDetailsVisible: true,
                C3DHeightDistributionVisible: true,
                C3DHeightDistributionSourceSha256: "fixture",
                C3DHeightColorMinimumRaw: 0,
                C3DHeightColorMaximumRaw: 1,
                C3DHeightColorRangeAuto: false,
                C3DHeightColorRangeRevision: 1,
                C3DHeightColorRangeSummary: "fixture range")
            {
                AvailableColorMaps = ["Height", "Solid"],
                SelectedColorMap = "Height",
                CanSelectColorMap = true,
                DiagnosticChannelOptions = [],
                CanSelectDiagnosticChannel = false,
                DiagnosticChannelSummary = "fixture diagnostics",
                ResultOverlayVisible = false,
                MeasurementVisible = false
            }
        };

        public string HostApiVersion => "1.0-test";

        public ViewerHostState HostState => state;

        public event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged;

        public string SelectedColorMap => state.Presentation.SelectedColorMap;

        public bool SelectionOverlayVisible => state.SelectionOverlayVisible;

        public bool ResultOverlayVisible => state.Presentation.ResultOverlayVisible;

        public bool MeasurementVisible => state.Presentation.MeasurementVisible;

        public void SetProfile(ViewerHostProfileState profile)
        {
            state = state with { Profile = profile };
            RaiseStateChanged(nameof(ViewerHostState.Profile));
        }

        public int MinimumShift { get; private set; }

        public List<int> MinimumDirections { get; } = [];

        public int MaximumShift { get; private set; }

        public List<int> MaximumDirections { get; } = [];

        public int ResetCount { get; private set; }

        public bool LoadC3DSource(string path) => true;

        public Task<bool> LoadC3DSourceAsync(string path, CancellationToken cancellationToken = default, IProgress<double>? progress = null) => Task.FromResult(true);

        public Task<bool> LoadViewerOnlySourceAsync(string path, CancellationToken cancellationToken = default, IProgress<double>? progress = null) => Task.FromResult(true);

        public bool TryGetCurrentC3DSourceBinding(string path, out ToolRecipeSelectionSourceBinding binding)
        {
            binding = new ToolRecipeSelectionSourceBinding("C3D", "fixture", 1, 1);
            return true;
        }

        public ViewerCameraState CaptureCameraState() =>
            new(0, 0, 1, 0, 0, 0, ViewerProjectionMode.Perspective, 1);

        public bool TryApplyCameraState(ViewerCameraState cameraState) => cameraState.IsValid;

        public bool TrySetSelectionMode(string selectionMode) => true;

        public bool TrySetSelectionOverlayVisible(bool visible)
        {
            state = state with { SelectionOverlayVisible = visible };
            RaiseStateChanged(nameof(ViewerHostState.SelectionOverlayVisible));
            return true;
        }

        public bool TrySetHudDetailsVisible(bool visible) => true;

        public bool TrySetC3DSampleVisible(bool visible) => true;

        public bool TrySetSelectedColorMap(string colorMap)
        {
            state = state with
            {
                Presentation = state.Presentation with { SelectedColorMap = colorMap }
            };
            RaiseStateChanged(nameof(ViewerHostState.Presentation));
            return true;
        }

        public bool TrySetSelectedDiagnosticChannel(ViewerDiagnosticChannelOption? channel) => true;

        public bool TrySetResultOverlayVisible(bool visible)
        {
            state = state with
            {
                Presentation = state.Presentation with { ResultOverlayVisible = visible }
            };
            RaiseStateChanged(nameof(ViewerHostState.Presentation));
            return true;
        }

        public bool TrySetMeasurementVisible(bool visible)
        {
            state = state with
            {
                Presentation = state.Presentation with { MeasurementVisible = visible }
            };
            RaiseStateChanged(nameof(ViewerHostState.Presentation));
            return true;
        }

        public bool TrySetC3DHeightColorMinimumRaw(double value) => true;

        public bool TrySetC3DHeightColorMaximumRaw(double value) => true;

        public bool TryShiftC3DHeightColorMinimum(int direction)
        {
            MinimumDirections.Add(direction);
            MinimumShift += direction;
            RaiseStateChanged(nameof(ViewerHostState.Presentation));
            return direction != 0;
        }

        public bool TryShiftC3DHeightColorMaximum(int direction)
        {
            MaximumDirections.Add(direction);
            MaximumShift += direction;
            RaiseStateChanged(nameof(ViewerHostState.Presentation));
            return direction != 0;
        }

        public bool TryResetC3DHeightColorRange()
        {
            ResetCount++;
            RaiseStateChanged(nameof(ViewerHostState.Presentation));
            return true;
        }

        public bool TryApplyLinkedC3DHeightColorRange(double minimum, double maximum) => true;

        public void FitAll()
        {
        }

        public void FitSelection()
        {
        }

        public void ResetView()
        {
        }

        public bool SaveRecipe(string path) => true;

        public void RaiseStateChanged(string? propertyName) =>
            HostStateChanged?.Invoke(this, new ViewerHostStateChangedEventArgs(state, propertyName));
    }
}
