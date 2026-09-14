using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

internal enum ThreeDIntegrationSharedKeyInputStatus
{
    NotConfigured,
    Ready,
    TooShort,
    InvalidBase64,
    Disposed
}

internal enum ThreeDIntegrationSharedKeyEnvironmentStatus
{
    Missing,
    Ready,
    Invalid,
    TooShort,
    Disposed
}

internal enum ThreeDIntegrationSharedKeyAcquireFailure
{
    SessionUnavailable,
    EnvironmentMissing,
    EnvironmentInvalid,
    Disposed
}

internal sealed class ThreeDIntegrationSharedKeySession : IDisposable
{
    internal const string EnvironmentVariableName = "OPENVISIONLAB_TCP_SHARED_KEY";
    internal const int MinimumKeyLength = 32;

    private readonly object gate = new();
    private byte[]? sessionKey;
    private bool hasSessionKeyInput;
    private int disposedState;

    internal bool HasSessionKeyInput
    {
        get
        {
            lock (gate)
            {
                return hasSessionKeyInput;
            }
        }
    }

    internal ThreeDIntegrationSharedKeyInputStatus SetSession(string? encodedKey)
    {
        lock (gate)
        {
            if (IsDisposed)
            {
                return ThreeDIntegrationSharedKeyInputStatus.Disposed;
            }

            ClearSessionKey();
            hasSessionKeyInput = !string.IsNullOrWhiteSpace(encodedKey);
            if (!hasSessionKeyInput)
            {
                return ThreeDIntegrationSharedKeyInputStatus.NotConfigured;
            }

            try
            {
                var parsed = Convert.FromBase64String(encodedKey!.Trim());
                if (parsed.Length < MinimumKeyLength)
                {
                    CryptographicOperations.ZeroMemory(parsed);
                    return ThreeDIntegrationSharedKeyInputStatus.TooShort;
                }

                sessionKey = parsed;
                return ThreeDIntegrationSharedKeyInputStatus.Ready;
            }
            catch (FormatException)
            {
                return ThreeDIntegrationSharedKeyInputStatus.InvalidBase64;
            }
        }
    }

    internal ThreeDIntegrationSharedKeyEnvironmentStatus DescribeEnvironment()
    {
        lock (gate)
        {
            if (IsDisposed)
            {
                return ThreeDIntegrationSharedKeyEnvironmentStatus.Disposed;
            }

            var encoded = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return ThreeDIntegrationSharedKeyEnvironmentStatus.Missing;
            }

            try
            {
                var key = Convert.FromBase64String(encoded.Trim());
                var status = key.Length >= MinimumKeyLength
                    ? ThreeDIntegrationSharedKeyEnvironmentStatus.Ready
                    : ThreeDIntegrationSharedKeyEnvironmentStatus.TooShort;
                CryptographicOperations.ZeroMemory(key);
                return status;
            }
            catch (FormatException)
            {
                return ThreeDIntegrationSharedKeyEnvironmentStatus.Invalid;
            }
        }
    }

    internal bool TryAcquire(
        out byte[] key,
        out ThreeDIntegrationSharedKeyAcquireFailure failure)
    {
        lock (gate)
        {
            key = [];
            if (IsDisposed)
            {
                failure = ThreeDIntegrationSharedKeyAcquireFailure.Disposed;
                return false;
            }

            if (hasSessionKeyInput)
            {
                if (sessionKey is null)
                {
                    failure = ThreeDIntegrationSharedKeyAcquireFailure.SessionUnavailable;
                    return false;
                }

                key = sessionKey.ToArray();
                failure = default;
                return true;
            }

            var encoded = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                failure = ThreeDIntegrationSharedKeyAcquireFailure.EnvironmentMissing;
                return false;
            }

            try
            {
                var parsed = Convert.FromBase64String(encoded.Trim());
                if (parsed.Length >= MinimumKeyLength)
                {
                    key = parsed;
                    failure = default;
                    return true;
                }

                CryptographicOperations.ZeroMemory(parsed);
            }
            catch (FormatException)
            {
            }

            failure = ThreeDIntegrationSharedKeyAcquireFailure.EnvironmentInvalid;
            return false;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (Interlocked.Exchange(ref disposedState, 1) != 0)
            {
                return;
            }

            ClearSessionKey();
        }
    }

    private bool IsDisposed => Volatile.Read(ref disposedState) != 0;

    private void ClearSessionKey()
    {
        if (sessionKey is not null)
        {
            CryptographicOperations.ZeroMemory(sessionKey);
            sessionKey = null;
        }

        hasSessionKeyInput = false;
    }
}
