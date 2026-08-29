using OpenVisionLab.ThreeD.Reporting.Integration;
using Xunit;

namespace OpenVisionLab.ThreeD.Reporting.Tests;

public sealed class ThreeDCoordinateProjectionContractTests
{
    [Fact]
    public void NormalizedMappingRoundTripsImageAndGridEndpoints()
    {
        var profile = CreateProfile();

        var grid = ThreeDCoordinateProjectionContract.MapImageToGrid(
            profile,
            639,
            479,
            1280,
            840);
        var image = ThreeDCoordinateProjectionContract.MapGridToImage(
            profile,
            grid.X,
            grid.Y,
            1280,
            840);

        Assert.Equal(1279, grid.X, precision: 10);
        Assert.Equal(839, grid.Y, precision: 10);
        Assert.Equal(639, image.X, precision: 10);
        Assert.Equal(479, image.Y, precision: 10);
    }

    [Fact]
    public void PairedProfilesMatchWhenOnlyTwoDTransactionIdentityDiffers()
    {
        var expected = CreateProfile("11111111-1111-1111-1111-111111111111");
        var actual = expected with { TwoDTransactionId = "22222222-2222-2222-2222-222222222222" };

        Assert.True(
            ThreeDCoordinateProjectionContract.ProfilesMatch(expected, actual));
    }

    private static ThreeDCoordinateProjectionProfile CreateProfile(
        string? twoDTransactionId = null) =>
        new(
            "1.0",
            "projection-test",
            twoDTransactionId,
            new(640, 480, "px", "top-left"),
            new("raw-height", "frame.c3d-grid-index", "top-left"),
            new("normalized-linear", 1.0, 1.0, 0.0, 0.0));
}
