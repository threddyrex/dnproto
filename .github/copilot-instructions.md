# dnproto - ATProto/Bluesky Tool

## Architecture Overview

**dnproto** is a C# .NET 10 CLI tool for interacting with ATProto/Bluesky. It provides both reusable SDK libraries (`src/sdk/`) and a command-line interface (`src/cli/`), plus an experimental PDS implementation (`src/pds/`).

### Project Structure
```
src/
  ├── cli/               # CLI framework and commands
  │   ├── CommandLineInterface.cs  # Reflection-based command discovery
  │   └── commands/      # Each command = one .cs file inheriting BaseCommand
  ├── sdk/               # Reusable libraries for C# programs
  │   ├── repo/          # CAR/CBOR parsing (Repo.cs, DagCborObject.cs, VarInt.cs, CidV1.cs)
  │   ├── firehose/      # WebSocket event stream consumer (Firehose.cs)
  │   ├── ws/            # HTTP client for XRPC API (BlueskyClient.cs)
  │   ├── fs/            # Local file system cache (LocalFileSystem.cs)
  │   ├── log/           # Logging abstractions
  │   ├── key/           # Cryptographic key handling
  │   └── uri/           # URI parsing utilities
  └── pds/               # Experimental PDS server implementation
test/                    # xUnit tests mirroring src/ structure
scripts/                 # PowerShell wrappers for each CLI command
data/                    # Local cache: actors/, repos/, sessions/, etc.
```

### Core Components

**CAR/Repo Parsing** ([src/sdk/repo/](src/sdk/repo/)):
- `Repo.WalkRepo()` - Entry point using callback pattern for header and records
- Parse format: `[VarInt | DagCborObject]` (header), then `[VarInt | CidV1 | DagCborObject]*` (records)
- `DagCborObject` handles six major types: map, array, string, bytes, unsigned int, CID
- `VarInt.Read/Write()` - MSB-style varint encoding per IPLD spec
- `CidV1.Parse()` - CID v1 with multicodec/multihash (always dag-cbor + sha256 in ATProto)

**Firehose Consumer** ([src/sdk/firehose/Firehose.cs](src/sdk/firehose/Firehose.cs)):
- Each WebSocket frame = two concatenated DAG-CBOR objects (header + message)
- Message's `blocks` property contains byte array in repo format - can be walked with `Repo.WalkRepo()`

**API Client** ([src/sdk/ws/BlueskyClient.cs](src/sdk/ws/BlueskyClient.cs)):
- Actor resolution chain: handle→DID (DNS TXT/HTTPS .well-known), DID→didDoc (PLC/web), didDoc→PDS endpoint
- `ResolveActorInfo()` returns complete resolution including all intermediate steps
- Static `BlueskyClient.Logger` must be set before API calls

**CLI Framework** ([src/cli/CommandLineInterface.cs](src/cli/CommandLineInterface.cs)):
- Reflection-based command discovery scans `dnproto.cli.commands` namespace
- Each command in `src/cli/commands/` inherits `BaseCommand` and lives in its own file

**Local Cache** ([src/sdk/fs/LocalFileSystem.cs](src/sdk/fs/LocalFileSystem.cs)):
- Auto-creates subdirs in `data/`: `actors/`, `backups/`, `repos/`, `preferences/`, `sessions/`, `pds/`, `scratch/`
- `Initialize()` validates `dataDir` and creates structure; returns `null` on failure
- Actor resolution cached in `actors/{did}.json`, repos in `repos/{did}.car`

### Data Flow Pattern

1. User runs PowerShell script in `scripts/` (e.g., `GetRepo.ps1 -actor handle.bsky.social`)
2. Script sources `_Defaults.ps1` for default values, calls exe: `dnproto.exe /command GetRepo /actor ... /dataDir ...`
3. `CommandLineInterface.RunMain()` parses args (lowercase keys), discovers command class via reflection
4. Command instance created, `Logger` set, `DoCommand(arguments)` invoked
5. Command uses `LocalFileSystem.Initialize()`, then `BlueskyClient` for API calls or `Repo.WalkRepo()` for parsing
6. Results written to `data/` directory structure

## Development Workflows

### Building and Running

```powershell
# Build from repo root (solution contains src and test projects)
dotnet build

# Run via PowerShell scripts (recommended - sources _Defaults.ps1 for defaults)
cd scripts
.\GetActorInfo.ps1 -actor handle.bsky.social
.\GetRepo.ps1 -actor handle.bsky.social          # Downloads and caches repo as CAR file
.\PrintRepoStats.ps1 -actor handle.bsky.social   # Analyzes cached repo
.\LogIn.ps1 -actor handle.bsky.social -password 'app-password'  # Stores session token
.\CreatePost.ps1 -text "hello world"             # Uses stored session

# Or invoke exe directly (scripts/_Defaults.ps1 shows the pattern)
..\src\bin\Debug\net10.0\dnproto.exe /command GetActorInfo /actor handle.bsky.social /dataDir ..\data\ /logLevel Info

# All scripts default to: actor=threddyrex.com, dataDir=..\data\, logLevel=Info (see _Defaults.ps1)
```

### Testing

```powershell
# Run all tests (xUnit framework)
dotnet test

# Tests follow Arrange→Act→Assert pattern with MemoryStream for serialization round-trips
# See test/sdk/repo/DagCborObjectTests.cs for extensive CBOR encoding/decoding tests
# Each test verifies binary serialization by writing to MemoryStream, resetting position, reading back
```

### Debugging Workflows

**Attach Debugger**: Use `/debugattach true` to pause execution until debugger attached
**Actor Troubleshooting**: Run this sequence when debugging account issues:
1. `GetActorInfo.ps1` - Verify DID, didDoc, PDS endpoint
2. `GetPlcHistory.ps1` - Check PLC directory history (multiple entries = account migration)
3. `GetPdsInfo.ps1` - List repos on their PDS
4. `StartFirehoseConsumer.ps1` - Test WebSocket connectivity to their PDS
**Moderation Labels**: Query `https://mod.bsky.app/xrpc/com.atproto.label.queryLabels?uriPatterns={did}`

## Key Conventions

### Command Implementation Pattern
All commands in [src/cli/commands/](src/cli/commands/) inherit from `BaseCommand` (one command per file):
```csharp
public class GetActorInfo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments() => 
        new HashSet<string>(new string[]{"actor"});
    
    public override HashSet<string> GetOptionalArguments() => 
        new HashSet<string>(); // or list optional args
    
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string actor = arguments["actor"];
        // Logger property available (set by framework)
        Logger.LogInfo($"Processing {actor}");
        // Implementation...
    }
}
```
- Command class name must match exactly (case-insensitive) for reflection discovery
- No constructor parameters needed - framework sets `Logger` property before calling `DoCommand()`

### Argument Parsing
- CLI uses `/name value` format (NOT `--name` or `-name`)
- All argument keys lowercased automatically
- Reserved arguments: `command`, `loglevel`, `debugattach`, `datadir`
- PowerShell scripts can use standard `-name value` syntax; `_Defaults.ps1` provides fallback values

### LocalFileSystem Initialization Pattern
Every command needing file I/O uses this null-check pattern:
```csharp
LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
if (lfs == null) return;  // Initialize logs error if dataDir invalid
var actorInfo = lfs.ResolveActorInfo(actor);
string repoFile = lfs.GetPath_RepoFile(actorInfo);
```
- `Initialize()` creates subdirs and returns `null` if `dataDir` doesn't exist
- Always check for null before using - don't throw

### Logging
- `Logger.LogLevel`: 0=trace, 1=info, 2=warning (set via `/loglevel trace|info|warning`)
- Static `BlueskyClient.Logger` must be set before API calls (see `CommandLineInterface.RunMain()`)
- Use `/loglevel trace` to debug CBOR parsing or see detailed API calls

### CAR File Processing Pattern
Use callback pattern with early return capability:
```csharp
Repo.WalkRepo(stream, 
    header => {
        // Access header.Version, header.Roots
        return true;  // false = stop processing
    },
    record => {
        // record.Cid (CidV1), record.Data (DagCborObject)
        var collection = record.Data.SelectString("$type");
        var map = record.Data.SelectMap("some_field");
        return true;  // false = stop processing
    }
);
```
- `SelectString()`, `SelectMap()`, `SelectArray()` navigate CBOR structure (null on missing)
- Records are ordered by rkey within collections (lexicographic sort)

### xUnit Test Pattern
All tests use MemoryStream for serialization round-trips:
```csharp
[Fact]
public void RoundTrip_TestCase()
{
    // Arrange
    var original = new DagCborObject { /* ... */ };
    
    // Act
    using var stream = new MemoryStream();
    DagCborObject.WriteToStream(original, stream);
    stream.Position = 0;
    var result = DagCborObject.ReadFromStream(stream);
    
    // Assert
    Assert.Equal(expected, result.Value);
}
```
- Test file structure mirrors `src/` structure under `test/`
- No mocking framework - tests use real streams and in-memory data

## Project-Specific Notes

- **Target Framework**: .NET 10 only - uses C# 13 features, implicit usings, nullable reference types
- **No Trimming/AOT**: Disabled in `Directory.Build.props` due to reflection-based command discovery
- **Cross-Platform**: Fully supports Linux (test suite passes on Ubuntu)
- **Dependencies**: Only `Microsoft.Data.Sqlite` NuGet package; uses AspNetCore framework reference for PDS
- **PowerShell Scripts**: All 35+ scripts in `scripts/` follow same pattern - dot-source `_Defaults.ps1`, call exe
- **Error Handling**: Commands log and return early (no exceptions); use trace logging to see parse errors
- **ATProto Specs**: Code includes inline spec URLs - follow these when modifying parsers
- **Data Directory**: Default `../data/` can be overridden. Stores repos as `.car` files named by DID
- **Session Management**: `SessionFile.cs` handles auth tokens. Use `LogIn.ps1`/`LogOut.ps1` for session management
- **Actor Resolution**: Always use `BlueskyClient.ResolveActorInfo()` - handles both handles and DIDs with full resolution chain

## Common Tasks

**Add a new command**: Create class in `src/cli/commands/`, inherit `BaseCommand`, implement required methods. Add corresponding `.ps1` wrapper in `scripts/`.

**Parse new record type**: Add to `RecordType.cs` enum, extend parsing logic in command's `WalkRepo` callback.

**Debug user account issues**: Run sequence: `GetActorInfo` → `GetPdsInfo` → `GetPlcHistory` → `StartFirehoseConsumer` to diagnose connectivity/configuration problems.
