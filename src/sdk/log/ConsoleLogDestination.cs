
namespace dnproto.sdk.log;

public class ConsoleLogDestination : ILogDestination
{
    public void WriteMessage(string? message)
    {
        Console.WriteLine($"{message}");
    }
}
