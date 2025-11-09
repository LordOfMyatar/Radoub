# Parley Plugin Development Guide

Guide for creating plugins that extend Parley's functionality with Python.

## Table of Contents

- [Overview](#overview)
- [Getting Started](#getting-started)
- [Plugin Structure](#plugin-structure)
- [Manifest Format](#manifest-format)
- [Available APIs](#available-apis)
- [Security](#security)
- [Best Practices](#best-practices)
- [Testing](#testing)
- [Distribution](#distribution)

[Back to TOC](#table-of-contents)

## Overview

Parley supports Python plugins that can extend the dialog editor's functionality. Plugins run in isolated processes and communicate with Parley via gRPC over named pipes.

**What plugins can do:**
- Play audio files
- Show UI notifications and dialogs
- Read and analyze dialog data
- Read files from sandboxed storage

**What plugins cannot do:**
- Access arbitrary files on the system
- Make network requests (unless granted permission in future)
- Modify Parley's core functionality
- Access other plugins' data

[Back to TOC](#table-of-contents)

## Getting Started

### Prerequisites

- Python 3.10 or newer (3.12+ recommended)
- Parley 0.1.5-alpha or newer

### Creating Your First Plugin

1. Create a plugin directory:
   ```
   ~/Parley/Plugins/Community/my-first-plugin/
   ```

2. Add required files:
   - `plugin.json` - Manifest
   - `main.py` - Entry point
   - `requirements.txt` - Python dependencies (optional)

3. Install Parley's Python library:
   ```bash
   # From Parley installation directory
   cd Parley/Python
   pip install -e .
   ```

   Or download the Python library from the Parley repository.

[Back to TOC](#table-of-contents)

## Plugin Structure

```
my-plugin/
├── plugin.json          # Required: Plugin manifest
├── main.py              # Required: Entry point
├── requirements.txt     # Optional: Python dependencies
├── README.md            # Recommended: Plugin description
└── assets/              # Optional: Plugin resources
    ├── sounds/
    └── images/
```

### Minimal Example

**plugin.json:**
```json
{
  "manifestVersion": "1.0",
  "plugin": {
    "id": "com.example.myplugin",
    "name": "My First Plugin",
    "version": "1.0.0",
    "author": "Your Name",
    "description": "Does something useful",
    "parleyVersion": ">=0.1.5"
  },
  "permissions": [
    "ui.show_notification"
  ],
  "entry_point": "main.py",
  "trust_level": "unverified"
}
```

**main.py:**
```python
from parley_plugin import ParleyPlugin

plugin = ParleyPlugin()

@plugin.on_activate()
async def on_activate():
    await plugin.ui.show_notification(
        "My First Plugin",
        "Plugin activated successfully!"
    )
    print("Plugin is running")

if __name__ == "__main__":
    plugin.run()
```

[Back to TOC](#table-of-contents)

## Manifest Format

The `plugin.json` file describes your plugin:

```json
{
  "manifestVersion": "1.0",
  "plugin": {
    "id": "reverse.domain.plugin-name",
    "name": "Display Name",
    "version": "1.0.0",
    "author": "Your Name",
    "description": "What this plugin does",
    "parleyVersion": ">=0.1.5"
  },
  "permissions": [
    "audio.play",
    "ui.show_notification",
    "dialog.read",
    "file.read"
  ],
  "entry_point": "main.py",
  "trust_level": "unverified"
}
```

### Manifest Fields

| Field | Required | Description |
|-------|----------|-------------|
| `manifestVersion` | Yes | Always "1.0" |
| `plugin.id` | Yes | Unique identifier (reverse domain notation) |
| `plugin.name` | Yes | Display name |
| `plugin.version` | Yes | Semantic version (e.g., "1.2.3") |
| `plugin.author` | Yes | Author name |
| `plugin.description` | Yes | Brief description |
| `plugin.parleyVersion` | Yes | Compatible Parley versions (e.g., ">=0.1.5", "^0.1.0") |
| `permissions` | No | Required permissions (see [Security](#security)) |
| `entry_point` | Yes | Python file to execute (e.g., "main.py") |
| `trust_level` | Yes | "unverified", "verified", or "official" |

### Version Matching

`parleyVersion` supports semantic versioning operators:

- `>=0.1.5` - Version 0.1.5 or higher
- `^0.1.0` - Compatible with 0.1.x (minor versions)
- `~0.1.5` - Compatible with 0.1.5+ (patch versions only)

[Back to TOC](#table-of-contents)

## Available APIs

### Audio API

Play audio files for previewing dialogue.

**Permission Required:** `audio.play` or `audio.*`

```python
# Play a sound file
await plugin.audio.play("path/to/sound.wav")

# Stop playback
await plugin.audio.stop()
```

**Supported Formats:** WAV, MP3, OGG

### UI API

Show notifications and dialogs.

**Permissions:**
- `ui.show_notification` - Show toast notifications
- `ui.show_dialog` - Show modal dialogs
- `ui.*` - All UI permissions

```python
# Show a notification
await plugin.ui.show_notification(
    title="Task Complete",
    message="Processing finished successfully"
)

# Show a dialog (returns user's choice)
choice = await plugin.ui.show_dialog(
    title="Confirm Action",
    message="Are you sure you want to continue?",
    buttons=["Yes", "No"]
)

if choice == "Yes":
    print("User confirmed")
```

### Dialog API

Read and analyze dialog data.

**Permission Required:** `dialog.read` or `dialog.*`

```python
# Get current dialog
dialog = await plugin.dialog.get_current()

print(f"Dialog has {len(dialog.entries)} entries")

# Get specific entry
entry = await plugin.dialog.get_entry(entry_id=42)
print(f"Entry text: {entry.text}")

# Search dialog
results = await plugin.dialog.search(query="Merchant")
for result in results:
    print(f"Found: {result.text}")
```

### File API

Read files from plugin's sandboxed storage.

**Permission Required:** `file.read` or `file.*`

```python
# Read a file from plugin data directory
content = await plugin.file.read("data/config.json")

# Files are sandboxed to:
# ~/Parley/PluginData/<plugin-id>/

# Attempting to read outside sandbox throws PermissionDeniedException
```

**Sandbox Rules:**
- All file paths are relative to `~/Parley/PluginData/<plugin-id>/`
- Path traversal (`../`) is blocked
- Absolute paths are rejected

[Back to TOC](#table-of-contents)

## Security

### Permission System

Plugins must declare required permissions in their manifest. Users see these permissions before enabling a plugin.

**Permission Categories:**

| Permission | Description |
|------------|-------------|
| `audio.play` | Play audio files |
| `audio.stop` | Stop audio playback |
| `audio.*` | All audio permissions |
| `ui.show_notification` | Show toast notifications |
| `ui.show_dialog` | Show modal dialogs |
| `ui.*` | All UI permissions |
| `dialog.read` | Read dialog data |
| `dialog.*` | All dialog permissions |
| `file.read` | Read files from sandbox |
| `file.write` | Write files to sandbox (future) |
| `file.*` | All file permissions |

**Wildcards:** Use `category.*` to request all permissions in a category.

**Case Insensitive:** Permissions are matched case-insensitively (`Audio.Play` = `audio.play`).

### Rate Limiting

Plugins are rate-limited to prevent abuse:
- **Limit:** 1000 API calls per minute per operation
- **Scope:** Per plugin, per operation type
- **Enforcement:** Exceeding the limit throws `RateLimitExceededException`

### Trust Levels

| Level | Description |
|-------|-------------|
| `official` | Shipped with Parley, fully trusted |
| `verified` | Community plugin reviewed by maintainers |
| `unverified` | Third-party plugin, user discretion advised |

**Note:** Users see visual indicators for trust levels in plugin settings.

### What Parley Won't Load

Parley will reject plugins that:
- Missing required manifest fields
- Invalid `plugin.id` format (must be reverse domain notation)
- Invalid semantic version in `plugin.version`
- Incompatible `parleyVersion`
- Missing entry point file
- Duplicate `plugin.id` with another installed plugin

### Security Audit Log

All security events are logged:
- Permission denials
- Rate limit violations
- Sandbox violations (path traversal attempts)
- Plugin crashes
- Timeouts

Users can review the audit log for suspicious activity.

[Back to TOC](#table-of-contents)

## Best Practices

### Error Handling

```python
from parley_plugin import ParleyPlugin, PermissionDeniedException, RateLimitExceededException

plugin = ParleyPlugin()

@plugin.on_activate()
async def on_activate():
    try:
        await plugin.audio.play("sound.wav")
    except PermissionDeniedException:
        await plugin.ui.show_notification(
            "Error",
            "Missing audio.play permission"
        )
    except RateLimitExceededException:
        await plugin.ui.show_notification(
            "Error",
            "Too many requests, please slow down"
        )
    except Exception as e:
        print(f"Unexpected error: {e}")
```

### Logging

```python
import logging

# Use Python's logging module
logging.info("Plugin activated")
logging.warning("Unusual condition detected")
logging.error("Operation failed")

# Logs appear in Parley's plugin log
```

### Resource Cleanup

```python
@plugin.on_deactivate()
async def on_deactivate():
    # Clean up resources when plugin is unloaded
    await plugin.audio.stop()
    print("Plugin shutting down")
```

### Minimize API Calls

```python
# Bad: Makes 100 API calls
for i in range(100):
    await plugin.ui.show_notification("Update", f"Step {i}")

# Good: Batch updates or use single notification
await plugin.ui.show_notification("Complete", "100 steps processed")
```

### Respect User Settings

Check if your plugin is enabled before performing actions:

```python
# Parley handles enable/disable at startup
# Your plugin only runs if enabled
```

[Back to TOC](#table-of-contents)

## Testing

### Local Testing

1. Copy your plugin to:
   ```
   ~/Parley/Plugins/Community/<your-plugin>/
   ```

2. Enable plugin in Parley:
   - Settings → Plugins → Find your plugin → Enable
   - Restart Parley

3. Check logs:
   - View → Show Logs
   - Look for plugin-related messages

### Crash Recovery

Parley detects plugin crashes and offers recovery:
- After 3 crashes, plugin is auto-disabled
- Crash recovery dialog appears on next startup
- Users can choose Safe Mode (all plugins disabled)

**Test crash handling:**
```python
# Force a crash to test recovery
raise Exception("Test crash")
```

### Safe Mode

Test your plugin's behavior when disabled:
- Settings → Plugins → Enable "Disable all plugins on next launch (Safe Mode)"
- Restart Parley
- Verify your plugin doesn't cause issues

[Back to TOC](#table-of-contents)

## Distribution

### Publishing

1. **Create README.md** with:
   - Plugin description
   - Features
   - Installation instructions
   - Usage guide
   - License

2. **Package structure:**
   ```
   your-plugin/
   ├── plugin.json
   ├── main.py
   ├── README.md
   ├── LICENSE
   └── requirements.txt
   ```

3. **Distribution options:**
   - GitHub repository
   - Parley Community Plugins directory (future)
   - Direct download ZIP

### Installation Instructions (for users)

```
1. Download plugin ZIP
2. Extract to ~/Parley/Plugins/Community/<plugin-name>/
3. In Parley: Settings → Plugins → Refresh Plugin List
4. Enable the plugin
5. Restart Parley
```

### Versioning

Follow semantic versioning:
- **MAJOR.MINOR.PATCH** (e.g., 1.2.3)
- **MAJOR:** Breaking changes
- **MINOR:** New features (backwards compatible)
- **PATCH:** Bug fixes

Update `parleyVersion` when requiring newer Parley features:
```json
"parleyVersion": ">=0.2.0"  // Requires Parley 0.2.0+
```

[Back to TOC](#table-of-contents)

## Advanced Topics

### Async/Await

All plugin APIs are asynchronous:

```python
import asyncio

async def my_function():
    # Use await for all plugin API calls
    await plugin.ui.show_notification("Hello", "World")

    # You can use other async operations
    await asyncio.sleep(1)
```

### Multiple Operations

```python
import asyncio

async def parallel_operations():
    # Run multiple operations concurrently
    await asyncio.gather(
        plugin.ui.show_notification("Task 1", "Starting"),
        plugin.audio.play("sound1.wav")
    )
```

### Lifecycle Hooks

```python
@plugin.on_activate()
async def on_activate():
    # Called when plugin is loaded
    print("Plugin activated")

@plugin.on_deactivate()
async def on_deactivate():
    # Called when plugin is unloaded
    print("Plugin deactivated")

@plugin.on_error()
async def on_error(error):
    # Called on unhandled exceptions
    print(f"Error: {error}")
```

[Back to TOC](#table-of-contents)

## Troubleshooting

### Plugin Not Appearing

- Check `plugin.json` is valid JSON
- Verify `plugin.id` is unique
- Ensure `parleyVersion` matches your Parley version
- Click "Refresh Plugin List" in Settings

### Permission Denied

- Add required permissions to `plugin.json`:
  ```json
  "permissions": ["ui.show_notification"]
  ```
- Restart Parley after changing manifest

### Rate Limit Exceeded

- Reduce API call frequency
- Batch operations when possible
- Current limit: 1000 calls/minute per operation

### Plugin Crashes

- Check Parley logs for Python exceptions
- Verify all dependencies are installed
- Test with `python main.py` outside Parley
- After 3 crashes, plugin is auto-disabled

### Safe Mode Issues

- Disable Safe Mode in Settings → Plugins
- Uncheck "Disable all plugins on next launch (Safe Mode)"
- Restart Parley

[Back to TOC](#table-of-contents)

## Examples

### Example: Dialog Analyzer

Analyzes dialog structure and shows statistics.

```python
from parley_plugin import ParleyPlugin

plugin = ParleyPlugin()

@plugin.on_activate()
async def on_activate():
    dialog = await plugin.dialog.get_current()

    entry_count = len(dialog.entries)
    avg_length = sum(len(e.text) for e in dialog.entries) / entry_count

    await plugin.ui.show_notification(
        "Dialog Statistics",
        f"Entries: {entry_count}, Avg Length: {avg_length:.1f} chars"
    )
```

### Example: Audio Preview

Plays speaker-specific audio cues.

```python
from parley_plugin import ParleyPlugin

plugin = ParleyPlugin()

SPEAKER_SOUNDS = {
    "Merchant": "sounds/merchant.wav",
    "Guard": "sounds/guard.wav",
    "Player": "sounds/player.wav"
}

@plugin.on_activate()
async def on_activate():
    dialog = await plugin.dialog.get_current()

    for entry in dialog.entries:
        speaker = entry.speaker
        if speaker in SPEAKER_SOUNDS:
            await plugin.audio.play(SPEAKER_SOUNDS[speaker])
            await plugin.ui.show_notification(
                f"Playing: {speaker}",
                entry.text[:50] + "..."
            )
```

[Back to TOC](#table-of-contents)

## Support

- **Issues:** https://github.com/anthropics/parley/issues
- **Discussions:** https://github.com/anthropics/parley/discussions
- **Documentation:** https://docs.parley.app

---

Generated with Parley v0.1.5-alpha
