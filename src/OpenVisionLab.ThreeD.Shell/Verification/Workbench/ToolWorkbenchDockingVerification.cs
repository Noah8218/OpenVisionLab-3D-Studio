using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.Views.Shell;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolWorkbenchDockingVerification
{
    private static readonly string[] WorkbenchContentIds =
    [
        "tool-library",
        "data-layers",
        "tool-inspector",
        "three-d-viewer",
        "evidence-workbench",
        "output-compare",
        "displayed-outputs",
        "linked-view",
        "height-profile",
        "fit-diagnostics",
        "intersection-evidence",
        "correspondence-evidence",
    ];

    private static readonly string[] CalibrationContentIds =
    [
        "calibration-explorer",
        "calibration-workspace",
        "calibration-inspector",
        "calibration-evidence",
    ];

    private const string ThicknessRecipeRelativePath =
        "3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json";

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D docking workspace verification",
            $"Generated: {DateTimeOffset.Now:O}",
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

        try
        {
            Check(
                "verification runs on STA",
                Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                Thread.CurrentThread.GetApartmentState().ToString());

            var dataContextOwner = new object();
            var viewerOwner = new object();
            var workbench = new ToolRecipeWorkbenchView
            {
                DataContext = dataContextOwner,
                ViewerContent = viewerOwner,
            };
            var workbenchContracts = workbench.GetDockingPaneContracts();
            var shortcutGestures = workbench.InputBindings
                .OfType<KeyBinding>()
                .Select(binding => (binding.Key, binding.Modifiers))
                .ToHashSet();
            Check(
                "Workbench exposes the bounded inspection shortcut set",
                shortcutGestures.SetEquals(
                [
                    (Key.N, ModifierKeys.Control),
                    (Key.O, ModifierKeys.Control),
                    (Key.S, ModifierKeys.Control),
                    (Key.S, ModifierKeys.Control | ModifierKeys.Shift),
                    (Key.Escape, ModifierKeys.None),
                    (Key.Enter, ModifierKeys.None),
                    (Key.O, ModifierKeys.Control | ModifierKeys.Shift),
                    (Key.F5, ModifierKeys.None),
                    (Key.F5, ModifierKeys.Control),
                ]),
                string.Join(", ", shortcutGestures.OrderBy(item => item.Key).ThenBy(item => item.Modifiers)));
            Check("Workbench exposes twelve dock panes", workbenchContracts.Count == 12, Describe(workbenchContracts));
            Check(
                "Workbench separates Tool Library and Recipe Flow without hosting Recipe Center",
                HasExactIds(workbenchContracts, WorkbenchContentIds)
                && workbenchContracts[0].ContentId == "tool-library"
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[0].HasContent
                && workbenchContracts[1].HasContent,
                Describe(workbenchContracts));
            Check(
                "Workbench model preserves Recipe Flow, Selected Tool, then Viewer order",
                workbench.HasRecipeFlowInspectorViewerOrder
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[2].ContentId == "tool-inspector"
                && workbenchContracts[3].ContentId == "three-d-viewer",
                Describe(workbenchContracts));
            Check(
                "Workbench default uses Recipe Chain and the single Selected Tool workspace",
                workbench.UsesInspectionWorkspaceV3Composition,
                $"v3Composition={workbench.UsesInspectionWorkspaceV3Composition}");
            Check(
                "Authoring exposes exactly one current-action guide",
                workbench.HasSingleVisibleAuthoringFirstAction,
                $"firstActionGuide={workbench.HasVisibleAuthoringFirstActionGuide}; singleCurrentAction={workbench.HasSingleVisibleAuthoringFirstAction}");
            Check(
                "Empty Viewer keeps one primary input action without a duplicate context ribbon",
                workbench.IsViewerInputFirstActionVisible
                && !workbench.IsViewerContextRibbonVisible
                && !workbench.IsNoRecipeStepBannerVisible,
                $"inputAction={workbench.IsViewerInputFirstActionVisible}; contextRibbon={workbench.IsViewerContextRibbonVisible}; noStepBanner={workbench.IsNoRecipeStepBannerVisible}");
            Check(
                "Selected Tool workspace owns bounded Thickness repeat-grid authoring controls",
                workbench.HasThicknessRepeatGridAuthoringControls,
                $"repeatGrid={workbench.HasThicknessRepeatGridAuthoringControls}");
            Check(
                "Selected Tool owns explicit Preview, Publish, Cancel, and Save actions",
                workbench.HasExplicitSelectedToolActions,
                $"selectedToolActions={workbench.HasExplicitSelectedToolActions}");
            Check(
                "Source Quality and Selected Tool surfaces are mutually exclusive",
                workbench.HasExclusiveSelectedToolWorkspaceSurface,
                $"exclusiveWorkspace={workbench.HasExclusiveSelectedToolWorkspaceSurface}");
            Check(
                "Displayed Outputs is adjacent to Viewer",
                workbench.HasAdjacentViewerOutputs,
                $"viewerOutputs={workbench.HasAdjacentViewerOutputs}");
            Check(
                "Workbench Viewer exposes one normal layout toolbar and two reusable slot hosts",
                workbench.HasViewerWorkspaceLayoutToolbar,
                $"viewerWorkspace={workbench.HasViewerWorkspaceLayoutToolbar}");
            Check(
                "Workbench gives the default Viewer a dominant width",
                workbench.HasDominantViewerWidth,
                $"dominantViewer={workbench.HasDominantViewerWidth}");
            Check(
                "Workbench removes the repeated full-width command and metadata row",
                workbench.HasNoVisibleWorkspaceCommandBar,
                $"commandBarHidden={workbench.HasNoVisibleWorkspaceCommandBar}");
            Check(
                "Workbench dock tabs use the top OpenVision themed strip",
                workbench.HasTopThemedDockTabs,
                $"topThemedTabs={workbench.HasTopThemedDockTabs}");
            Check(
                "Task panes expose side collapse while Viewer remains fixed",
                workbench.HasSideCollapsibleTaskPanes,
                $"sideCollapsible={workbench.HasSideCollapsibleTaskPanes}");
            var autoHideRoundTrip = workbench.VerifySupportAutoHideRoundTrip();
            Check(
                "Support pane side collapse restores without changing composition",
                autoHideRoundTrip.Collapsed
                && autoHideRoundTrip.Restored
                && workbench.HasRecipeFlowInspectorViewerOrder,
                $"collapsed={autoHideRoundTrip.Collapsed}; restored={autoHideRoundTrip.Restored}; composition={workbench.HasRecipeFlowInspectorViewerOrder}");

            var navigationRail = new StudioNavigationRailView
            {
                DataContext = new ShellMainWindowViewModel(),
            };
            navigationRail.ApplyResponsiveWidthForVerification(1920);
            var wideRailWidth = navigationRail.Width;
            navigationRail.ApplyResponsiveWidthForVerification(1280);
            Check(
                "Workbench v4 rail exposes accessible responsibility and utility routes",
                navigationRail.HasAccessibleResponsibilityRoutes
                && navigationRail.HasAccessibleUtilityRoutes,
                $"responsibilities={navigationRail.HasAccessibleResponsibilityRoutes}; utilities={navigationRail.HasAccessibleUtilityRoutes}");
            Check(
                "Workbench v4 rail switches from labeled Wide to icon Compact width",
                Math.Abs(wideRailWidth - 140) < 0.1
                && navigationRail.IsCompact
                && Math.Abs(navigationRail.Width - 60) < 0.1,
                $"wide={wideRailWidth:F0}; compact={navigationRail.Width:F0}; isCompact={navigationRail.IsCompact}");
            var titleBar = new StudioTitleBarView
            {
                Width = 1280,
                SourceQualityStatusText = "Quality 84.5% valid \u00b7 15.5% missing",
                SourceQualityStatusToolTip = "Current input quality detail",
                SourceQualityStatusKind = "Warning",
                IsSourceQualityStatusVisible = true,
            };
            titleBar.Measure(new Size(1280, 56));
            titleBar.Arrange(new Rect(0, 0, 1280, 56));
            titleBar.UpdateLayout();
            Check(
                "Job Bar exposes the accessible current-source quality state",
                titleBar.HasAccessibleCurrentSourceQualityStatus,
                $"visible={titleBar.IsSourceQualityStatusVisible}; kind={titleBar.SourceQualityStatusKind}; text={titleBar.SourceQualityStatusText}");
            titleBar.IsSourceQualityStatusVisible = false;
            titleBar.UpdateLayout();
            Check(
                "Job Bar hides current-source quality when no input exists",
                titleBar.HasAccessibleCurrentSourceQualityStatus,
                $"visible={titleBar.IsSourceQualityStatusVisible}");
            Check("Workbench hosts all twelve dockable views", workbench.HasAllDockContentHosts && workbenchContracts.All(contract => contract.HasContent), Describe(workbenchContracts));
            Check("Workbench panes can float", workbenchContracts.All(contract => contract.CanFloat), Describe(workbenchContracts));
            Check("Workbench required panes cannot close", workbenchContracts.All(contract => !contract.CanClose), Describe(workbenchContracts));
            Check("Fit Diagnostics may hide without closing", workbenchContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(workbenchContracts));
            Check(
                "Output Compare and Displayed Outputs may hide without closing",
                workbenchContracts.Single(contract => contract.ContentId == "output-compare").CanHide == true
                && workbenchContracts.Single(contract => contract.ContentId == "displayed-outputs").CanHide == true,
                Describe(workbenchContracts));

            var transition = workbench.VerifyFirstPaneFloatDockRoundTrip();
            Check(
                "Workbench pane Float then Dock transition",
                transition.Floated && transition.Redocked
                && transition.FloatingWindowCountAfterFloat == transition.FloatingWindowCountBefore + 1
                && transition.FloatingWindowCountAfterDock == transition.FloatingWindowCountBefore,
                transition.ToString());
            Check(
                "Workbench state owners survive dock transition",
                ReferenceEquals(workbench.DataContext, dataContextOwner)
                && ReferenceEquals(workbench.ViewerContent, viewerOwner),
                "DataContext and ViewerContent references retained");

            Check(
                "Workbench starts with the empty bottom review pane collapsed",
                !workbench.IsBottomPaneExpanded && !workbench.IsBottomPaneAttached,
                $"expanded={workbench.IsBottomPaneExpanded}, attached={workbench.IsBottomPaneAttached}");

            workbench.IsBottomPaneExpanded = false;
            var focusCollapsed = !workbench.IsBottomPaneAttached;
            workbench.IsBottomPaneExpanded = true;
            Check(
                "Workbench capture focus detaches then restores bottom pane",
                focusCollapsed && workbench.IsBottomPaneAttached,
                $"collapsed={focusCollapsed}, restored={workbench.IsBottomPaneAttached}");
            workbench.ActivateProfilePane();
            Check(
                "Workbench Profile command selects docked height-profile pane",
                workbench.IsBottomPaneAttached && workbench.IsProfilePaneSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsProfilePaneSelected}");
            workbench.ActivateSessionLogPane();
            Check(
                "Workbench Session Log command selects docked session pane",
                workbench.IsBottomPaneAttached && workbench.IsSessionLogPaneSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsSessionLogPaneSelected}");
            workbench.ActivateFlowMap();
            Check(
                "Workbench Flow Map command selects the read-only map in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsFlowMapSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFlowMapSelected}");
            workbench.ActivateProblems();
            Check(
                "Workbench Problems command selects the read-only port diagnostics in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsProblemsSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsProblemsSelected}");
            workbench.ActivateRunRecord();
            Check(
                "Workbench Run Record exposes open, recent, and export controls in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsRunRecordSelected && workbench.HasRunRecordHistoryControls,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsRunRecordSelected}, controls={workbench.HasRunRecordHistoryControls}");
            workbench.ActivateOutputComparePane();
            Check(
                "Workbench Output Compare command selects a floatable pane with usable default height",
                workbench.IsBottomPaneAttached
                && workbench.IsOutputComparePaneSelected
                && workbench.HasUsableOutputCompareDockHeight,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsOutputComparePaneSelected}, usableHeight={workbench.HasUsableOutputCompareDockHeight}");
            workbench.ActivateDisplayedOutputsPane();
            Check(
                "Workbench Displayed Outputs command restores the standard bottom-pane height",
                workbench.IsBottomPaneAttached
                && workbench.IsDisplayedOutputsPaneSelected
                && workbench.HasStandardBottomPaneHeight,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsDisplayedOutputsPaneSelected}, standardHeight={workbench.HasStandardBottomPaneHeight}");
            workbench.ActivateFitDiagnosticsPane();
            Check("Workbench Fit Diagnostics command selects docked diagnostics pane", workbench.IsBottomPaneAttached && workbench.IsFitDiagnosticsPaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFitDiagnosticsPaneSelected}");
            workbench.ActivateIntersectionEvidencePane();
            Check("Workbench Intersection Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsIntersectionEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsIntersectionEvidencePaneSelected}");
            workbench.ActivateCorrespondenceEvidencePane();
            Check("Workbench Correspondence Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsCorrespondenceEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsCorrespondenceEvidencePaneSelected}");

            var shell = new ShellMainWindowViewModel();
            var recipePath = Path.Combine(FindRepositoryRoot(), ThicknessRecipeRelativePath);
            Check(
                "stage verification opens the current bundled Thickness recipe",
                shell.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage)
                && shell.Workbench.PipelineSteps.Count == 8
                && shell.Workbench.SelectedPipelineStep is not null,
                openMessage);
            var selectedStep = shell.Workbench.SelectedPipelineStep;
            var selectedStepState = selectedStep?.State;
            var recipeStepCount = shell.Workbench.PipelineSteps.Count;
            var recipeDirty = shell.Workbench.IsDirty;
            var validationFixture =
                CompletenessValidationVerificationFixtureFactory.Create(
                    Path.Combine(
                        Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
                        "validation-set-fixture"));
            var validationSamplePaths = validationFixture.Samples
                .Select(sample => sample.SourcePath)
                .ToArray();
            Check(
                "stage verification fixture owns five readable Validation Set samples",
                validationSamplePaths.All(File.Exists),
                string.Join(", ", validationSamplePaths.Select(Path.GetFileName)));
            shell.Workbench.SetValidationSetSources(validationSamplePaths);
            var stageWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = shell,
                ViewerContent = viewerOwner,
            };
            var stageHost = new Window
            {
                Content = stageWorkbench,
                Width = 1600,
                Height = 900,
                Left = -10000,
                Top = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            stageHost.Show();
            stageHost.UpdateLayout();
            Check(
                "Initial Workbench mode composes the unified Authoring cockpit",
                stageWorkbench.OperatorStage == OpenVisionOperatorStage.Teach
                && stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasAuthoringStageComposition
                && stageWorkbench.HasAdjacentViewerOutputs
                && stageWorkbench.HasStableStageHostedDataContexts,
                $"stage={stageWorkbench.OperatorStage}; authoring={stageWorkbench.HasAuthoringStageComposition}; outputs={stageWorkbench.HasAdjacentViewerOutputs}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; bottom={stageWorkbench.IsBottomPaneAttached}");

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            stageHost.UpdateLayout();
            Check(
                "Teach composes the compact step rail, dominant Viewer, and Selected Tool",
                shell.IsTeachWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Teach
                && stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasStableStageHostedDataContexts,
                $"stage={stageWorkbench.OperatorStage}; teach={stageWorkbench.HasTeachStageComposition}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; bottom={stageWorkbench.IsBottomPaneAttached}");
            Check(
                "Authoring entry normalization preserves recipe and selected-step identity",
                ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && string.Equals(selectedStepState, shell.Workbench.SelectedPipelineStep?.State, StringComparison.Ordinal),
                $"selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}");

            Check(
                "ROI capture can start for the selected Thickness step",
                shell.Workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null),
                shell.Workbench.TeachingSelectionCaptureInstruction);
            shell.Workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "active ROI Review blocks stage navigation",
                shell.Workbench.IsSelectionCandidateActive
                && !shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Inspect),
                shell.InspectionStageNavigationStatus);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            Check(
                "blocked stage execution leaves Teach active",
                shell.IsTeachWorkspaceSelected,
                $"stage={shell.SelectedWorkspaceMode}; status={shell.StatusText}");
            shell.Workbench.CancelTeachingSelectionCaptureCommand.Execute(null);

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            stageHost.UpdateLayout();
            Check(
                "Validate pairs full-height validation evidence with a dominant Viewer",
                shell.IsValidateWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Validate
                && stageWorkbench.HasValidateStageComposition
                && stageWorkbench.HasEvidenceLinkedViewerComposition
                && stageWorkbench.IsDedicatedValidationWorkspace
                && stageWorkbench.HasStableStageHostedDataContexts
                && stageWorkbench.HasLocalizedValidationNavigation
                && stageWorkbench.HasValidationSamplesFirstUseClarity
                && !stageWorkbench.IsValidationIssueNavigationVisible
                && stageWorkbench.HasFocusedValidationPaneTabs
                && stageWorkbench.VisibleValidationPaneTabCount == 1
                && stageWorkbench.ValidationSetSampleCount == 5
                && stageWorkbench.CanRunValidationSet
                && !stageWorkbench.IsBottomPaneAttached,
                $"stage={stageWorkbench.OperatorStage}; validate={stageWorkbench.HasValidateStageComposition}; linkedViewer={stageWorkbench.HasEvidenceLinkedViewerComposition}; dedicated={stageWorkbench.IsDedicatedValidationWorkspace}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; localized={stageWorkbench.HasLocalizedValidationNavigation}; firstUse={stageWorkbench.HasValidationSamplesFirstUseClarity}; issueNav={stageWorkbench.IsValidationIssueNavigationVisible}; focusedTabs={stageWorkbench.HasFocusedValidationPaneTabs}; visibleTabs={stageWorkbench.VisibleValidationPaneTabCount}; samples={stageWorkbench.ValidationSetSampleCount}; canRun={stageWorkbench.CanRunValidationSet}; bottom={stageWorkbench.IsBottomPaneAttached}");
            stageHost.Width = 1280;
            stageHost.UpdateLayout();
            Check(
                "Compact Validate clamps restored ratios to a readable action-pane width",
                stageWorkbench.IsCompactDockLayout
                && stageWorkbench.HasReadableValidationPaneRatio
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty,
                $"compact={stageWorkbench.IsCompactDockLayout}; readableRatio={stageWorkbench.HasReadableValidationPaneRatio}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}");
            stageHost.Width = 1600;
            stageHost.UpdateLayout();
            foreach (var section in Enum.GetValues<ValidationWorkspaceSection>())
            {
                stageWorkbench.SetValidationWorkspaceSection(section);
            }
            Check(
                "Validate exposes five local drill-down sections without leaving the stage",
                stageWorkbench.ActiveValidationWorkspaceSection == ValidationWorkspaceSection.HeldOut
                && shell.IsValidateWorkspaceSelected,
                $"section={stageWorkbench.ActiveValidationWorkspaceSection}; stage={shell.SelectedWorkspaceMode}");
            stageWorkbench.ActivateSessionLogPane();
            stageHost.UpdateLayout();
            Check(
                "Validate reveals one explicitly requested advanced support pane without execution",
                shell.IsValidateWorkspaceSelected
                && stageWorkbench.IsSessionLogPaneSelected
                && stageWorkbench.VisibleValidationPaneTabCount == 2
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsValidationSetRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={stageWorkbench.OperatorStage}; sessionLog={stageWorkbench.IsSessionLogPaneSelected}; visibleTabs={stageWorkbench.VisibleValidationPaneTabCount}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; validationRunning={shell.Workbench.IsValidationSetRunning}; previewRunning={shell.Workbench.IsSelectedStepPreviewRunning}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            stageHost.UpdateLayout();
            Check(
                "Results pairs one dedicated read-only evidence workspace with a dominant Viewer",
                shell.IsResultsWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Results
                && stageWorkbench.HasResultsStageComposition
                && stageWorkbench.HasEvidenceLinkedViewerComposition
                && stageWorkbench.IsDedicatedResultsWorkspace
                && stageWorkbench.HasStableStageHostedDataContexts
                && stageWorkbench.HasLocalizedResultsNavigation
                && !stageWorkbench.IsBottomPaneAttached,
                $"stage={stageWorkbench.OperatorStage}; results={stageWorkbench.HasResultsStageComposition}; linkedViewer={stageWorkbench.HasEvidenceLinkedViewerComposition}; dedicated={stageWorkbench.IsDedicatedResultsWorkspace}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; localized={stageWorkbench.HasLocalizedResultsNavigation}; bottom={stageWorkbench.IsBottomPaneAttached}");
            var resultsSelectedStep = shell.Workbench.SelectedPipelineStep;
            var resultsStepCount = shell.Workbench.PipelineSteps.Count;
            var resultsDirty = shell.Workbench.IsDirty;
            var resultsViewerOutput = shell.Workbench.CurrentViewerOutputSummary;
            var resultsRunSummary = shell.RunSnapshotSummary;
            foreach (var section in Enum.GetValues<ResultsWorkspaceSection>())
            {
                stageWorkbench.SetResultsWorkspaceSection(section);
            }
            Check(
                "Results exposes Run Record, Output Compare, and Reports locally without mutation",
                stageWorkbench.ActiveResultsWorkspaceSection == ResultsWorkspaceSection.Reports
                && shell.IsResultsWorkspaceSelected
                && ReferenceEquals(resultsSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == resultsStepCount
                && shell.Workbench.IsDirty == resultsDirty
                && string.Equals(resultsViewerOutput, shell.Workbench.CurrentViewerOutputSummary, StringComparison.Ordinal)
                && string.Equals(resultsRunSummary, shell.RunSnapshotSummary, StringComparison.Ordinal),
                $"section={stageWorkbench.ActiveResultsWorkspaceSection}; stage={shell.SelectedWorkspaceMode}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; output={shell.Workbench.CurrentViewerOutputSummary}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Expert);
            Check(
                "Advanced diagnostics remain an explicit full-layout route",
                shell.IsExpertWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Legacy
                && stageWorkbench.HasAllLegacySupportPaneTabs
                && stageWorkbench.GetDockingPaneContracts().Count == 12,
                $"stage={stageWorkbench.OperatorStage}; mode={shell.SelectedWorkspaceMode}; supportTabs={stageWorkbench.HasAllLegacySupportPaneTabs}; panes={stageWorkbench.GetDockingPaneContracts().Count}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            Check(
                "Results and Advanced navigation preserve recipe, output, and run evidence",
                ReferenceEquals(resultsSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == resultsStepCount
                && shell.Workbench.IsDirty == resultsDirty
                && string.Equals(resultsViewerOutput, shell.Workbench.CurrentViewerOutputSummary, StringComparison.Ordinal)
                && string.Equals(resultsRunSummary, shell.RunSnapshotSummary, StringComparison.Ordinal),
                $"stage={stageWorkbench.OperatorStage}; selected={shell.Workbench.SelectedPipelineStep?.Id}; dirty={shell.Workbench.IsDirty}; output={shell.Workbench.CurrentViewerOutputSummary}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Workbench);
            Check(
                "all stage navigation is presentation-only",
                stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasAuthoringStageComposition
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && string.Equals(selectedStepState, shell.Workbench.SelectedPipelineStep?.State, StringComparison.Ordinal),
                $"stage={stageWorkbench.OperatorStage}; selected={shell.Workbench.SelectedPipelineStep?.Id}; state={shell.Workbench.SelectedPipelineStep?.State}; dirty={shell.Workbench.IsDirty}");
            stageHost.Close();

            var ownerPathShell = new ShellMainWindowViewModel();
            var ownerPathRecipe = validationFixture.RecipePath;
            var ownerPathRunRecord = WriteOwnerPathRunRecord(
                validationFixture);
            var ownerPathOpenMessage = string.Empty;
            var ownerPathRunMessage = string.Empty;
            Check(
                "IA-4b owner path opens the controlled recipe, five labeled samples, and Fail Run Record",
                ownerPathShell.Workbench.TryOpenTeachingRecipe(
                    ownerPathRecipe,
                    out ownerPathOpenMessage)
                && ownerPathShell.Workbench.ValidationSetSamples.Count == 5
                && ownerPathShell.LoadRunRecord(
                    ownerPathRunRecord,
                    out ownerPathRunMessage)
                && ownerPathShell.InspectionSteps.Count == 1,
                $"{ownerPathOpenMessage}; run={ownerPathRunMessage}; samples={ownerPathShell.Workbench.ValidationSetSamples.Count}; runSteps={ownerPathShell.InspectionSteps.Count}");
            Task.Run(() =>
                    ownerPathShell.Workbench.RunValidationSetAsync())
                .GetAwaiter()
                .GetResult();
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            var ownerPathWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = ownerPathShell,
                ViewerContent = viewerOwner,
            };
            var ownerPathHost = new Window
            {
                Content = ownerPathWorkbench,
                Width = 1600,
                Height = 900,
                Left = -10000,
                Top = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            ownerPathHost.Show();
            ownerPathHost.UpdateLayout();
            ownerPathWorkbench.SetValidationWorkspaceSection(
                ValidationWorkspaceSection.Samples);
            ownerPathHost.UpdateLayout();
            Check(
                "Validate exposes one stable keyboard-focusable sample-set action",
                ownerPathWorkbench.HasAccessibleValidationSampleSetAction
                && ownerPathWorkbench.HasValidationSamplesFirstUseClarity
                && !ownerPathWorkbench.IsValidationIssueNavigationVisible
                && ownerPathShell.Workbench.RunValidationSetCommand
                    .CanExecute(null),
                $"accessible={ownerPathWorkbench.HasAccessibleValidationSampleSetAction}; firstUse={ownerPathWorkbench.HasValidationSamplesFirstUseClarity}; issueNav={ownerPathWorkbench.IsValidationIssueNavigationVisible}; canRun={ownerPathShell.Workbench.RunValidationSetCommand.CanExecute(null)}");
            ownerPathWorkbench.SetValidationWorkspaceSection(
                ValidationWorkspaceSection.Failures);
            ownerPathHost.UpdateLayout();
            var ownerFailureStepId =
                ownerPathShell.Workbench.SelectedValidationSetStep?.StepId;
            Check(
                "Failure Analysis leads with selected sample rule and reason before technical evidence",
                ownerPathWorkbench.HasValidationFailureOperatorSummary
                && ownerPathWorkbench.HasValidationResultsReviewControls
                && ownerPathWorkbench.IsValidationIssueNavigationVisible,
                $"summary={ownerPathWorkbench.HasValidationFailureOperatorSummary}; reviewControls={ownerPathWorkbench.HasValidationResultsReviewControls}; issueNav={ownerPathWorkbench.IsValidationIssueNavigationVisible}; sample={ownerPathShell.Workbench.SelectedValidationSetSample?.FileName}; rule={ownerPathShell.Workbench.SelectedValidationSetStep?.ToolName}; reason={ownerPathShell.Workbench.SelectedValidationSetStep?.Evidence}");
            Check(
                "IA-4b explicit sample-set execution exposes a real failure-to-Teach route",
                ownerPathShell.Workbench.ValidationSetFailCount > 0
                && ownerPathShell.Workbench.SelectedValidationSetSample
                    ?.Status == "Fail"
                && ownerPathShell
                    .OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null),
                $"pass={ownerPathShell.Workbench.ValidationSetPassCount}; fail={ownerPathShell.Workbench.ValidationSetFailCount}; error={ownerPathShell.Workbench.ValidationSetErrorCount}; selected={ownerPathShell.Workbench.SelectedValidationSetSample?.Status}; step={ownerFailureStepId}");
            var ownerRecipePath = ownerPathShell.Workbench.RecipePath;
            var ownerSourcePath = ownerPathShell.Workbench.Source.Path;
            var ownerStepCount = ownerPathShell.Workbench.PipelineSteps.Count;
            var ownerDirty = ownerPathShell.Workbench.IsDirty;
            var ownerValidationSummary =
                ownerPathShell.Workbench.ValidationSetSummary;
            var ownerRunSummary = ownerPathShell.RunSnapshotSummary;
            var ownerInspectionSummary =
                ownerPathShell.InspectionStepSummary;
            ownerPathShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            ownerPathHost.UpdateLayout();
            var ownerSelectedStepId =
                ownerPathShell.Workbench.SelectedPipelineStep?.Id;
            Check(
                "IA-4b failure opens its owning Teach step without mutation or hidden execution",
                ownerPathShell.IsTeachWorkspaceSelected
                && string.Equals(
                    ownerSelectedStepId,
                    ownerFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; selected={ownerSelectedStepId}; failureStep={ownerFailureStepId}; dirty={ownerDirty}->{ownerPathShell.Workbench.IsDirty}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            var correctionContext =
                ownerPathShell.Workbench.ActiveValidationFailureCorrectionContext;
            Check(
                "Teach correction carries the failed sample, owning rule, reason, and cell summary",
                correctionContext is not null
                && correctionContext.SampleName.Length > 0
                && string.Equals(
                    correctionContext.StepId,
                    ownerFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && correctionContext.ToolName.Length > 0
                && correctionContext.Reason.Length > 0
                && correctionContext.CellSummary.Contains(
                    "failed cells",
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathWorkbench.IsFailureCorrectionContextVisible,
                $"sample={correctionContext?.SampleName}; step={correctionContext?.StepId}; rule={correctionContext?.ToolName}; reason={correctionContext?.Reason}; cells={correctionContext?.CellSummary}; card={ownerPathWorkbench.IsFailureCorrectionContextVisible}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            ownerPathHost.Width = 1100;
            ownerPathHost.Height = 760;
            ownerPathHost.UpdateLayout();
            ownerPathShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            ownerPathHost.UpdateLayout();
            Check(
                "Compact failure-to-Teach opens Selected Tool automatically without hidden execution",
                ownerPathShell.IsTeachWorkspaceSelected
                && ownerPathWorkbench.IsCompactDockLayout
                && ownerPathWorkbench.IsToolInspectorPaneSelected
                && ownerPathWorkbench.IsFailureCorrectionContextVisible
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; compact={ownerPathWorkbench.IsCompactDockLayout}; selectedTool={ownerPathWorkbench.IsToolInspectorPaneSelected}; card={ownerPathWorkbench.IsFailureCorrectionContextVisible}; dirty={ownerDirty}->{ownerPathShell.Workbench.IsDirty}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Review);
            ownerPathWorkbench.SetResultsWorkspaceSection(
                ResultsWorkspaceSection.RunRecord);
            ownerPathHost.UpdateLayout();
            Check(
                "Results leads with operator decision steps and a keyboard-focusable correction route",
                ownerPathWorkbench.HasResultsOperatorSummary
                && ownerPathShell
                    .OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null),
                $"summary={ownerPathWorkbench.HasResultsOperatorSummary}; decision={ownerPathShell.RunSnapshotSummary}; steps={ownerPathShell.InspectionStepSummary}; canFix={ownerPathShell.OpenSelectedValidationIssueInTeachCommand.CanExecute(null)}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Expert);
            ownerPathHost.UpdateLayout();
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Review);
            ownerPathWorkbench.SetResultsWorkspaceSection(
                ResultsWorkspaceSection.RunRecord);
            ownerPathHost.UpdateLayout();
            Check(
                "IA-4b Results to Advanced to Results preserves recipe, failure, validation, output, and Run Record evidence",
                ownerPathShell.IsResultsWorkspaceSelected
                && ownerPathWorkbench.HasResultsStageComposition
                && ownerPathWorkbench.HasStableStageHostedDataContexts
                && string.Equals(
                    ownerPathShell.Workbench.RecipePath,
                    ownerRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    ownerPathShell.Workbench.Source.Path,
                    ownerSourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.PipelineSteps.Count
                    == ownerStepCount
                && string.Equals(
                    ownerPathShell.Workbench.SelectedPipelineStep?.Id,
                    ownerSelectedStepId,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && string.Equals(
                    ownerPathShell.Workbench.ValidationSetSummary,
                    ownerValidationSummary,
                    StringComparison.Ordinal)
                && ownerPathShell.Workbench.ValidationSetFailCount > 0
                && string.Equals(
                    ownerPathShell.RunSnapshotSummary,
                    ownerRunSummary,
                    StringComparison.Ordinal)
                && string.Equals(
                    ownerPathShell.InspectionStepSummary,
                    ownerInspectionSummary,
                    StringComparison.Ordinal)
                && ownerPathShell.InspectionSteps.Count == 1,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; recipe={ownerPathShell.Workbench.RecipePath}; selected={ownerPathShell.Workbench.SelectedPipelineStep?.Id}; dirty={ownerPathShell.Workbench.IsDirty}; validation={ownerPathShell.Workbench.ValidationSetSummary}; run={ownerPathShell.RunSnapshotSummary}; runSteps={ownerPathShell.InspectionSteps.Count}");

            var beforeLayoutRecipePath = ownerPathShell.Workbench.RecipePath;
            var beforeLayoutDirty = ownerPathShell.Workbench.IsDirty;
            var beforeLayoutStepCount = ownerPathShell.Workbench.PipelineSteps.Count;
            var beforeLayoutValidationSummary =
                ownerPathShell.Workbench.ValidationSetSummary;
            var customDockState = OpenVisionDockPresentationState.Default with
            {
                Wide = OpenVisionDockPresentationState.Default.Wide with
                {
                    ValidateEvidence = 1.25,
                    ValidateViewer = 3.05,
                    ResultsEvidence = 1.30,
                    ResultsViewer = 3.00,
                },
                PrimaryContentId = "displayed-outputs",
                SupportContentId = "tool-inspector",
            };
            ownerPathWorkbench.ApplyDockPresentationState(customDockState);
            ownerPathWorkbench.ResetDockPresentationState();
            Check(
                "layout apply and reset are presentation-only",
                string.Equals(
                    ownerPathShell.Workbench.RecipePath,
                    beforeLayoutRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == beforeLayoutDirty
                && ownerPathShell.Workbench.PipelineSteps.Count
                    == beforeLayoutStepCount
                && string.Equals(
                    ownerPathShell.Workbench.ValidationSetSummary,
                    beforeLayoutValidationSummary,
                    StringComparison.Ordinal)
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"recipe={beforeLayoutRecipePath}; dirty={beforeLayoutDirty}->{ownerPathShell.Workbench.IsDirty}; steps={beforeLayoutStepCount}->{ownerPathShell.Workbench.PipelineSteps.Count}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            ownerPathHost.Close();

            var layoutVerificationDirectory = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(reportPath))
                    ?? Environment.CurrentDirectory,
                $"layout-profile-{Guid.NewGuid():N}");
            Directory.CreateDirectory(layoutVerificationDirectory);
            var layoutPath = Path.Combine(
                layoutVerificationDirectory,
                "studio-layout.json");
            var layoutStore = new StudioLayoutProfileStore(layoutPath);
            var savedProfile = StudioLayoutProfile.Default with
            {
                Workbench = customDockState,
                Window = new StudioWindowPlacement(
                    SystemParameters.VirtualScreenLeft + 20,
                    SystemParameters.VirtualScreenTop + 20,
                    1440,
                    900,
                    IsMaximized: false),
            };
            layoutStore.Save(savedProfile);
            var restoredLayout = layoutStore.Load();
            Check(
                "safe layout profile save and reload round-trip preserves allowlisted presentation state",
                restoredLayout.Status == StudioLayoutLoadStatus.Restored
                && restoredLayout.Profile.Workbench.Wide.ValidateEvidence
                    == 1.25
                && restoredLayout.Profile.Workbench.Wide.ValidateViewer
                    == 3.05
                && restoredLayout.Profile.Workbench.PrimaryContentId
                    == "displayed-outputs"
                && restoredLayout.Profile.Workbench.SupportContentId
                    == "tool-inspector",
                $"status={restoredLayout.Status}; evidence={restoredLayout.Profile.Workbench.Wide.ValidateEvidence}; viewer={restoredLayout.Profile.Workbench.Wide.ValidateViewer}; primary={restoredLayout.Profile.Workbench.PrimaryContentId}; support={restoredLayout.Profile.Workbench.SupportContentId}");
            Check(
                "layout save is atomic and leaves no temporary sidecar",
                !Directory.EnumerateFiles(
                        layoutVerificationDirectory,
                        "*.tmp",
                        SearchOption.TopDirectoryOnly)
                    .Any(),
                layoutVerificationDirectory);

            File.WriteAllText(layoutPath, "{ not-json");
            var corruptLayout = layoutStore.Load();
            Check(
                "corrupt layout fails safely to defaults and disables automatic overwrite",
                corruptLayout.Status == StudioLayoutLoadStatus.Corrupt
                && !corruptLayout.CanAutoSave
                && corruptLayout.Profile == StudioLayoutProfile.Default,
                $"status={corruptLayout.Status}; canAutoSave={corruptLayout.CanAutoSave}; message={corruptLayout.Message}");

            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(
                    savedProfile with { SchemaVersion = 999 },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            var incompatibleLayout = layoutStore.Load();
            Check(
                "incompatible layout schema fails safely to defaults",
                incompatibleLayout.Status
                    == StudioLayoutLoadStatus.Incompatible
                && !incompatibleLayout.CanAutoSave,
                $"status={incompatibleLayout.Status}; canAutoSave={incompatibleLayout.CanAutoSave}; message={incompatibleLayout.Message}");

            var unsafeDockState = customDockState with
            {
                Wide = customDockState.Wide with
                {
                    ValidateEvidence = 99,
                },
                PrimaryContentId = "unknown-content",
                SupportContentId = "unknown-support",
            };
            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(
                    savedProfile with
                    {
                        Workbench = unsafeDockState,
                        Window = new StudioWindowPlacement(
                            999999,
                            999999,
                            1200,
                            800,
                            IsMaximized: false),
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            var sanitizedLayout = layoutStore.Load();
            Check(
                "unknown IDs invalid ratios and off-screen bounds are sanitized",
                sanitizedLayout.Status
                    == StudioLayoutLoadStatus.RestoredWithFallback
                && sanitizedLayout.Profile.Window is null
                && sanitizedLayout.Profile.Workbench.Wide.ValidateEvidence
                    == OpenVisionDockPresentationState.Default.Wide
                        .ValidateEvidence
                && sanitizedLayout.Profile.Workbench.PrimaryContentId
                    == OpenVisionDockPresentationState.Default.PrimaryContentId
                && sanitizedLayout.Profile.Workbench.SupportContentId
                    == OpenVisionDockPresentationState.Default.SupportContentId,
                $"status={sanitizedLayout.Status}; window={sanitizedLayout.Profile.Window}; evidence={sanitizedLayout.Profile.Workbench.Wide.ValidateEvidence}; primary={sanitizedLayout.Profile.Workbench.PrimaryContentId}; support={sanitizedLayout.Profile.Workbench.SupportContentId}");
            layoutStore.Reset();
            Check(
                "explicit layout reset removes only the selected profile",
                !File.Exists(layoutPath)
                && Directory.Exists(layoutVerificationDirectory),
                $"profileExists={File.Exists(layoutPath)}; directoryExists={Directory.Exists(layoutVerificationDirectory)}");

            var advancedMarker = new object();
            var advanced = new OpenVisionDockWorkspaceView
            {
                ToolLibraryContent = advancedMarker,
                DataLayersContent = advancedMarker,
                ViewerContent = advancedMarker,
                ToolInspectorContent = advancedMarker,
                EvidenceContent = advancedMarker,
                OutputCompareContent = advancedMarker,
                DisplayedOutputsContent = advancedMarker,
                LinkedViewContent = advancedMarker,
                ProfileContent = advancedMarker,
                FitDiagnosticsContent = advancedMarker,
                IntersectionEvidenceContent = advancedMarker,
                CorrespondenceEvidenceContent = advancedMarker,
            };
            var advancedContracts = advanced.GetDockingPaneContracts();
            Check("Advanced exposes twelve dock panes", advancedContracts.Count == 12 && HasExactIds(advancedContracts, WorkbenchContentIds), Describe(advancedContracts));
            Check("Advanced panes can float and remain required", advancedContracts.All(contract => contract.CanFloat && !contract.CanClose) && advancedContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(advancedContracts));
            var reactivatedAdvancedViewer = new object();
            Check(
                "Advanced reactivation owns the requested Viewer in its live presenter",
                advanced.ReactivateViewerContent(reactivatedAdvancedViewer),
                $"viewer={ReferenceEquals(advanced.ViewerContent, reactivatedAdvancedViewer)}");

            var repositoryRoot = FindRepositoryRoot();
            var advancedXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "MainWindow.xaml"));
            var legacyAdvancedColors = new[]
            {
                "#ffffff",
                "#d1d5db",
                "#111827",
                "#6b7280",
                "#374151",
                "#b45309",
                "#b91c1c",
                "#f9fafb",
            };
            var advancedThemeResources = new[]
            {
                "ThreeD.PanelBrush",
                "ThreeD.PanelAlternateBrush",
                "ThreeD.ViewportBrush",
                "ThreeD.DividerBrush",
                "ThreeD.TextBrush",
                "ThreeD.PrimaryTextBrush",
                "ThreeD.SecondaryTextBrush",
                "ThreeD.MutedTextBrush",
                "ThreeD.WarningBrush",
                "ThreeD.FailBrush",
            };
            Check(
                "Advanced content surfaces use semantic theme resources without the legacy light palette",
                legacyAdvancedColors.All(color => !advancedXaml.Contains(color, StringComparison.OrdinalIgnoreCase))
                && advancedThemeResources.All(resource => advancedXaml.Contains(resource, StringComparison.Ordinal)),
                $"legacy={string.Join(',', legacyAdvancedColors.Where(color => advancedXaml.Contains(color, StringComparison.OrdinalIgnoreCase)))}; semantic={advancedThemeResources.Count(resource => advancedXaml.Contains(resource, StringComparison.Ordinal))}/{advancedThemeResources.Length}");

            var themeXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "Themes",
                "OpenVisionThreeDTheme.xaml"));
            var dockXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Docking.Controls",
                "Views",
                "OpenVisionDockWorkspaceView.xaml"));
            var controlStateMarkers = new[]
            {
                "IsMouseOver",
                "IsKeyboardFocusWithin",
                "Validation.HasError",
                "IsReadOnly",
                "IsEnabled",
                "ThreeD.AccentHoverBrush",
                "ThreeD.FocusBrush",
                "ThreeD.FailBrush",
                "ThreeD.DisabledSurfaceBrush",
            };
            var dockStateMarkers = new[]
            {
                "IsMouseOver",
                "IsSelected",
                "IsKeyboardFocused",
                "IsEnabled",
                "ThreeD.PanelAlternateBrush",
                "ThreeD.SelectedSurfaceBrush",
                "ThreeD.FocusBrush",
                "ThreeD.DisabledBrush",
            };
            Check(
                "Advanced generated controls retain semantic hover focus selected disabled read-only and validation states",
                controlStateMarkers.All(marker => themeXaml.Contains(marker, StringComparison.Ordinal))
                && dockStateMarkers.All(marker => dockXaml.Contains(marker, StringComparison.Ordinal)),
                $"controls={controlStateMarkers.Count(marker => themeXaml.Contains(marker, StringComparison.Ordinal))}/{controlStateMarkers.Length}; dockTabs={dockStateMarkers.Count(marker => dockXaml.Contains(marker, StringComparison.Ordinal))}/{dockStateMarkers.Length}");

            var calibrationMarker = new object();
            var calibration = new OpenVisionCalibrationDockWorkspaceView
            {
                ExplorerContent = calibrationMarker,
                WorkspaceContent = calibrationMarker,
                InspectorContent = calibrationMarker,
                EvidenceContent = calibrationMarker,
            };
            var calibrationContracts = calibration.GetDockingPaneContracts();
            Check("Calibration exposes four dock panes", calibrationContracts.Count == 4 && HasExactIds(calibrationContracts, CalibrationContentIds), Describe(calibrationContracts));
            Check("Calibration panes can float and cannot close", calibrationContracts.All(contract => contract.CanFloat && !contract.CanClose), Describe(calibrationContracts));
            Check("Calibration anchorables cannot hide", calibrationContracts.Where(contract => contract.CanHide.HasValue).All(contract => contract.CanHide == false), Describe(calibrationContracts));
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        var reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
        }

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Docking workspace verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static bool HasExactIds(
        IReadOnlyList<DockingPaneContract> contracts,
        IReadOnlyList<string> expectedIds) =>
        contracts.Select(contract => contract.ContentId).Order(StringComparer.Ordinal)
            .SequenceEqual(expectedIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
        && contracts.Select(contract => contract.ContentId).Distinct(StringComparer.Ordinal).Count() == contracts.Count;

    private static string WriteOwnerPathRunRecord(
        CompletenessValidationVerificationFixture fixture)
    {
        var path = Path.Combine(fixture.RootPath, "owner-path-run-record.json");
        var document = fixture.Document;
        var step = document.Steps.Single();
        var metrics = new[]
        {
            new InspectionRunMetric(
                "Passed cells",
                MetricKind.Count,
                2,
                "cells",
                ResultStatus.Fail),
            new InspectionRunMetric(
                "Failed cells",
                MetricKind.Count,
                2,
                "cells",
                ResultStatus.Fail),
        };
        var stepResult = new InspectionRunStepResult(
            0,
            step.Id,
            step.ToolId,
            step.ToolName,
            step.InputEntityIds,
            step.OutputEntityId,
            ResultStatus.Fail,
            "Completeness Grid Fail: 2 passed, 2 failed, 4 total cells.",
            0,
            metrics,
            []);
        var record = new InspectionRunRecord(
            "1.5",
            "run-workbench-owner-path-fixture",
            new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero),
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                fixture.RecipePath,
                Convert.ToHexString(SHA256.HashData(
                    File.ReadAllBytes(fixture.RecipePath)))),
            new InspectionRunSource(
                document.Source.Id,
                document.Source.Path,
                document.Source.ContentSha256!,
                document.Source.ByteLength
                    ?? new FileInfo(document.Source.Path).Length,
                document.Source.Unit),
            "Ordered Tool Recipe Replay",
            ResultStatus.Fail,
            "Executed 1 authored typed step in recipe order.",
            0,
            metrics,
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(fixture.RootPath, "owner-path-runner.txt"),
                null,
                null,
                path,
                null,
                null))
        {
            Steps = [stepResult],
        };
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                record,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() },
                }));
        return path;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, ThicknessRecipeRelativePath)))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository file {ThicknessRecipeRelativePath}.");
    }

    private static string Describe(IEnumerable<DockingPaneContract> contracts) =>
        string.Join(
            "; ",
            contracts.Select(contract =>
                $"{contract.ContentId}:'{contract.Title}'[float={contract.CanFloat},close={contract.CanClose},hide={contract.CanHide?.ToString() ?? "n/a"},content={contract.HasContent}]"));
}
