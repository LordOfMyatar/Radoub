# Test Files

This directory contains test DLG files and standalone test programs for dialog file operations.

## Test Files

- **lista.dlg** - Simple test conversation (minimal complexity)
- **myra_james.dlg** - Medium complexity conversation
- **chef.dlg** - Complex conversation with multiple branches

## Using Test Files in Code

All test programs should use **workspace-relative paths** instead of hardcoded paths.

### Correct Approach

```csharp
using TestingTools.TestFiles;

// Get path to a test file
string testFile = TestPathHelper.GetTestFilePath("chef.dlg");

// Get TestFiles directory
string testDir = TestPathHelper.GetTestFilesDir();

// Get workspace root
string workspace = TestPathHelper.GetWorkspaceRoot();
```

### Incorrect Approach (DO NOT USE)

```csharp
// ❌ Hardcoded paths - will break on other machines
string testFile = @"D:\LOM\Tools\LNS_DLG\TestFiles\chef.dlg";
string testFile = @"C:\Users\Name\Documents\...\chef.dlg";
```

## TestPathHelper

The `TestPathHelper` class automatically finds the workspace root by searching for `LNS_DLG.sln` in parent directories. This works regardless of:
- Build configuration (Debug/Release)
- Developer's directory structure
- Platform (Windows/Linux/macOS)

## Adding New Test Files

1. Place .dlg files directly in `TestingTools/TestFiles/`
2. Use `TestPathHelper.GetTestFilePath("filename.dlg")` in test code
3. Use `UnifiedLogger.SanitizePath()` when logging paths

## Privacy

Always use `UnifiedLogger.SanitizePath()` when logging file paths to prevent exposing user directory structures.
