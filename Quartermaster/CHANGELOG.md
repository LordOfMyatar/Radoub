# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Fix: HAK model rendering stability (#1314)

---

## [0.2.47-alpha] - 2026-03-15
**Branch**: `quartermaster/issue-1630` | **PR**: #1732

### Enhancement: Review NCW & LUW brush usage for readability (#1630)

---

## [0.2.46-alpha] - 2026-03-14
**Branch**: `radoub/issue-1685` | **PR**: #1686

### Sprint: HAK Resource Browsing (#1685, #1133, #1582)

- CreatureBrowser: "Base Game" checkbox shows UTC creatures from BIF archives (#1133)
- CreatureBrowser: BIF creatures shown as read-only with `[BIF]` type indicator (#1133)
- CreatureBrowser: GameDataService integration for BIF resource listing (#1133)
- Performance: `[TIMING]` instrumentation for HAK and BIF scanning (#1582)

- BIF-first model loading prevents CEP HAK overrides from breaking creature rendering
- Error handling in UpdateMeshBuffers prevents OpenGL crashes from malformed models
- Fix "(Dynamic) (Dynamic)" duplicate prefix in appearance dropdown
- Batch palette item additions to prevent UI thread crash
- Preserve module items when loading palette cache

---

## [0.2.45-alpha] - 2026-03-14
**Branch**: `quartermaster/issue-1520` | **PR**: #1684

### Sprint: Appearance Panel Search & Filter (#1520, #1201)

- Text search/filter for model names on the Appearance panel
- Checkbox filters to toggle visibility by resource source (Standard/BIF, Hak, Override)
- Search by model name (RACE column) or label
- Semicolon-separated exclude pattern filter to hide noise entries (default: `Invisible;object`)
- Exclude filter persists across sessions via SettingsService

---

## [0.2.44-alpha] - 2026-03-14
**Branch**: `quartermaster/issue-1610` | **PR**: #1682

### Sprint: File Ops & Validation (#1610)

- Save As dialog now lists current file type first, preserving .bic extension (#1594)
- Browser panel auto-save documented as intentional design; improved status message (#1535)
- Indeterminate progress bar in status bar during load, save, export, and initialization (#817)

---

## [0.2.43-alpha] - 2026-03-14
**Branch**: `quartermaster/issue-1518` | **PR**: #1680

### Bug: MDL reader crash + model pipeline hardening (#1518)

**MDL Parser Fixes**:
- Fix StackOverflow crash on c_kocrachn model — node tree depth limit (128) + cycle detection
- AABB tree recursion depth limit (64) + cycle detection
- Smart pointer base detection — both direct and base-subtraction strategies tried for every offset
- Pointer value 0 treated as NULL (was incorrectly used as valid offset, causing cow model circular refs)
- Face index validation rejects out-of-bounds data instead of rendering garbage
- Animation event count bounds checking
- ASCII MDL tvert unrolling — faces with split vertex/tvert indexing now render correctly (fixes wight face)

**Model Pipeline Hardening**:
- Body part 0 (invisible) now always overrides armor appearance
- TryAddBodyPart bone math exception handling + logging
- TextureService bare catch → specific exception logging
- ModelPreviewGLControl shader compilation, NaN vertex, UpdateTextures error handling
- AppearancePanel event handler try-catch wrappers

---

## [0.2.42-alpha] - 2026-03-12
**Branch**: `quartermaster/issue-1651` | **PR**: #1671

### Sprint: NCW Hardening (#1651)

- #1639 - NCW: Fighter gets invalid feats (package validation + MinSpellLevel gaps)
- #1629 - NCW: Require voice set selection for BIC files
- #1628 - Point buy total may vary by race (27 vs 30)

---

## [0.2.41-alpha] - 2026-03-12
**Branch**: `quartermaster/issue-1660` | **PR**: #1669

### Sprint: TDD Backwork Phase 2 — Medium-Lift Service Tests (#1660)

- FeatCacheService: idempotent load, cache hit/miss, IsMemoryCacheLoaded, three in-memory caches
- CharacterSheetService: markdown export format, stat calculation edge cases
- LevelUpApplicationService: ability score cap (255), retroactive CON HP adjustment edge cases
- SpellService: spell slot edge cases, spontaneous vs prepared distinction

---

## [0.2.40-alpha] - 2026-03-12
**Branch**: `quartermaster/issue-1658` | **PR**: #1666

### TDD: FeatService Advanced Prerequisite Coverage (#1658)

- 65 new tests in FeatServiceAdvancedTests covering all identified coverage gaps
- OR-required feat chains: 2-option, 5-option, mixed AND+OR, dual AND, all-met/none-met/partial
- Epic feat validation: level 21 pass, level 20 fail, multiclass total level, epic+ability+feat combos, BAB-met-but-not-epic
- Spell level prerequisites: Wizard level 3/5 casting, Fighter (no spells), multiclass spell access, level 1 spell threshold
- Dual skill requirements: both-met, one-met, neither-met, skill index beyond SkillList bounds
- Level requirements: MinLevel pass/fail, multiclass total level, class-specific level (Fighter 4), wrong class, MaxLevel cap, MinLevel+MaxLevel range
- Multiple ability prerequisites: dual ability (STR+DEX), all six abilities, single ability failure
- AutoAssignFeats: no-package alphabetical, package preferences, skip-owned, max count, prereq checker, bonus pool restriction, pool+package interaction, zero max, no duplicates
- Multiclass feat scenarios: feat in second class table, unavailable cross-class, combined granted union, granting class identification
- HasPrerequisites flag: no-prereqs false, simple-prereqs true, complex-prereqs true
- Tooltip formatting: OR section "One of:", multi-requirement-type inclusion

---

## [0.2.39-alpha] - 2026-03-12
**Branch**: `quartermaster/issue-1657` | **PR**: #1665

### TDD: AppearanceService Test Coverage (#1657)

- 85 total tests (35 new) covering all 21 AppearanceService methods
- GetAppearanceName: TLK resolution, `****` STRING_REF, empty STRING_REF, LABEL fallback
- IsPartBasedAppearance: uppercase/lowercase "P", null MODELTYPE
- GetSizeAcModifier: all 7 D&D size categories (Tiny through Colossal), invalid/star/unknown values
- GetAllAppearances: field population, star/empty label filtering, no-data empty list
- GetAllPhenotypes: break-on-empty-after-data logic, leading empty row skip, fallback default IDs
- Portrait methods: star value handling, po_ prefix case insensitivity
- GetAllWings/Tails: consecutive empty heuristic, entries surviving small gaps, star/invalid fallbacks
- GetAllFactions: null/empty dir, directory without FAC file, sequential default IDs
- Package methods: no-data fallback, star/empty ClassID filtering, empty data sets
- GetAllSoundSets: display name vs label, empty data set

---

## [0.2.38-alpha] - 2026-03-11
**Branch**: `quartermaster/issue-1659` | **PR**: #1664

### TDD: CreatureDisplayService.Combat Test Coverage (#1659)

- CombatCalculationTests: 110 test cases covering all combat calculation paths
- BAB via 2DA: full/3-4/half progressions for all base and prestige classes
- BAB multiclass: independent per-class calculation and summation
- BAB fallback estimates: reasonableness checks when 2DA unavailable
- Saving throws via 2DA: all base class progressions (good/poor formulas)
- Saving throw multiclass: independent summation across classes
- Equipment attack bonus: enhancement non-stacking, attack bonus stacking, mixed
- CalculateCombatStats integration: full breakdown with BAB + equipment + APR + sequence
- BuildAttackSequence: iterative -5 penalty formatting
- Epic APR: epic BAB doesn't grant extra attacks
- Edge cases: empty classes, zero levels, null items, negative levels

---

## [0.2.37-alpha] - 2026-03-11
**Branch**: `quartermaster/issue-1654` | **PR**: #1656

### Epic: TDD Backwork — Service Test Coverage Gaps (#1654)

#### Phase 1: Critical Services
- PaletteColorService: 24 tests (color lookup, gradient generation, caching, edge cases)
- QuartermasterScriptBrowserContext: 17 tests (path resolution, .mod working dirs, game data delegation)
- Removed dead ModularPaletteCacheService (replaced by SharedPaletteCacheService in Radoub.UI)
- Extended MockGameDataService with FindResource/ListResources support

#### Phase 2: Prestige Class Prerequisites
- PrestigePrerequisiteTests: 28 tests (BAB, feat, skill, FEATOR groups, ARCSPELL, available class filtering)

#### Audit Findings
- AppearanceService (48 tests), FeatCacheService (20 tests) — already covered, no gaps
- CharacterSheetService markdown export — already backfilled (10+ tests)
- FeatService OR-required/epic feats — already tested (10+ tests)
- SpellService spontaneous vs prepared — already tested (6+ tests)
- LevelUpApplicationService CON retroactive HP — already tested (5 tests)

---

## [0.2.36-alpha] - 2026-03-10
**Branch**: `quartermaster/issue-1608` | **PR**: #1655

### Sprint: Inventory UX (Part 2) (#1608)

- [x] #1313 — Drag from item palette to backpack (completed in prior sprint)
- [x] #1312 — Equipment slot validation by weapon size, feat requirements
  - Weapon size validation: creatures can wield weapons up to 1 size larger; 2+ sizes shows warning
  - Feat requirement validation: checks `ReqFeat0`-`ReqFeat4` from `baseitems.2da` against creature's feat list
  - Resolves feat names from `feat.2da` FEAT column + TLK for human-readable warnings
  - Warning UI: orange `ThemeWarning` border + badge on equipment slots with validation issues
  - Combined warnings: multiple issues (size + feats) shown together per slot
  - Validation runs on file load and after every equip operation
  - 32 unit tests (size, feats, edge cases, batch validation)
- [x] #1035 — Item Palette: Module context tracking (already implemented)

---

## [0.2.35-alpha] - 2026-03-10
**Branch**: `quartermaster/issue-1606` | **PR**: #1653

### Sprint: LUW Alignment & Followups (#1606)

- [x] #1589 — Fix alignment restriction double-inversion bug in ClassService
  - Scale inversion (0=Chaotic treated as Lawful), allowed logic inversion, type=0x03 axis logic
  - TN creature could take Monk (shouldn't), Paladin LE was allowed (shouldn't)
  - Description text swapped "Must be"/"Cannot be"
  - 24 new ClassAlignmentValidationTests exercising real GetAvailableClasses
- [x] #1018 — Already complete from prior sprints (spell selection, bonus feats, packages, deep copy)
- [x] Added PlayerClass column to MockGameDataService for proper class filtering tests
- [x] Test coverage audit identifying 15 services needing TDD backwork
- [x] TDD backwork: 50 AppearanceService tests (appearance, phenotype, portrait, wing, tail, soundset, faction, package lookups)
- [x] TDD backwork: 20 FeatCacheService tests (in-memory caching, class/race feat grants, lazy init, clear)
- [x] Extended MockGameDataService with phenotype, portrait, wingmodel, tailmodel, soundset 2DA data

---

## [0.2.34-alpha] - 2026-03-10
**Branch**: `quartermaster/issue-1604` | **PR**: #1649

### Sprint: LUW Skill UX (#1604)

- [x] #1500 — Add color coding for class skills in Level Up Wizard (green for class skills via BrushManager)
- [x] #1499 — Add skill search box to Level Up Wizard (case-insensitive name filter)
- fix: LUW feat step crash when filtering available feats (FindControl pattern for AvaloniaXamlLoader)
- fix: Non-class skills invisible in light mode (use SystemControlForegroundBaseHighBrush)
- fix: Feat prereqs badge using faint grey instead of warning color (ThemeWarning resource)
- fix: LUW class selection now shows all classes for UTC files (non-player classes like Animal, Commoner)
- test: 15 unit tests for SkillDisplayHelper (filter, indicator, color rules)
- chore: Improved unhandled exception logging with InnerException details

---

## [0.2.33-alpha] - 2026-03-10
**Branch**: `quartermaster/issue-1492` | **PR**: #1643

### Sprint: Multi-Level Character Creation (#1492)

- [x] #1617 — Add Class and Level Up buttons on ClassesPanel now launch Level Up Wizard
- [x] #1431 — Starting Level spinner on NCW Step 1; LUW loops for levels 2+ (single class)
- [x] #1018 (partial) — packages.2da auto-assign already integrated in LUW feats/skills/spells
- test: Multi-level stacking test for LevelUpApplicationService (600 tests passing)

---

## [0.2.32-alpha] - 2026-03-10
**Branch**: `radoub/issue-1641` | **PR**: #1642

### Sprint: Tech Debt Cleanup (#1641)

- [x] #1641 — Split FeatService.cs (1003 lines) into 3 partial classes: Core (609), Prerequisites (343), LevelUp (230)

---

## [0.2.31-alpha] - 2026-03-10
**Branch**: `quartermaster/issue-1603` | **PR**: #1638

### Sprint: LUW Sidebar & Ability Scores (#1603)

- [x] #1502 — Add dynamic sidebar summary to Level Up Wizard
- [x] #1501 — Add ability score increase at levels 4/8/12/16/20/24/28/32/36/40
- [x] #1155 — Add epic BAB handling for APR calculation

### LUW Validation Modes (CE/TN/LG)

- feat: CE mode — multi-ability toggle, unlimited feats/skills, no prereq enforcement
- feat: CE mode — feat step no longer skipped when no feats granted (e.g., Rogue 11)
- feat: TN mode — warnings only, never blocks Next button
- feat: Feat description panel with scrollbar in feat selection step
- feat: Filter out level-1-only feats (MaxLevel=1) from LUW feat list
- feat: CON retroactivity — HP recalculated for all previous levels when CON modifier changes
- test: 5 new unit tests for CON retroactive HP calculation

### Infrastructure

- fix: Remove IntegrationTests from Radoub.sln (prevents 15min FlaUI runs during `dotnet test`)
- fix: Mark Radoub.TestUtilities as non-test project (fixes "Test Run Aborted")

---

## [0.2.30-alpha] - 2026-03-08
**Branch**: `quartermaster/issue-1602` | **PR**: #1627

### Sprint: NCW Spell Step & Validation (#1602)

- [x] #1587 — NCW: Wizard spellbook mechanics (all cantrips + 3+INT mod level-1 spells; LUW: 2 free spells per level-up)
- [x] #1503 — Validation toggle for NCW and LUW (Chaotic Evil / True Neutral / Lawful Good)

### Bug Fixes

- fix: Wizard no longer misidentified as divine caster in NCW spell step
  - Added `IsArcaneCaster()` to SpellService (base class IDs + prestige `ArcSpellLvlMod`)
  - Rewrote `IsDivineCaster()` to delegate to `IsArcaneCaster()` instead of relying on `MemorizesSpells`/`SpellKnownTable`
- fix: True Neutral validation now warns instead of blocking (yellow ⚠ messages, Next still enabled)
- fix: Chaotic Evil mode bypasses all point/slot constraints (abilities, feats, skills, spells)
- fix: Wizard spell step defaults to Level 1 tab (not empty cantrips tab)
- fix: Familiar name validation clears when switching to non-familiar class
- fix: Warning (TN) mode allows over-assignment with warnings for abilities, feats, skills, spells
- fix: Warning (TN) mode allows selecting restricted alignments with warning (only Strict blocks)
- fix: Warning (TN) mode still requires familiar name (hard requirement — game breaks without it)
- fix: Feat count label uses warning brush when over limit
- fix: Alignment restriction warning uses theme-aware warning brush
- fix: Replaced hardcoded `Brushes.Goldenrod` with `BrushManager.GetWarningBrush()` in NCW and LUW

---

## [0.2.29-alpha] - 2026-03-07
**Branch**: `quartermaster/issue-1601` | **PR**: #1625

### Sprint: NCW UX Polish (Part 2) (#1601)

- [x] #1598 — NCW Summary/Review shows familiar type and custom name
- [x] #866 — Search/filter for package picker (filter by name or ID)
- [x] #1615 — NCW: Race descriptions already displayed on Race & Sex step (verified)
- [x] #1616 — NCW: Gender selection moved above race list with visual highlight

---

## [0.2.28-alpha] - 2026-03-07
**Branch**: `quartermaster/issue-1600` | **PR**: #1614

### Sprint: NCW UX Polish (Part 1) (#1600)

- [x] #1597 — UX: NCW Identity step separate from Summary
  - New Step 3 (Identity) with first/last name, portrait, voice set, age, description
  - Portrait moved from Appearance to Identity step with Browse button
  - Summary (Step 11) is now read-only review with Identity summary row
  - All subsequent steps renumbered (10 → 11 total steps)
- [x] #1596 — UX: NCW sound set Browse button placement
  - Voice set Browse button now immediately adjacent to voice set label in Identity step
- [x] #1595 — NCW filename validation at wizard start
  - Validates generated ResRef in Identity step name change handler
  - Warns when filename exceeds Aurora Engine 16-character limit
  - Advisory note for mixed-case filenames (will be lowercased)

---

## [0.2.27-alpha] - 2026-03-06
**Branch**: `quartermaster/issue-1580` | **PR**: #1613

### Sprint: Item Palette UX & Equipment Layout (#1580)

- [x] #1313 — Drag from item palette to backpack
  - Added PaletteItem/ItemViewModels drop handling to backpack drop handler
  - Reuses existing OnAddToBackpackRequested for item creation
  - Fixed drop target: moved from DataGrid to wrapping Grid (DataGrid column-reorder consumes drag events)
- [x] #1209 — Refine item palette search/filtering
  - Added slot-based filtering dropdown (Head, Chest, Right Hand, etc. + Non-Equipable)
  - Added "Slots" column to item list showing which equipment slots each item fits
  - Added EquipableSlotFlags/EquipableSlotsDisplay to ItemViewModel
  - New SlotFilterInfo model and filter state persistence
- [x] #1484 — Game-Style Equipment Paperdoll Layout
  - Added SlotSize enum (Small/Wide/Medium/Large/Tall/ExtraTall) with 32px base grid unit
  - Compact 4-row layout: Head+Cloak / Chest+Neck+Arms / Weapons+Belt / Boots+Ammo+Rings
  - EquipmentSlotControl icons scale with Stretch="Uniform" to fit slot dimensions
  - Equip-over-existing swaps old item to backpack automatically

---

## [0.2.26-alpha] - 2026-03-05
**Branch**: `quartermaster/issue-1611` | **PR**: #1612

### Sprint: Special Abilities & Scripts (#1611)

- [x] #1575 - Special Abilities panel UX overhaul
  - Extracted Special Abilities from Feats panel into dedicated "Spell-Like" nav panel
  - New `SpecialAbilitiesPanel` with full-width layout, summary count, column headers
  - Editable "Uses" NumericUpDown (0-255) — SpellFlags byte treated as integer uses/day count
  - Editable "Caster Level" NumericUpDown — greyed out for monster abilities (no class levels in spells.2da)
  - Spell picker filters by `UserType` column: only shows spells (1) and special abilities (2), excludes feats/items
  - Removed Special Abilities expander from FeatsPanel (deleted `FeatsPanel.SpecialAbilities.cs`)
  - New ✧ nav button in sidebar between Feats and Skills
- [x] #1487 - Load Script Set Option in Scripts Panel
  - Added "Load Script Set..." and "Save Script Set..." buttons to Scripts panel
  - `ScriptTemplateService` for reading/writing Aurora Toolset INI format (`[ResRefs]` section)
  - Bidirectional mapping between INI keys and UtcFile field names (all 13 script slots)
  - Default directory: `{NeverwinterNightsPath}/scripttemplates/` (auto-created on first use)
  - Compatible with existing Aurora Toolset template files (nwn1.ini, x2.ini, BattleAI.ini, etc.)
  - 13 unit tests for INI parsing, saving, round-trip, key mapping

---

## [0.2.25-alpha] - 2026-03-03
**Branch**: `quartermaster/issue-1586` | **PR**: #1590

### Sprint: Classes & Levels Panel Enhancements (#1586, #1588)

- [x] #1586 - Editable domain dropdowns on Classes & Levels panel
  - Domain 1 and Domain 2 ComboBoxes for Cleric classes (populated from `domains.2da`)
  - Changing domain swaps GrantedFeat in FeatList and updates Domain1/Domain2 on CreatureClass
  - Shows granted feats inline below dropdowns
  - Section auto-shows/hides based on class list
  - `DomainService.ResolveDomains()` — uses Domain1/Domain2 if set, falls back to feat inference
  - `DomainService.GetGrantedFeatId()` — lookup helper for domain→feat mapping
  - Fixed FeatsPanel domain display when Domain1/Domain2 are 0 (UTC files)
  - Fixed BicReader/BicWriter missing Domain1/Domain2 fields
  - Cross-panel refresh: FeatsPanel and SpellsPanel reload when domains change
- [x] #1588 - Familiar/companion support on Classes & Levels panel and NCW
  - Familiar type dropdown for any class with Associate in `packages.2da` (not hardcoded)
  - Familiar name TextBox on ClassesPanel and NCW (reads/writes `FamiliarName` CExoString)
  - Fixed BicReader/BicWriter missing `FamiliarType` read/write
  - Added `FamiliarName` property to UtcFile + all readers/writers
  - Fixed NCW familiar selection not persisting across step navigation
  - 2DA-driven `ClassGrantsFamiliar()` — checks packages.2da Associate column (supports custom classes)
  - Corrected mock data to match NWN game data (hen_familiar.2da, hen_companion.2da)
  - 22 new tests (ClassDomainAndFamiliar, DomainService, BIC round-trip)

---

## [0.2.24-alpha] - 2026-03-01
**Branch**: `quartermaster/issue-1493` | **PR**: #1585

### Sprint: Class Restrictions & Domain Display (#1493)

- [x] #1488 - Enforce class alignment restrictions in New Character Wizard
  - Fixed 0x prefix parsing bug in `AlignRestrict`/`AlignRstrctType` hex values
  - Rewrote alignment validation with per-axis logic (LC, GE, both-axis modes)
  - Monk requires Lawful, Paladin requires LG, Druid requires Neutral axis, Barbarian/Bard cannot be Lawful
  - 48 new alignment restriction tests
- [x] #1489 - Display domain spells, feats, and engine-granted abilities
  - New `DomainService` reading `domains.2da` for Level_1–9 spells and GrantedFeat
  - NCW Step 4: Domain info panel shows spells and granted feat when domains selected
  - NCW Spells step: Domain spell summary for divine casters
  - SpellsPanel (main editor): Domain Spells section in left column
  - FeatsPanel (main editor): Domain-granted feats section in Assigned Feats list
  - 22 new DomainService tests

---

## [0.2.23-alpha] - 2026-02-28
**Branch**: `quartermaster/issue-1557` | **PR**: #1583

### Bug: Dynamic elf model head/neck mismatch, halfling proportions off (#1557)

- Full SRT bone transform calculation (Scale * Rotation * Translation) for body part positioning
- Overlap-aware seam adjustment: detects thin vertex overlap at joints and nudges parts closer
- Part type tracking dictionary for reliable mesh-to-body-part mapping
- Gamma correction (pow 1/1.6) and improved ambient lighting to match NWN toolset brightness
- 15 new unit tests (bone transforms + seam overlaps)

---

## [0.2.22-alpha] - 2026-02-28
**Branch**: `quartermaster/sprint/metamagic` | **PR**: #1574

### Sprint: Metamagic Feats & Spell Variants (#638, #1156)

- [x] #638 - Meta-magic feats display: MetaMagicExpander shows creature's metamagic feats with level costs, loaded from feat.2da/TLK
- [x] #1156 - Metamagic variant spells: each metamagic feat generates variant rows in spell list with effective levels; memorization stores SpellMetaMagic flag; counts tracked per (spellId, metamagic) tuple
- [x] 34 new metamagic tests (level costs, effective levels, feat detection, flag storage, count keys)

---

## [0.2.21-alpha] - 2026-02-27
**Branch**: `radoub/issue-1560` | **PR**: #1561

### TDD Audit Followup (#1560)

- [x] Wire NCW `OnFinishClick()` to `CharacterCreationService.BuildCreature()` — remove duplicate inline logic (~235 lines deleted)
- [x] Dedicated tests for FeatService (53), SkillService (25), SpellService (26), CharacterSheetService (38) — 142 new tests

---

## [0.2.20-alpha] - 2026-02-27
**Branch**: `quartermaster/issue-1553` | **PR**: #1556

### Sprint: NCW/LUW Service Extraction & Unit Tests (#1553)

- [x] #1544 - Extract NCW wizard step logic into testable services
- [x] #1545 - Extract LUW wizard logic into testable services
- [x] #1546 - Unit tests for AbilityPointBuyService, CharacterCreationService, LevelUpApplicationService
- [x] #1547 - Round-trip validation tests for character creation and level-up

---

## [0.2.19-alpha] - 2026-02-26
**Branch**: `radoub/issue-1530` | **PR**: #1537

### Sprint: File Splits (#1530)

- LevelUpWizardWindow.axaml.cs split into 6 partials (base + ClassSelection + FeatSelection + SkillAllocation + SpellSelection + SummaryAndApply)

---

## [0.2.18-alpha] - 2026-02-25
**Branch**: `quartermaster/issue-1524` | **PR**: #1527

### Sprint: Theme Cleanup (#1524)

- [x] #1481 - Hardcoded Colors and Brushes (102 instances)
- [x] #1496 - Audit ScrollViewer padding across panels and wizards
- [x] #1531 - Simplify AppearancePanel color pickers — removed NumericUpDown, kept swatch-only UI
- Improved text contrast in wizard skill steps (BaseMediumLow → BaseMediumBrush)
- Fixed hardcoded `LimeGreen` in LevelUpWizard → `DynamicResource ThemeSuccess`
- Added named-color detection to theme brush hook

---

## [0.2.17-alpha] - 2026-02-24
**Branch**: `quartermaster/issue-1205` | **PR**: #1517

### Bug: Model rotation causes perspective distortion on limbs (#1205)

- Replaced orthographic projection with 30° FOV perspective — eliminates depth flattening during rotation
- Pre-center mesh vertices at geometric center — rotation now pivots around model center, not world origin
- Fixed matrix upload convention — System.Numerics row-vector matrices now correctly transposed for GLSL column-vector math
- Proper camera framing via `CreateLookAt` with auto-distance based on model radius
- NaN vertex exclusion from bounds computation prevents incorrect centering
- Removed shader `screenOffset` uniform (no longer needed with pre-centered vertices)

---

## [0.2.16-alpha] - 2026-02-22
**Branch**: `quartermaster/issue-1491` | **PR**: #1504

### Sprint: Level Up Wizard - Feature Parity & Spells (#1491)

- [x] #1473 - Auto-assign buttons for skills, feats, and spells using package-based defaults
  - Extracted auto-assign algorithms from NCW into shared `FeatService`, `SkillService`, `SpellService` methods
  - LUW resolves default package from `classes.2da` `Package` column
  - NCW refactored to call shared services (removed ~120 lines of inline logic)
- [x] #1474 - Divine/auto-grant caster spell display (Ranger, Paladin, Cleric, Druid)
  - Read-only info panel showing spells gained at each level
  - Uses `GetSpellsForClassAtLevel()` from shared `SpellService`
- [x] #1018 (partial) - Spell selection UI for spontaneous casters (Sorcerer/Bard)
  - Two-panel picker with spell level tabs, search filter, add/remove, auto-assign
  - Delta calculation: only new spells gained at current level (via `cls_spkn_*.2da`)
  - Spell write-back to creature's `KnownSpells` array on apply
- [x] #1018 (partial) - Spell selection UI for prepared casters (Wizard)
  - Spellbook picker using `cls_spgn_*.2da` for slots per level
  - Shares UI infrastructure with spontaneous caster picker
- [x] #1018 (partial) - Bonus feat restriction for Fighter/Wizard
  - Bonus feat slots restricted to `List=1` feats from `cls_feat_*.2da`
  - General feat slots remain unrestricted
  - UI labels indicate "Class Bonus Feat" vs "General Feat" selection
- [x] Auto-granted class feats (List=3) applied during level-up
  - Added `GetClassFeatsGrantedAtLevel()` to FeatService
  - Bard Song, Curse Song, and similar feats now correctly granted at appropriate levels

---

## [0.2.15-alpha] - 2026-02-22
**Branch**: `quartermaster/issue-1490` | **PR**: #1494

### Sprint: Level Up Wizard - Bug Fix & Foundation (#1490)

- [x] #1460 - Audit Level Up Wizard for parity with New Character Wizard
  - Comprehensive audit document comparing LUW (1,205 lines) vs NCW (3,572 lines) across feats, skills, spells, and cancel safety
  - Identified 11 findings, 4 fixed this sprint
- [x] #1475 - Fix feat prompting for multiclass characters
  - Added `GetLevelUpFeatCount()` to FeatService for per-level calculation
  - Fixed cumulative formula in `GetExpectedFeatCount` (was granting at 1,4,7... instead of 1,3,6,9...)
  - LUW now shows feat breakdown (general + class bonus + racial)
- [x] #1018 (partial) - Deep copy for cancel/undo
  - Added `DeepCopy()` to UtcFile with deep copy of all lists and reference types
  - LUW constructor now clones creature for rollback safety
  - `ApplyLevelUp()` wrapped in try/catch with `RestoreFromOriginal()` on failure
- [x] #1018 (partial) - Repeatable feats handling (`GAINMULTIPLE` in feat.2da)
  - Fixed `ApplyFeatFilter()` to keep GAINMULTIPLE feats in available list after selection
  - Fixed `ApplyLevelUp()` to allow duplicate feat entries for repeatable feats
- [x] Audit finding: Unavailable skill filtering
  - Skills that a creature cannot use are now dimmed (0.4 opacity) and disabled in LUW
  - Matches NCW behavior using `GetUnavailableSkillIds()`

---

## [0.2.14-alpha] - 2026-02-22
**Branch**: `quartermaster/issue-1482` | **PR**: #1483

### Sprint: New Character Wizard Missing Features (#1482)

- [x] #1477 - Fix: Creature browser now refreshes after saving new creature
- [x] #1461 - Feat: 3x3 alignment grid with class restriction validation
- [x] #1440 - Feat: Faction dropdown for UTC creation (Step 10)
- [x] #1478 - Feat: Searchable appearance type selection (TextBox filter + ListBox)
- [x] #1476 - Feat: Save location picker moved to Step 1 of wizard
- [x] #1472 - Feat: Feat description panel shows selected feat details
- [x] #1459 - Feat: Familiar selection for Wizard/Sorcerer (hen_familiar.2da + FamiliarType GFF field)

---

## [0.1.79-alpha] - 2026-02-22
**Branch**: `quartermaster/issue-1469` | **PR**: #1471

### Sprint: Hardcoded Values Bug Bash (#1469)

- [x] #1462 - Replace hardcoded Human race ID (6) with racialtypes.2da lookups
  - Added `GetRacialExtraFeatsAtFirstLevel()`, `GetRacialExtraSkillPointsPerLevel()`, `GetRacialDefaultAppearance()` to CreatureDisplayService
  - Replaced `== 6` checks in NewCharacterWizard (Race, Skills), LevelUpWizard, FeatService
  - Racial bonus skill points and feats now read from `ExtraFeatsAtFirstLevel` and `ExtraSkillPointsPerLvl` columns
- [x] #1463 - Replace hardcoded skill count (28) with dynamic skills.2da RowCount
  - Added `GetSkillCount()` to CreatureDisplayService
  - Updated skill loops in NewCharacterWizard (Skills, BuildCreature), LevelUpWizard
- [x] #1464 - Replace hardcoded divine caster class IDs (2, 3) with 2DA detection
  - Added `IsDivineCaster()` to CreatureDisplayService using SpellCaster/MemorizesSpells/SpellKnownTable columns
  - Replaced `classId == 2 || classId == 3` in NewCharacterWizard.Spells
  - Replaced hardcoded `{ 2, 3, 6, 7 }` in ClassService.CanCastDivineSpells with DivSpellLvlMod lookup
- [x] #1465 - Extract feat progression interval (3) as named constant
  - `FeatProgressionInterval = 3` in LevelUpWizard and FeatService
  - Documented as D&D 3.5/NWN engine rule (not 2DA-configurable)
- [x] #1466 - Replace hardcoded 2DA iteration limits with dynamic RowCount
  - AppearanceService: appearance (1000→dynamic), portraits (500→dynamic), soundsets (500→dynamic)
  - FeatService: feat.2da (2000→dynamic), class feat tables (200/300→dynamic), racial feat tables (100→dynamic)
  - SkillService: cls_skill_*.2da (50→dynamic)
  - PortraitBrowserWindow, SoundsetBrowserWindow: 500→dynamic
  - LevelUpWizard: feat table (300→dynamic)
- [x] #1467 - Check GAINMULTIPLE before excluding feats from selection
  - Added `CanFeatBeGainedMultipleTimes()` to CreatureDisplayService
  - LevelUpWizard now allows re-selecting feats with `GAINMULTIPLE=1` in feat.2da
- [x] #1468 - Extract skill point multiplier as named constant
  - `FirstLevelSkillMultiplier = 4` in NewCharacterWizard and LevelUpWizard
  - Documented as D&D 3.5/NWN engine rule (not 2DA-configurable)
- [x] Replace hardcoded point-buy total (30) with racialtypes.2da AbilitiesPointBuyNumber
  - Added `GetRacialAbilitiesPointBuyNumber()` to CreatureDisplayService
  - Point-buy budget now updates when race changes (e.g., Animal races get fewer points)
  - Ability scores reset when switching to a race with a different point pool

---

## [0.1.78-alpha] - 2026-02-22
**Branch**: `quartermaster/issue-1454` | **PR**: #1458

### Sprint: Bug Fix & Polish (#1454)

- [x] #1173 - Body part changes don't refresh or save (fixed in prior PR)
- [x] #1188 - CreatureBrowserPanel not searching LocalVault/ServerVault for BIC files
  - Added RadoubSettings fallback for vault path resolution when IScriptBrowserContext is null
  - Fixed vault BICs deduped by base class name check (vault entries now bypass dedup)
  - Fixed clicking vault BIC corrupted ModulePath, causing module UTCs to disappear
  - Fixed same-name files (kingsnake.utc/kingsnake.bic) colliding in highlight selection
  - Fixed vault entries counted as "module" in status label
  - Cleaned up checkbox labels and defaults (LocalVault off by default, tooltips added)
- [x] #957 - Move status bar to top of window
  - Moved DockPanel.Dock from Bottom to Top, matching Fence pattern
  - Added themed background, border styling, corner radius

---

## [0.1.77-alpha] - 2026-02-21
**Branch**: `quartermaster/issue-1153` | **PR**: #1451

### Sprint: 3D Preview & Appearance Fixes (#1153)

- [x] #1134 - 3D Preview: Texture/Coloring Issues After Geometry Fix
  - Add DDS texture fallback to TextureService (PLT → TGA → DDS → human fallback)
  - Add BioWare proprietary DDS format conversion (20-byte header → standard 128-byte DDS header)
  - Fix row-major matrix convention in GetWorldTransform() for correct hierarchical transforms
  - Fix creature mesh positioning: skin meshes skip transform (bind-pose), trimeshes use full hierarchy
  - Reparent composite body part meshes to prevent double-transform from original MDL hierarchy
  - Add texture fallback for placeholder bitmaps (mesh node names → model texture)
  - Add grayscale TGA support (image type 3/11) for ghost creature textures (allip, wraith)
  - Change untextured mesh color from red to neutral gray
  - Fixed: boar, beetle, frost giant, chicken, curst warrior, ogre elite, allip, wraith, duergar
- [x] #1031 - Appearance panel missing tattoo display (completed in prior sprint)

---

## [0.1.76-alpha] - 2026-02-21
**Branch**: `quartermaster/issue-1441` | **PR**: #1443

### Sprint: Tech Debt - File Splits (#1441)

- [x] #1432 - Split NewCharacterWizardWindow.axaml.cs (3856→758 lines, 9 partials)
- [x] #1380 - Split MainWindow.axaml.cs (1568→742 lines, 9 partials total)
- [x] #1125 - Split CharacterPanel.axaml.cs (903→613 lines, 3 partials)
- [x] #1417 - Split CreatureDisplayService.cs (915→655 lines, 2 partials)
- [x] #1418 - Split MainWindow.FileOps.cs (900→713 lines, 2 partials)
- [x] #1370 - Split SettingsWindow.axaml.cs (785→463 lines, 2 partials)

---

## [0.1.75-alpha] - 2026-02-21
**Branch**: `quartermaster/issue-1438` | **PR**: #1439

### Sprint: New Character Wizard Bug Fix & Polish (#1438)

- [x] #1430 - Default portrait based on gender (hu_m_99_ for male, hu_f_99_ for female)
- [x] #1428 - Populate inventory UI immediately after wizard creation
- [x] #1427 - Fix domain iteration: skip blank rows instead of stopping early
- [x] #1421 - Clear GPU texture cache on color change so preview refreshes
- [x] #1429 - Replace voice set ComboBox with SoundsetBrowserWindow
- [x] #1426 - Add auto-detection fallback for vault paths in CreatureBrowserWindow

---

## [0.1.74-alpha] - 2026-02-21
**Branch**: `quartermaster/issue-1412-1423` | **PR**: #1425

### Sprint: Feat Selection & Starting Equipment (#1423, #1412)

#### Feat Selection Step (#1423)

- [x] Add Step 6: Feats — two-panel UI (available ↔ selected), mirroring spell selection
- [x] Filter available feats by prerequisites (ability scores, race, class)
- [x] Show granted feats (race + class) as read-only pre-selected
- [x] Allow picking 1 general feat (+ 1 Human bonus + 1 Fighter bonus if applicable)
- [x] Auto-Assign button reads package feat preferences (`FeatPref2DA` in `packages.2da`)
- [x] Renumber Steps 6-8 → 7-10 (sidebar, navigation, all references)
- [x] Update `TotalSteps` constant from 8 to 10
- [x] Update `BuildCreature()` to include player-chosen feats alongside granted feats
- [x] Validate feat prerequisites against current wizard state

#### Starting Equipment Step (#1412)

- [x] Add equipment step using `packeq*.2da` package equipment tables
- [x] Load Package Defaults button reads `packeq*.2da` equipment tables
- [x] Equipment slot display resolves bitmask to primary slot name
- [x] Items split into equipped slots vs backpack based on `EquipableSlots`
- [x] Clear All button to reset equipment
- [x] Step is optional — user can skip and equip later in editor

#### Additional Enhancements

- [x] Save As dialog on Create — BIC defaults to local vault, UTC to module path
- [x] Voice set picker populated from `soundset.2da`
- [x] Cleric domain selection on Class & Package step (reads `domains.2da`)
- [x] Domain1/Domain2 fields added to CreatureClass in Radoub.Formats
- [x] Name, Last Name, Age (BIC only), Description fields on identity step
- [x] Default NWN scripts applied to UTC files

---

## [0.1.73-alpha] - 2026-02-19
**Branch**: `quartermaster/issue-1411` | **PR**: #1422

### Sprint: New Character Wizard - Steps 7-8 (Spells & Summary) (#1411)

- [x] Step 7: Spells — Skip for non-casters via `PrepareCurrentStep()` recursion
- [x] Spontaneous casters (Bard, Sorcerer): select known spells up to `GetSpellsKnownLimit()`
- [x] Divine casters (Cleric, Druid): auto-granted, show informational list
- [x] Wizard class: select spellbook spells
- [x] Two-panel UI: available spells (filtered by class + level, with search) ↔ selected spells
- [x] Auto-Assign button reads `packsp*.2da` spell preferences
- [x] Step 8: Summary & Create — Read-only review with [Edit] links to navigate back
- [x] Name field (FirstName) + auto-generated Tag/ResRef (16-char limit, lowercase, underscores)
- [x] UTC: PaletteID selection
- [x] BuildCreature() method — produces complete UtcFile with all wizard selections
- [x] Granted feats from race + class (via FeatService)
- [x] Proper SkillList sizing, SpellList population
- [x] All UtcFile fields set from 2DA data (no hardcoded defaults)
- [x] Fix: `GetMaxSpellLevel()` used wrong 2DA column names (`NumSpellLevels{n}` → `SpellLevel{n}`)

---

## [0.1.72-alpha] - 2026-02-19
**Branch**: `quartermaster/issue-1410` | **PR**: #1419

### Sprint: New Character Wizard - Steps 5-6 (Abilities & Skills) (#1410)

- [x] Step 5: Ability Scores — 6-row grid (STR/DEX/CON/INT/WIS/CHA) with [-][+] buttons, Base/Racial/Final/Modifier/Cost columns
- [x] Point-buy system: 30 points, base 8, cap 18 with cost table
- [x] Points remaining counter with color feedback (green/neutral/error)
- [x] Racial modifiers display (green/warning via BrushManager)
- [x] Step 6: Skills — Skill allocation grid with [-][+] buttons, class/cross-class distinction, search filter
- [x] Cross-class skills cost 2 points, class skills cost 1; unavailable skills grayed out
- [x] Skill points formula: (ClassSkillPointBase + INT modifier) × 4 at level 1 (+4 for Human)
- [x] Auto-Assign buttons for both steps — abilities from package `Attribute` column, skills from `SkillPref2DA`
- [x] Prestige prerequisite advisory banner on ability scores
- [x] HP calculation includes CON modifier from assigned ability scores
- [x] BuildCreature() outputs ability scores and skill list from wizard state

---

## [0.1.71-alpha] - 2026-02-19
**Branch**: `quartermaster/issue-1409` | **PR**: #1415

### Sprint: New Character Wizard - Steps 3-4 (Appearance, Class & Package) (#1409)

- [x] Step 3: Appearance — Appearance ComboBox from `appearance.2da`, phenotype ComboBox, portrait browser integration, body part controls (head, skin/hair/tattoo colors)
- [x] Step 3: All 18 body parts — Head, neck, torso, pelvis, belt, shoulders, biceps, forearms, hands, thighs, shins, feet in 2-column layout
- [x] Step 3: Color picker swatches — Clickable color swatches next to numeric controls; opens palette color picker window (skin, hair, tattoo 1/2)
- [x] Step 3: Portrait filter pre-population — Portrait browser auto-selects race/gender filters from wizard state
- [x] Step 3: Visualization advisory note — Informs user about post-creation appearance panel features
- [x] Step 4: Class & Package — Class ListBox from `classes.2da` (BIC: player classes only), class detail panel, racial favored class badge
- [x] Starting Package — ComboBox filtered by selected class (`packages.2da`), add `GetPackagesForClass()` to `AppearanceService`
- [x] Prestige Planning — Collapsible advisory panel showing prestige class prerequisites from `cls_pres_*.2da`

---

## [0.1.70-alpha] - 2026-02-19
**Branch**: `quartermaster/issue-1408` | **PR**: #1413

### Sprint: New Character Wizard - Skeleton + Steps 1-2 (#1408)

- [x] Wizard window skeleton with 8-step sidebar, navigation, button bar
- [x] Step 1: File Type — PC/NPC toggle cards
- [x] Step 2: Race & Sex — Race list from 2DA, racial stat block, sex toggle
- [x] Add `GetPlayerRaces()`, `GetFavoredClass()`, `GetRaceSizeCategory()` to CreatureDisplayService
- [x] Wire wizard into `NewFile()` in MainWindow
- [x] Fix `CreateNewCreature()` Class 7 bug (7 is Ranger, not Commoner; real Commoner is 255)

---

## [0.1.69-alpha] - 2026-02-16
**Branch**: `radoub/issue-1377` | **PR**: #1403

### Sprint: TLK Language & Delete Files (#1377)

- [x] #1363 - Add TLK language toggle (View > Language menu)
- [x] #1368 - Delete creature/character files from module (context menu in browser panel)

---

## [0.1.68-alpha] - 2026-02-15
**Branch**: `quartermaster/issue-1353` | **PR**: #1379

### Sprint: Inventory & Palette Bug Bash (#1353)

- [x] #1215 - Item palette is now read-only (DataGrid `IsReadOnly=True`)
- [x] #1214 - Items default to not droppable (`IsDropable` default changed from true to false)
- [x] #1213 - Creature-only items (skins, claws) can now equip to natural slots
- [x] #1211 - Item details panel works for equipment slots, backpack, and palette
- [x] #1210 - Removed duplicate checkbox column; single row selection mechanism

---

## [0.1.67-alpha] - 2026-02-11
**Branch**: `radoub/issue-1259` | **PR**: #1311

### Sprint: Cross-Tool Inventory Unification (#1259)

Shared inventory patterns with Fence, context menus, equipment slot operations.

---

## [0.1.66-alpha] - 2026-02-02
**Branch**: `quartermaster/issue-1206` | **PR**: #1207

### Feature: Roundtrip Read/Write (#1206)

Complete the read-write implementation for all creature fields. Currently inventory is read-only while spells, feats, and levels are writable.

---

## [0.1.65-alpha] - 2026-02-02
**Branch**: `quartermaster/issue-1174` | **PR**: #1200

### Bug: Static appearance creatures render incorrectly (#1174)

Investigation and fixes for static appearance models (animals, bandits, dragons) that don't render correctly in the 3D preview. This is expected to be multi-PR work.

#### Changes

**Transform Improvements (ModelPreviewGLControl.cs):**
- Accumulate position offsets from entire parent chain (fixes beetle legs)
- Apply mesh's own rotation to vertices and normals (fixes troll legs)
- Filter NaN vertices from skin nodes (parsing artifact)
- Skip faces that reference NaN vertices

**Models That Now Render Better:**
- ✅ Beholder - mostly renders correctly (eyestalks attached)
- ✅ Troll - legs now visible (had 180° rotation)
- ✅ Beetles - legs attached to body
- ✅ Polar bear - renders well
- ✅ Most humanoids, constructs, bears, cats

**Known Remaining Issues:**
- ❌ Fairy - midsection renders horizontal instead of vertical
- ❌ Dragon - feet and some wing parts broken
- ❌ Some models appear red (satyr, beholder mouth) - texture/material issue
- ❌ Ettin - still upside down

**Root Cause Analysis:**
NWN models use inconsistent transform conventions - some meshes have vertices in LOCAL space (centered at origin), some in WORLD space (already offset). The current approach improves many models but cannot fix all without model-type-specific handling.

**Diagnostic Tests Added:**
- `AppearanceAnalysisTests.DebugProblematicModels()` - analyze mesh transforms
- `AppearanceAnalysisTests.AnalyzeSkinNodes()` - inspect bone weight data
- `AppearanceAnalysisTests.AnalyzeModelHierarchy()` - print node hierarchy

---

## [0.1.64-alpha] - 2026-02-01
**Branch**: `quartermaster/issue-1183` | **PR**: #1189

### Sprint: QM Cleanup (#1183)

- [x] #867 - Browse buttons positioned adjacent to fields (AdvancedPanel, CharacterPanel)
- [x] #820 - Skills legend uses WrapPanel to handle narrow screens
- [x] #1019 - Comment field already exposed in AdvancedPanel (verified)
- [x] #773 - Filter padding/placeholder entries from item Type dropdown (Radoub.UI)

---

## [0.1.63-alpha] - 2026-02-01
**Branch**: `quartermaster/issue-1171` | **PR**: #1172

### Feature: Restart MDL Format Implementation (#1171)

Restart MDL (model) format implementation from scratch for Quartermaster's appearance preview.

#### Scope
- Remove or archive current MDL implementation
- Research MDL format specification thoroughly
- Implement MDL ASCII reader with comprehensive validation
- Implement MDL binary reader (if needed)
- Add unit tests with known-good model files
- Integrate with Quartermaster appearance preview

---

## [0.1.62-alpha] - 2026-01-31
**Branch**: `quartermaster/issue-1145` | **PR**: #1169

### Feature: Integrate CreatureBrowserPanel as collapsible left panel (#1145)

Add a collapsible left panel to Quartermaster's MainWindow that displays all .utc/.bic files from the current module, vaults, and HAKs, enabling single-click navigation.

#### Features Implemented
- [x] Search box at top of panel (inherited from FileBrowserPanelBase)
- [x] Source checkboxes: Module, LocalVault, ServerVault, HAK
- [x] Current file highlighted when loading a creature
- [x] Single-click → auto-save + load
- [x] Collapsible via View menu (F4 shortcut) or collapse button
- [x] Panel width persisted in settings

#### HAK Support
- Added HAK scanning for .utc files (ResourceTypes.Utc = 2027)
- Uses same caching pattern as StoreBrowserPanel
- Static cache persists across panel instances for performance

---

## [0.1.61-alpha] - 2026-01-29
**Branch**: `quartermaster/issue-1057` | **PR**: #1152

### Sprint: Stats & Display Polish (#1057)

Improve Quartermaster's display of combat-relevant statistics and complete appearance panel functionality.

#### Work Items
- [x] #1032 - Display AB/APR in Stats Panel
- [x] #1031 - Appearance panel missing tattoo display
- [x] #1053 - Metamagic slot counting by effective level

#### #1031 - Tattoo Color Controls Always Enabled
- Moved Colors section (Skin, Hair, Tattoo1, Tattoo2) outside Body Parts section
- Colors now always enabled regardless of appearance type (static vs part-based)
- Previously, color controls were disabled for static appearances (non-part-based creatures)
- Added tooltips explaining that tattoo colors only affect pixels painted in body PLT textures
- Note: NWN doesn't have tattoo "style" selection - patterns are baked into model textures

#### #1032 - Display AB/APR in Stats Panel
- Added Attacks Per Round (APR) display to Combat section
- Shows APR calculated from BAB: 1 attack at BAB 1-5, +1 per 5 BAB, max 4 at BAB 16+
- Displays attack sequence string (e.g., "+16/+11/+6/+1") as tooltip/subtitle
- Combat section now shows: Base Attack, Attacks/Round, Challenge Rating

#### #1053 - Metamagic Slot Counting by Effective Level
- Memorized Spells table now counts slots at effective level (base + metamagic cost)
- Example: Level 3 Fireball with Extend (+1) now consumes a level 4 slot
- Added `GetMetamagicLevelCost()` helper: Empower +2, Extend +1, Maximize +3, Quicken +4, Silent +1, Still +1
- Added `GetEffectiveSpellLevel()` helper for base + metamagic calculation
- NWN stores spells at base level with metamagic flag; game calculates effective level at runtime

---

## [0.1.60-alpha] - 2026-01-29
**Branch**: `quartermaster/issue-1126` | **PR**: #1147

### Refactor: Split StatsPanel.axaml.cs (#1126)

Split the 830-line StatsPanel.axaml.cs into focused partial classes for better maintainability.

#### Changed
- Split `StatsPanel.axaml.cs` (830 LOC) into 4 partial class files:
  - `StatsPanel.axaml.cs` (314 LOC) - Core initialization, loading, field definitions
  - `StatsPanel.Abilities.cs` (245 LOC) - STR/DEX/CON/INT/WIS/CHA handling, ability points summary
  - `StatsPanel.Combat.cs` (209 LOC) - HP, AC, BAB, CR calculations and display
  - `StatsPanel.Saves.cs` (101 LOC) - Fortitude, Reflex, Will saving throw handling

---

## [0.1.59-alpha] - 2026-01-28
**Branch**: `quartermaster/issue-1137` | **PR**: #1140

### Sprint: Code Quality Improvements (#1137)

Non-breaking quality improvements from recent code review findings.

#### Work Items
- [x] #1122 - Replace bare catch blocks with specific exception handling
- [x] #1123 - Fix unsafe null-forgiving operators
- [x] #1127 - Add cancellation tokens to fire-and-forget async operations
- [x] #1128 - Code quality improvements (duplication, logging, validation)

#### Details

**#1122 - Bare catch blocks** (11 instances fixed):
- App.axaml.cs, SettingsWindow.axaml.cs: Font handling (ArgumentException)
- SpellListViewModel, FeatListViewModel, SkillsPanel, InventoryPanel: Icon loading
- ModularPaletteCacheService: Cache file operations (IOException, JsonException)
- AppearanceService: Faction file parsing

**#1123 - Null-forgiving operators**:
- Designer constructors marked [Obsolete(error: true)]
- TextureService cache uses nullable Dictionary
- MainWindow service fields documented as safe (guaranteed initialization)

**#1127 - Cancellation tokens**:
- CancellationTokenSource pattern for MainWindow async operations
- Token propagation through LoadPaletteItemsAsync, InitializeCachesAsync
- Proper cancellation on window close

**#1128 - Parameter validation**:
- Added ArgumentNullException.ThrowIfNull to service constructors:
  FeatService, ClassService, CreatureDisplayService, CharacterSheetService, ItemIconService

---

## [0.1.58-alpha] - 2026-01-27
**Branch**: `quartermaster/issue-1124` | **PR**: #1136

### Refactor: Split FeatsPanel.axaml.cs (#1124)

Split the 1,195-line FeatsPanel.axaml.cs into focused partial classes for better maintainability.

#### Changed
- Split `FeatsPanel.axaml.cs` (1,195 LOC) into 5 partial class files:
  - `FeatsPanel.axaml.cs` (435 LOC) - Core initialization, loading, ViewModel creation
  - `FeatsPanel.Search.cs` (80 LOC) - Search and filter functionality
  - `FeatsPanel.Display.cs` (320 LOC) - Summary display, assigned feats list, theme helpers
  - `FeatsPanel.Selection.cs` (99 LOC) - Feat add/remove operations
  - `FeatsPanel.SpecialAbilities.cs` (172 LOC) - Special abilities management
- Moved `FeatListViewModel` (177 LOC) to `ViewModels/FeatListViewModel.cs`
- Moved `SpecialAbilityViewModel` (56 LOC) to `ViewModels/SpecialAbilityViewModel.cs`

All files now under 500 LOC threshold. No functional changes.

---

## [0.1.57-alpha] - 2026-01-27
**Branch**: `quartermaster/issue-1036` | **PR**: #1135

### Feature: Item Palette Modular Caching (#1036)

Refactor item palette caching to use per-source cache files with independent invalidation.

#### Added
- `ModularPaletteCacheService` with per-source granularity (BIF, Override, HAK)
- Cache tab in Settings UI with status, item count, size, and per-source breakdown
- "Clear and Reload Cache" button that rebuilds cache and refreshes UI
- `SetPaletteItems()` method for efficient bulk palette loading

#### Changed
- BIF cache invalidates only when `BaseGameInstallPath` changes
- Override cache invalidates only when `NeverwinterNightsPath` changes
- HAK caches track individual file modification times
- Module folder items scanned fresh (no caching - already unpacked)
- Removed batched UI loading with `Task.Delay()` - now adds all items at once

#### Removed
- Old monolithic `PaletteCacheService` (replaced by modular version)

---

## [0.1.56-alpha] - 2026-01-25
**Branch**: `quartermaster/issue-1055` | **PR**: #1129

### Feature: Migrate 3D Renderer to Silk.NET/OpenGL (#1055)

Replaced the SkiaSharp-based CPU renderer with a GPU-accelerated OpenGL renderer using Silk.NET.

#### Added
- `ModelPreviewGLControl` - GPU-accelerated 3D preview control using Avalonia's `OpenGlControlBase`
- GLSL vertex and fragment shaders with per-pixel lighting
- Proper depth buffer for correct occlusion (no more painter's algorithm)
- Perspective-correct texture mapping (GPU handles this automatically)
- Per-mesh texture binding for PLT textures
- Mipmap generation for better texture quality at distance

#### Changed
- AppearancePanel now uses `ModelPreviewGLControl` instead of `ModelPreviewControl`
- 3D model rotation/zoom controls updated to use new renderer

#### Removed
- `ModelPreviewControl` - Old SkiaSharp-based CPU renderer
- `ModelRenderOperation` - ICustomDrawOperation for SkiaSharp
- Painter's algorithm depth sorting code

---

## [0.1.55-alpha] - 2026-01-24
**Branch**: `radoub/issue-1096` | **PR**: #1101

### Sprint: Custom File Browsers (#1096)

- [x] #1083 - Use custom file browser for .utc/.bic files
  - File > Open now uses `CreatureBrowserWindow` from Radoub.UI
  - Shows creatures from current module directory
  - Filter by file type: All, Creature Blueprints (.utc), Player Characters (.bic)
  - Browse button to select different module folder
  - Consistent UX with other Radoub tools

---

## [0.1.54-alpha] - 2026-01-23
**Branch**: `quartermaster/issue-1046` | **PR**: #1062

### Tech Debt: Large Files Needing Refactoring (#1046)

- [x] MainWindow.axaml.cs - Removed unused `ResolveConversationPath` method and consolidated `StripCharacterToLevelOne` duplicate
- [x] MainWindow.Inventory.cs - Added null checks to fix CS8604 warnings
- [x] ModelService.cs - Removed 3 unused methods: `LoadBodyPartModel`, `GetRaceModelRef`, `IsPartBasedAppearance` (duplicated in AppearanceService)
- [x] Reviewed MainWindow.FileOps.cs, AdvancedPanel.axaml.cs, UtcFile.cs - already well-organized, no extraction needed

---

## [0.1.53-alpha] - 2026-01-22
**Branch**: `quartermaster/issue-1050` | **PR**: #1056

### Sprint: Bug Bash - UI Fixes (#1050)

- [x] #938 - Portrait resizes with font scaling (uses dynamic PortraitWidth/PortraitHeight resources)
- [x] #1021 - Re-level confirmation dialog now has OK/Cancel buttons and scrollable content
- [x] #1026 - Bad strref values filtered in item type names (shared fix in Radoub.UI)
- [x] #1049 - Item Palette shows formatted type names instead of raw BaseRef values

---

## [0.1.52-alpha] - 2026-01-22
**Branch**: `quartermaster/issue-746` | **PR**: #1054

### Feature: Appearance Preview Rendering (#746)

- Add visual preview of creature appearance in the Appearance panel

---

## [0.1.51-alpha] - 2026-01-22
**Branch**: `quartermaster/issue-1051` | **PR**: #1052

### Sprint: Creature Editing - Stats & Abilities (#1051)

- #588 - Add item details panel with icon display
- #728 - Edit Movement Speed
- #730 - Edit Base Saving Throws
- #733 - Edit Levelup Package
- #735 - Edit Special Abilities (Spell-Like Abilities)
- #739 - Metamagic Support for Spells

---

## [0.1.50-alpha] - 2026-01-21
**Branch**: `quartermaster/issue-1016` | **PR**: #1045

### Sprint: Variables & Metadata Fields (#1016)

#### Refactored (Already Completed)
- **BindableBase migration** (#926) - Migrated `FeatListViewModel` and `SkillViewModel` to inherit from `BindableBase`, replacing ~100+ lines of manual `INotifyPropertyChanged` boilerplate with `SetProperty()` calls
- **Theme color consolidation** (#925) - Consolidated duplicated theme-aware color methods (`GetDisabledBrush()`, `GetSuccessBrush()`, `GetInfoBrush()`, etc.) from FeatsPanel, SkillsPanel, and SpellsPanel into `BasePanelControl`

#### Added
- **Variables panel** (#946) - Add Variables, Category, and Comment fields for creature editing
- **INI script import** (#952) - Import script names from INI file for complex creatures

---

## [0.1.49-alpha] - 2026-01-20
**Branch**: `quartermaster/issue-586` | **PR**: #1034

### Feature: Item Palette Loading and Caching (#586)

Performance improvements for item palette - caching, background loading, and responsive UI.

#### Changed
- **On-demand palette loading** - Item palette now loads only when navigating to Inventory panel, not at app startup
  - Eliminates UI thread blocking from batch ObservableCollection updates
  - Tab navigation is now immediately responsive
- **Disk caching** - Item metadata cached to `~/Radoub/Quartermaster/palette_cache.json`
  - Cache pre-warms in background on startup (no UI impact)
  - Cache invalidates automatically when game paths change
  - Subsequent loads are near-instant from cache
- **Background cache building** - Full cache built on background thread
  - No UI updates during cache build
  - Standard items loaded first (visible immediately with default filter)
- **Batched UI loading** - Items added in batches of 100 with yields
  - UI remains responsive during palette population
  - Progress shown in status bar
- **Filter defaults** - "Show Custom" unchecked by default (CEP adds thousands)

#### Fixed
- **Tab unresponsiveness** - Clicking sidebar tabs during palette loading no longer blocks
  - Root cause: Background item loading was posting batch updates to UI thread
  - Fix: Defer all UI population until user navigates to Inventory panel
- **Palette persists across files** - Palette no longer disappears when opening second creature file

---

## [0.1.48-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-951` | **PR**: #1029

### Feature: Complete Creature Appearance UI (#1028)

Add skin to the creature body - appearance customization for body parts, skin/hair colors, and armor-provided limbs.

#### Added
- **Textured 3D model rendering** - Model preview now renders with proper texture mapping instead of flat shading
  - UV coordinates extracted from MDL mesh data
  - SKVertices-based GPU-accelerated triangle rendering
  - PLT textures rendered with skin/hair/tattoo colors via TextureService
  - Lighting still applied for depth perception
- **Armor-provided body part overrides** - Equipped chest armor now overrides creature body parts
  - Loads armor's `ArmorParts` dictionary from UTI
  - Applies armor-provided models for torso, arms, legs, etc.
  - Head is not overridden (matching NWN behavior)
- **Full body part model loading** - All NWN body part types now load correctly
  - Correct NWN naming convention: bicepl/bicepr, forel/forer, legl/legr, shinl/shinr, footl/footr, shol/shor
  - Human model fallback for race-specific models not found (pme0 → pmh0)
  - Human texture fallback when race-specific textures missing
  - Texture name derivation for empty bitmap fields
- **PLT armor colors** - Full 10-layer PLT color support
  - PltColorIndices class for all layers (skin, hair, metal1/2, cloth1/2, leather1/2, tattoo1/2)
  - Armor colors loaded from equipped chest armor UTI
  - TextureService refactored to pass all color indices

#### Changed
- **Startup performance** - Moved GameDataService and panel initialization from constructor to `Opened` event, runs on background thread. Cache and item loading run in parallel. Window appears in ~1.9s instead of ~18 seconds.

#### Fixed
- **MDL parser** - Added bounds checking to ParseControllers to prevent exceptions on malformed controller data

#### Known Issues
- Head/face geometry not rendering (needs investigation)
- Armor body parts not rendering (armor overrides may not be applying)
- Tattoos not visible on model (#1031)

---

## [0.1.47-alpha] - 2026-01-19
**Branch**: `quartermaster/issue-829` | **PR**: #1017

### Sprint: Level-Up Wizard System (#829)

Wizard-based character leveling that enforces D&D 3.5e/NWN rules during level changes.

- [x] Level Up wizard (Character menu, Ctrl+L)
- [x] Class selection with prestige prerequisite validation
- [x] Feat selection with prerequisite enforcement
- [x] Skill allocation with class/cross-class point costs
- [x] Spell selection step (deferred - shows guidance)
- [x] Re-Level: Strip character to level 1 for rebuild
- [x] Down-Level: Save a level 1 copy to new file

#### Added
- **ClassService**: Comprehensive class data service for prestige prerequisites
  - Parses `cls_pres_*.2da` for all prestige requirement types (FEAT, SKILL, BAB, etc.)
  - Validates alignment restrictions via bitmask columns
  - `GetAvailableClasses()` returns selectable classes with qualification status

- **ClassBrowserWindow**: Class selection browser with prerequisite display
  - Left panel: Filterable class list with prestige badge
  - Right panel: Class description, hit die, skill points, prerequisite checklist

- **LevelUpWizardWindow**: 5-step wizard for adding a level
  - Step 1: Class selection (prestige classes greyed if unqualified)
  - Step 2: Feat selection with [Y]/[N] prerequisite indicators
  - Step 3: Skill allocation with class/cross-class cost tracking
  - Step 4: Spell selection (deferred with guidance message)
  - Step 5: Summary showing all choices and stat changes

- **Character Menu**: New menu with Level Up, Re-Level, Down-Level options
- **Re-Level**: Strips creature to level 1 (first class), removes choosable feats/skills
- **Down-Level (Save Copy)**: Creates a level 1 copy without modifying original

#### Technical Notes
- No hardcoding: All class/feat/skill data from 2DA files via GameDataService
- Prestige prerequisite types supported: FEAT, FEATOR, SKILL, BAB, RACE, ARCSPELL, DIVSPELL, CLASSOR
- Cross-class skills cost 2 points per rank (1 point for class skills)
- Human bonus: +1 feat at level 1, +1 skill point per level
- Feat chains: Selecting a feat re-evaluates prerequisites (e.g., Dodge unlocks Mobility)

---

## [0.1.46-alpha] - 2026-01-19
**Branch**: `quartermaster/issue-884` | **PR**: #1012

### Sprint: Bug Bash - Settings & UI Fixes (#884)

- [x] #873 - Settings: Changing font to system font has no effect

#### Fixed
- **System font selection now applies correctly** (#873)
  - When "(System Default)" is selected, `GlobalFontFamily` is now explicitly set to `FontFamily.Default`
  - Previously, empty font string caused no action, leaving the previous font active
  - Fix applied to all 5 tools: Quartermaster, Trebuchet, Manifest, Fence (Parley already had the fix)
  - Also fixed in ThemeManager for themes specifying `$Default` or empty font

---

## [0.1.45-alpha] - 2026-01-19
**Branch**: `radoub/sprint-988-1010` | **PR**: #1011

### Sprint: Fix and Stabilize Integration Tests (#1010)

- [x] Run full integration test suite and document all failures
- [x] Root cause analysis: Tests were using user's actual NWN installation (82 HAK files scanned = slow startup)
- [x] Fix FlaUITestBase to pre-seed `BaseGameInstallPath` preventing AutoDetect from finding user's game

- [x] Update SpellsPanel test to match current UI (ComboBox instead of radio buttons)
- [x] Skip tests requiring game data (classes.2da, packages.2da) not in test environment
- [x] Add parleypirate.utc to test data for creature-dependent tests

#### Results
- **Before**: 0/15 tests passing (all failed due to slow startup / window detection timeout)
- **After**: 12/15 tests passing, 3 skipped (valid reasons)
- **3 Skipped**: 2 require game data (picker tests), 1 Avalonia Expander issue

---

## [0.1.44-alpha] - 2026-01-18
**Branch**: `quartermaster/issue-715` | **PR**: #948

### Sprint: Core Features & Workflow (#715)

- [x] #690 - Convert between UTC and BIC (Save As) - **Complete**
  - ✅ UTC→BIC conversion initializes QuickBar with 36 empty slots
  - ✅ Set reasonable default Age (25) for converted characters
  - ✅ Ensure HP is valid (dead creatures get CurrentHP = MaxHP)
  - ✅ UI reloads panels after format conversion
  - ✅ BIC→UTC sets PaletteID=1 (Custom category) so creatures appear in toolset palette
  - ✅ Added 19 unit tests for conversion validation (9 ToUtcFile, 10 FromUtcFile)
- [x] #689 - Create new UTC file - **Complete**
  - ✅ File > New menu item with Ctrl+N shortcut
  - ✅ Creates new creature with sensible defaults (Human Commoner level 1)
  - ✅ Prompts to save unsaved changes before creating new file
  - ✅ Ctrl+S on new file redirects to Save As dialog
  - ✅ Sets PaletteID=1 (Custom category) so creatures appear in toolset palette
  - ✅ Palette Category dropdown on Advanced panel for Aurora Toolset compatibility
- [ ] #949 - QuickBar Panel (BIC only) - **Read-only**
  - ✅ QuickBar nav button visible only for BIC files
  - ✅ Displays 36 slots organized as 3 bars × 12 slots (F1-F12)
  - ✅ Shows slot type and ID for assigned slots
  - ❌ Cannot edit/write QuickBar slots yet
- [x] #626 - Wire up themes and preferences/settings
- [x] #643 - Faction Display & Management

### Bug Fixes
- Fixed BIC portrait display (uses Portrait string when PortraitId=0)
- Fixed BIC→UTC conversion preserving Portrait string (character's actual portrait) instead of overwriting with default
- Fixed Aurora Toolset "must specify valid portrait" error on BIC→UTC conversion (looks up PortraitId from Portrait string via portraits.2da)
- Added Portrait ID and Portrait ResRef fields to Character panel for debugging portrait issues
  - UTC files: Portrait ID enabled (primary), Portrait ResRef grayed out
  - BIC files: Portrait ResRef enabled (primary), Portrait ID grayed out
- Fixed bitmap cache crash when switching panels (removed premature disposal)
- Added global exception handlers for crash diagnostics
- Added ITP reader for palette categories

---

## [0.1.43-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-932` | **PR**: #937

### Sprint: Spell System Polish II (#932)

- [x] #775 - Add toggle for class/race spell restrictions
  - "Ignore class restrictions" checkbox in Spells panel filter toolbar
  - When checked: All spells shown as available, uses innate level for non-class spells
  - When unchecked (default): Standard behavior with class spell list restrictions
  - Summary tables update to count all known spells when ignoring restrictions

Note: #776 and #777 completed in v0.1.42-alpha (#936)

---

## [0.1.42-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-885` | **PR**: #936

### Sprint: Spells Panel Polish (#885)

- [x] #816 - Spells panel filter toolbar overflow (completed in v0.1.39)
- [x] #815 - Spells panel class selector overflow (completed in v0.1.39)
- [x] #776 - Support multiple memorizations of same spell
  - Replaced memorized checkbox with +/- buttons and count display
  - Shows memorization count (e.g., "3" for three memorizations)
  - Status column shows "M×3" for multiple memorizations
  - Tracks spell memorization counts correctly in model for save/load
  - Spontaneous casters (Bard, Sorcerer) show dash instead of controls
- [x] #777 - Add Clear All Spells option
  - "Clear All" button in Spell Actions section
  - Confirmation dialog showing count of spells to be cleared
  - Clears both known and memorized spells for selected class

---

## [0.1.41-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-931` | **PR**: #933

### Sprint: Browsers & Selection UX (#931)

- [x] #928 - Portrait browser with race/gender filtering
  - Filter by race and gender, search by resref name
  - Visual preview of selected portrait
  - Browse button next to portrait dropdown in Character panel
- [x] #803 - Soundset browser with filter and preview playback
  - Search/filter soundsets by name and gender (Male/Female/Other)
  - Preview panel with sound type selector (Hello, Goodbye, Attack, etc.)
  - Unavailable sound types greyed out with 0.5 opacity
  - Play/Stop buttons for soundset preview
  - Browse button next to soundset dropdown in Character panel
- [x] #928 - Portrait browser mini icons (32x40 thumbnails in WrapPanel grid)
- [x] CharacterPanel soundset preview: unavailable types greyed out

---

## [0.1.40-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-922` | **PR**: #927

### Sprint: Portrait & Character Preview (#922)

- [x] #919 - Add portrait selection/change functionality
  - Portrait moved from Appearance panel to Character panel (Identity section)
  - Sidebar portrait display refreshes when portrait is changed
  - Phenotype selection wired up to update model preview
- [x] #914 - Display NPC portrait in character preview (completed in #916)
- [x] #768 - Add Sex/Gender column to Appearance panel
  - Added Gender dropdown to Model section (Male/Female options from gender.2da)
  - Gender changes update the 3D model preview
  - Supports non-standard gender values if present in creature files

---

## [0.1.39-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-923` | **PR**: #924

### Sprint: UI Polish & Bug Fixes (#923)

- [x] #801 - Settings window is now non-modal (users can interact with main window)
- [x] #837 - Font family changes now refresh all panels (Feats, Skills, Spells)
- [x] #839 - Path settings now validate in real-time as user types
- [x] #815 - Replaced class radio buttons with ComboBox for narrow screen compatibility
- [x] #816 - Compacted filter toolbar labels and reduced MinWidths for better wrapping

---

## [0.1.38-alpha] - 2026-01-16
**Branch**: `parley/issue-916` | **PR**: #918

### Enhancement: Portrait display and soundset preview (#916)

- Character portrait now displays in the sidebar when a creature is loaded
- Uses `ImageService.GetPortrait()` which loads from BIF archives
- Shows "No Portrait" placeholder when portrait unavailable
- Added soundset preview controls to Voice & Dialog section
- Dropdown allows selecting sound type (Hello, Goodbye, Attack, Death, etc.)
- Play button loads sound from SSF and plays via AudioService

---

## [0.1.37-alpha] - 2026-01-16
**Branch**: `quartermaster/issue-778` | **PR**: #912

### Refactor: SpellsPanel.axaml.cs Tech Debt (#778)

Refactor SpellsPanel.axaml.cs for improved maintainability (1232 lines).

---

## [0.1.36-alpha] - 2026-01-15
**Branch**: `radoub/issue-908` | **PR**: #909

### Backlog Cleanup (#802, #819)

- **#802**: Removed duplicate Conversation section from ScriptsPanel (now only in CharacterPanel)
  - Added browse button functionality to CharacterPanel's conversation field
  - CharacterPanel now receives GameDataService and file path for dialog browsing
- **#819**: Added "Not yet implemented" tooltips to disabled Undo/Redo/Cut/Copy/Paste menu items

---

## [0.1.35-alpha] - 2026-01-11
**Branch**: `quartermaster/sprint-630-871-870` | **PR**: #872

### Sprint: Level-Up Resource Tracking (#630, #871, #870)

Resource calculation and tracking for level-up workflow.

#### Work Items
- [x] #870 - Track Ability Score Increases from Leveling
- [x] #871 - Calculate Available Resources on Level-Up (HP, Skills, Feats, Spells)
- [x] #630 - Feats Panel Filter & UX Tuning

---

## [0.1.34-alpha] - 2026-01-11
**Branch**: `quartermaster/issue-791` | **PR**: #869

### Feat: Classes Panel MVP - Add Class & Level-Up (#791)

Implementation of Classes Panel MVP (Approach D - Phase 1). Add-only workflow: level-up and add class functionality without level-down or class removal.

#### Features
- Wire up + button click handlers for level-up
- Add Class button with ClassPickerWindow
- Level constraint validation (40 total, MaxLevel per class)
- BAB and save recalculation on level-up
- Total level display with cap warning

---

## [0.1.33-alpha] - 2026-01-11
**Branch**: `quartermaster/issue-864` | **PR**: #865

### Sprint: Classes Panel Prerequisites (#864)

Establish prerequisites for Classes Panel MVP implementation.

#### Work Items
- [x] #790 - Audit: Remove hardcoded game data (2DA compliance)
- [x] #733 - Edit Levelup Package (packages.2da integration) - *Already implemented*
- [x] #825 - Export Character Sheet (Text/Markdown)

#### 2DA Compliance (#790)

Refactored UI panels to use service methods instead of duplicated hardcoded fallbacks.

**CreatureDisplayService**
- Added `GetClassHitDie()` method - reads HitDie column from classes.2da

**ClassesPanel.axaml.cs**
- Removed 90+ lines of hardcoded class data (names, hit dice, skill points)
- Now delegates to CreatureDisplayService for all class lookups
- Class features text retained as acceptable hardcoding (display-only flavor text not in 2DA)

**SkillsPanel.axaml.cs**
- Removed 70+ lines of hardcoded skill data (names, key abilities, skill points)
- Now delegates to SkillService via CreatureDisplayService

**Pattern established**: Services (CreatureDisplayService, SkillService, FeatService) try 2DA first with minimal fallbacks. UI panels must use services - no duplicate fallback code.

#### Character Sheet Export (#825)

New CharacterSheetService generates formatted character sheets for reference.

**Features**
- Export to plain text (.txt) with ASCII formatting
- Export to Markdown (.md) with tables
- File > Export Character Sheet menu with submenu options
- Ctrl+Shift+E keyboard shortcut for quick text export

**Content Includes**
- Identity (name, race, gender, alignment, deity, subrace, tag, resref)
- Class progression with hit dice and skill points per level
- Ability scores with racial modifiers
- Combat statistics (HP, AC, BAB, saves)
- Skills with ranks, key ability, and class skill indicator
- Feats grouped by category (Combat, Active, Defensive, Magical, Class/Racial)
- Spells known by class and level (caster classes only)
- Equipment slots with item resrefs
- Scripts (UTC only, not shown for BIC player files)
- Gold and XP (BIC files only)

---

## [0.1.32-alpha] - 2026-01-09
**Branch**: `quartermaster/issue-734` | **PR**: #804

### Feat: Edit Skill Ranks (#734)

Enable skill rank editing via +/- buttons with validation against class skill maximums.

#### Skill Editing
- +/- buttons now functional for each skill row
- Max ranks enforced: class skills = level+3, cross-class = (level+3)/2
- Buttons disable appropriately at min (0) and max rank limits
- Changes immediately update UtcFile.SkillList for save
- SkillsChanged event marks document dirty

#### UI Improvements
- Added "Mod" column showing ability modifier (+X/-X format) with color coding
- Added skill points summary table (left panel) showing:
  - Base skill points per class from 2DA (SkillPointBase column)
  - Points per level calculation (base + INT modifier)
  - Total points spent vs estimated available
  - Warning color when over-allocated
- Summary displays character level and max rank limits
- Tooltips show current max rank or reason for disabled state
- Theme-aware sizing throughout (no hardcoded font sizes)
- Wider columns for better readability
- Footer shows max rank formula reference

---

## [0.1.31-alpha] - 2026-01-07
**Branch**: `quartermaster/issue-799` | **PR**: #800

### Refactor: Hardcoded Colors to Theme System (#799)

Refactor hardcoded colors throughout Quartermaster to use semantic theme colors for colorblind/accessibility support.

#### Theme Infrastructure
- Added `Disabled` color to `ThemeColors` model (auto-registered as `ThemeDisabled`)
- Added `disabled` color to all 8 Quartermaster theme JSON files
- Added derived font size resources: `FontSizeXSmall`, `FontSizeSmall`, `FontSizeNormal`, `FontSizeMedium`, `FontSizeLarge`, `FontSizeXLarge`, `FontSizeTitle`

#### Font Size Accessibility
- Panel headers now use `FontSizeXLarge` (scales with theme font size setting)
- Settings/Appearance headers use `FontSizeLarge`
- DialogHelper About dialog uses `FontSizeTitle`

#### Fixed Panels
- FeatsPanel: Status colors (available/known/granted/unavailable) now use theme colors
- SkillsPanel: Class skill indicators now use theme colors
- SpellsPanel: Spell status colors now use theme colors
- AppearancePanel: Color swatches and model preview container now theme-aware

#### Fixed Dialogs
- SettingsWindow: All border colors now use `ThemeBorder`
- ColorPickerWindow: Swatch borders and selection highlight now theme-aware
- FactionPickerWindow: Border and selection styles now theme-aware
- PackagePickerWindow: Border and selection styles now theme-aware

#### Fixed Controls
- ModelPreviewControl: Background and placeholder text now use theme colors

---

## [0.1.30-alpha] - 2026-01-05
**Branch**: `quartermaster/issue-769` | **PR**: #789

### Refactor: Large File Tech Debt (#769)

Split large files into partial classes for improved maintainability and AI readability.

#### MdlAsciiReader (1083 → 6 files ~200 lines each)

- `MdlAsciiReader.cs` - Core parsing, main loop
- `MdlAsciiReader.NodeParsing.cs` - Node creation and property routing
- `MdlAsciiReader.TrimeshParsing.cs` - Trimesh/mesh properties
- `MdlAsciiReader.ListParsing.cs` - Vertex, face, texture, color, weight lists
- `MdlAsciiReader.TypeParsing.cs` - Light, emitter, reference properties
- `MdlAsciiReader.AnimationParsing.cs` - Animation data
- `MdlAsciiReader.Utilities.cs` - Tokenization and parsing helpers

#### MdlBinaryReader (1071 → 7 files ~150 lines each)

- `MdlBinaryReader.cs` - Core parsing, file header
- `MdlBinaryReader.PointerHelpers.cs` - Memory pointer conversion
- `MdlBinaryReader.NodeParsing.cs` - Node creation, controllers, children
- `MdlBinaryReader.MeshParsing.cs` - Mesh/trimesh data
- `MdlBinaryReader.TypeParsing.cs` - Light, emitter, reference nodes
- `MdlBinaryReader.DataReading.cs` - Vertex, face, texcoord reading
- `MdlBinaryReader.AnimationParsing.cs` - Animation data
- `MdlBinaryReader.Utilities.cs` - Helper methods

#### AppearancePanel (755 → 3 files ~250 lines each)

- `AppearancePanel.axaml.cs` - Fields, constructor, core methods
- `AppearancePanel.DataLoading.cs` - Data loading from services
- `AppearancePanel.EventHandlers.cs` - Event wiring and handlers

#### MainWindow.Inventory (614 → 3 files ~200 lines each)

- `MainWindow.Inventory.cs` - UI population, operations, sync
- `MainWindow.ItemResolution.cs` - UTI file resolution
- `MainWindow.ItemPalette.cs` - Background palette loading

---

## [0.1.29-alpha] - 2026-01-04
**Branch**: `quartermaster/issue-761` | **PR**: #774

### Sprint: Spell System Polish (#761)

Enhance SpellsPanel with multi-class spell slot display and polish spell editing workflow.

- [x] #758 - Add spell slot summary for each caster class
- [x] #737 - Add/Remove Known Spells (already implemented in #756)
- [x] #738 - Add/Remove Memorized Spells (already implemented in #756)

#### Added

- **Spell Slot Table** (left panel) showing spell slots/known limits per class
  - Grid format with spell levels as rows, caster classes as columns
  - Color-coded: gold (full), green (partial), gray (empty/unavailable)
  - Spontaneous casters show "spells known" limits, prepared casters show spell slots
  - Filters out feat-based abilities from slot counts
- **Known Spells List** (left panel, below slot table)
  - Shows ALL caster classes with their spells grouped by level
  - Overlapping spells (same spell in multiple classes) highlighted in gold with ⬥
  - Feat-based abilities marked with asterisk (*) in gray
  - Selected class header highlighted in blue
  - Scrollable for characters with many spells

#### Fixed

- Spell list now loads all spells from spells.2da (fixed early termination bug)
  - Previously stopped at first gap after 100 spells
  - Now scans up to 2000 rows with consecutive-empty detection
  - Supports custom content (CEP, PRC, etc.)

---

## [0.1.28-alpha] - 2026-01-04
**Branch**: `radoub/issue-557` | **PR**: #772

### Sprint: Item Property Search (#557)

Add item property search to ItemFilterPanel. Search/filter items by their properties.

#### Added

- **Property Search** in ItemFilterPanel
  - New "Properties:" text box searches item property strings
  - Debounced input (300ms) for performance
  - Case-insensitive search
  - Clear button to reset property filter
  - Filter state persisted with other filter settings

#### Use Cases

- Find class-restricted items: search "monk" or "wizard"
- Find spell items by level: search "level 3"
- Find items with specific bonuses: search "enhancement" or "+2"
- Find damage types: search "fire" or "cold"

---

## [0.1.27-alpha] - 2026-01-04
**Branch**: `quartermaster/issue-617` | **PR**: #766

### Sprint: 3D Character Model Preview (#617)

Add 3D character model preview to the Appearance tab, showing the character's visual appearance alongside body part configuration.

#### Layout Changes
- Restructure Appearance tab: body parts on left, 3D model preview on right
- Reduce text box widths to respect Aurora Engine's 16-char filename limit

#### 3D Model Rendering
- MDL parser for NWN model format
- PLT (palette texture) rendering with color channels
- Skeleton/animation support (idle pose minimum)
- Real-time model updates when appearance parts change

---

## [0.1.26-alpha] - 2026-01-04
**Branch**: `quartermaster/issue-760` | **PR**: #765

### Sprint: Panel Refactoring (#760)

Reduce code duplication and improve maintainability across Quartermaster panels.

- [x] #686 - Extract common panel patterns to base class
- [x] #632 - Split CreatureDisplayService into focused services

#### Added

- **BasePanelControl** ([BasePanelControl.cs](Quartermaster/Views/Panels/BasePanelControl.cs))
  - Abstract base class for panels with `IsLoading`, `CurrentCreature`, `LoadCreature()`, `ClearPanel()`
  - `DeferLoadingReset()` for suppressing events during data binding
  - Helper methods: `SetText()`, `SetCheckBox()`, `SetTextBox()`

- **ComboBoxHelper** ([ComboBoxHelper.cs](Quartermaster/Views/Panels/ComboBoxHelper.cs))
  - Generic `SelectByTag<T>()` replacing type-specific overloads
  - `GetSelectedTag<T>()` and `GetSelectedTagOrDefault<T>()`

- **BindableBase** ([BindableBase.cs](Quartermaster/ViewModels/BindableBase.cs))
  - INotifyPropertyChanged base with `SetProperty<T>()` helper

- **Focused Services** (extracted from CreatureDisplayService)
  - `SkillService` - skill lookups, class skill calculations
  - `FeatService` - feat lookups, categories, prerequisites
  - `AppearanceService` - appearance, phenotype, portrait, wing, tail, faction lookups
  - `SpellService` - spell lookups, caster class info

#### Changed

- **CreatureDisplayService** - now delegates to focused services
  - Exposes `Skills`, `Feats`, `Appearances`, `Spells` properties
  - Backward-compatible delegation methods preserved
  - Reduced from 1983 to 468 lines

- **Panels refactored to use BasePanelControl**
  - AdvancedPanel, ScriptsPanel, SkillsPanel, ClassesPanel
  - Removed duplicate fields and helper methods

---

## [0.1.25-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-587` | **PR**: #759

### Feature: Extract NWN Item Icons from TGA/DDS (#587)

Add support for extracting and displaying actual NWN item icons from game files.

#### Added

- **Pfim Library Integration**
  - Added Pfim NuGet package (0.11.4) for TGA/DDS decoding
  - Pure managed .NET Standard 2.0 - cross-platform compatible

- **Image Service Infrastructure** (Radoub.Formats)
  - `IImageService` interface for loading NWN image assets
  - `ImageService` implementation with TGA/DDS decoding via Pfim
  - `ImageData` class for RGBA pixel data output
  - Memory cache with LRU eviction (~500 icons, ~2MB)

- **PLT (Palette Texture) Parser** (Radoub.Formats)
  - `PltReader` for NWN's layered texture format
  - 24-byte header parsing (signature, version, dimensions)
  - 2-byte pixel format (grayscale + layer ID)
  - `PltLayers` constants for 10 color layers (Skin, Hair, Metal1/2, Cloth1/2, etc.)
  - `PaletteData` for loading pal_*.tga palette files
  - Rendering with palette color application

- **Item Icon Service** (Quartermaster)
  - `ItemIconService` converts ImageData to Avalonia Bitmap
  - Icon lookup from baseitems.2da (ItemClass, DefaultIcon, MinRange, MaxRange)
  - Icon naming pattern support: i<ItemClass>_<number>.tga
  - Portrait loading support

- **UI Integration**
  - `ItemViewModel.IconBitmap` property for actual game icons
  - `ItemViewModel.HasGameIcon` for conditional rendering
  - `EquipmentSlotControl.axaml` updated to show game icons or SVG placeholders
  - `ItemListView.axaml` updated to show game icons in palette/backpack
  - Graceful fallback to existing SVG placeholders when game data unavailable

#### Fixed

- **Panel Crash on Icon Load** - Fixed crash when loading panels with many items
  - SpellsPanel/FeatsPanel: Converted from non-virtualized `ItemsControl` to virtualized `ListBox`
  - Inventory: Implemented lazy icon loading via delegate pattern (icons load on scroll into view)
  - Prevents loading hundreds of bitmaps simultaneously during panel render

- **Log Level Setting Ignored** - User's log level preference wasn't being applied on startup
  - `SettingsService.LoadSettings()` now calls `UnifiedLogger.SetLogLevel()` after loading saved value

#### Tests

- PLT reader tests (7 tests)
  - Valid PLT parsing and pixel extraction
  - Invalid signature/size handling
  - Grayscale fallback rendering
  - Palette ResRef lookup

---

## [0.1.24-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-756` | **PR**: #757

### Sprint: Spells Foundation (#756)

Enable SpellsPanel to display creature's actual known and memorized spells by implementing the GFF parsing layer.

#### Added

- **Spell List Parsing** (#740, #741)
  - `KnownSpell` model class with Spell, SpellFlags, SpellMetaMagic fields
  - `MemorizedSpell` model class with additional Ready field for game instances
  - `CreatureClass.KnownSpells[10]` - arrays of known spells by spell level (0-9)
  - `CreatureClass.MemorizedSpells[10]` - arrays of memorized spells by spell level (0-9)
  - UtcReader/BicReader: Parse KnownList0-9 and MemorizedList0-9 from creature GFF
  - UtcWriter/BicWriter: Write spell lists back to GFF (round-trip support)
  - BicFile conversion: Deep copy spell lists during UTC/BIC conversion

- **SpellsPanel Integration**
  - Populate _knownSpellIds from parsed KnownSpells data
  - Populate _memorizedSpellIds from parsed MemorizedSpells data
  - Spells now correctly show Known/Memorized status from creature file

- **Spell Editing**
  - Checkboxes now enabled for non-blocked spells
  - Click checkbox to add/remove spells from known list
  - Changes update model data and mark document dirty
  - SpellsChanged event for dirty state tracking

#### Fixed

- **Caster Class Selection** - Radio buttons now enable based on actual spell data in creature file, not just 2DA lookup results

#### Tests

- Round-trip tests for KnownSpells (UtcReaderTests)
- Round-trip tests for MemorizedSpells (UtcReaderTests)
- Parse tests verifying spell ID, flags, and metamagic extraction

---

## [0.1.23-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-751` | **PR**: #755

### Sprint: Character & Alignment (#751)

Add race and alignment editing to Character panel, plus fix the emoji display bug.

#### Added

- **Race Dropdown** (#748)
  - Add GetAllRaces() to CreatureDisplayService (loads from racialtypes.2da)
  - Race ComboBox in Identity section of Character panel
  - Custom races from modules added dynamically if not in 2DA

- **Editable Alignment** (#732)
  - Replace read-only ProgressBars with interactive Sliders
  - Good-Evil and Lawful-Chaotic axes (0-100 scale)
  - Alignment name updates live as sliders change
  - AlignmentChanged event for dirty flag tracking

- **Package Picker**
  - Add GetAllPackages() and GetPackageName() to CreatureDisplayService (loads from packages.2da)
  - PackagePickerWindow dialog for selecting auto-levelup packages
  - Picker button in Levelup Settings section of Classes panel
  - Package names now display from 2DA/TLK instead of hardcoded values

#### Fixed

- **Navigation Button Emojis** (#691)
  - Add NavIcon class to emoji TextBlocks to prevent Foreground inheritance
  - Emojis now maintain consistent appearance regardless of selection state

---

## [0.1.22-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-750` | **PR**: #752

### Sprint: Stats Panel Editing (#750)

Complete Stats panel editing to match Aurora Toolset parity.

#### Added

- **Hit Points Editing** (#726)
  - NumericUpDown control for Base HP (dice rolls)
  - Con Bonus and Max HP displayed as calculated values
  - Base HP changes automatically recalculate Max HP
  - Matches Aurora Toolset behavior (CurrentHP not exposed)
- **Natural AC Editing** (#727)
  - NumericUpDown control for natural armor class (0-255 range)
- **Armor Class Section** (Aurora Toolset parity)
  - Separated Armor Class section from Combat section
  - Display Base AC (10), Dex Bonus, Size Modifier, and Total AC
  - Size Modifier calculated from appearance.2da SIZECATEGORY
  - Total AC recalculates when Natural AC or Dex changes
- **Challenge Rating Editing** (#729)
  - NumericUpDown control for base CR (0-100, 0.25 increment)
  - Works alongside existing CR Adjust control

---

## [0.1.21-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-725` | **PR**: #749

### Feature: Edit Ability Scores (#725)

Add editing capability for the 6 ability scores (STR, DEX, CON, INT, WIS, CHA).

#### Added

- NumericUpDown controls for modifying base ability scores
- Validation for 3-40 range (NWN limits)
- Recalculation of derived stats on ability change:
  - MaxHP recalculated when CON changes
  - Saving throws updated for CON/DEX/WIS changes
  - Dex AC bonus displayed in Combat section
- AbilityScoresChanged event for dirty flag tracking

---

## [0.1.20-alpha] - 2026-01-02
**Branch**: `quartermaster/issue-645` | **PR**: #747

### Epic: Character Color Picker (#645)

Implement character color customization (Skin, Hair, Tattoo1, Tattoo2) for part-based appearances.

#### Added

- Color fields (Skin, Hair, Tattoo1, Tattoo2) in Appearance tab with NumericUpDown controls
- Color swatches displaying actual palette colors next to each color field
- Clickable swatches open ColorPickerWindow dialog
- ColorPickerWindow with 16x11 gradient swatch grid matching Aurora Toolset layout
- PaletteColorService for extracting colors and gradients from NWN palette TGA files
- TgaReader in Radoub.Formats for parsing uncompressed TGA images
- Double-click on swatch confirms selection

#### Fixed

- Handle Aurora Toolset's BodyPart_RFoot bug (incorrectly saved as ArmorPart_RFoot)
- NumericUpDown decimal display (added FormatString="0")

---

## [0.1.19-alpha] - 2026-01-01
**Branch**: `quartermaster/issue-715` | **PR**: #716

### Sprint: Core Features & Workflow (#715)

Complete core Quartermaster functionality to achieve Aurora Toolset feature parity for creature editing workflows.

#### Added

- #643 - Faction Display & Management
  - Created FacFile and FacReader in Radoub.Formats for parsing repute.fac
  - FactionPickerWindow dialog with ListBox showing faction ID and name
  - Display faction name from repute.fac if available, with ID fallback
  - Graceful fallback to 5 default NWN factions when repute.fac not found
- #626 - Wire up themes and preferences/settings
  - Created SettingsWindow with 3 tabs: Resource Paths, UI Settings, Logging
  - Resource Paths: Base game path and user data path with browse/auto-detect
  - UI Settings: Theme selection, font size slider, font family picker with preview
  - Logging: Log level dropdown, log retention slider
  - Wired View > Settings menu item to open SettingsWindow
- #690 - Convert between UTC and BIC (Save As)
  - Added BicFile.FromUtcFile() and BicFile.ToUtcFile() conversion methods
  - SaveFileAs automatically converts creature format based on selected extension
  - UTC→BIC: Creates BicFile copy, sets IsPC=true, initializes BIC-specific fields
  - BIC→UTC: Creates UtcFile copy, sets IsPC=false, discards player-specific data

---

## [0.1.18-alpha] - 2025-12-31
**Branch**: `quartermaster/sprint/tech-debt-devex` | **PR**: #698

### Sprint: Tech Debt & DevEx (#697)

Improve developer experience and code reliability.

#### Fixed

- #652 - Command-line arguments to open BIC/UTC files (already implemented, added tests)
- #685 - File marked dirty on initial load due to deferred TextChanged events from Avalonia dispatcher
- #687 - Settings MaxRecentFiles defaults to 10 if 0/corrupt; added logging for missing file cleanup
- #692 - Fixed navigation test to check CharacterPanel (actual default), added Character to Theory tests

---

## [0.1.17-alpha] - 2025-12-31
**Branch**: `quartermaster/sprint/inventory-sync` | **PR**: #695

### Sprint: Inventory Sync & Item Operations (#694)

Implement sync layer between inventory UI and data model so item changes persist on save.

#### Added

- Inventory metadata on ItemViewModel (grid position, dropable/pickpocketable flags)
- CreateBackpackItem factory method for items with full inventory metadata
- SyncInventoryToCreature() method syncs UI state to creature model before save
- AddToBackpackRequested event for adding items from palette
- EquipItemsRequested event for equipping items from palette
- AddToBackpack() and RemoveFromBackpack() methods on InventoryPanel
- UnequipToBackpack() method for moving equipped items to backpack

#### Changed

- SaveFile() now calls SyncInventoryToCreature() before writing
- CreatePlaceholderItem() refactored to use shared ResolveUtiFile() helper
- Backpack items now preserve grid position and flags on round-trip

---

## [0.1.16-alpha] - 2025-12-31
**Branch**: `quartermaster/sprint/bic-file-support` | **PR**: #688

### Sprint: BIC File Support (#680)

Enable Quartermaster to properly edit BIC files (player characters) with appropriate UI differences from UTC files (creature blueprints).

#### Added

**BIC-Specific Character Fields (#676)**
- Player Character section with Experience, Gold, and Age fields
- Biography section for character description (shown when examined)
- File type detection automatically shows/hides appropriate sections

**Application Branding**
- Application icon (Quartermaster.ico) displayed in title bar and taskbar

#### Changed

**File Type Handling (#676)**
- Scripts nav button hidden for BIC files (player characters don't have scripts)
- Conversation field hidden for BIC files (not used for player characters)
- Challenge Rating display hidden for BIC files (player characters don't have CR)
- Blueprint ResRef and Comment fields hidden for BIC files (not in BIC format)
- Title bar shows "(Player)" indicator when editing BIC files
- Automatically navigates away from Scripts if loading BIC while on Scripts panel

---

## [0.1.15-alpha] - 2025-12-30
**Branch**: `quartermaster/sprint/ui-polish-panel-reorg` | **PR**: #684

### Sprint: UI Polish & Panel Reorganization (#678)

Reorganize Quartermaster's panel structure for improved usability.

#### Added

**Appearance Panel (#671)**
- New dedicated panel for creature visual configuration
- Appearance Type dropdown with dynamic/static indicator
- Phenotype and Portrait dropdowns
- Body Parts section with all 14 body part controls
- Automatically enables/disables body parts based on appearance type

**Character Panel (#672)**
- New dedicated panel for character identity and roleplay info
- First Name and Last Name text fields with CExoLocString support
- Subrace and Deity text fields (moved from Advanced panel)
- Sound Set dropdown (moved from Advanced panel)
- Conversation field with browse and clear buttons

**Stats Panel Enhancement (#670)**
- Added Challenge Rating adjustment control
- Moved CR Adjust from Advanced panel to Stats panel

#### Changed
- Removed duplicate Identity section from Classes panel (#674)
- Removed separator between Inventory and Advanced navigation buttons (#673)
- Streamlined Advanced panel to focus on blueprint/tag, flags, and behavior settings

---

## [0.1.14-alpha] - 2025-12-30
**Branch**: `quartermaster/sprint/scripts-advanced-props` | **PR**: #669

### Sprint: Scripts & Advanced Properties (#668)

Complete Quartermaster's core creature editing functionality with Scripts panel and remaining Advanced Properties fields.

#### Added

**Scripts Panel (#646)**
- Editable script ResRef fields for all 13 event scripts
- Browse button per script to open ScriptBrowserWindow
- Clear button per script slot to remove assignment
- Browse button for Conversation to open DialogBrowserWindow
- Real-time summary showing assigned script count
- Editable Conversation field with "Open in Parley" integration
- ScriptsChanged event for dirty state tracking
- MaxLength=16 enforced per Aurora Engine constraint

**Shared Browser Windows**
- Moved ScriptBrowserWindow to Radoub.UI shared library
- Created DialogBrowserWindow for dialog file browsing
- Created IScriptBrowserContext interface for tool-specific implementations
- ParleyScriptBrowserContext and QuartermasterScriptBrowserContext implementations

#### Fixed

**Aurora Toolset Compatibility**
- UtcWriter/BicWriter: Always write all 13 script fields (even empty ones)
- Fixed typo: ScriptuserDefine → ScriptUserDefine

**Subrace & Deity Fields (#647)**
- Editable Subrace text field in Identity section
- Editable Deity text field in Identity section
- Changes persist to UTC file

**Challenge Rating Display (#648)**
- CR display showing stored ChallengeRating value
- Editable CR Adjustment spinner (-100 to +100)
- Changes persist to UTC file

---

## [0.1.13-alpha] - 2025-12-30
**Branch**: `quartermaster/fix/flaky-spells-navigation-test` | **PR**: #656

### Fix: Flaky FlaUI Navigation Test (#654)

Fix intermittent test failures in NavigationTests when running as part of full test suite.

**Root Cause**: FlaUI's `Button.Click()` uses simulated mouse clicks at screen coordinates. When VSCode or other windows steal focus, clicks go to the wrong window.

**Solution**: Use UIA Invoke pattern for button clicks, which sends events directly through the automation framework without relying on screen coordinates.

#### Changed
- NavigationTests: Use `button.Patterns.Invoke.Pattern.Invoke()` instead of `button.Click()` for reliable automation
- Added `WaitForPanelVisible()` with retry logic matching SpellsPanelTests pattern
- Added `EnsureFocused()` calls before interactions
- Added 2-second inter-suite delay in run-tests.ps1 between UI test suites

#### Removed
- Removed Spells from Navigation Theory (covered by SpellsPanelTests.SpellsPanel_NavigatesSuccessfully)
- Removed `Navigation_SwitchBetweenPanels_WorksCorrectly` (flaky as first test)

---

## [0.1.12-alpha] - 2025-12-30
**Branch**: `quartermaster/feat/open-in-parley` | **PR**: #651

### Feature: Open Conversation in Parley (#642)

- Add "Open in Parley" button to Scripts tab for creatures with conversation assigned
- Launch Parley with the referenced `.dlg` file via command-line
- Graceful error handling if conversation file not found

---

## [0.1.11-alpha] - 2025-12-29
**Branch**: `quartermaster/feat/appearance-identity` | **PR**: #644

### Feature: Advanced Properties - Appearance & Identity Fields (#641)

Implement the Advanced Properties page with appearance customization and identity field management.

#### Added
- **Appearance Section**
  - Preset appearance dropdown (populated from appearance.2da)
  - Custom appearance component selection (Head, Body, Tail, Wings)
  - Phenotype selection
  - Portrait selection/preview
- **Identity Fields**
  - Blueprint ResRef with copy button
  - Tag with copy button
  - Editable Comment field

---

## [0.1.10-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/spells-panel` | **PR**: #633

### Sprint: Spells Panel with Search & Filter (#616)

Add the Spells panel to Quartermaster with search and filtering - addressing Aurora Toolset's spell selection pain points.

#### Added
- **Spells Panel** - Full spell browsing with search and filtering
  - Real-time text search filtering by spell name
  - Clear button to reset search
- **Spell Level Filter** - Filter by spell level (0-9)
- **Spell School Filter** - Filter by school:
  - All Schools (default)
  - Abjuration / Conjuration / Divination / Enchantment
  - Evocation / Illusion / Necromancy / Transmutation
- **Status Filter** - Filter by spell status:
  - All Spells / Known Only / Memorized Only / Available / Blocked
- **Class Selection** - 8 class radio buttons (Class 1-8)
  - Shows class name, level, and max spell level (e.g., "Wizard (10) - Lvl 5")
  - Non-caster classes disabled
  - Low-level casters without spells yet show "No spells"
- **Detailed Spell List** - Columns:
  - Checkbox (known/memorized status, read-only)
  - Spell Name
  - Spell Level (for selected class)
  - School
  - Innate Level
  - Status (Known/Memorized/Blocked)
- **Meta-Magic Expander** - Placeholder for future metamagic display
- **Spell Actions** - Placeholder buttons for Clear/Save/Load spell lists
- **FlaUI Automation IDs** - Full test coverage for UI elements
- **CreatureDisplayService** - Added spell data methods:
  - `GetAllSpellIds()` - Gets all spells from spells.2da
  - `GetSpellInfo()` - Gets detailed spell info (school, levels by class)
  - `GetSpellSchoolName()` - Converts school enum to display name
  - `GetMaxSpellLevel()` - Gets max spell level for class at given level
  - `IsCasterClass()` - Checks if class has SpellGainTable in classes.2da

#### Note
- Spell lists (known/memorized) are not yet parsed from creature files
- Panel is read-only in this version
- Editing features planned for future sprint

---

## [0.1.9-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/feats-panel` | **PR**: #629

### Sprint: Feats Panel with Search & Filter (#615)

Add the Feats panel with robust search and filtering - addressing Aurora Toolset's major pain point.

#### Added
- **Search Box** - Real-time text search filtering by feat name
  - Clear button to reset search
  - Filters as you type
- **Category Filter Dropdown** - Filter by feat type:
  - All Feats (default)
  - Combat / Active Combat / Defensive / Magical / Class/Racial / Other
  - Assigned Only / Granted Only (class-granted feats)
  - Unassigned Only / Available Only / Unavailable Only
  - Prereqs Met / Prereqs Unmet / Has Prereqs (filter by prerequisite status)
- **All Feats List** - Shows ALL feats from feat.2da, not just assigned
  - Columns: Status | Feat Name | Category | Status Text
  - Tooltip shows feat description
- **Visual Status Distinction**:
  - ✓ Green checkmark + "Assigned" for chosen feats
  - ★ Gold star + "Granted" for class-granted feats
  - ⚠ Orange warning + "Unmet" for feats with unmet prerequisites
  - ○ Blue circle + "Available" for feats with all prerequisites met
  - ✗ Gray X + "Unavailable" for feats not available to class/race
  - Row highlighting: green for assigned, gold for granted, gray for unavailable
- **Special Abilities Section** - Collapsible expander showing spell-like abilities
- **Summary Line** - Shows assigned count, granted count, and filter status
- **Feat Add/Remove** - +/- buttons on each feat row
  - Add any feat not currently assigned
  - Remove user-assigned feats (class-granted feats cannot be removed)
  - Updates creature's feat list in real-time
  - `FeatsChanged` event for dirty state tracking
- **Prerequisite Checking** - Full prerequisite validation with tooltip display:
  - Required feats (AND logic)
  - Or-required feats (OR logic - need at least one)
  - Ability score requirements (STR, DEX, INT, WIS, CON, CHA)
  - Base Attack Bonus requirements
  - Spell level requirements
  - Skill rank requirements
  - Level requirements (character or class-specific)
  - Epic requirement (level 21+)
  - Tooltip shows ✓/✗ for each prerequisite
- **Availability Checking** - Feat availability based on class/race:
  - Universal feats available to all
  - Class-specific feats checked against cls_feat_*.2da tables
- **CreatureDisplayService Integration** - Added feat-related methods:
  - `GetFeatCategory()` - Feat category from TOOLSCATEGORIES column
  - `GetFeatDescription()` - Feat description from TLK
  - `IsFeatUniversal()` - Check ALLCLASSESCANUSE column
  - `GetAllFeatIds()` - Get all valid feat IDs from feat.2da
  - `GetFeatInfo()` - Get complete feat information
  - `GetClassGrantedFeatIds()` - Get feats granted by a class
  - `GetCombinedGrantedFeatIds()` - Combined granted feats for multiclass
  - `IsFeatAvailable()` - Check if feat is available to creature
  - `GetUnavailableFeatIds()` - Get set of unavailable feats
  - `GetFeatPrerequisites()` - Get prerequisite data for a feat
  - `CheckFeatPrerequisites()` - Check prerequisites against creature

---

## [0.1.8-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/skills-panel` | **PR**: #628

### Sprint: Skills Panel (#614)

Add the Skills panel displaying all skill ranks for the character.

#### Added
- **Skills List Display** - Shows all skills from skills.2da with columns:
  - Skill Name with key ability indicator (STR, DEX, INT, WIS, CHA, CON)
  - Rank value (base skill points invested)
  - Total value (Rank + Ability Modifier)
  - +/- buttons as disabled placeholders for future editing
- **Class Skill Highlighting** - Distinguishes class skills from cross-class skills
  - Visual indicator (● for class skill, ○ for cross-class)
  - Light blue background highlight for class skills
  - Uses cls_skill_*.2da for each of character's classes
  - Combined class skills from multiclass characters
- **Sorting Options** - Dropdown to sort skills:
  - Alphabetical (default)
  - By Rank (highest first)
  - Class Skills First
- **Filtering** - "Trained Only" checkbox to show only skills with ranks > 0
- **Summary Display** - Shows count of trained skills, total ranks, and class skill count
- **Unavailable Skill Detection** - Grays out skills the character cannot take
  - Checks `AllClassesCanUse` column in skills.2da
  - Cross-references cls_skill_*.2da for class-restricted skills
  - Shows ✗ indicator for unavailable skills
- **CreatureDisplayService Integration** - Added skill name resolution via 2DA/TLK:
  - `GetSkillName()` - Skill name from skills.2da with TLK lookup
  - `GetSkillKeyAbility()` - Key ability from skills.2da
  - `GetClassSkillsTable()` - Class skills table name from classes.2da
  - `IsClassSkill()` - Check if skill is class skill for a class
  - `GetClassSkillIds()` - Get set of class skill IDs for a class
  - `GetCombinedClassSkillIds()` - Combined class skills for multiclass
  - `IsSkillUniversal()` - Check if all classes can use a skill
  - `IsSkillAvailable()` - Check if skill is available to creature
  - `GetUnavailableSkillIds()` - Get set of unavailable skill IDs

---

## [0.1.7-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/classes-levels-panel` | **PR**: #625

### Sprint: Classes & Levels Panel (#613)

Add the Classes & Levels panel displaying character class progression and alignment.

#### Added
- **Class Slots Display** - Shows active classes only (up to 8 per Beamdog EE)
  - Each class shows name, level, hit die, and skill points per level
  - Key class features displayed (e.g., "Rage, Fast Movement" for Barbarian)
  - "Add Class" placeholder button for future editing
  - Level +/- buttons as disabled placeholders for future editing
  - Total level calculation displayed
- **Alignment Display** - Shows character alignment with visual progress bars
  - Good-Evil axis with numeric value (0-100)
  - Lawful-Chaotic axis with numeric value (0-100)
  - Alignment name calculated from axis values (e.g., "Lawful Good", "True Neutral")
- **Auto-Levelup Package Display** - Shows the creature's StartingPackage
  - Package name resolved from packages.2da (hardcoded fallbacks)
- **Levelup Wizard Placeholder** - Disabled button for future levelup wizard sprint
- **Identity Section** - Race, gender, subrace, and deity display
- **CreatureDisplayService Integration** - ClassesPanel now uses 2DA/TLK lookups when available

#### Changed
- ClassesPanel uses CreatureDisplayService for class/race/gender name resolution
- Hit die display per class (d4-d12 based on class type)

---

## [0.1.6-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/stats-identity-panel` | **PR**: #622

### Sprint: Character Stats & Identity Panel (#612)

Add the Stats & Identity panel displaying character name, portrait, and core statistics.

#### Added
- **CreatureDisplayService** - Centralized service for resolving creature names from 2DA/TLK
  - Race name lookup from `racialtypes.2da` with TLK resolution
  - Gender name lookup from `gender.2da` with TLK resolution
  - Class name lookup from `classes.2da` with TLK resolution
  - Racial ability modifier lookup (StrAdjust, DexAdjust, etc.)
  - Hardcoded fallbacks for common races/genders/classes when 2DA not available
- **Base Attack Bonus calculation** from class levels
  - Reads `AttackBonusTable` from `classes.2da` for each class
  - Looks up BAB from `cls_atk_*.2da` at appropriate level
  - Sums BAB across multiclass levels
  - Falls back to estimated progression (full/3/4/half) when 2DA unavailable
- **Equipment attack bonus** from equipped items
  - Scans item properties for Enhancement Bonus (PropertyName 6) and Attack Bonus (PropertyName 56)
  - Enhancement bonuses don't stack (highest wins)
  - Attack bonuses stack
- **Feat name resolution** - FeatsPanel now uses `feat.2da` + TLK for feat names
  - High feat IDs (e.g., 1089) now resolve correctly instead of showing "Feat 1089"
  - Spell names for special abilities resolved via `spells.2da` + TLK
- **Navigation FlaUI tests** - Sidebar nav button tests for all 8 panels
- **Enhanced StatsPanel** - Complete overhaul with detailed stat breakdown
  - Ability scores: Base | Racial Modifier | Total | Bonus columns
  - Hit points: Base HP (dice rolls) | Max HP (with Con) | Current HP (with %)
  - Combat stats: Natural AC | Base Attack (with breakdown) | Speed | Challenge Rating
  - Saving throws: Base | Ability Modifier | Total columns
- **Sidebar character header** - Now shows resolved race/gender/class names via 2DA/TLK lookup

#### Fixed
- **Walk rate display** - Fixed creaturespeed.2da mapping (was off-by-one)
  - Row 0 = PC, Row 4 = Normal, etc. (not Row 0 = Immobile)

#### Changed
- MainWindow now uses `CreatureDisplayService` for character summary in sidebar
- MainWindow passes equipped items to StatsPanel for BAB calculation
- StatsPanel receives display service via `SetDisplayService()` method
- StatsPanel shows BAB breakdown: "(X base + Y equip)" when equipment contributes

---

## [0.1.5-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/app-shell-layout` | **PR**: #619

### Sprint: Application Shell & Layout Foundation (#618)

Establishes the Quartermaster application shell with a FlaUI-friendly sidebar + content layout that avoids tab controls.

#### Added
- **Sidebar navigation layout** - Replaced tab-based layout with sidebar + content pattern for FlaUI compatibility
- **Character header** - Portrait placeholder, character name, and race/class summary in sidebar
- **Navigation buttons** - Stats, Classes, Skills, Feats, Spells, Inventory, Advanced, Scripts sections
- **Content panel switching** - Single content area that swaps UserControls based on navigation selection
- **StatsPanel** - Basic stats display showing ability scores, combat stats, and saving throws
- **InventoryPanel** - Extracted inventory UI into standalone UserControl with equipment slots, backpack, and palette
- **PlaceholderPanel** - "Coming Soon" panels for unimplemented sections
- **AutomationIds** - FlaUI-friendly identifiers on all interactive elements (NavButton_*, ContentArea, CharacterName, etc.)
- **SidebarWidth setting** - Persisted sidebar width preference

#### Changed
- MainWindow layout refactored from 3-panel inventory view to sidebar + content pattern
- Settings now saves sidebar width instead of left panel width

---

## [0.1.4-alpha] - 2025-12-27
**Branch**: `quartermaster/fix/flaui-closes-vscode` | **PR**: #606

### Fix: FlaUI tests close VSCode instead of just the app (#593)

#### Fixed
- **StopApplication() window targeting** - Changed from `Alt+F4` keystroke to `App.Close()` method
  - Alt+F4 sends to focused window, which could be VSCode if test app lost focus
  - `App.Close()` sends WM_CLOSE directly to test process, ensuring correct targeting
  - Kept 200ms delay before close to prevent SkiaSharp render crashes

---

## [0.1.3-alpha] - 2025-12-26
**Branch**: `quartermaster/feat/inventory-display-580` | **PR**: #585

### Feature: Wire up Inventory Display and File Search (#580)

#### Added
- **Item palette population** - Loads items from module directory, Override folder, and BIF archives
- **Item filtering** - Filter panel wired to GameDataService for item type dropdown
- **GitHub workflows** - `quartermaster-pr-build.yml` and `quartermaster-pr-tests.yml`
- **External branding** - Window title, About dialog, CLI help, menus use "Quartermaster"
- **Equipment slot display** - Equipped items from BIC files now display in slots with name labels
- **Placeholder icons** - SVG icons from game-icons.net (CC BY 3.0) for equipment slots and item lists
- **ItemIconHelper** - Maps equipment slots and item types to placeholder icons

#### Changed
- PaletteList now displays filtered items from ItemFilterPanel
- ClearInventoryUI clears palette items and selection state
- Equipment slot control now shows slot name on top, item name on bottom
- Item lists (palette, backpack) now show item type icons

#### Fixed
- **BIC equipped items** - Fixed parsing of `EquippedRes` field (was using wrong field name)
- **BIC inventory items** - Fixed parsing of `InventoryRes` field for backpack items

#### Infrastructure
- Workflow parity with Parley for PR checks and CI
- Internal namespace remains `CreatureEditor` (like Parley/DialogEditor pattern)
- Added Avalonia.Svg.Skia package for SVG icon support

---

## [0.1.2-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-cleanup` | **PR**: #584

### Sprint: CreatureEditor Cleanup (#582, #583)

Pre-emptive refactoring before codebase grows. Lessons from Parley's 2,400+ line MainWindow.

#### Refactoring (#582)
- **MainWindow.FileOps.cs** - Extracted file operations (Open/Save/Recent files)
- **MainWindow.Inventory.cs** - Extracted inventory population and UTI resolution
- **DialogHelper.cs** - Static helper for common dialogs (Unsaved/Error/About)
- MainWindow.axaml.cs reduced from 892 to 466 lines (48% reduction)

#### Testing (#583)
- **CreatureEditor.Tests project** - New xUnit test project (21 tests)
- **CommandLineServiceTests** - 9 tests for argument parsing
- **SettingsServiceTests** - 12 tests for property constraints and defaults
- FlaUI integration smoke tests added to Radoub.IntegrationTests

---

## [0.1.1-alpha] - 2025-12-26
**Branch**: `radoub/feat/uti-bif-loading` | **PR**: #581

### Feature: Load UTI Items from BIF Archives (#579)

#### Added

## [0.1.0-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-mvp` | **PR**: #578

### Sprint: Creature Editor MVP - Inventory Panel (#554)

Initial release of Quartermaster (Creature Editor).

#### Added
