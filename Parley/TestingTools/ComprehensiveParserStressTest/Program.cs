using DialogEditor.Models;
using DialogEditor.Services;

namespace ComprehensiveParserStressTest
{
    /// <summary>
    /// Comprehensive stress test for DialogParser
    /// Tests all dialog node properties and configurations to catch binary format issues
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("Comprehensive Parser Stress Test");
            Console.WriteLine("========================================\n");

            // Initialize unified logger for file output
            UnifiedLogger.SetLogLevel(LogLevel.DEBUG);
            UnifiedLogger.LogApplication(LogLevel.INFO, "Starting comprehensive parser stress test");

            try
            {
                // Create test file path
                var outputPath = Path.Combine(
                    Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? ".",
                    "comprehensive_test.dlg"
                );

                Console.WriteLine($"Creating test file: {outputPath}\n");

                // Build comprehensive test dialog
                var dialog = BuildComprehensiveTestDialog();

                // Save to disk
                var service = new DialogFileService();
                var success = await service.SaveToFileAsync(dialog, outputPath);

                if (!success)
                {
                    Console.WriteLine("❌ FAILED: Could not write test file");
                    return 1;
                }

                Console.WriteLine("✅ Test file created successfully\n");

                // Validate round-trip
                Console.WriteLine("Testing round-trip integrity...");
                var reloaded = await service.LoadFromFileAsync(outputPath);

                if (reloaded == null)
                {
                    Console.WriteLine("❌ FAILED: Could not reload test file");
                    return 1;
                }

                // Validation checks
                var validationResults = ValidateDialog(dialog, reloaded);

                Console.WriteLine("\n========================================");
                Console.WriteLine("Validation Results:");
                Console.WriteLine("========================================\n");

                foreach (var result in validationResults)
                {
                    var icon = result.Passed ? "✅" : "❌";
                    Console.WriteLine($"{icon} {result.TestName}");
                    if (!result.Passed && !string.IsNullOrEmpty(result.Details))
                    {
                        Console.WriteLine($"   Details: {result.Details}");
                    }
                }

                var allPassed = validationResults.All(r => r.Passed);
                Console.WriteLine("\n========================================");
                Console.WriteLine(allPassed ? "RESULT: ✅ ALL TESTS PASSED" : "RESULT: ❌ SOME TESTS FAILED");
                Console.WriteLine("========================================\n");

                Console.WriteLine($"\nTest file saved to: {outputPath}");
                Console.WriteLine("Next steps:");
                Console.WriteLine("1. Open in NWN Toolset to verify compatibility");
                Console.WriteLine("2. Check load time (slow = Aurora doing corrections)");
                Console.WriteLine("3. Save in NWN Toolset and compare with HexAnalysis.ps1");

                return allPassed ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Stress test failed: {ex.Message}");
                return 1;
            }
        }

        static Dialog BuildComprehensiveTestDialog()
        {
            var dialog = new Dialog();

            Console.WriteLine("Building comprehensive test dialog structure...\n");

            // Entry[0] - Minimal NPC (baseline test)
            Console.WriteLine("Creating Entry[0]: Minimal NPC");
            var entry0 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry0.Text.Add(0, "Hello, traveler. (Minimal entry with defaults)");
            entry0.Speaker = "";
            dialog.Entries.Add(entry0);

            var start0 = dialog.CreatePtr()!;
            start0.Type = DialogNodeType.Entry;
            start0.Node = entry0;
            start0.Index = 0;
            start0.IsStart = true;
            dialog.Starts.Add(start0);

            // Entry[1] - Full NPC with ALL properties
            Console.WriteLine("Creating Entry[1]: Full NPC with all properties");
            var entry1 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry1.Text.Add(0, "Greetings! I am the merchant. Let me show you my wares. (Full property test)");
            entry1.Speaker = "npc_merchant";
            entry1.Comment = "Test comment field - should survive round-trip";
            entry1.Sound = "vs_narrf1_hail";
            entry1.Animation = DialogAnimation.Salute;
            entry1.AnimationLoop = true;
            entry1.Delay = 5000;

            // TODO: Quest/Journal - add when properly implemented
            // entry1.Quest = "q_test_quest";
            // entry1.QuestEntry = 10;

            entry1.ScriptAction = "test_act_script"; // 15 chars (CResRef limit is 16)

            // TODO: Parameters - add when properly implemented
            // entry1.ActionParams["Param1"] = "Value1";
            // entry1.ActionParams["Param2"] = "Value2";

            dialog.Entries.Add(entry1);

            var start1 = dialog.CreatePtr()!;
            start1.Type = DialogNodeType.Entry;
            start1.Node = entry1;
            start1.Index = 1;
            start1.IsStart = true;
            dialog.Starts.Add(start1);

            // Reply[0] - PC response to Entry[1] with conditional script
            Console.WriteLine("Creating Reply[0]: PC with conditional script");
            var reply0 = dialog.CreateNode(DialogNodeType.Reply)!;
            reply0.Text.Add(0, "What do you have for sale? (PC reply with condition)");
            reply0.Speaker = ""; // PC always blank speaker
            dialog.Replies.Add(reply0);

            var ptr_entry1_to_reply0 = dialog.CreatePtr()!;
            ptr_entry1_to_reply0.Type = DialogNodeType.Reply;
            ptr_entry1_to_reply0.Node = reply0;
            ptr_entry1_to_reply0.Index = 0;
            ptr_entry1_to_reply0.ScriptAppears = "test_condition";

            // TODO: Parameters - add when properly implemented
            // ptr_entry1_to_reply0.ConditionParams["CondParam1"] = "CondValue1";

            entry1.Pointers.Add(ptr_entry1_to_reply0);

            // Entry[2] - Response to Reply[0], also serves as link target
            Console.WriteLine("Creating Entry[2]: Merchant response (link target)");
            var entry2 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry2.Text.Add(0, "I have weapons, armor, and potions. (Link target entry)");
            entry2.Speaker = "npc_merchant";
            entry2.Animation = DialogAnimation.TalkNormal;
            dialog.Entries.Add(entry2);

            var ptr_reply0_to_entry2 = dialog.CreatePtr()!;
            ptr_reply0_to_entry2.Type = DialogNodeType.Entry;
            ptr_reply0_to_entry2.Node = entry2;
            ptr_reply0_to_entry2.Index = 2;
            reply0.Pointers.Add(ptr_reply0_to_entry2);

            // Reply[1] - Link back to Entry[2] (circular conversation test)
            Console.WriteLine("Creating Reply[1]: PC link back (circular test)");
            var reply1 = dialog.CreateNode(DialogNodeType.Reply)!;
            reply1.Text.Add(0, "Tell me more about your wares. (Link back)");
            dialog.Replies.Add(reply1);

            var ptr_entry2_to_reply1 = dialog.CreatePtr()!;
            ptr_entry2_to_reply1.Type = DialogNodeType.Reply;
            ptr_entry2_to_reply1.Node = reply1;
            ptr_entry2_to_reply1.Index = 1;
            entry2.Pointers.Add(ptr_entry2_to_reply1);

            // Create LINK back to Entry[2] (IsLink = true)
            var ptr_reply1_link_entry2 = dialog.CreatePtr()!;
            ptr_reply1_link_entry2.Type = DialogNodeType.Entry;
            ptr_reply1_link_entry2.Node = entry2;
            ptr_reply1_link_entry2.Index = 2; // Same index as entry2
            ptr_reply1_link_entry2.IsLink = true; // Mark as link
            ptr_reply1_link_entry2.LinkComment = "Link back to merchant wares";
            reply1.Pointers.Add(ptr_reply1_link_entry2);

            // Reply[2] - Ending conversation (no children)
            Console.WriteLine("Creating Reply[2]: Conversation ending");
            var reply2 = dialog.CreateNode(DialogNodeType.Reply)!;
            reply2.Text.Add(0, "Farewell. (Conversation ending)");
            dialog.Replies.Add(reply2);

            var ptr_entry1_to_reply2 = dialog.CreatePtr()!;
            ptr_entry1_to_reply2.Type = DialogNodeType.Reply;
            ptr_entry1_to_reply2.Node = reply2;
            ptr_entry1_to_reply2.Index = 2;
            entry1.Pointers.Add(ptr_entry1_to_reply2);

            // Entry[3] - Multi-speaker test (different NPC)
            Console.WriteLine("Creating Entry[3]: Multi-speaker test");
            var entry3 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry3.Text.Add(0, "What's going on here? (Different speaker)");
            entry3.Speaker = "npc_guard";
            entry3.Animation = DialogAnimation.TalkForceful;
            dialog.Entries.Add(entry3);

            var start3 = dialog.CreatePtr()!;
            start3.Type = DialogNodeType.Entry;
            start3.Node = entry3;
            start3.Index = 3;
            start3.IsStart = true;
            dialog.Starts.Add(start3);

            // Reply[3] - PC response to guard
            Console.WriteLine("Creating Reply[3]: PC to guard");
            var reply3 = dialog.CreateNode(DialogNodeType.Reply)!;
            reply3.Text.Add(0, "Just browsing the merchant's goods. (PC to guard)");
            dialog.Replies.Add(reply3);

            var ptr_entry3_to_reply3 = dialog.CreatePtr()!;
            ptr_entry3_to_reply3.Type = DialogNodeType.Reply;
            ptr_entry3_to_reply3.Node = reply3;
            ptr_entry3_to_reply3.Index = 3;
            entry3.Pointers.Add(ptr_entry3_to_reply3);

            // Entry[4] - Captain joins conversation (3rd speaker)
            Console.WriteLine("Creating Entry[4]: Captain (3rd speaker)");
            var entry4 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry4.Text.Add(0, "Carry on, citizen. (Captain, 3rd speaker)");
            entry4.Speaker = "npc_captain";
            entry4.Animation = DialogAnimation.Salute;
            dialog.Entries.Add(entry4);

            var ptr_reply3_to_entry4 = dialog.CreatePtr()!;
            ptr_reply3_to_entry4.Type = DialogNodeType.Entry;
            ptr_reply3_to_entry4.Node = entry4;
            ptr_reply3_to_entry4.Index = 4;
            reply3.Pointers.Add(ptr_reply3_to_entry4);

            // Test various animation types
            Console.WriteLine("Creating Entry[5-8]: Animation variety test");
            var animations = new[]
            {
                DialogAnimation.Taunt,
                DialogAnimation.Bow,
                DialogAnimation.Victory1,
                DialogAnimation.Read
            };

            for (int i = 0; i < animations.Length; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry)!;
                entry.Text.Add(0, $"Testing animation: {animations[i]}");
                entry.Speaker = "npc_test";
                entry.Animation = animations[i];
                dialog.Entries.Add(entry);

                var start = dialog.CreatePtr()!;
                start.Type = DialogNodeType.Entry;
                start.Node = entry;
                start.Index = (uint)(5 + i);
                start.IsStart = true;
                dialog.Starts.Add(start);
            }

            Console.WriteLine($"\nTest dialog structure complete:");
            Console.WriteLine($"  Entries: {dialog.Entries.Count}");
            Console.WriteLine($"  Replies: {dialog.Replies.Count}");
            Console.WriteLine($"  Starts: {dialog.Starts.Count}");

            return dialog;
        }

        static List<ValidationResult> ValidateDialog(Dialog original, Dialog reloaded)
        {
            var results = new List<ValidationResult>();

            // Validate counts
            results.Add(new ValidationResult
            {
                TestName = "Entry Count",
                Passed = original.Entries.Count == reloaded.Entries.Count,
                Details = $"Original: {original.Entries.Count}, Reloaded: {reloaded.Entries.Count}"
            });

            results.Add(new ValidationResult
            {
                TestName = "Reply Count",
                Passed = original.Replies.Count == reloaded.Replies.Count,
                Details = $"Original: {original.Replies.Count}, Reloaded: {reloaded.Replies.Count}"
            });

            results.Add(new ValidationResult
            {
                TestName = "Start Count",
                Passed = original.Starts.Count == reloaded.Starts.Count,
                Details = $"Original: {original.Starts.Count}, Reloaded: {reloaded.Starts.Count}"
            });

            // Validate Entry[1] - Full property node
            if (original.Entries.Count > 1 && reloaded.Entries.Count > 1)
            {
                var origEntry1 = original.Entries[1];
                var reloadEntry1 = reloaded.Entries[1];

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Text",
                    Passed = origEntry1.Text.GetDefault() == reloadEntry1.Text.GetDefault(),
                    Details = $"'{origEntry1.Text.GetDefault()}' vs '{reloadEntry1.Text.GetDefault()}'"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Speaker",
                    Passed = origEntry1.Speaker == reloadEntry1.Speaker,
                    Details = $"'{origEntry1.Speaker}' vs '{reloadEntry1.Speaker}'"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Comment",
                    Passed = origEntry1.Comment == reloadEntry1.Comment,
                    Details = $"'{origEntry1.Comment}' vs '{reloadEntry1.Comment}'"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Sound",
                    Passed = origEntry1.Sound == reloadEntry1.Sound,
                    Details = $"'{origEntry1.Sound}' vs '{reloadEntry1.Sound}'"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Animation",
                    Passed = origEntry1.Animation == reloadEntry1.Animation,
                    Details = $"{origEntry1.Animation} vs {reloadEntry1.Animation}"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] AnimationLoop",
                    Passed = origEntry1.AnimationLoop == reloadEntry1.AnimationLoop,
                    Details = $"{origEntry1.AnimationLoop} vs {reloadEntry1.AnimationLoop}"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Delay",
                    Passed = origEntry1.Delay == reloadEntry1.Delay,
                    Details = $"{origEntry1.Delay} vs {reloadEntry1.Delay}"
                });

                // TODO: Quest validation - add when properly implemented
                // results.Add(new ValidationResult
                // {
                //     TestName = "Entry[1] Quest",
                //     Passed = origEntry1.Quest == reloadEntry1.Quest,
                //     Details = $"'{origEntry1.Quest}' vs '{reloadEntry1.Quest}'"
                // });

                // results.Add(new ValidationResult
                // {
                //     TestName = "Entry[1] QuestEntry",
                //     Passed = origEntry1.QuestEntry == reloadEntry1.QuestEntry,
                //     Details = $"{origEntry1.QuestEntry} vs {reloadEntry1.QuestEntry}"
                // });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] ScriptAction",
                    Passed = origEntry1.ScriptAction == reloadEntry1.ScriptAction,
                    Details = $"'{origEntry1.ScriptAction}' vs '{reloadEntry1.ScriptAction}'"
                });

                results.Add(new ValidationResult
                {
                    TestName = "Entry[1] Pointer Count",
                    Passed = origEntry1.Pointers.Count == reloadEntry1.Pointers.Count,
                    Details = $"{origEntry1.Pointers.Count} vs {reloadEntry1.Pointers.Count}"
                });
            }

            // Validate link structure (Reply[1] should link to Entry[2])
            if (original.Replies.Count > 1 && reloaded.Replies.Count > 1)
            {
                var origReply1 = original.Replies[1];
                var reloadReply1 = reloaded.Replies[1];

                if (origReply1.Pointers.Count > 0 && reloadReply1.Pointers.Count > 0)
                {
                    var origLink = origReply1.Pointers[0];
                    var reloadLink = reloadReply1.Pointers[0];

                    results.Add(new ValidationResult
                    {
                        TestName = "Link IsLink Flag",
                        Passed = origLink.IsLink == reloadLink.IsLink,
                        Details = $"Original: {origLink.IsLink}, Reloaded: {reloadLink.IsLink}"
                    });

                    results.Add(new ValidationResult
                    {
                        TestName = "Link Index",
                        Passed = origLink.Index == reloadLink.Index,
                        Details = $"Original: {origLink.Index}, Reloaded: {reloadLink.Index}"
                    });
                }
            }

            // Validate conditional script (Reply[0] -> Entry[1])
            if (original.Entries.Count > 1 && reloaded.Entries.Count > 1)
            {
                var origEntry1 = original.Entries[1];
                var reloadEntry1 = reloaded.Entries[1];

                if (origEntry1.Pointers.Count > 0 && reloadEntry1.Pointers.Count > 0)
                {
                    var origPtr = origEntry1.Pointers[0];
                    var reloadPtr = reloadEntry1.Pointers[0];

                    results.Add(new ValidationResult
                    {
                        TestName = "Conditional Script",
                        Passed = origPtr.ScriptAppears == reloadPtr.ScriptAppears,
                        Details = $"'{origPtr.ScriptAppears}' vs '{reloadPtr.ScriptAppears}'"
                    });
                }
            }

            // Validate animation variety (Entries 5-8)
            if (original.Entries.Count > 8 && reloaded.Entries.Count > 8)
            {
                var animationMatch = true;
                for (int i = 5; i < 9; i++)
                {
                    if (original.Entries[i].Animation != reloaded.Entries[i].Animation)
                    {
                        animationMatch = false;
                        break;
                    }
                }

                results.Add(new ValidationResult
                {
                    TestName = "Animation Variety (Entries 5-8)",
                    Passed = animationMatch,
                    Details = animationMatch ? "All animations preserved" : "Some animations changed"
                });
            }

            return results;
        }
    }

    class ValidationResult
    {
        public string TestName { get; set; } = "";
        public bool Passed { get; set; }
        public string Details { get; set; } = "";
    }
}
