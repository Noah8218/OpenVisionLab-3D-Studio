using System.IO;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record ValidationSetSampleRow(
    int Order,
    string SourcePath,
    ToolRecipeValidationSampleRole Role,
    string Status,
    string StatusText,
    string Message,
    string Duration,
    IReadOnlyList<ValidationSetStepRow> Steps)
{
    public string FileName => Path.GetFileName(SourcePath);
    public string RoleText => Role == ToolRecipeValidationSampleRole.HeldOut
        ? "Held-out"
        : Role.ToString();
}

public sealed record ValidationEvidenceDistributionRow(
    string Scope,
    string OwnerId,
    string OwnerName,
    string MetricName,
    string Unit,
    string Good,
    string Bad,
    string HeldOut);

public sealed record ValidationThresholdCandidateRow(
    string CandidateId,
    string Scope,
    string OwnerName,
    string MetricName,
    string Unit,
    string LimitKind,
    string Limits,
    int CorrectCount,
    int ErrorCount,
    int FalseAcceptCount,
    int FalseRejectCount,
    ToolRecipeThresholdCandidate Candidate);

public sealed record ValidationThresholdDecisionRow(
    int SampleOrder,
    string FileName,
    string SampleIdentity,
    string ExpectedRole,
    string PredictedRole,
    string Decision,
    string Value,
    string EvidenceLocator);

public sealed record ValidationThresholdParameterChangeRow(
    string ParameterName,
    string BeforeValue,
    string ProposedValue,
    string ManualValue = "");

public sealed record ValidationThresholdDevelopmentSampleRow(
    string Stage,
    int SampleOrder,
    string FileName,
    string SampleIdentity,
    string Role,
    string Status,
    string ExpectedMatch,
    string Metrics);

public sealed record ValidationThresholdHeldOutSampleRow(
    int SampleOrder,
    string FileName,
    string SampleIdentity,
    string Status,
    string Metrics);

public sealed record ValidationSetStepRow(
    int Order,
    string StepId,
    string ToolName,
    string Status,
    string StatusText,
    string Evidence,
    IReadOnlyList<ValidationSetMetricRow> Metrics,
    IReadOnlyList<ValidationSetOverlayRow> Overlays);

public sealed record ValidationSetMetricRow(
    string Name,
    string Value,
    string Unit,
    string Status,
    string StatusText);

public sealed record ValidationSetOverlayRow(
    string Kind,
    string Label,
    string Status,
    string StatusText);

public sealed record ValidationFailureCorrectionContext(
    string SourcePath,
    string SampleName,
    string SampleStatus,
    string StepId,
    string ToolName,
    string Reason,
    string CellSummary);

public enum ValidationSetStatusFilter
{
    All,
    Pass,
    Fail,
    Error
}

public enum ValidationThresholdAssistantStage
{
    Analyze,
    Propose,
    Review,
    Apply
}
