using System.IO;
using System.Text.Json;
using System.Windows;
using OpenVisionLab.ThreeD.Docking.Controls;

namespace OpenVisionLab.ThreeD.Shell.Layout;

internal enum StudioLayoutLoadStatus
{
    Missing,
    Restored,
    RestoredWithFallback,
    Corrupt,
    Incompatible,
}

internal sealed record StudioWindowPlacement(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsMaximized);

internal sealed record StudioLayoutProfile(
    int SchemaVersion,
    StudioWindowPlacement? Window,
    OpenVisionDockPresentationState Workbench,
    OpenVisionDockPresentationState Advanced)
{
    public const int CurrentSchemaVersion = 1;

    public static StudioLayoutProfile Default { get; } = new(
        CurrentSchemaVersion,
        Window: null,
        OpenVisionDockPresentationState.Default,
        OpenVisionDockPresentationState.Default);
}

internal sealed record StudioLayoutLoadResult(
    StudioLayoutProfile Profile,
    StudioLayoutLoadStatus Status,
    string Message)
{
    public bool CanAutoSave =>
        Status is StudioLayoutLoadStatus.Missing
            or StudioLayoutLoadStatus.Restored
            or StudioLayoutLoadStatus.RestoredWithFallback;
}

/// <summary>
/// Persists only an allowlisted, versioned presentation profile. No recipe,
/// source routing, draft, ROI capture, run command, or inspection result state
/// is serialized by this owner.
/// </summary>
internal sealed class StudioLayoutProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string path;

    public StudioLayoutProfileStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = System.IO.Path.GetFullPath(path);
    }

    public string Path => path;

    public StudioLayoutLoadResult Load()
    {
        if (!File.Exists(path))
        {
            return new StudioLayoutLoadResult(
                StudioLayoutProfile.Default,
                StudioLayoutLoadStatus.Missing,
                "No saved layout was found; defaults are active.");
        }

        try
        {
            var candidate = JsonSerializer.Deserialize<StudioLayoutProfile>(
                File.ReadAllText(path),
                JsonOptions);
            if (candidate is null)
            {
                return Corrupt("The saved layout is empty.");
            }

            if (candidate.SchemaVersion != StudioLayoutProfile.CurrentSchemaVersion)
            {
                return new StudioLayoutLoadResult(
                    StudioLayoutProfile.Default,
                    StudioLayoutLoadStatus.Incompatible,
                    $"Saved layout schema {candidate.SchemaVersion} is not supported; defaults are active.");
            }

            var usedFallback = false;
            var profile = candidate with
            {
                Window = SanitizeWindow(candidate.Window, ref usedFallback),
                Workbench = SanitizeDockState(
                    candidate.Workbench,
                    ref usedFallback),
                Advanced = SanitizeDockState(
                    candidate.Advanced,
                    ref usedFallback),
            };
            return new StudioLayoutLoadResult(
                profile,
                usedFallback
                    ? StudioLayoutLoadStatus.RestoredWithFallback
                    : StudioLayoutLoadStatus.Restored,
                usedFallback
                    ? "Saved layout restored with unsafe or unknown values reset to defaults."
                    : "Saved layout restored.");
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return Corrupt(
                $"Saved layout could not be read ({exception.GetType().Name}); defaults are active.");
        }
    }

    public void Save(StudioLayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var usedFallback = false;
        var safeProfile = profile with
        {
            SchemaVersion = StudioLayoutProfile.CurrentSchemaVersion,
            Window = SanitizeWindow(profile.Window, ref usedFallback),
            Workbench = SanitizeDockState(profile.Workbench, ref usedFallback),
            Advanced = SanitizeDockState(profile.Advanced, ref usedFallback),
        };

        var directory = System.IO.Path.GetDirectoryName(path)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temporaryPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(safeProfile, JsonOptions));
        try
        {
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void Reset()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static StudioLayoutLoadResult Corrupt(string message) =>
        new(
            StudioLayoutProfile.Default,
            StudioLayoutLoadStatus.Corrupt,
            message);

    private static StudioWindowPlacement? SanitizeWindow(
        StudioWindowPlacement? candidate,
        ref bool usedFallback)
    {
        if (candidate is null)
        {
            return null;
        }

        var valuesAreFinite = double.IsFinite(candidate.Left)
            && double.IsFinite(candidate.Top)
            && double.IsFinite(candidate.Width)
            && double.IsFinite(candidate.Height);
        var sizeIsUsable =
            candidate.Width is >= 1180 and <= 10000
            && candidate.Height is >= 720 and <= 10000;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var intersectsVisibleArea =
            candidate.Left + candidate.Width >= virtualLeft + 120
            && candidate.Left <= virtualRight - 120
            && candidate.Top + candidate.Height >= virtualTop + 80
            && candidate.Top <= virtualBottom - 80;

        if (valuesAreFinite && sizeIsUsable && intersectsVisibleArea)
        {
            return candidate;
        }

        usedFallback = true;
        return null;
    }

    private static OpenVisionDockPresentationState SanitizeDockState(
        OpenVisionDockPresentationState? candidate,
        ref bool usedFallback)
    {
        var defaults = OpenVisionDockPresentationState.Default;
        if (candidate is null
            || candidate.SchemaVersion
                != OpenVisionDockPresentationState.CurrentSchemaVersion)
        {
            usedFallback = true;
            return defaults;
        }

        var wide = SanitizeVariant(candidate.Wide, defaults.Wide, ref usedFallback);
        var compact = SanitizeVariant(
            candidate.Compact,
            defaults.Compact,
            ref usedFallback);
        var primary = candidate.PrimaryContentId is
            "three-d-viewer" or "displayed-outputs"
            ? candidate.PrimaryContentId
            : defaults.PrimaryContentId;
        var support = candidate.SupportContentId is
            "data-layers" or "tool-library" or "tool-inspector"
            ? candidate.SupportContentId
            : defaults.SupportContentId;
        usedFallback |= !string.Equals(
                primary,
                candidate.PrimaryContentId,
                StringComparison.Ordinal)
            || !string.Equals(
                support,
                candidate.SupportContentId,
                StringComparison.Ordinal);

        return candidate with
        {
            SchemaVersion = OpenVisionDockPresentationState.CurrentSchemaVersion,
            Wide = wide,
            Compact = compact,
            PrimaryContentId = primary,
            SupportContentId = support,
        };
    }

    private static OpenVisionDockLayoutVariant SanitizeVariant(
        OpenVisionDockLayoutVariant? candidate,
        OpenVisionDockLayoutVariant fallback,
        ref bool usedFallback)
    {
        if (candidate is null)
        {
            usedFallback = true;
            return fallback;
        }

        return new OpenVisionDockLayoutVariant(
            SafeRatio(candidate.AuthoringSupport, fallback.AuthoringSupport, ref usedFallback),
            SafeRatio(candidate.AuthoringInspector, fallback.AuthoringInspector, ref usedFallback),
            SafeRatio(candidate.AuthoringViewer, fallback.AuthoringViewer, ref usedFallback),
            SafeRatio(candidate.ValidateEvidence, fallback.ValidateEvidence, ref usedFallback),
            SafeRatio(candidate.ValidateViewer, fallback.ValidateViewer, ref usedFallback),
            SafeRatio(candidate.ResultsEvidence, fallback.ResultsEvidence, ref usedFallback),
            SafeRatio(candidate.ResultsViewer, fallback.ResultsViewer, ref usedFallback),
            SafeRatio(candidate.LegacyToolLibrary, fallback.LegacyToolLibrary, ref usedFallback),
            SafeRatio(candidate.LegacyDataLayers, fallback.LegacyDataLayers, ref usedFallback),
            SafeRatio(candidate.LegacyInspector, fallback.LegacyInspector, ref usedFallback),
            SafeRatio(candidate.LegacyViewer, fallback.LegacyViewer, ref usedFallback));
    }

    private static double SafeRatio(
        double value,
        double defaultValue,
        ref bool usedFallback)
    {
        if (double.IsFinite(value) && value is >= 0.20 and <= 8.00)
        {
            return value;
        }

        usedFallback = true;
        return defaultValue;
    }
}
