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
    /// Regression guard: single-reply auto-advance (AutoAdvance on, AutoSpeak off) must still
    /// chain forward after the #2524 Back-button snapshot push and _suppressAutoSpeak guard.
    /// </summary>
    public class ConversationSimulatorAutoAdvanceTests
    {
        private static void SetToggles(bool autoAdvance, bool autoSpeak, bool ttsEnabled)
        {
            var s = Program.Services.GetRequiredService<DialogEditor.Services.ISettingsService>();
            s.SimulatorAutoAdvance = autoAdvance;
            s.SimulatorAutoSpeak = autoSpeak;
            s.SimulatorTtsEnabled = ttsEnabled;
        }

        /// <summary>
        /// START -> Entry("A") -> Reply("only-1") -> Entry("B") -> Reply("only-2") -> Entry("C") (leaf reply -> ends)
        /// Every NPC entry has exactly one reply, so AutoAdvance should walk to the end from the first pick.
        /// </summary>
        private static Dialog BuildSingleReplyChain()
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
            // r3 has no pointers -> ends

            var start = dialog.CreatePtr()!;
            start.Type = DialogNodeType.Entry;
            start.Node = a;
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

        [AvaloniaFact]
        public void SingleReply_AutoAdvanceOn_AutoSpeakOff_WalksToEnd()
        {
            SetToggles(autoAdvance: true, autoSpeak: false, ttsEnabled: false);
            var vm = new ConversationSimulatorViewModel(BuildSingleReplyChain(), "test-aa.dlg", new MockTtsService());

            vm.StartConversation();
            vm.SelectReply(0);   // pick the root entry "A"; AutoAdvance should chain A -> B -> C -> end
            Dispatcher.UIThread.RunJobs();

            Assert.True(vm.HasEnded, "AutoAdvance with single replies should have walked to the conversation end.");
        }

        [AvaloniaFact]
        public void SingleReply_AutoAdvanceOn_AutoSpeakOn_WalksToEndAsSpeechCompletes()
        {
            SetToggles(autoAdvance: true, autoSpeak: true, ttsEnabled: true);
            var tts = new MockTtsService();
            var vm = new ConversationSimulatorViewModel(BuildSingleReplyChain(), "test-aa3.dlg", tts);

            vm.StartConversation();
            vm.SelectReply(0);   // pick root "A"; auto-speaks "A"
            Dispatcher.UIThread.RunJobs();

            // Drive speech completions; each completion drives the next auto-advance/auto-speak step.
            // Bound the loop so a regression (advance stops firing) fails via the assertion, not a hang.
            for (int i = 0; i < 20 && !vm.HasEnded; i++)
            {
                tts.CompleteSpeech();
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(vm.HasEnded,
                "AutoAdvance+AutoSpeak with single replies should walk to the end as each speech completes.");
        }

        [AvaloniaFact]
        public void SingleReply_AutoAdvanceOff_StopsAtFirstEntry()
        {
            SetToggles(autoAdvance: false, autoSpeak: false, ttsEnabled: false);
            var vm = new ConversationSimulatorViewModel(BuildSingleReplyChain(), "test-aa2.dlg", new MockTtsService());

            vm.StartConversation();
            vm.SelectReply(0);   // pick root "A"; without AutoAdvance it should stop showing "A" + its 1 reply
            Dispatcher.UIThread.RunJobs();

            Assert.False(vm.HasEnded);
            Assert.Equal("A", vm.NpcText);
            Assert.Single(vm.Replies);
        }
    }
}
