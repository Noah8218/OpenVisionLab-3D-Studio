using System.Security.Cryptography;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.Integration;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using Xunit;

namespace OpenVisionLab.ThreeD.Reporting.Tests;

public sealed class ThreeDIntegrationExchangeTests
{
    [Fact]
    public void PublishCompletedResult_UsesExactRunRecordCorrelationAndPreservesNg()
    {
        using var fixture = new ExchangeFixture();
        var acknowledgement = fixture.Accept();
        fixture.WriteRunRecord(ResultStatus.Fail);

        var result = ThreeDIntegrationExchange.PublishCompletedResult(
            fixture.Root,
            fixture.Handoff.TransactionId,
            ExchangeFixture.Consumer,
            fixture.RunRecordPath);

        Assert.Equal(acknowledgement.MessageId, result.AcknowledgementMessageId);
        Assert.Equal(IntegrationInspectionOutcome.Ng, result.Outcome);
        Assert.NotEqual(IntegrationInspectionOutcome.ExecutionError, result.Outcome);
        Assert.Equal(
            IntegrationRunCorrelation.FromContext(fixture.Handoff.Context),
            result.Correlation);
        Assert.True(File.Exists(
            Path.Combine(
                fixture.TransactionDirectory,
                IntegrationTransactionLayout.ArtifactsDirectoryName,
                "3d-run-record.json")));
    }

    [Fact]
    public void PublishCompletedResult_RejectsRunRecordWithoutIntegrationContext()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        fixture.WriteRunRecord(
            ResultStatus.Pass,
            integrationContext: null,
            includeIntegrationContext: false);

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationExchange.PublishCompletedResult(
                fixture.Root,
                fixture.Handoff.TransactionId,
                ExchangeFixture.Consumer,
                fixture.RunRecordPath));

        Assert.Equal(IntegrationErrorCode.CorrelationMismatch, exception.ErrorCode);
        Assert.False(File.Exists(
            Path.Combine(
                fixture.TransactionDirectory,
                IntegrationTransactionLayout.ResultFileName)));
    }

    [Fact]
    public void PublishCompletedResult_RejectsWrongStepEvenWhenRunIdIsPresent()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        var wrongContext = fixture.CreateIntegrationContext() with
        {
            StepId = "different-step"
        };
        fixture.WriteRunRecord(ResultStatus.Pass, wrongContext);

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationExchange.PublishCompletedResult(
                fixture.Root,
                fixture.Handoff.TransactionId,
                ExchangeFixture.Consumer,
                fixture.RunRecordPath));

        Assert.Equal(IntegrationErrorCode.CorrelationMismatch, exception.ErrorCode);
    }

    [Fact]
    public void ReadHandoff_FailsClosedWhenArtifactBytesAreTampered()
    {
        using var fixture = new ExchangeFixture();
        File.WriteAllBytes(
            Path.Combine(
                fixture.TransactionDirectory,
                IntegrationTransactionLayout.ArtifactsDirectoryName,
                "inspection-source.c3d"),
            [0xFF]);

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationExchange.ReadHandoff(
                fixture.Root,
                fixture.Handoff.TransactionId));

        Assert.Equal(IntegrationErrorCode.ArtifactLengthMismatch, exception.ErrorCode);
    }

    private sealed class ExchangeFixture : IDisposable
    {
        public ExchangeFixture()
        {
            Root = Path.Combine(
                "D:\\OpenVisionLab-TestData\\OpenVisionLab-3D-Studio",
                "integration-reporting-tests",
                Guid.NewGuid().ToString("N"));
            TransactionId = Guid.NewGuid();
            TransactionDirectory = Path.Combine(
                Root,
                IntegrationTransactionLayout.TransactionsDirectoryName,
                TransactionId.ToString("D"));
            var artifactsDirectory = Path.Combine(
                TransactionDirectory,
                IntegrationTransactionLayout.ArtifactsDirectoryName);
            Directory.CreateDirectory(artifactsDirectory);

            var projectPath = WriteArtifact(artifactsDirectory, "machine-project.ovmachine", [1, 2, 3]);
            var sourcePath = WriteArtifact(artifactsDirectory, "inspection-source.c3d", [4, 5, 6, 7]);
            var recipePath = WriteArtifact(artifactsDirectory, "inspection-recipe.json", [8, 9]);
            Handoff = new IntegrationHandoffV2(
                IntegrationContractSchema.V2,
                IntegrationMessageKind.Handoff,
                Guid.NewGuid(),
                TransactionId,
                DateTimeOffset.UtcNow,
                new IntegrationApplicationIdentity(
                    IntegrationApplicationIds.MachineStudio,
                    "0.1.0-rc.1",
                    new string('1', 40),
                    IntegrationSourceState.Clean),
                new IntegrationInspectionContextV2(
                    "machine-project-1",
                    "1.0",
                    "sequence-1",
                    "step-1",
                    "camera-1",
                    "acquisition-1",
                    "frame-1",
                    "mm",
                    IntegrationInspectionModality.ThreeD,
                    IntegrationInspectionInputKind.PointCloud,
                    HashFile(sourcePath),
                    HashFile(recipePath),
                    Consumer,
                    [
                        Artifact(IntegrationArtifactRoles.MachineProject, "machine-project", projectPath, "artifacts/machine-project.ovmachine"),
                        Artifact(IntegrationArtifactRoles.InspectionSource, "inspection-source", sourcePath, "artifacts/inspection-source.c3d"),
                        Artifact(IntegrationArtifactRoles.InspectionRecipe, "inspection-recipe", recipePath, "artifacts/inspection-recipe.json")
                    ]));
            File.WriteAllBytes(
                Path.Combine(TransactionDirectory, IntegrationTransactionLayout.HandoffFileName),
                IntegrationContractJson.SerializeCanonical(Handoff));
            RunRecordPath = Path.Combine(Root, "run-record.json");
        }

        public string Root { get; }
        public Guid TransactionId { get; }
        public string TransactionDirectory { get; }
        public string RunRecordPath { get; }
        public IntegrationHandoffV2 Handoff { get; }
        public static IntegrationApplicationIdentity Consumer { get; } = new(
            IntegrationApplicationIds.ThreeDStudio,
            "0.1.1",
            new string('2', 40),
            IntegrationSourceState.Clean);

        public IntegrationAcknowledgementV2 Accept() =>
            ThreeDIntegrationExchange.PublishAcknowledgement(
                Root,
                Handoff,
                Consumer);

        public void WriteRunRecord(
            ResultStatus status,
            InspectionRunIntegrationContext? integrationContext = null,
            bool includeIntegrationContext = true)
        {
            var context = integrationContext ?? CreateIntegrationContext();
            var record = new InspectionRunRecord(
                "1.9",
                "run-1",
                DateTimeOffset.UtcNow,
                new InspectionRunRecipe(
                    "tool-recipe",
                    "1.0",
                    "inspection-recipe.json",
                    Handoff.Context.RecipeSha256),
                new InspectionRunSource(
                    "source-1",
                    "inspection-source.c3d",
                    Handoff.Context.InputSha256,
                    4,
                    "mm"),
                "Integration Test",
                status,
                "Completed",
                1.0,
                [],
                [],
                "matched",
                new InspectionRunArtifacts(
                    "report.txt",
                    null,
                    null,
                    RunRecordPath,
                    null,
                    null))
            {
                Step = new InspectionRunStep(context.StepId, "source-1", [], []),
                IntegrationContext = includeIntegrationContext ? context : null
            };

            InspectionRunRecordJson.Write(RunRecordPath, record);
        }

        public InspectionRunIntegrationContext CreateIntegrationContext() =>
            new(
                Handoff.Context.ProjectId,
                Handoff.Context.ProjectSchema,
                Handoff.Context.SequenceId,
                Handoff.Context.StepId,
                Handoff.Context.CameraId,
                Handoff.Context.AcquisitionId,
                Handoff.Context.FrameId,
                Handoff.Context.Unit,
                Handoff.Context.Modality.ToString(),
                Handoff.Context.InputKind.ToString(),
                Handoff.Context.ConsumerBuild.ApplicationId,
                Handoff.Context.ConsumerBuild.ApplicationVersion,
                Handoff.Context.ConsumerBuild.SourceCommit,
                Handoff.Context.ConsumerBuild.SourceState.ToString());

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static string WriteArtifact(
            string directory,
            string fileName,
            byte[] bytes)
        {
            var path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static IntegrationArtifactReference Artifact(
            string role,
            string id,
            string path,
            string relativePath)
        {
            var info = new FileInfo(path);
            return new(role, id, relativePath, info.Length, HashFile(path));
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
