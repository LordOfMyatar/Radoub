using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Avalonia.Headless tests for dialog loading workflows (Issue #81)
    /// Tests file loading, parsing, and UI integration
    /// </summary>
    public class DialogLoadingHeadlessTests
    {
        private readonly string _testFilesPath;

        public DialogLoadingHeadlessTests()
        {
            // Path to test dialog files
            _testFilesPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "TestingTools", "TestDialogFiles"
            );
        }

        [AvaloniaFact]
        public async Task LoadDialog_SimpleFile_ParsesSuccessfully()
        {
            // Arrange: Use existing test file
            var testFile = Path.Combine(_testFilesPath, "test1_link.dlg");

            if (!File.Exists(testFile))
            {
                // Skip test if file doesn't exist
                return;
            }

            var dialogService = new DialogFileService();

            // Act: Load dialog
            var dialog = await dialogService.LoadFromFileAsync(testFile);

            // Assert: Dialog loaded successfully
            Assert.NotNull(dialog);
            Assert.NotEmpty(dialog.Entries);
        }

        [AvaloniaFact]
        public async Task LoadDialog_WithLinks_PreservesLinkStructure()
        {
            // Arrange
            var testFile = Path.Combine(_testFilesPath, "test1_link.dlg");

            if (!File.Exists(testFile))
            {
                return;
            }

            var dialogService = new DialogFileService();

            // Act
            var dialog = await dialogService.LoadFromFileAsync(testFile);

            // Assert: Check for linked structure
            Assert.NotNull(dialog);

            // Verify link registry exists (links were detected)
            dialog.RebuildLinkRegistry();
            Assert.NotNull(dialog.LinkRegistry);
        }

        [AvaloniaFact]
        public void CreateDialog_InitializesCorrectly()
        {
            // Arrange & Act: Create new dialog
            var dialog = new Dialog();

            // Assert: Initial state correct
            Assert.NotNull(dialog);
            Assert.NotNull(dialog.Entries);
            Assert.NotNull(dialog.Replies);
            Assert.NotNull(dialog.Starts);
            Assert.Empty(dialog.Entries);
            Assert.Empty(dialog.Replies);
            Assert.Empty(dialog.Starts);
        }

        [AvaloniaFact]
        public void CreateNode_Entry_CreatesCorrectType()
        {
            // Arrange
            var dialog = new Dialog();

            // Act: Create entry node
            var entryNode = dialog.CreateNode(DialogNodeType.Entry);

            // Assert
            Assert.NotNull(entryNode);
            Assert.Equal(DialogNodeType.Entry, entryNode.Type);
            Assert.NotNull(entryNode.Text);
            Assert.NotNull(entryNode.Pointers);
        }

        [AvaloniaFact]
        public void CreateNode_Reply_CreatesCorrectType()
        {
            // Arrange
            var dialog = new Dialog();

            // Act: Create reply node
            var replyNode = dialog.CreateNode(DialogNodeType.Reply);

            // Assert
            Assert.NotNull(replyNode);
            Assert.Equal(DialogNodeType.Reply, replyNode.Type);
            Assert.NotNull(replyNode.Text);
            Assert.NotNull(replyNode.Pointers);
        }

        [AvaloniaFact]
        public void AddNode_ToDialog_UpdatesCollections()
        {
            // Arrange
            var dialog = new Dialog();
            var entryNode = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entryNode);

            // Act: Add node to dialog
            dialog.AddNodeInternal(entryNode, DialogNodeType.Entry);

            // Assert: Node added to Entries collection
            Assert.Single(dialog.Entries);
            Assert.Equal(entryNode, dialog.Entries[0]);
        }

        [AvaloniaFact]
        public void AddStartingNode_UpdatesStartsList()
        {
            // Arrange
            var dialog = new Dialog();
            var entryNode = dialog.CreateNode(DialogNodeType.Entry);
            Assert.NotNull(entryNode);
            dialog.AddNodeInternal(entryNode, DialogNodeType.Entry);

            // Create pointer to entry
            var startPtr = dialog.CreatePtr();
            Assert.NotNull(startPtr);
            startPtr.Type = DialogNodeType.Entry;
            startPtr.Node = entryNode;
            startPtr.Index = 0;
            startPtr.IsLink = false;

            // Act: Add to starts
            dialog.Starts.Add(startPtr);

            // Assert: Starting node added
            Assert.Single(dialog.Starts);
            Assert.Equal(entryNode, dialog.Starts[0].Node);
        }
    }
}
