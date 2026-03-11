#!/bin/bash
# Hook: PostToolUse (Edit, Write)
# Purpose: Catch hardcoded colors instead of theme resources
# CLAUDE.md rule: "Use BrushManager for Success/Warning/Error colors"
# Anti-pattern: "Duplicate brush methods in each file"
# Must use DynamicResource or BrushManager, not hardcoded hex/named colors
# Note: Uses grep/cut instead of jq (not available in Windows Git Bash)
# For content matching, searches raw JSON input (patterns appear in the JSON string)

INPUT=$(cat)
TOOL=$(echo "$INPUT" | grep -o '"tool_name":"[^"]*"' | head -1 | cut -d'"' -f4)

# Only check Edit and Write tools
if [ "$TOOL" != "Edit" ] && [ "$TOOL" != "Write" ]; then
  exit 0
fi

FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | cut -d'"' -f4)

# Skip short inputs (unlikely to be adding color blocks)
INPUT_LEN=${#INPUT}
if [ "$INPUT_LEN" -lt 50 ]; then
  exit 0
fi

# === AXAML FILES: Check for hardcoded hex colors ===
if echo "$FILE_PATH" | grep -qP '\.axaml$'; then
  # Allow ThemeManager resource files (they define the themes)
  if echo "$FILE_PATH" | grep -qiP 'ThemeManager'; then
    exit 0
  fi

  # Search raw INPUT for patterns (content is embedded in the JSON string)
  # Check for hardcoded Foreground/Background/Fill/Stroke hex colors
  # Pattern: Property="#RRGGBB" or Property="#AARRGGBB"
  if echo "$INPUT" | grep -qP '(Foreground|Background|Fill|Stroke|BorderBrush|Color)="(#[0-9A-Fa-f]{6,8})"'; then
    # Allow transparent/semi-transparent (opacity patterns like #00000000)
    # Allow if within a Style or theme definition
    if echo "$INPUT" | grep -qP '<Style'; then
      exit 0
    fi
    echo "WARNING: Hardcoded hex color detected in AXAML. Use DynamicResource with theme keys (ThemeSuccess, ThemeWarning, ThemeError, ThemeInfo, etc.) or BrushManager in code-behind. See Radoub.UI/Services/BrushManager.cs." >&2
    exit 2
  fi

  # Check for hardcoded named colors in Foreground/Background attributes
  # Pattern: Foreground="LimeGreen", Background="Red", etc.
  # Allowed: "Transparent", "White" (used in Style setters for selected items)
  NAMED_COLORS='(LimeGreen|Red|Green|Orange|Yellow|DodgerBlue|OrangeRed|DarkRed|Crimson|Gray|DarkGray|LightGray|Blue|Cyan|Magenta|Pink|Purple|Brown|Gold|Silver)'
  if echo "$INPUT" | grep -qP "(Foreground|Background|Fill|Stroke)=\"$NAMED_COLORS\""; then
    # Allow if within a Style definition (e.g., selected item foreground)
    if echo "$INPUT" | grep -qP '<Style'; then
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

  # Search raw INPUT for patterns (content is embedded in the JSON string)
  # Check for new SolidColorBrush(Colors.X) pattern (named colors for semantic purposes)
  SEMANTIC_COLORS='(Colors\.(Green|Red|Orange|DodgerBlue|Yellow|LimeGreen|OrangeRed|DarkRed|Crimson))'
  if echo "$INPUT" | grep -qP "new SolidColorBrush\($SEMANTIC_COLORS\)"; then
    echo "WARNING: Hardcoded SolidColorBrush with named color detected. Use BrushManager.GetSuccessBrush(), GetWarningBrush(), GetErrorBrush(), GetInfoBrush() instead. See Radoub.UI/Services/BrushManager.cs." >&2
    exit 2
  fi

  # Check for Color.Parse with hex in non-ThemeManager context
  PARSE_COUNT=$(echo "$INPUT" | grep -oP 'Color\.Parse\("#[0-9A-Fa-f]+"' | wc -l)
  if [ "$PARSE_COUNT" -ge 2 ]; then
    echo "WARNING: Multiple Color.Parse() with hardcoded hex values detected. Use theme resources or BrushManager for semantic colors. See Radoub.UI/Services/BrushManager.cs." >&2
    exit 2
  fi
fi

exit 0
