using System.Collections.ObjectModel;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// Plain presentation data consumed by the LiveCharts XAML series template.
/// </summary>
public sealed class CalibrationRepeatabilityChartSeriesModel
{
    public CalibrationRepeatabilityChartSeriesModel(ObservableCollection<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }

    public ObservableCollection<double> Values { get; }
}
