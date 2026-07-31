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
            AuthoringSupport: 0.92,
            AuthoringInspector: 1.18,
            AuthoringViewer: 3.70,
            ValidateEvidence: 1.60,
            ValidateViewer: 2.70,
            ResultsEvidence: 1.60,
            ResultsViewer: 2.70,
            LegacyToolLibrary: 0.72,
            LegacyDataLayers: 0.90,
            LegacyInspector: 1.05,
            LegacyViewer: 3.30),
        new OpenVisionDockLayoutVariant(
            AuthoringSupport: 0.95,
            AuthoringInspector: 1.18,
            AuthoringViewer: 3.55,
            ValidateEvidence: 1.05,
            ValidateViewer: 2.45,
            ResultsEvidence: 1.05,
            ResultsViewer: 2.45,
            LegacyToolLibrary: 1.00,
            LegacyDataLayers: 0.90,
            LegacyInspector: 1.15,
            LegacyViewer: 3.00),
        PrimaryContentId: "three-d-viewer",
        SupportContentId: "data-layers");
}
