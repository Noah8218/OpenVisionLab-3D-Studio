using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class C3DMemoryAdmissionPolicyTests
{
    [Fact]
    public void LargestMeasuredScaleIsAdmitted()
    {
        var result = C3DMemoryAdmissionPolicy.Evaluate(4096, 4096);

        Assert.True(result.IsAdmitted);
        Assert.Equal(C3DMemoryAdmissionPolicy.MaxSupportedSampleCount, result.SampleCount);
    }

    [Fact]
    public void LargerScaleIsRejectedBeforePayloadAllocation()
    {
        var result = C3DMemoryAdmissionPolicy.Evaluate(4097, 4096);

        Assert.False(result.IsAdmitted);
        Assert.True(result.SampleCount > C3DMemoryAdmissionPolicy.MaxSupportedSampleCount);
        Assert.Contains("measured Dev admission limit", result.Reason, StringComparison.Ordinal);
    }
}
