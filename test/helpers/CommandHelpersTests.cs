namespace dnproto.tests.helpers;

using dnproto.commands;
using dnproto.helpers;

public class CommandHelpersTests
{

    [Fact]
    public void ParseArguments_OddLength()
    {
        Assert.Throws<Exception>(() => CommandHelpers.ParseArguments(new string[]{"one"}));
    }

    [Fact]
    public void ParseArguments_NoSlashes()
    {
        Assert.Throws<Exception>(() => CommandHelpers.ParseArguments(new string[]{"one", "two"}));
    }

    [Fact]
    public void ParseArguments_Correct()
    {
        var ret = CommandHelpers.ParseArguments(new string[]{"/one", "two"});
        Assert.NotNull(ret);
        Assert.Single(ret.Keys);
        Assert.Equal("two", ret["one"]);
    }

    [Fact]
    public void TryFindCommandType_NotFound()
    {
        Assert.Null(CommandHelpers.TryFindCommandType("notfound"));
    }


    [Fact]
    public void TryFindCommandType_Correct()
    {
        Assert.NotNull(CommandHelpers.TryFindCommandType("HelloWorld"));
    }

    [Fact]
    public void TryFindCommandType_CorrectLowercase()
    {
        Assert.NotNull(CommandHelpers.TryFindCommandType("helloWorld"));
    }

    [Fact]
    public void AssertArguments_NoRequired()
    {
        var command = new HelloWorld();
        var args = new Dictionary<string, string>();
        CommandHelpers.AssertArguments(command, args);
    }


    [Fact]
    public void AssertArguments_UnknownThrows()
    {
        var command = new HelloWorld();
        var args = CommandHelpers.ParseArguments(new string[]{"/unknown", "value"});
        Assert.Throws<ArgumentException>(() => CommandHelpers.AssertArguments(command, args));
    }


    [Fact]
    public void GetAllCommandTypes()
    {
        var types = CommandHelpers.GetAllCommandTypes();
        Assert.True(types.Count > 2);
    }
}
