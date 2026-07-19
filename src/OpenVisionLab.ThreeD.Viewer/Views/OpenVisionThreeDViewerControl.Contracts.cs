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
    private void WriteSceneContracts(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var displaySettings = viewModel.Display.EffectiveSettings;
        var c3dRenderProxyForContract = c3dSample is null ? null : GetC3DRenderProxy();
        var geometryRenderBridge = displaySettings.Source switch
        {
            ViewerDisplaySourceKind.C3DHeightGrid => "SharpGLC3DSampledGrid",
            ViewerDisplaySourceKind.ImportedTriangleMesh => "SharpGLImportedTriangleMesh",
            _ => "Pending"
        };
        var lines = new List<string>
        {
            viewModel.SceneContractSummary,
            "SourceEntities"
        };

        lines.AddRange(viewModel.SourceEntities.Select(InspectionContractText.FormatSourceEntity));
        lines.Add("EntityLayers");
        lines.AddRange(viewModel.EntityLayers.Select(InspectionContractText.FormatEntityLayer));
        lines.Add(InspectionContractText.PreviewToolResultMarker);
        var result = viewModel.PreviewToolResult;
        lines.Add(InspectionContractText.FormatToolResult(result));
        lines.Add(InspectionContractText.PreviewMetricsMarker);
        lines.AddRange(result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.PreviewOverlaysMarker);
        lines.AddRange(result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        lines.Add("ColorScaleLegend");
        lines.Add($"DeviationLegend|visible={viewModel.DeviationLegendVisible}|{viewModel.DeviationLegendStatus}|{viewModel.DeviationLegendPeak}|{viewModel.DeviationLegendTolerance}|{viewModel.DeviationLegendScale}");
        lines.Add($"PointCloudColorLegend|visible={viewModel.PointCloudColorLegendVisible}|{viewModel.PointCloudColorLegendLow}|{viewModel.PointCloudColorLegendHigh}|{viewModel.PointCloudColorLegendScale}");
        lines.Add($"C3DHeightDistribution|visible={viewModel.C3DHeightDistributionVisible}|overlayVisible={C3DHeightDistributionPanel.Visibility == Visibility.Visible}|sourceSha256={CleanContractText(viewModel.C3DHeightDistributionSourceSha256)}|colorMapId={CleanContractText(viewModel.SelectedColorMode)}|bins={viewModel.C3DHeightDistributionBinCount}|binSum={viewModel.C3DHeightDistributionBinSum}|valid={viewModel.C3DHeightDistributionValidSampleCount}|missing={viewModel.C3DHeightDistributionMissingSampleCount}|minRaw={FormatPreciseContractNumber(viewModel.C3DHeightDistributionMinimumRaw)}|meanRaw={FormatPreciseContractNumber(viewModel.C3DHeightDistributionMeanRaw)}|maxRaw={FormatPreciseContractNumber(viewModel.C3DHeightDistributionMaximumRaw)}|peakLowerRaw={FormatPreciseContractNumber(viewModel.C3DHeightDistributionPeakLowerRaw)}|peakUpperRaw={FormatPreciseContractNumber(viewModel.C3DHeightDistributionPeakUpperRaw)}|peakFraction={FormatPreciseContractNumber(viewModel.C3DHeightDistributionPeakFraction)}|constant={viewModel.C3DHeightDistributionIsConstant}|unit=raw-height|range=full-source-auto|displayOnly=True|physicalScale=Unverified|hitTest={C3DHeightDistributionPanel.IsHitTestVisible}");
        lines.Add("RenderControls");
        lines.Add($"PointSize|value={viewModel.PointSize.ToString("F1", CultureInfo.InvariantCulture)}");
        lines.Add($"ColorMode|mode={CleanContractText(viewModel.SelectedColorMode)}");
        lines.Add($"DisplaySettings|sourceId={displaySettings.Source}|activeSource={CleanContractText(viewModel.Display.ActiveSource)}|geometryStyleId={displaySettings.GeometryStyle}|geometryStyle={CleanContractText(viewModel.Display.EffectiveGeometryStyle)}|geometrySelectable={viewModel.Display.CanSelectGeometryStyle}|availableGeometry={CleanContractText(string.Join(",", viewModel.Display.AvailableGeometryStyles))}|colorMapId={displaySettings.ColorMap}|colorMap={CleanContractText(viewModel.Display.EffectiveColorMap)}|colorSelectable={viewModel.Display.CanSelectColorMap}|availableColorMaps={CleanContractText(string.Join(",", viewModel.Display.AvailableColorMaps))}|fallbackApplied={viewModel.Display.FallbackApplied}|fallback={CleanContractText(viewModel.Display.FallbackSummary)}|displayOnly={displaySettings.IsDisplayOnly}|renderBridge=ColorCompatibilitySnapshot|geometryRenderBridge={geometryRenderBridge}");
        lines.Add($"MeasurementOverlay|visible={viewModel.MeasurementVisible}");
        lines.Add($"RenderDensity|mode={viewModel.SelectedRenderDensity}|maxRenderedPoints={viewModel.C3DMaxRenderedPoints}|maxLazSampledPoints={viewModel.LazMaxSampledPoints}|maxImportedMeshTriangles={viewModel.ImportedMeshMaxRenderedTriangles}|maxNominalActualDisplaySamples={viewModel.NominalActualMaxDisplaySamples}|renderedC3DPoints={c3dSample?.Points.Length ?? 0}|sampledLazPoints={lazPointCloud?.SampledPoints.Length ?? 0}|renderedImportedMeshTriangles={GetImportedMeshRenderedTriangleCount()}|summary={viewModel.RenderDensitySummary}");
        lines.Add(c3dSample is null
            ? "C3DMap|loaded=False|displayFrame=NotAvailable|physicalScale=Unverified"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"C3DMap|loaded=True|displayFrame=right-handed-y-up|x=column|y=raw-height|z=row|modelUnit=unitless|rawUnit=raw-height|horizontalSpan={C3DHeightGrid.ViewerHorizontalSpan:R}|horizontalScale={c3dSample.HorizontalScale:R}|heightScale={C3DHeightGrid.ViewerHeightScale:R}|heightCenterRaw={c3dSample.Mean:R}|stride={c3dSample.PointStride}|physicalScale=Unverified"));
        lines.Add(c3dRenderProxyForContract is null
            ? "C3DRenderProxy|loaded=False"
            : $"C3DRenderProxy|loaded=True|points={c3dRenderProxyForContract.Points.Length}|triangles={c3dRenderProxyForContract.TriangleCount}|edges={c3dRenderProxyForContract.EdgeCount}|gridEdges={c3dRenderProxyForContract.GridEdgeCount}|surfaceEdges={c3dRenderProxyForContract.SurfaceEdgeCount}|surfaceEdgeInterval={C3DHeightGridRenderProxy.SurfaceEdgeSampleInterval}|topology=sampled-grid-neighbors|effectiveStyle={displaySettings.GeometryStyle}|renderCache=OpenGLDisplayList|renderCacheReady={c3dDisplayListId != 0}|displayOnly=True|measurementGeometry=SourceCells");
        lines.Add($"PointCloudPerformance|loadMs={FormatContractNumber(viewModel.LazLoadMilliseconds)}|samplePercent={FormatContractNumber(viewModel.LazSamplePercent)}|sampleStride={viewModel.LazSampleStride}|summary={CleanContractText(viewModel.LazSamplingSummary)}");
        lines.Add("ImportedMesh");
        lines.Add(CreateImportedMeshContractLine());
        lines.Add("ImportedPointCloud");
        lines.Add(CreateLazContractLine());
        lines.Add($"ViewerInternalHud|detailsVisible={viewModel.HudDetailsVisible}|importedMeshDetailsVisible={viewModel.ImportedMeshHudDetailsVisible}|lazDetailsVisible={viewModel.LazHudDetailsVisible}");
        var nominalActual = viewModel.NominalActual;
        var nominalActualInput = viewModel.NominalActualInput;
        var nominalActualResult = nominalActual.PreviewResult;
        lines.Add("NominalActualComparison");
        lines.Add($"NominalActualViewModel|type={nameof(NominalActualComparisonViewModel)}|state={nominalActual.State}|inputsReady={nominalActual.InputsReady}|canEdit={nominalActual.CanEdit}|canPreview={nominalActual.CanPreview}|canCancel={nominalActual.CanCancel}|canPublish={nominalActual.CanPublish}|actualVisible={nominalActual.ActualVisible}|nominalVisible={nominalActual.NominalVisible}|hudVisible={nominalActual.HudVisible}|legendVisible={nominalActual.LegendVisible}|distributionVisible={nominalActual.DistributionVisible}|typedResult={nominalActual.PreviewResult is not null}|lowerTolerance={FormatContractNumber(nominalActual.LowerTolerance)}|upperTolerance={FormatContractNumber(nominalActual.UpperTolerance)}|inputFingerprint={CleanContractText(nominalActual.CurrentInputFingerprint)}|previewFingerprint={CleanContractText(nominalActual.CompletedPreviewFingerprint)}|publishedFingerprint={CleanContractText(nominalActual.PublishedPreviewFingerprint)}");
        lines.Add($"NominalActualSummary|state={CleanContractText(nominalActual.StateSummary)}|validation={CleanContractText(nominalActual.ValidationSummary)}|actual={CleanContractText(nominalActual.ActualSourceSummary)}|nominal={CleanContractText(nominalActual.NominalSourceSummary)}|query={CleanContractText(nominalActual.QuerySourceSummary)}|frame={CleanContractText(nominalActual.FrameSummary)}|alignment={CleanContractText(nominalActual.AlignmentSummary)}|result={CleanContractText(nominalActual.ResultSummary)}|evidence={CleanContractText(nominalActual.EvidenceSummary)}");
        lines.Add(nominalActualInput is null
            ? "NominalActualInput|configured=False"
            : $"NominalActualInput|configured=True|step={CleanContractText(nominalActualInput.StepId)}|direction={NominalActualComparisonInput.Direction}|unit={CleanContractText(nominalActualInput.Unit)}|frame={CleanContractText(nominalActualInput.FrameId)}|alignment={CleanContractText(nominalActualInput.AlignmentId)}|actualId={CleanContractText(nominalActualInput.ActualSource.Id)}|actualBytes={nominalActualInput.ActualSource.ByteLength}|actualSha256={nominalActualInput.ActualSource.Sha256}|nominalId={CleanContractText(nominalActualInput.NominalSource.Id)}|nominalBytes={nominalActualInput.NominalSource.ByteLength}|nominalSha256={nominalActualInput.NominalSource.Sha256}|queryId={CleanContractText(nominalActualInput.QuerySource.Id)}|queryBytes={nominalActualInput.QuerySource.ByteLength}|querySha256={nominalActualInput.QuerySource.Sha256}|sourceFingerprint={nominalActualInput.SourceFingerprint}");
        lines.Add(nominalActualResult is null
            ? "NominalActualResult|available=False"
            : $"NominalActualResult|available=True|status={nominalActualResult.Status}|message={CleanContractText(nominalActualResult.Message)}|executionFingerprint={nominalActualResult.Input.ExecutionFingerprint}|points={nominalActualResult.ComparedPointCount}|below={nominalActualResult.BelowLowerToleranceCount}|within={nominalActualResult.WithinToleranceCount}|above={nominalActualResult.AboveUpperToleranceCount}|directSign={nominalActualResult.DirectSignResolvedCount}|robustRecovered={nominalActualResult.RobustSignRecoveredCount}|indexMs={FormatContractNumber(nominalActualResult.IndexElapsed.TotalMilliseconds)}|calculationMs={FormatContractNumber(nominalActualResult.CalculationElapsed.TotalMilliseconds)}|totalMs={FormatContractNumber(nominalActualResult.TotalElapsed.TotalMilliseconds)}|fullQuery=True");
        if (nominalActualResult is not null)
        {
            lines.Add($"NominalActualSignedStatistics|count={nominalActualResult.Signed.Count}|min={FormatPreciseContractNumber(nominalActualResult.Signed.Minimum)}|max={FormatPreciseContractNumber(nominalActualResult.Signed.Maximum)}|mean={FormatPreciseContractNumber(nominalActualResult.Signed.Mean)}|stdPopulation={FormatPreciseContractNumber(nominalActualResult.Signed.StandardDeviationPopulation)}|rms={FormatPreciseContractNumber(nominalActualResult.Signed.RootMeanSquare)}|unit={CleanContractText(nominalActualResult.Input.Unit)}");
            lines.Add($"NominalActualUnsignedStatistics|count={nominalActualResult.Unsigned.Count}|min={FormatPreciseContractNumber(nominalActualResult.Unsigned.Minimum)}|max={FormatPreciseContractNumber(nominalActualResult.Unsigned.Maximum)}|mean={FormatPreciseContractNumber(nominalActualResult.Unsigned.Mean)}|stdPopulation={FormatPreciseContractNumber(nominalActualResult.Unsigned.StandardDeviationPopulation)}|rms={FormatPreciseContractNumber(nominalActualResult.Unsigned.RootMeanSquare)}|unit={CleanContractText(nominalActualResult.Input.Unit)}");
            lines.Add($"NominalActualDisplaySampling|samples={nominalActualResult.DisplaySamples.Count}|stride={nominalActualResult.DisplaySampleStride}|measuredPoints={nominalActualResult.ComparedPointCount}|metricsIndependent=True|colorScale=zero-centred-blue-white-red");
        }
        lines.Add($"NominalActualDisplayDensityState|current={CleanContractText(nominalActual.CurrentDisplayDensity)}|currentBudget={nominalActual.CurrentDisplaySampleBudget}|next={CleanContractText(nominalActual.NextPreviewDisplayDensity)}|nextBudget={nominalActual.NextPreviewDisplaySampleBudget}|changePending={nominalActual.DisplaySamplingChangePending}|explicitPreviewRequired={nominalActual.DisplaySamplingChangePending}|currentSummary={CleanContractText(nominalActual.CurrentDisplaySamplingSummary)}|nextSummary={CleanContractText(nominalActual.NextPreviewSamplingSummary)}");
        lines.Add(nominalActual.SelectedDeviation is not { } selectedDeviation
            ? "NominalActualSelectedDeviation|selected=False"
            : $"NominalActualSelectedDeviation|selected=True|queryIndex={selectedDeviation.QueryPointIndex}|position={FormatVector(selectedDeviation.Position)}|signedDeviation={FormatPreciseContractNumber(selectedDeviation.SignedDeviation)}|unsignedDeviation={FormatPreciseContractNumber(selectedDeviation.UnsignedDeviation)}|nominalTriangleIndex={selectedDeviation.NominalTriangleIndex}|closestNominal={FormatVector(selectedDeviation.ClosestNominalPoint)}|toleranceStatus={CleanContractText(nominalActual.SelectedDeviationToleranceStatus)}|robustSignRecovered={selectedDeviation.RobustSignRecovered}|actualId={CleanContractText(nominalActualResult!.Input.ActualSource.Id)}|queryId={CleanContractText(nominalActualResult.Input.QuerySource.Id)}|unit={CleanContractText(nominalActualResult.Input.Unit)}");
        lines.Add($"ViewerStatus|summary={CleanContractText(viewModel.ViewerStatus)}|smokeExitCode={smokeExitCode}");
        lines.Add($"CoordinateFrame|visible=True|summary={CleanContractText(viewModel.CoordinateFrameSummary)}");
        lines.Add($"Camera|yaw={FormatContractNumber(viewModel.YawDegrees)}|pitch={FormatContractNumber(viewModel.PitchDegrees)}|distance={FormatContractNumber(viewModel.CameraDistance)}|target={FormatVector(GetCameraTarget())}|summary={CleanContractText(viewModel.BottomStatus)}");
        var cameraEye = GetCameraPosition();
        var cameraTarget = GetCameraTarget();
        lines.Add($"OrientationTriad|visible={OrientationTriadPanel.Visibility == Visibility.Visible}|frame=viewer-display/right-handed-y-up|cameraAware=True|x={FormatVector2(CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitX, cameraEye, cameraTarget))}|y={FormatVector2(CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitY, cameraEye, cameraTarget))}|z={FormatVector2(CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitZ, cameraEye, cameraTarget))}|hitTest={OrientationTriadPanel.IsHitTestVisible}");
        lines.Add(CreatePointerInputRegressionContractLine());
        lines.Add($"SelectionMode|value={viewModel.SelectedSelectionMode}");
        lines.Add($"PickCoordinate|value={CleanContractText(viewModel.PickCoordinate)}");
        lines.Add(CreateImportedMeshPickContractLine());
        lines.Add(CreateImportedMeshSurfaceOverlayContractLine());
        lines.Add(CreateLazPickContractLine());
        lines.Add($"Performance|fps={FormatContractNumber(viewModel.ViewportFps)}|drawMs={FormatContractNumber(viewModel.ViewportDrawMilliseconds)}|summary={CleanContractText(viewModel.PerformanceSummary)}");
        lines.Add($"PerformanceSmoke|configured={smokeRenderFrameCount > 0}|requestedFrames={smokeRenderFrameCount}|completedFrames={smokeRenderFramesCompleted}|finite={double.IsFinite(viewModel.ViewportFps) && double.IsFinite(viewModel.ViewportDrawMilliseconds)}|measurement=SharpGL.DoRender");
        lines.Add("TransformAlignment");
        lines.Add($"C3DTransform|entity={MainWindowViewModel.C3DEntityId}|tx={FormatContractNumber(viewModel.C3DModelTransform.TranslateX)}|ty={FormatContractNumber(viewModel.C3DModelTransform.TranslateY)}|tz={FormatContractNumber(viewModel.C3DModelTransform.TranslateZ)}|rx={FormatContractNumber(viewModel.C3DModelTransform.RotateXDegrees)}|ry={FormatContractNumber(viewModel.C3DModelTransform.RotateYDegrees)}|rz={FormatContractNumber(viewModel.C3DModelTransform.RotateZDegrees)}|scale={FormatContractNumber(viewModel.C3DModelTransform.Scale)}|summary={CleanContractText(viewModel.TransformSummary)}");
        lines.Add($"Alignment|summary={CleanContractText(viewModel.AlignmentSummary)}|mapping={CleanContractText(viewModel.CoordinateMappingSummary)}");
        lines.Add($"AlignmentWorkflow|summary={CleanContractText(viewModel.AlignmentWorkflowSummary)}");
        lines.Add("TwoPointMeasurement");
        lines.Add($"TwoPoint|visible={viewModel.TwoPointMeasurementVisible}|distance={FormatContractNumber(viewModel.TwoPointDistance)}|dx={FormatContractNumber(viewModel.TwoPointDeltaX)}|dy={FormatContractNumber(viewModel.TwoPointDeltaY)}|dz={FormatContractNumber(viewModel.TwoPointDeltaZ)}|heightDeltaRaw={FormatContractNumber(viewModel.TwoPointRawHeightDelta)}|summary={CleanContractText(viewModel.TwoPointMeasurementDetails)}");
        lines.Add($"HeightProfile|visible={viewModel.ProfileVisible}|validSamples={viewModel.ProfileValidSampleCount}|missingSamples={viewModel.ProfileMissingSampleCount}|summary={CleanContractText(viewModel.ProfileSummary)}|endpoints={CleanContractText(viewModel.ProfileEndpointSummary)}|range={CleanContractText(viewModel.ProfileRange)}|displayOnly=True|explicitPreviewRunUnchanged=True");
        lines.Add($"LAZAcceptance|visible={viewModel.LazSampleVisible}|summary={CleanContractText(viewModel.LazTwoPointAcceptanceSummary)}");
        lines.Add($"LAZAcceptanceParameters|visible={viewModel.LazSampleVisible}|expectedDistance={FormatContractNumber(viewModel.LazTwoPointExpectedDistance)}|distanceTolerance={FormatContractNumber(viewModel.LazTwoPointDistanceTolerance)}|expectedHeightDelta={FormatContractNumber(viewModel.LazTwoPointExpectedHeightDelta)}|heightDeltaTolerance={FormatContractNumber(viewModel.LazTwoPointHeightDeltaTolerance)}");
        lines.Add("ThicknessInspection");
        if (viewModel.ThicknessConfigured)
        {
            var thicknessStep = viewModel.CreateThicknessRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                thicknessStep.Id,
                C3DThicknessRule.ToolName,
                thicknessStep.SourceEntityId,
                thicknessStep.RoiReferenceId,
                thicknessStep.Enabled)));
            lines.Add($"C3DThicknessStep|configured=True|id={thicknessStep.Id}|source={thicknessStep.SourceEntityId}|reference={thicknessStep.RoiReferenceId}|row={thicknessStep.Roi.Row}|column={thicknessStep.Roi.Column}|rowCount={thicknessStep.Roi.RowCount}|columnCount={thicknessStep.Roi.ColumnCount}|minimum={FormatContractNumber(thicknessStep.Acceptance.MinimumThickness)}|maximum={FormatContractNumber(thicknessStep.Acceptance.MaximumThickness)}|minimumValidSamples={thicknessStep.MinimumValidSamples}|unit={CleanContractText(thicknessStep.Unit)}|frame={CleanContractText(thicknessStep.FrameId)}|enabled={thicknessStep.Enabled}");
        }
        else
        {
            lines.Add("C3DThicknessStep|configured=False");
        }

        lines.Add($"C3DThickness|visible={viewModel.ThicknessVisible}|status={(viewModel.ThicknessVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|mean={FormatContractNumber(viewModel.ThicknessMean)}|minimum={FormatContractNumber(viewModel.ThicknessMinimumMeasured)}|maximum={FormatContractNumber(viewModel.ThicknessMaximumMeasured)}|range={FormatContractNumber(viewModel.ThicknessRange)}|validSamples={viewModel.ThicknessValidSampleCount}|below={viewModel.ThicknessBelowLowerLimitCount}|above={viewModel.ThicknessAboveUpperLimitCount}|summary={CleanContractText(viewModel.ThicknessSummary)}|details={CleanContractText(viewModel.ThicknessDetails)}");
        lines.Add("PointPairDimensionsInspection");
        var pointPairStep = viewModel.CreatePointPairDimensionsRecipeStep();
        if (pointPairStep is not null)
        {
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                pointPairStep.Id,
                PointPairDimensionsRule.ToolName,
                pointPairStep.SourceEntityId,
                $"{pointPairStep.First.Id},{pointPairStep.Second.Id}",
                pointPairStep.Enabled)));
            lines.Add($"PointPairDimensionsStep|configured=True|id={pointPairStep.Id}|source={pointPairStep.SourceEntityId}|first={pointPairStep.First.Id}@({pointPairStep.First.Row},{pointPairStep.First.Column})|second={pointPairStep.Second.Id}@({pointPairStep.Second.Row},{pointPairStep.Second.Column})|enabled={pointPairStep.Enabled}|expectedDistance={FormatContractNumber(pointPairStep.Acceptance.ExpectedDistance)}|distanceTolerance={FormatContractNumber(pointPairStep.Acceptance.DistanceTolerance)}|expectedWidth={FormatContractNumber(pointPairStep.Acceptance.ExpectedWidth)}|widthTolerance={FormatContractNumber(pointPairStep.Acceptance.WidthTolerance)}|expectedAngle={FormatContractNumber(pointPairStep.Acceptance.ExpectedElevationAngleDegrees)}|angleTolerance={FormatContractNumber(pointPairStep.Acceptance.ElevationAngleToleranceDegrees)}|unit={pointPairStep.Unit}");
        }
        else
        {
            lines.Add($"PointPairDimensionsStep|configured=False|references={viewModel.HasPointPairReferences}");
        }

        lines.Add($"PointPairDimensions|visible={viewModel.PointPairDimensionsVisible}|status={(viewModel.PointPairDimensionsVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|distance={FormatContractNumber(viewModel.PointPairDistance)}|width={FormatContractNumber(viewModel.PointPairWidth)}|angleDegrees={FormatContractNumber(viewModel.PointPairAngleDegrees)}|summary={CleanContractText(viewModel.PointPairDimensionsSummary)}|details={CleanContractText(viewModel.PointPairDimensionsDetails)}");
        lines.Add("GapFlushInspection");
        if (viewModel.GapFlushConfigured)
        {
            var gapFlushStep = viewModel.CreateGapFlushRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                gapFlushStep.Id,
                GapFlushRule.ToolName,
                gapFlushStep.SourceEntityId,
                $"{gapFlushStep.LeftReferenceId},{gapFlushStep.RightReferenceId}",
                gapFlushStep.Enabled)));
            lines.Add($"GapFlushStep|configured=True|id={gapFlushStep.Id}|source={gapFlushStep.SourceEntityId}|leftReference={gapFlushStep.LeftReferenceId}|rightReference={gapFlushStep.RightReferenceId}|left={FormatContractRegion(gapFlushStep.LeftRegion)}|right={FormatContractRegion(gapFlushStep.RightRegion)}|expectedGap={FormatContractNumber(gapFlushStep.Acceptance.ExpectedGap)}|gapTolerance={FormatContractNumber(gapFlushStep.Acceptance.GapTolerance)}|expectedFlush={FormatContractNumber(gapFlushStep.Acceptance.ExpectedFlush)}|flushTolerance={FormatContractNumber(gapFlushStep.Acceptance.FlushTolerance)}|gapUnit={gapFlushStep.GapUnit}|flushUnit={gapFlushStep.FlushUnit}|maxSampledPoints={gapFlushStep.MaxSampledPoints}|enabled={gapFlushStep.Enabled}");
        }
        else
        {
            lines.Add("GapFlushStep|configured=False");
        }

        lines.Add($"GapFlush|visible={viewModel.GapFlushVisible}|status={(viewModel.GapFlushVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|gap={FormatContractNumber(viewModel.GapFlushGap)}|flush={FormatContractNumber(viewModel.GapFlushFlush)}|modelFlush={FormatContractNumber(viewModel.GapFlushModelFlush)}|leftCount={viewModel.GapFlushLeftPointCount}|rightCount={viewModel.GapFlushRightPointCount}|summary={CleanContractText(viewModel.GapFlushSummary)}|details={CleanContractText(viewModel.GapFlushDetails)}");
        lines.Add("VolumeInspection");
        if (viewModel.VolumeConfigured)
        {
            var volumeStep = viewModel.CreateVolumeRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(volumeStep.Id, VolumeRule.ToolName, volumeStep.SourceEntityId, $"{volumeStep.ReferenceId},{volumeStep.MeasurementId}", volumeStep.Enabled)));
            lines.Add($"VolumeStep|configured=True|id={volumeStep.Id}|source={volumeStep.SourceEntityId}|reference={volumeStep.ReferenceId}|measurement={volumeStep.MeasurementId}|referenceRegion={FormatContractRegion(volumeStep.ReferenceRegion)}|measurementRegion={FormatContractRegion(volumeStep.MeasurementRegion)}|expectedNet={FormatContractNumber(volumeStep.ExpectedNetVolume)}|tolerance={FormatContractNumber(volumeStep.Tolerance)}|unit={volumeStep.Unit}|maxSampledPoints={volumeStep.MaxSampledPoints}|enabled={volumeStep.Enabled}");
        }
        else lines.Add("VolumeStep|configured=False");
        lines.Add($"Volume|visible={viewModel.VolumeVisible}|status={(viewModel.VolumeVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|above={FormatContractNumber(viewModel.VolumeAbove)}|below={FormatContractNumber(viewModel.VolumeBelow)}|net={FormatContractNumber(viewModel.VolumeNet)}|referenceSamples={viewModel.VolumeReferenceSampleCount}|measurementSamples={viewModel.VolumeMeasurementSampleCount}|summary={CleanContractText(viewModel.VolumeSummary)}|details={CleanContractText(viewModel.VolumeDetails)}");
        lines.Add("CrossSectionInspection");
        if (viewModel.CrossSectionConfigured)
        {
            var crossSectionStep = viewModel.CreateCrossSectionRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(crossSectionStep.Id, CrossSectionDimensionsRule.ToolName, crossSectionStep.SourceEntityId, crossSectionStep.ReferenceId, crossSectionStep.Enabled)));
            lines.Add($"CrossSectionStep|configured=True|id={crossSectionStep.Id}|source={crossSectionStep.SourceEntityId}|reference={crossSectionStep.ReferenceId}|row={crossSectionStep.Row}|startColumn={crossSectionStep.StartColumn}|endColumn={crossSectionStep.EndColumn}|expectedWidth={FormatContractNumber(crossSectionStep.ExpectedWidth)}|widthTolerance={FormatContractNumber(crossSectionStep.WidthTolerance)}|expectedHeightRange={FormatContractNumber(crossSectionStep.ExpectedHeightRange)}|heightTolerance={FormatContractNumber(crossSectionStep.HeightTolerance)}|widthUnit={crossSectionStep.WidthUnit}|heightUnit={crossSectionStep.HeightUnit}|enabled={crossSectionStep.Enabled}");
        }
        else lines.Add("CrossSectionStep|configured=False");
        lines.Add($"CrossSection|visible={viewModel.CrossSectionVisible}|status={(viewModel.CrossSectionVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|width={FormatContractNumber(viewModel.CrossSectionWidth)}|heightRange={FormatContractNumber(viewModel.CrossSectionHeightRange)}|rawMinimum={FormatContractNumber(viewModel.CrossSectionRawMinimum)}|rawMaximum={FormatContractNumber(viewModel.CrossSectionRawMaximum)}|validSamples={viewModel.CrossSectionValidSampleCount}|summary={CleanContractText(viewModel.CrossSectionSummary)}|details={CleanContractText(viewModel.CrossSectionDetails)}");
        lines.Add("PlaneReferenceMeasurement");
        lines.Add($"PlaneReference|visible={viewModel.PlaneReferenceMeasurementVisible}|fit=least-squares-height-field|sampleBudget={PlaneFitMaxSampledPoints}|samples={viewModel.PlaneReferenceSampleCount}|normal=({FormatContractNumber(viewModel.PlaneReferenceNormalX)},{FormatContractNumber(viewModel.PlaneReferenceNormalY)},{FormatContractNumber(viewModel.PlaneReferenceNormalZ)})|rms={FormatContractNumber(viewModel.PlaneReferenceFitRms)}|signedDistance={FormatContractNumber(viewModel.PlaneReferenceSignedDistance)}|absoluteDistance={FormatContractNumber(viewModel.PlaneReferenceAbsoluteDistance)}|referenceY={FormatContractNumber(viewModel.PlaneReferenceY)}|targetY={FormatContractNumber(viewModel.PlaneReferenceTargetY)}|rawHeightDelta={FormatContractNumber(viewModel.PlaneReferenceRawHeightDelta)}|summary={CleanContractText(viewModel.PlaneReferenceMeasurementDetails)}");
        lines.Add("PlaneFlatnessInspection");
        if (viewModel.PlaneFlatnessConfigured)
        {
            var flatnessStep = viewModel.CreatePlaneFlatnessRecipeStep();
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                flatnessStep.Id,
                PlaneFlatnessRule.ToolName,
                flatnessStep.SourceEntityId,
                flatnessStep.ReferenceId,
                flatnessStep.Enabled)));
            lines.Add($"PlaneFlatnessStep|configured=True|id={flatnessStep.Id}|source={flatnessStep.SourceEntityId}|reference={flatnessStep.ReferenceId}|enabled={flatnessStep.Enabled}|roi={FormatContractRegion(flatnessStep.ReferenceRegion)}|tolerance={FormatContractNumber(flatnessStep.Tolerance)}|unit={flatnessStep.Unit}|maxSampledPoints={flatnessStep.MaxSampledPoints}");
        }
        else
        {
            lines.Add("PlaneFlatnessStep|configured=False");
        }

        lines.Add($"PlaneFlatness|visible={viewModel.PlaneFlatnessVisible}|status={(viewModel.PlaneFlatnessVisible ? viewModel.PreviewToolResult.Status : ResultStatus.NotRun)}|referenceSamples={viewModel.PlaneFlatnessReferenceSampleCount}|measurementSamples={viewModel.PlaneFlatnessMeasurementSampleCount}|minimum={FormatContractNumber(viewModel.PlaneFlatnessMinimumDeviation)}|maximum={FormatContractNumber(viewModel.PlaneFlatnessMaximumDeviation)}|flatness={FormatContractNumber(viewModel.PlaneFlatnessValue)}|rms={FormatContractNumber(viewModel.PlaneFlatnessRms)}|summary={CleanContractText(viewModel.PlaneFlatnessSummary)}");
        lines.Add("RoiStepMeasurement");
        lines.Add($"RoiStep|visible={viewModel.RoiStepMeasurementVisible}|mode={viewModel.RoiStepSelectionMode}|leftCount={viewModel.RoiStepLeftPointCount}|rightCount={viewModel.RoiStepRightPointCount}|leftMeanRaw={FormatContractNumber(viewModel.RoiStepLeftRawMean)}|rightMeanRaw={FormatContractNumber(viewModel.RoiStepRightRawMean)}|heightDeltaRaw={FormatContractNumber(viewModel.RoiStepRawHeightDelta)}|modelDeltaY={FormatContractNumber(viewModel.RoiStepModelHeightDelta)}|summary={CleanContractText(viewModel.RoiStepMeasurementDetails)}|edit={CleanContractText(viewModel.RoiStepEditSummary)}");
        lines.Add("RecipeState");
        if (nominalActualInput is not null)
        {
            lines.Add($"RecipeType|value={NominalActualComparisonRecipe.SupportedRecipeType}|version=1.0");
            lines.Add($"RecipeTolerance|lower={FormatPreciseContractNumber(nominalActual.LowerTolerance)}|upper={FormatPreciseContractNumber(nominalActual.UpperTolerance)}|unit={CleanContractText(nominalActualInput.Unit)}");
            lines.Add($"RecipeSource|actual={CleanContractText(nominalActualInput.ActualSource.Id)}|nominal={CleanContractText(nominalActualInput.NominalSource.Id)}|query={CleanContractText(nominalActualInput.QuerySource.Id)}");
            lines.Add($"RecipeFrame|direction={NominalActualComparisonInput.Direction}|sampling={NominalActualComparisonRecipe.FullQuerySampling}|frame={CleanContractText(nominalActualInput.FrameId)}|alignment={CleanContractText(nominalActualInput.AlignmentId)}");
            lines.Add("RecipeValidation|summary=Validation: OK");
            lines.Add("RecipeParameterSummary|summary=Full-query metrics / display sampling independent");
            lines.Add("RecipeTransform|applicable=False|reason=alignment-contract-owned");
            lines.Add("RecipeRoiStep|configured=False");
        }
        else
        {
            lines.Add($"RecipeTolerance|value={viewModel.RecipePeakTolerance.ToString("F3", CultureInfo.InvariantCulture)}|unit={viewModel.RecipeSourceUnit}");
            lines.Add($"RecipeSource|name={viewModel.RecipeSourceName}|path={viewModel.RecipeSourcePath}");
            lines.Add($"RecipeValidation|summary={CleanContractText(string.IsNullOrWhiteSpace(viewModel.RecipeValidationSummary) ? "Validation: OK" : viewModel.RecipeValidationSummary)}");
            lines.Add($"RecipeParameterSummary|summary={CleanContractText(viewModel.RecipeParameterSummary)}");
            lines.Add($"RecipeTransform|tx={FormatContractNumber(viewModel.C3DModelTransform.TranslateX)}|ty={FormatContractNumber(viewModel.C3DModelTransform.TranslateY)}|tz={FormatContractNumber(viewModel.C3DModelTransform.TranslateZ)}|rx={FormatContractNumber(viewModel.C3DModelTransform.RotateXDegrees)}|ry={FormatContractNumber(viewModel.C3DModelTransform.RotateYDegrees)}|rz={FormatContractNumber(viewModel.C3DModelTransform.RotateZDegrees)}|scale={FormatContractNumber(viewModel.C3DModelTransform.Scale)}");
            lines.Add(CreateCurrentRoiStepRecipe() is { } roiStep
                ? $"RecipeRoiStep|configured=True|mode={roiStep.Mode}|maxSampledPoints={roiStep.MaxSampledPoints}|left={FormatContractRegion(roiStep.Left)}|right={FormatContractRegion(roiStep.Right)}"
                : "RecipeRoiStep|configured=False");
        }
        lines.Add($"RecipeSave|summary={viewModel.RecipeSaveSummary}");
        lines.Add("LinkedViewHeightMap");
        lines.Add($"HeightMap|visible={viewModel.HeightMapVisible}|pixels={viewModel.HeightMapPixelWidth}x{viewModel.HeightMapPixelHeight}|summary={viewModel.HeightMapSummary.Replace('|', '/')}");
        lines.Add($"HeightMapRange|summary={viewModel.HeightMapRange.Replace('|', '/')}");
        lines.Add("LinkedViewProfile");
        lines.Add($"SectionProfile|visible={viewModel.SectionProfileVisible}|samples={viewModel.SectionProfileSampleCount}|summary={viewModel.SectionProfileSummary.Replace('|', '/')}");
        lines.Add($"SectionProfileRange|summary={viewModel.SectionProfileRange.Replace('|', '/')}");
        lines.Add("PublishedResultEntities");
        lines.AddRange(viewModel.ResultEntities.Select(InspectionContractText.FormatResultEntity));
        lines.Add("PublishedMetrics");
        lines.AddRange(viewModel.ResultEntities.SelectMany(entity => entity.Metrics.Select(metric =>
            InspectionContractText.FormatMetric(metric, entity.Id))));
        lines.Add("PublishedOverlays");
        lines.AddRange(viewModel.ResultEntities.SelectMany(entity => entity.Overlays.Select(overlay =>
            InspectionContractText.FormatOverlay(overlay, entity.Id))));

        File.WriteAllLines(path, lines);
    }

    private string FormatC3DPoint(HeightGridPoint point)
    {
        var aligned = TransformC3DPosition(point.Position);
        if (ModelTransformIsIdentity(viewModel.C3DModelTransform))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{CameraMath.FormatPoint(point.Position)} | raw {point.RawValue:F3}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"src {CameraMath.FormatPoint(point.Position)} -> aligned {CameraMath.FormatPoint(aligned)} | raw {point.RawValue:F3}");
    }

    private void SetLazPick(LazPointCloudPoint point, string status)
    {
        selectedLazPoint = point;
        var summary = FormatLazPoint(point);
        viewModel.SelectedEntity = "Public LAZ/LAS Point Cloud";
        viewModel.SelectedSelectionMode = "Point";
        viewModel.PickCoordinate = summary;
        viewModel.SelectionSummary = $"LAZ/LAS point: {summary}";
        viewModel.MeasurementSummary = $"LAZ/LAS point pick: {summary}";
        viewModel.ViewerStatus = status;
    }

    private void SetNominalActualDeviationPick(
        NominalActualDeviationSample sample,
        string status)
    {
        var comparison = viewModel.NominalActual;
        if (!comparison.SelectDeviation(sample))
        {
            viewModel.ViewerStatus = "Nominal/actual pick rejected: Preview sample is stale";
            return;
        }

        selectedImportedMeshPoint = null;
        selectedImportedMeshTriangleIndex = null;
        selectedImportedMeshSurfaceNormal = null;
        selectedLazPoint = null;
        viewModel.SelectedEntity = "Nominal / Actual Deviation Point";
        viewModel.SelectedSelectionMode = "Point";
        viewModel.PickCoordinate = string.Create(
            CultureInfo.InvariantCulture,
            $"query #{sample.QueryPointIndex:N0} | {CameraMath.FormatPoint(sample.Position)} | signed {sample.SignedDeviation:G7} {comparison.PreviewResult!.Input.Unit}");
        viewModel.SelectionSummary = comparison.SelectedDeviationSummary;
        viewModel.MeasurementSummary = comparison.SelectedDeviationDetails;
        viewModel.ViewerStatus = status;
    }

    private void SetImportedMeshPick(
        Vector3 point,
        string status,
        string kind = "mesh point",
        int? triangleIndex = null,
        Vector3? surfaceNormal = null)
    {
        selectedImportedMeshPoint = point;
        selectedImportedMeshPickKind = kind;
        selectedImportedMeshTriangleIndex = triangleIndex;
        selectedImportedMeshSurfaceNormal = surfaceNormal;
        selectedLazPoint = null;
        var summary = FormatImportedMeshPoint(point, kind);
        var format = viewModel.ImportedMeshFormat;
        viewModel.SelectedEntity = format == "GLB" ? "Public GLB Mesh" : $"{format} Mesh";
        viewModel.SelectedSelectionMode = "Point";
        viewModel.PickCoordinate = summary;
        viewModel.SelectionSummary = $"{format} {kind}: {summary}";
        viewModel.MeasurementSummary = $"{format} pick: {summary}";
        viewModel.ViewerStatus = status;
    }

    private Vector3 FindImportedMeshSmokePickTarget()
    {
        var mesh = importedMesh!;
        var center = (mesh.Min + mesh.Max) * 0.5f;
        return mesh.Positions.MinBy(position => Vector3.DistanceSquared(position, center));
    }

    private Vector3 FindImportedMeshSmokeSurfacePickTarget()
    {
        var mesh = importedMesh!;
        var center = (mesh.Min + mesh.Max) * 0.5f;
        var best = FindImportedMeshSmokePickTarget();
        var bestDistanceSquared = Vector3.DistanceSquared(best, center);

        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var firstIndex = mesh.Indices[i];
            var secondIndex = mesh.Indices[i + 1];
            var thirdIndex = mesh.Indices[i + 2];
            if (!ImportedMeshIndexInRange(mesh, firstIndex) || !ImportedMeshIndexInRange(mesh, secondIndex) || !ImportedMeshIndexInRange(mesh, thirdIndex))
            {
                continue;
            }

            var centroid = (mesh.Positions[firstIndex] + mesh.Positions[secondIndex] + mesh.Positions[thirdIndex]) / 3.0f;
            var distanceSquared = Vector3.DistanceSquared(centroid, center);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = centroid;
            }
        }

        return best;
    }

    private (Vector3 First, Vector3 Second) FindImportedMeshSmokeMeasurementPair()
    {
        var mesh = importedMesh!;
        var positions = mesh.Positions;
        var extent = mesh.Max - mesh.Min;
        var axis = extent.X >= extent.Y && extent.X >= extent.Z
            ? 0
            : extent.Y >= extent.Z ? 1 : 2;

        var first = positions[0];
        var second = positions[0];
        var minValue = AxisValue(first, axis);
        var maxValue = minValue;

        foreach (var position in positions)
        {
            var value = AxisValue(position, axis);
            if (value < minValue)
            {
                minValue = value;
                first = position;
            }
            else if (value > maxValue)
            {
                maxValue = value;
                second = position;
            }
        }

        return (first, second);
    }

    private static float AxisValue(Vector3 point, int axis) => axis switch
    {
        0 => point.X,
        1 => point.Y,
        _ => point.Z
    };

    private LazPointCloudPoint FindLazSmokePickTarget()
    {
        var metadata = lazPointCloud!.Metadata;
        var sourceCenter = new Vector3(
            (float)((metadata.MinX + metadata.MaxX) * 0.5),
            (float)((metadata.MinY + metadata.MaxY) * 0.5),
            (float)((metadata.MinZ + metadata.MaxZ) * 0.5));
        var viewerCenter = MapLazPosition(sourceCenter);
        return lazPointCloud.SampledPoints.MinBy(point => Vector3.DistanceSquared(MapLazPosition(point.Position), viewerCenter));
    }

    private string FormatLazPoint(LazPointCloudPoint point)
    {
        var viewer = MapLazPosition(point.Position);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"src {FormatVector(point.Position)} -> viewer {FormatVector(viewer)} | RGB {point.Red},{point.Green},{point.Blue}");
    }

    private static string FormatImportedMeshPoint(Vector3 point, string kind = "mesh point") =>
        string.Create(CultureInfo.InvariantCulture, $"{kind} {FormatVector(point)}");

    private Vector3 TransformC3DPosition(Vector3 sourcePosition) =>
        ApplyModelTransform(sourcePosition, viewModel.C3DModelTransform);

    private static Vector3 ApplyModelTransform(Vector3 sourcePosition, ModelTransform transform)
    {
        var position = sourcePosition * (float)transform.Scale;
        position = Vector3.Transform(position, Matrix4x4.CreateRotationX(ToRadians(transform.RotateXDegrees)));
        position = Vector3.Transform(position, Matrix4x4.CreateRotationY(ToRadians(transform.RotateYDegrees)));
        position = Vector3.Transform(position, Matrix4x4.CreateRotationZ(ToRadians(transform.RotateZDegrees)));
        return position + new Vector3((float)transform.TranslateX, (float)transform.TranslateY, (float)transform.TranslateZ);
    }

    private static float ToRadians(double degrees) => (float)(degrees * Math.PI / 180.0);

    private static bool ModelTransformIsIdentity(ModelTransform transform) =>
        transform.TranslateX == 0.0
        && transform.TranslateY == 0.0
        && transform.TranslateZ == 0.0
        && transform.RotateXDegrees == 0.0
        && transform.RotateYDegrees == 0.0
        && transform.RotateZDegrees == 0.0
        && transform.Scale == 1.0;

    private static string FormatVector(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

    private static string FormatVector2(Vector2 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3})");

    private string CreateImportedMeshContractLine()
    {
        var format = CleanContractText(viewModel.ImportedMeshFormat);
        if (importedMesh is null)
        {
            return $"{format}|loaded=False|source={CleanContractText(viewModel.GlbSampleSourcePath)}|summary={CleanContractText(viewModel.GlbSampleSummary)}";
        }

        return $"{format}|loaded=True|entity={MainWindowViewModel.GlbEntityId}|visible={viewModel.GlbSampleVisible}|source={CleanContractText(viewModel.GlbSampleSourcePath)}|vertices={importedMesh.Positions.Length}|triangles={importedMesh.TriangleCount}|renderedTriangles={GetImportedMeshRenderedTriangleCount()}|renderTriangleStride={GetImportedMeshRenderTriangleStride()}|vertexColors={importedMesh.VertexColors.Length}|usesVertexColors={importedMesh.HasVertexColors}|texCoords={importedMesh.TextureCoordinates.Length}|hasTexture={importedMesh.HasBaseColorTexture}|textureBytes={importedMesh.BaseColorTexture?.Bytes.Length ?? 0}|textureMime={importedMesh.BaseColorTexture?.MimeType ?? "(none)"}|textureUpload={CleanContractText(importedMeshTextureUploadSummary)}|min={FormatVector(importedMesh.Min)}|max={FormatVector(importedMesh.Max)}|summary={CleanContractText(viewModel.GlbSampleSummary)}";
    }

    private string CreateLazContractLine()
    {
        if (lazSample is null)
        {
            return $"LAZ|loaded=False|source={CleanContractText(viewModel.LazSampleSourcePath)}|summary={CleanContractText(viewModel.LazSampleSummary)}";
        }

        var common = $"LAZ|loaded=True|entity={MainWindowViewModel.LazEntityId}|visible={viewModel.LazSampleVisible}|source={CleanContractText(viewModel.LazSampleSourcePath)}|version={lazSample.Version}|pointFormat={lazSample.PointDataFormat}|rawPointFormat={lazSample.RawPointDataFormat}|compressed={lazSample.IsCompressed}|laszipVlr={lazSample.HasLaszipVlr}|points={lazSample.PointCount}|recordLength={lazSample.PointDataRecordLength}|offset={lazSample.PointDataOffset}|boundsX={FormatContractNumber(lazSample.MinX)}..{FormatContractNumber(lazSample.MaxX)}|boundsY={FormatContractNumber(lazSample.MinY)}..{FormatContractNumber(lazSample.MaxY)}|boundsZ={FormatContractNumber(lazSample.MinZ)}..{FormatContractNumber(lazSample.MaxZ)}";
        return lazPointCloud is null
            ? $"{common}|decoder=metadata-only|summary={CleanContractText(viewModel.LazSampleSummary)}"
            : $"{common}|decoder=points-decoded|decodedPoints={lazPointCloud.DecodedPointCount}|sampledPoints={lazPointCloud.SampledPoints.Length}|sampleStride={lazPointCloud.SampleStride}|rgb={lazPointCloud.HasRgb}|boundsMatch={lazPointCloud.BoundsMatch}|avgRgb={FormatContractNumber(lazPointCloud.AverageRed)},{FormatContractNumber(lazPointCloud.AverageGreen)},{FormatContractNumber(lazPointCloud.AverageBlue)}|summary={CleanContractText(viewModel.LazSampleSummary)}";
    }

    private string CreateImportedMeshPickContractLine()
    {
        var format = CleanContractText(viewModel.ImportedMeshFormat);
        if (selectedImportedMeshPoint is not { } point)
        {
            return $"{format}Pick|selected=False";
        }

        var triangleIndex = selectedImportedMeshTriangleIndex?.ToString(CultureInfo.InvariantCulture) ?? "(none)";
        var normal = selectedImportedMeshSurfaceNormal is { } surfaceNormal ? FormatVector(surfaceNormal) : "(none)";
        return $"{format}Pick|selected=True|kind={CleanContractText(selectedImportedMeshPickKind)}|triangleIndex={triangleIndex}|normal={normal}|position={FormatVector(point)}|summary={CleanContractText(viewModel.PickCoordinate)}";
    }

    private string CreateImportedMeshSurfaceOverlayContractLine()
    {
        var format = CleanContractText(viewModel.ImportedMeshFormat);
        var visible = selectedImportedMeshPoint is not null
            && selectedImportedMeshTriangleIndex is not null
            && selectedImportedMeshSurfaceNormal is not null;
        var triangleIndex = selectedImportedMeshTriangleIndex?.ToString(CultureInfo.InvariantCulture) ?? "(none)";
        var normal = selectedImportedMeshSurfaceNormal is { } surfaceNormal ? FormatVector(surfaceNormal) : "(none)";
        return $"{format}SurfaceOverlay|visible={visible}|triangleIndex={triangleIndex}|normal={normal}|normalScale={FormatContractNumber(visible ? GetImportedMeshSurfaceOverlayScale() : double.NaN)}";
    }

    private string CreateLazPickContractLine()
    {
        if (selectedLazPoint is not { } point)
        {
            return "LAZPick|selected=False";
        }

        return $"LAZPick|selected=True|source={FormatVector(point.Position)}|viewer={FormatVector(MapLazPosition(point.Position))}|rgb={point.Red},{point.Green},{point.Blue}|summary={CleanContractText(viewModel.PickCoordinate)}";
    }

    private static string FormatContractNumber(double value) =>
        double.IsFinite(value) ? value.ToString("F3", CultureInfo.InvariantCulture) : "(pending)";

    private static string FormatPreciseContractNumber(double value) =>
        double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : "(pending)";

    private static string FormatContractRegion(HeightDeviationRecipeRoiRegion region) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"cx={region.CenterX:F3},cz={region.CenterZ:F3},halfWidth={region.HalfWidth:F3},halfDepth={region.HalfDepth:F3}");

    private static bool IsRecipeRoiEditProperty(string? propertyName) =>
        propertyName is nameof(MainWindowViewModel.RecipeRoiLeftCenterX)
            or nameof(MainWindowViewModel.RecipeRoiLeftCenterZ)
            or nameof(MainWindowViewModel.RecipeRoiLeftHalfWidth)
            or nameof(MainWindowViewModel.RecipeRoiLeftHalfDepth)
            or nameof(MainWindowViewModel.RecipeRoiRightCenterX)
            or nameof(MainWindowViewModel.RecipeRoiRightCenterZ)
            or nameof(MainWindowViewModel.RecipeRoiRightHalfWidth)
            or nameof(MainWindowViewModel.RecipeRoiRightHalfDepth);

    private static string CleanContractText(string value) => value.Replace('|', '/').Replace(Environment.NewLine, " ");

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

    private static (double R, double G, double B) DeviationColor(double value)
    {
        var t = Clamp01(value);
        return (0.12 + 0.88 * t, 0.84 - 0.68 * t, 0.64 - 0.52 * t);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    private void RenderNow()
    {
        UpdateOrientationTriad();
        if (Viewport.IsLoaded)
        {
            Viewport.DoRender();
        }
    }
}
