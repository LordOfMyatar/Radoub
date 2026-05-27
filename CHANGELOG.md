# Changelog - Radoub (Shared Libraries & Cross-Cutting)

All notable changes to Radoub shared libraries (`Radoub.Formats`, `Radoub.UI`, `Radoub.Dictionary`) and cross-cutting repository work that does not belong to a single tool.

Tool-specific changes live in each tool's `CHANGELOG.md` (Parley, Manifest, Quartermaster, Fence, Relique, Trebuchet).

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

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
