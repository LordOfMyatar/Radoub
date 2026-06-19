# Changelog - Radoub (Shared Libraries & Cross-Cutting)

All notable changes to Radoub shared libraries (`Radoub.Formats`, `Radoub.UI`, `Radoub.Dictionary`) and cross-cutting repository work that does not belong to a single tool.

Tool-specific changes live in each tool's `CHANGELOG.md` (Parley, Manifest, Quartermaster, Fence, Relique, Trebuchet).

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Radoub.UI 0.2.13-alpha] - 2026-06-18
**Branch**: `radoub/issue-1995` | **PR**: #TBD

### Feat: Per-source palette filter — Override/HAK/Module (#1995)

- The item palette source filter splits the old binary Standard/Custom toggle into four independent checkboxes (Standard, Override, HAK, Module). The real `GameResourceSource` is now plumbed through `SharedPaletteCacheItem` (cache rebuilt under a new version) so Fence, Quartermaster, and Reliquary all distinguish all four sources. Note: module/HAK content now shows by default; Override is hidden by default.

---

## [Radoub.UI 0.2.12-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-1485` | **PR**: #2502

### Feat: MdlPartComposer wing/tail supermodel graft (#1485)

- `MdlPartComposer.Compose` gained an optional supermodel-attachment path: wing/tail MDLs graft under the body's named `wings`/`tail` bone, scaled by `WING_TAIL_SCALE`, with their authored textures preserved and their animations merged by name into the body's so they flap in sync. Consumed by the Quartermaster appearance preview (see Quartermaster CHANGELOG).

---

## [Radoub.UI 0.2.11-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-2497` | **PR**: #2500

### Feat: MTR-driven diffuse resolution + diagnostics (#2497)

- `TextureService` now resolves a mesh's diffuse from its MTR `texture0` (via the mesh `materialname`) ahead of the bare-name/`_d` chain, with the #1755 fallback preserved when no MTR applies. Forward-looking support: the common CEP/PRC HAKs ship no `.mtr` and the original beetle white-model was already fixed in #1200, so this no-ops on current content. Added `[MTR]` diagnostic logging so the first genuinely MTR-driven model is captured in logs. Consumed by the Quartermaster model preview.

---

## [Radoub.Formats 0.2.70-alpha] - 2026-06-18
**Branch**: `quartermaster/issue-2496` | **PR**: #2499

### Feat: MTR (3007) reader + MDL materialname parsing (#2496)

- Added the NWN:EE MTR material format (resource type 3007) reader and parsed the MDL `materialname` field in both binary and ASCII readers, so meshes can resolve their material/diffuse from model data instead of a `_d`-suffix guess. Format layer only; consumed by the #2482 model-preview white-model fix.

---

## [Radoub.UI] - 2026-06-14
**Branch**: `trebuchet/issue-2477` | **PR**: #2483

### Feature: ITP palette editor — Milestone 3: UI + Trebuchet host (#2477, closes #2301)

- `PaletteEditorControl` shared in `Radoub.UI`: one-window palette editing (resource-type selector, single category tree with blueprint leaves inline) with drag-drop reorganization, a virtual Uncategorized bucket, and TLK-resolved category names; hosted as a Trebuchet panel (Ctrl+5). Placement is authoritative by blueprint `PaletteID` (edits from other tools appear on Reload); every move saves immediately and checks the cross-tool file lock to avoid clobbering a blueprint open elsewhere. v1 is reorganize-only — category add/rename/delete (#2486) and full reorg undo (#2484) are follow-ups.

---

## [Radoub.UI] - 2026-06-15
**Branch**: `radoub/issue-2476` | **PR**: #2478

### Feature: ITP palette editor — Milestone 2: reorg core (dual-write) (#2476)

- Pure palette reorganization core (move/nest/reorder/add/rename/delete, drift classification) with atomic dual-write (`.itp` tree entry + blueprint `PaletteID`) and an all-or-nothing N-file save transaction.

---

## [Build 2026-06-14] - 2026-06-14
**Branch**: `radoub/issue-2362` | **PR**: #2465

### Test: Menu/Keyboard-Shortcut Coverage + Dead-Stub Lint (#2362)

- Added a lint guarding against re-introduced disabled "Not yet implemented" menu stubs (#2231 guard).
- Added per-tool menu/keyboard-shortcut FlaUI smoke tests, closing the last gap in the UI-wiring validation epic (#2359).

---

## [Radoub.UI] - 2026-06-14
**Branch**: `relique/issue-2231` | **PR**: #2466

### Undo: refuse-to-push + whole-field edit command (#2231)

- `IUndoableCommand.Do()` now returns `bool`; `UndoRedoManager` skips recording (in both Execute and Redo) when `Do()` self-rolls-back on a refresh failure, preventing undo-stack desync.
- Added `RecordedFieldEditCommand<T>` for document/whole-field undo of TwoWay-bound fields (records an already-applied edit; reverts the whole previous value). Supports Relique Sprint 1 adoption.

---

## [Radoub.Formats] - 2026-06-14
**Branch**: `trebuchet/issue-2419` | **PR**: #2463

- Added `RadoubSettings.LastSetupVersion` so tools can re-prompt first-run setup when a newer build adds settings worth reviewing (consumed by Trebuchet, #2419).

---

## [Build 2026-06-14] - 2026-06-14
**Branch**: `radoub/sprint/base-class-consolidation` | **PR**: #2457

### Sprint: Shared Base-Class Refactor Consolidation (#2441)

- Removed the obsolete SafeMode feature (`--safemode`) from all tools and Radoub.UI (#2403, supersedes #2011).
- Hoisted `SpellCheckEnabled` into base settings; migrated Manifest (#2390).
- Unified `FindWorkingDirectoryWithFallbacks` into `Radoub.Formats.Common.PathHelper`; Parley's `module.ifo` strictness preserved via an opt-in parameter (#2355).
- Hoisted `BrowserPanelWidth`/`Visible` into `BaseToolSettingsService`; migrated Relique + Reliquary (#2356).
- Wired existing theme spacing resources into a shared `SpacingStyles.axaml` (#788).

---

## [Build 2026-06-10] - 2026-06-10
**Branch**: `radoub/deps-test-sdk-2332` | **PR**: #2332

### Dependencies

- Bumped Microsoft.NET.Test.Sdk 18.5.1 → 18.6.0 (test tooling only; all 5,589 unit tests pass). Dependabot #2332.

---

## [Radoub.Formats 0.2.69-alpha] - 2026-06-14
**Branch**: `radoub/feat/itp-palette-editor` | **PR**: #2474

### ITP palette: nested-category fix + writer (#2280, #2301)

- ITP reader now preserves categories nested under other categories instead of dropping them; palette category lookups report deep categories with the correct parent path (#2280).
- New `ItpWriter` round-trips palette files, enabling future palette editing (#2301).

---

## [Radoub.Formats 0.2.68-alpha] - 2026-06-13
**Branch**: `trebuchet/issue-2442` | **PR**: #2454

### Case-preserving content replace (#2180)

- New `CaseStyle` helper and a `PreserveCase` option on the replace path (`ReplaceOperation`/`BatchReplaceService`/preview): replace now matches each occurrence's case — louis/Louis/LOUIS → lewie/Lewie/LEWIE. ResRefs and filenames stay lowercase. On by default in Marlinspike; verbatim for any consumer that doesn't set the flag.

---

## [Radoub.Formats 0.2.67-alpha] - 2026-06-10
**Branch**: `trebuchet/issue-1985` | **PR**: #2452

### HAK resolution + first-run wizard settings (#1162, #1020)

- `ModuleHakResolver.ResolveHakNames(names, searchPaths)` resolves an in-memory HAK list (no module.ifo read) to file paths in priority order and reports names it could not find. Used by Trebuchet's HAK conflict checker.
- `RadoubSettings` gained `WizardHasRun` + `AcknowledgedWizardGaps` (with `AcknowledgeWizardGaps`) to back Trebuchet's first-run / welcome-back wizard. Additive and back-compatible — older settings files load with the wizard treated as not-yet-run.

---

## [Radoub.UI 0.2.10-alpha] - 2026-06-09
**Branch**: `quartermaster/issue-2434` | **PR**: #2449

### Model preview: emitter fidelity for the fairy/pixie (#2434)

- Particle sizes now render at their authored MDL scale (the old model-radius multiply crushed small creatures' particles to specks); fairy/pixie dust matches Aurora.
- Emitter nodes follow the active animation pose, and `Billboard_to_Local_Z` quads orient to the emitter's local frame instead of always facing the camera.
- Oversized emitters are clamped to a fraction of the model radius so a stationary glow emitter (the fairy's wings) reads as a faint glow rather than a body-swallowing orb. Aurora's exact wing-blade render is undocumented — follow-up tracked separately. Follow-up to #2395.

---

## [Radoub.UI 0.2.9-alpha] - 2026-06-09
**Branch**: `reliquary/sprint/preview-and-statusbar` | **PR**: #2438

### Shared model-preview camera panel (#2430)

- New `ModelPreviewPanel` wraps `ModelPreviewGLControl` with a transparent input surface and a rotate/zoom/pan/reset camera bar (left-drag rotate, middle/Shift+left pan, wheel zoom, arrows/WASD, Home reset) plus an optional state selector. Camera input logic extracted from Quartermaster's AppearancePanel; adopted by Reliquary (QM/Relique migration to follow).

---

## [Radoub.UI 0.2.8-alpha] - 2026-06-08
**Branch**: `quartermaster/issue-2395` | **PR**: #2432

### Model preview: render particle emitters (#2395)

- Emitter nodes (glow orbs, sparkle trails, ghost wisps) now simulate and render as textured billboards in the shared model preview, animating at idle. Aurora particle math ported from rollnw + nwn_mdl_webviewer (MIT) — see README acknowledgments. Unsupported emitter modes fall back to a camera-facing billboard.

---

## [Radoub.Formats 0.2.66-alpha] - 2026-06-08
**Branch**: `quartermaster/issue-2395` | **PR**: #2432

### MDL emitter controllers + texture-width fix (#2395)

- Binary and ASCII MDL readers now parse emitter controller params (birthrate, lifeExp, velocity, size/color/alpha curves, spread, etc.) into typed fields. Fixed a pre-existing emitter texture-field width bug (32→64 bytes) that misaligned the fields after the texture name.

---

## [Radoub.UI 0.2.7-alpha] - 2026-06-07
**Branch**: `reliquary/sprint/inventory-and-defaults` | **PR**: #2420

### Shared palette-category combo binder + item-palette helpers

- `PaletteCategoryComboBinder` and `ComboBoxHelper` moved into `Radoub.UI` so item/placeable editors share one palette-category combo implementation; per-tool migrations tracked by #2421 (QM), #2422 (Fence), #2423 (Relique) (#2416).
- `ItemPaletteExclusions` extracted from Fence's per-tool list so item palettes hide creature natural weapons + the invalid marker consistently (#2411).
- `BrowserSaveNotifier.NotifyOrAddAsync` reloads + selects a new row on Save As of a not-yet-listed file, keeping the cheap in-place refresh for existing rows (#2413).

---

## [Radoub.Formats 0.2.65-alpha] - 2026-06-06
**Branch**: `trebuchet/sprint/data-loss-marlinspike` | **PR**: #2387

### ResRefValidator: clearer rejection messages (#2182)

- Length error now suggests a 16-char truncation; the invalid-character error names the offending characters instead of only restating the rule.

---

## [Radoub.UI 0.2.6-alpha] - 2026-06-07
**Branch**: `quartermaster/issue-2381` | **PR**: #2394

### Model preview: graft full-body parts (robes) as a subtree (#1989)

- A robe is a near-complete posed body (its own torso/limb hierarchy + coat/arms skin meshes), not a single part. The composer now grafts a robe's whole subtree preserving its internal transforms instead of splicing individual meshes onto skeleton bones (which collapsed/ballooned the arms). Skin meshes keep their `MdlSkinNode` type through cloning. Static-pose render now matches Aurora; per-variant residuals (Render=false limb meshes, cloak placement) tracked in #2398, animation in #2399.

---

## [Radoub.UI 0.2.5-alpha] - 2026-06-07
**Branch**: `quartermaster/issue-2381` | **PR**: #2394

### Model preview: textures follow the model's source tier (#1758)

- A creature model loaded from a HAK/Module now resolves its textures from the normal HAK-first chain instead of being forced to base-game BIF, so CEP-only skins (e.g. the green pixie `c_fairy`) render correctly instead of a stale 32×32 base stub. Base-game/Override models keep the BIF-preferred behavior (#1867 bat-wing fix). When a HAK creature's texture still falls back to a base-game stub, the preview now warns that it may be inaccurate. See follow-up refactor #2397.

---

## [Radoub.UI 0.2.4-alpha] - 2026-06-06
**Branch**: `quartermaster/issue-2381` | **PR**: #2394

### Model preview: softer lighting, less wash-out (#1762)

- Lowered preview ambient (0.95→0.70) and the midtone gamma lift (1/1.6→1/1.1) so textures show their real color instead of bleached/faded. The PBR `_d` fallback (0.2.3) made the old over-bright lighting obvious on many creatures; this restores accurate tone across all models.

---

## [Radoub.UI 0.2.3-alpha] - 2026-06-06
**Branch**: `quartermaster/issue-2381` | **PR**: #2394

### Model preview: PBR diffuse texture fallback (#1755)

- Creature skins that reference a bare texture name now fall back to the NWN:EE PBR diffuse map (`<name>_d`) when the bare name is missing. Fixes white/untextured CEP3 creatures ported from NWN2 (e.g. Txpple beetles).

---

## [Radoub.UI 0.2.2-alpha] - 2026-06-06
**Branch**: `trebuchet/sprint/data-loss-marlinspike` | **PR**: #2387

### Batch-replace preview value (#2224) + warning-color converter (#2182)

- `PendingChange.ComputedNewFieldValue` exposes the post-replace field value (literal substring substitution, no case folding) so replace previews can show the real result instead of the bare replacement term.
- New `BoolToWarningBrushConverter` — binds status text to the theme warning color so validator rejections are visible.

---

## [Radoub.UI 0.2.1-alpha] - 2026-06-06
**Branch**: `reliquary/sprint/epic-2289-followups` | **PR**: #2377

### Cross-tool dispatch: resolve target tool from settings

- `ToolDispatchService` now looks up the target tool's path from shared `RadoubSettings` (written when that tool runs) before falling back to relative-directory discovery. Fixes cross-tool "open in X" launches failing in dev/portable layouts where tools live in separate bin trees.

---

## [Docs] - 2026-06-06
**Branch**: `reliquary/issue-2367` | **PR**: #2371

### Bootstrap: require New-resource flow + audit gaps (post-Reliquary audit)

- `NEW_TOOL_BOOTSTRAP.md`: added a Day-1 requirement that every tool ship a `File → New` flow (Save-As-copy is not a substitute), plus verifiable Post-Implementation Audit rows for New-resource, Recent Files end-to-end, and no-dead-controls — gaps a 12/12 uniformity audit missed on Reliquary.
- Reliquary `CLAUDE.md`: corrected stale Status (all 7 sprints done, v0.1.0-alpha) and linked open follow-ups (#2367–#2370, #2354, #2363).

---

## [IntegrationTests 0.1.0-alpha] - 2026-06-06
**Branch**: `radoub/sprint/flaui-coverage` | **PR**: #2365

### Sprint: FlaUI depth + test reliability (#2361, #2360, #2168, #2304, #2303)

- Settings-bool persistence round-trip coverage — Parley (19 bools), Quartermaster, Trebuchet, RadoubSettings (#2361).
- UI palette/picker filter coverage — ItemFilterPanel, SearchBar, DialogBrowserPanel, shared picker match helper (#2360).
- Relique FlaUI tests — smoke + ItemBrowserPanel + deeper interaction coverage (#2168, closes #2303).
- Reliquary FlaUI tests — verified bootstrap infra and extended beyond smoke (#2304).
- FlaUI reliability hardening — a 3x soak caught the recurring "fails on first (cold) run" flake in FlaUIGlobalMutexTests (threadpool-starved `Task.Run`); fixed by switching to a dedicated foreground thread. Follow-up soak confirmation tracked in #2366.

---

## [Radoub.UI 0.1.70-alpha] - 2026-06-06
**Branch**: `reliquary/issue-2297` | **PR**: #2364

### Generic resource-path resolution for cross-tool dispatch (#2297)

- `ExternalEditorService.ResolveResourcePath(resRef, extension, …)` resolves any ResRef (not just `.nss`) to a file near the open document or module; `ResolveScriptPath` now delegates to it. Enables Reliquary's Conversation field to dispatch a `.dlg` to Parley.

---

## [Radoub.UI 0.1.69-alpha] - 2026-06-05
**Branch**: `reliquary/issue-2295` | **PR**: #2352

### Shared undo/redo foundation + cross-tool script editor (#2295, epic #2231)

- New `Radoub.UI.Undo`: `UndoRedoManager`, `IUndoableCommand`, `SetFieldCommand<T>`, `RelayUndoableCommand`. First consumer is Reliquary; foundation for the cross-tool undo epic.
- New shared `Radoub.UI.Services.ExternalEditorService` opens `.nss` scripts in the user's editor (or OS default), reading the new `RadoubSettings.CodeEditorPath`. Trebuchet's `CodeEditorPath` now persists there (one source for all tools; existing values migrate automatically).

---

## [Radoub.Formats 0.2.64-alpha] - 2026-06-05
**Branch**: `reliquary/issue-2294` | **PR**: #2349

### Add ReliquaryPath to RadoubSettings (#2294)

- New `ReliquaryPath` cross-tool discovery property so Trebuchet can launch the Reliquary placeable editor (mirrors the existing per-tool path properties).

---

## [Radoub 0.1.0-deps] - 2026-06-03
**Branch**: `radoub/issue-2332` | **PR**: #2342

### Dependabot: Avalonia + SkiaSharp patch bumps

- Avalonia framework 11.3.16 → 11.3.17 (all core packages + Headless.XUnit); SkiaSharp 3.119.2 → 3.119.4. Consolidates #2333, #2334, #2335, #2336, #2338, #2339, #2340.

---

## [Radoub.UI 0.1.68-alpha] - 2026-06-05
**Branch**: `radoub/issue-2350` | **PR**: #2351

### Delete-with-backup extracted to FileBrowserPanelBase (#2350)

- The shared file browser now backs every file up to `~/Radoub/Backups/` before deleting it, so a misclick is recoverable. Fixes silent data loss in Quartermaster, Fence, and Parley, which previously deleted browser files outright. All tools (and future ones) inherit the safe path.

---

## [Radoub.UI 0.1.67-alpha] - 2026-05-30
**Branch**: `reliquary/issue-2293` | **PR**: #2330

### Sprint 3: Shared VariablesPanel + migrate 4 tools (#2293)

- New shared `Radoub.UI.Controls.VariablesPanel` + superset `VariableViewModel` (all 5 var types: Int/Float/String/Object/Location, name validation, duplicate detection). Reliquary epic #2289 pre-work.
- Quartermaster, Fence, Relique, and Trebuchet migrated to the shared panel; tool-local `VariableViewModel.cs` and duplicate VM tests removed.
- Panel now self-validates on every edit (fixes stale validation), raises a distinct edit signal so screen switches no longer mark documents dirty, and keeps invalid numeric input instead of silently reverting it (fix or switch type to String).
- New variables seed a unique default name and focus the name cell; floats require an explicit decimal point (e.g. `5.0`); the grid scrolls when the host caps its height; Add is disabled until a document is loaded.

---

## [Radoub.UI 0.1.66-alpha] - 2026-05-30
**Branch**: `reliquary/issue-2291` | **PR**: #2328

### Sprint 1: PortraitBrowser extract (#2291)

- `PortraitBrowserWindow` moved into `Radoub.UI` driven by the shared `IPortraitBrowserContext`, so any tool can reuse it. Reliquary epic #2289 pre-work.
- Portrait thumbnails now decode on a background thread instead of blocking the dialog open.

---

## [Radoub.Formats 0.2.63-alpha] - 2026-05-30
**Branch**: `reliquary/issue-2291` | **PR**: #2328

### Sprint 1: PlaceableAppearanceService (#2291)

- New `IPlaceableAppearanceService` reads `placeables.2da` for placeable model/display names (StrRef→TLK with LABEL fallback). Reliquary epic #2289 pre-work.

---

## [Radoub docs] - 2026-05-30
**Branch**: `reliquary/issue-2290` | **PR**: #2327

### Sprint: Bootstrap doc extraction (#2290)

- New-tool bootstrap content moved out of always-loaded `CLAUDE.md` into on-demand `Documentation/NEW_TOOL_BOOTSTRAP.md`; 5 documented gaps fixed (portrait browser location, placeable appearance service, model preview control, shared variables panel, cross-tool dispatch). Reliquary epic #2289 pre-work.

---

## [Radoub.UI 0.1.65-alpha] - 2026-05-30
**Branch**: `quartermaster/issue-2220` | **PR**: #2326

### Fix: Trebuchet global font-size slider not propagating to tools (#2152)

- All bundled theme JSONs hardcode `fonts.size=14`, so `ThemeManager.ApplyFonts` reset the global font size to the theme default on every theme apply. `RadoubSettings.SharedFontSize` (Trebuchet slider) is now the sole authority for global font size; theme `fonts.size` is a fallback only when no shared value is set. Applies to all tools.

---

## [Radoub.UI 0.1.64-alpha] - 2026-05-29
**Branch**: `radoub/issue-2320` | **PR**: #2322

### Feat: Copy and Rename in file browser right-click menu (#2320)

- Shared `FileBrowserPanelBase` context menu now offers Copy (duplicate to a new ResRef) and Rename (rename the on-disk file) alongside Delete, with Aurora 16-char filename validation and browser-row refresh. Every tool's file browser gains both at once.

---

## [Radoub.UI 0.1.63-alpha] - 2026-05-29
**Branch**: `quartermaster/issue-1735` | **PR**: #2317

### Fix: Part-based creature models rendered with misplaced/accumulating parts (#1735)

- `MdlPartComposer` had three defects in part-based creature assembly: (1) seam-overlap and bounds measured each part with the mesh-local transform instead of the full world transform (parent bone chain × mesh-local), collapsing bone-parented parts to the bone origin; (2) composition mutated the cached skeleton/part models, so parts accumulated and nudges restacked on every re-render (models got "worse and worse" when toggling races); (3) the seam-overlap threshold was an absolute human-scale constant that over-nudged tiny human-proportioned creatures (Brownie), shoving the head into the chest. Seam/bounds now use world transforms, composition clones into a fresh composite without touching the cache, and the seam threshold scales with model height.

---

## [Radoub.Dictionary 0.2.4-alpha] - 2026-05-29
**Branch**: `radoub/issue-2314` | **PR**: #2315

### Fix: Color-token regex hole, CheckText O(N²), thread-unsafe DictionaryManager (#2264)

- Spell-check now ignores NWN `<cRGB>` color tokens even when a raw channel byte is `0x3E` (`>`), `CheckText` is linear in token count, and `DictionaryManager` is thread-safe.

---

## [Radoub.Dictionary 0.2.3-alpha] - 2026-05-29
**Branch**: `radoub/issue-2263` | **PR**: #2308

### Fix: Dual-singleton + no file locking causes user custom-word loss (#2263)

- Single source of truth for custom-word writes and atomic, cross-process-safe save of the user dictionary so words no longer vanish when tools run together.

---

## [Radoub.Formats 0.2.62-alpha] - 2026-05-29
**Branch**: `fence/issue-2256` | **PR**: #2306

### Feat: Shared cross-OS atomic file-replace helper (`AtomicFile.Replace`) (#2256)

- New `Radoub.Formats.Common.AtomicFile.Replace(source, dest, backupPath?)` consolidates the write-temp-then-swap pattern with a single `File.Move(overwrite:true)` — atomic on the same volume on Windows (MoveFileEx) and Unix (rename(2)). Optional `.bak` backup, handles the create-new case, and throws on a missing source. `ErfWriter.UpdateResource` and Fence's UTM save now route through it instead of inline rename logic.

---

## [Radoub.UI 0.1.62-alpha] - 2026-05-29
**Branch**: `radoub/issue-2314` | **PR**: #2315

### Fix: Marlinspike NSS checkbox is dead code — .nss files never searched (#2314)

- Marlinspike content search now discovers `.nss` (NWN script) files, so the "Include NSS" filter actually returns results.

---

## [Radoub.UI 0.1.61-alpha] - 2026-05-28
**Branch**: `radoub/issue-2262` | **PR**: #2288

### Fix: Static cache race, CT leak, async-void API, theme race, HAK dedup drops valid entries (#2262)

- Static HAK caches in `{Item,Store,Creature,Dialog}BrowserPanel` and `HakScriptScanner` made thread-safe (concurrent panel instances no longer race on writes).
- `FileBrowserPanelBase` now cancels and disposes `_indexingCts` on detach (no orphaned token/task on host teardown).
- `TokenInsertionHelper.OpenTokenWindow` replaced with awaitable `OpenTokenWindowAsync`; old `async void` shim kept only where event-handler subscription requires it.
- `ThemeManager.ApplyTheme` serialized against concurrent callers (no variant flicker / interleaved resource writes during settings+startup race).
- HAK item/store/creature/dialog browsers no longer drop valid HAK overrides; redundant inner dedup removed in favor of base-class `MergeAdditionalEntries` dedup.
- `TokenSelectorWindow` `</Start>` close-tag literal consolidated into a single `TokenDefinitions` constant.

---

## [Radoub.UI 0.1.60-alpha] - 2026-05-28
**Branch**: `relique/issue-2257-2261` | **PR**: #2283

### Fix: Stale HAK cache + ItemBrowserPanel HAK scan asymmetry (#2261)

- `SharedPaletteCacheService.HasValidSourceCache` and `LoadSourceCache` now invalidate HAK caches when the underlying HAK file no longer exists; previously deleted HAKs left their caches marked valid forever, producing ghost browser entries no extraction path could resolve. Affects every tool consuming `ISharedPaletteCacheService` (Relique, Fence, Quartermaster).
- `ItemBrowserPanel.LoadHakItemsAsync` now resolves HAK paths through `ModuleHakResolver` instead of scanning every HAK in the module + NWN/hak folders, matching `StoreBrowserPanel` and `CreatureBrowserPanel`. Eliminates slow Relique cold starts on large HAK collections (CEP, PRC) and removes irrelevant items from the item browser. Tool-specific notes in [Relique CHANGELOG](Relique/CHANGELOG.md#01020-alpha--2026-05-28).

---

## [Radoub.Formats 0.2.61-alpha] - 2026-05-27
**Branch**: `quartermaster/issue-2249` | **PR**: #2275

### Feat: Add CreatureCloning helper for runtime-type-preserving deep clone + extension-dispatched save (#2249)

- New `Radoub.Formats.Utc.CreatureCloning` with `Clone` (preserves `BicFile` runtime type so player-only fields survive a round-trip) and `Save` (dispatches BIC/UTC based on file extension, throws on `.bic` paired with non-BIC). Used by Quartermaster's Down-Level flow to stop silent BIC→UTC corruption. 14 new tests.

---

## [Radoub.Formats 0.2.60-alpha] - 2026-05-26
**Branch**: `radoub/issue-2242` | **PR**: #2272

### Fix: UTF-8 vs CP-1252 encoding mismatch corrupts accented characters (#2242)

- `GffWriter` (CExoString / CExoLocString), `ErfReader`/`ErfWriter` localized strings, and `TwoDAReader` switched from UTF-8 to Windows-1252 to match NWN1 native encoding (per [neverwinter.nim util.nim](https://github.com/niv/neverwinter.nim/blob/master/neverwinter/util.nim) `toNwnEncoding`/`fromNwnEncoding`). Round-tripping vanilla NWN files with accented characters (é, ü, ß) no longer rewrites bytes incorrectly. Affects German/Polish 2DA builds, French dialog, localized item descriptions. `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` hoisted out of per-call hot path in `GffReader` to static ctor.

---

## [Radoub.Formats 0.2.59] - 2026-05-26
**Branch**: `radoub/issue-2243` | **PR**: #2269

### Fix: Narrow SsfReader bare-catch to format exceptions (#2243)

- `SsfReader` bare `catch (Exception)` at line 73 replaced with specific handlers for `EndOfStreamException`, `InvalidDataException`, and `ArgumentException`. Unexpected exceptions (OOM, etc.) now propagate instead of being silently swallowed behind a WARN log. Null-return contract preserved for format-level corruption.

---

## [Radoub.Formats 0.2.58] - 2026-05-26
**Branch**: `radoub/issue-2244` | **PR**: #2266

### Fix: Parser hardening — integer overflow, atomic writes, silent truncation (#2244)

- ERF/KEY/BIF: uint arithmetic that wrapped before bounds checks now uses long-promoted comparisons.
- `ErfWriter.UpdateResource` switched to `File.Replace` (atomic — original survives mid-rename failures).
- ERF `ResId` preserved instead of silently renumbered to sequential index.
- GFF: `ReadCExoString` / `CExoLocString` no longer abandon entire string tables on a single oversized entry; bare `catch {}` in `GffFile.GetFieldValue<T>` narrowed and logged.
- TLK: `CleanResRef` no longer strips mid-string whitespace asymmetric with the writer.
- Dead UTF-8 fallback removed.

---

## [Radoub.Formats 0.2.57] - 2026-05-25
**Branch**: `radoub/issue-2241` | **PR**: #2265

### Fix: GFF 64-bit field types silently corrupt on round-trip (#2241)

- DWORD64/INT64/DOUBLE were classified as simple types and read/written as 32-bit, silently zeroing values on save and producing wrong floats on load. Now treated as complex types per Aurora spec — value stored as 8 bytes in `FieldData` section. Affects every tool consuming GFF (UTC, UTI, UTM, BIC, IFO, JRL, DLG).

---

## [Radoub.Formats 0.2.56] - 2026-05-25
**Branch**: `radoub/issue-2238` | **PR**: #2239

### Fix: Severe memory bloat — idle RSS dropped from multi-GB to ~377 MB (#2238)

- HAK index loading switched to `ErfReader.ReadMetadataOnly` so the resolver no longer buffers entire HAK byte arrays on the Large Object Heap. Eliminates the OOM-kill and hard-reboot risk when running multiple Radoub apps concurrently against large-HAK modules (CEP3 etc.).

---

## Establishing this CHANGELOG (#2203)

Shared-library work (`Radoub.Formats`, `Radoub.UI`, `Radoub.Dictionary`) previously had no logging home — entries were either omitted entirely or duplicated into every tool CHANGELOG. This file is the canonical record for shared-library and cross-cutting changes going forward; per-tool CHANGELOGs continue to cover tool-specific work without duplication.

---
