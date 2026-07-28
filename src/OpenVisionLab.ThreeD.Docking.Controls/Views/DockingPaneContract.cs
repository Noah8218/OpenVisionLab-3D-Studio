namespace OpenVisionLab.ThreeD.Docking.Controls;

public sealed record DockingPaneContract(
    string ContentId,
    string Title,
    bool CanFloat,
    bool CanClose,
    bool? CanHide,
    bool HasContent);

public sealed record DockingFloatDockResult(
    bool Floated,
    bool Redocked,
    int FloatingWindowCountBefore,
    int FloatingWindowCountAfterFloat,
    int FloatingWindowCountAfterDock,
    string Message);
