# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Trimmed to highlights only.

---

## [0.2.128-alpha] - 2026-06-21
**Branch**: `quartermaster/issue-2540` | **PR**: #2553

### Epic: Mesh transparency pipeline — alpha blend + alpha-test + depth sort (#2540)

- Model preview gains transparency support (ghost/glass blend, fur/foliage cutout) instead of rendering every mesh opaque. Shared `Radoub.UI` renderer change — see root CHANGELOG. Closes #2435, #2507 when complete.

---

## [0.2.127-alpha] - 2026-06-21
**Branch**: `quartermaster/issue-2399` | **PR**: #2545

### Fix: Robe animations badly off — head detaches during run (#2399)

- Robe-wearing creatures (e.g. Dana) no longer distort during animation playback; the robe body now deforms with the animated skeleton. Shared-library fix — see root CHANGELOG (`Radoub.UI 0.2.21-alpha`, `Radoub.Formats 0.2.74-alpha`).

---

## [0.2.126-alpha] - 2026-06-19
**Branch**: `quartermaster/issue-2440` | **PR**: #2525

### Sprint: Character Data + UI Bug Fix (#2440)

- Closing the window mid-cache-build no longer freezes the UI — the in-flight palette/HAK scan is cancelled on close (#2299).
- Feats panel: clearer separation between the row checkbox and the status column so the checkbox's column is unambiguous (#2286).
- Portrait browser: duplicate portraits removed and thumbnails enlarged for legibility; the editor's Browse now pre-filters to the creature's race/sex instead of showing all (#2329).

---

## [0.2.125-alpha] - 2026-06-19
**Branch**: `quartermaster/issue-2498` | **PR**: #2509

### Fix: Intermittent exploded/garbage model preview (#2510)

- Fixed a data race where a background model load (#1485) could corrupt the main render's MDL parse, intermittently dropping geometry (e.g. dire tiger rendering as exploded "wings"). Root cause and fix are a shared-library `MdlReader` thread-safety change — see the root CHANGELOG (`Radoub.Formats 0.2.72-alpha`).

### Fix: Model preview hid real geometry (hands, hair, dragon spikes, tongues) (#2498 / #2482 D)

- Removed the 30-vertex mesh-skip heuristic that hid small body parts sharing the body texture (Antoine's hands/neck/hair, dragon spikes/fins, snake tongue). Mesh visibility now matches the Aurora engine (nwnexplorer/borealis): honor the MDL `Render` flag and drop empty meshes — no vertex-count or shared-bitmap guess. Shared `Radoub.UI` change.

---

## [0.2.124-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-2029` | **PR**: #2506

### Fix: Dire tiger model-preview holes (#2029)

- The dire tiger (and other CEP creatures with >16-char texture references) now render textured correctly in the appearance preview. Root cause and fix are a shared-library ResRef-truncation change — see the root CHANGELOG (`Radoub.Formats 0.2.71-alpha`).
- Appearance preview no longer shows the misleading "N of M meshes hidden (Render=false)" status — those are the model author's intentional bone/internal meshes, normal for nearly every creature.

---

## [0.2.123-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-1485` | **PR**: #2502

### Feat: Wings & Tail in 3D appearance render (#1485)

- Part-based creature previews now render wings (`wingmodel.2da`) and tail (`tailmodel.2da`) with `WING_TAIL_SCALE` applied, flapping in sync with the body animation.

---

## [0.2.122-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-2497` | **PR**: #2500

### Feat: MTR-driven diffuse resolution + diagnostics (#2497)

- Model preview now resolves a mesh's diffuse from its `.mtr` material (`texture0`, via the mesh `materialname`) ahead of the existing bare-name/`_d` chain. Forward-looking MTR support — current CEP/PRC packs ship no `.mtr`, so this no-ops on today's content; added `[MTR]` diagnostic logging to capture the first genuinely MTR-driven model.

---

## [0.2.121-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-2496` | **PR**: #2499

### Feat: MTR reader + MDL materialname (#2496)

- Format-layer foundation for the model-preview white-model fix (#2482): MTR (3007) reader and MDL `materialname` parsing land in `Radoub.Formats` — see root CHANGELOG.

---

## [0.2.120-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-2487` | **PR**: #2494

### Sprint: Aurora Data Sourcing (#2487)

- Spell caster-class levels and creature-size AC/name now read from 2DA by label/row instead of hardcoded class IDs and size tables — fixes Quartermaster with CEP/PRC reordered 2DAs (#2480, #2479).
- Race/class/appearance enumeration covers all populated 2DA rows (drops the early-break heuristic that silently dropped CEP rows past index 50) (#2479).

---

## [0.2.119-alpha] - 2026-06-09
**Branch**: `quartermaster/issue-2434` | **PR**: #2449

### Feat: Fairy/pixie emitter preview fidelity (#2434)

- Fairy/pixie dust now renders at the correct authored size (matches Aurora), animated emitters follow the idle animation, and oversized wing-glow emitters no longer swallow the model. Shared `Radoub.UI` 0.2.10-alpha — see root CHANGELOG. Follow-up to #2395.

---

## [0.2.118-alpha] - 2026-06-08
**Branch**: `quartermaster/issue-2395` | **PR**: #2432

### Feat: Render particle emitters in model preview (#2395)

- Shared `Radoub.UI` model preview now draws MDL emitter nodes (particle systems) so emitter-driven creatures look closer to Aurora (see root CHANGELOG).

---

## [0.2.117-alpha] - 2026-06-06
**Branch**: `quartermaster/issue-2381` | **PR**: #2394

### Sprint: Rendering Bug Cleanup (#2381)

- Fire/stag/bombardier beetle (Txpple) and giant/glow spider (CEP `una`) now render textured, not white (#1755, #1760) — PBR diffuse fallback in shared Radoub.UI 0.2.3-alpha (see root CHANGELOG)
- Dire rat (CEP `una`) now renders textured; softer preview lighting so models aren't washed out (#1762) — shared Radoub.UI 0.2.4-alpha (see root CHANGELOG)
- Fey/pixy (`c_fairy`) and other CEP creatures render their own pack's textures, not a base-game stub (#1758) — preview textures follow the model's source tier (shared Radoub.UI 0.2.5-alpha, see root CHANGELOG)
- Robe armor renders as a proper combined body — no more back-gaps/missing-legs/ballooned arms; suppresses the body parts the robe replaces and grafts the robe's own posed subtree (#1989) — shared Radoub.UI 0.2.6-alpha (see root CHANGELOG). Per-variant residuals tracked in #2398/#2399.
- Snake floating fangs + flat pose (#2126) — root-caused as the un-applied skin-mesh deformation; deferred to skinning epic #2400 (not fixable without the shared renderer's bone-weight deformation work)
- Removed the disabled "Not yet implemented" Undo/Redo/Cut/Copy/Paste Edit-menu stubs (#2250) per the #2231 UI-uniformity rule

---

## [0.2.116-alpha] - 2026-06-05
**Branch**: `radoub/issue-2350` | **PR**: #2351

### Fix: Browser delete now backs up first (#2350)

- Deleting a creature from the browser backs it up to `~/Radoub/Backups/` first instead of removing it outright. See shared `Radoub.UI` 0.1.68-alpha (root CHANGELOG).

---

## [0.2.115-alpha] - 2026-05-30
**Branch**: `reliquary/issue-2291` | **PR**: #2328

### Fix: Blank portraits leading the browser list (#2291)

- Portrait browser now skips any blank or all-asterisk `portraits.2da` cell (e.g. CEP `***` padding), not just exact `****`, so empty tiles no longer sort to the front.

---

## [0.2.114-alpha] - 2026-05-30
**Branch**: `quartermaster/issue-2220` | **PR**: #2326

### Sprint: Non-Rendering Bug Cleanup (#2220)

- Filter master-feat subtypes by class eligibility (#2096)
- Honor Trebuchet font-size slider in QM (#2152)
- Appearance "Appearance N" fallback (#2162) — not reproducible on current main; CEP3 high rows resolve correctly via module-HAK 2DA merge. Added regression test, closed.

---

## [0.2.113-alpha] - 2026-05-29
**Branch**: `quartermaster/issue-1735` | **PR**: #2317

### Fix: Dynamic Brownie part-based models render with misplaced parts (#1735)

- Root cause + fix in shared `Radoub.UI` `MdlPartComposer` (world-space seam/bounds, no cache mutation, height-scaled seam threshold) — see root CHANGELOG [Radoub.UI 0.1.63-alpha].

---

## [0.2.112-alpha] - 2026-05-28
**Branch**: `quartermaster/issue-2252` | **PR**: #2284

### Sprint: Hygiene + game-data sourcing (#2252, #2251, #2285)

- Lifecycle, async, and process-launch hygiene (#2252)
- Hardcoded game-data fallback tables replaced with 2DA/TLK lookup + WARN-once fallback (#2251)
- F4 rename now refreshes CreatureBrowser — stale pre-rename row removed, new file appears (#2285)

---

## [0.2.96-alpha] - 2026-05-26
**Branch**: `quartermaster/issue-2249` | **PR**: #2275

### Fix: Down-Level corrupts .bic files + CreatureCopy strips BIC-only fields (#2249)

---

## [0.2.95-alpha] - 2026-05-26
**Branch**: `radoub/issue-2244` | **PR**: #2266

### Fix: Radoub.Formats parser hardening — integer overflow, atomic writes, silent truncation (#2244)

- Shared `Radoub.Formats` hardening from full-codebase review. ERF/KEY/BIF: uint arithmetic that wrapped before bounds checks now uses long-promoted comparisons; `ErfWriter.UpdateResource` switched to `File.Replace` (atomic, original survives mid-rename failures); ERF `ResId` preserved instead of silently renumbered to sequential index. GFF: `ReadCExoString` / `CExoLocString` no longer abandon entire string tables on a single oversized entry; bare `catch {}` in `GffFile.GetFieldValue<T>` narrowed and logged. TLK: `CleanResRef` no longer strips mid-string whitespace asymmetric with the writer. Dead UTF-8 fallback removed. Affects every tool consuming GFF/ERF/KEY/BIF/TLK.

---

## [0.2.94-alpha] - 2026-05-25
**Branch**: `radoub/issue-2241` | **PR**: #2265

### Fix: GFF 64-bit field types (DWORD64/INT64/DOUBLE) silently corrupt on round-trip (#2241)

- Shared `Radoub.Formats` fix: DWORD64/INT64/DOUBLE were classified as simple types and read/written as 32-bit, silently zeroing values on save and producing wrong floats on load. Now treated as complex types per Aurora spec — value stored as 8 bytes in FieldData section. Affects every tool consuming GFF (UTC, UTI, UTM, BIC, IFO, JRL, DLG).

---

## [0.2.93-alpha] - 2026-05-25
**Branch**: `radoub/issue-2238` | **PR**: #2239

### Fix: Severe memory bloat — idle RSS dropped from 5 GB to under 1 GB (#2238)

- Shared `Radoub.Formats` resolver fix: HAK index loading switched to `ErfReader.ReadMetadataOnly` so the resolver no longer buffers entire HAK byte arrays on the Large Object Heap. Eliminates the OOM-kill and hard-reboot risk when running multiple Radoub apps concurrently against large-HAK modules (CEP3 etc.).

---

## [0.2.92-alpha] - 2026-05-24
**Branch**: `quartermaster/issue-2201` | **PR**: #2213

### Feat: Adopt FileBrowserPanelBase Name/Tag sort + search (#2201)

Sprint 4 of #2186. `CreatureBrowserPanel` now supports Name and Tag sort/search via the FileBrowserPanelBase infrastructure landed in Sprint 1 (#2198), mirroring the Relique (#2199) and Fence (#2200) adoptions.

- HAK/BIF creatures indexed instantly from persistent palette cache (`~/Radoub/Cache/CreaturePalette/`)
- Module + vault creatures indexed lazily via background GFF read
- Saving a UTC/BIC refreshes its browser row Tag/Name without full reindex
- BIC files share the UTC indexing path (same GFF schema)

---

## [0.2.91-alpha] - 2026-05-01
**Branch**: `radoub/issue-2159` | **PR**: #2160

### Refactor: Extract MdlPartComposer to Radoub.UI (#1908 prep PR3a)

- Move part-mesh-onto-skeleton composition logic from `ModelService.LoadPartBasedCreatureModel` into shared `MdlPartComposer` in `Radoub.UI`
- `LoadPartBasedCreatureModel` becomes a thin adapter that resolves creature appearance + armor overrides + human fallback, then delegates to the composer
- No rendering behavior changes — QM creature preview renders identically (mesh re-parenting, texture-name overrides, #1557 seam-overlap nudge, bounds aggregation all preserved)
- Enables Relique 3D item preview (#1908 PR3b) to compose composite weapons (3 parts) and armor (up to 19 parts) using the same mechanics

### Fix: Replace hardcoded font sizes with themable DynamicResource bindings

- Replace hardcoded `FontSize="11"` / `"12"` on `AppearancePanel.PreviewStateText` and `AdvancedPanel.VariableValidationText` / DataGrid error labels with `{DynamicResource FontSizeSmall}`
- Required for low-vision users — hardcoded font sizes do not scale with the Trebuchet font-size slider

---

## [0.2.90-alpha] - 2026-05-01
**Branch**: `radoub/feat/promote-model-preview` | **PR**: #2156

### Refactor: Promote ModelPreviewGLControl to Radoub.UI (#1908 prep)

- Move `ModelPreviewGLControl`, `OpenGLShaderManager`, `ModelViewController`, `MeshSkipHeuristic`, `SmoothGroupNormals`, `VertexWelder`, and `TextureService` from Quartermaster to `Radoub.UI`
- Pure structural move — no rendering behavior changes; QM creature preview continues to render identically
- Memory-leak hardening from #2034 (LRU caches, EventSubscriptions, Unloaded cleanup, TRACE-level MDL logs, PreferBifTextures cache-clear) preserved verbatim
- Restore themable `FontSize` bindings on shared `ItemDetailsPanel` (#2155)

---

## [0.2.89-alpha] - 2026-04-30
**Branch**: `radoub/issue-1996` | **PR**: #2151

### Refactor: Adopt shared ItemDetailsPanel from Radoub.UI (#1996)

- Replace InventoryPanel inline item detail block with shared `ItemDetailsPanel` from Radoub.UI
- No behavior change — pure UI extraction for reuse across tools

---

## [0.2.88-alpha] - 2026-04-29
**Branch**: `radoub/issue-2144` | **PR**: #2150

### Refactor: Consolidate duplicate _cachedPaletteData (#2144)

- Remove local `_cachedPaletteData` field; read from `SharedPaletteCacheService` directly (single source of truth)

---

## [0.2.87-alpha] - 2026-04-27
**Branch**: `quartermaster/issue-2058` | **PR**: #2145

### Fix: Address general UI lag and performance (#2058)

- Move CharacterPanel race/portrait/soundset 2DA scans off the UI thread (spike measured 11.3s of synchronous load on first creature open)
- Same off-thread treatment for AppearancePanel and AdvancedPanel SetDisplayService data loads
- Cache `AppearanceService.GetAllPortraits` / `GetAllSoundSets` results so subsequent panel switches don't re-iterate the 2DAs

---

## [0.2.86-alpha] - 2026-04-25
**Branch**: `radoub/issue-2034-round2` | **PR**: #2142

### Fix: Memory leaks — round 2 top 3 (#2034)

- `SharedPaletteCacheService` now implements `IDisposable` and disposes its `ReaderWriterLockSlim` (eliminates kernel-handle leak; affects all tools via Radoub.UI)
- `ItemIconService` bitmap cache bounded with LRU eviction (was unbounded `ConcurrentDictionary`, could grow past the documented ~15–30 MB ceiling on large modules)
- `AdvancedPanel` lambda closures unwired on `Unloaded` so prior `CurrentCreature` graphs can be garbage-collected between creature switches
- Perf: demoted per-node/per-mesh MDL parser logs from DEBUG to TRACE (~165k log lines per creature-browser session at DEBUG level were causing visible app slowdown on startup and panel switches)

---

## [0.2.85-alpha] - 2026-04-25
**Branch**: `radoub/issue-1526` | **PR**: #2141

### Fix: FlaUI integration tests flaky under concurrent execution (#1526)

- Assembly-level `[CollectionBehavior(DisableTestParallelization = true)]` on `Radoub.IntegrationTests` so every FlaUI test runs sequentially regardless of `[Collection]` assignment — fixes the canonical `SmokeTests.Quartermaster_LaunchAndCoreUI` flake when multiple test projects load via `dotnet test` directory walks or IDE Test Explorer
- Named system mutex (`Global\Radoub.FlaUI.SerialExecution`) prevents two FlaUI test runs from racing across processes (e.g. terminal + IDE); 30 s timeout with a clear error
- Defense-in-depth: `DisableParallelization = true` added to Fence and Trebuchet collection definitions
- One-shot relaunch retry in `StartApplication` when `GetMainWindow` returns null (residual H3 mitigation for desktop-focus races); both null events log loudly so real launch regressions can't hide as transient
- Affects test-runner behavior only — no runtime change in Quartermaster itself

---

## [0.2.84-alpha] - 2026-04-22
**Branch**: `radoub/issue-2034` | **PR**: #2129

### Fix: Memory leaks — top 3 (#2034)

- Bounded `TextureService` palette and rendered-texture caches with LRU eviction (previously unbounded, grew to 500 MB+ in long sessions).
- `AppearancePanel` now unsubscribes its 27+ event handlers on `Unloaded`, letting prior `CurrentCreature` graphs be garbage-collected between creature switches.
- `GameDataService.ClearCache()` now clears the SSF and palette caches in addition to the 2DA cache, matching `ConfigureModuleHaks` behavior (affects all tools via Radoub.Formats).

---

## [0.2.83-alpha] - 2026-04-22
**Branch**: `quartermaster/issue-2124` | **PR**: #2125

### Feature: Comprehensive 3D preview controls (#2124)

- Free orbit — left-drag rotates on both axes with no X-axis clamp
- Pan via middle-click or Shift+left-drag; zooming now pivots on the cursor instead of world origin
- Scroll wheel zoom and keyboard camera shortcuts (arrows/WASD rotate, Home reset, F8 cycles debug mode)
- View preset buttons: F/B/L/R/T (front/back/left/right/top)
- Animation playback: dropdown of idle/walk/run/attack/cast etc., play-pause-scrub, loop, speed slider (0.1×–2×)
- Supermodel animation inheritance (`a_ba`, `a_fa`, etc.) so most creatures have anims without authoring their own
- Humanoid (part-based) creatures now use their skeleton's bone hierarchy so supermodel animations drive all body parts

---

## [0.2.82-alpha] - 2026-04-20
**Branch**: `quartermaster/issue-2026` | **PR**: #2114

### Fix: Human/elf head shading misaligned (#2026)

- 3D preview now matches the Aurora toolset's texture-dominant shading.
- Per-vertex normals computed from smoothgroup bitmasks for multi-group meshes (heads); stored normals used for single-group meshes (bodies/limbs) where the author encoded hard edges via vertex duplication.
- Vertex welding after normal selection so the GPU interpolates across shared edges while preserving legitimate seams.
- Directional light contribution reduced, ambient raised so texture-painted detail dominates.
- F8 in the appearance preview cycles debug visualisations (normals as RGB, lighting term, texture-only, lit-side tint).

---

## [0.2.81-alpha] - 2026-04-19
**Branch**: `quartermaster/issue-2064` | **PR**: #2112

### Sprint: Performance & UX Polish (#2064)

- Address general UI lag and performance (#2058)
- Color picker swatches not displaying until picker dialog opened (#2004)

---

## [0.2.80-alpha] - 2026-04-18
**Branch**: `fence/issue-2065` | **PR**: #2097

### Copy-to-Module for Creatures (#1479)

- Right-click any BIF/HAK creature in the browser → Copy to Module with rename dialog
- Edit TemplateResRef, Tag, and FirstName (LastName preserved) before writing to module
- Inherits shared implementation from FileBrowserPanelBase

---

## [0.2.79-alpha] - 2026-04-18
**Branch**: `quartermaster/issue-1978` | **PR**: #2095

### Sprint: Wizard & List Polish (#1978)

- NCW feat selection should show previously chosen feats (#1883)
- Simplify validation rules from LG/TN/CE to LG/CE (#1882)
- NCW/LUW skill list should sort unavailable skills to bottom (#1881)
- Consolidate GAINMULTIPLE subtypes (Weapon Focus, etc.) in feat list (#1734)

---

## [0.2.78-alpha] - 2026-04-12
**Branch**: `quartermaster/issue-2074` | **PR**: #2076

### Fix: 3D model preview not rendering on Linux (#2074)

---

## [0.2.77-alpha] - 2026-04-11
**Branch**: `quartermaster/issue-2057` | **PR**: #2059

### Fix: Dragon models missing tails in appearance preview (#2057)
- Texture-based mesh skip heuristic replaces blunt <30 vertex threshold
- Status line shows filtered trimesh count for diagnostic visibility

---

## [0.2.76-alpha] - 2026-04-11
**Branch**: `quartermaster/issue-1979` | **PR**: #2055

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
