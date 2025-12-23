using Microsoft.Extensions.Logging;
using dnproto.sdk.log;

namespace dnproto.pds;

/// <summary>
/// Adapter that bridges ASP.NET Core's ILogger to our custom Logger.
/// </summary>
public class AspNetCoreLoggerAdapter : Microsoft.Extensions.Logging.ILogger
{
    private readonly dnproto.sdk.log.IDnProtoLogger _customLogger;
    private readonly string _categoryName;

    public AspNetCoreLoggerAdapter(dnproto.sdk.log.IDnProtoLogger customLogger, string categoryName)
    {
        _customLogger = customLogger;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // Scopes not supported in our custom logger
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // Always return true; let our custom logger filter by its own level
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        
        // Include category name for context (e.g., "Microsoft.Hosting.Lifetime")
        var fullMessage = $"{_categoryName}: {message}";

        // Map ASP.NET Core log levels to our custom logger methods
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                _customLogger.LogTrace(fullMessage);
                break;
            case LogLevel.Information:
                _customLogger.LogInfo(fullMessage);
                break;
            case LogLevel.Warning:
                _customLogger.LogWarning(fullMessage);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _customLogger.LogError(fullMessage);
                if (exception != null)
                {
                    _customLogger.LogError($"Exception: {exception}");
                }
                break;
            case LogLevel.None:
                break;
        }
    }
}

/// <summary>
/// Logger provider that creates AspNetCoreLoggerAdapter instances.
/// </summary>
public class CustomLoggerProvider : ILoggerProvider
{
    private readonly dnproto.sdk.log.IDnProtoLogger _customLogger;

    public CustomLoggerProvider(dnproto.sdk.log.IDnProtoLogger customLogger)
    {
        _customLogger = customLogger;
    }

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return new AspNetCoreLoggerAdapter(_customLogger, categoryName);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
