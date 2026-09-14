using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

namespace OpenVisionLab.ThreeD.Verification.Integration;

internal static class ThreeDIntegrationSharedKeySessionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        const string environmentVariable = ThreeDIntegrationSharedKeySession.EnvironmentVariableName;
        var lines = new List<string>
        {
            "OpenVisionLab 3D Integration shared-key session verification",
            $"Generated: {DateTimeOffset.UtcNow:O}"
        };
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab-3D-IntegrationKeySession",
            Guid.NewGuid().ToString("N"));
        var previousEnvironmentValue = Environment.GetEnvironmentVariable(environmentVariable);
        var passed = 0;

        try
        {
            var keyA = CreateKey(1);
            var keyB = CreateKey(9);
            Environment.SetEnvironmentVariable(environmentVariable, null);
            using var session = new ThreeDIntegrationSharedKeySession();
            Check(
                "missing environment key is explicit",
                session.DescribeEnvironment() == ThreeDIntegrationSharedKeyEnvironmentStatus.Missing
                && !session.TryAcquire(
                    out _,
                    out var missingFailure)
                && missingFailure == ThreeDIntegrationSharedKeyAcquireFailure.EnvironmentMissing,
                "environment=missing; acquisition=EnvironmentMissing");

            Check(
                "valid session key is accepted",
                session.SetSession(Convert.ToBase64String(keyA)) == ThreeDIntegrationSharedKeyInputStatus.Ready
                && session.HasSessionKeyInput,
                "session=ready");
            Check(
                "acquisition returns defensive copies",
                session.TryAcquire(out var acquired, out _)
                && acquired.SequenceEqual(keyA)
                && SetFirstByte(acquired, 255)
                && session.TryAcquire(out var reacquired, out _)
                && reacquired.SequenceEqual(keyA),
                "first acquisition mutation did not alter retained session key");

            var shortSessionStatus = session.SetSession(Convert.ToBase64String(new byte[31]));
            var malformedSessionStatus = session.SetSession("not-base64");
            var acquiredAfterMalformedSession = session.TryAcquire(out _, out var invalidFailure);
            Check(
                "short and malformed session values fail closed",
                shortSessionStatus == ThreeDIntegrationSharedKeyInputStatus.TooShort
                && malformedSessionStatus == ThreeDIntegrationSharedKeyInputStatus.InvalidBase64
                && !acquiredAfterMalformedSession
                && invalidFailure == ThreeDIntegrationSharedKeyAcquireFailure.SessionUnavailable,
                $"short={shortSessionStatus}; malformed={malformedSessionStatus}; acquisition={(acquiredAfterMalformedSession ? "true" : "false")}/{invalidFailure}");

            Check(
                "replacement swaps the retained key",
                session.SetSession(Convert.ToBase64String(keyA)) == ThreeDIntegrationSharedKeyInputStatus.Ready
                && session.SetSession(Convert.ToBase64String(keyB)) == ThreeDIntegrationSharedKeyInputStatus.Ready
                && session.TryAcquire(out var replaced, out _)
                && replaced.SequenceEqual(keyB),
                "retained key is replacement B");

            session.SetSession(null);
            Environment.SetEnvironmentVariable(environmentVariable, Convert.ToBase64String(keyB));
            Check(
                "valid environment key is available without session persistence",
                session.DescribeEnvironment() == ThreeDIntegrationSharedKeyEnvironmentStatus.Ready
                && session.TryAcquire(out var environmentKey, out _)
                && environmentKey.SequenceEqual(keyB)
                && !session.HasSessionKeyInput,
                "environment=ready; session=false");

            Environment.SetEnvironmentVariable(environmentVariable, Convert.ToBase64String(new byte[31]));
            var shortEnvironment = session.DescribeEnvironment();
            Environment.SetEnvironmentVariable(environmentVariable, "not-base64");
            var malformedEnvironment = session.DescribeEnvironment();
            Check(
                "short and malformed environment keys are distinguished",
                shortEnvironment == ThreeDIntegrationSharedKeyEnvironmentStatus.TooShort
                && malformedEnvironment == ThreeDIntegrationSharedKeyEnvironmentStatus.Invalid,
                $"short={shortEnvironment}; malformed={malformedEnvironment}");

            session.Dispose();
            session.Dispose();
            Check(
                "dispose zeros and closes the session boundary",
                session.DescribeEnvironment() == ThreeDIntegrationSharedKeyEnvironmentStatus.Disposed
                && !session.TryAcquire(out _, out var disposedFailure)
                && disposedFailure == ThreeDIntegrationSharedKeyAcquireFailure.Disposed
                && session.SetSession(Convert.ToBase64String(keyA)) == ThreeDIntegrationSharedKeyInputStatus.Disposed,
                "environment=Disposed; acquisition=Disposed; replacement=Disposed");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL|unhandled|{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousEnvironmentValue);
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
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllLines(reportPath, lines);
        summary = failed == 0
            ? $"Integration shared-key session verification PASS ({passed} checks)"
            : $"Integration shared-key session verification FAIL ({passed} passed, {failed} failed)";
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

    private static byte[] CreateKey(byte firstByte)
    {
        var key = new byte[ThreeDIntegrationSharedKeySession.MinimumKeyLength];
        for (var index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(firstByte + index);
        }
        return key;
    }

    private static bool SetFirstByte(byte[] value, byte firstByte)
    {
        value[0] = firstByte;
        return true;
    }
}
