# Research Task

Investigate a GitHub issue or feature request and produce a research summary. Optionally spike with throwaway code on a disposable branch.

## Upfront Questions

**IMPORTANT**: Gather ALL required user input at the start, then execute autonomously.

Before starting research, collect these answers in ONE interaction:

1. **Scope Clarification** (if topic is broad): "What specific aspects should I focus on? [implementation/libraries/architecture/all]"
2. **Depth**: "Research depth? [quick-scan/thorough/exhaustive]"
3. **Output Location**: "Save research notes to NonPublic/Research/?" [y/n]"

After collecting answers, proceed through all exploration and analysis without further prompts unless critical ambiguity is discovered.

## Usage

```
/research #[issue-number]
/research [topic description]
/research --spike #[issue-number]     # Create throwaway branch for prototyping
/research --spike [topic] --timebox 2h
```

**Flags**:
- `--spike` - Create a throwaway branch for hands-on prototyping (branch is deleted after findings captured)
- `--timebox [duration]` - Set spike timebox (default: 2h, options: 1h/2h/4h)

## Workflow

### Step 0: Ensure Cache is Fresh

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
```

### Step 0b: Create Spike Branch (if `--spike` flag)

If `--spike` is passed, create a throwaway branch before starting:

```bash
git stash  # if needed
git checkout main
git checkout -b spike/[short-topic]
```

Display spike context:
```markdown
## Spike Started

**Topic**: [topic]
**Question(s)**: [what we're trying to answer]
**Timebox**: [duration, default 2h]
**Started**: [timestamp]
```

### Step 1: Understand the Request

**Cache-first**: Always read from cache. No direct `gh issue view` calls.

```bash
# Get issue details from cache
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number [number]

# Search for related issues
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "[keyword]"
```

Parse the issue to identify:
- **Goal**: What problem are we solving?
- **Scope**: What areas of the codebase are affected?
- **Constraints**: Any technical or design requirements mentioned?

If topic description provided, clarify with user if needed.

### Step 2: Codebase Exploration

Use native tools (not bash grep) for codebase exploration:
- **Glob** for finding files by pattern
- **Grep** for searching content
- **Task tool with Explore agent** for open-ended exploration

Identify:
- Existing patterns that could be reused
- Integration points
- Potential conflicts or dependencies

### Step 3: External Research

If needed, research:
- Library options (NuGet packages, Python packages)
- API documentation
- Best practices for the approach

**When evaluating external libraries**, check maintainability factors:
- **Last activity**: When was the last commit/release? (Stale > 2 years is a red flag)
- **Issue responsiveness**: Are maintainers active? Open issue count vs. closed?
- **Compatibility**: .NET version support, Avalonia/platform compatibility
- **License**: MIT/Apache preferred, check for restrictions
- **Adoption**: Download counts, stars, community usage
- **Alternatives**: Are there better-maintained alternatives?

Document sources and key findings.

### Step 4: Ask Clarifying Questions

Before proceeding, ask about:
- Ambiguous requirements
- Trade-offs between approaches
- User preferences on implementation style

Wait for user response before continuing.

### Step 5: Generate Research Notes

Create notes in `NonPublic/[Tool]/Research/` (per CLAUDE.md rules — NonPublic is always at repo root):

```markdown
# Research: [Issue/Topic Title]

**Date**: [YYYY-MM-DD]
**Issue**: #[number] (if applicable)
**Status**: Research Complete / Needs Clarification

## Summary

[2-3 sentence overview of findings]

## Key Findings

### Approach A: [Name]
- **Pros**: [list]
- **Cons**: [list]
- **Effort**: Low/Medium/High
- **Risk**: Low/Medium/High

### Approach B: [Name]
[Same structure]

## Recommendation

[Which approach and why]

## Open Questions

- [Questions for user or future investigation]

## Resources

- [Links to docs, examples, related issues]

## Code References

- `[file:line]` - [description of relevant code]
```

### Step 6: Report to User

Summarize findings concisely and present options.

## Output Location

Research notes go in `NonPublic/[Tool]/Research/`:
- `NonPublic/Parley/Research/` for Parley-related topics
- `NonPublic/Quartermaster/Research/` for QM research
- `NonPublic/Radoub/Research/` for cross-tool or format research

For spikes, use: `NonPublic/[Tool]/Research/spike-[topic].md`

## Spike Cleanup (if `--spike` flag was used)

After capturing findings:

```bash
# Switch back to previous branch
git checkout -  # or git checkout main

# Delete the spike branch (local and remote if pushed)
git branch -D spike/[short-topic]
git push origin --delete spike/[short-topic] 2>/dev/null  # ignore if not pushed
```

**Important rules for spikes:**
- **Never merge a spike branch** — spikes are throwaway
- **Always capture findings** before deleting the branch
- **Respect the timebox** — if time runs out, document what you have and stop
- **One question at a time** — log new questions as open questions for future research

## Notes

- Research is read-only — no code changes (unless `--spike` flag, which allows throwaway prototyping)
- Ask before making assumptions
- Document sources for future reference
- Keep notes even if approach is rejected (prevents re-research)
