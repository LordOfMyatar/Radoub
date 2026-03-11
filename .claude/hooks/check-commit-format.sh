#!/bin/bash
# Hook: PreToolUse (Bash)
# Purpose: Enforce commit message format: [Tool] type: description
# Only fires on git commit commands
# Note: Uses grep/cut instead of jq (not available in Windows Git Bash)

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only check git commit commands
if ! echo "$COMMAND" | grep -q 'git commit'; then
  exit 0
fi

# Extract the commit message from -m flag
# Handle both: git commit -m "msg" and git commit -m "$(cat <<'EOF' ... EOF )"
COMMIT_MSG=$(echo "$COMMAND" | grep -oP '(?<=-m\s")[^"]*' | head -1)

# If no simple message found, try heredoc pattern
if [ -z "$COMMIT_MSG" ]; then
  COMMIT_MSG=$(echo "$COMMAND" | grep -oP "(?<=EOF\n).*" | head -1)
fi

# If we still can't extract the message, allow it (don't block on parse failure)
if [ -z "$COMMIT_MSG" ]; then
  exit 0
fi

# Check for [Tool] prefix pattern: [Word] type: description
if ! echo "$COMMIT_MSG" | grep -qP '^\[[\w]+\]\s+\w+:'; then
  echo "Commit message must start with [Tool] type: format (e.g., [Parley] fix: description)" >&2
  exit 2
fi

exit 0
