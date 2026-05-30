using Radoub.UI.Models;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for global-font-size precedence (#2152).
///
/// Every bundled theme JSON hardcodes fonts.size=14, so the theme's font-size
/// would otherwise overwrite the Trebuchet global slider on every theme apply.
/// RadoubSettings.SharedFontSize is the single authority for global font size;
/// ThemeManager must honor it regardless of the theme's own fonts.size.
/// </summary>
public class ThemeFontSizeResolutionTests
{
    [Fact]
    public void SharedFontSize_OverridesThemeSize()
    {
        var fonts = new ThemeFonts { Size = 14 };
        // User dragged the Trebuchet slider to 20.
        Assert.Equal(20.0, ThemeManager.ResolveEffectiveFontSize(fonts, 20.0));
    }

    [Fact]
    public void SharedFontSize_OverridesEvenWhenThemeSizeDiffers()
    {
        var fonts = new ThemeFonts { Size = 18 };
        Assert.Equal(11.0, ThemeManager.ResolveEffectiveFontSize(fonts, 11.0));
    }

    [Fact]
    public void NullThemeFonts_UsesSharedFontSize()
    {
        Assert.Equal(16.0, ThemeManager.ResolveEffectiveFontSize(null, 16.0));
    }

    [Fact]
    public void NullThemeSize_UsesSharedFontSize()
    {
        var fonts = new ThemeFonts { Size = null };
        Assert.Equal(13.0, ThemeManager.ResolveEffectiveFontSize(fonts, 13.0));
    }

    [Fact]
    public void DefaultSlider_Returns14()
    {
        var fonts = new ThemeFonts { Size = 14 };
        Assert.Equal(14.0, ThemeManager.ResolveEffectiveFontSize(fonts, 14.0));
    }
}
