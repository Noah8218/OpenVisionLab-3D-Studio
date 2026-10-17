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
    public string ViewerName => Contract == "SignedDeltaHeightField" ? DisplayName
        : (IsSource || Kind == ViewerWorkspaceCandidateKind.HeightImage) && !string.IsNullOrWhiteSpace(SourcePath)
        ? System.IO.Path.GetFileName(SourcePath) : DisplayName;

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
