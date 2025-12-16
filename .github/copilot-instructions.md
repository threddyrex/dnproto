# dnproto - ATProto/Bluesky Tool

## Architecture Overview

**dnproto** is a C# .NET 10 CLI tool for interacting with ATProto/Bluesky. It provides both reusable libraries and a command-line interface.

### Core Components

- **[repo/](/src/repo/)** - CAR file parser for Bluesky repos. Implements the ATProto repository format with low-level CBOR/IPLD primitives:
  - `Repo.WalkRepo()` is the entry point - uses callbacks for header and record processing
  - `DagCborObject` - DAG-CBOR encoding/decoding (major types: map, array, string, bytes, unsigned int, CID)
  - `VarInt`, `CidV1` - varint and CID v1 encoding per IPLD specs
  - Parse format: `[VarInt | DagCborObject]` for header, then repeating `[VarInt | CidV1 | DagCborObject]` for records

- **[firehose/](/src/firehose/)** - WebSocket consumer for ATProto event streams. Each frame contains two DAG-CBOR objects (header + message). The message's "blocks" property contains repo-formatted byte arrays.

- **[ws/BlueskyClient.cs](/src/ws/BlueskyClient.cs)** - HTTP client for Bluesky XRPC API. Handles actor resolution (handle→DID via DNS/HTTP, DID→didDoc via PLC/Web, didDoc→PDS), authentication, and API calls.

- **[cli/](/src/cli/)** - Command framework where each command inherits from `BaseCommand` and implements `DoCommand()`. Commands are auto-discovered via reflection (see `CommandLineInterface.TryFindCommandType()`).

- **[fs/LocalFileSystem.cs](/src/fs/LocalFileSystem.cs)** - Local caching layer. Uses `data/` directory with subdirs: `actors/`, `backups/`, `repos/`, `preferences/`, `sessions/`.

### Data Flow Pattern

1. User runs PowerShell script in `scripts/` (e.g., `GetRepo.ps1`)
2. Script calls compiled exe with `/command CommandName /arg1 value1` format
3. `CommandLineInterface` parses args, creates command instance via reflection
4. Command initializes `LocalFileSystem` with `dataDir`
5. Command uses `BlueskyClient` for API calls or `Repo.WalkRepo()` for parsing
6. Results cached in `data/` directory structure

## Development Workflows

### Building and Running

```powershell
# Build from repo root
dotnet build

# Run commands via PowerShell wrapper scripts
cd scripts
.\GetActorInfo.ps1 -actor handle.bsky.social
.\GetRepo.ps1 -actor handle.bsky.social
.\PrintRepoStats.ps1 -actor handle.bsky.social

# Or invoke dnproto.exe directly
..\src\bin\Debug\net10.0\dnproto.exe /command GetActorInfo /actor handle.bsky.social /dataDir ..\data\ /logLevel Info
```

### Testing

```powershell
# Run all tests
dotnet test

# Test project uses xUnit with extensive round-trip tests for CBOR encoding/decoding
# See test/repo/DagCborObjectTests.cs for pattern: Arrange→Act→Assert with MemoryStream
```

### Debugging

Use `/debugattach true` argument to wait for debugger attachment, or set breakpoints and attach to process. PowerShell scripts in `scripts/_Defaults.ps1` set default paths and arguments.

## Key Conventions

### Command Pattern
All commands in `src/cli/commands/` inherit from `BaseCommand`:
- Override `GetRequiredArguments()` and `GetOptionalArguments()` to declare params
- Implement `DoCommand(Dictionary<string, string> arguments)` for logic
- Access `Logger` property (set by CLI framework) for logging

### Argument Parsing
CLI uses `/name value` format (not `--name` or `-name`). All argument names converted to lowercase. Reserved args: `command`, `loglevel`, `debugattach`.

### LocalFileSystem Usage Pattern
Commands needing file I/O follow this pattern:
```csharp
LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
if (lfs == null) return;
var actorInfo = lfs.ResolveActorInfo(actor);
string repoFile = lfs.GetPath_RepoFile(actorInfo);
```

### Logging Levels
Set via `Logger.LogLevel` (0=trace, 1=info, 2=warning). Use `/loglevel trace` for verbose debugging. Static `BlueskyClient.Logger` must be set before making API calls.

### CAR File Walking
When processing repos, use callback pattern:
```csharp
Repo.WalkRepo(stream, 
    header => { /* process header */ return true; },
    record => { /* process record */ return true; }
);
```
Records contain CID and DagCborObject. Use `SelectString()`, `SelectMap()` etc. to navigate CBOR structure.

## Project-Specific Notes

- **Target Framework**: .NET 10 only (uses C# 13 features, implicit usings, nullable reference types)
- **No Trimming/AOT**: Disabled in `Directory.Build.props` due to reflection-based command discovery
- **ATProto Specs**: Code includes inline spec URLs - follow these when modifying parsers
- **Data Directory**: Default `../data/` can be overridden. Stores repos as `.car` files named by DID
- **Session Management**: `SessionFile.cs` handles auth tokens. Use `LogIn.ps1`/`LogOut.ps1` for session management
- **Actor Resolution**: Always use `BlueskyClient.ResolveActorInfo()` - handles both handles and DIDs with full resolution chain

## Common Tasks

**Add a new command**: Create class in `src/cli/commands/`, inherit `BaseCommand`, implement required methods. Add corresponding `.ps1` wrapper in `scripts/`.

**Parse new record type**: Add to `RecordType.cs` enum, extend parsing logic in command's `WalkRepo` callback.

**Debug user account issues**: Run sequence: `GetActorInfo` → `GetPdsInfo` → `GetPlcHistory` → `StartFirehoseConsumer` to diagnose connectivity/configuration problems.
