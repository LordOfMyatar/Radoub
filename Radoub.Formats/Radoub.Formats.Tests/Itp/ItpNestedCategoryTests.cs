using Radoub.Formats.Gff;
using Radoub.Formats.Itp;
using static Radoub.Formats.Gff.GffFieldBuilder;
using Xunit;

namespace Radoub.Formats.Tests.Itp;

public class ItpNestedCategoryTests
{
    // Builds: MAIN -> [branch "Faction Specific" -> (category KAF id20 -> (category Armor id21 -> blueprint kaf_armor001))]
    private static byte[] BuildDeepNestedItp()
    {
        var armor = new GffStruct { Type = 0 };
        AddDwordField(armor, "STRREF", 21);
        AddByteField(armor, "ID", 21);
        var armorChildren = new GffList();
        var bp = new GffStruct { Type = 0 };
        AddCExoStringField(bp, "NAME", "KAF Armor 001");
        AddCExoStringField(bp, "RESREF", "kaf_armor001");
        armorChildren.Elements.Add(bp);
        AddListField(armor, "LIST", armorChildren);

        var kaf = new GffStruct { Type = 0 };
        AddDwordField(kaf, "STRREF", 20);
        AddByteField(kaf, "ID", 20);
        var kafChildren = new GffList();
        kafChildren.Elements.Add(armor);
        AddListField(kaf, "LIST", kafChildren);

        var branch = new GffStruct { Type = 0 };
        AddCExoStringField(branch, "NAME", "Faction Specific");
        var branchChildren = new GffList();
        branchChildren.Elements.Add(kaf);
        AddListField(branch, "LIST", branchChildren);

        var main = new GffList();
        main.Elements.Add(branch);
        var root = new GffStruct { Type = 0xFFFFFFFF };
        AddListField(root, "MAIN", main);

        var gff = new GffFile { FileType = "ITP ", FileVersion = "V3.2", RootStruct = root };
        return GffWriter.Write(gff);
    }

    [Fact]
    public void Read_CategoryNestedUnderCategory_IsPreserved()
    {
        var bytes = BuildDeepNestedItp();

        var itp = ItpReader.Read(bytes);

        Assert.NotNull(itp);
        var allCategories = itp!.GetCategories().ToList();
        Assert.Contains(allCategories, c => c.Id == 20);
        Assert.Contains(allCategories, c => c.Id == 21); // the deep one #2280 dropped

        var kaf = allCategories.First(c => c.Id == 20);
        Assert.Contains(kaf.ChildCategories, c => c.Id == 21);

        var armor = allCategories.First(c => c.Id == 21);
        Assert.Contains(armor.Blueprints, b => b.ResRef == "kaf_armor001");
    }
}
