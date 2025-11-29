# Flowchart View Plugin

ChatMapper-style flowchart visualization for Parley dialog trees.

## Overview

This plugin provides a visual flowchart representation of dialog trees, complementing Parley's tree view with a graphical node-and-edge display.

**Epic**: #40 (Epic 3: Advanced Visualization)
**Status**: Phase 1 - Foundation

## Features (Planned)

### Phase 1 - Foundation
- [x] Plugin scaffold and manifest (#223)
- [ ] Dockable/floating panel registration (#224)
- [ ] WebView.Avalonia.Cross integration (#225)
- [ ] Basic D3.js graph rendering (#226)
- [ ] Live dialog data integration (#227)

### Phase 2 - Layout and Visual Design
- [ ] Sugiyama auto-layout (dagre.js)
- [ ] Theme awareness
- [ ] NPC speaker color/shape integration
- [ ] Script indicators on nodes
- [ ] Link node styling

### Phase 3 - Interaction and Navigation
- [ ] Zoom and pan controls
- [ ] Bidirectional node selection sync
- [ ] User-controllable refresh settings
- [ ] Minimap navigation panel
- [ ] Circular reference handling

### Phase 4 - Advanced Features
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
