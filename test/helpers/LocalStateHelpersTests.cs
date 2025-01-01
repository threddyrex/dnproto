namespace dnproto.tests.helpers;

using dnproto.commands;
using dnproto.helpers;

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
