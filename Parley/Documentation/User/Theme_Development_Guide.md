# Theme Development Guide

**Status**: Draft for review
**Last Updated**: 2025-11-20

[Table of Contents](#table-of-contents)

## Table of Contents

- [Overview](#overview)
- [Theme Manifest Structure](#theme-manifest-structure)
- [Creating Your First Theme](#creating-your-first-theme)
- [Color Palette Design](#color-palette-design)
- [Font Selection](#font-selection)
- [Spacing and Layout](#spacing-and-layout)
- [Accessibility Guidelines](#accessibility-guidelines)
- [Example Themes](#example-themes)
- [Testing Your Theme](#testing-your-theme)
- [Technical Implementation Details](#technical-implementation-details)
- [Resources](#resources)

---

## Overview

[↑ Table of Contents](#table-of-contents)

Parley supports custom themes through JSON-based theme manifests. Themes are data-only (no code execution) and can customize:

- **Colors**: Background, text, UI elements, tree nodes, speaker colors
- **Fonts**: Font family, size, weight for UI and monospace text
- **Spacing**: Control padding, margins, panel spacing, tree indentation

Themes are automatically discovered from two locations:
1. **Official themes**: `Parley/Themes/` directory (deployed with application)
2. **User themes**: `~/Parley/Themes/` (user home directory, same as settings)

Changes apply immediately - the tree view auto-refreshes when you switch themes.

---

## Theme Manifest Structure

[↑ Table of Contents](#table-of-contents)

A theme is defined in a JSON file with the following structure:

```json
{
  "manifest_version": "1.0",
  "plugin": {
    "id": "org.parley.theme.my-theme",
    "name": "My Custom Theme",
    "version": "1.0.0",
    "author": "Your Name",
    "description": "A brief description of your theme",
    "type": "theme",
    "tags": ["custom", "dark", "accessibility"]
  },
  "base_theme": "Dark",
  "accessibility": {
    "type": "standard",
    "contrast_level": "AAA"
  },
  "colors": { },
  "fonts": { },
  "spacing": { }
}
```

### Plugin Metadata

- **id**: Unique identifier (reverse domain notation recommended)
- **name**: Display name shown in theme selector
- **version**: Semantic version (e.g., "1.0.0")
- **author**: Your name or organization
- **description**: Brief explanation shown in UI (2-3 sentences max)
- **type**: Must be "theme"
- **tags**: Array of keywords (e.g., ["dark", "blue", "accessibility"])

### Base Theme

- **base_theme**: Either "Light" or "Dark"
- Determines Avalonia's base styling before custom colors are applied

### Accessibility Metadata

- **type**: "standard", "colorblind", "high-contrast", or "nightmare" (easter eggs)
- **condition**: Optional - specific colorblindness type (deuteranopia, protanopia, tritanopia)
- **contrast_level**: "AAA", "AA", or "LOL" (for easter eggs)
- **warning**: Optional - displayed for accessibility concerns

---

## Creating Your First Theme

[↑ Table of Contents](#table-of-contents)

**Example: Star Trek LCARS Theme**

1. **Choose a base**: LCARS uses dark backgrounds, so use "Dark"
2. **Pick your palette**: Orange (#FF9900), blue (#9999FF), magenta (#CC99CC)
3. **Select fonts**: Antonio Bold (free LCARS-style font from Google Fonts)
4. **Define spacing**: LCARS has generous padding and rounded corners

```json
{
  "manifest_version": "1.0",
  "plugin": {
    "id": "org.parley.theme.lcars",
    "name": "LCARS (Star Trek)",
    "version": "1.0.0",
    "author": "Trek Fan",
    "description": "Star Trek LCARS interface theme with signature orange/blue palette",
    "type": "theme",
    "tags": ["dark", "scifi", "lcars"]
  },
  "base_theme": "Dark",
  "colors": {
    "background": "#000000",
    "sidebar": "#1A1A1A",
    "text": "#FF9900",
    "selection": "#9999FF",
    "border": "#FF9900",
    "accent": "#CC99CC",
    "tree_entry": "#FF9900",
    "tree_reply": "#9999FF",
    "tree_link": "#CC99CC",
    "speaker_1": "#FF9900",
    "speaker_2": "#9999FF",
    "speaker_3": "#CC99CC",
    "speaker_4": "#FFCC66",
    "speaker_5": "#6666FF",
    "speaker_6": "#FF66CC"
  },
  "fonts": {
    "primary": "Antonio",
    "monospace": "Consolas",
    "size": 14,
    "weight": "Bold"
  },
  "spacing": {
    "control_padding": 12,
    "control_margin": 8,
    "panel_spacing": 16,
    "min_control_height": 32,
    "tree_indent": 20
  }
}
```

3. Save as `Themes/lcars.json`
4. Restart Parley or reload settings
5. Select "LCARS (Star Trek)" from theme dropdown

---

## Color Palette Design

[↑ Table of Contents](#table-of-contents)

### Color Properties

**Core UI Colors**:
- `background`: Main window background
- `sidebar`: Tree view and panel backgrounds
- `text`: Primary text color
- `selection`: Selected item highlight
- `border`: Control borders and dividers
- `accent`: Primary accent color (buttons, links)

**Semantic Colors**:
- `error`: Error messages and warnings
- `warning`: Warning indicators
- `success`: Success messages
- `info`: Informational messages

**Tree Node Colors**:
- `tree_entry`: Entry nodes (NPC/Owner speaks) - **Default**: Orange (#FF8A65)
- `tree_reply`: Reply nodes (PC speaks) - **Default**: Blue (#4FC3F7)
- `tree_link`: Link nodes (conversation jumps)

**Speaker Colors** (Multi-NPC Conversations):
- `speaker_1` through `speaker_6`: Default colors for up to 6 speakers
- Users can override per-dialog (Issue #36)

**PC/Owner Color Overrides**:
Themes can override the default PC (blue) and Owner (orange) colors for accessibility:
- `tree_reply` value is used for PC speaker color (blue default)
- `tree_entry` value is used for Owner speaker color (orange default)
- If omitted, falls back to `ColorPalette.Blue` (#4FC3F7) and `ColorPalette.Orange` (#FF8A65)
- Critical for colorblind themes where blue/orange may not provide sufficient contrast

### Color Picker Tools

**Online Tools**:
- [Coolors.co](https://coolors.co/) - Generate and explore palettes
- [Adobe Color](https://color.adobe.com/) - Color wheel and harmony rules
- [Paletton](https://paletton.com/) - Advanced palette designer
- [Color Hunt](https://colorhunt.co/) - Curated palette gallery

**Contrast Checking**:
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/) - WCAG compliance
- [Coolors Contrast Checker](https://coolors.co/contrast-checker) - Quick validation

**Colorblind Simulation**:
- [Coblis](https://www.color-blindness.com/coblis-color-blindness-simulator/) - Upload and simulate
- [Adobe Color Accessibility Tools](https://color.adobe.com/create/color-accessibility) - Built-in checks

### Color Format

All colors must be 6-digit hex codes: `#RRGGBB`

```json
"colors": {
  "background": "#FFFFFF",   // ✅ Valid
  "text": "#000",            // ❌ Invalid (must be 6 digits)
  "accent": "blue"           // ❌ Invalid (must be hex)
}
```

---

## Font Selection

[↑ Table of Contents](#table-of-contents)

### Font Properties

- `primary`: Main UI font (menus, buttons, labels)
- `monospace`: Code and script display (fixed-width)
- `size`: Base font size in points (10-18 recommended)
- `weight`: "Normal", "Bold", "SemiBold", "Light"

### System Default Fonts

Use `"$Default"` to inherit system fonts:

```json
"fonts": {
  "primary": "$Default",
  "monospace": "Consolas",
  "size": 14,
  "weight": "Normal"
}
```

### Custom Fonts

**Important**: Users must have the font installed on their system.

**Cross-Platform Safe Fonts**:
- **Sans-Serif**: Arial, Helvetica, Segoe UI, Roboto
- **Serif**: Georgia, Times New Roman, Cambria
- **Monospace**: Consolas, Courier New, Monaco, Menlo

**Free Fonts from Google Fonts**:
- [Google Fonts](https://fonts.google.com/) - Hundreds of free, open-source fonts
- Popular choices: Roboto, Open Sans, Lato, Montserrat, Source Sans Pro

**Nerd Fonts** (Icon-Rich Programming Fonts):
- [Nerd Fonts](https://www.nerdfonts.com/) - Patched fonts with icons and glyphs
- Recommended for developer themes:
  - **JetBrains Mono Nerd Font** - Modern, clear programming font
  - **Fira Code Nerd Font** - Popular with ligatures support
  - **Cascadia Code Nerd Font** - Microsoft's coding font
  - **Hack Nerd Font** - Designed for source code

**LCARS and Specialty Fonts**:
- [Antonio](https://fonts.google.com/specimen/Antonio) - LCARS-style geometric sans
- [Orbitron](https://fonts.google.com/specimen/Orbitron) - Futuristic, geometric
- [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P) - Retro gaming

**Font Installation**:
- Windows: Copy `.ttf`/`.otf` to `C:\Windows\Fonts`
- macOS: Open font file, click "Install Font"
- Linux: Copy to `~/.local/share/fonts` and run `fc-cache -f -v`

### Font Fallbacks

If a font is not found, Parley falls back to system default. Always test on multiple platforms.

---

## Spacing and Layout

[↑ Table of Contents](#table-of-contents)

### Spacing Properties

- `control_padding`: Inner spacing within controls (4-16px)
- `control_margin`: Space between controls (2-10px)
- `panel_spacing`: Space between major UI panels (8-20px)
- `min_control_height`: Minimum height for buttons/inputs (24-40px)
- `tree_indent`: Indentation per tree level (12-24px)

### Layout Density Presets

**Compact** (for small screens):
```json
"spacing": {
  "control_padding": 4,
  "control_margin": 2,
  "panel_spacing": 6,
  "min_control_height": 24,
  "tree_indent": 12
}
```

**Standard** (default):
```json
"spacing": {
  "control_padding": 8,
  "control_margin": 4,
  "panel_spacing": 8,
  "min_control_height": 28,
  "tree_indent": 16
}
```

**Spacious** (LCARS, accessibility):
```json
"spacing": {
  "control_padding": 12,
  "control_margin": 8,
  "panel_spacing": 16,
  "min_control_height": 36,
  "tree_indent": 24
}
```

---

## Accessibility Guidelines

[↑ Table of Contents](#table-of-contents)

### WCAG Compliance

**AAA Standard** (Recommended):
- Normal text: 7:1 contrast ratio
- Large text (18pt+): 4.5:1 contrast ratio

**AA Standard** (Minimum):
- Normal text: 4.5:1 contrast ratio
- Large text: 3:1 contrast ratio

**Testing Tools**:
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [APCA Contrast Calculator](https://www.myndex.com/APCA/)

### Colorblind-Safe Palettes

**Deuteranopia** (Red-Green Colorblind, 5% of males):
- **Avoid**: Red/green combinations
- **Use**: Blue/orange, blue/yellow, purple/orange
- **Example**: Blue (#0066CC), Orange (#FF8800), Purple (#9933CC)
- **Recommended Background**: Medium gray (#DDDDDD) with darker sidebar (#AAAAAA)

**Protanopia** (Red Colorblind, 1% of males):
- **Avoid**: Red-based colors
- **Use**: Sky blue, orange, yellow
- **Example**: Sky Blue (#0099CC), Orange (#FF9944), Purple (#6600CC)
- **Recommended Background**: Medium gray (#DDDDDD) with darker sidebar (#AAAAAA)

**Tritanopia** (Blue-Yellow Colorblind, <1%):
- **Avoid**: Blue/yellow combinations
- **Use**: Red/blue, magenta/cyan
- **Example**: Red (#CC0066), Blue (#006699), Magenta (#CC3366)
- **Recommended Background**: Medium gray (#DDDDDD) with darker sidebar (#AAAAAA)

**Universal Design Tips**:
- Use shape/pattern in addition to color (tree node icons)
- Ensure sufficient contrast for all text
- Test with colorblind simulation tools
- **Avoid pure white** (#FFFFFF) for light themes - use soft off-white (#F8F8F8) or light gray (#DDDDDD)
- Pure white creates harsh contrast that strains eyes, especially with bright accent colors
- Medium gray backgrounds tested extensively with colorblind users for optimal comfort

---

## Example Themes

[↑ Table of Contents](#table-of-contents)

### Minimal Light Theme

```json
{
  "manifest_version": "1.0",
  "plugin": {
    "id": "org.parley.theme.minimal-light",
    "name": "Minimal Light",
    "version": "1.0.0",
    "author": "You",
    "description": "Clean, minimal light theme",
    "type": "theme",
    "tags": ["light", "minimal"]
  },
  "base_theme": "Light",
  "colors": {
    "background": "#FFFFFF",
    "text": "#000000"
  }
}
```

**Note**: Omitted properties inherit from base theme.

### High Contrast Dark

```json
{
  "manifest_version": "1.0",
  "plugin": {
    "id": "org.parley.theme.high-contrast-dark",
    "name": "High Contrast Dark",
    "version": "1.0.0",
    "author": "You",
    "description": "Maximum contrast for visibility",
    "type": "theme",
    "tags": ["dark", "high-contrast", "accessibility"]
  },
  "base_theme": "Dark",
  "accessibility": {
    "type": "high-contrast",
    "contrast_level": "AAA"
  },
  "colors": {
    "background": "#000000",
    "sidebar": "#0A0A0A",
    "text": "#FFFFFF",
    "selection": "#FFFF00",
    "border": "#FFFFFF",
    "accent": "#00FFFF",
    "speaker_1": "#00FF00",
    "speaker_2": "#FF00FF",
    "speaker_3": "#FFFF00",
    "speaker_4": "#00FFFF",
    "speaker_5": "#FF6600",
    "speaker_6": "#FF0099"
  },
  "fonts": {
    "primary": "$Default",
    "monospace": "Consolas",
    "size": 16,
    "weight": "Bold"
  }
}
```

---

## Testing Your Theme

[↑ Table of Contents](#table-of-contents)

### Installation

**For Development/Testing**:
1. Save your theme JSON to `~/Parley/Themes/your-theme-name.json`
2. Restart Parley (themes are discovered at startup)
3. Open Settings → Theme
4. Select your theme from dropdown
5. Tree view auto-refreshes immediately when theme changes

**For Contributing Official Themes**:
1. Save to `Parley/Parley/Themes/your-theme-name.json` in source code
2. Build project (themes auto-copy to output directory via MSBuild)
3. Submit PR with your theme JSON

### Visual Checks

- [ ] All text is readable against backgrounds
- [ ] Selection highlights are visible
- [ ] Tree nodes use distinct colors (Entry/Reply/Link)
- [ ] PC (blue) and Owner (orange) colors visible and overridden if needed
- [ ] Speaker colors are distinguishable (test with multi-NPC dialog)
- [ ] Buttons and controls are clearly defined
- [ ] Error/warning messages stand out
- [ ] Tree view refreshes immediately when switching themes (no restart needed)

### Accessibility Validation

- [ ] Run contrast checks on all text/background pairs
- [ ] Test with colorblind simulators
- [ ] Verify font size is legible (14pt minimum recommended)
- [ ] Check spacing doesn't cause overlap

### Multi-NPC Testing

Open a dialog with multiple speakers and verify:
- [ ] `speaker_1` through `speaker_6` colors are distinct
- [ ] Colors remain visible when nodes are selected
- [ ] Tree links don't clash with speaker colors

---

## Technical Implementation Details

[↑ Table of Contents](#table-of-contents)

### Theme Discovery and Loading

**Theme Discovery** (`ThemeManager.DiscoverThemes()`):
1. Scans `Parley/Themes/` directory for official themes (shipped with app)
2. Scans `~/Parley/Themes/` for user themes (Environment.SpecialFolder.UserProfile)
3. User themes with same ID override official themes
4. Invalid JSON files are logged and skipped

**Note**: Both ThemeManager and SettingsService use UserProfile (`~/Parley/`) for consistency across platforms.

**Theme Loading** (`ThemeManager.ApplyTheme(string themeId)`):
1. Deserializes JSON theme manifest
2. Sets Avalonia `Application.RequestedThemeVariant` (Light/Dark)
3. Creates `SolidColorBrush` resources for each color in `Application.Resources`
4. Maps `tree_reply` → `ThemePCColor` and `tree_entry` → `ThemeOwnerColor`
5. Fires `ThemeManager.ThemeApplied` event
6. UI components refresh automatically via event subscription

### Auto-Refresh Implementation

**Event-Driven Architecture**:
```csharp
// ThemeManager.cs
public event EventHandler? ThemeApplied;

public bool ApplyTheme(string themeId)
{
    // ... load and apply theme resources ...

    ThemeApplied?.Invoke(this, EventArgs.Empty);
    return true;
}
```

**UI Subscription** (`MainWindow.axaml.cs`):
```csharp
public MainWindow()
{
    InitializeComponent();

    // Subscribe to theme changes
    ThemeManager.Instance.ThemeApplied += OnThemeApplied;
}

private void OnThemeApplied(object? sender, EventArgs e)
{
    if (_viewModel.CurrentDialog != null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _viewModel.RefreshTreeViewColors();
        });
    }
}
```

**Tree Refresh** (`MainViewModel.RefreshTreeViewColors()`):
- Preserves expansion state of all nodes
- Rebuilds tree view with updated colors from `SpeakerVisualHelper`
- `SpeakerVisualHelper` checks `Application.Resources` for theme overrides

### PC/Owner Color Override Logic

**Default Behavior** (`SpeakerVisualHelper.GetSpeakerColor()`):
```csharp
// PC gets blue
if (isPC)
    return ColorPalette.Blue; // #4FC3F7

// Owner gets orange
if (string.IsNullOrEmpty(speaker))
    return ColorPalette.Orange; // #FF8A65

// NPCs get hash-based colors
return ColorPalette.GetNpcColor(speaker);
```

**Theme Override** (applied if `tree_reply` or `tree_entry` exist):
```csharp
// Check for theme PC color override
if (isPC && app?.Resources.TryGetResource("ThemePCColor", ..., out var pcColor))
    return (string)pcColor;

// Check for theme Owner color override
if (string.IsNullOrEmpty(speaker) &&
    app?.Resources.TryGetResource("ThemeOwnerColor", ..., out var ownerColor))
    return (string)ownerColor;
```

### Build and Deployment

**MSBuild Configuration** (`Parley.csproj`):
```xml
<!-- Copy Themes folder to output directory -->
<ItemGroup>
  <None Include="Themes\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

This ensures updated theme files deploy with every build without manual file copying.

---

## Resources

[↑ Table of Contents](#table-of-contents)

### Color Tools

- [Coolors.co](https://coolors.co/) - Palette generator
- [Adobe Color](https://color.adobe.com/) - Color wheel and themes
- [Paletton](https://paletton.com/) - Advanced palette designer
- [Color Hunt](https://colorhunt.co/) - Curated palettes
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/) - WCAG validation
- [Coblis Colorblind Simulator](https://www.color-blindness.com/coblis-color-blindness-simulator/)

### Font Resources

- [Google Fonts](https://fonts.google.com/) - Free, open-source fonts
- [Nerd Fonts](https://www.nerdfonts.com/) - Icon-patched programming fonts
- [Font Squirrel](https://www.fontsquirrel.com/) - Free commercial fonts
- [Programming Fonts](https://www.programmingfonts.org/) - Test drive coding fonts

### Design Inspiration

- [Dribble UI Themes](https://dribbble.com/tags/ui-theme) - Professional UI designs
- [Behance Color Palettes](https://www.behance.net/search/projects?search=color%20palette)
- [Material Design Color System](https://material.io/design/color)
- [Flat UI Colors](https://flatuicolors.com/) - Curated palettes

### Accessibility

- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [A11y Project](https://www.a11yproject.com/) - Accessibility checklist
- [Contrast Ratio Tool](https://contrast-ratio.com/) - Quick contrast checks

---

**Happy Theming!** 🎨

If you create a theme you'd like to share, submit a PR to the Parley repository with your theme JSON in the `Themes/` directory.
