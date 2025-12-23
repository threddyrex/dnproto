

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using dnproto.sdk.auth;
using dnproto.sdk.fs;
using dnproto.sdk.log;
using dnproto.sdk.mst;
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

    public required dnproto.sdk.log.IDnProtoLogger Logger;

    public required LocalFileSystem LocalFileSystem;

    public required PdsDb PdsDb;

    public required WebApplication App;

    public required Func<byte[], byte[]> CommitSigningFunction;

    public required MstRepository Repo;


    /// <summary>
    /// Initializes the PDS. Loads config, initializes database, and sets up endpoints.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Pds? LoadPdsForRun(string? dataDir, dnproto.sdk.log.IDnProtoLogger logger)
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
        // Load repo
        //
        logger.LogInfo("Loading user MST repo...");
        var repoPath = Path.Combine(lfs.DataDir, "pds", "repo.car");
        var dnprotoRepo = MstRepository.LoadFromFile(repoPath);

        if (dnprotoRepo == null)
        {
            logger.LogError("Failed to load user MST repo.");
            return null;
        }

        logger.LogInfo($"Current commit: {dnprotoRepo.CurrentCommit?.CommitCid?.ToString() ?? "null"}");


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
            CommitSigningFunction = commitSigningFunction,
            Repo = dnprotoRepo
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

    public static void InitializePds(dnproto.sdk.log.IDnProtoLogger Logger, string? dataDir, string? pdsHostname, string? availableUserDomain, string? userHandle, string? userDid, string? userEmail)
    {

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
            if (string.IsNullOrEmpty(userEmail))
            {
                Logger.LogError("userEmail argument is required.");
                return;
            }


            //
            // Create fresh pds db
            //
            PdsDb? pdsDb = PdsDb.InitializePdsDb(dataDir!, Logger);
            if (pdsDb == null)
            {
                Logger.LogError("Failed to initialize PDS database.");

                Logger.LogInfo("type 'Y' to delete the existing database and re-initialize (all data will be lost), or any other key to abort:");
                string? input = Console.ReadLine();
                if (input != null && input.ToUpper() == "Y")
                {
                    //
                    // Delete existing database file
                    //
                    string dbDir = Path.Combine(dataDir!, "pds");
                    string dbPath = Path.Combine(dbDir, "pds.db");
                    try
                    {
                        File.Delete(dbPath);
                        Logger.LogInfo("Deleted existing database file.");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete existing database file: {ex.Message}");
                        return;
                    }

                    //
                    // Try again to initialize
                    //
                    pdsDb = PdsDb.InitializePdsDb(dataDir!, Logger);
                    if (pdsDb == null)
                    {
                        Logger.LogError("Failed to initialize PDS database after deleting existing database file.");
                        return;
                    }
                }
                else
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
            config.UserEmail = userEmail!;
            
            // Generate user keypair for signing commits
            var userKeyPair = dnproto.sdk.auth.KeyPair.Generate(dnproto.sdk.auth.KeyTypes.P256);
            config.UserPublicKeyMultibase = userKeyPair.PublicKeyMultibase;
            config.UserPrivateKeyMultibase = userKeyPair.PrivateKeyMultibase;


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
            // Create commit signing function
            //
            var commitSigningFunction = sdk.auth.Signer.CreateCommitSigningFunction(config.UserPrivateKeyMultibase, config.UserPublicKeyMultibase);
            if (commitSigningFunction == null)
            {
                Logger.LogError("Failed to create commit signing function.");
                return;
            }


            //
            // Create new mst repo
            //
            var dnprotoRepo = MstRepository.CreateForNewUser(userDid, commitSigningFunction);
            if (dnprotoRepo == null)
            {
                Logger.LogError("Failed to create new MST repository for user.");
                return;
            }

            // write to disk
            var repoPath = Path.Combine(dataDir!, "pds", "repo.car");
            Logger.LogInfo($"Writing new MST repo to {repoPath}...");
            dnprotoRepo.SaveToFile(repoPath);

            if(! File.Exists(repoPath))
            {
                Logger.LogError("Failed to save MST repository to disk.");
                return;
            }


            //
            // Print out stuff that the user will need.
            //
            Logger.LogInfo("PDS initialized successfully.");
            Logger.LogInfo("");
            Logger.LogInfo("Important stuff to remember:");
            Logger.LogInfo("");
            Logger.LogInfo($"   Admin password: {adminPassword}");
            Logger.LogInfo($"   User password: {userPassword}");
            Logger.LogInfo("");
            Logger.LogInfo("    User signing keypair (for DID document and commit signing):");
            Logger.LogInfo($"       Public key (multibase):  {userKeyPair.PublicKeyMultibase}");
            Logger.LogInfo($"       Private key (multibase): {userKeyPair.PrivateKeyMultibase}");
            Logger.LogInfo($"       DID Key:                 {userKeyPair.DidKey}");
            Logger.LogInfo("");
            Logger.LogInfo($"Copy this powershell:\n\n$adminPassword = '{adminPassword}';\n$userHandle = '{userHandle}';\n$userPassword = '{userPassword}';\n\n to set the admin and user passwords in your environment for use with powershell.\n");
        
    }
}