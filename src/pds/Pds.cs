

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using dnproto.sdk.fs;
using dnproto.pds.db;
using dnproto.pds.xrpc;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds;


/// <summary>
/// Main class for pds implementation.
/// </summary>
public class Pds
{
    public required Config Config;

    public required dnproto.sdk.log.ILogger Logger;

    public required LocalFileSystem LocalFileSystem;

    public required PdsDb PdsDb;

    public required WebApplication App;

    public required Func<byte[], byte[]> CommitSigningFunction;


    /// <summary>
    /// Initializes the PDS. Loads config, initializes database, and sets up endpoints.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Pds? RunPds(string? dataDir, dnproto.sdk.log.ILogger logger)
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
        // Initialize PdsDb
        //
        PdsDb? pdsDb = PdsDb.ConnectPdsDb(lfs.DataDir, logger);
        if (pdsDb == null)
        {
            logger.LogError("Failed to initialize PDS database.");
            return null;
        }

        //
        // Get PDS config from db
        //
        var config = pdsDb.GetConfig();
        if (config == null)
        {
            logger.LogError("Failed to get PDS config from database.");
            return null;
        }

        //
        // Create commit signing function
        //
        var commitSigningFunction = sdk.auth.Signer.CreateCommitSigningFunction(config.UserPrivateKeyMultibase, config.UserPublicKeyMultibase);
        if (commitSigningFunction == null)
        {
            logger.LogError("Failed to create commit signing function.");
            return null;
        }



        //
        // Configure to listen on specified port with HTTPS
        //
        logger.LogInfo($"Starting minimal API with HTTPS on port {config.ListenPort}...");
        var builder = WebApplication.CreateBuilder();
        
        // Clear default logging providers and add custom logger
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new CustomLoggerProvider(logger));
        
        builder.WebHost.UseUrls($"https://{config.ListenHost}:{config.ListenPort}");
        var app = builder.Build();

        //
        // Construct pds object
        //
        var pds = new Pds()
        {
            Config = config,
            Logger = logger,
            LocalFileSystem = lfs,
            PdsDb = pdsDb,
            App = app,
            CommitSigningFunction = commitSigningFunction
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
        App.MapPost("/xrpc/com.atproto.server.createSession", (HttpContext context) => new ComAtprotoServer_CreateSession(){Pds = this, HttpContext = context}.GetResponse());
        App.MapPost("/xrpc/com.atproto.server.refreshSession", (HttpContext context) => new ComAtprotoServer_RefreshSession(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.server.getSession", (HttpContext context) => new ComAtprotoServer_GetSession(){Pds = this, HttpContext = context}.GetResponse());


        Logger.LogInfo("");
        Logger.LogInfo("Mapped XRPC endpoints:");
        Logger.LogInfo("");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/hello");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/xrpc/_health");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.describeServer");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.identity.resolveHandle");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.createSession");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.refreshSession");
        Logger.LogInfo($"https://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.getSession");
        Logger.LogInfo("");
    }
}