namespace Radoub.UI.Services;

/// <summary>
/// Unicode characters for UI icons that render consistently across platforms.
/// These avoid emoji sequences that may display differently on Windows/Mac/Linux.
/// </summary>
/// <remarks>
/// Prefer these over emoji for toolbar buttons and navigation.
/// Emoji icons (like 📂) render differently across platforms and may not
/// work well with screen readers.
///
/// Reference: Issue #821 - Emoji icons render inconsistently across platforms
/// </remarks>
public static class IconConstants
{
    // File operations - using geometric symbols
    public const string Open = "\u25A1";          // ▢ White square (open file)
    public const string Save = "\u25AA";          // ▪ Black small square (save)
    public const string New = "\u2795";           // + Heavy plus sign
    public const string Delete = "\u2716";        // ✖ Heavy multiplication X

    // Navigation - using geometric symbols
    public const string ExpandAll = "\u25BC";     // ▼ Black down-pointing triangle
    public const string CollapseAll = "\u25B6";   // ▶ Black right-pointing triangle
    public const string Browse = "\u2026";        // … Horizontal ellipsis

    // Actions
    public const string Clear = "\u00D7";         // × Multiplication sign (clear/close)
    public const string Search = "\u2315";        // ⌕ Telephone recorder (search-like)
    public const string Refresh = "\u21BB";       // ↻ Clockwise open circle arrow
    public const string Settings = "\u2699";      // ⚙ Gear

    // Status (same as StatusIndicatorHelper)
    public const string Success = "\u2713";       // ✓ Check mark
    public const string Error = "\u2717";         // ✗ Ballot X
    public const string Warning = "\u26A0";       // ⚠ Warning sign
    public const string Info = "\u2139";          // ℹ Information source

    // Quartermaster navigation - using simple geometric shapes
    public const string Character = "\u263A";     // ☺ White smiling face
    public const string Classes = "\u2606";       // ☆ White star
    public const string Combat = "\u2694";        // ⚔ Crossed swords (this one is fairly universal)
    public const string Stats = "\u2261";         // ≡ Identical to (bars)
    public const string Feats = "\u2605";         // ★ Black star
    public const string Skills = "\u25CE";        // ◎ Bullseye
    public const string Spells = "\u2728";        // ✨ Sparkles (may vary, fallback to *)
    public const string Inventory = "\u25A0";     // ■ Black square
    public const string Advanced = "\u2630";      // ☰ Trigram for heaven (hamburger menu)
    public const string Scripts = "\u2630";       // ☰ Same as advanced for consistency
}
