using System.Globalization;
using System.Numerics;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

return Run(args);

static int Run(string[] args)
{
    var lazProbePath = ReadOption(args, "--laz-probe");
    var c3DMapProbePath = ReadOption(args, "--c3d-map-probe");
    var c3DMapPlyPath = ReadOption(args, "--ply");
    var recipePath = ReadOption(args, "--recipe");
    var reportPath = ReadOption(args, "--report");
    var expectedStatus = ReadOption(args, "--expect-status");
    var compareContractPath = ReadOption(args, "--compare-contract");
    var verifyPlaneFlatness = args.Contains("--verify-plane-flatness", StringComparer.OrdinalIgnoreCase);
    var verifyPointPairDimensions = args.Contains("--verify-point-pair-dimensions", StringComparer.OrdinalIgnoreCase);
    var verifyGapFlush = args.Contains("--verify-gap-flush", StringComparer.OrdinalIgnoreCase);
    var verifyVolume = args.Contains("--verify-volume", StringComparer.OrdinalIgnoreCase);
    var verifyC3DMapFidelity = args.Contains("--verify-c3d-map-fidelity", StringComparer.OrdinalIgnoreCase);
    var c3DMapPointOnly = args.Contains("--point-only", StringComparer.OrdinalIgnoreCase);

    if (verifyC3DMapFidelity)
    {
        if (reportPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-map-fidelity --report <path>");
            return 2;
        }

        return C3DMapFidelityVerification.RunGolden(reportPath);
    }

    if (verifyPointPairDimensions)
    {
        if (reportPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-point-pair-dimensions --report <path>");
            return 2;
        }

        return PointPairDimensionsGoldenVerification.Run(reportPath);
    }

    if (verifyGapFlush)
    {
        if (reportPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-gap-flush --report <path>");
            return 2;
        }

        return GapFlushGoldenVerification.Run(reportPath);
    }

    if (verifyVolume)
    {
        if (reportPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-volume --report <path>");
            return 2;
        }

        return VolumeGoldenVerification.Run(reportPath);
    }

    if (verifyPlaneFlatness)
    {
        if (reportPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-plane-flatness --report <path>");
            return 2;
        }

        return PlaneFlatnessGoldenVerification.Run(reportPath);
    }

    if (lazProbePath is not null)
    {
        if (reportPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --laz-probe <path> --report <path> [--max-sampled-points <count>]");
            return 2;
        }

        int maxSampledPoints;
        try
        {
            maxSampledPoints = ReadIntOption(args, "--max-sampled-points") ?? 50000;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        return RunLazProbe(lazProbePath, reportPath, maxSampledPoints);
    }

    if (c3DMapProbePath is not null)
    {
        if (reportPath is null || c3DMapPlyPath is null)
        {
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --c3d-map-probe <path> --ply <path> --report <path> [--max-sampled-points <count>] [--point-only]");
            return 2;
        }

        int maxSampledPoints;
        try
        {
            maxSampledPoints = ReadIntOption(args, "--max-sampled-points") ?? 140000;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        return C3DMapFidelityVerification.RunProbe(c3DMapProbePath, c3DMapPlyPath, reportPath, maxSampledPoints, includeFaces: !c3DMapPointOnly);
    }

    if (recipePath is null || reportPath is null)
    {
        Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --recipe <path> --report <path> [--expect-status Pass|Fail|Warning|Error] [--compare-contract <path>]");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --laz-probe <path> --report <path> [--max-sampled-points <count>]");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --c3d-map-probe <path> --ply <path> --report <path> [--max-sampled-points <count>] [--point-only]");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-plane-flatness --report <path>");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-point-pair-dimensions --report <path>");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-gap-flush --report <path>");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-volume --report <path>");
        Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-map-fidelity --report <path>");
        return 2;
    }

    try
    {
        var fullRecipePath = Path.GetFullPath(recipePath);
        var recipeType = ReadRecipeType(fullRecipePath);
        if (recipeType.Equals(LazTwoPointMeasurementRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return RunLazTwoPointRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath);
        }

        if (recipeType.Equals(C3DPointPairDimensionsRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return RunC3DPointPairDimensionsRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath);
        }

        if (recipeType.Equals(C3DGapFlushRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return RunC3DGapFlushRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath);
        }

        var recipe = HeightDeviationRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var maxSampledPoints = new[]
        {
            recipe.RoiStep?.MaxSampledPoints ?? 0,
            recipe.PlaneFlatness is { Enabled: true } planeFlatnessStep ? planeFlatnessStep.MaxSampledPoints : 0,
            recipe.Volume is { Enabled: true } volumeStep ? volumeStep.MaxSampledPoints : 0
        }.Max();
        var grid = C3DHeightGrid.Load(sourcePath, maxSampledPoints);
        var heightDeviationResult = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
            recipe.Source.EntityId,
            recipe.Source.Name,
            grid.Min,
            grid.Max,
            grid.Mean,
            grid.ValidSampleCount,
            recipe.Rule.PeakTolerance,
            recipe.Source.Unit));
        var roiStepResult = recipe.RoiStep is null
            ? null
            : EvaluateRoiStep(recipe.RoiStep, recipe.Transform ?? ModelTransform.Identity, grid);
        var planeFlatnessResult = recipe.PlaneFlatness is { Enabled: true } planeFlatness
            ? EvaluatePlaneFlatness(planeFlatness, recipe.Transform ?? ModelTransform.Identity, grid)
            : null;
        var volumeResult = recipe.Volume is { Enabled: true } volume
            ? EvaluateVolume(volume, recipe.Transform ?? ModelTransform.Identity, grid)
            : null;
        var result = volumeResult?.Result ?? planeFlatnessResult?.Result ?? heightDeviationResult;

        WriteReport(reportPath, fullRecipePath, sourcePath, recipe, grid, result, roiStepResult, planeFlatnessResult, volumeResult);
        if (compareContractPath is not null)
        {
            if (volumeResult is not null)
                CompareUiContract(compareContractPath, result, "Above-plane volume", "Below-plane volume", "Signed net volume");
            else
                CompareUiContract(compareContractPath, result);
        }

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {result.Status}.");
            return 3;
        }

        Console.WriteLine($"{result.ToolName}: {result.Status}");
        return result.Status == ResultStatus.Error ? 4 : 0;
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int RunC3DPointPairDimensionsRecipe(
    string fullRecipePath,
    string reportPath,
    string? expectedStatus,
    string? compareContractPath)
{
    var recipe = C3DPointPairDimensionsRecipe.Load(fullRecipePath);
    var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
    var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);
    var first = grid.ReadPoint(recipe.Step.First.Row, recipe.Step.First.Column);
    var second = grid.ReadPoint(recipe.Step.Second.Row, recipe.Step.Second.Column);
    var transform = recipe.Transform ?? ModelTransform.Identity;
    var evaluation = PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
        recipe.Step.SourceEntityId,
        ApplyModelTransform(first.Position, transform),
        ApplyModelTransform(second.Position, transform),
        first.RawValue,
        second.RawValue,
        recipe.Step.Acceptance,
        recipe.Step.Unit,
        recipe.Source.Unit));

    WritePointPairDimensionsReport(reportPath, fullRecipePath, sourcePath, recipe, grid, first, second, evaluation);
    if (compareContractPath is not null)
    {
        CompareUiContract(
            compareContractPath,
            evaluation.Result,
            "3D distance",
            "XZ planar width",
            "Elevation angle");
    }

    if (expectedStatus is not null
        && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || evaluation.Result.Status != status))
    {
        Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {evaluation.Result.Status}.");
        return 3;
    }

    Console.WriteLine($"{evaluation.Result.ToolName}: {evaluation.Result.Status}");
    return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
}

static int RunC3DGapFlushRecipe(
    string fullRecipePath,
    string reportPath,
    string? expectedStatus,
    string? compareContractPath)
{
    var recipe = C3DGapFlushRecipe.Load(fullRecipePath);
    var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
    var grid = C3DHeightGrid.Load(sourcePath, recipe.Step.MaxSampledPoints);
    var transform = recipe.Transform ?? ModelTransform.Identity;
    TryCalculateRoiStats(grid.Points, recipe.Step.LeftRegion, transform, out var left);
    TryCalculateRoiStats(grid.Points, recipe.Step.RightRegion, transform, out var right);
    var evaluation = GapFlushRule.Evaluate(new GapFlushInput(
        recipe.Step.SourceEntityId,
        recipe.Step.LeftRegion,
        recipe.Step.RightRegion,
        new GapFlushRegionStats(left.Count, left.RawMean, left.ModelYMean),
        new GapFlushRegionStats(right.Count, right.RawMean, right.ModelYMean),
        recipe.Step.Acceptance,
        recipe.Step.GapUnit,
        recipe.Step.FlushUnit));

    WriteGapFlushReport(reportPath, fullRecipePath, sourcePath, recipe, grid, evaluation);
    if (compareContractPath is not null)
    {
        CompareUiContract(compareContractPath, evaluation.Result, "Signed gap", "Signed flush");
    }

    if (expectedStatus is not null
        && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || evaluation.Result.Status != status))
    {
        Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {evaluation.Result.Status}.");
        return 3;
    }

    Console.WriteLine($"{evaluation.Result.ToolName}: {evaluation.Result.Status}");
    return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
}

static int RunLazTwoPointRecipe(
    string fullRecipePath,
    string reportPath,
    string? expectedStatus,
    string? compareContractPath)
{
    var recipe = LazTwoPointMeasurementRecipe.Load(fullRecipePath);
    var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
    var pointCloud = LazPointCloud.Load(sourcePath, recipe.Measurement.MaxSampledPoints);
    if (pointCloud.SampledPoints.Length < 2)
    {
        throw new InvalidDataException("LAZ/LAS two-point recipe requires at least two sampled points.");
    }

    var first = pointCloud.SampledPoints.MinBy(point => MapLazPosition(point.Position).X);
    var second = pointCloud.SampledPoints.MaxBy(point => MapLazPosition(point.Position).X);
    var result = CreateLazTwoPointResult(first, second, recipe.Measurement.HeightUnit, recipe.Source.EntityId, recipe.Acceptance);

    WriteLazTwoPointReport(reportPath, fullRecipePath, sourcePath, recipe, pointCloud, first, second, result);
    if (compareContractPath is not null)
    {
        CompareUiContract(compareContractPath, result);
    }

    if (expectedStatus is not null
        && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || result.Status != status))
    {
        Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {result.Status}.");
        return 3;
    }

    Console.WriteLine($"{result.ToolName}: {result.Status}");
    return result.Status == ResultStatus.Error ? 4 : 0;
}

static int RunLazProbe(string lazPath, string reportPath, int maxSampledPoints)
{
    try
    {
        var pointCloud = LazPointCloud.Load(lazPath, maxSampledPoints);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, [
            "LazPointCloudProbe",
            pointCloud.FormatContractLine(),
            $"Metadata|version={pointCloud.Metadata.Version}|rawPointFormat={pointCloud.Metadata.RawPointDataFormat}|recordLength={pointCloud.Metadata.PointDataRecordLength}|laszipVlr={pointCloud.Metadata.HasLaszipVlr}|pointOffset={pointCloud.Metadata.PointDataOffset}",
            $"Sample|first={(pointCloud.SampledPoints.Length == 0 ? "(none)" : FormatLazPoint(pointCloud.SampledPoints[0]))}"
        ]);
        Console.WriteLine(pointCloud.FormatContractLine());
        return pointCloud.BoundsMatch && pointCloud.HasRgb && pointCloud.SampledPoints.Length > 0 ? 0 : 5;
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentOutOfRangeException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static string? ReadOption(IReadOnlyList<string> args, string name)
{
    for (var i = 0; i < args.Count - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static int? ReadIntOption(IReadOnlyList<string> args, string name)
{
    var value = ReadOption(args, name);
    if (value is null)
    {
        return null;
    }

    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : throw new InvalidDataException($"{name} must be an integer.");
}

static string ReadRecipeType(string path)
{
    using var stream = File.OpenRead(path);
    using var document = JsonDocument.Parse(stream);
    return document.RootElement.TryGetProperty("recipeType", out var recipeType)
        ? recipeType.GetString() ?? throw new InvalidDataException($"Recipe type is empty: {path}")
        : throw new InvalidDataException($"Recipe type is missing: {path}");
}

static string ResolveRecipePath(string path, string recipeDirectory)
{
    return Path.GetFullPath(Path.IsPathRooted(path)
        ? path
        : Path.Combine(recipeDirectory, path));
}

static void WriteReport(
    string reportPath,
    string recipePath,
    string sourcePath,
    HeightDeviationRecipe recipe,
    C3DHeightGrid grid,
    ToolResult result,
    RoiStepReport? roiStepResult,
    PlaneFlatnessEvaluation? planeFlatnessResult,
    VolumeEvaluation? volumeResult)
{
    var transform = recipe.Transform ?? ModelTransform.Identity;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
    var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={grid.Min.ToString("F3", CultureInfo.InvariantCulture)}|max={grid.Max.ToString("F3", CultureInfo.InvariantCulture)}|mean={grid.Mean.ToString("F3", CultureInfo.InvariantCulture)}",
        InspectionContractText.FormatToolResult(result, includePrefix: true),
        "RecipeTransform",
        $"Transform|configured={recipe.Transform is not null}|tx={FormatNumber(transform.TranslateX)}|ty={FormatNumber(transform.TranslateY)}|tz={FormatNumber(transform.TranslateZ)}|rx={FormatNumber(transform.RotateXDegrees)}|ry={FormatNumber(transform.RotateYDegrees)}|rz={FormatNumber(transform.RotateZDegrees)}|scale={FormatNumber(transform.Scale)}",
        "RecipeRoiStep",
        recipe.RoiStep is null
            ? "RoiStep|configured=False"
            : $"RoiStep|configured=True|mode={recipe.RoiStep.Mode}|maxSampledPoints={recipe.RoiStep.MaxSampledPoints}|left={FormatRegion(recipe.RoiStep.Left)}|right={FormatRegion(recipe.RoiStep.Right)}",
    };

    if (recipe.PlaneFlatness is { } planeFlatness)
    {
        lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
            planeFlatness.Id,
            PlaneFlatnessRule.ToolName,
            planeFlatness.SourceEntityId,
            planeFlatness.ReferenceId,
            planeFlatness.Enabled)));
        lines.Add($"PlaneFlatnessStep|configured=True|id={planeFlatness.Id}|source={planeFlatness.SourceEntityId}|reference={planeFlatness.ReferenceId}|enabled={planeFlatness.Enabled}|roi={FormatRegion(planeFlatness.ReferenceRegion)}|tolerance={FormatNumber(planeFlatness.Tolerance)}|unit={planeFlatness.Unit}|maxSampledPoints={planeFlatness.MaxSampledPoints}");
    }
    else
    {
        lines.Add("PlaneFlatnessStep|configured=False");
    }

    if (recipe.Volume is { } volume)
    {
        lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
            volume.Id,
            VolumeRule.ToolName,
            volume.SourceEntityId,
            $"{volume.ReferenceId},{volume.MeasurementId}",
            volume.Enabled)));
        lines.Add($"VolumeStep|configured=True|id={volume.Id}|source={volume.SourceEntityId}|reference={volume.ReferenceId}|measurement={volume.MeasurementId}|referenceRegion={FormatRegion(volume.ReferenceRegion)}|measurementRegion={FormatRegion(volume.MeasurementRegion)}|expectedNet={FormatNumber(volume.ExpectedNetVolume)}|tolerance={FormatNumber(volume.Tolerance)}|unit={volume.Unit}|maxSampledPoints={volume.MaxSampledPoints}|enabled={volume.Enabled}");
    }
    else
    {
        lines.Add("VolumeStep|configured=False");
    }

    lines.Add(InspectionContractText.MetricsMarker);

    lines.AddRange(result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
    lines.Add(InspectionContractText.OverlaysMarker);
    lines.AddRange(result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
    if (roiStepResult is not null)
    {
        lines.Add("RoiStepResult");
        lines.Add(
            $"RoiStepResult|leftCount={roiStepResult.LeftCount}|rightCount={roiStepResult.RightCount}|leftMeanRaw={FormatNumber(roiStepResult.LeftRawMean)}|rightMeanRaw={FormatNumber(roiStepResult.RightRawMean)}|heightDeltaRaw={FormatNumber(roiStepResult.RawHeightDelta)}|modelDeltaY={FormatNumber(roiStepResult.ModelHeightDelta)}");
    }

    if (planeFlatnessResult is not null)
    {
        lines.Add($"PlaneFlatness|status={planeFlatnessResult.Result.Status}|referenceSamples={planeFlatnessResult.ReferenceSampleCount}|measurementSamples={planeFlatnessResult.MeasurementSampleCount}|minimum={FormatNumber(planeFlatnessResult.MinimumSignedDistance)}|maximum={FormatNumber(planeFlatnessResult.MaximumSignedDistance)}|flatness={FormatNumber(planeFlatnessResult.Flatness)}|rms={FormatNumber(planeFlatnessResult.RootMeanSquareDistance)}|summary={InspectionContractText.Clean(planeFlatnessResult.Result.Message)}");
    }

    if (volumeResult is not null)
    {
        lines.Add($"Volume|status={volumeResult.Result.Status}|above={FormatNumber(volumeResult.AboveVolume)}|below={FormatNumber(volumeResult.BelowVolume)}|net={FormatNumber(volumeResult.NetVolume)}|referenceSamples={volumeResult.ReferenceSampleCount}|measurementSamples={volumeResult.MeasurementSampleCount}|summary={InspectionContractText.Clean(volumeResult.Result.Message)}");
    }

    File.WriteAllLines(reportPath, lines);
}

static void WriteLazTwoPointReport(
    string reportPath,
    string recipePath,
    string sourcePath,
    LazTwoPointMeasurementRecipe recipe,
    LazPointCloud pointCloud,
    LazPointCloudPoint first,
    LazPointCloudPoint second,
    ToolResult result)
{
    var firstPosition = MapLazPosition(first.Position);
    var secondPosition = MapLazPosition(second.Position);
    var delta = secondPosition - firstPosition;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
    var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        pointCloud.FormatContractLine(),
        InspectionContractText.FormatToolResult(result, includePrefix: true),
        $"MeasurementSelection|selection={recipe.Measurement.Selection}|maxSampledPoints={recipe.Measurement.MaxSampledPoints}|heightUnit={recipe.Measurement.HeightUnit}",
        $"Acceptance|expectedDistance={FormatNumber(recipe.Acceptance.ExpectedDistance)}|distanceTolerance={FormatNumber(recipe.Acceptance.DistanceTolerance)}|expectedHeightDelta={FormatNumber(recipe.Acceptance.ExpectedHeightDelta)}|heightDeltaTolerance={FormatNumber(recipe.Acceptance.HeightDeltaTolerance)}",
        $"TwoPointResult|distance={FormatNumber(delta.Length())}|dx={FormatNumber(delta.X)}|dy={FormatNumber(delta.Y)}|dz={FormatNumber(delta.Z)}|heightDeltaRaw={FormatNumber(secondPosition.Y - firstPosition.Y)}|first={FormatLazPoint(first)}|second={FormatLazPoint(second)}",
        InspectionContractText.MetricsMarker
    };

    lines.AddRange(result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
    lines.Add(InspectionContractText.OverlaysMarker);
    lines.AddRange(result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));

    File.WriteAllLines(reportPath, lines);
}

static void WritePointPairDimensionsReport(
    string reportPath,
    string recipePath,
    string sourcePath,
    C3DPointPairDimensionsRecipe recipe,
    C3DHeightGrid grid,
    HeightGridPoint first,
    HeightGridPoint second,
    PointPairDimensionsEvaluation evaluation)
{
    var transform = recipe.Transform ?? ModelTransform.Identity;
    var step = recipe.Step;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
    var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={FormatNumber(grid.Min)}|max={FormatNumber(grid.Max)}|mean={FormatNumber(grid.Mean)}",
        "RecipeTransform",
        $"Transform|configured={recipe.Transform is not null}|tx={FormatNumber(transform.TranslateX)}|ty={FormatNumber(transform.TranslateY)}|tz={FormatNumber(transform.TranslateZ)}|rx={FormatNumber(transform.RotateXDegrees)}|ry={FormatNumber(transform.RotateYDegrees)}|rz={FormatNumber(transform.RotateZDegrees)}|scale={FormatNumber(transform.Scale)}",
        InspectionContractText.FormatInspectionStep(new InspectionStep(
            step.Id,
            PointPairDimensionsRule.ToolName,
            step.SourceEntityId,
            $"{step.First.Id},{step.Second.Id}",
            step.Enabled)),
        $"PointPairDimensionsStep|configured=True|id={step.Id}|source={step.SourceEntityId}|first={step.First.Id}@({step.First.Row},{step.First.Column})|second={step.Second.Id}@({step.Second.Row},{step.Second.Column})|enabled={step.Enabled}|expectedDistance={FormatNumber(step.Acceptance.ExpectedDistance)}|distanceTolerance={FormatNumber(step.Acceptance.DistanceTolerance)}|expectedWidth={FormatNumber(step.Acceptance.ExpectedWidth)}|widthTolerance={FormatNumber(step.Acceptance.WidthTolerance)}|expectedAngle={FormatNumber(step.Acceptance.ExpectedElevationAngleDegrees)}|angleTolerance={FormatNumber(step.Acceptance.ElevationAngleToleranceDegrees)}|unit={step.Unit}",
        InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true),
        $"PointPairDimensions|status={evaluation.Result.Status}|distance={FormatNumber(evaluation.Distance)}|width={FormatNumber(evaluation.PlanarWidth)}|angleDegrees={FormatNumber(evaluation.ElevationAngleDegrees)}|rawHeightDelta={FormatNumber(evaluation.RawHeightDelta)}|firstRaw={FormatNumber(first.RawValue)}|secondRaw={FormatNumber(second.RawValue)}",
        InspectionContractText.MetricsMarker
    };

    lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
    lines.Add(InspectionContractText.OverlaysMarker);
    lines.AddRange(evaluation.Result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
    File.WriteAllLines(reportPath, lines);
}

static void WriteGapFlushReport(
    string reportPath,
    string recipePath,
    string sourcePath,
    C3DGapFlushRecipe recipe,
    C3DHeightGrid grid,
    GapFlushEvaluation evaluation)
{
    var step = recipe.Step;
    var transform = recipe.Transform ?? ModelTransform.Identity;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
    var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={FormatNumber(grid.Min)}|max={FormatNumber(grid.Max)}|mean={FormatNumber(grid.Mean)}",
        "RecipeTransform",
        $"Transform|configured={recipe.Transform is not null}|tx={FormatNumber(transform.TranslateX)}|ty={FormatNumber(transform.TranslateY)}|tz={FormatNumber(transform.TranslateZ)}|rx={FormatNumber(transform.RotateXDegrees)}|ry={FormatNumber(transform.RotateYDegrees)}|rz={FormatNumber(transform.RotateZDegrees)}|scale={FormatNumber(transform.Scale)}",
        InspectionContractText.FormatInspectionStep(new InspectionStep(
            step.Id,
            GapFlushRule.ToolName,
            step.SourceEntityId,
            $"{step.LeftReferenceId},{step.RightReferenceId}",
            step.Enabled)),
        $"GapFlushStep|configured=True|id={step.Id}|source={step.SourceEntityId}|leftReference={step.LeftReferenceId}|rightReference={step.RightReferenceId}|left={FormatRegion(step.LeftRegion)}|right={FormatRegion(step.RightRegion)}|expectedGap={FormatNumber(step.Acceptance.ExpectedGap)}|gapTolerance={FormatNumber(step.Acceptance.GapTolerance)}|expectedFlush={FormatNumber(step.Acceptance.ExpectedFlush)}|flushTolerance={FormatNumber(step.Acceptance.FlushTolerance)}|gapUnit={step.GapUnit}|flushUnit={step.FlushUnit}|maxSampledPoints={step.MaxSampledPoints}|enabled={step.Enabled}",
        InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true),
        $"GapFlush|status={evaluation.Result.Status}|gap={FormatNumber(evaluation.SignedGap)}|flush={FormatNumber(evaluation.SignedFlush)}|modelFlush={FormatNumber(evaluation.ModelFlush)}|leftCount={evaluation.LeftPointCount}|rightCount={evaluation.RightPointCount}",
        InspectionContractText.MetricsMarker
    };
    lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
    lines.Add(InspectionContractText.OverlaysMarker);
    lines.AddRange(evaluation.Result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
    File.WriteAllLines(reportPath, lines);
}

static ToolResult CreateLazTwoPointResult(
    LazPointCloudPoint first,
    LazPointCloudPoint second,
    string heightUnit,
    string sourceEntityId,
    LazTwoPointMeasurementRecipeAcceptance acceptance)
{
    var firstPosition = MapLazPosition(first.Position);
    var secondPosition = MapLazPosition(second.Position);
    var delta = secondPosition - firstPosition;
    var distance = delta.Length();
    var heightDelta = secondPosition.Y - firstPosition.Y;
    var distanceStatus = IsWithinTolerance(distance, acceptance.ExpectedDistance, acceptance.DistanceTolerance)
        ? ResultStatus.Pass
        : ResultStatus.Fail;
    var heightStatus = IsWithinTolerance(heightDelta, acceptance.ExpectedHeightDelta, acceptance.HeightDeltaTolerance)
        ? ResultStatus.Pass
        : ResultStatus.Fail;
    var status = distanceStatus == ResultStatus.Pass && heightStatus == ResultStatus.Pass
        ? ResultStatus.Pass
        : ResultStatus.Fail;

    return new ToolResult(
        "LAZ/LAS Two Point Measurement",
        status,
        status == ResultStatus.Pass
            ? "Runner replay within configured tolerance; source point cloud is unchanged."
            : "Runner replay exceeds configured tolerance; source point cloud is unchanged.",
        TimeSpan.Zero,
        [
            new Metric("Distance", MetricKind.Length, distance, "model", distanceStatus),
            new Metric("Delta X", MetricKind.Length, delta.X, "model", ResultStatus.Pass),
            new Metric("Delta Y", MetricKind.Length, delta.Y, "model", ResultStatus.Pass),
            new Metric("Delta Z", MetricKind.Length, delta.Z, "model", ResultStatus.Pass),
            new Metric("Source Z height delta", MetricKind.Length, heightDelta, heightUnit, heightStatus)
        ],
        [
            new Overlay("overlay.laz-two-point-line", OverlayKind.Polyline, "LAZ/LAS two-point distance line", distanceStatus, sourceEntityId),
            new Overlay("overlay.laz-two-point-height-marker", OverlayKind.Marker, "LAZ/LAS source-Z height delta marker", heightStatus, sourceEntityId)
        ]);
}

static PlaneFlatnessEvaluation EvaluatePlaneFlatness(
    HeightDeviationRecipePlaneFlatness step,
    ModelTransform transform,
    C3DHeightGrid grid)
{
    var measurementSamples = grid.Points
        .Select(point => new HeightFieldPlaneSample(ApplyModelTransform(point.Position, transform), point.RawValue))
        .ToArray();
    var referenceSamples = measurementSamples
        .Where(sample => Contains(step.ReferenceRegion, sample.Position))
        .ToArray();
    return PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
        step.SourceEntityId,
        referenceSamples,
        measurementSamples,
        step.Tolerance,
        step.Unit));
}

static VolumeEvaluation EvaluateVolume(
    HeightDeviationRecipeVolume step,
    ModelTransform transform,
    C3DHeightGrid grid)
{
    var samples = grid.Points
        .Select(point => new HeightFieldPlaneSample(ApplyModelTransform(point.Position, transform), point.RawValue))
        .ToArray();
    var referenceSamples = samples.Where(sample => Contains(step.ReferenceRegion, sample.Position)).ToArray();
    var measurementSamples = samples.Where(sample => Contains(step.MeasurementRegion, sample.Position)).ToArray();
    var spacing = grid.HorizontalScale * grid.PointStride * transform.Scale;
    return VolumeRule.Evaluate(new VolumeRuleInput(
        step.SourceEntityId,
        referenceSamples,
        measurementSamples,
        spacing * spacing,
        step.ExpectedNetVolume,
        step.Tolerance,
        step.Unit));
}

static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
    point.X >= region.CenterX - region.HalfWidth
    && point.X <= region.CenterX + region.HalfWidth
    && point.Z >= region.CenterZ - region.HalfDepth
    && point.Z <= region.CenterZ + region.HalfDepth;

static RoiStepReport EvaluateRoiStep(HeightDeviationRecipeRoiStep roiStep, ModelTransform transform, C3DHeightGrid grid)
{
    if (!TryCalculateRoiStats(grid.Points, roiStep.Left, transform, out var left))
    {
        throw new InvalidDataException("ROI step found no C3D points in the left recipe region.");
    }

    if (!TryCalculateRoiStats(grid.Points, roiStep.Right, transform, out var right))
    {
        throw new InvalidDataException("ROI step found no C3D points in the right recipe region.");
    }

    return new RoiStepReport(
        left.Count,
        right.Count,
        left.RawMean,
        right.RawMean,
        right.RawMean - left.RawMean,
        right.ModelYMean - left.ModelYMean);
}

static bool TryCalculateRoiStats(
    IReadOnlyList<HeightGridPoint> points,
    HeightDeviationRecipeRoiRegion region,
    ModelTransform transform,
    out (int Count, double RawMean, double ModelYMean) stats)
{
    var minX = region.CenterX - region.HalfWidth;
    var maxX = region.CenterX + region.HalfWidth;
    var minZ = region.CenterZ - region.HalfDepth;
    var maxZ = region.CenterZ + region.HalfDepth;
    var count = 0;
    var rawSum = 0.0;
    var ySum = 0.0;

    foreach (var point in points)
    {
        var position = ApplyModelTransform(point.Position, transform);
        if (position.X < minX || position.X > maxX || position.Z < minZ || position.Z > maxZ)
        {
            continue;
        }

        count++;
        rawSum += point.RawValue;
        ySum += position.Y;
    }

    if (count == 0)
    {
        stats = default;
        return false;
    }

    var inverse = 1.0 / count;
    stats = (count, rawSum * inverse, ySum * inverse);
    return true;
}

static Vector3 ApplyModelTransform(Vector3 sourcePosition, ModelTransform transform)
{
    var position = sourcePosition * (float)transform.Scale;
    position = Vector3.Transform(position, Matrix4x4.CreateRotationX(ToRadians(transform.RotateXDegrees)));
    position = Vector3.Transform(position, Matrix4x4.CreateRotationY(ToRadians(transform.RotateYDegrees)));
    position = Vector3.Transform(position, Matrix4x4.CreateRotationZ(ToRadians(transform.RotateZDegrees)));
    return position + new Vector3((float)transform.TranslateX, (float)transform.TranslateY, (float)transform.TranslateZ);
}

static float ToRadians(double degrees) => (float)(degrees * Math.PI / 180.0);

static string FormatRegion(HeightDeviationRecipeRoiRegion region) =>
    string.Create(
        CultureInfo.InvariantCulture,
        $"cx={region.CenterX:F3},cz={region.CenterZ:F3},halfWidth={region.HalfWidth:F3},halfDepth={region.HalfDepth:F3}");

static string FormatNumber(double value) =>
    double.IsFinite(value) ? value.ToString("F3", CultureInfo.InvariantCulture) : "(pending)";

static bool IsWithinTolerance(double actual, double expected, double tolerance) =>
    Math.Abs(actual - expected) <= tolerance;

static Vector3 MapLazPosition(Vector3 source) =>
    new(source.X, source.Z, source.Y);

static string FormatLazPoint(LazPointCloudPoint point) =>
    string.Create(
        CultureInfo.InvariantCulture,
        $"x={point.Position.X:F3},y={point.Position.Y:F3},z={point.Position.Z:F3},rgb={point.Red},{point.Green},{point.Blue}");

static void CompareUiContract(string path, ToolResult result, params string[] metricNames)
{
    var lines = File.ReadAllLines(path);
    var toolResultLine = lines.FirstOrDefault(line => line.StartsWith($"{result.ToolName}|", StringComparison.Ordinal));
    if (toolResultLine is null)
    {
        throw new InvalidDataException($"UI contract has no {result.ToolName} result: {path}");
    }

    var parts = toolResultLine.Split('|');
    if (parts.Length < 2 || !parts[1].Equals(result.Status.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException($"UI status mismatch. UI={parts.ElementAtOrDefault(1) ?? "(missing)"}, runner={result.Status}.");
    }

    var metrics = metricNames.Length == 0
        ? [result.Metrics.First()]
        : metricNames.Select(name => result.Metrics.Single(metric => metric.Name == name)).ToArray();
    foreach (var runnerMetric in metrics)
    {
        var uiMetricLine = lines.FirstOrDefault(line => line.StartsWith($"{runnerMetric.Name}|", StringComparison.Ordinal));
        if (uiMetricLine is null)
        {
            throw new InvalidDataException($"UI contract has no {runnerMetric.Name} metric: {path}");
        }

        var uiMetric = ParseMetricValue(uiMetricLine);
        if (Math.Abs(uiMetric - runnerMetric.Value) > 0.001)
        {
            throw new InvalidDataException($"{runnerMetric.Name} mismatch. UI={uiMetric:F3}, runner={runnerMetric.Value:F3}.");
        }

        if (runnerMetric.Status is { } metricStatus)
        {
            var uiStatus = ParseMetricStatus(uiMetricLine);
            if (!uiStatus.Equals(metricStatus.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{runnerMetric.Name} status mismatch. UI={uiStatus}, runner={metricStatus}.");
            }
        }
    }
}

static double ParseMetricValue(string line)
{
    foreach (var part in line.Split('|'))
    {
        if (part.StartsWith("value=", StringComparison.Ordinal))
        {
            return double.Parse(part["value=".Length..], CultureInfo.InvariantCulture);
        }
    }

    throw new InvalidDataException($"Metric line has no value field: {line}");
}

static string ParseMetricStatus(string line)
{
    foreach (var part in line.Split('|'))
    {
        if (part.StartsWith("status=", StringComparison.Ordinal))
        {
            return part["status=".Length..];
        }
    }

    throw new InvalidDataException($"Metric line has no status field: {line}");
}

public sealed record RoiStepReport(
    int LeftCount,
    int RightCount,
    double LeftRawMean,
    double RightRawMean,
    double RawHeightDelta,
    double ModelHeightDelta);
