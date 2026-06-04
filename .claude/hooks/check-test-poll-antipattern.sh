#!/usr/bin/env bash
# PreToolUse hook (matcher: Monitor and Bash).
#
# Purpose: stop the "Yes-spam" anti-pattern where Claude polls for test/build
# output via Monitor or ad-hoc `until ... grep ... dotnet test` shell chains.
# These commands carry unique temp paths every run, so the permission system
# can't prefix-match them and the user must click Yes repeatedly.
#
# Correct pattern instead:
#   - Run `dotnet test` (or Run-Tests.ps1) with run_in_background=true and let the
#     completion notification arrive. Then Read the output file with the Read tool.
#   - Never wrap test runs in Monitor / until-loops / grep pipes.
#
# GRACE + ALERTING:
#   This hook is a WORKAROUND for a harness limitation. If Anthropic fixes
#   background-task ergonomics (so polling is no longer needed), disable this
#   hook without code edits by either:
#     1. Setting env  RADOUB_TEST_POLL_HOOK=off
#     2. Creating file .claude/hooks/.test-poll-hook-disabled
#   When disabled, the hook still PRINTS a one-line reminder (alerting) so the
#   team notices the workaround is dormant and can delete it during cleanup.
#
# Review date: re-evaluate after 2026-12-01 — if upstream fixed, remove entirely.

set -euo pipefail

INPUT="$(cat)"

# Extract the tool name and the command/text being run from the hook payload.
tool="$(printf '%s' "$INPUT" | grep -oE '"tool_name"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed -E 's/.*"([^"]*)"$/\1/')"
# Pull a broad command blob (covers Bash.command and Monitor.command).
blob="$(printf '%s' "$INPUT" | tr '\n' ' ')"

DISABLE_FILE="$(dirname "$0")/.test-poll-hook-disabled"

# --- Grace / disable path -------------------------------------------------
if [ "${RADOUB_TEST_POLL_HOOK:-on}" = "off" ] || [ -f "$DISABLE_FILE" ]; then
  echo "[check-test-poll-antipattern] DISABLED (grace mode). If background-task polling is fixed upstream, delete this hook. See header." >&2
  exit 0
fi

# --- Detection ------------------------------------------------------------
# Flag Monitor used to watch test/build output, or Bash until/grep polling loops
# that reference dotnet test or a *.output/*.log temp file.
is_violation=0
reason=""

if [ "$tool" = "Monitor" ]; then
  if printf '%s' "$blob" | grep -qiE 'dotnet test|\.output|\.log|until .*grep|Passed!|Failed!'; then
    is_violation=1
    reason="Monitor is being used to poll for test/build output."
  fi
elif [ "$tool" = "Bash" ]; then
  # until/while loop that greps for a test result marker = polling anti-pattern.
  if printf '%s' "$blob" | grep -qiE '(until|while)[^|]*(grep|Select-String)' \
     && printf '%s' "$blob" | grep -qiE 'dotnet test|Passed!|Failed!|\.output'; then
    is_violation=1
    reason="A shell poll-loop (until/while + grep) is waiting on test output."
  fi
fi

if [ "$is_violation" -eq 1 ]; then
  cat >&2 <<EOF
[check-test-poll-antipattern] BLOCKED: $reason

This pattern creates a unique command every run, so it cannot be auto-authorized
and forces repeated permission prompts.

Do this instead:
  1. Run the test in the background and let the completion notification fire:
       dotnet test <project> --nologo            (run_in_background=true)
     or:
       pwsh .claude/scripts/Run-Tests.ps1 -Project <proj> -Filter <expr>
  2. When notified it finished, READ the output file with the Read tool.
     Do NOT poll with Monitor / until-loops / grep pipes.

Override (rare, intentional): set RADOUB_TEST_POLL_HOOK=off or create
.claude/hooks/.test-poll-hook-disabled
EOF
  exit 2
fi

exit 0
