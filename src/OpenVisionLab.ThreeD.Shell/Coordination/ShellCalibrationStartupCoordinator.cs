namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Supplies the application-state callbacks required by calibration startup.
/// The coordinator owns sequence policy; the Shell composition root owns the
/// concrete ViewModel callbacks.
/// </summary>
internal sealed class ShellCalibrationStartupCallbacks
{
    public required Action SelectCalibrationWorkspace { get; init; }
    public required Action SelectRepeatabilitySection { get; init; }
    public required Func<string, bool> LoadStudy { get; init; }
    public required Action Calculate { get; init; }
}

/// <summary>
/// Applies the command-line calibration startup sequence without depending on
/// WPF controls, Window lifetime, Viewer, or persistence types.
/// </summary>
internal sealed class ShellCalibrationStartupCoordinator
{
    private readonly ShellCalibrationStartupCallbacks callbacks;

    public ShellCalibrationStartupCoordinator(ShellCalibrationStartupCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public void Apply(string? studyPath, bool calculate)
    {
        if (studyPath is null)
        {
            return;
        }

        callbacks.SelectCalibrationWorkspace();
        callbacks.SelectRepeatabilitySection();
        if (callbacks.LoadStudy(studyPath) && calculate)
        {
            callbacks.Calculate();
        }
    }
}
