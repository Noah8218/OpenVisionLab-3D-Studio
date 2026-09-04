using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Artifacts;

internal static class RenderableC3DCatalogVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench Renderable C3D Catalog verification"
        };
        var passed = 0;
        var total = 0;
        var fullReportPath = Path.GetFullPath(reportPath);
        var fixtureRoot = Path.Combine(
            Path.GetDirectoryName(fullReportPath)!,
            "renderable-c3d-catalog-fixtures",
            Guid.NewGuid().ToString("N"));

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
            Directory.CreateDirectory(fixtureRoot);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.c3d.height-map",
                2,
                2,
                [1d, 2d, 3d, 4d]);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            source.SaveC3D(sourcePath);

            var preparationDefinitions = new[]
            {
                (ToolId: "filter", Name: "Filter", Suffix: "filter"),
                (ToolId: "remove-outlier-pixels", Name: "Remove Outlier", Suffix: "remove-outlier"),
                (ToolId: "domain-mask", Name: "Domain Mask", Suffix: "domain-mask"),
                (ToolId: "level-surface", Name: "Level Surface", Suffix: "level-surface"),
                (ToolId: "roi-crop", Name: "ROI Crop", Suffix: "roi-crop")
            };
            var preparations = new List<ToolWorkbenchRenderableC3DPreparationSnapshot>();
            var artifacts = new List<ToolWorkbenchArtifactItem>
            {
                new(
                    source.EntityId,
                    "Source C3D",
                    "SourceC3D / RawHeightField",
                    "Ready",
                    source.EntityId,
                    string.Empty,
                    source.Unit,
                    source.FrameId,
                    source.ContentSha256,
                    "Verified C3D source.",
                    null,
                    "Source")
            };

            for (var index = 0; index < preparationDefinitions.Length; index++)
            {
                var definition = preparationDefinitions[index];
                var outputEntityId = $"derived.{definition.Suffix}.01";
                var output = source.CreateDerived(
                    outputEntityId,
                    [1d + index, 2d + index, 3d + index, 4d + index],
                    $"verification:{definition.ToolId}");
                var path = Path.Combine(fixtureRoot, $"{definition.Suffix}.c3d");
                output.SaveC3D(path);
                var tool = new ToolWorkbenchToolItem(
                    "Preparation",
                    definition.Name,
                    definition.ToolId,
                    1,
                    "C3D",
                    "HeightField",
                    "Verification preparation tool.",
                    []);
                var step = new ToolWorkbenchPipelineStepItem(
                    $"step.{definition.Suffix}",
                    tool,
                    source.EntityId,
                    outputEntityId);
                var quality = new SourceQualityDelta(
                    source.EntityId,
                    source.ContentSha256,
                    output.EntityId,
                    output.ContentSha256,
                    source.RootSourceSha256,
                    output.RootSourceSha256,
                    source.ValidCount,
                    output.ValidCount,
                    source.MissingCount,
                    output.MissingCount,
                    null,
                    "not evaluated by verification");
                var contract = definition.ToolId is "filter" or "remove-outlier-pixels"
                    ? "FilteredHeightField"
                    : definition.ToolId == "level-surface"
                        ? "LeveledHeightField"
                        : "HeightField";
                artifacts.Add(
                    new ToolWorkbenchArtifactItem(
                        output.EntityId,
                        definition.Name,
                        contract,
                        index % 2 == 0 ? "Preview" : "Published",
                        source.EntityId,
                        source.EntityId,
                        source.Unit,
                        source.FrameId,
                        output.ContentSha256,
                        $"{definition.Name} metadata and quality evidence.",
                        step,
                        contract)
                    {
                        PreparationQualityDelta = quality
                    });
                preparations.Add(
                    new ToolWorkbenchRenderableC3DPreparationSnapshot(
                        definition.ToolId,
                        output,
                        path,
                        true,
                        false,
                        index % 2 == 0));
            }

            var validationOne = C3DHeightFieldSnapshot.CreateForVerification(
                "validation.sample.1",
                2,
                2,
                [5d, 6d, 7d, 8d]);
            var validationTwo = C3DHeightFieldSnapshot.CreateForVerification(
                "validation.sample.2",
                2,
                2,
                [9d, 10d, 11d, 12d]);
            var validationOnePath = Path.Combine(fixtureRoot, "validation-one.c3d");
            var validationTwoPath = Path.Combine(fixtureRoot, "validation-two.c3d");
            validationOne.SaveC3D(validationOnePath);
            validationTwo.SaveC3D(validationTwoPath);

            var snapshot = new ToolWorkbenchRenderableC3DCatalogSnapshot(
                new ToolWorkbenchRenderableC3DSourceSnapshot(
                    source.EntityId,
                    "Source C3D",
                    "C3D",
                    source.Unit,
                    source.FrameId,
                    sourcePath,
                    source.ContentSha256,
                    "Source snapshot detail.",
                    true,
                    new ToolRecipeSelectionSourceBinding(
                        "C3D",
                        source.ContentSha256,
                        source.Width,
                        source.Height,
                        source.EntityId,
                        source.RootSourceSha256,
                        source.Unit,
                        source.FrameId)),
                artifacts,
                preparations.AsEnumerable().Reverse().ToArray(),
                [
                    new ToolWorkbenchRenderableC3DValidationSampleSnapshot(
                        2,
                        validationTwoPath,
                        "Validation Set #2",
                        "ValidationSample / C3D",
                        "Pass",
                        "second sample"),
                    new ToolWorkbenchRenderableC3DValidationSampleSnapshot(
                        1,
                        validationOnePath,
                        "Validation Set #1",
                        "ValidationSample / C3D",
                        "Pending",
                        "first sample")
                ]);

            var owner = new ToolWorkbenchRenderableC3DCatalogOwner();
            var sourceStateBefore = snapshot.Source;
            var artifactIdsBefore = snapshot.Artifacts.Select(artifact => artifact.Id).ToArray();
            var preparationIdsBefore = snapshot.PreparationOutputs
                .Select(preparation => preparation.Output?.EntityId ?? string.Empty)
                .ToArray();
            owner.Rebuild(snapshot);

            var expectedIds = new[]
            {
                source.EntityId,
                "validation.sample.1",
                "validation.sample.2",
                "derived.filter.01",
                "derived.remove-outlier.01",
                "derived.domain-mask.01",
                "derived.level-surface.01",
                "derived.roi-crop.01"
            };
            Check(
                "source, Validation Set samples, and exactly five preparation outputs are projected",
                owner.Targets.Count == expectedIds.Length
                && owner.Targets.Select(target => target.Id).SequenceEqual(expectedIds),
                $"ids={string.Join(",", owner.Targets.Select(target => target.Id))}");
            Check(
                "case-insensitive catalog lookup is stable",
                owner.GetTarget("DeRiVeD.LeVeL-SURFACE.01")?.Id == "derived.level-surface.01"
                && owner.GetTarget("VALIDATION.SAMPLE.2")?.C3DPath == validationTwoPath,
                "lookup=passed");
            Check(
                "source target retains identity, state, path, and source flag",
                owner.GetTarget(source.EntityId) is
                {
                    C3DPath: var projectedSourcePath,
                    IsSource: true,
                    IsDisplayable: true,
                    State: "Ready"
                }
                && projectedSourcePath == sourcePath,
                $"sourcePath={owner.GetTarget(source.EntityId)?.C3DPath}");
            Check(
                "preparation metadata and optional quality evidence are preserved",
                owner.GetTarget("derived.filter.01") is { } filterTarget
                && !filterTarget.IsSource
                && filterTarget.IsDisplayable
                && filterTarget.Contract == "FilteredHeightField"
                && filterTarget.State == "Preview"
                && filterTarget.PreparationQualityDelta?.DerivedEntityId == "derived.filter.01"
                && filterTarget.PreparationQualityDelta?.SourceEntityId == source.EntityId,
                $"quality={owner.GetTarget("derived.filter.01")?.PreparationQualityDelta?.Summary}");
            Check(
                "Validation Set IDs and order are preserved independently of input enumeration order",
                owner.Targets.Skip(1).Take(2).Select(target => target.Id)
                    .SequenceEqual(["validation.sample.1", "validation.sample.2"]),
                "validation-order=1,2");

            var invalidPreparations = new[]
            {
                preparations[0] with { IsStale = true },
                preparations[1] with { C3DPath = Path.Combine(fixtureRoot, "missing.c3d") },
                preparations[2] with { C3DPath = " " },
                preparations[3],
                preparations[4]
            };
            var mismatchedArtifacts = artifacts
                .Select(artifact => artifact.Id == preparations[3].Output!.EntityId
                    ? artifact with { ContentSha256 = new string('0', 64) }
                    : artifact)
                .ToArray();
            var invalidSnapshot = snapshot with
            {
                Artifacts = mismatchedArtifacts,
                PreparationOutputs = invalidPreparations
                    .Append(new ToolWorkbenchRenderableC3DPreparationSnapshot(
                        "height-difference-edge",
                        preparations[0].Output,
                        preparations[0].C3DPath,
                        true,
                        false,
                        true))
                    .ToArray(),
                ValidationSamples =
                [
                    snapshot.ValidationSamples[0],
                    snapshot.ValidationSamples[1],
                    new ToolWorkbenchRenderableC3DValidationSampleSnapshot(
                        3,
                        Path.Combine(fixtureRoot, "evidence-only.txt"),
                        "Evidence only",
                        "Evidence / non-C3D",
                        "Pass",
                        "not a C3D sample")
                ]
            };
            File.WriteAllText(
                Path.Combine(fixtureRoot, "evidence-only.txt"),
                "evidence-only output");
            owner.Rebuild(invalidSnapshot);
            Check(
                "stale, missing, and blank paths fail closed",
                owner.GetTarget("derived.filter.01") is null
                && owner.GetTarget("derived.remove-outlier.01") is null
                && owner.GetTarget("derived.domain-mask.01") is null,
                "stale=removed;missing=removed;blank=removed");
            Check(
                "artifact identity mismatch fails closed",
                owner.GetTarget("derived.level-surface.01") is null,
                "mismatched-artifact=removed");
            Check(
                "evidence-only and non-C3D outputs are not render targets",
                owner.GetTarget("validation.sample.3") is null
                && owner.GetTarget("height-difference-edge") is null,
                "evidence-only=removed;non-c3d=removed");

            var nonC3DPreparation = preparations[4] with
            {
                C3DPath = Path.Combine(fixtureRoot, "roi-crop.txt")
            };
            File.WriteAllText(nonC3DPreparation.C3DPath, "not C3D");
            owner.Rebuild(snapshot with
            {
                PreparationOutputs = preparations
                    .Take(4)
                    .Append(nonC3DPreparation)
                    .ToArray()
            });
            Check(
                "typed preparation output with a non-C3D path fails closed",
                owner.GetTarget("derived.roi-crop.01") is null,
                "extension-and-header=validated");

            var sourceMismatch = snapshot with
            {
                Source = snapshot.Source with { ContentSha256 = new string('1', 64) }
            };
            owner.Rebuild(sourceMismatch);
            Check(
                "source binding and artifact identity mismatch fail closed",
                owner.GetTarget(source.EntityId) is null,
                "source-mismatch=removed");

            owner.Rebuild(snapshot with { PreparationOutputs = [], ValidationSamples = [] });
            Check(
                "rebuild removes absent outputs without retaining stale catalog entries",
                owner.Targets.Count == 1
                && owner.Targets[0].Id == source.EntityId,
                $"remaining={string.Join(",", owner.Targets.Select(target => target.Id))}");

            Check(
                "catalog rebuild does not mutate the supplied snapshot or execute work",
                ReferenceEquals(sourceStateBefore, snapshot.Source)
                && artifactIdsBefore.SequenceEqual(snapshot.Artifacts.Select(artifact => artifact.Id))
                && preparationIdsBefore.SequenceEqual(snapshot.PreparationOutputs.Select(preparation => preparation.Output?.EntityId ?? string.Empty))
                && snapshot.PreparationOutputs.All(preparation => preparation.IsCurrent)
                && snapshot.PreparationOutputs.All(preparation => !preparation.IsStale),
                "snapshot=unchanged;preview/publish/run/validation=not invoked");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch
            {
                // The report remains useful if a file lock delays fixture cleanup.
            }
        }

        var success = total > 0 && passed == total;
        summary = $"RenderableC3DCatalog|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return success;
    }
}
