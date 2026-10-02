using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class SignedDeltaRecipeSidecarVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? throw new InvalidOperationException(
                "Signed-delta recipe sidecar report has no directory.");
        Directory.CreateDirectory(reportDirectory);

        var lines = new List<string>
        {
            "OpenVisionLab 3D signed-delta recipe sidecar Save/Reopen verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: existing ToolWorkbenchViewModel recipe Save/Open owners; no Preview/Run during reopen"
        };
        var total = 0;
        var passed = 0;
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
            var root = Path.Combine(reportDirectory, "signed-delta-recipe-sidecar", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var currentPath = Path.Combine(root, "current.C3D");
            var backgroundPath = Path.Combine(root, "background.C3D");
            var recipePath = Path.Combine(root, "signed-delta.ov3d-recipe.json");
            WriteC3D(currentPath, 2, 2, [3f, 2f, 5f, float.NaN]);
            WriteC3D(backgroundPath, 2, 2, [1f, 2f, 3f, 4f]);

            var currentIdentity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(currentPath);
            var backgroundGrid = C3DHeightGrid.Load(backgroundPath, maxRenderedPoints: 0);
            var backgroundByteLength = new FileInfo(backgroundPath).Length;
            var backgroundEntityId = "background.recipe.sidecar.fixture";

            using var authoring = new ToolWorkbenchViewModel();
            authoring.SetC3DSource(currentPath, markDirty: false);
            var signedTool = authoring.Tools.Single(tool =>
                string.Equals(tool.Id, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.Ordinal));
            authoring.AddSelectedToolCommand.Execute(signedTool);
            var signedStep = authoring.SelectedPipelineStep
                ?? throw new InvalidOperationException("Signed-delta step was not selected after Add.");
            ConfigureBackground(authoring, signedStep, backgroundPath, backgroundEntityId, backgroundByteLength, backgroundGrid, currentIdentity);

            var previewed = authoring.PreviewSelectedSignedDeltaBackgroundSubtractionAsync()
                .GetAwaiter()
                .GetResult();
            Check(
                "published signed-delta output is available before Save",
                previewed
                && authoring.HasCurrentSignedDeltaPreview
                && authoring.CurrentSignedDeltaPreviewOutput is { } previewOutput
                && previewOutput.ZeroDeltaSampleCount == 1
                && previewOutput.MissingEitherSampleCount == 1,
                authoring.SignedDeltaOutputSummary);
            var outputBeforeSave = authoring.CurrentSignedDeltaPreviewOutput
                ?? throw new InvalidOperationException("Preview did not produce a signed-delta output.");
            authoring.PublishSelectedStepCommand.Execute(null);
            var published = authoring.IsSignedDeltaPreviewPublished;
            Check("signed-delta sample counters use count units rather than height units",
                authoring.SelectedToolWorkspace.Outputs.Single().Unit == "count"
                && authoring.SelectedToolWorkspace.Outputs.Single().Value.Contains("paired=", StringComparison.Ordinal),
                $"{authoring.SelectedToolWorkspace.Outputs.Single().Value};unit={authoring.SelectedToolWorkspace.Outputs.Single().Unit}");
            var saved = authoring.TrySaveTeachingRecipe(recipePath, out var saveMessage);
            Check(
                "Save writes the recipe and explicit sidecar reference",
                published
                && saved
                && File.Exists(recipePath),
                saveMessage);

            var stored = ToolRecipeDocumentStore.Load(recipePath);
            var storedStep = stored.Steps.Single(step =>
                string.Equals(step.ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.Ordinal));
            var reference = storedStep.SignedDeltaSidecar;
            var sidecarPath = reference is null
                ? string.Empty
                : Path.GetFullPath(Path.Combine(root, reference.Path));
            var savedReference = reference;
            Check(
                "recipe stores a relative, hash-bound signed-delta reference",
                savedReference is not null
                && !Path.IsPathFullyQualified(savedReference.Path)
                && savedReference.Path.EndsWith(C3DSignedHeightDeltaArtifactStore.FileExtension, StringComparison.OrdinalIgnoreCase)
                && savedReference.ContentSha256 == outputBeforeSave.ContentSha256
                && savedReference.OutputContentSha256 == outputBeforeSave.OutputContentSha256
                && File.Exists(sidecarPath),
                $"reference={savedReference?.Path ?? "none"}; sidecar={sidecarPath}");

            using var reopened = new ToolWorkbenchViewModel();
            var reopenActionCount = reopened.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var opened = reopened.TryOpenTeachingRecipe(recipePath, out var openMessage);
            var restored = reopened.CurrentSignedDeltaPreviewOutput;
            Check(
                "Open restores the published sidecar without recomputation",
                opened
                && reopened.IsSignedDeltaPreviewPublished
                && restored is not null
                && restored.ContentSha256 == outputBeforeSave.ContentSha256
                && restored.OutputContentSha256 == outputBeforeSave.OutputContentSha256
                && reopened.SelectedPipelineStep?.SignedDeltaSidecar is not null
                && reopened.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == reopenActionCount
                && reopened.SignedDeltaExecutionSummary.Contains("no Preview/Run", StringComparison.Ordinal),
                openMessage + " | " + reopened.SignedDeltaExecutionSummary);

            var reopenedSave = reopened.TrySaveTeachingRecipe(recipePath, out var reopenedSaveMessage);
            var reopenedDocument = ToolRecipeDocumentStore.Load(recipePath);
            var reopenedReference = reopenedDocument.Steps
                .Single(step => string.Equals(
                    step.ToolId,
                    C3DSignedHeightDeltaRecipePolicy.ToolId,
                    StringComparison.Ordinal))
                .SignedDeltaSidecar;
            var reopenedActionCountAfterSave = reopened.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            Check(
                "Reopen followed by Save retains the explicit reference without execution",
                reopenedSave
                && reopenedReference is not null
                && reopenedReference.ContentSha256 == outputBeforeSave.ContentSha256
                && reopenedActionCountAfterSave == reopenActionCount,
                $"{reopenedSaveMessage} | reference={reopenedReference?.Path ?? "none"}");

            using (var copyAuthoring = new ToolWorkbenchViewModel(Path.Combine(root, "copy-recent.json")))
            {
                if (!copyAuthoring.TryOpenTeachingRecipe(recipePath, out var copyOpenMessage)) throw new InvalidOperationException(copyOpenMessage);
                var variantSource = Path.Combine(root, "variant.C3D");
                WriteC3D(variantSource, 2, 2, [13f, 12f, 15f, float.NaN]);
                copyAuthoring.BeginCompatibleSourceVariantSetup();
                copyAuthoring.FirstRecipeName = "variant";
                copyAuthoring.FirstRecipeSourcePath = variantSource;
                copyAuthoring.IsVariantRoiReuseConfirmed = true;
                if (!copyAuthoring.TryGetFirstRecipeSetup(out var copySetup, out var copySetupMessage)) throw new InvalidOperationException(copySetupMessage);
                var originalSidecar = File.ReadAllBytes(sidecarPath);
                var originalRecipe = File.ReadAllBytes(recipePath);
                var copySaved = copyAuthoring.TryCreateCompatibleSourceVariant(copySetup, ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantSource), out var copyMessage);
                using var copyReopened = new ToolWorkbenchViewModel(Path.Combine(root, "copy-reopen-recent.json"));
                Check("new-input copy drops published signed-delta references without deleting original evidence",
                    copySaved && copyReopened.TryOpenTeachingRecipe(copySetup.RecipePath, out _)
                    && ToolRecipeDocumentStore.Load(copySetup.RecipePath).Steps.All(step => step.SignedDeltaSidecar is null)
                    && !copyAuthoring.HasCurrentSignedDeltaPreview && !copyAuthoring.IsSignedDeltaPreviewPublished
                    && !copyReopened.HasCurrentSignedDeltaPreview && !copyReopened.IsSignedDeltaPreviewPublished
                    && originalSidecar.SequenceEqual(File.ReadAllBytes(sidecarPath)) && originalRecipe.SequenceEqual(File.ReadAllBytes(recipePath)), copyMessage);
            }

            var restoredHash = restored?.ContentSha256;
            var originalSidecarBytes = File.ReadAllBytes(sidecarPath);
            var originalRecipeText = File.ReadAllText(recipePath);
            File.Delete(sidecarPath);
            var missingOpen = reopened.TryOpenTeachingRecipe(recipePath, out var missingMessage);
            Check(
                "missing explicitly referenced sidecar rejects Open without mutating the current result",
                !missingOpen
                && reopened.CurrentSignedDeltaPreviewOutput?.ContentSha256 == restoredHash,
                missingMessage);

            File.WriteAllBytes(sidecarPath, originalSidecarBytes);
            var traversalRecipeText = originalRecipeText.Replace(
                $"\"path\": \"{reference!.Path}\"",
                "\"path\": \"../outside.sdelta.json\"",
                StringComparison.Ordinal);
            File.WriteAllText(recipePath, traversalRecipeText);
            var traversalOpen = reopened.TryOpenTeachingRecipe(recipePath, out var traversalMessage);
            Check(
                "traversal sidecar reference rejects Open without mutating the current result",
                !traversalOpen
                && reopened.CurrentSignedDeltaPreviewOutput?.ContentSha256 == restoredHash,
                traversalMessage);
            File.WriteAllText(recipePath, originalRecipeText);

            var unsupportedText = File.ReadAllText(sidecarPath).Replace(
                "\"schemaVersion\": \"1.0\"",
                "\"schemaVersion\": \"9.9\"",
                StringComparison.Ordinal);
            File.WriteAllText(sidecarPath, unsupportedText);
            var unsupportedOpen = reopened.TryOpenTeachingRecipe(recipePath, out var unsupportedMessage);
            Check(
                "unsupported sidecar schema rejects Open without mutating the current result",
                !unsupportedOpen
                && reopened.CurrentSignedDeltaPreviewOutput?.ContentSha256 == restoredHash,
                unsupportedMessage);
            File.WriteAllBytes(sidecarPath, originalSidecarBytes);

            var tamperText = File.ReadAllText(sidecarPath);
            var tamperedText = tamperText.Replace(
                outputBeforeSave.ContentSha256,
                new string('F', 64),
                StringComparison.Ordinal);
            File.WriteAllText(sidecarPath, tamperedText);
            var tamperedOpen = reopened.TryOpenTeachingRecipe(recipePath, out var tamperedMessage);
            Check(
                "tampered sidecar hash rejects Open without mutating the current result",
                !tamperedOpen
                && reopened.CurrentSignedDeltaPreviewOutput?.ContentSha256 == restoredHash,
                tamperedMessage);

            File.WriteAllBytes(sidecarPath, originalSidecarBytes);
            File.WriteAllText(sidecarPath, "{ malformed signed-delta sidecar");
            var malformedOpen = reopened.TryOpenTeachingRecipe(recipePath, out var malformedMessage);
            Check(
                "malformed sidecar rejects Open without mutating the current result",
                !malformedOpen
                && reopened.CurrentSignedDeltaPreviewOutput?.ContentSha256 == restoredHash,
                malformedMessage);

            File.WriteAllBytes(sidecarPath, originalSidecarBytes);
            var recoveryOpen = reopened.TryOpenTeachingRecipe(recipePath, out var recoveryMessage);
            Check(
                "valid sidecar recovers through the same Save/Open owner",
                recoveryOpen
                && reopened.CurrentSignedDeltaPreviewOutput?.ContentSha256 == restoredHash
                && reopened.IsSignedDeltaPreviewPublished,
                recoveryMessage);

            lines.Add($"Evidence: recipe={recipePath}; sidecar={sidecarPath}");
        }
        catch (Exception exception)
        {
            lines.Add($"ERROR | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("ERROR |", StringComparison.Ordinal));
        File.WriteAllLines(fullReportPath, lines);
        summary = string.Create(
            CultureInfo.InvariantCulture,
            $"SignedDeltaRecipeSidecar|{(succeeded ? "PASS" : "FAIL")}|checks={passed}/{total}|report={fullReportPath}");
        return succeeded;
    }

    private static void ConfigureBackground(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        string backgroundPath,
        string backgroundEntityId,
        long backgroundByteLength,
        C3DHeightGrid backgroundGrid,
        ToolRecipeSelectionSourceBinding currentIdentity)
    {
        if (workbench.SelectedStepPropertyDraft is not SignedDeltaBackgroundSubtractionStepProperties draft)
        {
            throw new InvalidOperationException("Signed-delta property adapter was not created.");
        }

        draft.BackgroundPath = backgroundPath;
        draft.BackgroundEntityId = backgroundEntityId;
        draft.BackgroundByteLength = backgroundByteLength;
        draft.BackgroundContentSha256 = backgroundGrid.ContentSha256;
        draft.BackgroundWidth = backgroundGrid.Width;
        draft.BackgroundHeight = backgroundGrid.Height;
        draft.BackgroundUnit = "raw-height";
        draft.BackgroundFrameId = "frame.c3d-grid-index";
        workbench.MarkSelectedStepParameterDraftDirty();
        if (!workbench.TryApplySelectedStepParameterDraft(out var message))
        {
            throw new InvalidOperationException(message);
        }

        if (step.Parameters.Count != C3DSignedHeightDeltaRecipePolicy.ParameterNames.Count
            || currentIdentity.GridWidth != backgroundGrid.Width
            || currentIdentity.GridHeight != backgroundGrid.Height)
        {
            throw new InvalidDataException("Signed-delta fixture parameter or grid setup is incomplete.");
        }
    }

    private static void WriteC3D(string path, int width, int height, IReadOnlyList<float> values)
    {
        var bytes = new byte[checked(8 + values.Count * sizeof(float))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), height);
        for (var index = 0; index < values.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(8 + index * sizeof(float)),
                BitConverter.SingleToInt32Bits(values[index]));
        }

        File.WriteAllBytes(path, bytes);
    }
}
