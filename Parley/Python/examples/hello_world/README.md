# Hello World Plugin

Simple example plugin that demonstrates Parley's plugin system.

## What it does

- Shows a welcome notification when activated
- Shows a status update after 2 seconds
- Shows a goodbye notification when deactivated

## Installation

1. Copy this folder to:
   ```
   ~/Parley/Plugins/Official/hello_world/
   ```

2. In Parley: Settings → Plugins → Refresh Plugin List

3. Enable "Hello World" plugin

4. Restart Parley

## Expected Behavior

When Parley starts with this plugin enabled, you'll see:
1. Notification: "Hello World! 👋 - The Hello World plugin is now active and ready to use!"
2. After 2 seconds: "Plugin Status - Everything is working perfectly!"
3. When Parley closes: "Goodbye! 👋 - Hello World plugin is shutting down."

## Use Case

This plugin is for:
- Testing that the plugin system works
- Demonstrating basic plugin structure
- Verifying UI notifications work correctly
- Example for new plugin developers
