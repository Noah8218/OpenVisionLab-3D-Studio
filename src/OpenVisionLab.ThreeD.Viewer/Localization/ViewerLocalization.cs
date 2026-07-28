using System.ComponentModel;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Viewer.Localization;

public sealed class ViewerLocalization : INotifyPropertyChanged
{
    private static readonly (string English, string Korean)[] RuntimeReplacements =
    [
        ("generated cube and point cloud loaded", "생성된 큐브와 포인트 클라우드 로드됨"),
        ("recipe comparison matched", "레시피 비교 일치"),
        ("recipe comparison pending", "레시피 비교 대기"),
        ("Runner/UI contract matched", "Runner/UI 계약 일치"),
        ("C3D Height Deviation Rule", "C3D 높이 편차 규칙"),
        ("Balanced: up to", "균형: 최대"),
        ("Detailed: up to", "상세: 최대"),
        ("Fast: up to", "빠름: 최대"),
        ("C3D points", "C3D 포인트"),
        ("LAZ/LAS points", "LAZ/LAS 포인트"),
        ("mesh triangles", "메시 삼각형"),
        ("UI metric:", "UI 측정값:"),
        ("Runner metric:", "Runner 측정값:"),
        ("Key metric:", "핵심 측정값:"),
        ("Recipe steps:", "레시피 단계:"),
        ("Status: Pass", "상태: 통과"),
        ("Status: Fail", "상태: 실패"),
        ("Status: Error", "상태: 오류"),
        ("Evidence: Matched", "증거: 일치"),
        ("Evidence: Pending", "증거: 대기"),
        ("Status:", "상태:"),
        ("Evidence:", "증거:"),
        ("Viewer:", "뷰어:"),
        ("Compare:", "비교:"),
        ("Runner:", "Runner:"),
        ("UI:", "UI:"),
        ("Run:", "실행:"),
        (" / Fail", " / 실패"),
        (" | Fail", " | 실패"),
        (" | Matched", " | 일치"),
        (" | Pending", " | 대기"),
        ("current C3D height deviation", "현재 C3D 높이 편차"),
        ("Generated Unit Cube", "생성된 단위 큐브"),
        ("C3D Thickness Sample", "C3D 두께 샘플"),
        ("No comparison evidence.", "비교 증거 없음"),
        ("Source/result separation:", "소스/결과 분리:"),
        ("Fitted segment and direction", "피팅 선분 및 방향"),
        ("Closest-approach connector", "최근접 연결선"),
        ("Point pair dimensions", "포인트 쌍 치수"),
        ("point cloud", "포인트 클라우드"),
        ("height grid", "높이 그리드"),
        ("height delta", "높이 차이"),
        ("two-point", "2-포인트"),
        ("Cross-section Dimensions", "단면 치수"),
        ("waiting for first frame", "첫 프레임 대기 중"),
        ("Right-handed", "오른손 좌표계"),
        ("Y-up height", "Y축 높이"),
        ("raw-height retained", "원시 높이 유지"),
        ("source = aligned", "소스 = 정렬"),
        ("source frame", "소스 좌표계"),
        ("source only", "소스만"),
        ("not available", "사용할 수 없음"),
        ("not aligned", "미정렬"),
        ("not loaded", "로드되지 않음"),
        ("not set", "미설정"),
        ("preview not run", "미리보기 미실행"),
        ("visible", "표시"),
        ("hidden", "숨김"),
        ("updated", "갱신됨"),
        ("requires", "필요:"),
        ("failed:", "실패:"),
        ("cancelled", "취소됨"),
        ("retained", "유지됨"),
        ("Model units:", "모델 단위:"),
        ("Camera:", "카메라:"),
        ("Performance:", "성능:"),
        ("Transform:", "변환:"),
        ("Alignment:", "정렬:"),
        ("Mapping:", "매핑:"),
        ("Selection mode:", "선택 모드:"),
        ("Render density:", "렌더 밀도:"),
        ("Point size:", "포인트 크기:"),
        ("Viewer hosted", "뷰어 연결됨"),
        ("Cube width:", "큐브 너비:"),
        ("Expected center:", "예상 중심:"),
        ("Validation query:", "검증 쿼리:"),
        ("Actual:", "실제:"),
        ("Nominal:", "기준:"),
        ("Frame:", "좌표계:"),
        ("Units:", "단위:"),
        ("Recipe:", "레시피:"),
        ("Source:", "소스:"),
        ("Tolerance:", "허용오차:"),
        ("Published result:", "게시 결과:"),
        ("Ready:", "준비:"),
        ("X red", "X 빨강"),
        ("Y green", "Y 초록"),
        ("Z blue", "Z 파랑"),
        ("Identity", "항등"),
        ("unitless", "단위 없음"),
        ("orbit", "회전"),
        ("(none)", "(없음)")
    ];

    public static ViewerLocalization Shared { get; } = new();

    private ViewerLocalization() =>
        OpenVisionLanguageService.LanguageChanged += (_, _) => Refresh();

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Revision { get; private set; }

    public string Resolve(string key, string korean, string english)
    {
        var value = OpenVisionLanguageService.T(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean
            : value;
    }

    public string LocalizeRuntimeText(object? value, string? mode = null)
    {
        var text = value?.ToString() ?? string.Empty;
        if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English)
        {
            return Format(text, mode, "Selection", "Pick");
        }

        var localized = text switch
        {
            "Point" => "포인트",
            "Points" => "포인트",
            "Profile" => "프로파일",
            "Wireframe" => "와이어프레임",
            "Surface" => "표면",
            "Surface + Edges" => "표면 + 엣지",
            "Source" => "소스",
            "Solid" => "단색",
            "Grayscale" => "회색조",
            "Height" => "높이",
            "Thermal" => "열화상",
            "Deviation" => "편차",
            "Balanced" => "균형",
            "Detailed" => "상세",
            "Fast" => "빠름",
            _ => text
        };
        foreach (var (english, korean) in RuntimeReplacements)
        {
            localized = localized.Replace(english, korean, StringComparison.OrdinalIgnoreCase);
        }

        return Format(localized, mode, "선택", "선택점");
    }

    private static string Format(string text, string? mode, string selectionLabel, string pickLabel) =>
        mode switch
        {
            "Selection" => $"{selectionLabel}: {text}",
            "Pick" => $"{pickLabel}: {text}",
            "Mode" => $"{(selectionLabel == "Selection" ? "Mode" : "모드")}: {text}",
            "Active" => $"{(selectionLabel == "Selection" ? "Active" : "활성")}: {text}",
            _ => text
        };

    private void Refresh()
    {
        Revision++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Revision)));
    }
}
