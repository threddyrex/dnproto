# dnproto - an ATProto/Bluesky tool written in dotnet

This is a tool written in C# for interacting with ATProto and Bluesky. It's a work in progress.

Besides being a command line tool, there are a few classes that are reusable in C# programs:

- [Repo.cs](/src/repo/Repo.cs) - the entry point to the repo parsing code. This code allows you to read a CAR file, which is the format for a Bluesky repo. You can read the posts, follows, likes, etc. You'll need the entire "repo" directory.
- [Firehose.cs](/src/firehose/Firehose.cs) - entry point for listening to the firehose. Uses the repo parser for decoding the records coming over the wire.


&nbsp;

# Building dnproto

To get started, change into the src directory and build. I also like to set an alias.

```powershell
cd .\src\
dotnet build
Set-Alias dnproto .\bin\Debug\net9.0\dnproto.exe
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
dnproto /command handle_resolve /handle robtougher.com
```


&nbsp;

# Getting a Bluesky profile

This calls the [Bluesky public API](https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile) to get the profile.

```powershell
dnproto /command profile_get /actor robtougher.com
```


&nbsp;

# Getting repo status

With handle:

```powershell
dnproto /command repo_getstatus /handle robtougher.com
```

Or with did and pds:
```powershell
dnproto /command repo_getstatus /did "did:plc:watmxkxfjbwyxfuutganopfk" /pds "pds01.threddy.social"
```

&nbsp;

# Downloading a user's repo

Calls getRepo for the user, and writes the file to repoFilePath.

Using handle:

```powershell
dnproto /command repo_get /handle robtougher.com /outfile "myfile.car"
```

Or using did and pds:

```powershell
dnproto /command repo_get /did "did:plc:watmxkxfjbwyxfuutganopfk" /pds "pds01.threddy.social" /outfile "myfile.car"
```




&nbsp;

# Logging in and interacting as the user.

You can create a session on the server. The token for the session is
stored in a file on local disk (specified by $sessionFile).

```powershell
$sessionFile = "sessionfile.txt"

# log in
dnproto /command session_create /sessionfile $sessionFile /handle "handle" /password "password"

# create a post
dnproto /command session_post /sessionfile $sessionFile /text "text of post"

# get unread notification count
dnproto /command session_getunreadcount /sessionfile $sessionFile

# log out
dnproto /command session_delete /sessionfile $sessionFile
```


&nbsp;

# Comparing Two Repositories

You can compare the interactions between two repos (accounts) using the following.
It will print out likes, replies, reposts, and quote posts.

```powershell
# Download first repo from Bluesky
dnproto /command repo_get /handle "handle1.com" /repofile "handle1.car"

# Download second repo from Bluesky
dnproto /command repo_get /handle "handle2.com" /repofile "handle2.car"

# Compare the two repo files on disk and print out interactions
dnproto /command repo_compare /repofile1 "handle1.car" /repofile2 "handle2.car"
```



&nbsp;

# Write json responses to disk

Many of the commands are just calls to the Bluesky APIs, which return json responses. 
These commands usually provide a "outfile" argument for writing the response to disk:

```powershell
dnproto /command repo_getstatus /did "did:web:threddyrex.org" /pds "pds01.threddy.social" /outfile "file_path_to_create"
```



&nbsp;

# Exceptions

If you are parsing records and for some reason the parsing code cannot understand 
a certain record structure, it will create a DagCborObject and include a map entry 
with the key "DNPROTO_EXCEPTION", and continue to the next record. 
I've seen this recently with non-Bluesky record types, like the flashes app profile.




&nbsp;

# Download repo and print posts


```powershell
# Download full CAR file from the user's PDS, and store on local disk
dnproto /command repo_get /handle "yourhandle.com" /repofile "repo.car"
# Parse local CAR file and print posts
dnproto /command repo_printposts /repofile "repo.car" > posts.txt
```



&nbsp;

# Linux Support

Is Linux supported? Yes! I tested a few of the commands on an Ubuntu VM. dotnet is supported (generally) on Linux.
As of now, the test suite fully passes on an Ubuntu VM.

