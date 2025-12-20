

using dnproto.pds;
using dnproto.pds.db;
using dnproto.sdk.auth;


namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class InitializePds : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"pdshostname", "availableuserdomain", "userHandle", "userDid"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get data dir
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? pdsHostname = CommandLineInterface.GetArgumentValue(arguments, "pdshostname");
            string? availableUserDomain = CommandLineInterface.GetArgumentValue(arguments, "availableuserdomain");
            string? userHandle = CommandLineInterface.GetArgumentValue(arguments, "userHandle");
            string? userDid = CommandLineInterface.GetArgumentValue(arguments, "userDid");

            //
            // Verify params
            //
            if (string.IsNullOrEmpty(dataDir))
            {
                Logger.LogError("dataDir argument is required.");
                return;
            }
            if (string.IsNullOrEmpty(pdsHostname))
            {
                Logger.LogError("pdshostname argument is required.");
                return;
            }
            if (string.IsNullOrEmpty(availableUserDomain))
            {
                Logger.LogError("availableuserdomain argument is required.");
                return;
            }
            if (string.IsNullOrEmpty(userHandle))
            {
                Logger.LogError("userHandle argument is required.");
                return;
            }
            if (string.IsNullOrEmpty(userDid))
            {
                Logger.LogError("userDid argument is required.");
                return;
            }


            //
            // Create fresh pds db
            //
            PdsDb? pdsDb = PdsDb.InitializePdsDb(dataDir!, Logger);
            if (pdsDb == null)
            {
                Logger.LogError("Failed to initialize PDS database.");
                return;
            }


            //
            // Create fresh config
            //
            var config = new dnproto.pds.db.Config();
            config.Version = "0.0.001";
            config.ListenHost = "localhost";
            config.ListenPort = 5001;
            config.PdsHostname = pdsHostname!;
            config.PdsDid = "did:web:" + pdsHostname!;
            config.AvailableUserDomain = availableUserDomain!;
            var adminPassword = PasswordHasher.CreateNewAdminPassword();
            config.AdminHashedPassword = PasswordHasher.HashPassword(adminPassword);
            config.JwtSecret = JwtSecret.GenerateJwtSecret();
            config.UserHandle = userHandle!;
            config.UserDid = userDid!;
            var userPassword = PasswordHasher.CreateNewAdminPassword();
            config.UserHashedPassword = PasswordHasher.HashPassword(userPassword!);


            //
            // Insert config into db
            //
            bool insertResult = pdsDb.InsertConfig(config);
            if (insertResult == false)
            {
                Logger.LogError("Failed to insert config into PDS database.");
                return;
            }



            //
            // Print out stuff that the user will need.
            //
            Logger.LogInfo("PDS initialized successfully.");
            Logger.LogInfo($"Admin password: {adminPassword}");
            Logger.LogInfo($"User password: {userPassword}");

            Logger.LogInfo($"Copy this powershell:\n\n$adminPassword = '{adminPassword}';\n$userHandle = '{userHandle}';\n$userPassword = '{userPassword}';\n\n to set the admin and user passwords in your environment for use with powershell.\n");
        }
    }
}
