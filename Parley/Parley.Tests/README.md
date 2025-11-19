# Parley Test Suite

Comprehensive unit tests for Parley dialog editor (231 tests as of 2025-11-18)

## Test Organization

### Unit Tests (Parley.Tests/)
All tests use xUnit framework and follow AAA pattern (Arrange-Act-Assert).

**Test Categories**:
- **Parser Tests** - DLG file parsing and binary format validation
- **Orphan Node Tests** - Orphaned node detection and cleanup
- **Service Tests** - Isolated service class testing
- **Integration Tests** - Multi-component workflows
- **GUI Tests** - Headless UI testing (skipped by default)

### Test Files

#### Core Parsing
- **BasicParserTests.cs** (5 tests)
  - Simple dialog parsing
  - Field validation
  - Basic round-trip testing

- **GffParserTests.cs** (15 tests)
  - GFF binary format reading
  - Struct/field parsing
  - Index validation

#### Orphan Handling
- **OrphanNodeTests.cs** (18+ tests)
  - Parent-child link preservation
  - Orphaned node detection (2025-11-18: Fixed CollectReachableNodes to skip child links)
  - Orphaned link children (nodes with only IsLink=true incoming pointers)
  - Circular reference handling
  - Complex dialog structure testing

- **OrphanNodeCleanupTests.cs** (4 tests)
  - RemoveOrphanedNodes validation
  - Orphan subtree handling

- **OrphanContainerIntegrationTests.cs** (3 tests)
  - Full orphan workflow with file persistence
  - Orphan container creation

- **LazyLoadingOrphanDetectionTests.cs** (5 tests)
  - Performance testing for orphan detection
  - Large dialog handling

#### Services
- **DialogClipboardServiceTests.cs** (8 tests)
  - Copy/paste operations
  - Link vs duplicate creation
  - Clipboard state management

- **ScriptServiceCacheTests.cs** (6 tests)
  - Script metadata caching
  - Cache invalidation

- **SettingsServiceTests.cs** (10 tests)
  - Application settings persistence
  - Recent files management

- **UnifiedLoggerTests.cs** (8 tests)
  - Path sanitization
  - Log level filtering
  - Session log management

#### Node Operations
- **DeleteOperationTests.cs** (12 tests)
  - Node deletion with children
  - Link handling during deletion
  - Scrap integration

- **DeleteDeepTreeTests.cs** (4 tests)
  - Deep tree deletion performance
  - Nested structure handling

- **TreeNavigationManagerTests.cs** (16 tests, added 2025-11-17)
  - FindTreeNodeForDialogNode
  - Expansion state save/restore (both reference-based and path-based)
  - Circular reference handling in tree traversal
  - Tree structure capture for debugging

#### Copy/Paste
- **CopyPasteTests.cs** (6 tests)
  - Simple node copying
  - Link preservation
  - Index remapping

#### Utilities
- **DebounceTests.cs** (3 tests)
  - Debounce timing validation
  - Event throttling

- **UndoRedoTests.cs** (8 tests)
  - Undo stack management
  - Redo functionality

- **UndoStackLimitTests.cs** (3 tests)
  - Stack size limits
  - Memory management

#### GUI Tests (Headless - Skipped by Default)
- **DialogLoadingHeadlessTests.cs** (skipped)
- **NodeCreationHeadlessTests.cs** (skipped)
- **NodeDeletionHeadlessTests.cs** (skipped)
- **CopyPasteHeadlessTests.cs** (skipped)

GUI tests require Avalonia UI headless mode and are skipped in CI. Run manually when testing UI workflows.

#### Security Tests
- **MaliciousPluginTests.cs** - Plugin security validation
- **PermissionEnforcementTests.cs** - File permission checks
- **RateLimitTests.cs** - API rate limiting
- **SandboxTests.cs** - Plugin sandboxing
- **TimeoutTests.cs** - Operation timeout handling

## Running Tests

### All Tests
```bash
dotnet test Parley.Tests/Parley.Tests.csproj
```

### Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~OrphanNodeTests"
```

### Specific Test Method
```bash
dotnet test --filter "FullyQualifiedName~RemoveOrphanedNodes_RemovesNodeWithOnlyChildLinks"
```

### Excluding Skipped Tests (GUI tests)
```bash
dotnet test --filter "Category!=GUI"
```

## Test Patterns

### AAA Pattern (Arrange-Act-Assert)
All tests follow this structure:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and mocks
    var dialog = new Dialog();
    var node = CreateTestNode();

    // Act - Execute the method under test
    var result = _service.ProcessNode(dialog, node);

    // Assert - Verify expected outcomes
    Assert.NotNull(result);
    Assert.Equal(expectedValue, result.Value);
}
```

### Helper Methods
Common test helpers in each test class:
- `CreateSimpleDialog()` - Single entry dialog
- `CreateNestedDialog()` - Multi-level dialog structure
- `CreateCircularDialog()` - Dialog with circular references (for testing prevention)

### LocString Initialization
DialogNode.Text requires `Add()` pattern, not constructor:

```csharp
// ❌ WRONG
var node = new DialogNode { Text = new LocString("Hello") };

// ✅ CORRECT
var node = new DialogNode { Text = new LocString() };
node.Text.Add(0, "Hello");
```

## Critical Test Coverage Areas

### Orphan Node Handling (Most Complex)
**Why Critical**: Prevents data loss in complex dialog structures

**Key Tests**:
- `OrphanNodeTests:DeleteNode_PreservesParentInParentChildLink` - Ensures parent nodes with child links aren't orphaned incorrectly
- `OrphanNodeTests:RemoveOrphanedNodes_RemovesNodeWithOnlyChildLinks` (2025-11-18) - Validates orphan detection skips child links (IsLink=true)
- `OrphanNodeTests:CollectReachableNodes_DoesNotTraverseChildLinks` (2025-11-18) - Ensures child links don't prevent orphaning
- `OrphanContainerIntegrationTests:DeletingParentEntry_CreatesOrphanContainer_AndPersistsToFile` - Full workflow validation

**Recent Fixes (2025-11-18)**:
- Fixed `CollectReachableNodes` to skip child links (IsLink=true) when determining reachability
- Added immediate orphan removal during deletion (not deferred to save)
- Added 4 new regression tests for orphaned link children scenarios

### Parser Round-Trip Testing
**Why Critical**: Aurora Engine requires byte-perfect format compliance

**Key Tests**:
- `BasicParserTests:ParseFromFile_ValidDlg_ReturnsDialog` - Basic parsing validation
- `GffParserTests:ParseGffHeader_ValidFile_ReturnsCorrectHeader` - Binary format validation
- Round-trip tests in `TestingTools/DiagnoseExport/` (integration level)

### Link Structure Integrity
**Why Critical**: IsLink=true pointers create shared content, must preserve structure

**Key Tests**:
- `CopyPasteTests:CopyPaste_NodeWithLink_MaintainsCorrectIndices` - Link pointer validation
- `OrphanNodeTests:DeletingLinkParent_ShouldOrphanLinkedNodes` - Link parent orphaning behavior

## Test Data

### Test Files Location
`TestingTools/TestFiles/` - Sample DLG files for testing:
- `lista.dlg` - Simple single-conversation dialog
- `myra.dlg` - Complex multi-branch dialog
- `fox.dlg` - Dialog with parent-child links
- `shady_vendor.dlg` - Complex shared reply structures

### Creating Test Dialogs
Use helper methods to create in-memory test data:

```csharp
private Dialog CreateTestDialog()
{
    var dialog = new Dialog();

    var entry = new DialogNode
    {
        Type = DialogNodeType.Entry,
        Text = new LocString(),
        Parent = dialog
    };
    entry.Text.Add(0, "Test Entry");
    dialog.Entries.Add(entry);

    dialog.Starts.Add(new DialogPtr
    {
        Node = entry,
        Type = DialogNodeType.Entry,
        Index = 0
    });

    dialog.RebuildLinkRegistry();
    return dialog;
}
```

## Debugging Failed Tests

### Common Issues

**LocString Constructor Error**:
```
Error: No overload for method 'LocString' takes 1 arguments
Fix: Use Text.Add(languageId, text) pattern
```

**Null Reference in Tests**:
```
Error: NullReferenceException in CollectReachableNodes
Fix: Ensure dialog.RebuildLinkRegistry() called after setup
```

**Index Mismatch Errors**:
```
Error: Pointer index doesn't match position in list
Fix: Call dialog.RebuildLinkRegistry() or manually set pointer indices
```

### Test Logging
Enable verbose logging in tests:

```csharp
// Temporarily enable DEBUG logging
UnifiedLogger.SetMinimumLogLevel(LogLevel.DEBUG);

// Your test code here

// Reset to default
UnifiedLogger.SetMinimumLogLevel(LogLevel.INFO);
```

## CI/CD Integration

### GitHub Actions
Tests run automatically on PRs via `../.github/workflows/pr-build.yml`:
- Build validation
- All unit tests must pass
- GUI tests skipped (headless mode issues)

### Pre-Commit Testing
Before committing changes to node operations, orphan handling, or parsers:

```bash
# Run all tests
dotnet test

# Check for warnings/errors in logs
# Check ~/Parley/Logs/[latest session]/

# If parser changes, run regression tests
cd TestingTools/Scripts
./QuickRegressionCheck.ps1 -OriginalFile "path/to/original.dlg" -ExportedFile "path/to/exported.dlg"
```

## Adding New Tests

### Test File Naming
- Test class: `[Feature]Tests.cs`
- Test method: `MethodName_Scenario_ExpectedBehavior`

### Example New Test
```csharp
namespace Parley.Tests
{
    public class MyNewFeatureTests
    {
        private readonly MyNewFeatureService _service;

        public MyNewFeatureTests()
        {
            _service = new MyNewFeatureService();
        }

        [Fact]
        public void ProcessData_WithValidInput_ReturnsSuccess()
        {
            // Arrange
            var input = CreateTestInput();

            // Act
            var result = _service.ProcessData(input);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void ProcessData_WithInvalidInput_ThrowsException()
        {
            // Arrange
            var input = CreateInvalidInput();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _service.ProcessData(input));
        }

        private TestData CreateTestInput()
        {
            return new TestData { /* ... */ };
        }
    }
}
```

## Test Maintenance

### When to Update Tests
- After refactoring service classes (ensure tests still pass)
- After fixing bugs (add regression test)
- After adding new features (add feature tests)
- After changing orphan handling logic (update OrphanNodeTests)

### Refactoring Safety
Tests provide safety net during Epic #99 MainViewModel refactoring:
- Extract service → Run tests → All pass → Safe to commit
- If tests fail after extraction → Logic error in refactor
- 231 tests covering core functionality ensure refactoring doesn't break behavior

### Test Coverage Goals
- All public service methods should have tests
- All orphan detection scenarios should have tests
- All parser edge cases should have tests
- Critical user workflows should have integration tests

---

**Last Updated**: 2025-11-18
**Total Tests**: 231 passing
**Test Framework**: xUnit
**CI/CD**: GitHub Actions (.NET 9.0)
