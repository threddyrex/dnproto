

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using dnproto.auth;
using dnproto.fs;
using dnproto.pds.xrpc;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds;


/// <summary>
/// Main class for pds implementation.
/// </summary>
public class Pds
{
    public required Config Config;

    public required dnproto.log.IDnProtoLogger Logger;

    public required LocalFileSystem LocalFileSystem;

    public required PdsDb PdsDb;

    public required WebApplication App;

    public required Func<byte[], byte[]> CommitSigningFunction;

    public required UserRepo UserRepo;




    #region STARTUP

    /// <summary>
    /// Initializes the PDS for running. Loads config, initializes database, and sets up endpoints.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Pds? InitializePdsForRun(string? dataDir, dnproto.log.IDnProtoLogger logger)
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
        var commitSigningFunction = auth.Signer.CreateCommitSigningFunction(config.UserPrivateKeyMultibase, config.UserPublicKeyMultibase);
        if (commitSigningFunction == null)
        {
            logger.LogError("Failed to create commit signing function.");
            return null;
        }


        //
        // Load repo
        //
        var repo = new UserRepo(pdsDb, logger, commitSigningFunction, config.UserDid);

        //
        // Print repo commit
        //
        var repoCommit = pdsDb.GetRepoCommit();
        if (repoCommit != null)
        {
            logger.LogInfo($"Loaded repo commit: {repoCommit.Cid}");
        }
        else
        {
            logger.LogWarning("No repo commit found in database.");
        }

        //
        // Configure to listen
        //
        var builder = WebApplication.CreateBuilder();

        
        // Clear default logging providers and add custom logger
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new dnproto.log.CustomLoggerProvider(logger));
        
        builder.WebHost.UseUrls($"{config.ListenScheme}://{config.ListenHost}:{config.ListenPort}");
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
            CommitSigningFunction = commitSigningFunction,
            UserRepo = repo
        };


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
        App.MapPost("/xrpc/com.atproto.repo.uploadBlob", async (HttpContext context) => { var cmd = new ComAtprotoRepo_UploadBlob(){Pds = this, HttpContext = context}; return await cmd.GetResponseAsync(); });
        App.MapGet("/xrpc/com.atproto.sync.listBlobs", async (HttpContext context) => { var cmd = new ComAtprotoSync_ListBlobs(){Pds = this, HttpContext = context}; return await cmd.GetResponseAsync(); });
        App.MapGet("/xrpc/com.atproto.sync.getBlob", async (HttpContext context) => { var cmd = new ComAtprotoSync_GetBlob(){Pds = this, HttpContext = context}; return await cmd.GetResponseAsync(); });
        App.MapGet("/xrpc/app.bsky.actor.getPreferences", (HttpContext context) => new AppBskyActor_GetPreferences(){Pds = this, HttpContext = context}.GetResponse());
        App.MapPost("/xrpc/app.bsky.actor.putPreferences", async (HttpContext context) => { var cmd = new AppBskyActor_PutPreferences(){Pds = this, HttpContext = context}; return await cmd.GetResponseAsync(); });
        App.MapGet("/xrpc/com.atproto.sync.getRepo", async (HttpContext context) => { var cmd = new ComAtprotoSync_GetRepo(){Pds = this, HttpContext = context}; return await cmd.GetResponseAsync(); });
        App.MapPost("/xrpc/com.atproto.repo.createRecord", (HttpContext context) => new ComAtprotoRepo_CreateRecord(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.repo.getRecord", (HttpContext context) => new ComAtprotoRepo_GetRecord(){Pds = this, HttpContext = context}.GetResponse());
        App.MapPost("/xrpc/com.atproto.repo.deleteRecord", (HttpContext context) => new ComAtprotoRepo_DeleteRecord(){Pds = this, HttpContext = context}.GetResponse());
        App.MapPost("/xrpc/com.atproto.repo.putRecord", (HttpContext context) => new ComAtprotoRepo_PutRecord(){Pds = this, HttpContext = context}.GetResponse());

        // Catch-all for other app.bsky routes - proxy to Bluesky AppView
        App.MapFallback("/xrpc/{**rest}", async (HttpContext context) =>
        {
            var cmd = new AppBsky_Proxy() { Pds = this, HttpContext = context };

            // Only proxy app.bsky routes that aren't already handled
            if (context.Request.Path.Value?.StartsWith("/xrpc/app.bsky") == true)
            {
                return await cmd.ProxyToAppView(context);
            }

            return Results.NotFound();
        });


        Logger.LogInfo("Mapped XRPC endpoints:");
        Logger.LogInfo("");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/hello");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/_health");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.describeServer");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.identity.resolveHandle");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.createSession");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.refreshSession");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.server.getSession");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.repo.uploadBlob");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.sync.listBlobs");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.sync.getBlob");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/app.bsky.actor.getPreferences");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/app.bsky.actor.putPreferences");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.sync.getRepo");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/app.bsky -> proxied to app view");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.repo.createRecord");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.repo.getRecord");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.repo.deleteRecord");
        Logger.LogInfo($"   {Config.ListenScheme}://{Config.ListenHost}:{Config.ListenPort}/xrpc/com.atproto.repo.putRecord");
        Logger.LogInfo("");
    }

    #endregion
}
