using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.Dialogs;

/// <summary>
/// Owns Shell dialogs and operating-system launch adapters for current Run
/// Record and evidence artifacts. It delegates durable state to the ViewModel.
/// </summary>
internal sealed class ShellEvidenceDialogController
{
    private readonly Window owner;
    private readonly ShellMainWindowViewModel viewModel;
    private readonly ShellEvidenceDialogErrors errors;

    public ShellEvidenceDialogController(
        Window owner,
        ShellMainWindowViewModel viewModel,
        ShellEvidenceDialogErrors errors)
    {
        this.owner = owner;
        this.viewModel = viewModel;
        this.errors = errors;
    }

    public void OpenEvidenceArtifact(object? sender, EvidenceArtifactOpenRequestEventArgs args)
    {
        if (!File.Exists(args.Path) && !Directory.Exists(args.Path))
        {
            errors.ArtifactMissing(args.Label, args.Path);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(args.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            errors.ArtifactOpenFailure(args.Label, args.Path, ex.Message);
        }
    }

    public void OpenRunRecord(object? sender, EventArgs args)
    {
        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        var dialog = new OpenFileDialog
        {
            Title = english ? "Open Run Record" : "실행 기록 열기",
            Filter = english
                ? "OpenVisionLab Run Record (*.json)|*.json|All files (*.*)|*.*"
                : "OpenVisionLab 실행 기록 (*.json)|*.json|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(owner) == true
            && !viewModel.LoadRunRecord(dialog.FileName, out var message))
        {
            errors.RunRecordOpenFailure(message);
        }
    }

    public void ExportRunRecord(object? sender, EventArgs args)
    {
        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        var dialog = new OpenFolderDialog
        {
            Title = english ? "Export Run Record Bundle" : "실행 기록 묶음 내보내기",
            Multiselect = false
        };
        if (dialog.ShowDialog(owner) == true
            && !viewModel.ExportCurrentRunRecordBundle(dialog.FolderName, out var message))
        {
            errors.RunRecordExportFailure(message);
        }
    }

    public void ExportPrivacySafeSupportBundle(object? sender, EventArgs args)
    {
        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        var dialog = new OpenFolderDialog
        {
            Title = english
                ? "Export Privacy-Safe Support Bundle"
                : "개인정보 안전 지원 번들 내보내기",
            Multiselect = false
        };
        if (dialog.ShowDialog(owner) == true
            && !viewModel.ExportPrivacySafeSupportBundle(dialog.FolderName, out var message))
        {
            errors.RunRecordExportFailure(message);
        }
    }
}

internal sealed record ShellEvidenceDialogErrors
{
    public required Action<string, string> ArtifactMissing { get; init; }
    public required Action<string, string, string> ArtifactOpenFailure { get; init; }
    public required Action<string> RunRecordOpenFailure { get; init; }
    public required Action<string> RunRecordExportFailure { get; init; }
}
