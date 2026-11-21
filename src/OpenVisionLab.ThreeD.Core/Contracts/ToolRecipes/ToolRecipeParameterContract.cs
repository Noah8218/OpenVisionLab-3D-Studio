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
}
