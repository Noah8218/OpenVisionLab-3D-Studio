namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// 도구 하나의 레시피 계약을 한 번에 읽는 읽기 전용 보기.
/// 값을 복사해 두지 않고 기존 소유자(입력 계약, 선택 계약, 파라미터 계약, 한계 필수 규칙)에서
/// 조회 시점에 모은다. 그래서 이 보기와 각 소유자가 서로 어긋날 수 없다.
/// 저장 형식·실행 의미·UI는 바꾸지 않는다.
/// </summary>
public sealed record ToolDescriptor(
    string Id,
    string? PrimaryInputContract,
    string ProducedContract,
    bool RequiresAcceptanceLimits,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<ToolRecipeSelectionRouteRequirement> SelectionRoutes);

public static class ToolDescriptorCatalog
{
    /// <summary>
    /// 입력·산출·ROI 규칙 중 하나라도 계약이 있는 도구는 descriptor를 돌려준다.
    /// 어떤 계약도 없는 ID는 false다.
    /// </summary>
    public static bool TryGet(string? toolId, out ToolDescriptor descriptor)
    {
        descriptor = null!;
        // 레시피 검증기는 도구 ID를 대소문자 구분(소문자 kebab-case)으로 비교한다. 같은 기준을 써서
        // "Warpage" 같은 ID가 일부 계약만 일치하는 어긋난 descriptor가 되지 않게 한다.
        if (string.IsNullOrWhiteSpace(toolId) || !string.Equals(toolId, toolId.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return false;
        }

        var hasInput = ToolRecipePrimaryInputContract.TryGetRequiredContract(toolId, out var inputContract);
        var produced = ToolRecipePrimaryInputContract.GetProducedContract(toolId);
        var routes = ToolRecipeSelectionContract.Declarations
            .Where(route => string.Equals(route.ToolId, toolId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!hasInput && produced.Length == 0 && routes.Length == 0)
        {
            return false;
        }

        descriptor = new ToolDescriptor(
            toolId,
            hasInput ? inputContract : null,
            produced,
            ToolRecipeStep.RequiresAcceptanceLimits(toolId),
            ToolRecipeParameterContract.IsHeightMeasurementTool(toolId)
                ? ToolRecipeParameterContract.GetHeightMeasurementParameterNames(toolId)
                : [],
            routes);
        return true;
    }
}
