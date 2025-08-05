namespace dnproto.tests.utils;


using dnproto.cli;
using dnproto.cli.commands;
using dnproto.utils;

public class CommandLineInterfaceTests
{

    [Fact]
    public void ParseArguments_OddLength()
    {
        Assert.Throws<Exception>(() => CommandLineInterface.ParseArguments(new string[]{"one"}));
    }

    [Fact]
    public void ParseArguments_NoSlashes()
    {
        Assert.Throws<Exception>(() => CommandLineInterface.ParseArguments(new string[]{"one", "two"}));
    }

    [Fact]
    public void ParseArguments_Correct()
    {
        var ret = CommandLineInterface.ParseArguments(new string[]{"/one", "two"});
        Assert.NotNull(ret);
        Assert.Single(ret.Keys);
        Assert.Equal("two", ret["one"]);
    }

    [Fact]
    public void TryFindCommandType_NotFound()
    {
        Assert.Null(CommandLineInterface.TryFindCommandType("notfound"));
    }


    [Fact]
    public void TryFindCommandType_Correct()
    {
        Assert.NotNull(CommandLineInterface.TryFindCommandType("HelloWorld"));
    }

    [Fact]
    public void TryFindCommandType_CorrectLowercase()
    {
        Assert.NotNull(CommandLineInterface.TryFindCommandType("helloWorld"));
    }

    [Fact]
    public void AssertArguments_NoRequired()
    {
        var command = new HelloWorld();
        var args = new Dictionary<string, string>();
        Assert.True(CommandLineInterface.CheckArguments(command, args));
    }


    [Fact]
    public void CheckArguments_UnknownThrows()
    {
        var command = new HelloWorld();
        var args = CommandLineInterface.ParseArguments(new string[]{"/unknown", "value"});
        Assert.False(CommandLineInterface.CheckArguments(command, args));
    }


    [Fact]
    public void GetAllCommandTypes()
    {
        var types = CommandLineInterface.GetAllCommandTypes();
        Assert.True(types.Count > 2);
    }
}
