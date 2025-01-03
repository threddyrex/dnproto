namespace dnproto.tests.utils;

using dnproto.commands;
using dnproto.utils;

public class LocalStateHelpersTests
{
    [Fact]
    public void WriteAndReadProperty()
    {
        string dateTime = DateTime.Now.ToString();
        LocalStateHelpers.WriteSessionProperty("test_WriteAndReadProperty", dateTime);
        Assert.Equal(dateTime, LocalStateHelpers.ReadSessionProperty("test_WriteAndReadProperty"));
    }
}
