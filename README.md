
# dnproto - an ATProto/Bluesky tool written in dotnet

This is a tool written in C# for interacting with ATProto and Bluesky.


&nbsp;

# Source code

There are a few classes that are reusable in C# programs:

- [repo](/src/repo/) - repo parsing code. This code allows you to read a CAR file, which is the format for a Bluesky repo. You can read the posts, follows, likes, etc. The main files are the following:
  - [Repo.cs](/src/repo/Repo.cs) - entry point
  - [DagCborObject.cs](/src/repo/DagCborObject.cs) - decoding/encoding dag cbor
  - [VarInt.cs](/src/repo/VarInt.cs) - decoding/encoding varint
  - [CidV1.cs](/src/repo/CidV1.cs) - decoding/encoding cid
- [Firehose.cs](/src/firehose/Firehose.cs) - listening to the firehose. Uses the repo parser for decoding the records coming over the wire.
- [BlueskyClient.cs](/src/ws/BlueskyClient.cs) - calling the Bluesky API.


&nbsp;

# Using the command line tool

The following steps show how to use the command line tool on Windows in PowerShell.
Requires .NET 10.

To get started, change into the root directory and build.

```powershell
dotnet build
```

Next, change into the scripts directory, and list the files:

```powershell
cd scripts
ls
```

Most of the files in this directory represent one "command" of the tool. Here are some that I call most often:

```powershell
.\GetActorInfo.ps1 -actor <handle or did> # resolves the handle or did
.\GetRepo.ps1 -actor <handle or did> # downloads the user's repo and stores in the data directory
.\PrintRepoStats.ps1 -actor <handle or did> # prints stats for the downloaded repo
```



&nbsp;

# The data directory

When you are using the command line tool, it uses a local directory to store cached data.
By default, it uses the "data" directory in the repo. You can change this in the _Defaults.ps1 file.



&nbsp;

# Debugging a user

When someone is having issues with their account, I like to run the following commands:

- GetActorInfo - just check their DID, DID doc, etc
- GetPdsInfo - lists things on their PDS
- GetPlcHistory - checks their PLC history - make sure that the account isn't active in multiple places
- StartFirehoseConsumer - make sure you can connect to their PDS (I've seen this fail)

Also you can query the DID for moderation labels:

https://mod.bsky.app/xrpc/com.atproto.label.queryLabels?uriPatterns=USERS_DID





&nbsp;

# Linux Support

Is Linux supported? Yes! I tested a few of the commands on an Ubuntu VM. dotnet is supported (generally) on Linux.
As of now, the test suite fully passes on an Ubuntu VM.

