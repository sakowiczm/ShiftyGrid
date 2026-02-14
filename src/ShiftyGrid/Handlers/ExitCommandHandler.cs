using Windows.Win32;
using ShiftyGrid.Common;
using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal class ExitCommandHandler(Action<bool> setShouldExit, Func<bool> getShouldExit, uint mainThreadId)
    : IRequestHandler
{
    public Response Handle(Request request)
    {
        if (getShouldExit())
        {
            return Response.CreateSuccess("Server is already shutting down");
        }

        Console.WriteLine("Exit command received via IPC. Shutting down...");
        Logger.Info("Exit command received, stopping server");

        setShouldExit(true);

        // Post quit message to the main thread to exit the message loop
        PInvoke.PostThreadMessage(mainThreadId, PInvoke.WM_QUIT, 0, 0);

        return Response.CreateSuccess("Server is shutting down");
    }
}
