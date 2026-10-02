namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using OpenVisionLab.ThreeD.Core;

internal sealed class ToolWorkbenchRecipeSession
{
    internal const string DefaultName = "Untitled 3D Inspection";

    public string SchemaVersion { get; private set; } = ToolRecipeDocument.CurrentSchemaVersion;

    public string Name { get; private set; } = DefaultName;

    public string? Category { get; private set; }

    public string? InspectionTarget { get; private set; }

    public string? Description { get; private set; }

    public string? Path { get; private set; }

    public string? SavedSha256 { get; private set; }

    public bool IsDirty { get; private set; }

    public ToolRecipeValidationResult Validation { get; private set; } = new([], []);

    public ToolRecipeValidationResult StorageValidation { get; private set; } = new([], []);

    public IReadOnlyList<string> SourceBindingErrors { get; private set; } = [];

    public bool SetSchemaVersion(string value)
    {
        if (SchemaVersion == value)
        {
            return false;
        }

        SchemaVersion = value;
        return true;
    }

    public bool SetName(string value)
    {
        if (Name == value)
        {
            return false;
        }

        Name = value;
        return true;
    }

    public bool SetMetadata(string? category, string? inspectionTarget, string? description)
    {
        if (Category == category && InspectionTarget == inspectionTarget && Description == description)
        {
            return false;
        }

        Category = category;
        InspectionTarget = inspectionTarget;
        Description = description;
        return true;
    }

    public bool SetPath(string? value)
    {
        if (Path == value)
        {
            return false;
        }

        Path = value;
        SavedSha256 = null;
        return true;
    }

    public void AcceptSavedIdentity(string sha256) => SavedSha256 = sha256;

    public bool SetDirty(bool value)
    {
        if (IsDirty == value)
        {
            return false;
        }

        IsDirty = value;
        return true;
    }

    public void SetValidation(
        ToolRecipeValidationResult validation,
        ToolRecipeValidationResult storageValidation,
        IReadOnlyList<string> sourceBindingErrors)
    {
        Validation = validation;
        StorageValidation = storageValidation;
        SourceBindingErrors = sourceBindingErrors;
    }
}
