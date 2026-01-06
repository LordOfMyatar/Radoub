// MDL Binary Reader - Utility methods
// Partial class for reading helpers

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    private static string ReadFixedString(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        var nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
            return Encoding.ASCII.GetString(bytes, 0, nullIndex);
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }
}
