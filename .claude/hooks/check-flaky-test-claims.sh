#!/bin/bash
# Hook: PostToolUse (Bash)
# Purpose: When test failures are detected, remind Claude to investigate
# before dismissing as "flaky" or "pre-existing".
#
# Triggers on: dotnet test, run-tests.ps1 output containing failures
# Action: Warning message (exit 0) — does not block, but injects reminder

INPUT=$(cat)

# Only check Bash tool results
TOOL=$(echo "$INPUT" | grep -o '"tool_name":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ "$TOOL" != "Bash" ]; then
  exit 0
fi

# Get the command that was run
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only trigger on test commands
if ! echo "$COMMAND" | grep -qiE '(dotnet test|run-tests)'; then
  exit 0
fi

# Get the full output (stdout may be in tool_output)
OUTPUT=$(echo "$INPUT" | grep -oP '"stdout"\s*:\s*"[^"]*"' | head -1)

# Also check the raw input for failure indicators (output format varies)
if echo "$INPUT" | grep -qiE '(Failed:\s*[1-9]|FAIL\b|Test Run Failed)'; then
  cat >&2 <<'WARN'
⚠️ TEST FAILURES DETECTED

Before reporting these failures, you MUST:
1. Check if failing tests touch files changed in this PR
2. Rerun each failing test IN ISOLATION to confirm if intermittent
3. Only if the isolated rerun PASSES can you label it "intermittent"
4. If the isolated rerun FAILS AGAIN → it's a real failure, investigate root cause

Do NOT dismiss failures as "flaky", "pre-existing", or "unrelated" without completing steps 1-3.
WARN
fi

exit 0
