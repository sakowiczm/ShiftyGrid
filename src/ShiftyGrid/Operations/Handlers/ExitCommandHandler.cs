using Windows.Win32;
using ShiftyGrid.Server;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;

internal class ExitCommandHandler(Action<bool> setShouldExit, Func<bool> getShouldExit, uint mainThreadId)
    : IRequestHandler
{
    public Response Handle(Request request)
    {
        if (getShouldExit())
        {
            return Response.CreateSuccess("Server is already shutting down");
        }

        Logger.Info("Exit command received, stopping server");

        setShouldExit(true);

        // Post quit message to the main thread to exit the message loop
        PInvoke.PostThreadMessage(mainThreadId, PInvoke.WM_QUIT, 0, 0);

        return Response.CreateSuccess("Server is shutting down");
    }
}
