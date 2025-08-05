using dnproto.utils;

namespace dnproto.cli.commands;

/// <summary>
/// Show help/usage. This command is used if no command is specified.
/// </summary>
public class Help : BaseCommand
{        
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        Console.WriteLine("Usage:");
        Console.WriteLine();
        Console.WriteLine("    dnproto /command commandname [/arg1 val1 /arg2 val2]");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine();
        var commands = CommandLineInterface.GetAllCommandTypes();
        foreach (var command in commands.OrderBy(c => c.Name))
        {
            Console.WriteLine("    " + command.Name);
        }
    }
}