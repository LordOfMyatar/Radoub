using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for TTS / external-process argument construction (#2260).
    /// Args must be built as a token list for ProcessStartInfo.ArgumentList — no manual
    /// quoting or shell escaping. ArgumentList passes each token verbatim to the child,
    /// so shell metacharacters (backticks, $(), quotes) in the spoken text must survive
    /// as literal data rather than being mangled by a naive Replace("\"","\\\"").
    /// </summary>
    public class TtsArgumentBuilderTests
    {
        [Fact]
        public void Espeak_BuildArguments_IncludesSpeedVoiceAndVerbatimText()
        {
            var args = EspeakTtsService.BuildArguments(speed: 175, espeakVoiceCode: "en+m3", text: "Hello world");

            Assert.Equal("-s", args[0]);
            Assert.Equal("175", args[1]);
            Assert.Equal("-v", args[2]);
            Assert.Equal("en+m3", args[3]);
            Assert.Equal("Hello world", args[^1]);
        }

        [Fact]
        public void Espeak_BuildArguments_DoesNotEscapeOrQuoteShellMetacharacters()
        {
            var text = "say $(whoami) and `id` \"quoted\"";
            var args = EspeakTtsService.BuildArguments(speed: 175, espeakVoiceCode: null, text: text);

            // Text token must be the literal string — no backslash-escaping, no wrapping quotes.
            Assert.Equal(text, args[^1]);
            Assert.DoesNotContain("\\\"", args[^1]);
        }

        [Fact]
        public void Espeak_BuildArguments_OmitsVoiceWhenNull()
        {
            var args = EspeakTtsService.BuildArguments(speed: 200, espeakVoiceCode: null, text: "hi");

            Assert.DoesNotContain("-v", args);
            Assert.Equal("-s", args[0]);
            Assert.Equal("200", args[1]);
            Assert.Equal("hi", args[2]);
        }

        [Fact]
        public void MacOsSay_BuildArguments_IncludesRateVoiceAndVerbatimText()
        {
            var args = MacOsSayTtsService.BuildArguments(rate: 175, voiceName: "Alex", text: "Hello $(id)");

            Assert.Equal("-r", args[0]);
            Assert.Equal("175", args[1]);
            Assert.Equal("-v", args[2]);
            Assert.Equal("Alex", args[3]);
            Assert.Equal("Hello $(id)", args[^1]);
        }

        [Fact]
        public void MacOsSay_BuildArguments_OmitsVoiceWhenEmpty()
        {
            var args = MacOsSayTtsService.BuildArguments(rate: 150, voiceName: "", text: "hi");

            Assert.DoesNotContain("-v", args);
        }

        [Fact]
        public void Piper_BuildArguments_IncludesModelOutputAndScaleVerbatim()
        {
            var args = PiperTtsService.BuildArguments(
                modelPath: "/tmp/voice model.onnx",
                outputFile: "/tmp/out file.wav",
                lengthScale: 1.25);

            Assert.Equal("--model", args[0]);
            Assert.Equal("/tmp/voice model.onnx", args[1]); // path with space, no quotes added
            Assert.Equal("--output_file", args[2]);
            Assert.Equal("/tmp/out file.wav", args[3]);
            Assert.Equal("--length_scale", args[4]);
            Assert.Equal("1.25", args[5]);
        }
    }
}
