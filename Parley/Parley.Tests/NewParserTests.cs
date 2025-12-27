using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for the new Radoub.Formats.Dlg parser integration.
    /// Verifies DlgAdapter conversion and round-trip through new parser path.
    /// </summary>
    public class NewParserTests
    {
        [Fact]
        public void DlgAdapter_ToDialog_ConvertsBasicStructure()
        {
            // Arrange: Create a simple DlgFile
            var dlgFile = new DlgFile
            {
                DelayEntry = 100,
                DelayReply = 200,
                NumWords = 50,
                EndConversation = "end_script",
                EndConverAbort = "abort_script",
                PreventZoomIn = true
            };

            // Add an entry
            var entry = new DlgEntry
            {
                Speaker = "NPC_TAG",
                Animation = 28,
                AnimLoop = true,
                Script = "on_speak",
                Comment = "Test comment",
                Sound = "vo_hello"
            };
            entry.Text.LocalizedStrings[0] = "Hello adventurer!";
            dlgFile.Entries.Add(entry);

            // Add a reply
            var reply = new DlgReply
            {
                Animation = 29,
                AnimLoop = false,
                Script = "on_reply"
            };
            reply.Text.LocalizedStrings[0] = "Greetings!";
            dlgFile.Replies.Add(reply);

            // Add entry -> reply link
            entry.RepliesList.Add(new DlgLink { Index = 0, IsChild = false });

            // Add starting point
            dlgFile.StartingList.Add(new DlgLink { Index = 0, Active = "check_cond" });

            // Act
            var dialog = DlgAdapter.ToDialog(dlgFile);

            // Assert
            Assert.Equal(100u, dialog.DelayEntry);
            Assert.Equal(200u, dialog.DelayReply);
            Assert.Equal(50u, dialog.NumWords);
            Assert.Equal("end_script", dialog.ScriptEnd);
            Assert.Equal("abort_script", dialog.ScriptAbort);
            Assert.True(dialog.PreventZoom);

            Assert.Single(dialog.Entries);
            Assert.Equal("NPC_TAG", dialog.Entries[0].Speaker);
            Assert.Equal(DialogAnimation.Taunt, dialog.Entries[0].Animation);
            Assert.True(dialog.Entries[0].AnimationLoop);
            Assert.Equal("Hello adventurer!", dialog.Entries[0].Text.GetDefault());

            Assert.Single(dialog.Replies);
            Assert.Equal("Greetings!", dialog.Replies[0].Text.GetDefault());

            Assert.Single(dialog.Starts);
            Assert.Equal("check_cond", dialog.Starts[0].ScriptAppears);
        }

        [Fact]
        public void DlgAdapter_ToDlgFile_ConvertsBasicStructure()
        {
            // Arrange: Create a Parley Dialog
            var dialog = new Dialog
            {
                DelayEntry = 100,
                DelayReply = 200,
                NumWords = 50,
                ScriptEnd = "end_script",
                ScriptAbort = "abort_script",
                PreventZoom = true
            };

            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Speaker = "NPC_TAG";
            entry.Animation = DialogAnimation.Taunt;
            entry.AnimationLoop = true;
            entry.ScriptAction = "on_speak";
            entry.Text.Add(0, "Hello adventurer!");
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Animation = DialogAnimation.Greeting;
            reply.Text.Add(0, "Greetings!");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            // Link entry -> reply
            var ptr = dialog.CreatePtr();
            ptr!.Type = DialogNodeType.Reply;
            ptr.Node = reply;
            ptr.Index = 0;
            entry.Pointers.Add(ptr);

            // Add start
            var startPtr = dialog.CreatePtr();
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entry;
            startPtr.Index = 0;
            startPtr.ScriptAppears = "check_cond";
            startPtr.IsStart = true;
            dialog.Starts.Add(startPtr);

            // Act
            var dlgFile = DlgAdapter.ToDlgFile(dialog);

            // Assert
            Assert.Equal(100u, dlgFile.DelayEntry);
            Assert.Equal(200u, dlgFile.DelayReply);
            Assert.Equal(50u, dlgFile.NumWords);
            Assert.Equal("end_script", dlgFile.EndConversation);
            Assert.Equal("abort_script", dlgFile.EndConverAbort);
            Assert.True(dlgFile.PreventZoomIn);

            Assert.Single(dlgFile.Entries);
            Assert.Equal("NPC_TAG", dlgFile.Entries[0].Speaker);
            Assert.Equal(28u, dlgFile.Entries[0].Animation);
            Assert.True(dlgFile.Entries[0].AnimLoop);
            Assert.Equal("Hello adventurer!", dlgFile.Entries[0].Text.GetDefault());

            Assert.Single(dlgFile.Replies);
            Assert.Equal("Greetings!", dlgFile.Replies[0].Text.GetDefault());

            Assert.Single(dlgFile.Entries[0].RepliesList);
            Assert.Equal(0u, dlgFile.Entries[0].RepliesList[0].Index);

            Assert.Single(dlgFile.StartingList);
            Assert.Equal("check_cond", dlgFile.StartingList[0].Active);
        }

        [Fact]
        public void DlgAdapter_RoundTrip_PreservesData()
        {
            // Arrange: Create a Dialog with various features
            var original = new Dialog
            {
                DelayEntry = 150,
                DelayReply = 250,
                NumWords = 75,
                ScriptEnd = "my_end",
                ScriptAbort = "my_abort",
                PreventZoom = true
            };

            var entry1 = original.CreateNode(DialogNodeType.Entry);
            entry1!.Speaker = "MERCHANT";
            entry1.Animation = DialogAnimation.Bow;
            entry1.Text.Add(0, "Welcome to my shop!");
            entry1.Quest = "shopping_quest";
            entry1.QuestEntry = 1;
            original.AddNodeInternal(entry1, DialogNodeType.Entry);

            var entry2 = original.CreateNode(DialogNodeType.Entry);
            entry2!.Speaker = "MERCHANT";
            entry2.Text.Add(0, "Come back anytime!");
            original.AddNodeInternal(entry2, DialogNodeType.Entry);

            var reply1 = original.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Show me your wares.");
            reply1.ScriptAction = "open_store";
            original.AddNodeInternal(reply1, DialogNodeType.Reply);

            var reply2 = original.CreateNode(DialogNodeType.Reply);
            reply2!.Text.Add(0, "Goodbye.");
            original.AddNodeInternal(reply2, DialogNodeType.Reply);

            // Entry1 -> Reply1, Reply2
            var ptr1 = original.CreatePtr();
            ptr1!.Type = DialogNodeType.Reply;
            ptr1.Node = reply1;
            entry1.Pointers.Add(ptr1);

            var ptr2 = original.CreatePtr();
            ptr2!.Type = DialogNodeType.Reply;
            ptr2.Node = reply2;
            entry1.Pointers.Add(ptr2);

            // Reply1 -> Entry2
            var ptr3 = original.CreatePtr();
            ptr3!.Type = DialogNodeType.Entry;
            ptr3.Node = entry2;
            reply1.Pointers.Add(ptr3);

            // Reply2 -> Entry2 (link)
            var ptr4 = original.CreatePtr();
            ptr4!.Type = DialogNodeType.Entry;
            ptr4.Node = entry2;
            ptr4.IsLink = true;
            ptr4.LinkComment = "Same farewell";
            reply2.Pointers.Add(ptr4);

            // Start -> Entry1
            var startPtr = original.CreatePtr();
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entry1;
            startPtr.ScriptAppears = "is_shop_open";
            original.Starts.Add(startPtr);

            // Act: Round-trip through DlgFile
            var dlgFile = DlgAdapter.ToDlgFile(original);
            var restored = DlgAdapter.ToDialog(dlgFile);

            // Assert
            Assert.Equal(original.DelayEntry, restored.DelayEntry);
            Assert.Equal(original.DelayReply, restored.DelayReply);
            Assert.Equal(original.NumWords, restored.NumWords);
            Assert.Equal(original.ScriptEnd, restored.ScriptEnd);
            Assert.Equal(original.ScriptAbort, restored.ScriptAbort);
            Assert.Equal(original.PreventZoom, restored.PreventZoom);

            Assert.Equal(original.Entries.Count, restored.Entries.Count);
            Assert.Equal(original.Replies.Count, restored.Replies.Count);
            Assert.Equal(original.Starts.Count, restored.Starts.Count);

            // Check entry details
            Assert.Equal(original.Entries[0].Speaker, restored.Entries[0].Speaker);
            Assert.Equal(original.Entries[0].Quest, restored.Entries[0].Quest);
            Assert.Equal(original.Entries[0].QuestEntry, restored.Entries[0].QuestEntry);
            Assert.Equal(original.Entries[0].Text.GetDefault(), restored.Entries[0].Text.GetDefault());

            // Check reply details
            Assert.Equal(original.Replies[0].ScriptAction, restored.Replies[0].ScriptAction);

            // Check links preserved
            Assert.Equal(original.Entries[0].Pointers.Count, restored.Entries[0].Pointers.Count);
            Assert.Equal(original.Replies[1].Pointers[0].IsLink, restored.Replies[1].Pointers[0].IsLink);
            Assert.Equal(original.Replies[1].Pointers[0].LinkComment, restored.Replies[1].Pointers[0].LinkComment);

            // Check start
            Assert.Equal(original.Starts[0].ScriptAppears, restored.Starts[0].ScriptAppears);
        }

        [Fact]
        public void DialogFileService_NewParser_LoadsAndSaves()
        {
            // Arrange
            var service = new DialogFileService { UseNewParser = true };

            // Create a simple dialog
            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test entry");
            dialog.AddNodeInternal(entry, DialogNodeType.Entry);

            var startPtr = dialog.CreatePtr();
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entry;
            dialog.Starts.Add(startPtr);

            // Act: Save with new parser
            var tempPath = Path.Combine(Path.GetTempPath(), $"test_new_parser_{Guid.NewGuid()}.dlg");
            try
            {
                var saveResult = service.SaveToFileAsync(dialog, tempPath).Result;
                Assert.True(saveResult, "Save should succeed");

                // Load with new parser
                var loaded = service.LoadFromFileAsync(tempPath).Result;

                // Assert
                Assert.NotNull(loaded);
                Assert.Single(loaded.Entries);
                Assert.Equal("Test entry", loaded.Entries[0].Text.GetDefault());
                Assert.Single(loaded.Starts);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void DialogFileService_NewParser_IsValidDlgFile()
        {
            // Arrange
            var service = new DialogFileService { UseNewParser = true };

            // Create a valid DLG file
            var dlgFile = new DlgFile();
            dlgFile.Entries.Add(new DlgEntry());
            dlgFile.StartingList.Add(new DlgLink { Index = 0 });

            var tempPath = Path.Combine(Path.GetTempPath(), $"test_valid_{Guid.NewGuid()}.dlg");
            try
            {
                DlgWriter.Write(dlgFile, tempPath);

                // Act
                var isValid = service.IsValidDlgFile(tempPath);

                // Assert
                Assert.True(isValid);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void DlgAdapter_HandlesEmptyDialog()
        {
            // Arrange
            var emptyDlg = new DlgFile();

            // Act
            var dialog = DlgAdapter.ToDialog(emptyDlg);

            // Assert
            Assert.NotNull(dialog);
            Assert.Empty(dialog.Entries);
            Assert.Empty(dialog.Replies);
            Assert.Empty(dialog.Starts);
        }

        [Fact]
        public void DlgAdapter_HandlesActionParams()
        {
            // Arrange
            var dlgFile = new DlgFile();
            var entry = new DlgEntry();
            entry.ActionParams.Add(new DlgParam { Key = "target", Value = "npc_001" });
            entry.ActionParams.Add(new DlgParam { Key = "amount", Value = "100" });
            dlgFile.Entries.Add(entry);

            // Act
            var dialog = DlgAdapter.ToDialog(dlgFile);

            // Assert
            Assert.Equal(2, dialog.Entries[0].ActionParams.Count);
            Assert.Equal("npc_001", dialog.Entries[0].ActionParams["target"]);
            Assert.Equal("100", dialog.Entries[0].ActionParams["amount"]);
        }

        [Fact]
        public void DlgAdapter_HandlesConditionParams()
        {
            // Arrange
            var dlgFile = new DlgFile();
            var entry = new DlgEntry();
            dlgFile.Entries.Add(entry);

            var link = new DlgLink { Index = 0 };
            link.ConditionParams.Add(new DlgParam { Key = "min_level", Value = "5" });
            dlgFile.StartingList.Add(link);

            // Act
            var dialog = DlgAdapter.ToDialog(dlgFile);

            // Assert
            Assert.Single(dialog.Starts[0].ConditionParams);
            Assert.Equal("5", dialog.Starts[0].ConditionParams["min_level"]);
        }
    }
}
