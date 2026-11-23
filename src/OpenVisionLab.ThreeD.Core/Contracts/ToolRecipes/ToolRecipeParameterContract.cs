namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// 높이 측정 도구 8종의 ID와 레시피 파라미터 이름을 한 곳에서 소유한다.
/// 이전에는 같은 이름 목록이 레시피 검증기(Core)와 측정 실행기(Tools)에 각각 있었다.
/// 값의 범위·형식 검사는 각 실행기가 계속 맡고, 여기서는 "어떤 이름을 정확히 한 번씩 받는가"만 정한다.
/// </summary>
public static class ToolRecipeParameterContract
{
    // 공유 정적 목록이므로 호출자가 배열로 캐스팅해 바꿀 수 없게 읽기 전용 래퍼로 노출한다.
    private static readonly IReadOnlyList<string> Thickness = ReadOnly(["MinimumThickness", "MaximumThickness", "MinimumValidSampleCount"]);
    private static readonly IReadOnlyList<string> Warpage = ReadOnly(["MaximumPeakToValley", "MaximumRms", "MinimumValidSampleCount"]);
    private static readonly IReadOnlyList<string> PlaneFlatness = ReadOnly(["MaximumFlatness", "MinimumReferenceSampleCount", "MinimumMeasurementSampleCount"]);
    private static readonly IReadOnlyList<string> PointPair = ReadOnly(
        ["ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees"]);
    private static readonly IReadOnlyList<string> GapFlush = ReadOnly(["ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance"]);
    private static readonly IReadOnlyList<string> Volume = ReadOnly(["ExpectedNetVolume", "VolumeTolerance"]);
    private static readonly IReadOnlyList<string> CrossSection = ReadOnly(["ExpectedWidth", "WidthTolerance", "ExpectedHeightRange", "HeightTolerance"]);
    private static readonly IReadOnlyList<string> None = ReadOnly([]);

    /// <summary>
    /// 두께 기준면 맞춤 RMS 상한. 레시피에 없으면 기준 영역이 평면에서 얼마나 벗어났는지 검사하지 않는다(기존 동작).
    /// </summary>
    public const string ThicknessMaximumReferenceFitRms = "MaximumReferenceFitRms";

    // 필수 파라미터와 달리 없어도 되는 이름. 있으면 한 번만 허용한다.
    private static readonly IReadOnlyList<string> ThicknessOptional = ReadOnly([ThicknessMaximumReferenceFitRms]);

    private static IReadOnlyList<string> ReadOnly(string[] names) => Array.AsReadOnly(names);

    /// <summary>
    /// 높이 맵 위에서 판정·측정값을 내는 도구의 ID. 대소문자를 구분해 비교한다.
    /// </summary>
    public static IReadOnlySet<string> HeightMeasurementToolIds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "thickness",
        "warpage",
        "plane-flatness",
        "point-pair-dimensions",
        "gap-flush",
        "volume",
        "cross-section-dimensions",
        "completeness-grid"
    };

    public static bool IsHeightMeasurementTool(string? toolId) =>
        toolId is not null && HeightMeasurementToolIds.Contains(toolId);

    /// <summary>
    /// 레시피 단계가 정확히 한 번씩 가져야 하는 파라미터 이름. completeness-grid는 선택 파라미터만 있어 빈 목록이다.
    /// 목록에 없는 ID는 기존 동작을 유지해 plane-flatness 이름을 돌려준다(호출 전에 측정 도구인지 확인한다).
    /// </summary>
    public static IReadOnlyList<string> GetHeightMeasurementParameterNames(string? toolId) => toolId switch
    {
        "thickness" => Thickness,
        "warpage" => Warpage,
        "point-pair-dimensions" => PointPair,
        "gap-flush" => GapFlush,
        "volume" => Volume,
        "cross-section-dimensions" => CrossSection,
        "completeness-grid" => None,
        _ => PlaneFlatness
    };

    /// <summary>
    /// 레시피 단계가 없어도 되는 파라미터 이름. 필수 목록과 겹치지 않는다. 지금은 두께의 기준면 RMS 상한뿐이다.
    /// </summary>
    public static IReadOnlyList<string> GetOptionalHeightMeasurementParameterNames(string? toolId) => toolId switch
    {
        "thickness" => ThicknessOptional,
        _ => None
    };

    /// <summary>
    /// 필수 이름은 정확히 한 번씩, 선택 이름은 최대 한 번씩 있고 그 밖의 이름이 없으면 true.
    /// completeness-grid는 자체 프로필 검사를 쓰므로 호출하는 쪽에서 제외한다.
    /// 선택 파라미터가 없는 기존 레시피는 이전과 같은 결과를 얻는다.
    /// </summary>
    public static bool HasExpectedHeightMeasurementParameters(string? toolId, IReadOnlyList<ToolRecipeParameter>? parameters)
    {
        parameters ??= [];
        var required = GetHeightMeasurementParameterNames(toolId);
        var optional = GetOptionalHeightMeasurementParameterNames(toolId);
        if (required.Any(name => parameters.Count(parameter => parameter.Name == name) != 1)) return false;
        if (optional.Any(name => parameters.Count(parameter => parameter.Name == name) > 1)) return false;
        // 합계가 맞지 않으면 알 수 없는 이름이 섞여 있다.
        return parameters.Count == required.Count + optional.Count(name => parameters.Any(parameter => parameter.Name == name));
    }

    /// <summary>
    /// 두께 기준면 RMS 상한 값의 형식 규칙. 실행기의 숫자 규칙(공백·쉼표 없는 invariant 유한 수)과 같고 0 초과여야 한다.
    /// 검증기, 실행기, 편집 화면이 같은 규칙을 쓰도록 한 곳에 둔다.
    /// </summary>
    public static bool TryParseThicknessMaximumReferenceFitRms(string? value, out double limit)
    {
        limit = double.NaN;
        return value is not null
            && value == value.Trim()
            && !value.Contains(',', StringComparison.Ordinal)
            && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out limit)
            && double.IsFinite(limit)
            && limit > 0d;
    }

    /// <summary>선택 파라미터가 있으면 오류 문구에 덧붙일 안내. 없으면 빈 문자열.</summary>
    public static string DescribeOptionalHeightMeasurementParameters(string? toolId) =>
        GetOptionalHeightMeasurementParameterNames(toolId) is { Count: > 0 } optional
            ? $" and may add {string.Join(", ", optional)}"
            : string.Empty;
}
