namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record ViewerWorkspaceCandidateItem(
    string Id,
    string DisplayName,
    ViewerWorkspaceCandidateKind Kind,
    string SourcePath,
    string Contract,
    string State,
    bool IsSource);

public enum ViewerWorkspaceCandidateKind
{
    HeightImage,
    ThreeDArtifact
}
