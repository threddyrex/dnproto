

using dnproto.pds;


namespace dnproto.cli.commands;

/// <summary>
/// Install config
/// </summary>
public class InstallConfig : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"listenScheme", "listenHost", "listenPort"});
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get Arguments
        //
        string? listenScheme = CommandLineInterface.GetArgumentValue(arguments, "listenScheme");
        string? listenHost = CommandLineInterface.GetArgumentValue(arguments, "listenHost");
        string? listenPort = CommandLineInterface.GetArgumentValue(arguments, "listenPort");
        int listenPortInt = int.Parse(listenPort!);

        //
        // Install config
        //
        if(LocalFileSystem is null)
        {
            throw new Exception("LocalFileSystem is null");
        }

        Installer.InstallConfig(LocalFileSystem, Logger, listenScheme!, listenHost!, listenPortInt);

        //
        // Log
        //
        PdsDb db = PdsDb.ConnectPdsDb(LocalFileSystem, Logger);
        Logger.LogInfo($"ServerListenScheme: {db.GetConfigProperty("ServerListenScheme")}");
        Logger.LogInfo($"ServerListenHost: {db.GetConfigProperty("ServerListenHost")}");
        Logger.LogInfo($"ServerListenPort: {db.GetConfigProperty("ServerListenPort")}");
    }
}