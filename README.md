
# dnproto - an ATProto/Bluesky tool written in dotnet

This is a tool written in C# for interacting with ATProto and Bluesky.


&nbsp;

# Source code

For CAR repo parsing, see these classes:

- [Repo.cs](/src/sdk/repo/Repo.cs) - repo parsing code. entry point
- [DagCborObject.cs](/src/sdk/repo/DagCborObject.cs) - decoding/encoding dag cbor
- [VarInt.cs](/src/sdk/repo/VarInt.cs) - decoding/encoding varint
- [CidV1.cs](/src/sdk/repo/CidV1.cs) - decoding/encoding cid

You can consume the firehose with this class:

- [Firehose.cs](/src/sdk/firehose/Firehose.cs) - listening to the firehose. Uses the repo parser for decoding the records coming over the wire.

And for general Bluesky WS calls, see:

- [BlueskyClient.cs](/src/sdk/ws/BlueskyClient.cs) - calling the Bluesky API.

The GitHub copilot-instructions file has more information about working with the code:

- [copilot-instructions.md](/.github/copilot-instructions.md)

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
# resolve actor info and retrieve did, did doc
.\GetActorInfo.ps1 -actor <handle or did>

# get plc dir history for actor
.\GetPlcHistory.ps1 -actor <handle or did>

# download the user's repo and store in the data directory
.\GetRepo.ps1 -actor <handle or did>

# print stats for the downloaded repo
.\PrintRepoStats.ps1 -actor <handle or did>

# print records for the downloaded repo
.\PrintRepoRecords.ps1 -actor <handle or did>

# print posts for the downloaded repo
.\PrintRepoPosts.ps1

# log in to the account. stores session locally
.\LogIn.ps1 -actor <handle or did> -password <password>

# get unread count for logged in session
.\GetUnreadCount.ps1

# create new post for logged in session
.\CreatePost.ps1 -text "hello world"

# log out of account
.\LogOut.ps1 -actor <handle or did>


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

