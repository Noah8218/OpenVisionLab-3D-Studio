namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using OpenVisionLab.ThreeD.Core;

internal sealed class ToolWorkbenchSourceSession
{
    public ToolRecipeSelectionSourceBinding? SourceBinding { get; private set; }

    public ToolRecipeAcquisitionProvenance? SourceAcquisitionProvenance { get; private set; }

    public ToolRecipeSource? OpenedSourceIdentity { get; private set; }

    public IReadOnlyList<string> SourceIdentityErrors { get; private set; } = [];

    public bool SetSourceBinding(ToolRecipeSelectionSourceBinding? value)
    {
        if (SourceBinding == value)
        {
            return false;
        }

        SourceBinding = value;
        return true;
    }

    public bool SetSourceAcquisitionProvenance(ToolRecipeAcquisitionProvenance? value)
    {
        if (SourceAcquisitionProvenance == value)
        {
            return false;
        }

        SourceAcquisitionProvenance = value;
        return true;
    }

    public void CaptureOpenedSourceIdentity(ToolRecipeSource source) => OpenedSourceIdentity = source;

    public void AcceptCurrentSourceIdentity() => OpenedSourceIdentity = null;

    public bool SetSourceIdentityErrors(IReadOnlyList<string> errors)
    {
        if (ReferenceEquals(SourceIdentityErrors, errors))
        {
            return false;
        }

        SourceIdentityErrors = errors;
        return true;
    }
}
