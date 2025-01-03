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
.\dnproto.exe /command getrepostatus /did "did:web:threddyrex.org" /pds "pds01.threddy.social"
```



&nbsp;

# Creating a session (log in)

You can create a session (log in) using the following:

```powershell
.\dnproto.exe /command createsession /pds "pds" /username "handle" /password "password"
```

After successful session creation, you can call other commands in dnproto that require authentication.



&nbsp;

# Creating a text post

After creating a session with the server, you can create a text post with the following:

```powershell
.\dnproto.exe /command createpost /text "text of post"
```




&nbsp;

# Getting the current logged in user's unread notification count

```powershell
.\dnproto.exe /command getunreadcount
```


&nbsp;

# Deleting a session (log out)

Once you are done using the tool and want to log out, call the following:

```powershell
.\dnproto.exe /command deletesession
```



