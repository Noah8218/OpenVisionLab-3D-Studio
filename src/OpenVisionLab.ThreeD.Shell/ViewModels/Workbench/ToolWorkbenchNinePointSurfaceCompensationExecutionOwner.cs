using System.IO;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record NinePointSurfaceControlRow(int Number, NinePointSurfaceControlEvidence Point)
{
    public int Row => Point.Row;
    public int Column => Point.Column;
    public double PixelX => Point.Column + 0.5;
    public double PixelY => Point.Row + 0.5;
    public double X => Point.X;
    public double Y => Point.Y;
    public double Height => Point.Height;
    public double Plane => Point.ReferencePlaneHeight;
    public double Residual => Point.PlaneResidual;
    public double Compensated => Point.CompensatedHeight;
}

/// <summary>Owns only the explicit nine-point operation, cached outputs and presentation snapshots. The SDK owns its mathematics.</summary>
internal sealed class ToolWorkbenchNinePointSurfaceCompensationExecutionOwner : IDisposable
{
    private readonly Func<ToolWorkbenchPipelineStepItem?> getStep;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<bool> canStart;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string, C3DHeightFieldSnapshot?> getPublishedInput;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> display;
    private readonly Action changed;
    private readonly Func<string, string, string> localize;
    private sealed record PublishedOutput(C3DNinePointSurfaceCompensationEvaluation Evaluation, string Path, string Step,
        string Context, C3DHeightImageFrame Source, C3DHeightImageFrame Background, C3DHeightImageFrame Corrected)
    {
        public C3DHeightFieldSnapshot Output => Evaluation.Output!;
    }
    private readonly Dictionary<string, PublishedOutput> published = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? cancellation;
    private C3DNinePointSurfaceCompensationEvaluation? evaluation;
    private ToolWorkbenchPipelineStepItem? completedStep;
    private string context = string.Empty;
    private string? outputPath;
    private bool disposed;
    private bool stale;
    private string previewKind = "Source";
    private string? summaryStepId;
    private string summary = string.Empty;

    public ToolWorkbenchNinePointSurfaceCompensationExecutionOwner(Func<ToolWorkbenchPipelineStepItem?> getStep,
        Func<ToolRecipeDocument> createDocument, Func<bool> canStart, Func<string?> getRecipePath,
        Func<string, C3DHeightFieldSnapshot?> getPublishedInput,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> display, Action changed, Func<string, string, string> localize)
    {
        this.getStep = getStep;
        this.createDocument = createDocument;
        this.canStart = canStart;
        this.getRecipePath = getRecipePath;
        this.getPublishedInput = getPublishedInput;
        this.display = display;
        this.changed = changed;
        this.localize = localize;
    }

    public bool IsRunning { get; private set; }
    public bool HasCurrent => !disposed && !stale && !IsRunning && evaluation?.Output is not null
        && completedStep is not null && ReferenceEquals(completedStep, getStep());
    public string Summary
    {
        get
        {
            if (disposed || !string.Equals(summaryStepId, getStep()?.Id, StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return HasCurrent && evaluation?.Numerical is { } numerical
                ? $"{localize("9점 기준면 편차", "9-point plane deviation")} · P–V {numerical.ReferencePeakToValley:0.##} · RMS {numerical.ReferenceRms:0.##} {evaluation.Output!.Unit}"
                : summary;
        }
        private set => summary = value;
    }
    public C3DNinePointSurfaceCompensationEvaluation? Current => HasCurrent ? evaluation : null;
    public C3DHeightImageFrame? SourceFrame { get; private set; }
    public C3DHeightImageFrame? BackgroundFrame { get; private set; }
    public C3DHeightImageFrame? CorrectedFrame { get; private set; }
    public IReadOnlyList<NinePointSurfaceControlRow> Rows => Current?.Numerical?.ControlPoints
        .Select((point, index) => new NinePointSurfaceControlRow(index + 1, point)).ToArray() ?? [];
    public string PreviewKind
    {
        get => previewKind;
        set { if ((value is "Source" or "Background" or "Corrected") && previewKind != value) { previewKind = value; changed(); } }
    }
    public C3DHeightImageFrame? PreviewFrame => !HasCurrent ? null : previewKind switch
    {
        "Background" => BackgroundFrame,
        "Corrected" => CorrectedFrame,
        _ => SourceFrame
    };

    public bool CanPreview() => !disposed && !IsRunning && canStart() && getStep()?.ToolId == ToolRecipeNinePointSurfaceCompensationExecution.ToolId;

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getStep() is not { } step) return false;
        var document = createDocument();
        var capturedContext = document.CreateStepExecutionContext(step.Id);
        var inputId = step.InputEntityIds.FirstOrDefault() ?? string.Empty;
        var input = getPublishedInput(inputId);
        var tokenSource = new CancellationTokenSource();
        CancelAndDispose(Interlocked.Exchange(ref cancellation, tokenSource));
        var token = tokenSource.Token;
        published.Remove(step.OutputEntityId);
        evaluation = null;
        SourceFrame = BackgroundFrame = CorrectedFrame = null;
        IsRunning = true;
        stale = false;
        summaryStepId = step.Id;
        Summary = localize("실행 중", "Running");
        changed();
        try
        {
            var recipeDirectory = Path.GetDirectoryName(getRecipePath() ?? string.Empty) ?? Environment.CurrentDirectory;
            var result = await Task.Run(() =>
            {
                if (input is null && !string.Equals(inputId, document.Source.Id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Run the selected input producer first.");
                return ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, step.Id, input, recipeDirectory, token);
            }, token);
            token.ThrowIfCancellationRequested();
            if (disposed || !ReferenceEquals(cancellation, tokenSource) || !ReferenceEquals(step, getStep())
                || createDocument().CreateStepExecutionContext(step.Id) != capturedContext) return false;
            if (result.Result.Status != ResultStatus.Pass || result.Output is null || result.Numerical is null)
            {
                Summary = result.Numerical?.FailureReason switch
                {
                    NinePointSurfaceCompensationFailureReason.MissingReferenceSample => MissingReferenceSummary(
                        document.Steps.Single(item => item.Id == step.Id), result.Numerical.Input),
                    NinePointSurfaceCompensationFailureReason.InvalidReferenceGrid => localize(
                        "가운데 행·열을 입력 격자 범위의 25~75% 안으로 설정하세요.",
                        "Set the middle row and column within 25–75% of the input grid span."),
                    _ => result.Result.Message
                };
                step.State = "Error";
                return false;
            }
            var projection = await CreateProjectionAsync(result, token);
            token.ThrowIfCancellationRequested();
            if (disposed || !ReferenceEquals(cancellation, tokenSource) || !ReferenceEquals(step, getStep())
                || createDocument().CreateStepExecutionContext(step.Id) != capturedContext) return false;
            var numerical = result.Numerical;
            SourceFrame = projection.Source;
            BackgroundFrame = projection.Background;
            CorrectedFrame = projection.Corrected;
            evaluation = result;
            completedStep = step;
            context = capturedContext;
            outputPath = projection.Path;
            Summary = string.Empty;
            step.State = "Preview ready";
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!disposed) { Summary = localize("취소됨", "Canceled"); step.State = "Ready"; }
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OverflowException)
        { if (!disposed) { Summary = exception.Message; step.State = "Error"; } return false; }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref cancellation, null, tokenSource), tokenSource))
            {
                tokenSource.Dispose();
                IsRunning = false;
                if (!disposed) changed();
            }
        }
    }

    public void Publish()
    {
        if (!HasCurrent || completedStep is null || evaluation?.Output is not { } output || outputPath is null
            || !ReferenceEquals(completedStep, getStep()) || createDocument().CreateStepExecutionContext(completedStep.Id) != context) return;
        published[output.EntityId] = new(evaluation, outputPath, completedStep.Id, context,
            SourceFrame!, BackgroundFrame!, CorrectedFrame!);
        completedStep.State = "Published";
        display(new ToolWorkbenchFilterDisplayRequestEventArgs(outputPath, output.ContentSha256, false,
            localize("보정 높이 맵", "Compensated height map")));
        changed();
    }

    public async Task ApplyOrderedRunAsync(ToolWorkbenchPipelineStepItem step, C3DNinePointSurfaceCompensationEvaluation result,
        string capturedContext, CancellationToken token, string? materializedPath = null)
    {
        if (disposed || result.Result.Status != ResultStatus.Pass || result.Output is null || result.Numerical is null) return;
        var projection = await CreateProjectionAsync(result, token, materializedPath);
        token.ThrowIfCancellationRequested();
        if (disposed) return;
        if (createDocument().CreateStepExecutionContext(step.Id) != capturedContext)
            throw new InvalidDataException("The compensation input or conditions changed during ordered Run.");
        published[result.Output.EntityId] = new(result, projection.Path, step.Id, capturedContext,
            projection.Source, projection.Background, projection.Corrected);
        Refresh();
        changed();
    }

    private static Task<(string Path, C3DHeightImageFrame Source, C3DHeightImageFrame Background, C3DHeightImageFrame Corrected)>
        CreateProjectionAsync(C3DNinePointSurfaceCompensationEvaluation result, CancellationToken token, string? materializedPath = null) => Task.Run(() =>
    {
        var folder = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        folder = Path.Combine(string.IsNullOrWhiteSpace(folder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenVisionLab", "3DStudio")
            : Path.GetFullPath(folder), "Preview");
        Directory.CreateDirectory(folder);
        var path = materializedPath ?? Path.Combine(folder, $"nine-point-{result.Output!.ContentSha256}.c3d");
        if (materializedPath is null)
        {
            var temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
            try { result.Output!.SaveC3D(temporary); token.ThrowIfCancellationRequested(); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        var inputMap = result.Numerical!.Input;
        var sourceFrame = C3DHeightImageFrame.CreateScalarMap(inputMap, token);
        var background = C3DHeightImageFrame.CreateScalarMap(new HeightMap3D(inputMap.Rows, inputMap.Columns,
            inputMap.OriginX, inputMap.OriginY, inputMap.ColumnPitch, inputMap.RowPitch, result.Numerical!.BackgroundHeights.ToArray(),
            inputMap.PlanarUnit, inputMap.HeightUnit, inputMap.FrameId, inputMap.SourceId + ".background"), token);
        // Display the exact float32 C3D values that downstream tools consume.
        var corrected = C3DHeightImageFrame.CreateScalarMap(new HeightMap3D(inputMap.Rows, inputMap.Columns,
            inputMap.OriginX, inputMap.OriginY, inputMap.ColumnPitch, inputMap.RowPitch, result.Output!.Values.ToArray(),
            inputMap.PlanarUnit, inputMap.HeightUnit, inputMap.FrameId, result.Output!.EntityId), token);
        return (Path: path, Source: sourceFrame, Background: background, Corrected: corrected);
    }, token);

    public (C3DHeightFieldSnapshot Output, string Path)? GetPublishedOutput(string id)
    {
        if (disposed || !published.TryGetValue(id, out var entry)) return null;
        var document = createDocument();
        return document.Steps.Any(step => step.Id == entry.Step) && document.CreateStepExecutionContext(entry.Step) == entry.Context
            ? (entry.Output, entry.Path) : null;
    }

    public C3DHeightFieldSnapshot? GetOutput(string id) => GetPublishedOutput(id)?.Output
        ?? (HasCurrent && evaluation?.Output?.EntityId == id ? evaluation.Output : null);
    public string? GetPath(string id) => GetPublishedOutput(id)?.Path
        ?? (HasCurrent && evaluation?.Output?.EntityId == id ? outputPath : null);
    public bool IsPublished(string id) => GetPublishedOutput(id) is not null;
    private string MissingReferenceSummary(ToolRecipeStep step, HeightMap3D input)
    {
        // The SDK already validated the 3×3 grid. Only format its missing input cells for recovery.
        var rows = new[] { 0, int.Parse(step.Parameters!.Single(item => item.Name == "MiddleRow").Value, CultureInfo.InvariantCulture), input.Rows - 1 };
        var columns = new[] { 0, int.Parse(step.Parameters!.Single(item => item.Name == "MiddleColumn").Value, CultureInfo.InvariantCulture), input.Columns - 1 };
        var missing = from rowIndex in Enumerable.Range(0, 3)
                      from columnIndex in Enumerable.Range(0, 3)
                      let row = rows[rowIndex]
                      let column = columns[columnIndex]
                      where !double.IsFinite(input.GetHeight(row, column))
                      select localize($"{rowIndex * 3 + columnIndex + 1}번 (행 {row}, 열 {column})",
                          $"{rowIndex * 3 + columnIndex + 1} (row {row}, column {column})");
        return localize("높이값이 없는 기준점: ", "Reference points without height: ") + string.Join(" · ", missing);
    }
    public void WithdrawOutputs(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            published.Remove(id);
            if (completedStep?.OutputEntityId == id)
            {
                evaluation = null;
                completedStep = null;
                SourceFrame = BackgroundFrame = CorrectedFrame = null;
            }
        }
    }
    public void Cancel() { try { cancellation?.Cancel(); } catch (ObjectDisposedException) { } }
    public void Refresh()
    {
        if (disposed || IsRunning) return;
        var document = createDocument();
        if (getStep() is { } selected && published.TryGetValue(selected.OutputEntityId, out var entry)
            && document.CreateStepExecutionContext(selected.Id) == entry.Context)
        {
            evaluation = entry.Evaluation;
            completedStep = selected;
            context = entry.Context;
            outputPath = entry.Path;
            SourceFrame = entry.Source;
            BackgroundFrame = entry.Background;
            CorrectedFrame = entry.Corrected;
            summaryStepId = selected.Id;
            stale = false;
            Summary = string.Empty;
        }
        if (completedStep is null) return;
        if (!document.Steps.Any(step => step.Id == completedStep.Id) || document.CreateStepExecutionContext(completedStep.Id) != context)
        {
            stale = true;
            published.Remove(completedStep.OutputEntityId);
            SourceFrame = BackgroundFrame = CorrectedFrame = null;
            if (!IsRunning && evaluation is not null && summaryStepId == completedStep.Id)
                Summary = localize("입력 또는 조건이 바뀌었습니다. 다시 실행하세요.", "Input or conditions changed. Run again.");
        }
    }
    public void Clear()
    {
        Cancel();
        published.Clear();
        evaluation = null;
        completedStep = null;
        summaryStepId = null;
        SourceFrame = BackgroundFrame = CorrectedFrame = null;
        Summary = string.Empty;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CancelAndDispose(Interlocked.Exchange(ref cancellation, null));
        IsRunning = false;
        published.Clear();
        evaluation = null;
        SourceFrame = BackgroundFrame = CorrectedFrame = null;
    }
}
