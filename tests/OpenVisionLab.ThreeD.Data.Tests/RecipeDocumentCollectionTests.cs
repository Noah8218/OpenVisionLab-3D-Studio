using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class RecipeDocumentCollectionTests
{
    [Theory]
    [InlineData("references", false)]
    [InlineData("references", true)]
    [InlineData("steps", false)]
    [InlineData("steps", true)]
    [InlineData("inputs", false)]
    [InlineData("inputs", true)]
    public void EmptyDraftCollectionsReopenWithoutLosingRawIdentity(string missing, bool omitNullMembers)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"draft-collections-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recipePath = Path.Combine(directory, "draft.ov3d-recipe.json");
        var savedPath = Path.Combine(directory, "saved.ov3d-recipe.json");
        var step = new ToolRecipeStep("step.filter", "filter", "Filter", 1, [], "filtered", null!);
        var seed = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion, "Incomplete draft",
            new("source", "Source", "C3D", "raw-height", "frame.c3d-grid-index", "source.C3D"), [], [step]);
        var draft = missing switch
        {
            "references" => seed with { References = null! },
            "steps" => seed with { Steps = null! },
            "inputs" => seed with { Steps = [step with { InputEntityIds = null! }] },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = omitNullMembers ? JsonIgnoreCondition.WhenWritingNull : JsonIgnoreCondition.Never
        };
        var original = JsonSerializer.SerializeToUtf8Bytes(draft, options);
        try
        {
            Assert.True(ToolRecipeValidator.ValidateForStorage(draft).IsValid);
            File.WriteAllBytes(recipePath, original);
            var reopened = ToolRecipeDocumentStore.Load(recipePath, out var rawSha256);
            var context = reopened.CreateStepExecutionContext(step.Id);
            Assert.NotNull(reopened.References);
            Assert.Empty(reopened.References);
            Assert.NotNull(reopened.Steps);
            Assert.Equal(seed.Source, reopened.Source);
            Assert.Equal(seed.Name, reopened.Name);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(original)), rawSha256);
            Assert.Equal(original, File.ReadAllBytes(recipePath));
            if (missing == "steps")
            {
                Assert.Empty(reopened.Steps);
                Assert.Empty(context);
            }
            else
            {
                var restoredStep = Assert.Single(reopened.Steps);
                Assert.NotNull(restoredStep.InputEntityIds);
                Assert.Empty(restoredStep.InputEntityIds);
                Assert.Null(restoredStep.Parameters);
                Assert.Equal(step.Id, restoredStep.Id);
                Assert.NotEmpty(context);
            }

            ToolRecipeDocumentStore.Save(savedPath, reopened);
            var roundTrip = ToolRecipeDocumentStore.Load(savedPath);
            Assert.Equal(reopened.Steps.Count, roundTrip.Steps.Count);
            Assert.Empty(roundTrip.References);
            Assert.True(ToolRecipeValidator.ValidateForStorage(roundTrip).IsValid);
            Assert.Equal(original, File.ReadAllBytes(recipePath));
        }
        finally
        {
            File.Delete(recipePath);
            File.Delete(savedPath);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void PopulatedCollectionsSurviveAndNullStepStillFailsWithoutRewriting()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"draft-neighbor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recipePath = Path.Combine(directory, "draft.ov3d-recipe.json");
        var step = new ToolRecipeStep("step.filter", "filter", "Filter", 2, ["source"], "filtered", [new("Mode", "Median3x3")]);
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion, "Partial draft",
            new("source", "Source", "C3D", "raw-height", "frame.c3d-grid-index", "source.C3D"),
            [new("reference", "Datum", "plane")], [step]);
        try
        {
            ToolRecipeDocumentStore.Save(recipePath, document);
            var reopened = ToolRecipeDocumentStore.Load(recipePath);
            Assert.Equal(document.References[0], Assert.Single(reopened.References));
            var restoredStep = Assert.Single(reopened.Steps);
            Assert.Equal(step.InputEntityIds[0], Assert.Single(restoredStep.InputEntityIds));
            Assert.Equal(step.Parameters[0], Assert.Single(restoredStep.Parameters));

            var malformed = document with { Steps = [null!] };
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var original = JsonSerializer.SerializeToUtf8Bytes(malformed, options);
            File.WriteAllBytes(recipePath, original);
            Assert.Throws<InvalidDataException>(() => ToolRecipeDocumentStore.Load(recipePath));
            Assert.Equal(original, File.ReadAllBytes(recipePath));
            Assert.Throws<InvalidDataException>(() => ToolRecipeDocumentStore.Save(recipePath, malformed));
            Assert.Equal(original, File.ReadAllBytes(recipePath));
        }
        finally
        {
            File.Delete(recipePath);
            Directory.Delete(directory);
        }
    }
}
