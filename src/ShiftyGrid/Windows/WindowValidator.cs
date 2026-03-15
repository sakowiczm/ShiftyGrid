namespace ShiftyGrid.Windows;

/// <summary>
/// Validates windows for various operations like positioning, focusing, and organizing.
/// Centralizes all validation logic to ensure consistent behavior across handlers.
/// </summary>
internal class WindowValidator
{
    private readonly WindowMatcher _matcher;

    public WindowValidator(WindowMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>
    /// Result of a window validation operation.
    /// </summary>
    public record ValidationResult(bool IsValid, string? Reason)
    {
        public static ValidationResult Valid() => new(true, null);
        public static ValidationResult Invalid(string reason) => new(false, reason);
    }

    /// <summary>
    /// Validates whether a window can be positioned to a grid location.
    /// </summary>
    public ValidationResult ValidateForPositioning(Window window)
    {
        if (_matcher.ShouldIgnore(window))
            return ValidationResult.Invalid("Window matches ignore rule");

        if (window.IsFullscreen)
            return ValidationResult.Invalid("Window is fullscreen");

        if (!window.IsParent)
            return ValidationResult.Invalid("Window is not a parent window");

        if (window.State != WindowState.Normal)
            return ValidationResult.Invalid($"Window is {window.State}");

        if (!window.IsWindowReadyForPositioning())
            return ValidationResult.Invalid("Window not ready for positioning");

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates whether a window can receive focus.
    /// </summary>
    public ValidationResult ValidateForFocus(Window window)
    {
        if (_matcher.ShouldIgnore(window))
            return ValidationResult.Invalid("Window matches ignore rule");

        if (window.IsFullscreen)
            return ValidationResult.Invalid("Window is fullscreen");

        if (!window.IsParent)
            return ValidationResult.Invalid("Window is not a parent window");

        if (!window.IsWindowReady())
            return ValidationResult.Invalid("Window not ready");

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates whether a window can be organized automatically.
    /// </summary>
    public ValidationResult ValidateForOrganize(Window window)
    {
        if (_matcher.ShouldIgnore(window))
            return ValidationResult.Invalid("Window matches ignore rule");

        if (window.IsFullscreen)
            return ValidationResult.Invalid("Window is fullscreen");

        if (!window.IsParent)
            return ValidationResult.Invalid("Window is not a parent window");

        if (window.State != WindowState.Normal)
            return ValidationResult.Invalid($"Window is {window.State}");

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Quick check if a window is positionable (for performance-critical paths).
    /// </summary>
    public bool IsPositionable(Window window)
    {
        return !_matcher.ShouldIgnore(window) &&
               !window.IsFullscreen &&
               window.IsParent &&
               window.State == WindowState.Normal &&
               window.IsWindowReadyForPositioning();
    }

    /// <summary>
    /// Quick check if a window is focusable (for performance-critical paths).
    /// </summary>
    public bool IsFocusable(Window window)
    {
        return !_matcher.ShouldIgnore(window) &&
               !window.IsFullscreen &&
               window.IsParent &&
               window.IsWindowReady();
    }

    /// <summary>
    /// Quick check if a window can be organized (for performance-critical paths).
    /// </summary>
    public bool IsOrganizable(Window window)
    {
        return !_matcher.ShouldIgnore(window) &&
               !window.IsFullscreen &&
               window.IsParent &&
               window.State == WindowState.Normal;
    }
}
