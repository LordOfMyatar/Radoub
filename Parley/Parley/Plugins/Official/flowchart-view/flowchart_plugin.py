"""
Flowchart View Plugin for Parley

ChatMapper-style flowchart visualization for dialog trees.
Displays dialog nodes as colored boxes with connecting lines,
supporting zoom, pan, auto-layout, and export to PNG/SVG.

Epic 3: Advanced Visualization (Epic #40)
Phase 1: Foundation (#223-#227)
Phase 2: Layout and Visual Design (#228-#232)
"""

import time
import json
import threading
from typing import Optional, Dict, Any, List
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
        .node.selected rect {{
            stroke: #f1c40f;
            stroke-width: 3px;
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
        /* Edge styles */
        .edgePath path {{
            stroke: var(--link-color, #555);
            stroke-width: 2px;
            fill: none;
        }}
        /* Conditional edge styling (#231) */
        .edgePath.has-condition path {{
            stroke: var(--link-condition, #e74c3c);
            stroke-dasharray: 5,3;
        }}
        /* Link-to-link edges (#232) - dotted lines */
        .edgePath.to-link path {{
            stroke-dasharray: 3,3;
            opacity: 0.6;
        }}
        .edgeLabel {{
            font-size: 10px;
            fill: var(--text-secondary, #95a5a6);
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
        function getSpeakerColor(speaker) {{
            if (!speaker || speaker === "") return null;
            if (speakerColors[speaker]) return speakerColors[speaker];
            // Generate consistent color from speaker name
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

            // Create path
            const path = edgeGroup.append("path")
                .attr("class", edgeClass)
                .attr("d", () => {{
                    const points = edge.points || [
                        {{ x: sourceNode.x, y: sourceNode.y + nodeHeight/2 }},
                        {{ x: targetNode.x, y: targetNode.y - nodeHeight/2 }}
                    ];
                    return d3.line()
                        .x(d => d.x)
                        .y(d => d.y)
                        .curve(d3.curveBasis)(points);
                }})
                .attr("marker-end", edge.hasCondition ? "url(#arrowhead-condition)" : "url(#arrowhead)");
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

            // Node rectangle - apply speaker color for NPC (#230)
            const rect = nodeG.append("rect")
                .attr("width", nodeWidth)
                .attr("height", nodeHeight)
                .attr("rx", 6)
                .attr("ry", 6);

            // Apply speaker-specific color for NPC nodes
            if (node.type === "npc" && node.speaker) {{
                const speakerColor = getSpeakerColor(node.speaker);
                if (speakerColor) {{
                    rect.style("fill", speakerColor);
                    // Lighter stroke
                    rect.style("stroke", d3.color(speakerColor).brighter(0.5));
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

            // Click handler
            nodeG.on("click", function(event) {{
                // Remove previous selection
                d3.selectAll(".node").classed("selected", false);
                // Select clicked node
                d3.select(this).classed("selected", true);
                // Notify parent (if bridge exists)
                if (window.notifyNodeSelected) {{
                    window.notifyNodeSelected(node.id);
                }}
            }});
        }});

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

        // Initial fit after render
        setTimeout(fitToScreen, 100);
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
        Render the current dialog as a D3.js + dagre.js flowchart.

        Generates HTML with embedded dialog data for Sugiyama layout visualization.
        Phase 2: Includes theme awareness, speaker colors, script indicators.
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

        # Extract unique speakers and assign colors (#230)
        speaker_colors = self._generate_speaker_colors(dialog_data.get("nodes", []))

        # Determine theme (#229) - default to dark for now
        # TODO: Get actual theme from Parley settings in Phase 3
        theme = "dark"

        # Generate HTML with dialog data
        html = FLOWCHART_HTML_TEMPLATE.format(
            dialog_name=self.current_dialog_name or "Untitled",
            node_count=len(dialog_data.get("nodes", [])),
            dialog_data=json.dumps(dialog_data),
            speaker_colors=json.dumps(speaker_colors),
            theme=theme,
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
        Generate consistent colors for each unique speaker (#230).

        Uses predefined palette for common speakers, hash-based colors for others.

        Args:
            nodes: List of dialog nodes

        Returns:
            Dict mapping speaker names to hex colors
        """
        # Predefined palette for common speakers
        palette = [
            "#2d5a27",  # Green (default NPC)
            "#8e4585",  # Purple
            "#5a4d2d",  # Brown
            "#2d4a5a",  # Teal
            "#5a2d2d",  # Maroon
            "#4a5a2d",  # Olive
            "#5a5a2d",  # Gold
            "#3d5a4d",  # Forest
        ]

        # Collect unique speakers
        speakers = set()
        for node in nodes:
            speaker = node.get("speaker", "")
            if speaker and node.get("type") == "npc":
                speakers.add(speaker)

        # Assign colors
        colors = {}
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
