# Goodbye World Plugin (Unstable)

**⚠️ WARNING: FOR TESTING ONLY - DO NOT USE IN PRODUCTION**

Intentionally unstable plugin for testing Parley's crash recovery and security systems.

## What it does

Randomly selects one of five failure modes when activated:

1. **Crash Mode (20% chance)**: Crashes immediately after 2 seconds
2. **Spam Mode (20% chance)**: Attempts 2000 notifications to trigger rate limiting
3. **Slow Mode (20% chance)**: Takes 20 seconds to start (timeout test)
4. **Time Bomb Mode (20% chance)**: Crashes after 5 seconds
5. **Behaving Mode (20% chance)**: Actually works correctly

## Purpose

This plugin tests:
- Crash detection and recovery
- Rate limiting enforcement
- Timeout protection
- Auto-disable after 3 crashes
- Crash recovery dialog
- Security audit logging

## Installation

1. Copy this folder to:
   ```
   ~/Parley/Plugins/Community/goodbye_world/
   ```

2. In Parley: Settings → Plugins → Refresh Plugin List

3. Enable "Goodbye World (Unstable)" plugin

4. Restart Parley **multiple times** to see different failure modes

## Expected Behavior

### First Run
- Random failure mode activates
- Plugin may crash (if modes 1, 4, or 3)
- Notification spam may occur (mode 2)
- Or it might just work (mode 5)

### After 3 Crashes
- Plugin should be auto-disabled
- Crash recovery dialog appears on next startup
- Shows which plugins were running during crash
- Offers Safe Mode option

### Safe Mode
- Settings → Plugins → Enable Safe Mode
- All plugins disabled on next launch
- Allows you to disable problematic plugins

## Testing Checklist

- [ ] Plugin crashes are detected
- [ ] Crash count increments in settings
- [ ] Auto-disable after 3 crashes works
- [ ] Rate limiting catches spam (mode 2)
- [ ] Crash recovery dialog appears after crash
- [ ] Safe Mode disables all plugins
- [ ] Security audit log records events
- [ ] Plugin can be re-enabled after crashes cleared

## Cleanup

To reset crash counter:
1. Settings → Plugins → Disable "Goodbye World"
2. Manually edit `~/Parley/ParleySettings.json`
3. Remove entry from `pluginCrashHistory`
4. Restart Parley
