using System.Collections.Generic;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Unit tests for ProcessArgumentBuilder — pure helpers that produce
/// ProcessStartInfo.ArgumentList entries (one element per argument, NO surrounding
/// quotes). Replaces the old string-concat-with-quotes pattern flagged in #2248,
/// which broke when a file path contained a literal double-quote.
/// </summary>
public class ProcessArgumentBuilderTests
{
    [Fact]
    public void FileOpenArgs_EmitsFileFlagAndRawPath_NoQuotes()
    {
        var args = ProcessArgumentBuilder.FileOpenArgs(@"C:\Users\me\my dialog.dlg");

        Assert.Equal(new[] { "--file", @"C:\Users\me\my dialog.dlg" }, args);
    }

    [Fact]
    public void FileOpenArgs_PathWithLiteralQuote_PassedThroughVerbatim()
    {
        // A path containing a double-quote is legal on Linux/macOS. The old
        // $"--file \"{path}\"" concat would corrupt parsing; ArgumentList must
        // carry the raw path so the OS layer escapes it correctly.
        var weird = "/home/me/od\"d.dlg";
        var args = ProcessArgumentBuilder.FileOpenArgs(weird);

        Assert.Equal(new[] { "--file", weird }, args);
    }

    [Fact]
    public void SingleFileArg_EmitsRawPath_NoQuotes()
    {
        var args = ProcessArgumentBuilder.SingleFileArg(@"C:\tmp\a b.txt");

        Assert.Equal(new[] { @"C:\tmp\a b.txt" }, args);
    }

    [Fact]
    public void CompileArgs_SingleScript_NoGamePath()
    {
        var args = ProcessArgumentBuilder.CompileArgs(
            new[] { @"C:\m\scr.nss" }, gamePath: null);

        Assert.Equal(new[] { "-c", @"C:\m\scr.nss" }, args);
    }

    [Fact]
    public void CompileArgs_SingleScript_WithGamePath()
    {
        var args = ProcessArgumentBuilder.CompileArgs(
            new[] { @"C:\m\scr.nss" }, gamePath: @"C:\Games\NWN");

        Assert.Equal(
            new[] { "-c", @"C:\m\scr.nss", "--root", @"C:\Games\NWN" }, args);
    }

    [Fact]
    public void CompileArgs_Batch_AppendsContinueAndThreads()
    {
        var args = ProcessArgumentBuilder.CompileArgs(
            new[] { @"C:\m\a.nss", @"C:\m\b.nss" },
            gamePath: @"C:\Games\NWN",
            continueOnError: true,
            threadCount: 4);

        Assert.Equal(new[]
        {
            "-c", @"C:\m\a.nss", @"C:\m\b.nss", "-y", "-j", "4", "--root", @"C:\Games\NWN"
        }, args);
    }

    [Fact]
    public void CompileArgs_EmptyGamePath_OmitsRoot()
    {
        var args = ProcessArgumentBuilder.CompileArgs(
            new[] { @"C:\m\scr.nss" }, gamePath: "");

        Assert.Equal(new[] { "-c", @"C:\m\scr.nss" }, args);
    }
}
