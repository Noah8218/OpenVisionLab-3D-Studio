using System.Text.Json.Serialization;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Explicit meaning of height values. A unit string alone never establishes
/// physical calibration; callers must supply this optional evidence contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeightMeasurementEvidenceState>))]
public enum HeightMeasurementEvidenceState
{
    RawHeight,
    DeclaredUnit,
    CalibratedPhysical,
    Unavailable
}

/// <summary>
/// Optional, source-owned evidence for interpreting a height scalar. The
/// contract records identity and expiry metadata but never contains or infers
/// a calibration factor.
/// </summary>
public sealed record HeightMeasurementEvidence(
    string SchemaVersion,
    HeightMeasurementEvidenceState State,
    string Evidence,
    string? SensorId = null,
    string? CalibrationId = null,
    string? CalibrationFrameId = null,
    DateTimeOffset? ExpiresAtUtc = null)
{
    public const string CurrentSchemaVersion = "1.0";

    public static HeightMeasurementEvidence RawHeight() => new(
        CurrentSchemaVersion,
        HeightMeasurementEvidenceState.RawHeight,
        "Values are raw height scalars; physical calibration is not asserted.");

    public static HeightMeasurementEvidence DeclaredUnit(string evidence) => new(
        CurrentSchemaVersion,
        HeightMeasurementEvidenceState.DeclaredUnit,
        string.IsNullOrWhiteSpace(evidence)
            ? "The source declares a unit; calibration evidence was not supplied."
            : evidence.Trim());

    public static HeightMeasurementEvidence CalibratedPhysical(
        string sensorId,
        string calibrationId,
        string calibrationFrameId,
        string evidence,
        DateTimeOffset? expiresAtUtc = null) => new(
        CurrentSchemaVersion,
        HeightMeasurementEvidenceState.CalibratedPhysical,
        evidence,
        sensorId,
        calibrationId,
        calibrationFrameId,
        expiresAtUtc);

    public static HeightMeasurementEvidence Unavailable() => new(
        CurrentSchemaVersion,
        HeightMeasurementEvidenceState.Unavailable,
        "No explicit unit or physical-calibration evidence was supplied.");

    public static HeightMeasurementEvidence Normalize(HeightMeasurementEvidence? evidence) =>
        evidence ?? Unavailable();

    public bool IsExpired(DateTimeOffset atUtc) =>
        State == HeightMeasurementEvidenceState.CalibratedPhysical
        && ExpiresAtUtc is { } expiresAt
        && expiresAt <= atUtc;

    public bool TryValidate(
        string declaredUnit,
        string sourceFrameId,
        string? sourceSensorId,
        DateTimeOffset atUtc,
        out string validationMessage)
    {
        if (!string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal)
            || !Enum.IsDefined(State)
            || string.IsNullOrWhiteSpace(Evidence))
        {
            validationMessage = "Height measurement evidence schema, state, or evidence text is invalid.";
            return false;
        }

        switch (State)
        {
            case HeightMeasurementEvidenceState.RawHeight:
                if (!string.Equals(declaredUnit, "raw-height", StringComparison.OrdinalIgnoreCase))
                {
                    validationMessage = "Raw-height evidence requires the explicit raw-height unit.";
                    return false;
                }

                break;
            case HeightMeasurementEvidenceState.DeclaredUnit:
                break;
            case HeightMeasurementEvidenceState.CalibratedPhysical:
                if (string.IsNullOrWhiteSpace(SensorId)
                    || string.IsNullOrWhiteSpace(CalibrationId)
                    || string.IsNullOrWhiteSpace(CalibrationFrameId))
                {
                    validationMessage = "Calibrated physical evidence requires sensor, calibration, and frame identities.";
                    return false;
                }

                if (!string.Equals(SensorId, sourceSensorId, StringComparison.Ordinal))
                {
                    validationMessage = "Calibration evidence sensor identity does not match the source sensor.";
                    return false;
                }

                if (!string.Equals(CalibrationFrameId, sourceFrameId, StringComparison.Ordinal))
                {
                    validationMessage = "Calibration evidence frame identity does not match the source frame.";
                    return false;
                }

                if (IsExpired(atUtc))
                {
                    validationMessage = $"Calibration evidence '{CalibrationId}' expired at {ExpiresAtUtc:O}.";
                    return false;
                }

                break;
            case HeightMeasurementEvidenceState.Unavailable:
                break;
            default:
                validationMessage = "Height measurement evidence state is unsupported.";
                return false;
        }

        validationMessage = State switch
        {
            HeightMeasurementEvidenceState.RawHeight => "Raw height is explicit; physical calibration is not asserted.",
            HeightMeasurementEvidenceState.DeclaredUnit => $"Unit '{declaredUnit}' is declared without physical calibration evidence.",
            HeightMeasurementEvidenceState.CalibratedPhysical => $"Calibration evidence '{CalibrationId}' matches the source sensor/frame and expiry policy.",
            _ => "Physical unit evidence is unavailable."
        };
        return true;
    }
}
