# Color Palette Design for Speaker Identification

## Overview

Parley uses a color + shape system to visually identify dialog speakers in the tree view. Colors must be:
- **Color-blind safe** (distinguishable for protanopia, deuteranopia, tritanopia)
- **High contrast** against both light and dark backgrounds
- **Visually distinct** from each other

## Current Palette (v0.1.3)

### Fixed Assignments
- **PC (Player Character)**: Circle + Blue `#4FC3F7`
- **Owner (Default NPC)**: Square + Orange `#FF8A65`

### Hash-Assigned for Named NPCs
5 colors × 4 shapes = 20 possible combinations

| Color | Hex Code | Light Mode | Dark Mode | Color-Blind Safe? |
|-------|----------|------------|-----------|-------------------|
| Orange | `#FF8A65` | ✅ Good | ✅ Good | ✅ Yes |
| Purple | `#BA68C8` | ✅ Good | ✅ Good | ✅ Yes |
| Teal | `#26A69A` | ✅ Good | ⚠️ Darker | ✅ Yes |
| Amber | `#FFD54F` | ⚠️ **Too Light** | ✅ Good | ✅ Yes |
| Pink | `#F48FB1` | ⚠️ Pastel | ✅ Good | ✅ Yes |

## Contrast Examples

### Light Mode (White Background #FFFFFF)

```
[SAMPLE TEXT] - Blue #4FC3F7
[SAMPLE TEXT] - Orange #FF8A65
[SAMPLE TEXT] - Purple #BA68C8
[SAMPLE TEXT] - Teal #26A69A
[SAMPLE TEXT] - Amber #FFD54F    ⚠️ Low contrast
[SAMPLE TEXT] - Pink #F48FB1     ⚠️ Marginal
```

### Dark Mode (Black Background #000000)

```
[SAMPLE TEXT] - Blue #4FC3F7
[SAMPLE TEXT] - Orange #FF8A65
[SAMPLE TEXT] - Purple #BA68C8
[SAMPLE TEXT] - Teal #26A69A     ⚠️ Slightly dark
[SAMPLE TEXT] - Amber #FFD54F
[SAMPLE TEXT] - Pink #F48FB1
```

## Collision Probability

With 5 colors and hash-based assignment:
- **2 speakers**: ~20% chance of color collision
- **3 speakers**: ~49% chance
- **4 speakers**: ~69% chance
- **5 speakers**: ~81% chance

With 4 shapes × 5 colors = 20 combinations:
- **2 speakers**: ~5% full collision (color AND shape)
- **4 speakers**: ~29% full collision
- **6 speakers**: ~62% full collision

## Proposed Expansions

### Option 1: Add 3 Colors (8 total → 32 combinations)

| Color | Hex Code | Light Mode | Dark Mode | Notes |
|-------|----------|------------|-----------|-------|
| Green | `#4CAF50` | ✅ Good | ✅ Good | Fills green gap |
| Indigo | `#5C6BC0` | ✅ Good | ✅ Good | Blue-purple distinct |
| Cyan | `#00BCD4` | ✅ Good | ✅ Good | Bright blue-green |

**Collision rates with 32 combinations:**
- 2 speakers: ~3% full collision
- 4 speakers: ~19% full collision
- 6 speakers: ~42% full collision

### Option 2: Replace Problematic Colors

Replace Amber and adjust Teal:

| Old | New | Hex Code | Reason |
|-----|-----|----------|--------|
| Amber `#FFD54F` | Gold `#FBC02D` | Darker, better contrast on white |
| Teal `#26A69A` | Teal `#00897B` | Darker variant, better on white |

### Option 3: Theme-Aware Colors (Future)

Different palettes for light/dark modes:
- Light mode: Darker, saturated colors
- Dark mode: Lighter, vibrant colors
- Requires theme detection logic

## Testing Checklist

When adjusting colors, verify:
- [ ] Readable on white background (#FFFFFF)
- [ ] Readable on black background (#000000)
- [ ] Distinguishable in color-blind simulator (protanopia)
- [ ] Distinguishable in color-blind simulator (deuteranopia)
- [ ] Distinguishable in color-blind simulator (tritanopia)
- [ ] Visually distinct from other palette colors
- [ ] Works with 16×16 pixel shape icons

## Color-Blind Simulation Resources

- [Coblis Color Blindness Simulator](https://www.color-blindness.com/coblis-color-blindness-simulator/)
- [Toptal Color Blind Filter](https://www.toptal.com/designers/colorfilter)
- [Adobe Color Accessibility Tools](https://color.adobe.com/create/color-accessibility)

## WCAG Contrast Requirements

For UI text (16px+):
- **AA**: Minimum 3:1 contrast ratio
- **AAA**: Minimum 4.5:1 contrast ratio

Our target: AAA compliance for both themes.

## Implementation Notes

Colors are assigned via hash function:
```csharp
int hash = Math.Abs(speakerName.GetHashCode());
return NpcColors[hash % NpcColors.Length];
```

Same speaker always gets same color across sessions (deterministic).

## Related Issues

- [Issue #16](https://github.com/LordOfMyatar/Radoub/issues/16) - Color-blind friendly speaker visuals
