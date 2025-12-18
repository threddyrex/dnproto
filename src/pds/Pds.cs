

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using dnproto.sdk.log;
using dnproto.sdk.fs;
using dnproto.pds.db;
using dnproto.pds.xrpc;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class Pds
    {
        public required PdsConfig PdsConfig;

        public required BaseLogger Logger;

        public required LocalFileSystem LocalFileSystem;

        public required PdsDb PdsDb;

        public required WebApplication App;


        public static Pds? InitializePds(string? dataDir, BaseLogger logger)
        {
            //
            // Get local file system
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, logger);
            if (lfs == null)
            {
                logger.LogError("Failed to initialize LocalFileSystem.");
                return null;
            }


            //
            // Get PDS config path
            //
            string? pdsConfigPath = lfs.GetPath_PdsConfig();
            if (string.IsNullOrEmpty(pdsConfigPath))
            {
                logger.LogError("PDS config path is null or empty.");
                return null;
            }

            if (File.Exists(pdsConfigPath) == false)
            {
                logger.LogError($"PDS config file does not exist: {pdsConfigPath}");
                return null;
            }

            //
            // Load config
            //
            logger.LogInfo($"Loading PDS config from: {pdsConfigPath}");
            PdsConfig? pdsConfig = PdsConfig.LoadFromFile(logger, pdsConfigPath);
            if (pdsConfig == null)
            {
                logger.LogError("Failed to load PDS config.");
                return null;
            }

                //
            // Initialize PdsDb
            //
            PdsDb? pdsDb = PdsDb.InitializePdsDb(pdsConfig, lfs.DataDir, logger);
            if (pdsDb == null)
            {
                logger.LogError("Failed to initialize PDS database.");
                return null;
            }


            //
            // Configure to listen on specified port with HTTPS
            //
            logger.LogInfo($"Starting minimal API with HTTPS on port {pdsConfig.Port}...");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"https://{pdsConfig.Host}:{pdsConfig.Port}");
            var app = builder.Build();

            //
            // Construct pds object
            //
            var pds = new Pds()
            {
                PdsConfig = pdsConfig,
                Logger = logger,
                LocalFileSystem = lfs,
                PdsDb = pdsDb,
                App = app
            };



            //
            // Enable HTTPS redirection
            //
            app.UseHttpsRedirection();


            //
            // Map endpoints
            //
            pds.MapEndpoints();

            //
            // return
            //
            return pds;
        }
        
        public void Run()
        {
            Logger.LogInfo("Running PDS...");
            App.Run();
        }
        

        private void MapEndpoints()
        {
            App.MapGet("/hello", (HttpContext context) => new Hello(){Pds = this, HttpContext = context}.GetResponse());
            App.MapGet("/xrpc/_health", (HttpContext context) => new Health(){Pds = this, HttpContext = context}.GetResponse());
            App.MapGet("/xrpc/com.atproto.server.describeServer", (HttpContext context) => new ComAtprotoServer_DescribeServer(){Pds = this, HttpContext = context}.GetResponse());
            App.MapGet("/xrpc/com.atproto.identity.resolveHandle", (HttpContext context) => new ComAtprotoIdentity_ResolveHandle(){Pds = this, HttpContext = context}.GetResponse());
            App.MapPost("/xrpc/com.atproto.server.createInviteCode", (HttpContext context) => new ComAtprotoServer_CreateInviteCode(){Pds = this, HttpContext = context}.GetResponse());

            Logger.LogInfo("");
            Logger.LogInfo("Mapped XRPC endpoints:");
            Logger.LogInfo("");
            Logger.LogInfo($"https://{PdsConfig.Host}:{PdsConfig.Port}/hello");
            Logger.LogInfo($"https://{PdsConfig.Host}:{PdsConfig.Port}/xrpc/_health");
            Logger.LogInfo($"https://{PdsConfig.Host}:{PdsConfig.Port}/xrpc/com.atproto.server.describeServer");
            Logger.LogInfo($"https://{PdsConfig.Host}:{PdsConfig.Port}/xrpc/com.atproto.identity.resolveHandle");
            Logger.LogInfo($"https://{PdsConfig.Host}:{PdsConfig.Port}/xrpc/com.atproto.server.createInviteCode");
            Logger.LogInfo("");
        }
    }

}
