# Dependabot PR Consolidation

Evaluate and consolidate open dependabot PRs into a single sprint branch.

## Upfront Questions

**IMPORTANT**: Gather ALL required user input at the start, then execute autonomously.

Before running, collect these answers in ONE interaction (after fetching PR details):

1. **PR Selection**: "These are the open dependabot PRs. Which ones to include?" [all/specific numbers]
2. **Sprint Anchor**: "Which PR number to use as sprint anchor?" [default: lowest number]

After collecting answers, proceed through all steps without further prompts.

## Usage

```
/dependabot
/dependabot #1393 #1395-#1399
```

If no PR numbers provided, discovers all open dependabot PRs.

## Workflow

### Phase 0: Discovery

```bash
# Find all open dependabot PRs
gh pr list --author "app/dependabot" --state open \
  --json number,title,headRefName,labels \
  -q '.[] | "\(.number)\t\(.title)\t[labels: \([.labels[].name] | join(", "))]"'
```

Present findings as a table:

```markdown
| PR | Package | Version Change | Tool |
|----|---------|---------------|------|
```

**Group overlapping PRs**: If multiple PRs bump the same framework (e.g., Avalonia), note they should be applied together.

### Phase 1: Analysis

For each PR, assess:

**Risk Level**:

| Change | Risk | Notes |
|--------|------|-------|
| Patch bump (x.y.Z) | Low | Bug fixes only |
| Minor bump (x.Y.0) | Medium | New features, usually backward compatible |
| Major bump (X.0.0) | High | Breaking changes possible |

**Breaking Change Check**:
- Read PR body for release notes/changelog excerpts
- For major bumps, check if the package requires a newer .NET version
- For Avalonia bumps, check if related packages (Skia, DataGrid, etc.) need matching versions

**Central Package Management**:
```bash
# Check if versions are centrally managed
cat Directory.Packages.props | grep -i "[package-name]"
```

If using `Directory.Packages.props` (Radoub does), all version changes go in ONE file.

### Phase 2: Apply Updates

**Verify clean state**:
```bash
git status
git checkout main
git pull origin main
```

**Create sprint branch**:
```bash
git checkout -b radoub/issue-[anchor-number]
```

**Update versions in `Directory.Packages.props`**:
- Apply all version bumps
- Ensure related packages stay in sync (e.g., all Avalonia packages should be same version)

**Update tool CHANGELOGs** (if dependency changes affect specific tools):
- Add one-liner highlight entry to affected tool's CHANGELOG
- Include PR numbers for traceability

**Initial commit**:
```bash
git add Directory.Packages.props
git commit -m "[Radoub] chore: Initialize dependabot sprint branch for #[anchor]"
```

### Phase 3: Build & Test

**Restore packages**:
```bash
dotnet restore Radoub.sln
```

If restore fails, check for:
- Package version incompatibilities
- Minimum .NET version requirements (we target net9.0)
- Transitive dependency conflicts

**Build all projects**:
```bash
dotnet build Radoub.sln --no-restore
```

Record: warnings count, errors count, any new warnings introduced.

**Run unit tests**:
```bash
dotnet test Radoub.sln --no-build --filter "FullyQualifiedName!~IntegrationTests"
```

Record: passed, failed, skipped counts.

**Evaluate results**:

| Result | Action |
|--------|--------|
| Build + tests pass | Continue to Phase 4 |
| Build fails | Investigate, fix if possible, report if not |
| New test failures | Compare with main branch baseline to confirm they're new |
| Only pre-existing failures | Note in PR, continue |

### Phase 4: Commit, PR & Report

**Commit the version changes**:
```bash
git add Directory.Packages.props
git commit -m "[Radoub] feat: Bump [summary of changes] (#[anchor])"
git push -u origin [branch-name]
```

**Create draft PR**:
```bash
gh pr create --draft \
  --title "[Radoub] Sprint: Dependabot Dependency Updates (#[anchor])" \
  --body "$(cat <<'PREOF'
## Summary

[List each package update: name, old version → new version]

Consolidates dependabot PRs: #[list all PR numbers]

## Test Results

- Build: [X warnings, Y errors]
- Unit tests: [passed/failed/skipped]
- Integration tests: [note pre-existing failures if any]

## Risk Assessment

[Table of packages with risk levels]

## Checklist

- [x] Build passes
- [x] Unit tests pass
- [x] CHANGELOG updated
- [ ] Close superseded dependabot PRs after merge

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
PREOF
)"
```

**Update CHANGELOG with PR number**.

**Report summary**:

```markdown
## Dependabot Sprint Summary

**Branch**: [branch-name]
**PR**: #[number]
**Anchor Issue**: #[anchor]

### Updates Applied

| Package | From | To | Risk |
|---------|------|----|------|
| ... | ... | ... | Low/Med/High |

### Test Results

- Build: ✅/❌
- Unit Tests: ✅ [count] passed
- New Warnings: [count or "none"]

### Post-Merge Cleanup

After merging, close these superseded dependabot PRs:
- #[list each PR to close]

Use: `gh pr close [number] --comment "Superseded by #[sprint-pr]"`
```

## Post-Merge: Closing Superseded PRs

After the sprint PR merges, close each original dependabot PR:

```bash
# Close each superseded PR with explanation
gh pr close [number] --comment "Superseded by #[sprint-pr]. Updates applied in consolidated sprint branch."
```

**Do NOT merge the original dependabot PRs** - they modify individual packages and may conflict.

## Edge Cases

**Package conflicts**: If two dependabot PRs bump packages that are incompatible:
1. Note the conflict in the PR description
2. Apply the higher version if compatible
3. If truly incompatible, exclude the conflicting PR and explain why

**Framework version requirements**: If a major bump requires a newer .NET version:
1. Check current target framework in `.csproj` files
2. If upgrade is needed, flag for user decision (framework upgrades are bigger scope)

**Grouped PRs**: Dependabot sometimes creates "multi" PRs (e.g., `dependabot/nuget/multi-7a2beb92b6`). These already bundle related packages - apply them as a unit.

## Notes

- Always use `Directory.Packages.props` for version changes (central package management)
- Keep related framework packages in sync (all Avalonia packages same version)
- Pre-existing integration test failures (FlaUI) don't block dependency updates
- Major version bumps deserve extra scrutiny - read the release notes
- This skill replaces manual review of individual dependabot PRs
