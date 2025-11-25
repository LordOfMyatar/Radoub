using System;
using System.IO;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    public class CommandLineServiceTests
    {
        [Fact]
        public void Parse_NoArgs_ReturnsEmptyOptions()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.Null(options.FilePath);
            Assert.False(options.SafeMode);
            Assert.False(options.ExportScreenplay);
            Assert.False(options.ShowHelp);
            Assert.Null(options.OutputFile);
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("/?")]
        public void Parse_HelpFlag_SetsShowHelp(string flag)
        {
            // Arrange
            var args = new[] { flag };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.True(options.ShowHelp);
        }

        [Theory]
        [InlineData("--safe-mode")]
        [InlineData("-s")]
        public void Parse_SafeModeFlag_SetsSafeMode(string flag)
        {
            // Arrange
            var args = new[] { flag };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.True(options.SafeMode);
        }

        [Fact]
        public void Parse_ScreenplayFlag_SetsExportScreenplay()
        {
            // Arrange
            var args = new[] { "--screenplay" };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.True(options.ExportScreenplay);
        }

        [Fact]
        public void Parse_DlgFile_SetsFilePath()
        {
            // Arrange
            var args = new[] { "test.dlg" };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.Equal("test.dlg", options.FilePath);
        }

        [Theory]
        [InlineData("-o", "output.txt")]
        [InlineData("--output", "output.txt")]
        public void Parse_OutputFlag_SetsOutputFile(string flag, string output)
        {
            // Arrange
            var args = new[] { flag, output };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.Equal(output, options.OutputFile);
        }

        [Fact]
        public void Parse_CombinedArgs_ParsesAllCorrectly()
        {
            // Arrange
            var args = new[] { "--safe-mode", "--screenplay", "-o", "out.txt", "dialog.dlg" };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert
            Assert.True(options.SafeMode);
            Assert.True(options.ExportScreenplay);
            Assert.Equal("out.txt", options.OutputFile);
            Assert.Equal("dialog.dlg", options.FilePath);
        }

        [Fact]
        public void Parse_FilePathWithoutExtension_AcceptsExistingFile()
        {
            // Arrange - create a temp file
            var tempFile = Path.GetTempFileName();
            try
            {
                var args = new[] { tempFile };

                // Act
                var options = CommandLineService.Parse(args);

                // Assert - existing files without .dlg extension should be accepted
                Assert.Equal(tempFile, options.FilePath);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void Parse_UnknownFlags_Ignored()
        {
            // Arrange
            var args = new[] { "--unknown", "-x", "test.dlg" };

            // Act
            var options = CommandLineService.Parse(args);

            // Assert - unknown flags are ignored, valid ones parsed
            Assert.Equal("test.dlg", options.FilePath);
            Assert.False(options.SafeMode);
            Assert.False(options.ShowHelp);
        }

        [Fact]
        public void PrintHelp_DoesNotThrow()
        {
            // Act & Assert - just verify it doesn't throw
            var exception = Record.Exception(() => CommandLineService.PrintHelp());
            Assert.Null(exception);
        }

        [Fact]
        public async Task ExportScreenplayAsync_NonExistentFile_ReturnsError()
        {
            // Arrange
            var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dlg");

            // Act
            var result = await CommandLineService.ExportScreenplayAsync(nonExistentFile, null);

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public void Options_StaticProperty_ReturnsParsedOptions()
        {
            // Arrange
            var args = new[] { "--safe-mode" };
            CommandLineService.Parse(args);

            // Act
            var options = CommandLineService.Options;

            // Assert
            Assert.True(options.SafeMode);
        }
    }
}
