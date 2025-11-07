using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using DialogEditor.Parsers;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Comprehensive tests for GFF parser (Issue #22)
    /// Tests critical parsing logic including field indices, struct types, and format validation
    /// </summary>
    public class GffParserTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly DialogFileService _dialogService;

        public GffParserTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyGffTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _dialogService = new DialogFileService();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        #region Field Index Mapping Tests (Critical 4:1 Aurora Pattern)

        [Fact]
        public async Task FieldIndices_AuroraPattern_4To1Ratio()
        {
            // This tests the critical Aurora pattern: FieldIndicesCount should be ~4x FieldCount
            // This is the most important structural invariant in GFF files

            var dialog = new Dialog();

            // Create multiple entries with fields to generate field indices
            for (int i = 0; i < 5; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"Entry {i}");
                entry.Speaker = $"NPC_{i}";
                entry.ScriptAction = $"script_{i}";
                entry.Comment = $"Comment {i}";
                dialog.AddNodeInternal(entry, entry.Type);
            }

            var filePath = Path.Combine(_testDirectory, "field_indices_test.dlg");

            // Act - Save and inspect the raw GFF structure
            await _dialogService.SaveToFileAsync(dialog, filePath);

            // Read the GFF header to check field indices ratio
            using (var fs = File.OpenRead(filePath))
            using (var reader = new BinaryReader(fs))
            {
                // Skip to field counts (offset 20 and 44)
                fs.Seek(20, SeekOrigin.Begin);
                uint fieldCount = reader.ReadUInt32();

                fs.Seek(44, SeekOrigin.Begin);
                uint fieldIndicesCount = reader.ReadUInt32();

                // Assert 4:1 ratio (Aurora pattern)
                double ratio = (double)fieldIndicesCount / fieldCount;

                Assert.True(ratio >= 3.5 && ratio <= 4.5,
                    $"Field indices ratio should be ~4:1 (Aurora pattern). Got {ratio:F2} ({fieldIndicesCount} indices / {fieldCount} fields)");
            }
        }

        [Fact]
        public async Task FieldIndices_SingleEntry_CorrectMapping()
        {
            // Test that a simple dialog maps field indices correctly

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test text");
            entry.Speaker = "TestNPC";
            dialog.AddNodeInternal(entry, entry.Type);

            var start = dialog.CreatePtr();
            start!.Node = entry;
            start.Type = DialogNodeType.Entry;
            start.Index = 0;
            dialog.Starts.Add(start);

            var filePath = Path.Combine(_testDirectory, "single_entry_indices.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Data preserved correctly (proves field indices mapped correctly)
            Assert.Single(loaded.Entries);
            Assert.Equal("Test text", loaded.Entries[0].Text.GetDefault());
            Assert.Equal("TestNPC", loaded.Entries[0].Speaker);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        public async Task FieldIndices_MultipleEntries_ScalesCorrectly(int entryCount)
        {
            // Test that field indices scale properly with more entries

            var dialog = new Dialog();
            for (int i = 0; i < entryCount; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"Entry {i}");
                entry.Speaker = $"NPC_{i}";
                entry.ScriptAction = $"script_{i}";
                dialog.AddNodeInternal(entry, entry.Type);
            }

            var filePath = Path.Combine(_testDirectory, $"multi_entry_{entryCount}.dlg");

            // Act
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - All data preserved
            Assert.Equal(entryCount, loaded.Entries.Count);
            for (int i = 0; i < entryCount; i++)
            {
                Assert.Equal($"Entry {i}", loaded.Entries[i].Text.GetDefault());
                Assert.Equal($"NPC_{i}", loaded.Entries[i].Speaker);
                Assert.Equal($"script_{i}", loaded.Entries[i].ScriptAction);
            }
        }

        #endregion

        #region Struct Type Validation Tests

        [Fact]
        public async Task StructType_DialogRoot_IsCorrect()
        {
            // Dialog root struct should have type 0 (DLG)

            var dialog = new Dialog();
            var filePath = Path.Combine(_testDirectory, "struct_type_root.dlg");

            await _dialogService.SaveToFileAsync(dialog, filePath);

            // Read GFF and check root struct type
            using (var fs = File.OpenRead(filePath))
            using (var reader = new BinaryReader(fs))
            {
                // Read header to get struct offset
                fs.Seek(8, SeekOrigin.Begin);
                uint structOffset = reader.ReadUInt32();

                // Read first struct type
                fs.Seek(structOffset, SeekOrigin.Begin);
                uint structType = reader.ReadUInt32();

                Assert.Equal(0xFFFFFFFFu, structType); // Root DLG struct is type 0xFFFFFFFF per GFF spec
            }
        }

        [Fact]
        public async Task StructType_EntryList_PreservedInRoundTrip()
        {
            // Test that struct types for Entry nodes are preserved

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test entry");
            dialog.AddNodeInternal(entry, entry.Type);

            var filePath = Path.Combine(_testDirectory, "struct_type_entry.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Entry preserved with correct type
            Assert.Single(loaded.Entries);
            Assert.Equal(DialogNodeType.Entry, loaded.Entries[0].Type);
        }

        [Fact]
        public async Task StructType_ReplyList_PreservedInRoundTrip()
        {
            // Test that struct types for Reply nodes are preserved

            var dialog = new Dialog();
            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Test reply");
            dialog.AddNodeInternal(reply, reply.Type);

            var filePath = Path.Combine(_testDirectory, "struct_type_reply.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Reply preserved with correct type
            Assert.Single(loaded.Replies);
            Assert.Equal(DialogNodeType.Reply, loaded.Replies[0].Type);
        }

        #endregion

        #region CResRef Format Validation Tests

        [Theory]
        [InlineData("valid_name")]
        [InlineData("test123")]
        [InlineData("a")]
        [InlineData("sixteen_chars_ok")]
        public async Task CResRef_ValidNames_AcceptedAndPreserved(string resref)
        {
            // CResRef fields must be <= 16 characters, alphanumeric + underscore

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test");
            entry.ScriptAction = resref; // ScriptAction is stored as CResRef
            dialog.AddNodeInternal(entry, entry.Type);

            var filePath = Path.Combine(_testDirectory, $"cresref_{resref}.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - CResRef preserved
            Assert.Equal(resref, loaded.Entries[0].ScriptAction);
        }

        [Fact]
        public async Task CResRef_EmptyString_HandledCorrectly()
        {
            // Empty CResRef should be preserved as empty string

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test");
            entry.ScriptAction = ""; // Empty script
            dialog.AddNodeInternal(entry, entry.Type);

            var filePath = Path.Combine(_testDirectory, "cresref_empty.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Empty preserved
            Assert.Equal("", loaded.Entries[0].ScriptAction);
        }

        [Fact]
        public async Task CResRef_MaxLength16_Truncated()
        {
            // CResRef longer than 16 chars should be truncated

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test");
            entry.ScriptAction = "this_is_much_longer_than_sixteen_characters"; // Too long
            dialog.AddNodeInternal(entry, entry.Type);

            var filePath = Path.Combine(_testDirectory, "cresref_toolong.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Truncated to 16 chars
            Assert.True(loaded.Entries[0].ScriptAction.Length <= 16,
                $"CResRef should be truncated to 16 chars, got {loaded.Entries[0].ScriptAction.Length}");
        }

        #endregion

        #region Circular Reference Detection Tests

        [Fact]
        public async Task CircularReference_SimpleCycle_HandledCorrectly()
        {
            // Test that circular references (A->B->A) are handled without infinite loops

            var dialog = new Dialog();

            var entry1 = dialog.CreateNode(DialogNodeType.Entry);
            entry1!.Text.Add(0, "Entry 1");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Reply 1");
            dialog.AddNodeInternal(reply1, reply1.Type);

            // Create cycle: Entry1 -> Reply1 -> Entry1
            var ptr1 = dialog.CreatePtr();
            ptr1!.Node = reply1;
            ptr1.Type = DialogNodeType.Reply;
            ptr1.Index = 0;
            entry1.Pointers.Add(ptr1);

            var ptr2 = dialog.CreatePtr();
            ptr2!.Node = entry1;
            ptr2.Type = DialogNodeType.Entry;
            ptr2.Index = 0;
            ptr2.IsLink = true; // Mark as link to indicate back-reference
            reply1.Pointers.Add(ptr2);

            var filePath = Path.Combine(_testDirectory, "circular_simple.dlg");

            // Act - Should not hang or crash
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Structure preserved
            Assert.Single(loaded.Entries);
            Assert.Single(loaded.Replies);
            Assert.True(loaded.Replies[0].Pointers[0].IsLink, "Back-reference should be marked as link");
        }

        [Fact]
        public async Task CircularReference_ComplexCycle_NoStackOverflow()
        {
            // Test deeper circular structure doesn't cause stack overflow during parse

            var dialog = new Dialog();

            // Create A -> B -> C -> A cycle
            var entryA = dialog.CreateNode(DialogNodeType.Entry);
            entryA!.Text.Add(0, "A");
            dialog.AddNodeInternal(entryA, entryA.Type);

            var replyB = dialog.CreateNode(DialogNodeType.Reply);
            replyB!.Text.Add(0, "B");
            dialog.AddNodeInternal(replyB, replyB.Type);

            var entryC = dialog.CreateNode(DialogNodeType.Entry);
            entryC!.Text.Add(0, "C");
            dialog.AddNodeInternal(entryC, entryC.Type);

            // A -> B
            var ptrAB = dialog.CreatePtr();
            ptrAB!.Node = replyB;
            ptrAB.Type = DialogNodeType.Reply;
            ptrAB.Index = 0;
            entryA.Pointers.Add(ptrAB);

            // B -> C
            var ptrBC = dialog.CreatePtr();
            ptrBC!.Node = entryC;
            ptrBC.Type = DialogNodeType.Entry;
            ptrBC.Index = 1;
            replyB.Pointers.Add(ptrBC);

            // C -> A (cycle)
            var ptrCA = dialog.CreatePtr();
            ptrCA!.Node = entryA;
            ptrCA.Type = DialogNodeType.Entry;
            ptrCA.Index = 0;
            ptrCA.IsLink = true;
            entryC.Pointers.Add(ptrCA);

            var filePath = Path.Combine(_testDirectory, "circular_complex.dlg");

            // Act - Should complete without stack overflow
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Got here without crashing
            Assert.Equal(2, loaded.Entries.Count);
            Assert.Single(loaded.Replies);
        }

        #endregion

        #region Malformed GFF Security Tests

        [Fact]
        public async Task MalformedGff_InvalidHeader_ReturnsNull()
        {
            // Test that invalid GFF headers are rejected gracefully

            var filePath = Path.Combine(_testDirectory, "malformed_header.dlg");

            // Create file with invalid header
            using (var fs = File.Create(filePath))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(Encoding.ASCII.GetBytes("BADH")); // Invalid file type
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                // Rest of header would be garbage
                for (int i = 0; i < 50; i++)
                    writer.Write((uint)0);
            }

            // Act - Should return null, not crash
            var result = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task MalformedGff_ZeroLength_ReturnsNull()
        {
            // Test that zero-length files are rejected gracefully

            var filePath = Path.Combine(_testDirectory, "zero_length.dlg");
            File.WriteAllBytes(filePath, Array.Empty<byte>());

            // Act
            var result = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task MalformedGff_TruncatedFile_ReturnsNull()
        {
            // Test that truncated GFF files are rejected gracefully

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Test");
            dialog.AddNodeInternal(entry, entry.Type);

            var filePath = Path.Combine(_testDirectory, "truncated.dlg");
            await _dialogService.SaveToFileAsync(dialog, filePath);

            // Truncate the file
            var bytes = File.ReadAllBytes(filePath);
            File.WriteAllBytes(filePath, bytes.Take(bytes.Length / 2).ToArray());

            // Act - Should return null, not crash
            var result = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task MalformedGff_ExcessiveFieldCount_ReturnsNull()
        {
            // Test that absurdly large field counts are rejected gracefully (DoS protection)

            var filePath = Path.Combine(_testDirectory, "excessive_fields.dlg");

            // Create file with valid header but absurd field count
            using (var fs = File.Create(filePath))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(Encoding.ASCII.GetBytes("DLG "));
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                writer.Write((uint)56); // StructOffset
                writer.Write((uint)1);  // StructCount
                writer.Write((uint)64); // FieldOffset
                writer.Write(uint.MaxValue); // Excessive FieldCount - should be rejected
                // ... rest would cause out-of-memory if not validated
            }

            // Act - Should return null, not crash or allocate massive memory
            var result = await _dialogService.LoadFromFileAsync(filePath);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task GffParser_ComplexDialog_PreservesAllFieldTypes()
        {
            // Comprehensive test that exercises all GFF field types in a realistic dialog

            var dialog = new Dialog();
            dialog.PreventZoom = true; // BYTE field
            dialog.ScriptEnd = "on_end_script"; // CResRef
            dialog.ScriptAbort = "on_abort"; // CResRef

            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "English text"); // CExoLocString
            entry.Speaker = "npc_merchant"; // CExoString
            entry.ScriptAction = "check_gold"; // CResRef
            entry.Comment = "Quest dialog"; // CExoString
            entry.Delay = 5; // DWORD
            entry.Animation = DialogAnimation.TalkNormal; // DialogAnimation enum
            entry.AnimationLoop = true; // BYTE
            dialog.AddNodeInternal(entry, entry.Type);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Player response");
            dialog.AddNodeInternal(reply, reply.Type);

            var ptr = dialog.CreatePtr();
            ptr!.Node = reply;
            ptr.Type = DialogNodeType.Reply;
            ptr.Index = 0;
            entry.Pointers.Add(ptr);

            var filePath = Path.Combine(_testDirectory, "complex_all_types.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Dialog loaded successfully
            Assert.NotNull(loaded);

            // Assert - All field types preserved
            Assert.True(loaded!.PreventZoom);
            Assert.Equal("on_end_script", loaded.ScriptEnd);
            Assert.Equal("on_abort", loaded.ScriptAbort);

            var loadedEntry = loaded.Entries[0];
            Assert.Equal("English text", loadedEntry.Text.Get(0));
            Assert.Equal("npc_merchant", loadedEntry.Speaker);
            Assert.Equal("check_gold", loadedEntry.ScriptAction);
            Assert.Equal("Quest dialog", loadedEntry.Comment);
            Assert.Equal(5u, loadedEntry.Delay);
            Assert.Equal(DialogAnimation.TalkNormal, loadedEntry.Animation);
            Assert.True(loadedEntry.AnimationLoop);
        }

        [Fact]
        public async Task GffParser_EmptyOptionalFields_NotSavedOrLoadedIncorrectly()
        {
            // Test that empty optional fields don't pollute the GFF file

            var dialog = new Dialog();
            var entry = dialog.CreateNode(DialogNodeType.Entry);
            entry!.Text.Add(0, "Minimal entry");
            // Leave all optional fields empty
            entry.Speaker = "";
            entry.ScriptAction = "";
            entry.Comment = "";
            dialog.AddNodeInternal(entry, entry.Type);

            var filePath = Path.Combine(_testDirectory, "empty_optional.dlg");

            // Act - Round trip
            await _dialogService.SaveToFileAsync(dialog, filePath);
            var loaded = await _dialogService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);

            // Assert - Empty fields preserved as empty, not null or garbage
            Assert.Equal("", loaded.Entries[0].Speaker);
            Assert.Equal("", loaded.Entries[0].ScriptAction);
            Assert.Equal("", loaded.Entries[0].Comment);
        }

        #endregion
    }
}
