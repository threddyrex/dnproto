

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using dnproto.fs;
using dnproto.pds.xrpc;
using dnproto.repo;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Nodes;

namespace dnproto.pds;



/// <summary>
/// Main class for PDS implementation.
/// 
/// Objects used by this class:
///     Config - the config retrieved from the db
///     LocalFileSystem (dataDir, logger) - local file system access (and cache)
///     PdsDb (lfs, logger) - access to the sqlite db
///     UserRepo (lfs, logger, db, signer, userDid) - operations for repo
///     FirehoseEventGenerator (lfs, logger, db) - generates firehose events
///     BackgroundJobs (lfs, logger, db) - manages background jobs
/// 
/// </summary>
public class Pds
{
    public required Config Config;

    public required dnproto.log.IDnProtoLogger Logger;

    public required LocalFileSystem LocalFileSystem;

    public required PdsDb PdsDb;

    public required IBlobDb blobDb;

    public required WebApplication App;

    public required Func<byte[], byte[]> CommitSigningFunction;

    public required UserRepo UserRepo;

    public required FirehoseEventGenerator FirehoseEventGenerator;

    public required BackgroundJobs BackgroundJobs;


    /// <summary>
    /// Shared lock for synchronizing access to the PDS.
    /// Use Lock.Wait() for synchronous code and Lock.WaitAsync() for async code.
    /// Always release with Lock.Release() in a finally block.
    /// </summary>
    public static SemaphoreSlim GLOBAL_PDS_LOCK = new SemaphoreSlim(1, 1);



    #region STARTUP

    /// <summary>
    /// Initializes the PDS for running. Loads config, initializes database, and sets up endpoints.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Pds InitializePdsForRun(string dataDir, dnproto.log.IDnProtoLogger logger, int cacheExpiryMinutes_Actors = 3)
    {
        //
        // Get local file system
        //
        LocalFileSystem lfs = LocalFileSystem.Initialize(dataDir, logger);

        //
        // Initialize PdsDb
        //
        PdsDb pdsDb = PdsDb.ConnectPdsDb(lfs, logger);

        //
        // Initialize BlobDb. File-based for now.
        //
        IBlobDb blobDb = BlobDb.Create(lfs, logger);

        //
        // Get PDS config from db
        //
        Config config = pdsDb.GetConfig();

        //
        // Create commit signing function
        //
        Func<byte[], byte[]> commitSigningFunction = auth.Signer.CreateCommitSigningFunction(config.UserPrivateKeyMultibase, config.UserPublicKeyMultibase);

        //
        // Load repo
        //
        UserRepo repo = UserRepo.ConnectUserRepo(lfs, logger, pdsDb, commitSigningFunction, config.UserDid);

        //
        // Configure to listen
        //
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Configure Kestrel to disable minimum data rates for long-lived WebSocket connections
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MinRequestBodyDataRate = null;
            options.Limits.MinResponseDataRate = null;
        });
        
        // Clear default logging providers and add custom logger
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new dnproto.log.CustomLoggerProvider(logger));
        
        // Reduce shutdown timeout from default 30 seconds to 5 seconds
        builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });

        // Add CORS services to allow cross-origin requests from Bluesky app
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .SetPreflightMaxAge(TimeSpan.FromSeconds(86400));
            });
        });

        builder.WebHost.UseUrls($"{config.ListenScheme}://{config.ListenHost}:{config.ListenPort}");
        var app = builder.Build();

        // Log Kestrel timeout settings
        var kestrelLimits = app.Services.GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>()?.Value?.Limits;
        if (kestrelLimits != null)
        {
            logger.LogInfo($"[KESTREL] KeepAliveTimeout: {kestrelLimits.KeepAliveTimeout}");
            logger.LogInfo($"[KESTREL] RequestHeadersTimeout: {kestrelLimits.RequestHeadersTimeout}");
            logger.LogInfo($"[KESTREL] MaxRequestBodySize: {kestrelLimits.MaxRequestBodySize}");
            logger.LogInfo($"[KESTREL] MinRequestBodyDataRate: {kestrelLimits.MinRequestBodyDataRate?.BytesPerSecond ?? 0} bytes/sec");
            logger.LogInfo($"[KESTREL] MinResponseDataRate: {kestrelLimits.MinResponseDataRate?.BytesPerSecond ?? 0} bytes/sec");
        }

        // Enable CORS middleware - must be before routing/endpoints
        app.UseCors();

        //
        // Enable WebSockets for firehose subscribeRepos endpoint
        //
        app.UseWebSockets();


        //
        // Construct pds object
        //
        var pds = new Pds()
        {
            Config = config,
            Logger = logger,
            LocalFileSystem = lfs,
            PdsDb = pdsDb,
            blobDb = blobDb,
            App = app,
            CommitSigningFunction = commitSigningFunction,
            UserRepo = repo,
            FirehoseEventGenerator = new FirehoseEventGenerator(pdsDb),
            BackgroundJobs = new BackgroundJobs(lfs, (dnproto.log.Logger)logger, pdsDb)
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
        Logger.LogInfo("");
        Logger.LogInfo("!! Running PDS !!");
        Logger.LogInfo("");
        BackgroundJobs.Start();
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
        App.MapPost("/xrpc/com.atproto.repo.applyWrites", (HttpContext context) => new ComAtprotoRepo_ApplyWrites(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.sync.subscribeRepos", async (HttpContext context) => { var cmd = new ComAtprotoSync_SubscribeRepos(){Pds = this, HttpContext = context}; await cmd.HandleWebSocketAsync(); });
        App.MapPost("/xrpc/com.atproto.server.activateAccount", (HttpContext context) => new ComAtprotoServer_ActivateAccount(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.sync.listRepos", (HttpContext context) => new ComAtprotoSync_ListRepos(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.repo.describeRepo", (HttpContext context) => new ComAtprotoRepo_DescribeRepo(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.repo.listRecords", (HttpContext context) => new ComAtprotoRepo_ListRecords(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/xrpc/com.atproto.sync.getRecord", async (HttpContext context) => { var cmd = new ComAtprotoSync_GetRecord(){Pds = this, HttpContext = context}; return await cmd.GetResponseAsync(); });
        App.MapGet("/xrpc/com.atproto.sync.getRepoStatus", (HttpContext context) => new ComAtprotoSync_GetRepoStatus(){Pds = this, HttpContext = context}.GetResponse());
        App.MapPost("/xrpc/com.atproto.server.deactivateAccount", (HttpContext context) => new ComAtprotoServer_DeactivateAccount(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/.well-known/oauth-protected-resource", (HttpContext context) => new Oauth_ProtectedResource(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/.well-known/oauth-authorization-server", (HttpContext context) => new Oauth_AuthorizationServer(){Pds = this, HttpContext = context}.GetResponse());
        App.MapGet("/oauth/jwks", (HttpContext context) => new Oauth_Jwks(){Pds = this, HttpContext = context}.GetResponse());
        App.MapPost("/oauth/par", async (HttpContext context) => { var cmd = new Oauth_Par(){Pds = this, HttpContext = context}; return await cmd.GetResponse(); });
        App.MapGet("/oauth/authorize", async (HttpContext context) => { var cmd = new Oauth_Authorize_Get(){Pds = this, HttpContext = context}; return await cmd.GetResponse(); });
        App.MapPost("/oauth/authorize", async (HttpContext context) => { var cmd = new Oauth_Authorize_Post(){Pds = this, HttpContext = context}; return await cmd.GetResponse(); });
        App.MapPost("/oauth/token", async (HttpContext context) => { var cmd = new Oauth_Token(){Pds = this, HttpContext = context}; return await cmd.GetResponse(); });
        
        

        // Catch-all for other app.bsky routes - proxy to Bluesky AppView
        App.MapFallback("/xrpc/{**rest}", async (HttpContext context) =>
        {
            var cmd = new AppBsky_Proxy() { Pds = this, HttpContext = context };

            // Only proxy app.bsky routes that aren't already handled
            if (context.Request.Path.Value?.StartsWith("/xrpc/app.bsky") == true
                || context.Request.Path.Value?.StartsWith("/xrpc/chat.bsky") == true)
            {
                return await cmd.ProxyToAppView(context);
            }

            // Log unimplemented endpoints so we can track what's being called
            Logger.LogWarning($"UNIMPLEMENTED ENDPOINT: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");

            return Results.Json(new { error = "MethodNotImplemented", message = $"Endpoint not implemented: {context.Request.Path}" }, statusCode: 501);
        });
    }

    #endregion


    #region ACTIVATE


    /// <summary>
    /// Activates the user by setting the user as active in the database and updating the in-memory configuration.
    /// Also generates firehose events for the activation.
    /// </summary>
    public void ActivateAccount()
    {
        Pds.GLOBAL_PDS_LOCK.Wait();
        try
        {
            //
            // Set db
            //
            PdsDb.SetUserActive(true);


            //
            // Update in-memory config
            //
            Config.UserIsActive = true;



            //
            // FIREHOSE (#account)
            //
            FirehoseEventGenerator.GenerateFrame(
                header_t: "#account", 
                header_op: 1, 
                object2Json: new JsonObject()
                {
                    ["did"] = Config.UserDid,
                    ["active"] = true
                });



            //
            // FIREHOSE (#identity)
            //
            FirehoseEventGenerator.GenerateFrame(
                header_t: "#identity", 
                header_op: 1, 
                object2Json: new JsonObject()
                {
                    ["did"] = Config.UserDid,
                    ["handle"] = Config.UserHandle
                });


            //
            // FIREHOSE (#sync)
            //
            RepoCommit repoCommit = PdsDb.GetRepoCommit();
            RepoHeader repoHeader = PdsDb.GetRepoHeader();
            FirehoseEventGenerator.GenerateFrameWithBlocks(
                header_t: "#sync", 
                header_op: 1, 
                object2Json: new JsonObject()
                {
                    ["did"] = Config.UserDid,
                    ["rev"] = repoCommit.Rev,
                },
                repoHeader: repoHeader,
                dagCborObjects: new List<(CidV1 cid, DagCborObject dagCbor)>()
                {
                    (repoCommit.Cid!, repoCommit.ToDagCborObject())
                }
            );



        }
        finally
        {
            Pds.GLOBAL_PDS_LOCK.Release();
        }
    }


    public void DeactivateAccount()
    {
        Pds.GLOBAL_PDS_LOCK.Wait();
        try
        {
            //
            // Set db
            //
            PdsDb.SetUserActive(false);


            //
            // Update in-memory config
            //
            Config.UserIsActive = true;



            //
            // FIREHOSE (#account)
            //
            FirehoseEventGenerator.GenerateFrame(
                header_t: "#account", 
                header_op: 1, 
                object2Json: new JsonObject()
                {
                    ["did"] = Config.UserDid,
                    ["active"] = false,
                    ["status"] = "deactivated"
                });





        }
        finally
        {
            Pds.GLOBAL_PDS_LOCK.Release();
        }
    }


    #endregion
}
