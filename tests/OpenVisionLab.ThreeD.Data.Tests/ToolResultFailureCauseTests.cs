using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

/// <summary>
/// Fail 사유 문구가 결과에 이미 들어 있는 지표와 한계 값만 풀어 쓰는지 고정한다.
/// 판정(상태)을 바꾸거나 새로 만들지 않아야 한다.
/// </summary>
public sealed class ToolResultFailureCauseTests
{
    private static ToolResult Result(ResultStatus status, params Metric[] metrics) =>
        new("test", status, "message", TimeSpan.Zero, metrics, []);

    private static Metric Dev(string name, double value, string unit = "raw-height") =>
        new(name, MetricKind.Deviation, value, unit);

    private static Metric Count(string name, double value) => new(name, MetricKind.Count, value, "count");

    [Fact]
    public void Warpage_names_each_exceeded_limit()
    {
        var both = Result(ResultStatus.Fail,
            Dev("PeakToValley", 7.92969), Dev("Rms", 2.11015),
            Dev("MaximumPeakToValley", 5), Dev("MaximumRms", 2));
        Assert.Equal(
            "PeakToValley 7.92969 > MaximumPeakToValley 5 raw-height; Rms 2.11015 > MaximumRms 2 raw-height",
            ToolResultFailureCause.Describe(both));

        var rmsOnly = Result(ResultStatus.Fail,
            Dev("PeakToValley", 4), Dev("Rms", 2.5),
            Dev("MaximumPeakToValley", 5), Dev("MaximumRms", 2));
        Assert.Equal("Rms 2.5 > MaximumRms 2 raw-height", ToolResultFailureCause.Describe(rmsOnly));
    }

    [Fact]
    public void Thickness_reports_range_and_sample_counts()
    {
        var result = Result(ResultStatus.Fail,
            Dev("Mean", 8), Dev("Minimum", 7.636), Dev("Maximum", 8.401),
            Dev("LowerLimit", 7.75), Dev("UpperLimit", 8.25),
            Count("BelowLowerLimitCount", 102), Count("AboveUpperLimitCount", 79));
        Assert.Equal(
            "Minimum 7.636 < LowerLimit 7.75 raw-height (102 sample(s) below); Maximum 8.401 > UpperLimit 8.25 raw-height (79 sample(s) above)",
            ToolResultFailureCause.Describe(result));
    }

    [Fact]
    public void Count_only_excess_does_not_print_a_false_comparison()
    {
        var result = Result(ResultStatus.Fail,
            Dev("Minimum", 7.8), Dev("Maximum", 8.2),
            Dev("LowerLimit", 7.75), Dev("UpperLimit", 8.25),
            Count("BelowLowerLimitCount", 3), Count("AboveUpperLimitCount", 0));
        var text = ToolResultFailureCause.Describe(result);
        Assert.Equal("3 sample(s) below LowerLimit 7.75 raw-height", text);
        Assert.DoesNotContain("Minimum 7.8 <", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_that_print_alike_use_a_round_trip_format()
    {
        var result = Result(ResultStatus.Fail, Dev("PeakToValley", 5.0000004), Dev("MaximumPeakToValley", 5));
        var text = ToolResultFailureCause.Describe(result);
        Assert.DoesNotContain("PeakToValley 5 >", text, StringComparison.Ordinal);
        Assert.Contains("5.0000004", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_fail_results_with_matching_limits_get_a_cause()
    {
        var metrics = new[] { Dev("PeakToValley", 9), Dev("MaximumPeakToValley", 5) };
        Assert.Equal(string.Empty, ToolResultFailureCause.Describe(Result(ResultStatus.Pass, metrics)));
        Assert.Equal(string.Empty, ToolResultFailureCause.Describe(Result(ResultStatus.Error, metrics)));
        Assert.Equal(string.Empty, ToolResultFailureCause.Describe(Result(ResultStatus.Warning, metrics)));
        // Plane Flatness 등 이름 규칙과 맞지 않는 한계 지표는 사유를 만들지 않는다.
        Assert.Equal(string.Empty, ToolResultFailureCause.Describe(
            Result(ResultStatus.Fail, Dev("Flatness", 0.3), Dev("Flatness tolerance", 0.1))));
        Assert.Equal(string.Empty, ToolResultFailureCause.Describe(
            Result(ResultStatus.Fail, Dev("PeakToValley", double.NaN), Dev("MaximumPeakToValley", 5))));
    }
}
