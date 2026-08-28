using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightImageAlignmentGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("border-translated-rotated", VerifyBorderTranslatedRotated),
            Check("feature-translated", VerifyFeatureTranslated),
            Check("deterministic-content-identity", VerifyDeterminism),
            Check("runner-replay-parity", VerifyRunnerParity),
            Check("negative-no-result", VerifyNoResult),
            Check("negative-threshold-rejection", VerifyThresholdRejection),
            Check("negative-ambiguous-candidates", VerifyAmbiguousCandidates),
            Check("negative-invalid-roi", VerifyInvalidRoi),
            Check("negative-unit-frame", VerifyUnitAndFrame)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DHeightImageAlignmentGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|mapping=pixelX=column,pixelY=row,no-flip,one-source-cell-per-pixel|output=software-pixel-grid-pose|physical-calibration=not-claimed|modes=BorderTemplate,FeatureHomography"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"C3D Height Image Alignment golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyBorderTranslatedRotated()
    {
        var fixture = CreateFixture();
        var beforeReference = fixture.Reference.Values.ToArray();
        var beforeMoving = fixture.Moving.Values.ToArray();
        var evaluation = Evaluate(fixture, C3DHeightImageAlignmentMode.BorderTemplate);
        var output = evaluation.Output;
        var pose = output?.Pose;
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && pose is not null
            && Math.Abs(pose.TranslationX - fixture.TranslationX) <= 2.0
            && Math.Abs(pose.TranslationY - fixture.TranslationY) <= 2.0
            && Math.Abs(pose.RotationDegrees - fixture.RotationDegrees) <= 2.0
            && output.ContentSha256.Length == 64
            && fixture.Reference.Values.Span.SequenceEqual(beforeReference)
            && fixture.Moving.Values.Span.SequenceEqual(beforeMoving);
        return (passed, $"{Evidence(evaluation)},pose=({pose?.TranslationX:0.###},{pose?.TranslationY:0.###},{pose?.RotationDegrees:0.###}),expected=({fixture.TranslationX},{fixture.TranslationY},{fixture.RotationDegrees})");
    }

    private static (bool Passed, string Evidence) VerifyFeatureTranslated()
    {
        var fixture = CreateFeatureFixture();
        var evaluation = Evaluate(
            fixture,
            C3DHeightImageAlignmentMode.FeatureHomography,
            searchScoreMinimum: 0.75,
            acceptanceScoreMinimumPercent: 35d,
            angleMinimumDegrees: -180,
            angleMaximumDegrees: 180);
        var output = evaluation.Output;
        var pose = output?.Pose;
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && pose is not null
            && Math.Abs(pose.TranslationX - fixture.TranslationX) <= 2.0
            && Math.Abs(pose.TranslationY - fixture.TranslationY) <= 2.0
            && double.IsFinite(pose.RotationDegrees)
            && pose.RotationDegrees >= -180d
            && pose.RotationDegrees <= 180d;
        return (passed, $"{Evidence(evaluation)},pose=({pose?.TranslationX:0.###},{pose?.TranslationY:0.###},{pose?.RotationDegrees:0.###})");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var fixture = CreateFixture();
        var first = Evaluate(fixture, C3DHeightImageAlignmentMode.BorderTemplate);
        var second = Evaluate(fixture, C3DHeightImageAlignmentMode.BorderTemplate);
        var passed = first.Result.Status == ResultStatus.Pass
            && second.Result.Status == ResultStatus.Pass
            && first.Output?.ContentSha256 == second.Output?.ContentSha256
            && first.Output?.Pose == second.Output?.Pose
            && first.Output?.Diagnostics == second.Output?.Diagnostics;
        return (passed, $"first={first.Output?.ContentSha256},second={second.Output?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity()
    {
        var fixture = CreateFixture();
        var direct = Evaluate(fixture, C3DHeightImageAlignmentMode.BorderTemplate);
        var directory = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "HeightImageAlignment",
            $"runner-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var referencePath = Path.Combine(directory, "reference.c3d");
            var movingPath = Path.Combine(directory, "moving.c3d");
            fixture.Reference.SaveC3D(referencePath);
            fixture.Moving.SaveC3D(movingPath);
            var specification = new C3DHeightImageAlignmentRunnerSpecification
            {
                StepId = "step.height-image-align.01",
                SelectionId = "selection.height-image-template.01",
                OutputEntityId = "alignment.height-image.01",
                Mode = C3DHeightImageAlignmentMode.BorderTemplate,
                TemplateRow = fixture.Selection.Row,
                TemplateColumn = fixture.Selection.Column,
                TemplateRowCount = fixture.Selection.RowCount,
                TemplateColumnCount = fixture.Selection.ColumnCount,
                SearchScoreMinimum = 0.4d,
                AcceptanceScoreMinimumPercent = 60d,
                MinimumCandidateMarginPercent = 1d,
                AngleMinimumDegrees = -10,
                AngleMaximumDegrees = 10,
                AngleStepDegrees = 1d,
                Reference = ToRunnerSource(fixture.Reference, referencePath),
                Moving = ToRunnerSource(fixture.Moving, movingPath)
            };
            var specificationPath = Path.Combine(directory, "alignment.json");
            var reportPath = Path.Combine(directory, "runner.txt");
            File.WriteAllText(
                specificationPath,
                JsonSerializer.Serialize(specification, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                }));
            var runnerExit = C3DHeightImageAlignmentRunnerExecution.Run(specificationPath, reportPath);
            var report = File.ReadAllText(reportPath);
            var passed = direct.Output is not null
                && direct.Result.Status == ResultStatus.Pass
                && runnerExit == 0
                && report.Contains($"sha256={direct.Output.ContentSha256}", StringComparison.Ordinal)
                && report.Contains($"translation={direct.Output.Pose.TranslationX:R},{direct.Output.Pose.TranslationY:R}", StringComparison.Ordinal);
            return (passed, $"direct={direct.Output?.ContentSha256},runnerExit={runnerExit},hashMatched={report.Contains(direct.Output?.ContentSha256 ?? "(none)", StringComparison.Ordinal)}");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static C3DHeightImageAlignmentRunnerSource ToRunnerSource(
        C3DHeightFieldSnapshot snapshot,
        string path)
        => new()
        {
            Path = path,
            EntityId = snapshot.EntityId,
            Unit = snapshot.Unit,
            FrameId = snapshot.FrameId,
            ByteLength = snapshot.ByteLength,
            ContentSha256 = snapshot.ContentSha256,
            Width = snapshot.Width,
            Height = snapshot.Height
        };

    private static (bool Passed, string Evidence) VerifyNoResult()
    {
        var fixture = CreateFixture();
        var blankValues = Enumerable.Repeat(10d, fixture.Moving.Width * fixture.Moving.Height).ToArray();
        var blank = C3DHeightFieldSnapshot.CreateForVerification(
            "moving.blank",
            fixture.Moving.Width,
            fixture.Moving.Height,
            blankValues,
            fixture.Moving.Unit,
            fixture.Moving.FrameId);
        var evaluation = Evaluate(fixture with { Moving = blank }, C3DHeightImageAlignmentMode.BorderTemplate);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null, evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyThresholdRejection()
    {
        var fixture = CreateFixture();
        var evaluation = Evaluate(
            fixture,
            C3DHeightImageAlignmentMode.BorderTemplate,
            acceptanceScoreMinimumPercent: 80d);
        var passed = evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output is null
            && evaluation.Result.Message.Contains("below the acceptance threshold", StringComparison.Ordinal);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyAmbiguousCandidates()
    {
        var fixture = CreateAmbiguousFixture();
        var evaluation = Evaluate(
            fixture,
            C3DHeightImageAlignmentMode.BorderTemplate,
            searchScoreMinimum: 0.2,
            acceptanceScoreMinimumPercent: 30d,
            angleMinimumDegrees: 0,
            angleMaximumDegrees: 0);
        var passed = evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output is null
            && evaluation.Result.Message.Contains("ambiguous", StringComparison.OrdinalIgnoreCase);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidRoi()
    {
        var fixture = CreateFixture() with
        {
            Selection = new ToolRecipeGridRectangle(0, 0, 7, 7)
        };
        var evaluation = Evaluate(fixture, C3DHeightImageAlignmentMode.BorderTemplate);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null, evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyUnitAndFrame()
    {
        var fixture = CreateFixture();
        var unitMismatch = fixture with
        {
            Moving = C3DHeightFieldSnapshot.CreateForVerification(
                "moving.unit-mismatch",
                fixture.Moving.Width,
                fixture.Moving.Height,
                fixture.Moving.Values.ToArray(),
                "millimetre",
                fixture.Moving.FrameId)
        };
        var frameMismatch = fixture with
        {
            Moving = C3DHeightFieldSnapshot.CreateForVerification(
                "moving.frame-mismatch",
                fixture.Moving.Width,
                fixture.Moving.Height,
                fixture.Moving.Values.ToArray(),
                fixture.Moving.Unit,
                "other-frame")
        };
        var unitEvaluation = Evaluate(unitMismatch, C3DHeightImageAlignmentMode.BorderTemplate);
        var frameEvaluation = Evaluate(frameMismatch, C3DHeightImageAlignmentMode.BorderTemplate);
        return (unitEvaluation.Result.Status == ResultStatus.Error
            && frameEvaluation.Result.Status == ResultStatus.Error
            && unitEvaluation.Output is null
            && frameEvaluation.Output is null,
            $"unit={unitEvaluation.Result.Message};frame={frameEvaluation.Result.Message}");
    }

    private static C3DHeightImageAlignmentEvaluation Evaluate(
        Fixture fixture,
        C3DHeightImageAlignmentMode mode,
        double searchScoreMinimum = 0.4,
        double acceptanceScoreMinimumPercent = 60d,
        int angleMinimumDegrees = -10,
        int angleMaximumDegrees = 10)
        => C3DHeightImageAlignmentAdapter.Evaluate(new C3DHeightImageAlignmentInput(
            "step.height-image-align.01",
            fixture.Reference,
            fixture.Moving,
            "selection.height-image-template.01",
            fixture.Selection,
            "alignment.height-image.01",
            mode,
            searchScoreMinimum,
            acceptanceScoreMinimumPercent,
            1d,
            angleMinimumDegrees,
            angleMaximumDegrees,
            1d));

    private static Fixture CreateFixture()
    {
        const int width = 128;
        const int height = 96;
        var selection = new ToolRecipeGridRectangle(30, 38, 32, 32);
        var referenceValues = CreatePattern(width, height, selection);
        const double translationX = 7d;
        const double translationY = -5d;
        // MatchingTool reports the OpenCV image-plane sign for this fixture;
        // the +4° raster transform is therefore expected as -4° in its result.
        const double rotationDegrees = -4d;
        var movingValues = TransformPattern(referenceValues, width, height, selection, translationX, translationY, 4d);
        return new Fixture(
            C3DHeightFieldSnapshot.CreateForVerification("reference.height-image", width, height, referenceValues),
            C3DHeightFieldSnapshot.CreateForVerification("moving.height-image", width, height, movingValues),
            selection,
            translationX,
            translationY,
            rotationDegrees);
    }

    private static Fixture CreateFeatureFixture()
    {
        const int width = 144;
        const int height = 108;
        var selection = new ToolRecipeGridRectangle(4, 4, 96, 80);
        var referenceValues = Enumerable.Repeat(10d, width * height).ToArray();
        for (var y = selection.Row; y < selection.Row + selection.RowCount; y++)
        {
            for (var x = selection.Column; x < selection.Column + selection.ColumnCount; x++)
            {
                referenceValues[y * width + x] = 35d + ((x * 17 + y * 29 + x * x + y * y * 3) % 180);
            }
        }
        for (var y = 18; y < 50; y++)
        {
            for (var x = 24; x < 64; x++)
            {
                referenceValues[y * width + x] = 220d;
            }
        }
        for (var x = 4; x < width - 5; x++)
        {
            var y = (int)Math.Round((height - 7) + (4 - (height - 7)) * (x - 3d) / (width - 8d));
            for (var offset = -1; offset <= 1; offset++)
            {
                var row = y + offset;
                if (row >= 0 && row < height)
                {
                    referenceValues[row * width + x] = 130d;
                }
            }
        }
        var circleCenterX = width - 12;
        var circleCenterY = height - 11;
        for (var y = circleCenterY - 6; y <= circleCenterY + 6; y++)
        {
            for (var x = circleCenterX - 6; x <= circleCenterX + 6; x++)
            {
                if ((x - circleCenterX) * (x - circleCenterX) + (y - circleCenterY) * (y - circleCenterY) <= 25)
                {
                    referenceValues[y * width + x] = 255d;
                }
            }
        }

        const double translationX = 9d;
        const double translationY = 6d;
        var movingValues = TransformPattern(referenceValues, width, height, selection, translationX, translationY, 0d);
        return new Fixture(
            C3DHeightFieldSnapshot.CreateForVerification("reference.feature-image", width, height, referenceValues),
            C3DHeightFieldSnapshot.CreateForVerification("moving.feature-image", width, height, movingValues),
            selection,
            translationX,
            translationY,
            0d);
    }

    private static Fixture CreateAmbiguousFixture()
    {
        const int width = 160;
        const int height = 120;
        var selection = new ToolRecipeGridRectangle(20, 20, 32, 32);
        var referenceValues = CreatePattern(width, height, selection);
        var movingValues = Enumerable.Repeat(10d, width * height).ToArray();
        CopyPattern(referenceValues, movingValues, width, height, selection, 8, 10);
        CopyPattern(referenceValues, movingValues, width, height, selection, 86, 56);
        return new Fixture(
            C3DHeightFieldSnapshot.CreateForVerification("reference.ambiguous-image", width, height, referenceValues),
            C3DHeightFieldSnapshot.CreateForVerification("moving.ambiguous-image", width, height, movingValues),
            selection,
            8d,
            10d,
            0d);
    }

    private static void CopyPattern(
        IReadOnlyList<double> source,
        IList<double> destination,
        int width,
        int height,
        ToolRecipeGridRectangle selection,
        int targetColumn,
        int targetRow)
    {
        for (var row = 0; row < selection.RowCount; row++)
        {
            for (var column = 0; column < selection.ColumnCount; column++)
            {
                var value = source[(selection.Row + row) * width + selection.Column + column];
                if (value == 10d)
                {
                    continue;
                }

                var destinationColumn = targetColumn + column;
                var destinationRow = targetRow + row;
                if (destinationColumn >= 0 && destinationColumn < width
                    && destinationRow >= 0 && destinationRow < height)
                {
                    destination[destinationRow * width + destinationColumn] = value;
                }
            }
        }
    }

    private static double[] CreatePattern(int width, int height, ToolRecipeGridRectangle selection)
    {
        var values = Enumerable.Repeat(10d, width * height).ToArray();
        var left = selection.Column;
        var top = selection.Row;
        var right = left + selection.ColumnCount - 1;
        var bottom = top + selection.RowCount - 1;
        for (var y = top + 3; y <= bottom - 3; y++)
        {
            for (var x = left + 3; x <= right - 3; x++)
            {
                var border = x == left + 3 || x == right - 3 || y == top + 3 || y == bottom - 3;
                var diagonal = (x - left - 3) == (y - top - 3)
                    || (x - left - 3) + (y - top - 3) == selection.ColumnCount - 6;
                var cross = x == left + selection.ColumnCount / 2 || y == top + selection.RowCount / 2;
                var notch = (x - left) % 7 == 0 && (y - top) % 5 == 0;
                if (border || diagonal || cross || notch)
                {
                    values[y * width + x] = border ? 220d : (diagonal ? 180d : (cross ? 140d : 90d));
                }
            }
        }
        return values;
    }

    private static double[] TransformPattern(
        IReadOnlyList<double> source,
        int width,
        int height,
        ToolRecipeGridRectangle selection,
        double translationX,
        double translationY,
        double rotationDegrees)
    {
        var output = Enumerable.Repeat(10d, width * height).ToArray();
        var centerX = selection.Column + selection.ColumnCount / 2d;
        var centerY = selection.Row + selection.RowCount / 2d;
        var radians = rotationDegrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        for (var y = selection.Row; y < selection.Row + selection.RowCount; y++)
        {
            for (var x = selection.Column; x < selection.Column + selection.ColumnCount; x++)
            {
                var value = source[y * width + x];
                if (value == 10d)
                {
                    continue;
                }

                var relativeX = x - centerX;
                var relativeY = y - centerY;
                var targetX = centerX + cos * relativeX - sin * relativeY + translationX;
                var targetY = centerY + sin * relativeX + cos * relativeY + translationY;
                var targetColumn = (int)Math.Round(targetX, MidpointRounding.AwayFromZero);
                var targetRow = (int)Math.Round(targetY, MidpointRounding.AwayFromZero);
                if (targetColumn >= 0 && targetColumn < width && targetRow >= 0 && targetRow < height)
                {
                    output[targetRow * width + targetColumn] = value;
                }
            }
        }
        return output;
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> check)
    {
        try
        {
            var result = check();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, exception.GetBaseException().Message);
        }
    }

    private static string Evidence(C3DHeightImageAlignmentEvaluation evaluation)
        => $"status={evaluation.Result.Status};message={evaluation.Result.Message};hash={evaluation.Output?.ContentSha256};candidates={evaluation.Output?.Diagnostics.CandidateCount};best={evaluation.Output?.Diagnostics.BestScorePercent:0.###};second={evaluation.Output?.Diagnostics.SecondScorePercent:0.###};margin={evaluation.Output?.Diagnostics.ScoreMarginPercent:0.###}";

    private static string Clean(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');

    private sealed record Fixture(
        C3DHeightFieldSnapshot Reference,
        C3DHeightFieldSnapshot Moving,
        ToolRecipeGridRectangle Selection,
        double TranslationX,
        double TranslationY,
        double RotationDegrees);
}
