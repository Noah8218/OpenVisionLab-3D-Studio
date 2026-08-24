using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeRoiCropExecution
{
    public static C3DRoiCropEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, recipeDirectory, out var input, out var message))
        {
            return new C3DRoiCropEvaluation(
                new ToolResult(C3DRoiCropRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []),
                null,
                null);
        }
        return C3DRoiCropRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory,
        out C3DRoiCropInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(string.Join(" ", validation.Errors));
            }

            var step = document.Steps.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "roi-crop", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the ROI / Crop v1 adapter.");
            }
            if (step.InputEntityIds.Count != 2
                || !string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("ROI / Crop requires the raw C3D source followed by one GridRectangle.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId)
                || step.InputEntityIds.Any(id => string.Equals(id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("ROI / Crop output ID must be explicit and differ from its inputs.");
            }
            var parameters = step.Parameters ?? [];
            if (parameters.Count != 2
                || parameters.Count(parameter => parameter.Name == "ROI" && parameter.Value == "Select in Viewer") != 1
                || parameters.Count(parameter => parameter.Name == "Output frame" && parameter.Value == "Keep source frame") != 1)
            {
                throw new InvalidDataException("ROI / Crop v1 requires its fixed ROI and source-frame policies without unknown parameters.");
            }

            var selection = (document.Selections ?? []).SingleOrDefault(candidate =>
                string.Equals(candidate.Id, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"ROI / Crop selection '{step.InputEntityIds[1]}' is missing.");
            var source = document.Source;
            if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || source.ByteLength is null
                || string.IsNullOrWhiteSpace(source.ContentSha256)
                || source.GridWidth is null
                || source.GridHeight is null)
            {
                throw new InvalidDataException("ROI / Crop requires a complete recipe-bound C3D source identity.");
            }
            var path = Path.IsPathFullyQualified(source.Path)
                ? Path.GetFullPath(source.Path)
                : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, source.Path));
            var snapshot = C3DHeightFieldSnapshot.LoadVerified(
                path,
                source.Id,
                source.Unit,
                source.FrameId,
                source.ByteLength.Value,
                source.ContentSha256,
                source.GridWidth.Value,
                source.GridHeight.Value);
            input = new C3DRoiCropInput(step.Id, snapshot, selection, step.OutputEntityId);
            C3DRoiCropRule.ValidateInput(input);
            message = "ROI / Crop v1 is ready from one exact source-bound GridRectangle.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }
}
