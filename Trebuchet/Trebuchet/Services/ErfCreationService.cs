using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Creates new, empty ERF archives from scratch.
/// Wraps <see cref="ErfWriter"/> (which already supports write-from-scratch with an
/// empty resource list) and enforces Aurora filename constraints up front (#2268).
/// </summary>
public class ErfCreationService
{
    /// <summary>
    /// Create a new empty ERF archive at the given path.
    /// </summary>
    /// <param name="filePath">Output path. The filename stem (without extension) must satisfy
    /// Aurora's 16-char, lowercase, alphanumeric/underscore constraints.</param>
    /// <param name="description">Optional localized description (English, neutral gender).</param>
    /// <param name="overwrite">When false (default), throws if the file already exists.</param>
    /// <exception cref="ArgumentException">The filename violates Aurora naming rules.</exception>
    /// <exception cref="IOException">The file exists and <paramref name="overwrite"/> is false.</exception>
    public void CreateErf(string filePath, string? description = null, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var stem = Path.GetFileNameWithoutExtension(filePath);
        var validation = AuroraFilenameValidator.Validate(stem);
        if (!validation.IsValid)
            throw new ArgumentException(validation.GetErrorMessage(), nameof(filePath));

        if (!overwrite && File.Exists(filePath))
            throw new IOException($"File already exists: {filePath}");

        var erf = new ErfFile
        {
            FileType = "ERF ",
            FileVersion = "V1.0",
        };

        if (!string.IsNullOrEmpty(description))
        {
            // LanguageId 0 = English, neutral/masculine gender (LanguageID * 2 + Gender).
            erf.LocalizedStrings.Add(new ErfLocalizedString { LanguageId = 0, Text = description });
        }

        // Empty archive: no resources, so an empty data dictionary.
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        ErfWriter.Write(erf, filePath, resourceData);

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Created new empty ERF: {PrivacyHelper.SanitizePath(filePath)}");
    }
}
