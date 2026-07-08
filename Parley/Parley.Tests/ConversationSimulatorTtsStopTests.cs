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
    /// #2523: The Stop button must halt in-progress TTS and must NOT let the
    /// completion event fired by cancellation advance the conversation or restart speech.
    /// The Windows synthesizer's SpeakAsyncCancelAll() raises SpeakCompleted (Cancelled=true),
    /// which is exactly what MockTtsService.Stop() simulates. The auto-advance runs via
    /// Dispatcher.UIThread.Post, so these use [AvaloniaFact] + RunJobs() to pump those jobs.
    /// </summary>
    public class ConversationSimulatorTtsStopTests
    {
        private static void EnableTts()
        {
            var settings = Program.Services.GetRequiredService<DialogEditor.Services.ISettingsService>();
            settings.SimulatorTtsEnabled = true;
            settings.SimulatorAutoSpeak = true;
            settings.SimulatorAutoAdvance = true;
        }

        /// <summary>
        /// Builds: START -> Entry("NPC greets") -> Reply("Hi") -> Entry("NPC replies").
        /// </summary>
        private static Dialog BuildTwoStepDialog()
        {
            var dialog = new Dialog();

            var entry1 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry1.Text.Add(0, "NPC greets");
            dialog.AddNodeInternal(entry1, entry1.Type);

            var reply = dialog.CreateNode(DialogNodeType.Reply)!;
            reply.Text.Add(0, "Hi");
            dialog.AddNodeInternal(reply, reply.Type);

            var entry2 = dialog.CreateNode(DialogNodeType.Entry)!;
            entry2.Text.Add(0, "NPC replies");
            dialog.AddNodeInternal(entry2, entry2.Type);

            var e1ToReply = dialog.CreatePtr()!;
            e1ToReply.Type = DialogNodeType.Reply;
            e1ToReply.Node = reply;
            entry1.Pointers.Add(e1ToReply);

            var replyToE2 = dialog.CreatePtr()!;
            replyToE2.Type = DialogNodeType.Entry;
            replyToE2.Node = entry2;
            reply.Pointers.Add(replyToE2);

            var start = dialog.CreatePtr()!;
            start.Type = DialogNodeType.Entry;
            start.Node = entry1;
            dialog.Starts.Add(start);

            return dialog;
        }

        [AvaloniaFact]
        public void StopSpeaking_WhileSpeakingPcReply_DoesNotAdvanceOrRestartSpeech()
        {
            EnableTts();
            var tts = new MockTtsService();
            var vm = new ConversationSimulatorViewModel(BuildTwoStepDialog(), "test-stop-1.dlg", tts);

            vm.StartConversation();      // shows root entry
            vm.SelectReply(0);           // choose the root entry -> NPC entry1 + its reply (auto-speaks NPC1)
            Dispatcher.UIThread.RunJobs();
            vm.SelectReply(0);           // choose the PC reply -> speaks PC reply, pends advance
            Dispatcher.UIThread.RunJobs();

            int spokenBeforeStop = tts.SpokenTexts.Count;

            vm.StopSpeaking();           // user presses Stop; mock fires SpeakCompleted (cancellation)
            Dispatcher.UIThread.RunJobs();

            // Bug: the cancellation-fired completion advances to entry2 and auto-speaks it.
            Assert.Equal(spokenBeforeStop, tts.SpokenTexts.Count);
            Assert.DoesNotContain("NPC replies", tts.SpokenTexts);
        }

        [AvaloniaFact]
        public void StopSpeaking_ClearsPendingAutoAdvanceState()
        {
            EnableTts();
            var tts = new MockTtsService();
            var vm = new ConversationSimulatorViewModel(BuildTwoStepDialog(), "test-stop-2.dlg", tts);

            vm.StartConversation();
            vm.SelectReply(0);
            Dispatcher.UIThread.RunJobs();
            vm.SelectReply(0);           // now mid-speaking PC reply with a pending advance
            Dispatcher.UIThread.RunJobs();

            vm.StopSpeaking();
            Dispatcher.UIThread.RunJobs();

            // The pending PC-reply advance must be discarded: a subsequent stray completion
            // must never advance to the next NPC entry ("NPC replies") that the cancelled
            // PC reply pointed to.
            tts.CompleteSpeech();
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("NPC replies", tts.SpokenTexts);
        }
    }
}
