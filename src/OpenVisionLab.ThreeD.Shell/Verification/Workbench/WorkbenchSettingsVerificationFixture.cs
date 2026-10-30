using System.IO;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class WorkbenchSettingsVerificationFixture
{
    public static string CreateNoiseRecipe(string reportDirectory)
    {
        var root = Path.Combine(reportDirectory, "settings-fixture", Guid.NewGuid().ToString("N"));
        var source = CreateSource(root);
        var tool = ToolWorkbenchToolCatalog.Create().Single(tool => tool.Id == "remove-outlier-pixels");
        var step = new ToolRecipeStep("step.noise", tool.Id, tool.Name, tool.MinimumInputCount,
            [source.Id], "output.noise", tool.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.DefaultValue)).ToArray());
        var path = Path.Combine(root, "noise.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(path, new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion,
            "Noise settings fixture", source, [], [step], []));
        return path;
    }

    public static string CreatePrecisionRecipe(string reportDirectory)
    {
        var root = Path.Combine(reportDirectory, "precision-fixture", Guid.NewGuid().ToString("N"));
        var source = CreateSource(root);
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(source.Path);
        var reference = new ToolRecipeSelection("selection.reference", "Reference", ToolRecipeSelectionKinds.GridRectangle,
            source.Id, source.FrameId, binding, new ToolRecipeGridRectangle(0, 0, 2, 4), null, null);
        var measurement = new ToolRecipeSelection("selection.measurement", "Measurement", ToolRecipeSelectionKinds.GridRectangle,
            source.Id, source.FrameId, binding, new ToolRecipeGridRectangle(2, 0, 2, 4), null, null);
        var step = new ToolRecipeStep("step.thickness", "thickness", "Precision fixture", 3,
            [source.Id, reference.Id, measurement.Id], "output.thickness",
            [new("MinimumThickness", (-6.7890123456789).ToString("G17", CultureInfo.InvariantCulture)),
                new("MaximumThickness", 127.56625366210938.ToString("G17", CultureInfo.InvariantCulture)), new("MinimumValidSampleCount", "1")]) { AcceptanceLimitsConfigured = true };
        var path = Path.Combine(root, "precision.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(path, new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion,
            "Precision fixture", source, [], [step], [reference, measurement]));
        return path;
    }

    private static ToolRecipeSource CreateSource(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "source.C3D");
        C3DHeightFieldSnapshot.CreateForVerification("source.settings", 4, 4,
            [10, 10, 10, 10, 10, 10, 10, 10, 15, 15, 15, 15, 15, 15, 15, 15]).SaveC3D(path);
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(path);
        return new("source.settings", "Settings fixture", "C3D", "raw-height", "frame.c3d-grid-index",
            path, new FileInfo(path).Length, binding.ContentSha256, binding.GridWidth, binding.GridHeight);
    }
}
