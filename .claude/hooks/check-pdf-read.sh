#!/bin/bash
# Hook: PreToolUse (Read)
# Purpose: Block reading PDF files without explicit user instruction
# CLAUDE.md rule: "NEVER read PDF files without explicit user instruction"
# PDFs can exceed context limits and cause "prompt too long" errors

INPUT=$(cat)
TOOL=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only check Read tool
if [ "$TOOL" != "Read" ]; then
  exit 0
fi

FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Block PDF file reads
if echo "$FILE_PATH" | grep -qiP '\.pdf$'; then
  echo "BLOCKED: Reading PDF files is prohibited without explicit user instruction. PDFs can exceed context limits. Ask the user before reading PDF files." >&2
  exit 2
fi

exit 0
