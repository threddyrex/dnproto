namespace dnproto.tests.commands;

using dnproto.commands;

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
    public void FindCommandType_NotFound()
    {
        Assert.Throws<Exception>(() => CommandHelpers.FindCommandType("notfound"));
    }


    [Fact]
    public void FindCommandType_Correct()
    {
        Assert.NotNull(CommandHelpers.FindCommandType("HelloWorld"));
    }

    [Fact]
    public void FindCommandType_CorrectLowercase()
    {
        Assert.NotNull(CommandHelpers.FindCommandType("helloWorld"));
    }
}
