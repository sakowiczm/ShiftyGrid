using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Keyboard.Models;

namespace ShiftyGrid.Keyboard.Hooks;

/// <summary>
/// Implements a low-level Windows keyboard hook to intercept keyboard input system-wide.
/// </summary>
/// <remarks>
/// <para>
/// This class uses the Windows WH_KEYBOARD_LL hook to intercept all keyboard events
/// before they reach any application. This enables:
/// - Detection of key combinations for shortcuts
/// - Blocking keys from reaching other applications
/// - Tracking modifier key state (Ctrl, Alt, Shift, Win)
/// </para>
/// <para><strong>IMPORTANT: Threading and Message Pump Requirements</strong></para>
/// <para>
/// Low-level keyboard hooks MUST be installed on a thread with a Windows message pump.
/// For console applications, this requires:
/// 1. Creating a dedicated STA thread
/// 2. Installing the hook on that thread
/// 3. Running GetMessage/DispatchMessage loop on that thread
/// </para>
/// <para><strong>Performance Considerations</strong></para>
/// <para>
/// The hook callback is called for EVERY keyboard event system-wide. It must:
/// - Execute extremely fast (Microsoft recommends under 300ms)
/// - Not block or perform heavy operations
/// - Dispatch work to thread pool if needed
/// - Avoid throwing exceptions
/// </para>
/// <para>
/// Slow hook callbacks can cause system-wide keyboard lag or hook timeout/removal by Windows.
/// </para>
/// <para><strong>Memory Management</strong></para>
/// <para>
/// The hook delegate (_hookProc) is stored as an instance field to prevent garbage collection.
/// If the delegate is collected, Windows will call invalid memory, causing crashes.
/// </para>
/// </remarks>
public class LowLevelKeyboardHook : IDisposable
{
    /// <summary>
    /// The hook callback delegate. Stored as instance field to prevent GC collection.
    /// CRITICAL: Do not remove this field or make it local - it prevents crashes.
    /// </summary>
    private readonly HOOKPROC _hookProc;

    /// <summary>
    /// Handle to the installed keyboard hook. HHOOK.Null if not installed.
    /// </summary>
    private HHOOK _hookHandle;

    /// <summary>
    /// Tracks the currently pressed modifier keys. Updated on every key event.
    /// </summary>
    private ModifierKeys _currentModifiers;

    /// <summary>
    /// Lock object for thread-safe access to hook state.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Occurs when a key is pressed down.
    /// </summary>
    /// <remarks>
    /// This event is raised on the hook thread (message pump thread).
    /// Subscribers should be very fast or dispatch work to another thread.
    /// Set <see cref="KeyEventArgs.ShouldBlock"/> to true to prevent the key from
    /// reaching other applications.
    /// </remarks>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>
    /// Occurs when a key is released.
    /// </summary>
    /// <remarks>
    /// This event is raised on the hook thread (message pump thread).
    /// Less commonly used than KeyDown for shortcut detection.
    /// </remarks>
    public event EventHandler<KeyEventArgs>? KeyUp;

    /// <summary>
    /// Initializes a new instance of the <see cref="LowLevelKeyboardHook"/> class.
    /// </summary>
    /// <remarks>
    /// The hook is not installed until <see cref="Install"/> is called.
    /// </remarks>
    public LowLevelKeyboardHook()
    {
        // Keep delegate as instance field to prevent GC collection
        // CRITICAL: The native Windows hook stores a function pointer to this delegate.
        // If the delegate is garbage collected, Windows will call invalid memory -> crash!
        _hookProc = HookCallback;
    }

    /// <summary>
    /// Installs the low-level keyboard hook.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMPORTANT: This must be called on a thread with a Windows message pump!
    /// For console apps, call this from an STA thread running GetMessage/DispatchMessage.
    /// </para>
    /// <para>
    /// The hook monitors ALL keyboard input system-wide using the WH_KEYBOARD_LL hook type.
    /// This is a low-level hook that sees every keystroke before any application.
    /// </para>
    /// <para>
    /// The hook remains active until Uninstall() is called or the application exits.
    /// If the hook callback is too slow (&gt;300ms), Windows may automatically remove it.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if SetWindowsHookEx fails. Common causes:
    /// - Not called from a thread with a message pump
    /// - Insufficient permissions
    /// - Too many hooks already installed
    /// </exception>
    public void Install()
    {
        lock (_lock)
        {
            // Already installed - idempotent operation
            if (!_hookHandle.IsNull)
                return;

            // Install WH_KEYBOARD_LL (low-level keyboard hook)
            // Parameters:
            // - WH_KEYBOARD_LL: Low-level keyboard hook type (sees all keystrokes)
            // - _hookProc: Our callback delegate
            // - HINSTANCE.Null: For LL hooks, this must be NULL
            // - 0: Thread ID - 0 means hook all threads (global hook)
            _hookHandle = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
                _hookProc,
                HINSTANCE.Null,
                0);

            if (_hookHandle.IsNull)
            {
                throw new InvalidOperationException("Failed to install keyboard hook");
            }
        }
    }

    /// <summary>
    /// Uninstalls the keyboard hook if it is currently installed.
    /// </summary>
    /// <remarks>
    /// This is safe to call multiple times. After uninstalling, no more keyboard
    /// events will be received. The hook can be re-installed by calling Install() again.
    /// </remarks>
    public void Uninstall()
    {
        lock (_lock)
        {
            if (!_hookHandle.IsNull)
            {
                PInvoke.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = default; // Set to null handle
            }
        }
    }

    /// <summary>
    /// Hook callback procedure invoked by Windows for every keyboard event system-wide.
    /// </summary>
    /// <param name="nCode">
    /// Hook code. If &lt; 0, must call CallNextHookEx and return immediately.
    /// If &gt;= 0, contains HC_ACTION indicating normal keyboard event.
    /// </param>
    /// <param name="wParam">
    /// Message identifier: WM_KEYDOWN (0x0100), WM_KEYUP (0x0101),
    /// WM_SYSKEYDOWN (0x0104), or WM_SYSKEYUP (0x0105).
    /// SYS variants are for Alt+Key combinations and system keys.
    /// </param>
    /// <param name="lParam">
    /// Pointer to KBDLLHOOKSTRUCT containing virtual key code and other event data.
    /// </param>
    /// <returns>
    /// -1 to block the key from being processed further (consumed).
    /// Otherwise, result from CallNextHookEx to pass the event to the next hook.
    /// </returns>
    /// <remarks>
    /// <para><strong>PERFORMANCE CRITICAL:</strong></para>
    /// <para>
    /// This method is called on the hook thread for EVERY keyboard event system-wide.
    /// It must execute as fast as possible (&lt;300ms recommended by Microsoft).
    /// Slow processing can cause system-wide keyboard lag or hook removal by Windows.
    /// </para>
    /// <para><strong>Key Blocking Mechanism:</strong></para>
    /// <para>
    /// Returning -1 tells Windows to stop processing this key event - it won't reach
    /// any application. This is how we "consume" shortcut keys. Use carefully as it
    /// can override important system shortcuts (e.g., Ctrl+Alt+Delete).
    /// </para>
    /// </remarks>
    private LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        // Windows hook protocol: if nCode < 0, we MUST call CallNextHookEx immediately
        // and return its result without processing. This is a Windows requirement.
        if (nCode >= 0)
        {
            unsafe
            {
                // Extract keyboard event data from the KBDLLHOOKSTRUCT pointer
                // lParam points to a KBDLLHOOKSTRUCT in unmanaged memory
                var hookStruct = *(KBDLLHOOKSTRUCT*)lParam.Value;
                var vkCode = (int)hookStruct.vkCode;

                // Determine if this is a key down or key up event
                // WM_KEYDOWN (0x0100): Normal key press
                // WM_SYSKEYDOWN (0x0104): System key press (Alt+Key combinations)
                // WM_KEYUP (0x0101): Normal key release
                // WM_SYSKEYUP (0x0105): System key release
                var isKeyDown = wParam.Value == 0x0100 || wParam.Value == 0x0104;
                var isKeyUp = wParam.Value == 0x0101 || wParam.Value == 0x0105;

                // Update our internal modifier state tracking
                // This must happen BEFORE we create the event args, so args.Modifiers
                // reflects the current state including this key if it's a modifier
                UpdateModifierState(vkCode, isKeyDown);

                // Create event arguments with current keyboard state
                var args = new KeyEventArgs
                {
                    VirtualKeyCode = vkCode,
                    Modifiers = _currentModifiers,
                    ShouldBlock = false  // Default: don't block
                };

                // Raise appropriate event - subscribers can set args.ShouldBlock = true
                if (isKeyDown)
                {
                    KeyDown?.Invoke(this, args);
                }
                else if (isKeyUp)
                {
                    KeyUp?.Invoke(this, args);
                }

                // If any subscriber requested blocking, return -1 to consume the key
                // This prevents the key from reaching any other application
                if (args.ShouldBlock)
                {
                    return (LRESULT)(-1);
                }
            }
        }

        // Pass the event to the next hook in the chain (or to the target window)
        // HHOOK.Null is acceptable for low-level hooks
        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    /// <summary>
    /// Updates the internal modifier key state based on key press/release events.
    /// </summary>
    /// <param name="vkCode">The virtual key code of the key.</param>
    /// <param name="isPressed">True if the key was pressed; false if released.</param>
    /// <remarks>
    /// <para>
    /// This method tracks the state of Control, Alt, Shift, and Win modifier keys.
    /// It handles both left/right specific keys (e.g., VK_LCONTROL, VK_RCONTROL)
    /// and generic keys (e.g., VK_CONTROL).
    /// </para>
    /// <para>
    /// Uses bitwise OR to add modifiers and bitwise AND with NOT to remove them,
    /// allowing multiple modifiers to be tracked simultaneously.
    /// </para>
    /// </remarks>
    private void UpdateModifierState(int vkCode, bool isPressed)
    {
        // Map virtual key codes to modifier key flags
        // We check for left/right specific variants AND generic variants
        // because different APIs and keyboards may report different codes
        var modifier = vkCode switch
        {
            0xA2 => ModifierKeys.Control, // VK_LCONTROL - Left Control
            0xA3 => ModifierKeys.Control, // VK_RCONTROL - Right Control
            0x11 => ModifierKeys.Control, // VK_CONTROL - Generic Control
            0xA4 => ModifierKeys.Alt,     // VK_LMENU - Left Alt (Menu key)
            0xA5 => ModifierKeys.Alt,     // VK_RMENU - Right Alt (AltGr on some keyboards)
            0x12 => ModifierKeys.Alt,     // VK_MENU - Generic Alt
            0xA0 => ModifierKeys.Shift,   // VK_LSHIFT - Left Shift
            0xA1 => ModifierKeys.Shift,   // VK_RSHIFT - Right Shift
            0x10 => ModifierKeys.Shift,   // VK_SHIFT - Generic Shift
            0x5B => ModifierKeys.Win,     // VK_LWIN - Left Windows key
            0x5C => ModifierKeys.Win,     // VK_RWIN - Right Windows key
            _ => ModifierKeys.None        // Not a modifier key
        };

        if (modifier != ModifierKeys.None)
        {
            if (isPressed)
            {
                // Add modifier to current state using bitwise OR
                // Example: _currentModifiers = Control, pressing Alt:
                // Control (0001) | Alt (0010) = Control|Alt (0011)
                _currentModifiers |= modifier;
            }
            else
            {
                // Remove modifier from current state using bitwise AND with NOT
                // Example: _currentModifiers = Control|Alt (0011), releasing Alt:
                // 0011 & ~0010 = 0011 & 1101 = 0001 = Control
                _currentModifiers &= ~modifier;
            }
        }
    }

    /// <summary>
    /// Gets the currently pressed modifier keys.
    /// </summary>
    /// <returns>A combination of modifier keys currently pressed.</returns>
    /// <remarks>
    /// This is thread-safe and returns a snapshot of the current modifier state.
    /// Useful for checking modifier state outside of key events.
    /// </remarks>
    public ModifierKeys GetCurrentModifiers()
    {
        lock (_lock)
        {
            return _currentModifiers;
        }
    }

    /// <summary>
    /// Uninstalls the keyboard hook and releases resources.
    /// </summary>
    public void Dispose()
    {
        Uninstall();
    }
}

/// <summary>
/// Provides data for keyboard events from the low-level hook.
/// </summary>
/// <remarks>
/// This class is used to pass keyboard event information from the hook callback
/// to event subscribers. Subscribers can set <see cref="ShouldBlock"/> to true
/// to prevent the key from being processed by other applications.
/// </remarks>
public class KeyEventArgs : EventArgs
{
    /// <summary>
    /// Gets or initializes the Windows virtual key code for the key that triggered this event.
    /// </summary>
    /// <remarks>
    /// See: https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    /// Common examples: 0x41-0x5A (A-Z), 0x30-0x39 (0-9), 0x70-0x7B (F1-F12).
    /// </remarks>
    public int VirtualKeyCode { get; init; }

    /// <summary>
    /// Gets or initializes the modifier keys (Ctrl, Alt, Shift, Win) pressed during this event.
    /// </summary>
    /// <remarks>
    /// This represents the state of modifiers at the time of the event.
    /// Multiple modifiers can be combined using bitwise OR.
    /// </remarks>
    public ModifierKeys Modifiers { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this key event should be blocked from
    /// reaching other applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to true, the hook callback returns -1, which tells Windows to stop
    /// processing this key event. It won't reach any other hook or application.
    /// </para>
    /// <para>
    /// Use this to "consume" shortcut keys so they don't interfere with other applications.
    /// For example, if you handle Ctrl+S, you might want to block it from reaching
    /// a text editor which would also try to handle Ctrl+S.
    /// </para>
    /// <para>
    /// WARNING: Use carefully! Blocking important system shortcuts (like Ctrl+Alt+Delete)
    /// can make the system difficult to use.
    /// </para>
    /// </remarks>
    public bool ShouldBlock { get; set; }
}
