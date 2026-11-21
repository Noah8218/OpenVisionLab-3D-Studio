using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

// Owns the report-only linked queue test and its WPF synchronization context.
// The ordinary ordered-run verifier remains directly WPF-neutral.
internal static class LinkedInspectionQueueVerification
{
    public static bool Verify(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Linked MAP repeated inspection and per-input output identity; no Window or Viewer constructed." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok, string detail = "") { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previous = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        var previousContext = SynchronizationContext.Current;
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
        bool WaitUntil(Func<bool> done, double seconds)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!done() && elapsed.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            return done();
        }
        bool Await(Task<bool> task)
        {
            if (!WaitUntil(() => task.IsCompleted, 45)) throw new TimeoutException("Linked inspection did not finish.");
            return task.GetAwaiter().GetResult();
        }
        try
        {
            using var shell = new ShellMainWindowViewModel(
                recentRunRecordsPath: Path.Combine(root, "recent-runs-linked-queue.json"),
                recentRecipesPath: Path.Combine(root, "recent-recipes-linked-queue.json"),
                orderedRunRecordRoot: Path.Combine(root, "records"));
            var workbench = shell.Workbench;
            if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
            var source = C3DHeightFieldSnapshot.LoadIdentified(workbench.Source.Path, workbench.Source.Id, workbench.Source.Unit, workbench.Source.FrameId);
            var variantValues = source.Values.ToArray();
            for (var row = 58; row < 96; row++)
                for (var column = 58; column < 168; column++) variantValues[row * source.Width + column] += 100;
            var variantPath = Path.Combine(root, "variant.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("owned-variant", source.Width, source.Height, variantValues).SaveC3D(variantPath);
            var missingValues = source.Values.ToArray();
            missingValues[0] = double.NaN;
            var missingPath = Path.Combine(root, "missing-reference.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("owned-missing", source.Width, source.Height, missingValues).SaveC3D(missingPath);
            Check("teaching baseline creates its own output", Await(workbench.RunTeachingRecipeAsync())
                && workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _));
            workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out var teaching, out _);
            var originalPath = workbench.Source.Path;
            var queue = workbench.InspectionQueue;
            queue.AddSources([originalPath, variantPath, missingPath]);
            queue.RunAllCommand.Execute(null);
            Check("three queued inputs finish explicitly", WaitUntil(() => !queue.IsRunning, 45)
                && queue.AllCount == 3);
            foreach (var sample in queue.Samples)
            {
                var missing = sample.SourcePath == missingPath;
                if (missing)
                {
                    Check("failed producer exposes no earlier sample map", sample.Record?.Status == ResultStatus.Error && sample.HeightMaps.Count == 0);
                    continue;
                }
                var expected = sample.SourcePath == variantPath ? 1588d : 1488d;
                var metric = sample.Record?.Steps?.Single(step => step.Id == "step.thickness.1").Metrics.Single(value => value.Name == "Mean");
                var map = sample.HeightMaps.GetValueOrDefault("output.flat-coupons");
                Check("queued thickness uses this sample's corrected map", metric?.Value == expected && map is not null
                    && map.RootSourceSha256 == sample.Record!.Source.Sha256,
                    $"{sample.FileName};value={metric?.Value};root={map?.RootSourceSha256}");
                if (map is not null)
                {
                    var actual = C3DHeightFieldSnapshot.LoadIdentified(map.Path, map.EntityId, map.Unit, map.FrameId);
                    Check("queued display file matches the actual measured surface", actual.ContentSha256 == map.ContentSha256
                        && actual.Values.Span[70 * actual.Width + 80] == 10000 + expected);
                }
            }
            Check("inspection leaves the taught source and output separate", workbench.Source.Path == originalPath
                && workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out var retained, out _)
                && retained?.ContentSha256 == teaching?.ContentSha256);
            var failed = queue.Samples.Single(sample => sample.SourcePath == missingPath);
            File.Copy(variantPath, missingPath, true);
            queue.SelectedSample = failed;
            queue.RunSelectedCommand.Execute(null);
            var recovered = WaitUntil(() => !queue.IsRunning, 30);
            Check("valid retry replaces the failed sample's output identity", recovered && failed.Record?.Steps?.Count == 12
                && failed.HeightMaps.TryGetValue("output.flat-coupons", out var retry) && retry.RootSourceSha256 == failed.Record.Source.Sha256
                && retry.RootSourceSha256 != source.ContentSha256);
        }
        catch (Exception exception) { Check("linked repeated inspection", false, exception.ToString()); }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previous); }
        summary = $"Linked inspection queue: {passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return total > 0 && passed == total;
    }

}
