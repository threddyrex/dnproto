using dnproto.pds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class RunPds : BaseCommand
    {
        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string> { "port" };
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            // Get port from arguments or use default
            var port = arguments.ContainsKey("port") ? arguments["port"] : "5001";

            new Pds(Logger, port).Run();
        }
    }
}
