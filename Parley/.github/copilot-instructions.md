<!-- .github/copilot-instructions.md -->
# Quick instructions for AI coding agents working on LNS_DLG

This file contains the essential, actionable knowledge an AI needs to be immediately productive in this repository.

1. Big picture
   - ArcReactor is the main app (dialog editor). Active root: `ArcReactor.Avalonia/` (Avalonia/.NET 9). The old WPF `ArcReactor/` has been removed/cleaned out.
   - Core concerns: parsing/writing Aurora GFF DLG binary format, MVVM UI (ViewModels in `ArcReactor/ViewModels/`), and testing tools in `TestingTools/`.

2. Typical tasks & quick commands
   - Run the app (Avalonia):
     dotnet run --project ArcReactor.Avalonia/ArcReactor.Avalonia.csproj
   - Build entire solution:
     dotnet build LNS_DLG.sln
   - Run specific tests/tools (examples in `CLAUDE.md`):
     dotnet run --project TestingTools/DiagnoseCompatibility/DiagnoseCompatibility.csproj
     powershell -ExecutionPolicy Bypass -File TestingTools/ExportFile.ps1 -InputFile input.dlg -OutputFile output.dlg

3. Project-specific patterns & gotchas
   - Binary format rules are strict: round-trip byte-for-byte validation is required. See `Documentation/DLG_FORMAT_SPECIFICATION.md` and `Documentation/bioware_aurora_engine_file_formats/`.
   - Field indices use a 4:1 bytes ratio and root struct uses special type (see `CLAUDE.md` "Root Struct Type"). Tests and tools perform hex/struct comparisons — prefer those over visual checks.
   - NO interactive console prompts (e.g. Console.ReadKey) — CLI tools must exit cleanly.
   - Keep UI logic in `ArcReactor/Handlers/` and ViewModels; MainWindow acts as a thin coordinator. Don't move business logic back into MainWindow.xaml.cs.

4. Conventions & workflows
   - Branching: feature/*, fix/*, refactor/*; develop is integration; main is protected. Use PRs for all merges. See `.github/PULL_REQUEST_TEMPLATE.md` and `BRANCH_WORKFLOW_GUIDE.md`.
   - Commit messages: explain technical context; update `PROJECT_STATUS.md` and `Documentation/TECHNICAL_LEARNINGS.md` for discoveries.
   - Testing: add checklists in `Testing/` and use `TestingTools/` scripts for round-trip and binary diff validation. PR checklist requires round-trip verification for any DLG changes.

5. Integration points & important files
   - Parser: `ArcReactor/Parsers/ArcLightDialogParser.cs` — modify carefully; changes affect all binary round-trip behavior.
   - Models: `ArcReactor/Models/` (Dialog, DialogNode, DialogPtr) — these map directly to GFF structs.
   - Handlers: `ArcReactor/Handlers/` — preferred place for UI behavior.
   - Testing tools and scripts: `TestingTools/` and `DebugScripts/` (use PowerShell wrappers provided in repo).

6. Examples to cite in suggestions
   - Example DLG: `LNS_DLG/chef.dlg` — use it for parameter/round-trip tests.
   - Export/compare scripts: `TestingTools/ExportFile.ps1`, `TestingTools/CompareStructures.ps1`.

   7. Agent sync & configs (how AI tools share guidance)
      - Short answers:
        - Copilot (GitHub Copilot) uses editor/IDE settings and workspace files for context, but there is no single global repo file that all AIs will automatically sync from. This `.github/copilot-instructions.md` is a recognized convention for repo-level guidance and helps Copilot/Chat-based assistants when present in the workspace.
        - Claude (Anthropic) does not automatically "pull" from `AGENT.md` unless your Claude integration or session prompt explicitly loads that file. `CLAUDE.md` in this repo is a project-specific convention used by Claude sessions here.
      - Recommendation: keep one canonical source (for example `CLAUDE.md` or `AGENT.md`) and copy it to other filenames used by tools. A tiny sync script keeps files consistent (PowerShell example):

   ```powershell
   # Sync canonical CLAUDE.md to copilot instructions and AGENT.md
   Copy-Item -Path .\CLAUDE.md -Destination .\.github\copilot-instructions.md -Force
   Copy-Item -Path .\CLAUDE.md -Destination .\AGENT.md -Force
   ```

      - NOTE (assumption): different AI integrations may read different file names; keep the content small and canonical and sync to other expected filenames. If you want, I can add a CI step or small script in `tools/` to keep these files identical automatically.

   8. When opening PRs or proposing changes

7. When opening PRs or proposing changes
   - Ensure round-trip testing passes (load → modify → save → load → binary compare).
   - Update `PROJECT_STATUS.md` and `TECHNICAL_LEARNINGS.md` for format-related fixes.
   - Include platform notes if changes affect paths or UI behavior (Windows/macOS/Linux).

8. If unsure, read (in order)
   - `PROJECT_STATUS.md`, `CLAUDE.md`, `Documentation/DLG_FORMAT_SPECIFICATION.md`, `ArcReactor/Parsers/ArcLightDialogParser.cs`, `TestingTools/` scripts.

If you want changes or a longer-form agent guide with examples and test snippets, say which areas to expand.
