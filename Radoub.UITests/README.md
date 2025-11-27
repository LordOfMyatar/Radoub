# Radoub.UITests

Automated GUI testing for Radoub tools using Appium and WinAppDriver.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Setup](#setup)
- [Running Tests](#running-tests)
- [Project Structure](#project-structure)
- [Writing New Tests](#writing-new-tests)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

1. **Windows 10/11** - WinAppDriver only works on Windows
2. **.NET 9.0 SDK** - Same as the main projects
3. **WinAppDriver** - Microsoft's Windows Application Driver

### Installing WinAppDriver

1. Download from: https://github.com/microsoft/WinAppDriver/releases
2. Install to default location: `C:\Program Files\Windows Application Driver\`
3. Enable Developer Mode in Windows Settings:
   - Settings → Privacy & Security → For developers → Developer Mode: ON

---

## Setup

### One-Time Setup

1. Install WinAppDriver (see above)
2. Build Parley in Debug mode:
   ```powershell
   cd Parley/Parley
   dotnet build
   ```

### Before Running Tests

1. Start WinAppDriver (run as Administrator):
   ```powershell
   & "C:\Program Files\Windows Application Driver\WinAppDriver.exe"
   ```
   Keep this window open while running tests.

2. Build the test project:
   ```powershell
   cd Radoub.UITests
   dotnet build
   ```

---

## Running Tests

### From Command Line

Run all tests:
```powershell
dotnet test Radoub.UITests
```

Run only smoke tests:
```powershell
dotnet test Radoub.UITests --filter "Category=Smoke"
```

Run Parley tests only:
```powershell
dotnet test Radoub.UITests --filter "FullyQualifiedName~Parley"
```

### From Visual Studio

1. Open the solution in Visual Studio
2. Open Test Explorer (Test → Test Explorer)
3. Run tests from there

---

## Project Structure

```
Radoub.UITests/
├── Radoub.UITests.csproj    # Test project file
├── README.md                 # This file
├── Shared/                   # Common utilities
│   ├── AppiumTestBase.cs     # Base class with session management
│   └── TestPaths.cs          # Path resolution utilities
└── Parley/                   # Parley-specific tests
    ├── ParleyTestBase.cs     # Parley test base class
    └── SmokeTests.cs         # Basic launch tests
```

---

## Writing New Tests

### Basic Pattern

1. Create a test class that inherits from the appropriate base:
   ```csharp
   public class MyTests : ParleyTestBase
   {
       [Fact]
       public void MyTest()
       {
           StartApplication();

           // Find elements and interact
           var button = Driver!.FindElement(By.Name("ButtonName"));
           button.Click();

           // Assert results
           Assert.True(/* condition */);
       }
   }
   ```

2. Use `[Trait("Category", "...")]` to categorize tests:
   - `Smoke` - Basic launch/existence tests
   - `UI` - UI interaction tests
   - `Integration` - Full workflow tests

### Finding Elements

WinAppDriver uses Windows Automation IDs. Common locator strategies:

```csharp
// By automation ID (preferred)
Driver.FindElement(By.Id("SaveButton"));

// By name (visible text)
Driver.FindElement(By.Name("File"));

// By XPath (flexible but slower)
Driver.FindElement(By.XPath("//Button[@Name='Save']"));
```

### Tips

- Set AutomationId in XAML for reliable element location
- Use implicit waits for elements that load asynchronously
- Clean up with `StopApplication()` or let `Dispose()` handle it

---

## Troubleshooting

### "WinAppDriver is not running"

Start WinAppDriver as Administrator:
```powershell
& "C:\Program Files\Windows Application Driver\WinAppDriver.exe"
```

### "Application not found"

Build the application first:
```powershell
dotnet build Parley/Parley
```

### "Element not found"

- Check the element name/ID matches exactly
- Increase `ImplicitWaitSeconds` in the test base
- Use Inspect.exe (Windows SDK) to find correct element names

### "Developer Mode not enabled"

Enable in Windows Settings:
Settings → Privacy & Security → For developers → Developer Mode: ON

---

## Future Improvements

- [ ] CI/CD integration (GitHub Actions with self-hosted runner)
- [ ] Screenshot capture on test failure
- [ ] Performance benchmarking tests
- [ ] Cross-tool test utilities

---

## Resources

- [WinAppDriver GitHub](https://github.com/microsoft/WinAppDriver)
- [Appium Documentation](https://appium.io/docs/en/latest/)
- [Windows UI Automation](https://docs.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32)
