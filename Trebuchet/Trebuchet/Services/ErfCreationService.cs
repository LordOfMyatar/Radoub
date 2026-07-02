using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Creates new, empty ERF-family archives (ERF/HAK/MOD/…) from scratch.
/// Wraps <see cref="ErfWriter"/> (which already supports write-from-scratch with an
/// empty resource list) and enforces Aurora filename constraints up front (#2268, #2267).
/// </summary>
public class ErfCreationService
{
    /// <summary>
    /// ERF-family FileType values accepted by the writer/reader (matches
    /// <c>ErfReader.ValidFileTypes</c>). The trailing space is part of the 4-char header field.
    /// </summary>
    private static readonly string[] ValidFileTypes = { "ERF ", "HAK ", "MOD ", "SAV ", "NWM " };

    /// <summary>
    /// Create a new empty ERF-family archive at the given path.
    /// </summary>
    /// <param name="filePath">Output path. The filename stem (without extension) must satisfy
    /// Aurora's 16-char, lowercase, alphanumeric/underscore constraints.</param>
    /// <param name="description">Optional localized description (English, neutral gender).</param>
    /// <param name="overwrite">When false (default), throws if the file already exists.</param>
    /// <param name="fileType">4-char ERF-family header type (default <c>"ERF "</c>).</param>
    /// <exception cref="ArgumentException">The filename violates Aurora naming rules, or
    /// <paramref name="fileType"/> is not an ERF-family type.</exception>
    /// <exception cref="IOException">The file exists and <paramref name="overwrite"/> is false.</exception>
    public void CreateErf(string filePath, string? description = null, bool overwrite = false, string fileType = "ERF ")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        if (!Array.Exists(ValidFileTypes, t => t == fileType))
            throw new ArgumentException(
                $"Invalid ERF file type: '{fileType}', expected one of: {string.Join(", ", ValidFileTypes)}",
                nameof(fileType));

        var stem = Path.GetFileNameWithoutExtension(filePath);
        var validation = AuroraFilenameValidator.Validate(stem);
        if (!validation.IsValid)
            throw new ArgumentException(validation.GetErrorMessage(), nameof(filePath));

        if (!overwrite && File.Exists(filePath))
            throw new IOException($"File already exists: {filePath}");

        var erf = new ErfFile
        {
            FileType = fileType,
            FileVersion = "V1.0",
            // BuildYear = years since 1900; BuildDay = day-of-year (1-366).
            BuildYear = (uint)(DateTime.Now.Year - 1900),
            BuildDay = (uint)DateTime.Now.DayOfYear,
        };

        if (!string.IsNullOrEmpty(description))
        {
            // LanguageId 0 = English, neutral/masculine gender (LanguageID * 2 + Gender).
            erf.LocalizedStrings.Add(new ErfLocalizedString { LanguageId = 0, Text = description });
        }

        // Empty archive: no resources, so an empty data dictionary.
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        ErfWriter.Write(erf, filePath, resourceData);

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Created new empty {fileType.Trim()}: {PrivacyHelper.SanitizePath(filePath)}");
    }

    /// <summary>
    /// Create a new empty HAK archive at the given path. Thin convenience over
    /// <see cref="CreateErf"/> with <c>fileType = "HAK "</c> (#2267).
    /// </summary>
    /// <param name="filePath">Output path (stem must satisfy Aurora naming rules).</param>
    /// <param name="description">Optional localized description.</param>
    /// <param name="overwrite">When false (default), throws if the file already exists.</param>
    public void CreateHak(string filePath, string? description = null, bool overwrite = false)
        => CreateErf(filePath, description, overwrite, "HAK ");
}
