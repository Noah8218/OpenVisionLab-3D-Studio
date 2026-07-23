using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolWorkbenchDockingVerification
{
    private static readonly string[] WorkbenchContentIds =
    [
        "tool-library",
        "data-layers",
        "three-d-viewer",
        "tool-inspector",
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
            Check("Workbench exposes twelve dock panes", workbenchContracts.Count == 12, Describe(workbenchContracts));
            Check(
                "Workbench separates Tool Library and Recipe Flow without hosting Recipe Center",
                HasExactIds(workbenchContracts, WorkbenchContentIds)
                && workbenchContracts[0].ContentId == "tool-library"
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[0].HasContent
                && workbenchContracts[1].HasContent,
                Describe(workbenchContracts));
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
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
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

    private static string Describe(IEnumerable<DockingPaneContract> contracts) =>
        string.Join(
            "; ",
            contracts.Select(contract =>
                $"{contract.ContentId}:'{contract.Title}'[float={contract.CanFloat},close={contract.CanClose},hide={contract.CanHide?.ToString() ?? "n/a"},content={contract.HasContent}]"));
}
