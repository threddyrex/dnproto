
namespace dnproto.cli.commands;

/// <summary>
/// Show help/usage. This command is used if no command is specified.
/// </summary>
public class Help : BaseCommand
{        
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        Logger.LogInfo("Usage:");
        Logger.LogInfo("    dnproto /command commandname [/arg1 val1 /arg2 val2]");
        Logger.LogInfo("Available commands:");
        var commands = CommandLineInterface.GetAllCommandTypes();
        foreach (var command in commands.OrderBy(c => c.Name))
        {
            Logger.LogInfo("    " + command.Name);
        }
    }
}