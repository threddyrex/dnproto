namespace dnproto.tests.cli;


using dnproto.cli;
using dnproto.cli.commands;
using dnproto.log;

public class CommandLineInterfaceTests
{

    [Fact]
    public void ParseArguments_NoSlashes()
    {
        Assert.Throws<Exception>(() => CommandLineInterface.ParseArguments(new string[]{"one", "two"}, new Logger()));
    }

    [Fact]
    public void ParseArguments_Correct()
    {
        var ret = CommandLineInterface.ParseArguments(new string[]{"/one", "two"}, new Logger());
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
        var args = CommandLineInterface.ParseArguments(new string[]{"/unknown", "value"}, new Logger());
        Assert.NotNull(args);
        Assert.False(CommandLineInterface.CheckArguments(command, args));
    }


    [Fact]
    public void GetAllCommandTypes()
    {
        var types = CommandLineInterface.GetAllCommandTypes();
        Assert.True(types.Count > 2);
    }
}
