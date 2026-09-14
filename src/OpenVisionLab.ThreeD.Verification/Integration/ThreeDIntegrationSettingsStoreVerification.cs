using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

namespace OpenVisionLab.ThreeD.Verification.Integration;

internal static class ThreeDIntegrationSettingsStoreVerification
{
    private const string DefaultAddress = "127.0.0.1";
    private const int DefaultListenPort = 45103;
    private const string DefaultPeerHost = "127.0.0.1";
    private const int DefaultPeerPort = 45102;

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Integration settings store verification",
            $"Generated: {DateTimeOffset.UtcNow:O}"
        };
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab-3D-IntegrationSettings",
            Guid.NewGuid().ToString("N"));
        var passed = 0;

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var settingsPath = Path.Combine(fixtureRoot, "nested", "machine-exchange.json");
            var store = new ThreeDIntegrationSettingsStore(settingsPath);
            var defaults = store.Load();
            Check(
                "missing settings return documented defaults",
                defaults.ExchangeRoot == string.Empty
                && defaults.TcpListenAddress == DefaultAddress
                && defaults.TcpListenPort == DefaultListenPort
                && defaults.TcpPeerHost == DefaultPeerHost
                && defaults.TcpPeerPort == DefaultPeerPort,
                $"root={defaults.ExchangeRoot}; listen={defaults.TcpListenAddress}:{defaults.TcpListenPort}; peer={defaults.TcpPeerHost}:{defaults.TcpPeerPort}");

            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{ not-json }");
            var malformed = store.Load();
            Check(
                "malformed settings fall back without throwing",
                malformed.ExchangeRoot == string.Empty
                && malformed.TcpListenPort == DefaultListenPort
                && malformed.TcpPeerPort == DefaultPeerPort,
                $"listen={malformed.TcpListenPort}; peer={malformed.TcpPeerPort}");

            var expected = new ThreeDIntegrationSettings
            {
                ExchangeRoot = Path.Combine(fixtureRoot, "exchange"),
                TcpListenAddress = "192.168.10.4",
                TcpListenPort = 45123,
                TcpPeerHost = "192.168.10.5",
                TcpPeerPort = 45124
            };
            store.Save(expected);
            var roundTrip = store.Load();
            var json = File.ReadAllText(settingsPath);
            Check(
                "valid settings round-trip with stable JSON names",
                roundTrip.ExchangeRoot == expected.ExchangeRoot
                && roundTrip.TcpListenAddress == expected.TcpListenAddress
                && roundTrip.TcpListenPort == expected.TcpListenPort
                && roundTrip.TcpPeerHost == expected.TcpPeerHost
                && roundTrip.TcpPeerPort == expected.TcpPeerPort
                && json.Contains("\"ExchangeRoot\"", StringComparison.Ordinal)
                && json.Contains("\"TcpListenPort\"", StringComparison.Ordinal),
                $"root={roundTrip.ExchangeRoot}; listen={roundTrip.TcpListenAddress}:{roundTrip.TcpListenPort}; peer={roundTrip.TcpPeerHost}:{roundTrip.TcpPeerPort}");

            expected.TcpListenPort = 45125;
            store.Save(expected);
            Check(
                "atomic replacement leaves one current file and no temporary files",
                store.Load().TcpListenPort == 45125
                && File.Exists(settingsPath)
                && !Directory.EnumerateFiles(
                    Path.GetDirectoryName(settingsPath)!,
                    "*.tmp",
                    SearchOption.TopDirectoryOnly).Any(),
                $"port={store.Load().TcpListenPort}; temporaryFiles={Directory.EnumerateFiles(Path.GetDirectoryName(settingsPath)!, "*.tmp", SearchOption.TopDirectoryOnly).Count()}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL|unhandled|{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch
            {
            }
        }

        var failed = lines.Count(line => line.StartsWith("FAIL|", StringComparison.Ordinal));
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllLines(fullReportPath, lines);
        summary = failed == 0
            ? $"Integration settings store verification PASS ({passed} checks)"
            : $"Integration settings store verification FAIL ({passed} passed, {failed} failed)";
        return failed == 0;

        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}|{detail}");
            if (condition)
            {
                passed++;
            }
        }
    }
}
