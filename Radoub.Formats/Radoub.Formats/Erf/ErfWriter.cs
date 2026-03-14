using System.Text;

namespace Radoub.Formats.Erf;

/// <summary>
/// Writes ERF files (including HAK, MOD, SAV) to binary format.
/// Reference: BioWare Aurora ERF format spec, neverwinter.nim erf.nim
/// </summary>
public static class ErfWriter
{
    private const int HeaderSize = 160;
    private const int KeyEntrySize = 24;
    private const int ResourceEntrySize = 8;

    /// <summary>
    /// Write an ERF file to disk.
    /// </summary>
    /// <param name="erf">The ERF data to write.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="resourceData">Dictionary mapping ResRef+Type to resource data bytes.</param>
    public static void Write(ErfFile erf, string filePath, Dictionary<(string ResRef, ushort Type), byte[]> resourceData)
    {
        using var fs = File.Create(filePath);
        Write(erf, fs, resourceData);
    }

    /// <summary>
    /// Write an ERF file to a stream.
    /// </summary>
    /// <param name="erf">The ERF data to write.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="resourceData">Dictionary mapping ResRef+Type to resource data bytes.</param>
    public static void Write(ErfFile erf, Stream stream, Dictionary<(string ResRef, ushort Type), byte[]> resourceData)
    {
        // Calculate sizes
        var localizedStringData = BuildLocalizedStrings(erf.LocalizedStrings);
        var keyListSize = erf.Resources.Count * KeyEntrySize;
        var resourceListSize = erf.Resources.Count * ResourceEntrySize;

        // Calculate offsets
        var offsetToLocalizedString = (uint)HeaderSize;
        var offsetToKeyList = offsetToLocalizedString + (uint)localizedStringData.Length;
        var offsetToResourceList = offsetToKeyList + (uint)keyListSize;
        var offsetToResourceData = offsetToResourceList + (uint)resourceListSize;

        // Build header
        var header = new byte[HeaderSize];
        Encoding.ASCII.GetBytes(erf.FileType.PadRight(4)[..4], 0, 4, header, 0);
        Encoding.ASCII.GetBytes(erf.FileVersion.PadRight(4)[..4], 0, 4, header, 4);
        BitConverter.GetBytes((uint)erf.LocalizedStrings.Count).CopyTo(header, 8);
        BitConverter.GetBytes((uint)localizedStringData.Length).CopyTo(header, 12);
        BitConverter.GetBytes((uint)erf.Resources.Count).CopyTo(header, 16);
        BitConverter.GetBytes(offsetToLocalizedString).CopyTo(header, 20);
        BitConverter.GetBytes(offsetToKeyList).CopyTo(header, 24);
        BitConverter.GetBytes(offsetToResourceList).CopyTo(header, 28);
        BitConverter.GetBytes(erf.BuildYear).CopyTo(header, 32);
        BitConverter.GetBytes(erf.BuildDay).CopyTo(header, 36);
        BitConverter.GetBytes(erf.DescriptionStrRef).CopyTo(header, 40);
        // Bytes 44-159: Reserved (116 bytes, zeroed)

        // Write header
        stream.Write(header, 0, HeaderSize);

        // Write localized strings
        stream.Write(localizedStringData, 0, localizedStringData.Length);

        // Build key list and resource list together, calculating data offsets
        var keyList = new byte[keyListSize];
        var resourceList = new byte[resourceListSize];
        var currentDataOffset = offsetToResourceData;

        for (int i = 0; i < erf.Resources.Count; i++)
        {
            var entry = erf.Resources[i];
            var keyOffset = i * KeyEntrySize;
            var resOffset = i * ResourceEntrySize;

            // Get resource data
            byte[] data;
            if (resourceData.TryGetValue((entry.ResRef.ToLowerInvariant(), entry.ResourceType), out var found))
            {
                data = found;
            }
            else
            {
                // No data provided for this resource - write empty
                data = Array.Empty<byte>();
            }

            // Write key entry
            // ResRef must be null-padded, not space-padded (Aurora Engine format)
            var resRefBytes = new byte[16];
            var nameBytes = Encoding.ASCII.GetBytes(entry.ResRef);
            var copyLen = Math.Min(nameBytes.Length, 16);
            Array.Copy(nameBytes, 0, resRefBytes, 0, copyLen);
            // Remaining bytes are already 0 (null) from byte[] initialization
            Array.Copy(resRefBytes, 0, keyList, keyOffset, 16);
            BitConverter.GetBytes((uint)i).CopyTo(keyList, keyOffset + 16); // ResId = index
            BitConverter.GetBytes(entry.ResourceType).CopyTo(keyList, keyOffset + 20);
            // Bytes 22-23: Unused (zeroed)

            // Write resource list entry
            BitConverter.GetBytes(currentDataOffset).CopyTo(resourceList, resOffset);
            BitConverter.GetBytes((uint)data.Length).CopyTo(resourceList, resOffset + 4);

            // Update offset for next resource
            currentDataOffset += (uint)data.Length;
        }

        // Write key list
        stream.Write(keyList, 0, keyListSize);

        // Write resource list
        stream.Write(resourceList, 0, resourceListSize);

        // Write resource data
        for (int i = 0; i < erf.Resources.Count; i++)
        {
            var entry = erf.Resources[i];
            if (resourceData.TryGetValue((entry.ResRef.ToLowerInvariant(), entry.ResourceType), out var data))
            {
                stream.Write(data, 0, data.Length);
            }
        }
    }

    /// <summary>
    /// Build the localized string data block.
    /// </summary>
    private static byte[] BuildLocalizedStrings(List<ErfLocalizedString> strings)
    {
        if (strings.Count == 0)
            return Array.Empty<byte>();

        using var ms = new MemoryStream();
        foreach (var str in strings)
        {
            var textBytes = Encoding.UTF8.GetBytes(str.Text);
            ms.Write(BitConverter.GetBytes(str.LanguageId), 0, 4);
            ms.Write(BitConverter.GetBytes((uint)textBytes.Length), 0, 4);
            ms.Write(textBytes, 0, textBytes.Length);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Update a single resource in an existing ERF file.
    /// Creates a backup of the original file, then rewrites the entire ERF with the updated resource.
    /// </summary>
    /// <param name="erfPath">Path to the ERF file to update.</param>
    /// <param name="resRef">ResRef of the resource to update.</param>
    /// <param name="resourceType">Type of the resource to update.</param>
    /// <param name="newData">New data for the resource.</param>
    /// <param name="createBackup">If true, create a timestamped backup before modifying.</param>
    /// <returns>Path to the backup file if created, null otherwise.</returns>
    public static string? UpdateResource(string erfPath, string resRef, ushort resourceType, byte[] newData, bool createBackup = true)
    {
        string? backupPath = null;

        // Create backup if requested
        if (createBackup)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var directory = Path.GetDirectoryName(erfPath) ?? ".";
            var fileName = Path.GetFileNameWithoutExtension(erfPath);
            var extension = Path.GetExtension(erfPath);
            backupPath = Path.Combine(directory, $"{fileName}_backup_{timestamp}{extension}");
            File.Copy(erfPath, backupPath, overwrite: false);
        }

        // Read existing ERF
        var erf = ErfReader.Read(erfPath);

        // Build resource data dictionary from existing file
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        foreach (var entry in erf.Resources)
        {
            var key = (entry.ResRef.ToLowerInvariant(), entry.ResourceType);
            if (key == (resRef.ToLowerInvariant(), resourceType))
            {
                // Use new data for the updated resource
                resourceData[key] = newData;
            }
            else
            {
                // Extract existing data
                resourceData[key] = ErfReader.ExtractResource(erfPath, entry);
            }
        }

        // Check if resource exists; if not, add it
        var targetKey = (resRef.ToLowerInvariant(), resourceType);
        if (!resourceData.ContainsKey(targetKey))
        {
            resourceData[targetKey] = newData;
            erf.Resources.Add(new ErfResourceEntry
            {
                ResRef = resRef,
                ResourceType = resourceType,
                ResId = (uint)erf.Resources.Count
            });
        }

        // Write updated ERF to temp file first
        var tempPath = erfPath + ".tmp";
        try
        {
            Write(erf, tempPath, resourceData);

            // Replace original with temp
            File.Delete(erfPath);
            File.Move(tempPath, erfPath);
        }
        finally
        {
            // Clean up temp file if it still exists
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        return backupPath;
    }
}
