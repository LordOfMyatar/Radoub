# Parley Build Scripts

Clean rebuild scripts for Parley development.

## Scripts

### `rebuild-debug.ps1`
Clean rebuild and launch Parley in **Debug** mode.

**Usage**:
```powershell
.\rebuild-debug.ps1
```

**What it does**:
1. Stops all Parley.exe and project-related dotnet processes
2. Cleans bin/obj directories for all projects
3. Clears NuGet package cache
4. Restores NuGet packages
5. Builds solution in Debug configuration
6. Launches Parley.exe from Debug build

**Use when**: Debugging, testing features, investigating issues

---

### `rebuild-release.ps1`
Clean rebuild and launch Parley in **Release** mode.

**Usage**:
```powershell
.\rebuild-release.ps1
```

**What it does**:
1. Stops all Parley.exe and project-related dotnet processes
2. Cleans bin/obj directories for all projects
3. Clears NuGet package cache
4. Restores NuGet packages
5. Builds solution in Release configuration
6. Launches Parley.exe from Release build

**Use when**: Performance testing, user acceptance testing, pre-release validation

---

## When to Use Clean Rebuild

Use these scripts when:
- Build artifacts are stale or corrupted
- Switching between Debug and Release modes
- Testing performance (always use Release mode)
- NuGet cache issues
- "File locked" errors during build
- After major code changes (like lazy loading fix)
- Before creating PRs or releases

## Notes

- Scripts automatically stop running instances to prevent file locking
- Warnings are displayed but don't stop the build
- Scripts exit with error code 1 on build failure
- Parley launches automatically after successful build
