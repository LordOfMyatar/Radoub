#!/bin/bash
# Hook: PreToolUse (Read)
# Purpose: Block reading PDF files without explicit user instruction
# CLAUDE.md rule: "NEVER read PDF files without explicit user instruction"
# PDFs can exceed context limits and cause "prompt too long" errors
# Note: Uses grep/cut instead of jq (not available in Windows Git Bash)

INPUT=$(cat)
TOOL=$(echo "$INPUT" | grep -o '"tool_name":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only check Read tool
if [ "$TOOL" != "Read" ]; then
  exit 0
fi

FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | cut -d'"' -f4)

# Block PDF file reads
if echo "$FILE_PATH" | grep -qiP '\.pdf$'; then
  echo "BLOCKED: Reading PDF files is prohibited without explicit user instruction. PDFs can exceed context limits. Ask the user before reading PDF files." >&2
  exit 2
fi

exit 0
