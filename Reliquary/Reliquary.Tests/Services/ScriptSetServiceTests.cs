using System.Collections.Generic;
using System.Linq;
using PlaceableEditor.Services;
using PlaceableEditor.ViewModels;
using Radoub.Formats.Utp;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// Round-trip + apply behavior for the script-set preset (#2369): the 13 event slots
/// serialize to JSON and reload onto another placeable's slots by stable EventName.
/// </summary>
public class ScriptSetServiceTests
{
    private static PlaceableViewModel MakePlaceable() => new(new UtpFile());

    private static void SetSlot(PlaceableViewModel vm, string eventName, string resRef)
        => vm.Scripts.First(s => s.EventName == eventName).ResRef = resRef;

    [Fact]
    public void Serialize_OnlyIncludesAssignedSlots()
    {
        var vm = MakePlaceable();
        SetSlot(vm, "OnOpen", "chest_open");
        SetSlot(vm, "OnClosed", "chest_close");

        var map = ScriptSetService.Parse(ScriptSetService.Serialize(vm.Scripts));

        Assert.Equal(2, map.Count);
        Assert.Equal("chest_open", map["OnOpen"]);
        Assert.Equal("chest_close", map["OnClosed"]);
    }

    [Fact]
    public void RoundTrip_AppliesScriptsToAnotherPlaceable()
    {
        var source = MakePlaceable();
        SetSlot(source, "OnOpen", "door_open");
        SetSlot(source, "OnUsed", "door_used");

        var bytes = ScriptSetService.Serialize(source.Scripts);
        var map = ScriptSetService.Parse(bytes);

        var target = MakePlaceable();
        var applied = ScriptSetService.Apply(target.Scripts, map);

        Assert.Equal(2, applied);
        Assert.Equal("door_open", target.Scripts.First(s => s.EventName == "OnOpen").ResRef);
        Assert.Equal("door_used", target.Scripts.First(s => s.EventName == "OnUsed").ResRef);
    }

    [Fact]
    public void Serialize_WritesPlainTextKeyValueLines()
    {
        var vm = MakePlaceable();
        SetSlot(vm, "OnOpen", "chest_open");

        var text = System.Text.Encoding.UTF8.GetString(ScriptSetService.Serialize(vm.Scripts));

        Assert.Contains("OnOpen=chest_open", text);
        Assert.DoesNotContain("{", text); // not JSON
    }

    [Fact]
    public void Parse_ToleratesBlankLinesAndComments()
    {
        var text = "# placeable script set\n\nOnOpen=chest_open\n  \nOnClosed = chest_close \n";
        var map = ScriptSetService.Parse(System.Text.Encoding.UTF8.GetBytes(text));

        Assert.Equal(2, map.Count);
        Assert.Equal("chest_open", map["OnOpen"]);
        Assert.Equal("chest_close", map["OnClosed"]); // trims whitespace around key + value
    }

    [Fact]
    public void Apply_IgnoresUnknownEventNames()
    {
        var target = MakePlaceable();
        var map = new Dictionary<string, string> { ["NotAnEvent"] = "x", ["OnOpen"] = "ok" };

        var applied = ScriptSetService.Apply(target.Scripts, map);

        Assert.Equal(1, applied); // only OnOpen matched a real slot
        Assert.Equal("ok", target.Scripts.First(s => s.EventName == "OnOpen").ResRef);
    }

    [Fact]
    public void Apply_ClearsSlotsAbsentFromPreset()
    {
        var target = MakePlaceable();
        SetSlot(target, "OnDeath", "leftover");
        var map = new Dictionary<string, string> { ["OnOpen"] = "fresh" };

        ScriptSetService.Apply(target.Scripts, map);

        // A preset is the full picture of the script set — slots it omits are cleared.
        Assert.Equal("fresh", target.Scripts.First(s => s.EventName == "OnOpen").ResRef);
        Assert.Equal(string.Empty, target.Scripts.First(s => s.EventName == "OnDeath").ResRef);
    }
}
