using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record ValidationSetSampleRow(
    int Order,
    string SourcePath,
    ToolRecipeValidationSampleRole Role,
    string Status,
    string StatusText,
    string Message,
    string Duration,
    IReadOnlyList<ValidationSetStepRow> Steps,
    IReadOnlyList<ValidationRecipeStepSnapshot>? RecipeSteps = null)
{
    public ToolRecipeValidationExpectation? Expectation { get; init; }
    public ToolRecipeValidationUsage? Usage { get; init; }
    public ToolRecipeValidationExpectation EffectiveExpectation => ToolRecipeValidationSampleDefinition.ResolveExpectation(Role, Expectation);
    public ToolRecipeValidationUsage EffectiveUsage => ToolRecipeValidationSampleDefinition.ResolveUsage(Role, Usage);
    public ToolRecipeValidationSampleRole DevelopmentRole => ToolRecipeValidationSampleDefinition.ResolveDevelopmentRole(EffectiveExpectation, EffectiveUsage);
    public bool HasExpectation => EffectiveExpectation != ToolRecipeValidationExpectation.Unset;
    public bool IsUnset => !HasExpectation;
    public bool IsAdjustment => EffectiveUsage == ToolRecipeValidationUsage.Adjustment;
    public ValidationSampleRoleChange UnsetRoleChange => new(this, ToolRecipeValidationSampleRole.HeldOut) { Value = "Unset" };
    public string FileName => Path.GetFileName(SourcePath);
    public string DescriptionText => string.IsNullOrWhiteSpace(Duration) ? Message : $"{Duration} · {Message}";
    public bool IsGood => EffectiveExpectation == ToolRecipeValidationExpectation.Ok;
    public bool IsBad => EffectiveExpectation == ToolRecipeValidationExpectation.Ng;
    public bool IsHeldOut => EffectiveUsage == ToolRecipeValidationUsage.HeldOut;
    public ValidationSampleRoleChange GoodRoleChange => new(this, ToolRecipeValidationSampleRole.Good);
    public ValidationSampleRoleChange BadRoleChange => new(this, ToolRecipeValidationSampleRole.Bad);
    public ValidationSampleRoleChange HeldOutRoleChange => new(this, ToolRecipeValidationSampleRole.HeldOut);
    public string RoleText => Role == ToolRecipeValidationSampleRole.HeldOut
        ? "Held-out"
        : Role.ToString();
    public string ActualVerdictText => Status switch { "Pass" => "OK", "Fail" => "NG", _ => StatusText };
    public string ExpectedVerdictText => EffectiveExpectation switch
    {
        ToolRecipeValidationExpectation.Ok => "OK", ToolRecipeValidationExpectation.Ng => "NG",
        _ => ThreeDLocalization.Shared.Resolve("ThreeD.Validation.Unset", "미지정", "Unset")
    };
    public string Agreement => Status == "Error" ? "Error" : !HasExpectation ? "Unset" : Status switch
    {
        "Pending" => "Pending",
        "Error" => "Error",
        "Warning" => "Warning",
        "Pass" when IsBad => "FalseAccept",
        "Fail" when IsGood => "FalseReject",
        "Pass" or "Fail" => "Match",
        _ => "HeldOut"
    };
    public bool IsMismatch => HasExpectation && Agreement is ("FalseAccept" or "FalseReject" or "Error");
    public ValidationSetStepRow? MismatchStep => Steps.FirstOrDefault(step => step.Status == "Error")
        ?? Steps.FirstOrDefault(step => step.Status == "Fail");
    public ValidationSetStepRow? ReviewOutputStep => MismatchStep
        ?? Steps.FirstOrDefault(step => step.Metrics.Any(metric => !string.IsNullOrWhiteSpace(metric.Status)));
    public ValidationSetMetricRow? ReviewOutputMetric => ReviewOutputStep?.Metrics.FirstOrDefault(metric => metric.Status == "Error")
        ?? ReviewOutputStep?.Metrics.FirstOrDefault(metric => metric.Status == "Fail")
        ?? ReviewOutputStep?.Metrics.FirstOrDefault(metric => !string.IsNullOrWhiteSpace(metric.Status));
    public bool HasReviewOutput => ReviewOutputMetric is not null;
    public string MismatchEvidenceText
    {
        get
        {
            var l = ThreeDLocalization.Shared;
            if (Agreement == "FalseAccept" && (!Steps.SelectMany(step => step.Metrics).Any(metric => !string.IsNullOrWhiteSpace(metric.Status))
                || Steps.SelectMany(step => step.Metrics).Any(metric => !string.IsNullOrWhiteSpace(metric.Status) && metric.Status != "Pass")))
                return l.Resolve("ThreeD.Validation.MissingPassEvidence", "실행 결과는 OK이지만 통과한 판정 출력의 기록이 충분하지 않습니다. 확정된 원인 도구는 없습니다.", "The run result is OK, but recorded passing judged outputs are insufficient. No cause tool is established.");
            if (Agreement == "FalseAccept") return l.Resolve("ThreeD.Validation.MissedEvidence", "기록된 판정 출력은 통과했지만 기대 판정은 NG입니다. 확정된 원인 도구는 없습니다.", "Recorded judged outputs passed, but the expected decision is NG. No cause tool is established.");
            if (MismatchStep is not { } step) return l.Resolve("ThreeD.Validation.MissingEvidence", "기록된 문제 단계 없음", "No recorded issue step") + " · " + Message;
            var metric = ReviewOutputMetric;
            var lower = step.Metrics.FirstOrDefault(item => item.Name == "LowerLimit" && item.Unit == metric?.Unit);
            var upper = step.Metrics.FirstOrDefault(item => item.Name == "UpperLimit" && item.Unit == metric?.Unit);
            var value = metric is null ? l.Resolve("ThreeD.Validation.NoRecordedValue", "기록된 판정 값 없음", "No recorded judged value") : $"{metric.Name} {metric.Value} {metric.Unit} · {metric.StatusText}"
                + (metric.Status is "Error" or "Fail" ? "" : " · " + l.Resolve("ThreeD.Validation.ReviewOutput", "기록된 검토 출력", "Recorded output for review"));
            var range = lower is null && upper is null ? l.Resolve("ThreeD.Validation.NoRecordedRange", "기록된 허용 범위 없음", "No recorded limits") : l.Resolve("ThreeD.Validation.RecordedRange", "실행 허용", "Executed limits") + $" {lower?.Value ?? "—"} – {upper?.Value ?? "—"} {metric?.Unit}";
            return $"{step.ToolName} · {step.StatusText} · {value} · {range}\n{step.Evidence}";
        }
    }
    public string MismatchGuidance => Agreement switch
    {
        "FalseReject" => ThreeDLocalization.Shared.Resolve("ThreeD.Validation.RejectGuidance", "• 공정 편차에 비해 허용 범위가 좁은지 검토하세요.\n• 영역이 경계나 경사면을 포함하는지 확인하세요.", "• Review whether the limits are too narrow for process variation.\n• Check whether the region includes an edge or slope."),
        "FalseAccept" => ThreeDLocalization.Shared.Resolve("ThreeD.Validation.AcceptGuidance", "• 국부 결함이 평균에 가려지는지 영역과 측정 방식을 검토하세요.\n• 실제 판정 출력과 최소·최대 측정값을 확인하세요.", "• Review regions and measurement methods for local defects hidden by averaging.\n• Check recorded judged outputs and minimum/maximum measurements."),
        _ => ThreeDLocalization.Shared.Resolve("ThreeD.Validation.ErrorGuidance", "• 입력과 영역 위치를 확인하세요.\n• 실행 오류는 OK로 처리하지 않습니다.", "• Check the input and region location.\n• Execution errors are not treated as OK.")
    };
    public string EvidenceSummary => string.Join(" · ", Steps.Where(step => step.Status is "Fail" or "Error").Select(step => $"{step.ToolName}: {step.Evidence}"));
}

public sealed class ValidationSampleRoleChange(ValidationSetSampleRow sample, ToolRecipeValidationSampleRole role)
{
    public ValidationSetSampleRow Sample { get; } = sample;
    public ToolRecipeValidationSampleRole Role { get; } = role;
    public string Value { get; init; } = role.ToString();
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
    IReadOnlyList<ValidationSetOverlayRow> Overlays)
{
    public HeightMeasurementColorSnapshot? ColorSnapshot { get; init; }
}

public sealed record ValidationSetMetricRow(
    string Name,
    string Value,
    string Unit,
    string Status,
    string StatusText,
    double? NumericValue = null);

public sealed record ValidationRecipeStepSnapshot(string StepId, string ToolName, ToolRecipeStepGroup? Group, bool OutputEnabled);

public sealed record ValidationTableColumn(string Header, ValidationMatrixColumn? Metric, IReadOnlyList<ValidationRecipeStepSnapshot>? Members = null);

public sealed record ValidationGroupMemberCell(string ToolName, string Status, string Detail);

public sealed record ValidationMatrixColumn(string StepId, string ToolName, string MetricName, string Unit, int StepOrder = 0);

public sealed record ValidationDistributionPoint(ValidationSetSampleRow Sample, double Value, string ValueText, string Status);

public sealed record ValidationDistributionOutput(ValidationMatrixColumn Column, IReadOnlyList<ValidationDistributionPoint> Points, double? Minimum, double? Maximum)
{
    public string Label => $"{Column.StepOrder:00} {Column.ToolName} · {Column.MetricName}";
    public string RangeText => Minimum is null && Maximum is null ? "—" : $"{Minimum?.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) ?? "−∞"} – {Maximum?.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) ?? "+∞"} {Column.Unit}";
}

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
    Error,
    Mismatch,
    FalseReject,
    FalseAccept,
    HeldOut
}

public enum ValidationThresholdAssistantStage
{
    Analyze,
    Propose,
    Review,
    Apply
}
