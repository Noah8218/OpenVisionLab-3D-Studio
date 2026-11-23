using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// 두께 기준면 RMS 상한 초과 오류의 한글 문구. <see cref="DualSurfaceThicknessRule"/>가 만든 영어 메시지
/// ("접두 문구 {RMS} &gt; {상한} {단위}. 꼬리 문구")에서 숫자를 그대로 가져와 한글 문장을 만든다.
/// 결과 표의 사유와 검증 화면의 오류 사유가 같은 문장을 쓰도록 한 곳에 둔다. 영어 화면이거나 이 메시지가 아니면 원문을 돌려준다.
/// </summary>
internal static class ThicknessReferenceFitMessage
{
    internal static bool IsReferenceFitExceeded(string? message) =>
        message is not null && message.Contains(DualSurfaceThicknessRule.ReferenceFitExceededMessagePrefix, StringComparison.Ordinal);

    internal static string Localize(string message)
    {
        if (!IsReferenceFitExceeded(message) || OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English)
        {
            return message;
        }

        // 실행 전체 메시지에 앞 글자가 붙어 있어도 접두 문구 뒤만 해석한다.
        var start = message.IndexOf(DualSurfaceThicknessRule.ReferenceFitExceededMessagePrefix, StringComparison.Ordinal)
            + DualSurfaceThicknessRule.ReferenceFitExceededMessagePrefix.Length;
        var body = message[start..].Trim();
        var suffixAt = body.IndexOf(DualSurfaceThicknessRule.ReferenceFitExceededMessageSuffix, StringComparison.Ordinal);
        if (suffixAt >= 0) body = body[..suffixAt];

        var parts = body.Split(" > ", 2, StringSplitOptions.None);
        return parts.Length == 2
            ? $"기준면 맞춤 RMS {parts[0]} > 상한 {parts[1]} · 기준 영역이 평면에서 벗어나 두께를 계산하지 않았습니다."
            : "기준 영역이 평면에서 벗어나 두께를 계산하지 않았습니다.";
    }
}
