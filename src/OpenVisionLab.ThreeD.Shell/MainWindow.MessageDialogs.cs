extern alias OvlMessageDialogs;

using WpfMessageDialog = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialog;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow
{
    private static string DialogText(string key, string korean, string english) =>
        ThreeDLocalization.Shared.Resolve(key, korean, english);

    private void ShowLoadSourceFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.LoadSource.Title",
            "3D 입력 불러오기",
            "Load 3D Input",
            "ThreeD.Dialog.LoadSource.Failed",
            "3D 입력을 불러오지 못했습니다. 파일 형식과 데이터를 확인한 뒤 다시 시도하세요.",
            "The 3D input could not be loaded. Check the file format and data, then try again.",
            details);

    private void ShowMissingToolLabStep(string toolName) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Info,
            "ThreeD.Dialog.ToolLab.StepRequired.Title",
            $"{toolName} 도구 랩",
            $"{toolName} Tool Lab",
            "ThreeD.Dialog.ToolLab.StepRequired.Message",
            $"도구 랩을 열기 전에 레시피에 {toolName} 단계를 추가하거나 기존 단계를 여세요.",
            $"Add or open a {toolName} step in the recipe before opening its Tool Lab.");

    private void ShowRecipeSaveFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RecipeSave.Title",
            "레시피 저장",
            "Save Recipe",
            "ThreeD.Dialog.RecipeSave.Failed",
            "레시피 파일을 저장할 수 없습니다. 표시된 파일 또는 구조 오류를 확인하세요.",
            "The recipe file could not be saved. Check the listed file or structural error.",
            details);

    private void ShowRecipeFileUnavailable(string path) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Info,
            "ThreeD.Dialog.RecipeOpen.Unavailable.Title",
            "레시피 열기",
            "Open Recipe",
            "ThreeD.Dialog.RecipeOpen.Unavailable.Message",
            $"레시피 파일을 찾을 수 없습니다.{Environment.NewLine}{path}",
            $"The recipe file is unavailable.{Environment.NewLine}{path}");

    private void ShowRecipeOpenFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RecipeOpen.Failed.Title",
            "레시피 열기 실패",
            "Open Recipe Failed",
            "ThreeD.Dialog.RecipeOpen.Failed.Message",
            "레시피를 열지 못했습니다. 파일 내용과 버전을 확인하세요.",
            "The recipe could not be opened. Check its contents and version.",
            details);

    private void ShowRecipeSourceNotReady() =>
        ShowStudioDialog(
            WpfMessageDialogKind.Info,
            "ThreeD.Dialog.RecipeSource.NotReady.Title",
            "레시피 입력 확인",
            "Recipe Input Check",
            "ThreeD.Dialog.RecipeSource.NotReady.Message",
            $"레시피는 열렸지만 3D 입력이 준비되지 않았습니다. 레시피는 계속 편집할 수 있으며 검사는 실행되지 않았습니다.{Environment.NewLine}{Environment.NewLine}{_viewModel.Workbench.LocalizedSourceReadinessSummary}",
            $"The recipe was opened, but its 3D input is not ready. The recipe remains editable and no inspection was run.{Environment.NewLine}{Environment.NewLine}{_viewModel.Workbench.LocalizedSourceReadinessSummary}");

    private void ShowRecipeSourceLoadFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RecipeSource.LoadFailed.Title",
            "레시피 입력 불러오기 실패",
            "Recipe Input Load Failed",
            "ThreeD.Dialog.RecipeSource.LoadFailed.Message",
            "레시피의 3D 입력을 불러오지 못했습니다. 유효한 C3D 입력을 다시 연결하세요.",
            "The recipe's 3D input could not be loaded. Relink a valid C3D input.",
            details);

    private WpfMessageDialogResult ConfirmUnsavedRecipeChanges() =>
        WpfMessageDialog.Show(
            GetRecipeLifecycleDialogOwner(),
            new WpfMessageDialogOptions
            {
                Title = DialogText("ThreeD.Dialog.UnsavedRecipe.Title", "저장하지 않은 레시피", "Unsaved Recipe"),
                Message = DialogText(
                    "ThreeD.Dialog.UnsavedRecipe.Message",
                    "현재 레시피의 변경 내용을 저장하시겠습니까?",
                    "Save changes to the current recipe?"),
                Kind = WpfMessageDialogKind.Question,
                Buttons = WpfMessageDialogButtons.YesNoCancel,
                DefaultResult = WpfMessageDialogResult.Yes,
                PrimaryButtonText = DialogText("ThreeD.Dialog.UnsavedRecipe.Save", "저장", "Save"),
                SecondaryButtonText = DialogText("ThreeD.Dialog.UnsavedRecipe.DoNotSave", "저장 안 함", "Don't Save"),
                TertiaryButtonText = DialogText("ThreeD.Dialog.UnsavedRecipe.Cancel", "취소", "Cancel")
            });

    private WpfMessageDialogResult ConfirmPendingParameterChanges() =>
        ShowStudioDialog(
            WpfMessageDialogKind.Question,
            WpfMessageDialogButtons.YesNoCancel,
            "ThreeD.Dialog.PendingParameters.Title",
            "적용하지 않은 단계 파라미터",
            "Unapplied Step Parameters",
            "ThreeD.Dialog.PendingParameters.Message",
            "선택한 단계의 파라미터 변경을 적용하시겠습니까? ‘아니오’를 선택하면 아직 적용하지 않은 PropertyGrid 변경만 취소됩니다.",
            "Apply the selected step's parameter changes? Choosing No discards only the unapplied PropertyGrid draft.");

    private void ShowParameterApplyFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.Parameters.Failed.Title",
            "단계 파라미터",
            "Step Parameters",
            "ThreeD.Dialog.Parameters.Failed.Message",
            "단계 파라미터를 적용하지 못했습니다. 입력값을 확인하세요.",
            "The step parameters could not be applied. Check the entered values.",
            details);

    private void ShowEvidenceArtifactMissing(string label, string path) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.Evidence.Missing.Title",
            "증거 파일 열기",
            "Open Evidence File",
            "ThreeD.Dialog.Evidence.Missing.Message",
            $"{label} 파일을 찾을 수 없습니다.{Environment.NewLine}{Environment.NewLine}{path}",
            $"The {label} file was not found.{Environment.NewLine}{Environment.NewLine}{path}");

    private void ShowEvidenceArtifactOpenFailure(string label, string path, string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.Evidence.OpenFailed.Title",
            "증거 파일 열기 실패",
            "Open Evidence File Failed",
            "ThreeD.Dialog.Evidence.OpenFailed.Message",
            $"{label} 파일을 열지 못했습니다.{Environment.NewLine}{Environment.NewLine}{path}",
            $"The {label} file could not be opened.{Environment.NewLine}{Environment.NewLine}{path}",
            details);

    private void ShowRunRecordOpenFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RunRecord.OpenFailed.Title",
            "\uC2E4\uD589 \uAE30\uB85D \uC5F4\uAE30 \uC2E4\uD328",
            "Open Run Record Failed",
            "ThreeD.Dialog.RunRecord.OpenFailed.Message",
            "\uC2E4\uD589 \uAE30\uB85D\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. JSON \uD30C\uC77C\uACFC \uC2A4\uD0A4\uB9C8\uB97C \uD655\uC778\uD558\uC138\uC694.",
            "The Run Record could not be read. Check the JSON file and schema.",
            details);

    private void ShowRunRecordExportFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RunRecord.ExportFailed.Title",
            "\uC2E4\uD589 \uAE30\uB85D \uB0B4\uBCF4\uB0B4\uAE30 \uC2E4\uD328",
            "Export Run Record Failed",
            "ThreeD.Dialog.RunRecord.ExportFailed.Message",
            "\uC2E4\uD589 \uAE30\uB85D JSON\uACFC \uBCF4\uACE0\uC11C\uB97C \uB0B4\uBCF4\uB0BC \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB300\uC0C1 \uD3F4\uB354 \uAD8C\uD55C\uACFC \uD30C\uC77C\uC744 \uD655\uC778\uD558\uC138\uC694.",
            "The Run Record JSON and reports could not be exported. Check the target folder permissions and files.",
            details);

    private void ShowStudioDialog(
        WpfMessageDialogKind kind,
        string titleKey,
        string koreanTitle,
        string englishTitle,
        string messageKey,
        string koreanMessage,
        string englishMessage,
        string details = "") =>
        ShowStudioDialog(
            kind,
            WpfMessageDialogButtons.OK,
            titleKey,
            koreanTitle,
            englishTitle,
            messageKey,
            koreanMessage,
            englishMessage,
            details);

    private WpfMessageDialogResult ShowStudioDialog(
        WpfMessageDialogKind kind,
        WpfMessageDialogButtons buttons,
        string titleKey,
        string koreanTitle,
        string englishTitle,
        string messageKey,
        string koreanMessage,
        string englishMessage,
        string details = "") =>
        WpfMessageDialog.Show(
            GetRecipeLifecycleDialogOwner(),
            new WpfMessageDialogOptions
            {
                Title = DialogText(titleKey, koreanTitle, englishTitle),
                Message = DialogText(messageKey, koreanMessage, englishMessage),
                Details = details,
                Kind = kind,
                Buttons = buttons
            });
}
