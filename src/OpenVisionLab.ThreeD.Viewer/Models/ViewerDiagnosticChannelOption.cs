using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// Display-only projection of one source channel. Source truth remains owned
/// by <see cref="SourceQualityChannelAvailability"/> in Core/Data.
/// </summary>
public sealed record ViewerDiagnosticChannelOption(
    SourceQualityChannel Channel,
    string Label,
    SourceQualityChannelState State,
    bool IsDisplayable,
    string Evidence)
{
    public bool IsSelectable =>
        State == SourceQualityChannelState.Available && IsDisplayable;

    public string DisplayLabel => State switch
    {
        SourceQualityChannelState.Unavailable => $"{Label} (unavailable)",
        _ when !IsDisplayable => $"{Label} (mode pending)",
        _ => Label
    };

    public string HelpText => State switch
    {
        SourceQualityChannelState.Unavailable => $"Unavailable: {Evidence}",
        _ when !IsDisplayable => $"Source channel is available, but this Viewer diagnostic mode is pending: {Evidence}",
        _ => Evidence
    };
}
