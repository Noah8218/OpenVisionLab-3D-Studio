using System.IO;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool HasRecipeIdentity => !string.IsNullOrWhiteSpace(RecipePath);

    public string LocalizedSourceReadinessSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? Localization.SourceNotSelected
        : !string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            ? $"{Localization.SourceUnsupportedFormat} ({Source.Format})"
            : !File.Exists(Source.Path)
                ? Localization.SourceMissing
                : sourceIdentityErrors.Count > 0
                    ? Localization.SourceIdentityMismatch
                    : loadedSourceBinding is null
                        ? Localization.SourceUnreadable
                        : string.Format(
                            Localization.SourceReadyFormat,
                            loadedSourceBinding.GridWidth,
                            loadedSourceBinding.GridHeight);

    public string LocalizedRecipePathSummary => string.IsNullOrWhiteSpace(RecipePath)
        ? Localization.NotSavedYet
        : RecipePath;

    public string LocalizedRecipeSaveBlocker => Localization.RecipeSaveBlockedCorrections;

    public string LocalizedRecipeStateSummary
    {
        get
        {
            var validationState = sourceIdentityErrors.Count > 0
                ? string.Format(Localization.SourceCorrectionsFormat, sourceIdentityErrors.Count)
                : sourceBindingErrors.Count > 0
                    ? string.Format(Localization.StaleSelectionsFormat, sourceBindingErrors.Count)
                    : validation.IsValid
                        ? validation.Warnings.Count == 0
                            ? Localization.Valid
                            : string.Format(Localization.ValidWarningsFormat, validation.Warnings.Count)
                        : storageValidation.IsValid
                            ? string.Format(Localization.ExecutionRequirementsFormat, validation.Errors.Count)
                            : string.Format(Localization.CorrectionsFormat, storageValidation.Errors.Count);
            var saveState = IsDirty
                ? Localization.Modified
                : string.IsNullOrWhiteSpace(RecipePath)
                    ? Localization.Unsaved
                    : Localization.Saved;
            return $"{validationState} | {saveState}";
        }
    }

    private void InitializeFirstRecipeUx()
    {
        OpenVisionLanguageService.LanguageChanged += OnFirstRecipeLanguageChanged;
    }

    private void OnFirstRecipeLanguageChanged(object? sender, EventArgs args) =>
        NotifyFirstRecipeUx();

    private void NotifyFirstRecipeUx()
    {
        OnPropertyChanged(nameof(LocalizedSourceReadinessSummary));
        OnPropertyChanged(nameof(HasRecipeIdentity));
        OnPropertyChanged(nameof(LocalizedRecipePathSummary));
        OnPropertyChanged(nameof(LocalizedRecipeSaveBlocker));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
    }
}
