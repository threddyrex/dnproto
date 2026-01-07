
# dnproto - an ATProto/Bluesky tool written in dotnet

This is code for interacting with ATProto and Bluesky.

It started as a toolset for debugging and working with ATproto. Things like
resolving handles, downloading repos, parsing repos, etc.

More recently we've started working on a PDS implementation.
This is in progress.



&nbsp;

# Source code

CAR repo encoding and decoding:

- [Repo.cs](/src/repo/Repo.cs) - repo parsing entry point
- [DagCborObject.cs](/src/repo/DagCborObject.cs) - decoding/encoding dag cbor
- [CidV1.cs](/src/repo/CidV1.cs) - decoding/encoding cid
- [VarInt.cs](/src/repo/VarInt.cs) - decoding/encoding varint

MST decoding:

- [Mst.cs](/src/repo/Mst.cs) - MST parsing entry point
- [MstNode.cs](/src/repo/MstNode.cs) - represents one node in the MST
- [MstEntry.cs](/src/repo/MstEntry.cs) - represents on entry in a MST node

PDS implementation (in progress):

- [Installer.cs](/src/pds/Installer.cs) - installing the PDS
- [Pds.cs](/src/pds/Pds.cs) - PDS entry point
- [UserRepo.cs](/src/pds/UserRepo.cs) - operations on the user's repo
- [PdsDb.cs](/src/pds/PdsDb.cs) - the database interface, where the repo is stored
- [MstDb.cs](/src/pds/MstDb.cs) - a db-backed implementation of MST

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

- GetActorInfo - just check their DID, DID doc, etc
- GetPdsInfo - lists things on their PDS
- GetPlcHistory - checks their PLC history - make sure that the account isn't active in multiple places
- StartFirehoseConsumer - make sure you can connect to their PDS (I've seen this fail)
- DescribeRepo - check that it looks ok

Also you can query the DID for moderation labels:

https://mod.bsky.app/xrpc/com.atproto.label.queryLabels?uriPatterns=USERS_DID





&nbsp;

# Linux Support

Is Linux supported? Yes! I tested a few of the commands on an Ubuntu VM. dotnet is supported (generally) on Linux.
As of now, the test suite fully passes on an Ubuntu VM.
Also running PDS on Linux.


&nbsp;

# Helpful Links for PDS Implementation

- PDS implementation
  - [docs.bsky.app - AT Protocol XRPC API](https://docs.bsky.app/docs/api/at-protocol-xrpc-api)
  - [event stream - atproto](https://atproto.com/specs/event-stream)
  - [repository - atproto](https://atproto.com/specs/repository)
  - [API hosts and Auth](https://docs.bsky.app/docs/advanced-guides/api-directory)
  - [ipld carv1](https://ipld.io/specs/transport/car/carv1/)
  - [ipld dag-cbor](https://ipld.io/specs/codecs/dag-cbor/spec/)
  - [DavidBuchanan314/millipds](https://github.com/DavidBuchanan314/millipds)
  - [DavidBuchanan314/atmst](https://github.com/DavidBuchanan314/atmst)
  - [haleyok/cocoon](https://github.com/haileyok/cocoon)
  - [HTTP API (XRPC) - Service Proxy](https://atproto.com/specs/xrpc#service-proxying)
  - [What does a PDS implementation entail?](https://github.com/bluesky-social/atproto/discussions/2350)
  - [Adversarial ATProto PDS Migration](https://www.da.vidbuchanan.co.uk/blog/adversarial-pds-migration.html)
  - [bluesky-social/jetstream](https://github.com/bluesky-social/jetstream)
  - [bluesky-social/pds](https://github.com/bluesky-social/pds)
  - [bluesky-social/pds/ACCOUNT_MIGRATION](https://github.com/bluesky-social/pds/blob/main/ACCOUNT_MIGRATION.md)
  - [multiformats](https://github.com/multiformats/multicodec/blob/master/table.csv)

