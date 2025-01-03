# dnproto - an ATProto/Bluesky tool written in dotnet

This is a tool written in C# for interacting with ATProto and Bluesky. It's a work in progress.

&nbsp;


# Running the unit tests

You can run the unit tests with the following commands:

```powershell
cd .\test\
dotnet test
```


&nbsp;

# Building dnproto

```powershell
cd .\src\
dotnet build
```


&nbsp;

# Showing the help

You can view help for the utility by calling it with no arguments.

```powershell
.\dnproto.exe
```


&nbsp;

# Running a command

Each feature of the utility is a "command". You can specify which command you want when running the tool, along with arguments.

To run one of the commands:

```powershell
.\dnproto.exe /command <commandname> /arg1 value1 /arg2 value2...
```

Calling dnproto with no arguments will print the help.


&nbsp;

# Resolving a Bluesky handle

This calls the [Bluesky public API](https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle) to resolve a handle.

```powershell
.\dnproto.exe /command ResolveHandle /handle threddyrex.com
```


&nbsp;

# Getting a Bluesky profile

This calls the [Bluesky public API](https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile) to get the profile.

```powershell
.\dnproto.exe /command GetProfile /actor threddyrex.com
```


&nbsp;

# Getting repo status

```powershell
.\dnproto.exe /command GetRepoStatus /did "did:web:threddyrex.org" /pds "pds01.threddy.social"
```

&nbsp;

# Downloading a user's repo

```powershell
.\dnproto.exe /command GetRepo /did "did" /pds "pds hostname" /outputfilepath "file to write repo"
```


&nbsp;

# Logging in and interacting as the user.


```powershell
# log in
.\dnproto.exe /command CreateSession /pds "pds" /username "handle" /password "password"

# create a post
.\dnproto.exe /command CreatePost /text "text of post"

# get unread notification count
.\dnproto.exe /command GetUnreadCount

# log out
.\dnproto.exe /command DeleteSession
```



