

using dnproto.pds;


namespace dnproto.cli.commands;

/// <summary>
/// Install config
/// </summary>
public class InstallFeatureConfig : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{});
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Install config
        //
        if(LocalFileSystem is null)
        {
            throw new Exception("LocalFileSystem is null");
        }

        Installer.InstallFeatureConfig(LocalFileSystem, Logger);

    }
}