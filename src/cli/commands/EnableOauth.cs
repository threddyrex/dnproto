

using dnproto.pds;


namespace dnproto.cli.commands
{

    /// <summary>
    /// </summary>
    public class EnableOauth : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"enabled"});
        }


        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get Arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? enabledStr = CommandLineInterface.GetArgumentValue(arguments, "enabled");
            bool enable;

            if(!bool.TryParse(enabledStr, out enable))
            {
                throw new Exception("Invalid value for 'enabled' argument");
            }


            PdsDb pdsDb = PdsDb.ConnectPdsDb(LocalFileSystem!, Logger);
            Logger.LogInfo($"Setting OAuth enabled to {enable}");
            pdsDb.SetConfigPropertyBool("FeatureEnabled_Oauth", enable);
            Logger.LogInfo($"OAuth enabled: {pdsDb.GetConfigPropertyBool("FeatureEnabled_Oauth")}");
        }
    }
}
