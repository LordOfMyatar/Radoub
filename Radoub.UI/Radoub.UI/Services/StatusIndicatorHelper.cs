namespace Radoub.UI.Services;

/// <summary>
/// Provides Unicode icons for status indicators that work alongside color
/// to support colorblind accessibility (WCAG 2.1 1.4.1 - Use of Color).
/// </summary>
/// <remarks>
/// These icons should be displayed WITH color indicators, not instead of them.
/// The combination provides redundant visual cues for colorblind users.
///
/// Pattern matches Quartermaster's FeatsPanel/SkillsPanel existing implementation.
/// </remarks>
public static class StatusIndicatorHelper
{
    /// <summary>Success indicator (checkmark) - U+2713</summary>
    public const string Success = "\u2713";

    /// <summary>Error indicator (X mark) - U+2717</summary>
    public const string Error = "\u2717";

    /// <summary>Warning indicator (warning sign) - U+26A0</summary>
    public const string Warning = "\u26A0";

    /// <summary>Info indicator (information) - U+2139</summary>
    public const string Info = "\u2139";

    /// <summary>
    /// Formats a validation message with the appropriate status icon.
    /// </summary>
    /// <param name="message">The validation message text</param>
    /// <param name="isValid">True for success icon, false for error icon</param>
    /// <returns>Message prefixed with status icon and space</returns>
    public static string FormatValidation(string message, bool isValid)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var icon = isValid ? Success : Error;
        return $"{icon} {message}";
    }

    /// <summary>
    /// Formats a message with a warning icon.
    /// </summary>
    /// <param name="message">The warning message text</param>
    /// <returns>Message prefixed with warning icon and space</returns>
    public static string FormatWarning(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        return $"{Warning} {message}";
    }

    /// <summary>
    /// Formats a message with an info icon.
    /// </summary>
    /// <param name="message">The info message text</param>
    /// <returns>Message prefixed with info icon and space</returns>
    public static string FormatInfo(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        return $"{Info} {message}";
    }
}
