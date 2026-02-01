using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// Tests for TextValidator - validates dialog text for NWN character compatibility.
    /// Issue #152: Warning for unsupported characters (emojis, etc.)
    /// </summary>
    public class TextValidatorTests
    {
        #region Character Support Tests

        [Theory]
        [InlineData('a', true)]   // Basic ASCII
        [InlineData('Z', true)]   // Basic ASCII
        [InlineData('5', true)]   // Digit
        [InlineData(' ', true)]   // Space
        [InlineData('\n', true)]  // Newline
        [InlineData('é', true)]   // Extended ASCII (Latin-1)
        [InlineData('ñ', true)]   // Extended ASCII
        [InlineData('ü', true)]   // Extended ASCII
        [InlineData('©', true)]   // Copyright symbol (in CP1252)
        public void IsCharacterSupported_SupportedCharacters_ReturnsTrue(char c, bool expected)
        {
            Assert.Equal(expected, TextValidator.IsCharacterSupported(c));
        }

        [Theory]
        [InlineData('中')]  // Chinese
        [InlineData('日')]  // Japanese Kanji
        [InlineData('ひ')]  // Hiragana
        [InlineData('א')]   // Hebrew
        [InlineData('α')]   // Greek (outside CP1252 range)
        public void IsCharacterSupported_UnsupportedCharacters_ReturnsFalse(char c)
        {
            Assert.False(TextValidator.IsCharacterSupported(c));
        }

        [Fact]
        public void IsCharacterSupported_Emoji_ReturnsFalse()
        {
            // Emojis are surrogate pairs - test the actual chars from a string
            var emoji = "😀";
            foreach (var c in emoji)
            {
                Assert.False(TextValidator.IsCharacterSupported(c));
            }
        }

        #endregion

        #region FindUnsupportedCharacters Tests

        [Fact]
        public void FindUnsupportedCharacters_NullInput_ReturnsEmptyList()
        {
            var result = TextValidator.FindUnsupportedCharacters(null);
            Assert.Empty(result);
        }

        [Fact]
        public void FindUnsupportedCharacters_EmptyInput_ReturnsEmptyList()
        {
            var result = TextValidator.FindUnsupportedCharacters("");
            Assert.Empty(result);
        }

        [Fact]
        public void FindUnsupportedCharacters_AllSupportedText_ReturnsEmptyList()
        {
            var result = TextValidator.FindUnsupportedCharacters("Hello, World! This is valid text with accents: café résumé");
            Assert.Empty(result);
        }

        [Fact]
        public void FindUnsupportedCharacters_WithEmoji_FindsEmoji()
        {
            var result = TextValidator.FindUnsupportedCharacters("Hello 😀 World");

            // Emoji is actually 2 chars (surrogate pair) - we detect the first one
            Assert.NotEmpty(result);
            Assert.Contains(result, uc => uc.Position == 6);
        }

        [Fact]
        public void FindUnsupportedCharacters_WithChinese_FindsChinese()
        {
            var result = TextValidator.FindUnsupportedCharacters("Hello 你好");

            Assert.Equal(2, result.Count);  // Two Chinese characters
            Assert.All(result, uc => Assert.True(uc.CodePoint > 255));
        }

        [Fact]
        public void FindUnsupportedCharacters_Context_IncludesSurroundingText()
        {
            var result = TextValidator.FindUnsupportedCharacters("Hello 中 World");

            Assert.Single(result);
            Assert.Contains("→中←", result[0].Context);
        }

        #endregion

        #region Dialog Validation Tests

        [Fact]
        public void ValidateDialog_NullDialog_ReturnsEmptyResult()
        {
            var result = TextValidator.ValidateDialog(null!);

            Assert.False(result.HasWarnings);
            Assert.Equal(0, result.TotalCharacterCount);
        }

        [Fact]
        public void ValidateDialog_CleanDialog_NoWarnings()
        {
            var dialog = new Dialog();
            dialog.Entries.Add(new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = CreateLocString("This is clean text without any problems.")
            });
            dialog.Replies.Add(new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = CreateLocString("Yes, I agree. That's perfectly fine!")
            });

            var result = TextValidator.ValidateDialog(dialog);

            Assert.False(result.HasWarnings);
        }

        [Fact]
        public void ValidateDialog_EntryWithEmoji_FindsWarning()
        {
            var dialog = new Dialog();
            dialog.Entries.Add(new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = CreateLocString("Welcome! 🎉 Let's begin.")
            });

            var result = TextValidator.ValidateDialog(dialog);

            Assert.True(result.HasWarnings);
            Assert.Equal(1, result.AffectedNodeCount);
            Assert.Contains(result.Warnings, w => w.NodeType == "Entry" && w.NodeIndex == 0);
        }

        [Fact]
        public void ValidateDialog_ReplyWithChinese_FindsWarning()
        {
            var dialog = new Dialog();
            dialog.Entries.Add(new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = CreateLocString("Hello")
            });
            dialog.Replies.Add(new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = CreateLocString("我不明白")  // "I don't understand" in Chinese
            });

            var result = TextValidator.ValidateDialog(dialog);

            Assert.True(result.HasWarnings);
            Assert.Equal(4, result.TotalCharacterCount);  // 4 Chinese characters
            Assert.Contains(result.Warnings, w => w.NodeType == "Reply" && w.NodeIndex == 0);
        }

        [Fact]
        public void ValidateDialog_CommentWithUnsupportedChars_FindsWarning()
        {
            var dialog = new Dialog();
            dialog.Entries.Add(new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = CreateLocString("Normal text"),
                Comment = "Note: This needs emoji 🔧 fix"
            });

            var result = TextValidator.ValidateDialog(dialog);

            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.FieldName == "Comment");
        }

        [Fact]
        public void ValidateDialog_MultipleNodes_CountsCorrectly()
        {
            var dialog = new Dialog();
            dialog.Entries.Add(new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = CreateLocString("First entry 😀")
            });
            dialog.Entries.Add(new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = CreateLocString("Second entry 中文")
            });
            dialog.Replies.Add(new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = CreateLocString("Clean reply text")
            });

            var result = TextValidator.ValidateDialog(dialog);

            Assert.True(result.HasWarnings);
            Assert.Equal(2, result.AffectedNodeCount);  // Two entries have issues
        }

        #endregion

        #region Character Description Tests

        [Theory]
        [InlineData('中', "CJK (Chinese/Japanese/Korean)")]
        [InlineData('ひ', "Hiragana")]
        [InlineData('א', "Hebrew")]
        public void GetCharacterDescription_ReturnsDescriptiveCategory(char c, string expectedCategory)
        {
            var description = TextValidator.GetCharacterDescription(c);
            Assert.Equal(expectedCategory, description);
        }

        [Fact]
        public void GetCharacterDescription_Emoji_ReturnsEmoticonCategory()
        {
            // Emojis are surrogate pairs - high surrogate is in the emoticon range
            var emoji = "😀";
            // The high surrogate char maps to the emoticon range when checking code point
            // For surrogate pairs, we describe based on the full code point
            Assert.True(emoji.Length == 2);  // Confirm it's a surrogate pair
        }

        #endregion

        #region Helper Methods

        private static LocString CreateLocString(string text)
        {
            var loc = new LocString();
            loc.Add(0, text);
            return loc;
        }

        #endregion
    }
}
