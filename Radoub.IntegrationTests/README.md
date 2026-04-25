# Radoub.IntegrationTests

Automated GUI testing for Radoub tools using FlaUI.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Running Tests](#running-tests)
- [Project Structure](#project-structure)
- [Writing New Tests](#writing-new-tests)
- [Troubleshooting](#troubleshooting)

---

## Overview

This project uses [FlaUI](https://github.com/FlaUI/FlaUI) for Windows UI automation. FlaUI wraps Microsoft's UI Automation (UIA) framework, providing a clean .NET API for interacting with desktop applications.

**Why FlaUI over Appium/WinAppDriver?**
- No external dependencies to install (just NuGet packages)
- Actively maintained
- Native .NET library
- Works with WPF, WinForms, UWP, and Avalonia apps

### Serialization guarantee (#1526)

FlaUI tests share desktop-global resources — the foreground window, keyboard focus, and the UIA client — so they must run sequentially.

Two safeguards enforce this:

1. **Within the assembly**: `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (`AssemblyInfo.cs`) makes every test in `Radoub.IntegrationTests` run one at a time, regardless of `[Collection]` assignment.
2. **Across processes**: `FlaUITestBase` acquires a named system mutex (`Global\Radoub.FlaUI.SerialExecution`) per test. A second `dotnet test` invocation, IDE Test Explorer run, or developer session attempting to run FlaUI tests at the same time will block on the mutex (30 s timeout, then a clear error naming the lock).

The FlaUI mutex itself is unit-tested in `Shared/FlaUIGlobalMutexTests.cs` — those tests use unique `Local\…` mutex names so they don't interfere with real FlaUI runs.

---

## Prerequisites

1. **Windows 10/11** - UI Automation is Windows-only
2. **.NET 9.0 SDK** - Same as the main projects
3. **Built application** - Parley must be built before running tests

That's it - no WinAppDriver or other tools needed.

---

## Running Tests

### Build the Application First

```powershell
dotnet build Parley/Parley
```

### Run All UI Tests

```powershell
dotnet test Radoub.IntegrationTests
```

### Run Only Smoke Tests

```powershell
dotnet test Radoub.IntegrationTests --filter "Category=Smoke"
```

### Run Parley Tests Only

```powershell
dotnet test Radoub.IntegrationTests --filter "FullyQualifiedName~Parley"
```

### From Visual Studio

1. Open the solution in Visual Studio
2. Open Test Explorer (Test → Test Explorer)
3. Run tests from there

---

## Project Structure

```
Radoub.IntegrationTests/
├── Radoub.IntegrationTests.csproj    # Test project file
├── README.md                 # This file
├── Shared/                   # Common utilities
│   ├── FlaUITestBase.cs      # Base class with app launch/teardown
│   └── TestPaths.cs          # Path resolution utilities
└── Parley/                   # Parley-specific tests
    ├── ParleyTestBase.cs     # Parley test base class
    └── SmokeTests.cs         # Basic launch tests
```

---

## Writing New Tests

### Basic Pattern

```csharp
public class MyTests : ParleyTestBase
{
    [Fact]
    public void MyTest()
    {
        StartApplication();

        // Find elements and interact
        var button = MainWindow!.FindFirstDescendant(cf => cf.ByName("Save"));
        button?.AsButton().Click();

        // Assert results
        Assert.NotNull(button);
    }
}
```

### Finding Elements

FlaUI provides several ways to find UI elements:

```csharp
// By name (visible text or automation name)
var element = MainWindow.FindFirstDescendant(cf => cf.ByName("File"));

// By automation ID (most reliable)
var element = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SaveButton"));

// By control type
var buttons = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));

// Combined conditions
var element = MainWindow.FindFirstDescendant(cf =>
    cf.ByControlType(ControlType.Button).And(cf.ByName("OK")));
```

### Interacting with Elements

```csharp
// Buttons
element.AsButton().Click();

// Text boxes
element.AsTextBox().Enter("Hello");

// Menu items
menuItem.AsMenuItem().Click();

// Check boxes
element.AsCheckBox().IsChecked = true;
```

### Test Categories

Use `[Trait("Category", "...")]` to categorize tests:
- `Smoke` - Basic launch/existence tests
- `UI` - UI interaction tests
- `Integration` - Full workflow tests

---

## Troubleshooting

### "Application not found"

Build the application first:
```powershell
dotnet build Parley/Parley
```

### "Element not found" / Returns null

- Check the element name matches exactly (case-sensitive)
- Use FlaUI Inspect or Windows Accessibility Insights to find correct names/IDs
- Add a small delay if the element loads asynchronously:
  ```csharp
  Thread.Sleep(500); // Or use FlaUI's retry mechanisms
  ```

### Tests hang or timeout

The application might be showing a dialog. Check for:
- Error dialogs
- First-run wizards
- Unsaved changes prompts

### Getting Element Info for Debugging

```csharp
// Print all descendants for debugging
foreach (var element in MainWindow.FindAllDescendants())
{
    Console.WriteLine($"{element.ControlType}: '{element.Name}' [{element.AutomationId}]");
}
```

---

## Useful Tools

- **FlaUI Inspect** - Comes with FlaUI, helps identify elements
- **Accessibility Insights for Windows** - Microsoft's free tool for inspecting UI automation tree
- **Spy++** - Visual Studio tool for window inspection

---

## Future Improvements

- [ ] Screenshot capture on test failure
- [ ] CI/CD integration (GitHub Actions)
- [ ] Performance benchmarking tests
- [ ] Cross-tool test utilities

---

## Resources

- [FlaUI GitHub](https://github.com/FlaUI/FlaUI)
- [FlaUI Wiki](https://github.com/FlaUI/FlaUI/wiki)
- [UI Automation Overview](https://docs.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32)
