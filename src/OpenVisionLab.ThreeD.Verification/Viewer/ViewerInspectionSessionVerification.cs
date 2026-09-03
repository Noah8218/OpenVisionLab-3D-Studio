using System.IO;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerInspectionSessionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer inspection-session verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        try
        {
            var session = new ViewerInspectionSession();
            Check(
                "default state is the synthetic preview identity",
                session.ActiveKind == ViewerInspectionKind.SyntheticHeightDeviation
                && session.SourceEntityId == MainWindowViewModel.PointCloudEntityId
                && session.ResultEntityId == MainWindowViewModel.SyntheticResultEntityId,
                $"{session.ActiveKind}|{session.SourceEntityId}|{session.ResultEntityId}");

            var identities = new List<(string PreviewLayerId, string ResultEntityId, string SourceEntityId)>();
            foreach (var kind in Enum.GetValues<ViewerInspectionKind>())
            {
                session.Activate(kind);
                if (session.ActiveKind != kind
                    || !session.PreviewLayerId.StartsWith("layer.preview.", StringComparison.Ordinal)
                    || !session.ResultEntityId.StartsWith("result.", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(session.PreviewLayerName)
                    || string.IsNullOrWhiteSpace(session.SourceEntityId)
                    || string.IsNullOrWhiteSpace(session.ResultEntityName))
                {
                    throw new InvalidOperationException($"Identity is incomplete for {kind}.");
                }

                identities.Add((session.PreviewLayerId, session.ResultEntityId, session.SourceEntityId));
            }

            Check(
                "every inspection kind resolves a complete identity",
                identities.Count == Enum.GetValues<ViewerInspectionKind>().Length,
                $"count={identities.Count}");
            Check(
                "preview and result identities are unique by inspection kind",
                identities.Select(identity => identity.PreviewLayerId).Distinct(StringComparer.Ordinal).Count() == identities.Count
                && identities.Select(identity => identity.ResultEntityId).Distinct(StringComparer.Ordinal).Count() == identities.Count,
                $"preview={identities.Select(identity => identity.PreviewLayerId).Distinct(StringComparer.Ordinal).Count()}|result={identities.Select(identity => identity.ResultEntityId).Distinct(StringComparer.Ordinal).Count()}");
            Check(
                "session source identities match the existing Viewer sources",
                identities.Select(identity => identity.SourceEntityId).Distinct(StringComparer.Ordinal).Order().SequenceEqual(
                    new[]
                    {
                        MainWindowViewModel.C3DEntityId,
                        MainWindowViewModel.C3DWarpageEntityId,
                        MainWindowViewModel.LazEntityId,
                        MainWindowViewModel.PointCloudEntityId
                    }.Order(),
                    StringComparer.Ordinal),
                string.Join(",", identities.Select(identity => identity.SourceEntityId).Distinct(StringComparer.Ordinal)));

            session.Activate(ViewerInspectionKind.C3DVolume);
            session.Reset();
            Check(
                "reset restores the complete synthetic identity",
                session.ActiveKind == ViewerInspectionKind.SyntheticHeightDeviation
                && session.PreviewLayerId == "layer.preview.synthetic-height-deviation"
                && session.PreviewLayerName == "Preview: Synthetic Height Deviation"
                && session.SourceEntityId == MainWindowViewModel.PointCloudEntityId
                && session.ResultEntityId == MainWindowViewModel.SyntheticResultEntityId
                && session.ResultEntityName == "Published Synthetic Height Deviation",
                $"{session.ActiveKind}|{session.PreviewLayerId}|{session.ResultEntityId}");

            summary = $"Viewer inspection-session verification: Pass ({passed}/5 checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Viewer inspection-session verification: Fail after {passed}/5 checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}
