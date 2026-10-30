using System.Text.Json;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class RecipeRecentFileStoreTests
{
    [Fact]
    public void MalformedRecentPathDoesNotHideValidEntriesOrRewriteStoredBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"recent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recentPath = Path.Combine(directory, "recent.json");
        var first = Path.Combine(directory, "first.ov3d-recipe.json");
        var second = Path.Combine(directory, "second.ov3d-recipe.json");
        var third = Path.Combine(directory, "third.ov3d-recipe.json");
        var invalid = "invalid\0recipe.json";
        var original = JsonSerializer.SerializeToUtf8Bytes(new[] { "", invalid, first, first.ToUpperInvariant(), second, third });
        try
        {
            File.WriteAllBytes(recentPath, original);
            Assert.Equal(new[] { first, second }, RecipeRecentFileStore.Load(recentPath, 2));
            Assert.Equal(original, File.ReadAllBytes(recentPath));

            Assert.Throws<ArgumentException>(() => RecipeRecentFileStore.Save(recentPath, new[] { first, invalid }));
            Assert.Equal(original, File.ReadAllBytes(recentPath));

            RecipeRecentFileStore.Save(recentPath, new[] { second, first, second });
            Assert.Equal(new[] { second, first }, RecipeRecentFileStore.Load(recentPath));
        }
        finally
        {
            File.Delete(recentPath);
            Directory.Delete(directory);
        }
    }
}