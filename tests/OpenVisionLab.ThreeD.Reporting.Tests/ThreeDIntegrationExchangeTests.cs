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

        var result = ThreeDIntegrationV2Exchange.PublishCompletedResult(
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
    public void PublishCompletedResult_RejectsMismatchedSourceHash()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        fixture.WriteRunRecord(ResultStatus.Pass, sourceSha256: new string('A', 64));

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationV2Exchange.PublishCompletedResult(
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
    public void PublishCompletedResult_RejectsMismatchedRecipeHash()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        fixture.WriteRunRecord(ResultStatus.Pass, recipeSha256: new string('B', 64));

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationV2Exchange.PublishCompletedResult(
                fixture.Root,
                fixture.Handoff.TransactionId,
                ExchangeFixture.Consumer,
                fixture.RunRecordPath));

        Assert.Equal(IntegrationErrorCode.CorrelationMismatch, exception.ErrorCode);
    }

    [Fact]
    public void PublishCompletedResult_RejectsMismatchedStepIdentity()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        fixture.WriteRunRecord(ResultStatus.Pass, stepId: "different-step");

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationV2Exchange.PublishCompletedResult(
                fixture.Root,
                fixture.Handoff.TransactionId,
                ExchangeFixture.Consumer,
                fixture.RunRecordPath));

        Assert.Equal(IntegrationErrorCode.CorrelationMismatch, exception.ErrorCode);
    }

    [Fact]
    public void PublishCompletedResult_CopiesAndValidatesAdditionalEvidence()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        fixture.WriteRunRecord(ResultStatus.Pass);
        var evidenceSourcePath = Path.Combine(fixture.Root, "coordinate-projection-result.json");
        File.WriteAllText(evidenceSourcePath, "{\"projectionId\":\"projection-1\"}");

        var result = ThreeDIntegrationV2Exchange.PublishCompletedResult(
            fixture.Root,
            fixture.Handoff.TransactionId,
            ExchangeFixture.Consumer,
            fixture.RunRecordPath,
            [new ThreeDIntegrationEvidenceArtifact(
                ThreeDCoordinateProjectionContract.ResultEvidenceRole,
                ThreeDCoordinateProjectionContract.ResultEvidenceArtifactId,
                evidenceSourcePath,
                "artifacts/coordinate-projection-result.json")]);

        var evidence = Assert.Single(result.Evidence);
        Assert.Equal(ThreeDCoordinateProjectionContract.ResultEvidenceRole, evidence.Role);
        Assert.Equal(
            ThreeDCoordinateProjectionContract.ResultEvidenceArtifactId,
            evidence.ArtifactId);
        var persisted = ThreeDIntegrationV2Exchange.ReadResult(
            fixture.Root,
            fixture.Handoff.TransactionId);
        Assert.Equal(evidence, Assert.Single(persisted.Evidence));
        Assert.Equal(
            "{\"projectionId\":\"projection-1\"}",
            File.ReadAllText(Path.Combine(fixture.TransactionDirectory, evidence.RelativePath)));
    }

    [Fact]
    public void PublishCompletedResult_RejectsEvidenceOutsideArtifactsAndCleansPublication()
    {
        using var fixture = new ExchangeFixture();
        fixture.Accept();
        fixture.WriteRunRecord(ResultStatus.Pass);
        var evidenceSourcePath = Path.Combine(fixture.Root, "coordinate-projection-result.json");
        File.WriteAllText(evidenceSourcePath, "{}");

        var exception = Assert.Throws<IntegrationContractException>(() =>
            ThreeDIntegrationV2Exchange.PublishCompletedResult(
                fixture.Root,
                fixture.Handoff.TransactionId,
                ExchangeFixture.Consumer,
                fixture.RunRecordPath,
                [new ThreeDIntegrationEvidenceArtifact(
                    ThreeDCoordinateProjectionContract.ResultEvidenceRole,
                    ThreeDCoordinateProjectionContract.ResultEvidenceArtifactId,
                    evidenceSourcePath,
                    "../outside.json")]));

        Assert.Equal(IntegrationErrorCode.UnsafeArtifactPath, exception.ErrorCode);
        Assert.False(File.Exists(Path.Combine(
            fixture.TransactionDirectory,
            IntegrationTransactionLayout.ResultFileName)));
        Assert.False(File.Exists(Path.Combine(
            fixture.TransactionDirectory,
            IntegrationTransactionLayout.ArtifactsDirectoryName,
            "3d-run-record.json")));
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
            ThreeDIntegrationV2Exchange.ReadHandoff(
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
                    IntegrationInspectionInputKind.HeightMap,
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
            ThreeDIntegrationV2Exchange.PublishAcknowledgement(
                Root,
                Handoff,
                Consumer);

        public void WriteRunRecord(
            ResultStatus status,
            string? sourceSha256 = null,
            string? recipeSha256 = null,
            string? stepId = null)
        {
            var record = new InspectionRunRecord(
                "1.9",
                "run-1",
                DateTimeOffset.UtcNow,
                new InspectionRunRecipe(
                    "tool-recipe",
                    "1.0",
                    "inspection-recipe.json",
                    recipeSha256 ?? Handoff.Context.RecipeSha256),
                new InspectionRunSource(
                    "source-1",
                    "inspection-source.c3d",
                    sourceSha256 ?? Handoff.Context.InputSha256,
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
                Step = new InspectionRunStep(
                    stepId ?? Handoff.Context.StepId,
                    "source-1",
                    [],
                    [])
            };

            InspectionRunRecordJson.Write(RunRecordPath, record);
        }

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
