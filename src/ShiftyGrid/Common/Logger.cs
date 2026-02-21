using System.Threading.Channels;

namespace ShiftyGrid.Common;

internal enum LogLevel
{
    None = 0,
    Error = 1,
    Warn = 2,
    Info = 3,
    Debug = 4
}

internal static class Logger
{
#pragma warning disable CS8618

    private static string LogFileName => $"ShiftyGrid_{DateTime.Now:yyyyMMdd}.log";
    public static string LogFilePath { get; private set; }
    public static LogLevel MinimumLogLevel { get; private set; }

    private static Channel<string> _logChannel;
    private static Task _loggingTask;
    private static CancellationTokenSource _cancellationTokenSource;

#pragma warning restore CS8618

    public static void Initialize(string? logPath = null, LogLevel logLevel = LogLevel.Info)
    {
        MinimumLogLevel = logLevel;

        if (logLevel == LogLevel.None)
        {
            LogFilePath = string.Empty;
            return;
        }

        if (string.IsNullOrEmpty(logPath))
            return;

        // Convert relative path to absolute path
        var absoluteLogsPath = Path.IsPathRooted(logPath)
            ? logPath
            : Path.GetFullPath(logPath);

        if (!Directory.Exists(absoluteLogsPath))
        {
            Console.WriteLine($"Error: Log directory does not exist: {absoluteLogsPath}. \r\nUsing default path.");

            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            LogFilePath = Path.Combine(exeDir, LogFileName);
        }
        else
        {
            LogFilePath = Path.Combine(absoluteLogsPath, LogFileName);
        }

        _logChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _cancellationTokenSource = new CancellationTokenSource();

        _loggingTask = Task.Run(ProcessLogQueueAsync);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static async Task ProcessLogQueueAsync()
    {
        var token = _cancellationTokenSource.Token;

        try
        {
            // If logging is disabled, just drain the channel without writing to file
            if (MinimumLogLevel == LogLevel.None)
            {
                await foreach (var _ in _logChannel.Reader.ReadAllAsync(token))
                {
                    // Discard log entries
                }
                return;
            }

            using var fileStream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
            using var writer = new StreamWriter(fileStream) { AutoFlush = false };

            await foreach (var logEntry in _logChannel.Reader.ReadAllAsync(token))
            {
                await writer.WriteLineAsync(logEntry);

                if (_logChannel.Reader.Count == 0)
                {
                    await writer.FlushAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logger error: {ex}");
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        _cancellationTokenSource.Cancel();
        _logChannel.Writer.Complete();
        _loggingTask.Wait(TimeSpan.FromMilliseconds(500));
    }

    private static void Log(LogLevel logLevel, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{logLevel}] {message}";

        // Write to file if logLevel meets file threshold
        if (logLevel <= MinimumLogLevel)
        {
            _logChannel.Writer.TryWrite(entry);
        }
    }

    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warn, message);
    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception != null
            ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}\n{exception.StackTrace}"
            : message;

        Log(LogLevel.Error, fullMessage);
    }

    public static LogLevel GetLogLevel(string logLevel) => logLevel.ToLowerInvariant() switch
    {
        "debug" => LogLevel.Debug,
        "info" => LogLevel.Info,
        "warn" => LogLevel.Warn,
        "error" => LogLevel.Error,
        "none" => LogLevel.None,
        _ => LogLevel.Info
    };

}
