"""
Flowchart View Plugin for Parley

ChatMapper-style flowchart visualization for dialog trees.
Displays dialog nodes as colored boxes with connecting lines,
supporting zoom, pan, auto-layout, and bidirectional selection sync.

Epic #40: Advanced Visualization

Refactored for security and maintainability:
- D3.js and dagre.js bundled locally (no CDN)
- CSS, JS, and HTML separated into static/templates
"""

import os
import sys
import time
import json
import hashlib
from pathlib import Path
from typing import Optional, Dict, Any, List

# Force unbuffered output so logs appear immediately
sys.stdout.reconfigure(line_buffering=True)
sys.stderr.reconfigure(line_buffering=True)

from parley_plugin import ParleyClient


class FlowchartPlugin:
    """
    Main plugin class for the Flowchart View.

    This plugin provides a visual flowchart representation of dialog trees,
    complementing Parley's tree view with a graphical node-and-edge display.
    """

    def __init__(self):
        self.client: Optional[ParleyClient] = None
        self.running = False
        self.current_dialog_id: Optional[str] = None
        self.current_dialog_name: Optional[str] = None
        self._last_structure_hash: Optional[str] = None
        self._last_selected_node_id: Optional[str] = None  # Track selection for bidirectional sync (#234)

        # Plugin state
        self._initialized = False
        self._panel_registered = False
        self._window_closed_by_user = False  # Track if exiting due to user close (#235)

        # Refresh settings (#235)
        self._auto_refresh_enabled = True  # Default: auto-refresh on
        self._sync_selection_enabled = True  # Default: bidirectional sync on
        self._refresh_interval = 2.0  # Default: 2 seconds
        self._poll_count = 0  # For reducing log verbosity
        self._logged_no_dialog = False  # Avoid spamming "no dialog" message

        # Paths to bundled assets
        self._plugin_dir = Path(__file__).parent
        self._template_path = self._plugin_dir / "templates" / "flowchart.html"
        self._css_path = self._plugin_dir / "static" / "flowchart.css"
        self._js_path = self._plugin_dir / "static" / "flowchart.js"
        self._d3_path = self._plugin_dir / "vendor" / "d3.v7.min.js"
        self._dagre_path = self._plugin_dir / "vendor" / "dagre.min.js"

        # Cache loaded assets
        self._template_cache: Optional[str] = None
        self._css_cache: Optional[str] = None
        self._js_cache: Optional[str] = None
        self._d3_cache: Optional[str] = None
        self._dagre_cache: Optional[str] = None

    def _load_assets(self) -> bool:
        """Load static assets from disk. Returns True if successful."""
        try:
            self._template_cache = self._template_path.read_text(encoding="utf-8")
            self._css_cache = self._css_path.read_text(encoding="utf-8")
            self._js_cache = self._js_path.read_text(encoding="utf-8")
            self._d3_cache = self._d3_path.read_text(encoding="utf-8")
            self._dagre_cache = self._dagre_path.read_text(encoding="utf-8")
            print("[Flowchart] Loaded bundled assets (D3, dagre, CSS, JS)")
            return True
        except Exception as e:
            print(f"[Flowchart] Failed to load assets: {e}")
            return False

    def initialize(self) -> bool:
        """
        Initialize the plugin and connect to Parley.

        Returns:
            True if initialization successful, False otherwise
        """
        try:
            # Load static assets first
            if not self._load_assets():
                print("[Flowchart] Cannot start without bundled assets")
                return False

            self.client = ParleyClient()
            print("[Flowchart] Connected to Parley gRPC server")

            # Register the flowchart panel
            if not self._register_panel():
                print("[Flowchart] Warning: Panel registration failed")
                # Continue anyway - panel registration is non-fatal for now

            # Query initial dialog state (no startup notification - panel presence is enough)
            self._refresh_dialog_state()

            self._initialized = True
            return True

        except Exception as e:
            print(f"[Flowchart] Initialization failed: {e}")
            return False

    def _register_panel(self) -> bool:
        """
        Register the flowchart panel with Parley.

        Returns:
            True if panel registration successful
        """
        if not self.client:
            return False

        success, error_msg, actual_id = self.client.register_panel(
            panel_id="flowchart-view",
            title="Flowchart View",
            position="right",
            render_mode="webview",
            initial_width=600,
            initial_height=400,
            can_float=True,
            can_close=True,
        )

        if success:
            print(f"[Flowchart] Panel registered with ID: {actual_id}")
            self._panel_registered = True

            # Set initial content - placeholder HTML
            self._update_panel_placeholder()
            return True
        else:
            print(f"[Flowchart] Panel registration failed: {error_msg}")
            return False

    def _update_panel_placeholder(self):
        """Update panel with placeholder content until dialog is loaded."""
        if not self.client or not self._panel_registered:
            return

        placeholder_html = """
<!DOCTYPE html>
<html>
<head>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            height: 100vh;
            box-sizing: border-box;
            background: var(--bg-color, #f5f5f5);
            color: var(--text-color, #333);
        }
        .placeholder {
            text-align: center;
            opacity: 0.7;
        }
        .icon {
            font-size: 48px;
            margin-bottom: 16px;
        }
        h2 {
            margin: 0 0 8px 0;
            font-weight: 500;
        }
        p {
            margin: 0;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div class="placeholder">
        <div class="icon">📊</div>
        <h2>Flowchart View</h2>
        <p>Open a dialog file to view the flowchart</p>
    </div>
</body>
</html>
"""
        success, error_msg = self.client.update_panel_content(
            panel_id="flowchart-view",
            content_type="html",
            content=placeholder_html,
        )

        if not success:
            print(f"[Flowchart] Failed to update panel content: {error_msg}")

    def _refresh_dialog_state(self):
        """Refresh the current dialog state from Parley."""
        if not self.client:
            return

        dialog_id, dialog_name = self.client.get_current_dialog()

        # Check for dialog change (open/close)
        if dialog_id != self.current_dialog_id:
            self.current_dialog_id = dialog_id
            self.current_dialog_name = dialog_name
            self._last_structure_hash = None  # Reset hash on dialog change

            if dialog_id:
                print(f"[Flowchart] Dialog loaded: {dialog_name}")
                self._logged_no_dialog = False
                self._on_dialog_changed()
            else:
                if not self._logged_no_dialog:
                    print("[Flowchart] No dialog loaded")
                    self._logged_no_dialog = True
                self._update_panel_placeholder()
        elif dialog_id:
            # Same dialog - check if content changed
            structure = self.client.get_dialog_structure()
            if structure.get("success", False):
                # Compute simple hash of structure
                struct_json = json.dumps(structure, sort_keys=True)
                current_hash = hashlib.md5(struct_json.encode()).hexdigest()

                if current_hash != self._last_structure_hash:
                    print(f"[Flowchart] Dialog content changed, re-rendering")
                    self._last_structure_hash = current_hash
                    self._render_flowchart_with_data(structure)
            else:
                # Log structure fetch failures sparingly (every 30 polls = ~1 min)
                self._poll_count += 1
                if self._poll_count % 30 == 1:
                    error_msg = structure.get("error_message", "Unknown error")
                    print(f"[Flowchart] Failed to get dialog structure: {error_msg}")

    def _on_dialog_changed(self):
        """
        Handle dialog change events.

        This will be called when:
        - A new dialog is opened
        - The current dialog is modified
        - The dialog is closed
        """
        if not self.current_dialog_id:
            # Dialog closed - show placeholder
            print("[Flowchart] Dialog closed, showing placeholder")
            self._update_panel_placeholder()
            return

        print(f"[Flowchart] Rendering flowchart for: {self.current_dialog_name}")
        self._render_flowchart()

    def _render_flowchart(self):
        """
        Render the current dialog as a D3.js + dagre.js flowchart.

        Generates HTML with embedded dialog data for Sugiyama layout visualization.
        Phase 2: Includes theme awareness, speaker colors, script indicators.
        """
        if not self.client or not self._panel_registered:
            return

        # Get dialog structure from Parley API (#227)
        structure = self.client.get_dialog_structure()
        self._render_flowchart_with_data(structure)

    def _render_flowchart_with_data(self, structure: Dict[str, Any]):
        """
        Render flowchart with pre-fetched structure data.

        Args:
            structure: Dialog structure from get_dialog_structure API
        """
        if not self.client or not self._panel_registered:
            return

        if not structure.get("success", False):
            # Fall back to demo data if API fails
            print(f"[Flowchart] Failed to get structure: {structure.get('error_message', 'Unknown error')}")
            dialog_data = self._generate_demo_graph_data()
        else:
            # Use real dialog data
            dialog_data = {
                "nodes": structure.get("nodes", []),
                "links": structure.get("links", []),
            }

        # Extract unique speakers and assign colors (#230)
        speaker_colors = self._generate_speaker_colors(dialog_data.get("nodes", []))

        # Get theme from Parley settings (#229)
        theme_info = self.client.get_theme()
        theme = "dark" if theme_info.get("is_dark", True) else "light"

        # Get current selection for initial highlight (#234)
        selected_node_id = self._last_selected_node_id
        if not selected_node_id and self.client:
            node_id, _ = self.client.get_selected_node()
            if node_id:
                selected_node_id = node_id
                self._last_selected_node_id = node_id

        # Check for stored UI toggle settings (#235)
        # This persists settings across re-renders
        found, stored_sync = self.client.get_panel_setting("flowchart-view", "sync_selection")
        if found:
            self._sync_selection_enabled = stored_sync == "true"

        found, stored_auto = self.client.get_panel_setting("flowchart-view", "auto_refresh")
        if found:
            self._auto_refresh_enabled = stored_auto == "true"

        # Generate HTML from template with bundled assets
        auto_refresh_icon = "⏸" if self._auto_refresh_enabled else "▶"
        sync_checked = "checked" if self._sync_selection_enabled else ""

        html = self._template_cache
        html = html.replace("{{css_content}}", self._css_cache)
        html = html.replace("{{d3_content}}", self._d3_cache)
        html = html.replace("{{dagre_content}}", self._dagre_cache)
        html = html.replace("{{js_content}}", self._js_cache)
        html = html.replace("{{theme}}", theme)
        html = html.replace("{{dialog_name}}", self.current_dialog_name or "Untitled")
        html = html.replace("{{node_count}}", str(len(dialog_data.get("nodes", []))))
        html = html.replace("{{dialog_data}}", json.dumps(dialog_data))
        html = html.replace("{{speaker_colors}}", json.dumps(speaker_colors))
        html = html.replace("{{selected_node_id}}", json.dumps(selected_node_id))
        html = html.replace("{{auto_refresh_icon}}", auto_refresh_icon)
        html = html.replace("{{auto_refresh_enabled}}", "true" if self._auto_refresh_enabled else "false")
        html = html.replace("{{sync_selection_enabled}}", "true" if self._sync_selection_enabled else "false")
        html = html.replace("{{sync_checked}}", sync_checked)

        # Send to panel
        success, error_msg = self.client.update_panel_content(
            panel_id="flowchart-view",
            content_type="html",
            content=html,
        )

        if not success:
            print(f"[Flowchart] Failed to render: {error_msg}")

    def _generate_speaker_colors(self, nodes: List[Dict[str, Any]]) -> Dict[str, str]:
        """
        Get speaker colors from Parley settings, with fallback to local generation.

        Calls the GetSpeakerColors API to use the same colors as Parley's tree view.
        Falls back to generating colors locally if the API fails.

        Args:
            nodes: List of dialog nodes

        Returns:
            Dict mapping speaker names to hex colors, plus "_pc" and "_owner" keys
        """
        colors = {}

        # Try to get colors from Parley API first
        if self.client:
            try:
                parley_colors = self.client.get_speaker_colors()
                colors["_pc"] = parley_colors.get("pc_color", "#4FC3F7")
                colors["_owner"] = parley_colors.get("owner_color", "#FF8A65")
                colors.update(parley_colors.get("speaker_colors", {}))
                return colors
            except Exception as e:
                print(f"[Flowchart] Failed to get speaker colors from API, using fallback: {e}")

        # Fallback: generate colors locally
        colors["_pc"] = "#4FC3F7"  # Default PC blue
        colors["_owner"] = "#FF8A65"  # Default Owner orange

        # Predefined palette for named speakers
        palette = [
            "#BA68C8",  # Purple
            "#26A69A",  # Teal
            "#FFD54F",  # Amber
            "#F48FB1",  # Pink
            "#8e4585",  # Violet
            "#5a4d2d",  # Brown
        ]

        # Collect unique speakers
        speakers = set()
        for node in nodes:
            speaker = node.get("speaker", "")
            if speaker and node.get("type") == "npc":
                speakers.add(speaker)

        # Assign colors to named speakers
        for i, speaker in enumerate(sorted(speakers)):
            if i < len(palette):
                colors[speaker] = palette[i]
            else:
                # Generate hash-based color for overflow
                hash_val = sum(ord(c) for c in speaker)
                hue = hash_val % 360
                colors[speaker] = f"hsl({hue}, 50%, 35%)"

        return colors

    def _generate_demo_graph_data(self) -> Dict[str, Any]:
        """
        Generate demo graph data for testing dagre.js rendering.

        Returns a sample dialog structure to verify the visualization works.
        Phase 2: Includes speaker, script indicators for demo.

        Returns:
            Dict with 'nodes' and 'links' arrays for dagre.js
        """
        # Demo dialog tree structure with Phase 2 features
        nodes = [
            {"id": "root", "type": "root", "text": "Dialog Start", "speaker": ""},
            {"id": "npc_1", "type": "npc", "text": "Hello, traveler!", "speaker": "Guard",
             "has_action": False, "has_condition": False},
            {"id": "pc_1", "type": "pc", "text": "Greetings.", "speaker": "",
             "has_action": False, "has_condition": False},
            {"id": "pc_2", "type": "pc", "text": "What do you want?", "speaker": "",
             "has_action": False, "has_condition": False},
            {"id": "pc_3", "type": "pc", "text": "[Leave]", "speaker": "",
             "has_action": True, "has_condition": False, "action_script": "nw_walk_wp"},
            {"id": "npc_2", "type": "npc", "text": "I have a quest for you.", "speaker": "Guard",
             "has_action": True, "has_condition": False, "action_script": "sc_start_quest"},
            {"id": "npc_3", "type": "npc", "text": "No need to be rude!", "speaker": "Guard",
             "has_action": False, "has_condition": False},
            {"id": "pc_4", "type": "pc", "text": "Tell me more.", "speaker": "",
             "has_action": False, "has_condition": False},
            {"id": "pc_5", "type": "pc", "text": "Not interested.", "speaker": "",
             "has_action": False, "has_condition": False},
            {"id": "npc_4", "type": "npc", "text": "There's a cave nearby...", "speaker": "Merchant",
             "has_action": False, "has_condition": False},
            {"id": "link_1", "type": "link", "text": "-> Quest Accepted", "speaker": "",
             "is_link": True, "link_target": "npc_2",
             "has_action": False, "has_condition": True, "condition_script": "gc_has_item"},
        ]

        links = [
            {"source": "root", "target": "npc_1", "has_condition": False},
            {"source": "npc_1", "target": "pc_1", "has_condition": False},
            {"source": "npc_1", "target": "pc_2", "has_condition": False},
            {"source": "npc_1", "target": "pc_3", "has_condition": False},
            {"source": "pc_1", "target": "npc_2", "has_condition": False},
            {"source": "pc_2", "target": "npc_3", "has_condition": False},
            {"source": "npc_2", "target": "pc_4", "has_condition": True, "condition_script": "gc_check_skill"},
            {"source": "npc_2", "target": "pc_5", "has_condition": False},
            {"source": "pc_4", "target": "npc_4", "has_condition": False},
            {"source": "npc_4", "target": "link_1", "has_condition": False},
        ]

        return {"nodes": nodes, "links": links}

    def run(self):
        """
        Main plugin loop.

        Keeps the plugin alive and responsive to events.
        """
        self.running = True
        print("[Flowchart] Plugin running...")

        while self.running:
            try:
                # Check if panel is still open (#235)
                if self.client and self._panel_registered:
                    if not self.client.is_panel_open("flowchart-view"):
                        print("[Flowchart] Panel closed by user, stopping plugin")
                        self._window_closed_by_user = True
                        break

                # Only poll when auto-refresh is enabled (#235)
                if self._auto_refresh_enabled:
                    self._refresh_dialog_state()

                    # Track selection changes for bidirectional sync (#234)
                    if self.client:
                        node_id, node_text = self.client.get_selected_node()
                        if node_id and node_id != self._last_selected_node_id:
                            self._last_selected_node_id = node_id

                time.sleep(self._refresh_interval)

            except KeyboardInterrupt:
                print("[Flowchart] Interrupted")
                break
            except Exception as e:
                print(f"[Flowchart] Error in main loop: {e}")
                time.sleep(self._refresh_interval)

    def shutdown(self):
        """Clean up resources and disconnect."""
        print("[Flowchart] Shutting down...")
        self.running = False

        if self.client:
            try:
                # Only unregister panel if NOT exiting due to user closing window (#235)
                if self._panel_registered and not self._window_closed_by_user:
                    self.client.close_panel("flowchart-view")
                    self._panel_registered = False

                if not self._window_closed_by_user:
                    self.client.show_notification(
                        "Flowchart View",
                        "Plugin stopped."
                    )
            except Exception:
                pass  # May fail if Parley is already closing

            self.client.close()
            self.client = None

        self._initialized = False
        print("[Flowchart] Shutdown complete")


def main():
    """Entry point for the Flowchart View plugin."""
    print("[Flowchart] Starting plugin...")

    plugin = FlowchartPlugin()

    try:
        if plugin.initialize():
            plugin.run()
        else:
            print("[Flowchart] Failed to initialize")
    except Exception as e:
        print(f"[Flowchart] Fatal error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        plugin.shutdown()

    print("[Flowchart] Plugin exited")


if __name__ == "__main__":
    main()
