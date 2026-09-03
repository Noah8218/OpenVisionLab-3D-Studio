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
                && owner.Samples[1].Role == ToolRecipeValidationSampleRole.Good
                && owner.Samples[2].Role == ToolRecipeValidationSampleRole.Good,
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
