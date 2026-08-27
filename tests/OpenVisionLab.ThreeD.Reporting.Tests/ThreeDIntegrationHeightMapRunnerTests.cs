using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.Integration;
using Xunit;

namespace OpenVisionLab.ThreeD.Reporting.Tests;

public sealed class ThreeDIntegrationHeightMapRunnerTests
{
    [Fact]
    public void RunAcceptedHeightMapHandoff_MaterializesC3dAndPreservesPassAndNg()
    {
        using var fixture = new HeightMapExchangeFixture();

        var passHandoff = fixture.CreateHandoff(
            "pass",
            [
                1.00, 1.05, 1.02,
                1.04, 1.01, 1.03,
                1.02, 1.06, 1.01
            ]);
        fixture.Accept(passHandoff);
        var pass = ThreeDIntegrationHeightMapRunner.RunAcceptedHandoff(
            fixture.Root,
            passHandoff.TransactionId,
            HeightMapExchangeFixture.Consumer,
            new ThreeDHeightMapInspectionRequest(0.50),
            TestContext.Current.CancellationToken);

        var ngHandoff = fixture.CreateHandoff(
            "ng",
            [
                1.00, 1.00, 1.00,
                3.00, 3.00, 3.00,
                1.00, 1.00, 1.00
            ]);
        fixture.Accept(ngHandoff);
        var ng = ThreeDIntegrationHeightMapRunner.RunAcceptedHandoff(
            fixture.Root,
            ngHandoff.TransactionId,
            HeightMapExchangeFixture.Consumer,
            new ThreeDHeightMapInspectionRequest(0.50),
            TestContext.Current.CancellationToken);

        Assert.Equal(IntegrationInspectionOutcome.Pass, pass.Outcome);
        Assert.Equal(IntegrationInspectionOutcome.Ng, ng.Outcome);
        Assert.NotNull(pass.RunRecord);
        Assert.NotNull(ng.RunRecord);
        Assert.Equal(IntegrationInspectionInputKind.HeightMap, pass.Correlation.InputKind);
        Assert.Equal(IntegrationInspectionModality.ThreeD, pass.Correlation.Modality);
        Assert.True(File.Exists(
            Path.Combine(
                fixture.TransactionDirectory(passHandoff),
                IntegrationTransactionLayout.ArtifactsDirectoryName,
                "3d-run-record.json")));
        Assert.Equal(
            IntegrationInspectionOutcome.Ng,
            ThreeDIntegrationExchange.ReadResult(
                fixture.Root,
                ngHandoff.TransactionId).Outcome);
    }

    [Fact]
    public void RunAcceptedHeightMapHandoffFromRecipe_UsesCopiedRecipePolicy()
    {
        using var fixture = new HeightMapExchangeFixture();

        var handoff = fixture.CreateHandoff(
            "recipe-pass",
            [
                1.00, 1.05, 1.02,
                1.04, 1.01, 1.03,
                1.02, 1.06, 1.01
            ],
            writeValidRecipe: true);
        fixture.Accept(handoff);

        var result = ThreeDIntegrationHeightMapRunner.RunAcceptedHandoffFromRecipe(
            fixture.Root,
            handoff.TransactionId,
            HeightMapExchangeFixture.Consumer,
            TestContext.Current.CancellationToken);

        Assert.Equal(IntegrationInspectionOutcome.Pass, result.Outcome);
        Assert.Contains(
            result.Metrics,
            metric => string.Equals(
                metric.Name,
                "MaximumPeakToValley",
                StringComparison.Ordinal));
    }

    [Fact]
    public void RunAcceptedHeightMapHandoffFromRecipe_RejectsContextMismatch()
    {
        using var fixture = new HeightMapExchangeFixture();

        var handoff = fixture.CreateHandoff(
            "recipe-mismatch",
            [
                1.00, 1.05, 1.02,
                1.04, 1.01, 1.03,
                1.02, 1.06, 1.01
            ],
            writeValidRecipe: true,
            recipeFrameId: "different-frame");
        fixture.Accept(handoff);

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationHeightMapRunner.RunAcceptedHandoffFromRecipe(
                fixture.Root,
                handoff.TransactionId,
                HeightMapExchangeFixture.Consumer,
                TestContext.Current.CancellationToken));

        Assert.Equal(IntegrationErrorCode.CorrelationMismatch, exception.ErrorCode);
    }

    [Fact]
    public void RunAcceptedHeightMapHandoff_CancellationBeforeStartDoesNotPublishResult()
    {
        using var fixture = new HeightMapExchangeFixture();

        var handoff = fixture.CreateHandoff(
            "cancel-before-start",
            [
                1.00, 1.05, 1.02,
                1.04, 1.01, 1.03,
                1.02, 1.06, 1.01
            ]);
        fixture.Accept(handoff);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            RunCancelledHeightMapHandoff(
                fixture.Root,
                handoff.TransactionId,
                HeightMapExchangeFixture.Consumer,
                new ThreeDHeightMapInspectionRequest(0.50),
                cancellation.Token));
        Assert.False(File.Exists(
            Path.Combine(
                fixture.TransactionDirectory(handoff),
                IntegrationTransactionLayout.ResultFileName)));
    }

    private static void RunCancelledHeightMapHandoff(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild,
        ThreeDHeightMapInspectionRequest request,
        CancellationToken cancellationToken) =>
        ThreeDIntegrationHeightMapRunner.RunAcceptedHandoff(
            exchangeRoot,
            transactionId,
            consumerBuild,
            request,
            cancellationToken);

    private sealed class HeightMapExchangeFixture : IDisposable
    {
        public static IntegrationApplicationIdentity Consumer { get; } = new(
            IntegrationApplicationIds.ThreeDStudio,
            "0.2.0-alpha.1",
            new string('3', 40),
            IntegrationSourceState.Clean);

        public HeightMapExchangeFixture()
        {
            Root = Path.Combine(
                "D:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio",
                "integration-heightmap-runner-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public IntegrationHandoffV2 CreateHandoff(
            string caseName,
            IReadOnlyList<double> values,
            bool writeValidRecipe = false,
            string? recipeFrameId = null)
        {
            var transactionId = Guid.NewGuid();
            string transactionDirectory = Path.Combine(
                Root,
                IntegrationTransactionLayout.TransactionsDirectoryName,
                transactionId.ToString("D"));
            string artifactsDirectory = Path.Combine(
                transactionDirectory,
                IntegrationTransactionLayout.ArtifactsDirectoryName);
            Directory.CreateDirectory(artifactsDirectory);

            string sourcePath = Path.Combine(
                artifactsDirectory,
                "inspection-source.c3d");
            string frameId = $"frame-{caseName}";
            C3DHeightFieldSnapshot.CreateForVerification(
                    $"source-{caseName}",
                    3,
                    3,
                    values,
                    "mm",
                    frameId)
                .SaveC3D(sourcePath);
            string recipePath = Path.Combine(
                artifactsDirectory,
                "inspection-recipe.json");
            string recipeJson = writeValidRecipe
                ? $$"""
                  {
                    "recipeType": "c3d-warpage",
                    "version": "1.0",
                    "source": {
                      "entityId": "source.{{caseName}}",
                      "name": "Verification source",
                      "path": "source.c3d",
                      "unit": "mm"
                    },
                    "step": {
                      "id": "step.{{caseName}}",
                      "sourceEntityId": "source.{{caseName}}",
                      "referenceId": "reference.{{caseName}}",
                      "referenceMode": "BestFitInspectionRoi",
                      "roi": {
                        "row": 0,
                        "column": 0,
                        "rowCount": 3,
                        "columnCount": 3
                      },
                      "acceptance": {
                        "maximumPeakToValley": 0.5,
                        "maximumRms": null
                      },
                      "unit": "mm",
                      "frameId": "{{recipeFrameId ?? frameId}}",
                      "minimumValidSamples": 3,
                      "enabled": true
                    }
                  }
                  """
                : "{\"tool\":\"warpage\",\"maximumPeakToValley\":0.5}";
            File.WriteAllText(recipePath, recipeJson, new UTF8Encoding(false));
            string projectPath = Path.Combine(
                artifactsDirectory,
                "machine-project.ovmachine");
            File.WriteAllText(
                projectPath,
                "{\"schema\":\"machine-project/1.0\"}",
                new UTF8Encoding(false));

            var source = Artifact(
                IntegrationArtifactRoles.InspectionSource,
                $"source-{caseName}",
                sourcePath,
                "artifacts/inspection-source.c3d");
            var recipe = Artifact(
                IntegrationArtifactRoles.InspectionRecipe,
                $"recipe-{caseName}",
                recipePath,
                "artifacts/inspection-recipe.json");
            var project = Artifact(
                IntegrationArtifactRoles.MachineProject,
                $"project-{caseName}",
                projectPath,
                "artifacts/machine-project.ovmachine");
            var handoff = new IntegrationHandoffV2(
                IntegrationContractSchema.V2,
                IntegrationMessageKind.Handoff,
                Guid.NewGuid(),
                transactionId,
                DateTimeOffset.UtcNow,
                new IntegrationApplicationIdentity(
                    IntegrationApplicationIds.MachineStudio,
                    "1.4.0",
                    new string('4', 40),
                    IntegrationSourceState.Clean),
                new IntegrationInspectionContextV2(
                    "machine-project",
                    "machine-project/1.0",
                    "sequence-001",
                    "inspect-heightmap",
                    "camera-virtual",
                    $"acquisition-{caseName}",
                    frameId,
                    "mm",
                    IntegrationInspectionModality.ThreeD,
                    IntegrationInspectionInputKind.HeightMap,
                    source.Sha256,
                    recipe.Sha256,
                    Consumer,
                    [project, source, recipe]));
            File.WriteAllBytes(
                Path.Combine(
                    transactionDirectory,
                    IntegrationTransactionLayout.HandoffFileName),
                IntegrationContractJson.SerializeCanonical(handoff));
            return handoff;
        }

        public void Accept(IntegrationHandoffV2 handoff) =>
            ThreeDIntegrationExchange.PublishAcknowledgement(
                Root,
                handoff,
                Consumer);

        public string TransactionDirectory(IntegrationHandoffV2 handoff) =>
            Path.Combine(
                Root,
                IntegrationTransactionLayout.TransactionsDirectoryName,
                handoff.TransactionId.ToString("D"));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static IntegrationArtifactReference Artifact(
            string role,
            string id,
            string path,
            string relativePath)
        {
            using var stream = File.OpenRead(path);
            return new(
                role,
                id,
                relativePath,
                stream.Length,
                Convert.ToHexString(SHA256.HashData(stream)));
        }
    }
}
