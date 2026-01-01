# Research Task

Investigate a GitHub issue or feature request and produce a research summary.

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
```

## Workflow

### Step 1: Understand the Request

If issue number provided:
```bash
gh issue view [number] --json title,body,labels,comments
```

Parse the issue to identify:
- **Goal**: What problem are we solving?
- **Scope**: What areas of the codebase are affected?
- **Constraints**: Any technical or design requirements mentioned?

If topic description provided, clarify with user if needed.

### Step 2: Codebase Exploration

Search for related code:
```bash
# Find related files
git grep -l "[keyword]" --include="*.cs"

# Check existing implementations
grep -r "class.*[FeatureName]" --include="*.cs"
```

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

Create notes in `Parley/NonPublic/Research/` (or tool-appropriate folder):

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

Research notes go in:
- `Parley/NonPublic/Research/` for Parley-related topics
- `[Tool]/NonPublic/Research/` for other tool research
- `Documentation/Research/` for cross-tool or format research

## Notes

- Research is read-only - no code changes
- Ask before making assumptions
- Document sources for future reference
- Keep notes even if approach is rejected (prevents re-research)
