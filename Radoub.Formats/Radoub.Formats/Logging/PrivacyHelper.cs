namespace Radoub.Formats.Logging;

/// <summary>
/// Helper for sanitizing paths and other potentially sensitive information in logs.
/// Prevents user home directory paths from appearing in log files.
/// </summary>
public static class PrivacyHelper
{
    private static readonly string? UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Sanitize a file path for logging - replaces home directory with ~
    /// </summary>
    public static string? SanitizePath(string? path)
    {
        if (path == null)
            return null;
        if (path.Length == 0)
            return string.Empty;

        if (!string.IsNullOrEmpty(UserProfile) &&
            path.StartsWith(UserProfile, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(UserProfile.Length);
        }

        return path;
    }

    /// <summary>
    /// Detects if a string appears to be a file path based on heuristics.
    /// </summary>
    private static bool LooksLikePath(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 3)
            return false;

        // If message contains common log patterns, it's not a pure path
        if (text.Contains(": ") || text.Contains(" = ") || text.Contains(" - "))
            return false;

        // Windows absolute paths: C:\, D:\, etc.
        if (text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/'))
            return true;

        // Unix absolute paths: /home/, /usr/, etc.
        if (text.StartsWith('/'))
            return true;

        // Windows UNC paths: \\server\share
        if (text.StartsWith("\\\\"))
            return true;

        // Contains path separators - check for file extension or common directories
        if (text.Contains('\\') || (text.Contains('/') && !text.StartsWith("http://") && !text.StartsWith("https://")))
        {
            var lastPart = text.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (lastPart != null && (lastPart.Contains('.') ||
                lastPart.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                lastPart.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                lastPart.Equals("Release", StringComparison.OrdinalIgnoreCase) ||
                lastPart.Equals("Plugins", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Automatically sanitizes paths embedded in a message string.
    /// Finds and replaces any user profile paths with ~.
    /// </summary>
    public static string AutoSanitizeMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return message ?? string.Empty;

        // Check if the entire message looks like a path
        if (LooksLikePath(message))
        {
            return SanitizePath(message) ?? string.Empty;
        }

        // For messages with potential embedded paths
        if (string.IsNullOrEmpty(UserProfile))
            return message;

        // If message contains the user profile path, sanitize it
        if (!message.Contains(UserProfile, StringComparison.OrdinalIgnoreCase))
            return message;

        var result = message;
        var startIndex = result.IndexOf(UserProfile, StringComparison.OrdinalIgnoreCase);

        while (startIndex != -1)
        {
            var endIndex = startIndex + UserProfile.Length;

            // Find end of path (next delimiter)
            while (endIndex < result.Length &&
                   result[endIndex] != ' ' &&
                   result[endIndex] != '"' &&
                   result[endIndex] != '\'' &&
                   result[endIndex] != '\n' &&
                   result[endIndex] != '\r')
            {
                endIndex++;
            }

            var fullPath = result.Substring(startIndex, endIndex - startIndex);
            var sanitized = SanitizePath(fullPath) ?? fullPath;
            result = result.Substring(0, startIndex) + sanitized + result.Substring(endIndex);

            // Look for next occurrence
            startIndex = result.IndexOf(UserProfile!, startIndex + sanitized.Length, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
