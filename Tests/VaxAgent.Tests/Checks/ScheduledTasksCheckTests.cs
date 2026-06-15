using System;
using Xunit;
using VaxDrive.VaxAgent.Checks;

namespace VaxDrive.VaxAgent.Tests.Checks;

public class ScheduledTasksCheckTests
{
    [Theory]
    [InlineData("TaskName,\"Task To Run\",\"Run As User\"", new[] { "TaskName", "Task To Run", "Run As User" })] // Normal row
    [InlineData("\"Task,Name\",\"Task To Run\",\"Run As User\"", new[] { "Task,Name", "Task To Run", "Run As User" })] // Quoted comma field
    [InlineData("\"Task\"\"Name\"\"\",\"Task To Run\",\"Run As User\"", new[] { "Task\"Name\"", "Task To Run", "Run As User" })] // This looks tricky in inline data due to escaping
    public void ParseCsvLine_HandlesQuotesCorrectly(string input, string[] expected)
    {
        string[]? actual = ScheduledTasksCheck.ParseCsvLine(input);
        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseCsvLine_EscapedQuotes_ParsesCorrectly()
    {
        string input = "\"Task\"\"Name\",\"Command\",\"User\"";
        string[] expected = { "Task\"Name", "Command", "User" };
        
        string[]? actual = ScheduledTasksCheck.ParseCsvLine(input);
        
        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseCsvLine_MalformedNoCloseQuote_ReturnsNull()
    {
        string input = "\"TaskName,\"Command\",\"User\"";
        
        string[]? actual = ScheduledTasksCheck.ParseCsvLine(input);
        
        Assert.Null(actual);
    }
}
