using System.Text.Json.Serialization;

namespace ShiftyGrid.Server;

/// <summary>
/// Represents a request sent from client to server via IPC
/// </summary>
public class Request
{
    /// <summary>
    /// The command name (e.g., "exit", "status")
    /// </summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Request arguments
    /// </summary>
    [JsonPropertyName("args")]
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Timestamp when request was created
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a response sent from server to client via IPC
/// </summary>
public class Response
{
    /// <summary>
    /// Whether the request executed successfully
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message about the result
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional data payload (for commands like "status")
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, string>? Data { get; set; }

    /// <summary>
    /// Creates a success response
    /// </summary>
    public static Response CreateSuccess(string message, Dictionary<string, string>? data = null)
    {
        return new Response
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates an error response
    /// </summary>
    public static Response CreateError(string message)
    {
        return new Response
        {
            Success = false,
            Message = message
        };
    }
}

/// <summary>
/// JSON serialization context for IPC messages (Native AOT compatibility)
/// </summary>
[JsonSerializable(typeof(Request))]
[JsonSerializable(typeof(Response))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class IpcJsonContext : JsonSerializerContext
{
}

