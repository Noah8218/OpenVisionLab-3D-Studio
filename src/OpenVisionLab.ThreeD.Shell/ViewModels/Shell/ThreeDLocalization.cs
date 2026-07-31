using System.ComponentModel;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// Shared, view-only language surface for the 3D Studio authoring UI.
/// The OpenVisionLab localization catalog owns persistence and language changes.
/// </summary>
public sealed class ThreeDLocalization : INotifyPropertyChanged
{
    private static readonly string[] PropertyNames =
    [
        nameof(StudioSubtitle), nameof(Teach), nameof(RecipeWorkbench), nameof(Calibrate), nameof(RecipeManager), nameof(RecipeCenter), nameof(ToolLabs),
        nameof(CalibrationOverview), nameof(CalibrationHeightCalibration), nameof(CalibrationSensorAlignment),
        nameof(CalibrationRepeatability), nameof(CalibrationHistory), nameof(CalibrationRunLog),
        nameof(CalibrationProfileHistory), nameof(CalibrationTransform), nameof(CalibrationComingSoon),
        nameof(CalibrationSoonShort), nameof(CalibrationComingSoonToolTip), nameof(CalibrationProfileLifecycleComingSoon),
        nameof(AdvancedLayout), nameof(Language), nameof(OpenRecipeManagerToolTip), nameof(OpenToolLabsToolTip),
        nameof(OpenAdvancedToolTip), nameof(Filter), nameof(HeightDifferenceEdge), nameof(TwoPointLine),
        nameof(ThreePointPlane), nameof(DatumPlaneDeviation), nameof(LineIntersection), nameof(LandmarkCorrespondence),
        nameof(XYZAffineSolve), nameof(XYZAffineApply), nameof(ToolboxAndEntities), nameof(ToolLibrary), nameof(ToolLibraryHint),
        nameof(ToolSearch), nameof(AllTools), nameof(RecipeFlow), nameof(RecipeFlowHint), nameof(FilterOptionalHint),
        nameof(AddSelectedStep), nameof(AddToolFromCatalog), nameof(AddToolFromCatalogToolTip), nameof(Viewer),
        nameof(ViewerLayout), nameof(ViewerSingle), nameof(ViewerSplitVertical), nameof(ViewerSplitHorizontal),
        nameof(ViewerPopOut), nameof(ViewerMainSlot), nameof(ViewerAuxiliarySlot), nameof(ViewerAuxiliaryOutput),
        nameof(ViewerAuxiliaryNoOutput), nameof(ViewerPresentationOnly), nameof(ViewerPopOutTitle),
        nameof(ViewerReturnToSingle), nameof(HeightImage), nameof(OpenHeightImage), nameof(HeightImageLoading),
        nameof(HeightImageUnavailable), nameof(HeightImageFit), nameof(HeightImageActualPixels),
        nameof(HeightImageZoomOut), nameof(HeightImageZoomIn),
        nameof(HeightImageCoordinateHint), nameof(HeightImagePanZoomHint), nameof(HeightImageViewOnly),
        nameof(HeightImageMissingValue), nameof(HeightImagePalette), nameof(HeightImagePaletteHeight),
        nameof(HeightImagePaletteGrayscale), nameof(HeightImagePaletteThermal),
        nameof(HeightImageAutoRange), nameof(HeightImageManualRange), nameof(HeightImageAutoRangeToolTip),
        nameof(HeightImageRangeMinimum), nameof(HeightImageRangeMaximum), nameof(HeightImageApplyRange),
        nameof(HeightImageApplyRangeToolTip), nameof(HeightImageRangeInvalid),
        nameof(HeightImageInvalidOverlay), nameof(HeightImageInvalidOverlayToolTip),
        nameof(HeightImageInvalidOverlayLegend), nameof(HeightImageInvalidOverlayHidden),
        nameof(HeightImageInvalidOverlayUnavailable),
        nameof(HeightImageViewOnlyShort),
        nameof(HeightImageRoiEditing), nameof(HeightImageRoiDrawHint),
        nameof(HeightImageRoiReviewHint), nameof(HeightImageRoiSelectHint),
        nameof(HeightImageRoiApply), nameof(HeightImageRoiDelete),
        nameof(SharedHeightCursor), nameof(HeightImageCursorFromHeightImage),
        nameof(HeightImageCursorFromThreeD),
        nameof(SourceQuality), nameof(OpenSourceQuality),
        nameof(SourceQualityLoading), nameof(SourceQualityUnavailable), nameof(SourceQualityReady),
        nameof(SourceQualityError), nameof(SourceQualityHint), nameof(SourceQualityViewOnly),
        nameof(ThicknessRepeatGrid), nameof(RepeatAsGrid),
        nameof(ThicknessRepeatReady), nameof(ThicknessRepeatUnavailable),
        nameof(ThicknessRepeatColumns), nameof(ThicknessRepeatRows), nameof(ThicknessRepeatColumnPitch),
        nameof(ThicknessRepeatRowPitch), nameof(ThicknessRepeatNamePattern),
        nameof(ThicknessRepeatReviewFormat), nameof(ApplyThicknessRepeat),
        nameof(CancelThicknessRepeat), nameof(ThicknessGroupFormat), nameof(StepParameters),
        nameof(PipelineValidation), nameof(RunRecord), nameof(RunRecordTitle), nameof(RunRecordDetail),
        nameof(RunRecordOpen), nameof(RunRecordOpenCurrent), nameof(RunRecordOpenHtml), nameof(RunRecordOpenCsv),
        nameof(RunRecordOpenFolder), nameof(RunRecordExport), nameof(RunRecordRecent), nameof(RunRecordOpenRecent),
        nameof(RunRecordSummaryFormat), nameof(RunRecordThresholdCorrection),
        nameof(RunRecordThresholdCorrectionDetail), nameof(RunRecordOpenFailed),
        nameof(RunRecordExportedFormat), nameof(ResultsWorkspaceTitle),
        nameof(ResultsWorkspaceDetail), nameof(ResultsWorkspaceRunRecord),
        nameof(ResultsWorkspaceOutputCompare), nameof(ResultsWorkspaceReports),
        nameof(ResultsWorkspaceReportsDetail), nameof(ResultsWorkspaceAdvanced),
        nameof(ResultsWorkspaceImmutable), nameof(ResultsOperatorSummaryTitle),
        nameof(ResultsOperatorDecision), nameof(ResultsOperatorAffectedSteps),
        nameof(ResultsOperatorNextAction),
        nameof(ValidationSet), nameof(ValidationSetTitle), nameof(ValidationSetDetail),
        nameof(ValidationSetAddSamples), nameof(ValidationSetAddCurrentInput), nameof(ValidationSetRunAll),
        nameof(ValidationSetRunAllHint), nameof(ValidationSetClear),
        nameof(ValidationSetSamples), nameof(ValidationSetSelectedRecord), nameof(ValidationSetNoSamples),
        nameof(ValidationSetNoSelection), nameof(ValidationSetFile), nameof(ValidationSetDuration),
        nameof(ValidationSetCoverage), nameof(ValidationSetFilterAll), nameof(ValidationSetFilterPass),
        nameof(ValidationSetFilterFail), nameof(ValidationSetFilterError), nameof(ValidationSetPreviousIssue),
        nameof(ValidationSetNextIssue), nameof(ValidationSetOpenComparison), nameof(ValidationSetCancel),
        nameof(ValidationSetMetrics), nameof(ValidationSetOverlays), nameof(ValidationSetNoMetrics),
        nameof(ValidationSetNoOverlays), nameof(ValidationSetComparisonHint),
        nameof(ValidationSetRole), nameof(ValidationSetLabeledEvidence),
        nameof(ValidationSetScope), nameof(ValidationSetOwner), nameof(ValidationSetMetric),
        nameof(ValidationSetThresholdCandidates), nameof(ValidationSetThresholdReadOnly),
        nameof(ValidationSetThresholdReview), nameof(ValidationSetThresholdCancelReview),
        nameof(ValidationSetThresholdApplyDraft), nameof(ValidationSetThresholdRevalidateDevelopment),
        nameof(ValidationSetThresholdReplayHeldOut),
        nameof(ValidationWorkspaceSamples), nameof(ValidationWorkspaceResults),
        nameof(ValidationWorkspaceFailures), nameof(ValidationWorkspaceThresholds),
        nameof(ValidationWorkspaceHeldOut), nameof(ValidationWorkspaceOpenInTeach),
        nameof(ValidationWorkspaceOpenInTeachHint), nameof(ValidationFailureSummaryTitle),
        nameof(ValidationFailureSample), nameof(ValidationFailureRule),
        nameof(ValidationFailureReason), nameof(ValidationFailureNextAction),
        nameof(ValidationFailureNextActionDetail),
        nameof(ValidationSetLimits), nameof(ValidationSetCorrect), nameof(ValidationSetErrors),
        nameof(ValidationSetFalseAccept), nameof(ValidationSetFalseReject),
        nameof(ValidationSetExpected), nameof(ValidationSetPredicted), nameof(ValidationSetDecision),
        nameof(ColumnEvidence), nameof(SessionLog), nameof(HeightProfile), nameof(FitDiagnostics),
        nameof(IntersectionEvidence), nameof(CorrespondenceEvidence), nameof(OutputCompare), nameof(OutputCompareTitle),
        nameof(OutputCompareDetail), nameof(OutputCompareNoSelection), nameof(OutputComparePinnedOutput), nameof(FlowMap), nameof(FlowMapTitle),
        nameof(DisplayedOutputs), nameof(DisplayedOutputsTitle), nameof(DisplayedOutputsDetail),
        nameof(DisplayedOutputsNoViewerSelection), nameof(CurrentViewerDisplay), nameof(DisplayedInViewer),
        nameof(ShowInViewer), nameof(PinToCompare), nameof(FocusStep), nameof(DisplayedOutputsSummaryFormat),
        nameof(DisplayableC3DData), nameof(EvidenceOnlyOutput), nameof(NoCurrentDisplayableOutput), nameof(PinnedSlotsFormat),
        nameof(FlowMapDetail), nameof(FlowMapReadOnly), nameof(FlowMapInput), nameof(FlowMapOutput),
        nameof(FlowMapPortState), nameof(FlowMapEmptyHint), nameof(Problems), nameof(ProblemsTitle),
        nameof(ProblemsDetail), nameof(ProblemsSummaryFormat), nameof(ProblemsRouteChecks),
        nameof(ProblemsValidationMessages), nameof(ProblemsEmptyHint), nameof(FlowPortReady),
        nameof(FlowPortWaitingForUpstream), nameof(FlowPortStale), nameof(FlowPortUnresolved),
        nameof(FlowPortDeclared), nameof(FlowPortCurrent), nameof(FlowPortNoInputDetail),
        nameof(FlowPortUnresolvedDetailFormat), nameof(FlowPortWaitingDetailFormat),
        nameof(FlowPortStaleDetailFormat), nameof(FlowPortDeclaredDetailFormat),
        nameof(FlowPortCurrentDetailFormat), nameof(NavigatorHint), nameof(RecipeSource),
        nameof(RecipeNavigator), nameof(CompatibleToolCatalogTitle), nameof(CompatibleToolCatalogDetail),
        nameof(CompatibleToolCatalogSummaryFormat), nameof(CompatibleToolCatalogEmpty), nameof(SelectCompatibleTool),
        nameof(AddCompatibleTool), nameof(AddCompatibleToolToolTip), nameof(CompatibleToolBlockerLabel),
        nameof(CompatibleToolBlockerDetailFormat),
        nameof(AddInspectionStep), nameof(StepProperties), nameof(NoRecipeStepSelected),
        nameof(NoRecipeStepSelectedDetail), nameof(RecipePipelineTeachReview), nameof(Validate), nameof(MoveUp),
        nameof(MoveDown), nameof(Remove), nameof(ColumnNumber), nameof(ColumnTool), nameof(ColumnInputs),
        nameof(ColumnTypedOutput), nameof(ColumnState), nameof(Preview), nameof(Run), nameof(Publish), nameof(Cancel),
        nameof(SelectedPaletteItem), nameof(Input), nameof(Output), nameof(ParameterAdapter), nameof(Inputs),
        nameof(InputParameterOutputSummary), nameof(TypedParameters), nameof(StepPropertiesEditDetail),
        nameof(Discard), nameof(ApplyParameters), nameof(Produces), nameof(OutputEntity),
        nameof(ExpectedData), nameof(InputEntities), nameof(ToolboxSequenceHint), nameof(SelectedRoute),
        nameof(OpenSelectedToolLab), nameof(ToolLabReview), nameof(ToolLabReviewDetail),
        nameof(ShowInput), nameof(TeachingSelections), nameof(ThicknessRoiTeaching), nameof(ThicknessRoiTeachingDetail),
        nameof(ThicknessRoiReadyDetail), nameof(ThicknessMeasurementRoi), nameof(TwoGridCorners),
        nameof(RecipeOwnedSelection), nameof(CaptureSelection), nameof(ReplaceSelection), nameof(RemoveSelection),
        nameof(UseExistingSelection), nameof(UseSelection), nameof(UndoLastPoint), nameof(ApplySelection),
        nameof(SurfaceRoiEditor), nameof(SurfaceRoiEditorDetail), nameof(RoiRow), nameof(RoiColumn),
        nameof(RoiRowCount), nameof(RoiColumnCount), nameof(SourceFrameFootprint),
        nameof(SelectionCapture), nameof(SelectionCaptureInactive), nameof(SelectionCaptureProgressFormat),
        nameof(RoiCaptureReadyProgress),
        nameof(RoiCaptureStartInstruction), nameof(RoiCaptureSecondInstruction), nameof(RoiCaptureReadyInstruction),
        nameof(PlaneFlatnessRoiTeaching), nameof(PlaneFlatnessRoiTeachingDetail),
        nameof(ReferenceRoi), nameof(MeasurementRoi), nameof(RoiComplete), nameof(RoiWaiting),
        nameof(RoiMissing), nameof(RoiDrawing), nameof(RoiReview), nameof(RoiApplied),
        nameof(CaptureRoi), nameof(ReplaceRoi), nameof(DrawRoi), nameof(RedrawRoi), nameof(EditRoi), nameof(FitRoi), nameof(ReuseRoi), nameof(ExistingCompatibleRoi),
        nameof(ReferenceRoiRequiredFirst), nameof(NoRoiTaught), nameof(GapFlushRoiTeaching),
        nameof(GapFlushRoiTeachingDetail), nameof(VolumeRoiTeaching), nameof(VolumeRoiTeachingDetail),
        nameof(CompletenessRoiTeaching), nameof(CompletenessRoiTeachingDetail), nameof(InspectionGridRoi),
        nameof(CrossSectionSelection), nameof(CrossSectionSelectionDetail),
        nameof(FirstRoi), nameof(SecondRoi), nameof(FirstRoiRequiredFirst),
        nameof(RecipeJourneyGuide), nameof(JourneyRecipe), nameof(JourneyInput), nameof(JourneyTools),
        nameof(JourneyTeachPreview), nameof(JourneyValidateRun), nameof(NextAction),
        nameof(LoadInputActionTitle), nameof(LoadInputActionDetail), nameof(Open3DMap), nameof(Open3DMapToolTip),
        nameof(Loading3DMapFormat), nameof(Cancel3DMapLoadToolTip), nameof(AddFirstToolActionTitle),
        nameof(AddFirstToolActionDetail), nameof(SelectStepActionTitle), nameof(SelectStepActionDetail),
        nameof(TeachSelectedStepActionTitle), nameof(TeachSelectedStepActionDetail),
        nameof(NewRecipe), nameof(OpenExistingRecipe), nameof(CurrentRecipe), nameof(RecentRecipes),
        nameof(RecipeNameLabel), nameof(RecipeStatusLabel), nameof(RecipePathLabel), nameof(SourceLabel),
        nameof(StepsLabel), nameof(Save), nameof(SaveAs), nameof(RemoveFromRecent),
        nameof(RemoveFromRecentToolTip), nameof(Available), nameof(Unavailable), nameof(RecipeCenterDetail),
        nameof(SourceNotSelected), nameof(SourceUnsupportedFormat), nameof(SourceMissing), nameof(SourceIdentityMismatch),
        nameof(SourceUnreadable), nameof(SourceReadyFormat), nameof(NotSavedYet), nameof(Valid),
        nameof(ValidWarningsFormat), nameof(CorrectionsFormat), nameof(SourceCorrectionsFormat),
        nameof(StaleSelectionsFormat), nameof(Modified), nameof(Unsaved), nameof(Saved),
        nameof(RecipeSaveBlockedTitle), nameof(RecipeSaveBlockedCorrections),
        nameof(AdvancedIdentityAndOrder), nameof(StepName), nameof(StepId)
    ];

    public static ThreeDLocalization Shared { get; } = new();

    private ThreeDLocalization() => OpenVisionLanguageService.LanguageChanged += (_, _) => Refresh();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StudioSubtitle => T("ThreeD.Header.StudioSubtitle", "3D \uAC80\uC0AC \uB808\uC2DC\uD53C \uC6CC\uD06C\uBCA4\uCE58", "3D inspection recipe workbench");
    public string Teach => T("ThreeD.Header.Teach", "\uD2F0\uCE6D", "Teach");
    public string Calibrate => T("ThreeD.Header.Calibrate", "\uAD50\uC815", "Calibrate");
    public string CalibrationOverview => T("ThreeD.Calibration.Overview", "\uAC1C\uC694", "Overview");
    public string CalibrationHeightCalibration => T("ThreeD.Calibration.HeightCalibration", "\uB192\uC774 \uAD50\uC815", "Height Calibration");
    public string CalibrationSensorAlignment => T("ThreeD.Calibration.SensorAlignment", "\uC13C\uC11C \uC815\uB82C", "Sensor Alignment");
    public string CalibrationRepeatability => T("ThreeD.Calibration.Repeatability", "\uBC18\uBCF5\uC131", "Repeatability");
    public string CalibrationHistory => T("ThreeD.Calibration.History", "\uC774\uB825", "History");
    public string CalibrationRunLog => T("ThreeD.Calibration.RunLog", "\uC2E4\uD589 \uAE30\uB85D", "Run Log");
    public string CalibrationProfileHistory => T("ThreeD.Calibration.ProfileHistory", "\uD504\uB85C\uD30C\uC77C \uC774\uB825", "Profile History");
    public string CalibrationTransform => T("ThreeD.Calibration.Transform", "\uBCC0\uD658", "Transform");
    public string CalibrationComingSoon => T("ThreeD.Calibration.ComingSoon", "\uC900\uBE44 \uC911", "Coming soon");
    public string CalibrationSoonShort => T("ThreeD.Calibration.SoonShort", "\uC900\uBE44", "Soon");
    public string CalibrationComingSoonToolTip => T(
        "ThreeD.Calibration.ComingSoonToolTip",
        "\uC774 \uAD50\uC815 \uAE30\uB2A5\uC740 \uC544\uC9C1 \uAD6C\uD604\u00B7\uAC80\uC99D\uB418\uC9C0 \uC54A\uC544 \uC120\uD0DD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
        "This calibration capability is not selectable because it has not been implemented and verified.");
    public string CalibrationProfileLifecycleComingSoon => T(
        "ThreeD.Calibration.ProfileLifecycleComingSoon",
        "\uD504\uB85C\uD30C\uC77C \uAC80\uC99D\u00B7\uD65C\uC131\uD654\uB294 \uC900\uBE44 \uC911\uC785\uB2C8\uB2E4.",
        "Profile validation and activation are coming soon.");
    public string RecipeManager => T("ThreeD.Header.RecipeManager", "\uB808\uC2DC\uD53C \uC13C\uD130", "Recipe Center");
    public string RecipeCenter => T("ThreeD.Header.RecipeCenter", "\uB808\uC2DC\uD53C \uC13C\uD130", "Recipe Center");
    public string RecipeWorkbench => T("ThreeD.Header.RecipeWorkbench", "\uAC80\uC0AC \uB808\uC2DC\uD53C", "Inspection Recipe");
    public string SourceQuality => T(
        "ThreeD.SourceQuality.Title",
        "\uB370\uC774\uD130 \uD488\uC9C8",
        "Source Quality");
    public string OpenSourceQuality => T(
        "ThreeD.SourceQuality.Open",
        "\uD488\uC9C8 \uBCF4\uAE30",
        "View quality");
    public string SourceQualityLoading => T(
        "ThreeD.SourceQuality.Loading",
        "\uD488\uC9C8 \uBD84\uC11D \uC911",
        "Analyzing source quality");
    public string SourceQualityUnavailable => T(
        "ThreeD.SourceQuality.Unavailable",
        "\uD488\uC9C8 \uC815\uBCF4 \uC5C6\uC74C",
        "Source quality unavailable");
    public string SourceQualityReady => T(
        "ThreeD.SourceQuality.Ready",
        "\uBD84\uC11D \uC644\uB8CC",
        "Analysis ready");
    public string SourceQualityError => T(
        "ThreeD.SourceQuality.Error",
        "\uBD84\uC11D \uC2E4\uD328",
        "Analysis failed");
    public string SourceQualityHint => T(
        "ThreeD.SourceQuality.Hint",
        "\uAC80\uC0AC \uC804\uC5D0 \uC785\uB825 \uADF8\uB9AC\uB4DC, \uC720\uD6A8\u00B7\uB204\uB77D \uC140, \uB192\uC774 \uBD84\uD3EC\uC640 \uCC44\uB110 \uC81C\uC57D\uC744 \uD655\uC778\uD569\uB2C8\uB2E4.",
        "Review the input grid, valid and missing cells, height distribution, and channel limits before inspection.");
    public string SourceQualityViewOnly => T(
        "ThreeD.SourceQuality.ViewOnly",
        "\uBCF4\uAE30 \uC804\uC6A9 \u00B7 \uB808\uC2DC\uD53C \uBCC0\uACBD \uC5C6\uC74C \u00B7 Preview/Run \uC2E4\uD589 \uC5C6\uC74C",
        "Read-only \u00b7 recipe unchanged \u00b7 no Preview or Run");
    public string ToolLabs => T("ThreeD.Header.ToolLabs", "\uD234 \uB7A9", "Tool Labs");
    public string AdvancedLayout => T("ThreeD.Header.AdvancedLayout", "\uACE0\uAE09 \uB808\uC774\uC544\uC6C3", "Advanced layout");
    public string Language => T("ThreeD.Header.Language", "\uC5B8\uC5B4", "Language");
    public string OpenRecipeManagerToolTip => T("ThreeD.Header.OpenRecipeManagerToolTip", "\uBCC4\uB3C4 \uB808\uC2DC\uD53C \uC13C\uD130\uC5D0\uC11C \uC0C8 \uAC80\uC0AC\uB97C \uC2DC\uC791\uD558\uAC70\uB098 \uAE30\uC874 \uB808\uC2DC\uD53C\uB97C \uC5FD\uB2C8\uB2E4.", "Open the separate Recipe Center to start a new inspection or open an existing recipe.");
    public string OpenToolLabsToolTip => T("ThreeD.Header.OpenToolLabsToolTip", "\uAE30\uC874 \uB3C4\uAD6C\uC758 \uC785\uB825\u00B7\uCD9C\uB825\u00B7\uC99D\uAC70 \uC804\uC6A9 \uBDF0\uB97C \uC5FD\uB2C8\uB2E4.", "Open an existing tool's focused input, output, and evidence view.");
    public string OpenAdvancedToolTip => T("ThreeD.Header.OpenAdvancedToolTip", "\uAE30\uC874 \uC9C4\uB2E8 \uC804\uC6A9 \uB808\uC774\uC544\uC6C3\uC744 \uC5FD\uB2C8\uB2E4.", "Open the existing diagnostic dock layout.");
    public string Filter => T("ThreeD.Tool.Filter", "\uD544\uD130", "Filter");
    public string HeightDifferenceEdge => T("ThreeD.Tool.HeightDifferenceEdge", "\uB192\uC774 \uCC28\uC774 \uC5E3\uC9C0", "Height Difference Edge");
    public string TwoPointLine => T("ThreeD.Tool.TwoPointLine", "2-\uD3EC\uC778\uD2B8 \uB77C\uC778", "2-Point Line");
    public string ThreePointPlane => T("ThreeD.Tool.ThreePointPlane", "3-\uD3EC\uC778\uD2B8 \uD3C9\uBA74", "3-Point Plane");
    public string DatumPlaneDeviation => T("ThreeD.Tool.DatumPlaneDeviation", "\uB370\uC774\uD140 \uD3C9\uBA74 \uB192\uC774 \uD3B8\uCC28", "Datum Plane Deviation");
    public string LineIntersection => T("ThreeD.Tool.LineIntersection", "\uB77C\uC778 \uAD50\uCC28\uC810", "Line Intersection");
    public string LandmarkCorrespondence => T("ThreeD.Tool.LandmarkCorrespondence", "\uB79C\uB4DC\uB9C8\uD06C \uB300\uC751", "Landmark Correspondence");
    public string XYZAffineSolve => T("ThreeD.Tool.XYZAffineSolve", "XYZ \uC5B4\uD30C\uC778 \uACC4\uC0B0", "XYZ Affine Solve");
    public string XYZAffineApply => T("ThreeD.Tool.XYZAffineApply", "XYZ \uC5B4\uD30C\uC778 \uC801\uC6A9", "Apply XYZ Affine");
    public string RegridHeightMap => T("ThreeD.Tool.RegridHeightMap", "\uB192\uC774 \uB9F5 \uC7AC\uACA9\uC790\uD654", "Re-grid Height Map");
    public string ToolboxAndEntities => T("ThreeD.Workbench.ToolboxAndEntities", "\uD234\uBC15\uC2A4 \uBC0F \uC5D4\uD2F0\uD2F0", "Toolbox & Entities");
    public string ToolLibrary => T("ThreeD.Workbench.ToolLibrary", "\uAC80\uC0AC \uB3C4\uAD6C", "Inspection Tools");
    public string ToolLibraryHint => T("ThreeD.Workbench.ToolLibraryHint", "3D \uC785\uB825\uC774 \uC900\uBE44\uB418\uBA74 \uD638\uD658\uB418\uB294 \uAC80\uC0AC \uB3C4\uAD6C\uB97C \uCD94\uAC00\uD558\uC138\uC694.", "After 3D input is ready, add a compatible inspection tool.");
    public string ToolSearch => T("ThreeD.Workbench.ToolSearch", "\uB3C4\uAD6C \uAC80\uC0C9", "Search tools");
    public string AllTools => T("ThreeD.Workbench.AllTools", "\uC804\uCCB4 \uB3C4\uAD6C", "All tools");
    public string RecipeFlow => T("ThreeD.Workbench.RecipeFlow", "\uAC80\uC0AC \uAD6C\uC131", "Inspection Flow");
    public string RecipeFlowHint => T("ThreeD.Workbench.RecipeFlowHint", "\uB2E8\uACC4\uB97C \uC120\uD0DD\uD558\uBA74 \uC624\uB978\uCABD\uC5D0\uC11C \uC124\uC815\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uC120\uD0DD\uD55C \uB2E8\uACC4\uB294 \uC774\uB3D9\uD558\uAC70\uB098 \uC0AD\uC81C\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "Select a step to edit it on the right. The selected step can also be reordered or removed here.");
    public string FilterOptionalHint => T("ThreeD.Workbench.FilterOptionalHint", "\uD544\uD130\uB294 \uC120\uD0DD \uC0AC\uD56D\uC785\uB2C8\uB2E4. \uD604\uC7AC \uC785\uB825\uC774 \uD638\uD658\uB418\uBA74 \uCE21\uC815 \uB3C4\uAD6C\uB97C \uBC14\uB85C \uCD94\uAC00\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "Filter is optional. Add a measurement tool directly when the current input is compatible.");
    public string AddSelectedStep => T("ThreeD.Command.AddSelectedStep", "\uC120\uD0DD \uB2E8\uACC4 \uCD94\uAC00", "Add selected step");
    public string AddToolFromCatalog => T("ThreeD.Command.AddToolFromCatalog", "\uB3C4\uAD6C \uCD94\uAC00", "Add tool");
    public string AddToolFromCatalogToolTip => T("ThreeD.Command.AddToolFromCatalogToolTip", "\uC774 \uB3C4\uAD6C\uB97C \uB808\uC2DC\uD53C\uC5D0 \uCD94\uAC00\uD569\uB2C8\uB2E4. \uCD94\uAC00\uB9CC \uD558\uBA70 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Add this tool to the recipe. This does not run inspection.");
    public string Viewer => T("ThreeD.Workbench.Viewer", "3D \uBDF0", "3D View");
    public string ViewerLayout => T("ThreeD.Workbench.ViewerLayout", "\uBDF0\uC5B4 \uBC30\uCE58", "Viewer layout");
    public string ViewerSingle => T("ThreeD.Command.ViewerSingle", "\uB2E8\uC77C", "Single");
    public string ViewerSplitVertical => T("ThreeD.Command.ViewerSplitVertical", "\uC88C\uC6B0 \uBD84\uD560", "Side by side");
    public string ViewerSplitHorizontal => T("ThreeD.Command.ViewerSplitHorizontal", "\uC0C1\uD558 \uBD84\uD560", "Stacked");
    public string ViewerPopOut => T("ThreeD.Command.ViewerPopOut", "\uC0C8 \uCC3D", "Pop out");
    public string ViewerMainSlot => T("ThreeD.Workbench.ViewerMainSlot", "\uC8FC \uBDF0", "Main view");
    public string ViewerAuxiliarySlot => T("ThreeD.Workbench.ViewerAuxiliarySlot", "\uBCF4\uC870 \uBDF0", "Auxiliary view");
    public string ViewerAuxiliaryOutput => T("ThreeD.Workbench.ViewerAuxiliaryOutput", "\uBCF4\uC870 \uBDF0 \uCD9C\uB825", "Auxiliary view output");
    public string ViewerAuxiliaryNoOutput => T("ThreeD.Workbench.ViewerAuxiliaryNoOutput", "\uD45C\uC2DC\uD560 \uC2E4\uC81C 3D \uC0B0\uCD9C\uBB3C \uC5C6\uC74C", "No real 3D output is available");
    public string ViewerPresentationOnly => T("ThreeD.Workbench.ViewerPresentationOnly", "\uBCF4\uAE30 \uC804\uC6A9 \u00B7 \uB808\uC2DC\uD53C/\uAC80\uC0AC \uBCC0\uACBD \uC5C6\uC74C", "View only \u00B7 recipe and inspection stay unchanged");
    public string ViewerPopOutTitle => T("ThreeD.Workbench.ViewerPopOutTitle", "OpenVisionLab 3D \uBCF4\uC870 \uBDF0", "OpenVisionLab 3D Auxiliary View");
    public string ViewerReturnToSingle => T("ThreeD.Command.ViewerReturnToSingle", "\uB2E8\uC77C \uBDF0\uB85C \uBCF5\uADC0", "Return to single view");
    public string HeightImage => T("ThreeD.Workbench.HeightImage", "\uB192\uC774 \uC774\uBBF8\uC9C0", "Height Image");
    public string OpenHeightImage => T("ThreeD.Command.OpenHeightImage", "\uB192\uC774 \uC774\uBBF8\uC9C0 \uC5F4\uAE30", "Open Height Image");
    public string HeightImageLoading => T("ThreeD.Workbench.HeightImageLoading", "\uC804\uCCB4 \uD574\uC0C1\uB3C4 \uB192\uC774 \uC774\uBBF8\uC9C0 \uB85C\uB4DC \uC911\u2026", "Loading full-resolution Height Image\u2026");
    public string HeightImageUnavailable => T("ThreeD.Workbench.HeightImageUnavailable", "\uD45C\uC2DC\uD560 C3D \uB192\uC774 \uC774\uBBF8\uC9C0 \uC5C6\uC74C", "No C3D Height Image is available");
    public string HeightImageFit => T("ThreeD.Command.HeightImageFit", "\uD654\uBA74\uC5D0 \uB9DE\uCDA4", "Fit");
    public string HeightImageActualPixels => T("ThreeD.Command.HeightImageActualPixels", "1:1 \uD53D\uC140", "1:1 pixels");
    public string HeightImageZoomOut => T("ThreeD.Command.HeightImageZoomOut", "\uCD95\uC18C", "Zoom out");
    public string HeightImageZoomIn => T("ThreeD.Command.HeightImageZoomIn", "\uD655\uB300", "Zoom in");
    public string HeightImageCoordinateHint => T("ThreeD.Workbench.HeightImageCoordinateHint", "\uCEE4\uC11C\uB97C \uC774\uB3D9\uD574 column / row / H \uD655\uC778", "Move the pointer to inspect column / row / H");
    public string HeightImagePanZoomHint => T("ThreeD.Workbench.HeightImagePanZoomHint", "\uD720: \uD655\uB300/\uCD95\uC18C \u00B7 \uC911\uAC04 \uB4DC\uB798\uADF8: \uC774\uB3D9", "Wheel: zoom \u00B7 middle drag: pan");
    public string HeightImageViewOnly => T("ThreeD.Workbench.HeightImageViewOnly", "\uD45C\uC2DC/\uCEE4\uC11C\uB294 \uBCF4\uAE30 \uC804\uC6A9 \u00B7 ROI \uC801\uC6A9\uB9CC \uB808\uC2DC\uD53C \uBCC0\uACBD \u00B7 \uAC80\uC0AC \uC2E4\uD589 \uC5C6\uC74C", "Display and cursor are view only \u00B7 only ROI Apply changes the recipe \u00B7 inspection does not run");
    public string HeightImageMissingValue => T("ThreeD.Workbench.HeightImageMissingValue", "\uACB0\uCE21 \uC140", "missing cell");
    public string HeightImagePalette => T("ThreeD.Workbench.HeightImagePalette", "\uD314\uB808\uD2B8", "Palette");
    public string HeightImagePaletteHeight => T("ThreeD.Workbench.HeightImagePaletteHeight", "\uB192\uC774", "Height");
    public string HeightImagePaletteGrayscale => T("ThreeD.Workbench.HeightImagePaletteGrayscale", "\uD68C\uC0C9\uC870", "Grayscale");
    public string HeightImagePaletteThermal => T("ThreeD.Workbench.HeightImagePaletteThermal", "\uC5F4\uD654\uC0C1", "Thermal");
    public string HeightImageAutoRange => T("ThreeD.Command.HeightImageAutoRange", "\uC790\uB3D9 \uBC94\uC704", "Auto range");
    public string HeightImageManualRange => T("ThreeD.Workbench.HeightImageManualRange", "\uC218\uB3D9 \uBC94\uC704", "Manual range");
    public string HeightImageAutoRangeToolTip => T("ThreeD.Command.HeightImageAutoRangeToolTip", "\uC804\uCCB4 \uC720\uD6A8 \uB192\uC774\uC758 \uCD5C\uC18C/\uCD5C\uB300\uB85C \uD45C\uC2DC \uBC94\uC704\uB97C \uBCF5\uC6D0\uD569\uB2C8\uB2E4. \uBCF4\uAE30 \uC804\uC6A9\uC785\uB2C8\uB2E4.", "Restore the display range to the full valid-height minimum and maximum. View only.");
    public string HeightImageRangeMinimum => T("ThreeD.Workbench.HeightImageRangeMinimum", "\uCD5C\uC18C", "Min");
    public string HeightImageRangeMaximum => T("ThreeD.Workbench.HeightImageRangeMaximum", "\uCD5C\uB300", "Max");
    public string HeightImageApplyRange => T("ThreeD.Command.HeightImageApplyRange", "\uBC94\uC704 \uC801\uC6A9", "Apply range");
    public string HeightImageApplyRangeToolTip => T("ThreeD.Command.HeightImageApplyRangeToolTip", "\uC785\uB825\uD55C \uCD5C\uC18C/\uCD5C\uB300\uB97C \uB192\uC774 \uC774\uBBF8\uC9C0 \uC0C9\uC0C1\uC5D0\uB9CC \uC801\uC6A9\uD569\uB2C8\uB2E4. \uB370\uC774\uD130\uC640 \uB808\uC2DC\uD53C\uB294 \uBCC0\uACBD\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Apply the entered minimum and maximum to Height Image colors only. Data and recipe stay unchanged.");
    public string HeightImageRangeInvalid => T("ThreeD.Workbench.HeightImageRangeInvalid", "\uC720\uD55C\uD55C \uC22B\uC790\uB97C \uC785\uB825\uD558\uACE0 \uCD5C\uC18C\uB97C \uCD5C\uB300\uBCF4\uB2E4 \uC791\uAC8C \uC124\uC815\uD558\uC138\uC694.", "Enter finite numbers and keep minimum below maximum.");
    public string HeightImageInvalidOverlay => T("ThreeD.Workbench.HeightImageInvalidOverlay", "\uACB0\uCE21 \uC140 \uD45C\uC2DC", "Show missing cells");
    public string HeightImageInvalidOverlayToolTip => T("ThreeD.Workbench.HeightImageInvalidOverlayToolTip", "\uACB0\uCE21 \uC140\uC744 \uC790\uD64D\uC0C9\uC73C\uB85C \uD45C\uC2DC\uD569\uB2C8\uB2E4. \uC6D0\uBCF8 \uB370\uC774\uD130, ROI, \uB808\uC2DC\uD53C\uC640 \uAC80\uC0AC \uACB0\uACFC\uB294 \uBCC0\uACBD\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Show missing cells in magenta. Source data, ROI, recipe, and inspection results remain unchanged.");
    public string HeightImageInvalidOverlayLegend => T("ThreeD.Workbench.HeightImageInvalidOverlayLegend", "\uC790\uD64D\uC0C9 = \uACB0\uCE21 \uC140", "Magenta = missing cell");
    public string HeightImageInvalidOverlayHidden => T("ThreeD.Workbench.HeightImageInvalidOverlayHidden", "\uC228\uAE40 = \uACB0\uCE21 \uC140", "Hidden missing cells");
    public string HeightImageInvalidOverlayUnavailable => T("ThreeD.Workbench.HeightImageInvalidOverlayUnavailable", "\uACB0\uCE21 \uC140 \uC815\uBCF4 \uC5C6\uC74C", "Missing-cell information unavailable");
    public string HeightImageViewOnlyShort => T("ThreeD.Workbench.HeightImageViewOnlyShort", "\uBCF4\uAE30 \uC804\uC6A9", "view only");
    public string HeightImageRoiEditing => T("ThreeD.Workbench.HeightImageRoiEditing", "ROI \uB3D9\uAE30\uD654 \uD3B8\uC9D1", "Synchronized ROI editing");
    public string HeightImageRoiDrawHint => T("ThreeD.Workbench.HeightImageRoiDrawHint", "\uB192\uC774 \uC774\uBBF8\uC9C0\uC5D0\uC11C \uB4DC\uB798\uADF8\uD574 ROI\uB97C \uADF8\uB9BD\uB2C8\uB2E4. \uC644\uB8CC \uD6C4 Enter\uB85C \uC801\uC6A9\uD558\uAC70\uB098 Esc\uB85C \uCDE8\uC18C\uD558\uC138\uC694.", "Drag on the Height Image to draw the ROI. Then press Enter to Apply or Esc to Cancel.");
    public string HeightImageRoiReviewHint => T("ThreeD.Workbench.HeightImageRoiReviewHint", "\uC911\uC559\uC744 \uB4DC\uB798\uADF8\uD574 \uC774\uB3D9\uD558\uACE0 \uBAA8\uC11C\uB9AC \uD578\uB4E4\uB85C \uD06C\uAE30\uB97C \uC870\uC815\uD55C \uB4A4 Enter\uB85C \uC801\uC6A9\uD558\uC138\uC694.", "Drag inside to move or drag a corner handle to resize, then press Enter to Apply.");
    public string HeightImageRoiSelectHint => T("ThreeD.Workbench.HeightImageRoiSelectHint", "ROI\uB97C \uD074\uB9AD\uD574 \uC5ED\uD560\uC744 \uC120\uD0DD\uD558\uACE0, \uC120\uD0DD \uB3C4\uAD6C\uC758 ROI \uD3B8\uC9D1/\uADF8\uB9AC\uAE30\uB85C \uC2DC\uC791\uD558\uC138\uC694.", "Click an ROI to select its role, then start Draw/Edit ROI in Selected Tool.");
    public string HeightImageRoiApply => T("ThreeD.Command.HeightImageRoiApply", "ROI \uC801\uC6A9", "Apply ROI");
    public string HeightImageRoiDelete => T("ThreeD.Command.HeightImageRoiDelete", "ROI \uC0AD\uC81C", "Delete ROI");
    public string SharedHeightCursor => T("ThreeD.Workbench.SharedHeightCursor", "\uC5F0\uACB0 \uC88C\uD45C", "Linked cursor");
    public string HeightImageCursorFromHeightImage => T("ThreeD.Workbench.HeightImageCursorFromHeightImage", "2D\uC5D0\uC11C", "from 2D");
    public string HeightImageCursorFromThreeD => T("ThreeD.Workbench.HeightImageCursorFromThreeD", "3D\uC5D0\uC11C", "from 3D");
    public string ThicknessRepeatGrid => T("ThreeD.Workbench.ThicknessRepeatGrid", "\uB450\uAED8 4 \u00D7 2 \uBC18\uBCF5", "Thickness repeat grid");
    public string RepeatAsGrid => T("ThreeD.Command.RepeatAsGrid", "\uACA9\uC790\uB85C \uBC18\uBCF5", "Repeat as grid");
    public string ThicknessRepeatReady => T("ThreeD.Workbench.ThicknessRepeatReady", "\uC801\uC6A9\uB41C \uAE30\uC900/\uCE21\uC815 ROI\uB97C \uBC18\uBCF5 \uD6C4\uBCF4\uB85C \uAC80\uD1A0\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "The applied Reference and Measurement ROIs are ready for repeat review.");
    public string ThicknessRepeatUnavailable => T("ThreeD.Workbench.ThicknessRepeatUnavailable", "\uC644\uB8CC\uB41C Thickness\uC640 \uC801\uC6A9\uB41C \uAE30\uC900/\uCE21\uC815 ROI, \uADF8\uB9AC\uACE0 \uC801\uC6A9\uB41C \uD30C\uB77C\uBBF8\uD130\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4.", "A complete Thickness step, both applied ROIs, and committed parameters are required.");
    public string ThicknessRepeatColumns => T("ThreeD.Workbench.ThicknessRepeatColumns", "\uC5F4", "Columns");
    public string ThicknessRepeatRows => T("ThreeD.Workbench.ThicknessRepeatRows", "\uD589", "Rows");
    public string ThicknessRepeatColumnPitch => T("ThreeD.Workbench.ThicknessRepeatColumnPitch", "X \uD53C\uCE58(\uC5F4)", "X pitch (columns)");
    public string ThicknessRepeatRowPitch => T("ThreeD.Workbench.ThicknessRepeatRowPitch", "Z \uD53C\uCE58(\uD589)", "Z pitch (rows)");
    public string ThicknessRepeatNamePattern => T("ThreeD.Workbench.ThicknessRepeatNamePattern", "\uC774\uB984 \uD328\uD134", "Name pattern");
    public string ThicknessRepeatReviewFormat => T("ThreeD.Workbench.ThicknessRepeatReviewFormat", "\uB2E8\uACC4 {0}\uAC1C \u00B7 ROI {1}\uAC1C \u00B7 \uC801\uC6A9 \uC804 \uB808\uC2DC\uD53C \uBCC0\uACBD \uC5C6\uC74C", "{0} steps \u00B7 {1} ROIs \u00B7 recipe unchanged until Apply");
    public string ApplyThicknessRepeat => T("ThreeD.Command.ApplyThicknessRepeat", "\uBC18\uBCF5 \uC801\uC6A9", "Apply repeat");
    public string CancelThicknessRepeat => T("ThreeD.Command.CancelThicknessRepeat", "\uBC18\uBCF5 \uCDE8\uC18C", "Cancel repeat");
    public string ThicknessGroupFormat => T("ThreeD.Workbench.ThicknessGroupFormat", "\uB450\uAED8 \uADF8\uB8F9 ({0})", "Thickness group ({0})");
    public string StepParameters => T("ThreeD.Workbench.StepParameters", "\uB2E8\uACC4 \uD30C\uB77C\uBBF8\uD130", "Step Parameters");
    public string PipelineValidation => T("ThreeD.Workbench.PipelineValidation", "\uD30C\uC774\uD504\uB77C\uC778 / \uAC80\uC99D", "Pipeline / Validation");
    public string RunRecord => T("ThreeD.Workbench.RunRecord", "\uC2E4\uD589 \uAE30\uB85D", "Run Record");
    public string RunRecordTitle => T("ThreeD.Workbench.RunRecordTitle", "\uC21C\uC11C\uD615 \uB2E4\uC911 \uB2E8\uACC4 \uC2E4\uD589 \uAE30\uB85D", "Ordered multi-step run record");
    public string RunRecordDetail => T("ThreeD.Workbench.RunRecordDetail", "\uAC01 \uB3C4\uAD6C\uC758 \uC785\uB825\u00B7\uCD9C\uB825 \uC5D4\uD2F0\uD2F0, \uC0C1\uD0DC, \uD575\uC2EC \uCE21\uC815\uAC12\uC744 \uC77D\uAE30 \uC804\uC6A9\uC73C\uB85C \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.", "Read-only input/output entities, state, and key metric for each executed tool.");
    public string RunRecordOpen => T("ThreeD.Command.RunRecordOpen", "\uC2E4\uD589 \uAE30\uB85D \uC5F4\uAE30", "Open record");
    public string RunRecordOpenCurrent => T("ThreeD.Command.RunRecordOpenCurrent", "JSON \uC5F4\uAE30", "Open JSON");
    public string RunRecordOpenHtml => T("ThreeD.Command.RunRecordOpenHtml", "HTML \uC5F4\uAE30", "Open HTML");
    public string RunRecordOpenCsv => T("ThreeD.Command.RunRecordOpenCsv", "CSV \uC5F4\uAE30", "Open CSV");
    public string RunRecordOpenFolder => T("ThreeD.Command.RunRecordOpenFolder", "\uD3F4\uB354 \uC5F4\uAE30", "Open folder");
    public string RunRecordExport => T("ThreeD.Command.RunRecordExport", "\uACB0\uACFC \uBB36\uC74C \uB0B4\uBCF4\uB0B4\uAE30", "Export bundle");
    public string RunRecordRecent => T("ThreeD.Workbench.RunRecordRecent", "\uCD5C\uADFC \uC2E4\uD589 \uAE30\uB85D", "Recent Run Records");
    public string RunRecordOpenRecent => T("ThreeD.Command.RunRecordOpenRecent", "\uC120\uD0DD \uAE30\uB85D \uC5F4\uAE30", "Open selected");
    public string RunRecordSummaryFormat => T("ThreeD.Workbench.RunRecordSummaryFormat", "Run Record \uC2A4\uD0A4\uB9C8 {0} | \uC21C\uC11C\uD615 \uB2E8\uACC4 {1}\uAC1C | \uCD5C\uC885 {2}", "Run Record schema {0} | Ordered steps: {1} | Overall: {2}");
    public string RunRecordThresholdCorrection => T("ThreeD.Workbench.RunRecordThresholdCorrection", "\uC784\uACC4\uAC12 \uAD50\uC815 \uC99D\uAC70", "Threshold correction evidence");
    public string RunRecordThresholdCorrectionDetail => T("ThreeD.Workbench.RunRecordThresholdCorrectionDetail", "\uC2E4\uD589 \uC2DC\uC810\uC758 sidecar\uB97C \uC77D\uAE30 \uC804\uC6A9\uC73C\uB85C \uBCF4\uC874\uD569\uB2C8\uB2E4. \uC2E4\uD589\u00B7\uC801\uC6A9\u00B7\uC7AC\uC0DD\uC740 \uC218\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Read-only sidecar snapshot at run time; it never executes, applies, or replays inspection.");
    public string RunRecordOpenFailed => T("ThreeD.Message.RunRecordOpenFailed", "\uC2E4\uD589 \uAE30\uB85D\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. JSON \uD30C\uC77C\uACFC \uC2A4\uD0A4\uB9C8\uB97C \uD655\uC778\uD558\uC138\uC694.", "The Run Record could not be read. Check the JSON file and schema.");
    public string RunRecordExportedFormat => T("ThreeD.Message.RunRecordExportedFormat", "\uC2E4\uD589 \uAE30\uB85D \uBB36\uC74C\uC744 \uB0B4\uBCF4\uB0C8\uC2B5\uB2C8\uB2E4: {0}", "Run Record bundle exported: {0}");
    public string ResultsWorkspaceTitle => T("ThreeD.Results.Title", "\uACB0\uACFC \uAC80\uD1A0", "Results review");
    public string ResultsWorkspaceDetail => T("ThreeD.Results.Detail", "\uAE30\uB85D\uB41C \uC2E4\uD589\u00B7\uCD9C\uB825\u00B7\uBCF4\uACE0\uC11C \uC99D\uAC70\uB97C \uC77D\uAE30 \uC804\uC6A9\uC73C\uB85C \uAC80\uD1A0\uD569\uB2C8\uB2E4.", "Review recorded run, output, and report evidence without changing the recipe.");
    public string ResultsWorkspaceRunRecord => T("ThreeD.Results.RunRecord", "\uC2E4\uD589 \uAE30\uB85D", "Run record");
    public string ResultsWorkspaceOutputCompare => T("ThreeD.Results.OutputCompare", "\uCD9C\uB825 \uBE44\uAD50", "Output compare");
    public string ResultsWorkspaceReports => T("ThreeD.Results.Reports", "\uBCF4\uACE0\uC11C\u00B7\uB0B4\uBCF4\uB0B4\uAE30", "Reports & export");
    public string ResultsWorkspaceReportsDetail => T("ThreeD.Results.ReportsDetail", "\uD604\uC7AC \uC2E4\uD589\uC758 JSON, HTML, CSV \uC99D\uAC70\uB97C \uC5F4\uAC70\uB098 \uACB0\uACFC \uBB36\uC74C\uC73C\uB85C \uB0B4\uBCF4\uB0C5\uB2C8\uB2E4.", "Open the current JSON, HTML, and CSV evidence or export the complete result bundle.");
    public string ResultsWorkspaceAdvanced => T("ThreeD.Results.Advanced", "\uACE0\uAE09 \uC9C4\uB2E8", "Advanced diagnostics");
    public string ResultsWorkspaceImmutable => T("ThreeD.Results.Immutable", "\uC774 \uD654\uBA74\uC758 \uC2E4\uD589 \uC99D\uAC70\uB294 \uC77D\uAE30 \uC804\uC6A9\uC785\uB2C8\uB2E4. \uD2F0\uCE6D\uACFC \uD30C\uB77C\uBBF8\uD130 \uC218\uC815\uC740 \uD2F0\uCE6D \uD654\uBA74\uC5D0\uC11C\uB9CC \uC218\uD589\uD558\uC138\uC694.", "Recorded evidence is read-only. Return to Teach to change regions or parameters.");
    public string ResultsOperatorSummaryTitle => T("ThreeD.Results.OperatorSummaryTitle", "\uC791\uC5C5\uC790 \uACB0\uACFC \uC694\uC57D", "Operator result summary");
    public string ResultsOperatorDecision => T("ThreeD.Results.OperatorDecision", "\uCD5C\uC885 \uD310\uC815\uACFC \uD575\uC2EC \uCE21\uC815\uAC12", "Decision and key measurement");
    public string ResultsOperatorAffectedSteps => T("ThreeD.Results.OperatorAffectedSteps", "\uC2E4\uD589 \uB2E8\uACC4", "Executed steps");
    public string ResultsOperatorNextAction => T(
        "ThreeD.Results.OperatorNextAction",
        "\uC2E4\uD328\uAC00 \uC788\uC73C\uBA74 \uC2E4\uD589 \uAE30\uB85D\uC744 \uD655\uC778\uD558\uACE0 \uD2F0\uCE6D\uC5D0\uC11C \uC218\uC815\uD558\uC138\uC694. \uACE0\uAE09 \uC9C4\uB2E8\uC740 \uCD94\uAC00 \uBD84\uC11D\uC774 \uD544\uC694\uD560 \uB54C\uB9CC \uC5FD\uB2C8\uB2E4.",
        "If a step failed, review the Run Record and fix it in Teach. Open Advanced only when deeper diagnosis is needed.");
    public string ValidationSet => T("ThreeD.Workbench.ValidationSet", "\uBC18\uBCF5 \uAC80\uC99D", "Validation Set");
    public string ValidationSetTitle => T("ThreeD.Workbench.ValidationSetTitle", "\uB2E4\uC911 \uC0D8\uD50C \uBC18\uBCF5 \uAC80\uC99D", "Multi-sample repeat validation");
    public string ValidationSetDetail => T("ThreeD.Workbench.ValidationSetDetail", "\uD2F0\uCE6D\uB41C \uB808\uC2DC\uD53C\uB97C \uC120\uD0DD\uD55C C3D \uC0D8\uD50C\uC5D0 \uC21C\uC11C\uB300\uB85C \uC2E4\uD589\uD569\uB2C8\uB2E4. \uC0D8\uD50C \uC120\uD0DD\uC740 3D \uBDF0\uC5B4\uC758 \uD45C\uC2DC \uADFC\uAC70\uB9CC \uBC14\uAFB8\uBA70, \uB808\uC2DC\uD53C\uB97C \uBCC0\uACBD\uD558\uAC70\uB098 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Run the taught recipe sequentially on selected C3D samples. Selecting a sample only changes the evidence shown in the 3D Viewer; it never changes the recipe or runs inspection.");
    public string ValidationSetAddSamples => T("ThreeD.Command.ValidationSetAddSamples", "\uC0D8\uD50C \uCD94\uAC00", "Add samples");
    public string ValidationSetAddCurrentInput => T("ThreeD.Command.ValidationSetAddCurrentInput", "\uD604\uC7AC \uC785\uB825 \uCD94\uAC00", "Add current input");
    public string ValidationSetRunAll => T(
        "ThreeD.Command.ValidationSetRunAll",
        "\uC0D8\uD50C \uC138\uD2B8 \uC2E4\uD589",
        "Run sample set");
    public string ValidationSetRunAllHint => T(
        "ThreeD.Command.ValidationSetRunAllHint",
        "\uAC80\uC99D \uC0D8\uD50C \uC138\uD2B8\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C \uC2E4\uD589\uD569\uB2C8\uB2E4. Tab\uC73C\uB85C \uC774\uB3D9\uD55C \uB4A4 Space \uB610\uB294 Enter\uB85C \uC2E4\uD589\uD558\uC138\uC694.",
        "Explicitly run the validation sample set. Focus it with Tab, then press Space or Enter.");
    public string ValidationSetClear => T("ThreeD.Command.ValidationSetClear", "\uBAA9\uB85D \uBE44\uC6B0\uAE30", "Clear list");
    public string ValidationSetSamples => T("ThreeD.Workbench.ValidationSetSamples", "\uAC80\uC99D \uC0D8\uD50C", "Validation samples");
    public string ValidationSetSelectedRecord => T("ThreeD.Workbench.ValidationSetSelectedRecord", "\uC120\uD0DD \uC0D8\uD50C \uC2E4\uD589 \uAE30\uB85D", "Selected sample record");
    public string ValidationSetNoSamples => T("ThreeD.Workbench.ValidationSetNoSamples", "\uC544\uC9C1 \uAC80\uC99D \uC0D8\uD50C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.", "No validation samples have been added.");
    public string ValidationSetNoSelection => T("ThreeD.Workbench.ValidationSetNoSelection", "\uC0D8\uD50C\uC744 \uC120\uD0DD\uD558\uBA74 \uB2E8\uACC4\uBCC4 \uC2E4\uD589 \uADFC\uAC70\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.", "Select a sample to inspect its step-by-step evidence.");
    public string ValidationSetFile => T("ThreeD.Column.ValidationSetFile", "\uD30C\uC77C", "File");
    public string ValidationSetDuration => T("ThreeD.Column.ValidationSetDuration", "\uC2E4\uD589 \uC2DC\uAC04", "Duration");
    public string ValidationSetCoverage => T("ThreeD.Workbench.ValidationSetCoverage", "\uC2E4\uD589 \uC801\uC6A9 \uBC94\uC704", "Execution coverage");
    public string ValidationSetFilterAll => T("ThreeD.Command.ValidationSetFilterAll", "\uC804\uCCB4", "All");
    public string ValidationSetFilterPass => T("ThreeD.Command.ValidationSetFilterPass", "\uD1B5\uACFC", "Pass");
    public string ValidationSetFilterFail => T("ThreeD.Command.ValidationSetFilterFail", "\uC2E4\uD328", "Fail");
    public string ValidationSetFilterError => T("ThreeD.Command.ValidationSetFilterError", "\uC624\uB958", "Error");
    public string ValidationSetPreviousIssue => T("ThreeD.Command.ValidationSetPreviousIssue", "\uC774\uC804 \uBB38\uC81C", "Previous issue");
    public string ValidationSetNextIssue => T("ThreeD.Command.ValidationSetNextIssue", "\uB2E4\uC74C \uBB38\uC81C", "Next issue");
    public string ValidationSetOpenComparison => T("ThreeD.Command.ValidationSetOpenComparison", "3D \uBE44\uAD50 \uC5F4\uAE30", "Open 3D comparison");
    public string ValidationSetCancel => T("ThreeD.Command.ValidationSetCancel", "\uC2E4\uD589 \uCDE8\uC18C", "Cancel run");
    public string ValidationSetMetrics => T("ThreeD.Workbench.ValidationSetMetrics", "\uCE21\uC815\uAC12", "Metrics");
    public string ValidationSetRole => T("ThreeD.Workbench.ValidationSetRole", "\uC5ED\uD560", "Role");
    public string ValidationSetLabeledEvidence => T("ThreeD.Workbench.ValidationSetLabeledEvidence", "\uB77C\uBCA8 \uC0D8\uD50C \uBD84\uD3EC \uC99D\uAC70", "Labeled sample distributions");
    public string ValidationSetScope => T("ThreeD.Workbench.ValidationSetScope", "\uBC94\uC704", "Scope");
    public string ValidationSetOwner => T("ThreeD.Workbench.ValidationSetOwner", "\uB2E8\uACC4 / ROI", "Step / ROI");
    public string ValidationSetMetric => T("ThreeD.Workbench.ValidationSetMetric", "\uCE21\uC815\uAC12", "Metric");
    public string ValidationSetThresholdCandidates => T("ThreeD.Workbench.ValidationSetThresholdCandidates", "\uC784\uACC4\uAC12 \uD6C4\uBCF4\uC640 \uC624\uB958\uD45C", "Threshold candidates and error table");
    public string ValidationSetThresholdReadOnly => T("ThreeD.Workbench.ValidationSetThresholdReadOnly", "\uC120\uD0DD\uC740 \uC77D\uAE30 \uC804\uC6A9 \u00B7 \uC801\uC6A9\uC740 PropertyGrid \uCD08\uC548\uB9CC \uBCC0\uACBD", "Selection is read-only \u00B7 Apply changes the PropertyGrid draft only");
    public string ValidationSetThresholdReview => T("ThreeD.Command.ValidationSetThresholdReview", "\uAC80\uD1A0", "Review");
    public string ValidationWorkspaceSamples => T(
        "ThreeD.Workbench.ValidationWorkspaceSamples",
        "\uC0D8\uD50C",
        "Samples");
    public string ValidationWorkspaceResults => T(
        "ThreeD.Workbench.ValidationWorkspaceResults",
        "\uC2E4\uD589 \uACB0\uACFC",
        "Run results");
    public string ValidationWorkspaceFailures => T(
        "ThreeD.Workbench.ValidationWorkspaceFailures",
        "\uC2E4\uD328 \uBD84\uC11D",
        "Failure analysis");
    public string ValidationWorkspaceThresholds => T(
        "ThreeD.Workbench.ValidationWorkspaceThresholds",
        "\uC784\uACC4\uAC12 \uAC80\uD1A0",
        "Threshold review");
    public string ValidationWorkspaceHeldOut => T(
        "ThreeD.Workbench.ValidationWorkspaceHeldOut",
        "Held-out",
        "Held-out");
    public string ValidationWorkspaceOpenInTeach => T(
        "ThreeD.Workbench.ValidationWorkspaceOpenInTeach",
        "\uD2F0\uCE6D\uC5D0\uC11C \uC218\uC815",
        "Fix in Teach");
    public string ValidationWorkspaceOpenInTeachHint => T(
        "ThreeD.Workbench.ValidationWorkspaceOpenInTeachHint",
        "\uC120\uD0DD\uD55C \uC2E4\uD328 \uB2E8\uACC4\uB97C \uD2F0\uCE6D \uD654\uBA74\uC5D0\uC11C \uC5FD\uB2C8\uB2E4. \uB808\uC2DC\uD53C\uB97C \uBCC0\uACBD\uD558\uAC70\uB098 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
        "Open the selected failed step in Teach without changing or running the recipe.");
    public string ValidationFailureSummaryTitle => T("ThreeD.Validation.FailureSummaryTitle", "\uC9C0\uAE08 \uC218\uC815\uD560 \uC2E4\uD328", "Failure to correct now");
    public string ValidationFailureSample => T("ThreeD.Validation.FailureSample", "\uC2E4\uD328 \uC0D8\uD50C", "Failed sample");
    public string ValidationFailureRule => T("ThreeD.Validation.FailureRule", "\uC2E4\uD328 \uADDC\uCE59", "Failed rule");
    public string ValidationFailureReason => T("ThreeD.Validation.FailureReason", "\uC2E4\uD328 \uC774\uC720", "Reason");
    public string ValidationFailureNextAction => T("ThreeD.Validation.FailureNextAction", "\uB2E4\uC74C \uC791\uC5C5", "Next action");
    public string ValidationFailureNextActionDetail => T(
        "ThreeD.Validation.FailureNextActionDetail",
        "\uC601\uD5A5\uC744 \uBC1B\uC740 \uD615\uC0C1\uC744 \uD655\uC778\uD55C \uB4A4 '\uD2F0\uCE6D\uC5D0\uC11C \uC218\uC815'\uC744 \uC120\uD0DD\uD558\uC138\uC694. \uAE30\uC220 \uCE21\uC815\uAC12\uACFC \uC624\uBC84\uB808\uC774\uB294 \uC544\uB798\uC5D0\uC11C \uAC80\uD1A0\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
        "Review the affected geometry, then choose Fix in Teach. Technical metrics and overlays remain available below.");
    public string ValidationSetThresholdCancelReview => T("ThreeD.Command.ValidationSetThresholdCancelReview", "\uAC80\uD1A0 \uCDE8\uC18C", "Cancel review");
    public string ValidationSetThresholdApplyDraft => T("ThreeD.Command.ValidationSetThresholdApplyDraft", "\uCD08\uC548\uC5D0 \uC801\uC6A9", "Apply to draft");
    public string ValidationSetThresholdRevalidateDevelopment => T("ThreeD.Command.ValidationSetThresholdRevalidateDevelopment", "\uAC1C\uBC1C \uC7AC\uAC80\uC99D", "Revalidate development");
    public string ValidationSetThresholdReplayHeldOut => T("ThreeD.Command.ValidationSetThresholdReplayHeldOut", "Held-out \uC7AC\uC2E4\uD589", "Replay Held-out");
    public string ValidationSetLimits => T("ThreeD.Workbench.ValidationSetLimits", "\uD55C\uACC4", "Limits");
    public string ValidationSetCorrect => T("ThreeD.Workbench.ValidationSetCorrect", "\uC815\uB2F5", "Correct");
    public string ValidationSetErrors => T("ThreeD.Workbench.ValidationSetErrors", "\uC624\uB958", "Errors");
    public string ValidationSetFalseAccept => T("ThreeD.Workbench.ValidationSetFalseAccept", "\uC624\uD310\uC815 \uD1B5\uACFC", "False accept");
    public string ValidationSetFalseReject => T("ThreeD.Workbench.ValidationSetFalseReject", "\uC815\uC0C1 \uAC70\uBD80", "False reject");
    public string ValidationSetExpected => T("ThreeD.Workbench.ValidationSetExpected", "\uAE30\uB300", "Expected");
    public string ValidationSetPredicted => T("ThreeD.Workbench.ValidationSetPredicted", "\uD310\uC815", "Predicted");
    public string ValidationSetDecision => T("ThreeD.Workbench.ValidationSetDecision", "\uACB0\uACFC", "Decision");
    public string AdvancedIdentityAndOrder => T("ThreeD.Workbench.AdvancedIdentityAndOrder", "\uACE0\uAE09 \uC2DD\uBCC4\uC790 \uBC0F \uC21C\uC11C", "Advanced identity & order");
    public string StepName => T("ThreeD.Workbench.StepName", "\uAC80\uC0AC \uC774\uB984", "Inspection name");
    public string StepId => T("ThreeD.Workbench.StepId", "\uB2E8\uACC4 ID", "Step ID");
    public string ValidationSetOverlays => T("ThreeD.Workbench.ValidationSetOverlays", "\uC624\uBC84\uB808\uC774", "Overlays");
    public string ValidationSetNoMetrics => T("ThreeD.Workbench.ValidationSetNoMetrics", "\uC774 \uB2E8\uACC4\uC5D0\uB294 \uCE21\uC815\uAC12\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.", "This step has no metrics.");
    public string ValidationSetNoOverlays => T("ThreeD.Workbench.ValidationSetNoOverlays", "\uC774 \uB2E8\uACC4\uC5D0\uB294 \uC624\uBC84\uB808\uC774 \uC99D\uAC70\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", "This step has no overlay evidence.");
    public string ValidationSetComparisonHint => T(
        "ThreeD.Workbench.ValidationSetComparisonHint",
        "\uB808\uC2DC\uD53C \uC18C\uC2A4\uC640 \uC120\uD0DD \uC0D8\uD50C\uC744 \uB3C4\uD0B9 \uCD9C\uB825 \uBE44\uAD50\uC5D0 \uC5FD\uB2C8\uB2E4. \uB808\uC2DC\uD53C\uC640 \uBA54\uC778 3D \uBDF0 \uC785\uB825\uC740 \uBC14\uB00C\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
        "Open the recipe source and selected sample in docked Output Compare. The recipe and main 3D Viewer input are not changed.");
    public string ColumnEvidence => T("ThreeD.Column.Evidence", "\uC2E4\uD589 \uADFC\uAC70", "Execution evidence");
    public string SessionLog => T("ThreeD.Workbench.SessionLog", "\uC138\uC158 \uB85C\uADF8", "Session Log");
    public string HeightProfile => T("ThreeD.Workbench.HeightProfile", "\uB192\uC774 \uD504\uB85C\uD30C\uC77C", "Height Profile");
    public string FitDiagnostics => T("ThreeD.Workbench.FitDiagnostics", "\uD53C\uD305 \uC9C4\uB2E8", "Fit Diagnostics");
    public string IntersectionEvidence => T("ThreeD.Workbench.IntersectionEvidence", "\uAD50\uCC28\uC810 \uC99D\uAC70", "Intersection Evidence");
    public string CorrespondenceEvidence => T("ThreeD.Workbench.CorrespondenceEvidence", "\uB300\uC751 \uC99D\uAC70", "Correspondence Evidence");
    public string OutputCompare => T("ThreeD.Workbench.OutputCompare", "\uCD9C\uB825 \uBE44\uAD50", "Output Compare");
    public string OutputCompareTitle => T("ThreeD.Workbench.OutputCompareTitle", "\uD604\uC7AC \uC0B0\uCD9C\uBB3C \uBE44\uAD50", "Compare current outputs");
    public string OutputCompareDetail => T("ThreeD.Workbench.OutputCompareDetail", "\uC2E4\uC81C\uB85C \uB85C\uB4DC\uB41C \uC18C\uC2A4\uC640 \uC0B0\uCD9C\uBB3C\uB9CC \uB3C4\uD0B9 \uBE44\uAD50 \uC2AC\uB86F\uC5D0 \uACE0\uC815\uD569\uB2C8\uB2E4. \uC120\uD0DD\uC740 \uB808\uC2DC\uD53C\uB97C \uC218\uC815\uD558\uAC70\uB098 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Pin only currently loaded sources or outputs to docked compare slots. Selection never edits or executes the recipe.");
    public string OutputCompareNoSelection => T("ThreeD.Workbench.OutputCompareNoSelection", "\uACE0\uC815\uB41C \uC0B0\uCD9C\uBB3C \uC5C6\uC74C", "No output pinned");
    public string OutputComparePinnedOutput => T("ThreeD.Workbench.OutputComparePinnedOutput", "\uACE0\uC815 \uC0B0\uCD9C\uBB3C", "Pinned output");
    public string DisplayedOutputs => T("ThreeD.Workbench.DisplayedOutputs", "\uD45C\uC2DC \uC0B0\uCD9C\uBB3C", "Displayed Outputs");
    public string DisplayedOutputsTitle => T("ThreeD.Workbench.DisplayedOutputsTitle", "\uC0B0\uCD9C\uBB3C \uD45C\uC2DC \uAD00\uB9AC", "Displayed Outputs / Overlay Manager");
    public string DisplayedOutputsDetail => T("ThreeD.Workbench.DisplayedOutputsDetail", "\uC2E4\uC81C C3D \uC0B0\uCD9C\uBB3C\uB9CC 3D \uBDF0\uC5D0 \uD45C\uC2DC\uD558\uAC70\uB098 \uBE44\uAD50\uC5D0 \uACE0\uC815\uD569\uB2C8\uB2E4. \uD53C\uCC98 \uC0B0\uCD9C\uBB3C\uC740 \uAC00\uC9DC \uD45C\uBA74 \uC5C6\uC774 \uC99D\uAC70\uB85C\uB9CC \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.", "Show or pin only existing C3D artifacts. Feature outputs stay evidence-only; no synthetic surface is created.");
    public string DisplayedOutputsNoViewerSelection => T("ThreeD.Workbench.DisplayedOutputsNoViewerSelection", "\uC0B0\uCD9C\uBB3C \uAD00\uB9AC\uC790\uC5D0\uC11C \uC120\uD0DD\uB41C 3D \uD45C\uC2DC \uC5C6\uC74C", "No 3D display selected by Output Manager");
    public string CurrentViewerDisplay => T("ThreeD.Workbench.CurrentViewerDisplay", "\uD604\uC7AC 3D \uBDF0 \uD45C\uC2DC", "Current 3D Viewer display");
    public string DisplayedInViewer => T("ThreeD.Workbench.DisplayedInViewer", "\uBDF0\uC5B4\uC5D0 \uD45C\uC2DC\uB428", "Displayed in Viewer");
    public string ShowInViewer => T("ThreeD.Command.ShowInViewer", "3D \uBDF0\uC5D0 \uD45C\uC2DC", "Show in 3D View");
    public string PinToCompare => T("ThreeD.Command.PinToCompare", "\uBE44\uAD50\uC5D0 \uACE0\uC815", "Pin to Compare");
    public string FocusStep => T("ThreeD.Command.FocusStep", "\uB2E8\uACC4 \uD3EC\uCEE4\uC2A4", "Focus Step");
    public string DisplayedOutputsSummaryFormat => T("ThreeD.Workbench.DisplayedOutputsSummaryFormat", "\uD45C\uC2DC \uAC00\uB2A5 {0} | \uC99D\uAC70 \uC804\uC6A9 {1}", "{0} renderable | {1} evidence-only");
    public string DisplayableC3DData => T("ThreeD.Workbench.DisplayableC3DData", "C3D \uBDF0\uC5B4 \uB370\uC774\uD130 \uC0AC\uC6A9 \uAC00\uB2A5", "C3D viewer data available");
    public string EvidenceOnlyOutput => T("ThreeD.Workbench.EvidenceOnlyOutput", "\uC99D\uAC70 \uC804\uC6A9: \uAC00\uC9DC 3D \uD45C\uBA74\uC744 \uB9CC\uB4E4\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4", "Evidence only; no synthetic 3D surface is created");
    public string NoCurrentDisplayableOutput => T("ThreeD.Workbench.NoCurrentDisplayableOutput", "\uD604\uC7AC \uD45C\uC2DC \uAC00\uB2A5\uD55C \uC0B0\uCD9C\uBB3C \uC5C6\uC74C", "No current displayable output");
    public string PinnedSlotsFormat => T("ThreeD.Workbench.PinnedSlotsFormat", "\uBE44\uAD50 \uC2AC\uB86F {0}\uC5D0 \uACE0\uC815", "Pinned to comparison slot {0}");
    public string FlowMap => T("ThreeD.Workbench.FlowMap", "\uD750\uB984 \uB9F5", "Flow Map");
    public string FlowMapTitle => T("ThreeD.Workbench.FlowMapTitle", "\uC785\uB825 \u2192 \uB3C4\uAD6C \u2192 \uC815\uC2DD \uCD9C\uB825", "Input → Tool → Typed output");
    public string FlowMapDetail => T("ThreeD.Workbench.FlowMapDetail", "\uD2B8\uB9AC \uC120\uD0DD\uACFC \uB3D9\uAE30\uD654\uB41C \uC77D\uAE30 \uC804\uC6A9 \uB370\uC774\uD130 \uACBD\uB85C\uC785\uB2C8\uB2E4. \uC5F0\uACB0\uC744 \uC218\uC815\uD558\uAC70\uB098 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Read-only data routes synchronized with the tree. It does not edit connections or run a tool.");
    public string FlowMapReadOnly => T("ThreeD.Workbench.FlowMapReadOnly", "\uC77D\uAE30 \uC804\uC6A9 \u2022 \uC5F0\uACB0 \uC218\uC815 \uC5C6\uC74C", "Read-only • no connection edits");
    public string FlowMapInput => T("ThreeD.Workbench.FlowMapInput", "\uC785\uB825 \uD3EC\uD2B8", "Input port");
    public string FlowMapOutput => T("ThreeD.Workbench.FlowMapOutput", "\uCD9C\uB825 \uD3EC\uD2B8", "Output port");
    public string FlowMapEmptyHint => T("ThreeD.Workbench.FlowMapEmptyHint", "\uD2F0\uCE6D\uB41C \uB3C4\uAD6C \uB2E8\uACC4\uAC00 \uC5C6\uC5B4 \uD750\uB984\uC744 \uD45C\uC2DC\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.", "No taught tool steps are available to map.");
    public string NavigatorHint => T("ThreeD.Workbench.NavigatorHint", "\uB808\uC2DC\uD53C \uD0D0\uC0C9\uAE30\uB294 \uC77D\uAE30 \uC6B0\uC120\uC785\uB2C8\uB2E4. \uD30C\uC774\uD504\uB77C\uC778 \uB178\uB4DC\uB97C \uC120\uD0DD\uD574 \uD574\uB2F9 \uB2E8\uACC4 \uD30C\uB77C\uBBF8\uD130\uB97C \uD655\uC778\uD558\uACE0, \uBBF8\uB9AC\uBCF4\uAE30\uC640 \uAC8C\uC2DC\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C \uC2E4\uD589\uD558\uC138\uC694.", "Recipe Navigator is read-first. Select a pipeline node to focus its typed Step Parameters; Preview and Publish remain explicit.");
    public string RecipeSource => T("ThreeD.Workbench.RecipeSource", "\uB808\uC2DC\uD53C \uC18C\uC2A4", "Recipe source");
    public string RecipeNavigator => T("ThreeD.Workbench.RecipeNavigator", "\uAC80\uC0AC \uB2E8\uACC4 \uBAA9\uB85D", "Inspection steps");
    public string CompatibleToolCatalogTitle => T("ThreeD.Workbench.CompatibleToolCatalogTitle", "\uD638\uD658 \uB2E4\uC74C \uB3C4\uAD6C", "Compatible next tools");
    public string CompatibleToolCatalogDetail => T("ThreeD.Workbench.CompatibleToolCatalogDetail", "\uD604\uC7AC \uC785\uB825\uB9CC \uD655\uC778\uD569\uB2C8\uB2E4. \uB3C4\uAD6C \uC120\uD0DD\uC740 \uB2E8\uACC4\u00B7\uC5F0\uACB0\u00B7\uC2E4\uD589\uC744 \uBC14\uAFB8\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Checks current inputs only. Selecting never adds, connects, or runs.");
    public string CompatibleToolCatalogSummaryFormat => T("ThreeD.Workbench.CompatibleToolCatalogSummaryFormat", "\uD638\uD658 \uB3C4\uAD6C {0}\uAC1C", "{0} compatible");
    public string CompatibleToolCatalogEmpty => T("ThreeD.Workbench.CompatibleToolCatalogEmpty", "\uB2E4\uC74C \uB3C4\uAD6C \uCD94\uCC9C\uC744 \uC704\uD55C \uD604\uC7AC \uC785\uB825\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.", "No current inputs are available for a next-tool suggestion.");
    public string SelectCompatibleTool => T("ThreeD.Command.SelectCompatibleTool", "\uD234\uBC15\uC2A4\uC5D0\uC11C \uC120\uD0DD", "Select in Toolbox");
    public string AddCompatibleTool => T("ThreeD.Command.AddCompatibleTool", "\uCD94\uAC00", "Add");
    public string AddCompatibleToolToolTip => T("ThreeD.Command.AddCompatibleToolToolTip", "\uD45C\uC2DC\uB41C \uC785\uB825\uC73C\uB85C \uAC80\uC0AC \uB2E8\uACC4\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C \uCD94\uAC00\uD569\uB2C8\uB2E4. \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Explicitly add a step with the displayed inputs. It does not run.");
    public string CompatibleToolBlockerLabel => T("ThreeD.Workbench.CompatibleToolBlockerLabel", "\uB2E4\uC74C \uBBF8\uCDA9\uC871 \uC785\uB825", "Next missing input");
    public string CompatibleToolBlockerDetailFormat => T("ThreeD.Workbench.CompatibleToolBlockerDetailFormat", "{0}: {1} \uD544\uC694", "{0}: requires {1}");
    public string AddInspectionStep => T("ThreeD.Workbench.AddInspectionStep", "\uAC80\uC0AC \uB2E8\uACC4 \uCD94\uAC00", "Add inspection step");
    public string StepProperties => T("ThreeD.Workbench.StepProperties", "\uB2E8\uACC4 \uC18D\uC131", "Step properties");
    public string NoRecipeStepSelected => T("ThreeD.Workbench.NoRecipeStepSelected", "\uB2E8\uACC4 \uC124\uC815 \uB300\uAE30", "Step setup is waiting");
    public string NoRecipeStepSelectedDetail => T("ThreeD.Workbench.NoRecipeStepSelectedDetail", "\uB3C4\uAD6C \uB77C\uC774\uBE0C\uB7EC\uB9AC\uC5D0\uC11C \uB2E8\uACC4\uB97C \uCD94\uAC00\uD558\uBA74 \uC5EC\uAE30\uC5D0 \uC785\uB825, \uD30C\uB77C\uBBF8\uD130, \uCD9C\uB825 \uC124\uC815\uC774 \uD45C\uC2DC\uB429\uB2C8\uB2E4.", "Add a step from Tool Library to show its Inputs, Parameters, and Outputs here.");
    public string RecipePipelineTeachReview => T("ThreeD.Workbench.RecipePipelineTeachReview", "\uB808\uC2DC\uD53C \uD30C\uC774\uD504\uB77C\uC778 / \uD2F0\uCE6D \uAC80\uD1A0", "Recipe Pipeline / Teach Review");
    public string Validate => T("ThreeD.Command.Validate", "\uAC80\uC99D", "Validate");
    public string MoveUp => T("ThreeD.Command.MoveUp", "\uC704\uB85C", "Up");
    public string MoveDown => T("ThreeD.Command.MoveDown", "\uC544\uB798\uB85C", "Down");
    public string Remove => T("ThreeD.Command.Remove", "\uC0AD\uC81C", "Remove");
    public string ColumnNumber => T("ThreeD.Column.Number", "\uBC88\uD638", "#");
    public string ColumnTool => T("ThreeD.Column.Tool", "\uB3C4\uAD6C", "Tool");
    public string ColumnInputs => T("ThreeD.Column.Inputs", "\uC785\uB825", "Inputs");
    public string ColumnTypedOutput => T("ThreeD.Column.TypedOutput", "\uC815\uC2DD \uCD9C\uB825", "Typed output");
    public string ColumnState => T("ThreeD.Column.State", "\uC0C1\uD0DC", "State");
    public string Preview => T("ThreeD.Command.Preview", "\uBBF8\uB9AC\uBCF4\uAE30", "Preview");
    public string Run => T("ThreeD.Command.Run", "\uC2E4\uD589", "Run");
    public string Publish => T("ThreeD.Command.Publish", "\uAC8C\uC2DC", "Publish");
    public string Cancel => T("ThreeD.Command.Cancel", "\uCDE8\uC18C", "Cancel");
    public string SelectedPaletteItem => T("ThreeD.Workbench.SelectedPaletteItem", "\uC120\uD0DD\uB41C \uD234 \uD56D\uBAA9", "Selected palette item");
    public string Input => T("ThreeD.Label.Input", "\uC785\uB825", "Input");
    public string Output => T("ThreeD.Label.Output", "\uCD9C\uB825", "Output");
    public string ParameterAdapter => T("ThreeD.Label.ParameterAdapter", "\uD30C\uB77C\uBBF8\uD130 \uC5B4\uB311\uD130", "Parameter adapter");
    public string Inputs => T("ThreeD.Label.Inputs", "\uC785\uB825", "Inputs");
    public string InputParameterOutputSummary => T("ThreeD.Label.InputParameterOutputSummary", "\uC785\uB825 \u2192 \uD30C\uB77C\uBBF8\uD130 \u2192 \uCD9C\uB825", "Inputs → Parameters → Output");
    public string TypedParameters => T("ThreeD.Label.TypedParameters", "\uC815\uC2DD \uD30C\uB77C\uBBF8\uD130", "Typed parameters");
    public string StepPropertiesEditDetail => T("ThreeD.Workbench.StepPropertiesEditDetail", "\uD3B8\uC9D1\uC740 \uC791\uC131\uB41C \uB808\uC2DC\uD53C\uB9CC \uBC14\uAFC9\uB2C8\uB2E4. \uC9C0\uC6D0\uB41C \uC815\uC2DD \uB2E8\uACC4\uB294 \uBBF8\uB9AC\uBCF4\uAE30\uB85C \uBA85\uC2DC\uC801\uC73C\uB85C \uC2E4\uD589\uD569\uB2C8\uB2E4.", "Editing changes only the authored recipe. Use Preview explicitly to execute a supported typed step.");
    public string Discard => T("ThreeD.Command.Discard", "\uBC84\uB9AC\uAE30", "Discard");
    public string ApplyParameters => T("ThreeD.Command.ApplyParameters", "\uD30C\uB77C\uBBF8\uD130 \uC801\uC6A9", "Apply parameters");
    public string Produces => T("ThreeD.Label.Produces", "\uC0B0\uCD9C \uC815\uC2DD", "Produces");
    public string OutputEntity => T("ThreeD.Label.OutputEntity", "\uCD9C\uB825 \uC5D4\uD2F0\uD2F0", "Output entity");
    public string ExpectedData => T("ThreeD.Label.ExpectedData", "\uAE30\uB300 \uB370\uC774\uD130", "Expected data");
    public string InputEntities => T("ThreeD.Label.InputEntities", "\uC785\uB825 \uC5D4\uD2F0\uD2F0(\uC138\uBBF8\uCF5C\uB860\uC73C\uB85C \uAD6C\uBD84)", "Input entities (separate with ;)");
    public string ToolboxSequenceHint => T("ThreeD.Workbench.ToolboxSequenceHint", "\uB808\uC2DC\uD53C\uB97C \uC21C\uC11C\uB300\uB85C \uAD6C\uC131\uD558\uC138\uC694: \uC900\uBE44, \uD53C\uCC98, \uAD6C\uC131, \uC815\uB82C, \uCE21\uC815, \uAC80\uD1A0.", "Build the recipe in order: prepare, feature, construct, align, measure, then review.");
    public string SelectedRoute => T("ThreeD.Workbench.SelectedRoute", "\uC120\uD0DD\uD55C \uAC80\uC0AC \uB2E8\uACC4", "Selected inspection step");
    public string SelectedStepActionsHint => T("ThreeD.Workbench.SelectedStepActionsHint", "\uC774 \uB2E8\uACC4\uC758 \uC0C1\uC138 \uC124\uC815\uC744 \uC5F4\uAC70\uB098 \uC21C\uC11C\uB97C \uBC14\uAFB8\uACE0, \uD544\uC694 \uC5C6\uC73C\uBA74 \uC0AD\uC81C\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "Open this step's detailed settings, change its order, or remove it.");
    public string OpenSelectedToolLab => T("ThreeD.Command.OpenSelectedToolLab", "\uC0C1\uC138 \uC124\uC815 \uC5F4\uAE30", "Open detailed settings");
    public string RemoveStep => T("ThreeD.Command.RemoveStep", "\uB2E8\uACC4 \uC0AD\uC81C", "Remove step");
    public string ToolLabReview => T("ThreeD.ToolLab.Review", "\uD30C\uB77C\uBBF8\uD130 \uBC0F \uC2E4\uD589 \uADFC\uAC70", "Parameters & execution evidence");
    public string ToolLabReviewDetail => T("ThreeD.ToolLab.ReviewDetail", "\uD30C\uB77C\uBBF8\uD130\uB294 \uB808\uC2DC\uD53C \uCD08\uC548\uC5D0\uB9CC \uC801\uC6A9\uB429\uB2C8\uB2E4. \uBBF8\uB9AC\uBCF4\uAE30\uC640 \uAC8C\uC2DC\uB294 \uBA85\uC2DC\uC801\uC73C\uB85C \uC218\uD589\uD569\uB2C8\uB2E4.", "Edits stay in the recipe draft. Preview and Publish remain explicit.");
    public string ShowInput => T("ThreeD.Command.ShowInput", "\uC785\uB825 \uBCF4\uAE30", "Show input");
    public string TeachingSelections => T("ThreeD.Workbench.TeachingSelections", "\uD2F0\uCE6D \uC120\uD0DD \uC601\uC5ED", "Teaching selections");
    public string ThicknessRoiTeaching => T("ThreeD.Workbench.ThicknessRoiTeaching", "\uB450\uAED8 \uAC80\uC0AC ROI \uD2F0\uCE6D", "Thickness ROI teaching");
    public string ThicknessRoiTeachingDetail => T(
        "ThreeD.Workbench.ThicknessRoiTeachingDetail",
        "1. \uAE30\uC900\uBA74 ROI\uB97C \uC9C0\uC815\uD558\uACE0 \uC801\uC6A9\uD569\uB2C8\uB2E4. 2. \uCE21\uC815\uBA74 ROI\uB97C \uC9C0\uC815\uD558\uACE0 \uC801\uC6A9\uD569\uB2C8\uB2E4. 3. \uD5C8\uC6A9\uAC12\uC744 \uC124\uC815\uD55C \uB4A4 \uBBF8\uB9AC\uBCF4\uAE30\uB97C \uB204\uB985\uB2C8\uB2E4. \uACB0\uACFC\uB294 \uAE30\uC900\uBA74 \uD3C9\uBA74\uC5D0\uC11C \uCE21\uC815\uBA74\uAE4C\uC9C0\uC758 H\uCD95 \uAC70\uB9AC\uC785\uB2C8\uB2E4.",
        "1. Capture and Apply the Reference surface ROI. 2. Capture and Apply the Measurement surface ROI. 3. Set limits, then select Preview. The result is signed H-axis separation from the fitted reference surface.");
    public string ThicknessRoiReadyDetail => T(
        "ThreeD.Workbench.ThicknessRoiReadyDetail",
        "\uD604\uC7AC ROI\uAC00 \uB808\uC2DC\uD53C\uC5D0 \uC800\uC7A5\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4. \uBCC0\uACBD\uD558\uB824\uBA74 ROI \uAD50\uCCB4 \uD6C4 \uBDF0\uC5B4\uC5D0\uC11C \uB450 \uBAA8\uC11C\uB9AC\uB97C \uB2E4\uC2DC \uC9C0\uC815\uD558\uC138\uC694. \uD5C8\uC6A9\uAC12\uC744 \uD655\uC778\uD55C \uB4A4 \uBBF8\uB9AC\uBCF4\uAE30\uB97C \uB204\uB985\uB2C8\uB2E4.",
        "The current ROI is stored in the recipe. To change it, select Replace ROI and pick the two corners again. Check tolerances, then select Preview.");
    public string ThicknessMeasurementRoi => T("ThreeD.Workbench.ThicknessMeasurementRoi", "\uB450\uAED8 \uCE21\uC815 ROI", "Thickness measurement ROI");
    public string TwoGridCorners => T("ThreeD.Workbench.TwoGridCorners", "3D \uADF8\uB9AC\uB4DC \uBAA8\uC11C\uB9AC 2\uAC1C", "2 grid corners");
    public string RecipeOwnedSelection => T("ThreeD.Workbench.RecipeOwnedSelection", "\uB808\uC2DC\uD53C\uC5D0 \uC800\uC7A5\uB41C ROI / \uC120\uD0DD", "Recipe-owned ROI / selection");
    public string CaptureSelection => T("ThreeD.Command.CaptureSelection", "\uC120\uD0DD \uC601\uC5ED \uC9C0\uC815", "Capture selection");
    public string ReplaceSelection => T("ThreeD.Command.ReplaceSelection", "\uC120\uD0DD \uC601\uC5ED \uAD50\uCCB4", "Replace selection");
    public string RemoveSelection => T("ThreeD.Command.RemoveSelection", "\uC120\uD0DD \uC601\uC5ED \uC0AD\uC81C", "Remove selection");
    public string UseExistingSelection => T("ThreeD.Workbench.UseExistingSelection", "\uAE30\uC874 \uD638\uD658 ROI / \uC120\uD0DD \uC601\uC5ED \uC7AC\uC0AC\uC6A9", "Reuse an existing compatible ROI / selection");
    public string UseSelection => T("ThreeD.Command.UseSelection", "\uC7AC\uC0AC\uC6A9", "Reuse");
    public string UndoLastPoint => T("ThreeD.Command.UndoLastPoint", "\uB9C8\uC9C0\uB9C9 \uD3EC\uC778\uD2B8 \uCDE8\uC18C", "Undo last point");
    public string ApplySelection => T("ThreeD.Command.ApplySelection", "ROI / \uC120\uD0DD \uC601\uC5ED \uC801\uC6A9", "Apply ROI / selection");
    public string SurfaceRoiEditor => T("ThreeD.Workbench.SurfaceRoiEditor", "\uD45C\uBA74 ROI \uD3B8\uC9D1", "Surface ROI editing");
    public string SurfaceRoiEditorDetail => T(
        "ThreeD.Workbench.SurfaceRoiEditorDetail",
        "ROI \uAD50\uCCB4\uB97C \uC2DC\uC791\uD55C \uB4A4 Viewer \uD578\uB4E4\uB85C \uC774\uB3D9\u00B7\uD06C\uAE30 \uC870\uC808\uD558\uAC70\uB098 \uC544\uB798 \uAC12\uC744 \uD3B8\uC9D1\uD558\uC138\uC694. \uC801\uC6A9 \uC804\uAE4C\uC9C0 \uB808\uC2DC\uD53C\uC640 \uAC80\uC0AC \uACB0\uACFC\uB294 \uBCC0\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
        "Start Replace ROI, then move or resize with Viewer handles or edit the values below. The recipe and inspection result stay unchanged until Apply.");
    public string RoiRow => T("ThreeD.Workbench.RoiRow", "\uC2DC\uC791 Z (\uD589)", "Start Z (row)");
    public string RoiColumn => T("ThreeD.Workbench.RoiColumn", "\uC2DC\uC791 X (\uC5F4)", "Start X (column)");
    public string RoiRowCount => T("ThreeD.Workbench.RoiRowCount", "Z \uAE38\uC774 (\uD589)", "Z length (rows)");
    public string RoiColumnCount => T("ThreeD.Workbench.RoiColumnCount", "\uB108\uBE44 (\uC5F4)", "Width (columns)");
    public string SourceFrameFootprint => T("ThreeD.Workbench.SourceFrameFootprint", "\uC18C\uC2A4 \uD504\uB808\uC784 X/Z \uC601\uC5ED", "Source-frame X/Z footprint");
    public string SelectionCapture => T("ThreeD.Workbench.SelectionCapture", "ROI / \uC120\uD0DD \uC601\uC5ED \uC9C0\uC815", "ROI / selection capture");
    public string SelectionCaptureInactive => T("ThreeD.Workbench.SelectionCaptureInactive", "\uC9C0\uC815 \uB300\uAE30 \uC0C1\uD0DC", "Capture is inactive.");
    public string SelectionCaptureProgressFormat => T("ThreeD.Workbench.SelectionCaptureProgressFormat", "{0}/{1}\uAC1C \uC9C0\uC815 \u00B7 Esc\uB85C \uCDE8\uC18C", "{0}/{1} picked \u00B7 Esc cancels");
    public string RoiCaptureReadyProgress => T(
        "ThreeD.Workbench.RoiCaptureReadyProgress",
        "\uADF8\uB9AC\uAE30 \uC644\uB8CC \u00B7 \uAC80\uD1A0 \uBAA8\uB4DC \u00B7 Enter \uC801\uC6A9 \u00B7 Esc \uCDE8\uC18C",
        "Drawing complete \u00B7 Review mode \u00B7 Enter applies \u00B7 Esc cancels");
    public string RoiCaptureStartInstruction => T(
        "ThreeD.Workbench.RoiCaptureStartInstruction",
        "\uD45C\uBA74\uC5D0\uC11C \uCCAB \uBAA8\uC11C\uB9AC\uB97C \uC88C\uD074\uB9AD\uD558\uAC70\uB098, \uC6D0\uD558\uB294 ROI\uB97C \uB300\uAC01\uC120\uC73C\uB85C \uB4DC\uB798\uADF8\uD558\uC138\uC694.",
        "Left-click the first surface corner, or drag diagonally across the ROI.");
    public string RoiCaptureSecondInstruction => T(
        "ThreeD.Workbench.RoiCaptureSecondInstruction",
        "\uBC18\uB300\uD3B8 \uBAA8\uC11C\uB9AC\uB97C \uC88C\uD074\uB9AD\uD558\uC138\uC694.",
        "Left-click the opposite corner.");
    public string RoiCaptureReadyInstruction => T(
        "ThreeD.Workbench.RoiCaptureReadyInstruction",
        "ROI \uADF8\uB9AC\uAE30\uAC00 \uB05D\uB0AC\uC2B5\uB2C8\uB2E4. \uCD94\uAC00 ROI\uB294 \uADF8\uB824\uC9C0\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uBE48 \uACF3 \uB4DC\uB798\uADF8\uB85C \uBCF4\uAE30\uB97C \uD68C\uC804\uD558\uACE0, \uBAA8\uC11C\uB9AC\u00B7\uC911\uC559\u00B7\uB178\uB780 Y\u2195 \uD578\uB4E4\uC744 \uC870\uC815\uD55C \uB4A4 Enter \uB610\uB294 \uC801\uC6A9\uC744 \uB204\uB974\uC138\uC694.",
        "ROI drawing is complete; no additional ROI will be drawn. Drag empty space to orbit, adjust corners, center, or the yellow Y\u2195 handle, then press Enter or Apply.");
    public string PlaneFlatnessRoiTeaching => T("ThreeD.Workbench.PlaneFlatnessRoiTeaching", "\uD3C9\uBA74\uB3C4 ROI \uD2F0\uCE6D \uC21C\uC11C", "Plane Flatness ROI teaching order");
    public string PlaneFlatnessRoiTeachingDetail => T("ThreeD.Workbench.PlaneFlatnessRoiTeachingDetail", "1. \uAE30\uC900 \uD3C9\uBA74 ROI\uB97C \uC9C0\uC815\uD55C \uB4A4 2. \uCE21\uC815 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694. \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. Reference ROI, then 2. Measurement ROI. Teaching never runs inspection.");
    public string ReferenceRoi => T("ThreeD.Workbench.ReferenceRoi", "\uAE30\uC900 \uD3C9\uBA74 ROI", "Reference ROI");
    public string MeasurementRoi => T("ThreeD.Workbench.MeasurementRoi", "\uCE21\uC815 ROI", "Measurement ROI");
    public string RoiComplete => T("ThreeD.Workbench.RoiComplete", "\uC644\uB8CC", "Complete");
    public string RoiWaiting => T("ThreeD.Workbench.RoiWaiting", "\uB300\uAE30", "Waiting");
    public string RoiMissing => T("ThreeD.Workbench.RoiMissing", "\uC5C6\uC74C", "Missing");
    public string RoiDrawing => T("ThreeD.Workbench.RoiDrawing", "\uADF8\uB9AC\uB294 \uC911", "Drawing");
    public string RoiReview => T("ThreeD.Workbench.RoiReview", "\uAC80\uD1A0", "Review");
    public string RoiApplied => T("ThreeD.Workbench.RoiApplied", "\uC801\uC6A9\uB428", "Applied");
    public string CaptureRoi => T("ThreeD.Command.CaptureRoi", "ROI \uC9C0\uC815", "Capture ROI");
    public string ReplaceRoi => T("ThreeD.Command.ReplaceRoi", "ROI \uAD50\uCCB4", "Replace ROI");
    public string DrawRoi => T("ThreeD.Command.DrawRoi", "ROI \uADF8\uB9AC\uAE30", "Draw ROI");
    public string RedrawRoi => T("ThreeD.Command.RedrawRoi", "ROI \uB2E4\uC2DC \uADF8\uB9AC\uAE30", "Redraw ROI");
    public string EditRoi => T("ThreeD.Command.EditRoi", "ROI \uD3B8\uC9D1", "Edit ROI");
    public string FitRoi => T("ThreeD.Command.FitRoi", "ROI \uB9DE\uCDA4", "Fit ROI");
    public string ReuseRoi => T("ThreeD.Command.ReuseRoi", "\uAE30\uC874 ROI \uC7AC\uC0AC\uC6A9", "Reuse ROI");
    public string ExistingCompatibleRoi => T("ThreeD.Workbench.ExistingCompatibleRoi", "\uC7AC\uC0AC\uC6A9\uD560 \uD638\uD658 ROI", "Compatible ROI to reuse");
    public string ReferenceRoiRequiredFirst => T("ThreeD.Workbench.ReferenceRoiRequiredFirst", "\uBA3C\uC800 \uAE30\uC900 \uD3C9\uBA74 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694.", "Teach the Reference ROI first.");
    public string NoRoiTaught => T("ThreeD.Workbench.NoRoiTaught", "\uC9C0\uC815\uB41C ROI \uC5C6\uC74C", "No ROI taught");
    public string GapFlushRoiTeaching => T("ThreeD.Workbench.GapFlushRoiTeaching", "Gap / Flush ROI \uD2F0\uCE6D \uC21C\uC11C", "Gap / Flush ROI teaching order");
    public string GapFlushRoiTeachingDetail => T("ThreeD.Workbench.GapFlushRoiTeachingDetail", "1. \uCCAB \uBC88\uC9F8 ROI\uC640 2. \uB458\uC9F8 ROI\uB97C U\uCD95 \uBC29\uD5A5 \uC21C\uC11C\uB85C \uC9C0\uC815\uD558\uC138\uC694. ROI \uC21C\uC11C\uAC00 Gap\uACFC Flush\uC758 \uBD80\uD638\uB97C \uACB0\uC815\uD558\uBA70, \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. First ROI, then 2. Second ROI in U-axis order. ROI order defines the Gap/Flush sign; teaching never runs inspection.");
    public string VolumeRoiTeaching => T("ThreeD.Workbench.VolumeRoiTeaching", "\uCCB4\uC801 ROI \uD2F0\uCE6D \uC21C\uC11C", "Volume ROI teaching order");
    public string VolumeRoiTeachingDetail => T("ThreeD.Workbench.VolumeRoiTeachingDetail", "1. \uAE30\uC900 \uD3C9\uBA74 ROI\uB97C \uC9C0\uC815\uD55C \uB4A4 2. \uCCB4\uC801 \uCE21\uC815 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694. \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. Reference ROI, then 2. Volume measurement ROI. Teaching never runs inspection.");
    public string CompletenessRoiTeaching => T("ThreeD.Workbench.CompletenessRoiTeaching", "\uC644\uC804\uC131 \uC140 \uADF8\uB9AC\uB4DC ROI \uD2F0\uCE6D \uC21C\uC11C", "Completeness cell-grid ROI teaching order");
    public string CompletenessRoiTeachingDetail => T("ThreeD.Workbench.CompletenessRoiTeachingDetail", "1. \uAE30\uC900 ROI\uB97C \uC9C0\uC815\uD55C \uB4A4 2. \uC140 \uADF8\uB9AC\uB4DC\uB97C \uBC30\uCE58\uD560 \uAC80\uC0AC ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694. \uD2F0\uCE6D\uC740 \uACC4\uC0B0\uC774\uB098 \uD310\uC815\uC744 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. Reference ROI, then 2. Inspection Grid ROI. Teaching never calculates metrics or applies acceptance.");
    public string InspectionGridRoi => T("ThreeD.Workbench.InspectionGridRoi", "\uAC80\uC0AC \uADF8\uB9AC\uB4DC ROI", "Inspection Grid ROI");
    public string CrossSectionSelection => T("ThreeD.Workbench.CrossSectionSelection", "\uB2E8\uBA74 \uD589 \uAD6C\uAC04", "Cross-section row segment");
    public string CrossSectionSelectionDetail => T("ThreeD.Workbench.CrossSectionSelectionDetail", "A3\uC758 \uAC19\uC740 \uD589\uC5D0\uC11C \uC2DC\uC791 \uC140\uACFC \uB05D \uC140\uC744 \uC9C0\uC815\uD558\uC138\uC694. \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Pick the start and end cells on the same A3 row. Teaching never runs inspection.");
    public string FirstRoi => T("ThreeD.Workbench.FirstRoi", "\uCCAB \uBC88\uC9F8 ROI", "First ROI");
    public string SecondRoi => T("ThreeD.Workbench.SecondRoi", "\uB458\uC9F8 ROI", "Second ROI");
    public string FirstRoiRequiredFirst => T("ThreeD.Workbench.FirstRoiRequiredFirst", "\uBA3C\uC800 \uCCAB \uBC88\uC9F8 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694.", "Teach the First ROI first.");

    public string RecipeJourneyGuide => T("ThreeD.Workbench.RecipeJourneyGuide", "\uCCAB \uB808\uC2DC\uD53C \uC791\uC5C5 \uC21C\uC11C", "First recipe journey");
    public string JourneyRecipe => T("ThreeD.Workbench.JourneyRecipe", "1  \uB808\uC2DC\uD53C", "1  Recipe");
    public string JourneyInput => T("ThreeD.Workbench.JourneyInput", "2  \uC785\uB825", "2  Input");
    public string JourneyTools => T("ThreeD.Workbench.JourneyTools", "3  \uB3C4\uAD6C \uAD6C\uC131", "3  Add tools");
    public string JourneyTeachPreview => T("ThreeD.Workbench.JourneyTeachPreview", "4  \uD2F0\uCE6D\u00B7\uBBF8\uB9AC\uBCF4\uAE30", "4  Teach & Preview");
    public string JourneyValidateRun => T("ThreeD.Workbench.JourneyValidateRun", "5  \uAC80\uC99D\u00B7\uC2E4\uD589", "5  Validate & Run");
    public string NextAction => T("ThreeD.Workbench.NextAction", "\uB2E4\uC74C \uD560 \uC77C", "Next action");
    public string LoadInputActionTitle => T("ThreeD.Workbench.LoadInputActionTitle", "3D \uC785\uB825 \uB370\uC774\uD130\uB97C \uC120\uD0DD\uD558\uC138\uC694", "Select 3D input data");
    public string LoadInputActionDetail => T("ThreeD.Workbench.LoadInputActionDetail", "\uB808\uC2DC\uD53C\uC5D0 \uC0AC\uC6A9\uD560 C3D \uB192\uC774 \uB9F5\uC744 \uBD88\uB7EC\uC628 \uB4A4 \uB2E8\uACC4\uB97C \uCD94\uAC00\uD558\uC138\uC694.", "Load the C3D height map used by this recipe before adding a step.");
    public string Open3DMap => T("ThreeD.Workbench.Open3DMap", "3D \uB9F5 \uC5F4\uAE30", "Open 3D Map");
    public string Open3DMapToolTip => T("ThreeD.Workbench.Open3DMapToolTip", "\uBDF0\uC5B4\uC640 \uD604\uC7AC \uB808\uC2DC\uD53C\uC5D0 C3D \uB192\uC774 \uB9F5\uC744 \uBD88\uB7EC\uC635\uB2C8\uB2E4. (Ctrl+Shift+O)", "Load a C3D height map into the Viewer and current recipe. (Ctrl+Shift+O)");
    public string Loading3DMapFormat => T("ThreeD.Workbench.Loading3DMapFormat", "3D \uB9F5 \uBD88\uB7EC\uC624\uB294 \uC911 \u00B7 {0} \u00B7 {1:0}%", "Loading 3D map \u00B7 {0} \u00B7 {1:0}%");
    public string Cancel3DMapLoadToolTip => T("ThreeD.Workbench.Cancel3DMapLoadToolTip", "\uD604\uC7AC \uC18C\uC2A4\uB97C \uC720\uC9C0\uD558\uACE0 \uC0C8 3D \uB9F5 \uBD88\uB7EC\uC624\uAE30\uB97C \uCDE8\uC18C\uD569\uB2C8\uB2E4.", "Cancel the new 3D map load and retain the current source.");
    public string AddFirstToolActionTitle => T("ThreeD.Workbench.AddFirstToolActionTitle", "\uCCAB \uAC80\uC0AC \uB3C4\uAD6C\uB97C \uCD94\uAC00\uD558\uC138\uC694", "Add the first inspection tool");
    public string AddFirstToolActionDetail => T("ThreeD.Workbench.AddFirstToolActionDetail", "\uB3C4\uAD6C \uB77C\uC774\uBE0C\uB7EC\uB9AC\uC5D0\uC11C \uD638\uD658 \uD56D\uBAA9\uC744 \uCD94\uAC00\uD558\uC138\uC694. \uC120\uD0DD\uB9CC\uC73C\uB85C\uB294 \uC2E4\uD589\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Add a compatible item from Tool Library. Selection alone never executes it.");
    public string SelectStepActionTitle => T("ThreeD.Workbench.SelectStepActionTitle", "\uC124\uC815\uD560 \uB808\uC2DC\uD53C \uB2E8\uACC4\uB97C \uC120\uD0DD\uD558\uC138\uC694", "Select a recipe step to configure");
    public string SelectStepActionDetail => T("ThreeD.Workbench.SelectStepActionDetail", "\uB808\uC2DC\uD53C \uD750\uB984\uC5D0\uC11C \uB2E8\uACC4\uB97C \uC120\uD0DD\uD55C \uB4A4 \uC785\uB825, \uD30C\uB77C\uBBF8\uD130, \uCD9C\uB825 \uC21C\uC11C\uB85C \uD2F0\uCE6D\uD558\uC138\uC694.", "Select a step in Recipe Flow, then teach Inputs, Parameters, and Outputs in order.");
    public string TeachSelectedStepActionTitle => T("ThreeD.Workbench.TeachSelectedStepActionTitle", "\uC120\uD0DD\uD55C \uAC80\uC0AC \uB2E8\uACC4\uB97C \uD2F0\uCE6D\uD558\uC138\uC694", "Teach the selected inspection step");
    public string TeachSelectedStepActionDetail => T("ThreeD.Workbench.TeachSelectedStepActionDetail", "\uC624\uB978\uCABD \uB2E8\uACC4 \uD30C\uB77C\uBBF8\uD130\uC5D0\uC11C \uC785\uB825\u00B7ROI\u00B7\uD30C\uB77C\uBBF8\uD130\uB97C \uC124\uC815\uD55C \uB4A4 \uBBF8\uB9AC\uBCF4\uAE30\uB97C \uC2E4\uD589\uD558\uC138\uC694.", "Set its inputs, ROI, and parameters in Step Parameters, then run Preview explicitly.");

    public string NewRecipe => T("ThreeD.RecipeCenter.NewRecipe", "\uC0C8 \uB808\uC2DC\uD53C", "New recipe");
    public string OpenExistingRecipe => T("ThreeD.RecipeCenter.OpenExistingRecipe", "\uAE30\uC874 \uB808\uC2DC\uD53C \uC5F4\uAE30", "Open existing recipe");
    public string CurrentRecipe => T("ThreeD.RecipeCenter.CurrentRecipe", "\uD604\uC7AC \uB808\uC2DC\uD53C", "Current recipe");
    public string RecentRecipes => T("ThreeD.RecipeCenter.RecentRecipes", "\uCD5C\uADFC \uB808\uC2DC\uD53C", "Recent recipes");
    public string RecipeNameLabel => T("ThreeD.RecipeCenter.RecipeNameLabel", "\uB808\uC2DC\uD53C \uC774\uB984", "Recipe name");
    public string RecipeStatusLabel => T("ThreeD.RecipeCenter.RecipeStatusLabel", "\uC0C1\uD0DC", "Status");
    public string RecipePathLabel => T("ThreeD.RecipeCenter.RecipePathLabel", "\uC800\uC7A5 \uACBD\uB85C", "Save path");
    public string SourceLabel => T("ThreeD.RecipeCenter.SourceLabel", "3D \uC785\uB825", "3D input");
    public string StepsLabel => T("ThreeD.RecipeCenter.StepsLabel", "\uAC80\uC0AC \uB2E8\uACC4", "Inspection steps");
    public string Save => T("ThreeD.Command.Save", "\uC800\uC7A5", "Save");
    public string SaveAs => T("ThreeD.Command.SaveAs", "\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uC800\uC7A5", "Save as");
    public string RemoveFromRecent => T("ThreeD.RecipeCenter.RemoveFromRecent", "\uCD5C\uADFC \uBAA9\uB85D\uC5D0\uC11C \uC81C\uAC70", "Remove from recent");
    public string RemoveFromRecentToolTip => T("ThreeD.RecipeCenter.RemoveFromRecentToolTip", "\uD30C\uC77C\uC740 \uC0AD\uC81C\uD558\uC9C0 \uC54A\uACE0 \uCD5C\uADFC \uBAA9\uB85D\uC5D0\uC11C\uB9CC \uC81C\uAC70\uD569\uB2C8\uB2E4.", "Remove only from the recent list; the file is not deleted.");
    public string Available => T("ThreeD.RecipeCenter.Available", "\uC5F4\uAE30 \uAC00\uB2A5", "Available");
    public string Unavailable => T("ThreeD.RecipeCenter.Unavailable", "\uD30C\uC77C \uC5C6\uC74C", "Unavailable");
    public string RecipeCenterDetail => T("ThreeD.RecipeCenter.Detail", "\uC0C8 \uAC80\uC0AC\uB97C \uC2DC\uC791\uD558\uAC70\uB098 \uC774\uC804 \uB808\uC2DC\uD53C\uB97C \uC5F4\uACE0, \uD604\uC7AC \uC791\uC5C5\uC758 \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uC138\uC694.", "Start a new inspection or open a previous recipe, then review the current session's save state.");
    public string SourceNotSelected => T("ThreeD.RecipeCenter.SourceNotSelected", "3D \uC785\uB825 \uBBF8\uC120\uD0DD", "3D input not selected");
    public string SourceUnsupportedFormat => T("ThreeD.RecipeCenter.SourceUnsupportedFormat", "\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 \uD615\uC2DD", "Unsupported format");
    public string SourceMissing => T("ThreeD.RecipeCenter.SourceMissing", "\uC18C\uC2A4 \uD30C\uC77C \uC5C6\uC74C \u00B7 \uB2E4\uC2DC \uC5F0\uACB0 \uD544\uC694", "Source missing \u00B7 relink required");
    public string SourceIdentityMismatch => T("ThreeD.RecipeCenter.SourceIdentityMismatch", "\uC18C\uC2A4 \uC2DD\uBCC4 \uBD88\uC77C\uCE58 \u00B7 \uB2E4\uC2DC \uC5F0\uACB0 \uD544\uC694", "Source identity mismatch \u00B7 relink required");
    public string SourceUnreadable => T("ThreeD.RecipeCenter.SourceUnreadable", "\uC18C\uC2A4\uB97C \uC77D\uC744 \uC218 \uC5C6\uC74C", "Source unreadable");
    public string SourceReadyFormat => T("ThreeD.RecipeCenter.SourceReadyFormat", "\uC785\uB825 \uC900\uBE44\uB428 \u00B7 {0} x {1}", "Input ready \u00B7 {0} x {1}");
    public string NotSavedYet => T("ThreeD.RecipeCenter.NotSavedYet", "\uC544\uC9C1 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC74C", "Not saved yet");
    public string Valid => T("ThreeD.RecipeCenter.Valid", "\uC720\uD6A8", "Valid");
    public string ValidWarningsFormat => T("ThreeD.RecipeCenter.ValidWarningsFormat", "\uC720\uD6A8 \u00B7 \uACBD\uACE0 {0}\uAC1C", "Valid \u00B7 {0} warning(s)");
    public string CorrectionsFormat => T("ThreeD.RecipeCenter.CorrectionsFormat", "\uC218\uC815 \uD544\uC694 {0}\uAC1C", "{0} correction(s)");
    public string ExecutionRequirementsFormat => T("ThreeD.RecipeCenter.ExecutionRequirementsFormat", "\uC2E4\uD589 \uC900\uBE44 \uD544\uC694 {0}\uAC1C", "{0} execution requirement(s)");
    public string SourceCorrectionsFormat => T("ThreeD.RecipeCenter.SourceCorrectionsFormat", "\uC18C\uC2A4 \uC218\uC815 \uD544\uC694 {0}\uAC1C", "Source needs {0} correction(s)");
    public string StaleSelectionsFormat => T("ThreeD.RecipeCenter.StaleSelectionsFormat", "\uC624\uB798\uB41C \uC120\uD0DD \uC601\uC5ED {0}\uAC1C", "{0} stale selection(s)");
    public string Modified => T("ThreeD.RecipeCenter.Modified", "\uC218\uC815\uB428", "Modified");
    public string Unsaved => T("ThreeD.RecipeCenter.Unsaved", "\uBBF8\uC800\uC7A5", "Unsaved");
    public string Saved => T("ThreeD.RecipeCenter.Saved", "\uC800\uC7A5\uB428", "Saved");
    public string RecipeSaveBlockedTitle => T("ThreeD.RecipeCenter.SaveBlockedTitle", "\uC800\uC7A5 \uC804\uC5D0 \uC644\uB8CC\uD560 \uC791\uC5C5", "Complete before saving");
    public string RecipeSaveBlockedCorrections => T("ThreeD.RecipeCenter.SaveBlockedCorrections", "\uC6CC\uD06C\uBCA4\uCE58\uC5D0\uC11C \uD45C\uC2DC\uB41C \uC785\uB825, \uACBD\uB85C \uB610\uB294 \uD30C\uB77C\uBBF8\uD130 \uC218\uC815 \uD56D\uBAA9\uC744 \uBA3C\uC800 \uD574\uACB0\uD558\uC138\uC694.", "Resolve the listed input, route, or parameter corrections in the Workbench first.");

    public string FlowMapPortState => T("ThreeD.Workbench.FlowMapPortState", "\uD3EC\uD2B8 \uC0C1\uD0DC", "Port state");
    public string Problems => T("ThreeD.Workbench.Problems", "\uBB38\uC81C", "Problems");
    public string ProblemsTitle => T("ThreeD.Workbench.ProblemsTitle", "\uACBD\uB85C \uBB38\uC81C", "Route problems");
    public string ProblemsDetail => T("ThreeD.Workbench.ProblemsDetail", "\uD3EC\uD2B8 \uC0C1\uD0DC\uC640 \uAE30\uC874 \uAC80\uC99D \uBA54\uC2DC\uC9C0\uB9CC \uC77D\uC5B4 \uD45C\uC2DC\uD569\uB2C8\uB2E4. \uB2E8\uACC4 \uD3EC\uCEE4\uC2A4\uB294 \uAC00\uB2A5\uD558\uC9C0\uB9CC \uC5F0\uACB0 \uC218\uC815\uC774\uB098 \uC2E4\uD589\uC740 \uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Read only the port state and existing validation messages. Focus a step, but do not edit a connection or run it.");
    public string ProblemsSummaryFormat => T("ThreeD.Workbench.ProblemsSummaryFormat", "\uACBD\uB85C \uC810\uAC80 {0}\uAC1C | \uAC80\uC99D \uBA54\uC2DC\uC9C0 {1}\uAC1C", "{0} route checks | {1} validation messages");
    public string ProblemsRouteChecks => T("ThreeD.Workbench.ProblemsRouteChecks", "\uACBD\uB85C \uC810\uAC80", "Route checks");
    public string ProblemsValidationMessages => T("ThreeD.Workbench.ProblemsValidationMessages", "\uB808\uC2DC\uD53C \uAC80\uC99D \uBA54\uC2DC\uC9C0", "Recipe validation messages");
    public string ProblemsEmptyHint => T("ThreeD.Workbench.ProblemsEmptyHint", "\uD3EC\uD2B8 \uACBD\uB85C \uBB38\uC81C\uC640 \uAC80\uC99D \uBA54\uC2DC\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", "No route problems or validation messages.");
    public string FlowPortReady => T("ThreeD.Workbench.FlowPortReady", "\uC900\uBE44\uB428", "Ready");
    public string FlowPortWaitingForUpstream => T("ThreeD.Workbench.FlowPortWaitingForUpstream", "\uC0C1\uC704 \uC0B0\uCD9C\uBB3C \uB300\uAE30", "Waiting for upstream");
    public string FlowPortStale => T("ThreeD.Workbench.FlowPortStale", "\uC7AC\uC0DD\uC131 \uD544\uC694", "Stale");
    public string FlowPortUnresolved => T("ThreeD.Workbench.FlowPortUnresolved", "\uC785\uB825 \uBBF8\uD574\uACB0", "Unresolved input");
    public string FlowPortDeclared => T("ThreeD.Workbench.FlowPortDeclared", "\uC120\uC5B8\uB428", "Declared");
    public string FlowPortCurrent => T("ThreeD.Workbench.FlowPortCurrent", "\uD604\uC7AC \uC0B0\uCD9C\uBB3C", "Current output");
    public string FlowPortNoInputDetail => T("ThreeD.Workbench.FlowPortNoInputDetail", "\uC785\uB825 \uC5D4\uD2F0\uD2F0 ID\uB97C \uC9C0\uC815\uD558\uC138\uC694.", "Specify an input entity ID.");
    public string FlowPortUnresolvedDetailFormat => T("ThreeD.Workbench.FlowPortUnresolvedDetailFormat", "\uC785\uB825 '{0}'\uC744(\uB97C) \uB808\uC2DC\uD53C \uC544\uD2F0\uD329\uD2B8\uC5D0\uC11C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.", "Input '{0}' is not present in the recipe artifacts.");
    public string FlowPortWaitingDetailFormat => T("ThreeD.Workbench.FlowPortWaitingDetailFormat", "\uC0C1\uC704 '{0}'\uC740(\uB294) \uC120\uC5B8\uB9CC \uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4. \uD574\uB2F9 \uB2E8\uACC4\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C Preview/Publish\uD558\uC138\uC694.", "Upstream '{0}' is declared only. Preview or Publish its step explicitly.");
    public string FlowPortStaleDetailFormat => T("ThreeD.Workbench.FlowPortStaleDetailFormat", "\uC0C1\uC704 '{0}'\uC774(\uAC00) \uC624\uB798\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uD574\uB2F9 \uB2E8\uACC4\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C \uB2E4\uC2DC Preview/Publish\uD558\uC138\uC694.", "Upstream '{0}' is stale. Preview or Publish its step explicitly again.");
    public string FlowPortDeclaredDetailFormat => T("ThreeD.Workbench.FlowPortDeclaredDetailFormat", "\uC815\uC2DD \uCD9C\uB825 '{0}'\uC774(\uAC00) \uC120\uC5B8\uB418\uC5C8\uC9C0\uB9CC \uD604\uC7AC Preview/Published \uC99D\uAC70\uB294 \uC5C6\uC2B5\uB2C8\uB2E4.", "Typed output '{0}' is declared, but has no current Preview or Published evidence.");
    public string FlowPortCurrentDetailFormat => T("ThreeD.Workbench.FlowPortCurrentDetailFormat", "\uC815\uC2DD \uCD9C\uB825 '{0}'\uC774(\uAC00) \uD604\uC7AC \uC0C1\uD0DC\uC785\uB2C8\uB2E4.", "Typed output '{0}' is current.");

    private static string T(string key, string korean, string english)
    {
        var value = OpenVisionLanguageService.T(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean
            : value;
    }

    internal string Resolve(string key, string korean, string english) => T(key, korean, english);

    private void Refresh()
    {
        foreach (var propertyName in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
