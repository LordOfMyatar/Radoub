#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Catch real user paths in documentation and code comments
# CLAUDE.md rule: "No real usernames in examples", "Use ~ for home directory"
# Only checks markdown and documentation files

INPUT=$(cat)
TOOL=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only check Edit and Write tools
if [ "$TOOL" != "Edit" ] && [ "$TOOL" != "Write" ]; then
  exit 0
fi

FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Only check markdown, text, and documentation files
if ! echo "$FILE_PATH" | grep -qiP '\.(md|txt|rst|adoc)$'; then
  exit 0
fi

# Get the new content
NEW_STRING=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')

# Check for real Windows user paths (C:\Users\username or /c/Users/username patterns)
# Exclude sanitized paths that use ~ or generic placeholders
if echo "$NEW_STRING" | grep -qP '(C:\\\\Users\\\\[A-Z][a-z]+|/[Cc]/Users/[A-Z][a-z]+|/home/[a-z]+/)'; then
  # Allow if it's in a privacy-related context (teaching about sanitization)
  if echo "$NEW_STRING" | grep -qiP '(sanitiz|privacy|example.*(bad|incorrect))'; then
    exit 0
  fi
  echo "WARNING: Real user path detected in documentation. Use ~ for home directory paths per CLAUDE.md privacy guidelines." >&2
  exit 2
fi

exit 0
