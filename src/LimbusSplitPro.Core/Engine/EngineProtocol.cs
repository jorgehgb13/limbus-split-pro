using System.Text.Json.Serialization;

namespace LimbusSplitPro.Core.Engine;

/// <summary>
/// Un evento crudo recibido del motor por stdout, una línea JSON = un
/// evento. El campo "type" decide cómo se interpreta el resto.
/// Debe mantenerse en sincronía con engine/limbus_engine/protocol.py.
/// </summary>
public sealed class EngineEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "progress" | "stage" | "error" | "done" | "log"

    [JsonPropertyName("stage")]
    public string? Stage { get; init; }

    [JsonPropertyName("pct")]
    public double? Pct { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; } // ver EngineErrorCode

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("device")]
    public string? Device { get; init; } // "cpu" | "cuda:0" | ...

    [JsonPropertyName("stems")]
    public List<EngineStemResult>? Stems { get; init; }
}

public sealed class EngineStemResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("sample_rate")]
    public required int SampleRate { get; init; }

    [JsonPropertyName("channels")]
    public required int Channels { get; init; }

    [JsonPropertyName("duration_seconds")]
    public required double DurationSeconds { get; init; }
}

/// <summary>
/// Comando enviado al motor por stdin, una línea JSON. Ver
/// engine/limbus_engine/protocol.py para el lado que lo consume.
/// </summary>
public sealed class EngineCommand
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "separate" | "cancel"

    [JsonPropertyName("input_path")]
    public string? InputPath { get; init; }

    [JsonPropertyName("output_dir")]
    public string? OutputDir { get; init; }

    [JsonPropertyName("model_id")]
    public string? ModelId { get; init; }

    [JsonPropertyName("model_path")]
    public string? ModelPath { get; init; }

    [JsonPropertyName("native_sources_to_export")]
    public List<string>? NativeSourcesToExport { get; init; }

    [JsonPropertyName("fold_into_other")]
    public List<string>? FoldIntoOther { get; init; }

    [JsonPropertyName("device_preference")]
    public string? DevicePreference { get; init; } // "auto" | "cpu" | "gpu"
}

/// <summary>Códigos de error estables que la UI puede mapear a mensajes claros (sección 22).</summary>
public static class EngineErrorCode
{
    public const string ModelMissing = "MODEL_MISSING";
    public const string HashMismatch = "HASH_MISMATCH";
    public const string UnsupportedFormat = "UNSUPPORTED_FORMAT";
    public const string OutOfMemory = "OUT_OF_MEMORY";
    public const string GpuIncompatible = "GPU_INCOMPATIBLE";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string FileLocked = "FILE_LOCKED";
    public const string NetworkPathUnavailable = "NETWORK_PATH_UNAVAILABLE";
    public const string AudioDeviceUnavailable = "AUDIO_DEVICE_UNAVAILABLE";
    public const string CancelledByUser = "CANCELLED_BY_USER";
    public const string Unknown = "UNKNOWN";
}
