"""
Flowchart View Plugin for Parley

ChatMapper-style flowchart visualization for dialog trees.
Displays dialog nodes as colored boxes with connecting lines,
supporting zoom, pan, auto-layout, and export to PNG/SVG.

Epic 3: Advanced Visualization (Epic #40)
Phase 1: Foundation (#223-#227)
Phase 2: Layout and Visual Design (#228-#232)
"""

import sys
import time
import json
import threading
from typing import Optional, Dict, Any, List

# Force unbuffered output so logs appear immediately
sys.stdout.reconfigure(line_buffering=True)
sys.stderr.reconfigure(line_buffering=True)

from parley_plugin import ParleyClient


# D3.js + dagre.js flowchart HTML template
# Uses dagre-d3 for Sugiyama hierarchical layout (#228)
# Supports theme awareness (#229), speaker colors (#230),
# script indicators (#231), and link node styling (#232)
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
        }}
        /* Theme-aware colors (#229) */
        body.dark {{
            background: #1e1e1e;
            --text-primary: #ecf0f1;
            --text-secondary: #95a5a6;
            --control-bg: #333;
            --control-border: #555;
            --control-hover: #444;
            --link-color: #555;
            --link-condition: #e74c3c;
            --selection-color: #f1c40f;
            --selection-glow: rgba(241, 196, 15, 0.6);
        }}
        body.light {{
            background: #f5f5f5;
            --text-primary: #2c3e50;
            --text-secondary: #7f8c8d;
            --control-bg: #fff;
            --control-border: #ccc;
            --control-hover: #e0e0e0;
            --link-color: #95a5a6;
            --link-condition: #c0392b;
            --selection-color: #d4ac0d;
            --selection-glow: rgba(212, 172, 13, 0.5);
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
        /* Node type colors - NPC (#230 - speaker-based coloring handled in JS) */
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
        /* Link node styling (#232) - grayed appearance */
        .node.link rect {{
            fill: #4a4a4a;
            stroke: #888;
            stroke-dasharray: 4,2;
            opacity: 0.7;
        }}
        /* Selection highlight - theme-aware (#234) */
        .node.selected rect {{
            stroke: var(--selection-color) !important;
            stroke-width: 4px !important;
            filter: drop-shadow(0 0 8px var(--selection-glow)) drop-shadow(0 0 12px var(--selection-glow));
        }}
        /* Secondary highlight for link target nodes (#234) */
        .node.target-highlight rect {{
            stroke: var(--selection-color) !important;
            stroke-width: 3px !important;
            stroke-dasharray: 6,3 !important;
            filter: drop-shadow(0 0 4px var(--selection-glow));
        }}
        .node text {{
            fill: var(--text-primary, #ecf0f1);
            font-size: 11px;
            pointer-events: none;
        }}
        .node .node-type {{
            font-size: 9px;
            fill: var(--text-secondary, #95a5a6);
            text-transform: uppercase;
        }}
        .node .speaker-tag {{
            font-size: 9px;
            font-weight: bold;
        }}
        /* Script indicators (#231) */
        .node .script-indicator {{
            font-size: 10px;
            fill: #f39c12;
        }}
        .node .script-indicator.condition {{
            fill: #e74c3c;
        }}
        .node .script-indicator.action {{
            fill: #27ae60;
        }}
        /* Edge styles - path element has the class directly */
        path.edgePath {{
            stroke: var(--link-color, #555);
            stroke-width: 2px;
            fill: none;
        }}
        /* Conditional edge styling (#231) */
        path.edgePath.has-condition {{
            stroke: var(--link-condition, #e74c3c);
            stroke-dasharray: 5,3;
        }}
        /* Link-to-link edges (#232) - dotted lines */
        path.edgePath.to-link {{
            stroke-dasharray: 3,3;
            opacity: 0.6;
        }}
        .edgeLabel {{
            font-size: 10px;
            fill: var(--text-secondary, #95a5a6);
        }}
        .edge-condition-marker {{
            font-size: 14px;
            fill: #e74c3c;
            pointer-events: none;
        }}
        marker {{
            fill: var(--link-color, #555);
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
            background: var(--control-bg, #333);
            color: var(--text-primary, #fff);
            border: 1px solid var(--control-border, #555);
            padding: 6px 12px;
            cursor: pointer;
            border-radius: 4px;
            font-size: 12px;
        }}
        #controls button:hover {{
            background: var(--control-hover, #444);
        }}
        #info {{
            position: absolute;
            bottom: 10px;
            left: 10px;
            color: var(--text-secondary, #888);
            font-size: 11px;
            z-index: 100;
        }}
        #legend {{
            position: absolute;
            bottom: 10px;
            right: 10px;
            display: flex;
            gap: 12px;
            font-size: 10px;
            color: var(--text-secondary, #888);
            z-index: 100;
        }}
        #legend .legend-item {{
            display: flex;
            align-items: center;
            gap: 4px;
        }}
        #legend .legend-color {{
            width: 12px;
            height: 12px;
            border-radius: 2px;
        }}
    </style>
</head>
<body class="{theme}">
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
    <div id="legend">
        <div class="legend-item"><div class="legend-color" style="background:#2d5a27"></div>NPC</div>
        <div class="legend-item"><div class="legend-color" style="background:#1a4a6e"></div>PC</div>
        <div class="legend-item"><div class="legend-color" style="background:#5a2d5a"></div>Root</div>
        <div class="legend-item"><div class="legend-color" style="background:#4a4a4a;border:1px dashed #888"></div>Link</div>
        <div class="legend-item"><span style="color:#27ae60">⚡</span>Action</div>
        <div class="legend-item"><span style="color:#e74c3c">❓</span>Condition</div>
    </div>
    <svg id="flowchart"></svg>

    <!-- D3.js v7 -->
    <script src="https://d3js.org/d3.v7.min.js"></script>
    <!-- dagre for layout algorithm -->
    <script src="https://cdn.jsdelivr.net/npm/dagre@0.8.5/dist/dagre.min.js"></script>
    <script>
        // Dialog data and config injected by Python
        const dialogData = {dialog_data};
        const speakerColors = {speaker_colors};
        const initialSelectedNodeId = {selected_node_id};

        const svg = d3.select("#flowchart");
        let width = window.innerWidth;
        let height = window.innerHeight;

        svg.attr("width", width).attr("height", height);

        // Create container group for zoom/pan
        const g = svg.append("g").attr("class", "graph-container");

        // Define arrowhead marker
        const defs = svg.append("defs");
        defs.append("marker")
            .attr("id", "arrowhead")
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 8)
            .attr("refY", 0)
            .attr("markerWidth", 6)
            .attr("markerHeight", 6)
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("fill", "var(--link-color, #555)");

        // Conditional edge marker (red)
        defs.append("marker")
            .attr("id", "arrowhead-condition")
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 8)
            .attr("refY", 0)
            .attr("markerWidth", 6)
            .attr("markerHeight", 6)
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("fill", "var(--link-condition, #e74c3c)");

        // Set up zoom behavior
        const zoom = d3.zoom()
            .scaleExtent([0.1, 4])
            .on("zoom", (event) => g.attr("transform", event.transform));

        svg.call(zoom);

        // Parse nodes and links
        const nodes = dialogData.nodes || [];
        const links = dialogData.links || [];

        // Create dagre graph for Sugiyama layout (#228)
        const dagreGraph = new dagre.graphlib.Graph();
        dagreGraph.setGraph({{
            rankdir: "TB",     // Top to bottom (Entry at top)
            nodesep: 60,       // Horizontal separation
            ranksep: 80,       // Vertical separation between ranks
            marginx: 20,
            marginy: 20
        }});
        dagreGraph.setDefaultEdgeLabel(() => ({{}}));

        // Node dimensions
        const nodeWidth = 160;
        const nodeHeight = 60;

        // Add nodes to dagre
        nodes.forEach(node => {{
            dagreGraph.setNode(node.id, {{
                width: nodeWidth,
                height: nodeHeight,
                ...node
            }});
        }});

        // Add edges to dagre
        links.forEach(link => {{
            dagreGraph.setEdge(link.source, link.target, {{
                hasCondition: link.has_condition || false,
                conditionScript: link.condition_script || ""
            }});
        }});

        // Run the layout algorithm
        dagre.layout(dagreGraph);

        // Get speaker color for NPC nodes (#230)
        // Uses colors from Parley settings via GetSpeakerColors API
        function getSpeakerColor(nodeType, speaker) {{
            // PC nodes use the PC color
            if (nodeType === "pc") {{
                return speakerColors["_pc"] || "#4FC3F7";
            }}
            // Named NPC speakers - check API colors first
            if (speaker && speakerColors[speaker]) {{
                return speakerColors[speaker];
            }}
            // Owner/default NPC (empty speaker) uses owner color
            if (!speaker || speaker === "") {{
                return speakerColors["_owner"] || "#FF8A65";
            }}
            // Fallback for unknown speakers - generate from hash
            let hash = 0;
            for (let i = 0; i < speaker.length; i++) {{
                hash = speaker.charCodeAt(i) + ((hash << 5) - hash);
            }}
            const hue = Math.abs(hash % 360);
            return `hsl(${{hue}}, 50%, 35%)`;
        }}

        // Build script indicator text (#231)
        function getScriptIndicators(node) {{
            let indicators = [];
            if (node.has_condition) indicators.push("❓");
            if (node.has_action) indicators.push("⚡");
            return indicators.join(" ");
        }}

        // Draw edges first (so they appear behind nodes)
        const edgeGroup = g.append("g").attr("class", "edges");

        dagreGraph.edges().forEach(e => {{
            const edge = dagreGraph.edge(e);
            const sourceNode = dagreGraph.node(e.v);
            const targetNode = dagreGraph.node(e.w);

            if (!sourceNode || !targetNode) return;

            // Determine edge class
            let edgeClass = "edgePath";
            if (edge.hasCondition) edgeClass += " has-condition";
            if (targetNode.type === "link") edgeClass += " to-link";

            // Get edge points for path
            const points = edge.points || [
                {{ x: sourceNode.x, y: sourceNode.y + nodeHeight/2 }},
                {{ x: targetNode.x, y: targetNode.y - nodeHeight/2 }}
            ];

            // Create path
            edgeGroup.append("path")
                .attr("class", edgeClass)
                .attr("d", d3.line()
                    .x(d => d.x)
                    .y(d => d.y)
                    .curve(d3.curveBasis)(points))
                .attr("marker-end", edge.hasCondition ? "url(#arrowhead-condition)" : "url(#arrowhead)");

            // Add condition marker ❓ at midpoint of conditional edges
            if (edge.hasCondition && points.length >= 2) {{
                const midIdx = Math.floor(points.length / 2);
                const midPoint = points[midIdx];
                edgeGroup.append("text")
                    .attr("class", "edge-condition-marker")
                    .attr("x", midPoint.x + 5)
                    .attr("y", midPoint.y - 5)
                    .text("❓");
            }}
        }});

        // Draw nodes
        const nodeGroup = g.append("g").attr("class", "nodes");

        dagreGraph.nodes().forEach(nodeId => {{
            const node = dagreGraph.node(nodeId);
            if (!node) return;

            const nodeG = nodeGroup.append("g")
                .attr("class", `node ${{node.type || 'npc'}}`)
                .attr("transform", `translate(${{node.x - nodeWidth/2}}, ${{node.y - nodeHeight/2}})`)
                .attr("data-id", node.id)
                .style("cursor", "pointer");

            // Node rectangle - apply Parley speaker colors (#230)
            const rect = nodeG.append("rect")
                .attr("width", nodeWidth)
                .attr("height", nodeHeight)
                .attr("rx", 6)
                .attr("ry", 6);

            // Apply Parley color scheme for NPC and PC nodes
            if (node.type === "npc" || node.type === "pc") {{
                const nodeColor = getSpeakerColor(node.type, node.speaker || "");
                if (nodeColor) {{
                    rect.style("fill", nodeColor);
                    // Lighter stroke
                    const strokeColor = d3.color(nodeColor);
                    if (strokeColor) {{
                        rect.style("stroke", strokeColor.brighter(0.5));
                    }}
                }}
            }}

            // Type label with speaker tag (#230)
            let typeLabel = (node.type || "npc").toUpperCase();
            if (node.type === "npc" && node.speaker) {{
                typeLabel = node.speaker.substring(0, 12);
                if (node.speaker.length > 12) typeLabel += "…";
            }}

            nodeG.append("text")
                .attr("class", node.speaker ? "speaker-tag" : "node-type")
                .attr("x", 8)
                .attr("y", 14)
                .text(typeLabel);

            // Script indicators (#231)
            const indicators = getScriptIndicators(node);
            if (indicators) {{
                nodeG.append("text")
                    .attr("class", "script-indicator")
                    .attr("x", nodeWidth - 8)
                    .attr("y", 14)
                    .attr("text-anchor", "end")
                    .text(indicators);
            }}

            // Node text (truncated)
            const text = node.text || node.id;
            const truncated = text.length > 22 ? text.substring(0, 22) + "…" : text;
            nodeG.append("text")
                .attr("x", 8)
                .attr("y", 36)
                .text(truncated);

            // Link target indicator for link nodes (#232)
            if (node.is_link && node.link_target) {{
                nodeG.append("text")
                    .attr("x", 8)
                    .attr("y", 52)
                    .attr("class", "node-type")
                    .text(`→ ${{node.link_target}}`);
            }}

            // Click handler (Epic 40 Phase 3 / #234)
            nodeG.on("click", function(event) {{
                // Remove previous selection and target highlights
                d3.selectAll(".node").classed("selected", false);
                d3.selectAll(".node").classed("target-highlight", false);

                // Select clicked node
                d3.select(this).classed("selected", true);

                // Determine what ID to send to Parley for tree selection
                let selectionId = node.id;

                // If this is a link node, also highlight the target node (#234)
                // Send the TARGET id to Parley (for tree selection), not the link id
                if (node.is_link && node.link_target) {{
                    d3.selectAll(".node").each(function() {{
                        const el = d3.select(this);
                        if (el.attr("data-id") === node.link_target) {{
                            el.classed("target-highlight", true);
                        }}
                    }});
                    // Send target ID so Parley selects the actual node in tree
                    // This prevents the plugin from detecting a mismatch and re-rendering
                    selectionId = node.link_target;
                    console.log("[Flowchart] Link node clicked:", node.id, "-> selecting target:", node.link_target);
                }} else {{
                    console.log("[Flowchart] Node clicked:", node.id);
                }}

                // Notify Parley via custom URL scheme (BeforeNavigate interception)
                window.location.href = "parley://selectnode/" + encodeURIComponent(selectionId);
            }});
        }});

        // Function to select a node by ID (called when Parley selection changes)
        function selectNodeById(nodeId) {{
            if (!nodeId) return;

            // Clear all highlights
            d3.selectAll(".node").classed("selected", false);
            d3.selectAll(".node").classed("target-highlight", false);

            // Find the node data to check if it's a link
            const nodeData = nodes.find(n => n.id === nodeId);

            d3.selectAll(".node").each(function() {{
                const el = d3.select(this);
                if (el.attr("data-id") === nodeId) {{
                    el.classed("selected", true);
                    // Scroll node into view
                    scrollToNode(el);
                }}
            }});

            // If selecting a link node, also highlight its target (#234)
            if (nodeData && nodeData.is_link && nodeData.link_target) {{
                d3.selectAll(".node").each(function() {{
                    const el = d3.select(this);
                    if (el.attr("data-id") === nodeData.link_target) {{
                        el.classed("target-highlight", true);
                    }}
                }});
            }}
        }}

        // Scroll the SVG to center on a node
        function scrollToNode(nodeEl) {{
            try {{
                const node = nodeEl.node();
                if (!node) return;
                const bbox = node.getBBox();
                const transform = nodeEl.attr("transform");
                // Extract translate values
                const match = /translate\(([^,]+),\s*([^)]+)\)/.exec(transform);
                if (match) {{
                    const tx = parseFloat(match[1]) + bbox.width / 2;
                    const ty = parseFloat(match[2]) + bbox.height / 2;
                    // Center the view on this node
                    const scale = d3.zoomTransform(svg.node()).k || 1;
                    const newX = width / 2 - tx * scale;
                    const newY = height / 2 - ty * scale;
                    svg.transition().duration(300).call(
                        zoom.transform,
                        d3.zoomIdentity.translate(newX, newY).scale(scale)
                    );
                }}
            }} catch (e) {{
                console.log("scrollToNode error:", e);
            }}
        }}

        // Apply initial selection if provided
        // Skip fitToScreen if selecting a node - just scroll to it instead
        if (initialSelectedNodeId) {{
            setTimeout(() => {{
                selectNodeById(initialSelectedNodeId);
            }}, 100);
        }} else {{
            // Initial fit only when no selection (first load)
            setTimeout(fitToScreen, 100);
        }}

        // Helper functions
        function truncateText(text, maxLen) {{
            if (!text) return "";
            return text.length > maxLen ? text.substring(0, maxLen) + "…" : text;
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
            if (bounds.width === 0 || bounds.height === 0) return;

            const fullWidth = width;
            const fullHeight = height;
            const bWidth = bounds.width;
            const bHeight = bounds.height;
            const scale = 0.9 * Math.min(fullWidth / bWidth, fullHeight / bHeight);
            const tx = (fullWidth - scale * bWidth) / 2 - scale * bounds.x;
            const ty = (fullHeight - scale * bHeight) / 2 - scale * bounds.y;
            svg.transition().duration(500).call(zoom.transform, d3.zoomIdentity.translate(tx, ty).scale(scale));
        }}

        // Handle window resize
        window.addEventListener("resize", () => {{
            width = window.innerWidth;
            height = window.innerHeight;
            svg.attr("width", width).attr("height", height);
        }});
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
        self._last_structure_hash: Optional[str] = None
        self._last_selected_node_id: Optional[str] = None  # Track selection for bidirectional sync (#234)

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
                self._on_dialog_changed()
            else:
                print("[Flowchart] No dialog loaded")
                self._update_panel_placeholder()
        elif dialog_id:
            # Same dialog - check if content changed
            structure = self.client.get_dialog_structure()
            if structure.get("success", False):
                # Compute simple hash of structure
                import hashlib
                struct_json = json.dumps(structure, sort_keys=True)
                current_hash = hashlib.md5(struct_json.encode()).hexdigest()

                if current_hash != self._last_structure_hash:
                    print(f"[Flowchart] Dialog content changed, re-rendering")
                    self._last_structure_hash = current_hash
                    self._render_flowchart_with_data(structure)

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
            print(f"[Flowchart] Got {len(dialog_data['nodes'])} nodes from Parley API")

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

        # Generate HTML with dialog data
        html = FLOWCHART_HTML_TEMPLATE.format(
            dialog_name=self.current_dialog_name or "Untitled",
            node_count=len(dialog_data.get("nodes", [])),
            dialog_data=json.dumps(dialog_data),
            speaker_colors=json.dumps(speaker_colors),
            theme=theme,
            selected_node_id=json.dumps(selected_node_id),  # null or "entry_0" etc
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
                print(f"[Flowchart] Got speaker colors from Parley: PC={colors['_pc']}, Owner={colors['_owner']}, {len(parley_colors.get('speaker_colors', {}))} named speakers")
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

    def _on_flowchart_node_clicked(self, node_id: str):
        """
        Handle node click in the flowchart (Epic 40 Phase 3 / #234).
        Calls Parley's SelectNode API to sync selection back to tree view.

        Args:
            node_id: ID of the clicked node
        """
        if not self.client:
            return

        print(f"[Flowchart] User clicked node: {node_id}")

        # Request Parley to select this node in the tree view
        success, error = self.client.select_node(node_id)
        if success:
            print(f"[Flowchart] Successfully requested selection of {node_id}")
            self._last_selected_node_id = node_id  # Update tracking to avoid feedback loop
        else:
            print(f"[Flowchart] Failed to select node: {error}")

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

                # Check for selection changes (bidirectional sync #234)
                # NOTE: We only track selection, we do NOT re-render on selection changes.
                # Re-rendering would wipe out the JavaScript highlight state set by flowchart clicks.
                # The flowchart JS handles its own highlighting when user clicks in flowchart.
                # Tree→Flow sync would require ExecuteJavaScript which isn't available.
                if self.client:
                    node_id, node_text = self.client.get_selected_node()
                    if node_id and node_id != self._last_selected_node_id:
                        self._last_selected_node_id = node_id
                        print(f"[Flowchart] Selection changed to: {node_id} (tracking only, no re-render)")

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
