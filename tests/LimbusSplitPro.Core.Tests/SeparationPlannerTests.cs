using LimbusSplitPro.Core.Models;
using Xunit;

namespace LimbusSplitPro.Core.Tests;

public class SeparationPlannerTests
{
    [Fact]
    public void SelectingOnlyVoices_FoldsDrumsBassOtherIntoOther()
    {
        var plan = SeparationPlanner.Plan(new SeparationRequest(new HashSet<StemCategory> { StemCategory.VocesTodas }));

        Assert.Equal("htdemucs", plan.ModelId);
        Assert.Equal(new[] { "vocals" }, plan.NativeSourcesToKeepDirectly);
        Assert.Contains("drums", plan.NativeSourcesToFoldIntoOther);
        Assert.Contains("bass", plan.NativeSourcesToFoldIntoOther);
        Assert.Contains("other", plan.NativeSourcesToFoldIntoOther);
    }

    [Fact]
    public void SelectingDrumsAndBass_KeepsBothDirectlyAndFoldsRestIntoOther()
    {
        var plan = SeparationPlanner.Plan(new SeparationRequest(
            new HashSet<StemCategory> { StemCategory.BateriaCompleta, StemCategory.Bajo }));

        Assert.Equal(2, plan.NativeSourcesToKeepDirectly.Count);
        Assert.Contains("drums", plan.NativeSourcesToKeepDirectly);
        Assert.Contains("bass", plan.NativeSourcesToKeepDirectly);
        Assert.DoesNotContain("drums", plan.NativeSourcesToFoldIntoOther);
        Assert.DoesNotContain("bass", plan.NativeSourcesToFoldIntoOther);
        Assert.Contains("vocals", plan.NativeSourcesToFoldIntoOther);
    }

    [Fact]
    public void SelectingGuitar_SwitchesToSixSourceModel()
    {
        var plan = SeparationPlanner.Plan(new SeparationRequest(new HashSet<StemCategory> { StemCategory.Guitarra }));

        Assert.Equal("htdemucs_6s", plan.ModelId);
        Assert.Contains("guitar", plan.NativeSourcesToKeepDirectly);
        Assert.Contains("piano", plan.NativeSourcesToFoldIntoOther);
    }

    [Fact]
    public void RequestingUnavailableCategory_Throws()
    {
        // VozPrincipal está marcada IsAvailable=false en el catálogo
        // (ver StemCategoryCatalog) — el planner debe rechazarla como
        // defensa en profundidad aunque la UI no debería permitirlo.
        Assert.Throws<InvalidOperationException>(() =>
            SeparationPlanner.Plan(new SeparationRequest(new HashSet<StemCategory> { StemCategory.VozPrincipal })));
    }

    [Fact]
    public void AllCategoriesInCatalog_HaveAUniqueDisplayName()
    {
        var names = StemCategoryCatalog.All.Select(c => c.DisplayName).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
