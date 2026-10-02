namespace OpenVisionLab.ThreeD.Docking.Controls;

/// <summary>
/// Versioned, presentation-only dock proportions. It intentionally excludes
/// AvalonDock type names, serialized layout XML, recipe state, and execution
/// state so a restored layout cannot execute inspection or mutate a recipe.
/// </summary>
public sealed record OpenVisionDockLayoutVariant(
    double AuthoringSupport,
    double AuthoringInspector,
    double AuthoringViewer,
    double ValidateEvidence,
    double ValidateViewer,
    double ResultsEvidence,
    double ResultsViewer,
    double LegacyToolLibrary,
    double LegacyDataLayers,
    double LegacyInspector,
    double LegacyViewer);

public sealed record OpenVisionDockPresentationState(
    int SchemaVersion,
    OpenVisionDockLayoutVariant Wide,
    OpenVisionDockLayoutVariant Compact,
    string PrimaryContentId,
    string SupportContentId)
{
    public const int CurrentSchemaVersion = 1;

    public static OpenVisionDockPresentationState Default { get; } = new(
        CurrentSchemaVersion,
        new OpenVisionDockLayoutVariant(
            AuthoringSupport: 2.13,
            AuthoringInspector: 1.97,
            AuthoringViewer: 5.90,
            ValidateEvidence: 0.72,
            ValidateViewer: 2.74,
            ResultsEvidence: 0.72,
            ResultsViewer: 2.74,
            LegacyToolLibrary: 0.72,
            LegacyDataLayers: 0.90,
            LegacyInspector: 1.05,
            LegacyViewer: 3.30),
        new OpenVisionDockLayoutVariant(
            AuthoringSupport: 1.00,
            AuthoringInspector: 1.188,
            AuthoringViewer: 1.199,
            ValidateEvidence: 0.86,
            ValidateViewer: 2.46,
            ResultsEvidence: 0.86,
            ResultsViewer: 2.46,
            LegacyToolLibrary: 1.00,
            LegacyDataLayers: 0.90,
            LegacyInspector: 1.15,
            LegacyViewer: 3.00),
        PrimaryContentId: "three-d-viewer",
        SupportContentId: "data-layers");
}
