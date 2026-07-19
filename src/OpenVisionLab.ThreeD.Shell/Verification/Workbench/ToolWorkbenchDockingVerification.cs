using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolWorkbenchDockingVerification
{
    private static readonly string[] WorkbenchContentIds =
    [
        "data-layers",
        "three-d-viewer",
        "tool-inspector",
        "evidence-workbench",
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
            Check("Workbench exposes nine dock panes", workbenchContracts.Count == 9, Describe(workbenchContracts));
            Check(
                "Workbench left pane is Toolbox and does not host Recipe Manager",
                HasExactIds(workbenchContracts, WorkbenchContentIds)
                && workbenchContracts.Select(contract => contract.Title).SequenceEqual(
                    ["Toolbox & Entities", "3D View", "Step Parameters", "Pipeline / Validation", "Session Log", "Height Profile", "Fit Diagnostics", "Intersection Evidence", "Correspondence Evidence"],
                    StringComparer.Ordinal),
                Describe(workbenchContracts));
            Check("Workbench hosts all nine dockable views", workbench.HasAllDockContentHosts && workbenchContracts.All(contract => contract.HasContent), Describe(workbenchContracts));
            Check("Workbench panes can float", workbenchContracts.All(contract => contract.CanFloat), Describe(workbenchContracts));
            Check("Workbench required panes cannot close", workbenchContracts.All(contract => !contract.CanClose), Describe(workbenchContracts));
            Check("Fit Diagnostics may hide without closing", workbenchContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(workbenchContracts));

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
            workbench.ActivateFitDiagnosticsPane();
            Check("Workbench Fit Diagnostics command selects docked diagnostics pane", workbench.IsBottomPaneAttached && workbench.IsFitDiagnosticsPaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFitDiagnosticsPaneSelected}");
            workbench.ActivateIntersectionEvidencePane();
            Check("Workbench Intersection Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsIntersectionEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsIntersectionEvidencePaneSelected}");
            workbench.ActivateCorrespondenceEvidencePane();
            Check("Workbench Correspondence Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsCorrespondenceEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsCorrespondenceEvidencePaneSelected}");

            var advancedMarker = new object();
            var advanced = new OpenVisionDockWorkspaceView
            {
                DataLayersContent = advancedMarker,
                ViewerContent = advancedMarker,
                ToolInspectorContent = advancedMarker,
                EvidenceContent = advancedMarker,
                LinkedViewContent = advancedMarker,
                ProfileContent = advancedMarker,
                FitDiagnosticsContent = advancedMarker,
                IntersectionEvidenceContent = advancedMarker,
                CorrespondenceEvidenceContent = advancedMarker,
            };
            var advancedContracts = advanced.GetDockingPaneContracts();
            Check("Advanced exposes nine dock panes", advancedContracts.Count == 9 && HasExactIds(advancedContracts, WorkbenchContentIds), Describe(advancedContracts));
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
