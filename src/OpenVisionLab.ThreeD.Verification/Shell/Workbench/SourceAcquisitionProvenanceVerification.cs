using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class SourceAcquisitionProvenanceVerification
{
    private static readonly HashSet<string> ExecutionCategories = new(
        ["Preview", "Publish", "Run", "Validate", "Validation"],
        StringComparer.OrdinalIgnoreCase);

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D acquisition/source provenance verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            nameof(SourceAcquisitionProvenanceVerification),
            Guid.NewGuid().ToString("N"));

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
            Directory.CreateDirectory(root);
            var firstSourcePath = Path.Combine(root, "source-a.c3d");
            var secondSourcePath = Path.Combine(root, "source-b.c3d");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.provenance.a",
                3,
                2,
                [1.0, 2.0, 3.0, 4.0, 5.0, 6.0]).SaveC3D(firstSourcePath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.provenance.b",
                2,
                2,
                [7.0, 8.0, 9.0, 10.0]).SaveC3D(secondSourcePath);

            var recipePath = Path.Combine(root, "available-provenance.ovl3d.json");
            var unavailableRecipePath = Path.Combine(root, "unavailable-provenance.ovl3d.json");
            var legacyRecipePath = Path.Combine(root, "legacy-no-provenance.ovl3d.json");
            var workbench = new ToolWorkbenchViewModel();
            workbench.SetC3DSource(firstSourcePath, markDirty: false);
            var editor = workbench.SourceQuality;

            Check(
                "new-source-has-explicit-unavailable-default",
                workbench.SourceAcquisitionProvenance?.State
                    == ToolRecipeAcquisitionProvenanceState.Unavailable
                && !string.IsNullOrWhiteSpace(workbench.SourceAcquisitionProvenance.Evidence)
                && !string.IsNullOrWhiteSpace(workbench.SourceAcquisitionProvenance.LimitationNotes),
                $"state={workbench.SourceAcquisitionProvenance?.State};persisted={editor.IsAcquisitionProvenancePersisted}");

            var availableOption = editor.AcquisitionStateOptions.Single(option =>
                option.State == ToolRecipeAcquisitionProvenanceState.Available);
            var logsBeforeDraft = workbench.RunLog.Count;
            editor.SelectedAcquisitionStateOption = availableOption;
            editor.AcquisitionEvidenceDraft =
                "Structured-light scan exported by station S-04; batch record ACQ-20260804-17.";
            editor.AcquisitionLimitationNotesDraft =
                "Sensor pose and calibration file were not included; viewpoint is not inferred.";
            editor.IsAcquisitionReflectiveFlagDraft = true;
            editor.IsAcquisitionLowCoverageFlagDraft = true;

            Check(
                "draft-is-visible-but-does-not-mutate-recipe",
                editor.HasPendingAcquisitionProvenanceChanges
                && editor.CanApplyAcquisitionProvenance
                && !workbench.IsDirty
                && workbench.SourceAcquisitionProvenance?.State
                    == ToolRecipeAcquisitionProvenanceState.Unavailable,
                $"pending={editor.HasPendingAcquisitionProvenanceChanges};dirty={workbench.IsDirty};state={workbench.SourceAcquisitionProvenance?.State}");
            Check(
                "draft-does-not-execute",
                workbench.RunLog.Count == logsBeforeDraft
                && !workbench.IsSelectedStepPreviewRunning
                && !workbench.IsValidationSetRunning,
                $"logs={logsBeforeDraft}->{workbench.RunLog.Count};preview={workbench.IsSelectedStepPreviewRunning};validation={workbench.IsValidationSetRunning}");

            editor.ResetAcquisitionProvenanceCommand.Execute(null);
            Check(
                "reset-restores-applied-value-without-mutation",
                !editor.HasPendingAcquisitionProvenanceChanges
                && editor.SelectedAcquisitionStateOption?.State
                    == ToolRecipeAcquisitionProvenanceState.Unavailable
                && !editor.IsAcquisitionReflectiveFlagDraft
                && !editor.IsAcquisitionLowCoverageFlagDraft
                && !workbench.IsDirty
                && workbench.RunLog.Count == logsBeforeDraft,
                $"pending={editor.HasPendingAcquisitionProvenanceChanges};dirty={workbench.IsDirty};logs={workbench.RunLog.Count}");

            editor.SelectedAcquisitionStateOption = editor.AcquisitionStateOptions.Single(option =>
                option.State == ToolRecipeAcquisitionProvenanceState.Available);
            editor.AcquisitionEvidenceDraft =
                "Structured-light scan exported by station S-04; batch record ACQ-20260804-17.";
            editor.AcquisitionLimitationNotesDraft =
                "Sensor pose and calibration file were not included; viewpoint is not inferred.";
            editor.IsAcquisitionReflectiveFlagDraft = true;
            editor.IsAcquisitionLowCoverageFlagDraft = true;
            editor.SelectedAcquisitionDirectionStateOption =
                editor.AcquisitionDirectionStateOptions.Single(option =>
                    option.State == ToolRecipeAcquisitionDirectionState.Available);
            editor.AcquisitionDirectionXDraft = "0";
            editor.AcquisitionDirectionYDraft = "0";
            editor.AcquisitionDirectionZDraft = "-2";
            var logsBeforeApply = workbench.RunLog.Count;
            editor.ApplyAcquisitionProvenanceCommand.Execute(null);

            Check(
                "apply-records-exact-available-contract",
                workbench.IsDirty
                && workbench.SourceAcquisitionProvenance is
                {
                    State: ToolRecipeAcquisitionProvenanceState.Available,
                    Evidence: "Structured-light scan exported by station S-04; batch record ACQ-20260804-17.",
                    LimitationNotes: "Sensor pose and calibration file were not included; viewpoint is not inferred.",
                    AcquisitionDirection:
                    {
                        State: ToolRecipeAcquisitionDirectionState.Available,
                        Convention: ToolRecipeAcquisitionDirectionConvention.SensorToScene,
                        Vector: ToolRecipeXyz { X: 0.0, Y: 0.0, Z: -1.0 }
                    }
                }
                && workbench.SourceAcquisitionProvenance.LimitationFlags is
                [
                    { Kind: ToolRecipeAcquisitionLimitationKind.Reflective, Origin: ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored },
                    { Kind: ToolRecipeAcquisitionLimitationKind.LowCoverage, Origin: ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored }
                ]
                && editor.IsAcquisitionProvenancePersisted
                && editor.IsAcquisitionDirectionPersisted
                && !editor.HasPendingAcquisitionProvenanceChanges,
                $"dirty={workbench.IsDirty};state={workbench.SourceAcquisitionProvenance?.State};pending={editor.HasPendingAcquisitionProvenanceChanges}");
            Check(
                "apply-does-not-execute",
                workbench.RunLog.Count == logsBeforeApply
                && !workbench.IsSelectedStepPreviewRunning
                && !workbench.IsValidationSetRunning,
                $"logs={logsBeforeApply}->{workbench.RunLog.Count};preview={workbench.IsSelectedStepPreviewRunning};validation={workbench.IsValidationSetRunning}");

            var savedAvailable = workbench.TrySaveTeachingRecipe(recipePath, out var availableSaveMessage);
            var loadedAvailable = savedAvailable
                ? ToolRecipeDocumentStore.Load(recipePath)
                : null;
            Check(
                "available-contract-saves-in-source-descriptor",
                savedAvailable
                && loadedAvailable is not null
                && SameProvenance(
                    loadedAvailable.Source.AcquisitionProvenance,
                    workbench.SourceAcquisitionProvenance)
                && loadedAvailable.Source.ByteLength == new FileInfo(firstSourcePath).Length
                && loadedAvailable.Source.ContentSha256 is { Length: 64 }
                && File.ReadAllText(recipePath).Contains(
                    "\"state\": \"Available\"",
                    StringComparison.Ordinal)
                && File.ReadAllText(recipePath).Contains(
                    "\"convention\": \"SensorToScene\"",
                    StringComparison.Ordinal)
                && File.ReadAllText(recipePath).Contains(
                    "\"limitationFlags\"",
                    StringComparison.Ordinal),
                $"saved={savedAvailable};message={availableSaveMessage};state={loadedAvailable?.Source.AcquisitionProvenance?.State};sourceSha256={loadedAvailable?.Source.ContentSha256}");

            var reopened = new ToolWorkbenchViewModel();
            var reopenedOk = reopened.TryOpenTeachingRecipe(recipePath, out var reopenMessage);
            Check(
                "available-contract-reopens-exactly",
                reopenedOk
                && SameProvenance(
                    reopened.SourceAcquisitionProvenance,
                    loadedAvailable?.Source.AcquisitionProvenance)
                && SameProvenance(
                    reopened.SourceQuality.AppliedAcquisitionProvenance,
                    loadedAvailable?.Source.AcquisitionProvenance)
                && reopened.SourceQuality.AcquisitionEvidenceDraft
                    == loadedAvailable?.Source.AcquisitionProvenance?.Evidence
                && reopened.SourceQuality.AcquisitionLimitationNotesDraft
                    == loadedAvailable?.Source.AcquisitionProvenance?.LimitationNotes
                && reopened.SourceQuality.IsAcquisitionReflectiveFlagDraft
                && reopened.SourceQuality.IsAcquisitionLowCoverageFlagDraft
                && reopened.SourceQuality.IsAcquisitionProvenancePersisted
                && reopened.SourceQuality.IsAcquisitionDirectionPersisted
                && reopened.SourceQuality.AcquisitionDirectionXDraft == "0"
                && reopened.SourceQuality.AcquisitionDirectionYDraft == "0"
                && reopened.SourceQuality.AcquisitionDirectionZDraft == "-1"
                && !reopened.IsDirty,
                $"opened={reopenedOk};message={reopenMessage};state={reopened.SourceAcquisitionProvenance?.State};dirty={reopened.IsDirty}");
            Check(
                "save-and-reopen-do-not-run-inspection",
                !workbench.RunLog.Any(item => ExecutionCategories.Contains(item.Category))
                && !reopened.RunLog.Any(item => ExecutionCategories.Contains(item.Category))
                && workbench.PipelineSteps.Count == 0
                && reopened.PipelineSteps.Count == 0
                && !reopened.IsSelectedStepPreviewRunning
                && !reopened.IsValidationSetRunning,
                $"saveCategories={string.Join(',', workbench.RunLog.Select(item => item.Category).Distinct())};reopenCategories={string.Join(',', reopened.RunLog.Select(item => item.Category).Distinct())}");

            reopened.SetC3DSource(secondSourcePath, markDirty: false);
            Check(
                "different-source-resets-source-scoped-provenance",
                reopened.SourceAcquisitionProvenance?.State
                    == ToolRecipeAcquisitionProvenanceState.Unavailable
                && reopened.SourceAcquisitionProvenance.AcquisitionDirection?.State
                    == ToolRecipeAcquisitionDirectionState.Unavailable
                && !string.IsNullOrWhiteSpace(reopened.SourceAcquisitionProvenance.Evidence)
                && !string.IsNullOrWhiteSpace(reopened.SourceAcquisitionProvenance.LimitationNotes)
                && reopened.SourceQuality.IsAcquisitionReflectiveFlagDraft == false
                && reopened.SourceQuality.IsAcquisitionLowCoverageFlagDraft == false
                && !reopened.SourceQuality.AcquisitionEvidenceDraft.Contains(
                    "ACQ-20260804-17",
                    StringComparison.Ordinal),
                $"state={reopened.SourceAcquisitionProvenance?.State};evidence={reopened.SourceQuality.AcquisitionEvidenceDraft}");

            var unavailableEditor = reopened.SourceQuality;
            unavailableEditor.AcquisitionEvidenceDraft =
                "Acquisition provenance was explicitly unavailable in the delivered source package.";
            unavailableEditor.AcquisitionLimitationNotesDraft =
                "Viewpoint, direction, sensor pose, calibration, and capture conditions are unavailable.";
            unavailableEditor.ApplyAcquisitionProvenanceCommand.Execute(null);
            var savedUnavailable = reopened.TrySaveTeachingRecipe(
                unavailableRecipePath,
                out var unavailableSaveMessage);
            var reopenedUnavailable = new ToolWorkbenchViewModel();
            var reopenedUnavailableOk = reopenedUnavailable.TryOpenTeachingRecipe(
                unavailableRecipePath,
                out var unavailableReopenMessage);
            Check(
                "explicit-unavailable-contract-saves-and-reopens",
                savedUnavailable
                && reopenedUnavailableOk
                && reopenedUnavailable.SourceAcquisitionProvenance is
                {
                    State: ToolRecipeAcquisitionProvenanceState.Unavailable,
                    Evidence: "Acquisition provenance was explicitly unavailable in the delivered source package.",
                    LimitationNotes: "Viewpoint, direction, sensor pose, calibration, and capture conditions are unavailable."
                },
                $"saved={savedUnavailable};saveMessage={unavailableSaveMessage};opened={reopenedUnavailableOk};openMessage={unavailableReopenMessage}");

            var legacyDirectionRecipePath = Path.Combine(
                root,
                "legacy-no-direction.ovl3d.json");
            var legacyDirectionDocument = loadedAvailable! with
            {
                Source = loadedAvailable.Source with
                {
                    AcquisitionProvenance = loadedAvailable.Source.AcquisitionProvenance! with
                    {
                        AcquisitionDirection = null
                    }
                }
            };
            ToolRecipeDocumentStore.Save(legacyDirectionRecipePath, legacyDirectionDocument);
            var legacyDirectionWorkbench = new ToolWorkbenchViewModel();
            var legacyDirectionOpened = legacyDirectionWorkbench.TryOpenTeachingRecipe(
                legacyDirectionRecipePath,
                out var legacyDirectionMessage);
            Check(
                "legacy-provenance-without-direction-falls-back-unavailable",
                legacyDirectionOpened
                && legacyDirectionWorkbench.SourceAcquisitionProvenance?.State
                    == ToolRecipeAcquisitionProvenanceState.Available
                && legacyDirectionWorkbench.SourceAcquisitionProvenance.AcquisitionDirection is null
                && legacyDirectionWorkbench.SourceQuality.SelectedAcquisitionDirectionStateOption?.State
                    == ToolRecipeAcquisitionDirectionState.Unavailable
                && !legacyDirectionWorkbench.SourceQuality.IsAcquisitionDirectionPersisted
                && !legacyDirectionWorkbench.SourceQuality.HasPendingAcquisitionDirectionChanges
                && !legacyDirectionWorkbench.IsDirty,
                $"opened={legacyDirectionOpened};message={legacyDirectionMessage};direction={legacyDirectionWorkbench.SourceAcquisitionProvenance?.AcquisitionDirection};persisted={legacyDirectionWorkbench.SourceQuality.IsAcquisitionDirectionPersisted}");

            var legacyDocument = ToolRecipeDocumentStore.Load(recipePath) with
            {
                Source = ToolRecipeDocumentStore.Load(recipePath).Source with
                {
                    AcquisitionProvenance = null
                }
            };
            ToolRecipeDocumentStore.Save(legacyRecipePath, legacyDocument);
            var legacyWorkbench = new ToolWorkbenchViewModel();
            var legacyOpened = legacyWorkbench.TryOpenTeachingRecipe(
                legacyRecipePath,
                out var legacyOpenMessage);
            Check(
                "legacy-recipe-without-field-falls-back-unavailable",
                legacyOpened
                && ToolRecipeDocumentStore.Load(legacyRecipePath).Source.AcquisitionProvenance is null
                && legacyWorkbench.SourceAcquisitionProvenance is null
                && legacyWorkbench.SourceQuality.AppliedAcquisitionProvenance.State
                    == ToolRecipeAcquisitionProvenanceState.Unavailable
                && !legacyWorkbench.SourceQuality.IsAcquisitionProvenancePersisted
                && !legacyWorkbench.SourceQuality.HasPendingAcquisitionProvenanceChanges
                && !legacyWorkbench.IsDirty,
                $"opened={legacyOpened};message={legacyOpenMessage};sourceValue={legacyWorkbench.SourceAcquisitionProvenance};editorState={legacyWorkbench.SourceQuality.AppliedAcquisitionProvenance.State}");

            var legacyLogsBeforeInvalidDraft = legacyWorkbench.RunLog.Count;
            legacyWorkbench.SourceQuality.AcquisitionEvidenceDraft = " ";
            Check(
                "blank-draft-shows-validation-and-cannot-apply",
                legacyWorkbench.SourceQuality.HasAcquisitionValidationError
                && !legacyWorkbench.SourceQuality.CanApplyAcquisitionProvenance
                && !legacyWorkbench.SourceQuality.ApplyAcquisitionProvenanceCommand.CanExecute(null)
                && legacyWorkbench.SourceAcquisitionProvenance is null
                && !legacyWorkbench.IsDirty
                && legacyWorkbench.RunLog.Count == legacyLogsBeforeInvalidDraft,
                $"validation={legacyWorkbench.SourceQuality.HasAcquisitionValidationError};canApply={legacyWorkbench.SourceQuality.CanApplyAcquisitionProvenance};dirty={legacyWorkbench.IsDirty};logs={legacyLogsBeforeInvalidDraft}->{legacyWorkbench.RunLog.Count}");

            var invalidEvidence = legacyDocument with
            {
                Source = legacyDocument.Source with
                {
                    AcquisitionProvenance = new(
                        ToolRecipeAcquisitionProvenanceState.Available,
                        " ",
                        "Known limitation")
                }
            };
            var invalidLimitations = legacyDocument with
            {
                Source = legacyDocument.Source with
                {
                    AcquisitionProvenance = new(
                        ToolRecipeAcquisitionProvenanceState.Unavailable,
                        "Evidence supplied",
                        " ")
                }
            };
            var invalidState = legacyDocument with
            {
                Source = legacyDocument.Source with
                {
                    AcquisitionProvenance = new(
                        (ToolRecipeAcquisitionProvenanceState)int.MaxValue,
                        "Evidence supplied",
                        "Known limitation")
                }
            };
            var invalidLimitationFlags = legacyDocument with
            {
                Source = legacyDocument.Source with
                {
                    AcquisitionProvenance = new(
                        ToolRecipeAcquisitionProvenanceState.Unavailable,
                        "Evidence supplied",
                        "Known limitation",
                        null,
                        [
                            new(
                                ToolRecipeAcquisitionLimitationKind.Reflective,
                                ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored),
                            new(
                                ToolRecipeAcquisitionLimitationKind.Reflective,
                                ToolRecipeAcquisitionLimitationOrigin.Imported)
                        ])
                }
            };
            Check(
                "present-contract-requires-defined-state-evidence-limitations-and-unique-flags",
                !ToolRecipeValidator.ValidateForStorage(invalidEvidence).IsValid
                && !ToolRecipeValidator.ValidateForStorage(invalidLimitations).IsValid
                && !ToolRecipeValidator.ValidateForStorage(invalidState).IsValid
                && !ToolRecipeValidator.ValidateForStorage(invalidLimitationFlags).IsValid,
                $"stateErrors={ToolRecipeValidator.ValidateForStorage(invalidState).Errors.Count};evidenceErrors={ToolRecipeValidator.ValidateForStorage(invalidEvidence).Errors.Count};limitationErrors={ToolRecipeValidator.ValidateForStorage(invalidLimitations).Errors.Count};flagErrors={ToolRecipeValidator.ValidateForStorage(invalidLimitationFlags).Errors.Count}");

            var invalidDirectionFrame = loadedAvailable with
            {
                Source = loadedAvailable.Source with
                {
                    AcquisitionProvenance = loadedAvailable.Source.AcquisitionProvenance! with
                    {
                        AcquisitionDirection = loadedAvailable.Source.AcquisitionProvenance.AcquisitionDirection! with
                        {
                            FrameId = "frame.other"
                        }
                    }
                }
            };
            var invalidDirectionLength = loadedAvailable with
            {
                Source = loadedAvailable.Source with
                {
                    AcquisitionProvenance = loadedAvailable.Source.AcquisitionProvenance! with
                    {
                        AcquisitionDirection = loadedAvailable.Source.AcquisitionProvenance.AcquisitionDirection! with
                        {
                            Vector = new ToolRecipeXyz(0.0, 0.0, -2.0)
                        }
                    }
                }
            };
            var invalidUnavailableVector = loadedAvailable with
            {
                Source = loadedAvailable.Source with
                {
                    AcquisitionProvenance = loadedAvailable.Source.AcquisitionProvenance! with
                    {
                        AcquisitionDirection = new ToolRecipeAcquisitionDirection(
                            ToolRecipeAcquisitionDirectionState.Unavailable,
                            ToolRecipeAcquisitionDirectionConvention.SensorToScene,
                            loadedAvailable.Source.FrameId,
                            new ToolRecipeXyz(0.0, 0.0, -1.0))
                    }
                }
            };
            Check(
                "direction-contract-rejects-frame-length-and-unavailable-vector",
                !ToolRecipeValidator.ValidateForStorage(invalidDirectionFrame).IsValid
                && !ToolRecipeValidator.ValidateForStorage(invalidDirectionLength).IsValid
                && !ToolRecipeValidator.ValidateForStorage(invalidUnavailableVector).IsValid,
                $"frameErrors={ToolRecipeValidator.ValidateForStorage(invalidDirectionFrame).Errors.Count};lengthErrors={ToolRecipeValidator.ValidateForStorage(invalidDirectionLength).Errors.Count};unavailableErrors={ToolRecipeValidator.ValidateForStorage(invalidUnavailableVector).Errors.Count}");

            legacyDirectionWorkbench.SourceQuality.SelectedAcquisitionDirectionStateOption =
                legacyDirectionWorkbench.SourceQuality.AcquisitionDirectionStateOptions.Single(option =>
                    option.State == ToolRecipeAcquisitionDirectionState.Available);
            legacyDirectionWorkbench.SourceQuality.AcquisitionDirectionXDraft = "0";
            legacyDirectionWorkbench.SourceQuality.AcquisitionDirectionYDraft = "0";
            legacyDirectionWorkbench.SourceQuality.AcquisitionDirectionZDraft = "0";
            Check(
                "zero-direction-draft-cannot-apply",
                legacyDirectionWorkbench.SourceQuality.HasAcquisitionValidationError
                && !legacyDirectionWorkbench.SourceQuality.CanApplyAcquisitionProvenance
                && !legacyDirectionWorkbench.IsDirty,
                $"validation={legacyDirectionWorkbench.SourceQuality.HasAcquisitionValidationError};canApply={legacyDirectionWorkbench.SourceQuality.CanApplyAcquisitionProvenance};dirty={legacyDirectionWorkbench.IsDirty}");
        }
        catch (Exception exception)
        {
            Check(
                "unexpected-exception",
                false,
                $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        var passedAll = passed == total;
        lines.Add($"Result={(passedAll ? "PASS" : "FAIL")}|{passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        return passedAll;
    }

    private static bool SameProvenance(
        ToolRecipeAcquisitionProvenance? left,
        ToolRecipeAcquisitionProvenance? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.State == right.State
            && left.Evidence == right.Evidence
            && left.LimitationNotes == right.LimitationNotes
            && left.AcquisitionDirection == right.AcquisitionDirection
            && (left.LimitationFlags ?? [])
                .Select(flag => (flag.Kind, flag.Origin))
                .SequenceEqual(
                    (right.LimitationFlags ?? [])
                        .Select(flag => (flag.Kind, flag.Origin)));
    }
}
