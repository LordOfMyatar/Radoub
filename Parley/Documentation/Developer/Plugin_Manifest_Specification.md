# Plugin Manifest Specification

Version: 1.0
Last Updated: 2025-11-08

## Overview

Every Parley plugin must include a `plugin.json` manifest file in its root directory. The manifest declares plugin metadata, required permissions, and version compatibility.

## Manifest Schema

### Complete Example

```json
{
  "manifest_version": "1.0",
  "plugin": {
    "id": "org.parley.flowchart",
    "name": "Flowchart View",
    "version": "1.0.0",
    "author": "Parley Team",
    "description": "Visual flowchart view for dialogs",
    "parley_version": ">=0.1.5"
  },
  "permissions": [
    "ui.create_panel",
    "ui.show_dialog",
    "dialog.read",
    "dialog.subscribe_changes"
  ],
  "entry_point": "flowchart_plugin.py",
  "trust_level": "official"
}
```

## Field Descriptions

### manifest_version (required)

**Type**: String
**Value**: "1.0"

The manifest schema version. Currently only "1.0" is supported.

### plugin (required)

**Type**: Object

Plugin metadata and information.

#### plugin.id (required)

**Type**: String
**Format**: Reverse domain notation recommended
**Example**: `org.parley.flowchart`

Unique identifier for the plugin. Must be unique across all plugins.

#### plugin.name (required)

**Type**: String
**Example**: `Flowchart View`

Human-readable plugin name displayed in UI.

#### plugin.version (required)

**Type**: String
**Format**: Semantic versioning (MAJOR.MINOR.PATCH)
**Example**: `1.0.0`

Plugin version following semantic versioning.

#### plugin.author (required)

**Type**: String
**Example**: `Parley Team`

Plugin author or organization name.

#### plugin.description (optional)

**Type**: String
**Example**: `Visual flowchart view for dialogs`

Brief description of plugin functionality.

#### plugin.parley_version (optional)

**Type**: String
**Format**: Version requirement expression
**Examples**:
- `>=0.1.5` - Requires Parley 0.1.5 or higher
- `^1.0.0` - Compatible with 1.x.x (not 2.0.0)
- `~1.2.3` - Compatible with 1.2.x (not 1.3.0)
- `1.0.0` - Exact version match

Minimum or compatible Parley version required.

**Omit this field** to indicate compatibility with all Parley versions.

### permissions (required)

**Type**: Array of strings

List of permissions the plugin requires. Parley enforces these at API boundaries.

#### Permission Categories

**Audio Permissions**:
- `audio.play` - Play audio files
- `audio.record` - Record audio
- `audio.waveform` - Generate waveform visualizations
- `audio.*` - All audio permissions (wildcard)

**UI Permissions**:
- `ui.create_panel` - Create custom UI panels
- `ui.show_dialog` - Show modal dialogs
- `ui.show_notification` - Display notifications
- `ui.*` - All UI permissions (wildcard)

**Dialog Permissions**:
- `dialog.read` - Read dialog data
- `dialog.write` - Modify dialog data
- `dialog.subscribe_changes` - Receive change notifications
- `dialog.*` - All dialog permissions (wildcard)

**File Permissions**:
- `file.read` - Read files (sandboxed)
- `file.write` - Write files (sandboxed)
- `file.dialog` - Show file open/save dialogs
- `file.*` - All file permissions (wildcard)

### entry_point (required)

**Type**: String
**Example**: `flowchart_plugin.py`

Path to the Python script that serves as the plugin entry point. Relative to the plugin directory.

### trust_level (optional)

**Type**: String
**Values**: `official`, `verified`, `unverified`
**Default**: `unverified`

Plugin trust level:
- **official**: Shipped with Parley, full trust
- **verified**: Signed by recognized developer
- **unverified**: User-installed, requires permission prompts

## Validation Rules

### Required Fields

All fields marked "required" must be present and non-empty.

### Version Format

- Plugin version must follow semantic versioning: `MAJOR.MINOR.PATCH`
- Parley version requirement must use valid operators: `>=`, `^`, `~`, or exact match

### Permission Format

- Permissions must start with a valid category: `audio.`, `ui.`, `dialog.`, or `file.`
- Invalid permissions will cause manifest validation to fail

### ID Uniqueness

Plugin IDs must be unique. Reverse domain notation is recommended but not enforced.

## Version Compatibility

### Semantic Version Operators

**>= (Greater than or equal)**:
```json
"parley_version": ">=0.1.5"
```
Allows Parley 0.1.5, 0.1.6, 0.2.0, 1.0.0, etc.

**^ (Caret - Compatible with)**:
```json
"parley_version": "^1.0.0"
```
Allows 1.0.0, 1.0.1, 1.9.9, but NOT 2.0.0.
Major version must match, minor/patch can be higher.

**~ (Tilde - Approximately equivalent)**:
```json
"parley_version": "~1.2.3"
```
Allows 1.2.3, 1.2.4, 1.2.9, but NOT 1.3.0.
Major and minor must match, patch can be higher.

**Exact Match**:
```json
"parley_version": "1.0.0"
```
Only allows exactly 1.0.0.

## Loading Process

1. Parley scans plugin directories for `plugin.json` files
2. Each manifest is loaded and validated
3. Version compatibility is checked
4. Valid plugins are added to the available plugins list
5. Invalid manifests are logged with specific error messages

## Error Messages

### Missing Required Field

```
plugin.id is required
```

### Invalid Version

```
Invalid plugin.version: abc
```

### Unsupported Manifest Version

```
Unsupported manifest_version: 2.0
```

### Invalid Permission

```
Invalid permission: invalid.permission
```

### Version Incompatibility

Plugin won't load if Parley version doesn't match the requirement.

## Best Practices

1. **Use semantic versioning**: Follow MAJOR.MINOR.PATCH strictly
2. **Declare minimal permissions**: Only request permissions you actually use
3. **Specify version requirements**: Use `>=` for minimum version requirements
4. **Use reverse domain IDs**: `com.yourcompany.pluginname`
5. **Write clear descriptions**: Help users understand what your plugin does
6. **Test version compatibility**: Verify your plugin works with the specified Parley versions

## Related Documentation

- [Plugin Development Guide](Plugin_Development_Guide.md) (coming soon)
- [Plugin API Reference](Plugin_API_Reference.md) (coming soon)
- [Security Model](Plugin_Security_Model.md) (coming soon)
