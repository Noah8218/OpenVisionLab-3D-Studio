using OpenVisionLab.ThreeD.Core;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

/// <summary>
/// 도구 descriptor가 기존 계약 소유자(입력·선택·파라미터·한계 필수 규칙)와 같은 값을 돌려주는지 고정한다.
/// 기대값은 descriptor 도입 전 레시피 검증기와 측정 실행기가 각각 들고 있던 목록과 같다.
/// </summary>
public sealed class ToolDescriptorTests
{
    public static TheoryData<string, string[]> MeasurementParameterNames => new()
    {
        { "thickness", ["MinimumThickness", "MaximumThickness", "MinimumValidSampleCount"] },
        { "warpage", ["MaximumPeakToValley", "MaximumRms", "MinimumValidSampleCount"] },
        { "plane-flatness", ["MaximumFlatness", "MinimumReferenceSampleCount", "MinimumMeasurementSampleCount"] },
        { "point-pair-dimensions", ["ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees"] },
        { "gap-flush", ["ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance"] },
        { "volume", ["ExpectedNetVolume", "VolumeTolerance"] },
        { "cross-section-dimensions", ["ExpectedWidth", "WidthTolerance", "ExpectedHeightRange", "HeightTolerance"] },
        { "completeness-grid", Array.Empty<string>() }
    };

    [Theory]
    [MemberData(nameof(MeasurementParameterNames))]
    public void Measurement_tool_parameter_names_keep_their_recorded_order(string toolId, string[] expected)
    {
        Assert.True(ToolDescriptorCatalog.TryGet(toolId, out var descriptor));
        Assert.Equal(expected, descriptor.ParameterNames);
        Assert.Equal(expected, ToolRecipeParameterContract.GetHeightMeasurementParameterNames(toolId));
        Assert.True(ToolRecipeParameterContract.IsHeightMeasurementTool(toolId));
        Assert.Equal(expected.Length, expected.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Height_measurement_tool_set_has_the_eight_recorded_ids()
    {
        Assert.Equal(
            new[] { "completeness-grid", "cross-section-dimensions", "gap-flush", "plane-flatness", "point-pair-dimensions", "thickness", "volume", "warpage" },
            ToolRecipeParameterContract.HeightMeasurementToolIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.False(ToolRecipeParameterContract.IsHeightMeasurementTool("filter"));
        Assert.False(ToolRecipeParameterContract.IsHeightMeasurementTool("Warpage"));
        Assert.False(ToolRecipeParameterContract.IsHeightMeasurementTool(null));
    }

    [Fact]
    public void Every_measurement_tool_requires_acceptance_limits_and_a_height_field_input()
    {
        foreach (var toolId in ToolRecipeParameterContract.HeightMeasurementToolIds)
        {
            Assert.True(ToolDescriptorCatalog.TryGet(toolId, out var descriptor), toolId);
            Assert.True(descriptor.RequiresAcceptanceLimits, toolId);
            Assert.NotNull(descriptor.PrimaryInputContract);
            Assert.True(ToolRecipePrimaryInputContract.TryGetRequiredContract(toolId, out var contract), toolId);
            Assert.Equal(contract, descriptor.PrimaryInputContract);
        }
    }

    [Fact]
    public void Descriptor_selection_routes_equal_the_selection_contract_declarations()
    {
        var declaredToolIds = ToolRecipeSelectionContract.Declarations
            .Select(route => route.ToolId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(declaredToolIds);
        foreach (var toolId in declaredToolIds)
        {
            Assert.True(ToolDescriptorCatalog.TryGet(toolId, out var descriptor), toolId);
            var expected = ToolRecipeSelectionContract.Declarations
                .Where(route => string.Equals(route.ToolId, toolId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.Equal(expected, descriptor.SelectionRoutes);
        }
    }

    [Fact]
    public void Warpage_and_thickness_keep_their_roi_roles()
    {
        Assert.True(ToolDescriptorCatalog.TryGet("warpage", out var warpage));
        Assert.Equal([ToolRecipeSelectionRoles.MeasurementRegion], warpage.SelectionRoutes.Select(route => route.Role).ToArray());
        Assert.True(ToolDescriptorCatalog.TryGet("thickness", out var thickness));
        Assert.Equal(
            [ToolRecipeSelectionRoles.ReferenceRegion, ToolRecipeSelectionRoles.MeasurementRegion],
            thickness.SelectionRoutes.Select(route => route.Role).ToArray());
        Assert.Equal("MeasurementResult", warpage.ProducedContract);
    }

    [Fact]
    public void Descriptor_rejects_ids_that_the_recipe_validator_would_not_match()
    {
        Assert.False(ToolDescriptorCatalog.TryGet("Warpage", out _));
        Assert.False(ToolDescriptorCatalog.TryGet(" warpage", out _));
        Assert.True(ToolDescriptorCatalog.TryGet("warpage", out _));
    }

    [Fact]
    public void Shared_parameter_name_lists_cannot_be_mutated_through_a_cast()
    {
        var names = ToolRecipeParameterContract.GetHeightMeasurementParameterNames("warpage");
        Assert.False(names is string[]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)names)[0] = "Tampered");
        Assert.Equal("MaximumPeakToValley", ToolRecipeParameterContract.GetHeightMeasurementParameterNames("warpage")[0]);
    }

    [Fact]
    public void Tool_without_any_contract_has_no_descriptor()
    {
        Assert.False(ToolDescriptorCatalog.TryGet("not-a-tool", out _));
        Assert.False(ToolDescriptorCatalog.TryGet(null, out _));
        Assert.False(ToolDescriptorCatalog.TryGet("  ", out _));
    }
}
