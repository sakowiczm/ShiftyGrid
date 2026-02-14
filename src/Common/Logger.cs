using System.Threading.Channels;

namespace ShiftyGrid.Common;

internal static class Logger
{
    public static string LogFilePath { get; private set; }
    public static bool IsLoggingDisabled { get; private set; }
    private static readonly Channel<string> _logChannel;
    private static readonly Task _loggingTask;
    private static readonly CancellationTokenSource _cancellationTokenSource;

    static Logger()
    {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        var timestamp = DateTime.Now.ToString("yyyyMMdd");
        LogFilePath = Path.Combine(exeDir, $"ShiftyGrid_{timestamp}.log");

        _logChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _cancellationTokenSource = new CancellationTokenSource();

        _loggingTask = Task.Run(ProcessLogQueueAsync);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public static void Initialize(string? logDirectory = null, bool disableLogging = false)
    {
        IsLoggingDisabled = disableLogging;

        if (disableLogging)
        {
            LogFilePath = string.Empty;
            return;
        }

        if (string.IsNullOrEmpty(logDirectory))
            return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd");
        LogFilePath = Path.Combine(logDirectory, $"ShiftyGrid_{timestamp}.log");
    }

    private static async Task ProcessLogQueueAsync()
    {
        var token = _cancellationTokenSource.Token;

        try
        {
            // If logging is disabled, just drain the channel without writing to file
            if (IsLoggingDisabled)
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

    private static void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{level}] {message}";

        _logChannel.Writer.TryWrite(entry);

        //Console.ForegroundColor = level switch
        //{
        //    "DEBUG" => ConsoleColor.DarkGray,
        //    "INFO" => ConsoleColor.White,
        //    "WARN" => ConsoleColor.Yellow,
        //    "ERROR" => ConsoleColor.Red,
        //    _ => Console.ForegroundColor
        //};

        //Console.WriteLine(entry);
        //Console.ResetColor();
    }

    public static void Debug(string message) => Log("DEBUG", message);
    public static void Info(string message) => Log("INFO", message);
    public static void Warning(string message) => Log("WARN", message);
    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception != null
            ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}\n{exception.StackTrace}"
            : message;

        Log("ERROR", fullMessage);
    }

}
