namespace LimbusSplitPro.Core.Models;

/// <summary>
/// Lo que el usuario pidió: un conjunto de categorías, todas ya validadas
/// como disponibles (ver <see cref="StemCategoryCatalog"/>) antes de llegar
/// aquí — la UI nunca debería dejar seleccionar una deshabilitada, pero el
/// planner vuelve a validarlo por si acaso (defensa en profundidad).
/// </summary>
public sealed record SeparationRequest(IReadOnlySet<StemCategory> RequestedCategories);

/// <summary>
/// Fuentes nativas que un modelo concreto puede producir en una sola
/// pasada (p. ej. htdemucs: vocals, drums, bass, other).
/// </summary>
public sealed record ModelNativeSources(string ModelId, IReadOnlyList<string> Sources);

/// <summary>
/// Resultado de planear qué modelo(s) correr y cómo construir "Other".
/// </summary>
public sealed record SeparationPlan(
    string ModelId,
    IReadOnlyDictionary<StemCategory, string> CategoryToNativeSource,
    IReadOnlyList<string> NativeSourcesToKeepDirectly,
    IReadOnlyList<string> NativeSourcesToFoldIntoOther,
    bool WillProduceOtherFile);

public static class SeparationPlanner
{
    private static readonly Dictionary<StemCategory, string> HtdemucsMap = new()
    {
        [StemCategory.VocesTodas] = "vocals",
        [StemCategory.BateriaCompleta] = "drums",
        [StemCategory.Bajo] = "bass",
    };

    private static readonly Dictionary<StemCategory, string> Htdemucs6sMap = new(HtdemucsMap)
    {
        [StemCategory.Guitarra] = "guitar",
        [StemCategory.PianoTeclados] = "piano",
    };

    /// <summary>
    /// Decide qué modelo usar (htdemucs vs htdemucs_6s) y cómo repartir las
    /// fuentes nativas entre "pistas pedidas" y "fold into Other", siguiendo
    /// al pie de la letra la sección 6 del prompt original: Other = todo lo
    /// que el usuario NO pidió, nunca un archivo vacío inventado.
    /// </summary>
    public static SeparationPlan Plan(SeparationRequest request)
    {
        var requested = request.RequestedCategories
            .Where(c => c != StemCategory.Other)
            .ToList();

        foreach (var c in requested)
        {
            var info = StemCategoryCatalog.Get(c);
            if (!info.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"La categoría '{info.DisplayName}' no está disponible en este build " +
                    $"({info.UnavailableReason}). La UI no debería haber permitido seleccionarla.");
            }
        }

        var needsGuitarOrPiano = requested.Contains(StemCategory.Guitarra) ||
                                  requested.Contains(StemCategory.PianoTeclados);

        var modelId = needsGuitarOrPiano ? "htdemucs_6s" : "htdemucs";
        var map = needsGuitarOrPiano ? Htdemucs6sMap : HtdemucsMap;
        var allNativeSources = needsGuitarOrPiano
            ? new[] { "vocals", "drums", "bass", "guitar", "piano", "other" }
            : new[] { "vocals", "drums", "bass", "other" };

        var categoryToNativeSource = requested.ToDictionary(c => c, c => map[c]);
        var keepDirectly = categoryToNativeSource.Values.Distinct().ToList();

        // "other" nativo del propio modelo SIEMPRE se pliega en el Other
        // final que ve el usuario (no es una pista seleccionable aparte).
        var foldIntoOther = allNativeSources
            .Where(s => s != "other" && !keepDirectly.Contains(s))
            .Append("other")
            .ToList();

        // Si el usuario pidió absolutamente todas las fuentes nativas
        // disponibles, Other solo se genera si queda material residual real
        // (el "other" propio del modelo) — nunca un archivo de puro silencio
        // fabricado. Esa comprobación de "silencio real" ocurre en tiempo de
        // ejecución sobre el audio decodificado (ver engine/separation.py,
        // función `has_meaningful_signal`), no aquí en el planner.
        var willProduceOther = true;

        return new SeparationPlan(
            modelId,
            categoryToNativeSource,
            keepDirectly,
            foldIntoOther,
            willProduceOther);
    }
}
