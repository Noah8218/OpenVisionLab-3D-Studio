using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Reporting.RunRecords;

public sealed record OrderedRunRecordIdentity(
    DateTimeOffset RecordedAtUtc,
    string RunId,
    string RecipePath,
    string RecipeSha256,
    string SourcePath,
    string SourceSha256,
    long SourceByteLength)
{
    public static OrderedRunRecordIdentity Create(
        string recipePath,
        string sourcePath,
        DateTimeOffset? recordedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullRecipePath = Path.GetFullPath(recipePath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var recipeSha256 = HashFile(fullRecipePath);
        var recordedAt = recordedAtUtc ?? DateTimeOffset.UtcNow;
        return new OrderedRunRecordIdentity(
            recordedAt,
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeSha256[..12].ToLowerInvariant()}",
            fullRecipePath,
            recipeSha256,
            fullSourcePath,
            HashFile(fullSourcePath),
            new FileInfo(fullSourcePath).Length);
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
