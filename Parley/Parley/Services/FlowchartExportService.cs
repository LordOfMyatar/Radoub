using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for exporting flowchart visualizations to various formats.
    /// Supports PNG and SVG export.
    /// </summary>
    public static class FlowchartExportService
    {
        /// <summary>
        /// Exports a control to PNG format.
        /// </summary>
        /// <param name="control">The control to export</param>
        /// <param name="filePath">The destination file path</param>
        /// <param name="dpi">The DPI for the export (default 96)</param>
        /// <returns>True if export succeeded</returns>
        public static async Task<bool> ExportToPngAsync(Control control, string filePath, double dpi = 96)
        {
            try
            {
                if (control == null || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot export: control has no size");
                    return false;
                }

                // Calculate pixel size based on DPI
                var scale = dpi / 96.0;
                var pixelWidth = (int)(control.Bounds.Width * scale);
                var pixelHeight = (int)(control.Bounds.Height * scale);

                // Create render target
                var pixelSize = new PixelSize(pixelWidth, pixelHeight);
                var renderBitmap = new RenderTargetBitmap(pixelSize, new Vector(dpi, dpi));

                // Render the control
                renderBitmap.Render(control);

                // Save to file
                await Task.Run(() =>
                {
                    using (var stream = File.Create(filePath))
                    {
                        renderBitmap.Save(stream);
                    }
                });

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Flowchart exported to PNG: {UnifiedLogger.SanitizePath(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"PNG export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Exports a flowchart graph to SVG format.
        /// This creates a simplified SVG representation of the graph structure.
        /// </summary>
        /// <param name="graph">The flowchart graph to export</param>
        /// <param name="filePath">The destination file path</param>
        /// <param name="fileName">The dialog filename for the title</param>
        /// <returns>True if export succeeded</returns>
        public static async Task<bool> ExportToSvgAsync(FlowchartGraph graph, string filePath, string? fileName = null)
        {
            try
            {
                if (graph == null || graph.IsEmpty)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot export: graph is empty");
                    return false;
                }

                var svg = GenerateSvg(graph, fileName);

                await File.WriteAllTextAsync(filePath, svg, Encoding.UTF8);

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Flowchart exported to SVG: {UnifiedLogger.SanitizePath(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"SVG export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates SVG content from a flowchart graph.
        /// Uses hierarchical layout matching the visual flowchart display.
        /// </summary>
        private static string GenerateSvg(FlowchartGraph graph, string? fileName)
        {
            var sb = new StringBuilder();

            // Basic layout parameters
            const int nodeWidth = 180;
            const int nodeHeight = 60;
            const int horizontalSpacing = 200;
            const int verticalSpacing = 100;
            const int padding = 50;

            // Build child lookup for hierarchical layout
            var childrenOf = new Dictionary<string, List<string>>();
            var hasParent = new HashSet<string>();

            foreach (var edge in graph.Edges)
            {
                if (!childrenOf.ContainsKey(edge.SourceId))
                    childrenOf[edge.SourceId] = new List<string>();
                childrenOf[edge.SourceId].Add(edge.TargetId);
                hasParent.Add(edge.TargetId);
            }

            // Find root nodes (nodes with no incoming edges)
            var rootNodes = new List<string>();
            foreach (var node in graph.Nodes.Values)
            {
                if (!hasParent.Contains(node.Id))
                    rootNodes.Add(node.Id);
            }

            // Calculate subtree widths for balanced layout
            var subtreeWidths = new Dictionary<string, int>();
            var visited = new HashSet<string>();

            int CalculateSubtreeWidth(string nodeId)
            {
                if (visited.Contains(nodeId) || !graph.Nodes.ContainsKey(nodeId))
                    return 1;
                visited.Add(nodeId);

                if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
                {
                    subtreeWidths[nodeId] = 1;
                    return 1;
                }

                int totalWidth = 0;
                foreach (var childId in children)
                {
                    totalWidth += CalculateSubtreeWidth(childId);
                }
                subtreeWidths[nodeId] = Math.Max(1, totalWidth);
                return subtreeWidths[nodeId];
            }

            foreach (var rootId in rootNodes)
            {
                CalculateSubtreeWidth(rootId);
            }

            // Position nodes using hierarchical layout
            var nodePositions = new Dictionary<string, (int x, int y)>();
            var positioned = new HashSet<string>();
            int maxX = 0, maxY = 0;

            void PositionNode(string nodeId, int x, int y, int availableWidth)
            {
                if (positioned.Contains(nodeId) || !graph.Nodes.ContainsKey(nodeId))
                    return;
                positioned.Add(nodeId);

                // Center node in available space
                int nodeX = x + (availableWidth * horizontalSpacing - nodeWidth) / 2;
                nodePositions[nodeId] = (nodeX, y);
                maxX = Math.Max(maxX, nodeX + nodeWidth);
                maxY = Math.Max(maxY, y + nodeHeight);

                if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
                    return;

                // Position children below
                int childY = y + verticalSpacing;
                int childX = x;

                foreach (var childId in children)
                {
                    int childWidth = subtreeWidths.TryGetValue(childId, out var w) ? w : 1;
                    PositionNode(childId, childX, childY, childWidth);
                    childX += childWidth;
                }
            }

            // Position all root nodes side by side
            int rootX = 0;
            foreach (var rootId in rootNodes)
            {
                int rootWidth = subtreeWidths.TryGetValue(rootId, out var rw) ? rw : 1;
                PositionNode(rootId, rootX, padding, rootWidth);
                rootX += rootWidth;
            }

            // Position any orphan nodes (shouldn't happen but safety check)
            int orphanY = maxY + verticalSpacing;
            int orphanX = padding;
            foreach (var node in graph.Nodes.Values)
            {
                if (!nodePositions.ContainsKey(node.Id))
                {
                    nodePositions[node.Id] = (orphanX, orphanY);
                    maxX = Math.Max(maxX, orphanX + nodeWidth);
                    maxY = Math.Max(maxY, orphanY + nodeHeight);
                    orphanX += horizontalSpacing;
                }
            }

            // SVG header
            int svgWidth = maxX + padding;
            int svgHeight = maxY + padding;

            sb.AppendLine($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth}\" height=\"{svgHeight}\" viewBox=\"0 0 {svgWidth} {svgHeight}\">");
            sb.AppendLine($"  <title>Flowchart{(fileName != null ? $" - {EscapeXml(fileName)}" : "")}</title>");
            sb.AppendLine($"  <desc>Dialog flowchart exported from Parley</desc>");

            // Define marker for arrows
            sb.AppendLine("  <defs>");
            sb.AppendLine("    <marker id=\"arrowhead\" markerWidth=\"10\" markerHeight=\"7\" refX=\"9\" refY=\"3.5\" orient=\"auto\">");
            sb.AppendLine("      <polygon points=\"0 0, 10 3.5, 0 7\" fill=\"#666\"/>");
            sb.AppendLine("    </marker>");
            sb.AppendLine("  </defs>");

            // Draw edges first (so they appear behind nodes)
            sb.AppendLine("  <g class=\"edges\">");
            foreach (var edge in graph.Edges)
            {
                if (nodePositions.TryGetValue(edge.SourceId, out var sourcePos) &&
                    nodePositions.TryGetValue(edge.TargetId, out var targetPos))
                {
                    int x1 = sourcePos.x + nodeWidth / 2;
                    int y1 = sourcePos.y + nodeHeight;
                    int x2 = targetPos.x + nodeWidth / 2;
                    int y2 = targetPos.y;

                    var strokeStyle = edge.IsConditional ? "stroke-dasharray=\"5,5\"" : "";
                    var strokeColor = edge.IsLinkEdge ? "#999" : "#666";

                    sb.AppendLine($"    <line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{strokeColor}\" stroke-width=\"2\" {strokeStyle} marker-end=\"url(#arrowhead)\"/>");
                }
            }
            sb.AppendLine("  </g>");

            // Draw nodes
            sb.AppendLine("  <g class=\"nodes\">");
            foreach (var node in graph.Nodes.Values)
            {
                if (!nodePositions.TryGetValue(node.Id, out var pos)) continue;

                // Node colors
                var fillColor = node.NodeType == FlowchartNodeType.Entry ? "#E8F4FD" : "#FDF4E8";
                var strokeColor = node.NodeType == FlowchartNodeType.Entry ? "#0066CC" : "#CC6600";
                if (node.IsLink)
                {
                    fillColor = "#F0F0F0";
                    strokeColor = "#999";
                }

                // Node rectangle
                sb.AppendLine($"    <g transform=\"translate({pos.x},{pos.y})\">");
                sb.AppendLine($"      <rect width=\"{nodeWidth}\" height=\"{nodeHeight}\" rx=\"5\" ry=\"5\" fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"2\"/>");

                // Speaker text
                if (!string.IsNullOrEmpty(node.Speaker))
                {
                    sb.AppendLine($"      <text x=\"10\" y=\"18\" font-size=\"10\" font-weight=\"bold\" fill=\"#666\">{EscapeXml(node.Speaker)}</text>");
                }

                // Node text (truncated)
                var displayText = node.ShortText ?? "";
                if (displayText.Length > 30) displayText = displayText.Substring(0, 27) + "...";
                sb.AppendLine($"      <text x=\"10\" y=\"38\" font-size=\"11\" fill=\"#333\">{EscapeXml(displayText)}</text>");

                // Indicators
                int indicatorX = 10;
                if (node.HasCondition)
                {
                    sb.AppendLine($"      <text x=\"{indicatorX}\" y=\"55\" font-size=\"9\" fill=\"#0066CC\">?</text>");
                    indicatorX += 15;
                }
                if (node.HasAction)
                {
                    sb.AppendLine($"      <text x=\"{indicatorX}\" y=\"55\" font-size=\"9\" fill=\"#FF6600\">!</text>");
                }

                sb.AppendLine("    </g>");
            }
            sb.AppendLine("  </g>");

            sb.AppendLine("</svg>");

            return sb.ToString();
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
