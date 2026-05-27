using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for MainWindowViewModel.BuildAdditionalInfo — the About dialog body text.
/// Covers the regression in #2247 where the body was a hand-maintained string literal
/// that listed only Parley / Manifest / Quartermaster / Fence and omitted Relique.
/// </summary>
public class AboutAdditionalInfoTests
{
    private static ToolInfo MakeTool(string name, string description) => new()
    {
        Name = name,
        Description = description,
        FileTypes = ".xxx",
        Maturity = ToolMaturity.Alpha,
    };

    [Fact]
    public void BuildAdditionalInfo_IncludesEveryProvidedTool()
    {
        var tools = new[]
        {
            MakeTool("Parley", "Dialog Editor"),
            MakeTool("Manifest", "Journal Editor"),
            MakeTool("Quartermaster", "Creature Editor"),
            MakeTool("Fence", "Merchant Editor"),
            MakeTool("Relique", "Item Editor"),
        };

        var body = MainWindowViewModel.BuildAdditionalInfo(tools);

        Assert.Contains("Parley - Dialog Editor", body);
        Assert.Contains("Manifest - Journal Editor", body);
        Assert.Contains("Quartermaster - Creature Editor", body);
        Assert.Contains("Fence - Merchant Editor", body);
        Assert.Contains("Relique - Item Editor", body);
    }

    [Fact]
    public void BuildAdditionalInfo_AutoIncludesNewTools()
    {
        // Regression: #2247 - hardcoded literal didn't pick up new tools.
        // A future tool registered with ToolLauncherService should appear without code edits.
        var tools = new[]
        {
            MakeTool("Parley", "Dialog Editor"),
            MakeTool("FutureTool", "Future Editor"),
        };

        var body = MainWindowViewModel.BuildAdditionalInfo(tools);

        Assert.Contains("FutureTool - Future Editor", body);
    }

    [Fact]
    public void BuildAdditionalInfo_RetainsThirdPartyAttribution()
    {
        var tools = new[] { MakeTool("Parley", "Dialog Editor") };

        var body = MainWindowViewModel.BuildAdditionalInfo(tools);

        Assert.Contains("nwn_script_comp", body);
        Assert.Contains("Bernhard Stöckner", body);
    }
}
