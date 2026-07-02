using Radoub.UI.Models;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for global-font-family precedence (#2404).
///
/// Fonts have one source of truth: RadoubSettings.SharedFontFamily (the Trebuchet
/// shared setting). A theme's own fonts.Primary is only a fallback used when no
/// shared family is configured. Previously each tool re-applied SharedFontFamily
/// after theme apply to win the last-write race; ResolveEffectiveFontFamily makes
/// the precedence explicit and testable so all tools resolve identically.
/// </summary>
public class ThemeFontFamilyResolutionTests
{
    [Fact]
    public void SharedFontFamily_OverridesThemePrimary()
    {
        var fonts = new ThemeFonts { Primary = "Consolas" };
        Assert.Equal("Arial", ThemeManager.ResolveEffectiveFontFamily(fonts, "Arial"));
    }

    [Fact]
    public void EmptySharedFamily_FallsBackToThemePrimary()
    {
        var fonts = new ThemeFonts { Primary = "Consolas" };
        Assert.Equal("Consolas", ThemeManager.ResolveEffectiveFontFamily(fonts, ""));
    }

    [Fact]
    public void WhitespaceSharedFamily_FallsBackToThemePrimary()
    {
        var fonts = new ThemeFonts { Primary = "Consolas" };
        Assert.Equal("Consolas", ThemeManager.ResolveEffectiveFontFamily(fonts, "   "));
    }

    [Fact]
    public void NoSharedFamily_NoThemePrimary_ReturnsNull()
    {
        var fonts = new ThemeFonts { Primary = null };
        Assert.Null(ThemeManager.ResolveEffectiveFontFamily(fonts, ""));
    }

    [Fact]
    public void NullThemeFonts_UsesSharedFamily()
    {
        Assert.Equal("Segoe UI", ThemeManager.ResolveEffectiveFontFamily(null, "Segoe UI"));
    }

    [Fact]
    public void ThemeDefaultSentinel_TreatedAsNoThemeFamily()
    {
        // "$Default" in a theme means "system default", not a literal family name.
        var fonts = new ThemeFonts { Primary = "$Default" };
        Assert.Null(ThemeManager.ResolveEffectiveFontFamily(fonts, ""));
    }
}
