#!/bin/bash
# Hook: PostToolUse (Bash)
# Purpose: When test/build failures are detected, remind Claude to follow
# the systematic-debugging skill methodology instead of guessing at fixes.
#
# Triggers on: dotnet test, dotnet build output containing failures/errors
# Action: Warning message (exit 0) — does not block, but injects reminder

INPUT=$(cat)

# Only check Bash tool results
TOOL=$(echo "$INPUT" | grep -o '"tool_name":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ "$TOOL" != "Bash" ]; then
  exit 0
fi

# Get the command that was run
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only trigger on build/test commands
if ! echo "$COMMAND" | grep -qiE '(dotnet (test|build)|run-tests)'; then
  exit 0
fi

# Check for build failures
if echo "$INPUT" | grep -qiE '(Build FAILED|error (CS|MSB)[0-9]+|: error :)'; then
  cat >&2 <<'WARN'
🔧 BUILD FAILURE — Use systematic-debugging skill (.claude/skills/systematic-debugging/)
Phase 1: Read error messages carefully. Note CS/MSB error codes.
Phase 2: Find working examples of the same pattern in the codebase.
Phase 3: Form a single hypothesis, test with minimal change.
Do NOT propose multiple fixes at once.
WARN
fi

# Check for test failures (complementary to check-flaky-test-claims.sh)
if echo "$INPUT" | grep -qiE '(Failed:\s*[1-9]|FAIL\b|Test Run Failed)'; then
  cat >&2 <<'WARN'
🔧 TEST FAILURE — Use systematic-debugging skill (.claude/skills/systematic-debugging/)
Phase 1: Read the failure output completely. Reproduce consistently.
Phase 2: Compare against working tests.
Phase 3: Single hypothesis, minimal change.
Then use test-driven-development skill for the fix.
WARN
fi

exit 0
