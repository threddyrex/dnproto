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


# Running a command

Each feature of the utility is a "command". You can specify which command you want when running the tool, along with arguments.

For example:

```powershell
dnproto.exe /command <commandname> /arg1 value1 /arg2 value2...
```

&nbsp;


# Resolving a Bluesky handle

This calls the [Bluesky public API](https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle) to resolve a handle.

```powershell
dnproto.exe /command ResolveHandle /handle threddyrex.com
```


