using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Headless verification for selection persistence, including schema 1.4
/// OrientedBox3D geometry, schema 1.5 dual-ROI role routing, and the C3D
/// source-binding boundary. It does not invoke a Viewer or inspection tool.
/// </summary>
public static class ToolRecipeSelectionContractVerification
{
    private static readonly string[] OrientedBoxContractCaseNames =
    [
        "schema 1.4 accepts a finite rotated right-handed OrientedBox3D",
        "current schema accepts the schema 1.4 OrientedBox3D contract",
        "rotated OrientedBox3D center axes and half-extents survive save and reopen",
        "schema 1.3 rejects the new OrientedBox3D kind",
        "zero-length OrientedBox3D axis is rejected",
        "finite non-unit OrientedBox3D axis is rejected",
        "parallel non-orthogonal OrientedBox3D axes are rejected",
        "left-handed OrientedBox3D axes are rejected",
        "non-finite OrientedBox3D center axis and half-extent are rejected",
        "non-positive OrientedBox3D half-extent is rejected",
        "OrientedBox3D rejects mixed rectangle payloads"
    ];

    private static readonly string[] GridCircleContractCaseNames =
    [
        "current schema accepts an in-bounds GridCircle",
        "GridCircle center and radius survive save and reopen",
        "schema 1.5 rejects the new GridCircle kind",
        "GridCircle requires its geometry payload",
        "GridCircle rejects a radius below one cell",
        "GridCircle rejects a non-finite radius",
        "GridCircle rejects an out-of-grid footprint",
        "GridCircle rejects mixed rectangle payloads",
        "undeclared GridCircle consumer fails closed"
    ];

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
        var orientedBoxSubsetComplete = false;
        var gridCircleSubsetComplete = false;
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

            var declaredTools = ToolRecipeSelectionContract.Declarations
                .Select(requirement => requirement.ToolId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var expectedDeclaredTools = new[]
            {
                "completeness-grid",
                "cross-section-dimensions",
                "datum-plane-raw-height-deviation",
                "gap-flush",
                "grid-circle-authoring",
                "height-difference-edge",
                "landmark-correspondence",
                "level-surface",
                "plane-flatness",
                "point-pair-dimensions",
                "roi-crop",
                "thickness",
                "three-point-plane",
                "two-point-line",
                "volume",
                "warpage"
            };
            Check(
                "selection-consuming tool inventory has one explicit compatibility matrix",
                ToolRecipeSelectionContract.Declarations.Count == 21
                && declaredTools.SequenceEqual(expectedDeclaredTools, StringComparer.Ordinal)
                && ToolRecipeSelectionContract.Declarations
                    .GroupBy(requirement => (requirement.ToolId, requirement.Role))
                    .All(group => group.Count() == 1)
                && ToolRecipeSelectionContract.Declarations.All(requirement =>
                    requirement.FirstInputIndex >= 0
                    && requirement.LastInputIndex >= requirement.FirstInputIndex
                    && requirement.MinimumCount >= 1
                    && requirement.MaximumCount >= requirement.MinimumCount)
                && declaredTools.All(toolId => Enumerable.Range(0, 24).All(inputIndex =>
                    ToolRecipeSelectionContract.Declarations.Count(requirement =>
                        requirement.ToolId == toolId
                        && requirement.AcceptsInputIndex(inputIndex)) <= 1)),
                $"rows={ToolRecipeSelectionContract.Declarations.Count};tools={string.Join(',', declaredTools)}");
            var hasThicknessReference = ToolRecipeSelectionContract.TryGetRequirement(
                "thickness",
                1,
                out var thicknessReference);
            var hasThicknessMeasurement = ToolRecipeSelectionContract.TryGetRequirement(
                "thickness",
                2,
                out var thicknessMeasurement);
            Check(
                "dual-ROI roles are declared independently",
                hasThicknessReference
                && thicknessReference.Role == ToolRecipeSelectionRoles.ReferenceRegion
                && hasThicknessMeasurement
                && thicknessMeasurement.Role == ToolRecipeSelectionRoles.MeasurementRegion
                && thicknessReference.Kind == ToolRecipeSelectionKinds.GridRectangle
                && thicknessMeasurement.Kind == ToolRecipeSelectionKinds.GridRectangle,
                $"reference={thicknessReference};measurement={thicknessMeasurement}");

            var undeclaredToolValidation = ToolRecipeValidator.Validate(
                dualRoiDocument with
                {
                    Steps =
                    [
                        dualRoiDocument.Steps[0] with
                        {
                            ToolId = "undeclared-selection-consumer",
                            ToolName = "Undeclared selection consumer",
                            MinimumInputCount = 2,
                            InputEntityIds = [current.Source.Id, dualReference.Id],
                            DualRoiRouting = null,
                            Parameters = []
                        }
                    ],
                    Selections = [dualReference]
                });
            Check(
                "undeclared tool selection route fails closed",
                !undeclaredToolValidation.IsValid
                && undeclaredToolValidation.Errors.Any(error =>
                    error.Contains("no declared selection role", StringComparison.Ordinal)),
                string.Join(" | ", undeclaredToolValidation.Errors));

            var wrongKindValidation = ToolRecipeValidator.Validate(
                dualRoiDocument with
                {
                    Steps =
                    [
                        new ToolRecipeStep(
                            "step.two-point-line.wrong-kind",
                            "two-point-line",
                            "2-Point Line",
                            2,
                            [current.Source.Id, dualReference.Id],
                            "derived.two-point-line.wrong-kind",
                            [])
                    ],
                    Selections = [dualReference]
                });
            Check(
                "declared role rejects an unsupported selection kind",
                !wrongKindValidation.IsValid
                && wrongKindValidation.Errors.Any(error =>
                    error.Contains("line-points", StringComparison.Ordinal)
                    && error.Contains(ToolRecipeSelectionKinds.PointSet, StringComparison.Ordinal)),
                string.Join(" | ", wrongKindValidation.Errors));

            var threePointSelection = CreatePointSetSelection(
                dualReference.SourceBinding,
                collinear: false);
            var wrongPointCountValidation = ToolRecipeValidator.Validate(
                dualRoiDocument with
                {
                    Steps =
                    [
                        new ToolRecipeStep(
                            "step.two-point-line.wrong-count",
                            "two-point-line",
                            "2-Point Line",
                            2,
                            [current.Source.Id, threePointSelection.Id],
                            "derived.two-point-line.wrong-count",
                            [])
                    ],
                    Selections = [threePointSelection]
                });
            Check(
                "declared PointSet role rejects the wrong point count",
                !wrongPointCountValidation.IsValid
                && wrongPointCountValidation.Errors.Any(error =>
                    error.Contains("requires exactly 2 point", StringComparison.Ordinal)),
                string.Join(" | ", wrongPointCountValidation.Errors));

            var supportedThreePointValidation = ToolRecipeValidator.Validate(
                dualRoiDocument with
                {
                    Steps =
                    [
                        new ToolRecipeStep(
                            "step.three-point-plane.supported",
                            "three-point-plane",
                            "3-Point Plane",
                            2,
                            [current.Source.Id, threePointSelection.Id],
                            "derived.three-point-plane.supported",
                            [])
                    ],
                    Selections = [threePointSelection]
                });
            Check(
                "declared PointSet role accepts its exact point count",
                supportedThreePointValidation.IsValid,
                string.Join(" | ", supportedThreePointValidation.Errors));

            var routedOrientedBox = CreateOrientedBoxSelection(
                dualReference.SourceBinding);
            var unsupportedOrientedBoxValidation = ToolRecipeValidator.Validate(
                dualRoiDocument with
                {
                    Steps =
                    [
                        new ToolRecipeStep(
                            "step.roi-crop.unsupported-box",
                            "roi-crop",
                            "ROI / Crop",
                            2,
                            [current.Source.Id, routedOrientedBox.Id],
                            "derived.roi-crop.unsupported-box",
                            [
                                new ToolRecipeParameter("ROI", "Select in Viewer"),
                                new ToolRecipeParameter("Output frame", "Keep source frame")
                            ])
                    ],
                    Selections = [routedOrientedBox]
                });
            Check(
                "existing OrientedBox3D is rejected until a tool role explicitly supports it",
                !unsupportedOrientedBoxValidation.IsValid
                && unsupportedOrientedBoxValidation.Errors.Any(error =>
                    error.Contains("role 'region' requires grid-rectangle", StringComparison.Ordinal)),
                string.Join(" | ", unsupportedOrientedBoxValidation.Errors));

            var selectionlessToolValidation = ToolRecipeValidator.Validate(
                dualRoiDocument with
                {
                    Steps =
                    [
                        new ToolRecipeStep(
                            "step.filter.selection-route",
                            "filter",
                            "Filter",
                            1,
                            [current.Source.Id, dualReference.Id],
                            "derived.filter.selection-route",
                            [
                                new ToolRecipeParameter("Method", "Median"),
                                new ToolRecipeParameter("KernelSize", "3"),
                                new ToolRecipeParameter("MissingValuePolicy", "PreserveMask"),
                                new ToolRecipeParameter("BoundaryPolicy", "AvailableNeighbors")
                            ])
                    ],
                    Selections = [dualReference]
                });
            Check(
                "selectionless tool rejects an injected selection route",
                !selectionlessToolValidation.IsValid
                && selectionlessToolValidation.Errors.Any(error =>
                    error.Contains("no declared selection role", StringComparison.Ordinal)),
                string.Join(" | ", selectionlessToolValidation.Errors));

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

            var orientedBoxPassedBefore = passed;
            var orientedBoxTotalBefore = total;
            var orientedBox = CreateOrientedBoxSelection(binding);
            var orientedBoxDocument = CreateDocument(
                ToolRecipeDocument.OrientedBox3DSchemaVersion,
                sourcePath,
                [orientedBox],
                orientedBox.Id);
            var orientedBoxValidation = ToolRecipeValidator.Validate(orientedBoxDocument);
            Check(
                "schema 1.4 accepts a finite rotated right-handed OrientedBox3D",
                orientedBoxValidation.IsValid,
                string.Join(" | ", orientedBoxValidation.Errors));

            var currentSchemaOrientedBoxValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with
                {
                    SchemaVersion = ToolRecipeDocument.CurrentSchemaVersion
                });
            Check(
                "current schema accepts the schema 1.4 OrientedBox3D contract",
                currentSchemaOrientedBoxValidation.IsValid,
                string.Join(" | ", currentSchemaOrientedBoxValidation.Errors));

            var orientedBoxPath = Path.Combine(fixtureRoot, "oriented-box.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(orientedBoxPath, orientedBoxDocument);
            var reopenedOrientedBox = ToolRecipeDocumentStore.Load(orientedBoxPath);
            Check(
                "rotated OrientedBox3D center axes and half-extents survive save and reopen",
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

            var nonUnitAxis = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    AxisX = new ToolRecipeXyz(2, 0, 0)
                }
            };
            var nonUnitAxisValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [nonUnitAxis] });
            Check(
                "finite non-unit OrientedBox3D axis is rejected",
                !nonUnitAxisValidation.IsValid
                && nonUnitAxisValidation.Errors.Any(error =>
                    error.Contains("unit length", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", nonUnitAxisValidation.Errors));

            var nonOrthogonal = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    AxisY = orientedBox.OrientedBox3D.AxisX
                }
            };
            var nonOrthogonalValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [nonOrthogonal] });
            Check(
                "parallel non-orthogonal OrientedBox3D axes are rejected",
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

            var nonFiniteGeometry = orientedBox with
            {
                OrientedBox3D = orientedBox.OrientedBox3D! with
                {
                    Center = new ToolRecipeXyz(double.NaN, 20, 1.5),
                    AxisX = new ToolRecipeXyz(double.PositiveInfinity, 0, 0),
                    HalfExtents = new ToolRecipeXyz(1, 2, double.NegativeInfinity)
                }
            };
            var nonFiniteGeometryValidation = ToolRecipeValidator.Validate(
                orientedBoxDocument with { Selections = [nonFiniteGeometry] });
            Check(
                "non-finite OrientedBox3D center axis and half-extent are rejected",
                !nonFiniteGeometryValidation.IsValid
                && nonFiniteGeometryValidation.Errors.Any(error =>
                    error.Contains("center XYZ must be finite", StringComparison.OrdinalIgnoreCase))
                && nonFiniteGeometryValidation.Errors.Any(error =>
                    error.Contains("X axis XYZ must be finite", StringComparison.OrdinalIgnoreCase))
                && nonFiniteGeometryValidation.Errors.Any(error =>
                    error.Contains("half-extents XYZ must be finite", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", nonFiniteGeometryValidation.Errors));

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

            var orientedBoxPassed = passed - orientedBoxPassedBefore;
            var orientedBoxTotal = total - orientedBoxTotalBefore;
            orientedBoxSubsetComplete =
                orientedBoxTotal == OrientedBoxContractCaseNames.Length
                && orientedBoxPassed == orientedBoxTotal
                && OrientedBoxContractCaseNames.All(caseName => lines.Any(line =>
                    line.StartsWith($"PASS | {caseName} | ", StringComparison.Ordinal)));
            lines.Add(
                $"OrientedBox3DContractVerification|{(orientedBoxSubsetComplete ? "PASS" : "FAIL")}|cases={orientedBoxTotal}|passed={orientedBoxPassed}|failed={orientedBoxTotal - orientedBoxPassed}");

            var gridCirclePassedBefore = passed;
            var gridCircleTotalBefore = total;
            var gridCircle = CreateGridCircleSelection(binding);
            var gridCircleDocument = CreateDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                sourcePath,
                [gridCircle],
                gridCircle.Id);
            var gridCircleValidation = ToolRecipeValidator.Validate(gridCircleDocument);
            Check(
                "current schema accepts an in-bounds GridCircle",
                gridCircleValidation.IsValid,
                string.Join(" | ", gridCircleValidation.Errors));

            var gridCirclePath = Path.Combine(fixtureRoot, "grid-circle.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(gridCirclePath, gridCircleDocument);
            var reopenedGridCircle = ToolRecipeDocumentStore.Load(gridCirclePath);
            Check(
                "GridCircle center and radius survive save and reopen",
                reopenedGridCircle.Selections is [var reopenedCircle]
                && reopenedCircle.Id == gridCircle.Id
                && reopenedCircle.Kind == ToolRecipeSelectionKinds.GridCircle
                && reopenedCircle.GridCircle == gridCircle.GridCircle
                && reopenedCircle.SourceBinding == binding,
                $"schema={reopenedGridCircle.SchemaVersion};circle={reopenedGridCircle.Selections?[0].GridCircle}");

            var oldSchemaGridCircleValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    SchemaVersion = ToolRecipeDocument.DualRoiRoutingSchemaVersion
                });
            Check(
                "schema 1.5 rejects the new GridCircle kind",
                !oldSchemaGridCircleValidation.IsValid
                && oldSchemaGridCircleValidation.Errors.Any(error =>
                    error.Contains("schema 1.6", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", oldSchemaGridCircleValidation.Errors));

            var missingGridCircleValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    Selections = [gridCircle with { GridCircle = null }]
                });
            Check(
                "GridCircle requires its geometry payload",
                !missingGridCircleValidation.IsValid
                && missingGridCircleValidation.Errors.Any(error =>
                    error.Contains("payload is required", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", missingGridCircleValidation.Errors));

            var smallRadiusValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    Selections = [gridCircle with { GridCircle = gridCircle.GridCircle! with { Radius = 0.5 } }]
                });
            Check(
                "GridCircle rejects a radius below one cell",
                !smallRadiusValidation.IsValid
                && smallRadiusValidation.Errors.Any(error =>
                    error.Contains("at least", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", smallRadiusValidation.Errors));

            var nonFiniteRadiusValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    Selections = [gridCircle with { GridCircle = gridCircle.GridCircle! with { Radius = double.NaN } }]
                });
            Check(
                "GridCircle rejects a non-finite radius",
                !nonFiniteRadiusValidation.IsValid
                && nonFiniteRadiusValidation.Errors.Any(error =>
                    error.Contains("finite", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", nonFiniteRadiusValidation.Errors));

            var outsideGridCircleValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    Selections = [gridCircle with { GridCircle = gridCircle.GridCircle! with { CenterRow = 3 } }]
                });
            Check(
                "GridCircle rejects an out-of-grid footprint",
                !outsideGridCircleValidation.IsValid
                && outsideGridCircleValidation.Errors.Any(error =>
                    error.Contains("inside", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", outsideGridCircleValidation.Errors));

            var mixedGridCircleValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    Selections = [gridCircle with { GridRectangle = new ToolRecipeGridRectangle(0, 0, 1, 1) }]
                });
            Check(
                "GridCircle rejects mixed rectangle payloads",
                !mixedGridCircleValidation.IsValid
                && mixedGridCircleValidation.Errors.Any(error =>
                    error.Contains("cannot contain", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", mixedGridCircleValidation.Errors));

            var undeclaredGridCircleValidation = ToolRecipeValidator.Validate(
                gridCircleDocument with
                {
                    Steps =
                    [
                        gridCircleDocument.Steps[0] with
                        {
                            ToolId = "roi-crop",
                            ToolName = "ROI / Crop"
                        }
                    ]
                });
            Check(
                "undeclared GridCircle consumer fails closed",
                !undeclaredGridCircleValidation.IsValid
                && undeclaredGridCircleValidation.Errors.Any(error =>
                    error.Contains("requires grid-rectangle", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", undeclaredGridCircleValidation.Errors));

            var gridCirclePassed = passed - gridCirclePassedBefore;
            var gridCircleTotal = total - gridCircleTotalBefore;
            gridCircleSubsetComplete =
                gridCircleTotal == GridCircleContractCaseNames.Length
                && gridCirclePassed == gridCircleTotal
                && GridCircleContractCaseNames.All(caseName => lines.Any(line =>
                    line.StartsWith($"PASS | {caseName} | ", StringComparison.Ordinal)));
            lines.Add(
                $"GridCircleContractVerification|{(gridCircleSubsetComplete ? "PASS" : "FAIL")}|cases={gridCircleTotal}|passed={gridCirclePassed}|failed={gridCircleTotal - gridCirclePassed}");

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
            && orientedBoxSubsetComplete
            && gridCircleSubsetComplete
            && !lines.Any(line => line.StartsWith("FAIL |", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Tool Recipe selection contract verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static ToolRecipeDocument CreateDocument(
        string schemaVersion,
        string sourcePath,
        IReadOnlyList<ToolRecipeSelection>? selections,
        string inputEntityId)
    {
        var routedSelection = selections?.FirstOrDefault(selection =>
            string.Equals(selection.Id, inputEntityId, StringComparison.OrdinalIgnoreCase));
        var sourceId = "source.c3d.height-map";
        var toolId = routedSelection?.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => "roi-crop",
            ToolRecipeSelectionKinds.PointSet when routedSelection.Points?.Count == 2 => "two-point-line",
            ToolRecipeSelectionKinds.PointSet => "three-point-plane",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => "landmark-correspondence",
            ToolRecipeSelectionKinds.GridCircle => "grid-circle-authoring",
            _ => "selection-fixture"
        };
        var stepInputs = toolId switch
        {
            "landmark-correspondence" => new[] { inputEntityId },
            "selection-fixture" => new[] { sourceId },
            _ => new[] { sourceId, inputEntityId }
        };
        var parameters = toolId == "roi-crop"
            ? new[]
            {
                new ToolRecipeParameter("ROI", "Select in Viewer"),
                new ToolRecipeParameter("Output frame", "Keep source frame")
            }
            : [];
        var file = new FileInfo(sourcePath);
        var binding = routedSelection?.SourceBinding;
        return new(
            schemaVersion,
            "Selection contract fixture",
            new ToolRecipeSource(
                sourceId,
                "Selection source",
                "C3D",
                "raw-height",
                "frame.c3d-grid-index",
                sourcePath,
                file.Exists ? file.Length : null,
                binding?.ContentSha256,
                binding?.GridWidth,
                binding?.GridHeight),
            [],
            [new ToolRecipeStep(
                "step.fixture.01",
                toolId,
                toolId,
                stepInputs.Length,
                stepInputs,
                "derived.fixture.01",
                parameters)],
            selections);
    }

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
        ToolRecipeSelectionSourceBinding binding)
    {
        var diagonal = Math.Sqrt(0.5);
        return new(
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
                new ToolRecipeXyz(diagonal, diagonal, 0),
                new ToolRecipeXyz(-diagonal, diagonal, 0),
                new ToolRecipeXyz(0, 0, 1),
                new ToolRecipeXyz(1, 2, 1)));
    }

    private static ToolRecipeSelection CreateGridCircleSelection(
        ToolRecipeSelectionSourceBinding binding) =>
        new(
            "selection.circle.01",
            "Circular inspection region",
            ToolRecipeSelectionKinds.GridCircle,
            "source.c3d.height-map",
            "frame.c3d-grid-index",
            binding,
            null,
            null,
            null,
            GridCircle: new ToolRecipeGridCircle(1, 1, 1));

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
