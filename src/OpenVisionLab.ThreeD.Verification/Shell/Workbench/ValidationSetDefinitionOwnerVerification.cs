using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ValidationSetDefinitionOwnerVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var fullReportPath = Path.GetFullPath(reportPath);
        var root = Path.Combine(
            Path.GetDirectoryName(fullReportPath)!,
            "validation-set-definition-owner-fixture");
        Directory.CreateDirectory(root);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Validation Set definition owner verification"
        };
        var passed = 0;
        var total = 0;

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
            var firstPath = Path.Combine(root, "first.C3D");
            var secondPath = Path.Combine(root, "second.C3D");
            var thirdPath = Path.Combine(root, "third.C3D");
            var recipePath = Path.Combine(root, "definition-roundtrip.ov3d-recipe.json");
            var missingRecipePath = Path.Combine(root, "missing-definition.ov3d-recipe.json");
            var sourceHash = new string('a', 64);
            var mismatchedHash = new string('b', 64);
            var documentFactoryCalls = 0;
            var definitionRefreshes = 0;
            var dirtyNotifications = 0;
            var document = CreateDocument(
                "Definition owner fixture",
                sourceHash);

            var owner = new ToolWorkbenchValidationSetDefinitionOwner(
                () =>
                {
                    documentFactoryCalls++;
                    return document;
                },
                () => document.Name,
                static (_, english) => english,
                _ => definitionRefreshes++,
                _ => dirtyNotifications++);

            var suppliedPaths = new[] { firstPath, secondPath, firstPath.ToUpperInvariant() };
            owner.SetValidationSetSources(suppliedPaths);
            Check(
                "source staging preserves caller input and stable order",
                suppliedPaths.SequenceEqual(
                    new[] { firstPath, secondPath, firstPath.ToUpperInvariant() })
                && owner.Samples.Select(sample => sample.SourcePath)
                    .SequenceEqual(new[] { Path.GetFullPath(firstPath), Path.GetFullPath(secondPath) })
                && owner.Samples.Select(sample => sample.Order).SequenceEqual([1, 2])
                && owner.Samples.All(sample => sample.Status == "Pending"),
                $"count={owner.Samples.Count};orders={string.Join(',', owner.Samples.Select(sample => sample.Order))};documentCalls={documentFactoryCalls}");

            var changedRole = owner.SetSelectedSampleRole(
                owner.Samples[1],
                ToolRecipeValidationSampleRole.Bad.ToString());
            Check(
                "role mutation resets only transient sample result and keeps definition identity",
                changedRole?.Role == ToolRecipeValidationSampleRole.Bad
                && changedRole.Status == "Pending"
                && changedRole.Steps.Count == 0
                && owner.Samples[1].Role == ToolRecipeValidationSampleRole.Bad
                && owner.IsValidationSetDefinitionDirty
                && documentFactoryCalls == 0,
                $"role={owner.Samples[1].Role};status={owner.Samples[1].Status};dirty={owner.IsValidationSetDefinitionDirty}");

            owner.SetValidationSetSources([secondPath, thirdPath, firstPath]);
            Check(
                "re-staging preserves existing role by case-insensitive source identity and reorders explicitly",
                owner.Samples.Select(sample => sample.SourcePath)
                    .SequenceEqual(
                    [
                        Path.GetFullPath(secondPath),
                        Path.GetFullPath(thirdPath),
                        Path.GetFullPath(firstPath)
                    ])
                && owner.Samples[0].Role == ToolRecipeValidationSampleRole.Bad
                && owner.Samples[1].IsUnset && owner.Samples[1].IsAdjustment
                && owner.Samples[2].IsUnset && owner.Samples[2].IsAdjustment,
                $"order={string.Join(',', owner.Samples.Select(sample => sample.FileName))};roles={string.Join(',', owner.Samples.Select(sample => sample.Role))}");

            owner.SaveForRecipe(recipePath);
            var manifestPath = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(recipePath);
            var saved = ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath);
            Check(
                "save writes the existing sidecar format with source SHA, order, paths, and roles",
                File.Exists(manifestPath)
                && saved is not null
                && saved.SchemaVersion == ToolRecipeValidationSetDefinition.CurrentSchemaVersion
                && saved.RecipeName == document.Name
                && saved.RecipeSourceSha256 == sourceHash
                && saved.Samples.Select(sample => sample.SourcePath)
                    .SequenceEqual(owner.Samples.Select(sample => sample.SourcePath))
                && saved.Samples.Select(sample => sample.Role)
                    .SequenceEqual(owner.Samples.Select(sample => sample.Role))
                && owner.IsValidationSetDefinitionDirty == false,
                $"manifest={manifestPath};roles={string.Join(',', saved?.Samples.Select(sample => sample.Role) ?? [])};dirty={owner.IsValidationSetDefinitionDirty}");

            var reopenedRefreshes = 0;
            var reopened = new ToolWorkbenchValidationSetDefinitionOwner(
                () => document,
                () => document.Name,
                static (_, english) => english,
                _ => reopenedRefreshes++,
                _ => { });
            var loaded = reopened.LoadForRecipe(recipePath, document);
            Check(
                "matching recipe reopen restores exact ordered roles and pending/no-run state",
                loaded
                && reopened.Samples.Select(sample => sample.SourcePath)
                    .SequenceEqual(owner.Samples.Select(sample => sample.SourcePath))
                && reopened.Samples.Select(sample => sample.Role)
                    .SequenceEqual(owner.Samples.Select(sample => sample.Role))
                && reopened.Samples.All(sample =>
                    sample.Status == "Pending"
                    && sample.Steps.Count == 0)
                && !reopened.IsValidationSetDefinitionDirty
                && reopenedRefreshes == 1,
                $"loaded={loaded};count={reopened.Samples.Count};refreshes={reopenedRefreshes};dirty={reopened.IsValidationSetDefinitionDirty}");

            var originalSavedSample = reopened.Samples[0];
            var toggledExpectation = reopened.SetSelectedSampleRole(originalSavedSample, "Good")!;
            Check("changing expectation alone marks sample definitions dirty", reopened.IsValidationSetDefinitionDirty && toggledExpectation.IsGood, "no numerical execution");
            reopened.SetSelectedSampleRole(toggledExpectation, originalSavedSample.IsUnset ? "Unset" : "Bad");
            Check("returning expectation to its saved value clears the dirty state", !reopened.IsValidationSetDefinitionDirty, "saved ordered definition equality");
            var toggledUsage = reopened.SetSelectedSampleRole(reopened.Samples[0], "HeldOut")!;
            reopened.SetSelectedSampleRole(toggledUsage, "HeldOut");
            Check("roundtrip sample use restores the clean state independently", !reopened.IsValidationSetDefinitionDirty, "expected verdict unchanged");
            reopened.SetValidationSetSources(reopened.Samples.Select(sample => sample.SourcePath).ToArray());
            Check("restaging identical ordered source definitions remains clean", !reopened.IsValidationSetDefinitionDirty, "paths/order/expectation/use match");

            var mismatch = document with
            {
                Source = document.Source with { ContentSha256 = mismatchedHash }
            };
            var mismatchLoaded = reopened.LoadForRecipe(recipePath, mismatch);
            Check(
                "source SHA mismatch fails closed without importing stale samples",
                !mismatchLoaded
                && reopened.Samples.Count == 0
                && !reopened.IsValidationSetDefinitionDirty,
                $"loaded={mismatchLoaded};count={reopened.Samples.Count};dirty={reopened.IsValidationSetDefinitionDirty}");

            var missingLoaded = reopened.LoadForRecipe(missingRecipePath, document);
            Check(
                "missing definition fails closed and leaves no samples",
                !missingLoaded
                && reopened.Samples.Count == 0
                && !reopened.IsValidationSetDefinitionDirty,
                $"loaded={missingLoaded};count={reopened.Samples.Count};dirty={reopened.IsValidationSetDefinitionDirty}");

            var held = owner.SetSelectedSampleRole(owner.Samples[0], "HeldOut")!;
            var heldOk = owner.SetSelectedSampleRole(held, "Good")!;
            Check("expectation can change on a held-out sample without changing its use",
                heldOk.IsGood && heldOk.IsHeldOut && heldOk.DevelopmentRole == ToolRecipeValidationSampleRole.HeldOut,
                $"expectation={heldOk.EffectiveExpectation};use={heldOk.EffectiveUsage}");
            owner.SaveForRecipe(recipePath);
            reopened.LoadForRecipe(recipePath, document);
            Check("independent expectation and use survive save and reopen", reopened.Samples[0].IsGood && reopened.Samples[0].IsHeldOut
                && reopened.Samples[1].IsUnset && reopened.Samples[1].IsAdjustment, "explicit expectation/use; pending; no execution");
            var adjusted = owner.SetSelectedSampleRole(heldOk, "Adjustment")!;
            Check("changing use preserves the expected verdict", adjusted.IsGood && adjusted.IsAdjustment && owner.IsValidationSetDefinitionDirty, "OK retained; definition dirty");
            var unset = owner.SetSelectedSampleRole(adjusted, "Unset")!;
            Check("unset expectation is excluded from agreement and development", unset.IsUnset && unset.IsAdjustment && unset.Agreement == "Unset"
                && unset.DevelopmentRole == ToolRecipeValidationSampleRole.HeldOut, "no invented expected verdict");
            var lockedSaveFailed = false;
            using (var locked = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { owner.SaveForRecipe(recipePath); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { lockedSaveFailed = true; }
            }
            Check("sample definition save failure preserves dirty state and prior manifest", lockedSaveFailed && owner.IsValidationSetDefinitionDirty
                && ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath)!.Samples[0].EffectiveExpectation == ToolRecipeValidationExpectation.Ok,
                "isolated sidecar locked; original definitions retained");
            owner.SaveForRecipe(recipePath);
            Check("valid retry saves definitions without mutating recipe conditions", !owner.IsValidationSetDefinitionDirty
                && System.Text.Json.JsonSerializer.Serialize(document) == System.Text.Json.JsonSerializer.Serialize(CreateDocument("Definition owner fixture", sourceHash)), "recipe source/conditions unchanged");
            var legacyPath = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(missingRecipePath);
            File.WriteAllText(legacyPath, System.Text.Json.JsonSerializer.Serialize(new ToolRecipeValidationSetDefinition("1.0", document.Name, sourceHash,
                [new(1, firstPath, ToolRecipeValidationSampleRole.Good), new(2, secondPath, ToolRecipeValidationSampleRole.Bad), new(3, thirdPath, ToolRecipeValidationSampleRole.HeldOut)]),
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
            var legacy = ToolRecipeValidationSetDefinitionStore.LoadForRecipe(missingRecipePath)!;
            Check("legacy Good/Bad/HeldOut resolve to OK/NG/unset with independent use", legacy.SchemaVersion == "2.0"
                && legacy.Samples[0].EffectiveExpectation == ToolRecipeValidationExpectation.Ok
                && legacy.Samples[1].EffectiveExpectation == ToolRecipeValidationExpectation.Ng
                && legacy.Samples[2].EffectiveExpectation == ToolRecipeValidationExpectation.Unset
                && legacy.Samples[2].EffectiveUsage == ToolRecipeValidationUsage.HeldOut, "schema1.0 read; schema2.0 normalized");
            var documentFactoryCallsBeforeClear = documentFactoryCalls;
            owner.ClearDefinition();
            Check(
                "clear marks definition dirty and invokes only the projection boundary",
                owner.Samples.Count == 0
                && owner.IsValidationSetDefinitionDirty
                && definitionRefreshes >= 3
                && dirtyNotifications >= 2
                && documentFactoryCalls == documentFactoryCallsBeforeClear,
                $"count={owner.Samples.Count};dirty={owner.IsValidationSetDefinitionDirty};refreshes={definitionRefreshes};dirtyNotifications={dirtyNotifications};documentCalls={documentFactoryCalls}");

            var documentFactoryCallsBeforeEmptySave = documentFactoryCalls;
            owner.SaveForRecipe(recipePath);
            Check(
                "empty save removes the existing definition sidecar without execution",
                !File.Exists(manifestPath)
                && !owner.IsValidationSetDefinitionDirty
                && documentFactoryCalls == documentFactoryCallsBeforeEmptySave,
                $"manifestExists={File.Exists(manifestPath)};dirty={owner.IsValidationSetDefinitionDirty};documentCalls={documentFactoryCalls};execution=not-invoked");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        var success = total > 0 && passed == total;
        summary =
            $"ValidationSetDefinitionOwner|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return success;
    }


    public static bool VerifyExpectationUsageMatrix(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|DQ13 declaration round-trip and review/observation eligibility; controlled recorded rows, no inspection or SDK accuracy verification." };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            var sourcePath = Path.Combine(root, "matrix-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.validation-definition-owner", 2, 2, [10, 11, 12, 13]).SaveC3D(sourcePath);
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var document = CreateDocument("DQ13 matrix", binding.ContentSha256) with { Source = CreateDocument("DQ13 matrix", binding.ContentSha256).Source with { Path = sourcePath, ByteLength = new FileInfo(sourcePath).Length } };
            var recipePath = Path.Combine(root, "matrix.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var recipeBytes = File.ReadAllBytes(recipePath);
            var declarations = (from expectation in new[] { ToolRecipeValidationExpectation.Unset, ToolRecipeValidationExpectation.Ok, ToolRecipeValidationExpectation.Ng }
                from usage in new[] { ToolRecipeValidationUsage.Adjustment, ToolRecipeValidationUsage.HeldOut }
                select (expectation, usage)).ToArray();
            var paths = declarations.Select((_, index) => Path.Combine(root, $"sample-{index}.C3D")).ToArray();
            foreach (var path in paths) File.Copy(sourcePath, path);
            var changes = 0;
            var owner = new ToolWorkbenchValidationSetDefinitionOwner(() => document, () => document.Name, (_, english) => english, _ => changes++, _ => { });
            owner.SetValidationSetSources(paths);
            for (var index = 0; index < declarations.Length; index++)
            {
                var (expectation, usage) = declarations[index];
                var row = owner.Samples[index];
                row = owner.SetSelectedSampleRole(row, expectation switch { ToolRecipeValidationExpectation.Ok => "Good", ToolRecipeValidationExpectation.Ng => "Bad", _ => "Unset" }) ?? row;
                if (usage == ToolRecipeValidationUsage.HeldOut) row = owner.SetSelectedSampleRole(row, "HeldOut")!;
                Check($"{expectation}/{usage} independent declaration", row.EffectiveExpectation == expectation && row.EffectiveUsage == usage && row.Status == "Pending" && row.Steps.Count == 0, "definition mutation only");
            }
            owner.SaveForRecipe(recipePath);
            var reopened = new ToolWorkbenchValidationSetDefinitionOwner(() => document, () => document.Name, (_, english) => english, _ => { }, _ => { });
            Check("all six declarations reopen", reopened.LoadForRecipe(recipePath, document) && reopened.Samples.Count == 6 && !reopened.IsValidationSetDefinitionDirty, "actual definition store");
            for (var index = 0; index < declarations.Length; index++)
                Check($"{declarations[index]} saved identity", reopened.Samples[index].EffectiveExpectation == declarations[index].expectation && reopened.Samples[index].EffectiveUsage == declarations[index].usage && reopened.Samples[index].Status == "Pending", "no execution on load");
            Check("sample declaration save preserves recipe bytes", recipeBytes.SequenceEqual(File.ReadAllBytes(recipePath)) && changes > 0, "no recipe condition write");

            var rows = new System.Collections.ObjectModel.ObservableCollection<ValidationSetSampleRow>();
            var executionRows = new List<OpenVisionLab.ThreeD.Tools.ToolRecipeValidationSampleResult>();
            foreach (var (expectation, usage) in declarations)
            foreach (var status in new[] { "Pass", "Fail", "Error", "Pending" })
            {
                var order = rows.Count + 1;
                var role = ToolRecipeValidationSampleDefinition.ResolveDevelopmentRole(expectation, usage);
                var finite = status is "Pass" or "Fail";
                var lower = expectation == ToolRecipeValidationExpectation.Unset ? -1000d : 0d;
                var upper = expectation == ToolRecipeValidationExpectation.Unset ? 1000d : 10d;
                ValidationSetMetricRow[] metrics = [new("Height", finite ? "4" : "—", "raw-height", status, status, finite ? 4 : null), new("LowerLimit", lower.ToString(System.Globalization.CultureInfo.InvariantCulture), "raw-height", "", "", lower), new("UpperLimit", upper.ToString(System.Globalization.CultureInfo.InvariantCulture), "raw-height", "", "", upper)];
                var row = new ValidationSetSampleRow(order, paths[0], role, status, status, "isolated recorded result", "", status == "Pending" ? [] : [new(1, "step.matrix", "Matrix", status, status, "", metrics, [])]) { Expectation = expectation, Usage = usage };
                rows.Add(row);
                var mismatch = expectation != ToolRecipeValidationExpectation.Unset && (status == "Error" || expectation == ToolRecipeValidationExpectation.Ok && status == "Fail" || expectation == ToolRecipeValidationExpectation.Ng && status == "Pass");
                Check($"{expectation}/{usage}/{status} comparison eligibility", row.IsMismatch == mismatch, $"agreement={row.Agreement};mismatch={row.IsMismatch}");
                executionRows.Add(new(order, sourcePath, binding.ContentSha256, role, Enum.Parse<ResultStatus>(status == "Pending" ? "Warning" : status), "recorded", TimeSpan.Zero,
                    finite ? [new(1, "step.matrix", "Matrix", Enum.Parse<ResultStatus>(status), "", [new("Height", MetricKind.Number, 4, "raw-height"), new(C3DCompletenessMetricNames.FiniteCoverage("r001.c001"), MetricKind.Number, 0.9, "ratio")], [])] : []) { Expectation = expectation, Usage = usage });
            }
            var review = new ToolWorkbenchValidationSetReviewOwner(rows, () => false, () => true, () => throw new InvalidOperationException("No automatic comparison expected"));
            review.RefreshSamples();
            Check("unset errors are excluded from mismatch count/list", review.ValidationSetMismatchCount == 8 && review.ValidationMismatchSamples.All(row => row.HasExpectation), $"mismatch={review.ValidationSetMismatchCount}");
            review.SetFilter(ValidationSetStatusFilter.Error);
            Check("all recorded errors remain visible", review.ValidationSetSamples.Count == 6 && review.ValidationSetSamples.Count(row => row.IsUnset) == 2, "raw Error state is independent of expectation");
            review.SetFilter(ValidationSetStatusFilter.Mismatch);
            Check("mismatch filter excludes every unset sample", review.ValidationSetSamples.Count == 8 && review.ValidationSetSamples.All(row => row.HasExpectation), $"visible={review.ValidationSetSamples.Count}");
            review.SetFilter(ValidationSetStatusFilter.All);
            Check("agreement denominator uses known adjustment samples", review.ValidationSetAdjustmentCount == 8 && review.ValidationSetMatchCount == 2 && review.ValidationSetFalseAcceptCount == 1 && review.ValidationSetFalseRejectCount == 1 && review.ValidationSetAdjustmentErrorCount == 2 && review.ValidationSetAdjustmentPendingCount == 2, "counts derived from six declarations × four recorded states");
            var distribution = review.ValidationDistributionOutputs.Single();
            Check("value distribution excludes unset under both uses", distribution.Points.Count == 8 && distribution.Points.All(point => point.Sample.HasExpectation), $"points={distribution.Points.Count}");
            Check("missing executed limits remain unknown", distribution.Minimum is null && distribution.Maximum is null, "known Pending rows have no recorded bounds; never inherit live settings");
            var completedRows = new System.Collections.ObjectModel.ObservableCollection<ValidationSetSampleRow>(rows.Where(row => row.Status is "Pass" or "Fail"));
            var completedReview = new ToolWorkbenchValidationSetReviewOwner(completedRows, () => false, () => true, () => { });
            completedReview.RefreshSamples();
            var completedDistribution = completedReview.ValidationDistributionOutputs.Single();
            Check("unset ranges cannot erase recorded known-sample limits", completedDistribution.Minimum == 0 && completedDistribution.Maximum == 10, $"completed range={completedDistribution.Minimum}..{completedDistribution.Maximum}");

            var result = new OpenVisionLab.ThreeD.Tools.ToolRecipeValidationSetResult(ResultStatus.Pass, "recorded fixture", TimeSpan.Zero, executionRows);
            var observed = OpenVisionLab.ThreeD.Tools.ToolRecipeLabeledEvidenceAnalyzer.CollectObservations(document, result);
            var knownOrders = rows.Where(row => row.HasExpectation && row.Status is "Pass" or "Fail").Select(row => row.Order).ToHashSet();
            Check("candidate step observations exclude every unset sample", observed.Count == 16 && observed.All(item => knownOrders.Contains(item.SampleOrder)), $"observations={observed.Count};not an SDK numerical check");
            var completenessDocument = document with { Steps = [new("step.matrix", "completeness-grid", "Matrix", 1, [document.Source.Id], "output.matrix", [])] };
            var completeness = OpenVisionLab.ThreeD.Tools.ToolRecipeLabeledEvidenceAnalyzer.CollectObservations(completenessDocument, result);
            Check("completeness candidate observations exclude every unset sample", completeness.Count == 24 && completeness.All(item => knownOrders.Contains(item.SampleOrder)), $"observations={completeness.Count}");
            var selection = new ToolRecipeSelection("selection.matrix", "Matrix region", ToolRecipeSelectionKinds.GridRectangle, document.Source.Id, document.Source.FrameId, binding, new(0, 0, 2, 2), null, null);
            var regionDocument = document with { Selections = [selection], Steps = [new("step.matrix", "thickness", "Matrix", 1, [document.Source.Id, selection.Id], "output.matrix", [])] };
            var region = OpenVisionLab.ThreeD.Tools.ToolRecipeLabeledEvidenceAnalyzer.CollectObservations(regionDocument, result);
            Check("region observations exclude every unset sample regardless of use", region.Where(item => item.Scope == ToolRecipeEvidenceScope.RegionMetric).All(item => rows[item.SampleOrder - 1].HasExpectation)
                && region.Count(item => item.Scope == ToolRecipeEvidenceScope.RegionMetric) == 32, $"region observations={region.Count(item => item.Scope == ToolRecipeEvidenceScope.RegionMetric)};existing SDK result consumed, arithmetic not tested");
        }
        catch (Exception exception) { total++; lines.Add("FAIL | unexpected | " + exception); }
        summary = $"ValidationExpectationUsageMatrix|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool VerifyRecipeSaveRollback(string reportPath, out string summary)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var root = Path.GetDirectoryName(fullReportPath)!;
        var lines = new List<string> { "Boundary|Isolated recipe/definition/evidence files; full Workbench save entry point; no inspection execution or SDK verification." };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            var recipePath = Path.Combine(root, "transaction.ov3d-recipe.json");
            var document = ToolRecipeDocumentStore.Load(Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json"));
            ToolRecipeDocumentStore.Save(recipePath, document);
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            Check("transaction fixture opens", workbench.TryOpenTeachingRecipe(recipePath, out var message), message);
            workbench.SetValidationSetSources([document.Source.Path]);
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single();
            workbench.SetValidationSampleRoleCommand.Execute("Good");
            Check("transaction baseline saves recipe and explicit sample definition", workbench.TrySaveTeachingRecipe(recipePath, out message)
                && !workbench.IsDirty && !workbench.IsValidationSetDefinitionDirty, message);
            var definitionPath = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(recipePath);
            var evidencePath = ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(recipePath);
            var recipeBefore = File.ReadAllBytes(recipePath);
            var definitionBefore = File.ReadAllBytes(definitionPath);
            var identityBefore = workbench.RecipeSession.SavedSha256;
            workbench.RecipeName += " updated";
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single();
            workbench.SetValidationSampleRoleCommand.Execute("Bad");
            bool saved;
            using (var locked = new FileStream(definitionPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                saved = workbench.TrySaveTeachingRecipe(recipePath, out message);
            Check("definition replacement failure rolls back recipe bytes", !saved && File.ReadAllBytes(recipePath).SequenceEqual(recipeBefore)
                && File.ReadAllBytes(definitionPath).SequenceEqual(definitionBefore), message);
            Check("failed multipart save retains pending changes and saved identity", workbench.IsDirty && workbench.IsValidationSetDefinitionDirty
                && workbench.RecipeSession.SavedSha256 == identityBefore && !workbench.IsRecipeSaving && workbench.RecipePath == recipePath,
                $"dirty={workbench.IsDirty};samples={workbench.IsValidationSetDefinitionDirty};saving={workbench.IsRecipeSaving}");
            Check("unlock and retry saves the pending condition and sample changes", workbench.TrySaveTeachingRecipe(recipePath, out message)
                && !workbench.IsDirty && !workbench.IsValidationSetDefinitionDirty
                && ToolRecipeDocumentStore.Load(recipePath).Name == workbench.RecipeName
                && ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath)!.Samples.Single().EffectiveExpectation == ToolRecipeValidationExpectation.Ng, message);
            recipeBefore = File.ReadAllBytes(recipePath);
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single();
            workbench.SetValidationSampleRoleCommand.Execute("Good");
            Check("sample-only retry keeps recipe bytes unchanged", workbench.TrySaveTeachingRecipe(recipePath, out message)
                && File.ReadAllBytes(recipePath).SequenceEqual(recipeBefore) && !workbench.IsValidationSetDefinitionDirty, message);
            File.WriteAllText(evidencePath, "isolated preexisting evidence bytes");
            recipeBefore = File.ReadAllBytes(recipePath);
            definitionBefore = File.ReadAllBytes(definitionPath);
            var evidenceBefore = File.ReadAllBytes(evidencePath);
            workbench.RecipeName += " evidence";
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single();
            workbench.SetValidationSampleRoleCommand.Execute("HeldOut");
            using (var locked = new FileStream(evidencePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                saved = workbench.TrySaveTeachingRecipe(recipePath, out message);
            Check("evidence removal failure rolls back both earlier file writes", !saved && File.ReadAllBytes(recipePath).SequenceEqual(recipeBefore)
                && File.ReadAllBytes(definitionPath).SequenceEqual(definitionBefore) && File.ReadAllBytes(evidencePath).SequenceEqual(evidenceBefore), message);
            Check("second-stage failure keeps both drafts pending", workbench.IsDirty && workbench.IsValidationSetDefinitionDirty && !workbench.IsRecipeSaving,
                "file identity accepted only after all three persistence stages");
            Check("evidence unlock and retry recovers all three stages", workbench.TrySaveTeachingRecipe(recipePath, out message)
                && !File.Exists(evidencePath) && !workbench.IsDirty && !workbench.IsValidationSetDefinitionDirty, message);
            var saveAsPath = Path.Combine(root, "new-target.ov3d-recipe.json");
            var saveAsDefinition = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(saveAsPath);
            Directory.CreateDirectory(saveAsDefinition);
            var originalPath = workbench.RecipePath;
            recipeBefore = File.ReadAllBytes(recipePath);
            workbench.RecipeName += " save-as";
            saved = workbench.TrySaveTeachingRecipe(saveAsPath, out message);
            Check("new-target failure removes the newly created partial recipe", !saved && !File.Exists(saveAsPath)
                && Directory.Exists(saveAsDefinition) && workbench.RecipePath == originalPath && workbench.IsDirty
                && File.ReadAllBytes(recipePath).SequenceEqual(recipeBefore), message);
            Directory.Delete(saveAsDefinition);
            Check("new-target retry accepts the complete destination", workbench.TrySaveTeachingRecipe(saveAsPath, out message)
                && File.Exists(saveAsPath) && File.Exists(saveAsDefinition) && workbench.RecipePath == saveAsPath && !workbench.IsDirty, message);
            using var reopened = new ToolWorkbenchViewModel(Path.Combine(root, "reopened-recent.json"));
            Check("reopen restores saved condition and independent expectation/use", reopened.TryOpenTeachingRecipe(saveAsPath, out message)
                && reopened.RecipeName == workbench.RecipeName && reopened.ValidationSetSamples.Single().IsGood
                && reopened.ValidationSetSamples.Single().IsHeldOut && !reopened.IsDirty && !reopened.IsValidationSetDefinitionDirty
                && !reopened.IsOrderedRunRunning && !reopened.IsSelectedStepPreviewRunning, message);
            Check("successful recovery leaves no save backups", !Directory.EnumerateFiles(root, "*.save-backup-*").Any(), "only owned test files; no Run or numeric execution");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        var success = total > 0 && passed == total;
        summary = $"ValidationRecipeSaveRollback|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Add(summary);
        Directory.CreateDirectory(root);
        File.WriteAllLines(fullReportPath, lines);
        return success;
    }


    public static bool VerifyRecipeOpenPreservation(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var lines = new List<string> { "Boundary|Production recipe Open; isolated malformed/locked sidecars; no UI or SDK numerical acceptance." };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            var fixture = Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json");
            foreach (var kind in new[] { "definition", "threshold" })
            foreach (var fault in new[] { "truncated", "unsupported", "locked" })
            {
                var caseRoot = Path.Combine(root, kind + "-" + fault);
                Directory.CreateDirectory(caseRoot);
                using var workbench = new ToolWorkbenchViewModel(Path.Combine(caseRoot, "recent.json"));
                if (!workbench.TryOpenTeachingRecipe(fixture, out var message)) throw new InvalidOperationException(message);
                var originalPath = Path.Combine(caseRoot, "original.ov3d-recipe.json");
                if (!workbench.TrySaveTeachingRecipe(originalPath, out message)) throw new InvalidOperationException(message);
                var incomingPath = Path.Combine(caseRoot, "incoming.ov3d-recipe.json");
                var original = ToolRecipeDocumentStore.Load(originalPath);
                ToolRecipeDocumentStore.Save(incomingPath, original with { Name = "Incoming must not replace pending work" });
                workbench.SetValidationSetSources([workbench.Source.Path]);
                workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single();
                workbench.SetValidationSampleRoleCommand.Execute("Bad");
                workbench.SetValidationSampleRoleCommand.Execute("HeldOut");
                workbench.SelectPipelineStep(workbench.PipelineSteps.First(step => step.ToolId == "thickness").Id);
                var draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
                draft.MaximumThickness += 1;
                workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
                string Capture() => System.Text.Json.JsonSerializer.Serialize(new
                {
                    workbench.RecipeName,
                    workbench.Source.Path,
                    workbench.SourceSession.SourceBinding,
                    workbench.Selections,
                    Steps = workbench.PipelineSteps.Select(step => new { step.Id, step.ToolId, step.InputEntityIds, step.OutputEntityId, step.Parameters, step.AcceptanceLimitsConfigured })
                });
                var before = Capture();
                var savedIdentity = workbench.RecipeSession.SavedSha256;
                var recipeBytes = File.ReadAllBytes(originalPath);
                var sidecar = kind == "definition" ? ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(incomingPath)
                    : ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(incomingPath);
                File.WriteAllText(sidecar, fault == "unsupported" ? "{}" : "{");
                var incomingBytes = File.ReadAllBytes(incomingPath);
                var badBytes = File.ReadAllBytes(sidecar);
                bool opened;
                using (var locked = fault == "locked" ? new FileStream(sidecar, FileMode.Open, FileAccess.Read, FileShare.None) : null)
                    opened = workbench.TryOpenTeachingRecipe(incomingPath, out message);
                Check($"{kind}/{fault} rejects Open with a reason", !opened && !string.IsNullOrWhiteSpace(message), message);
                Check($"{kind}/{fault} retains recipe/source/ROI/condition identity", workbench.RecipePath == originalPath
                    && savedIdentity == workbench.RecipeSession.SavedSha256 && before == Capture(), workbench.RecipeName);
                Check($"{kind}/{fault} retains pending editor and independent sample declarations", ReferenceEquals(draft, workbench.SelectedStepPropertyDraft)
                    && workbench.HasPendingStepParameterChanges && workbench.IsValidationSetDefinitionDirty
                    && workbench.ValidationSetSamples is { Count: 1 } && workbench.ValidationSetSamples.Single().IsBad && workbench.ValidationSetSamples.Single().IsHeldOut,
                    $"pending={workbench.HasPendingStepParameterChanges};samples={workbench.ValidationSetSamples.Count}");
                Check($"{kind}/{fault} preserves all files", recipeBytes.SequenceEqual(File.ReadAllBytes(originalPath))
                    && incomingBytes.SequenceEqual(File.ReadAllBytes(incomingPath)) && badBytes.SequenceEqual(File.ReadAllBytes(sidecar)), "byte equality after rejected Open");
                // Repair only the owned test sidecar, then prove the same Open entry point recovers.
                File.Delete(sidecar);
                workbench.DiscardSelectedStepParameterDraftCommand.Execute(null);
                Check($"{kind}/{fault} valid retry after explicit draft cancellation opens without execution", workbench.TryOpenTeachingRecipe(incomingPath, out message)
                    && workbench.RecipeName == "Incoming must not replace pending work" && !workbench.IsDirty && !workbench.IsValidationSetDefinitionDirty
                    && !workbench.HasPendingStepParameterChanges && !workbench.IsOrderedRunRunning && !workbench.IsSelectedStepPreviewRunning
                    && workbench.RunLog.All(entry => entry.Category is not ("Run" or "Preview" or "Publish")), message);
            }
        }
        catch (Exception exception) { Check("unexpected exception", false, exception.ToString()); }
        summary = $"RecipeOpenPreservation|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    private static ToolRecipeDocument CreateDocument(
        string name,
        string sourceHash) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            name,
            new ToolRecipeSource(
                "source.validation-definition-owner",
                "Definition owner source",
                "C3D",
                "raw-height",
                "frame.c3d-grid-index",
                "definition-owner-source.C3D",
                16,
                sourceHash,
                2,
                2),
            [],
            []);
}
