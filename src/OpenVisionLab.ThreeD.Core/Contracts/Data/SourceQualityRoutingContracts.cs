using System.Globalization;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Stable reason codes for the source-quality gate used by product routing.
/// The gate is deliberately narrower than source identity readiness: tools that
/// own their own selection or typed-artifact contract remain routable when they
/// do not require a complete source grid.
/// </summary>
public enum SourceQualityToolGateReason
{
    Allowed,
    NotApplicable,
    ReportUnavailable,
    ReportError,
    MissingDiagnostics,
    GridDiagnosticsError,
    NoValidSamples,
    SourceIdentityMismatch
}

public sealed record SourceQualityToolGateResult(
    bool IsAllowed,
    SourceQualityToolGateReason Reason,
    string Detail)
{
    public bool IsBlocked => !IsAllowed;
}

public static class SourceQualityToolGate
{
    private static readonly HashSet<string> GridQualityTools = new(
        [
            "filter",
            "level-surface",
            "remove-outlier-pixels",
            "roi-crop",
            "completeness-grid"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool RequiresSourceQuality(string? toolId) =>
        toolId is not null && GridQualityTools.Contains(toolId);

    public static SourceQualityToolGateResult Evaluate(
        string? toolId,
        string? inputContract,
        SourceQualityReport? report,
        string? reportError = null,
        string? expectedSourceEntityId = null,
        string? expectedSourceContentSha256 = null)
    {
        if (!RequiresSourceQuality(toolId)
            || !IsRawSourceContract(inputContract))
        {
            return new(
                true,
                SourceQualityToolGateReason.NotApplicable,
                "Source Quality gate is not required for this typed input route.");
        }

        if (report is null)
        {
            return string.IsNullOrWhiteSpace(reportError)
                ? new(
                    false,
                    SourceQualityToolGateReason.ReportUnavailable,
                    "Source Quality is unavailable; this source route is blocked until the current report is ready.")
                : new(
                    false,
                    SourceQualityToolGateReason.ReportError,
                    $"Source Quality failed closed: {reportError.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(expectedSourceEntityId)
            && !string.Equals(
                expectedSourceEntityId,
                report.Source.EntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(
                false,
                SourceQualityToolGateReason.SourceIdentityMismatch,
                $"Source Quality belongs to source '{report.Source.EntityId}', not the current source '{expectedSourceEntityId}'. Reload Source Quality before routing.");
        }

        if (!string.IsNullOrWhiteSpace(expectedSourceContentSha256)
            && !string.Equals(
                expectedSourceContentSha256,
                report.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(
                false,
                SourceQualityToolGateReason.SourceIdentityMismatch,
                "Source Quality content identity does not match the current source binding. Reload Source Quality before routing.");
        }

        if (report.GridDiagnostics is null)
        {
            return new(
                false,
                SourceQualityToolGateReason.MissingDiagnostics,
                "Source Quality has no current grid diagnostics; this source route is blocked until diagnostics are available.");
        }

        var failedCheck = report.GridDiagnostics.Checks.FirstOrDefault(check =>
            check.State == SourceQualityGridDiagnosticState.Error);
        if (report.GridDiagnostics.State == SourceQualityGridDiagnosticState.Error
            || failedCheck is not null)
        {
            var detail = failedCheck is null
                ? "Source Quality grid diagnostics failed."
                : $"Source Quality grid diagnostics failed at {failedCheck.Code}: {failedCheck.Message}";
            return new(
                false,
                SourceQualityToolGateReason.GridDiagnosticsError,
                detail);
        }

        if (report.Coverage.ValidSampleCount <= 0)
        {
            return new(
                false,
                SourceQualityToolGateReason.NoValidSamples,
                "Source Quality reports no valid height samples; this source route is blocked.");
        }

        return new(
            true,
            SourceQualityToolGateReason.Allowed,
            report.Coverage.MissingSampleCount > 0
                ? "Source Quality is valid; the tool's declared missing-sample policy remains responsible for the missing mask."
                : "Source Quality is valid for this source route.");
    }

    private static bool IsRawSourceContract(string? inputContract) =>
        string.Equals(inputContract, "SourceC3D / RawHeightField", StringComparison.Ordinal)
        || string.Equals(inputContract, "RawHeightField", StringComparison.Ordinal);
}

/// <summary>
/// Numeric before/after evidence for one derived height-field preparation
/// artifact. Outlier evidence is nullable because most preparation tools do not
/// classify outliers; null is reported as not evaluated rather than fabricated as
/// zero.
/// </summary>
public sealed record SourceQualityDelta(
    string SourceEntityId,
    string SourceContentSha256,
    string DerivedEntityId,
    string DerivedContentSha256,
    string SourceRootSourceSha256,
    string DerivedRootSourceSha256,
    long BeforeValidSampleCount,
    long AfterValidSampleCount,
    long BeforeMissingSampleCount,
    long AfterMissingSampleCount,
    long? DetectedOutlierCount,
    string OutlierEvidence)
{
    public long ValidSampleDelta => AfterValidSampleCount - BeforeValidSampleCount;
    public long MissingSampleDelta => AfterMissingSampleCount - BeforeMissingSampleCount;

    public bool SourceIdentityRetained =>
        string.Equals(SourceRootSourceSha256, DerivedRootSourceSha256, StringComparison.OrdinalIgnoreCase);

    public string Summary => string.Create(
        CultureInfo.InvariantCulture,
        $"quality delta | valid {FormatSigned(ValidSampleDelta)} | missing {FormatSigned(MissingSampleDelta)} | outliers {FormatOutliers()}");

    private string FormatOutliers() => DetectedOutlierCount is { } count
        ? count.ToString("N0", CultureInfo.InvariantCulture)
        : OutlierEvidence;

    private static string FormatSigned(long value) => value switch
    {
        > 0 => $"+{value.ToString("N0", CultureInfo.InvariantCulture)}",
        < 0 => value.ToString("N0", CultureInfo.InvariantCulture),
        _ => "0"
    };
}
