using System.IO;
using Xunit;

namespace Parley.Tests;

/// <summary>
/// Regression test for #2210: Parley's App.axaml must include the Avalonia
/// DataGrid theme so DialogBrowserPanel renders its rows.
///
/// FileBrowserPanelBase switched from ListBox to DataGrid in #2198. Without
/// the explicit DataGrid theme StyleInclude, Avalonia's DataGrid measures
/// zero-width/zero-height — the browser pane appears completely empty even
/// though entries are loaded into the underlying list.
///
/// This test reads App.axaml from disk because XAML resource registration is
/// the bug surface — runtime mocking of StyleInclude is more brittle than a
/// simple content assertion.
/// </summary>
public class AppStylesIncludeTests
{
    [Fact]
    public void AppAxaml_IncludesAvaloniaDataGridTheme()
    {
        var appAxamlPath = FindAppAxaml();
        var content = File.ReadAllText(appAxamlPath);

        Assert.Contains(
            "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml",
            content);
    }

    private static string FindAppAxaml()
    {
        // Walk up from the test binary directory to the repo root, then descend
        // to Parley/Parley/App.axaml. Avoids brittle ".." path math.
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Parley", "Parley", "App.axaml");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Parley/Parley/App.axaml not found from any ancestor of the test working directory.");
    }
}
