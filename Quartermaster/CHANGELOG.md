# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Trimmed to highlights only.

---

## [0.2.76-alpha] - 2026-04-11
**Branch**: `quartermaster/issue-1979` | **PR**: #TBD

### Sprint: Appearance Preview & Search (#1979)
- Show model resref in appearance list with copy context menu (#1870)
- Model completeness indicator below 3D preview (#1873)
- Search creature inventory/backpack item ResRefs in Marlinspike (#1947)
- Custom token support and Description editor for spell-checked fields (#1697)

---

## [0.2.75-alpha] - 2026-03-29
**Branch**: `radoub/issue-1817` | **PR**: #2030

### Token Chooser Control (#1817)
- Token insertion UI for player-facing text fields (color tokens, custom tokens)

---

## [0.2.74-alpha] - 2026-03-28
**Branch**: `quartermaster/issue-1676` | **PR**: #2025

### CEP/HAK Model Loading (#1676)
- Removed `LoadModelPreferBIF` workaround — HAK models now load through standard resolution order
- Parser verified against 1,737 CEP HAK creature models (binary and ASCII) with 100% pass rate

---

## [0.2.73-alpha] - 2026-03-28 | PR #2024
Rename "Assigned" to "Chosen" for feats terminology

---

## [0.2.72-alpha] - 2026-03-25 | PR #1993
Preview state overlay for emitter-only models; split ModelPreviewGLControl; Export Logs menu

## [0.2.71-alpha] - 2026-03-25 | PR #1988
3D preview now respects equipped armor; module directory as highest-priority resource source

## [0.2.70-alpha] - 2026-03-24 | PR #1969
Module switch HAK clearing; appearance source filter with part-based model resolution

## [0.2.69-alpha] - 2026-03-23 | PR #1953
Extract shared wizard search/filter helpers and display item classes

## [0.2.68-alpha] - 2026-03-22 | PR #1938
Ctrl+F search and Ctrl+H replace for creature files (Marlinspike rollout)

## [0.2.67-alpha] - 2026-03-21 | PR #1886
Alignment panel direct integer input; rename Identity panel to 'File and Metadata'

## [0.2.66-alpha] - 2026-03-21 | PR #1880
LUW feat prereqs use pre-level-up creature state; NCW delegates to FeatService

## [0.2.65-alpha] - 2026-03-20 | PR #1879
Fix NCW voice set play button reliability; add DM vault option in creature browser

## [0.2.64-alpha] - 2026-03-20 | PR #1877
Fix bat wing UV/two-sided lighting, DDS R-B channel swap, module switch detection

## [0.2.63-alpha] - 2026-03-20 | PR #1872
Appearance ID prefix and model name tooltips; wraith "bones only" traced to CEP 2DA

## [0.2.62-alpha] - 2026-03-20 | PR #1866
Honor MDL Render flag — skip meshes with Render=false (146/486 standalone models affected)

## [0.2.52-alpha] - 2026-03-19 | PR #1862
Panel spacing uniformity; default sorts for Skills/Spells/Feats panels

## [0.2.51-alpha] - 2026-03-17 | PR #1769
Metamagic/arcane caster IDs from 2DA; split large test and wizard files

## [0.2.50-alpha] - 2026-03-17 | PR #1748
Fix dragon wings: MDL raw data offset is model data SIZE, not file offset; fix skin mesh bone weight parsing

## [0.2.49-alpha] - 2026-03-15 | PR #1742
LUW saves fix, weapon proficiency validation, feat IDs from 2DA LABEL

## [0.2.48-alpha] - 2026-03-15 | PR #1733
Consolidated multi-level wizard with Levels spinner; CE/TN/LG validation modes

## [0.2.46-alpha] - 2026-03-14 | PR #1686
CreatureBrowser BIF archive support; BIF-first model loading

## [0.2.45-alpha] - 2026-03-14 | PR #1684
Appearance panel text search/filter by model name and resource source

## [0.2.43-alpha] - 2026-03-14 | PR #1680
Fix MDL StackOverflow crash (depth limit + cycle detection); ASCII tvert unrolling

## [0.2.42-alpha] - 2026-03-12 | PR #1671
NCW hardening: Fighter invalid feats, BIC voice set requirement, racial point buy

## [0.2.41 to 0.2.37-alpha] - 2026-03-11/12 | PRs #1656-#1669
TDD backwork: 325+ new tests across FeatService, AppearanceService, Combat, PaletteColor, ScriptBrowser, PrestigePrerequisite

## [0.2.36-alpha] - 2026-03-10 | PR #1655
Equipment slot validation (weapon size, feat requirements, warning badges)

## [0.2.35-alpha] - 2026-03-10 | PR #1653
Fix alignment restriction double-inversion bug; TDD backwork (70 new tests)

## [0.2.34-alpha] - 2026-03-10 | PR #1649
LUW class skill color coding and skill search box

## [0.2.33-alpha] - 2026-03-10 | PR #1643
Multi-level character creation; Starting Level spinner on NCW

## [0.2.31-alpha] - 2026-03-10 | PR #1638
LUW sidebar summary, ability score increases, CON retroactive HP, validation modes

## [0.2.30-alpha] - 2026-03-08 | PR #1627
NCW wizard spellbook mechanics; validation toggle (CE/TN/LG); fix divine caster detection

## [0.2.28-alpha] - 2026-03-07 | PR #1614
NCW Identity step (name, portrait, voice, age); filename validation (16-char limit)

## [0.2.27-alpha] - 2026-03-06 | PR #1613
Drag from palette to backpack; slot-based item filtering; paperdoll equipment layout

## [0.2.26-alpha] - 2026-03-05 | PR #1612
Dedicated Special Abilities panel; Load/Save Script Set (Aurora Toolset INI format)

## [0.2.25-alpha] - 2026-03-03 | PR #1590
Editable cleric domain dropdowns; familiar/companion support

## [0.2.24-alpha] - 2026-03-01 | PR #1585
Class alignment restrictions in NCW (48 tests); domain spells/feats display

## [0.2.23-alpha] - 2026-02-28 | PR #1583
SRT bone transform calculation; overlap-aware seam adjustment; gamma correction

## [0.2.22-alpha] - 2026-02-28 | PR #1574
Metamagic feat display with level costs and variant spell memorization

## [0.2.21-alpha] - 2026-02-27 | PR #1561
Wire NCW to CharacterCreationService (~235 lines deleted); 142 new service tests

## [0.2.20-alpha] - 2026-02-27 | PR #1556
Extract wizard logic into testable services; round-trip validation tests

## [0.2.18-alpha] - 2026-02-25 | PR #1527
Fix 102 hardcoded color/brush instances across panels and wizards

## [0.2.17-alpha] - 2026-02-24 | PR #1517
Replace orthographic with perspective projection; fix matrix convention for GLSL

## [0.2.16-alpha] - 2026-02-22 | PR #1504
LUW auto-assign (skills/feats/spells); spell selection for spontaneous/prepared casters

## [0.2.15-alpha] - 2026-02-22 | PR #1494
LUW vs NCW audit (11 findings); deep copy for cancel/undo; GAINMULTIPLE feats

## [0.2.14-alpha] - 2026-02-22 | PR #1483
NCW: alignment grid, faction dropdown, familiar selection, save location on Step 1

## [0.1.79-alpha] - 2026-02-22 | PR #1471
Replace all hardcoded race/class/skill IDs with 2DA lookups; dynamic iteration limits

## [0.1.78-alpha] - 2026-02-22 | PR #1458
CreatureBrowser searches LocalVault/ServerVault; status bar moved to top

## [0.1.77-alpha] - 2026-02-21 | PR #1451
DDS/TGA texture fallback chain; fix mesh positioning (skin vs trimesh transforms)

## [0.1.76-alpha] - 2026-02-21 | PR #1443
File splits: NCW (3856 to 758 lines), MainWindow, CharacterPanel, CreatureDisplayService

## [0.1.74-alpha] - 2026-02-21 | PR #1425
NCW feat selection with prereqs and auto-assign; starting equipment from packeq 2DA

## [0.1.73-alpha] - 2026-02-19 | PR #1422
NCW Steps 7-8: spell selection (spontaneous/divine/wizard) and summary/create

## [0.1.72-alpha] - 2026-02-19 | PR #1419
NCW Steps 5-6: point-buy abilities with racial mods; skill allocation

## [0.1.71-alpha] - 2026-02-19 | PR #1415
NCW Steps 3-4: appearance (18 body parts, colors), class/package, prestige planning

## [0.1.70-alpha] - 2026-02-19 | PR #1413
New Character Wizard skeleton; Steps 1-2 (file type, race & sex)

## [0.1.69-alpha] - 2026-02-16 | PR #1403
TLK language toggle; delete creature files from module browser

## [0.1.68-alpha] - 2026-02-15 | PR #1379
Inventory bug bash: palette read-only, creature-only items to natural slots

## [0.1.67-alpha] - 2026-02-11 | PR #1311
Cross-tool inventory unification with Fence

## [0.1.66-alpha] - 2026-02-02 | PR #1207
Complete roundtrip read/write for all creature fields

## [0.1.65-alpha] - 2026-02-02 | PR #1200
Static appearance model rendering fixes (beholder, troll, beetles)

## [0.1.63-alpha] - 2026-02-01 | PR #1172
Restart MDL format implementation from scratch (ASCII + binary readers)

## [0.1.62-alpha] - 2026-01-31 | PR #1169
CreatureBrowserPanel: collapsible left panel with search and HAK scanning

## [0.1.61-alpha] - 2026-01-29 | PR #1152
AB/APR display; metamagic slot counting by effective level

## [0.1.59-alpha] - 2026-01-28 | PR #1140
Fix 11 bare catch blocks; add cancellation tokens to async operations

## [0.1.57-alpha] - 2026-01-27 | PR #1135
Modular item palette caching (BIF/Override/HAK with independent invalidation)

## [0.1.56-alpha] - 2026-01-25 | PR #1129
GPU-accelerated OpenGL renderer (Silk.NET) replacing SkiaSharp CPU renderer

## [0.1.52-alpha] - 2026-01-22 | PR #1054
Appearance preview rendering in Appearance panel

## [0.1.51-alpha] - 2026-01-22 | PR #1052
Item details panel, movement speed, saving throws, special abilities, metamagic editing

## [0.1.49-alpha] - 2026-01-20 | PR #1034
Item palette: on-demand loading, disk caching (startup 18s to 1.9s)

## [0.1.48-alpha] - 2026-01-20 | PR #1029
Textured 3D rendering with PLT colors; armor body part overrides; deferred init

## [0.1.47-alpha] - 2026-01-19 | PR #1017
Level-Up Wizard (5 steps); ClassService for prestige prereqs; Re-Level/Down-Level

## [0.1.44-alpha] - 2026-01-18 | PR #948
UTC/BIC conversion; File > New; QuickBar panel; themes/settings; factions

## [0.1.42-alpha] - 2026-01-17 | PR #936
Multiple spell memorizations with +/- buttons; Clear All Spells

## [0.1.41-alpha] - 2026-01-17 | PR #933
Portrait browser (race/gender filter, mini icons); soundset browser with playback

## [0.1.38-alpha] - 2026-01-16 | PR #918
Portrait display in sidebar; soundset preview with audio playback

## [0.1.35-alpha] - 2026-01-11 | PR #872
Level-up resource tracking (ability increases, HP/skills/feats/spells calculation)

## [0.1.34-alpha] - 2026-01-11 | PR #869
Classes Panel MVP: add class and level-up with ClassPickerWindow

## [0.1.33-alpha] - 2026-01-11 | PR #865
2DA compliance audit; export Character Sheet (text/markdown)

## [0.1.32-alpha] - 2026-01-09 | PR #804
Editable skill ranks with +/- buttons; skill points summary table

## [0.1.31-alpha] - 2026-01-07 | PR #800
Semantic theme colors across all panels; derived font size resources

## [0.1.30-alpha] - 2026-01-05 | PR #789
Split MDL readers (1000+ lines each) and AppearancePanel into partials

## [0.1.29-alpha] - 2026-01-04 | PR #774
Spell slot summary per caster class; known spells with overlap highlighting

## [0.1.27-alpha] - 2026-01-04 | PR #766
3D character model preview: MDL parser, PLT textures, real-time updates

## [0.1.26-alpha] - 2026-01-04 | PR #765
BasePanelControl base class; extract SkillService, FeatService, AppearanceService, SpellService

## [0.1.25-alpha] - 2026-01-03 | PR #759
NWN item icons: PLT parser, ImageService (TGA/DDS), ItemIconService

## [0.1.24-alpha] - 2026-01-03 | PR #757
Spell list GFF parsing (known/memorized); spell editing with dirty state

## [0.1.23-alpha] - 2026-01-03 | PR #755
Race dropdown, editable alignment sliders, package picker

## [0.1.22-alpha] - 2026-01-03 | PR #752
Editable HP, Natural AC, Challenge Rating; AC breakdown display

## [0.1.21-alpha] - 2026-01-03 | PR #749
Edit ability scores with derived stat recalculation

## [0.1.20-alpha] - 2026-01-02 | PR #747
Character color picker: ColorPickerWindow, PaletteColorService, TgaReader

## [0.1.19-alpha] - 2026-01-01 | PR #716
Faction display (FacReader); SettingsWindow (3 tabs); UTC/BIC conversion

## [0.1.17-alpha] - 2025-12-31 | PR #695
Inventory sync for save persistence; add/remove/equip/unequip operations

## [0.1.16-alpha] - 2025-12-31 | PR #688
BIC file support: XP, Gold, Age, Biography; file-type-aware UI sections

## [0.1.15-alpha] - 2025-12-30 | PR #684
New Appearance and Character panels; Stats Panel CR adjustment

## [0.1.14-alpha] - 2025-12-30 | PR #669
Scripts Panel (13 event scripts); ScriptBrowserWindow; subrace/deity/CR fields

## [0.1.12-alpha] - 2025-12-30 | PR #651
Open Conversation in Parley from Scripts tab

## [0.1.10-alpha] - 2025-12-28 | PR #633
Spells Panel: search, level/school/status filters, 8-class selection

## [0.1.9-alpha] - 2025-12-28 | PR #629
Feats Panel: all feats with status icons, category filters, prereq checking, add/remove

## [0.1.8-alpha] - 2025-12-28 | PR #628
Skills Panel: class/cross-class highlighting, sorting, filtering

## [0.1.7-alpha] - 2025-12-28 | PR #625
Classes & Levels Panel: class slots, alignment display, auto-levelup package

## [0.1.6-alpha] - 2025-12-28 | PR #622
CreatureDisplayService (2DA/TLK); BAB calculation; enhanced StatsPanel

## [0.1.5-alpha] - 2025-12-28 | PR #619
Application shell: sidebar + content layout, StatsPanel, InventoryPanel, AutomationIds

## [0.1.3-alpha] - 2025-12-26 | PR #585
Inventory display: item palette, equipment slots, placeholder icons

## [0.1.2-alpha] - 2025-12-26 | PR #584
MainWindow split into partials (892 to 466 lines); 21 unit tests

## [0.1.0-alpha] - 2025-12-26 | PR #578
Initial release: Creature Editor MVP with Inventory Panel
