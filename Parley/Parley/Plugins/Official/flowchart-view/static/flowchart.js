/**
 * Flowchart View Plugin JavaScript
 * Epic #40: Advanced Visualization
 *
 * D3.js + dagre.js flowchart rendering for dialog trees.
 * Supports zoom, pan, auto-layout, and bidirectional selection sync.
 */

// These globals are set by the HTML template before this script loads
// dialogData, speakerColors, speakerShapes, initialSelectedNodeId, syncSelectionEnabled, maxTextLength

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

// Node dimensions - fixed width with word wrap
const textLimit = (typeof maxTextLength !== 'undefined') ? maxTextLength : 64;
const nodeWidth = 240;  // Fixed width, text wraps
const lineHeight = 14;  // Line height for wrapped text
const charsPerLine = 38;  // ~6px per char at 11px font, 240px - 16px padding

/**
 * Wrap text to multiple lines based on character limit per line
 * Returns array of line strings
 */
function wrapText(text, maxCharsPerLine) {
    if (!text) return [""];
    const words = text.split(/\s+/);
    const lines = [];
    let currentLine = "";

    for (const word of words) {
        if (currentLine.length === 0) {
            currentLine = word;
        } else if (currentLine.length + 1 + word.length <= maxCharsPerLine) {
            currentLine += " " + word;
        } else {
            lines.push(currentLine);
            currentLine = word;
        }
    }
    if (currentLine) lines.push(currentLine);

    return lines.length > 0 ? lines : [""];
}

/**
 * Calculate node height based on text content
 * Header row (20px) + text lines + optional link target row
 */
function calcNodeHeight(node) {
    const text = node.text || node.id;
    const lines = wrapText(text, charsPerLine);
    const maxLines = Math.ceil(textLimit / charsPerLine);  // Cap based on setting
    const displayLines = Math.min(lines.length, maxLines);

    let height = 24;  // Header row
    height += displayLines * lineHeight;  // Text lines
    height += 8;  // Bottom padding

    if (node.is_link && node.link_target) {
        height += lineHeight;  // Link target row
    }

    return Math.max(50, height);  // Minimum height
}

// Add nodes to dagre with dynamic heights
nodes.forEach(node => {
    const height = calcNodeHeight(node);
    dagreGraph.setNode(node.id, {
        width: nodeWidth,
        height: height,
        nodeHeight: height,  // Store for rendering
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
 * Shape name to SVG path mapping
 * Shapes scaled to 10x10 viewport, centered at (5,5)
 */
const shapePaths = {
    "Circle": "M 5,1 A 4,4 0 1,1 5,9 A 4,4 0 1,1 5,1 Z",
    "Square": "M 1,1 L 9,1 L 9,9 L 1,9 Z",
    "Triangle": "M 5,1 L 9,9 L 1,9 Z",
    "Diamond": "M 5,1 L 9,5 L 5,9 L 1,5 Z",
    "Pentagon": "M 5,1 L 9,4 L 7.5,9 L 2.5,9 L 1,4 Z",
    "Star": "M 5,1 L 6,4 L 9,4 L 6.5,6 L 7.5,9 L 5,7 L 2.5,9 L 3.5,6 L 1,4 L 4,4 Z"
};

/**
 * Get speaker shape SVG path data
 * Uses shapes from Parley API (speakerShapes), falling back to defaults
 * Matches SpeakerVisualHelper shapes from Parley tree view
 */
function getSpeakerShape(nodeType, speaker) {
    // PC nodes - use API shape or default to Circle
    if (nodeType === "pc") {
        const shapeName = speakerShapes["_pc"] || "Circle";
        return shapePaths[shapeName] || shapePaths["Circle"];
    }
    // Owner (empty speaker on NPC) - use API shape or default to Square
    if (nodeType === "npc" && (!speaker || speaker === "")) {
        const shapeName = speakerShapes["_owner"] || "Square";
        return shapePaths[shapeName] || shapePaths["Square"];
    }
    // Named NPCs - check API shapes first, fall back to hash-based
    if (nodeType === "npc" && speaker) {
        if (speakerShapes[speaker]) {
            return shapePaths[speakerShapes[speaker]] || shapePaths["Triangle"];
        }
        // Fallback: hash-based shape assignment
        const fallbackShapes = ["Triangle", "Diamond", "Pentagon", "Star"];
        let hash = 0;
        for (let i = 0; i < speaker.length; i++) {
            hash = speaker.charCodeAt(i) + ((hash << 5) - hash);
        }
        return shapePaths[fallbackShapes[Math.abs(hash) % fallbackShapes.length]];
    }
    // Root/link nodes - no shape
    return null;
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

    // Get edge points for path (use stored node heights)
    const srcHeight = sourceNode.nodeHeight || 60;
    const tgtHeight = targetNode.nodeHeight || 60;
    const points = edge.points || [
        { x: sourceNode.x, y: sourceNode.y + srcHeight/2 },
        { x: targetNode.x, y: targetNode.y - tgtHeight/2 }
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

    const thisHeight = node.nodeHeight || 60;

    const nodeG = nodeGroup.append("g")
        .attr("class", `node ${node.type || 'npc'}`)
        .attr("transform", `translate(${node.x - nodeWidth/2}, ${node.y - thisHeight/2})`)
        .attr("data-id", node.id)
        .style("cursor", "pointer");

    // Get speaker color and shape for this node
    const nodeColor = getSpeakerColor(node.type, node.speaker || "");
    const shapePath = getSpeakerShape(node.type, node.speaker || "");

    // Node rectangle - thick colored border with theme background
    // Border color identifies speaker/type, fill uses theme for readable text
    const rect = nodeG.append("rect")
        .attr("width", nodeWidth)
        .attr("height", thisHeight)
        .attr("rx", 6)
        .attr("ry", 6);

    // Apply speaker color as stroke (border) for NPC and PC nodes
    if ((node.type === "npc" || node.type === "pc") && nodeColor) {
        rect.style("stroke", nodeColor);
    }

    // Left side: Shape icon + Type label (PC/NPC or speaker name)
    // NWN tags are max 32 chars; show up to 20 to leave room for shape + script icons

    let labelX = 8;  // Default text position

    // Draw shape icon if available (PC, NPC, named speakers)
    if (shapePath) {
        nodeG.append("path")
            .attr("class", "speaker-shape")
            .attr("d", shapePath)
            .attr("transform", "translate(4, 4)")  // Position at (4,4) so shape is at ~(9,9) center
            .attr("fill", nodeColor)
            .attr("stroke", "none");
        labelX = 18;  // Move text right to make room for shape
    }

    let typeLabel = (node.type || "npc").toUpperCase();
    if (node.type === "npc" && node.speaker) {
        typeLabel = node.speaker.substring(0, 20);
        if (node.speaker.length > 20) typeLabel += "…";
    }

    nodeG.append("text")
        .attr("class", node.speaker ? "speaker-tag" : "node-type")
        .attr("x", labelX)
        .attr("y", 14)
        .text(typeLabel);

    // Right side: Script indicators - Condition (❓) then Action (⚡)
    // Layout: ... [❓] [⚡] |  (action is rightmost)
    let rightX = nodeWidth - 8;

    // Action indicator (rightmost)
    if (node.has_action) {
        nodeG.append("text")
            .attr("class", "script-indicator action")
            .attr("x", rightX)
            .attr("y", 14)
            .attr("text-anchor", "end")
            .text("⚡");
        rightX -= 16;  // Move left for next indicator
    }

    // Condition indicator (left of action)
    if (node.has_condition) {
        nodeG.append("text")
            .attr("class", "script-indicator condition")
            .attr("x", rightX)
            .attr("y", 14)
            .attr("text-anchor", "end")
            .text("❓");
    }

    // Node text with word wrap
    const text = node.text || node.id;
    const textLines = wrapText(text, charsPerLine);
    const maxLines = Math.ceil(textLimit / charsPerLine);
    const displayLines = textLines.slice(0, maxLines);

    // Add ellipsis to last line if truncated
    if (textLines.length > maxLines && displayLines.length > 0) {
        const lastIdx = displayLines.length - 1;
        displayLines[lastIdx] = displayLines[lastIdx].substring(0, charsPerLine - 1) + "…";
    }

    const textEl = nodeG.append("text")
        .attr("x", 8)
        .attr("y", 28);  // First line position

    displayLines.forEach((line, i) => {
        textEl.append("tspan")
            .attr("x", 8)
            .attr("dy", i === 0 ? 0 : lineHeight)
            .text(line);
    });

    // Link target indicator for link nodes (#232)
    if (node.is_link && node.link_target) {
        const linkY = 28 + (displayLines.length * lineHeight) + 4;
        nodeG.append("text")
            .attr("x", 8)
            .attr("y", linkY)
            .attr("class", "node-type")
            .text(`→ ${node.link_target}`);
    }

    // Hover tooltip showing full text
    const fullText = node.text || node.id;
    let tooltipText = fullText;
    if (node.speaker) {
        tooltipText = `[${node.speaker}] ${fullText}`;
    }
    if (node.has_action && node.action_script) {
        tooltipText += `\n⚡ Action: ${node.action_script}`;
    }
    if (node.has_condition && node.condition_script) {
        tooltipText += `\n❓ Condition: ${node.condition_script}`;
    }
    nodeG.append("title").text(tooltipText);

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

/**
 * Export flowchart as SVG (#239)
 * Serializes the SVG element and sends to Parley for file saving
 */
function exportSVG() {
    try {
        // Clone the SVG to avoid modifying the displayed version
        const svgElement = document.getElementById("flowchart");
        const svgClone = svgElement.cloneNode(true);

        // Apply current transform to make the export show the full graph
        const graphContainer = svgClone.querySelector(".graph-container");
        if (graphContainer) {
            // Reset transform to show full graph at origin
            graphContainer.removeAttribute("transform");
        }

        // Calculate bounding box of content
        const bounds = g.node().getBBox();
        const padding = 20;
        const exportWidth = bounds.width + padding * 2;
        const exportHeight = bounds.height + padding * 2;

        // Update SVG dimensions and viewBox for export
        svgClone.setAttribute("width", exportWidth);
        svgClone.setAttribute("height", exportHeight);
        svgClone.setAttribute("viewBox", `${bounds.x - padding} ${bounds.y - padding} ${exportWidth} ${exportHeight}`);

        // Add xmlns for standalone SVG
        svgClone.setAttribute("xmlns", "http://www.w3.org/2000/svg");

        // Embed CSS styles directly into SVG for standalone export
        const styleElement = document.createElement("style");
        styleElement.textContent = getEmbeddedStyles();
        svgClone.insertBefore(styleElement, svgClone.firstChild);

        // Serialize to string
        const serializer = new XMLSerializer();
        const svgString = serializer.serializeToString(svgClone);

        // Encode as base64 and send to Parley
        const base64Data = btoa(unescape(encodeURIComponent(svgString)));

        console.log("[Flowchart] Exporting SVG, size:", svgString.length, "bytes");
        sendExportToParley("svg", base64Data);

    } catch (e) {
        console.error("[Flowchart] SVG export failed:", e);
        alert("SVG export failed: " + e.message);
    }
}

/**
 * Export flowchart as PNG (#238)
 * Renders the SVG to a canvas and converts to PNG
 */
function exportPNG() {
    try {
        const svgElement = document.getElementById("flowchart");

        // Calculate content bounds
        const bounds = g.node().getBBox();
        const padding = 20;
        const exportWidth = Math.ceil(bounds.width + padding * 2);
        const exportHeight = Math.ceil(bounds.height + padding * 2);

        // Create a temporary SVG with embedded styles
        const svgClone = svgElement.cloneNode(true);
        const graphContainer = svgClone.querySelector(".graph-container");
        if (graphContainer) {
            // Translate to origin
            graphContainer.setAttribute("transform", `translate(${padding - bounds.x}, ${padding - bounds.y})`);
        }

        svgClone.setAttribute("width", exportWidth);
        svgClone.setAttribute("height", exportHeight);
        svgClone.setAttribute("xmlns", "http://www.w3.org/2000/svg");

        // Embed styles
        const styleElement = document.createElement("style");
        styleElement.textContent = getEmbeddedStyles();
        svgClone.insertBefore(styleElement, svgClone.firstChild);

        // Serialize SVG
        const serializer = new XMLSerializer();
        const svgString = serializer.serializeToString(svgClone);

        // Create an image from SVG
        const img = new Image();
        const svgBlob = new Blob([svgString], {type: "image/svg+xml;charset=utf-8"});
        const url = URL.createObjectURL(svgBlob);

        img.onload = function() {
            // Create canvas and draw image
            const canvas = document.createElement("canvas");
            // Use 2x scale for better quality
            const scale = 2;
            canvas.width = exportWidth * scale;
            canvas.height = exportHeight * scale;

            const ctx = canvas.getContext("2d");
            ctx.scale(scale, scale);

            // Fill background based on theme
            const isDark = document.body.classList.contains("dark");
            ctx.fillStyle = isDark ? "#1e1e1e" : "#f5f5f5";
            ctx.fillRect(0, 0, exportWidth, exportHeight);

            // Draw the SVG
            ctx.drawImage(img, 0, 0, exportWidth, exportHeight);

            // Convert to PNG base64
            const pngDataUrl = canvas.toDataURL("image/png");
            // Remove "data:image/png;base64," prefix
            const base64Data = pngDataUrl.substring("data:image/png;base64,".length);

            console.log("[Flowchart] Exporting PNG, dimensions:", exportWidth, "x", exportHeight);
            sendExportToParley("png", base64Data);

            // Cleanup
            URL.revokeObjectURL(url);
        };

        img.onerror = function(e) {
            console.error("[Flowchart] PNG image load failed:", e);
            URL.revokeObjectURL(url);
            alert("PNG export failed: Could not render image");
        };

        img.src = url;

    } catch (e) {
        console.error("[Flowchart] PNG export failed:", e);
        alert("PNG export failed: " + e.message);
    }
}

/**
 * Get embedded CSS styles for standalone SVG/PNG export
 * Inlines CSS variables with their current values
 * Uses thick colored borders with theme background for readable text
 */
function getEmbeddedStyles() {
    const isDark = document.body.classList.contains("dark");
    const textPrimary = isDark ? "#ecf0f1" : "#2c3e50";
    const textSecondary = isDark ? "#95a5a6" : "#7f8c8d";
    const linkColor = isDark ? "#555" : "#95a5a6";
    const linkCondition = isDark ? "#e74c3c" : "#c0392b";
    const nodeBg = isDark ? "#2a2a2a" : "#ffffff";
    const nodeBgAlt = isDark ? "#333" : "#f0f0f0";

    return `
        .node rect { stroke-width: 4px; fill: ${nodeBg}; }
        .node.npc rect { stroke: #4a9c3f; }
        .node.pc rect { stroke: #3498db; }
        .node.root rect { stroke: #9b59b6; fill: ${nodeBgAlt}; }
        .node.link rect { stroke: #888; stroke-width: 3px; stroke-dasharray: 4,2; fill: ${nodeBgAlt}; opacity: 0.85; }
        .node text { fill: ${textPrimary}; font-size: 11px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        .node .node-type { font-size: 9px; fill: ${textSecondary}; }
        .node .speaker-tag { font-size: 9px; font-weight: bold; }
        .node .speaker-shape { pointer-events: none; }
        .node .script-indicator { font-size: 12px; }
        .node .script-indicator.action { fill: #f39c12; }
        .node .script-indicator.condition { fill: #e74c3c; }
        .edge-condition-marker { font-size: 14px; fill: #e74c3c; }
        path.edgePath { stroke: ${linkColor}; stroke-width: 2px; fill: none; }
        path.edgePath.has-condition { stroke: ${linkCondition}; stroke-dasharray: 5,3; }
        path.edgePath.to-link { stroke-dasharray: 3,3; opacity: 0.6; }
        marker path { fill: ${linkColor}; }
    `;
}

/**
 * Send export data to Parley via custom URL scheme
 * Uses chunked transfer for large data (base64 strings > 32KB can fail in URL)
 */
function sendExportToParley(format, base64Data) {
    // For large exports, we need to chunk the data
    // WebView URL length limits vary, so we use a reasonable chunk size
    const CHUNK_SIZE = 20000;  // 20KB chunks

    if (base64Data.length <= CHUNK_SIZE) {
        // Small enough to send in one request
        window.location.href = `parley://export/${format}/${base64Data}`;
    } else {
        // Need to chunk the data
        const totalChunks = Math.ceil(base64Data.length / CHUNK_SIZE);
        const exportId = Date.now().toString(36);  // Unique ID for this export

        console.log(`[Flowchart] Large export: ${base64Data.length} bytes, ${totalChunks} chunks`);

        // Send chunks with small delays to avoid overwhelming the handler
        let chunkIndex = 0;

        function sendNextChunk() {
            if (chunkIndex >= totalChunks) {
                // Send finalize signal
                window.location.href = `parley://export-done/${format}/${exportId}`;
                return;
            }

            const start = chunkIndex * CHUNK_SIZE;
            const end = Math.min(start + CHUNK_SIZE, base64Data.length);
            const chunk = base64Data.substring(start, end);

            // Format: parley://export-chunk/{format}/{exportId}/{chunkIndex}/{totalChunks}/{data}
            sendSettingToParley(`parley://export-chunk/${format}/${exportId}/${chunkIndex}/${totalChunks}/${chunk}`);

            chunkIndex++;
            setTimeout(sendNextChunk, 50);  // Small delay between chunks
        }

        sendNextChunk();
    }
}
