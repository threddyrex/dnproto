
namespace dnproto.log;

public class ConsoleLogDestination : ILogDestination
{
    public void WriteTrace(string? message)
    {
        WriteMessage(message);
    }
    public void WriteInfo(string? message)
    {
        WriteMessage(message);
    }
    public void WriteWarning(string? message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        WriteMessage(message);
        Console.ResetColor();
    }
    public void WriteError(string? message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        WriteMessage(message);
        Console.ResetColor();
    }

    private void WriteMessage(string? message)
    {
        Console.WriteLine($"{message}");
    }
}
