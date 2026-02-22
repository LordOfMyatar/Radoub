#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Catch hardcoded game data in C# code
# CLAUDE.md rule: "NEVER hardcode game data (races, classes, feats, skills, appearances, etc.)"
# Must use 2DA files and TLK strings via IGameDataService

INPUT=$(cat)
TOOL=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only check Edit and Write tools
if [ "$TOOL" != "Edit" ] && [ "$TOOL" != "Write" ]; then
  exit 0
fi

# Get the file path
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Only check C# files, skip test files
if ! echo "$FILE_PATH" | grep -qP '\.cs$'; then
  exit 0
fi
if echo "$FILE_PATH" | grep -qiP '(test|spec)'; then
  exit 0
fi

# Get the new content
NEW_STRING=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')

# Skip if content is short (likely a small edit, not a data block)
CONTENT_LEN=${#NEW_STRING}
if [ "$CONTENT_LEN" -lt 200 ]; then
  exit 0
fi

# Look for patterns that suggest hardcoded game data:
# - Arrays/dictionaries of race names, class names, feat names
# - Multiple game-specific terms in a list/array context
GAME_DATA_PATTERN='("Human"|"Elf"|"Dwarf"|"Halfling"|"Half-Orc"|"Gnome"|"Fighter"|"Wizard"|"Cleric"|"Rogue"|"Barbarian"|"Bard"|"Druid"|"Monk"|"Paladin"|"Ranger"|"Sorcerer")'

# Count matches - if 3+ game terms appear, likely hardcoded data
MATCH_COUNT=$(echo "$NEW_STRING" | grep -oP "$GAME_DATA_PATTERN" | wc -l)

if [ "$MATCH_COUNT" -ge 3 ]; then
  echo "WARNING: Possible hardcoded game data detected ($MATCH_COUNT game terms found). Per CLAUDE.md, use IGameDataService with 2DA/TLK lookups instead of hardcoding races, classes, feats, etc." >&2
  exit 2
fi

exit 0
