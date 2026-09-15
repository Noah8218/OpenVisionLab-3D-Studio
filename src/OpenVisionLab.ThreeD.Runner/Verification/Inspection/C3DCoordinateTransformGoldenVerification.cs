using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DCoordinateTransformGoldenVerification
{
    private const string SourceId = "source.coordinate-transform.fixture";
    private const string SourceUnit = "raw-height";
    private const string SourceFrame = "frame.c3d-grid-index";
    private const string ReferenceFrame = "frame.coordinate-transform.reference";
    private const string ReferenceUnit = "mm";
    private const double HeightScale = 4.0;
    private const double HeightOffset = 5.0;
    private const double CellFraction = 0.25;

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("pitch-origin-z-scale-offset-roundtrip-and-roi", VerifyPitchOriginScaleOffsetAndRoi),
            Check("axis-exchange-reversal-and-right-handed-grid", VerifyAxisExchangeAndReversal),
            Check("reflection-is-distinct-from-rotation", VerifyReflectionAndRotation),
            Check("zero-negative-pitch-singular-identity-and-handedness-fail-closed", VerifyFailureBoundaries)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath,
        [
            $"C3DCoordinateTransformGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|input=raw-C3D-plus-Published-AffineTransform3D|scene=ReferenceGridProfile|output=TransformedHeightField|pitch=positive-authored|axes=explicit-right-handed|calibration=excluded",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D coordinate transform golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyPitchOriginScaleOffsetAndRoi()
    {
        var snapshot = CreateSnapshot();
        var profile = CreateProfile(
            new C3DReferenceGridVector(10, 20, 30),
            new C3DReferenceGridVector(1, 0, 0),
            new C3DReferenceGridVector(0, 1, 0),
            new C3DReferenceGridVector(0, 0, 1),
            pitchU: 2,
            pitchV: 3);
        var transform = CreatePublishedTransform(snapshot, (column, rawHeight, row) =>
            (10 + (column + CellFraction) * 2,
             20 + (row + CellFraction) * 3,
             30 + HeightOffset + HeightScale * rawHeight));
        var applied = Apply(snapshot, transform, "derived.coordinate-transform.scene");
        var regridded = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput(
            "step.coordinate-transform.regrid",
            applied.Output ?? throw new InvalidDataException("Coordinate transform apply did not produce a cloud."),
            profile,
            "derived.coordinate-transform.grid"));
        var expectedHeights = snapshot.Values.ToArray()
            .Select(value => HeightOffset + HeightScale * value)
            .ToArray();
        var transformedExpected = new[]
        {
            (X: 10.5, Y: 20.75, Z: 39.0),
            (X: 12.5, Y: 20.75, Z: 47.0),
            (X: 10.5, Y: 23.75, Z: 43.0),
            (X: 12.5, Y: 23.75, Z: 59.0)
        };
        var actualPoints = applied.Output?.Points ?? [];
        var maxTransformError = actualPoints.Zip(transformedExpected)
            .Select(pair => Math.Max(
                Math.Max(Math.Abs(pair.First.X - pair.Second.X), Math.Abs(pair.First.Y - pair.Second.Y)),
                Math.Abs(pair.First.Z - pair.Second.Z)))
            .DefaultIfEmpty(double.PositiveInfinity)
            .Max();
        var maxRoundTripError = actualPoints
            .Select(point => Math.Max(
                Math.Max(Math.Abs((point.X - 10) / 2 - (point.Column + CellFraction)),
                    Math.Abs((point.Y - 20) / 3 - (point.Row + CellFraction))),
                Math.Abs((point.Z - 30 - HeightOffset) / HeightScale - point.RawHeight)))
            .DefaultIfEmpty(double.PositiveInfinity)
            .Max();
        var roiSourceLocators = regridded.Output?.Cells
            .Select(cell => (cell.SourceRow, cell.SourceColumn))
            .ToArray() ?? [];
        var pass = applied.Result.Status == ResultStatus.Pass
            && regridded.Result.Status == ResultStatus.Pass
            && actualPoints.Count == 4
            && maxTransformError <= 1e-10
            && maxRoundTripError <= 1e-10
            && regridded.Output is not null
            && regridded.Output.Cells.Count == 4
            && regridded.Output.Cells.Select(cell => cell.Height).Zip(expectedHeights).All(pair => Nearly(pair.First, pair.Second))
            && roiSourceLocators.SequenceEqual([(0, 0), (0, 1), (1, 0), (1, 1)]);
        return (pass,
            $"apply={applied.Result.Status};regrid={regridded.Result.Status}:{regridded.Result.Message};transformError={maxTransformError:G17};roundTripError={maxRoundTripError:G17};points={FormatPoints(actualPoints)};heights={FormatHeights(regridded.Output?.Cells.Select(cell => cell.Height))};roi={FormatLocators(roiSourceLocators)}");
    }

    private static (bool Passed, string Evidence) VerifyAxisExchangeAndReversal()
    {
        var snapshot = CreateSnapshot();
        var profile = CreateProfile(
            new C3DReferenceGridVector(100, 200, 300),
            new C3DReferenceGridVector(0, 1, 0),
            new C3DReferenceGridVector(1, 0, 0),
            new C3DReferenceGridVector(0, 0, -1),
            pitchU: 2,
            pitchV: 3);
        var transform = CreatePublishedTransform(snapshot, (column, rawHeight, row) =>
        {
            var u = (column + CellFraction) * 2;
            var v = (row + CellFraction) * 3;
            var h = HeightOffset + HeightScale * rawHeight;
            return (100 + v, 200 + u, 300 - h);
        });
        var applied = Apply(snapshot, transform, "derived.coordinate-transform.axis-exchange");
        var cloud = applied.Output ?? throw new InvalidDataException("Axis exchange apply did not produce a cloud.");
        var regridded = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput(
            "step.coordinate-transform.axis-exchange",
            cloud,
            profile,
            "derived.coordinate-transform.axis-grid"));
        var expectedPoints = new[]
        {
            (X: 100.75, Y: 200.5, Z: 291.0),
            (X: 100.75, Y: 202.5, Z: 283.0),
            (X: 103.75, Y: 200.5, Z: 287.0),
            (X: 103.75, Y: 202.5, Z: 271.0)
        };
        var maxTransformError = cloud.Points.Zip(expectedPoints)
            .Select(pair => Math.Max(
                Math.Max(Math.Abs(pair.First.X - pair.Second.X), Math.Abs(pair.First.Y - pair.Second.Y)),
                Math.Abs(pair.First.Z - pair.Second.Z)))
            .DefaultIfEmpty(double.PositiveInfinity)
            .Max();
        var handedness = Determinant(profile.UAxis, profile.VAxis, profile.HAxis);
        var pass = applied.Result.Status == ResultStatus.Pass
            && regridded.Result.Status == ResultStatus.Pass
            && maxTransformError <= 1e-10
            && Nearly(handedness, 1)
            && regridded.Output is not null
            && regridded.Output.Cells.Select(cell => cell.Height).Zip(
                snapshot.Values.ToArray().Select(value => HeightOffset + HeightScale * value))
                .All(pair => Nearly(pair.First, pair.Second))
            && regridded.Output.Cells.Select(cell => (cell.SourceRow, cell.SourceColumn))
                .SequenceEqual([(0, 0), (0, 1), (1, 0), (1, 1)]);
        return (pass,
            $"status={regridded.Result.Status}:{regridded.Result.Message};transformError={maxTransformError:G17};points={FormatPoints(cloud.Points)};handednessDeterminant={handedness:G17};heights={FormatHeights(regridded.Output?.Cells.Select(cell => cell.Height))}");
    }

    private static (bool Passed, string Evidence) VerifyReflectionAndRotation()
    {
        var snapshot = CreateSnapshot();
        var rotationTransform = CreatePublishedTransform(snapshot, (x, y, z) =>
            (10 - y, 20 + x, 30 + z));
        var reflectionTransform = CreatePublishedTransform(snapshot, (x, y, z) =>
            (10 - x, 20 + y, 30 + z));
        var rotation = Apply(snapshot, rotationTransform, "derived.coordinate-transform.rotation");
        var reflection = Apply(snapshot, reflectionTransform, "derived.coordinate-transform.reflection");
        var rotationDeterminant = Determinant(rotationTransform.Matrix);
        var reflectionDeterminant = Determinant(reflectionTransform.Matrix);
        var rotationPoint = rotation.Output?.Points[0];
        var reflectionPoint = reflection.Output?.Points[0];
        var pass = rotation.Result.Status == ResultStatus.Pass
            && reflection.Result.Status == ResultStatus.Pass
            && rotation.Output is not null
            && reflection.Output is not null
            && rotationDeterminant > 0
            && reflectionDeterminant < 0
            && rotationPoint is { } rotationValue
            && reflectionPoint is { } reflectionValue
            && Nearly(rotationValue.X, 9)
            && Nearly(rotationValue.Y, 20)
            && Nearly(reflectionValue.X, 10)
            && Nearly(reflectionValue.Y, 21)
            && !Nearly(rotationValue.X, reflectionValue.X);
        return (pass,
            $"rotationStatus={rotation.Result.Status};reflectionStatus={reflection.Result.Status};rotationDeterminant={rotationDeterminant:G17};reflectionDeterminant={reflectionDeterminant:G17};rotationPoint={FormatPoint(rotationPoint)};reflectionPoint={FormatPoint(reflectionPoint)}");
    }

    private static (bool Passed, string Evidence) VerifyFailureBoundaries()
    {
        var pitchZeroRejected = ThrowsInvalidProfile(0, 1);
        var pitchNegativeRejected = ThrowsInvalidProfile(-1, 1);
        var singular = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
            "step.coordinate-transform.singular",
            "derived.coordinate-transform.singular",
            CreateCorrespondence(
            [
                Pair("a", 0, 0, 0, 0, 0, 0),
                Pair("b", 1, 0, 0, 1, 0, 0),
                Pair("c", 0, 1, 0, 0, 1, 0),
                Pair("d", 1, 1, 0, 1, 1, 0)
            ]),
            1000,
            1e-10));
        var undefinedFrameRejected = ThrowsInvalidProfile(1, 1, referenceFrame: "", referenceUnit: ReferenceUnit);
        var undefinedUnitRejected = ThrowsInvalidProfile(1, 1, referenceFrame: ReferenceFrame, referenceUnit: "");

        var snapshot = CreateSnapshot();
        var validProfile = CreateProfile(
            new C3DReferenceGridVector(10, 20, 30),
            new C3DReferenceGridVector(1, 0, 0),
            new C3DReferenceGridVector(0, 1, 0),
            new C3DReferenceGridVector(0, 0, 1),
            pitchU: 2,
            pitchV: 3);
        var transform = CreatePublishedTransform(snapshot, (column, rawHeight, row) =>
            (10 + (column + CellFraction) * 2,
             20 + (row + CellFraction) * 3,
             30 + HeightOffset + HeightScale * rawHeight));
        var cloud = Apply(snapshot, transform, "derived.coordinate-transform.handedness-cloud").Output
            ?? throw new InvalidDataException("Failure-boundary fixture did not produce a cloud.");
        var leftHandedProfile = C3DReferenceGridProfile.Create(
            validProfile.ReferenceFrameId,
            validProfile.ReferenceUnit,
            validProfile.ReferenceProvenance,
            validProfile.ReferenceRevision,
            validProfile.Origin,
            validProfile.UAxis,
            validProfile.VAxis,
            new C3DReferenceGridVector(0, 0, -1),
            validProfile.PitchU,
            validProfile.PitchV,
            validProfile.RowCount,
            validProfile.ColumnCount,
            validProfile.MinimumCoverageRatio);
        var leftHanded = C3DRegridHeightFieldRule.Evaluate(new C3DRegridHeightFieldInput(
            "step.coordinate-transform.left-handed",
            cloud,
            leftHandedProfile,
            "derived.coordinate-transform.left-handed"));
        var pass = pitchZeroRejected
            && pitchNegativeRejected
            && singular.Result.Status == ResultStatus.Error
            && (singular.Result.Message.Contains("singular", StringComparison.OrdinalIgnoreCase)
                || singular.Result.Message.Contains("rank", StringComparison.OrdinalIgnoreCase)
                || singular.Result.Message.Contains("degenerate", StringComparison.OrdinalIgnoreCase)
                || singular.Result.Message.Contains("independence", StringComparison.OrdinalIgnoreCase))
            && undefinedFrameRejected
            && undefinedUnitRejected
            && leftHanded.Result.Status == ResultStatus.Error;
        return (pass,
            $"pitchZero={pitchZeroRejected};pitchNegative={pitchNegativeRejected};singular={singular.Result.Status}:{singular.Result.Message};undefinedFrame={undefinedFrameRejected};undefinedUnit={undefinedUnitRejected};leftHanded={leftHanded.Result.Status}:{leftHanded.Result.Message};cloudIdentity={cloud.ReferenceFrameId}/{cloud.ReferenceUnit}/{cloud.ReferenceProvenance}/{cloud.ReferenceRevision};profileIdentity={leftHandedProfile.ReferenceFrameId}/{leftHandedProfile.ReferenceUnit}/{leftHandedProfile.ReferenceProvenance}/{leftHandedProfile.ReferenceRevision}");
    }

    private static bool ThrowsInvalidProfile(
        double pitchU,
        double pitchV,
        string referenceFrame = ReferenceFrame,
        string referenceUnit = ReferenceUnit)
    {
        try
        {
            _ = CreateProfile(
                new C3DReferenceGridVector(0, 0, 0),
                new C3DReferenceGridVector(1, 0, 0),
                new C3DReferenceGridVector(0, 1, 0),
                new C3DReferenceGridVector(0, 0, 1),
                pitchU,
                pitchV,
                referenceFrame,
                referenceUnit);
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return true;
        }
    }

    private static C3DAffineApplyEvaluation Apply(
        C3DHeightFieldSnapshot snapshot,
        C3DAffineTransform3D transform,
        string outputEntityId) =>
        C3DAffineApplyRule.Evaluate(new C3DAffineApplyInput(
            "step.coordinate-transform.apply",
            snapshot,
            transform,
            outputEntityId));

    private static C3DReferenceGridProfile CreateProfile(
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis,
        double pitchU,
        double pitchV,
        string referenceFrame = ReferenceFrame,
        string referenceUnit = ReferenceUnit) =>
        C3DReferenceGridProfile.Create(
            referenceFrame,
            referenceUnit,
            "coordinate transform golden",
            "R1",
            origin,
            uAxis,
            vAxis,
            hAxis,
            pitchU,
            pitchV,
            2,
            2,
            1.0);

    private static C3DHeightFieldSnapshot CreateSnapshot() =>
        C3DHeightFieldSnapshot.CreateForVerification(
            SourceId,
            2,
            2,
            [1d, 3d, 2d, 6d],
            SourceUnit,
            SourceFrame);

    private static C3DAffineTransform3D CreatePublishedTransform(
        C3DHeightFieldSnapshot snapshot,
        Func<double, double, double, (double X, double Y, double Z)> map)
    {
        var locators = new[] { (Row: 0, Column: 0), (Row: 0, Column: 1), (Row: 1, Column: 0), (Row: 1, Column: 1) };
        var pairs = locators.Select((locator, index) =>
        {
            var rawHeight = snapshot.Values.Span[locator.Row * snapshot.Width + locator.Column];
            var reference = map(locator.Column, rawHeight, locator.Row);
            return new C3DLandmarkCorrespondencePair(
                $"derived.coordinate-transform.corner.{index}",
                "Coordinate transform golden corner",
                snapshot.RootSourceSha256,
                locator.Column,
                rawHeight,
                locator.Row,
                $"coordinate-transform.reference.{index}",
                reference.X,
                reference.Y,
                reference.Z);
        }).ToArray();
        var correspondence = C3DLandmarkCorrespondenceSet.Create(
            "derived.coordinate-transform.correspondence",
            pairs,
            snapshot.EntityId,
            snapshot.RootSourceSha256,
            snapshot.Unit,
            snapshot.FrameId,
            ReferenceFrame,
            ReferenceUnit,
            "coordinate transform golden",
            "R1",
            1e-12,
            4,
            4,
            0.1,
            0.1,
            "3D-015 coordinate transform golden");
        var solve = C3DAffineSolveRule.Evaluate(new C3DAffineSolveInput(
            "step.coordinate-transform.solve",
            "derived.coordinate-transform.affine",
            correspondence,
            1000,
            1e-10));
        if (solve.Result.Status != ResultStatus.Pass || solve.Output is null)
        {
            throw new InvalidDataException($"Coordinate transform golden could not publish its affine transform: {solve.Result.Message}");
        }

        return solve.Output;
    }

    private static C3DLandmarkCorrespondenceSet CreateCorrespondence(
        IReadOnlyList<C3DLandmarkCorrespondencePair> pairs) =>
        C3DLandmarkCorrespondenceSet.Create(
            "derived.coordinate-transform.singular-correspondence",
            pairs,
            SourceId,
            new string('A', 64),
            SourceUnit,
            SourceFrame,
            ReferenceFrame,
            ReferenceUnit,
            "coordinate transform singular fixture",
            "R1",
            1e-12,
            4,
            4,
            0.1,
            0.1,
            "3D-015 singular matrix golden");

    private static C3DLandmarkCorrespondencePair Pair(
        string id,
        double sourceX,
        double sourceY,
        double sourceZ,
        double referenceX,
        double referenceY,
        double referenceZ) =>
        new(
            $"derived.coordinate-transform.{id}",
            "Coordinate transform singular corner",
            new string('A', 64),
            sourceX,
            sourceY,
            sourceZ,
            $"coordinate-transform.{id}",
            referenceX,
            referenceY,
            referenceZ);

    private static double Determinant(C3DReferenceGridVector u, C3DReferenceGridVector v, C3DReferenceGridVector h) =>
        u.X * (v.Y * h.Z - v.Z * h.Y)
        - u.Y * (v.X * h.Z - v.Z * h.X)
        + u.Z * (v.X * h.Y - v.Y * h.X);

    private static double Determinant(C3DAffineMatrix3x4 matrix) =>
        matrix.M11 * ((matrix.M22 * matrix.M33) - (matrix.M23 * matrix.M32))
        - matrix.M12 * ((matrix.M21 * matrix.M33) - (matrix.M23 * matrix.M31))
        + matrix.M13 * ((matrix.M21 * matrix.M32) - (matrix.M22 * matrix.M31));

    private static bool Nearly(double actual, double expected) => Math.Abs(actual - expected) <= 1e-10;

    private static string FormatHeights(IEnumerable<double>? heights) =>
        heights is null ? "none" : string.Join(',', heights.Select(value => value.ToString("G8")));

    private static string FormatLocators(IEnumerable<(int SourceRow, int SourceColumn)> locators) =>
        string.Join(';', locators.Select(locator => $"{locator.SourceRow},{locator.SourceColumn}"));

    private static string FormatPoint(C3DTransformedPoint? point) =>
        point is { } value ? $"{value.X:G8},{value.Y:G8},{value.Z:G8}" : "none";

    private static string FormatPoints(IEnumerable<C3DTransformedPoint> points) =>
        string.Join(';', points.Select(point => $"{point.Row},{point.Column}:{point.X:G8},{point.Y:G8},{point.Z:G8}"));

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
