using System.IO;
using System.Windows;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Dialogs;

/// <summary>
/// Owns only recipe file selection dialogs. Recipe state, validation, source
/// loading, Preview, Publish, and Run remain with their existing owners.
/// </summary>
internal sealed class RecipeFileDialogService
{
    private readonly Func<Window> getOwner;

    public RecipeFileDialogService(Func<Window> getOwner)
    {
        this.getOwner = getOwner;
    }

    public bool TrySelectSavePath(string? currentPath, bool forceDialog, out string path)
    {
        var dialog = new SaveFileDialog
        {
            Title = forceDialog
                ? DialogText("ThreeD.FileDialog.SaveRecipeAs.Title", "3D 검사 레시피 다른 이름으로 저장", "Save 3D Inspection Recipe As")
                : DialogText("ThreeD.FileDialog.SaveRecipe.Title", "3D 검사 레시피 저장", "Save 3D Inspection Recipe"),
            Filter = DialogText("ThreeD.FileDialog.SaveRecipe.Filter", "OpenVisionLab 3D 검사 레시피 (*.ov3d-recipe.json)|*.ov3d-recipe.json|기존 티칭 레시피 (*.ov3d-teach.json)|*.ov3d-teach.json|JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "OpenVisionLab 3D inspection recipe (*.ov3d-recipe.json)|*.ov3d-recipe.json|Legacy teaching recipe (*.ov3d-teach.json)|*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*"),
            FileName = string.IsNullOrWhiteSpace(currentPath)
                ? "inspection-recipe.ov3d-recipe.json"
                : Path.GetFileName(currentPath),
            InitialDirectory = string.IsNullOrWhiteSpace(currentPath)
                ? null
                : Path.GetDirectoryName(currentPath),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(getOwner()) != true)
        {
            path = string.Empty;
            return false;
        }

        path = dialog.FileName;
        return true;
    }

    public bool TrySelectOpenPath(out string path)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText("ThreeD.FileDialog.OpenRecipe.Title", "3D 검사 레시피 열기", "Open 3D Inspection Recipe"),
            Filter = DialogText("ThreeD.FileDialog.OpenRecipe.Filter", "OpenVisionLab 3D 검사 레시피 (*.ov3d-recipe.json;*.ov3d-teach.json)|*.ov3d-recipe.json;*.ov3d-teach.json|JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*", "OpenVisionLab 3D inspection recipe (*.ov3d-recipe.json;*.ov3d-teach.json)|*.ov3d-recipe.json;*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(getOwner()) != true)
        {
            path = string.Empty;
            return false;
        }

        path = dialog.FileName;
        return true;
    }

    private static string DialogText(string key, string korean, string english) =>
        ThreeDLocalization.Shared.Resolve(key, korean, english);
}
