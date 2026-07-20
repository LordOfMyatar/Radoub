# UI Text Guidelines — Muted Text and Tooltips

Conventions for text contrast and tooltip content across all Radoub tools (#1567).

## Table of Contents

- [Muted text](#muted-text)
- [Tooltips](#tooltips)
- [When to use a popup instead](#when-to-use-a-popup-instead)
- [Implementation reference](#implementation-reference)

## Muted text

Muted brushes (`SystemControlForegroundBaseMediumLowBrush`, `TextMuted`) reduce
contrast. That is a feature for structure and a bug for content.

Use muted for:

- Borders, separators, dividers, grid splitters
- Recessive empty-state placeholders — "Select an item to view details"
- Secondary metadata that is genuinely optional to read

Never use muted for:

- Data the user needs to read: paths, filenames, values, counts, status messages
- Error, warning, or validation text
- Anything that is the primary content of its region

When a control shows different states, set contrast per state rather than picking
one muted brush for all of them. The shared browser windows are the reference: an
explicit override path renders at `BaseHigh`, the current module directory at
`BaseMedium`, and a missing module in the warning brush.

[Back to TOC](#table-of-contents)

## Tooltips

Tooltips are for **short supplementary hints** — a few words to a sentence.

Rules:

1. **Obey the theme.** Never hardcode tooltip colors or font sizes. The shared
   `TooltipStyles.axaml` drives background, foreground, border, and font size from
   theme resources. Hardcoding bypasses every custom theme, including the three
   colour-blind accessibility themes.
2. **Obey sizing.** Tooltips cap at 400px wide and wrap. Do not override `MaxWidth`
   to fit more text — that is a sign the content does not belong in a tooltip.
3. **Never bind raw game data.** TLK descriptions for feats, spells, and item
   properties run to several hundred characters with hard line breaks. Binding one
   directly produces an unreadable wall of text. Pass it through
   `TooltipText.Summarize()` and keep the full string for a details view.

```xml
<!-- WRONG: unbounded TLK prose in a tooltip -->
<Border ToolTip.Tip="{Binding Description}">

<!-- RIGHT: summarized hint; full text stays available for a details panel -->
<Border ToolTip.Tip="{Binding DescriptionTooltip}">
```

[Back to TOC](#table-of-contents)

## When to use a popup instead

If the content is longer than a sentence or two, it is not a tooltip. Prefer, in
order:

1. **A details panel** in the existing layout — best for content the user reads
   while working (item details, creature stats).
2. **A non-modal info popup** — for on-demand longer text. Follow the repo's
   non-modal rule: use `Show()`, never `ShowDialog()`, so the main window stays
   usable.
3. **A tooltip** — only for the short hint that points at one of the above.

A tooltip that has to be resized to fit its content is the wrong control.

[Back to TOC](#table-of-contents)

## Implementation reference

| Concern | Where |
|---------|-------|
| Tooltip theming and sizing | `Radoub.UI/Styles/TooltipStyles.axaml` |
| Summarizing long text | `Radoub.UI/Utils/TooltipText.cs` |
| Per-state contrast example | `Radoub.UI/Views/*BrowserWindow.axaml.cs` |

`TooltipStyles.axaml` is included from every tool's `App.axaml`. A new tool must
add the `StyleInclude` alongside the other shared styles.

[Back to TOC](#table-of-contents)
