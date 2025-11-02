# Pull Request

## Description
<!-- Provide a brief description of the changes in this PR -->

## Related Issues
<!-- Link to related issues: Fixes #123, Relates to #456 -->

## Type of Change
<!-- Mark relevant items with an [x] -->

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Refactoring (code organization, no functional changes)
- [ ] Documentation update
- [ ] Performance improvement

## Changes Made
<!-- List specific changes made in this PR -->

-
-
-

## Testing Checklist

### Code Quality
- [ ] Code follows project patterns and conventions
- [ ] No console applications use `Console.ReadKey()` (causes crashes with `dotnet run`)
- [ ] Error handling is explicit (no silent failures)
- [ ] Variable names are descriptive and clear
- [ ] Comments only where complex logic requires explanation

### Functional Testing
- [ ] Manual testing completed on development machine
- [ ] All existing features still work correctly
- [ ] New features work as expected

### File Format Testing (if applicable to DLG file changes)
- [ ] **Round-trip testing passed**: Load → Modify → Save → Load → Compare
- [ ] Binary comparison validation completed
- [ ] Aurora Toolset can read exported files
- [ ] Field indices calculation correct (4:1 ratio maintained)
- [ ] No conversation data loss (all nodes preserved)
- [ ] Pointer resolution successful (no broken links)

### Cross-Platform Testing (Phase 0+)
<!-- Mark N/A if not yet on Avalonia -->
- [ ] **Windows 10/11**: Full functionality verified
- [ ] **macOS**: Full functionality verified
- [ ] **Linux (Ubuntu 22.04+)**: Full functionality verified
- [ ] N/A - Not yet on Avalonia

### UI Testing (if applicable)
- [ ] TreeView operations work (expand/collapse/select)
- [ ] Copy operations functional (tree text, node text, properties)
- [ ] Properties panel displays correctly
- [ ] Theme switching works
- [ ] Recent files list updates
- [ ] All buttons and menus functional

### Performance Testing (if applicable to large files)
- [ ] Complex dialogs (100+ nodes) load < 1 second
- [ ] Tree rendering is smooth (no visible lag)
- [ ] Memory usage reasonable (< 200MB for large files)
- [ ] No memory leaks detected

### Documentation
- [ ] README.md updated (if needed)
- [ ] TECHNICAL_LEARNINGS.md updated with new discoveries
- [ ] PROJECT_STATUS.md updated with progress
- [ ] Code comments added for complex logic
- [ ] CHANGELOG.md updated (if applicable)

## Test Files Used
<!-- List specific test files used for validation -->

- [ ] Simple dialog (5-10 nodes): `[filename]`
- [ ] Medium complexity (30-50 nodes): `[filename]`
- [ ] Complex dialog (100+ nodes): `[filename]`
- [ ] Aurora vanilla game file: `[filename]`

## Screenshots (if UI changes)
<!-- Add screenshots showing before/after for UI changes -->

## Platform-Specific Notes
<!-- Document any platform-specific behavior or considerations -->

**Windows**:

**macOS**:

**Linux**:

## Breaking Changes
<!-- If this PR introduces breaking changes, describe them here and provide migration guidance -->

None / [Describe breaking changes and migration path]

## Rollback Plan
<!-- How to revert if this PR causes issues -->

## Additional Notes
<!-- Any additional context, concerns, or information reviewers should know -->

---

## Reviewer Checklist
<!-- For PR reviewers -->

- [ ] Code quality meets project standards
- [ ] Testing checklist completed appropriately
- [ ] Documentation is adequate
- [ ] No security concerns
- [ ] Performance acceptable
- [ ] Ready to merge to `develop` (or `main` if from `develop`)

## Merge Requirements
<!-- Do not merge until: -->

- [ ] All tests passing
- [ ] At least one approval
- [ ] No merge conflicts
- [ ] CI/CD pipeline successful (when implemented)
