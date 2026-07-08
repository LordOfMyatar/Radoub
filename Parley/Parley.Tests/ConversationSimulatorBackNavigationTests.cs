using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DialogEditor;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Parley.Tests.Mocks;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// #2524: The Conversation Simulator needs a Back button that steps to the prior entry
    /// in the current playthrough. Back restores the previous NPC text + replies, clears the
    /// ended/loop state when returning from a leaf, and preserves already-recorded coverage.
    /// </summary>
    public class ConversationSimulatorBackNavigationTests
    {
        private static void DisableTts()
        {
            var settings = Program.Services.GetRequiredService<DialogEditor.Services.ISettingsService>();
            settings.SimulatorTtsEnabled = false;
            settings.SimulatorAutoSpeak = false;
            settings.SimulatorAutoAdvance = false;
        }

        /// <summary>
        /// START -> Entry("NPC greets") -> [Reply("Ask name"), Reply("Leave")]
        /// Reply("Ask name") -> Entry("I am Bob") -> Reply("Bye") -> (leaf, ends)
        /// Reply("Leave") -> (leaf, ends)
        /// </summary>
        private static Dialog BuildBranchingDialog()
        {
            var dialog = new Dialog();

            var greet = dialog.CreateNode(DialogNodeType.Entry)!;
            greet.Text.Add(0, "NPC greets");
            dialog.AddNodeInternal(greet, greet.Type);

            var askName = dialog.CreateNode(DialogNodeType.Reply)!;
            askName.Text.Add(0, "Ask name");
            dialog.AddNodeInternal(askName, askName.Type);

            var leave = dialog.CreateNode(DialogNodeType.Reply)!;
            leave.Text.Add(0, "Leave");
            dialog.AddNodeInternal(leave, leave.Type);

            var iAmBob = dialog.CreateNode(DialogNodeType.Entry)!;
            iAmBob.Text.Add(0, "I am Bob");
            dialog.AddNodeInternal(iAmBob, iAmBob.Type);

            var bye = dialog.CreateNode(DialogNodeType.Reply)!;
            bye.Text.Add(0, "Bye");
            dialog.AddNodeInternal(bye, bye.Type);

            AddPtr(dialog, greet, askName, DialogNodeType.Reply);
            AddPtr(dialog, greet, leave, DialogNodeType.Reply);
            AddPtr(dialog, askName, iAmBob, DialogNodeType.Entry);
            AddPtr(dialog, iAmBob, bye, DialogNodeType.Reply);
            // bye has no pointers -> conversation ends

            var start = dialog.CreatePtr()!;
            start.Type = DialogNodeType.Entry;
            start.Node = greet;
            dialog.Starts.Add(start);

            return dialog;
        }

        private static void AddPtr(Dialog dialog, DialogNode from, DialogNode to, DialogNodeType type)
        {
            var ptr = dialog.CreatePtr()!;
            ptr.Type = type;
            ptr.Node = to;
            from.Pointers.Add(ptr);
        }

        private static ConversationSimulatorViewModel NewVm(Dialog dialog)
        {
            DisableTts();
            return new ConversationSimulatorViewModel(dialog, "test-back.dlg", new MockTtsService());
        }

        [AvaloniaFact]
        public void CanGoBack_IsFalse_AtRootSelectionAndFirstEntry()
        {
            var vm = NewVm(BuildBranchingDialog());
            vm.StartConversation();
            Assert.False(vm.CanGoBack); // at root-entry selection

            vm.SelectReply(0); // choose the root entry -> first NPC entry
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.CanGoBack); // can now step back to root selection
        }

        /// <summary>
        /// #2524 race guard: GoBack calls _ttsService.Stop(), and on Windows/espeak/say that
        /// fires SpeakCompleted synchronously → OnTtsSpeakCompleted. With AutoAdvance+AutoSpeak on
        /// and the pre-Back display showing a single reply, the NPC-auto-advance gate could fire a
        /// spurious forward SelectReply, undoing the Back. GoBack must not advance forward.
        /// </summary>
        [AvaloniaFact]
        public void GoBack_WithAutoAdvanceAndSingleReply_DoesNotSpuriouslyAdvanceForward()
        {
            var settings = Program.Services.GetRequiredService<DialogEditor.Services.ISettingsService>();
            settings.SimulatorTtsEnabled = true;
            settings.SimulatorAutoSpeak = true;
            settings.SimulatorAutoAdvance = true;

            var tts = new MockTtsService(); // StopFiresCompleted = true (Windows-style)
            // Single-reply chain: A -> B -> C, each NPC entry has exactly one reply.
            var dialog = BuildSingleReplyChainForBack();
            var vm = new ConversationSimulatorViewModel(dialog, "test-back-race.dlg", tts);

            vm.StartConversation();
            vm.SelectReply(0);              // pick root "A"; auto-speaks
            Dispatcher.UIThread.RunJobs();
            tts.CompleteSpeech();           // NPC "A" done -> auto-advance to speak PC reply
            Dispatcher.UIThread.RunJobs();
            tts.CompleteSpeech();           // PC reply done -> advance to "B"
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("B", vm.NpcText);  // now at B with its single reply displayed

            int spokenBeforeBack = tts.SpokenTexts.Count;

            vm.GoBack();                    // GoBack -> Stop() fires completion; must NOT advance forward
            Dispatcher.UIThread.RunJobs();

            // We stepped back to "A" and must stay there, not get bounced forward to "B"/"C".
            Assert.Equal("A", vm.NpcText);
            // GoBack must not trigger any new speech (a spurious SelectReply would re-speak a reply).
            Assert.Equal(spokenBeforeBack, tts.SpokenTexts.Count);
        }

        /// <summary>A -> only-1 -> B -> only-2 -> C -> only-3 (leaf ends). Every entry: 1 reply.</summary>
        private static Dialog BuildSingleReplyChainForBack()
        {
            var dialog = new Dialog();
            var a = dialog.CreateNode(DialogNodeType.Entry)!; a.Text.Add(0, "A"); dialog.AddNodeInternal(a, a.Type);
            var r1 = dialog.CreateNode(DialogNodeType.Reply)!; r1.Text.Add(0, "only-1"); dialog.AddNodeInternal(r1, r1.Type);
            var b = dialog.CreateNode(DialogNodeType.Entry)!; b.Text.Add(0, "B"); dialog.AddNodeInternal(b, b.Type);
            var r2 = dialog.CreateNode(DialogNodeType.Reply)!; r2.Text.Add(0, "only-2"); dialog.AddNodeInternal(r2, r2.Type);
            var c = dialog.CreateNode(DialogNodeType.Entry)!; c.Text.Add(0, "C"); dialog.AddNodeInternal(c, c.Type);
            var r3 = dialog.CreateNode(DialogNodeType.Reply)!; r3.Text.Add(0, "only-3"); dialog.AddNodeInternal(r3, r3.Type);
            AddPtr(dialog, a, r1, DialogNodeType.Reply);
            AddPtr(dialog, r1, b, DialogNodeType.Entry);
            AddPtr(dialog, b, r2, DialogNodeType.Reply);
            AddPtr(dialog, r2, c, DialogNodeType.Entry);
            AddPtr(dialog, c, r3, DialogNodeType.Reply);
            var start = dialog.CreatePtr()!; start.Type = DialogNodeType.Entry; start.Node = a; dialog.Starts.Add(start);
            return dialog;
        }

        [AvaloniaFact]
        public void GoBack_FromFirstEntry_ReturnsToRootSelection()
        {
            var vm = NewVm(BuildBranchingDialog());
            vm.StartConversation();
            vm.SelectReply(0); // -> "NPC greets" with 2 replies
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("NPC greets", vm.NpcText);

            vm.GoBack();
            Dispatcher.UIThread.RunJobs();

            Assert.False(vm.CanGoBack);
            Assert.False(vm.HasEnded);
            // Back at the root-entry selection menu (single start -> 1 root option)
            Assert.Single(vm.Replies);
            Assert.Contains(vm.Replies, r => r.Text.Contains("NPC greets"));
        }

        [AvaloniaFact]
        public void GoBack_RestoresPriorNpcTextAndReplies()
        {
            var vm = NewVm(BuildBranchingDialog());
            vm.StartConversation();
            vm.SelectReply(0);            // -> "NPC greets" (replies: Ask name, Leave)
            Dispatcher.UIThread.RunJobs();
            vm.SelectReply(0);            // pick "Ask name" -> "I am Bob" (reply: Bye)
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("I am Bob", vm.NpcText);

            vm.GoBack();                  // back to "NPC greets"
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("NPC greets", vm.NpcText);
            Assert.Equal(2, vm.Replies.Count);
            Assert.Contains(vm.Replies, r => r.Text.Contains("Ask name"));
            Assert.Contains(vm.Replies, r => r.Text.Contains("Leave"));
        }

        [AvaloniaFact]
        public void GoBack_FromEndedConversation_ClearsEndedAndRestoresLastEntry()
        {
            var vm = NewVm(BuildBranchingDialog());
            vm.StartConversation();
            vm.SelectReply(0);            // "NPC greets"
            Dispatcher.UIThread.RunJobs();
            vm.SelectReply(0);            // "Ask name" -> "I am Bob"
            Dispatcher.UIThread.RunJobs();
            vm.SelectReply(0);            // "Bye" -> leaf -> conversation ends
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.HasEnded);

            vm.GoBack();
            Dispatcher.UIThread.RunJobs();

            Assert.False(vm.HasEnded);
            Assert.Equal("I am Bob", vm.NpcText);
            Assert.Contains(vm.Replies, r => r.Text.Contains("Bye"));
        }

        [AvaloniaFact]
        public void GoBack_PreservesRecordedCoverage()
        {
            var dialog = BuildBranchingDialog();
            var vm = NewVm(dialog);
            vm.StartConversation();
            vm.SelectReply(0);            // "NPC greets"
            Dispatcher.UIThread.RunJobs();
            vm.SelectReply(0);            // "Ask name" -> "I am Bob"
            Dispatcher.UIThread.RunJobs();

            var coverageBefore = vm.Coverage.VisitedReplies;

            vm.GoBack();                  // step back to "NPC greets"
            Dispatcher.UIThread.RunJobs();

            // Coverage is not reverted by Back; the "Ask name" reply stays recorded.
            Assert.Equal(coverageBefore, vm.Coverage.VisitedReplies);
        }
    }
}
