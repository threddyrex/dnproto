

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using dnproto.log;
using dnproto.pds.xrpc;

namespace dnproto.pds
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class Pds
    {
        private readonly BaseLogger Logger;

        private readonly LocalFileSystem lfs;

        public Pds(BaseLogger logger, LocalFileSystem lfs)
        {
            Logger = logger;
            this.lfs = lfs;
        }


        public void Run()
        {
            //
            // Get PDS config path
            //
            string? pdsConfigPath = lfs.GetPath_PdsConfig();
            if (string.IsNullOrEmpty(pdsConfigPath))
            {
                Logger.LogError("PDS config path is null or empty.");
                return;
            }

            if (File.Exists(pdsConfigPath) == false)
            {
                Logger.LogError($"PDS config file does not exist: {pdsConfigPath}");
                return;
            }

            //
            // Load config
            //
            Logger.LogInfo($"Loading PDS config from: {pdsConfigPath}");
            PdsConfig? pdsConfig = PdsConfig.LoadFromFile(Logger, pdsConfigPath);
            if (pdsConfig == null)
            {
                Logger.LogError("Failed to load PDS config.");
                return;
            }

            //
            // Configure to listen on specified port with HTTPS
            //
            Logger.LogInfo($"Starting minimal API with HTTPS on port {pdsConfig.Port}...");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"https://{pdsConfig.Host}:{pdsConfig.Port}");
            var app = builder.Build();

            //
            // Enable HTTPS redirection
            //
            app.UseHttpsRedirection();


            //
            // Map endpoints
            //
            XrpcEndpoints.MapEndpoints(app, Logger, pdsConfig);

            //
            // run
            //
            app.Run();
        }


    }

}
