# dnproto - an ATProto/Bluesky tool written in dotnet

This is a tool written in C# for interacting with ATProto and Bluesky. It's a work in progress.

Besides being a command line tool, there are a few classes that are reusable in C# programs:

- [Repo.cs](/src/repo/Repo.cs) - repo parsing code. This code allows you to read a CAR file, which is the format for a Bluesky repo. You can read the posts, follows, likes, etc. You'll need the entire "repo" directory.
- [Firehose.cs](/src/firehose/Firehose.cs) - listening to the firehose. Uses the repo parser for decoding the records coming over the wire.
- [BlueskyClient.cs](/src/ws/BlueskyClient.cs) - calling the Bluesky API.


&nbsp;

# Building dnproto

To get started, change into the root directory and build. I also like to set an alias.

```powershell
dotnet build
Set-Alias dnproto (Resolve-Path .\src\bin\Debug\net9.0\dnproto.exe).Path
```


&nbsp;

# Showing the help

You can view help for the utility by calling it with no arguments.

```powershell
dnproto
```


&nbsp;

# Running a command

Each feature of the utility is a "command". You can specify which command you want when running the tool, along with arguments.

To run one of the commands:

```powershell
dnproto /command <commandname> /arg1 value1 /arg2 value2...
```

Calling dnproto with no arguments will print the help.


&nbsp;

# Resolving a Bluesky handle

This calls the [Bluesky public API](https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle) to resolve a handle.

```powershell
dnproto /command gethandleinfo /handle robtougher.com
```


&nbsp;

# Getting a Bluesky profile

This calls the [Bluesky public API](https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile) to get the profile.

```powershell
dnproto /command getprofile /actor robtougher.com
```


&nbsp;

# Getting repo status

With handle:

```powershell
dnproto /command getrepostatus /handle robtougher.com
```


&nbsp;

# Downloading a user's repo

Calls getRepo for the user, and writes the file to repoFilePath.

Using handle:

```powershell
dnproto /command getrepo /handle robtougher.com /datadir "path_to_local_filesystem_dir"
```




&nbsp;

# Download repo and print posts


```powershell
# Download full CAR file from the user's PDS, and store on local disk
dnproto /command getrepo /handle "yourhandle.com"  /datadir "path_to_local_filesystem_dir"
# Parse local CAR file and print posts
dnproto /command printrepoposts /handle "yourhandle.com"  /datadir "path_to_local_filesystem_dir" > posts.txt
```



&nbsp;

# Linux Support

Is Linux supported? Yes! I tested a few of the commands on an Ubuntu VM. dotnet is supported (generally) on Linux.
As of now, the test suite fully passes on an Ubuntu VM.

