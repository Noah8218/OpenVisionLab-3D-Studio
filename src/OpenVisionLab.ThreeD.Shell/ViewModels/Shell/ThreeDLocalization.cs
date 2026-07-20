using System.ComponentModel;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// Shared, view-only language surface for the 3D Studio authoring UI.
/// The OpenVisionLab localization catalog owns persistence and language changes.
/// </summary>
public sealed class ThreeDLocalization : INotifyPropertyChanged
{
    private static readonly string[] PropertyNames =
    [
        nameof(StudioSubtitle), nameof(Teach), nameof(Calibrate), nameof(RecipeManager), nameof(ToolLabs),
        nameof(AdvancedLayout), nameof(Language), nameof(OpenRecipeManagerToolTip), nameof(OpenToolLabsToolTip),
        nameof(OpenAdvancedToolTip), nameof(Filter), nameof(HeightDifferenceEdge), nameof(LineIntersection),
        nameof(LandmarkCorrespondence), nameof(ToolboxAndEntities), nameof(Viewer), nameof(StepParameters),
        nameof(PipelineValidation), nameof(SessionLog), nameof(HeightProfile), nameof(FitDiagnostics),
        nameof(IntersectionEvidence), nameof(CorrespondenceEvidence), nameof(NavigatorHint), nameof(RecipeSource),
        nameof(RecipeNavigator), nameof(AddInspectionStep), nameof(StepProperties), nameof(NoRecipeStepSelected),
        nameof(NoRecipeStepSelectedDetail), nameof(RecipePipelineTeachReview), nameof(Validate), nameof(MoveUp),
        nameof(MoveDown), nameof(Remove), nameof(ColumnNumber), nameof(ColumnTool), nameof(ColumnInputs),
        nameof(ColumnTypedOutput), nameof(ColumnState), nameof(Preview), nameof(Run), nameof(Publish), nameof(Cancel),
        nameof(SelectedPaletteItem), nameof(Input), nameof(Output), nameof(ParameterAdapter), nameof(Inputs),
        nameof(ExpectedData), nameof(InputEntities), nameof(ToolboxSequenceHint), nameof(SelectedRoute),
        nameof(OpenSelectedToolLab)
    ];

    public static ThreeDLocalization Shared { get; } = new();

    private ThreeDLocalization() => OpenVisionLanguageService.LanguageChanged += (_, _) => Refresh();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StudioSubtitle => T("ThreeD.Header.StudioSubtitle", "3D \uAC80\uC0AC \uB808\uC2DC\uD53C \uC6CC\uD06C\uBCA4\uCE58", "3D inspection recipe workbench");
    public string Teach => T("ThreeD.Header.Teach", "\uD2F0\uCE6D", "Teach");
    public string Calibrate => T("ThreeD.Header.Calibrate", "\uAD50\uC815", "Calibrate");
    public string RecipeManager => T("ThreeD.Header.RecipeManager", "\uB808\uC2DC\uD53C \uAD00\uB9AC\uC790", "Recipe Manager");
    public string ToolLabs => T("ThreeD.Header.ToolLabs", "\uD234 \uB7A9", "Tool Labs");
    public string AdvancedLayout => T("ThreeD.Header.AdvancedLayout", "\uACE0\uAE09 \uB808\uC774\uC544\uC6C3", "Advanced layout");
    public string Language => T("ThreeD.Header.Language", "\uC5B8\uC5B4", "Language");
    public string OpenRecipeManagerToolTip => T("ThreeD.Header.OpenRecipeManagerToolTip", "\uBCC4\uB3C4 \uB808\uC2DC\uD53C \uC218\uBA85\uC8FC\uAE30 \uCC3D\uC744 \uC5FD\uB2C8\uB2E4.", "Open the separate recipe lifecycle window.");
    public string OpenToolLabsToolTip => T("ThreeD.Header.OpenToolLabsToolTip", "\uAE30\uC874 \uB3C4\uAD6C\uC758 \uC785\uB825\u00B7\uCD9C\uB825\u00B7\uC99D\uAC70 \uC804\uC6A9 \uBDF0\uB97C \uC5FD\uB2C8\uB2E4.", "Open an existing tool's focused input, output, and evidence view.");
    public string OpenAdvancedToolTip => T("ThreeD.Header.OpenAdvancedToolTip", "\uAE30\uC874 \uC9C4\uB2E8 \uC804\uC6A9 \uB808\uC774\uC544\uC6C3\uC744 \uC5FD\uB2C8\uB2E4.", "Open the existing diagnostic dock layout.");
    public string Filter => T("ThreeD.Tool.Filter", "\uD544\uD130", "Filter");
    public string HeightDifferenceEdge => T("ThreeD.Tool.HeightDifferenceEdge", "\uB192\uC774 \uCC28\uC774 \uC5E3\uC9C0", "Height Difference Edge");
    public string LineIntersection => T("ThreeD.Tool.LineIntersection", "\uB77C\uC778 \uAD50\uCC28\uC810", "Line Intersection");
    public string LandmarkCorrespondence => T("ThreeD.Tool.LandmarkCorrespondence", "\uB79C\uB4DC\uB9C8\uD06C \uB300\uC751", "Landmark Correspondence");
    public string ToolboxAndEntities => T("ThreeD.Workbench.ToolboxAndEntities", "\uD234\uBC15\uC2A4 \uBC0F \uC5D4\uD2F0\uD2F0", "Toolbox & Entities");
    public string Viewer => T("ThreeD.Workbench.Viewer", "3D \uBDF0", "3D View");
    public string StepParameters => T("ThreeD.Workbench.StepParameters", "\uB2E8\uACC4 \uD30C\uB77C\uBBF8\uD130", "Step Parameters");
    public string PipelineValidation => T("ThreeD.Workbench.PipelineValidation", "\uD30C\uC774\uD504\uB77C\uC778 / \uAC80\uC99D", "Pipeline / Validation");
    public string SessionLog => T("ThreeD.Workbench.SessionLog", "\uC138\uC158 \uB85C\uADF8", "Session Log");
    public string HeightProfile => T("ThreeD.Workbench.HeightProfile", "\uB192\uC774 \uD504\uB85C\uD30C\uC77C", "Height Profile");
    public string FitDiagnostics => T("ThreeD.Workbench.FitDiagnostics", "\uD53C\uD305 \uC9C4\uB2E8", "Fit Diagnostics");
    public string IntersectionEvidence => T("ThreeD.Workbench.IntersectionEvidence", "\uAD50\uCC28\uC810 \uC99D\uAC70", "Intersection Evidence");
    public string CorrespondenceEvidence => T("ThreeD.Workbench.CorrespondenceEvidence", "\uB300\uC751 \uC99D\uAC70", "Correspondence Evidence");
    public string NavigatorHint => T("ThreeD.Workbench.NavigatorHint", "\uB808\uC2DC\uD53C \uD0D0\uC0C9\uAE30\uB294 \uC77D\uAE30 \uC6B0\uC120\uC785\uB2C8\uB2E4. \uD30C\uC774\uD504\uB77C\uC778 \uB178\uB4DC\uB97C \uC120\uD0DD\uD574 \uD574\uB2F9 \uB2E8\uACC4 \uD30C\uB77C\uBBF8\uD130\uB97C \uD655\uC778\uD558\uACE0, \uBBF8\uB9AC\uBCF4\uAE30\uC640 \uAC8C\uC2DC\uB97C \uBA85\uC2DC\uC801\uC73C\uB85C \uC2E4\uD589\uD558\uC138\uC694.", "Recipe Navigator is read-first. Select a pipeline node to focus its typed Step Parameters; Preview and Publish remain explicit.");
    public string RecipeSource => T("ThreeD.Workbench.RecipeSource", "\uB808\uC2DC\uD53C \uC18C\uC2A4", "Recipe source");
    public string RecipeNavigator => T("ThreeD.Workbench.RecipeNavigator", "\uB808\uC2DC\uD53C \uD0D0\uC0C9\uAE30", "Recipe Navigator");
    public string AddInspectionStep => T("ThreeD.Workbench.AddInspectionStep", "\uAC80\uC0AC \uB2E8\uACC4 \uCD94\uAC00", "Add inspection step");
    public string StepProperties => T("ThreeD.Workbench.StepProperties", "\uB2E8\uACC4 \uC18D\uC131", "Step properties");
    public string NoRecipeStepSelected => T("ThreeD.Workbench.NoRecipeStepSelected", "\uC120\uD0DD\uB41C \uB808\uC2DC\uD53C \uB2E8\uACC4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4", "No recipe step selected");
    public string NoRecipeStepSelectedDetail => T("ThreeD.Workbench.NoRecipeStepSelectedDetail", "\uD234\uBC15\uC2A4\uC5D0\uC11C \uAC80\uC0AC \uB2E8\uACC4 \uCD94\uAC00\uB97C \uC120\uD0DD\uD558\uC138\uC694. \uCE74\uD0C8\uB85C\uADF8 \uD56D\uBAA9\uB9CC \uC120\uD0DD\uD574\uC11C\uB294 \uB808\uC2DC\uD53C \uB2E8\uACC4\uAC00 \uC0DD\uC131\uB418\uAC70\uB098 \uC218\uC815\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", "Use Add inspection step in Toolbox. Selecting a catalog item alone does not create or edit a recipe step.");
    public string RecipePipelineTeachReview => T("ThreeD.Workbench.RecipePipelineTeachReview", "\uB808\uC2DC\uD53C \uD30C\uC774\uD504\uB77C\uC778 / \uD2F0\uCE6D \uAC80\uD1A0", "Recipe Pipeline / Teach Review");
    public string Validate => T("ThreeD.Command.Validate", "\uAC80\uC99D", "Validate");
    public string MoveUp => T("ThreeD.Command.MoveUp", "\uC704\uB85C", "Up");
    public string MoveDown => T("ThreeD.Command.MoveDown", "\uC544\uB798\uB85C", "Down");
    public string Remove => T("ThreeD.Command.Remove", "\uC0AD\uC81C", "Remove");
    public string ColumnNumber => T("ThreeD.Column.Number", "\uBC88\uD638", "#");
    public string ColumnTool => T("ThreeD.Column.Tool", "\uB3C4\uAD6C", "Tool");
    public string ColumnInputs => T("ThreeD.Column.Inputs", "\uC785\uB825", "Inputs");
    public string ColumnTypedOutput => T("ThreeD.Column.TypedOutput", "\uC815\uC2DD \uCD9C\uB825", "Typed output");
    public string ColumnState => T("ThreeD.Column.State", "\uC0C1\uD0DC", "State");
    public string Preview => T("ThreeD.Command.Preview", "\uBBF8\uB9AC\uBCF4\uAE30", "Preview");
    public string Run => T("ThreeD.Command.Run", "\uC2E4\uD589", "Run");
    public string Publish => T("ThreeD.Command.Publish", "\uAC8C\uC2DC", "Publish");
    public string Cancel => T("ThreeD.Command.Cancel", "\uCDE8\uC18C", "Cancel");
    public string SelectedPaletteItem => T("ThreeD.Workbench.SelectedPaletteItem", "\uC120\uD0DD\uB41C \uD234 \uD56D\uBAA9", "Selected palette item");
    public string Input => T("ThreeD.Label.Input", "\uC785\uB825", "Input");
    public string Output => T("ThreeD.Label.Output", "\uCD9C\uB825", "Output");
    public string ParameterAdapter => T("ThreeD.Label.ParameterAdapter", "\uD30C\uB77C\uBBF8\uD130 \uC5B4\uB311\uD130", "Parameter adapter");
    public string Inputs => T("ThreeD.Label.Inputs", "\uC785\uB825", "Inputs");
    public string ExpectedData => T("ThreeD.Label.ExpectedData", "\uAE30\uB300 \uB370\uC774\uD130", "Expected data");
    public string InputEntities => T("ThreeD.Label.InputEntities", "\uC785\uB825 \uC5D4\uD2F0\uD2F0(\uC138\uBBF8\uCF5C\uB860\uC73C\uB85C \uAD6C\uBD84)", "Input entities (separate with ;)");
    public string ToolboxSequenceHint => T("ThreeD.Workbench.ToolboxSequenceHint", "\uB808\uC2DC\uD53C\uB97C \uC21C\uC11C\uB300\uB85C \uAD6C\uC131\uD558\uC138\uC694: \uC900\uBE44, \uD53C\uCC98, \uAD6C\uC131, \uC815\uB82C, \uCE21\uC815, \uAC80\uD1A0.", "Build the recipe in order: prepare, feature, construct, align, measure, then review.");
    public string SelectedRoute => T("ThreeD.Workbench.SelectedRoute", "\uC120\uD0DD\uB41C \uAC80\uC0AC \uACBD\uB85C", "Selected inspection route");
    public string OpenSelectedToolLab => T("ThreeD.Command.OpenSelectedToolLab", "\uC120\uD0DD \uB3C4\uAD6C \uB7A9 \uC5F4\uAE30", "Open selected Tool Lab");

    private static string T(string key, string korean, string english)
    {
        var value = OpenVisionLanguageService.T(key);
        return string.Equals(value, key, StringComparison.Ordinal)
            ? OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean
            : value;
    }

    private void Refresh()
    {
        foreach (var propertyName in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
