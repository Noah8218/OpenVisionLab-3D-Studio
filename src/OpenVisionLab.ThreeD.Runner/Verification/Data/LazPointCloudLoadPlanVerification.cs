using OpenVisionLab.ThreeD.Data;

internal static class LazPointCloudLoadPlanVerification
{
    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D LAZ/LAS metadata-only load-plan verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var samplePath = Path.Combine(
            "3D",
            "PublicSamples",
            "PointCloud",
            "interesting.las");
        var fullPath = Path.GetFullPath(samplePath);
        var fixtureExists = File.Exists(fullPath);
        Check(
            "LAS fixture exists for metadata-only planning",
            fixtureExists,
            $"path={fullPath}");

        if (fixtureExists)
        {
            var metadata = LazPointCloudMetadata.Load(fullPath);
            var plan = LazPointCloudLoadPlan.Create(metadata, 64);
            Check(
                "plan preserves metadata point count and source size",
                plan.PointCount == metadata.PointCount
                && plan.SourceFileBytes == new FileInfo(fullPath).Length
                && plan.PointDataRecordLength == metadata.PointDataRecordLength,
                $"points={plan.PointCount};sourceBytes={plan.SourceFileBytes};recordLength={plan.PointDataRecordLength}");
            Check(
                "plan reuses deterministic sampling policy",
                plan.SampledPointCount == Math.Min(metadata.PointCount, 64UL)
                && plan.RequestedSampleLimit == 64
                && plan.SampledPointCount <= (ulong)plan.RequestedSampleLimit,
                $"requested={plan.RequestedSampleLimit};sampled={plan.SampledPointCount}");
            Check(
                "plan reports logical memory estimates and full-stream requirement",
                plan.EstimatedDecodedRecordBytes == metadata.PointCount * metadata.PointDataRecordLength
                && plan.EstimatedManagedSampleBytes == plan.SampledPointCount * (ulong)plan.SampledPointSizeBytes
                && plan.SampledPointSizeBytes > 0
                && plan.FullPointStreamDecodeRequired
                && !plan.EstimatesSaturated,
                $"decodedBytes={plan.EstimatedDecodedRecordBytes};managedSampleBytes={plan.EstimatedManagedSampleBytes};pointSize={plan.SampledPointSizeBytes};fullDecode={plan.FullPointStreamDecodeRequired}");
            Check(
                "plan contract line is repeatable",
                plan.FormatContractLine() == plan.FormatContractLine()
                && plan.FormatContractLine().Contains("LAZ-LOAD-PLAN|", StringComparison.Ordinal),
                plan.FormatContractLine());

            var saturated = LazPointCloudLoadPlan.Create(
                metadata with
                {
                    PointCount = ulong.MaxValue,
                    PointDataRecordLength = ushort.MaxValue
                },
                int.MaxValue);
            Check(
                "plan saturates overflowing logical estimates",
                saturated.EstimatesSaturated
                && saturated.EstimatedDecodedRecordBytes == ulong.MaxValue
                && saturated.EstimatedManagedSampleBytes
                    == (ulong)int.MaxValue * (ulong)saturated.SampledPointSizeBytes,
                $"saturated={saturated.EstimatesSaturated};decodedBytes={saturated.EstimatedDecodedRecordBytes};managedSampleBytes={saturated.EstimatedManagedSampleBytes}");
        }

        var invalidLimitRejected = false;
        try
        {
            var metadata = new LazPointCloudMetadata(
                fullPath,
                "1.2",
                string.Empty,
                string.Empty,
                0,
                0,
                227,
                227,
                0,
                3,
                3,
                false,
                34,
                1,
                0.001,
                0.001,
                0.001,
                0,
                0,
                0,
                0,
                1,
                0,
                1,
                0,
                1,
                false);
            _ = LazPointCloudLoadPlan.Create(metadata, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            invalidLimitRejected = true;
        }

        Check(
            "plan rejects a non-positive sample limit",
            invalidLimitRejected,
            $"rejected={invalidLimitRejected}");

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"LazPointCloudLoadPlanVerification|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}");
        return succeeded ? 0 : 5;
    }
}
