#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Catch ShowDialog() usage in new C# code
# CLAUDE.md rule: "NEVER use modal dialogs (ShowDialog()) that block the main application"
# Exception: Confirmation dialogs for destructive actions may be modal
# Note: Uses grep/cut instead of jq (not available in Windows Git Bash)
# For content matching, searches raw JSON input (patterns appear in the JSON string)

INPUT=$(cat)
TOOL=$(echo "$INPUT" | grep -o '"tool_name":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only check Edit and Write tools
if [ "$TOOL" != "Edit" ] && [ "$TOOL" != "Write" ]; then
  exit 0
fi

# Get the file path
FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only check C# files
if ! echo "$FILE_PATH" | grep -qP '\.cs$'; then
  exit 0
fi

# Search raw INPUT for patterns (content is embedded in the JSON string)
# Check for ShowDialog in the new content
if echo "$INPUT" | grep -qP '\.ShowDialog\s*\('; then
  # Allow if it's clearly a confirmation dialog (contains confirm/delete/overwrite keywords nearby)
  if echo "$INPUT" | grep -qiP '(confirm|delete|overwrite|destructive|remove|discard)'; then
    exit 0
  fi
  echo "WARNING: ShowDialog() detected. Use Show() for info windows per CLAUDE.md UI/UX guidelines. ShowDialog() is only acceptable for destructive action confirmations." >&2
  exit 2
fi

exit 0
