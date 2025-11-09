# Parley Plugin System - Python Library

Python library for developing Parley plugins.

## Installation

```bash
pip install parley-plugin
```

## Quick Start

### Minimal Plugin

```python
from parley_plugin import Plugin, run_plugin

class MyPlugin(Plugin):
    async def on_initialize(self):
        self.logger.info("Plugin initialized!")
        return True

    async def on_shutdown(self, reason):
        self.logger.info(f"Shutting down: {reason}")

if __name__ == "__main__":
    run_plugin(MyPlugin)
```

### Using Services

```python
from parley_plugin import Plugin, run_plugin, requires_permission

class AudioPlugin(Plugin):
    async def on_initialize(self):
        await self.play_welcome_sound()
        return True

    @requires_permission("audio.play")
    async def play_welcome_sound(self):
        try:
            await self.audio.play("/path/to/welcome.mp3")
            self.logger.info("Played welcome sound")
        except PermissionError as e:
            self.logger.error(f"Permission denied: {e}")

    async def on_shutdown(self, reason):
        await self.audio.stop()

if __name__ == "__main__":
    run_plugin(AudioPlugin)
```

## Available Services

### Audio Service

```python
# Play audio
await self.audio.play("/path/to/file.mp3")

# Stop audio
await self.audio.stop()
```

**Required Permission**: `audio.play`

### UI Service

```python
# Show notification
await self.ui.show_notification("Hello!", "Greeting")

# Show dialog with buttons
button_index = await self.ui.show_dialog(
    "Are you sure?",
    "Confirmation",
    buttons=["Yes", "No"]
)
```

**Required Permissions**: `ui.show_notification`, `ui.show_dialog`

### Dialog Service

```python
# Get current dialog
dialog_id, dialog_name = await self.dialog.get_current_dialog()

# Get selected node
node_id, node_text = await self.dialog.get_selected_node()
```

**Required Permission**: `dialog.read`

### File Service

All file operations are sandboxed to `~/Parley/PluginData/` directory.

```python
# Read file (relative to sandbox)
content = await self.file.read_file("data.txt")

# Write file
await self.file.write_file("output.txt", b"Hello World")

# File dialogs
file_path = await self.file.open_file_dialog("Select File", "*.txt")
if file_path:
    save_path = await self.file.save_file_dialog("Save As", "*.txt")
```

**Required Permissions**: `file.read`, `file.write`, `file.dialog`

## Event Handlers

### Dialog Changed Event

```python
async def on_dialog_changed(self, dialog_id: str, change_type: str):
    self.logger.info(f"Dialog {dialog_id} changed: {change_type}")
```

### Node Selected Event

```python
async def on_node_selected(self, node_id: str):
    self.logger.info(f"Node {node_id} selected")
```

## Plugin Manifest

Create a `plugin.json` file alongside your plugin:

```json
{
  "manifest_version": "1.0",
  "plugin": {
    "id": "com.example.myplugin",
    "name": "My Plugin",
    "version": "1.0.0",
    "author": "Your Name",
    "description": "A simple example plugin",
    "parley_version": ">=0.1.5"
  },
  "permissions": [
    "audio.play",
    "ui.show_notification"
  ],
  "entry_point": "my_plugin.py",
  "trust_level": "unverified"
}
```

## Permission System

Plugins must declare required permissions in their manifest. Available permissions:

- **Audio**: `audio.play`, `audio.*`
- **UI**: `ui.show_notification`, `ui.show_dialog`, `ui.*`
- **Dialog**: `dialog.read`, `dialog.write`, `dialog.*`
- **File**: `file.read`, `file.write`, `file.dialog`, `file.*`

Wildcard (`*`) grants all permissions in that category.

## Development

### Running Tests

```bash
pip install -e ".[dev]"
pytest
```

### Code Formatting

```bash
black parley_plugin/
```

## License

MIT License - see LICENSE file for details
