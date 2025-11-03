# CLAUDE.md - Radoub Toolset

Project guidance for Claude Code sessions working with the Radoub multi-tool repository.

---

## Repository Overview

**Radoub** is a multi-tool repository for Neverwinter Nights modding. Each tool maintains its own codebase, documentation, and development workflow within its subdirectory.

### Current Tools

- **Parley**: Dialog editor (`.dlg` files) - See `Parley/CLAUDE.md` for tool-specific guidance

### Planned Tools

Future tools will be added as subdirectories with their own README, CLAUDE.md, and development infrastructure.

---

## Repository Structure

```
Radoub/
├── README.md (landing page)
├── LICENSE
├── CLAUDE.md (this file - repo-level guidance)
├── .gitignore
├── Documentation/ (Aurora Engine format specs - shared across tools)
│   ├── BioWare_Original_PDFs/
│   └── Bioware_Aurora_*.md
├── About/ (project history and AI collaboration documentation)
│   ├── CLAUDE_DEVELOPMENT_TIMELINE.md
│   └── ON_USING_CLAUDE.md
├── Parley/ (dialog editor)
│   ├── README.md
│   ├── CLAUDE.md (Parley-specific guidance)
│   ├── CHANGELOG.md
│   ├── Parley/ (source code)
│   ├── TestingTools/
│   ├── Documentation/ (Parley-specific docs)
│   └── .github/
└── [Future tools will be added here]
```

---

## Working with Multiple Tools

### Tool-Specific Work

When working on a specific tool (e.g., Parley):
1. **Always read the tool's CLAUDE.md first** (`Parley/CLAUDE.md`)
2. Follow tool-specific conventions and workflows
3. Tool-specific issues/PRs reference the tool in title: `[Parley] Fix parser bug`
4. Run tool-specific tests before committing

### Shared Resources

**Public Documentation** (`Documentation/`):
- Shared across all tools
- Contains BioWare format specifications
- Read-only reference material
- Updates only for format corrections or additions
- Original and Markdown versions of Bioware File Format documentation

**About Documentation** (`About/`):
- Project history and development experience
- AI collaboration documentation
- Updated when major project milestones occur
- Never edit ON_USING_CLAUDE.md; only Lord makes changes to that.

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

---

## Branch Workflow

**Main Branches**:
- `main` - Production-ready releases
- `develop` - Integration branch for all tools

**Feature Branches**:
- Tool-specific: `parley/feature/name`
- Cross-tool: `radoub/feature/name`
- Documentation: `docs/name`

**Important**: All work via Pull Requests, even for "small" changes.

---

## Adding New Tools

When adding a new tool to Radoub:

1. **Create tool directory** with standard structure:
   ```
   ToolName/
   ├── README.md (tool-specific)
   ├── CLAUDE.md (tool-specific guidance)
   ├── CHANGELOG.md (tool changelog)
   ├── ToolName/ (source code)
   ├── TestingTools/ (if applicable)
   ├── Documentation/ (tool-specific docs)
   └── .github/ (tool-specific workflows)
   ```

2. **Update Radoub README.md** to list new tool

3. **Create tool-specific CLAUDE.md** with:
   - Tool overview and architecture
   - Development workflow
   - Testing requirements
   - Tool-specific conventions

4. **Set up CI/CD** in `.github/workflows/` with tool prefix

5. **Initial commit**: `[Radoub] feat: Add ToolName - [brief description]`

---

## Testing Requirements

**Before Committing**:
- Run tool-specific tests (see tool CLAUDE.md)
- Verify affected tool(s) build successfully
- Check for hardcoded paths (privacy)
- Verify cross-platform compatibility if applicable

**Before PRs to Main**:
- All tools must build
- All tool tests must pass
- Private documentation to the Private folder
- Public Documentation approved before push
- CHANGELOG updated for affected tools

---

## Documentation Standards

Follow the same standards as Parley (see `Parley/CLAUDE.md`):
- ATX headers (no decorative bold/italic)
- Google Docs compatible formatting (minimal bolding)
- Table of Contents for docs over 2 pages
- Privacy-safe examples (no real usernames/paths)

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
- Update tool CHANGELOG for user-facing changes
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
- Reference related issues
- Include testing checklist
- Tag tool-specific reviewers if applicable

---

## Privacy & Security

**Always**:
- Use `~` for home directory in logs and docs
- Sanitize paths before logging
- No hardcoded user paths in code
- No real usernames in examples
- Keep NonPublic/ out of public repo

---

## Resources

- **BioWare Aurora Specs**: `Documentation/`
- **Project History**: `About/CLAUDE_DEVELOPMENT_TIMELINE.md`
- **AI Collaboration Story**: `About/ON_USING_CLAUDE.md`
- **Tool-Specific Guidance**: `ToolName/CLAUDE.md`

---

**For detailed tool-specific guidance, always refer to the tool's CLAUDE.md file.**
