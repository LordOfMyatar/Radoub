#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Flag "fall back" and "dedup" patterns in C# code
# These patterns have caused bugs: silent fallbacks mask real errors,
# and name-based dedup silently drops entries from different sources.
# Force the developer to justify whether a fallback/dedup is correct
# or if it should be a proper error/distinct entry.
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

# Only check C# files, skip test files
if ! echo "$FILE_PATH" | grep -qP '\.cs$'; then
  exit 0
fi
if echo "$FILE_PATH" | grep -qiP '(test|spec)'; then
  exit 0
fi

# Skip if input is very short (variable rename, etc.)
INPUT_LEN=${#INPUT}
if [ "$INPUT_LEN" -lt 50 ]; then
  exit 0
fi

WARNINGS=""

# Search raw INPUT for patterns (content is embedded in the JSON string)
# Note: We can't filter out comments from raw JSON, but false positives
# from JSON keys/structure are unlikely to match these specific code patterns

# Pattern 1: Null-coalescing fallback to a different service/instance
# e.g., _context?.Foo ?? SomeOtherService.Instance.Foo
if echo "$INPUT" | grep -qP '\?\?.*\.(Instance|Default|Current)'; then
  WARNINGS="${WARNINGS}\n- FALLBACK: Null-coalescing to a different service instance detected. Is this masking a missing dependency? Should the caller be required to provide this value instead of silently falling back?"
fi

# Pattern 2: Explicit fallback language in code
if echo "$INPUT" | grep -qiP '(fall\s*back|fallback)'; then
  WARNINGS="${WARNINGS}\n- FALLBACK: Code uses 'fallback' language. Should this be an error/warning instead? Silent fallbacks can mask configuration problems and make debugging harder."
fi

# Pattern 3: Dedup by name only (common pattern: .Any(x => x.Name.Equals(...)))
if echo "$INPUT" | grep -qP '\.Any\(\s*\w+\s*=>\s*\w+\.Name\.(Equals|==)'; then
  WARNINGS="${WARNINGS}\n- DEDUP: Name-only deduplication detected. Entries from different sources (module/vault/HAK) can share names but be different files. Should this also check Source, FilePath, or another distinguishing field?"
fi

# Pattern 4: Generic dedup that might lose data
if echo "$INPUT" | grep -qP '\.Distinct\(\)|\.DistinctBy\(.*Name'; then
  WARNINGS="${WARNINGS}\n- DEDUP: Distinct/DistinctBy on Name detected. Will this silently drop entries from different sources that share the same name?"
fi

if [ -n "$WARNINGS" ]; then
  echo "REVIEW REQUIRED: Fallback/dedup patterns detected that have previously caused bugs:${WARNINGS}" >&2
  echo "" >&2
  echo "These patterns are not always wrong, but they need explicit justification. If the fallback/dedup is intentional, add a comment explaining why it's safe." >&2
  exit 2
fi

exit 0
