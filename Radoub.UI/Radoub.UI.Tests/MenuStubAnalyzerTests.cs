using Radoub.UI.Utils;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Dead-stub lint for top-level menus (#2362, #2231 guard).
///
/// A "dead stub" is a top-level <c>&lt;Menu&gt;</c> item with a hardcoded
/// <c>IsEnabled="False"</c> — the disabled "Not yet implemented" anti-pattern.
/// A genuinely wired menu item controls its enabled state via a binding or
/// code-behind, never a literal False. The audit (#2359 Part 4) found disabled
/// Undo/Redo stubs in Manifest and Fence; this lint guards against re-introducing
/// them and complements the #2231 Undo/Redo wiring work.
///
/// The analyzer is a pure string-in / violations-out function so the rule itself
/// is unit-testable without the filesystem; a companion test walks the real
/// MainWindow.axaml files.
/// </summary>
public class MenuStubAnalyzerTests
{
    [Fact]
    public void Flags_TopLevelMenuItem_With_Literal_IsEnabledFalse()
    {
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui">
              <Menu>
                <MenuItem Header="_Edit">
                  <MenuItem Header="_Undo" InputGesture="Ctrl+Z" IsEnabled="False"/>
                </MenuItem>
              </Menu>
            </Window>
            """;

        var violations = MenuStubAnalyzer.FindDisabledMenuStubs(axaml);

        Assert.Single(violations);
        Assert.Contains("Undo", violations[0].Header);
    }

    [Fact]
    public void Flags_Stub_With_NotYetImplemented_Tooltip()
    {
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui">
              <Menu>
                <MenuItem Header="_Edit">
                  <MenuItem Header="_Redo" IsEnabled="False" ToolTip.Tip="Not yet implemented"/>
                </MenuItem>
              </Menu>
            </Window>
            """;

        var violations = MenuStubAnalyzer.FindDisabledMenuStubs(axaml);

        Assert.Single(violations);
        Assert.Contains("Redo", violations[0].Header);
    }

    [Fact]
    public void Ignores_Binding_Controlled_IsEnabled()
    {
        // The wired pattern: enabled state driven by a binding, not a literal.
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui">
              <Menu>
                <MenuItem Header="_File">
                  <MenuItem Header="_Save" InputGesture="Ctrl+S" IsEnabled="{Binding HasFile}"/>
                </MenuItem>
              </Menu>
            </Window>
            """;

        var violations = MenuStubAnalyzer.FindDisabledMenuStubs(axaml);

        Assert.Empty(violations);
    }

    [Fact]
    public void Ignores_IsEnabledFalse_On_NonMenu_Controls()
    {
        // Buttons disabled-until-valid (OK buttons, wizard nav) are legitimate and
        // overwhelmingly common — the lint must not touch anything outside <Menu>.
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui">
              <StackPanel>
                <Button Content="OK" IsEnabled="False"/>
              </StackPanel>
            </Window>
            """;

        var violations = MenuStubAnalyzer.FindDisabledMenuStubs(axaml);

        Assert.Empty(violations);
    }

    [Fact]
    public void Ignores_IsEnabledFalse_In_ContextMenu()
    {
        // ContextMenu items (e.g. Parley FlowchartPanel "Go to Parent Node") legitimately
        // pair IsEnabled="False" with an IsVisible binding — not a top-level menu stub.
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui">
              <Border>
                <Border.ContextMenu>
                  <ContextMenu>
                    <MenuItem Header="Go to Parent Node" IsEnabled="False" IsVisible="{Binding !IsLink}"/>
                  </ContextMenu>
                </Border.ContextMenu>
              </Border>
            </Window>
            """;

        var violations = MenuStubAnalyzer.FindDisabledMenuStubs(axaml);

        Assert.Empty(violations);
    }

    [Fact]
    public void Empty_When_No_Menu_Present()
    {
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui">
              <TextBlock Text="No menu here"/>
            </Window>
            """;

        var violations = MenuStubAnalyzer.FindDisabledMenuStubs(axaml);

        Assert.Empty(violations);
    }
}
