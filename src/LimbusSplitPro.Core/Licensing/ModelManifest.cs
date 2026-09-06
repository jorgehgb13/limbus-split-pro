using System.Text.Json.Serialization;

namespace LimbusSplitPro.Core.Licensing;

/// <summary>
/// Espejo en C# de models/manifest.schema.json. Un modelo que no cumpla
/// todos los campos requeridos, o cuyo hash no coincida, es rechazado por
/// <see cref="ModelManifestVerifier"/> — nunca se "asume" nada a favor del
/// modelo (fail-closed, sección 7 del prompt original).
/// </summary>
public sealed class ModelEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("display_name")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("sources")]
    public required List<string> Sources { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("origin_url")]
    public required string OriginUrl { get; init; }

    [JsonPropertyName("download_url")]
    public required string DownloadUrl { get; init; }

    [JsonPropertyName("relative_path")]
    public required string RelativePath { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }

    [JsonPropertyName("expected_size_bytes")]
    public required long ExpectedSizeBytes { get; init; }

    [JsonPropertyName("code_license")]
    public required string CodeLicense { get; init; }

    [JsonPropertyName("weights_license_status")]
    public required string WeightsLicenseStatus { get; init; }

    [JsonPropertyName("weights_license_evidence_url")]
    public required string WeightsLicenseEvidenceUrl { get; init; }

    [JsonPropertyName("author")]
    public required string Author { get; init; }

    [JsonPropertyName("attribution")]
    public required string Attribution { get; init; }

    [JsonPropertyName("capabilities")]
    public required List<string> Capabilities { get; init; }

    [JsonPropertyName("redistribution_authorized")]
    public required bool RedistributionAuthorized { get; init; }

    [JsonPropertyName("commercial_use_authorized")]
    public required bool CommercialUseAuthorized { get; init; }

    [JsonPropertyName("distribution_mode")]
    public required string DistributionMode { get; init; } // "bundled" | "download_on_demand"
}

public sealed class ModelManifestDocument
{
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("models")]
    public required List<ModelEntry> Models { get; init; }
}
