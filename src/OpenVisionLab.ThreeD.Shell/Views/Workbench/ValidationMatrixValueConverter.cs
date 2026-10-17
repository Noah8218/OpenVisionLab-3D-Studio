using System.Globalization;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed class ValidationMatrixValueConverter : IValueConverter
{
    public bool IncludeStatus { get; set; } = true;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationSetSampleRow groupedSample && parameter is ValidationTableColumn { Members: { } members })
        {
            return members.Select(member =>
            {
                var step = groupedSample.Steps.FirstOrDefault(item => item.StepId == member.StepId);
                var metric = step?.Metrics.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Status));
                var status = step?.Status == "Error" ? "Error" : metric?.Status is "Pass" or "Fail" ? metric.Status : "None";
                var lower = step?.Metrics.FirstOrDefault(item => item.Name == "LowerLimit");
                var upper = step?.Metrics.FirstOrDefault(item => item.Name == "UpperLimit");
                var range = lower is null && upper is null ? "—" : $"{lower?.Value ?? "−∞"} – {upper?.Value ?? "+∞"} {lower?.Unit ?? upper?.Unit}";
                var statusText = status == "Error" ? step!.StatusText : status == "None"
                    ? ThreeDLocalization.Shared.Resolve("ThreeD.Validation.NoResult", "결과 없음", "No result") : metric!.StatusText;
                var detail = $"{member.ToolName} · {(metric is null ? "—" : metric.Value + " " + metric.Unit)} · {statusText}"
                    + $" · {ThreeDLocalization.Shared.Resolve("ThreeD.Validation.AllowedRange", "허용", "Allowed")} {range} · {step?.Evidence ?? groupedSample.Message}";
                return new ValidationGroupMemberCell(member.ToolName, status, detail);
            }).ToArray();
        }
        if (value is not ValidationSetSampleRow sample || parameter is not ValidationMatrixColumn column) return "—";
        var metric = sample.Steps.FirstOrDefault(step => step.StepId == column.StepId)?.Metrics
            .FirstOrDefault(metric => metric.Name == column.MetricName && metric.Unit == column.Unit);
        return metric is null ? "—" : IncludeStatus ? $"{metric.StatusText} · {metric.Value} {metric.Unit}".Trim() : $"{metric.Value} {metric.Unit}".Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
