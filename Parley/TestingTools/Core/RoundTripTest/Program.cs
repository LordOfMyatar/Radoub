using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Parsers;
using DialogEditor.Services;
using SharedTestUtils;

namespace RoundTripTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Round-Trip Aurora Compatibility Test ===");
            UnifiedLogger.SetLogLevel(LogLevel.INFO);

            string inputFile;

            // Parse command line arguments
            if (args.Length == 0)
            {
                // No args - show available files and use default
                Console.WriteLine();
                TestFileHelper.PrintAvailableFiles();
                Console.WriteLine();
                Console.WriteLine("Usage: dotnet run [file-key|full-path|suite:name]");
                Console.WriteLine("  file-key:   Use predefined test file (e.g., 'chef', 'hicks_hudson')");
                Console.WriteLine("  full-path:  Use custom DLG file path");
                Console.WriteLine("  suite:name: Run test suite (e.g., 'suite:quick', 'suite:medium')");
                Console.WriteLine();
                Console.WriteLine("Using default: chef");
                inputFile = TestFileHelper.GetTestFilePath("chef");
            }
            else if (args[0].StartsWith("suite:", StringComparison.OrdinalIgnoreCase))
            {
                // Test suite mode
                var suiteName = args[0].Substring(6);
                var suiteFiles = TestFileHelper.GetTestSuite(suiteName);
                Console.WriteLine($"\n=== Running Test Suite: {suiteName} ({suiteFiles.Count} files) ===\n");

                int passed = 0;
                int failed = 0;

                foreach (var file in suiteFiles)
                {
                    Console.WriteLine($"\n{'=',60}");
                    Console.WriteLine($"Testing: {Path.GetFileName(file)}");
                    Console.WriteLine($"{'=',60}\n");

                    bool success = await RunRoundTripTest(file);
                    if (success) passed++; else failed++;
                }

                Console.WriteLine($"\n\n{'=',60}");
                Console.WriteLine($"SUITE RESULTS: {passed} passed, {failed} failed");
                Console.WriteLine($"{'=',60}");
                return;
            }
            else if (File.Exists(args[0]))
            {
                // Full path provided
                inputFile = args[0];
            }
            else
            {
                // Assume it's a test file key
                try
                {
                    inputFile = TestFileHelper.GetTestFilePath(args[0]);
                }
                catch (ArgumentException)
                {
                    Console.WriteLine($"ERROR: '{args[0]}' is not a valid file key or path");
                    Console.WriteLine();
                    TestFileHelper.PrintAvailableFiles();
                    return;
                }
            }

            await RunRoundTripTest(inputFile);
        }

        static async Task<bool> RunRoundTripTest(string inputFile)
        {
            var exportPath = inputFile.Replace(".dlg", "_roundtrip.dlg");

            try
            {
                var parser = new DialogParser();

                Console.WriteLine("=== STEP 1: PARSE ORIGINAL FILE ===");
                Console.WriteLine($"File: {inputFile}");
                var originalDialog = await parser.ParseFromFileAsync(inputFile);

                if (originalDialog == null)
                {
                    Console.WriteLine("❌ Failed to parse original file");
                    return false;
                }

                Console.WriteLine($"Original: {originalDialog.Entries.Count} entries, {originalDialog.Replies.Count} replies, {originalDialog.Starts.Count} starts");

                // Show original conversation flow
                if (originalDialog.Starts.Count > 0)
                {
                    var start0 = originalDialog.Starts[0];
                    var originalStartText = originalDialog.Entries[(int)start0.Index].Text.GetDefault();
                    var originalStartPreview = originalStartText.Length > 60 ? originalStartText.Substring(0, 60) + "..." : originalStartText;
                    Console.WriteLine($"Original Start[0] → Entry[{start0.Index}]: \"{originalStartPreview}\"");

                    if (originalDialog.Replies.Count > 0)
                    {
                        var reply0 = originalDialog.Replies[0];
                        if (reply0.Pointers.Count > 0)
                        {
                            var entryPtr = reply0.Pointers[0];
                            var originalReplyText = originalDialog.Entries[(int)entryPtr.Index].Text.GetDefault();
                            var originalReplyPreview = originalReplyText.Length > 60 ? originalReplyText.Substring(0, 60) + "..." : originalReplyText;
                            Console.WriteLine($"Original Reply[0] → Entry[{entryPtr.Index}]: \"{originalReplyPreview}\"");
                        }
                    }
                }

                Console.WriteLine("\n=== STEP 2: EXPORT WITH AURORA GLOBAL INDICES ===");
                await parser.WriteToFileAsync(originalDialog, exportPath);
                Console.WriteLine($"Exported to: {exportPath}");

                Console.WriteLine("\n=== STEP 3: RE-IMPORT AND TEST ROUND-TRIP ===");
                var importedDialog = await parser.ParseFromFileAsync(exportPath);

                if (importedDialog == null)
                {
                    Console.WriteLine("❌ Failed to re-import exported file");
                    return false;
                }

                Console.WriteLine($"Imported: {importedDialog.Entries.Count} entries, {importedDialog.Replies.Count} replies, {importedDialog.Starts.Count} starts");

                // Test conversation structure preservation
                Console.WriteLine("\n=== STEP 4: VERIFY CONVERSATION STRUCTURE ===");
                bool structurePreserved = true;

                if (importedDialog.Starts.Count > 0 && originalDialog.Starts.Count > 0)
                {
                    var importedStart0 = importedDialog.Starts[0];
                    var originalStart0 = originalDialog.Starts[0];

                    var importedStartText = importedDialog.Entries[(int)importedStart0.Index].Text.GetDefault();
                    var importedStartPreview = importedStartText.Length > 60 ? importedStartText.Substring(0, 60) + "..." : importedStartText;
                    Console.WriteLine($"Imported Start[0] → Entry[{importedStart0.Index}]: \"{importedStartPreview}\"");

                    if (importedStart0.Index != originalStart0.Index)
                    {
                        Console.WriteLine($"❌ Start pointer mismatch: original={originalStart0.Index}, imported={importedStart0.Index}");
                        structurePreserved = false;
                    }

                    if (importedDialog.Replies.Count > 0 && originalDialog.Replies.Count > 0)
                    {
                        var importedReply0 = importedDialog.Replies[0];
                        var originalReply0 = originalDialog.Replies[0];

                        if (importedReply0.Pointers.Count > 0 && originalReply0.Pointers.Count > 0)
                        {
                            var importedPtr = importedReply0.Pointers[0];
                            var originalPtr = originalReply0.Pointers[0];

                            var importedReplyText = importedDialog.Entries[(int)importedPtr.Index].Text.GetDefault();
                            var importedReplyPreview = importedReplyText.Length > 60 ? importedReplyText.Substring(0, 60) + "..." : importedReplyText;
                            Console.WriteLine($"Imported Reply[0] → Entry[{importedPtr.Index}]: \"{importedReplyPreview}\"");

                            if (importedPtr.Index != originalPtr.Index)
                            {
                                Console.WriteLine($"❌ Reply pointer mismatch: original={originalPtr.Index}, imported={importedPtr.Index}");
                                structurePreserved = false;
                            }
                        }
                    }
                }

                Console.WriteLine("\n=== STEP 5: FINAL VERIFICATION ===");
                if (structurePreserved)
                {
                    Console.WriteLine("✅ SUCCESS: Round-trip preserved conversation structure!");
                    Console.WriteLine("✅ NWN Toolset can read the global indices correctly");
                    Console.WriteLine("✅ ArcReactor can import the global indices back to local indices");
                }
                else
                {
                    Console.WriteLine("❌ FAILURE: Conversation structure was not preserved in round-trip");
                }

                Console.WriteLine($"\n📁 Export location: {exportPath}");
                return structurePreserved;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}
