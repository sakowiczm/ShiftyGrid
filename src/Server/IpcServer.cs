using System.IO.Pipes;
using System.Text.Json;
using ShiftyGrid.Common;

namespace ShiftyGrid.Server;

public class IpcServer : IDisposable
{
    private const string PipeName = "ShiftyGrid_Commands";
    private readonly Thread _serverThread;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Func<Request, Response> _requestHandler;

    /// <summary>
    /// Creates a new IPC server
    /// </summary>
    /// <param name="requestHandler">Function to handle requests on main thread</param>
    public IpcServer(Func<Request, Response> requestHandler)
    {
        _requestHandler = requestHandler;
        _cancellationTokenSource = new CancellationTokenSource();

        _serverThread = new Thread(ServerThreadProc)
        {
            Name = "IPC Server Thread",
            IsBackground = true
        };
    }

    public void Start()
    {
        Logger.Debug($"Starting IPC server on: \\\\.\\pipe\\{PipeName}");
        _serverThread.Start();
    }

    /// <summary>
    /// Server thread procedure - listens for client connections
    /// </summary>
    private void ServerThreadProc()
    {
        try
        {
            Logger.Info("IPC server thread started");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Create a new pipe instance for this connection
                    using var pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    Logger.Debug("Waiting for client connection...");

                    // Wait for client connection (with cancellation support)
                    var connectTask = pipeServer.WaitForConnectionAsync(_cancellationTokenSource.Token);
                    connectTask.GetAwaiter().GetResult();

                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    Logger.Debug("Client connected");

                    // Handle the client connection
                    HandleClientConnection(pipeServer);
                }
                catch (OperationCanceledException)
                {
                    // Expected when shutting down
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in IPC server loop: {ex.Message}", ex);

                    // Don't spin too fast on errors
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }

            Logger.Info("IPC server thread stopped");
        }
        catch (Exception ex)
        {
            Logger.Error($"Fatal error in IPC server thread: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles a single client connection
    /// </summary>
    private void HandleClientConnection(NamedPipeServerStream pipeServer)
    {
        try
        {
            // Read request from client
            using var reader = new StreamReader(pipeServer, leaveOpen: true);
            var requestJson = reader.ReadLine();

            if (string.IsNullOrEmpty(requestJson))
            {
                Logger.Error("Received empty request from client");
                return;
            }

            Logger.Debug($"Received request JSON: {requestJson}");

            // Deserialize request
            var request = JsonSerializer.Deserialize<Request>(requestJson, IpcJsonContext.Default.Request);

            if (request == null)
            {
                Logger.Error("Failed to deserialize request");
                SendResponse(pipeServer, Response.CreateError("Invalid request format"));
                return;
            }

            Logger.Info($"Processing request: {request.Command}");

            // Process request via handler
            Response response;
            try
            {
                response = _requestHandler(request);
            }
            catch (Exception ex)
            {
                Logger.Error($"Request handler threw exception: {ex.Message}", ex);
                response = Response.CreateError($"Request execution failed: {ex.Message}");
            }

            // Send response back to client
            SendResponse(pipeServer, response);

            Logger.Debug($"Response sent to client: Success={response.Success}");
        }
        catch (IOException ex)
        {
            Logger.Error($"I/O error handling client: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling client connection: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (pipeServer.IsConnected)
                {
                    pipeServer.Disconnect();
                }
            }
            catch
            {
                // Ignore disconnect errors
            }
        }
    }

    /// <summary>
    /// Sends a response to the client
    /// </summary>
    private static void SendResponse(NamedPipeServerStream pipeServer, Response response)
    {
        try
        {
            var responseJson = JsonSerializer.Serialize(response, IpcJsonContext.Default.Response);

            using var writer = new StreamWriter(pipeServer, leaveOpen: true) { AutoFlush = true };
            writer.WriteLine(responseJson);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error sending response: {ex.Message}", ex);
        }
    }

    public void Stop()
    {
        Logger.Info("Stopping IPC server...");
        _cancellationTokenSource.Cancel();

        // Give the thread time to clean up
        if (!_serverThread.Join(TimeSpan.FromSeconds(2)))
        {
            Logger.Error("IPC server thread did not stop gracefully");
        }
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
    }
}

