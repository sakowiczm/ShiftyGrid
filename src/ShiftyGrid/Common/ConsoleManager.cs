using Windows.Win32;
using Windows.Win32.System.Console;

namespace ShiftyGrid.Common;

/// <summary>
/// Manages console attachment and output when launched from command line
/// </summary>
internal static class ConsoleManager
{
    private const uint ATTACH_PARENT_PROCESS = unchecked((uint)-1);

    /// <summary>
    /// Gets whether the application is attached to a console
    /// </summary>
    public static bool IsAttached { get; private set; }

    /// <summary>
    /// Attempts to attach to the parent process's console.
    /// Returns true if successfully attached to a console, false otherwise.
    /// </summary>
    public static bool Attach()
    {
        // Try to attach to parent console (e.g., cmd.exe, PowerShell)
        if (PInvoke.AttachConsole(ATTACH_PARENT_PROCESS))
        {
            try
            {
                // Redirect Console streams to the attached console
                var stdoutHandle = PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
                var stderrHandle = PInvoke.GetStdHandle(STD_HANDLE.STD_ERROR_HANDLE);
                //var stdinHandle = PInvoke.GetStdHandle(STD_HANDLE.STD_INPUT_HANDLE);

                if (!stdoutHandle.IsNull)
                    Console.Out.Flush();
                if (!stderrHandle.IsNull)
                    Console.Error.Flush();

                IsAttached = true;
                return true;
            }
            catch
            {
                IsAttached = false;
                return false;
            }
        }

        IsAttached = false;
        return false;
    }

    /// <summary>
    /// Detaches from the currently attached console.
    /// After detachment, Console.WriteLine() calls will silently fail (no exception).
    /// Returns true if successfully detached, false otherwise.
    /// 
    /// Detach from console to prevent forced termination when console closes
    /// All early validation and startup messages have been displayed
    /// </summary>
    public static bool Detach()
    {
        if (!IsAttached)
        {
            return false;
        }

        try
        {
            if (PInvoke.FreeConsole())
            {
                IsAttached = false;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}

