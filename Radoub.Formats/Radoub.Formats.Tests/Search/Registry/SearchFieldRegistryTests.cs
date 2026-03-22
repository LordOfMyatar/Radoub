using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Registry;

public class SearchFieldRegistryTests
{
    private static FieldDefinition MakeField(
        string name, SearchFieldType type = SearchFieldType.Text,
        SearchFieldCategory category = SearchFieldCategory.Content,
        bool replaceable = true) =>
        new() { Name = name, GffPath = name, FieldType = type, Category = category, IsReplaceable = replaceable };

    [Fact]
    public void GetSearchableFields_UnregisteredType_ReturnsEmpty()
    {
        var registry = new SearchFieldRegistry();
        var fields = registry.GetSearchableFields(9999);
        Assert.Empty(fields);
    }

    [Fact]
    public void RegisterFileType_AddsFields()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg, MakeField("Text"), MakeField("Speaker"));
        var fields = registry.GetSearchableFields(ResourceTypes.Dlg);
        Assert.Equal(2, fields.Count);
    }

    [Fact]
    public void RegisterFileType_MultipleCalls_Accumulates()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg, MakeField("Text"));
        registry.RegisterFileType(ResourceTypes.Dlg, MakeField("Speaker"));
        var fields = registry.GetSearchableFields(ResourceTypes.Dlg);
        Assert.Equal(2, fields.Count);
    }

    [Fact]
    public void GetFieldsByCategory_FiltersCorrectly()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg,
            MakeField("Text", category: SearchFieldCategory.Content),
            MakeField("Script", category: SearchFieldCategory.Script));
        var scripts = registry.GetFieldsByCategory(ResourceTypes.Dlg, SearchFieldCategory.Script);
        Assert.Single(scripts);
        Assert.Equal("Script", scripts[0].Name);
    }

    [Fact]
    public void GetAllFileTypes_ReturnsRegistered()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg, MakeField("Text"));
        registry.RegisterFileType(ResourceTypes.Utc, MakeField("Name"));
        var types = registry.GetAllFileTypes();
        Assert.Contains(ResourceTypes.Dlg, types);
        Assert.Contains(ResourceTypes.Utc, types);
    }

    [Fact]
    public void IsReplaceable_RespectsFieldSetting()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg,
            MakeField("Text", replaceable: true),
            MakeField("ReadOnly", replaceable: false));
        Assert.True(registry.IsReplaceable(ResourceTypes.Dlg, "Text"));
        Assert.False(registry.IsReplaceable(ResourceTypes.Dlg, "ReadOnly"));
    }

    [Fact]
    public void IsReplaceable_UnknownField_ReturnsFalse()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg, MakeField("Text"));
        Assert.False(registry.IsReplaceable(ResourceTypes.Dlg, "NonExistent"));
    }

    [Fact]
    public void IsRegistered_RegisteredType_ReturnsTrue()
    {
        var registry = new SearchFieldRegistry();
        registry.RegisterFileType(ResourceTypes.Dlg, MakeField("Text"));
        Assert.True(registry.IsRegistered(ResourceTypes.Dlg));
    }

    [Fact]
    public void IsRegistered_UnregisteredType_ReturnsFalse()
    {
        var registry = new SearchFieldRegistry();
        Assert.False(registry.IsRegistered(9999));
    }
}
