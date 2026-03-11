#!/bin/bash
# Hook: PreToolUse (Bash)
# Purpose: Before creating a GH issue, search for existing duplicates
# and prompt Claude to check/supplement instead of creating new

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

# Only check gh issue create commands
if ! echo "$COMMAND" | grep -q 'gh issue create'; then
  exit 0
fi

# Extract the title from --title flag
TITLE=$(echo "$COMMAND" | grep -oP '(?<=--title\s")[^"]*' | head -1)
if [ -z "$TITLE" ]; then
  TITLE=$(echo "$COMMAND" | grep -oP "(?<=--title\s')[^']*" | head -1)
fi

if [ -z "$TITLE" ]; then
  # Can't extract title — allow but warn
  echo "WARN: Could not extract issue title to check for duplicates. Proceeding." >&2
  exit 0
fi

# Strip common prefixes to get search keywords
KEYWORDS=$(echo "$TITLE" | sed -E 's/^\[(Parley|Manifest|Quartermaster|Fence|Trebuchet|Radoub)\]\s*//i' | sed -E 's/^(feat|fix|docs|refactor|test|chore|Sprint|Tech Debt|Epic)[: ]+//i')

# Search cache first (fast, no API call)
CACHE_FILE=".claude/cache/github-data.json"
CACHE_HITS=""
if [ -f "$CACHE_FILE" ]; then
  # Extract 2-3 significant words for cache search
  SEARCH_WORDS=$(echo "$KEYWORDS" | tr ' ' '\n' | grep -vEi '^(the|a|an|in|on|for|to|of|and|with|from|by)$' | head -3 | tr '\n' '|' | sed 's/|$//')
  if [ -n "$SEARCH_WORDS" ]; then
    CACHE_HITS=$(grep -iE "$SEARCH_WORDS" "$CACHE_FILE" 2>/dev/null | grep -oP '"title"\s*:\s*"[^"]*"' | head -5)
  fi
fi

# Also search via GH API for open+closed issues with similar terms
API_SEARCH_TERMS=$(echo "$KEYWORDS" | tr ' ' '+' | head -c 100)
API_RESULTS=$(gh search issues "$API_SEARCH_TERMS" --repo LordOfMyatar/Radoub --limit 5 --json number,title,state 2>/dev/null)

if [ -z "$API_RESULTS" ] || [ "$API_RESULTS" = "[]" ]; then
  # No matches found — safe to create
  exit 0
fi

# Format results for Claude
RESULT_COUNT=$(echo "$API_RESULTS" | jq 'length')
if [ "$RESULT_COUNT" -eq 0 ]; then
  exit 0
fi

FORMATTED=$(echo "$API_RESULTS" | jq -r '.[] | "  #\(.number) [\(.state)] \(.title)"')

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
