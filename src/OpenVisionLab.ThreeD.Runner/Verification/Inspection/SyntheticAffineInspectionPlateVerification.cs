using System.Globalization;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SyntheticAffineInspectionPlateVerification
{
    private const int Width = 240;
    private const int Height = 160;
    private const string SourceId = "source.synthetic-affine-plate.v1";
    private const string SourceFileName = "source-affine-inspection-plate-v1.C3D";
    private const string SourceUnit = "raw-height";
    private const string SourceFrame = "frame.c3d-grid-index";
    private const string ReferenceFrame = "frame.synthetic-fixture.v1";
    private const string ReferenceUnit = "synthetic-unit";
    private const string ReferenceProvenance = "OpenVisionLab Synthetic Affine Inspection Plate v1";
    private const string ReferenceRevision = "V1";
    private const string FilterOutputId = "derived.filtered-height.v1";
    private const string CorrespondenceSelectionId = "selection.landmark-correspondence.v1";
    private const string CorrespondenceOutputId = "derived.landmark-correspondence.v1";
    private const string AffineOutputId = "derived.xyz-affine.v1";
    private const string CloudOutputId = "derived.transformed-point-cloud.v1";
    private const string HeightFieldOutputId = "derived.transformed-height-field.v1";
    private const double PitchU = 1.25;
    private const double PitchV = 0.8;
    private const double HeightScale = 0.5;

    private static readonly Vec3 Origin = new(100, -25, 50);
    private static readonly (Vec3 U, Vec3 V, Vec3 H) Axes = CreateAxes();
    private static readonly ToolRecipeGridRectangle ThicknessRoi = new(50, 60, 20, 40);
    private static readonly ToolRecipeGridRectangle WarpageRoi = new(82, 100, 40, 60);
    private static readonly LandmarkDefinition[] Landmarks =
    [
        new("upper-left", "UpperLeftAnchor", 20, 20, 20),
        new("upper-right", "UpperRightAnchor", 185, 20, 35),
        new("lower-left", "LowerLeftAnchor", 20, 115, 50),
        new("lower-right", "LowerRightAnchor", 185, 115, 85)
    ];
    private static readonly (int Row, int Column, double Height)[] Impulses =
    [
        (10, 100, 95), (35, 150, 75), (75, 30, 70), (140, 130, 90)
    ];

    public static int Run(string packageDirectory, string reportPath)
    {
        var checks = new List<CheckResult>();
        try
        {
            var package = Path.GetFullPath(packageDirectory);
            Directory.CreateDirectory(package);
            var c3dPath = Path.Combine(package, SourceFileName);
            var seed = C3DHeightFieldSnapshot.CreateForVerification(SourceId, Width, Height, CreateSourceValues());
            seed.SaveC3D(c3dPath);
            var source = C3DHeightFieldSnapshot.LoadVerified(
                c3dPath, SourceId, SourceUnit, SourceFrame,
                seed.ByteLength, seed.ContentSha256, Width, Height);

            var baseDocument = CreateDocument(source, includeMeasurements: false, null, null);
            var baseValidation = ToolRecipeValidator.Validate(baseDocument);
            checks.Add(Check("schema-1.3 generic ordered recipe validates", baseValidation.IsValid,
                string.Join(" / ", baseValidation.Errors)));

            var filter = ToolRecipeFilterExecution.Execute(baseDocument, "step.filter", package);
            RequirePass(filter.Result, filter.Output, "Filter");
            var filtered = filter.Output!;
            checks.Add(Check("median filter preserves the missing mask",
                filtered.MissingCount == source.MissingCount,
                $"sourceMissing={source.MissingCount};filteredMissing={filtered.MissingCount}"));
            checks.Add(Check("median filter removes four deterministic impulses",
                Impulses.All(item => Nearly(filtered.Values.Span[item.Row * Width + item.Column], 10, 1e-12)),
                string.Join(",", Impulses.Select(item =>
                    $"{item.Row}:{item.Column}={filtered.Values.Span[item.Row * Width + item.Column]:G17}"))));

            var corners = new List<C3DLineIntersectionFeature>();
            foreach (var landmark in Landmarks)
            {
                var horizontalEdge = ToolRecipeHeightDifferenceEdgeExecution.Execute(
                    baseDocument, EdgeAcrossColumnsStepId(landmark), filtered);
                RequirePass(horizontalEdge.Result, horizontalEdge.Output, $"{landmark.Name} AcrossColumns Edge");
                var horizontalLine = ToolRecipeLineFitExecution.Execute(
                    baseDocument, LineAcrossColumnsStepId(landmark), horizontalEdge.Output!);
                RequirePass(horizontalLine.Result, horizontalLine.Output, $"{landmark.Name} AcrossColumns Line");

                var verticalEdge = ToolRecipeHeightDifferenceEdgeExecution.Execute(
                    baseDocument, EdgeAcrossRowsStepId(landmark), filtered);
                RequirePass(verticalEdge.Result, verticalEdge.Output, $"{landmark.Name} AcrossRows Edge");
                var verticalLine = ToolRecipeLineFitExecution.Execute(
                    baseDocument, LineAcrossRowsStepId(landmark), verticalEdge.Output!);
                RequirePass(verticalLine.Result, verticalLine.Output, $"{landmark.Name} AcrossRows Line");

                var intersection = ToolRecipeLineIntersectionExecution.Execute(
                    baseDocument, IntersectionStepId(landmark), horizontalLine.Output!, verticalLine.Output!);
                RequirePass(intersection.Result, intersection.Output, $"{landmark.Name} Intersection");
                corners.Add(intersection.Output!);

                var expected = ExpectedSourceAnchor(landmark);
                var actual = intersection.Output!;
                var error = Distance(expected, new Vec3(actual.CornerAnchorX, actual.CornerAnchorY, actual.CornerAnchorZ));
                checks.Add(Check($"{landmark.Name} edge-line-intersection anchor", error <= 1e-9,
                    $"expected={Format(expected)};actual={actual.CornerAnchorX:G17},{actual.CornerAnchorY:G17},{actual.CornerAnchorZ:G17};error={error:G17}"));
            }

            var correspondence = ToolRecipeLandmarkCorrespondenceExecution.Execute(
                baseDocument, "step.landmark-correspondence", corners);
            RequirePass(correspondence.Result, correspondence.Output, "Landmark Correspondence");
            checks.Add(Check("four source and reference anchors are affine-independent",
                correspondence.Output!.SourceRank == 4 && correspondence.Output.ReferenceRank == 4,
                $"sourceRank={correspondence.Output.SourceRank};referenceRank={correspondence.Output.ReferenceRank};sourceVolume={correspondence.Output.SourceNormalizedTetrahedronVolume:G17};referenceVolume={correspondence.Output.ReferenceNormalizedTetrahedronVolume:G17}"));

            var affine = ToolRecipeXYZAffineSolveExecution.Execute(
                baseDocument, "step.xyz-affine", correspondence.Output!);
            RequirePass(affine.Result, affine.Output, "XYZ Affine Solve");
            var intendedMatrix = CreateExpectedMatrix();
            var matrixError = MaximumAbsoluteDifference(intendedMatrix.Values, affine.Output!.Matrix.Values);
            checks.Add(Check("A1 recovers the intended full-XYZ affine", matrixError <= 1e-9,
                $"maximumMatrixError={matrixError:G17};condition={affine.Output.ConditionEstimate:G17};residual={affine.Output.ArithmeticMaximumResidual:G17}"));

            var applied = ToolRecipeXYZAffineApplyExecution.Execute(
                baseDocument, "step.xyz-affine-apply", affine.Output!, package);
            RequirePass(applied.Result, applied.Output, "Apply XYZ Affine");
            var pointError = MaximumPointError(applied.Output!);
            checks.Add(Check("A2 transforms every finite C3D point in source locator order", pointError <= 1e-8,
                $"finite={applied.Output!.FinitePointCount};missing={applied.Output.MissingPointCount};maximumPointError={pointError:G17}"));

            var regrid = ToolRecipeRegridHeightFieldExecution.Execute(
                baseDocument, "step.regrid", applied.Output!);
            RequirePass(regrid.Result, regrid.Output, "Re-grid Height Map");
            var field = regrid.Output!;
            var expectedCoverage = (double)source.ValidCount / (Width * Height);
            var heightError = MaximumHeightFieldError(source, field);
            checks.Add(Check("A3 preserves dimensions, holes, and exact source locators",
                field.RowCount == Height && field.ColumnCount == Width
                && field.PopulatedCellCount == source.ValidCount
                && field.MissingCellCount == source.MissingCount
                && field.CollisionCount == 0
                && Nearly(field.CoverageRatio, expectedCoverage, 1e-12)
                && heightError <= 1e-8,
                $"grid={field.ColumnCount}x{field.RowCount};populated={field.PopulatedCellCount};missing={field.MissingCellCount};coverage={field.CoverageRatio:G17};collisions={field.CollisionCount};maximumHeightError={heightError:G17}"));

            var transformedBinding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(field);
            var finalDocument = CreateDocument(source, includeMeasurements: true, transformedBinding, field);
            var finalValidation = ToolRecipeValidator.Validate(finalDocument);
            checks.Add(Check("artifact-owned Thickness and Warpage routes validate",
                finalValidation.IsValid, string.Join(" / ", finalValidation.Errors)));

            var ordered = ToolRecipeTransformedHeightFieldMeasurementSequence.ExecuteOrdered(
                finalDocument, "step.regrid", applied.Output!);
            RequirePass(ordered.Result, ordered.Output, "Ordered A3 measurement sequence");
            checks.Add(Check("ordered Runner executes both measurements in authored order",
                ordered.Output!.Measurements.Select(item => item.StepId)
                    .SequenceEqual(["step.thickness", "step.warpage"]),
                string.Join(",", ordered.Output.Measurements.Select(item => $"{item.RecipeIndex}:{item.StepId}"))));

            var thicknessExpected = CalculateThickness(field, ThicknessRoi);
            var thicknessActual = ordered.Output.Measurements[0].Output.Result;
            var thicknessError = MaximumMetricError(thicknessActual,
                ("Mean", thicknessExpected.Mean), ("Minimum", thicknessExpected.Minimum),
                ("Maximum", thicknessExpected.Maximum), ("Range", thicknessExpected.Range),
                ("ValidSampleCount", thicknessExpected.Count));
            checks.Add(Check("Thickness ROI matches independent min/max/mean/range truth",
                thicknessActual.Status == ResultStatus.Pass && thicknessError <= 1e-10,
                $"mean={thicknessExpected.Mean:G17};min={thicknessExpected.Minimum:G17};max={thicknessExpected.Maximum:G17};range={thicknessExpected.Range:G17};count={thicknessExpected.Count};maximumMetricError={thicknessError:G17}"));

            var warpageExpected = CalculateWarpage(field, WarpageRoi);
            var warpageActual = ordered.Output.Measurements[1].Output.Result;
            var warpageError = MaximumMetricError(warpageActual,
                ("PeakToValley", warpageExpected.PeakToValley), ("Rms", warpageExpected.Rms),
                ("MinimumResidual", warpageExpected.MinimumResidual), ("MaximumResidual", warpageExpected.MaximumResidual),
                ("PlaneSlopeX", warpageExpected.SlopeX), ("PlaneSlopeY", warpageExpected.SlopeY),
                ("PlaneIntercept", warpageExpected.Intercept), ("ValidSampleCount", warpageExpected.Count));
            checks.Add(Check("Warpage ROI matches independent best-fit-plane truth",
                warpageActual.Status == ResultStatus.Pass && warpageError <= 1e-9,
                $"p2v={warpageExpected.PeakToValley:G17};rms={warpageExpected.Rms:G17};min={warpageExpected.MinimumResidual:G17};max={warpageExpected.MaximumResidual:G17};count={warpageExpected.Count};maximumMetricError={warpageError:G17}"));

            var recipePath = Path.Combine(package, "inspection-recipe.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, finalDocument);
            var reopened = ToolRecipeDocumentStore.Load(recipePath);
            checks.Add(Check("recipe save/reopen preserves schema and ordered tool graph",
                reopened.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && reopened.Steps.Select(step => step.Id).SequenceEqual(finalDocument.Steps.Select(step => step.Id)),
                $"schema={reopened.SchemaVersion};steps={reopened.Steps.Count};selections={reopened.Selections?.Count}"));

            WriteGroundTruth(
                package, source, corners, affine.Output, field,
                thicknessExpected, warpageExpected, checks);
            WriteReadme(package);
        }
        catch (Exception exception)
        {
            checks.Add(Check("unexpected exception", false, $"{exception.GetType().Name}: {exception.Message}"));
        }

        var passed = checks.Count(check => check.Passed);
        var success = checks.Count > 0 && passed == checks.Count;
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath,
        [
            $"SyntheticAffineInspectionPlateVerification|{(success ? "Pass" : "Fail")}|checks={checks.Count}|passed={passed}|failed={checks.Count - passed}",
            $"Package|{Path.GetFullPath(packageDirectory)}",
            "Boundary|synthetic display-frame golden only; no physical calibration, sensor fidelity, Gauge R&R, or metrology claim",
            .. checks.Select(check => $"{(check.Passed ? "PASS" : "FAIL")}|{check.Name}|{Clean(check.Evidence)}")
        ]);
        Console.WriteLine($"Synthetic Affine Inspection Plate verification: {(success ? "Pass" : "Fail")} ({passed}/{checks.Count})");
        return success ? 0 : 5;
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        bool includeMeasurements,
        ToolRecipeSelectionSourceBinding? transformedBinding,
        C3DTransformedHeightField? field)
    {
        var sourceBinding = new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, Width, Height);
        var selections = new List<ToolRecipeSelection>();
        var steps = new List<ToolRecipeStep>
        {
            new("step.filter", "filter", "Median Filter", 1, [SourceId], FilterOutputId, FilterParameters())
        };

        foreach (var landmark in Landmarks)
        {
            var acrossColumnsSelection = new ToolRecipeSelection(
                EdgeAcrossColumnsSelectionId(landmark), $"{landmark.Name} column edge band",
                ToolRecipeSelectionKinds.GridRectangle, SourceId, SourceFrame, sourceBinding,
                new ToolRecipeGridRectangle(landmark.Row + 4, landmark.Column - 2, 18, 5), null, null);
            var acrossRowsSelection = new ToolRecipeSelection(
                EdgeAcrossRowsSelectionId(landmark), $"{landmark.Name} row edge band",
                ToolRecipeSelectionKinds.GridRectangle, SourceId, SourceFrame, sourceBinding,
                new ToolRecipeGridRectangle(landmark.Row - 2, landmark.Column + 4, 5, 18), null, null);
            selections.Add(acrossColumnsSelection);
            selections.Add(acrossRowsSelection);

            steps.Add(new ToolRecipeStep(
                EdgeAcrossColumnsStepId(landmark), "height-difference-edge", "Height Difference Edge", 2,
                [FilterOutputId, acrossColumnsSelection.Id], EdgeAcrossColumnsOutputId(landmark), EdgeParameters("AcrossColumns")));
            steps.Add(new ToolRecipeStep(
                LineAcrossColumnsStepId(landmark), "three-d-line-fit", "3D Line Fit", 1,
                [EdgeAcrossColumnsOutputId(landmark)], LineAcrossColumnsOutputId(landmark), LineParameters()));
            steps.Add(new ToolRecipeStep(
                EdgeAcrossRowsStepId(landmark), "height-difference-edge", "Height Difference Edge", 2,
                [FilterOutputId, acrossRowsSelection.Id], EdgeAcrossRowsOutputId(landmark), EdgeParameters("AcrossRows")));
            steps.Add(new ToolRecipeStep(
                LineAcrossRowsStepId(landmark), "three-d-line-fit", "3D Line Fit", 1,
                [EdgeAcrossRowsOutputId(landmark)], LineAcrossRowsOutputId(landmark), LineParameters()));
            steps.Add(new ToolRecipeStep(
                IntersectionStepId(landmark), "line-intersection", "Line Intersection", 2,
                [LineAcrossColumnsOutputId(landmark), LineAcrossRowsOutputId(landmark)],
                IntersectionOutputId(landmark), IntersectionParameters(landmark.Role)));
        }

        var correspondenceRows = Landmarks.Select(landmark =>
        {
            var reference = ApplyExpectedAffine(ExpectedSourceAnchor(landmark));
            return new ToolRecipeLandmarkCorrespondence(
                IntersectionOutputId(landmark), $"reference.{landmark.Id}",
                new ToolRecipeXyz(reference.X, reference.Y, reference.Z), ReferenceFrame);
        }).ToArray();
        selections.Add(new ToolRecipeSelection(
            CorrespondenceSelectionId, "Four synthetic CornerAnchor correspondences",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, SourceId, SourceFrame, sourceBinding,
            null, null, correspondenceRows,
            new ToolRecipeLandmarkCorrespondenceDescriptor(
                ReferenceFrame, ReferenceUnit, ReferenceProvenance, ReferenceRevision,
                "ExactlyFour", "CurrentPublishedCornerAnchor", "RequireNonDegenerateTetrahedra", 1e-6)));

        steps.Add(new ToolRecipeStep(
            "step.landmark-correspondence", "landmark-correspondence", "Landmark Correspondence", 1,
            [CorrespondenceSelectionId], CorrespondenceOutputId, CorrespondenceParameters()));
        steps.Add(new ToolRecipeStep(
            "step.xyz-affine", "xyz-affine-solve", "XYZ Affine Solve", 1,
            [CorrespondenceOutputId], AffineOutputId, AffineParameters()));
        steps.Add(new ToolRecipeStep(
            "step.xyz-affine-apply", "xyz-affine-apply", "Apply XYZ Affine", 2,
            [SourceId, AffineOutputId], CloudOutputId, []));
        steps.Add(new ToolRecipeStep(
            "step.regrid", "re-grid-height-map", "Re-grid Height Map", 1,
            [CloudOutputId], HeightFieldOutputId, CreateReferenceGridProfile().ToRecipeParameters()));

        if (includeMeasurements)
        {
            ArgumentNullException.ThrowIfNull(transformedBinding);
            ArgumentNullException.ThrowIfNull(field);
            var thicknessSelection = new ToolRecipeSelection(
                "selection.thickness-roi", "Known thickness statistics ROI",
                ToolRecipeSelectionKinds.GridRectangle, SourceId, ReferenceFrame, transformedBinding,
                ThicknessRoi, null, null);
            var warpageSelection = new ToolRecipeSelection(
                "selection.warpage-roi", "Known dome and depression warpage ROI",
                ToolRecipeSelectionKinds.GridRectangle, SourceId, ReferenceFrame, transformedBinding,
                WarpageRoi, null, null);
            selections.Add(thicknessSelection);
            selections.Add(warpageSelection);
            steps.Add(new ToolRecipeStep(
                "step.thickness", "thickness", "Thickness", 2,
                [HeightFieldOutputId, thicknessSelection.Id], "result.thickness",
                [new("MinimumThickness", "19.4"), new("MaximumThickness", "20.6"), new("MinimumValidSampleCount", "800")]));
            steps.Add(new ToolRecipeStep(
                "step.warpage", "warpage", "Warpage", 2,
                [HeightFieldOutputId, warpageSelection.Id], "result.warpage",
                [new("MaximumPeakToValley", "5"), new("MaximumRms", "2"), new("MinimumValidSampleCount", "2400")]));
        }

        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Synthetic Multi-tool 3D Inspection Recipe - Affine Plate v1",
            new ToolRecipeSource(
                SourceId, "Synthetic Affine Inspection Plate v1", "C3D", SourceUnit, SourceFrame,
                SourceFileName, source.ByteLength, source.ContentSha256, Width, Height),
            [new("reference.synthetic-fixture.v1", "Synthetic fixture landmark set", "Reference landmark set")],
            steps, selections);
    }

    private static double[] CreateSourceValues()
    {
        var values = Enumerable.Repeat(10d, Width * Height).ToArray();
        foreach (var landmark in Landmarks)
        {
            Fill(values, new ToolRecipeGridRectangle(landmark.Row, landmark.Column, 26, 26), (_, _) => landmark.Height);
        }

        Fill(values, ThicknessRoi, (_, column) =>
        {
            var localColumn = column - ThicknessRoi.Column;
            return localColumn < 10 ? 39 : localColumn < 30 ? 40 : 41;
        });
        Fill(values, WarpageRoi, (row, column) =>
        {
            var u = (column - (WarpageRoi.Column + (WarpageRoi.ColumnCount - 1) / 2.0)) / ((WarpageRoi.ColumnCount - 1) / 2.0);
            var v = (row - (WarpageRoi.Row + (WarpageRoi.RowCount - 1) / 2.0)) / ((WarpageRoi.RowCount - 1) / 2.0);
            var dome = 4.0 * Math.Exp(-4.0 * (u * u + v * v));
            var depression = 2.5 * Math.Exp(-30.0 * ((u - 0.45) * (u - 0.45) + (v + 0.20) * (v + 0.20)));
            return 30.0 + dome - depression;
        });

        foreach (var impulse in Impulses) values[impulse.Row * Width + impulse.Column] = impulse.Height;
        for (var row = 2; row < Height; row += 23)
        {
            for (var column = 3; column < Width; column += 37)
            {
                if (!IsProtected(row, column)) values[row * Width + column] = double.NaN;
            }
        }
        Fill(values, new ToolRecipeGridRectangle(73, 48, 3, 5), (_, _) => double.NaN);
        return values;
    }

    private static bool IsProtected(int row, int column) =>
        Contains(ThicknessRoi, row, column)
        || Contains(WarpageRoi, row, column)
        || Landmarks.Any(landmark => row >= landmark.Row - 3 && row < landmark.Row + 29
            && column >= landmark.Column - 3 && column < landmark.Column + 29)
        || Impulses.Any(impulse => impulse.Row == row && impulse.Column == column);

    private static void Fill(double[] values, ToolRecipeGridRectangle rectangle, Func<int, int, double> value)
    {
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
        for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
            values[row * Width + column] = value(row, column);
    }

    private static C3DReferenceGridProfile CreateReferenceGridProfile() =>
        C3DReferenceGridProfile.Create(
            ReferenceFrame, ReferenceUnit, ReferenceProvenance, ReferenceRevision,
            Origin.ToGridVector(), Axes.U.ToGridVector(), Axes.V.ToGridVector(), Axes.H.ToGridVector(),
            PitchU, PitchV, Height, Width, 0.99);

    private static C3DAffineMatrix3x4 CreateExpectedMatrix()
    {
        var translation = Origin + Axes.U * (PitchU * 0.5) + Axes.V * (PitchV * 0.5);
        var x = Axes.U * PitchU;
        var y = Axes.H * HeightScale;
        var z = Axes.V * PitchV;
        return new C3DAffineMatrix3x4(
            x.X, y.X, z.X, translation.X,
            x.Y, y.Y, z.Y, translation.Y,
            x.Z, y.Z, z.Z, translation.Z);
    }

    private static Vec3 ApplyExpectedAffine(Vec3 source)
    {
        var result = CreateExpectedMatrix().Transform(source.X, source.Y, source.Z);
        return new Vec3(result.X, result.Y, result.Z);
    }

    private static Vec3 ExpectedSourceAnchor(LandmarkDefinition landmark) =>
        new(landmark.Column - 0.5, (10 + landmark.Height) / 2.0, landmark.Row - 0.5);

    private static double MaximumPointError(C3DTransformedPointCloud cloud)
    {
        var maximum = 0d;
        foreach (var point in cloud.Points)
        {
            var expected = ApplyExpectedAffine(new Vec3(point.Column, point.RawHeight, point.Row));
            maximum = Math.Max(maximum, Distance(expected, new Vec3(point.X, point.Y, point.Z)));
        }
        return maximum;
    }

    private static double MaximumHeightFieldError(C3DHeightFieldSnapshot source, C3DTransformedHeightField field)
    {
        var maximum = 0d;
        for (var index = 0; index < field.Cells.Count; index++)
        {
            var sourceValue = source.Values.Span[index];
            var cell = field.Cells[index];
            if (!double.IsFinite(sourceValue))
            {
                if (cell.HasValue) return double.PositiveInfinity;
                continue;
            }
            if (!cell.HasValue || cell.SourceRow != index / Width || cell.SourceColumn != index % Width)
                return double.PositiveInfinity;
            maximum = Math.Max(maximum, Math.Abs(cell.Height - HeightScale * sourceValue));
        }
        return maximum;
    }

    private static ThicknessTruth CalculateThickness(C3DTransformedHeightField field, ToolRecipeGridRectangle roi)
    {
        var values = Values(field, roi).ToArray();
        return new ThicknessTruth(values.Average(), values.Min(), values.Max(), values.Max() - values.Min(), values.Length);
    }

    private static WarpageTruth CalculateWarpage(C3DTransformedHeightField field, ToolRecipeGridRectangle roi)
    {
        var samples = new List<(double X, double Y, double Z)>();
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
        for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++)
        {
            var cell = field.Cells[row * field.ColumnCount + column];
            if (cell.HasValue) samples.Add((column, row, cell.Height));
        }
        var matrix = new double[3, 4];
        foreach (var sample in samples)
        {
            matrix[0, 0] += sample.X * sample.X; matrix[0, 1] += sample.X * sample.Y;
            matrix[0, 2] += sample.X; matrix[0, 3] += sample.X * sample.Z;
            matrix[1, 0] += sample.X * sample.Y; matrix[1, 1] += sample.Y * sample.Y;
            matrix[1, 2] += sample.Y; matrix[1, 3] += sample.Y * sample.Z;
            matrix[2, 0] += sample.X; matrix[2, 1] += sample.Y;
            matrix[2, 2] += 1; matrix[2, 3] += sample.Z;
        }
        var solved = Solve3x3(matrix);
        var residuals = samples.Select(sample => sample.Z - (solved[0] * sample.X + solved[1] * sample.Y + solved[2])).ToArray();
        var minimum = residuals.Min();
        var maximum = residuals.Max();
        var rms = Math.Sqrt(residuals.Sum(value => value * value) / residuals.Length);
        return new WarpageTruth(maximum - minimum, rms, minimum, maximum, solved[0], solved[1], solved[2], residuals.Length);
    }

    private static double[] Solve3x3(double[,] matrix)
    {
        for (var pivot = 0; pivot < 3; pivot++)
        {
            var best = Enumerable.Range(pivot, 3 - pivot).MaxBy(row => Math.Abs(matrix[row, pivot]));
            if (Math.Abs(matrix[best, pivot]) < 1e-18) throw new InvalidDataException("Independent best-fit plane solve is singular.");
            if (best != pivot)
                for (var column = pivot; column < 4; column++)
                    (matrix[pivot, column], matrix[best, column]) = (matrix[best, column], matrix[pivot, column]);
            var divisor = matrix[pivot, pivot];
            for (var column = pivot; column < 4; column++) matrix[pivot, column] /= divisor;
            for (var row = 0; row < 3; row++)
            {
                if (row == pivot) continue;
                var factor = matrix[row, pivot];
                for (var column = pivot; column < 4; column++) matrix[row, column] -= factor * matrix[pivot, column];
            }
        }
        return [matrix[0, 3], matrix[1, 3], matrix[2, 3]];
    }

    private static IEnumerable<double> Values(C3DTransformedHeightField field, ToolRecipeGridRectangle roi)
    {
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
        for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++)
        {
            var cell = field.Cells[row * field.ColumnCount + column];
            if (cell.HasValue) yield return cell.Height;
        }
    }

    private static double MaximumMetricError(ToolResult result, params (string Name, double Expected)[] expected) =>
        expected.Max(item => Math.Abs(result.Metrics.Single(metric => metric.Name == item.Name).Value - item.Expected));

    private static void WriteGroundTruth(
        string package,
        C3DHeightFieldSnapshot source,
        IReadOnlyList<C3DLineIntersectionFeature> corners,
        C3DAffineTransform3D affine,
        C3DTransformedHeightField field,
        ThicknessTruth thickness,
        WarpageTruth warpage,
        IReadOnlyList<CheckResult> checks)
    {
        var truth = new
        {
            schemaVersion = "1.0",
            name = "Synthetic Affine Inspection Plate v1",
            boundary = "Synthetic display-frame golden only; not physical calibration or metrology evidence.",
            source = new
            {
                file = SourceFileName,
                width = Width,
                height = Height,
                unit = SourceUnit,
                frameId = SourceFrame,
                byteLength = source.ByteLength,
                contentSha256 = source.ContentSha256,
                validCount = source.ValidCount,
                missingCount = source.MissingCount,
                impulses = Impulses.Select(item => new { item.Row, item.Column, item.Height })
            },
            landmarks = Landmarks.Select((landmark, index) =>
            {
                var expectedSource = ExpectedSourceAnchor(landmark);
                var expectedReference = ApplyExpectedAffine(expectedSource);
                var actual = corners[index];
                return new
                {
                    landmark.Id,
                    landmark.Role,
                    padHeight = landmark.Height,
                    expectedSource,
                    expectedReference,
                    extractedSource = new { x = actual.CornerAnchorX, y = actual.CornerAnchorY, z = actual.CornerAnchorZ },
                    extractionError = Distance(expectedSource, new Vec3(actual.CornerAnchorX, actual.CornerAnchorY, actual.CornerAnchorZ))
                };
            }),
            affine = new
            {
                sourceConvention = C3DAffineApplyRule.SourceCoordinateConvention,
                intendedMatrix = CreateExpectedMatrix().Values,
                solvedMatrix = affine.Matrix.Values,
                maximumMatrixError = MaximumAbsoluteDifference(CreateExpectedMatrix().Values, affine.Matrix.Values),
                affine.ConditionEstimate,
                affine.ArithmeticMaximumResidual,
                reference = new { frameId = ReferenceFrame, unit = ReferenceUnit, provenance = ReferenceProvenance, revision = ReferenceRevision },
                grid = new { origin = Origin, uAxis = Axes.U, vAxis = Axes.V, hAxis = Axes.H, pitchU = PitchU, pitchV = PitchV }
            },
            regrid = new
            {
                rows = field.RowCount,
                columns = field.ColumnCount,
                field.PopulatedCellCount,
                field.MissingCellCount,
                field.CoverageRatio,
                field.CollisionCount,
                field.ContentSha256
            },
            measurements = new
            {
                thickness = new { roi = ThicknessRoi, thickness.Mean, thickness.Minimum, thickness.Maximum, thickness.Range, validSampleCount = thickness.Count },
                warpage = new { roi = WarpageRoi, warpage.PeakToValley, warpage.Rms, warpage.MinimumResidual, warpage.MaximumResidual, warpage.SlopeX, warpage.SlopeY, warpage.Intercept, validSampleCount = warpage.Count }
            },
            verification = new { checks = checks.Count, passed = checks.Count(check => check.Passed) }
        };
        File.WriteAllText(Path.Combine(package, "ground-truth.json"), JsonSerializer.Serialize(truth, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }

    private static void WriteReadme(string package) => File.WriteAllText(
        Path.Combine(package, "README.md"),
        """
        # Synthetic Affine Inspection Plate v1

        Deterministic 240 x 160 C3D validation package for the generic OpenVisionLab 3D tool chain:

        `Median Filter -> 8 Height Difference Edges -> 8 Line Fits -> 4 Line Intersections -> Landmark Correspondence -> XYZ Affine Solve -> Apply -> Re-grid -> Thickness -> Warpage`

        - `source-affine-inspection-plate-v1.C3D`: exact source bytes.
        - `inspection-recipe.ov3d-recipe.json`: schema 1.3 generic ordered recipe.
        - `ground-truth.json`: source identity, extracted anchors, intended/solved affine, A3 coverage, and measurement truth.
        - `source-height-preview.png`: human-readable source preview generated by `scripts/render-synthetic-affine-inspection-plate.py`.
        - `reference-height-preview.png`: expected A3 scalar preview generated by the same script.

        Boundary: this is synthetic display-frame evidence. It does not prove sensor fidelity, physical scale, calibration, Gauge R&R, or metrology accuracy.
        """);

    private static IReadOnlyList<ToolRecipeParameter> FilterParameters() =>
    [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")];

    private static IReadOnlyList<ToolRecipeParameter> EdgeParameters(string axis) =>
    [
        new("ComparisonAxis", axis), new("Polarity", "Rising"), new("MinimumDelta", "5"),
        new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"),
        new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")
    ];

    private static IReadOnlyList<ToolRecipeParameter> LineParameters() =>
    [
        new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "0.01"),
        new("MinimumInlierCount", "10"), new("MinimumInlierRatio", "1"), new("MinimumInlierScanlineSpan", "10"),
        new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"),
        new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"),
        new("EndpointPolicy", "InlierProjectionExtents")
    ];

    private static IReadOnlyList<ToolRecipeParameter> IntersectionParameters(string role) =>
    [
        new("MaximumClosestApproachDistance", "0.01"), new("MinimumAcuteAngleDegrees", "80"),
        new("MaximumSupportExtension", "6"), new("OutputRole", role),
        new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"),
        new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")
    ];

    private static IReadOnlyList<ToolRecipeParameter> CorrespondenceParameters() =>
    [new("PairCountPolicy", "ExactlyFour"), new("SourceArtifactPolicy", "CurrentPublishedCornerAnchor"), new("AffineIndependencePolicy", "RequireNonDegenerateTetrahedra")];

    private static IReadOnlyList<ToolRecipeParameter> AffineParameters() =>
    [new("SolvePolicy", "ExactFourPartialPivot"), new("MaximumConditionEstimate", "1000000000"), new("ArithmeticResidualWarning", "1E-9")];

    private static string EdgeAcrossColumnsSelectionId(LandmarkDefinition item) => $"selection.edge.{item.Id}.across-columns";
    private static string EdgeAcrossRowsSelectionId(LandmarkDefinition item) => $"selection.edge.{item.Id}.across-rows";
    private static string EdgeAcrossColumnsStepId(LandmarkDefinition item) => $"step.edge.{item.Id}.across-columns";
    private static string EdgeAcrossRowsStepId(LandmarkDefinition item) => $"step.edge.{item.Id}.across-rows";
    private static string LineAcrossColumnsStepId(LandmarkDefinition item) => $"step.line.{item.Id}.across-columns";
    private static string LineAcrossRowsStepId(LandmarkDefinition item) => $"step.line.{item.Id}.across-rows";
    private static string IntersectionStepId(LandmarkDefinition item) => $"step.intersection.{item.Id}";
    private static string EdgeAcrossColumnsOutputId(LandmarkDefinition item) => $"derived.edge.{item.Id}.across-columns";
    private static string EdgeAcrossRowsOutputId(LandmarkDefinition item) => $"derived.edge.{item.Id}.across-rows";
    private static string LineAcrossColumnsOutputId(LandmarkDefinition item) => $"derived.line.{item.Id}.across-columns";
    private static string LineAcrossRowsOutputId(LandmarkDefinition item) => $"derived.line.{item.Id}.across-rows";
    private static string IntersectionOutputId(LandmarkDefinition item) => $"derived.corner-anchor.{item.Id}";

    private static (Vec3 U, Vec3 V, Vec3 H) CreateAxes()
    {
        var yaw = Math.PI / 6.0;
        var tilt = Math.PI / 9.0;
        var u = new Vec3(Math.Cos(yaw), 0, -Math.Sin(yaw));
        var h = new Vec3(Math.Sin(yaw) * Math.Sin(tilt), Math.Cos(tilt), Math.Cos(yaw) * Math.Sin(tilt));
        var v = Cross(h, u);
        return (u, v, h);
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static bool Contains(ToolRecipeGridRectangle rectangle, int row, int column) =>
        row >= rectangle.Row && row < rectangle.Row + rectangle.RowCount
        && column >= rectangle.Column && column < rectangle.Column + rectangle.ColumnCount;

    private static double MaximumAbsoluteDifference(IReadOnlyList<double> expected, IReadOnlyList<double> actual) =>
        expected.Zip(actual, (left, right) => Math.Abs(left - right)).Max();

    private static double Distance(Vec3 a, Vec3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static bool Nearly(double actual, double expected, double tolerance) => Math.Abs(actual - expected) <= tolerance;
    private static string Format(Vec3 value) => $"{value.X:G17},{value.Y:G17},{value.Z:G17}";
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private static CheckResult Check(string name, bool passed, string evidence) => new(name, passed, evidence);

    private static void RequirePass<T>(ToolResult result, T? output, string label) where T : class
    {
        if (result.Status != ResultStatus.Pass || output is null)
            throw new InvalidDataException($"{label} failed: {result.Status}: {result.Message}");
    }

    private sealed record LandmarkDefinition(string Id, string Role, int Column, int Row, double Height)
    {
        public string Name => Id.Replace('-', ' ');
    }

    private readonly record struct Vec3(double X, double Y, double Z)
    {
        public static Vec3 operator +(Vec3 left, Vec3 right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        public static Vec3 operator *(Vec3 value, double scale) => new(value.X * scale, value.Y * scale, value.Z * scale);
        public C3DReferenceGridVector ToGridVector() => new(X, Y, Z);
    }

    private sealed record ThicknessTruth(double Mean, double Minimum, double Maximum, double Range, int Count);
    private sealed record WarpageTruth(double PeakToValley, double Rms, double MinimumResidual, double MaximumResidual, double SlopeX, double SlopeY, double Intercept, int Count);
    private sealed record CheckResult(string Name, bool Passed, string Evidence);
}
