using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolRecipeOrderedRunVerification
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool VerifyOutputHandoff(string manifestPath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Ordered output handoff: actual typed SDK results through Workbench; no Window or Viewer constructed." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok, string detail = "") { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previous = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            foreach (var fixture in manifest.RootElement.EnumerateArray())
            {
                var name = fixture.GetProperty("name").GetString()!;
                try
                {
                    using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, name + "-recent.json"));
                    if (!workbench.TryOpenTeachingRecipe(fixture.GetProperty("recipe").GetString()!, out var message))
                        throw new InvalidDataException(message);
                    var sourceId = workbench.Source.Id;
                    var sourcePath = workbench.Source.Path;
                    var ran = workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult();
                    var result = workbench.CurrentOrderedRunResult;
                    Check(name + " executes its authored graph", ran && result is not null
                        && result.Steps.Count == fixture.GetProperty("expectedSteps").GetInt32(), workbench.OrderedRunCapabilitySummary);
                    var publishedIds = (result?.Steps ?? []).Where(step => step.RuntimeOutput is not null)
                        .Select(step => step.OutputEntityId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    Check(name + " published outputs are ready in the flow", result is not null
                        && !workbench.FlowPortDiagnostics.Any(item => item.Port == "Input"
                            && item.Step.InputEntityIds.Any(publishedIds.Contains)),
                        string.Join(" | ", workbench.FlowPortDiagnostics.Select(item => $"{item.Step.Id}:{item.Kind}:{item.Detail}")));
                    foreach (var executed in result?.Steps ?? [])
                    {
                        var artifact = workbench.ArtifactRegistry.Single(item => item.Id == executed.OutputEntityId);
                        Check(name + ": " + executed.StepId + " retains the exact typed output", executed.RuntimeOutput is not null
                            && artifact.State == "Published" && artifact.ContentSha256 == executed.OutputContentSha256,
                            $"{executed.ToolId};{executed.Result.Status};{artifact.State};{executed.Result.Message}");
                        if (executed.RuntimeOutput is C3DHeightFieldSnapshot height)
                        {
                            var target = workbench.GetRenderableC3DTarget(height.EntityId);
                            Check(name + ": " + executed.StepId + " can display the actual map", target is not null
                                && ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(target.C3DPath).ContentSha256 == height.ContentSha256
                                && workbench.TryGetCurrentPublishedHeightField(height.EntityId)?.ContentSha256 == height.ContentSha256);
                        }
                        if (executed.RuntimeOutput is C3DSignedHeightDeltaArtifact delta)
                            Check(name + " retains signed zero/negative map without C3D conversion",
                                workbench.GetRenderableSignedDeltaArtifact(delta.OutputEntityId)?.ContentSha256 == delta.ContentSha256);
                        if (executed.RuntimeOutput is C3DTransformedHeightField transformed)
                            Check(name + " retains the physical reference frame",
                                workbench.TryGetPublishedRegridHeightFieldOutput(executed.OutputEntityId, out var field)
                                && field?.ContentSha256 == transformed.ContentSha256 && artifact.Unit == transformed.ReferenceUnit
                                && artifact.FrameId == transformed.ReferenceFrameId);
                    }
                    if (name == "affine-chain" && ran)
                    {
                        var line = result!.Steps.First(item => item.ToolId == "three-d-line-fit");
                        var authored = ToolRecipeDocumentStore.Load(fixture.GetProperty("recipe").GetString()!);
                        var affineStep = authored.Steps.Single(step => step.ToolId == "xyz-affine-solve");
                        var changed = authored with { Steps = authored.Steps.Select(step => step.Id != line.StepId ? step : step with
                        {
                            Parameters = step.Parameters!.Select((parameter, index) => index == 0 ? parameter with { Value = parameter.Value + "-changed" } : parameter).ToArray()
                        }).ToArray() };
                        Check("structured correspondence rows retain their producer dependencies", authored.DependsOnEntity(affineStep.Id, line.OutputEntityId)
                            && authored.CreateStepExecutionContext(affineStep.Id) != changed.CreateStepExecutionContext(affineStep.Id));
                        var referenceLabel = authored.Selections!.First(selection => selection.Rows?.Count > 0).Rows![0].ReferenceLandmarkId;
                        var unrelated = authored.Steps.First(step => step.Id == line.StepId) with
                        {
                            Id = "step.unrelated-reference-label",
                            OutputEntityId = referenceLabel,
                            InputEntityIds = [authored.Source.Id]
                        };
                        var withLabelCollision = authored with { Steps = [.. authored.Steps, unrelated] };
                        var changedLabelCollision = withLabelCollision with
                        {
                            Steps = [.. authored.Steps, unrelated with { OutputEnabled = false }]
                        };
                        Check("inline reference landmark labels are not producer dependencies",
                            !withLabelCollision.DependsOnEntity(affineStep.Id, referenceLabel)
                            && withLabelCollision.CreateStepExecutionContext(affineStep.Id) == changedLabelCollision.CreateStepExecutionContext(affineStep.Id));
                        var measurementStep = result.Steps.Single(item => item.ToolId == "thickness");
                        workbench.SelectPipelineStep(measurementStep.StepId);
                        var canMeasure = workbench.RunSelectedStepCommand.CanExecute(null);
                        workbench.RunSelectedStepCommand.Execute(null);
                        var measurementSettled = SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(30));
                        Check("individual measurement consumes the full-run map and rebound ROI", canMeasure && measurementSettled
                            && !workbench.HasSelectedStepFlowProblem
                            && workbench.PipelineSteps.Single(step => step.Id == measurementStep.StepId).State == "Published");
                        workbench.SelectPipelineStep(line.StepId);
                        var canRun = workbench.RunSelectedStepCommand.CanExecute(null);
                        workbench.RunSelectedStepCommand.Execute(null);
                        var settled = SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(30));
                        Check("line fit consumes the full-run edge without regenerating it", canRun && settled
                            && workbench.TryGetPublishedLineFitOutput(line.OutputEntityId, out var fitted) && fitted is not null);
                        Check("selected producer rerun withdraws its downstream results", workbench.CurrentOrderedRunResult is null
                            && workbench.ArtifactRegistry.Single(item => item.Id == result.Steps.Single(step => step.ToolId == "xyz-affine-solve").OutputEntityId).State == "Declared");
                    }
                    Check(name + " keeps the raw input identity", sourceId == workbench.Source.Id && sourcePath == workbench.Source.Path);
                }
                catch (Exception exception) { Check(name + " fixture", false, exception.ToString()); }
            }
        }
        catch (Exception exception) { Check("handoff manifest", false, exception.ToString()); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previous); }
        summary = $"Ordered output handoff: {passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return total > 0 && passed == total;
    }

    internal static bool VerifyOrderedRunOwnerLifecycle(
        ToolRecipeDocument document,
        string recipePath,
        out string detail)
    {
        var capabilityCalls = 0;
        using var capabilityEntered = new ManualResetEventSlim();
        using var releaseCapability = new ManualResetEventSlim();
        var gatedOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ =>
            {
                Interlocked.Increment(ref capabilityCalls);
                capabilityEntered.Set();
                releaseCapability.Wait();
                return (false, "blocked for single-flight verification");
            },
            () => recipePath,
            () => string.Empty,
            () => null,
            () => Array.Empty<ToolWorkbenchPipelineStepItem>(),
            _ => string.Empty,
            (_, english) => english,
            (_, _) => { },
            () => { },
            _ => { },
            () => { });

        var firstRun = Task.Run(() => gatedOwner.RunAsync());
        if (!capabilityEntered.Wait(TimeSpan.FromSeconds(5)))
        {
            detail = "single-flight capability gate was not entered within 5 seconds";
            return false;
        }

        // RunAsync checks the gate before its first await, so submit the
        // competing call directly while the first capability callback is
        // still blocked. A second ThreadPool work item would race the release
        // signal and make this focused proof scheduler-dependent.
        var secondRun = gatedOwner.RunAsync();
        releaseCapability.Set();
        var firstResult = firstRun.GetAwaiter().GetResult();
        var secondResult = secondRun.GetAwaiter().GetResult();
        var singleFlight = !firstResult
            && !secondResult
            && capabilityCalls == 1;

        using var sourceEntered = new ManualResetEventSlim();
        using var releaseSource = new ManualResetEventSlim();
        var completionCount = 0;
        var cancelledRecords = 0;
        var cancellationOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () =>
            {
                sourceEntered.Set();
                releaseSource.Wait();
                return string.Empty;
            },
            () => null,
            () => Array.Empty<ToolWorkbenchPipelineStepItem>(),
            _ => "completed",
            (_, english) => english,
            (_, _) => { },
            () => { },
            _ => Interlocked.Increment(ref completionCount),
            () => { },
            recordExecution: (_, status, cancelled) => { if (cancelled && status is null) Interlocked.Increment(ref cancelledRecords); });
        var cancellationRun = Task.Run(() => cancellationOwner.RunAsync());
        if (!sourceEntered.Wait(TimeSpan.FromSeconds(5)))
        {
            detail = "cancellation source gate was not entered within 5 seconds";
            releaseSource.Set();
            cancellationRun.GetAwaiter().GetResult();
            return false;
        }

        var cancelCommandWasEnabled = cancellationOwner.CancelCommand.CanExecute(null);
        cancellationOwner.Cancel();
        releaseSource.Set();
        var cancellationResult = cancellationRun.GetAwaiter().GetResult();
        var cancellationWorked = !cancellationResult
            && !cancellationOwner.IsRunning
            && completionCount == 0
            && cancellationOwner.Summary.Contains("canceled", StringComparison.OrdinalIgnoreCase)
            && cancelCommandWasEnabled
            && cancelledRecords == 1;

        using var delayedSourceEntered = new ManualResetEventSlim();
        using var releaseDelayedSource = new ManualResetEventSlim();
        var delayedSourcePath = document.Source.Path;
        var delayedCompletionCount = 0;
        var delayedOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () => delayedSourcePath,
            () =>
            {
                delayedSourceEntered.Set();
                releaseDelayedSource.Wait();
                return null;
            },
            () => Array.Empty<ToolWorkbenchPipelineStepItem>(),
            _ => "completed",
            (_, english) => english,
            (_, _) => { },
            () => { },
            _ => Interlocked.Increment(ref delayedCompletionCount),
            () => { });
        var delayedRun = Task.Run(() => delayedOwner.RunAsync());
        if (!delayedSourceEntered.Wait(TimeSpan.FromSeconds(5)))
        {
            detail = "delayed Run source gate was not entered within 5 seconds";
            releaseDelayedSource.Set();
            delayedRun.GetAwaiter().GetResult();
            return false;
        }

        delayedSourcePath += ".changed";
        delayedOwner.Invalidate("The input changed while the saved recipe was running.");
        releaseDelayedSource.Set();
        var delayedRunResult = delayedRun.GetAwaiter().GetResult();
        var staleCompletionRejected = !delayedRunResult
            && delayedCompletionCount == 0
            && delayedOwner.Result is null
            && delayedOwner.RecordPath is null
            && !delayedOwner.IsRunning
            && delayedOwner.Summary.Contains("canceled", StringComparison.OrdinalIgnoreCase);
        delayedOwner.Dispose();

        using var commandFailureObserved = new ManualResetEventSlim();
        var failedRecords = 0;
        var commandFailureOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () => document.Source.Path,
            () => null,
            () => throw new InvalidOperationException("pipeline callback failure"),
            _ => "completed",
            (_, english) => english,
            (category, _) =>
            {
                if (string.Equals(category, "Error", StringComparison.Ordinal))
                {
                    commandFailureObserved.Set();
                }
            },
            () => { },
            _ => { },
            () => { },
            recordExecution: (_, status, cancelled) => { if (!cancelled && status == ResultStatus.Error) Interlocked.Increment(ref failedRecords); });
        var commandWasEnabled = commandFailureOwner.RunCommand.CanExecute(null);
        commandFailureOwner.RunCommand.Execute(null);
        var commandFailureWasObserved = commandFailureObserved.Wait(TimeSpan.FromSeconds(5))
            && !commandFailureOwner.IsRunning
            && commandFailureOwner.Summary.Contains("failed", StringComparison.OrdinalIgnoreCase)
            && failedRecords == 1;
        commandFailureOwner.Dispose();

        detail = $"singleFlight={singleFlight};capabilityCalls={capabilityCalls};"
            + $"cancellation={cancellationWorked};commandEnabled={commandWasEnabled};"
            + $"staleCompletionRejected={staleCompletionRejected};"
            + $"commandFailureObserved={commandFailureWasObserved};completionCount={completionCount};"
            + $"summary={cancellationOwner.Summary}";
        return singleFlight && cancellationWorked && staleCompletionRejected
            && commandWasEnabled && commandFailureWasObserved;
    }

    internal static string ChangeSha256(string value) =>
        (value.StartsWith('0') ? "1" : "0") + value[1..];

    internal static ToolRecipeDocument CreateThicknessDocument(
        string sourcePath,
        double minimum,
        double maximum)
    {
        var identity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(
            sourcePath);
        var source = new ToolRecipeSource(
            "source.c3d.height-map",
            "Thickness verification",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            Path.GetFullPath(sourcePath),
            new FileInfo(sourcePath).Length,
            identity.ContentSha256,
            identity.GridWidth,
            identity.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.reference",
            "Reference surface ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            identity,
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            null,
            null);
        var measurement = new ToolRecipeSelection(
            "selection.measurement",
            "Measurement ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            identity,
            new ToolRecipeGridRectangle(2, 0, 2, 4),
            null,
            null);
        var step = new ToolRecipeStep(
            "step.thickness.01",
            "thickness",
            "Thickness",
            3,
            [source.Id, reference.Id, measurement.Id],
            "derived.measurementresult.01",
            [
                new ToolRecipeParameter("MinimumThickness", minimum.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)),
                new ToolRecipeParameter("MaximumThickness", maximum.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Thickness ordered Run verification",
            source,
            [],
            [step],
            [reference, measurement]);
    }

    internal static bool CompareSemanticRunRecord(
        InspectionRunRecord? uiRecord,
        ToolRecipeDocument document,
        ToolRecipeOrderedGraphExecutionResult headless,
        string recipePath,
        out string detail)
    {
        var projection = ToolRecipeOrderedGraphRunRecordProjection.Create(
            document,
            headless);
        var uiSteps = uiRecord?.Steps ?? [];
        var stepParity = uiSteps.Count == projection.Count
            && uiSteps.Zip(projection).All(pair =>
                pair.First.Id == pair.Second.Id
                && pair.First.ToolId == pair.Second.ToolId
                && pair.First.InputEntityIds.SequenceEqual(pair.Second.InputEntityIds)
                && pair.First.OutputEntityId == pair.Second.OutputEntityId
                && pair.First.Status == pair.Second.Status
                && pair.First.OutputContentSha256 == pair.Second.OutputContentSha256
                && pair.First.Metrics.SequenceEqual(pair.Second.Metrics));
        var sourceParity = uiRecord is not null
            && uiRecord.Status == headless.Status
            && uiRecord.Source.EntityId == document.Source.Id
            && uiRecord.Source.Sha256 == document.Source.ContentSha256
            && uiRecord.Source.ByteLength == document.Source.ByteLength
            && uiRecord.Source.Unit == document.Source.Unit
            && headless.SourceContentSha256 == document.Source.ContentSha256;
        var recipeBytes = File.ReadAllBytes(recipePath);
        var recipeHash = Convert.ToHexString(SHA256.HashData(recipeBytes));
        var headlessCropOutputSha256 = headless.Steps.FirstOrDefault()?.OutputContentSha256;
        var recipeParity = uiRecord?.Recipe.Path == Path.GetFullPath(recipePath)
            && string.Equals(uiRecord.Recipe.Sha256, recipeHash, StringComparison.OrdinalIgnoreCase)
            && document.Steps.Any(step => step.ToolId == "roi-crop")
            && document.Selections?.Where(selection => selection.SourceBinding.OwnerEntityId == "derived.thickness.crop")
                .Count() == 2
            && headless.ReboundDocument.Selections?.Where(selection => selection.SourceBinding.OwnerEntityId == "derived.thickness.crop")
                .All(selection => headlessCropOutputSha256 is not null
                    && selection.SourceBinding.ContentSha256 == headlessCropOutputSha256)
                == true;
        detail = $"status={uiRecord?.Status}/{headless.Status};steps={uiSteps.Count}/{projection.Count};"
            + $"source={sourceParity};recipe={recipeParity};stepFields={stepParity};"
            + $"headlessMessage={headless.Message};outputs={string.Join(',', uiSteps.Select(step => step.OutputContentSha256 ?? "(none)"))}";
        return sourceParity && recipeParity && stepParity;
    }

    internal static string ComputeHashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static InspectionRunRecord? ReadRecord(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null
            : JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(path),
                JsonOptions);

    internal static string FormatRecordedMetric(InspectionRunMetric metric) =>
        string.IsNullOrWhiteSpace(metric.Unit)
            ? $"{metric.Name}={metric.Value:G6}"
            : $"{metric.Name}={metric.Value:G6} {metric.Unit}";

    internal static void VerifyAtomicRecordReplacement(string root, InspectionRunRecord? record, Action<string, bool, string> Check)
    {
        var atomicReplacementPassed = false;
        var atomicReplacementDetail = "record unavailable";
        if (record is not null)
        {
            var atomicRecordPath = Path.Combine(root, "atomic-replacement-run-record.json");
            InspectionRunRecordJson.Write(atomicRecordPath, record);
            var initialAtomicRecordBytes = File.ReadAllBytes(atomicRecordPath);
            var replacementRecord = record with
            {
                RunId = "atomic-replacement",
                Message = "atomic replacement"
            };
            InspectionRunRecordJson.Write(atomicRecordPath, replacementRecord);
            var replacementAtomicRecordBytes = File.ReadAllBytes(atomicRecordPath);
            var reloadedReplacementRecord = InspectionRunRecordJson.Read(atomicRecordPath);
            var stagedAtomicRecordFiles = Directory.GetFiles(
                root,
                $"{Path.GetFileName(atomicRecordPath)}.tmp.*");
            atomicReplacementPassed =
                !initialAtomicRecordBytes.SequenceEqual(replacementAtomicRecordBytes)
                && reloadedReplacementRecord.RunId == "atomic-replacement"
                && reloadedReplacementRecord.Message == "atomic replacement"
                && stagedAtomicRecordFiles.Length == 0;
            atomicReplacementDetail =
                $"runId={reloadedReplacementRecord.RunId};staged={stagedAtomicRecordFiles.Length}";
        }
        Check(
            "Run Record JSON replacement preserves the contract and cleans staged files",
            atomicReplacementPassed,
            atomicReplacementDetail);
    }

    internal static void VerifyRecipeIdentityRace(ToolRecipeDocument document, string path, byte[] originalBytes, Action<string, bool, string> check)
    {
        var inject = false;
        var completions = 0;
        using var owner = new ToolWorkbenchOrderedRunExecutionOwner(() => document, _ =>
        {
            if (inject) File.AppendAllText(path, " ");
            return (true, "ready");
        }, () => path, () => document.Source.Path, () => null, () => [], _ => "complete", (_, english) => english,
            (_, _) => { }, () => { }, _ => completions++, () => { }, getSavedRecipeSha256: () => ComputeHashBytes(originalBytes));
        File.WriteAllBytes(path, originalBytes);
        owner.RunAsync().GetAwaiter().GetResult();
        var ownerPrevious = owner.Result;
        inject = true;
        check("recipe edit between admission and identity capture retains the old result", !owner.RunAsync().GetAwaiter().GetResult()
            && completions == 1 && ReferenceEquals(ownerPrevious, owner.Result) && owner.Summary.Contains("changed", StringComparison.OrdinalIgnoreCase), owner.Summary);
        inject = false;
        File.WriteAllBytes(path, originalBytes);
        check("identity race recovery executes only on a new explicit Run", owner.RunAsync().GetAwaiter().GetResult() && completions == 2, $"completions={completions}");
    }
}
