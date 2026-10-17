namespace OpenVisionLab.ThreeD.Presentation.Models;

public enum HeightLegendEditKind { Begin, Lower, Upper, Shift, Scale, Commit, Cancel, Reset, InputLower, InputUpper }
public sealed record HeightLegendEditRequest(HeightLegendEditKind Kind, double Value = 0, double Pivot = 0, string? Text = null);
