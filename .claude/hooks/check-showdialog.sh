#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Catch ShowDialog() usage in new C# code
# CLAUDE.md rule: "NEVER use modal dialogs (ShowDialog()) that block the main application"
# Exception: Confirmation dialogs for destructive actions may be modal

INPUT=$(cat)
TOOL=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only check Edit and Write tools
if [ "$TOOL" != "Edit" ] && [ "$TOOL" != "Write" ]; then
  exit 0
fi

# Get the file path
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Only check C# files
if ! echo "$FILE_PATH" | grep -qP '\.cs$'; then
  exit 0
fi

# Get the new content being written/edited
NEW_STRING=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')

# Check for ShowDialog in the new content
if echo "$NEW_STRING" | grep -qP '\.ShowDialog\s*\('; then
  # Allow if it's clearly a confirmation dialog (contains confirm/delete/overwrite keywords nearby)
  if echo "$NEW_STRING" | grep -qiP '(confirm|delete|overwrite|destructive|remove|discard)'; then
    exit 0
  fi
  echo "WARNING: ShowDialog() detected. Use Show() for info windows per CLAUDE.md UI/UX guidelines. ShowDialog() is only acceptable for destructive action confirmations." >&2
  exit 2
fi

exit 0
