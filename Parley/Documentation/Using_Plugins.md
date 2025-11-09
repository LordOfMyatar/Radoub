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
- Plugins run as separate programs (isolated from Parley)
- They start automatically when you launch Parley
- If a plugin crashes, Parley continues running normally
- Plugins that crash repeatedly are automatically disabled

[Back to TOC](#table-of-contents)

## Installing Plugins

### Plugin Locations

Parley looks for plugins in two places:

**Official Plugins** (bundled with Parley):
- Included with your Parley installation
- Verified and tested by the Parley team
- Marked with [OFFICIAL] badge

**Community Plugins** (installed by you):
- Located in `~/Parley/Plugins/Community/`
  - Windows: `C:\Users\YourName\Parley\Plugins\Community\`
  - macOS/Linux: `~/Parley/Plugins/Community/`

### Installing a Community Plugin

1. **Download the plugin** to your computer
   - Usually a `.zip` file containing plugin files

2. **Extract to Community folder**
   - Create the folder if it doesn't exist: `~/Parley/Plugins/Community/`
   - Extract the plugin so the structure looks like:
     ```
     ~/Parley/Plugins/Community/
     └── my-plugin/
         ├── plugin.json
         ├── main.py
         └── (other plugin files)
     ```

3. **Restart Parley** to load the new plugin

4. **Enable the plugin** in Settings
   - Open Settings → Plugins tab
   - Find your plugin in the list
   - Toggle it ON
   - Restart Parley again

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

### Safe Mode

If Parley crashes or behaves unexpectedly after installing a plugin:

1. Open Settings → Plugins tab
2. Check **"Disable all plugins on next launch (Safe Mode)"**
3. Restart Parley
4. Parley will start with all plugins disabled
5. Enable plugins one at a time to identify the problem

### Viewing Plugin Information

In the Settings → Plugins tab, each plugin shows:

- **Name and version** - e.g., "Hello World v1.0.0"
- **Author** - Who created the plugin
- **Trust level** - [OFFICIAL], [VERIFIED], or [UNVERIFIED]
- **Description** - What the plugin does
- **Permissions** - What the plugin can access

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

**Community plugins:**
1. Disable the plugin in Settings
2. Delete the plugin folder from `~/Parley/Plugins/Community/`
3. Restart Parley

**Official plugins:**
- Cannot be uninstalled (bundled with Parley)
- Can be disabled if not needed

[Back to TOC](#table-of-contents)

## Troubleshooting

### Plugin Not Appearing in List

**Check folder structure:**
- Plugin must be in `~/Parley/Plugins/Community/plugin-name/`
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

**Use Safe Mode:**
1. Delete `~/Parley/PluginSettings.json`
   - This resets plugin settings to defaults
2. Restart Parley
3. All plugins will be disabled
4. Re-enable plugins one at a time

**Alternative:**
1. Delete or move the problem plugin from `~/Parley/Plugins/Community/`
2. Restart Parley

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

### Trust Levels

Parley assigns trust levels to all plugins:

**[OFFICIAL]** - Verified by Parley team
- Bundled with Parley installation
- Thoroughly tested
- Safe to use

**[VERIFIED]** - Verified by community moderators
- Reviewed for malicious code
- Tested by community
- Generally safe

**[UNVERIFIED]** - Not reviewed
- Use caution
- Review source code if possible
- Only install from trusted sources

### What Plugins Can Access

**Currently (POC Phase):**
- Plugins can only read files in their own directory
- All output goes to Parley's log (monitored by you)
- Plugins run in isolation (crash won't affect Parley)

**Future (gRPC Phase):**
- Plugins will request specific permissions
- You can review permissions before enabling
- Sandboxed file access (plugins can't access arbitrary files)

### Best Practices

**Before installing a plugin:**
- Check trust level ([OFFICIAL] is safest)
- Read plugin description and permissions
- Review author's reputation
- If available, check source code

**After installing a plugin:**
- Enable in a test environment first
- Monitor plugin logs for suspicious activity
- Disable immediately if problems occur
- Report malicious plugins to Parley team

**Red flags:**
- Plugin requests excessive permissions
- Author is unknown or suspicious
- No documentation or source code
- Reports of malicious behavior

### Reporting Problems

If you encounter:
- Malicious plugin behavior
- Crashes or instability
- Security concerns

Report to: [Parley GitHub Issues](https://github.com/LordOfMyatar/Radoub/issues)

[Back to TOC](#table-of-contents)

---

**Need more help?**
- [Plugin Development Guide](Plugin_Development_Guide.md) - For creating plugins
- [Plugin Manifest Specification](Plugin_Manifest_Specification.md) - Technical details
- [Parley Documentation](../README.md) - Main documentation
