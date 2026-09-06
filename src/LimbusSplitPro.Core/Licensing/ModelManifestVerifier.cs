using System.Security.Cryptography;
using System.Text.Json;

namespace LimbusSplitPro.Core.Licensing;

public enum VerificationFailureReason
{
    NotRegistered,
    FileMissing,
    SizeMismatch,
    HashMismatch,
    MissingCapability,
    RedistributionNotAuthorized,
    CommercialUseNotAuthorized,
}

public sealed record VerificationResult(bool IsAllowed, VerificationFailureReason? Reason, string? Detail)
{
    public static VerificationResult Allowed() => new(true, null, null);
    public static VerificationResult Denied(VerificationFailureReason reason, string detail) =>
        new(false, reason, detail);
}

/// <summary>
/// Verificador "fail-closed": si algo no se puede confirmar con certeza,
/// la respuesta es NO, no "probablemente sí". Ningún modelo se usa en un
/// build público (<paramref name="requireRedistributionAuthorized"/> = true)
/// sin que el manifiesto declare explícitamente
/// <c>redistribution_authorized: true</c>. Para uso puramente personal en la
/// propia máquina del usuario (build de desarrollo), ese requisito se puede
/// relajar explícitamente porque no hay redistribución involucrada — pero
/// nunca de forma silenciosa: el llamador debe pedirlo a propósito.
/// </summary>
public sealed class ModelManifestVerifier
{
    private readonly ModelManifestDocument _manifest;

    public ModelManifestVerifier(ModelManifestDocument manifest)
    {
        _manifest = manifest;
    }

    public static ModelManifestVerifier LoadFrom(string manifestJsonPath)
    {
        var json = File.ReadAllText(manifestJsonPath);
        var doc = JsonSerializer.Deserialize<ModelManifestDocument>(json)
            ?? throw new InvalidDataException("El manifiesto de modelos está vacío o mal formado.");
        return new ModelManifestVerifier(doc);
    }

    public ModelEntry? Find(string modelId) => _manifest.Models.FirstOrDefault(m => m.Id == modelId);

    /// <summary>
    /// Verifica un archivo de modelo ya descargado en disco contra el
    /// manifiesto: existencia, tamaño exacto, hash SHA-256, capacidades
    /// requeridas y (opcionalmente, para builds públicos) autorización de
    /// redistribución/uso comercial.
    /// </summary>
    public VerificationResult Verify(
        string modelId,
        string localFilePath,
        IReadOnlyCollection<string>? requiredCapabilities = null,
        bool requireRedistributionAuthorized = false,
        bool requireCommercialUseAuthorized = false)
    {
        var entry = Find(modelId);
        if (entry is null)
        {
            return VerificationResult.Denied(
                VerificationFailureReason.NotRegistered,
                $"El modelo '{modelId}' no está registrado en el manifiesto. No se usará.");
        }

        if (!File.Exists(localFilePath))
        {
            return VerificationResult.Denied(
                VerificationFailureReason.FileMissing,
                $"No se encontró el archivo esperado en '{localFilePath}'.");
        }

        var actualSize = new FileInfo(localFilePath).Length;
        if (actualSize != entry.ExpectedSizeBytes)
        {
            return VerificationResult.Denied(
                VerificationFailureReason.SizeMismatch,
                $"Tamaño esperado {entry.ExpectedSizeBytes} bytes, encontrado {actualSize} bytes.");
        }

        var actualHash = ComputeSha256(localFilePath);
        if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return VerificationResult.Denied(
                VerificationFailureReason.HashMismatch,
                $"Hash SHA-256 no coincide. Esperado {entry.Sha256}, calculado {actualHash}. " +
                "El archivo pudo corromperse o ser alterado; no se usará.");
        }

        if (requiredCapabilities is { Count: > 0 })
        {
            var missing = requiredCapabilities.Where(c => !entry.Capabilities.Contains(c)).ToList();
            if (missing.Count > 0)
            {
                return VerificationResult.Denied(
                    VerificationFailureReason.MissingCapability,
                    $"El modelo no declara las capacidades requeridas: {string.Join(", ", missing)}.");
            }
        }

        if (requireRedistributionAuthorized && !entry.RedistributionAuthorized)
        {
            return VerificationResult.Denied(
                VerificationFailureReason.RedistributionNotAuthorized,
                $"El modelo '{modelId}' no tiene autorización de redistribución confirmada " +
                "(ver docs/MODELS.md). Bloqueado para build público.");
        }

        if (requireCommercialUseAuthorized && !entry.CommercialUseAuthorized)
        {
            return VerificationResult.Denied(
                VerificationFailureReason.CommercialUseNotAuthorized,
                $"El modelo '{modelId}' no tiene autorización de uso comercial confirmada.");
        }

        return VerificationResult.Allowed();
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
