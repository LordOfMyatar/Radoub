# Testing Guide - Parley

**Purpose**: Developer guide for writing and running tests in Parley
**Last Updated**: 2025-11-16
**Related**: Issue #81 - GUI Test Coverage Expansion

---

## Table of Contents

[TOC]

---

## Overview

Parley uses **xUnit** for unit testing with **Avalonia.Headless** for GUI integration tests. Test coverage includes:

- **Parser/Binary Format** tests (GFF, DLG round-trip)
- **Service-level** tests (Clipboard, Undo, File operations)
- **Security** tests (Plugin sandbox, permissions)
- **GUI Integration** tests (Dialog loading, node creation)

**Current Coverage**: 201 tests across 22 test files

---

## Running Tests

### All Tests

```bash
cd Parley
dotnet test
```

### Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~DialogLoadingHeadlessTests"
```

### Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName=Parley.Tests.GUI.DialogLoadingHeadlessTests.CreateDialog_InitializesCorrectly"
```

### With Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## Test Types

### 1. Unit Tests (Service/Model Level)

**When to use**: Testing business logic, data models, services

**Example**: [UnifiedLoggerTests.cs](../../Parley.Tests/UnifiedLoggerTests.cs)

```csharp
[Fact]
public void SanitizePath_ReplacesUserProfileWithTilde_Windows()
{
    // Arrange
    var testPath = Path.Combine(_testUserProfile, "Parley", "Logs", "test.log");

    // Act
    var sanitized = UnifiedLogger.SanitizePath(testPath);

    // Assert
    Assert.StartsWith("~", sanitized);
    Assert.DoesNotContain(_testUserProfile, sanitized);
    Assert.Contains("Parley", sanitized);
}
```

**Pattern**:
- **Arrange**: Set up test data
- **Act**: Execute function under test
- **Assert**: Verify expected outcome

---

### 2. Avalonia.Headless Tests (GUI Integration)

**When to use**: Testing UI workflows, dialog loading, node creation

**Example**: [DialogLoadingHeadlessTests.cs](../../Parley.Tests/GUI/DialogLoadingHeadlessTests.cs)

```csharp
[AvaloniaFact]  // Special attribute for Headless tests
public void CreateDialog_InitializesCorrectly()
{
    // Arrange & Act: Create new dialog
    var dialog = new Dialog();

    // Assert: Initial state correct
    Assert.NotNull(dialog);
    Assert.NotNull(dialog.Entries);
    Assert.NotNull(dialog.Replies);
    Assert.Empty(dialog.Entries);
}
```

**Key Differences**:
- Use `[AvaloniaFact]` instead of `[Fact]`
- Use `[AvaloniaTheory]` instead of `[Theory]`
- Tests run in headless mode (no actual UI window)
- Avalonia application initialized automatically

---

### 3. Async Tests

**When to use**: Testing file I/O, network operations, async workflows

**Example**:

```csharp
[AvaloniaFact]
public async Task LoadDialog_SimpleFile_ParsesSuccessfully()
{
    // Arrange
    var testFile = Path.Combine(_testFilesPath, "test1_link.dlg");
    var dialogService = new DialogFileService();

    // Act
    var dialog = await dialogService.LoadFromFileAsync(testFile);

    // Assert
    Assert.NotNull(dialog);
    Assert.NotEmpty(dialog.Entries);
}
```

**Pattern**:
- Return type: `Task` or `Task<T>`
- Use `async`/`await` keywords
- Async methods automatically waited for completion

---

## Writing New Tests

### Step 1: Choose Test Type

| Scenario | Test Type | Attribute |
|----------|-----------|-----------|
| Business logic | Unit Test | `[Fact]` |
| Dialog loading | Headless Test | `[AvaloniaFact]` |
| File operations | Async Test | `[Fact]` + `async Task` |
| UI interaction | Headless Test | `[AvaloniaFact]` |

### Step 2: Create Test File

**Location**: `Parley.Tests/` or `Parley.Tests/GUI/` for Headless tests

**Naming Convention**:
- Class: `<Feature>Tests.cs` (e.g., `DialogLoadingHeadlessTests.cs`)
- Method: `<Scenario>_<Expected>` (e.g., `LoadDialog_SimpleFile_ParsesSuccessfully`)

**Example Structure**:

```csharp
using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using Xunit;

namespace Parley.Tests.GUI
{
    public class MyFeatureHeadlessTests
    {
        [AvaloniaFact]
        public void Scenario_ExpectedBehavior()
        {
            // Arrange
            // Act
            // Assert
        }
    }
}
```

### Step 3: Write Test Logic

**Good Test Checklist**:
- [ ] Test name describes scenario and expected outcome
- [ ] Arrange/Act/Assert pattern followed
- [ ] Single responsibility (test one thing)
- [ ] No dependencies on external state
- [ ] Fast execution (< 2s per test)
- [ ] Deterministic (same input → same output)

---

## Common Test Patterns

### Pattern 1: Dialog Creation

```csharp
private Dialog CreateSimpleDialog()
{
    var dialog = new Dialog();

    var entry = dialog.CreateNode(DialogNodeType.Entry);
    entry!.Text.Add(0, "Test Entry");
    dialog.AddNodeInternal(entry, DialogNodeType.Entry);

    var entryPtr = dialog.CreatePtr();
    entryPtr!.Type = DialogNodeType.Entry;
    entryPtr.Node = entry;
    entryPtr.Index = 0;
    entryPtr.IsLink = false;
    dialog.Starts.Add(entryPtr);

    return dialog;
}
```

### Pattern 2: File Loading

```csharp
[AvaloniaFact]
public async Task LoadDialog_File_ParsesCorrectly()
{
    var testFile = Path.Combine(_testFilesPath, "test.dlg");

    if (!File.Exists(testFile))
    {
        return; // Skip if test file missing
    }

    var dialogService = new DialogFileService();
    var dialog = await dialogService.LoadFromFileAsync(testFile);

    Assert.NotNull(dialog);
}
```

### Pattern 3: Test Data Setup

```csharp
public class MyTests : IDisposable
{
    private readonly string _testDirectory;

    public MyTests()
    {
        // Setup: Create temp directory
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ParleyTests_{Guid.NewGuid()}"
        );
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Cleanup: Delete temp directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
```

---

## Privacy & Security in Tests

### Rule 1: No Real Usernames in Test Data

❌ **Bad**:
```csharp
[InlineData("C:\\Users\\....\\Documents\\file.txt")]
```

✅ **Good**:
```csharp
[InlineData("~\\Documents\\file.txt")]
```

### Rule 2: Use Environment Variables

✅ **Good**:
```csharp
var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var testPath = Path.Combine(userProfile, "Parley", "test.dlg");
```

### Rule 3: Sanitize Paths in Assertions

✅ **Good**:
```csharp
Assert.StartsWith("~", sanitized);
Assert.DoesNotContain(userProfile, sanitized);
```

---

## Debugging Tests

### Debug Single Test in IDE

1. Set breakpoint in test method
2. Right-click test in Test Explorer
3. Select "Debug Test"

### View Test Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Common Issues

#### Issue: `TypeLoadException` in Headless tests

**Cause**: Avalonia.Headless version mismatch

**Fix**: Ensure `Avalonia.Headless.XUnit` version matches main Avalonia version in `Parley.csproj`

```xml
<!-- Parley.Tests.csproj -->
<PackageReference Include="Avalonia.Headless.XUnit" Version="11.3.6" />
```

#### Issue: Test file not found

**Cause**: Relative path incorrect

**Fix**: Use robust path resolution:

```csharp
var testFile = Path.Combine(
    Directory.GetCurrentDirectory(),
    "..", "..", "..", "..", // Navigate to repo root
    "TestingTools", "TestDialogFiles", "test.dlg"
);
```

Or skip test if file missing:

```csharp
if (!File.Exists(testFile))
{
    return; // Skip test
}
```

---

## CI/CD Integration

### GitHub Actions

Tests run automatically on PR creation and push:

```yaml
- name: Run Tests
  run: dotnet test --logger "console;verbosity=minimal"
```

**Requirements**:
- All tests must pass before PR can merge
- Test execution time < 30s for full suite
- No flaky tests (tests that randomly fail)

---

## Best Practices

### DO:
- ✅ Write tests before fixing bugs (TDD for bug fixes)
- ✅ Keep tests fast (< 2s per test)
- ✅ Use descriptive test names
- ✅ Test edge cases and error conditions
- ✅ Clean up test data in `Dispose()`

### DON'T:
- ❌ Test implementation details (test behavior, not internals)
- ❌ Use real user paths/names in test data
- ❌ Create dependencies between tests
- ❌ Skip assertions (every test must assert something)
- ❌ Test framework code (test your logic, not xUnit/Avalonia)

---

## Test Coverage Goals

### Current Coverage (2025-11-16)

| Category | Tests | Status |
|----------|-------|--------|
| Parser/GFF | 32 | ✅ Excellent |
| Copy/Paste | 21 | ✅ Excellent |
| Delete Operations | 10 | ✅ Good |
| Undo/Redo | 5 | ✅ Good |
| Orphan Detection | 14 | ✅ Excellent |
| Plugin Security | 35 | ✅ Excellent |
| Script Service | 13 | ✅ Good |
| Settings/Logging | 35 | ✅ Excellent |
| GUI Workflows | 7 | ⚠️ Basic |

### Target Coverage

- **Critical workflows**: 100% (parser, file I/O)
- **Business logic**: 80%+ (services, managers)
- **UI integration**: 50%+ (Headless tests for key workflows)

---

## Resources

- **xUnit Documentation**: https://xunit.net/
- **Avalonia.Headless**: https://github.com/AvaloniaUI/Avalonia/tree/master/tests/Avalonia.Headless.XUnit
- **Existing Tests**: `Parley.Tests/` directory
- **Issue #81**: GUI test coverage expansion tracking

---

**Next Steps**: Add more Headless tests for TreeView operations, node editing, and link creation UI workflows.
