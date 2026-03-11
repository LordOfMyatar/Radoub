#!/bin/bash
# Hook: PreToolUse (Bash)
# Purpose: Before creating a GH issue, search for existing duplicates
# and prompt Claude to check/supplement instead of creating new
# Note: No jq available on Windows — uses grep/sed for JSON parsing

INPUT=$(cat)

# Extract command from hook JSON input using grep (no jq)
COMMAND=$(echo "$INPUT" | grep -oP '"command"\s*:\s*"[^"]*"' | head -1 | sed 's/"command"\s*:\s*"//;s/"$//')

# Only check gh issue create commands
if ! echo "$COMMAND" | grep -q 'gh issue create'; then
  exit 0
fi

# Extract the title from --title flag
TITLE=$(echo "$COMMAND" | grep -oP '(?<=--title ")[^"]*' | head -1)
if [ -z "$TITLE" ]; then
  TITLE=$(echo "$COMMAND" | grep -oP "(?<=--title ')[^']*" | head -1)
fi
# Try escaped quotes (JSON-encoded command)
if [ -z "$TITLE" ]; then
  TITLE=$(echo "$COMMAND" | grep -oP '(?<=--title \\")[^\\]*' | head -1)
fi

if [ -z "$TITLE" ]; then
  # Can't extract title — allow but warn
  echo "WARN: Could not extract issue title to check for duplicates. Proceeding." >&2
  exit 0
fi

# Strip common prefixes to get search keywords
KEYWORDS=$(echo "$TITLE" | sed -E 's/^\[(Parley|Manifest|Quartermaster|Fence|Trebuchet|Radoub)\]\s*//i' | sed -E 's/^(feat|fix|docs|refactor|test|chore|Sprint|Tech Debt|Epic)[: ]+//i')

# Search via GH API for open+closed issues with similar terms
# Use gh's built-in --jq flag (works without system jq)
API_SEARCH_TERMS=$(echo "$KEYWORDS" | tr ' ' '+' | head -c 100)
FORMATTED=$(gh search issues "$API_SEARCH_TERMS" --repo LordOfMyatar/Radoub --limit 5 --json number,title,state --jq '.[] | "  #\(.number) [\(.state)] \(.title)"' 2>/dev/null)

if [ -z "$FORMATTED" ]; then
  # No matches found — safe to create
  exit 0
fi

RESULT_COUNT=$(echo "$FORMATTED" | wc -l)

cat >&2 <<EOF
DUPLICATE CHECK: Found $RESULT_COUNT potentially related issues for "$TITLE":

$FORMATTED

Before creating a new issue:
1. Check if any of these cover the same topic
2. If a match exists, consider adding a comment or supplemental info instead
3. If this is genuinely new, proceed with creation

To proceed anyway, re-run the command unchanged.
EOF

exit 2
