# CLAUDE.md - Radoub Toolset

Project guidance for Claude Code sessions working with the Radoub multi-tool repository.

---

## Repository Overview

**Radoub** is a multi-tool repository for Neverwinter Nights modding. Each tool maintains its own codebase, documentation, and development workflow within its subdirectory.

### Current Tools

| Tool | Abbrev | Description | CLAUDE.md |
|------|--------|-------------|-----------|
| **Parley** | PAR | Dialog editor (`.dlg` files) | `Parley/CLAUDE.md` |
| **Manifest** | MAN | Journal editor (`.jrl` files) | `Manifest/CLAUDE.md` |
| **Quartermaster** | QM | Creature/inventory editor (`.utc`, `.bic` files) | `Quartermaster/CLAUDE.md` |
| **Fence** | FEN | Merchant/store editor (`.utm` files) | `Fence/CLAUDE.md` |
| **Relique** | REL | Item blueprint editor (`.uti` files) | `Relique/CLAUDE.md` |
| **Reliquary** | RLQ | Placeable blueprint editor (`.utp` files; namespace `PlaceableEditor`) | `Reliquary/CLAUDE.md` |
| **Trebuchet** | TRE | Radoub launcher/hub | `Trebuchet/CLAUDE.md` |
| **Marlinspike** | MAR | Search & replace across files (lives in Trebuchet) | `Trebuchet/CLAUDE.md` |
| **Radoub** | RAD | Repository-level / shared | This file |

### Shared Libraries

- **Radoub.Formats**: Aurora Engine file format parsers (KEY, BIF, etc.) - Shared library for all tools

### Planned Tools

None currently. Future tools land as subdirectories with their own README, CLAUDE.md, and development infrastructure. Bootstrap follows the [New Tool Bootstrap guide](Documentation/NEW_TOOL_BOOTSTRAP.md).

---

## Repository Structure

```
Radoub/
├── README.md (landing page)
├── LICENSE
├── CLAUDE.md (this file - repo-level guidance)
├── Radoub.sln (builds all tools; excludes Windows-only integration tests)
├── .gitignore
├── .claude/commands/ (slash commands for Claude Code)
├── Documentation/ (Aurora Engine format specs - shared across tools)
│   └── BioWare_Original_PDFs/ (original BioWare PDFs)
├── About/ (project history and AI collaboration documentation)
│   ├── CLAUDE_DEVELOPMENT_TIMELINE.md
│   └── ON_USING_CLAUDE.md
├── NonPublic/ (private docs, specs, research — NOT in git)
│   ├── Relique/ (Relique specs, plans, research)
│   ├── Parley/ (Parley specs, plans, research)
│   ├── Quartermaster/ (QM specs, plans, research)
│   ├── Fence/ (Fence assets, research)
│   └── Trebuchet/ (Trebuchet research)
├── Parley/ (dialog editor)
│   ├── README.md
│   ├── CLAUDE.md (Parley-specific guidance)
│   ├── CHANGELOG.md (Parley-specific changes)
│   ├── Parley/ (source code)
│   ├── TestingTools/
│   ├── Documentation/ (Approved Parley-specific docs)
├── Manifest/ (journal editor)
│   ├── CLAUDE.md (Manifest-specific guidance)
│   ├── CHANGELOG.md (Manifest-specific changes)
│   ├── Manifest/ (source code)
│   └── Manifest.Tests/ (unit tests)
├── Quartermaster/ (creature/inventory editor)
│   ├── CLAUDE.md (Quartermaster-specific guidance)
│   ├── CHANGELOG.md (Quartermaster-specific changes)
│   ├── Quartermaster/ (source code)
│   └── Quartermaster.Tests/ (unit tests)
├── Fence/ (merchant/store editor)
│   ├── CLAUDE.md (Fence-specific guidance)
│   ├── CHANGELOG.md (Fence-specific changes)
│   ├── Fence/ (source code)
│   └── Fence.Tests/ (unit tests)
├── Relique/ (item blueprint editor)
│   ├── CLAUDE.md (Relique-specific guidance)
│   ├── CHANGELOG.md (Relique-specific changes)
│   ├── Relique/ (source code, namespace: ItemEditor)
│   └── Relique.Tests/ (unit tests)
├── Trebuchet/ (launcher/hub)
│   ├── CLAUDE.md (Trebuchet-specific guidance)
│   ├── CHANGELOG.md (Trebuchet-specific changes)
│   ├── Trebuchet/ (source code)
│   └── Trebuchet.Tests/ (unit tests)
├── Radoub.Formats/ (shared library)
│   ├── Radoub.Formats.sln
│   ├── Radoub.Formats/ (source code)
│   └── Radoub.Formats.Tests/ (unit tests)
└── [Future tools will be added here]
```

---

## Working with Multiple Tools

### Building

**Root-level solution**: Use `Radoub.sln` to build all tools at once:

```bash
# Build all tools (excludes Windows-only integration tests)
dotnet build Radoub.sln

# Build with release configuration
dotnet build Radoub.sln --configuration Release
```

**Individual tool builds**:
```bash
dotnet build Parley/Parley.sln
dotnet build Manifest/Manifest/Manifest.csproj
dotnet build Quartermaster/Quartermaster/Quartermaster.csproj
```

**Note**: `Radoub.IntegrationTests` is excluded from `Radoub.sln` because it targets `net9.0-windows` (FlaUI requires Windows).

### Tool-Specific Work

When working on a specific tool (e.g., Parley):
1. **Always read the tool's CLAUDE.md first** (`Parley/CLAUDE.md`)
2. Follow tool-specific conventions and workflows
3. Tool-specific issues/PRs reference the tool in title: `[Parley] Fix parser bug`
4. Run tool-specific tests before committing

### Shared Resources

**Public Documentation** (`Documentation/`):
- Shared across all tools
- Contains original BioWare PDF format specifications
- Read-only reference material
- Markdown conversions of BioWare docs are now in the Wiki (see Resources section)
- **NEVER read PDF files without explicit user instruction** - PDFs can exceed context limits and cause "prompt too long" errors

**About Documentation** (`About/`):
- Project history and development experience
- AI collaboration documentation
- Updated when major project milestones occur
- Never edit ON_USING_CLAUDE.md; only Lord makes changes to that.

**NonPublic Docs**
- `NonPublic/` (repo root, gitignored) holds private specs, plans, and research. It is never committed.
- The human will move approved docs to public after review.

### Cross-Tool Work

If work affects multiple tools:
- Create separate commits per tool when possible
- Use clear commit messages: `[Parley] Update parser` + `[FutureTool] Update importer`
- Test all affected tools before committing
- Document cross-tool dependencies

---

## Commit Standards

Use tool prefixes in commit messages:

```
[Parley] fix: Resolve parser buffer overflow
[Parley] feat: Add script parameter preview
[Radoub] docs: Update main README
[Radoub] chore: Update shared BioWare documentation
```

### Commit Types
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation only
- `refactor:` - Code organization without feature changes
- `test:` - Test additions or improvements
- `chore:` - Maintenance tasks

### Tool-Specific Standards

Tool-specific CLAUDE.md files may add enforcement rules:
- **Parley**: PR length limits (15 sentences max), Claude enforcement role for workflow discipline
- **Binary format tools**: Stricter pre-commit testing requirements

Tool-specific standards take precedence over repository-wide standards when they conflict.

---

## Branch Workflow

**Single main branch approach** (solo developer, rapid iteration):

**Main Branch**:
- `main` - Production-ready releases (protected by PR review process)

**Feature Branches**:
- Tool-specific: `parley/feature/name` or `parley/fix/name`
- Cross-tool: `radoub/feature/name`
- Documentation: `docs/name`

**Workflow**:
```
main (production)
  └── feature/fix branches → PR → main
```

**Important**:
- All work via Pull Requests, even for "small" changes
- PRs protect main branch (review before merge)
- GitHub releases mark stable milestones
- If project grows to multiple contributors, reconsider adding a `develop` integration branch

---

## Starting a New Feature Branch

**Command**: "Init a new feature for [tool] epic/issue #[number]"

**Example**: "Init a new feature for Parley epic #37"

**Process**:
1. Sync with main branch (`git checkout main && git pull`)
2. Create feature branch following naming convention:
   - Epic: `[tool]/feat/epic-[N]-[short-name]`
   - Feature: `[tool]/feat/[short-name]`
   - Fix: `[tool]/fix/[short-name]`
3. Update tool's CHANGELOG.md with a new versioned section (highlights only, never use `[Unreleased]`)
4. Commit and push branch
5. Create draft PR on GitHub
6. Update CHANGELOG.md with actual PR number
7. Commit and push PR number update

**Example CHANGELOG Entry**:
```markdown
## [0.1.5-alpha] - 2026-03-28
**Branch**: `parley/feat/epic-0-plugins` | **PR**: #84

### Epic 0: Plugin Foundation
- Plugin discovery and loading framework
- Hot-reload support for development
```

**Benefits**:
- CHANGELOG highlights give users a quick overview of what changed
- Draft PR created early for visibility and discussion
- Git history preserves all implementation details

---

## New Tool Bootstrap

Starting a new tool? Read `Documentation/NEW_TOOL_BOOTSTRAP.md` — the full checklist
(required components, shared-library references, Trebuchet integration, cross-tool dispatch,
UI uniformity, file-browser adoption, versioning, common mistakes). Not auto-loaded every session.

---

## Test-Driven Development (TDD) Policy

**MANDATORY**: Before writing implementation code, check this table. If TDD is required, write the failing test FIRST. Do not skip this step.

| Scenario | TDD Required? | Action |
|----------|--------------|--------|
| New feature / service / parser | **Yes** | Write failing test → implement → verify |
| New shared library or cross-tool code | **Yes** | Write failing test → implement → verify |
| Bug fix (reproducible) | **Yes** | Write failing test that reproduces bug → fix → verify |
| Bug fix (investigation needed) | No | Debug first, add regression test after |
| UI layout / styling / AXAML only | No | Manual verification |
| Config / documentation only | No | No tests needed |

**If you catch yourself writing implementation code without a test for a "Yes" scenario, STOP. Write the test first, then continue.**

**TDD Workflow**:
1. Write a failing test that describes the expected behavior
2. Run the test — confirm it fails for the right reason
3. Write the minimum code to make the test pass
4. Refactor if needed (tests still pass)
5. Repeat

---

## Testing Requirements

**Before committing**: run the affected tool's tests, confirm it builds, and check for
hardcoded paths.

**Before a PR to main**: all tools build, all tests pass, private docs are in
`NonPublic/`, public docs are approved, and each affected tool's CHANGELOG has a
versioned section — never `[Unreleased]`.

### Test output (MANDATORY)

**Never pipe test output through `tail` or `head`.** It discards failures and produces
false "all tests pass" claims. Run in the background, then grep the output file:

```bash
dotnet test Radoub.sln --no-build   # with run_in_background=true
grep -E "^(Failed|Passed|  Failed)" $OUTPUT_FILE
```

The `Failed!`/`Passed!` summary plus `  Failed` detail lines give full visibility. Read
the whole file when failures appear.

### FlaUI window focus (CRITICAL)

**Never call `Keyboard.TypeSimultaneously()` directly** — keystrokes reach the focused
window, which may not be the test app. VSCode stealing focus turns Ctrl+Shift+E into its
file explorer instead of a test action.

Use `FlaUITestBase` helpers instead: `SendCtrlS()`, `SendCtrlZ()`, `SendCtrlY()`,
`SendCtrlD()`, or `SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_X)`
for custom combinations. Call `EnsureFocused()` before any raw keyboard input.

### FlaUI stability (Avalonia)

Closing an Avalonia app mid-render crashes it in `SkiaSharp.SKCanvas.Flush()`. Close via
`App.Close()` — not Alt+F4, which hits whatever holds focus — after a delay that lets the
compositor drain, and wait for the process to exit before the next test. See
`FlaUITestBase.StopApplication()`.

Execution is serialized two ways (#1526): `AssemblyInfo.cs` carries
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` within the assembly,
and `FlaUITestBase` holds a named system mutex (`Global\Radoub.FlaUI.SerialExecution`,
30 s timeout) across processes. A terminal run colliding with IDE Test Explorer blocks on
the mutex with a clear error instead of racing for the desktop.

---

## Sprint Workflow

Per item: TDD check (see the TDD Policy table) → implement → verify → commit and
push. One commit per discrete item, each referencing the sprint issue. Every commit
should leave a working state, so a bad item is easy to revert or bisect.

```
git add . && git commit -m "[Radoub] feat: Add UTC reader for creature blueprints (#549)"
```

### Sprint completion — manual spot-check list

Before `/pre-merge`, list what a human must verify. Skip the list only when the
sprint is purely internal — refactoring, tests, or docs — or fully covered by
automated tests.

Write spot-checks for:

- UI visuals: icons, colors, layout, themes
- User-facing behavior: shortcuts, menu items, dialogs
- Event wiring that should produce a visible update
- File format changes — round-trip a real file
- Platform-dependent behavior, tagged `(Linux)` (see below)

Format:

```markdown
### Manual Spot-Checks

Verify in the running app before `/pre-merge`:

- [ ] [Specific thing] — [where/how to verify]
- [ ] (Linux) [Platform-specific thing] — verify on a Linux box, not just Windows
```

Be specific and say how to trigger the behavior; "check the UI" helps nobody. List
only what automated tests cannot cover.

**Linux tagging.** Development happens on Windows, so a Windows-only check can pass
while the Linux path is broken. Tag any item whose backend differs and name the
dependency. When unsure, tag it. Common triggers:

- **Filesystem**: case sensitivity, separators, symlinks, permissions, temp-dir
  resolution (`Path.GetTempPath()`, `SpecialFolder`), delete-while-open semantics
- **Audio/TTS**: Windows `System.Speech` vs Linux Piper/`espeak-ng` vs macOS `say`;
  playback (`aplay`/`afplay`); stop semantics (#2523 — `Stop()` fires completion
  synchronously on Windows, silently on Piper)
- **Process launch**: external editors, spawned CLIs, argument quoting, PATH lookup
- **Native/interop**: SkiaSharp/OpenGL, platform packages, `[SupportedOSPlatform]`
- Keep it short (3-8 items typical)

**Example** (for a sprint with a new FlowView icon + theme fixes + a TTS stop fix):
```markdown
### Manual Spot-Checks

- [ ] 📋 icon appears in FlowView when quest tag is set on a node
- [ ] 📋 icon disappears when quest tag is cleared (✕ button)
- [ ] 📋 icon appears when quest selected via Browse dialog
- [ ] Browser window text readable on dark theme (no white-on-white)
- [ ] Browser window text readable on light theme (no invisible text)
- [ ] (Linux) TTS Stop halts in-progress speech — Piper/espeak backend differs from Windows;
      verify audio actually stops and does not auto-advance
- [ ] (Linux) File open/save round-trips with a module on a case-sensitive filesystem
```

---

## Spike Solutions

Spikes are **timeboxed throwaway prototypes** (from XP/Extreme Programming) used to reduce uncertainty before committing to an implementation approach.

**When to Spike**:

| Situation | Spike? |
|-----------|--------|
| New file format parser (unknown edge cases) | Yes |
| Unfamiliar 2DA interaction pattern | Yes |
| UI pattern not yet used in Radoub | Yes |
| Library evaluation (performance, compatibility) | Yes |
| Well-understood feature addition | No |
| Bug fix with clear reproduction | No |
| Test-only work | No |

**Spike vs Research**:
- `/research` = information gathering (reading, no code changes)
- `/research --spike` = hands-on prototyping (throwaway code on a disposable branch)

**Workflow**: Run `/research --spike [topic or #issue]` to start. Creates a throwaway branch, sets a timebox, and on completion generates a findings document in `NonPublic/[Tool]/Research/spike-[topic].md`. The spike branch is **never merged** — it is deleted after findings are captured.

**Rules**:
- Never merge a spike branch
- Always capture findings before deleting the branch
- Respect the timebox — document what you have and stop
- One question at a time — log new questions for future spikes

---

## Documentation Standards

Follow the same standards as Parley (see `Parley/CLAUDE.md`):
- ATX headers (no decorative bold/italic)
- Google Docs compatible formatting (minimal bolding)
- Table of Contents for docs over 2 pages
- Privacy-safe examples (no real usernames/paths)

---

## CHANGELOG Management

**Per-tool CHANGELOGs for tool-specific work, plus a root `/CHANGELOG.md` for shared-library and cross-cutting work.** Git history is the detailed archive.

| Location | Contents |
|----------|----------|
| `/CHANGELOG.md` (repo root) | `Radoub.Formats`, `Radoub.UI`, `Radoub.Dictionary`, and cross-cutting work that does not belong to a single tool |
| `Parley/CHANGELOG.md` | Parley highlights |
| `Manifest/CHANGELOG.md` | Manifest highlights |
| `Quartermaster/CHANGELOG.md` | Quartermaster highlights |
| `Fence/CHANGELOG.md` | Fence highlights |
| `Relique/CHANGELOG.md` | Relique highlights |
| `Trebuchet/CHANGELOG.md` | Trebuchet highlights |

**Routing rules**:
- Pure shared-library change (no immediate tool-facing behavior) → root `/CHANGELOG.md` only
- Shared-library change with tool-visible impact → root `/CHANGELOG.md` is the canonical entry; tool CHANGELOGs may add a one-liner that links the root entry (no duplication of details)
- Tool-specific work → tool CHANGELOG only
- Cross-tool sprint touching multiple tools → each affected tool gets its own one-liner; root `/CHANGELOG.md` only if a shared-library change is also part of the sprint

**Rules**:
- Highlights only — major features, breaking changes, notable fixes
- Git history is the detailed archive
- No cross-references between tool CHANGELOGs (shared-lib changes go in root)
- Never use `[Unreleased]` — all entries go in versioned sections
- One entry per feature, not implementation checklists
- Root `/CHANGELOG.md` versions per shared library (e.g. `[Radoub.Formats 0.2.60-alpha]`) since the shared libraries version independently of tools

---

## Session Management

**Starting a New Session**:
1. Read this file (Radoub-level guidance)
2. Read tool-specific CLAUDE.md if working on specific tool
3. Check recent git commits for context
4. Check GitHub issues/PRs for active work

**Session Best Practices**:
- Use TodoWrite tool for task tracking
- Commit regularly with clear messages
- Update appropriate CHANGELOG for user-facing changes (see CHANGELOG Management above)
- Mark in-progress work clearly if session ends incomplete

---

## Community Interaction

**Issue Tracking**:
- Use tool labels: `[Parley]`, `[ToolName]`
- Cross-tool issues use `[Radoub]` label
- Apply priority labels consistently
- Link related issues

**Pull Requests**:
- Fill out PR template completely
- Reference related issues (`Closes #X`, `Relates to #Y`)
- Include testing checklist
- Tag tool-specific reviewers if applicable
- **CHANGELOG entries** — highlights only, versioned sections, never `[Unreleased]`

---

## Privacy & Security

**Always**:
- Use `~` for home directory in logs and docs
- Sanitize paths before logging
- No hardcoded user paths in code
- No real usernames in examples
- Keep NonPublic/ out of public repo

---

## Shell Usage on Windows

### Launch every .ps1 from the Bash tool (HARD RULE)

PowerShell-tool permission rules never match on Windows, so that tool prompts on
every call however the allow rule is written. This is an upstream bug
(anthropics/claude-code#57013, #60289, #42318). Adding allow rules does not fix it;
they become dead entries.

Canonical form — never `pwsh`, which launches the Microsoft Store:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View status
```

Controlled test, 2026-07-20, varying only the tool and path:

| Tool | Path | Result |
|------|------|--------|
| Bash | relative | no prompt |
| Bash | absolute | no prompt |
| PowerShell | absolute, with `&` | prompts |
| PowerShell | relative, no `&` | prompts |

The tool decides. Path form and the `&` call operator do not matter.

Reserve the PowerShell tool for work that is not a script call — `Get-Process`,
`Stop-Process`, PS7 loading net9.0 DLLs. Those prompt unavoidably.

Avoid inline `-Command`; escaping breaks it:

```bash
# BAD - requires \$ escaping
powershell.exe -Command "\$data = Get-Content file.json | ConvertFrom-Json; \$data.property"
```

### Which tool for which task

The column names the Claude Code **tool** that dispatches the call, not the language you
write in. Launching a `.ps1` from Bash still runs PowerShell — Bash is only the dispatcher
whose allowlist works.

| Task | Tool |
|------|------|
| Git, GitHub CLI, simple file operations | Bash |
| Any `.ps1` — data processing, JSON parsing | Bash |
| Process control, PS7 loading net9.0 DLLs | PowerShell |

**Write the logic in PowerShell, not POSIX pipelines.** This is a Windows box: `jq` is not
installed, and `sed`/`awk`/`xargs` differ from their Linux behavior or mangle Windows paths.
Reaching for them wastes a cycle on a command that fails or, worse, silently produces the
wrong answer. Put real work in a `.ps1` and launch it from Bash.

| Instead of | Use |
|------------|-----|
| `jq '.field' f.json` | `Get-Content f.json \| ConvertFrom-Json` then `.field` |
| `sed`/`awk` field munging | `Select-String`, `-split`, `-replace`, `ForEach-Object` |
| `xargs` | `ForEach-Object` over the pipeline |
| `wc -l` | `(Get-Content f \| Measure-Object -Line).Lines` |
| `grep -c` on structured data | parse it as an object, then `.Count` |

Bash is fine for what it does well here: `git`, `gh`, `ls`, `cp`, `mkdir`, and single-purpose
`grep` over plain text. Anything involving JSON, objects, or multi-step transformation belongs
in PowerShell.

### Bash on Windows (Git Bash)

**Use forward slashes** - they work:
```bash
cp d:/LOM/workspace/Radoub/source/file.txt d:/LOM/workspace/Radoub/dest/
mkdir -p d:/LOM/workspace/Radoub/new/path/
ls -la d:/LOM/workspace/Radoub/some/path/
```

**AVOID**:
- Backslashes in paths (escaping issues)
- `cmd /c` commands (quoting problems)
- `grep` with regex alternation `\|` (use PowerShell search instead)
- **Git range syntax** (`main...HEAD`, `origin/<branch>..HEAD`) — the dot-dot/triple-dot
  patterns trip the Bash sandbox path-traversal guard and get denied (#2468). Use non-range
  equivalents:
  - `git diff main...HEAD` → `git diff "$(git merge-base main HEAD)" HEAD`
  - `git log origin/<branch>..HEAD` (list unpushed commits) → `git cherry -v "origin/<branch>" HEAD`
  - `git rev-list --count origin/<branch>..HEAD` → `git rev-list --count HEAD --not "origin/<branch>"`

### PowerShell Script Patterns

**Good patterns** (in `.ps1` files):
```powershell
# Normal variables - no escaping needed
$data = Get-Content $CacheFile | ConvertFrom-Json
$age = (Get-Date) - (Get-Item $CacheFile).LastWriteTime

# String interpolation works naturally
Write-Host "Found $($results.Count) items"
```

**Avoid in slash commands**:
- Inline `powershell.exe -Command "..."` blocks with variable escaping
- Multi-line PowerShell embedded in markdown
- Complex logic that should be in a script file

### Slash Command Best Practices

1. **Keep bash simple**: `gh`, `git`, file ops only
2. **Complex logic → script file**: Add to `.claude/scripts/`
3. **Call scripts with `-File`**: Clean, no escaping issues
4. **Parameters over string building**: Use `-Param value` not string concatenation

---

## Code Quality Standards

**Game data (MANDATORY)**: never hardcode races, classes, feats, skills, appearances, or
anything else the game files carry. Load it from 2DA and TLK through `IGameDataService`,
so CEP, PRC, and other custom content that adds or modifies entries keeps working.
Hardcode a fallback only for when the lookup itself fails.

**Paths**: resolve with `Environment.GetFolderPath()` and `SpecialFolder` constants,
validate with `Path.GetFullPath()` against traversal, and pass arguments via
`ProcessStartInfo.ArgumentList` rather than string concatenation.

**Hygiene**: no commented-out code — git remembers. TODOs cite an issue
(`// TODO (#123): description`). Methods stay under 100 lines. Debug code goes before
the commit.

### Exception handling

Catch specific types, never bare `catch`. Log every exception at `LogLevel.WARN` or
above. Never swallow one silently.

**UI handlers that mutate the editor model must roll back when the refresh throws.**
Wrap the `model.Add(...)` → `RefreshUI()` → `MarkDirty()` sequence in try/catch and undo
the model change on failure. Validation at populate time is not enough: ComboBox
selections and tree expansion hold stale state across refreshes, so a bad-state combo
crashes deep in the Avalonia render loop rather than in your handler. Recheck the
validation table at add time too — each layer covers the other's gaps.

The canonical single-add pattern is `MainWindow.ItemProperties.TryAddProperty` (Relique,
#2166): an outer catch for `CreateItemProperty`, an inner catch for
`RefreshAssignedProperties` that removes the entry it just added.

**Batch-add, remove, and clear-all handlers need the same discipline** and are easy to
miss, because the mutation is a loop or a lone `Clear()`/`RemoveAt()` instead of an
`Add()`. Extract mutate-refresh-rollback into a pure helper so it unit-tests without
FlaUI — see `Relique/Services/PropertyListMutator.cs` (#2258).

Reliquary and the other single-resource blueprint editors inherit their handler
structure from Relique, so propagate fixes to this pattern across the siblings.

---

## UI/UX Guidelines

**Dialog and Window Behavior**:
- **NEVER use modal dialogs** (`ShowDialog()`) that block the main application
- **ALWAYS use non-modal windows** (`Show()`) for informational messages
- Users must be able to interact with the main application while notifications/messages are visible
- Exception: Confirmation dialogs for destructive actions (delete, overwrite) may be modal
- Prefer toast notifications or status bar messages over popup windows

**Examples**:
```csharp
// ❌ BAD - Blocks main window
await msgBox.ShowDialog(this);

// ✅ GOOD - Non-blocking
msgBox.Show();

// ✅ ALSO GOOD - Auto-closing notification
ShowToastNotification("Plugin started", 3000);
```

**Detailed UI patterns** (button labeling, progress indicators by duration, deferred loading / lifecycle event responsibilities, fire-and-forget, cancellation tokens, async anti-patterns) live in `Documentation/NEW_TOOL_BOOTSTRAP.md` — read it when building a tool's UI.

---

## Aurora Engine File Naming Constraints

**CRITICAL**: Aurora Engine (Neverwinter Nights) has strict filename limitations:

- **Maximum filename length**: 16 characters (excluding extension)
- **Case**: Lowercase recommended for compatibility
- **Characters**: Alphanumeric and underscore only
- **Examples**:
  - ✅ `test1_link.dlg` (10 chars)
  - ✅ `merchant_01.dlg` (11 chars)
  - ❌ `Test1_SharedReply.dlg` (17 chars - too long)
  - ❌ `my-dialog.dlg` (hyphen not recommended)

**Why this matters**:
- Parley can open files with long names
- Aurora Engine and NWN game cannot load them
- Files appear "missing" in-game despite being valid

**Tools must**:
- Validate filename length before saving
- Warn users when filenames exceed 16 characters
- Suggest shortened alternatives

---

## Aurora File Format Implementation

Implementing an Aurora Engine file parser (GFF, ERF, KEY, BIF, TLK, 2DA, SSF)? The reference strategy (neverwinter.nim primary, BioWare specs secondary) lives in `Documentation/NEW_TOOL_BOOTSTRAP.md`.

---

## GitHub Issue Cache

**Strategy**: Cache-first. All commands read GitHub data from the local cache — never call `gh issue view` or `gh pr view` directly for reads. Mutations (`gh issue create`, `gh pr create`, `gh issue close`) trigger a cache refresh afterward.

**Location**: `.claude/cache/github-data.json`

**Refresh Cache**:
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"        # Only if stale (>1 hour)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force # Force refresh
```

**Read Cache**:
```bash
# Issue details
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number 123

# Search issues
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "keyword"

# List view (no bodies, ~25KB)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View list

# Summary stats (~1KB)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View summary
```

**Cache Contents**:
- Up to 200 open issues with labels, assignees, milestones, comments
- Up to 20 open PRs with status and review state
- Summary stats (stale issues, missing labels)
- Auto-refreshes when >1 hour old

**Verify Live State** (closed issues are not in cache, but their numbers may appear in other issues' bodies):

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Test-IssueState.ps1" -Numbers "1902,1903,1905"
```

Returns JSON array with live `state` (OPEN/CLOSED) and `closedAt`. Use when `/init-item` or `/research` surfaces referenced issue numbers that may have been closed since the cache was last written.

**Commands that use cache**: `/backlog`, `/init-item`, `/pre-merge`, `/research`

**Commands that trigger post-mutation refresh**: `/init-item` (after PR create), `/pre-merge` (after tech debt issue create), `/post-merge` (after issue close/comment)

**Exception**: `/dependabot` uses direct `gh pr list` (infrequent, needs freshest PR state)

---

## Session Log Searching

A script for searching Radoub tool session logs with regex patterns.

**Location**: `.claude/scripts/Search-SessionLogs.ps1`

**Usage**:
```powershell
# List recent sessions
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -ListSessions -MostRecent 5

# Search with regex (supports alternation)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -Pattern "error|warn|exception"

# Search specific tool with context
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -Tool Fence -Pattern "focus|keyboard" -Context 2 -MostRecent 3

# Case-sensitive search
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -Pattern "ERROR" -CaseSensitive
```

**Parameters**:
| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Tool` | Parley | Tool to search (Parley, Manifest, Quartermaster, Fence, Trebuchet) |
| `-Pattern` | (required) | Regex pattern - supports `this\|that\|other` |
| `-MostRecent` | 1 | Number of recent sessions to search |
| `-Context` | 0 | Lines of context before/after matches |
| `-MaxResults` | 50 | Truncate results at this count |
| `-CaseSensitive` | false | Enable case-sensitive matching |
| `-ListSessions` | - | List available sessions without searching |

**Log Location**: `~/Radoub/{Tool}/Logs/Session_YYYYMMDD_HHMMSS/`

---

## Scratch Investigation Scripts

Two committed, reusable PowerShell slots exist so Claude can run one-off diagnostics
**without prompting the user on every new-file creation**:

- `Scripts/Scratch-Investigate-1.ps1`
- `Scripts/Scratch-Investigate-2.ps1`

**How to use them**:
- For a throwaway investigation, **edit one of these in place** (the Edit tool does not
  prompt the way file creation does) rather than creating a new `Foo-Bar.ps1`.
- Run via the absolute-path allowlist (PS7 when loading net9.0 DLLs):
  ```bash
  & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass \
      -File "Scripts/Scratch-Investigate-1.ps1"
  ```
- The body is disposable — the next investigation overwrites it. Commit the rewritten body
  when it produced evidence worth reproducing; otherwise leave it for the next overwrite.

**Hard rule — READ-ONLY / INVESTIGATION ONLY**: these scripts must never do destructive or
mutating work. No writing, deleting, moving, or overwriting of game files, module files,
fixtures, repo files, git state, or GitHub state. No `Set-Content`/`Remove-Item`/`Move-Item`/
`New-Item`, no `git`/`gh` mutations. Output goes to stdout; if a finding is worth keeping,
write it to a `NonPublic/{Tool}/Research/` doc, not from the scratch script. A scratch script
that needs to mutate something is the wrong tool — promote it to a named, reviewed script.

---

## Mutual Workflow Testing (Claude + Human Loop)

For verifying computed/derived values against the real game (e.g. item cost vs the Aurora
toolset, model preview vs in-game), Claude and the human split the loop:

**Claude can:**
1. **Generate fixture files** with known inputs (e.g. `.claude/scripts/New-CostTestUti.ps1`,
   `New-AppearanceTestUtc.ps1` for appearance swaps, `New-RobeCloakTestUtc.ps1` to clone an armor
   UTI with an overridden `Robe`/body part and equip it on a cloned creature) written into a test
   module (`LNS_DLG`). These use the built `Radoub.Formats.dll` via PS7 so the GFF round-trip
   matches the tool. The generated `aN.utc`/`robeNNNt.utc`+`.uti` files are throwaway (not
   committed); the generator scripts are reusable and committed.
2. **Launch the tool directed at a fixture** — `dotnet run --project <Tool>/<Tool>/<Tool>.csproj
   -- --mod <Module> --file <file>` (a GUI launch; run with `run_in_background`).
3. **Read the tool's own logs** to capture computed values — add temporary `[Tag]` diagnostic
   log lines (e.g. `[CostCalc]`) to the code under test, then
   `grep` the newest `~/Radoub/{Tool}/Logs/Session_*/` for that tag.
4. **Stop the app** between fixtures: `Get-Process <Tool> | Stop-Process -Force` (PowerShell tool).

**Human verifies** the same fixture in the authoritative source — open it in the Aurora toolset
to read the ground-truth value, or run UAT for behavior Claude can't observe from logs.

**Loop**: Claude generates fixtures + collects tool values → human reads Aurora/UAT values →
Claude compares, fixes, regenerates. Remove the temporary diagnostic logging before committing.

**Notes**:
- App launch is a real GUI process (not FlaUI) — fine to launch for log capture; still stop it
  when done so it doesn't lock the build output (`Relique.exe` is locked while running).
- This is distinct from FlaUI integration tests (which drive the UI and take over the desktop —
  never run without explicit user request).
- Keep generated fixtures in the test module; they are throwaway, not committed.

---

## Agent Skills (Radoub-tuned, originally from obra/superpowers)

Skills in `.claude/skills/<name>/SKILL.md` are mandatory when their trigger fires — no
user invocation needed, no skipping without an explicit override.

**Where a skill conflicts with this file, CLAUDE.md wins.** Most often the TDD Policy
table above, whose **No** rows (AXAML, config, docs, investigation-first bug fixes) are
narrower than the generic skill's "always".

| Skill | Apply when |
|-------|------------|
| **systematic-debugging** | Any bug, test failure, build error, round-trip failure, or unexplained behavior — especially when tempted to try a quick fix. Work all four phases: root cause → pattern → hypothesis → implementation. |
| **test-driven-development** | Implementing a feature or fixing a reproducible bug. Scope is the TDD Policy table, not the skill's broader claim. |
| **verification-before-completion** | Before any completion claim, commit, PR, or next sprint item. Run the command, show the output, then claim. |

They chain: debug to find root cause → TDD to write the failing test and fix → verify with
evidence.

These are project-owned copies, seeded from https://github.com/obra/superpowers (MIT) and
tuned for Radoub. Edit them freely; do not resync from upstream, which would overwrite the
tuning. When a session also loads the `superpowers:*` plugin skills, the unprefixed local
copy is authoritative.

---

## Resources

- **Wiki**: `d:\LOM\workspace\Radoub.wiki\` (local clone of https://github.com/LordOfMyatar/Radoub/wiki)
  - BioWare Aurora format specs (Markdown conversions)
  - Tool architecture documentation
  - Developer guides
- **BioWare Original PDFs**: `Documentation/BioWare_Original_PDFs/`
- **neverwinter.nim Reference**: https://github.com/niv/neverwinter.nim
- **MDL render/rendering references** (consult these for model-preview/MDL/MTR/mesh-visibility questions):
  - `nwnexplorer` — `d:\LOM\workspace\nwnexplorer` (BSD) — authoritative Aurora-engine mesh-draw gating (`MdlRtNode.cpp`); honors the Render flag, no vertex-count heuristic
  - `rollnw` — `d:\LOM\workspace\rollnw` (MIT) — authoritative binary MDL layout, emitter compile model
  - `borealis_nwn_mdl` — `d:\LOM\workspace\borealis_nwn_mdl` (GPL-3.0) — MDL/material parsing + `docs/RendererIntegration.dox`. **Understanding only — never copy GPL code into this MIT repo**
  - `nwn_mdl_webviewer` — `d:\LOM\workspace\nwn_mdl_webviewer` (MIT) — JS/Three.js viewer; per-node/type visibility toggles, no skip heuristic
  - `mdlops` (ndixUR/mdlops on GitHub) — Perl, skin-mesh reference
- **Project History**: `About/CLAUDE_DEVELOPMENT_TIMELINE.md`
- **AI Collaboration Story**: `About/ON_USING_CLAUDE.md`
- **Tool-Specific Guidance**: `ToolName/CLAUDE.md`
- **GitHub Issue Cache**: `.claude/cache/github-data.json`

---

**For detailed tool-specific guidance, always refer to the tool's CLAUDE.md file.**
