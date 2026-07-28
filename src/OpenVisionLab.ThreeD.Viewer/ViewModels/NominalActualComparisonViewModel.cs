using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public enum NominalActualComparisonState
{
    NoInputs,
    InputsReady,
    PreviewStale,
    PreviewRunning,
    PreviewReady,
    Published,
    Failed
}

public sealed class NominalActualPreviewRequestedEventArgs(
    long requestId,
    string fingerprint,
    string displayDensity,
    int maximumDisplaySamples,
    CancellationToken cancellationToken) : EventArgs
{
    public long RequestId { get; } = requestId;
    public string Fingerprint { get; } = fingerprint;
    public string DisplayDensity { get; } = displayDensity;
    public int MaximumDisplaySamples { get; } = maximumDisplaySamples;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}

public sealed class NominalActualPublishRequestedEventArgs(string fingerprint) : EventArgs
{
    public string Fingerprint { get; } = fingerprint;
}

public sealed class NominalActualComparisonViewModel : INotifyPropertyChanged
{
    private const double DefaultLowerTolerance = -0.300;
    private const double DefaultUpperTolerance = 0.300;

    private readonly RelayCommand previewCommand;
    private readonly RelayCommand cancelCommand;
    private readonly RelayCommand publishCommand;
    private CancellationTokenSource? previewCancellation;
    private long nextRequestId;
    private long activeRequestId;
    private string activePreviewFingerprint = "(none)";
    private string completedPreviewFingerprint = "(none)";
    private string publishedPreviewFingerprint = "(none)";
    private string currentInputFingerprint = "(none)";
    private string actualSourceSummary = "Actual: not loaded";
    private string nominalSourceSummary = "Nominal: not loaded";
    private string querySourceSummary = "Validation query: not loaded";
    private string frameSummary = "Frame: not set | Units: not set";
    private string alignmentSummary = "Alignment: not set";
    private string inputValidationSummary = "Actual and nominal inputs are required.";
    private string unit = "(not set)";
    private bool inputsReady;
    private bool actualVisible;
    private bool nominalVisible;
    private double lowerTolerance = DefaultLowerTolerance;
    private double upperTolerance = DefaultUpperTolerance;
    private double progressPercent;
    private string progressSummary = "Progress: not started";
    private string resultSummary = "No comparison preview.";
    private string hudSummary = "Surface Deviation | Actual to Nominal";
    private string hudDetails = "Inputs and frame: not ready";
    private string legendSummary = "Signed deviation result: not available";
    private string evidenceSummary = "No comparison evidence.";
    private string distributionSummary = "Deviation distribution: not available";
    private NominalActualDeviationSample? selectedDeviation;
    private string selectedDeviationSummary = "Selected point: none";
    private string selectedDeviationDetails = "";
    private string selectedDeviationToleranceStatus = "Not selected";
    private string nextPreviewDisplayDensity = "Balanced";
    private int nextPreviewDisplaySampleBudget = 60000;
    private string activePreviewDisplayDensity = "(none)";
    private int activePreviewDisplaySampleBudget;
    private string currentDisplayDensity = "(none)";
    private int currentDisplaySampleBudget;
    private bool hasPreviewResult;
    private NominalActualComparisonResult? previewResult;
    private NominalActualComparisonState state = NominalActualComparisonState.NoInputs;

    public NominalActualComparisonViewModel()
    {
        previewCommand = new RelayCommand(_ => RequestPreview(), _ => CanPreview);
        cancelCommand = new RelayCommand(_ => CancelPreview(), _ => CanCancel);
        publishCommand = new RelayCommand(_ => RequestPublish(), _ => CanPublish);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<NominalActualPreviewRequestedEventArgs>? PreviewRequested;
    public event EventHandler<NominalActualPublishRequestedEventArgs>? PublishRequested;

    public ICommand PreviewCommand => previewCommand;
    public ICommand CancelCommand => cancelCommand;
    public ICommand PublishCommand => publishCommand;

    public NominalActualComparisonState State
    {
        get => state;
        private set
        {
            if (SetField(ref state, value))
            {
                RefreshDerivedProperties();
            }
        }
    }

    public string StateSummary => !ToleranceIsValid && InputsReady
        ? "Status: Inputs ready | invalid tolerance"
        : State switch
        {
            NominalActualComparisonState.NoInputs => "Status: No inputs",
            NominalActualComparisonState.InputsReady => "Status: Inputs ready",
            NominalActualComparisonState.PreviewStale => "Status: Preview stale",
            NominalActualComparisonState.PreviewRunning => "Status: Preview running",
            NominalActualComparisonState.PreviewReady => "Status: Preview ready",
            NominalActualComparisonState.Published => "Status: Published",
            NominalActualComparisonState.Failed => "Status: Failed",
            _ => "Status: Unknown"
        };

    public string ActualSourceSummary
    {
        get => actualSourceSummary;
        private set => SetField(ref actualSourceSummary, value);
    }

    public string NominalSourceSummary
    {
        get => nominalSourceSummary;
        private set => SetField(ref nominalSourceSummary, value);
    }

    public string QuerySourceSummary
    {
        get => querySourceSummary;
        private set => SetField(ref querySourceSummary, value);
    }

    public string FrameSummary
    {
        get => frameSummary;
        private set => SetField(ref frameSummary, value);
    }

    public string DirectionSummary { get; } = "Direction: Actual to Nominal";

    public string AlignmentSummary
    {
        get => alignmentSummary;
        private set => SetField(ref alignmentSummary, value);
    }

    public string ValidationSummary => ToleranceIsValid
        ? inputValidationSummary
        : "Lower tolerance must be below zero and upper tolerance must be above zero.";

    public string Unit
    {
        get => unit;
        private set
        {
            if (SetField(ref unit, value))
            {
                OnPropertyChanged(nameof(LowerLegendLabel));
                OnPropertyChanged(nameof(ZeroLegendLabel));
                OnPropertyChanged(nameof(UpperLegendLabel));
            }
        }
    }

    public bool InputsReady
    {
        get => inputsReady;
        private set
        {
            if (SetField(ref inputsReady, value))
            {
                RefreshDerivedProperties();
            }
        }
    }

    public bool ActualVisible
    {
        get => actualVisible;
        set => SetField(ref actualVisible, InputsReady && value);
    }

    public bool NominalVisible
    {
        get => nominalVisible;
        set => SetField(ref nominalVisible, InputsReady && value);
    }

    public double LowerTolerance
    {
        get => lowerTolerance;
        set
        {
            if (SetField(ref lowerTolerance, value))
            {
                OnToleranceChanged();
            }
        }
    }

    public double UpperTolerance
    {
        get => upperTolerance;
        set
        {
            if (SetField(ref upperTolerance, value))
            {
                OnToleranceChanged();
            }
        }
    }

    public bool ToleranceIsValid =>
        double.IsFinite(LowerTolerance)
        && double.IsFinite(UpperTolerance)
        && LowerTolerance < 0.0
        && UpperTolerance > 0.0
        && LowerTolerance < UpperTolerance;

    public bool CanEdit => InputsReady && State != NominalActualComparisonState.PreviewRunning;
    public bool CanPreview => InputsReady && ToleranceIsValid && State != NominalActualComparisonState.PreviewRunning;
    public bool CanCancel => State == NominalActualComparisonState.PreviewRunning && activeRequestId != 0;
    public bool CanPublish =>
        State == NominalActualComparisonState.PreviewReady
        && hasPreviewResult
        && string.Equals(BuildPreviewFingerprint(), completedPreviewFingerprint, StringComparison.Ordinal);

    public double ProgressPercent
    {
        get => progressPercent;
        private set => SetField(ref progressPercent, value);
    }

    public string ProgressSummary
    {
        get => progressSummary;
        private set => SetField(ref progressSummary, value);
    }

    public string ResultSummary
    {
        get => resultSummary;
        private set => SetField(ref resultSummary, value);
    }

    public bool HudVisible => InputsReady || hasPreviewResult || State == NominalActualComparisonState.Failed;

    public string HudSummary
    {
        get => hudSummary;
        private set => SetField(ref hudSummary, value);
    }

    public string HudDetails
    {
        get => hudDetails;
        private set => SetField(ref hudDetails, value);
    }

    public bool LegendVisible => hasPreviewResult;
    public string LowerLegendLabel =>
        $"{FormatNumber(previewResult?.Input.LowerTolerance ?? LowerTolerance)} {Unit}";
    public string ZeroLegendLabel => $"0 {Unit}";
    public string UpperLegendLabel =>
        $"{FormatNumber(previewResult?.Input.UpperTolerance ?? UpperTolerance)} {Unit}";

    public string LegendSummary
    {
        get => legendSummary;
        private set => SetField(ref legendSummary, value);
    }

    public string EvidenceSummary
    {
        get => evidenceSummary;
        private set => SetField(ref evidenceSummary, value);
    }

    public bool DistributionVisible => hasPreviewResult;

    public string DistributionSummary
    {
        get => distributionSummary;
        private set => SetField(ref distributionSummary, value);
    }

    public string CurrentDisplayDensity => currentDisplayDensity;
    public int CurrentDisplaySampleBudget => currentDisplaySampleBudget;
    public string NextPreviewDisplayDensity => nextPreviewDisplayDensity;
    public int NextPreviewDisplaySampleBudget => nextPreviewDisplaySampleBudget;

    public bool DisplaySamplingChangePending =>
        hasPreviewResult
        && (!string.Equals(currentDisplayDensity, nextPreviewDisplayDensity, StringComparison.Ordinal)
            || currentDisplaySampleBudget != nextPreviewDisplaySampleBudget);

    public string CurrentDisplaySamplingSummary => previewResult is null
        ? "Current display: no comparison result"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"Current display: {currentDisplayDensity} | {previewResult.DisplaySamples.Count:N0}/{previewResult.ComparedPointCount:N0} points | stride {previewResult.DisplaySampleStride} | budget {currentDisplaySampleBudget:N0}");

    public string NextPreviewSamplingSummary => DisplaySamplingChangePending
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"Next Preview: {nextPreviewDisplayDensity} | up to {nextPreviewDisplaySampleBudget:N0} points | run Preview to apply")
        : previewResult is null
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Next Preview: {nextPreviewDisplayDensity} | up to {nextPreviewDisplaySampleBudget:N0} points")
            : $"Next Preview: {nextPreviewDisplayDensity} | matches current display";

    public bool HasSelectedDeviation => selectedDeviation.HasValue;
    public NominalActualDeviationSample? SelectedDeviation => selectedDeviation;

    public string SelectedDeviationSummary
    {
        get => selectedDeviationSummary;
        private set => SetField(ref selectedDeviationSummary, value);
    }

    public string SelectedDeviationDetails
    {
        get => selectedDeviationDetails;
        private set => SetField(ref selectedDeviationDetails, value);
    }

    public string SelectedDeviationToleranceStatus
    {
        get => selectedDeviationToleranceStatus;
        private set => SetField(ref selectedDeviationToleranceStatus, value);
    }

    public string CurrentInputFingerprint => currentInputFingerprint;
    public string CompletedPreviewFingerprint => completedPreviewFingerprint;
    public string PublishedPreviewFingerprint => publishedPreviewFingerprint;
    public long ActiveRequestId => activeRequestId;
    public NominalActualComparisonResult? PreviewResult => previewResult;

    public void ConfigureNextDisplaySampling(string density, int maximumDisplaySamples)
    {
        var normalizedDensity = Normalize(density, "Balanced");
        if (maximumDisplaySamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDisplaySamples),
                maximumDisplaySamples,
                "The nominal/actual display sample budget must be positive.");
        }

        if (string.Equals(nextPreviewDisplayDensity, normalizedDensity, StringComparison.Ordinal)
            && nextPreviewDisplaySampleBudget == maximumDisplaySamples)
        {
            return;
        }

        nextPreviewDisplayDensity = normalizedDensity;
        nextPreviewDisplaySampleBudget = maximumDisplaySamples;
        RefreshDisplaySamplingProperties();
    }

    public void ApplyInputValidation(
        string actualSummary,
        string nominalSummary,
        string querySummary,
        string frame,
        string alignment,
        string modelUnit,
        string inputFingerprint,
        string? validationIssue = null)
    {
        var normalizedFingerprint = Normalize(inputFingerprint, "(none)");
        var normalizedActual = Normalize(actualSummary, "Actual: not loaded");
        var normalizedNominal = Normalize(nominalSummary, "Nominal: not loaded");
        var normalizedQuery = Normalize(querySummary, "Validation query: not loaded");
        var normalizedFrame = Normalize(frame, "Frame: not set | Units: not set");
        var normalizedAlignment = Normalize(alignment, "Alignment: not set");
        var normalizedUnit = Normalize(modelUnit, "(not set)");
        var normalizedIssue = validationIssue?.Trim();
        var ready = string.IsNullOrWhiteSpace(normalizedIssue)
            && normalizedFingerprint != "(none)"
            && normalizedActual != "Actual: not loaded"
            && normalizedNominal != "Nominal: not loaded"
            && normalizedQuery != "Validation query: not loaded"
            && normalizedUnit != "(not set)";

        if (State == NominalActualComparisonState.PreviewRunning)
        {
            CancelActivePreview();
        }

        ClearSelectedDeviation();

        ActualSourceSummary = normalizedActual;
        NominalSourceSummary = normalizedNominal;
        QuerySourceSummary = normalizedQuery;
        FrameSummary = normalizedFrame;
        AlignmentSummary = normalizedAlignment;
        Unit = normalizedUnit;
        currentInputFingerprint = ready ? normalizedFingerprint : "(none)";
        inputValidationSummary = ready ? "Inputs validated." : Normalize(normalizedIssue, "Actual and nominal inputs are required.");
        InputsReady = ready;

        if (!ready)
        {
            actualVisible = false;
            nominalVisible = false;
            ClearPreviewResult();
            ResultSummary = inputValidationSummary;
            HudDetails = inputValidationSummary;
            State = NominalActualComparisonState.NoInputs;
            OnPropertyChanged(nameof(ActualVisible));
            OnPropertyChanged(nameof(NominalVisible));
            RefreshDerivedProperties();
            return;
        }

        if (!ActualVisible && !NominalVisible)
        {
            ActualVisible = true;
        }

        HudSummary = "Surface Deviation | Actual to Nominal";
        HudDetails = $"{FrameSummary} | {AlignmentSummary}";
        RefreshFreshnessState();
        RefreshDerivedProperties();
    }

    public bool ReportPreviewProgress(
        long requestId,
        long processed,
        long total,
        TimeSpan elapsed,
        string stage = "Comparing actual to nominal")
    {
        if (!IsActiveRequest(requestId))
        {
            return false;
        }

        var safeTotal = Math.Max(1, total);
        var safeProcessed = Math.Clamp(processed, 0, safeTotal);
        ProgressPercent = 100.0 * safeProcessed / safeTotal;
        ProgressSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{Normalize(stage, "Comparing actual to nominal")}: {safeProcessed:N0} / {safeTotal:N0} ({ProgressPercent:F1}%) | {Math.Max(0.0, elapsed.TotalSeconds):F1} s");
        HudDetails = ProgressSummary;
        return true;
    }

    public bool CompletePreview(long requestId, NominalActualComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsActiveRequest(requestId))
        {
            return false;
        }

        if (!result.Input.ExecutionFingerprint.Equals(activePreviewFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Comparison result fingerprint does not match the active Preview request.");
        }

        if (activePreviewDisplaySampleBudget <= 0
            || result.DisplaySamples.Count > activePreviewDisplaySampleBudget)
        {
            throw new InvalidDataException("Comparison display samples do not match the active Preview density budget.");
        }

        completedPreviewFingerprint = activePreviewFingerprint;
        currentDisplayDensity = activePreviewDisplayDensity;
        currentDisplaySampleBudget = activePreviewDisplaySampleBudget;
        CompleteActiveRequest();
        ClearSelectedDeviation();
        hasPreviewResult = true;
        previewResult = result;
        ResultSummary = result.Message;
        HudSummary = $"Surface Deviation | {result.Status}";
        HudDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Full query: {result.ComparedPointCount:N0} | signed {result.Signed.Minimum:G6}..{result.Signed.Maximum:G6} {result.Input.Unit} | {result.TotalElapsed.TotalSeconds:F1} s");
        LegendSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Signed range {result.Signed.Minimum:G6}..{result.Signed.Maximum:G6} {result.Input.Unit} | full-query metrics");
        EvidenceSummary =
            $"Preview only | actual={FormatIdentity(result.Input.ActualSource)} | nominal={FormatIdentity(result.Input.NominalSource)} | query={FormatIdentity(result.Input.QuerySource)}";
        DistributionSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Below: {result.BelowLowerToleranceCount:N0} | Within: {result.WithinToleranceCount:N0} | Above: {result.AboveUpperToleranceCount:N0} | display {result.DisplaySamples.Count:N0}/{result.ComparedPointCount:N0} (stride {result.DisplaySampleStride})");
        ProgressPercent = 100.0;
        ProgressSummary = "Progress: complete";
        State = NominalActualComparisonState.PreviewReady;
        OnPropertyChanged(nameof(PreviewResult));
        OnPropertyChanged(nameof(LowerLegendLabel));
        OnPropertyChanged(nameof(UpperLegendLabel));
        RefreshDisplaySamplingProperties();
        RefreshDerivedProperties();
        return true;
    }

    public bool SelectDeviation(NominalActualDeviationSample sample)
    {
        if (previewResult is null || !previewResult.DisplaySamples.Contains(sample))
        {
            return false;
        }

        selectedDeviation = sample;
        SelectedDeviationToleranceStatus = sample.SignedDeviation < previewResult.Input.LowerTolerance
            ? "Below lower tolerance"
            : sample.SignedDeviation > previewResult.Input.UpperTolerance
                ? "Above upper tolerance"
                : "Within tolerance";
        SelectedDeviationSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Query point #{sample.QueryPointIndex:N0} | {SelectedDeviationToleranceStatus}");
        SelectedDeviationDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"XYZ {FormatVector(sample.Position)} | signed {sample.SignedDeviation:G7} {previewResult.Input.Unit} | unsigned {sample.UnsignedDeviation:G7} {previewResult.Input.Unit} | nearest nominal triangle #{sample.NominalTriangleIndex:N0} | sign {(sample.RobustSignRecovered ? "robust" : "direct")} | actual={previewResult.Input.ActualSource.Id} | query={previewResult.Input.QuerySource.Id}");
        OnPropertyChanged(nameof(SelectedDeviation));
        OnPropertyChanged(nameof(HasSelectedDeviation));
        return true;
    }

    public void ClearSelectedDeviation()
    {
        if (selectedDeviation is null
            && SelectedDeviationSummary == "Selected point: none"
            && SelectedDeviationDetails.Length == 0
            && SelectedDeviationToleranceStatus == "Not selected")
        {
            return;
        }

        selectedDeviation = null;
        SelectedDeviationSummary = "Selected point: none";
        SelectedDeviationDetails = "";
        SelectedDeviationToleranceStatus = "Not selected";
        OnPropertyChanged(nameof(SelectedDeviation));
        OnPropertyChanged(nameof(HasSelectedDeviation));
    }

    public bool FailPreview(long requestId, string message)
    {
        if (!IsActiveRequest(requestId))
        {
            return false;
        }

        CompleteActiveRequest();
        var failure = Normalize(message, "Comparison preview failed.");
        if (!hasPreviewResult)
        {
            ClearPreviewPresentation();
        }

        ResultSummary = $"Preview failed: {failure}";
        ProgressSummary = "Progress: failed; no partial result published";
        HudDetails = ResultSummary;
        State = NominalActualComparisonState.Failed;
        RefreshDerivedProperties();
        return true;
    }

    public bool ConfirmPublished(string evidence)
    {
        if (!CanPublish)
        {
            return false;
        }

        publishedPreviewFingerprint = completedPreviewFingerprint;
        EvidenceSummary = Normalize(evidence, "Published result evidence recorded.");
        State = NominalActualComparisonState.Published;
        RefreshDerivedProperties();
        return true;
    }

    private void RequestPreview()
    {
        if (!CanPreview)
        {
            return;
        }

        CancelActivePreview();
        ClearSelectedDeviation();
        previewCancellation = new CancellationTokenSource();
        activeRequestId = ++nextRequestId;
        activePreviewFingerprint = BuildPreviewFingerprint();
        activePreviewDisplayDensity = nextPreviewDisplayDensity;
        activePreviewDisplaySampleBudget = nextPreviewDisplaySampleBudget;
        ProgressPercent = 0.0;
        ProgressSummary = "Progress: starting";
        ResultSummary = "Comparison preview running.";
        HudDetails = $"{FrameSummary} | {AlignmentSummary} | {ProgressSummary}";
        State = NominalActualComparisonState.PreviewRunning;

        var previewRequested = PreviewRequested;
        if (previewRequested is null)
        {
            FailPreview(activeRequestId, "Comparison executor is not connected.");
            return;
        }

        try
        {
            previewRequested.Invoke(
                this,
                new NominalActualPreviewRequestedEventArgs(
                    activeRequestId,
                    activePreviewFingerprint,
                    activePreviewDisplayDensity,
                    activePreviewDisplaySampleBudget,
                    previewCancellation.Token));
        }
        catch (Exception ex)
        {
            FailPreview(activeRequestId, $"Preview request failed: {ex.Message}");
        }
    }

    private void CancelPreview()
    {
        if (!CanCancel)
        {
            return;
        }

        CancelActivePreview();
        ProgressPercent = 0.0;
        ProgressSummary = "Progress: cancelled; no partial result published";
        if (!hasPreviewResult)
        {
            ResultSummary = "Comparison preview cancelled. No result published.";
        }

        RefreshFreshnessState();
        RefreshDerivedProperties();
    }

    private void RequestPublish()
    {
        if (!CanPublish)
        {
            return;
        }

        var publishRequested = PublishRequested;
        if (publishRequested is null)
        {
            EvidenceSummary = "Publish failed: result persistence is not connected.";
            return;
        }

        try
        {
            publishRequested.Invoke(this, new NominalActualPublishRequestedEventArgs(completedPreviewFingerprint));
        }
        catch (Exception ex)
        {
            EvidenceSummary = $"Publish failed: {ex.Message}";
        }
    }

    private void OnToleranceChanged()
    {
        ClearSelectedDeviation();
        OnPropertyChanged(nameof(ToleranceIsValid));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(LowerLegendLabel));
        OnPropertyChanged(nameof(UpperLegendLabel));

        if (State == NominalActualComparisonState.PreviewRunning)
        {
            CancelActivePreview();
            ProgressSummary = "Progress: cancelled because tolerances changed";
        }

        if (!ToleranceIsValid && !hasPreviewResult)
        {
            ResultSummary = ValidationSummary;
        }
        else if (ToleranceIsValid && !hasPreviewResult)
        {
            ResultSummary = "No comparison preview.";
        }

        RefreshFreshnessState();
        RefreshDerivedProperties();
    }

    private void RefreshFreshnessState()
    {
        if (!InputsReady)
        {
            State = NominalActualComparisonState.NoInputs;
            return;
        }

        if (!hasPreviewResult)
        {
            State = NominalActualComparisonState.InputsReady;
            return;
        }

        var currentFingerprint = BuildPreviewFingerprint();
        if (!string.Equals(currentFingerprint, completedPreviewFingerprint, StringComparison.Ordinal))
        {
            State = NominalActualComparisonState.PreviewStale;
            return;
        }

        State = string.Equals(currentFingerprint, publishedPreviewFingerprint, StringComparison.Ordinal)
            ? NominalActualComparisonState.Published
            : NominalActualComparisonState.PreviewReady;
    }

    private bool IsActiveRequest(long requestId) =>
        State == NominalActualComparisonState.PreviewRunning
        && requestId != 0
        && requestId == activeRequestId
        && previewCancellation is { IsCancellationRequested: false };

    private void CancelActivePreview()
    {
        previewCancellation?.Cancel();
        previewCancellation?.Dispose();
        previewCancellation = null;
        activeRequestId = 0;
        activePreviewFingerprint = "(none)";
        activePreviewDisplayDensity = "(none)";
        activePreviewDisplaySampleBudget = 0;
    }

    private void CompleteActiveRequest()
    {
        previewCancellation?.Dispose();
        previewCancellation = null;
        activeRequestId = 0;
        activePreviewFingerprint = "(none)";
        activePreviewDisplayDensity = "(none)";
        activePreviewDisplaySampleBudget = 0;
    }

    private void ClearPreviewResult()
    {
        CancelActivePreview();
        ClearSelectedDeviation();
        hasPreviewResult = false;
        previewResult = null;
        currentDisplayDensity = "(none)";
        currentDisplaySampleBudget = 0;
        completedPreviewFingerprint = "(none)";
        publishedPreviewFingerprint = "(none)";
        ProgressPercent = 0.0;
        ProgressSummary = "Progress: not started";
        ClearPreviewPresentation();
        OnPropertyChanged(nameof(PreviewResult));
        OnPropertyChanged(nameof(LowerLegendLabel));
        OnPropertyChanged(nameof(UpperLegendLabel));
        RefreshDisplaySamplingProperties();
        RefreshDerivedProperties();
    }

    private void ClearPreviewPresentation()
    {
        ResultSummary = "No comparison preview.";
        LegendSummary = "Signed deviation result: not available";
        EvidenceSummary = "No comparison evidence.";
        DistributionSummary = "Deviation distribution: not available";
    }

    private string BuildPreviewFingerprint()
    {
        if (!InputsReady)
        {
            return "(none)";
        }

        return NominalActualComparisonInput.BuildExecutionFingerprint(
            currentInputFingerprint,
            LowerTolerance,
            UpperTolerance);
    }

    private void RefreshDerivedProperties()
    {
        OnPropertyChanged(nameof(StateSummary));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanPreview));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanPublish));
        OnPropertyChanged(nameof(HudVisible));
        OnPropertyChanged(nameof(LegendVisible));
        OnPropertyChanged(nameof(DistributionVisible));
        OnPropertyChanged(nameof(CurrentInputFingerprint));
        OnPropertyChanged(nameof(CompletedPreviewFingerprint));
        OnPropertyChanged(nameof(PublishedPreviewFingerprint));
        OnPropertyChanged(nameof(ActiveRequestId));
        previewCommand.RaiseCanExecuteChanged();
        cancelCommand.RaiseCanExecuteChanged();
        publishCommand.RaiseCanExecuteChanged();
    }

    private void RefreshDisplaySamplingProperties()
    {
        OnPropertyChanged(nameof(CurrentDisplayDensity));
        OnPropertyChanged(nameof(CurrentDisplaySampleBudget));
        OnPropertyChanged(nameof(NextPreviewDisplayDensity));
        OnPropertyChanged(nameof(NextPreviewDisplaySampleBudget));
        OnPropertyChanged(nameof(DisplaySamplingChangePending));
        OnPropertyChanged(nameof(CurrentDisplaySamplingSummary));
        OnPropertyChanged(nameof(NextPreviewSamplingSummary));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string FormatNumber(double value) =>
        double.IsFinite(value)
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";

    private static string FormatIdentity(NominalActualFileIdentity identity) =>
        $"{identity.Id}@{identity.Sha256[..8]}";

    private static string FormatVector(System.Numerics.Vector3 value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"({value.X:F6}, {value.Y:F6}, {value.Z:F6})");
}
