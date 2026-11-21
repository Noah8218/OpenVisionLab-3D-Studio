using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Reporting.RunRecords;

/// <summary>
/// Fail 판정 결과에서 어떤 한계를 넘었는지를 리포트용 한 줄로 설명한다.
/// 판정 자체는 각 도구(SDK)가 내린 결과를 그대로 쓰며, 여기서는 이미 결과에 들어 있는
/// 지표 값과 한계 값을 비교해 사유만 풀어 쓴다. 판정이나 저장된 지표 상태는 바꾸지 않는다.
/// </summary>
/// <remarks>
/// 한계 지표는 이름 규칙으로 찾는다: 값 지표 <c>X</c>의 상한은 <c>MaximumX</c>,
/// 두께류의 <c>Minimum</c>/<c>Maximum</c>은 <c>LowerLimit</c>/<c>UpperLimit</c>과 짝이다.
/// 규칙에 맞는 한계 지표가 없는 도구(예: Plane Flatness, Gap/Flush 등)와
/// 한계 지표 비교로 설명되지 않는 실패(예: 유효 비율 부족)는 사유를 만들지 않는다.
/// </remarks>
public static class ToolResultFailureCause
{
    public static string Describe(ToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status != ResultStatus.Fail)
        {
            return string.Empty;
        }

        var causes = new List<string>();
        foreach (var metric in result.Metrics)
        {
            if (!double.IsFinite(metric.Value))
            {
                continue;
            }

            if (Find(result, "Maximum" + metric.Name) is { } upper
                && double.IsFinite(upper.Value)
                && metric.Value > upper.Value)
            {
                var (actual, limit) = FormatPair(metric.Value, upper.Value);
                causes.Add(Join($"{metric.Name} {actual} > {upper.Name} {limit}", metric.Unit));
            }
        }

        AddRangeCause(result, "Minimum", "LowerLimit", "BelowLowerLimitCount", below: true, causes);
        AddRangeCause(result, "Maximum", "UpperLimit", "AboveUpperLimitCount", below: false, causes);
        return string.Join("; ", causes);
    }

    private static void AddRangeCause(
        ToolResult result,
        string valueName,
        string limitName,
        string countName,
        bool below,
        List<string> causes)
    {
        var value = Find(result, valueName);
        var limit = Find(result, limitName);
        var count = Find(result, countName);
        if (value is null || limit is null || !double.IsFinite(value.Value) || !double.IsFinite(limit.Value))
        {
            return;
        }

        var outside = below ? value.Value < limit.Value : value.Value > limit.Value;
        var hasCount = count is { } c && double.IsFinite(c.Value) && c.Value > 0;
        if (!outside && !hasCount)
        {
            return;
        }

        var samples = hasCount
            ? $"{Format(count!.Value)} sample(s) {(below ? "below" : "above")}"
            : string.Empty;
        if (!outside)
        {
            // 개수 지표만 한계 초과를 알리고 최솟값·최댓값 지표는 한계 안에 있는 경우에는 거짓 비교를 쓰지 않는다.
            causes.Add(Join($"{samples} {limitName} {Format(limit.Value)}", limit.Unit));
            return;
        }

        var (actual, limitText) = FormatPair(value.Value, limit.Value);
        var text = Join($"{valueName} {actual} {(below ? "<" : ">")} {limitName} {limitText}", value.Unit);
        causes.Add(hasCount ? $"{text} ({samples})" : text);
    }

    private static Metric? Find(ToolResult result, string name) =>
        result.Metrics.FirstOrDefault(metric => metric.Name.Equals(name, StringComparison.Ordinal));

    private static string Join(string text, string unit) =>
        string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";

    private static string Format(double value) =>
        value.ToString("G6", CultureInfo.InvariantCulture);

    /// <summary>
    /// 두 값이 G6 표기에서 같아져 "5 &gt; 5"처럼 보이는 경우에만 왕복 가능한 표기("R")를 쓴다.
    /// </summary>
    private static (string Actual, string Limit) FormatPair(double actual, double limit)
    {
        var actualText = Format(actual);
        var limitText = Format(limit);
        return actualText == limitText
            ? (actual.ToString("R", CultureInfo.InvariantCulture), limit.ToString("R", CultureInfo.InvariantCulture))
            : (actualText, limitText);
    }
}
