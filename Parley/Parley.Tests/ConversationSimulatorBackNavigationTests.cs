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
