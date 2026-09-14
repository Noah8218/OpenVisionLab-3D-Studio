using System.IO;
using System.Text.Json;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

internal sealed class ThreeDIntegrationSettings
{
    public string ExchangeRoot { get; set; } = string.Empty;
    public string TcpListenAddress { get; set; } = "127.0.0.1";
    public int TcpListenPort { get; set; } = 45103;
    public string TcpPeerHost { get; set; } = "127.0.0.1";
    public int TcpPeerPort { get; set; } = 45102;
}

internal sealed class ThreeDIntegrationSettingsStore
{
    private readonly string settingsPath;

    public ThreeDIntegrationSettingsStore(string settingsPath)
    {
        this.settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
    }

    public ThreeDIntegrationSettings Load()
    {
        try
        {
            return File.Exists(settingsPath)
                ? JsonSerializer.Deserialize<ThreeDIntegrationSettings>(File.ReadAllText(settingsPath)) ?? new()
                : new();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(ThreeDIntegrationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var fullPath = Path.GetFullPath(settingsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
