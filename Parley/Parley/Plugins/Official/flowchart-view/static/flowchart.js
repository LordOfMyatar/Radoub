/**
 * Flowchart View Plugin JavaScript
 * Epic #40: Advanced Visualization
 *
 * D3.js + dagre.js flowchart rendering for dialog trees.
 * Supports zoom, pan, auto-layout, and bidirectional selection sync.
 */

// These globals are set by the HTML template before this script loads
// dialogData, speakerColors, initialSelectedNodeId, syncSelectionEnabled

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
dagreGraph.setGraph({
    rankdir: "TB",     // Top to bottom (Entry at top)
    nodesep: 60,       // Horizontal separation
    ranksep: 80,       // Vertical separation between ranks
    marginx: 20,
    marginy: 20
});
dagreGraph.setDefaultEdgeLabel(() => ({}));

// Node dimensions
const nodeWidth = 160;
const nodeHeight = 60;

// Add nodes to dagre
nodes.forEach(node => {
    dagreGraph.setNode(node.id, {
        width: nodeWidth,
        height: nodeHeight,
        ...node
    });
});

// Add edges to dagre
links.forEach(link => {
    dagreGraph.setEdge(link.source, link.target, {
        hasCondition: link.has_condition || false,
        conditionScript: link.condition_script || ""
    });
});

// Run the layout algorithm
dagre.layout(dagreGraph);

/**
 * Get speaker color for NPC nodes (#230)
 * Uses colors from Parley settings via GetSpeakerColors API
 */
function getSpeakerColor(nodeType, speaker) {
    // PC nodes use the PC color
    if (nodeType === "pc") {
        return speakerColors["_pc"] || "#4FC3F7";
    }
    // Named NPC speakers - check API colors first
    if (speaker && speakerColors[speaker]) {
        return speakerColors[speaker];
    }
    // Owner/default NPC (empty speaker) uses owner color
    if (!speaker || speaker === "") {
        return speakerColors["_owner"] || "#FF8A65";
    }
    // Fallback for unknown speakers - generate from hash
    let hash = 0;
    for (let i = 0; i < speaker.length; i++) {
        hash = speaker.charCodeAt(i) + ((hash << 5) - hash);
    }
    const hue = Math.abs(hash % 360);
    return `hsl(${hue}, 50%, 35%)`;
}

/**
 * Build script indicator text (#231)
 */
function getScriptIndicators(node) {
    let indicators = [];
    if (node.has_condition) indicators.push("❓");  // Only for link nodes
    return indicators.join(" ");
}

// Draw edges first (so they appear behind nodes)
const edgeGroup = g.append("g").attr("class", "edges");

dagreGraph.edges().forEach(e => {
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
        { x: sourceNode.x, y: sourceNode.y + nodeHeight/2 },
        { x: targetNode.x, y: targetNode.y - nodeHeight/2 }
    ];

    // Create path
    edgeGroup.append("path")
        .attr("class", edgeClass)
        .attr("d", d3.line()
            .x(d => d.x)
            .y(d => d.y)
            .curve(d3.curveBasis)(points))
        .attr("marker-end", edge.hasCondition ? "url(#arrowhead-condition)" : "url(#arrowhead)");

    // Add condition marker at midpoint of conditional edges
    if (edge.hasCondition && points.length >= 2) {
        const midIdx = Math.floor(points.length / 2);
        const midPoint = points[midIdx];
        edgeGroup.append("text")
            .attr("class", "edge-condition-marker")
            .attr("x", midPoint.x + 5)
            .attr("y", midPoint.y - 5)
            .text("❓");
    }
});

// Draw nodes
const nodeGroup = g.append("g").attr("class", "nodes");

dagreGraph.nodes().forEach(nodeId => {
    const node = dagreGraph.node(nodeId);
    if (!node) return;

    const nodeG = nodeGroup.append("g")
        .attr("class", `node ${node.type || 'npc'}`)
        .attr("transform", `translate(${node.x - nodeWidth/2}, ${node.y - nodeHeight/2})`)
        .attr("data-id", node.id)
        .style("cursor", "pointer");

    // Node rectangle - apply Parley speaker colors (#230)
    const rect = nodeG.append("rect")
        .attr("width", nodeWidth)
        .attr("height", nodeHeight)
        .attr("rx", 6)
        .attr("ry", 6);

    // Apply Parley color scheme for NPC and PC nodes
    if (node.type === "npc" || node.type === "pc") {
        const nodeColor = getSpeakerColor(node.type, node.speaker || "");
        if (nodeColor) {
            rect.style("fill", nodeColor);
            // Lighter stroke
            const strokeColor = d3.color(nodeColor);
            if (strokeColor) {
                rect.style("stroke", strokeColor.brighter(0.5));
            }
        }
    }

    // Type label with speaker tag (#230)
    let typeLabel = (node.type || "npc").toUpperCase();
    if (node.type === "npc" && node.speaker) {
        typeLabel = node.speaker.substring(0, 12);
        if (node.speaker.length > 12) typeLabel += "…";
    }

    nodeG.append("text")
        .attr("class", node.speaker ? "speaker-tag" : "node-type")
        .attr("x", 8)
        .attr("y", 14)
        .text(typeLabel);

    // Script indicators (#231)
    const indicators = getScriptIndicators(node);
    if (indicators) {
        nodeG.append("text")
            .attr("class", "script-indicator")
            .attr("x", nodeWidth - 8)
            .attr("y", 14)
            .attr("text-anchor", "end")
            .text(indicators);
    }

    // Node text (truncated)
    const text = node.text || node.id;
    const truncated = text.length > 22 ? text.substring(0, 22) + "…" : text;
    nodeG.append("text")
        .attr("x", 8)
        .attr("y", 36)
        .text(truncated);

    // Link target indicator for link nodes (#232)
    if (node.is_link && node.link_target) {
        nodeG.append("text")
            .attr("x", 8)
            .attr("y", 52)
            .attr("class", "node-type")
            .text(`→ ${node.link_target}`);
    }

    // Click handler (Epic 40 Phase 3 / #234)
    nodeG.on("click", function(event) {
        // Remove previous selection and target highlights
        d3.selectAll(".node").classed("selected", false);
        d3.selectAll(".node").classed("target-highlight", false);

        // Select clicked node
        d3.select(this).classed("selected", true);

        // If this is a link node, also highlight the target node (#234)
        if (node.is_link && node.link_target) {
            d3.selectAll(".node").each(function() {
                const el = d3.select(this);
                if (el.attr("data-id") === node.link_target) {
                    el.classed("target-highlight", true);
                }
            });
        }

        // Send node ID to Parley via custom URL scheme (if sync enabled)
        console.log("[Flowchart] Node clicked:", node.id);
        if (syncSelectionEnabled) {
            window.location.href = "parley://selectnode/" + encodeURIComponent(node.id);
        }
    });
});

// Draw action script markers above nodes (#235)
const markerGroup = g.append("g").attr("class", "markers");

dagreGraph.nodes().forEach(nodeId => {
    const node = dagreGraph.node(nodeId);
    if (!node || !node.has_action) return;

    // Position marker above the node, aligned to left edge
    markerGroup.append("text")
        .attr("class", "node-action-marker")
        .attr("x", node.x - nodeWidth/2 + 8)
        .attr("y", node.y - nodeHeight/2 - 8)
        .attr("text-anchor", "start")
        .text("⚡");
});

/**
 * Select a node by ID (called when Parley selection changes)
 * Exposed as window.selectNodeById for C# ExecuteScript calls (#234)
 * Respects syncSelectionEnabled setting (#235)
 */
window.selectNodeById = function(nodeId) {
    if (!nodeId) return;

    // If sync is disabled, ignore incoming selection requests (#235)
    if (!syncSelectionEnabled) return;

    // Clear all highlights
    d3.selectAll(".node").classed("selected", false);
    d3.selectAll(".node").classed("target-highlight", false);

    // Find the node data to check if it's a link
    const nodeData = nodes.find(n => n.id === nodeId);

    d3.selectAll(".node").each(function() {
        const el = d3.select(this);
        if (el.attr("data-id") === nodeId) {
            el.classed("selected", true);
            // Scroll node into view
            scrollToNode(el);
        }
    });

    // If selecting a link node, also highlight its target (#234)
    if (nodeData && nodeData.is_link && nodeData.link_target) {
        d3.selectAll(".node").each(function() {
            const el = d3.select(this);
            if (el.attr("data-id") === nodeData.link_target) {
                el.classed("target-highlight", true);
            }
        });
    }
};

/**
 * Scroll the SVG to center on a node
 */
function scrollToNode(nodeEl) {
    try {
        const node = nodeEl.node();
        if (!node) return;
        const bbox = node.getBBox();
        const transform = nodeEl.attr("transform");
        // Extract translate values
        const match = /translate\(([^,]+),\s*([^)]+)\)/.exec(transform);
        if (match) {
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
        }
    } catch (e) {
        console.log("scrollToNode error:", e);
    }
}

// Apply initial selection if provided
if (initialSelectedNodeId) {
    setTimeout(() => {
        window.selectNodeById(initialSelectedNodeId);
    }, 100);
} else {
    // Initial fit only when no selection (first load)
    setTimeout(fitToScreen, 100);
}

// Zoom controls
function zoomIn() {
    svg.transition().call(zoom.scaleBy, 1.3);
}

function zoomOut() {
    svg.transition().call(zoom.scaleBy, 0.7);
}

function resetZoom() {
    svg.transition().call(zoom.transform, d3.zoomIdentity);
}

function fitToScreen() {
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
}

// Manual refresh button (#235)
function requestRefresh() {
    window.location.href = "parley://refresh";
}

// Send setting to Parley via iframe navigation (more reliable than location.href) (#235)
// Uses a hidden iframe to avoid race conditions with page reloads
function sendSettingToParley(url) {
    // Create or reuse hidden iframe for settings communication
    let iframe = document.getElementById("parleySettingsFrame");
    if (!iframe) {
        iframe = document.createElement("iframe");
        iframe.id = "parleySettingsFrame";
        iframe.style.display = "none";
        document.body.appendChild(iframe);
    }
    iframe.src = url;
}

// Auto-refresh toggle (#235)
// autoRefreshEnabled is set by the HTML template
function toggleAutoRefresh() {
    const btn = document.getElementById("autoRefreshBtn");
    // Read current state from button to avoid desync
    autoRefreshEnabled = btn.textContent === "▶";  // Was paused, now enabling
    btn.textContent = autoRefreshEnabled ? "⏸" : "▶";
    btn.title = autoRefreshEnabled ? "Pause auto-refresh" : "Resume auto-refresh";
    sendSettingToParley("parley://autorefresh/" + (autoRefreshEnabled ? "on" : "off"));
}

// Selection sync toggle (#235)
// syncSelectionEnabled is set by the HTML template
// Called by onclick - fires immediately on click before page can reload
function toggleSyncSelection() {
    // Toggle state - onclick fires before checkbox.checked updates visually
    syncSelectionEnabled = !syncSelectionEnabled;
    console.log("[Flowchart] Sync selection:", syncSelectionEnabled ? "enabled" : "disabled");
    // Notify Parley of the change so it persists across re-renders (#235)
    sendSettingToParley("parley://synctoggle/" + (syncSelectionEnabled ? "on" : "off"));
}

// Handle window resize
window.addEventListener("resize", () => {
    width = window.innerWidth;
    height = window.innerHeight;
    svg.attr("width", width).attr("height", height);
});
