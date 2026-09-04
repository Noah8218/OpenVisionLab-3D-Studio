extern alias OvlMessageDialogs;

using WpfMessageDialog = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialog;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using System.Windows;

namespace OpenVisionLab.ThreeD.Shell.Dialogs;

/// <summary>
/// Owns Shell message-dialog policy and converts dialog results for the
/// lifecycle and Workbench coordinators. The Window owner is resolved only
/// when a dialog is shown so Recipe Manager ownership remains unchanged.
/// </summary>
internal sealed class ShellMessageDialogController
{
    private readonly Func<Window> getOwner;
    private readonly ShellMainWindowViewModel viewModel;
    private readonly Func<string, string, string, string> dialogText;

    public ShellMessageDialogController(
        Func<Window> getOwner,
        ShellMainWindowViewModel viewModel,
        Func<string, string, string, string> dialogText)
    {
        this.getOwner = getOwner;
        this.viewModel = viewModel;
        this.dialogText = dialogText;
    }

    public void ShowLoadSourceFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.LoadSource.Title",
            "3D 입력 불러오기",
            "Load 3D Input",
            "ThreeD.Dialog.LoadSource.Failed",
            "3D 입력을 불러오지 못했습니다. 파일 형식과 데이터를 확인한 뒤 다시 시도하세요.",
            "The 3D input could not be loaded. Check the file format and data, then try again.",
            details);

    public void ShowMissingToolLabStep(string toolName) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Info,
            "ThreeD.Dialog.ToolLab.StepRequired.Title",
            $"{toolName} 도구 랩",
            $"{toolName} Tool Lab",
            "ThreeD.Dialog.ToolLab.StepRequired.Message",
            $"도구 랩을 열기 전에 레시피에 {toolName} 단계를 추가하거나 기존 단계를 여세요.",
            $"Add or open a {toolName} step in the recipe before opening its Tool Lab.");

    public void ShowRecipeSaveFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RecipeSave.Title",
            "레시피 저장",
            "Save Recipe",
            "ThreeD.Dialog.RecipeSave.Failed",
            "레시피 파일을 저장할 수 없습니다. 표시된 파일 또는 구조 오류를 확인하세요.",
            "The recipe file could not be saved. Check the listed file or structural error.",
            details);

    public void ShowFirstRecipeCreateFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.FirstRecipeCreate.Title",
            "새 레시피 만들기",
            "Create New Recipe",
            "ThreeD.Dialog.FirstRecipeCreate.Failed",
            "선택한 C3D 입력과 시작 작업으로 레시피를 만들 수 없습니다.",
            "The recipe could not be created from the selected C3D input and task starter.",
            details);

    public void ShowFirstRecipeSetupPersistenceFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.FirstRecipeSetupPersistence.Title",
            "첫 레시피 설정 저장",
            "Save First Recipe Setup",
            "ThreeD.Dialog.FirstRecipeSetupPersistence.Failed",
            "레시피는 만들었지만 다음 사용을 위한 설정은 저장하지 못했습니다.",
            "The recipe was created, but its setup could not be saved for next time.",
            details);

    public void ShowRecipeFileUnavailable(string path) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Info,
            "ThreeD.Dialog.RecipeOpen.Unavailable.Title",
            "레시피 열기",
            "Open Recipe",
            "ThreeD.Dialog.RecipeOpen.Unavailable.Message",
            $"레시피 파일을 찾을 수 없습니다.{Environment.NewLine}{path}",
            $"The recipe file is unavailable.{Environment.NewLine}{path}");

    public void ShowRecipeOpenFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RecipeOpen.Failed.Title",
            "레시피 열기 실패",
            "Open Recipe Failed",
            "ThreeD.Dialog.RecipeOpen.Failed.Message",
            "레시피를 열지 못했습니다. 파일 내용과 버전을 확인하세요.",
            "The recipe could not be opened. Check its contents and version.",
            details);

    public void ShowRecipeSourceNotReady() =>
        ShowStudioDialog(
            WpfMessageDialogKind.Info,
            "ThreeD.Dialog.RecipeSource.NotReady.Title",
            "레시피 입력 확인",
            "Recipe Input Check",
            "ThreeD.Dialog.RecipeSource.NotReady.Message",
            $"레시피는 열렸지만 3D 입력이 준비되지 않았습니다. 레시피는 계속 편집할 수 있으며 검사는 실행되지 않았습니다.{Environment.NewLine}{Environment.NewLine}{viewModel.Workbench.LocalizedSourceReadinessSummary}",
            $"The recipe was opened, but its 3D input is not ready. The recipe remains editable and no inspection was run.{Environment.NewLine}{Environment.NewLine}{viewModel.Workbench.LocalizedSourceReadinessSummary}");

    public void ShowRecipeSourceLoadFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RecipeSource.LoadFailed.Title",
            "레시피 입력 불러오기 실패",
            "Recipe Input Load Failed",
            "ThreeD.Dialog.RecipeSource.LoadFailed.Message",
            "레시피의 3D 입력을 불러오지 못했습니다. 유효한 C3D 입력을 다시 연결하세요.",
            "The recipe's 3D input could not be loaded. Relink a valid C3D input.",
            details);

    public WpfMessageDialogResult ConfirmUnsavedRecipeChanges() =>
        WpfMessageDialog.Show(
            getOwner(),
            new WpfMessageDialogOptions
            {
                Title = dialogText("ThreeD.Dialog.UnsavedRecipe.Title", "저장하지 않은 레시피", "Unsaved Recipe"),
                Message = dialogText(
                    "ThreeD.Dialog.UnsavedRecipe.Message",
                    "현재 레시피의 변경 내용을 저장하시겠습니까?",
                    "Save changes to the current recipe?"),
                Kind = WpfMessageDialogKind.Question,
                Buttons = WpfMessageDialogButtons.YesNoCancel,
                DefaultResult = WpfMessageDialogResult.Yes,
                PrimaryButtonText = dialogText("ThreeD.Dialog.UnsavedRecipe.Save", "저장", "Save"),
                SecondaryButtonText = dialogText("ThreeD.Dialog.UnsavedRecipe.DoNotSave", "저장 안 함", "Don't Save"),
                TertiaryButtonText = dialogText("ThreeD.Dialog.UnsavedRecipe.Cancel", "취소", "Cancel")
            });

    public WpfMessageDialogResult ConfirmPendingParameterChanges() =>
        ShowStudioDialog(
            WpfMessageDialogKind.Question,
            WpfMessageDialogButtons.YesNoCancel,
            "ThreeD.Dialog.PendingParameters.Title",
            "적용하지 않은 단계 파라미터",
            "Unapplied Step Parameters",
            "ThreeD.Dialog.PendingParameters.Message",
            "선택한 단계의 파라미터 변경을 적용하시겠습니까? ‘아니오’를 선택하면 아직 적용하지 않은 PropertyGrid 변경만 취소됩니다.",
            "Apply the selected step's parameter changes? Choosing No discards only the unapplied PropertyGrid draft.");

    public void OnWorkbenchRemoveSelectedStepRequested(
        object? sender,
        ToolWorkbenchStepRemovalRequestEventArgs args)
    {
        if (WpfMessageDialog.Show(
                getOwner(),
                CreateRecipeStepRemovalDialogOptions(args)) == WpfMessageDialogResult.Yes)
        {
            viewModel.Workbench.ConfirmSelectedStepRemoval(args.StepId);
        }
    }

    public WpfMessageDialogOptions CreateRecipeStepRemovalDialogOptions(
        ToolWorkbenchStepRemovalRequestEventArgs args)
    {
        var selectionImpact = args.OrphanedSelectionNames.Count == 0
            ? dialogText(
                "ThreeD.Dialog.RemoveStep.NoSelections",
                "연결된 선택 영역은 삭제되지 않습니다.",
                "No teaching selections will be removed.")
            : dialogText(
                "ThreeD.Dialog.RemoveStep.OrphanSelections",
                $"다른 단계가 사용하지 않는 선택 영역 {args.OrphanedSelectionNames.Count}개도 함께 삭제됩니다: {string.Join(", ", args.OrphanedSelectionNames)}",
                $"{args.OrphanedSelectionNames.Count} teaching selection(s) not used by another step will also be removed: {string.Join(", ", args.OrphanedSelectionNames)}");
        return new WpfMessageDialogOptions
        {
            Title = dialogText(
                "ThreeD.Dialog.RemoveStep.Title",
                "레시피 단계 삭제",
                "Remove Recipe Step"),
            Message = dialogText(
                "ThreeD.Dialog.RemoveStep.Message",
                $"'{args.StepName}' 단계를 삭제하시겠습니까?{Environment.NewLine}{Environment.NewLine}{selectionImpact}",
                $"Remove the '{args.StepName}' step?{Environment.NewLine}{Environment.NewLine}{selectionImpact}"),
            Kind = WpfMessageDialogKind.Warning,
            Buttons = WpfMessageDialogButtons.YesNo,
            DefaultResult = WpfMessageDialogResult.No,
            PrimaryButtonText = dialogText(
                "ThreeD.Dialog.RemoveStep.Remove",
                "삭제",
                "Remove"),
            SecondaryButtonText = dialogText(
                "ThreeD.Dialog.RemoveStep.Cancel",
                "취소",
                "Cancel")
        };
    }

    public void ShowParameterApplyFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.Parameters.Failed.Title",
            "단계 파라미터",
            "Step Parameters",
            "ThreeD.Dialog.Parameters.Failed.Message",
            "단계 파라미터를 적용하지 못했습니다. 입력값을 확인하세요.",
            "The step parameters could not be applied. Check the entered values.",
            details);

    public void ShowEvidenceArtifactMissing(string label, string path) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.Evidence.Missing.Title",
            "증거 파일 열기",
            "Open Evidence File",
            "ThreeD.Dialog.Evidence.Missing.Message",
            $"{label} 파일을 찾을 수 없습니다.{Environment.NewLine}{Environment.NewLine}{path}",
            $"The {label} file was not found.{Environment.NewLine}{Environment.NewLine}{path}");

    public void ShowEvidenceArtifactOpenFailure(string label, string path, string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.Evidence.OpenFailed.Title",
            "증거 파일 열기 실패",
            "Open Evidence File Failed",
            "ThreeD.Dialog.Evidence.OpenFailed.Message",
            $"{label} 파일을 열지 못했습니다.{Environment.NewLine}{Environment.NewLine}{path}",
            $"The {label} file could not be opened.{Environment.NewLine}{Environment.NewLine}{path}",
            details);

    public void ShowRunRecordOpenFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RunRecord.OpenFailed.Title",
            "실행 기록 열기 실패",
            "Open Run Record Failed",
            "ThreeD.Dialog.RunRecord.OpenFailed.Message",
            "실행 기록을 읽을 수 없습니다. JSON 파일과 스키마를 확인하세요.",
            "The Run Record could not be read. Check the JSON file and schema.",
            details);

    public void ShowRunRecordExportFailure(string details) =>
        ShowStudioDialog(
            WpfMessageDialogKind.Warning,
            "ThreeD.Dialog.RunRecord.ExportFailed.Title",
            "실행 기록 내보내기 실패",
            "Export Run Record Failed",
            "ThreeD.Dialog.RunRecord.ExportFailed.Message",
            "실행 기록 JSON과 보고서를 내보낼 수 없습니다. 대상 폴더 권한과 파일을 확인하세요.",
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
            getOwner(),
            new WpfMessageDialogOptions
            {
                Title = dialogText(titleKey, koreanTitle, englishTitle),
                Message = dialogText(messageKey, koreanMessage, englishMessage),
                Details = details,
                Kind = kind,
                Buttons = buttons
            });
}
