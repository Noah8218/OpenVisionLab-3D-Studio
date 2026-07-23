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
        nameof(AddSelectedStep), nameof(Viewer), nameof(StepParameters),
        nameof(PipelineValidation), nameof(RunRecord), nameof(RunRecordTitle), nameof(RunRecordDetail),
        nameof(RunRecordOpen), nameof(RunRecordOpenCurrent), nameof(RunRecordOpenHtml), nameof(RunRecordOpenCsv),
        nameof(RunRecordOpenFolder), nameof(RunRecordExport), nameof(RunRecordRecent), nameof(RunRecordOpenRecent),
        nameof(RunRecordSummaryFormat), nameof(RunRecordOpenFailed), nameof(RunRecordExportedFormat),
        nameof(ValidationSet), nameof(ValidationSetTitle), nameof(ValidationSetDetail),
        nameof(ValidationSetAddSamples), nameof(ValidationSetRunAll), nameof(ValidationSetClear),
        nameof(ValidationSetSamples), nameof(ValidationSetSelectedRecord), nameof(ValidationSetNoSamples),
        nameof(ValidationSetNoSelection), nameof(ValidationSetFile), nameof(ValidationSetDuration),
        nameof(ValidationSetCoverage),
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
        nameof(ShowInput), nameof(TeachingSelections), nameof(PlaneFlatnessRoiTeaching), nameof(PlaneFlatnessRoiTeachingDetail),
        nameof(ReferenceRoi), nameof(MeasurementRoi), nameof(RoiComplete), nameof(RoiWaiting),
        nameof(CaptureRoi), nameof(ReplaceRoi), nameof(ReuseRoi), nameof(ExistingCompatibleRoi),
        nameof(ReferenceRoiRequiredFirst), nameof(NoRoiTaught), nameof(GapFlushRoiTeaching),
        nameof(GapFlushRoiTeachingDetail), nameof(VolumeRoiTeaching), nameof(VolumeRoiTeachingDetail),
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
        nameof(RecipeSaveBlockedTitle), nameof(RecipeSaveBlockedCorrections)
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
    public string RecipeFlowHint => T("ThreeD.Workbench.RecipeFlowHint", "3D \uC785\uB825 \u2192 \uAC80\uC0AC \uB2E8\uACC4 \u2192 \uCD9C\uB825 \uACB0\uACFC \uC21C\uC11C\uB97C \uD655\uC778\uD558\uACE0, \uB2E8\uACC4\uB97C \uC120\uD0DD\uD574 \uD2F0\uCE6D\uD558\uC138\uC694.", "Review 3D input -> inspection steps -> outputs, then select a step to teach it.");
    public string FilterOptionalHint => T("ThreeD.Workbench.FilterOptionalHint", "\uD544\uD130\uB294 \uC120\uD0DD \uC0AC\uD56D\uC785\uB2C8\uB2E4. \uD604\uC7AC \uC785\uB825\uC774 \uD638\uD658\uB418\uBA74 \uCE21\uC815 \uB3C4\uAD6C\uB97C \uBC14\uB85C \uCD94\uAC00\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "Filter is optional. Add a measurement tool directly when the current input is compatible.");
    public string AddSelectedStep => T("ThreeD.Command.AddSelectedStep", "\uC120\uD0DD \uB2E8\uACC4 \uCD94\uAC00", "Add selected step");
    public string Viewer => T("ThreeD.Workbench.Viewer", "3D \uBDF0", "3D View");
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
    public string RunRecordOpenFailed => T("ThreeD.Message.RunRecordOpenFailed", "\uC2E4\uD589 \uAE30\uB85D\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. JSON \uD30C\uC77C\uACFC \uC2A4\uD0A4\uB9C8\uB97C \uD655\uC778\uD558\uC138\uC694.", "The Run Record could not be read. Check the JSON file and schema.");
    public string RunRecordExportedFormat => T("ThreeD.Message.RunRecordExportedFormat", "\uC2E4\uD589 \uAE30\uB85D \uBB36\uC74C\uC744 \uB0B4\uBCF4\uB0C8\uC2B5\uB2C8\uB2E4: {0}", "Run Record bundle exported: {0}");
    public string ValidationSet => T("ThreeD.Workbench.ValidationSet", "\uBC18\uBCF5 \uAC80\uC99D", "Validation Set");
    public string ValidationSetTitle => T("ThreeD.Workbench.ValidationSetTitle", "\uB2E4\uC911 \uC0D8\uD50C \uBC18\uBCF5 \uAC80\uC99D", "Multi-sample repeat validation");
    public string ValidationSetDetail => T("ThreeD.Workbench.ValidationSetDetail", "\uD2F0\uCE6D\uB41C \uB808\uC2DC\uD53C\uB97C \uC120\uD0DD\uD55C C3D \uC0D8\uD50C\uC5D0 \uC21C\uC11C\uB300\uB85C \uC2E4\uD589\uD569\uB2C8\uB2E4. \uC0D8\uD50C \uC120\uD0DD\uC740 \uB808\uC2DC\uD53C\uB098 3D \uBDF0\uC5B4 \uC785\uB825\uC744 \uBC14\uAFB8\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Run the taught recipe sequentially on selected C3D samples. Selecting samples never changes the recipe or 3D Viewer input.");
    public string ValidationSetAddSamples => T("ThreeD.Command.ValidationSetAddSamples", "\uC0D8\uD50C \uCD94\uAC00", "Add samples");
    public string ValidationSetRunAll => T("ThreeD.Command.ValidationSetRunAll", "\uC804\uCCB4 \uC2E4\uD589", "Run all");
    public string ValidationSetClear => T("ThreeD.Command.ValidationSetClear", "\uBAA9\uB85D \uBE44\uC6B0\uAE30", "Clear list");
    public string ValidationSetSamples => T("ThreeD.Workbench.ValidationSetSamples", "\uAC80\uC99D \uC0D8\uD50C", "Validation samples");
    public string ValidationSetSelectedRecord => T("ThreeD.Workbench.ValidationSetSelectedRecord", "\uC120\uD0DD \uC0D8\uD50C \uC2E4\uD589 \uAE30\uB85D", "Selected sample record");
    public string ValidationSetNoSamples => T("ThreeD.Workbench.ValidationSetNoSamples", "\uC544\uC9C1 \uAC80\uC99D \uC0D8\uD50C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.", "No validation samples have been added.");
    public string ValidationSetNoSelection => T("ThreeD.Workbench.ValidationSetNoSelection", "\uC0D8\uD50C\uC744 \uC120\uD0DD\uD558\uBA74 \uB2E8\uACC4\uBCC4 \uC2E4\uD589 \uADFC\uAC70\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.", "Select a sample to inspect its step-by-step evidence.");
    public string ValidationSetFile => T("ThreeD.Column.ValidationSetFile", "\uD30C\uC77C", "File");
    public string ValidationSetDuration => T("ThreeD.Column.ValidationSetDuration", "\uC2E4\uD589 \uC2DC\uAC04", "Duration");
    public string ValidationSetCoverage => T("ThreeD.Workbench.ValidationSetCoverage", "\uC2E4\uD589 \uC801\uC6A9 \uBC94\uC704", "Execution coverage");
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
    public string RecipeNavigator => T("ThreeD.Workbench.RecipeNavigator", "\uB808\uC2DC\uD53C \uD0D0\uC0C9\uAE30", "Recipe Navigator");
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
    public string SelectedRoute => T("ThreeD.Workbench.SelectedRoute", "\uC120\uD0DD\uB41C \uAC80\uC0AC \uACBD\uB85C", "Selected inspection route");
    public string OpenSelectedToolLab => T("ThreeD.Command.OpenSelectedToolLab", "\uC120\uD0DD \uB3C4\uAD6C \uB7A9 \uC5F4\uAE30", "Open selected Tool Lab");
    public string ToolLabReview => T("ThreeD.ToolLab.Review", "\uD30C\uB77C\uBBF8\uD130 \uBC0F \uC2E4\uD589 \uADFC\uAC70", "Parameters & execution evidence");
    public string ToolLabReviewDetail => T("ThreeD.ToolLab.ReviewDetail", "\uD30C\uB77C\uBBF8\uD130\uB294 \uB808\uC2DC\uD53C \uCD08\uC548\uC5D0\uB9CC \uC801\uC6A9\uB429\uB2C8\uB2E4. \uBBF8\uB9AC\uBCF4\uAE30\uC640 \uAC8C\uC2DC\uB294 \uBA85\uC2DC\uC801\uC73C\uB85C \uC218\uD589\uD569\uB2C8\uB2E4.", "Edits stay in the recipe draft. Preview and Publish remain explicit.");
    public string ShowInput => T("ThreeD.Command.ShowInput", "\uC785\uB825 \uBCF4\uAE30", "Show input");
    public string TeachingSelections => T("ThreeD.Workbench.TeachingSelections", "\uD2F0\uCE6D \uC120\uD0DD \uC601\uC5ED", "Teaching selections");
    public string PlaneFlatnessRoiTeaching => T("ThreeD.Workbench.PlaneFlatnessRoiTeaching", "\uD3C9\uBA74\uB3C4 ROI \uD2F0\uCE6D \uC21C\uC11C", "Plane Flatness ROI teaching order");
    public string PlaneFlatnessRoiTeachingDetail => T("ThreeD.Workbench.PlaneFlatnessRoiTeachingDetail", "1. \uAE30\uC900 \uD3C9\uBA74 ROI\uB97C \uC9C0\uC815\uD55C \uB4A4 2. \uCE21\uC815 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694. \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. Reference ROI, then 2. Measurement ROI. Teaching never runs inspection.");
    public string ReferenceRoi => T("ThreeD.Workbench.ReferenceRoi", "\uAE30\uC900 \uD3C9\uBA74 ROI", "Reference ROI");
    public string MeasurementRoi => T("ThreeD.Workbench.MeasurementRoi", "\uCE21\uC815 ROI", "Measurement ROI");
    public string RoiComplete => T("ThreeD.Workbench.RoiComplete", "\uC644\uB8CC", "Complete");
    public string RoiWaiting => T("ThreeD.Workbench.RoiWaiting", "\uB300\uAE30", "Waiting");
    public string CaptureRoi => T("ThreeD.Command.CaptureRoi", "ROI \uC9C0\uC815", "Capture ROI");
    public string ReplaceRoi => T("ThreeD.Command.ReplaceRoi", "ROI \uAD50\uCCB4", "Replace ROI");
    public string ReuseRoi => T("ThreeD.Command.ReuseRoi", "\uAE30\uC874 ROI \uC7AC\uC0AC\uC6A9", "Reuse ROI");
    public string ExistingCompatibleRoi => T("ThreeD.Workbench.ExistingCompatibleRoi", "\uC7AC\uC0AC\uC6A9\uD560 \uD638\uD658 ROI", "Compatible ROI to reuse");
    public string ReferenceRoiRequiredFirst => T("ThreeD.Workbench.ReferenceRoiRequiredFirst", "\uBA3C\uC800 \uAE30\uC900 \uD3C9\uBA74 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694.", "Teach the Reference ROI first.");
    public string NoRoiTaught => T("ThreeD.Workbench.NoRoiTaught", "\uC9C0\uC815\uB41C ROI \uC5C6\uC74C", "No ROI taught");
    public string GapFlushRoiTeaching => T("ThreeD.Workbench.GapFlushRoiTeaching", "Gap / Flush ROI \uD2F0\uCE6D \uC21C\uC11C", "Gap / Flush ROI teaching order");
    public string GapFlushRoiTeachingDetail => T("ThreeD.Workbench.GapFlushRoiTeachingDetail", "1. \uCCAB \uBC88\uC9F8 ROI\uC640 2. \uB458\uC9F8 ROI\uB97C U\uCD95 \uBC29\uD5A5 \uC21C\uC11C\uB85C \uC9C0\uC815\uD558\uC138\uC694. ROI \uC21C\uC11C\uAC00 Gap\uACFC Flush\uC758 \uBD80\uD638\uB97C \uACB0\uC815\uD558\uBA70, \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. First ROI, then 2. Second ROI in U-axis order. ROI order defines the Gap/Flush sign; teaching never runs inspection.");
    public string VolumeRoiTeaching => T("ThreeD.Workbench.VolumeRoiTeaching", "\uCCB4\uC801 ROI \uD2F0\uCE6D \uC21C\uC11C", "Volume ROI teaching order");
    public string VolumeRoiTeachingDetail => T("ThreeD.Workbench.VolumeRoiTeachingDetail", "1. \uAE30\uC900 \uD3C9\uBA74 ROI\uB97C \uC9C0\uC815\uD55C \uB4A4 2. \uCCB4\uC801 \uCE21\uC815 ROI\uB97C \uC9C0\uC815\uD558\uC138\uC694. \uD2F0\uCE6D\uC740 \uAC80\uC0AC\uB97C \uC2E4\uD589\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Teach 1. Reference ROI, then 2. Volume measurement ROI. Teaching never runs inspection.");
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
