using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

return Run(args);

static int Run(string[] args)
{
    var recipePath = ReadOption(args, "--recipe");
    var reportPath = ReadOption(args, "--report");
    var expectedStatus = ReadOption(args, "--expect-status");

    if (recipePath is null || reportPath is null)
    {
        Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --recipe <path> --report <path> [--expect-status Pass|Fail|Warning|Error]");
        return 2;
    }

    try
    {
        var fullRecipePath = Path.GetFullPath(recipePath);
        var recipe = HeightDeviationRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);
        var result = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
            recipe.Source.EntityId,
            recipe.Source.Name,
            grid.Min,
            grid.Max,
            grid.Mean,
            grid.ValidSampleCount,
            recipe.Rule.PeakTolerance,
            recipe.Source.Unit));

        WriteReport(reportPath, fullRecipePath, sourcePath, recipe, grid, result);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {result.Status}.");
            return 3;
        }

        Console.WriteLine($"{result.ToolName}: {result.Status}");
        return result.Status == ResultStatus.Error ? 4 : 0;
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
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
    ToolResult result)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
    var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={grid.Min.ToString("F3", CultureInfo.InvariantCulture)}|max={grid.Max.ToString("F3", CultureInfo.InvariantCulture)}|mean={grid.Mean.ToString("F3", CultureInfo.InvariantCulture)}",
        $"ToolResult|{result.ToolName}|{result.Status}|elapsedMs={result.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}|metrics={result.Metrics.Count}|overlays={result.Overlays.Count}|message={result.Message}",
        "Metrics"
    };

    lines.AddRange(result.Metrics.Select(metric =>
        $"{metric.Name}|{metric.Kind}|value={metric.Value.ToString("F3", CultureInfo.InvariantCulture)}|unit={metric.Unit}|status={metric.Status?.ToString() ?? "(none)"}"));
    lines.Add("Overlays");
    lines.AddRange(result.Overlays.Select(overlay =>
        $"{overlay.Id}|{overlay.Kind}|label={overlay.Label}|status={overlay.Status?.ToString() ?? "(none)"}|source={overlay.SourceEntityId ?? "(none)"}"));

    File.WriteAllLines(reportPath, lines);
}
