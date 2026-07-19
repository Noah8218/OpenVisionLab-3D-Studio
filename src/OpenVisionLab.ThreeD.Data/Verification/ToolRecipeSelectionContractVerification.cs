using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Headless verification for the schema 1.0/1.1/1.2 selection persistence and
/// C3D source-binding boundary. It does not invoke a Viewer or inspection tool.
/// </summary>
public static class ToolRecipeSelectionContractVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Tool Recipe selection contract verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "ToolRecipeSelectionContractVerification",
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "selection-source.C3D");
            WriteC3D(sourcePath, 4, 4, 10.0f);
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            Check(
                "identity reads exact C3D grid and SHA-256",
                binding.GridWidth == 4
                && binding.GridHeight == 4
                && binding.ContentSha256.Length == 64,
                $"grid={binding.GridWidth}x{binding.GridHeight}; sha256={binding.ContentSha256}");

            var legacy = CreateDocument(
                ToolRecipeDocument.LegacySchemaVersion,
                sourcePath,
                null,
                "source.c3d.height-map");
            Check(
                "schema 1.0 without selections remains valid",
                ToolRecipeValidator.Validate(legacy).IsValid,
                "legacy selections are absent");

            var invalidLegacy = legacy with
            {
                Selections = [CreateRectangleSelection(binding)]
            };
            Check(
                "schema 1.0 rejects structured selections",
                !ToolRecipeValidator.Validate(invalidLegacy).IsValid,
                string.Join(" | ", ToolRecipeValidator.Validate(invalidLegacy).Errors));

            var rectangle = CreateRectangleSelection(binding);
            var current = CreateDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                sourcePath,
                [rectangle],
                rectangle.Id);
            Check(
                "schema 1.2 routes a valid rectangle selection",
                ToolRecipeValidator.Validate(current).IsValid,
                string.Join(" | ", ToolRecipeValidator.Validate(current).Errors));

            var recipePath = Path.Combine(fixtureRoot, "selection.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, current);
            var reopened = ToolRecipeDocumentStore.Load(recipePath);
            Check(
                "schema 1.2 selection survives save and reopen",
                reopened.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && reopened.Selections is { Count: 1 } reopenedSelections
                && reopenedSelections[0].GridRectangle == rectangle.GridRectangle
                && reopenedSelections[0].SourceBinding == binding,
                $"schema={reopened.SchemaVersion}; selections={reopened.Selections?.Count ?? 0}");

            var outOfBounds = rectangle with
            {
                GridRectangle = new ToolRecipeGridRectangle(3, 3, 2, 2)
            };
            var outOfBoundsValidation = ToolRecipeValidator.Validate(current with { Selections = [outOfBounds] });
            Check(
                "rectangle outside recorded grid is rejected",
                !outOfBoundsValidation.IsValid
                && outOfBoundsValidation.Errors.Any(error => error.Contains("outside", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", outOfBoundsValidation.Errors));

            var pointSet = CreatePointSetSelection(binding, collinear: false);
            var pointDocument = CreateDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                sourcePath,
                [pointSet],
                pointSet.Id);
            Check(
                "three distinct non-collinear C3D points validate",
                ToolRecipeValidator.Validate(pointDocument).IsValid,
                string.Join(" | ", ToolRecipeValidator.Validate(pointDocument).Errors));

            var collinear = CreatePointSetSelection(binding, collinear: true);
            var collinearValidation = ToolRecipeValidator.Validate(pointDocument with { Selections = [collinear] });
            Check(
                "three collinear captured positions are rejected",
                !collinearValidation.IsValid
                && collinearValidation.Errors.Any(error => error.Contains("collinear", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", collinearValidation.Errors));

            var correspondence = new ToolRecipeSelection(
                "selection.correspondence.01",
                "Fixture correspondence",
                ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
                "source.c3d.height-map",
                "frame.c3d-grid-index",
                binding,
                null,
                null,
                [new ToolRecipeLandmarkCorrespondence(
                    "source.c3d.height-map",
                    "fixture.origin",
                    new ToolRecipeXyz(0, 0, 0),
                    "frame.fixture")]);
            var correspondenceValidation = ToolRecipeValidator.Validate(CreateDocument(
                ToolRecipeDocument.SelectionSchemaVersion,
                sourcePath,
                [correspondence],
                correspondence.Id));
            Check(
                "correspondence below four rows remains a warning",
                correspondenceValidation.IsValid
                && correspondenceValidation.Warnings.Any(warning => warning.Contains("four", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", correspondenceValidation.Warnings));

            var forwardCorrespondence = correspondence with
            {
                Rows =
                [
                    correspondence.Rows![0] with
                    {
                        SourceEntityId = "derived.late.01"
                    }
                ]
            };
            var forwardReferenceDocument = CreateDocument(
                ToolRecipeDocument.SelectionSchemaVersion,
                sourcePath,
                [forwardCorrespondence],
                forwardCorrespondence.Id) with
            {
                Steps =
                [
                    new ToolRecipeStep(
                        "step.consume.01",
                        "consume-correspondence",
                        "Consume Correspondence",
                        1,
                        [forwardCorrespondence.Id],
                        "derived.consumed.01",
                        []),
                    new ToolRecipeStep(
                        "step.produce-late.01",
                        "produce-late",
                        "Produce Late",
                        1,
                        ["source.c3d.height-map"],
                        "derived.late.01",
                        [])
                ]
            };
            var forwardReferenceValidation = ToolRecipeValidator.Validate(forwardReferenceDocument);
            Check(
                "correspondence cannot consume a later step output",
                !forwardReferenceValidation.IsValid
                && forwardReferenceValidation.Errors.Any(error => error.Contains("produced before", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", forwardReferenceValidation.Errors));

            var duplicateRows = correspondence with
            {
                Rows =
                [
                    correspondence.Rows![0],
                    correspondence.Rows[0] with { ReferenceLandmarkId = "fixture.second" }
                ]
            };
            var duplicateValidation = ToolRecipeValidator.Validate(CreateDocument(
                ToolRecipeDocument.SelectionSchemaVersion,
                sourcePath,
                [duplicateRows],
                duplicateRows.Id));
            Check(
                "duplicate correspondence source entity is rejected",
                !duplicateValidation.IsValid
                && duplicateValidation.Errors.Any(error => error.Contains("repeats correspondence source", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", duplicateValidation.Errors));

            var duplicateGlobalId = current with
            {
                Steps = [current.Steps[0] with { Id = rectangle.Id }]
            };
            var duplicateGlobalValidation = ToolRecipeValidator.Validate(duplicateGlobalId);
            Check(
                "selection and step IDs share one global uniqueness domain",
                !duplicateGlobalValidation.IsValid
                && duplicateGlobalValidation.Errors.Any(error => error.Contains("duplicated", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", duplicateGlobalValidation.Errors));

            var wrongFrame = rectangle with { FrameId = "frame.other" };
            var wrongFrameValidation = ToolRecipeValidator.Validate(current with { Selections = [wrongFrame] });
            Check(
                "selection source frame mismatch is rejected",
                !wrongFrameValidation.IsValid
                && wrongFrameValidation.Errors.Any(error => error.Contains("does not match source frame", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", wrongFrameValidation.Errors));

            var legacyFixturePath = Path.Combine(fixtureRoot, "legacy-placeholder.C3D");
            File.WriteAllBytes(legacyFixturePath, [0x43, 0x33, 0x44, 0x00]);
            var legacyRecipePath = Path.Combine(fixtureRoot, "legacy.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(
                legacyRecipePath,
                CreateDocument(ToolRecipeDocument.LegacySchemaVersion, legacyFixturePath, null, "source.c3d.height-map"));
            var legacyJson = File.ReadAllText(legacyRecipePath);
            Check(
                "selectionless legacy save skips C3D binding and omits null selections",
                File.Exists(legacyRecipePath)
                && !legacyJson.Contains("\"selections\"", StringComparison.Ordinal),
                $"bytes={new FileInfo(legacyRecipePath).Length}");

            WriteC3D(sourcePath, 4, 4, 100.0f);
            var stale = ToolRecipeSelectionSourceBindingVerifier.Verify(sourcePath, binding);
            Check(
                "same-path source replacement is detected as stale",
                !stale.IsCurrent
                && stale.CurrentBinding is not null
                && !stale.CurrentBinding.ContentSha256.Equals(binding.ContentSha256, StringComparison.OrdinalIgnoreCase),
                stale.Message);

            var structurallyReopened = ToolRecipeDocumentStore.Load(recipePath);
            Check(
                "stale selection recipe still opens for recapture",
                structurallyReopened.Selections?.Count == 1,
                $"selections={structurallyReopened.Selections?.Count ?? 0}");

            var staleSaveRejected = false;
            try
            {
                ToolRecipeDocumentStore.Save(Path.Combine(fixtureRoot, "stale-save.ov3d-teach.json"), structurallyReopened);
            }
            catch (InvalidDataException)
            {
                staleSaveRejected = true;
            }

            Check(
                "stale selection binding blocks save",
                staleSaveRejected,
                "save must fail closed after source byte replacement");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
            catch (IOException exception)
            {
                lines.Add($"FAIL | fixture cleanup | {exception.Message}");
            }
        }

        var reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Tool Recipe selection contract verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static ToolRecipeDocument CreateDocument(
        string schemaVersion,
        string sourcePath,
        IReadOnlyList<ToolRecipeSelection>? selections,
        string inputEntityId) =>
        new(
            schemaVersion,
            "Selection contract fixture",
            new ToolRecipeSource(
                "source.c3d.height-map",
                "Selection source",
                "C3D",
                "raw-height",
                "frame.c3d-grid-index",
                sourcePath),
            [],
            [new ToolRecipeStep(
                "step.fixture.01",
                "fixture-tool",
                "Fixture Tool",
                1,
                [inputEntityId],
                "derived.fixture.01",
                [])],
            selections);

    private static ToolRecipeSelection CreateRectangleSelection(ToolRecipeSelectionSourceBinding binding) =>
        new(
            "selection.roi.01",
            "Inspection ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            "source.c3d.height-map",
            "frame.c3d-grid-index",
            binding,
            new ToolRecipeGridRectangle(1, 1, 2, 2),
            null,
            null);

    private static ToolRecipeSelection CreatePointSetSelection(
        ToolRecipeSelectionSourceBinding binding,
        bool collinear) =>
        new(
            "selection.points.01",
            "Datum points",
            ToolRecipeSelectionKinds.PointSet,
            "source.c3d.height-map",
            "frame.c3d-grid-index",
            binding,
            null,
            [
                new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", 0, 0), new ToolRecipeXyz(0, 0, 0), 10),
                new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", 0, 1), new ToolRecipeXyz(1, 0, 0), 11),
                new ToolRecipeSelectionPoint(
                    new ToolRecipeGridCellLocator("grid-cell", 1, 0),
                    collinear ? new ToolRecipeXyz(2, 0, 0) : new ToolRecipeXyz(0, 1, 0),
                    12)
            ],
            null);

    private static void WriteC3D(string path, int width, int height, float offset)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(width);
        writer.Write(height);
        for (var index = 0; index < checked(width * height); index++)
        {
            writer.Write(offset + index);
        }
    }
}
