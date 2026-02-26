#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Catch hardcoded colors instead of theme resources
# CLAUDE.md rule: "Use BrushManager for Success/Warning/Error colors"
# Anti-pattern: "Duplicate brush methods in each file"
# Must use DynamicResource or BrushManager, not hardcoded hex/named colors

INPUT=$(cat)
TOOL=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only check Edit and Write tools
if [ "$TOOL" != "Edit" ] && [ "$TOOL" != "Write" ]; then
  exit 0
fi

FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
NEW_STRING=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')

# Skip short edits (unlikely to be adding color blocks)
CONTENT_LEN=${#NEW_STRING}
if [ "$CONTENT_LEN" -lt 50 ]; then
  exit 0
fi

# === AXAML FILES: Check for hardcoded hex colors ===
if echo "$FILE_PATH" | grep -qP '\.axaml$'; then
  # Allow ThemeManager resource files (they define the themes)
  if echo "$FILE_PATH" | grep -qiP 'ThemeManager'; then
    exit 0
  fi

  # Check for hardcoded Foreground/Background/Fill/Stroke hex colors
  # Pattern: Property="#RRGGBB" or Property="#AARRGGBB"
  if echo "$NEW_STRING" | grep -qP '(Foreground|Background|Fill|Stroke|BorderBrush|Color)="(#[0-9A-Fa-f]{6,8})"'; then
    # Allow transparent/semi-transparent (opacity patterns like #00000000)
    # Allow if within a Style or theme definition
    if echo "$NEW_STRING" | grep -qP '<Style'; then
      exit 0
    fi
    echo "WARNING: Hardcoded hex color detected in AXAML. Use DynamicResource with theme keys (ThemeSuccess, ThemeWarning, ThemeError, ThemeInfo, etc.) or BrushManager in code-behind. See Radoub.UI/Services/BrushManager.cs." >&2
    exit 2
  fi

  # Check for hardcoded named colors in Foreground/Background attributes
  # Pattern: Foreground="LimeGreen", Background="Red", etc.
  # Allowed: "Transparent", "White" (used in Style setters for selected items)
  NAMED_COLORS='(LimeGreen|Red|Green|Orange|Yellow|DodgerBlue|OrangeRed|DarkRed|Crimson|Gray|DarkGray|LightGray|Blue|Cyan|Magenta|Pink|Purple|Brown|Gold|Silver)'
  if echo "$NEW_STRING" | grep -qP "(Foreground|Background|Fill|Stroke)=\"$NAMED_COLORS\""; then
    # Allow if within a Style definition (e.g., selected item foreground)
    if echo "$NEW_STRING" | grep -qP '<Style'; then
      exit 0
    fi
    echo "WARNING: Hardcoded named color detected in AXAML. Use DynamicResource with theme keys (ThemeSuccess, ThemeWarning, ThemeError, ThemeInfo, etc.) instead. See Radoub.UI/Services/BrushManager.cs." >&2
    exit 2
  fi
fi

# === C# FILES: Check for hardcoded brush creation ===
if echo "$FILE_PATH" | grep -qP '\.cs$'; then
  # Allow ThemeManager, BrushManager themselves
  if echo "$FILE_PATH" | grep -qiP '(ThemeManager|BrushManager)'; then
    exit 0
  fi
  # Allow test files
  if echo "$FILE_PATH" | grep -qiP '(test|spec)'; then
    exit 0
  fi

  # Check for new SolidColorBrush(Colors.X) pattern (named colors for semantic purposes)
  SEMANTIC_COLORS='(Colors\.(Green|Red|Orange|DodgerBlue|Yellow|LimeGreen|OrangeRed|DarkRed|Crimson))'
  if echo "$NEW_STRING" | grep -qP "new SolidColorBrush\($SEMANTIC_COLORS\)"; then
    echo "WARNING: Hardcoded SolidColorBrush with named color detected. Use BrushManager.GetSuccessBrush(), GetWarningBrush(), GetErrorBrush(), GetInfoBrush() instead. See Radoub.UI/Services/BrushManager.cs." >&2
    exit 2
  fi

  # Check for Color.Parse with hex in non-ThemeManager context
  if echo "$NEW_STRING" | grep -qP 'Color\.Parse\("#[0-9A-Fa-f]+"' | grep -cP 'Color\.Parse' > /dev/null 2>&1; then
    PARSE_COUNT=$(echo "$NEW_STRING" | grep -oP 'Color\.Parse\("#[0-9A-Fa-f]+"' | wc -l)
    if [ "$PARSE_COUNT" -ge 2 ]; then
      echo "WARNING: Multiple Color.Parse() with hardcoded hex values detected. Use theme resources or BrushManager for semantic colors. See Radoub.UI/Services/BrushManager.cs." >&2
      exit 2
    fi
  fi
fi

exit 0
