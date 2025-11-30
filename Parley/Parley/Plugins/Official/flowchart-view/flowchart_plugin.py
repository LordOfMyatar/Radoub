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
from typing import Optional, Dict, Any, List
from parley_plugin import ParleyClient


# D3.js flowchart HTML template
# Uses D3.js v7 from CDN for force-directed graph layout
FLOWCHART_HTML_TEMPLATE = '''<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <style>
        * {{
            box-sizing: border-box;
        }}
        html, body {{
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            overflow: hidden;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #1e1e1e;
        }}
        #flowchart {{
            width: 100%;
            height: 100%;
        }}
        .node {{
            cursor: pointer;
        }}
        .node rect {{
            stroke-width: 2px;
            rx: 6;
            ry: 6;
        }}
        .node.npc rect {{
            fill: #2d5a27;
            stroke: #4a9c3f;
        }}
        .node.pc rect {{
            fill: #1a4a6e;
            stroke: #3498db;
        }}
        .node.root rect {{
            fill: #5a2d5a;
            stroke: #9b59b6;
        }}
        .node.link rect {{
            fill: #6e4a1a;
            stroke: #e67e22;
        }}
        .node.selected rect {{
            stroke: #f1c40f;
            stroke-width: 3px;
        }}
        .node text {{
            fill: #ecf0f1;
            font-size: 11px;
            pointer-events: none;
        }}
        .node .node-type {{
            font-size: 9px;
            fill: #95a5a6;
            text-transform: uppercase;
        }}
        .link {{
            fill: none;
            stroke: #555;
            stroke-width: 2px;
        }}
        .link.highlight {{
            stroke: #f1c40f;
            stroke-width: 3px;
        }}
        .arrowhead {{
            fill: #555;
        }}
        #controls {{
            position: absolute;
            top: 10px;
            right: 10px;
            display: flex;
            gap: 5px;
            z-index: 100;
        }}
        #controls button {{
            background: #333;
            color: #fff;
            border: 1px solid #555;
            padding: 6px 12px;
            cursor: pointer;
            border-radius: 4px;
            font-size: 12px;
        }}
        #controls button:hover {{
            background: #444;
        }}
        #info {{
            position: absolute;
            bottom: 10px;
            left: 10px;
            color: #888;
            font-size: 11px;
            z-index: 100;
        }}
    </style>
</head>
<body>
    <div id="controls">
        <button onclick="zoomIn()">+</button>
        <button onclick="zoomOut()">-</button>
        <button onclick="resetZoom()">Reset</button>
        <button onclick="fitToScreen()">Fit</button>
    </div>
    <div id="info">
        <span id="dialog-name">{dialog_name}</span> |
        <span id="node-count">{node_count} nodes</span>
    </div>
    <svg id="flowchart"></svg>

    <script src="https://d3js.org/d3.v7.min.js"></script>
    <script>
        // Dialog data injected by Python
        const dialogData = {dialog_data};

        const svg = d3.select("#flowchart");
        const width = window.innerWidth;
        const height = window.innerHeight;

        svg.attr("width", width).attr("height", height);

        // Create container group for zoom/pan
        const g = svg.append("g");

        // Define arrowhead marker
        svg.append("defs").append("marker")
            .attr("id", "arrowhead")
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 20)
            .attr("refY", 0)
            .attr("markerWidth", 6)
            .attr("markerHeight", 6)
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("class", "arrowhead");

        // Set up zoom behavior
        const zoom = d3.zoom()
            .scaleExtent([0.1, 4])
            .on("zoom", (event) => g.attr("transform", event.transform));

        svg.call(zoom);

        // Parse nodes and links
        const nodes = dialogData.nodes || [];
        const links = dialogData.links || [];

        // Create force simulation
        const simulation = d3.forceSimulation(nodes)
            .force("link", d3.forceLink(links)
                .id(d => d.id)
                .distance(120))
            .force("charge", d3.forceManyBody().strength(-400))
            .force("center", d3.forceCenter(width / 2, height / 2))
            .force("collision", d3.forceCollide().radius(60));

        // Draw links
        const link = g.append("g")
            .attr("class", "links")
            .selectAll("path")
            .data(links)
            .join("path")
            .attr("class", "link")
            .attr("marker-end", "url(#arrowhead)");

        // Draw nodes
        const node = g.append("g")
            .attr("class", "nodes")
            .selectAll("g")
            .data(nodes)
            .join("g")
            .attr("class", d => `node ${{d.type || 'npc'}}`)
            .call(d3.drag()
                .on("start", dragstarted)
                .on("drag", dragged)
                .on("end", dragended));

        // Node rectangles
        node.append("rect")
            .attr("width", 140)
            .attr("height", 50)
            .attr("x", -70)
            .attr("y", -25);

        // Node type label
        node.append("text")
            .attr("class", "node-type")
            .attr("x", -65)
            .attr("y", -10)
            .text(d => d.type ? d.type.toUpperCase() : "NPC");

        // Node text (truncated)
        node.append("text")
            .attr("x", -65)
            .attr("y", 8)
            .text(d => truncateText(d.text || d.id, 20));

        // Click handler
        node.on("click", (event, d) => {{
            // Remove previous selection
            node.classed("selected", false);
            // Select clicked node
            d3.select(event.currentTarget).classed("selected", true);
            // Notify parent (if bridge exists)
            if (window.notifyNodeSelected) {{
                window.notifyNodeSelected(d.id);
            }}
        }});

        // Simulation tick
        simulation.on("tick", () => {{
            link.attr("d", d => {{
                const dx = d.target.x - d.source.x;
                const dy = d.target.y - d.source.y;
                return `M${{d.source.x}},${{d.source.y}} L${{d.target.x}},${{d.target.y}}`;
            }});

            node.attr("transform", d => `translate(${{d.x}},${{d.y}})`);
        }});

        // Drag functions
        function dragstarted(event) {{
            if (!event.active) simulation.alphaTarget(0.3).restart();
            event.subject.fx = event.subject.x;
            event.subject.fy = event.subject.y;
        }}

        function dragged(event) {{
            event.subject.fx = event.x;
            event.subject.fy = event.y;
        }}

        function dragended(event) {{
            if (!event.active) simulation.alphaTarget(0);
            event.subject.fx = null;
            event.subject.fy = null;
        }}

        // Helper functions
        function truncateText(text, maxLen) {{
            if (!text) return "";
            return text.length > maxLen ? text.substring(0, maxLen) + "..." : text;
        }}

        // Zoom controls
        function zoomIn() {{
            svg.transition().call(zoom.scaleBy, 1.3);
        }}

        function zoomOut() {{
            svg.transition().call(zoom.scaleBy, 0.7);
        }}

        function resetZoom() {{
            svg.transition().call(zoom.transform, d3.zoomIdentity);
        }}

        function fitToScreen() {{
            const bounds = g.node().getBBox();
            const fullWidth = width;
            const fullHeight = height;
            const bWidth = bounds.width;
            const bHeight = bounds.height;
            const scale = 0.9 * Math.min(fullWidth / bWidth, fullHeight / bHeight);
            const tx = (fullWidth - scale * bWidth) / 2 - scale * bounds.x;
            const ty = (fullHeight - scale * bHeight) / 2 - scale * bounds.y;
            svg.transition().call(zoom.transform, d3.zoomIdentity.translate(tx, ty).scale(scale));
        }}

        // Handle window resize
        window.addEventListener("resize", () => {{
            const w = window.innerWidth;
            const h = window.innerHeight;
            svg.attr("width", w).attr("height", h);
        }});

        // Initial fit after simulation settles
        setTimeout(fitToScreen, 1500);
    </script>
</body>
</html>
'''


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

            # Register the flowchart panel
            if not self._register_panel():
                print("[Flowchart] Warning: Panel registration failed")
                # Continue anyway - panel registration is non-fatal for now

            # Show startup notification
            self.client.show_notification(
                "Flowchart View",
                "Plugin loaded. Flowchart panel registered."
            )

            # Query initial dialog state
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
            # Dialog closed - show placeholder
            print("[Flowchart] Dialog closed, showing placeholder")
            self._update_panel_placeholder()
            return

        print(f"[Flowchart] Rendering flowchart for: {self.current_dialog_name}")
        self._render_flowchart()

    def _render_flowchart(self):
        """
        Render the current dialog as a D3.js flowchart.

        Generates HTML with embedded dialog data for D3.js visualization.
        """
        if not self.client or not self._panel_registered:
            return

        # Get dialog structure from Parley API (#227)
        structure = self.client.get_dialog_structure()

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
            print(f"[Flowchart] Got {len(dialog_data['nodes'])} nodes from Parley API")

        # Generate HTML with dialog data
        html = FLOWCHART_HTML_TEMPLATE.format(
            dialog_name=self.current_dialog_name or "Untitled",
            node_count=len(dialog_data.get("nodes", [])),
            dialog_data=json.dumps(dialog_data),
        )

        # Send to panel
        success, error_msg = self.client.update_panel_content(
            panel_id="flowchart-view",
            content_type="html",
            content=html,
        )

        if success:
            print(f"[Flowchart] Rendered {len(dialog_data['nodes'])} nodes")
        else:
            print(f"[Flowchart] Failed to render: {error_msg}")

    def _generate_demo_graph_data(self) -> Dict[str, Any]:
        """
        Generate demo graph data for testing D3.js rendering.

        Returns a sample dialog structure to verify the visualization works.
        This will be replaced with real dialog data in Phase 2 (#227).

        Returns:
            Dict with 'nodes' and 'links' arrays for D3.js
        """
        # Demo dialog tree structure
        nodes = [
            {"id": "root", "type": "root", "text": "Dialog Start"},
            {"id": "npc_1", "type": "npc", "text": "Hello, traveler!"},
            {"id": "pc_1", "type": "pc", "text": "Greetings."},
            {"id": "pc_2", "type": "pc", "text": "What do you want?"},
            {"id": "pc_3", "type": "pc", "text": "[Leave]"},
            {"id": "npc_2", "type": "npc", "text": "I have a quest."},
            {"id": "npc_3", "type": "npc", "text": "No need to be rude!"},
            {"id": "pc_4", "type": "pc", "text": "Tell me more."},
            {"id": "pc_5", "type": "pc", "text": "Not interested."},
            {"id": "npc_4", "type": "npc", "text": "There's a cave..."},
            {"id": "link_1", "type": "link", "text": "-> Quest Accepted"},
        ]

        links = [
            {"source": "root", "target": "npc_1"},
            {"source": "npc_1", "target": "pc_1"},
            {"source": "npc_1", "target": "pc_2"},
            {"source": "npc_1", "target": "pc_3"},
            {"source": "pc_1", "target": "npc_2"},
            {"source": "pc_2", "target": "npc_3"},
            {"source": "npc_2", "target": "pc_4"},
            {"source": "npc_2", "target": "pc_5"},
            {"source": "pc_4", "target": "npc_4"},
            {"source": "npc_4", "target": "link_1"},
        ]

        return {"nodes": nodes, "links": links}

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
                # Close the panel
                if self._panel_registered:
                    self.client.close_panel("flowchart-view")
                    self._panel_registered = False

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
