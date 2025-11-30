# Flowchart View Plugin

Flowchart visualization for Parley dialog trees.

## Overview

This plugin provides a visual flowchart representation of dialog trees, complementing Parley's tree view with a graphical node-and-edge display.

**Epic**: #40 (Epic 3: Advanced Visualization)
**Status**: Phase 2 Complete

## Features

### Phase 1 - Foundation (Complete)
- [x] Plugin scaffold and manifest (#223)
- [x] Dockable/floating panel registration (#224)
- [x] WebView.Avalonia.Cross integration (#225)
- [x] Basic D3.js graph rendering (#226)
- [x] Live dialog data integration (#227)

### Phase 2 - Layout and Visual Design (Complete)
- [x] Sugiyama auto-layout (dagre.js) (#228)
- [x] Theme awareness - dark/light mode (#229)
- [x] NPC speaker color integration via GetSpeakerColors API (#230)
- [x] Script indicators on nodes - ⚡ action, ❓ condition (#231)
- [x] Link node styling - dashed borders, reduced opacity (#232)

### Phase 3 - Interaction and Navigation (Planned)
- [x] Zoom and pan controls (basic)
- [ ] Bidirectional node selection sync
- [ ] User-controllable refresh settings
- [ ] Minimap navigation panel
- [ ] Circular reference handling

### Phase 4 - Advanced Features (Planned)
- [ ] PNG export
- [ ] SVG export
- [ ] Drag-drop node repositioning (optional)

## Requirements

- Parley >= 0.1.33
- Python 3.8+
- parley_plugin library

## Permissions

This plugin requires the following permissions:
- `ui.create_panel` - Create the flowchart panel
- `ui.show_notification` - Status notifications
- `dialog.read` - Read dialog structure
- `dialog.subscribe_changes` - React to dialog changes

## Development

### Running Locally

The plugin is automatically discovered by Parley from:
```
Parley/Parley/Plugins/Official/flowchart-view/
```

### Testing

1. Launch Parley
2. Open Settings > Plugins
3. Enable "Flowchart View"
4. Open a dialog file
5. Check console output for plugin activity

## Architecture

```
Parley Host (C#)
    |
    | gRPC
    v
Flowchart Plugin (Python)
    |
    | HTML/JS
    v
WebView Panel (D3.js + dagre.js)
```

## License

Part of the Parley project. See main LICENSE file.
