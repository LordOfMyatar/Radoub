using Xunit;
using Radoub.UI.Services;

namespace Radoub.UI.Tests;

public class TokenInsertionHelperTests
{
    [Fact]
    public void ComputeInsertion_EmptyField_InsertsDirectly()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "", selectionStart: 0, selectionLength: 0, token: "<FirstName>");
        Assert.Equal("<FirstName>", result.NewText);
        Assert.Equal(11, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_MidText_AddsLeadingSpace()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Hello world", selectionStart: 5, selectionLength: 0, token: "<FirstName>");
        Assert.Equal("Hello <FirstName>world", result.NewText);
        Assert.Equal(17, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_AfterWhitespace_NoLeadingSpace()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Hello ", selectionStart: 6, selectionLength: 0, token: "<FirstName>");
        Assert.Equal("Hello <FirstName>", result.NewText);
        Assert.Equal(17, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_AtPositionZero_NoLeadingSpace()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Hello", selectionStart: 0, selectionLength: 0, token: "<FirstName>");
        Assert.Equal("<FirstName>Hello", result.NewText);
        Assert.Equal(11, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_AfterNewline_NoLeadingSpace()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Line1\n", selectionStart: 6, selectionLength: 0, token: "<FirstName>");
        Assert.Equal("Line1\n<FirstName>", result.NewText);
        Assert.Equal(17, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_WithSelection_ReplacesSelection()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Hello world", selectionStart: 6, selectionLength: 5, token: "<FirstName>");
        Assert.Equal("Hello <FirstName>", result.NewText);
        Assert.Equal(17, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_SelectionAtStart_NoLeadingSpace()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Hello", selectionStart: 0, selectionLength: 5, token: "<FirstName>");
        Assert.Equal("<FirstName>", result.NewText);
        Assert.Equal(11, result.NewCaretPosition);
    }

    [Fact]
    public void ComputeInsertion_EndOfField_AddsLeadingSpace()
    {
        var result = TokenInsertionHelper.ComputeInsertion(
            currentText: "Hello", selectionStart: 5, selectionLength: 0, token: "<FirstName>");
        Assert.Equal("Hello <FirstName>", result.NewText);
        Assert.Equal(17, result.NewCaretPosition);
    }
}
