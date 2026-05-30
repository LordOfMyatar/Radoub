# Changelog - Radoub (Shared Libraries & Cross-Cutting)

All notable changes to Radoub shared libraries (`Radoub.Formats`, `Radoub.UI`, `Radoub.Dictionary`) and cross-cutting repository work that does not belong to a single tool.

Tool-specific changes live in each tool's `CHANGELOG.md` (Parley, Manifest, Quartermaster, Fence, Relique, Trebuchet).

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

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
