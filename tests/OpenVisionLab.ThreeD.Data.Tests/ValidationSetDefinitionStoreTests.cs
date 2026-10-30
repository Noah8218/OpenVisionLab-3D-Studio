using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class ValidationSetDefinitionStoreTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void UnsupportedRoleSavePreservesManifestAndAllowsValidRecovery(int unsupportedRole)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"role-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recipePath = Path.Combine(directory, "recipe.ov3d-recipe.json");
        var manifestPath = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(recipePath);
        var sample = new ToolRecipeValidationSampleDefinition(1, Path.Combine(directory, "source.C3D"), ToolRecipeValidationSampleRole.Good);
        var valid = new ToolRecipeValidationSetDefinition("1.0", "Role contract", new string('A', 64), [sample]);
        try
        {
            ToolRecipeValidationSetDefinitionStore.SaveForRecipe(recipePath, valid);
            var original = File.ReadAllBytes(manifestPath);
            var invalid = valid with { Samples = [sample with { Role = (ToolRecipeValidationSampleRole)unsupportedRole }] };
            Assert.Throws<InvalidDataException>(() => ToolRecipeValidationSetDefinitionStore.SaveForRecipe(recipePath, invalid));
            Assert.Equal(original, File.ReadAllBytes(manifestPath));

            ToolRecipeValidationSetDefinitionStore.SaveForRecipe(recipePath, valid with { Samples = [sample with { Role = ToolRecipeValidationSampleRole.Bad }] });
            var restored = Assert.IsType<ToolRecipeValidationSetDefinition>(ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath));
            var actual = Assert.Single(restored.Samples);
            Assert.Equal(ToolRecipeValidationSampleRole.Bad, actual.Role);
            Assert.Equal(ToolRecipeValidationExpectation.Ng, actual.Expectation);
            Assert.Equal(ToolRecipeValidationUsage.Adjustment, actual.Usage);
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullSamplesFailControlledWithoutWritingAndAllowValidRecovery(bool nullCollection)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recipePath = Path.Combine(directory, "recipe.ov3d-recipe.json");
        var manifestPath = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(recipePath);
        var malformed = new ToolRecipeValidationSetDefinition("1.0", "Null sample", new string('A', 64), nullCollection ? null! : [null!]);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(malformed, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        try
        {
            File.WriteAllBytes(manifestPath, bytes);
            Assert.Throws<InvalidDataException>(() => ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath));
            Assert.Equal(bytes, File.ReadAllBytes(manifestPath));
            Assert.Throws<InvalidDataException>(() => ToolRecipeValidationSetDefinitionStore.SaveForRecipe(recipePath, malformed));
            Assert.Equal(bytes, File.ReadAllBytes(manifestPath));

            var samplePath = Path.Combine(directory, "source.C3D");
            var valid = malformed with { Samples = [new ToolRecipeValidationSampleDefinition(1, samplePath, ToolRecipeValidationSampleRole.Good)] };
            ToolRecipeValidationSetDefinitionStore.SaveForRecipe(recipePath, valid);
            var restored = Assert.IsType<ToolRecipeValidationSetDefinition>(ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath));
            Assert.Equal(ToolRecipeValidationSetDefinition.CurrentSchemaVersion, restored.SchemaVersion);
            var sample = Assert.Single(restored.Samples);
            Assert.Equal(samplePath, sample.SourcePath);
            Assert.Equal(ToolRecipeValidationExpectation.Ok, sample.Expectation);
            Assert.Equal(ToolRecipeValidationUsage.Adjustment, sample.Usage);
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(directory);
        }
    }
}