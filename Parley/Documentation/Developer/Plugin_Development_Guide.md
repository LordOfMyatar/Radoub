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

Parley supports Python plugins that can extend the dialog editor's functionality. Plugins run in isolated processes for security and stability, communicating with Parley via gRPC.

**What plugins can do:**
- Run as separate Python processes with crash isolation
- Communicate with Parley via gRPC (bidirectional)
- Show UI notifications and dialogs
- Create custom panels with HTML/JavaScript content
- Read and analyze dialog data in real-time
- Subscribe to dialog change events
- Read/write files in sandboxed storage
- Query theme and speaker color preferences

**Security Model:**
- Permission-based manifest system (controls access to Parley APIs)
- Crash recovery with auto-disable after 3 crashes
- Rate limiting (1000 calls/minute per operation)
- File API sandboxed to `~/Parley/PluginData/{pluginId}/`

**Important Security Note:**
Plugins run as full Python processes with user-level permissions. The security model restricts what plugins can do *through Parley's gRPC APIs*, but does not sandbox Python itself. A malicious plugin could use native Python to access files, network, etc. **Only install plugins from trusted sources.**

[Back to TOC](#table-of-contents)

## Getting Started

### Prerequisites

- Python 3.10 or newer (3.12+ recommended)
- Parley 0.1.33-alpha or newer
- Python packages: `grpcio` and `protobuf`
  ```bash
  pip install grpcio protobuf
  ```

### SDK Location

The Python plugin SDK (`parley_plugin`) is bundled with Parley in the install directory:

```
<Parley Install Folder>/Python/parley_plugin/
```

Parley automatically adds the SDK to Python's path when launching plugins. You don't need to install it separately.

### Creating Your First Plugin

1. Create a plugin directory:
   ```
   ~/Parley/Plugins/Community/my-first-plugin/
   ```

2. Add required files:
   - `plugin.json` - Manifest
   - `main.py` - Entry point
   - `requirements.txt` - Python dependencies (optional)

3. **For development**: Use the deploy script to sync plugins and SDK:
   ```powershell
   # From Parley/Scripts directory
   .\deploy-plugins.ps1
   ```
   This script:
   - Regenerates Python gRPC stubs from `plugin.proto`
   - Copies Official plugins to `bin/Debug` and `bin/Release` output directories
   - Copies Python SDK to build output directories
   - Creates `~/Parley/Plugins/Community/` folder for your plugins

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
    "parleyVersion": ">=0.1.33"
  },
  "permissions": ["ui.show_notification"],
  "entry_point": "main.py",
  "trust_level": "unverified"
}
```

**main.py:**
```python
"""
Simple plugin with gRPC communication
"""
from parley_plugin import ParleyClient

print("My Plugin starting...")

try:
    # Connect to Parley's gRPC server
    with ParleyClient() as client:
        print("[OK] Connected to Parley")

        # Show notification
        success = client.show_notification(
            "My Plugin",
            "Plugin is running!"
        )
        if success:
            print("[OK] Notification sent")

        # Query current dialog
        dialog_id, dialog_name = client.get_current_dialog()
        if dialog_id:
            print(f"Current dialog: {dialog_name}")
        else:
            print("No dialog loaded")

except Exception as e:
    print(f"[ERROR] {e}")
    import traceback
    traceback.print_exc()

print("My Plugin exiting...")
```

**What happens:**
- Parley launches your plugin when enabled
- `ParleyClient` connects via gRPC (port from `PARLEY_GRPC_PORT` env var)
- All `print()` output goes to Parley's Debug tab
- If plugin crashes 3 times, it auto-disables

### Panel-Based Plugin Example

For plugins that need a persistent UI (like Flowchart View):

**plugin.json:**
```json
{
  "manifestVersion": "1.0",
  "plugin": {
    "id": "com.example.mypanel",
    "name": "My Panel Plugin",
    "version": "1.0.0",
    "author": "Your Name",
    "description": "Plugin with a custom panel",
    "parleyVersion": ">=0.1.33"
  },
  "permissions": [
    "ui.create_panel",
    "ui.show_notification",
    "dialog.read",
    "dialog.subscribe_changes"
  ],
  "entry_point": "main.py",
  "trust_level": "unverified"
}
```

**main.py:**
```python
"""
Panel plugin - creates a floating window with HTML content
"""
from parley_plugin import ParleyClient
import time

print("Panel Plugin starting...")

try:
    with ParleyClient() as client:
        # Register a panel (opens as a separate window)
        success = client.register_panel(
            panel_id="my-panel",
            title="My Custom Panel",
            width=600,
            height=400,
            html_content="<html><body><h1>Hello from my plugin!</h1></body></html>"
        )

        if success:
            print("[OK] Panel registered")

            # Keep plugin running to handle events
            while True:
                # Check if dialog changed
                dialog_id, dialog_name = client.get_current_dialog()
                if dialog_id:
                    # Update panel content
                    new_html = f"<html><body><h1>Dialog: {dialog_name}</h1></body></html>"
                    client.update_panel_content("my-panel", new_html)

                time.sleep(1)

except KeyboardInterrupt:
    print("Plugin stopped by user")
except Exception as e:
    print(f"[ERROR] {e}")

print("Panel Plugin exiting...")
```

**Key Panel APIs:**
- `register_panel(panel_id, title, width, height, html_content)` - Create a panel window
- `update_panel_content(panel_id, html_content)` - Update panel HTML
- `get_current_dialog()` - Get loaded dialog info
- `get_dialog_structure()` - Get full dialog tree structure
- `get_speaker_colors()` - Get configured speaker colors for consistency

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

**Test crash handling:**
```python
# Force a crash to test recovery
raise Exception("Test crash")
```

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

- **Issues:** https://github.com/LordOfMyatar/Radoub/issues
- **Discussions:** https://github.com/LordOfMyatar/Radoub/discussions
- **Reference Implementation:** See `Plugins/Official/flowchart-view/` for a complete example

---

Last updated for Parley v0.1.43-alpha
