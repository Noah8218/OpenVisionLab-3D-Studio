using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeDatumPlaneDeviationRunnerExecution
{
    public static int Run(string recipePath, string deviationStepId, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var step = document.Steps.Single(candidate => string.Equals(candidate.Id, deviationStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(step.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal) || step.InputEntityIds.Count != 3)
            {
                throw new InvalidDataException("Runner Datum Plane Raw-Height Deviation step must have raw C3D, Published PlaneFeature, and GridRectangle inputs.");
            }

            var planeStep = document.Steps.Single(candidate => string.Equals(candidate.ToolId, "three-point-plane", StringComparison.Ordinal)
                && string.Equals(candidate.OutputEntityId, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            var plane = ToolRecipeThreePointPlaneExecution.Execute(document, planeStep.Id, Path.GetDirectoryName(fullRecipePath));
            if (plane.Result.Status != ResultStatus.Pass || plane.Output is null)
            {
                throw new InvalidDataException($"Runner upstream 3-Point Plane failed: {plane.Result.Message}");
            }

            var deviation = ToolRecipeDatumPlaneDeviationExecution.Execute(document, step.Id, plane.Output, Path.GetDirectoryName(fullRecipePath));
            if (deviation.Output is null || deviation.Result.Status is not (ResultStatus.Pass or ResultStatus.Fail))
            {
                throw new InvalidDataException($"Runner Datum Plane Raw-Height Deviation failed: {deviation.Result.Message}");
            }

            var output = deviation.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath,
            [
                "OpenVisionLab 3D Datum Plane Raw-Height Deviation Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                $"Plane|step={planeStep.Id}|output={plane.Output.OutputEntityId}|sha256={plane.Output.ContentSha256}|normal={plane.Output.NormalX:R},{plane.Output.NormalY:R},{plane.Output.NormalZ:R}|offset={plane.Output.PlaneOffset:R}",
                $"DatumPlaneDeviation|status={deviation.Result.Status}|step={step.Id}|output={output.OutputEntityId}|sha256={output.ContentSha256}|role={output.OutputRole}",
                $"ROI|selection={output.MeasurementSelectionId}|sha256={output.MeasurementSelectionContentSha256}|row={output.MeasurementRectangle.Row}|column={output.MeasurementRectangle.Column}|rows={output.MeasurementRectangle.RowCount}|columns={output.MeasurementRectangle.ColumnCount}",
                $"Metrics|p2vRawHeight={output.PeakToValleyRawHeight:R}|rmsRawHeightResidual={output.RmsRawHeightResidual:R}|minimumRawHeightResidual={output.MinimumRawHeightResidual:R}|maximumRawHeightResidual={output.MaximumRawHeightResidual:R}|valid={output.ValidSampleCount}|missing={output.MissingSampleCount}",
                $"Policy|residual={output.ResidualPolicy}|minimumValid={output.MinimumValidSampleCount}|minimumAbsoluteNormalY={output.MinimumAbsoluteNormalY:R}|display={output.DisplaySamplingPolicy}|contract={C3DDatumPlaneDeviationFeature.ContractVersion}",
                "Boundary|local raw-height software result only; source C3D unchanged; not physical calibration or metrology."
            ]);
            Console.WriteLine($"3D Datum Plane Raw-Height Deviation Runner: {deviation.Result.Status} ({output.OutputRole}, P2V {output.PeakToValleyRawHeight:G6}, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, ["OpenVisionLab 3D Datum Plane Raw-Height Deviation Runner report", $"Error|{exception.Message}"]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}
