using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace OpenVisionLab.ThreeD.Shell.Dialogs;

/// <summary>
/// Owns only Shell source-file selection dialogs. Calibration, validation-set
/// state, source loading, and workflow policy remain with their existing owners.
/// </summary>
internal sealed class ShellSourceFileDialogService
{
    private readonly Func<Window> getOwner;

    public ShellSourceFileDialogService(Func<Window> getOwner)
    {
        this.getOwner = getOwner ?? throw new ArgumentNullException(nameof(getOwner));
    }

    public bool TrySelectC3DSourcePath(out string path)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText(
                "ThreeD.FileDialog.LoadC3D.Title",
                "레시피 티칭용 C3D 입력 불러오기",
                "Load C3D Input for Recipe Teaching"),
            Filter = DialogText(
                "ThreeD.FileDialog.LoadC3D.Filter",
                "C3D 높이 맵 (*.C3D)|*.C3D|모든 파일 (*.*)|*.*",
                "C3D height map (*.C3D)|*.C3D|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        return TrySelectPath(dialog, out path);
    }

    public bool TrySelect3DDataPath(out string path)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText(
                "ThreeD.FileDialog.Import3D.Title",
                "3D 데이터 가져오기",
                "Import 3D Data"),
            Filter = DialogText(
                "ThreeD.FileDialog.Import3D.Filter",
                "3D: C3D/GLB/STL/LAS/LAZ|*.C3D;*.GLB;*.STL;*.LAS;*.LAZ|C3D 높이 맵|*.C3D|GLB 메시|*.GLB|STL 메시|*.STL|LAS/LAZ 포인트 클라우드|*.LAS;*.LAZ",
                "3D: C3D/GLB/STL/LAS/LAZ|*.C3D;*.GLB;*.STL;*.LAS;*.LAZ|C3D height map|*.C3D|GLB mesh|*.GLB|STL mesh|*.STL|LAS/LAZ point cloud|*.LAS;*.LAZ"),
            CheckFileExists = true,
            Multiselect = false
        };
        return TrySelectPath(dialog, out path);
    }

    public bool TrySelectFirstRecipeSourcePath(
        string currentSourcePath,
        string currentFolderPath,
        out string path)
    {
        var source = currentSourcePath.Trim();
        var folder = currentFolderPath.Trim();
        var dialog = new OpenFileDialog
        {
            Title = DialogText(
                "ThreeD.FileDialog.FirstRecipeSource.Title",
                "새 레시피의 C3D 입력 선택",
                "Select C3D Input for New Recipe"),
            Filter = DialogText(
                "ThreeD.FileDialog.LoadC3D.Filter",
                "C3D 높이 맵 (*.C3D)|*.C3D|모든 파일 (*.*)|*.*",
                "C3D height map (*.C3D)|*.C3D|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = File.Exists(source)
                ? Path.GetDirectoryName(source)
                : Directory.Exists(folder)
                    ? folder
                    : null
        };
        return TrySelectPath(dialog, out path);
    }

    public bool TrySelectRepeatabilityStudyPath(out string path)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText("ThreeD.FileDialog.LoadRepeatability.Title", "두께 반복성 연구 불러오기", "Load Thickness Repeatability Study"),
            Filter = DialogText("ThreeD.FileDialog.LoadRepeatability.Filter", "두께 반복성 연구 (*.json)|*.json|모든 파일 (*.*)|*.*", "Thickness Repeatability Study (*.json)|*.json|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        return TrySelectPath(dialog, out path);
    }

    public bool TrySelectValidationSetSources(out IReadOnlyList<string> paths)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogText(
                "ThreeD.FileDialog.AddValidationSamples.Title",
                "반복 검증 C3D 샘플 추가",
                "Add Validation C3D Samples"),
            Filter = DialogText(
                "ThreeD.FileDialog.AddValidationSamples.Filter",
                "C3D 높이 맵 (*.c3d)|*.c3d|모든 파일 (*.*)|*.*",
                "C3D height maps (*.c3d)|*.c3d|All files (*.*)|*.*"),
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(getOwner()) != true)
        {
            paths = Array.Empty<string>();
            return false;
        }

        paths = dialog.FileNames;
        return true;
    }

    private bool TrySelectPath(OpenFileDialog dialog, out string path)
    {
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
