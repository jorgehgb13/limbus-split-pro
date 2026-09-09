namespace LimbusSplitPro.Core.Models;

/// <summary>
/// Categorías de pista que el usuario puede pedir. Las que no tienen un
/// modelo local verificado con licencia clara quedan marcadas
/// <see cref="StemCategoryInfo.IsAvailable"/> = false y NUNCA generan un
/// archivo (ver docs/MODELS.md, sección "Categorías... DESHABILITADAS").
/// </summary>
public enum StemCategory
{
    VocesTodas,
    VozPrincipal,
    CorosSegundasVoces,
    EfectosVocales,
    RuidoArtefactos,
    BateriaCompleta,
    Bombo,
    Caja,
    Toms,
    Platos,
    Bajo,
    Guitarra,
    GuitarraAcustica,
    GuitarraElectrica,
    PianoTeclados,
    Other,
}

public sealed record StemCategoryInfo(
    StemCategory Category,
    string DisplayName,
    bool IsAvailable,
    string? UnavailableReason,
    string? RequiredModelId);

/// <summary>
/// Catálogo estático de qué puede ofrecer honestamente este v1.
/// Cambiar esto SOLO cuando haya un modelo real, investigado y con
/// licencia verificada respaldándolo (ver docs/MODELS.md).
/// </summary>
public static class StemCategoryCatalog
{
    private const string NoModel =
        "No existe todavía, en este build, un modelo local con licencia de " +
        "pesos verificada que separe esta categoría de forma confiable. " +
        "Ver docs/MODELS.md para el detalle de la investigación.";

    private const string Htdemucs6sUnavailable =
        "htdemucs_6s (el modelo que separaría esto) no se pudo resolver de forma confiable " +
        "durante la compilación: no tiene un repositorio oficial en Hugging Face Hub, y el " +
        "repositorio legado de Meta tampoco respondió. Además, el propio demucs lo marca " +
        "como modelo experimental, con problemas de calidad conocidos en el piano. " +
        "Ver docs/MODELS.md.";

    public static readonly IReadOnlyList<StemCategoryInfo> All = new List<StemCategoryInfo>
    {
        new(StemCategory.VocesTodas, "Voces", true, null, "htdemucs"),
        new(StemCategory.VozPrincipal, "Voz principal", false, NoModel, null),
        new(StemCategory.CorosSegundasVoces, "Coros y segundas voces", false, NoModel, null),
        new(StemCategory.EfectosVocales, "Efectos vocales / reverberación", false, NoModel, null),
        new(StemCategory.RuidoArtefactos, "Ruido o artefactos", false, NoModel, null),
        new(StemCategory.BateriaCompleta, "Batería completa", true, null, "htdemucs"),
        new(StemCategory.Bombo, "Bombo", false, NoModel, null),
        new(StemCategory.Caja, "Caja", false, NoModel, null),
        new(StemCategory.Toms, "Toms", false, NoModel, null),
        new(StemCategory.Platos, "Platos", false, NoModel, null),
        new(StemCategory.Bajo, "Bajo", true, null, "htdemucs"),
        new(StemCategory.Guitarra, "Guitarra", false, Htdemucs6sUnavailable, null),
        new(StemCategory.GuitarraAcustica, "Guitarra acústica", false,
            "htdemucs_6s solo entrega una pista de guitarra combinada; no distingue acústica de eléctrica.", null),
        new(StemCategory.GuitarraElectrica, "Guitarra eléctrica", false,
            "htdemucs_6s solo entrega una pista de guitarra combinada; no distingue acústica de eléctrica.", null),
        new(StemCategory.PianoTeclados, "Piano y teclados", false, Htdemucs6sUnavailable, null),
        new(StemCategory.Other, "Other (resto)", true, null, null),
    };

    public static StemCategoryInfo Get(StemCategory category) =>
        All.First(c => c.Category == category);
}
