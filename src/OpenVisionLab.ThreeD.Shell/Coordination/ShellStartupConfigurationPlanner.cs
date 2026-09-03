using System.Globalization;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

internal enum ShellStartupViewerView
{
    None,
    Top,
    Perspective
}

internal enum ShellStartupBottomPane
{
    None,
    FlowMap,
    Problems,
    RunRecord,
    ValidationSet,
    OutputCompare,
    DisplayedOutputs,
    SessionLog,
    Profile,
    FitDiagnostics,
    IntersectionEvidence,
    CorrespondenceEvidence
}

/// <summary>
/// Immutable, WPF-neutral startup intent parsed once by the Shell composition root.
/// MainWindow remains responsible for applying the intent to controls and ViewModels.
/// </summary>
internal sealed record ShellStartupConfigurationPlan
{
    public OpenVisionLanguage? RequestedLanguage { get; init; }
    public int EvidenceTabIndex { get; init; }
    public ShellWorkspaceMode? Workspace { get; init; }
    public ResultsWorkspaceSection? ResultsSection { get; init; }
    public ShellInspectionTask? InspectionTask { get; init; }
    public ShellWorkspaceMode? StageWorkspace { get; init; }
    public ShellStartupViewerView ViewerView { get; init; }
    public bool FitRoi { get; init; }
    public double? HeightColorMinimumRaw { get; init; }
    public double? HeightColorMaximumRaw { get; init; }
    public ShellStartupBottomPane BottomPane { get; init; }
    public string CompareSlotAArtifactId { get; init; } = string.Empty;
    public string CompareSlotBArtifactId { get; init; } = string.Empty;
    public string CompareSlotCArtifactId { get; init; } = string.Empty;
    public double? C3DSourceLoadProgress { get; init; }
    public bool IsAutomatedShellRun { get; init; }
    public bool ShouldStartWithEmptyRecipeInput { get; init; }
}

/// <summary>
/// Parses the stable subset of Shell startup flags that only describes initial
/// presentation and selection. It deliberately does not perform any UI or
/// inspection action, so the plan can be verified without constructing a Window.
/// </summary>
internal static class ShellStartupConfigurationPlanner
{
    public static ShellStartupConfigurationPlan Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var automated = IsAutomatedShellRun(args);
        return new ShellStartupConfigurationPlan
        {
            RequestedLanguage = ParseLanguage(GetValue(args, "--ui-language")),
            EvidenceTabIndex = ParseEvidenceTabIndex(GetValue(args, "--shell-evidence-tab")),
            Workspace = ParseEnum<ShellWorkspaceMode>(GetValue(args, "--shell-workspace")),
            ResultsSection = ParseEnum<ResultsWorkspaceSection>(
                GetValue(args, "--shell-results-section")),
            InspectionTask = ParseEnum<ShellInspectionTask>(GetValue(args, "--shell-task")),
            StageWorkspace = ParseStageWorkspace(GetValue(args, "--smoke-stage")),
            ViewerView = ParseViewerView(GetValue(args, "--smoke-view")),
            FitRoi = HasFlag(args, "--smoke-fit-roi"),
            HeightColorMinimumRaw = ParseInvariantDouble(
                GetValue(args, "--smoke-height-color-min")),
            HeightColorMaximumRaw = ParseInvariantDouble(
                GetValue(args, "--smoke-height-color-max")),
            BottomPane = ParseBottomPane(GetValue(args, "--workbench-bottom-pane")),
            CompareSlotAArtifactId = GetValue(args, "--workbench-compare-slot-a") ?? string.Empty,
            CompareSlotBArtifactId = GetValue(args, "--workbench-compare-slot-b") ?? string.Empty,
            CompareSlotCArtifactId = GetValue(args, "--workbench-compare-slot-c") ?? string.Empty,
            C3DSourceLoadProgress = ParseInvariantDouble(
                GetValue(args, "--smoke-c3d-load-progress")),
            IsAutomatedShellRun = automated,
            ShouldStartWithEmptyRecipeInput = !automated
                || HasFlag(args, "--smoke-input-first-start")
        };
    }

    private static string? GetValue(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return index + 1 < args.Count ? args[index + 1] : null;
            }
        }

        return null;
    }

    private static bool HasFlag(IReadOnlyList<string> args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static OpenVisionLanguage? ParseLanguage(string? value)
    {
        return value?.Trim() switch
        {
            { } language when language.Equals("ko", StringComparison.OrdinalIgnoreCase)
                || language.Equals("korean", StringComparison.OrdinalIgnoreCase)
                => OpenVisionLanguage.Korean,
            { } language when language.Equals("en", StringComparison.OrdinalIgnoreCase)
                || language.Equals("english", StringComparison.OrdinalIgnoreCase)
                => OpenVisionLanguage.English,
            _ => null
        };
    }

    private static int ParseEvidenceTabIndex(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "runner" or "runner-report" => 1,
        "snapshot" or "run" or "run-record" => 2,
        "steps" or "timeline" => 3,
        "history" => 4,
        _ => 0
    };

    private static T? ParseEnum<T>(string? value)
        where T : struct, Enum
    {
        return Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(T), parsed)
            ? parsed
            : null;
    }

    private static ShellWorkspaceMode? ParseStageWorkspace(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "setup" => ShellWorkspaceMode.Workbench,
            "teach" => ShellWorkspaceMode.Teach,
            "validate" => ShellWorkspaceMode.Inspect,
            "results" => ShellWorkspaceMode.Review,
            _ => null
        };

    private static ShellStartupViewerView ParseViewerView(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "top" => ShellStartupViewerView.Top,
            "perspective" => ShellStartupViewerView.Perspective,
            _ => ShellStartupViewerView.None
        };

    private static double? ParseInvariantDouble(string? value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static ShellStartupBottomPane ParseBottomPane(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "flow" or "flow-map" => ShellStartupBottomPane.FlowMap,
            "problems" or "flow-problems" => ShellStartupBottomPane.Problems,
            "run-record" or "record" or "execution-record" => ShellStartupBottomPane.RunRecord,
            "validation-set" or "repeat-validation" => ShellStartupBottomPane.ValidationSet,
            "compare" or "output-compare" => ShellStartupBottomPane.OutputCompare,
            "outputs" or "displayed-outputs" => ShellStartupBottomPane.DisplayedOutputs,
            "session" or "session-log" => ShellStartupBottomPane.SessionLog,
            "profile" or "height-profile" => ShellStartupBottomPane.Profile,
            "fit" or "fit-diagnostics" => ShellStartupBottomPane.FitDiagnostics,
            "intersection" or "intersection-evidence" => ShellStartupBottomPane.IntersectionEvidence,
            "correspondence" or "correspondence-evidence" => ShellStartupBottomPane.CorrespondenceEvidence,
            _ => ShellStartupBottomPane.None
        };

    private static bool IsAutomatedShellRun(IReadOnlyList<string> args) => args.Any(argument =>
        argument.StartsWith("--smoke-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--two-point-line-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--three-point-plane-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--datum-plane-deviation-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--line-intersection-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--landmark-correspondence-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--xyz-affine-solve-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--xyz-affine-apply-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--regrid-height-map-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--message-dialog-", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("--shell-smoke-screenshot", StringComparison.OrdinalIgnoreCase));
}
