namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record ViewerWorkspaceCandidateItem(
    string Id,
    string DisplayName,
    ViewerWorkspaceCandidateKind Kind,
    string SourcePath,
    string Contract,
    string State,
    bool IsSource)
{
    public bool Equals(ViewerWorkspaceCandidateItem? other) =>
        other is not null
        && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
}

public enum ViewerWorkspaceCandidateKind
{
    HeightImage,
    ThreeDArtifact
}
