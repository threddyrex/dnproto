namespace dnproto.tests.utils;

using dnproto.commands;
using dnproto.utils;

public class LocalStateHelpersTests
{
    [Fact]
    public void WriteAndReadProperty()
    {
        string dateTime = DateTime.Now.ToString();
        LocalStateSession.WriteSessionProperty("test_WriteAndReadProperty", dateTime);
        Assert.Equal(dateTime, LocalStateSession.ReadSessionProperty("test_WriteAndReadProperty"));
    }
}
