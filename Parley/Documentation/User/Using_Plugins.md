# Using Plugins in Parley

User guide for installing and managing Parley plugins.

## Table of Contents

- [What Are Plugins?](#what-are-plugins)
- [Installing Plugins](#installing-plugins)
- [Managing Plugins](#managing-plugins)
- [Troubleshooting](#troubleshooting)
- [Safety and Security](#safety-and-security)

[Back to TOC](#table-of-contents)

## What Are Plugins?

Plugins extend Parley's functionality without modifying the core application. They can add features like:

- Spell checking and grammar tools
- Dialog analysis and statistics
- Integration with external tools
- Custom audio playback
- Automated formatting

**How plugins work:**
1. Install the plugin to `~/Parley/Plugins/Community/`
2. Enable the plugin in Settings → Plugins tab
3. Launch Parley - plugins start automatically
4. Use the features the plugin provides (notifications, dialogs, etc.)

**Safety features:**
- Plugins run as separate programs (isolated from Parley)
- If a plugin crashes, Parley should continue running normally
- Plugins that crash 3 times are automatically disabled

[Back to TOC](#table-of-contents)

## Installing Plugins

### Plugin Location

Plugins are installed to your Parley data folder:

| Platform | Path |
|----------|------|
| Windows | `%USERPROFILE%\Parley\` |
| Linux | `~/Parley/` |
| macOS | `~/Parley/` |

After installation, the structure looks like:
```
~/Parley/
├── Plugins/
│   └── plugin-name/
│       ├── plugin.json
│       ├── plugin.py
│       └── ...
├── Python/
│   └── parley_plugin/    # Client library (included in plugin ZIPs)
│       └── ...
└── ...
```

### Installing a Plugin

1. **Download the plugin ZIP** to your computer
   - Example: `flowchart-view-0.1.0.zip`

2. **Extract to your Parley folder**
   - Windows: Extract to `%USERPROFILE%\Parley\`
   - Linux/macOS: Extract to `~/Parley/`
   - The ZIP contains the correct folder structure - just extract directly

3. **Restart Parley** to discover the new plugin

4. **Enable the plugin** in Settings
   - Open Settings → Plugins tab
   - Find your plugin in the list
   - Toggle it ON

5. **Restart Parley again** to start the plugin

**Note:** Two restarts are required - once to discover, once to start after enabling.

### Verifying Installation

After restarting Parley with the plugin enabled:

1. Check the plugin appears in Settings → Plugins tab
2. Open the plugin log: `~/Parley/Logs/Session_*/Plugin_*.log`
3. Look for messages from your plugin

[Back to TOC](#table-of-contents)

## Managing Plugins

### Opening Plugin Settings

1. Open Parley
2. Click **Settings** (or press appropriate hotkey)
3. Navigate to the **Plugins** tab

### Enabling/Disabling Plugins

**To enable a plugin:**
1. Find the plugin in the list
2. Toggle the switch to ON
3. Restart Parley

**To disable a plugin:**
1. Find the plugin in the list
2. Toggle the switch to OFF
3. Restart Parley

**Note:** Changes require restarting Parley to take effect.

### Disabling All Plugins

If Parley behaves unexpectedly after installing a plugin:

**Option 1 - Using Settings:**
1. Open Settings → Plugins tab
2. Uncheck the problematic plugin
3. Restart Parley
4. Enable plugins one at a time to identify if there is a conflict between them

**Option 2 - Manual (if Parley won't start):**
1. Close Parley
2. Delete `~/Parley/PluginSettings.json`
3. Restart Parley - all plugins will be disabled

### Viewing Plugin Information

In the Settings → Plugins tab, each plugin shows:

- **Name and version** - e.g., "Hello World v1.0.0"
- **Author** - Who created the plugin
- **Description** - What the plugin does
- **Permissions** - What the plugin can access (when implemented)

### Checking Plugin Logs

To see what plugins are doing:

1. Navigate to `~/Parley/Logs/`
2. Open the most recent `Session_*` folder
3. Open `Plugin_*.log`

The log shows:
- When plugins start and stop
- Plugin output and messages
- Any errors or crashes
- Which plugins are enabled/disabled

### Uninstalling Plugins

1. Disable the plugin in Settings
2. Delete the plugin folder from `~/Parley/Plugins/`
3. Restart Parley

[Back to TOC](#table-of-contents)

## Troubleshooting

### Plugin Not Appearing in List

**Check folder structure:**
- Plugin must be in `~/Parley/Plugins/plugin-name/`
- Must contain `plugin.json` file
- Check Settings → Plugins tab for any error messages

**Click "Refresh Plugin List":**
- Sometimes Parley needs a manual refresh
- After refreshing, restart Parley

### Plugin Crashes on Startup

**Check requirements:**
- Plugin may require Python 3.10 or newer
- Plugin may need additional Python packages
- Check plugin's README for installation instructions

**Auto-disable protection:**
- If a plugin crashes 3 times, Parley automatically disables it
- Re-enable in Settings only after fixing the issue

### Parley Won't Start After Installing Plugin

1. Delete `~/Parley/PluginSettings.json`
   - This resets plugin settings and disables all plugins
2. Restart Parley
3. Re-enable plugins one at a time to identify the problem

**Alternative:**
- Delete or move the problem plugin from `~/Parley/Plugins/`

### Plugin Not Doing Anything

**Check the log:**
1. Open `~/Parley/Logs/Session_*/Plugin_*.log`
2. Look for messages from your plugin
3. Errors will show up here

**Check it's enabled:**
- Settings → Plugins tab
- Plugin toggle should be ON
- Requires restart to take effect

[Back to TOC](#table-of-contents)

## Safety and Security

**Alpha Status:**
During alpha development, plugin security features are limited. Trust levels and permission systems will be implemented in future releases. At this time, only use plugins that are available from [Radoub / Parley github](https://github.com/LordOfMyatar/Radoub).

### What Plugins Can Access

**Current Capabilities:**
- Plugins run as separate Python processes
- Can send notifications and create UI panels via gRPC
- Can query dialog data (when loaded)
- All output logged to `~/Parley/Logs/Session_*/Plugin_*.log`
- Plugins run in isolation (crash won't affect Parley)

**Future Features (requires code signing):**
- Permission system enforcement
- Sandboxed file operations
- Trust level verification

### Reporting Problems

**Support Scope:**
- We only provide support for plugins created by the Parley team
- Community plugins are not officially supported
- Plugin developers should provide their own support channels

**What to Report:**
- **Malicious behavior** from any parley plugin (we want to know!)
- **Parley crashes or bugs** caused by the plugin system itself
- **Security vulnerabilities** in the plugin infrastructure

**What NOT to Report:**
- Issues with community plugins (contact plugin author)
- Feature requests for specific plugins (contact plugin author)
- Plugin compatibility problems (contact plugin author)

Report plugin system issues to: [Parley GitHub Issues](https://github.com/LordOfMyatar/Radoub/issues)

[Back to TOC](#table-of-contents)

---

**Need more help?**
- [Plugin Development Guide](Plugin_Development_Guide.md) - For creating plugins
- [Plugin Manifest Specification](Plugin_Manifest_Specification.md) - Technical details
- [Parley Documentation](../README.md) - Main documentation
