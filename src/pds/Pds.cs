

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using dnproto.log;

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
            builder.WebHost.UseUrls($"https://localhost:{pdsConfig.Port}");            
            var app = builder.Build();

            // Enable HTTPS redirection
            app.UseHttpsRedirection();

            // Define the /hello endpoint
            app.MapGet("/hello", () => "world");

            Logger.LogInfo($"Server running at https://localhost:{pdsConfig.Port}/hello");
            
            app.Run();
        }
    }
}
