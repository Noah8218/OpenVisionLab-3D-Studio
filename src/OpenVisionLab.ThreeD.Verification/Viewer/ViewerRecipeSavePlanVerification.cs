using System.IO;
using OpenVisionLab.ThreeD.Viewer.Recipes;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerRecipeSavePlanVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer recipe-save plan verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        static ViewerRecipeSavePlan Resolve(
            bool nominalActual = false,
            bool lazTwoPoint = false,
            bool warpage = false,
            bool thickness = false,
            bool gapFlush = false,
            bool pointPairDimensions = false) =>
            ViewerRecipeSavePlan.Resolve(
                nominalActual,
                lazTwoPoint,
                warpage,
                thickness,
                gapFlush,
                pointPairDimensions);

        var nominalActualPlan = Resolve(nominalActual: true);
        Check(
            "nominal/actual route and file name",
            nominalActualPlan.Route == ViewerRecipeSaveRoute.NominalActual
            && nominalActualPlan.DefaultFileName == "nominal-actual-surface-deviation.recipe.json",
            $"route={nominalActualPlan.Route};file={nominalActualPlan.DefaultFileName}");

        var lazPlan = Resolve(lazTwoPoint: true);
        Check(
            "LAZ/LAS route and file name",
            lazPlan.Route == ViewerRecipeSaveRoute.LazTwoPoint
            && lazPlan.DefaultFileName == "laz-two-point-measurement.recipe.json",
            $"route={lazPlan.Route};file={lazPlan.DefaultFileName}");

        var c3dPlans = new[]
        {
            (Resolve(warpage: true), ViewerRecipeSaveRoute.Warpage, "c3d-warpage.recipe.json"),
            (Resolve(thickness: true), ViewerRecipeSaveRoute.Thickness, "c3d-thickness.recipe.json"),
            (Resolve(gapFlush: true), ViewerRecipeSaveRoute.GapFlush, "c3d-gap-flush.recipe.json"),
            (Resolve(pointPairDimensions: true), ViewerRecipeSaveRoute.PointPairDimensions, "c3d-point-pair-dimensions.recipe.json")
        };
        Check(
            "C3D route names and file names",
            c3dPlans.All(item => item.Item1.Route == item.Item2 && item.Item1.DefaultFileName == item.Item3),
            string.Join(';', c3dPlans.Select(item => $"{item.Item1.Route}:{item.Item1.DefaultFileName}")));

        var precedencePlan = Resolve(
            nominalActual: true,
            lazTwoPoint: true,
            warpage: true,
            thickness: true,
            gapFlush: true,
            pointPairDimensions: true);
        Check(
            "existing precedence is stable",
            precedencePlan.Route == ViewerRecipeSaveRoute.NominalActual,
            $"route={precedencePlan.Route}");

        var fallbackPlan = Resolve();
        Check(
            "height-deviation remains the fallback route",
            fallbackPlan.Route == ViewerRecipeSaveRoute.HeightDeviation
            && fallbackPlan.DefaultFileName == "c3d-height-deviation.recipe.json",
            $"route={fallbackPlan.Route};file={fallbackPlan.DefaultFileName}");

        var repeatA = Resolve(warpage: true, pointPairDimensions: true);
        var repeatB = Resolve(warpage: true, pointPairDimensions: true);
        Check(
            "repeated resolution is deterministic",
            repeatA == repeatB,
            $"first={repeatA};second={repeatB}");

        var partialPlan = Resolve(pointPairDimensions: true, gapFlush: true);
        Check(
            "lower-priority C3D route wins only when higher routes are unavailable",
            partialPlan.Route == ViewerRecipeSaveRoute.GapFlush,
            $"route={partialPlan.Route}");

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerRecipeSavePlan|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
