using System.IO;
using System.Linq;
using Radoub.Formats.Itp;
using Radoub.Formats.Uti;
using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

public class PaletteEditorLoaderTests : System.IDisposable
{
    private readonly string _dir;
    public PaletteEditorLoaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "pel_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
    }
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteItp(string name, ItpFile itp) => ItpWriter.Write(itp, Path.Combine(_dir, name));
    private void WriteUti(string resRef, byte id) =>
        UtiWriter.Write(new UtiFile { TemplateResRef = resRef, PaletteID = id }, Path.Combine(_dir, resRef + ".uti"));

    [Fact]
    public void Load_reads_custom_palette_and_pools_loose_blueprints()
    {
        var itp = new ItpFile();
        itp.MainNodes.Add(new PaletteCategoryNode { Id = 1, Name = "Weapons" });
        WriteItp("itempalcus.itp", itp);
        WriteUti("acid_dagger", 1);
        WriteUti("mystery_ring", 99); // points at a nonexistent category -> uncategorized

        var ctx = new PaletteEditorLoader().Load(_dir, PaletteResourceType.Item);

        Assert.Single(ctx.Palette.GetCategories());
        Assert.Contains("acid_dagger", ctx.Store.ResRefs);
        Assert.Contains("mystery_ring", ctx.Store.ResRefs);
        Assert.Equal((byte)1, ctx.Store.GetPaletteId("acid_dagger"));
    }

    [Fact]
    public void Load_missing_custom_palette_yields_empty_tree_not_null()
    {
        WriteUti("lonely", 0); // no itempalcus.itp present
        var ctx = new PaletteEditorLoader().Load(_dir, PaletteResourceType.Item);
        Assert.NotNull(ctx.Palette);
        Assert.Empty(ctx.Palette.MainNodes);
        Assert.Contains("lonely", ctx.Store.ResRefs);
    }

    [Fact]
    public void Load_seeds_NextUseableId_from_skeleton_when_custom_omits_it()
    {
        WriteItp("itempalcus.itp", new ItpFile()); // custom lacks NextUseableId
        WriteItp("itempalstd.itp", new ItpFile { NextUseableId = 42 });

        var ctx = new PaletteEditorLoader().Load(_dir, PaletteResourceType.Item);
        Assert.Equal((byte)42, ctx.Palette.NextUseableId);
    }

    [Fact]
    public void Load_keeps_custom_NextUseableId_when_present()
    {
        WriteItp("itempalcus.itp", new ItpFile { NextUseableId = 7 });
        WriteItp("itempalstd.itp", new ItpFile { NextUseableId = 42 });

        var ctx = new PaletteEditorLoader().Load(_dir, PaletteResourceType.Item);
        Assert.Equal((byte)7, ctx.Palette.NextUseableId);
    }

    [Fact]
    public void Load_normalizes_mixed_case_blueprint_resref_to_lowercase()
    {
        // A real module may ship ACID_DAGGER.UTI; the pooled ResRef must match the lowercase
        // text the .itp tree uses, so the store keys and drift classification line up.
        UtiWriter.Write(new UtiFile { TemplateResRef = "ACID_DAGGER", PaletteID = 1 },
            Path.Combine(_dir, "ACID_DAGGER.UTI"));

        var ctx = new PaletteEditorLoader().Load(_dir, PaletteResourceType.Item);
        Assert.Contains("acid_dagger", ctx.Store.ResRefs);
    }
}
