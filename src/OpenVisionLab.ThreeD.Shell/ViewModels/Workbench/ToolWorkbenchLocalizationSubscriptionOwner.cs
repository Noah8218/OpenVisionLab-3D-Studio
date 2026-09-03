using System.ComponentModel;
using System.Threading;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench facade's subscriptions to process-wide language and
/// shared localization state. The callbacks remain owned by the feature
/// partial that exposes the corresponding presentation, while this type owns
/// the subscription lifetime and deterministic unsubscription.
/// </summary>
internal sealed class ToolWorkbenchLocalizationSubscriptionOwner : IDisposable
{
    private readonly ThreeDLocalization localization;
    private readonly PropertyChangedEventHandler completenessChangedHandler;
    private readonly PropertyChangedEventHandler planeFlatnessChangedHandler;
    private readonly PropertyChangedEventHandler validationSetChangedHandler;
    private readonly PropertyChangedEventHandler displayedOutputsChangedHandler;
    private readonly PropertyChangedEventHandler compatibleToolCatalogChangedHandler;
    private readonly PropertyChangedEventHandler teachingChangedHandler;
    private readonly PropertyChangedEventHandler outputCompareChangedHandler;
    private readonly PropertyChangedEventHandler thicknessRepeatGridChangedHandler;
    private readonly PropertyChangedEventHandler viewerWorkspaceChangedHandler;
    private readonly EventHandler firstRecipeLanguageChangedHandler;
    private int disposalState;

    public ToolWorkbenchLocalizationSubscriptionOwner(
        ThreeDLocalization localization,
        PropertyChangedEventHandler completenessChangedHandler,
        PropertyChangedEventHandler planeFlatnessChangedHandler,
        PropertyChangedEventHandler validationSetChangedHandler,
        PropertyChangedEventHandler displayedOutputsChangedHandler,
        PropertyChangedEventHandler compatibleToolCatalogChangedHandler,
        PropertyChangedEventHandler teachingChangedHandler,
        PropertyChangedEventHandler outputCompareChangedHandler,
        PropertyChangedEventHandler thicknessRepeatGridChangedHandler,
        PropertyChangedEventHandler viewerWorkspaceChangedHandler,
        EventHandler firstRecipeLanguageChangedHandler)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.completenessChangedHandler = completenessChangedHandler
            ?? throw new ArgumentNullException(nameof(completenessChangedHandler));
        this.planeFlatnessChangedHandler = planeFlatnessChangedHandler
            ?? throw new ArgumentNullException(nameof(planeFlatnessChangedHandler));
        this.validationSetChangedHandler = validationSetChangedHandler
            ?? throw new ArgumentNullException(nameof(validationSetChangedHandler));
        this.displayedOutputsChangedHandler = displayedOutputsChangedHandler
            ?? throw new ArgumentNullException(nameof(displayedOutputsChangedHandler));
        this.compatibleToolCatalogChangedHandler = compatibleToolCatalogChangedHandler
            ?? throw new ArgumentNullException(nameof(compatibleToolCatalogChangedHandler));
        this.teachingChangedHandler = teachingChangedHandler
            ?? throw new ArgumentNullException(nameof(teachingChangedHandler));
        this.outputCompareChangedHandler = outputCompareChangedHandler
            ?? throw new ArgumentNullException(nameof(outputCompareChangedHandler));
        this.thicknessRepeatGridChangedHandler = thicknessRepeatGridChangedHandler
            ?? throw new ArgumentNullException(nameof(thicknessRepeatGridChangedHandler));
        this.viewerWorkspaceChangedHandler = viewerWorkspaceChangedHandler
            ?? throw new ArgumentNullException(nameof(viewerWorkspaceChangedHandler));
        this.firstRecipeLanguageChangedHandler = firstRecipeLanguageChangedHandler
            ?? throw new ArgumentNullException(nameof(firstRecipeLanguageChangedHandler));

        localization.PropertyChanged += this.completenessChangedHandler;
        localization.PropertyChanged += this.planeFlatnessChangedHandler;
        localization.PropertyChanged += this.validationSetChangedHandler;
        localization.PropertyChanged += this.displayedOutputsChangedHandler;
        localization.PropertyChanged += this.compatibleToolCatalogChangedHandler;
        localization.PropertyChanged += this.teachingChangedHandler;
        localization.PropertyChanged += this.outputCompareChangedHandler;
        localization.PropertyChanged += this.thicknessRepeatGridChangedHandler;
        localization.PropertyChanged += this.viewerWorkspaceChangedHandler;
        OpenVisionLanguageService.LanguageChanged += this.firstRecipeLanguageChangedHandler;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localization.PropertyChanged -= completenessChangedHandler;
        localization.PropertyChanged -= planeFlatnessChangedHandler;
        localization.PropertyChanged -= validationSetChangedHandler;
        localization.PropertyChanged -= displayedOutputsChangedHandler;
        localization.PropertyChanged -= compatibleToolCatalogChangedHandler;
        localization.PropertyChanged -= teachingChangedHandler;
        localization.PropertyChanged -= outputCompareChangedHandler;
        localization.PropertyChanged -= thicknessRepeatGridChangedHandler;
        localization.PropertyChanged -= viewerWorkspaceChangedHandler;
        OpenVisionLanguageService.LanguageChanged -= firstRecipeLanguageChangedHandler;
    }
}
