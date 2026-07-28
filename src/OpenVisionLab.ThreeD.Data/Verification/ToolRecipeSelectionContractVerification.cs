using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Headless verification for selection persistence, including schema 1.4
/// OrientedBox3D geometry, schema 1.5 dual-ROI role routing, and the C3D
/// source-binding boundary. It does not invoke a Viewer or inspection tool.
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
                "current schema routes a valid rectangle selection",
                ToolRecipeValidator.Validate(current).IsValid,
                string.Join(" | ", ToolRecipeValidator.Validate(current).Errors));

            var recipePath = Path.Combine(fixtureRoot, "selection.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, current);
            var reopened = ToolRecipeDocumentStore.Load(recipePath);
            Check(
                "current-schema rectangle survives save and reopen",
                reopened.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && reopened.Selections is { Count: 1 } reopenedSelections
                && reopenedSelections[0].GridRectangle == rectangle.GridRectangle
                && reopenedSelections[0].SourceBinding == binding,
                $"schema={reopened.SchemaVersion}; selections={reopened.Selections?.Count ?? 0}");

            var dualReference = rectangle with
            {
                Id = "selection.thickness.reference",
                Name = "Thickness reference",
                GridRectangle = new ToolRecipeGridRectangle(0, 0, 2, 2)
            };
            var dualMeasurement = rectangle with
            {
                Id = "selection.thickness.measurement",
                Name = "Thickness measurement",
                GridRectangle = new ToolRecipeGridRectangle(2, 2, 2, 2)
            };
            var dualRoiDocument = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Dual ROI role fixture",
                current.Source,
                [],
                [new ToolRecipeStep(
                    "step.thickness.01",
                    "thickness",
                    "Thickness",
                    3,
                    [current.Source.Id, dualReference.Id, dualMeasurement.Id],
                    "derived.thickness.01",
                    [
                        new ToolRecipeParameter("MinimumThickness", "0"),
                        new ToolRecipeParameter("MaximumThickness", "100"),
                        new ToolRecipeParameter("MinimumValidSampleCount", "1")
                    ],
                    new ToolRecipeDualRoiRouting(dualReference.Id, dualMeasurement.Id))],
                [dualReference, dualMeasurement]);
            Check(
                "schema 1.5 validates explicit ordered dual-ROI role routing",
                ToolRecipeValidator.Validate(dualRoiDocument).IsValid,
                string.Join(" | ", ToolRecipeValidator.Validate(dualRoiDocument).Errors));

            var incompleteDualRoiDocument = dualRoiDocument with
            {
                Steps =
                [
                    dualRoiDocument.Steps[0] with
                    {
                        InputEntityIds = [current.Source.Id, dualMeasurement.Id],
                        DualRoiRouting = new ToolRecipeDualRoiRouting(null, dualMeasurement.Id)
                    }
                ],
                Selections = [dualMeasurement]
            };
            Check(
                "schema 1.5 stores a missing-Reference draft without losing Measurement role",
                ToolRecipeValidator.ValidateForStorage(incompleteDualRoiDocument).IsValid
                && !ToolRecipeValidator.Validate(incompleteDualRoiDocument).IsValid,
                string.Join(" | ", ToolRecipeValidator.ValidateForStorage(incompleteDualRoiDocument).Errors));

            var incompleteDualRoiPath = Path.Combine(fixtureRoot, "dual-roi-incomplete.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(incompleteDualRoiPath, incompleteDualRoiDocument);
            var reopenedIncompleteDualRoi = ToolRecipeDocumentStore.Load(incompleteDualRoiPath);
            Check(
                "dual-ROI role routing survives incomplete save and reopen",
                reopenedIncompleteDualRoi.Steps.Single().DualRoiRouting
                    == new ToolRecipeDualRoiRouting(null, dualMeasurement.Id)
                && reopenedIncompleteDualRoi.Steps.Single().InputEntityIds
                    .SequenceEqual([current.Source.Id, dualMeasurement.Id]),
                $"route={string.Join(';', reopenedIncompleteDualRoi.Steps.Single().InputEntityIds)}");

            var oldSchemaDualRoutingValidation = ToolRecipeValidator.ValidateForStorage(
                incompleteDualRoiDocument with
                {
                    SchemaVersion = ToolRecipeDocument.OrientedBox3DSchemaVersion
                });
            Check(
                "schema 1.4 rejects schema 1.5 dual-ROI role metadata",
                !oldSchemaDualRoutingValidation.IsValid
                && oldSchemaDualRoutingValidation.Errors.Any(error =>
                    error.Contains("schema 1.5", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", oldSchemaDualRoutingValidation.Errors));

            var orientedBox = CreateOrientedBoxSelection(binding);
            var orientedBoxDocument = CreateDocument(
                ToolRecipeDocument.OrientedBox3DSchemaVersion,
                sourcePath,
                [orientedBox],
                orientedBox.Id);
            var orientedBoxValidation = ToolRecipeValidator.Validate(orientedBoxDocument);
            Check(
                "schema 1.4 accepts a finite right-handed OrientedBox3D",
                orientedBoxValidation.IsValid,
                string.Join(" | ", orientedBoxValidation.Errors));

            var orientedBoxPath = Path.Combine(fixtureRoot, "oriented-box.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(orientedBoxPath, orientedBoxDocument);
            var reopenedOrientedBox = ToolRecipeDocumentStore.Load(orientedBoxPath);
            Check(
                "OrientedBox3D center axes and half-extents survive save and reopen",
                reopenedOrientedBox.SchemaVersion == ToolRecipeDocument.OrientedBox3DSchemaVersion
                && reopenedOrientedBox.Selections is { Count: 1 } reopenedBoxes
                && reopenedBoxes[0].Kind == ToolRecipeSelectionKinds.OrientedBox3D
                && reopenedBoxes[0].OrientedBox3D == orientedBox.OrientedBox3D,
                $"schema={reopenedOrientedBox.SchemaVersion}; box={reopenedOrientedBox.Selections?[0].OrientedBox3D}");

            var oldSchemaOrientedBox = orientedBoxDocument with
            {
                SchemaVersion = ToolRecipeDocument.ArtifactOwnedSelectionSchemaVersion
            };
            var oldSchemaOrientedBoxValidation = ToolRecipeValidator.Validate(oldSchemaOrientedBox);
            Check(
                "schema 1.3 rejects the new OrientedBox3D kind",
                !oldSchemaOrientedBoxValidation.IsValid
                && oldSchemaOrientedBoxValidation.Errors.Any(error =>
                    error.Contains("schema 1.4", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", oldSchemaOrientedBoxValidation.Errors));

            var zeroAxis = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    AxisX = new ToolRecipeXyz(0, 0, 0)
                }
            };
            var zeroAxisValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [zeroAxis] });
            Check(
                "zero-length OrientedBox3D axis is rejected",
                !zeroAxisValidation.IsValid
                && zeroAxisValidation.Errors.Any(error =>
                    error.Contains("unit length", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", zeroAxisValidation.Errors));

            var nonOrthogonal = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    AxisY = new ToolRecipeXyz(1, 0, 0)
                }
            };
            var nonOrthogonalValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [nonOrthogonal] });
            Check(
                "non-orthogonal OrientedBox3D axes are rejected",
                !nonOrthogonalValidation.IsValid
                && nonOrthogonalValidation.Errors.Any(error =>
                    error.Contains("orthogonal", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", nonOrthogonalValidation.Errors));

            var leftHanded = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    AxisZ = new ToolRecipeXyz(0, 0, -1)
                }
            };
            var leftHandedValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [leftHanded] });
            Check(
                "left-handed OrientedBox3D axes are rejected",
                !leftHandedValidation.IsValid
                && leftHandedValidation.Errors.Any(error =>
                    error.Contains("right-handed", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", leftHandedValidation.Errors));

            var invalidExtent = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    HalfExtents = new ToolRecipeXyz(1, 0, 1)
                }
            };
            var invalidExtentValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [invalidExtent] });
            Check(
                "non-positive OrientedBox3D half-extent is rejected",
                !invalidExtentValidation.IsValid
                && invalidExtentValidation.Errors.Any(error =>
                    error.Contains("half-extents", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", invalidExtentValidation.Errors));

            var mixedPayload = orientedBox with
            {
                GridRectangle = new ToolRecipeGridRectangle(0, 0, 1, 1)
            };
            var mixedPayloadValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [mixedPayload] });
            Check(
                "OrientedBox3D rejects mixed rectangle payloads",
                !mixedPayloadValidation.IsValid
                && mixedPayloadValidation.Errors.Any(error =>
                    error.Contains("cannot contain", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", mixedPayloadValidation.Errors));

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

    private static ToolRecipeSelection CreateOrientedBoxSelection(
        ToolRecipeSelectionSourceBinding binding) =>
        new(
            "selection.box.01",
            "Inspection volume",
            ToolRecipeSelectionKinds.OrientedBox3D,
            "source.c3d.height-map",
            "frame.c3d-grid-index",
            binding,
            null,
            null,
            null,
            null,
            new ToolRecipeOrientedBox3D(
                new ToolRecipeXyz(1.5, 20, 1.5),
                new ToolRecipeXyz(1, 0, 0),
                new ToolRecipeXyz(0, 1, 0),
                new ToolRecipeXyz(0, 0, 1),
                new ToolRecipeXyz(1, 2, 1)));

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
