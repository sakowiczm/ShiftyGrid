using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace ShiftyGrid.Windows;

/// <summary>
/// Monitors window creation events using WinEvent hooks and auto-organizes matching windows with progressive retry.
/// </summary>
internal class WindowEventMonitor : IDisposable
{

    /// <summary>
    /// State tracking for window organize attempts with progressive retry logic.
    /// </summary>
    internal class WindowOrganizeAttempt
    {
        public HWND WindowHandle { get; init; }
        public int AttemptCount { get; set; }
        public DateTime FirstAttemptTime { get; init; }
        public DateTime? LastAttemptTime { get; set; }
        public OrganizeAttemptState State { get; set; }
    }

    /// <summary>
    /// Attempt state for organizing windows.
    /// </summary>
    internal enum OrganizeAttemptState
    {
        Pending,      // Never attempted
        Retrying,     // In progress, will retry
        Succeeded,    // Successfully positioned
        Failed        // Max retries exceeded, giving up
    }

    private HWINEVENTHOOK _hookHandle;
    private WINEVENTPROC? _hookDelegate;  // Keep alive reference to prevent GC
    private readonly Dictionary<HWND, WindowOrganizeAttempt> _organizeAttempts = new();
    private readonly Dictionary<HWND, Timer> _pendingTimers = new();
    private readonly object _lock = new();
    private bool _isRunning;

    // Progressive retry schedule: 300ms, 900ms (300+600), 2100ms (300+600+1200) from window creation
    private readonly int[] _retryDelays = new[] { 300, 600, 1200 };
    private const int MAX_ATTEMPTS = 3;

    private Timer? _cleanupTimer;

    /// <summary>
    /// Starts monitoring window creation events.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        // Keep delegate alive to prevent garbage collection
        _hookDelegate = new WINEVENTPROC(WinEventCallback);

        // Install hook for window creation events
        _hookHandle = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_CREATE,
            PInvoke.EVENT_OBJECT_CREATE,
            HMODULE.Null,
            _hookDelegate,
            0,  // All processes
            0,  // All threads
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS
        );

        if (_hookHandle == IntPtr.Zero)
        {
            Logger.Error("WindowEventMonitor: Failed to install window event hook");
            return;
        }

        _isRunning = true;

        // Start periodic cleanup of old attempts (every 5 minutes)
        _cleanupTimer = new Timer(
            callback: _ => CleanupOldAttempts(),
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5)
        );

        Logger.Info("WindowEventMonitor: Started");
    }

    /// <summary>
    /// Stops monitoring and cleans up resources.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        lock (_lock)
        {
            // Cancel all pending timers
            foreach (var timer in _pendingTimers.Values)
            {
                timer?.Dispose();
            }
            _pendingTimers.Clear();
            _organizeAttempts.Clear();
        }

        // Stop cleanup timer
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;

        if (_hookHandle != IntPtr.Zero)
        {
            PInvoke.UnhookWinEvent(_hookHandle);
            _hookHandle = default;
        }

        _isRunning = false;
        _hookDelegate = null;
        Logger.Info("WindowEventMonitor: Stopped");
    }

    /// <summary>
    /// WinEvent callback - filters and schedules organize after delay.
    /// </summary>
    private void WinEventCallback(
        HWINEVENTHOOK hWinEventHook,
        uint eventType,
        HWND hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        // Filter: Only process window objects (not child controls)
        // OBJID_WINDOW = 0 (window object itself, not a child element)
        if (idObject != 0 || idChild != 0)
            return;

        // Validate window handle
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return;

        lock (_lock)
        {
            // Check if already processed
            if (_organizeAttempts.TryGetValue(hwnd, out var existing))
            {
                // Skip if succeeded or failed (don't retry completed attempts)
                if (existing.State == OrganizeAttemptState.Succeeded ||
                    existing.State == OrganizeAttemptState.Failed)
                {
                    return;
                }
            }
            else
            {
                // Create new attempt record
                _organizeAttempts[hwnd] = new WindowOrganizeAttempt
                {
                    WindowHandle = hwnd,
                    AttemptCount = 0,
                    FirstAttemptTime = DateTime.UtcNow,
                    State = OrganizeAttemptState.Pending
                };
            }

            // Schedule first attempt after 300ms delay (allows window to initialize)
            ScheduleAttempt(hwnd, _retryDelays[0]);
        }
    }

    /// <summary>
    /// Delayed organize callback with retry logic - runs on ThreadPool.
    /// </summary>
    private void OrganizeWindowDelayed(HWND hwnd)
    {
        WindowOrganizeAttempt? attempt = null;

        lock (_lock)
        {
            // Clean up timer
            if (_pendingTimers.TryGetValue(hwnd, out var timer))
            {
                timer?.Dispose();
                _pendingTimers.Remove(hwnd);
            }

            // Get/update attempt record
            if (!_organizeAttempts.TryGetValue(hwnd, out attempt))
            {
                Logger.Debug($"AutoOrganize: No attempt record for {hwnd}");
                return;
            }

            attempt.AttemptCount++;
            attempt.LastAttemptTime = DateTime.UtcNow;
            attempt.State = OrganizeAttemptState.Retrying;
        }

        // Create Window object
        Window? window = Window.FromHandle(hwnd, zOrder: 0);
        if (window == null)
        {
            Logger.Debug($"AutoOrganize: Window {hwnd} no longer exists");
            MarkAsFailed(hwnd, attempt);
            return;
        }

        // Skip child windows, dialogs, and popups - only organize root windows
        if (!window.IsParent)
        {
            Logger.Debug($"AutoOrganize: Skipping child window '{window.Text}'");
            MarkAsFailed(hwnd, attempt);
            return;
        }

        // Enhanced readiness check
        if (!window.IsWindowReadyForPositioning())
        {
            Logger.Debug($"AutoOrganize: Window '{window.Text}' not ready (attempt {attempt.AttemptCount}/{MAX_ATTEMPTS})");
            HandleFailedAttempt(hwnd, attempt, window);
            return;
        }

        // Try to organize
        bool success = TryOrganizeWindow(window);

        if (success)
        {
            // Verify positioning actually worked
            if (VerifyPositioning(hwnd))
            {
                Logger.Info($"AutoOrganize: Successfully organized '{window.Text}' on attempt {attempt.AttemptCount}");
                MarkAsSucceeded(hwnd, attempt);
            }
            else
            {
                Logger.Debug($"AutoOrganize: Positioning verification failed for '{window.Text}'");
                HandleFailedAttempt(hwnd, attempt, window);
            }
        }
        else
        {
            Logger.Debug($"AutoOrganize: TryOrganizeWindow returned false for '{window.Text}'");
            HandleFailedAttempt(hwnd, attempt, window);
        }
    }

    // todo: unify in WindowMatcher
    private const string IGNORED_WINDOW_CLASS = "ClunkyBordersOverlayClass";

    // todo: WindowEventMonitor should be standalone - what action is exetucted is a different thing
    //  unit TryOrganizeWindow in OrganizeCommandHandler

    /// <summary>
    /// Attempts to organize a window if it matches any organize rule.
    /// Returns true if window was organized, false if no match or failed.
    /// </summary>
    public static bool TryOrganizeWindow(Window window)
    {
        // Skip ignored windows
        if (window.ClassName == IGNORED_WINDOW_CLASS)
            return false;

        // Skip minimized windows
        if (window.State == WindowState.Minimized)
            return false;

        // Skip child windows, dialogs, and popups - only organize root windows
        if (!window.IsParent)
            return false;

        // todo: fix config reference

        // Find matching rule
        var matcher = WindowMatcher.FindMatchingRule(window, OrganizeConfig.GetDefault());
        if (matcher == null)
        {
            Logger.Debug($"AutoOrganize: No match for '{window.Text}'");
            return false;
        }

        Logger.Info($"AutoOrganize: Matched '{window.Text}' -> {matcher.Position}");

        // Apply position
        try
        {
            return WindowPositioner.ChangePosition(window, matcher.Position, Config.GAP);
        }
        catch (Exception ex)
        {
            Logger.Error($"AutoOrganize: Error positioning '{window.Text}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Handles a failed organize attempt - either schedules retry or gives up.
    /// </summary>
    private void HandleFailedAttempt(HWND hwnd, WindowOrganizeAttempt attempt, Window? window = null)
    {
        lock (_lock)
        {
            if (attempt.AttemptCount >= MAX_ATTEMPTS)
            {
                Logger.Warning($"AutoOrganize: Max attempts reached for '{window?.Text ?? hwnd.ToString()}', giving up");
                MarkAsFailed(hwnd, attempt);
                return;
            }

            // Schedule next retry with progressive delay
            int nextDelay = _retryDelays[attempt.AttemptCount];
            Logger.Debug($"AutoOrganize: Scheduling retry {attempt.AttemptCount + 1} in {nextDelay}ms for '{window?.Text ?? hwnd.ToString()}'");
            ScheduleAttempt(hwnd, nextDelay);
        }
    }

    /// <summary>
    /// Schedules an organize attempt after the specified delay.
    /// </summary>
    private void ScheduleAttempt(HWND hwnd, int delayMs)
    {
        var timer = new Timer(
            callback: _ => OrganizeWindowDelayed(hwnd),
            state: null,
            dueTime: delayMs,
            period: Timeout.Infinite
        );
        _pendingTimers[hwnd] = timer;
    }

    /// <summary>
    /// Verifies that positioning succeeded by checking window still exists and is visible.
    /// </summary>
    private bool VerifyPositioning(HWND hwnd)
    {
        Thread.Sleep(50);  // Brief moment for positioning to complete
        return PInvoke.IsWindow(hwnd) && PInvoke.IsWindowVisible(hwnd);
    }

    /// <summary>
    /// Marks a window organize attempt as succeeded.
    /// </summary>
    private void MarkAsSucceeded(HWND hwnd, WindowOrganizeAttempt attempt)
    {
        lock (_lock)
        {
            attempt.State = OrganizeAttemptState.Succeeded;
        }
    }

    /// <summary>
    /// Marks a window organize attempt as failed (max retries exceeded).
    /// </summary>
    private void MarkAsFailed(HWND hwnd, WindowOrganizeAttempt attempt)
    {
        lock (_lock)
        {
            attempt.State = OrganizeAttemptState.Failed;
        }
    }

    /// <summary>
    /// Cleans up old attempt records to prevent memory leaks.
    /// </summary>
    private void CleanupOldAttempts()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            var toRemove = _organizeAttempts
                .Where(kvp => kvp.Value.FirstAttemptTime < cutoff &&
                             (kvp.Value.State == OrganizeAttemptState.Succeeded ||
                              kvp.Value.State == OrganizeAttemptState.Failed))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var hwnd in toRemove)
            {
                _organizeAttempts.Remove(hwnd);
            }

            if (toRemove.Count > 0)
            {
                Logger.Debug($"WindowEventMonitor: Cleaned up {toRemove.Count} old attempt records");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

}
