using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using Radoub.UI.Services;
using Radoub.UI.Views;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Headless smoke tests for the shared SaveBlueprintWindow (#2515). Verifies the
/// window constructs with single- and multi-extension options and that Result is
/// null before Save is confirmed. Logic coverage lives in
/// SaveBlueprintPathResolverTests.
/// </summary>
public class SaveBlueprintWindowTests
{
    [AvaloniaFact]
    public void Constructs_SingleExtension_ResultNullBeforeConfirm()
    {
        var options = new SaveBlueprintOptions("Save Store", new[] { "utm" }, "general_store", null);
        var window = new SaveBlueprintWindow(options);

        Assert.Equal("Save Store", window.Title);
        Assert.Null(window.Result);
    }

    [AvaloniaFact]
    public void Constructs_MultiExtension_ResultNullBeforeConfirm()
    {
        var options = new SaveBlueprintOptions("Save Creature", new[] { "utc", "bic" }, "npc_guard", null);
        var window = new SaveBlueprintWindow(options);

        Assert.Equal("Save Creature", window.Title);
        Assert.Null(window.Result);
    }
}
