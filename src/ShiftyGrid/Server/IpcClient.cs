using System.IO.Pipes;
using System.Text.Json;
using ShiftyGrid.Common;

namespace ShiftyGrid.Server;

public class IpcClient : IDisposable
{
    private const string PipeName = "ShiftyGrid_Commands";
    private const int ConnectionTimeoutMs = 5000; // 5 seconds
    private NamedPipeClientStream? _pipeClient;

    public async Task<Response> SendRequestAsync(Request request)
    {
        try
        {
            _pipeClient = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            Logger.Debug($"Connecting to IPC server: \\\\.\\pipe\\{PipeName}");

            using var cts = new CancellationTokenSource(ConnectionTimeoutMs);
            await _pipeClient.ConnectAsync(cts.Token);

            Logger.Info("Connected to IPC server");

            // Serialize and send request
            var requestJson = JsonSerializer.Serialize(request, IpcJsonContext.Default.Request);
            Logger.Debug($"Sending request: {request.Command}");
            
            using var writer = new StreamWriter(_pipeClient, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(requestJson);

            // Wait for response
            Logger.Debug("Waiting for response...");
            using var reader = new StreamReader(_pipeClient, leaveOpen: true);
            var responseJson = await reader.ReadLineAsync();

            if (string.IsNullOrEmpty(responseJson))
            {
                Logger.Error("Received empty response from server");
                return Response.CreateError("No response from server");
            }

            // Deserialize response
            var response = JsonSerializer.Deserialize<Response>(responseJson, IpcJsonContext.Default.Response);

            if (response == null)
            {
                Logger.Error("Failed to deserialize response");
                return Response.CreateError("Invalid response from server");
            }

            Logger.Debug($"Received response: Success={response.Success}, Message={response.Message}");
            return response;
        }
        catch (TimeoutException)
        {
            Logger.Error("Connection timeout - is ShiftyGrid running?");
            return Response.CreateError("Connection timeout. No ShiftyGrid instance is running.");
        }
        catch (IOException ex) when (ex.Message.Contains("pipe"))
        {
            Logger.Error($"Pipe connection failed: {ex.Message}");
            return Response.CreateError("Cannot connect to ShiftyGrid. No instance is running.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error sending request: {ex.Message}", ex);
            return Response.CreateError($"Error: {ex.Message}");
        }
    }

    public Response SendRequest(Request request)
    {
        return SendRequestAsync(request).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _pipeClient?.Dispose();
    }
}


