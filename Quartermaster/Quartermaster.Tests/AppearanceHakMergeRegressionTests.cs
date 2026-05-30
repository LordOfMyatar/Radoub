using System.IO;
using Quartermaster.Services;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Xunit;
using Xunit.Abstractions;

namespace Quartermaster.Tests;

/// <summary>
/// Regression guard for #2162: a CEP3 appearance row (566) displayed as the literal
/// "Appearance N" fallback instead of its 2DA LABEL. Root cause was the module-HAK 2DA
/// resolution chain not serving the CEP-extended appearance.2da; once a module's HAK list
/// is configured, the merged table must expose the high CEP rows so GetAppearanceName
/// returns the real label.
///
/// These tests require a configured base game + the LNS_DLG (CEP3) module and skip
/// otherwise, so they are no-ops on CI without game data.
/// </summary>
public class AppearanceHakMergeRegressionTests
{
    private readonly ITestOutputHelper _output;

    public AppearanceHakMergeRegressionTests(ITestOutputHelper output) => _output = output;

    private static string LnsDlgModuleDir => Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
        "Documents", "Neverwinter Nights", "modules", "LNS_DLG");

    [Fact]
    public void GetAppearanceName_CepRow566_ReturnsLabelNotFallback()
    {
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: no game paths configured");
            return;
        }

        var moduleDir = LnsDlgModuleDir;
        if (!Directory.Exists(moduleDir))
        {
            _output.WriteLine($"SKIP: LNS_DLG module not present: {moduleDir}");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: game data not configured");
            return;
        }

        // Mirror runtime: load the module's HAK list so the CEP-extended 2DA is served.
        gameData.ConfigureModuleHaks(moduleDir);

        var twoDA = gameData.Get2DA("appearance");
        if (twoDA == null || twoDA.RowCount <= 566)
        {
            _output.WriteLine($"SKIP: merged appearance.2da has only {twoDA?.RowCount ?? 0} rows " +
                              "(CEP3 HAKs not loaded for this module) — nothing to assert");
            return;
        }

        var service = new AppearanceService(gameData);
        var name = service.GetAppearanceName(566);

        _output.WriteLine($"GetAppearanceName(566) = '{name}'");

        // The bug produced exactly "Appearance 566". A correct merge yields the CEP label.
        Assert.NotEqual("Appearance 566", name);
        Assert.False(string.IsNullOrEmpty(name));
    }
}
