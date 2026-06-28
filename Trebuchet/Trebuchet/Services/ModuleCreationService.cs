using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Creates a new module (.mod) from scratch by cloning a blank-forest template (#2268).
///
/// The template is a complete, NWN:EE-loadable blank module — one area (.are/.git/.gic),
/// module.ifo, Repute.fac, and the standard palette .itp files. Cloning the template
/// (rather than synthesizing each format) guarantees the result loads in-game without
/// reimplementing ARE/GIT/IFO generation.
///
/// NOTE: This service is intentionally NOT wired into the Trebuchet UI yet. There is
/// little value in a "New Module" command until area creation exists; this lays the
/// tested foundation to wire in later.
/// </summary>
public class ModuleCreationService
{
    /// <summary>
    /// Create a new module at <paramref name="filePath"/> by cloning <paramref name="templatePath"/>.
    /// </summary>
    /// <param name="filePath">Output .mod path. The filename stem must satisfy Aurora's
    /// 16-char, lowercase, alphanumeric/underscore constraints.</param>
    /// <param name="templatePath">Path to a blank-forest template .mod to clone.</param>
    /// <param name="overwrite">When false (default), throws if the output already exists.</param>
    /// <exception cref="ArgumentException">The filename violates Aurora naming rules.</exception>
    /// <exception cref="FileNotFoundException">The template does not exist.</exception>
    /// <exception cref="IOException">The output exists and <paramref name="overwrite"/> is false.</exception>
    public void CreateModule(string filePath, string templatePath, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var stem = Path.GetFileNameWithoutExtension(filePath);
        var validation = AuroraFilenameValidator.Validate(stem);
        if (!validation.IsValid)
            throw new ArgumentException(validation.GetErrorMessage(), nameof(filePath));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Module template not found: {templatePath}", templatePath);

        if (!overwrite && File.Exists(filePath))
            throw new IOException($"File already exists: {filePath}");

        // Read the template and re-extract every resource so the new module is a clean,
        // self-contained copy rather than a reference to the template on disk.
        var template = ErfReader.Read(templatePath);
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        foreach (var entry in template.Resources)
        {
            resourceData[(entry.ResRef.ToLowerInvariant(), entry.ResourceType)] =
                ErfReader.ExtractResource(templatePath, entry);
        }

        var mod = new ErfFile
        {
            FileType = "MOD ",
            FileVersion = "V1.0",
            // Stamp the new module's build date (days since 1900-01-01).
            BuildYear = (uint)(DateTime.Now.Year - 1900),
            BuildDay = (uint)DateTime.Now.DayOfYear,
            Resources = template.Resources,
            LocalizedStrings = template.LocalizedStrings,
            DescriptionStrRef = template.DescriptionStrRef,
        };

        ErfWriter.Write(mod, filePath, resourceData);

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Created new module from template: {PrivacyHelper.SanitizePath(filePath)} " +
            $"({mod.Resources.Count} resources)");
    }
}
