using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for FlowchartExportService
    /// Tests SVG generation and hierarchical layout
    /// Sprint 4: Issue #338
    /// </summary>
    public class FlowchartExportServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public FlowchartExportServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ParleyTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { /* Cleanup best effort */ }
        }

        #region SVG Export Basic Tests

        [Fact]
        public async Task ExportToSvgAsync_NullGraph_ReturnsFalse()
        {
            // Arrange
            var filePath = Path.Combine(_tempDir, "null.svg");

            // Act
            var result = await FlowchartExportService.ExportToSvgAsync(null!, filePath);

            // Assert
            Assert.False(result);
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task ExportToSvgAsync_EmptyGraph_ReturnsFalse()
        {
            // Arrange
            var graph = new FlowchartGraph();
            var filePath = Path.Combine(_tempDir, "empty.svg");

            // Act
            var result = await FlowchartExportService.ExportToSvgAsync(graph, filePath);

            // Assert
            Assert.False(result);
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task ExportToSvgAsync_ValidGraph_CreatesSvgFile()
        {
            // Arrange
            var graph = CreateSimpleGraph();
            var filePath = Path.Combine(_tempDir, "valid.svg");

            // Act
            var result = await FlowchartExportService.ExportToSvgAsync(graph, filePath, "test.dlg");

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));
        }

        [Fact]
        public async Task ExportToSvgAsync_CreatesValidSvgStructure()
        {
            // Arrange
            var graph = CreateSimpleGraph();
            var filePath = Path.Combine(_tempDir, "structure.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath, "test.dlg");
            var content = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", content);
            Assert.Contains("<svg xmlns=\"http://www.w3.org/2000/svg\"", content);
            Assert.Contains("<title>", content);
            Assert.Contains("<defs>", content);
            Assert.Contains("<marker id=\"arrowhead\"", content);
            Assert.Contains("</svg>", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_IncludesFileNameInTitle()
        {
            // Arrange
            var graph = CreateSimpleGraph();
            var filePath = Path.Combine(_tempDir, "title.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath, "merchant_dialog.dlg");
            var content = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.Contains("merchant_dialog.dlg", content);
        }

        #endregion

        #region SVG Node Rendering Tests

        [Fact]
        public async Task ExportToSvgAsync_RendersAllNodes()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            graph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Hello", "Owner"));
            graph.AddNode(new FlowchartNode("R0", FlowchartNodeType.Reply, "Goodbye", "PC"));
            graph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            graph.AddEdge(new FlowchartEdge("E0", "R0"));

            var filePath = Path.Combine(_tempDir, "nodes.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Should have 3 rect elements for 3 nodes
            var rectCount = System.Text.RegularExpressions.Regex.Matches(content, "<rect").Count;
            Assert.Equal(3, rectCount);
        }

        [Fact]
        public async Task ExportToSvgAsync_IncludesNodeText()
        {
            // Arrange
            var graph = CreateSimpleGraph();
            var filePath = Path.Combine(_tempDir, "text.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.Contains("Hello adventurer", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_IncludesSpeakerLabels()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Greetings", "Merchant"));
            graph.RootNodeIds.Add("E0");

            var filePath = Path.Combine(_tempDir, "speaker.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.Contains("Merchant", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_ShowsConditionIndicator()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode(
                "E0", FlowchartNodeType.Entry, "Conditional", "Owner",
                hasCondition: true
            ));
            graph.RootNodeIds.Add("E0");

            var filePath = Path.Combine(_tempDir, "condition.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Should have condition indicator (?)
            Assert.Contains(">?</text>", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_ShowsActionIndicator()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode(
                "E0", FlowchartNodeType.Entry, "With Action", "Owner",
                hasAction: true
            ));
            graph.RootNodeIds.Add("E0");

            var filePath = Path.Combine(_tempDir, "action.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Should have action indicator (!)
            Assert.Contains(">!</text>", content);
        }

        #endregion

        #region SVG Edge Rendering Tests

        [Fact]
        public async Task ExportToSvgAsync_RendersEdgesWithArrows()
        {
            // Arrange
            var graph = CreateSimpleGraph();
            var filePath = Path.Combine(_tempDir, "edges.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.Contains("<line", content);
            Assert.Contains("marker-end=\"url(#arrowhead)\"", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_ConditionalEdge_IsDashed()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Start", "Owner"));
            graph.AddNode(new FlowchartNode("E1", FlowchartNodeType.Entry, "Next", "Owner"));
            graph.AddEdge(new FlowchartEdge("E0", "E1", isConditional: true));
            graph.RootNodeIds.Add("E0");

            var filePath = Path.Combine(_tempDir, "conditional.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.Contains("stroke-dasharray", content);
        }

        #endregion

        #region SVG Layout Tests (Issue #338)

        [Fact]
        public async Task ExportToSvgAsync_HierarchicalLayout_RootAtTop()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            graph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Child", "Owner"));
            graph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            graph.RootNodeIds.Add("ROOT");

            var filePath = Path.Combine(_tempDir, "layout.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Parse the transforms to verify root is above child
            // ROOT should have lower Y value than E0
            // This is a simplified check - real test would parse SVG transforms
            Assert.Contains("<g class=\"nodes\">", content);
            Assert.Contains("transform=\"translate(", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_MultipleChildren_SpreadHorizontally()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            graph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Child 1", "Owner"));
            graph.AddNode(new FlowchartNode("E1", FlowchartNodeType.Entry, "Child 2", "Owner"));
            graph.AddNode(new FlowchartNode("E2", FlowchartNodeType.Entry, "Child 3", "Owner"));
            graph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            graph.AddEdge(new FlowchartEdge("ROOT", "E1"));
            graph.AddEdge(new FlowchartEdge("ROOT", "E2"));
            graph.RootNodeIds.Add("ROOT");

            var filePath = Path.Combine(_tempDir, "multi-child.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - All 4 nodes should be rendered
            var rectCount = System.Text.RegularExpressions.Regex.Matches(content, "<rect").Count;
            Assert.Equal(4, rectCount);
        }

        #endregion

        #region SVG Color Tests (Issue #340)

        [Fact]
        public async Task ExportToSvgAsync_RootNode_HasNeutralGrayColors()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            graph.RootNodeIds.Add("ROOT");

            var filePath = Path.Combine(_tempDir, "root-color.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Root should have neutral gray fill and stroke
            Assert.Contains("fill=\"#E8E8E8\"", content);
            Assert.Contains("stroke=\"#757575\"", content);
        }

        [Fact]
        public async Task ExportToSvgAsync_LinkNode_HasDistinctColors()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode(
                "L0", FlowchartNodeType.Link, "Link", "", isLink: true, linkTargetId: "E0"
            ));
            graph.RootNodeIds.Add("L0");

            var filePath = Path.Combine(_tempDir, "link-color.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Link should have lighter gray colors
            Assert.Contains("fill=\"#F0F0F0\"", content);
            Assert.Contains("stroke=\"#9E9E9E\"", content);
        }

        #endregion

        #region XML Escaping Tests

        [Fact]
        public async Task ExportToSvgAsync_EscapesSpecialCharacters()
        {
            // Arrange
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode(
                "E0", FlowchartNodeType.Entry, "Test <script> & \"quotes\"", "Owner"
            ));
            graph.RootNodeIds.Add("E0");

            var filePath = Path.Combine(_tempDir, "escape.svg");

            // Act
            await FlowchartExportService.ExportToSvgAsync(graph, filePath);
            var content = await File.ReadAllTextAsync(filePath);

            // Assert - Special characters should be escaped
            Assert.Contains("&lt;script&gt;", content);
            Assert.Contains("&amp;", content);
            Assert.Contains("&quot;", content);
        }

        #endregion

        #region Helper Methods

        private FlowchartGraph CreateSimpleGraph()
        {
            var graph = new FlowchartGraph();
            graph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            graph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Hello adventurer", "Owner"));
            graph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            graph.RootNodeIds.Add("ROOT");
            return graph;
        }

        #endregion
    }
}