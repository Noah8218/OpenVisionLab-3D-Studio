namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns cumulative observations for the Viewer OpenGL resource-retirement
/// boundary. The Viewer View remains responsible for context availability,
/// provider teardown, and every context-bound delete call.
/// </summary>
internal sealed class OpenGLResourceRetirementTelemetry
{
    public int AttemptCount { get; private set; }

    public int CallbackCount { get; private set; }

    public int ContextUnavailableCount { get; private set; }

    public int FailureCount { get; private set; }

    public void RecordAttempt() => AttemptCount++;

    public void RecordCallback() => CallbackCount++;

    public void RecordContextUnavailable() => ContextUnavailableCount++;

    public void RecordFailure() => FailureCount++;
}
