using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace CreateMartaDialog;

/// <summary>
/// Creates npc_marta.dlg - Innkeeper Marta dialog for Conversation Simulator testing.
/// Tests INT-based branching with conditional scripts.
///
/// Structure:
/// - Root 0: Looking for work (3 INT variants with conditions)
/// - Root 1: Quest complete (journal condition)
/// - Root 2: Default greeting (fallback)
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Creating npc_marta.dlg...");
        Console.WriteLine("Innkeeper Marta - INT-branching dialog for simulator testing");

        var dialog = new Dialog();

        // ============================================================
        // ENTRIES (NPC lines)
        // ============================================================

        // Entry 0: High INT greeting (condition: gc_int_check 14+)
        var entryHighInt = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryHighInt.Text.Add(0, "Ah, you've got sharp eyes. I can tell you're the observant type. There's been trouble brewing in the Whispering Caves to the east - goblin raids. Their chief, Skullcrusher, has been organizing them. Eliminate him, and I'll make it worth your while. 200 gold, and free room and board for a tenday.");
        dialog.Entries.Add(entryHighInt);

        // Entry 1: Average INT greeting (condition: gc_int_check 10+)
        var entryAvgInt = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryAvgInt.Text.Add(0, "Looking for work, are ya? Well, there's goblins making trouble in the caves east of here. Kill their boss - big ugly brute called Skullcrusher. 150 gold when it's done.");
        dialog.Entries.Add(entryAvgInt);

        // Entry 2: Low INT greeting (no condition - fallback)
        var entryLowInt = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryLowInt.Text.Add(0, "Work. Yes. Caves. East. Kill goblin. Big goblin. Chief. Come back. Get gold. Understand?");
        dialog.Entries.Add(entryLowInt);

        // Entry 3: Quest complete entry
        var entryQuestDone = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryQuestDone.Text.Add(0, "You did it! Word travels fast - the goblin raids have stopped completely. Here's your gold, as promised. You're welcome at the Rusty Flagon anytime, friend.");
        dialog.Entries.Add(entryQuestDone);

        // Entry 4: Default greeting (after quest done)
        var entryDefault = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryDefault.Text.Add(0, "Welcome back! What can I get you today?");
        dialog.Entries.Add(entryDefault);

        // Entry 5: Cave details (high INT follow-up)
        var entryCaveDetails = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryCaveDetails.Text.Add(0, "The Whispering Caves are about half a day's walk east, past the old mill. You'll hear them before you see them - the wind through the cave mouth makes an eerie sound. The goblins have set traps near the entrance. Watch for tripwires.");
        dialog.Entries.Add(entryCaveDetails);

        // Entry 6: Backstory (high INT follow-up)
        var entryBackstory = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryBackstory.Text.Add(0, "*sighs* My husband... he was a trader. Used that route through the caves for years. One day, the goblins got organized. They took everything. Including him. The constable won't do anything - says it's outside town jurisdiction.");
        dialog.Entries.Add(entryBackstory);

        // Entry 7: Simple directions (average INT follow-up)
        var entryDirections = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryDirections.Text.Add(0, "Just follow the east road past the mill. Can't miss 'em - there's goblin tracks everywhere these days. Big cave in the hillside.");
        dialog.Entries.Add(entryDirections);

        // Entry 8: She points (low INT follow-up)
        var entryShePoints = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryShePoints.Text.Add(0, "*points firmly toward the door* That way. Walk. Sun rises there. Caves. Go now. Kill goblin. Come back. Gold.");
        dialog.Entries.Add(entryShePoints);

        // Entry 9: Room quality joke
        var entryRoomJoke = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryRoomJoke.Text.Add(0, "*laughs* Don't expect the Ducal Palace! But the bed's clean, the roof doesn't leak... most of the time... and I make a mean breakfast stew. Best deal in three villages!");
        dialog.Entries.Add(entryRoomJoke);

        // Entry 10: Future hook
        var entryFutureHook = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = "Marta"
        };
        entryFutureHook.Text.Add(0, "Hmm, now that you mention it... I've been hearing strange noises from the cellar lately. Probably just rats, but... well, come back later and I might have something for you.");
        dialog.Entries.Add(entryFutureHook);

        // ============================================================
        // REPLIES (PC lines)
        // ============================================================

        // Reply 0: High INT accept
        var replyHighAccept = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyHighAccept.Text.Add(0, "Consider it done. I'll bring you his head.");
        dialog.Replies.Add(replyHighAccept);

        // Reply 1: High INT ask about caves
        var replyAskCaves = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyAskCaves.Text.Add(0, "Tell me more about these caves.");
        dialog.Replies.Add(replyAskCaves);

        // Reply 2: High INT ask backstory
        var replyAskBackstory = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyAskBackstory.Text.Add(0, "What's your connection to these goblins?");
        dialog.Replies.Add(replyAskBackstory);

        // Reply 3: Average INT accept
        var replyAvgAccept = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyAvgAccept.Text.Add(0, "Goblins? I can handle goblins.");
        dialog.Replies.Add(replyAvgAccept);

        // Reply 4: Average INT ask directions
        var replyAskDirections = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyAskDirections.Text.Add(0, "Where are these caves exactly?");
        dialog.Replies.Add(replyAskDirections);

        // Reply 5: Low INT accept
        var replyLowAccept = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyLowAccept.Text.Add(0, "Me kill goblin good!");
        dialog.Replies.Add(replyLowAccept);

        // Reply 6: Low INT ask direction
        var replyLowAskWhere = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyLowAskWhere.Text.Add(0, "Uh... which way east?");
        dialog.Replies.Add(replyLowAskWhere);

        // Reply 7: Quest done - humble
        var replyHumble = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyHumble.Text.Add(0, "Just doing my job.");
        dialog.Replies.Add(replyHumble);

        // Reply 8: Quest done - ask about room
        var replyAboutRoom = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyAboutRoom.Text.Add(0, "About that free room...");
        dialog.Replies.Add(replyAboutRoom);

        // Reply 9: Default - passing through
        var replyPassingThrough = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyPassingThrough.Text.Add(0, "Just passing through.");
        dialog.Replies.Add(replyPassingThrough);

        // Reply 10: Default - more trouble
        var replyMoreTrouble = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyMoreTrouble.Text.Add(0, "Any other trouble you need handled?");
        dialog.Replies.Add(replyMoreTrouble);

        // Reply 11: Generic continue after info
        var replyContinue = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString()
        };
        replyContinue.Text.Add(0, "I'll take care of it.");
        dialog.Replies.Add(replyContinue);

        // ============================================================
        // WIRE UP POINTERS
        // ============================================================

        // --- High INT branch ---
        entryHighInt.Pointers.Add(new DialogPtr { Node = replyHighAccept, Type = DialogNodeType.Reply, Index = 0, Parent = dialog });
        entryHighInt.Pointers.Add(new DialogPtr { Node = replyAskCaves, Type = DialogNodeType.Reply, Index = 1, Parent = dialog });
        entryHighInt.Pointers.Add(new DialogPtr { Node = replyAskBackstory, Type = DialogNodeType.Reply, Index = 2, Parent = dialog });

        // Cave details → continue
        replyAskCaves.Pointers.Add(new DialogPtr { Node = entryCaveDetails, Type = DialogNodeType.Entry, Index = 5, Parent = dialog });
        entryCaveDetails.Pointers.Add(new DialogPtr { Node = replyContinue, Type = DialogNodeType.Reply, Index = 11, Parent = dialog });

        // Backstory → continue
        replyAskBackstory.Pointers.Add(new DialogPtr { Node = entryBackstory, Type = DialogNodeType.Entry, Index = 6, Parent = dialog });
        entryBackstory.Pointers.Add(new DialogPtr { Node = replyContinue, Type = DialogNodeType.Reply, Index = 11, Parent = dialog });

        // --- Average INT branch ---
        entryAvgInt.Pointers.Add(new DialogPtr { Node = replyAvgAccept, Type = DialogNodeType.Reply, Index = 3, Parent = dialog });
        entryAvgInt.Pointers.Add(new DialogPtr { Node = replyAskDirections, Type = DialogNodeType.Reply, Index = 4, Parent = dialog });

        // Directions → continue
        replyAskDirections.Pointers.Add(new DialogPtr { Node = entryDirections, Type = DialogNodeType.Entry, Index = 7, Parent = dialog });
        entryDirections.Pointers.Add(new DialogPtr { Node = replyContinue, Type = DialogNodeType.Reply, Index = 11, Parent = dialog });

        // --- Low INT branch ---
        entryLowInt.Pointers.Add(new DialogPtr { Node = replyLowAccept, Type = DialogNodeType.Reply, Index = 5, Parent = dialog });
        entryLowInt.Pointers.Add(new DialogPtr { Node = replyLowAskWhere, Type = DialogNodeType.Reply, Index = 6, Parent = dialog });

        // She points → continue
        replyLowAskWhere.Pointers.Add(new DialogPtr { Node = entryShePoints, Type = DialogNodeType.Entry, Index = 8, Parent = dialog });
        entryShePoints.Pointers.Add(new DialogPtr { Node = replyContinue, Type = DialogNodeType.Reply, Index = 11, Parent = dialog });

        // --- Quest complete branch ---
        entryQuestDone.Pointers.Add(new DialogPtr { Node = replyHumble, Type = DialogNodeType.Reply, Index = 7, Parent = dialog });
        entryQuestDone.Pointers.Add(new DialogPtr { Node = replyAboutRoom, Type = DialogNodeType.Reply, Index = 8, Parent = dialog });

        // Room joke is terminal
        replyAboutRoom.Pointers.Add(new DialogPtr { Node = entryRoomJoke, Type = DialogNodeType.Entry, Index = 9, Parent = dialog });

        // --- Default greeting branch ---
        entryDefault.Pointers.Add(new DialogPtr { Node = replyPassingThrough, Type = DialogNodeType.Reply, Index = 9, Parent = dialog });
        entryDefault.Pointers.Add(new DialogPtr { Node = replyMoreTrouble, Type = DialogNodeType.Reply, Index = 10, Parent = dialog });

        // Future hook is terminal
        replyMoreTrouble.Pointers.Add(new DialogPtr { Node = entryFutureHook, Type = DialogNodeType.Entry, Index = 10, Parent = dialog });

        // ============================================================
        // STARTING ENTRIES (with conditions)
        // ============================================================

        // Root 0: High INT (condition: gc_int_check with param for INT 14+)
        var startHighInt = new DialogPtr
        {
            Node = entryHighInt,
            Type = DialogNodeType.Entry,
            Index = 0,
            ScriptAppears = "gc_int_check",
            Parent = dialog
        };
        startHighInt.ConditionParams["nInt"] = "14";
        dialog.Starts.Add(startHighInt);

        // Root 1: Average INT (condition: gc_int_check with param for INT 10+)
        var startAvgInt = new DialogPtr
        {
            Node = entryAvgInt,
            Type = DialogNodeType.Entry,
            Index = 1,
            ScriptAppears = "gc_int_check",
            Parent = dialog
        };
        startAvgInt.ConditionParams["nInt"] = "10";
        dialog.Starts.Add(startAvgInt);

        // Root 2: Low INT (no condition - fallback)
        dialog.Starts.Add(new DialogPtr
        {
            Node = entryLowInt,
            Type = DialogNodeType.Entry,
            Index = 2,
            Parent = dialog
        });

        // Root 3: Quest complete (condition: journal check)
        var startQuestDone = new DialogPtr
        {
            Node = entryQuestDone,
            Type = DialogNodeType.Entry,
            Index = 3,
            ScriptAppears = "gc_journal",
            Parent = dialog
        };
        startQuestDone.ConditionParams["sTag"] = "goblin_quest";
        startQuestDone.ConditionParams["nState"] = "100";
        dialog.Starts.Add(startQuestDone);

        // Root 4: Default greeting (condition: already helped)
        var startDefault = new DialogPtr
        {
            Node = entryDefault,
            Type = DialogNodeType.Entry,
            Index = 4,
            ScriptAppears = "gc_global_int",
            Parent = dialog
        };
        startDefault.ConditionParams["sName"] = "nMartaHelped";
        startDefault.ConditionParams["nValue"] = "1";
        dialog.Starts.Add(startDefault);

        // Rebuild LinkRegistry
        dialog.RebuildLinkRegistry();

        // ============================================================
        // SAVE TO FILE
        // ============================================================
        string outputPath = @"C:\Users\Sheri\Documents\Neverwinter Nights\modules\LNS_DLG\npc_marta.dlg";

        // Ensure directory exists
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileService = new DialogFileService();
        bool success = await fileService.SaveToFileAsync(dialog, outputPath);

        if (!success)
        {
            Console.WriteLine("Failed to create dialog file!");
            return;
        }

        Console.WriteLine($"Created: {outputPath}");
        Console.WriteLine();
        Console.WriteLine("Dialog Structure:");
        Console.WriteLine("  [gc_int_check 14+] High INT: Detailed quest offer");
        Console.WriteLine("    - Accept / Ask about caves / Ask backstory");
        Console.WriteLine("  [gc_int_check 10+] Avg INT: Standard quest offer");
        Console.WriteLine("    - Accept / Ask directions");
        Console.WriteLine("  [no condition] Low INT: Simple quest offer");
        Console.WriteLine("    - Me kill goblin! / Which way?");
        Console.WriteLine("  [gc_journal] Quest Complete: Reward and thanks");
        Console.WriteLine("  [gc_global_int] Default: Post-quest greeting");
        Console.WriteLine();
        Console.WriteLine("Use this dialog to test the Conversation Simulator!");
    }
}
