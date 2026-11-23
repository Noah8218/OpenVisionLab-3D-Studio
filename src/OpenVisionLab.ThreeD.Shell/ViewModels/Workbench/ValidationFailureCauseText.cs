using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// 검증 샘플이 NG(Fail)일 때 어느 도구의 어떤 출력이 허용 범위를 넘었는지 한 줄로 만든다.
/// 새로 계산하지 않고 도구가 이미 낸 지표(값, 상한/하한 지표, 지표별 상태)를 비교해 문구만 만든다.
/// 상한 비교 규칙은 결과 표의 상태 툴팁(<see cref="TeachingMeasurementResultItem.OutputStatusReason"/>)과 같다.
/// </summary>
internal static class ValidationFailureCauseText
{
    /// <summary>
    /// 예: <c>Warpage: P–V 7.93 > 5 · RMS 2.11 > 2</c>, <c>Thickness: 두께 694.13 &lt; 700</c>.
    /// 한계 비교로 설명할 수 없는 실패(유효 샘플 부족, 오류 등)는 빈 문자열을 돌려주고, 호출한 쪽이 기존 문구를 쓴다.
    /// </summary>
    public static string Describe(IEnumerable<ToolRecipeValidationStepResult> steps)
    {
        var parts = new List<string>();
        foreach (var step in steps)
        {
            if (step.Status != ResultStatus.Fail) continue;
            var causes = DescribeStep(step.Metrics);
            if (causes.Count > 0) parts.Add($"{step.ToolName}: {string.Join(" · ", causes)}");
        }

        return string.Join("; ", parts);
    }

    private static List<string> DescribeStep(IReadOnlyList<Metric> metrics)
    {
        var causes = new List<string>();
        var lower = metrics.FirstOrDefault(metric => metric.Name == "LowerLimit" && double.IsFinite(metric.Value));
        var upper = metrics.FirstOrDefault(metric => metric.Name == "UpperLimit" && double.IsFinite(metric.Value));
        var statusCauseAdded = false;
        foreach (var metric in metrics)
        {
            if (!double.IsFinite(metric.Value)) continue;

            // 휨처럼 지표마다 "Maximum<이름>" 상한 지표가 따로 있는 도구.
            if (metrics.FirstOrDefault(max => max.Name == "Maximum" + metric.Name && double.IsFinite(max.Value)) is { } maximum)
            {
                if (metric.Value > maximum.Value) causes.Add($"{DisplayName(metric.Name)} {Format(metric.Value)} > {Format(maximum.Value)}");
                continue;
            }

            // 두께처럼 하한·상한 지표 하나씩과 값 지표의 지표별 상태(Fail)로 판정하는 도구.
            // 평균·최소·최대가 같은 한계를 함께 넘는 경우가 많아, 지표 순서상 처음 넘은 하나만 적는다(두께는 평균이 먼저 온다).
            if (!statusCauseAdded && metric.Status == ResultStatus.Fail)
            {
                if (upper is not null && metric.Value > upper.Value) causes.Add($"{DisplayName(metric.Name)} {Format(metric.Value)} > {Format(upper.Value)}");
                else if (lower is not null && metric.Value < lower.Value) causes.Add($"{DisplayName(metric.Name)} {Format(metric.Value)} < {Format(lower.Value)}");
                statusCauseAdded = causes.Count > 0;
            }
        }

        return causes;
    }

    private static string DisplayName(string metricName) => metricName switch
    {
        "PeakToValley" => "P–V",
        "Rms" => "RMS",
        "Mean" => ThreeDLocalization.Shared.Resolve("ThreeD.Results.ThicknessOutput", "두께", "Thickness"),
        _ => metricName
    };

    // 결과 표와 같은 표시 정밀도(소수 둘째 자리까지). 계산 값은 바꾸지 않는다.
    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
