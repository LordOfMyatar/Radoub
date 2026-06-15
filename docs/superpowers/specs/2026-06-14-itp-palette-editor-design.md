# ITP Palette Editor — Design Spec

Date: 2026-06-14
Issues: #2301 (hosting/scope), #2302 (drag-drop reorg UX), depends on #2280 (nested-category parse bug)
Status: Approved design, pending implementation plan

## Table of Contents

1. [Purpose](#purpose)
2. [Background: how palettes are stored](#background-how-palettes-are-stored)
3. [The data model that drives everything](#the-data-model-that-drives-everything)
4. [Architecture](#architecture)
5. [Components](#components)
6. [Reorganization model](#reorganization-model)
7. [Error handling and data integrity](#error-handling-and-data-integrity)
8. [Scope](#scope)
9. [Build order](#build-order)
10. [Open follow-ups](#open-follow-ups)

## Purpose

No Radoub tool can author or reorganize an Aurora palette (`.itp`). Palettes
define the category tree that DMs and builders browse in the Aurora toolset and
the in-game DM client; a poorly organized palette directly hurts DM usability.
Today every tool rolls its own palette-category dropdown, blueprints default to
`custom1`, and there is no way to move a blueprint to a different category or to
edit the category tree itself.

The primary verb of this tool is **reorganization** — moving blueprints between
categories and moving/nesting/renaming the category folders themselves — in one
unified window with a Windows-Explorer feel and drag-and-drop.

## Background: how palettes are stored

Confirmed against the on-disk game files (not assumed):

Custom palettes (`itempalcus.itp`, `creaturepalcus.itp`, `placeablepalcus.itp`,
`storepalcus.itp`, etc.) live **loose in the module folder**. Every module on
disk carries the full set. These hold the per-module blueprint placements and are
exactly what the Aurora toolset writes. v1 reads and writes these — no archive
writing is required.

Skeleton / standard palettes (`itempalstd`, etc.) define the category-tree
template and ship in the base-game BIFs; a module may override them via Override
or HAK. These are resolved through the existing Override -> HAK -> BIF chain. v1
treats them as read-only (writing into a HAK requires ERF/HAK archive writing,
tracked separately in #1577).

So ITP files are not HAK-only. The file v1 edits — the loose module `*palcus.itp`
— needs no HAK/ERF writing.

## The data model that drives everything

A blueprint's category membership is stored in **two places that must agree**:

1. The blueprint file's own `PaletteID` byte (`UtiFile`, `UtpFile`, `UtmFile`,
   `UtcFile` all carry it, with existing round-trip tests).
2. The custom palette tree, which lists that ResRef under the category whose
   `Id == PaletteID`.

Consequences:

- Recategorizing a blueprint is a **dual write**: update the `.itp` tree entry
  AND rewrite the blueprint file's `PaletteID`. The editor edits blueprint files
  as a deliberate side effect of reorganizing.
- A **mismatch** between the two (blueprint `PaletteID` says category 7, but the
  palette lists the ResRef elsewhere or not at all) is what produces an
  "uncategorized" or misplaced blueprint. This is sometimes intentional
  (security-through-obscurity: a builder leaves something uncategorized to hide
  it). The editor must surface mismatches but never auto-fix them.

Decisions:

- Dual write is **atomic**: both the `.itp` entry and the blueprint `PaletteID`
  are written as one transactional unit, both-or-neither.
- For display, the **palette tree wins**. A blueprint whose own `PaletteID`
  disagrees with the tree is shown per the tree and flagged as *drifted*.
- "Uncategorized" = blueprints with no usable placement (no tree entry, or a
  `PaletteID` pointing at a nonexistent category). It is the reconciliation
  surface, never auto-filed.

## Architecture

A shared `Radoub.UI` control, `PaletteEditorControl`, hosted as a panel in
Trebuchet for v1 and embeddable in individual tools later. One window:

- Resource-type selector (item / creature / placeable / store / ...).
- Category tree on the left (the `.itp` structure).
- Blueprint pool on the right (loose module files of the selected type).
- Save.

Data flow:

```
Module folder -> ItpReader      -> ItpFile (in-memory tree) -> PaletteEditorViewModel
Module folder -> blueprint reader -> blueprint PaletteID      ->
                                                                   | user reorg
                                                                   v
                          mutated ItpFile + mutated blueprint PaletteID(s)
                                                                   | Save (atomic, both files)
                          ItpWriter  -> *palcus.itp
                          blueprint writer -> *.uti/.utc/.utp/.utm
```

One palette per resource type at a time. Each maps to its `*palcus` file and its
`*palstd` skeleton (for category-name resolution).

## Components

Format layer (`Radoub.Formats`), built and tested first:

1. `PaletteCategoryNode` gains child-category support (#2280 fix). Today a
   category can only hold blueprints. The model, the reader
   (`ItpReader.ParseCategoryNode`), and the flatten walkers (`FlattenCategories`,
   `GameDataService.ExtractCategories`) must allow and recurse through
   category-under-category nesting. Without this, deep trees silently collapse.
2. `ItpWriter` (new). Serializes `ItpFile` back to GFF/ITP bytes with round-trip
   fidelity. This is the missing half of the format — a reader exists, no writer.

Blueprint writers already exist (Relique UTI, Reliquary UTP, Fence UTM, QM UTC),
each with `PaletteID` round-trip tests. v1 reuses them; the only new writer is
`ItpWriter`.

UI layer (`Radoub.UI`):

3. `PaletteEditorControl` — the one-window view (type selector, tree, pool, save).
4. `PaletteEditorViewModel` — holds the working `ItpFile` and the loose-file pool;
   exposes the tree and the reorg operations.
5. `PaletteReorgMutator` (pure helper, mirrors the Relique `PropertyListMutator`
   pattern) — the mutate-and-rollback core for every reorg op, unit-testable
   without FlaUI. Each operation is a pure mutation:
   - `MoveBlueprint(bp, fromCat, toCat)` — dual write (tree + `PaletteID`)
   - `MoveCategory(cat, newParent, index)`
   - `AddCategory(parent)` / `RenameCategory(cat)` / `RemoveCategory(cat)`
   - `ReorderWithin(parent, oldIndex, newIndex)`

## Reorganization model

Reorganization is the headline capability. Interactions:

- Drag a blueprint from one category to another, or out of the Uncategorized
  bucket into a category. This recategorizes (dual write).
- Drag a category onto another to nest it, or between siblings to reorder.
- Add / rename / delete category folders.
- Undo/redo on every operation (plugs into the existing undo model from epic
  #2231 — document/whole-field granularity for blueprint editors).

Uncategorized bucket:

- A virtual top-level node, e.g. `⚠ Uncategorized (n)`, listing blueprints with no
  usable placement. It is a view projection only — never written to the `.itp` as
  a real category. Dragging an item out files it; nothing forces filing.

### Reorg operation semantics

These rules are the contract for `PaletteReorgMutator` and must be enforced as
preconditions/postconditions, each unit-tested:

- Delete a non-empty category. Blocked by default: deleting a category that
  contains blueprints or child categories prompts, and on confirm the contents
  are **reparented to Uncategorized** (blueprints) or to the deleted category's
  parent (child categories) — never cascade-deleted, never orphaned. Reparented
  blueprints become drifted until refiled, which is the honest reflection of what
  removing a category does.
- Cycle guard. `MoveCategory(cat, newParent, index)` rejects any `newParent` that
  is `cat` itself or a descendant of `cat` (ancestor-check precondition). A drag
  that would create a cycle is refused, not silently dropped.
- ID retirement. A deleted category's `Id` is **retired, never recycled**.
  `NextUseableId` only ever advances. Reusing a freed `Id` while a stale blueprint
  `PaletteID` still references it would silently mis-file that blueprint.
- `NextUseableId` source for custom palettes. Custom (`*palcus`) files may lack
  the `NextUseableId`/`ResType` fields (they are skeleton-palette fields). When
  allocating a new category Id, source the starting value from the paired
  `*palstd` skeleton (already loaded for name resolution); if neither file
  provides it, fall back to `max(existing Id) + 1`. Always advance, never reuse.
- Structural-only vs blueprint-touching ops. `AddCategory` / `RenameCategory` /
  `MoveCategory` / `ReorderWithin` write only the `.itp` (they never change a
  blueprint's `PaletteID`). Only `MoveBlueprint` (and delete-with-reparent, which
  moves blueprints to Uncategorized) is a blueprint-touching op.

### Classifying a blueprint (drift vs uncategorized)

Evaluated against the loaded tree:

- ResRef appears nowhere in the tree -> **Uncategorized**, regardless of whether
  its `PaletteID` names a valid category. (Not listed = not filed.)
- ResRef appears in the tree under category X, but its `PaletteID != X.Id` ->
  **Drifted**. Displayed under X (palette tree wins), flagged.
- ResRef appears in the tree under category X and `PaletteID == X.Id` -> in sync.

Resolving drift is a deliberate user action: dragging the blueprint (even onto its
current tree category) re-runs the dual write and re-syncs its `PaletteID` to the
tree. The flag is paired with this action, not just a passive warning.

Blueprint pool (right pane): blueprints physically present in the module folder
(loose `.uti`/`.utc`/`.utp`/`.utm` of the selected type). These are the things a
builder actually files.

## Error handling and data integrity

Cardinal rule: never silently corrupt a palette. The format-first sequencing
exists so the writer is trustworthy before any UI touches it.

Round-trip guard (writer acceptance gate): read -> write -> read again must
produce a structurally identical tree for every real on-disk palette
(`base_mod` ~4 KB, CEP starter ~29 KB `itempalcus.itp`) plus a hand-built
deep-nested fixture for #2280. Aurora tolerates field reordering but not lost
placements; assert same categories, IDs, blueprint placements, and nesting.

Save safety (an N-file transaction, not just a pair):

- A single recategorize touches `.itp` + one blueprint, but a category
  move or delete-with-reparent can change the effective placement of many
  blueprints at once. The save transaction is therefore `.itp` + *every touched
  blueprint*. Stage all writes to temp files, validate each re-reads, then
  atomically replace the originals. A failed write at any stage aborts the whole
  commit — all-or-nothing across every file. This N-file path is the
  highest-corruption-risk operation and gets dedicated rollback tests.
- Aurora 16-char filename constraints still apply (standard palette names like
  `itempalcus` are already safe).

Reorg integrity:

- Category ID stability — blueprints reference categories by `byte Id`
  (`PaletteID`). Moving/reordering categories must preserve IDs, never renumber,
  or every blueprint placement silently breaks. New categories draw from
  `NextUseableId`.
- Mutate-refresh-rollback on every drop (the Relique `PropertyListMutator`
  pattern): if the tree refresh throws after a move, the model change rolls back
  so UI and data never diverge.
- Drift: a blueprint whose `PaletteID` disagrees with its tree placement is shown
  per the tree and flagged; it is never silently rewritten outside an explicit
  reorg.
- Uncategorized is never written — it is a view projection; saving emits only real
  categories.

Read resilience: unknown/malformed nodes are logged at WARN and skipped, not
fatal (matches existing `ExtractCategories` behavior). A partially malformed
palette still opens.

Testing: round-trip tests in `Radoub.Formats.Tests`; pure `PaletteReorgMutator`
tests for every reorg op including ID-preservation, drift detection, and two-file
rollback; FlaUI smoke only on explicit request per repo policy.

## Scope

In scope for v1:

- `ItpWriter` + #2280 nested-category fix, with round-trip tests (format
  milestone, done first).
- `PaletteEditorControl` shared in `Radoub.UI`, hosted as a Trebuchet panel.
- One window: type selector -> category tree (left) -> loose-file blueprint pool
  (right).
- Reorganize: drag blueprints between categories, drag categories to
  nest/reorder, add/rename/delete categories.
- Atomic dual write (`.itp` entry + blueprint `PaletteID`) with two-file
  transaction and rollback.
- Virtual read-only Uncategorized bucket; drift flagging.
- Atomic backup-on-save; category-ID preservation; undo/redo.

Out of scope for v1 (YAGNI):

- No HAK/ERF writing — skeleton/std palettes stay read-only (waits on #1577).
- No writing to Override.
- No embedding in individual tools yet — built shared so it can later; v1 ships
  only the Trebuchet host.
- No blueprint content editing beyond `PaletteID` — this organizes placements; it
  does not edit item/creature stats (that is what Relique/QM/Fence/Reliquary do).
- No editing more than one resource-type palette at a time.

## Build order

1. Format milestone — `PaletteCategoryNode` child-category support + reader
   recursion (#2280), then `ItpWriter`, with round-trip tests against
   base_mod / CEP / deep-nested fixtures. TDD: tests first. (Blueprint writers
   already exist and are reused.)
2. Reorg core — `PaletteReorgMutator` + `PaletteEditorViewModel` operations and
   the dual-write transaction, pure unit tests (move/nest/reorder/add/rename/
   delete, ID-preservation, ID-retirement, cycle guard, delete-non-empty
   reparenting, drift vs uncategorized classification, N-file rollback).
3. UI — `PaletteEditorControl` (tree + pool + drag-drop + Uncategorized bucket +
   drift flagging), Trebuchet panel host, undo/redo wiring.
4. Manual spot-check list for the drag-drop UX before pre-merge.

Closing: #2301 (hosting decided — shared control in Trebuchet) and #2302
(drag-drop reorg). Depends on #2280.

## Open follow-ups

- HAK/ERF write support (#1577) would let v2 edit skeleton/std palettes and write
  palettes into HAKs.
- Embedding `PaletteEditorControl` directly in each tool (post-v1).
