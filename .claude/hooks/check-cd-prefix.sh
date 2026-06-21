#!/usr/bin/env bash
# PreToolUse hook (matcher: Bash).
#
# Purpose: stop the "cd <abs-path> && <realcmd>" prefixing anti-pattern.
# The Bash tool's working directory is ALREADY d:\LOM\workspace\Radoub, so a
# leading `cd "d:\LOM\workspace\Radoub" && grep ...` is redundant. Worse, it
# defeats the permission allowlist: an entry like Bash(grep:*) or Bash(git log:*)
# matches on the command PREFIX, but `cd ... && grep ...` starts with `cd`, so
# the allowlisted read-only command no longer matches and the user gets a prompt.
# Path-form drift makes it worse (cd /d, cd /mnt/d, cd /d\, mixed d:/ vs D:\).
#
# Correct pattern instead:
#   - Just run the command. The cwd is the repo root already.
#   - Need a subdirectory? Pass the path to the tool (grep -r ./Relique,
#     git -C <path>, find ./Radoub.UI ...) instead of cd-ing first.
#
# This reinforces the user's standing preference (memory: feedback_no_cd_in_bash.md)
# which was being ignored across recent sessions.
#
# OVERRIDE (rare, intentional): set RADOUB_CD_PREFIX_HOOK=off or create
# .claude/hooks/.cd-prefix-hook-disabled

set -euo pipefail

INPUT="$(cat)"

DISABLE_FILE="$(dirname "$0")/.cd-prefix-hook-disabled"
if [ "${RADOUB_CD_PREFIX_HOOK:-on}" = "off" ] || [ -f "$DISABLE_FILE" ]; then
  echo "[check-cd-prefix] DISABLED (grace mode)." >&2
  exit 0
fi

tool="$(printf '%s' "$INPUT" | grep -oE '"tool_name"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed -E 's/.*"([^"]*)"$/\1/')"
[ "$tool" = "Bash" ] || exit 0

# Pull the command string out of the JSON payload (single-line blob is fine).
blob="$(printf '%s' "$INPUT" | tr '\n' ' ')"

# Detect a command that STARTS with `cd ` (the `"command":"cd ` prefix) AND
# chains into another command with && or ;. We test the two conditions
# separately rather than as one contiguous regex, because the command value can
# contain escaped quotes (cd "d:\path\") that defeat a single [^"]* span.
# A bare `cd somewhere` with no chaining is harmless and allowed.
starts_with_cd=0
has_chain=0
printf '%s' "$blob" | grep -qiE '"command"[[:space:]]*:[[:space:]]*"[[:space:]]*cd[[:space:]/]' && starts_with_cd=1
printf '%s' "$blob" | grep -qE '(&&|;)' && has_chain=1
if [ "$starts_with_cd" -eq 1 ] && [ "$has_chain" -eq 1 ]; then
  cat >&2 <<'EOF'
[check-cd-prefix] BLOCKED: command starts with `cd <path> && ...`.

The Bash tool's working directory is ALREADY d:\LOM\workspace\Radoub. Prefixing
`cd` is redundant AND breaks the permission allowlist: `cd ... && grep ...`
matches the `cd` rule, not your allowlisted `grep:*` / `git log:*` rules, so it
forces a permission prompt every time.

Do this instead:
  - Drop the `cd` entirely and run the command directly.
  - To work in a subdirectory, pass the path to the tool:
      grep -r "pattern" ./Relique
      git -C d:/LOM/workspace/Radoub log --oneline
      find ./Radoub.UI -name "*.axaml"

Override (rare): set RADOUB_CD_PREFIX_HOOK=off or create
.claude/hooks/.cd-prefix-hook-disabled
EOF
  exit 2
fi

exit 0
