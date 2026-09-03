using System.IO;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class DisplayedOutputsOwnerVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench Displayed Outputs owner verification"
        };
        var passed = 0;
        var total = 0;
        var fullReportPath = Path.GetFullPath(reportPath);

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            var filterTool = ToolWorkbenchToolCatalog.Create()
                .Single(tool => tool.Id == "filter");
            var filterStep = new ToolWorkbenchPipelineStepItem(
                "step.filter.displayed-output",
                filterTool,
                "source.displayed-output",
                "output.displayed-a");
            filterStep.Order = "01";

            static ToolWorkbenchArtifactItem CreateArtifact(
                string id,
                string displayName,
                string state,
                string contentHash,
                ToolWorkbenchPipelineStepItem? step,
                string nodeKind) =>
                new(
                    id,
                    displayName,
                    nodeKind == "Source" ? "SourceC3D" : "HeightField",
                    state,
                    "source.displayed-output",
                    "source.displayed-output",
                    "µm",
                    "frame.displayed-output",
                    contentHash,
                    $"{displayName} detail",
                    step,
                    nodeKind);

            var renderableA = CreateArtifact(
                "output.displayed-a",
                "Renderable A",
                "Published",
                new string('a', 64),
                filterStep,
                "HeightField");
            var renderableB = CreateArtifact(
                "output.displayed-b",
                "Renderable B",
                "Published",
                new string('b', 64),
                filterStep,
                "HeightField");
            var renderableC = CreateArtifact(
                "output.displayed-c",
                "Renderable C",
                "Published",
                new string('c', 64),
                filterStep,
                "HeightField");
            var evidenceOnly = CreateArtifact(
                "evidence.displayed",
                "Evidence output",
                "Published",
                new string('d', 64),
                null,
                "MeasurementResult");
            var stale = CreateArtifact(
                "output.stale",
                "Stale output",
                "Stale",
                new string('e', 64),
                null,
                "HeightField");
            var missing = CreateArtifact(
                "output.missing",
                "Missing output",
                "Published",
                new string('f', 64),
                null,
                "HeightField");
            var blankPath = CreateArtifact(
                "output.blank-path",
                "Blank path output",
                "Published",
                new string('1', 64),
                null,
                "HeightField");
            var source = CreateArtifact(
                "source.displayed-output",
                "Displayed source",
                "Ready",
                new string('2', 64),
                null,
                "Source");
            var artifactSnapshot = new[]
            {
                source,
                renderableA,
                renderableB,
                renderableC,
                evidenceOnly,
                stale,
                missing,
                blankPath
            };
            var currentArtifacts = artifactSnapshot;

            var targetById = new Dictionary<string, ToolWorkbenchRenderableC3DTarget>(
                StringComparer.OrdinalIgnoreCase)
            {
                [renderableA.Id] = new(
                    renderableA.Id,
                    renderableA.DisplayName,
                    renderableA.Contract,
                    renderableA.State,
                    "a.c3d",
                    renderableA.Detail,
                    false,
                    true,
                    null,
                    "filter"),
                [renderableB.Id] = new(
                    renderableB.Id,
                    renderableB.DisplayName,
                    renderableB.Contract,
                    renderableB.State,
                    "b.c3d",
                    renderableB.Detail,
                    false,
                    true,
                    null,
                    "filter"),
                [renderableC.Id] = new(
                    renderableC.Id,
                    renderableC.DisplayName,
                    renderableC.Contract,
                    renderableC.State,
                    "c.c3d",
                    renderableC.Detail,
                    false,
                    true,
                    null,
                    "filter"),
                [stale.Id] = new(
                    stale.Id,
                    stale.DisplayName,
                    stale.Contract,
                    stale.State,
                    "stale.c3d",
                    stale.Detail,
                    false,
                    false,
                    null,
                    "filter"),
                [blankPath.Id] = new(
                    blankPath.Id,
                    blankPath.DisplayName,
                    blankPath.Contract,
                    blankPath.State,
                    string.Empty,
                    blankPath.Detail,
                    false,
                    true,
                    null,
                    "filter")
            };

            string compareSlotA = string.Empty;
            string compareSlotB = string.Empty;
            string compareSlotC = string.Empty;
            ToolWorkbenchDisplayedOutputsOwner? owner = null;
            var displayRequestCount = 0;
            var displayCommitCount = 0;
            var focusCount = 0;
            var refreshCount = 0;
            var allowDisplay = true;
            var displayedArtifactId = string.Empty;
            var requestedPath = string.Empty;
            ToolWorkbenchPipelineStepItem? focusedStep = null;

            string GetComparePins(string artifactId)
            {
                var slots = new List<string>(3);
                if (string.Equals(compareSlotA, artifactId, StringComparison.OrdinalIgnoreCase))
                {
                    slots.Add("A");
                }

                if (string.Equals(compareSlotB, artifactId, StringComparison.OrdinalIgnoreCase))
                {
                    slots.Add("B");
                }

                if (string.Equals(compareSlotC, artifactId, StringComparison.OrdinalIgnoreCase))
                {
                    slots.Add("C");
                }

                return slots.Count == 0 ? string.Empty : string.Join(", ", slots);
            }

            bool TryPin(string artifactId)
            {
                if (string.IsNullOrWhiteSpace(compareSlotA))
                {
                    compareSlotA = artifactId;
                }
                else if (string.IsNullOrWhiteSpace(compareSlotB))
                {
                    compareSlotB = artifactId;
                }
                else if (string.IsNullOrWhiteSpace(compareSlotC))
                {
                    compareSlotC = artifactId;
                }
                else
                {
                    return false;
                }

                owner?.RefreshPresentation();
                return true;
            }

            owner = new ToolWorkbenchDisplayedOutputsOwner(
                () => currentArtifacts,
                id => id is not null && targetById.TryGetValue(id, out var target)
                    ? target
                    : null,
                ThreeDLocalization.Shared,
                GetComparePins,
                () => string.IsNullOrWhiteSpace(compareSlotA)
                      || string.IsNullOrWhiteSpace(compareSlotB)
                      || string.IsNullOrWhiteSpace(compareSlotC),
                TryPin,
                id =>
                {
                    displayCommitCount++;
                    displayedArtifactId = id;
                },
                step =>
                {
                    focusCount++;
                    focusedStep = step;
                },
                () => refreshCount++);
            owner.ViewerArtifactDisplayRequested += (_, request) =>
            {
                displayRequestCount++;
                requestedPath = request.C3DPath;
                request.WasDisplayed = allowDisplay;
            };

            var publicCollectionReference = owner.DisplayedOutputs;
            owner.Rebuild();
            Check(
                "owner projects the explicit artifact snapshot in stable order and keeps collection identity",
                ReferenceEquals(publicCollectionReference, owner.DisplayedOutputs)
                && owner.DisplayedOutputs.Select(item => item.Id)
                    .SequenceEqual(artifactSnapshot.Select(item => item.Id)),
                $"count={owner.DisplayedOutputs.Count};identity=stable;order={string.Join(',', owner.DisplayedOutputs.Select(item => item.Id))}");
            Check(
                "owner preserves artifact metadata while separating renderable and evidence-only presentation",
                owner.DisplayedOutputs.Single(item => item.Id == renderableA.Id) is
                {
                    Contract: "HeightField",
                    Detail: "Renderable A detail",
                    NodeKind: "HeightField",
                    IsRenderableInViewer: true,
                    CanShowInViewer: true
                }
                && owner.DisplayedOutputs.Single(item => item.Id == evidenceOnly.Id) is
                {
                    IsRenderableInViewer: false,
                    IsEvidenceOnly: true,
                    HasNoCurrentOutput: false,
                    CanShowInViewer: false,
                    CanPinToCompare: false
                },
                $"renderable={owner.DisplayedOutputs.Single(item => item.Id == renderableA.Id).Availability};evidence={owner.DisplayedOutputs.Single(item => item.Id == evidenceOnly.Id).Availability}");
            Check(
                "stale, missing, and blank-path catalog targets fail closed",
                owner.DisplayedOutputs.Single(item => item.Id == stale.Id) is
                {
                    IsRenderableInViewer: false,
                    CanShowInViewer: false,
                    HasNoCurrentOutput: true
                }
                && owner.DisplayedOutputs.Single(item => item.Id == missing.Id).CanShowInViewer == false
                && owner.DisplayedOutputs.Single(item => item.Id == blankPath.Id).CanShowInViewer == false,
                $"stale={owner.DisplayedOutputs.Single(item => item.Id == stale.Id).CanShowInViewer};missing={owner.DisplayedOutputs.Single(item => item.Id == missing.Id).CanShowInViewer};blank={owner.DisplayedOutputs.Single(item => item.Id == blankPath.Id).CanShowInViewer}");

            var renderableAItem = owner.DisplayedOutputs.Single(item => item.Id == renderableA.Id);
            var requestCountBeforeShow = displayRequestCount;
            owner.ShowDisplayedOutputInViewerCommand.Execute(renderableAItem);
            Check(
                "valid display request preserves the event contract and commits Viewer presentation only after acceptance",
                displayRequestCount == requestCountBeforeShow + 1
                && displayCommitCount == 1
                && displayedArtifactId == renderableA.Id
                && requestedPath == "a.c3d"
                && renderableAItem.IsShownInViewer
                && refreshCount > 0,
                $"requests={displayRequestCount};commits={displayCommitCount};id={displayedArtifactId};path={requestedPath}");

            allowDisplay = false;
            var rejectedCommitCount = displayCommitCount;
            owner.ShowDisplayedOutputInViewerCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == renderableB.Id));
            Check(
                "Viewer rejection does not commit or change the selected display",
                displayCommitCount == rejectedCommitCount
                && displayedArtifactId == renderableA.Id
                && renderableAItem.IsShownInViewer
                && !owner.DisplayedOutputs.Single(item => item.Id == renderableB.Id).IsShownInViewer,
                $"commits={displayCommitCount};selected={displayedArtifactId};Bshown={owner.DisplayedOutputs.Single(item => item.Id == renderableB.Id).IsShownInViewer}");
            allowDisplay = true;

            var requestsBeforeUnavailable = displayRequestCount;
            owner.ShowDisplayedOutputInViewerCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == stale.Id));
            owner.ShowDisplayedOutputInViewerCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == missing.Id));
            owner.ShowDisplayedOutputInViewerCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == blankPath.Id));
            Check(
                "unavailable targets do not raise a Viewer request",
                displayRequestCount == requestsBeforeUnavailable,
                $"requests={displayRequestCount};before={requestsBeforeUnavailable}");

            var uppercaseA = new ToolWorkbenchDisplayedOutputItem(
                renderableA with { Id = renderableA.Id.ToUpperInvariant() });
            uppercaseA.UpdatePresentation(
                true,
                false,
                string.Empty,
                true,
                ThreeDLocalization.Shared.DisplayableC3DData,
                string.Empty);
            owner.RequestDisplayedOutputInViewer(uppercaseA);
            Check(
                "target lookup and displayed identity reconciliation are case-insensitive",
                requestedPath == "a.c3d"
                && owner.DisplayedOutputs.Single(item => item.Id == renderableA.Id).IsShownInViewer,
                $"requested={uppercaseA.Id};resolvedPath={requestedPath}");

            owner.PinDisplayedOutputToCompareCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == renderableA.Id));
            owner.PinDisplayedOutputToCompareCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == renderableB.Id));
            owner.PinDisplayedOutputToCompareCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == renderableC.Id));
            Check(
                "compare pin command delegates slot policy and exposes stable A/B/C presentation",
                compareSlotA == renderableA.Id
                && compareSlotB == renderableB.Id
                && compareSlotC == renderableC.Id
                && owner.DisplayedOutputs.Single(item => item.Id == renderableA.Id).ComparePins == "A"
                && owner.DisplayedOutputs.Single(item => item.Id == renderableB.Id).ComparePins == "B"
                && owner.DisplayedOutputs.Single(item => item.Id == renderableC.Id).ComparePins == "C"
                && owner.DisplayedOutputs.All(item => !item.CanPinToCompare
                    || item.Id is not ("output.displayed-a" or "output.displayed-b" or "output.displayed-c")),
                $"A={compareSlotA};B={compareSlotB};C={compareSlotC}");

            owner.FocusDisplayedOutputStepCommand.Execute(
                owner.DisplayedOutputs.Single(item => item.Id == renderableB.Id));
            Check(
                "focus command uses the host selection callback without execution",
                ReferenceEquals(focusedStep, filterStep)
                && focusCount == 1,
                $"focused={focusedStep?.Id};focusCallbacks={focusCount};execution=not-invoked");

            currentArtifacts = artifactSnapshot
                .Where(item => !string.Equals(item.Id, renderableA.Id, StringComparison.Ordinal))
                .ToArray();
            owner.Rebuild();
            Check(
                "rebuild reconciles a removed displayed item without mutating the supplied snapshot",
                !owner.DisplayedOutputs.Any(item => item.Id == renderableA.Id)
                && owner.DisplayedOutputs.All(item => !item.IsShownInViewer)
                && artifactSnapshot.Select(item => item.Id)
                    .SequenceEqual(new[]
                    {
                        source.Id,
                        renderableA.Id,
                        renderableB.Id,
                        renderableC.Id,
                        evidenceOnly.Id,
                        stale.Id,
                        missing.Id,
                        blankPath.Id
                    })
                && artifactSnapshot[1].Detail == "Renderable A detail",
                $"remaining={owner.DisplayedOutputs.Count};shown={owner.DisplayedOutputs.Count(item => item.IsShownInViewer)};snapshot=unchanged");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        var success = total > 0 && passed == total;
        summary = $"DisplayedOutputsOwner|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return success;
    }
}
