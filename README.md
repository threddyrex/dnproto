

# dnproto - software for ATProto and Bluesky

This project started as a toolset for debugging and working with ATProto. Things like
resolving handles, downloading repos, parsing repos, etc.

More recently it's become a PDS implementation.

My main account is hosted on a dnproto PDS:

- https://bsky.app/profile/threddyrex.org



&nbsp;

# Source code

CAR repo encoding and decoding:

- [DagCborObject.cs](/src/repo/DagCborObject.cs) - decoding/encoding dag cbor
- [CidV1.cs](/src/repo/CidV1.cs) - decoding/encoding cid
- [VarInt.cs](/src/repo/VarInt.cs) - decoding/encoding varint
- [Repo.cs](/src/repo/Repo.cs) - repo parsing entry point

MST data structure:

- [Mst.cs](/src/mst/Mst.cs) - MST
- [MstNode.cs](/src/mst/MstNode.cs) - represents one node in the MST
- [MstEntry.cs](/src/mst/MstEntry.cs) - represents on entry in a MST node

PDS implementation:

- [/xrpc/](/src/pds/xrpc/) - the XRPC endpoints
- [Installer.cs](/src/pds/Installer.cs) - installing the PDS
- [Pds.cs](/src/pds/Pds.cs) - PDS entry point
- [PdsDb.cs](/src/pds/db/PdsDb.cs) - the database interface, where the repo is stored
- [RepoMst.cs](/src/repo/RepoMst.cs) - converting MST into dag-cbor for use in repos
- [UserRepo.cs](/src/pds/UserRepo.cs) - operations on the user's repo

Listening to a firehose:

- [Firehose.cs](/src/firehose/Firehose.cs)

General Bluesky WS calls:

- [BlueskyClient.cs](/src/ws/BlueskyClient.cs) - calling the Bluesky API.


&nbsp;

# Using the command line tool (Windows)

The following steps show how to use the command line tool on Windows in PowerShell.
Requires .NET 10.

To get started, change into the root directory and build.

```powershell
dotnet build
```

Next, change into the scripts directory, and list the files:

```powershell
cd powershell
ls
```

Most of the files in this directory represent one "command" of the tool. Here are some that I call most often:

```powershell
# resolve actor info and retrieve did, did doc
.\ResolveActorInfo.ps1 -actor <handle or did>

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
.\CreateSession.ps1 -actor <handle or did> -password <password>

# get unread count for logged in session
.\GetUnreadCount.ps1

# create new post for logged in session
.\CreatePost.ps1 -text "hello world"

# log out of account
.\DeleteSession.ps1 -actor <handle or did>


```



&nbsp;

# The data directory

When you are using the command line tool, it uses a local directory to store cached data.
By default, it uses the "data" directory in the repo. You can change this in the _Defaults.ps1 file.



&nbsp;

# Debugging a user

When someone is having issues with their account, I like to run the following commands:

- ResolveActorInfo - just check their DID, DID doc, etc
- GetPdsInfo - lists things on their PDS
- GetPlcHistory - checks their PLC history - make sure that the account isn't active in multiple places
- StartFirehoseConsumer - make sure you can connect to their PDS (I've seen this fail)
- DescribeRepo - check that it looks ok

Also you can query the DID for moderation labels:

https://mod.bsky.app/xrpc/com.atproto.label.queryLabels?uriPatterns=USERS_DID





&nbsp;

# Linux Support

Is Linux supported? Yes! I run my dnproto PDS on Linux.


