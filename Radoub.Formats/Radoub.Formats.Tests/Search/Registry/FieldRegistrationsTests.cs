using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Registry;

public class FieldRegistrationsTests
{
    [Fact]
    public void RegisterAll_RegistersDlgFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var fields = registry.GetSearchableFields(ResourceTypes.Dlg);
        Assert.NotEmpty(fields);
        Assert.Contains(fields, f => f.Name == "Text" && f.FieldType == SearchFieldType.LocString);
        Assert.Contains(fields, f => f.Name == "Speaker");
        Assert.Contains(fields, f => f.Name == "Action Script" && f.FieldType == SearchFieldType.Script);
        Assert.Contains(fields, f => f.Name == "Condition Script");
        Assert.Contains(fields, f => f.Name == "Comment");
    }

    [Fact]
    public void RegisterAll_RegistersUtcFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var fields = registry.GetSearchableFields(ResourceTypes.Utc);
        Assert.NotEmpty(fields);
        Assert.Contains(fields, f => f.Name == "First Name" && f.FieldType == SearchFieldType.LocString);
        Assert.Contains(fields, f => f.Name == "Tag" && f.FieldType == SearchFieldType.Tag);
        Assert.Contains(fields, f => f.Name == "Local Variables" && f.FieldType == SearchFieldType.Variable);
    }

    [Fact]
    public void RegisterAll_BicSharesUtcFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var utcFields = registry.GetSearchableFields(ResourceTypes.Utc);
        var bicFields = registry.GetSearchableFields(ResourceTypes.Bic);
        Assert.Equal(utcFields.Count, bicFields.Count);
    }

    [Fact]
    public void RegisterAll_RegistersUtiFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var fields = registry.GetSearchableFields(ResourceTypes.Uti);
        Assert.NotEmpty(fields);
        Assert.Contains(fields, f => f.Name == "Name" && f.FieldType == SearchFieldType.LocString);
        Assert.Contains(fields, f => f.Name == "Tag");
    }

    [Fact]
    public void RegisterAll_RegistersUtmFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var fields = registry.GetSearchableFields(ResourceTypes.Utm);
        Assert.NotEmpty(fields);
        Assert.Contains(fields, f => f.Name == "Name" && f.FieldType == SearchFieldType.LocString);
        Assert.Contains(fields, f => f.Name == "OnOpenStore" && f.FieldType == SearchFieldType.Script);
    }

    [Fact]
    public void RegisterAll_RegistersJrlFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var fields = registry.GetSearchableFields(ResourceTypes.Jrl);
        Assert.NotEmpty(fields);
        Assert.Contains(fields, f => f.Name == "Category Name");
        Assert.Contains(fields, f => f.Name == "Entry Text");
        Assert.Contains(fields, f => f.Name == "Category Tag" && f.FieldType == SearchFieldType.Tag);
    }

    [Fact]
    public void RegisterAll_RegistersIfoFields()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        var fields = registry.GetSearchableFields(ResourceTypes.Ifo);
        Assert.NotEmpty(fields);
        Assert.Contains(fields, f => f.Name == "Module Name");
    }

    [Fact]
    public void RegisterAll_AllFieldsHaveDescriptions()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        foreach (var type in registry.GetAllFileTypes())
        {
            foreach (var field in registry.GetSearchableFields(type))
            {
                Assert.False(string.IsNullOrEmpty(field.Description),
                    $"Field {field.Name} on type {type} has no description");
            }
        }
    }
}
