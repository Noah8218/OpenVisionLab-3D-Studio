namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using OpenVisionLab.ThreeD.Core;

internal sealed class ToolWorkbenchRecipeSession
{
    public string SchemaVersion { get; private set; } = ToolRecipeDocument.CurrentSchemaVersion;

    public string Name { get; private set; } = "Untitled 3D Inspection";

    public string? Path { get; private set; }

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

    public bool SetPath(string? value)
    {
        if (Path == value)
        {
            return false;
        }

        Path = value;
        return true;
    }

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
