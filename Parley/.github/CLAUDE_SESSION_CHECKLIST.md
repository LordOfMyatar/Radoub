# Claude Session Checklist

**Purpose**: Automated checklist to ensure session continuity. Claude reads this FIRST, every session.

## Session Start (Claude: Read These Files)

- [ ] `Documentation/CODE_PATH_MAP.md` - Active code paths (avoid dead code)
- [ ] `git log --oneline -10` - Recent commits
- [ ] GitHub issues - Check open issues for current priorities
- [ ] This checklist - Any session-specific notes below

## Session End (Claude: Update Before Ending)

- [ ] If major breakthrough: Create/update subject-specific documentation in Documentation/
- [ ] If parser architecture changed: Update PARSER_ARCHITECTURE.md
- [ ] If code paths changed: Update CODE_PATH_MAP.md
- [ ] If phase milestone reached: Ask user about testing checklist
- [ ] Commit with detailed technical context

## Active Session Notes

### Current Focus (User: Update This Section)
**Phase**: Phase 3 - Script & Parameter Support
**Priority**: Medium
**Blocking Issues**: None
**Last Testing Date**: N/A

### Commits Requiring Review
<!-- Lord: Flag specific commits if needed -->
<!-- Example: "Review commit 0de0727 - struct type changes" -->
None currently

### Dead-End Commits (Ignore These)
<!-- Auto-populated by Claude when reverting -->
<!-- Example: "Commits a05f9a9-7de8d60 reverted, embedded list approach abandoned" -->
- Commits a05f9a9-7de8d60: Reverted, embedded list approach wrong

### Debugging Time-Box
<!-- Lord: Set limits if Claude is thrashing -->
<!-- Example: "Embedded list bug - 3 day limit, then document as known issue" -->
None active

## "3 Strikes Rule" Tracker

**Rule**: After 3 commits on same issue without progress, require external validation (NWN testing, hex comparison, user input)

**Current Strike Count**: 0
**Issue**: N/A
**Commits**: N/A

**Action When 3 Strikes Hit**:
1. Claude: Stop coding, document findings so far
2. Claude: Request NWN engine testing OR ask architectural question
3. Lord: Test and provide console output OR make architectural decision
4. Reset counter after external validation

## Automated Reminders

### For Claude
- After 2 reverts on same issue → Build comparison tool, request validation
- After 5 commits in single day → Create subject-specific documentation for the issue
- Before binary format changes → Ask: "Should we test in NWN before proceeding?"
- At 80% confidence on architecture → Ask question, don't guess at 95%

### For User (Optional - You Choose When to Act)
- When Claude hits 3 strikes → Provide NWN testing or architectural decision
- After Claude's 3rd day on single bug → Decide: continue or document as known issue
- When testing checklist complete → Review and close related GitHub issues

---

**Last Updated**: 2025-11-01
**Next Review**: Every session start
