"""
Flowchart View Plugin for Parley

ChatMapper-style flowchart visualization for dialog trees.
Displays dialog nodes as colored boxes with connecting lines,
supporting zoom, pan, auto-layout, and export to PNG/SVG.

Epic 3: Advanced Visualization (Epic #40)
Phase 1: Foundation (#223-#227)
"""

import time
import json
import threading
from typing import Optional, Dict, Any
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

        # Plugin state
        self._initialized = False
        self._panel_registered = False

    def initialize(self) -> bool:
        """
        Initialize the plugin and connect to Parley.

        Returns:
            True if initialization successful, False otherwise
        """
        try:
            self.client = ParleyClient()
            print("[Flowchart] Connected to Parley gRPC server")

            # Show startup notification
            self.client.show_notification(
                "Flowchart View",
                "Plugin loaded. Flowchart panel will be available in View menu."
            )

            # Query initial dialog state
            self._refresh_dialog_state()

            self._initialized = True
            return True

        except Exception as e:
            print(f"[Flowchart] Initialization failed: {e}")
            return False

    def _refresh_dialog_state(self):
        """Refresh the current dialog state from Parley."""
        if self.client:
            dialog_id, dialog_name = self.client.get_current_dialog()

            if dialog_id != self.current_dialog_id:
                self.current_dialog_id = dialog_id
                self.current_dialog_name = dialog_name

                if dialog_id:
                    print(f"[Flowchart] Dialog loaded: {dialog_name}")
                    self._on_dialog_changed()
                else:
                    print("[Flowchart] No dialog loaded")

    def _on_dialog_changed(self):
        """
        Handle dialog change events.

        This will be called when:
        - A new dialog is opened
        - The current dialog is modified
        - The dialog is closed
        """
        if not self.current_dialog_id:
            # Dialog closed - clear flowchart
            print("[Flowchart] Dialog closed, clearing view")
            return

        # TODO: Fetch full dialog structure via GetCurrentDialog API (#103)
        # TODO: Transform dialog to graph format
        # TODO: Render via D3.js in WebView panel (#226, #227)
        print(f"[Flowchart] Would render flowchart for: {self.current_dialog_name}")

    def _on_node_selected(self, node_id: str):
        """
        Handle node selection events.

        Syncs selection between tree view and flowchart.

        Args:
            node_id: ID of the selected node
        """
        # TODO: Highlight node in flowchart (#234)
        # TODO: Scroll flowchart to show selected node
        print(f"[Flowchart] Node selected: {node_id}")

    def run(self):
        """
        Main plugin loop.

        Keeps the plugin alive and responsive to events.
        """
        self.running = True
        print("[Flowchart] Plugin running...")

        # Main loop - poll for changes
        # In Phase 2+, this will be replaced with event subscriptions
        poll_interval = 2.0  # seconds

        while self.running:
            try:
                # Periodic state refresh (POC approach)
                # Will be replaced with proper event subscription in Epic 0 completion
                self._refresh_dialog_state()

                # Also check selected node
                if self.client:
                    node_id, node_text = self.client.get_selected_node()
                    if node_id:
                        # Could track previous selection to detect changes
                        pass

                time.sleep(poll_interval)

            except KeyboardInterrupt:
                print("[Flowchart] Interrupted")
                break
            except Exception as e:
                print(f"[Flowchart] Error in main loop: {e}")
                time.sleep(poll_interval)

    def shutdown(self):
        """Clean up resources and disconnect."""
        print("[Flowchart] Shutting down...")
        self.running = False

        if self.client:
            try:
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
    print("=" * 60)
    print("FLOWCHART VIEW PLUGIN")
    print("Epic 3: Advanced Visualization")
    print("=" * 60)
    print("Plugin ID: org.parley.flowchart")
    print("=" * 60)

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

    print("=" * 60)
    print("FLOWCHART VIEW PLUGIN EXITED")
    print("=" * 60)


if __name__ == "__main__":
    main()
