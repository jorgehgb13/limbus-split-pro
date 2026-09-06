using System.Security.Cryptography;
using System.Text;
using LimbusSplitPro.Core.Licensing;
using Xunit;

namespace LimbusSplitPro.Core.Tests;

public class ModelManifestVerifierTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _modelFilePath;
    private readonly ModelManifestVerifier _verifier;

    public ModelManifestVerifierTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("limbus-tests-").FullName;
        _modelFilePath = Path.Combine(_tempDir, "fake-model.th");
        var content = "contenido de prueba, no es un modelo real"u8.ToArray();
        File.WriteAllBytes(_modelFilePath, content);
        var realHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var manifest = new ModelManifestDocument
        {
            SchemaVersion = "1.0",
            Models = new List<ModelEntry>
            {
                new()
                {
                    Id = "fake-model",
                    DisplayName = "Fake Model",
                    Sources = new List<string> { "vocals" },
                    Version = "1",
                    OriginUrl = "https://example.invalid/origin",
                    DownloadUrl = "https://example.invalid/download",
                    RelativePath = "fake-model.th",
                    Sha256 = realHash,
                    ExpectedSizeBytes = content.Length,
                    CodeLicense = "MIT",
                    WeightsLicenseStatus = "confirmed_permissive",
                    WeightsLicenseEvidenceUrl = "https://example.invalid/evidence",
                    Author = "Test",
                    Attribution = "Test",
                    Capabilities = new List<string> { "vocals" },
                    RedistributionAuthorized = false,
                    CommercialUseAuthorized = false,
                    DistributionMode = "download_on_demand",
                },
            },
        };

        _verifier = new ModelManifestVerifier(manifest);
    }

    [Fact]
    public void CorrectFile_IsAllowed()
    {
        var result = _verifier.Verify("fake-model", _modelFilePath);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void UnregisteredModel_IsDenied()
    {
        var result = _verifier.Verify("does-not-exist", _modelFilePath);
        Assert.False(result.IsAllowed);
        Assert.Equal(VerificationFailureReason.NotRegistered, result.Reason);
    }

    [Fact]
    public void MissingFile_IsDenied()
    {
        var result = _verifier.Verify("fake-model", Path.Combine(_tempDir, "no-existe.th"));
        Assert.False(result.IsAllowed);
        Assert.Equal(VerificationFailureReason.FileMissing, result.Reason);
    }

    [Fact]
    public void TamperedFile_FailsHashCheck()
    {
        File.WriteAllBytes(_modelFilePath, "contenido de prueba, no es un modelo real - ALTERADO"u8.ToArray());
        var result = _verifier.Verify("fake-model", _modelFilePath);
        Assert.False(result.IsAllowed);
        // El tamaño también cambió, así que puede fallar por tamaño o por
        // hash — lo importante es que NUNCA se permite un archivo alterado.
        Assert.True(
            result.Reason == VerificationFailureReason.HashMismatch ||
            result.Reason == VerificationFailureReason.SizeMismatch);
    }

    [Fact]
    public void PublicBuildRequiresRedistributionAuthorization()
    {
        var result = _verifier.Verify(
            "fake-model", _modelFilePath, requireRedistributionAuthorized: true);
        Assert.False(result.IsAllowed);
        Assert.Equal(VerificationFailureReason.RedistributionNotAuthorized, result.Reason);
    }

    [Fact]
    public void MissingRequiredCapability_IsDenied()
    {
        var result = _verifier.Verify(
            "fake-model", _modelFilePath, requiredCapabilities: new[] { "guitar" });
        Assert.False(result.IsAllowed);
        Assert.Equal(VerificationFailureReason.MissingCapability, result.Reason);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort en limpieza de temporales de prueba
        }
    }
}
