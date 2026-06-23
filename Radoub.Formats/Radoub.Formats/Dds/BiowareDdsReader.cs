using Radoub.Formats.Services;

namespace Radoub.Formats.Dds;

/// <summary>
/// Decodes NWN:EE's BioWare-format DDS textures.
///
/// Unlike Microsoft DDS (which begins with the ASCII magic "DDS " = 0x20534444 and is handled
/// by Pfim), BioWare DDS has no magic. It starts with a 20-byte header
/// { u32 width, u32 height, u32 colors, u32 reserved[2] } where colors is 3 (DXT1) or 4 (DXT5),
/// followed by raw DXT1/DXT5 blocks (no mip chain is required for decode — we read level 0 only).
///
/// Layout and block-decode logic ported from the reference implementations (rollnw Image.cpp,
/// which uses the public-domain stb DDS block decoder). This is why our high-res EE creature
/// textures (e.g. a 512x512 Drow Matron) load instead of falling back to the legacy low-res TGA.
/// </summary>
public static class BiowareDdsReader
{
    /// <summary>Microsoft DDS magic ("DDS " little-endian). BioWare DDS does NOT have this.</summary>
    private const uint MicrosoftDdsMagic = 0x20534444;

    private const int HeaderSize = 20;

    /// <summary>Largest texture dimension we will allocate for (guards corrupt/hostile headers).</summary>
    private const uint MaxDimension = 4096;

    /// <summary>
    /// True if the buffer looks like a BioWare DDS (i.e. NOT a standard Microsoft DDS).
    /// Used to route between this decoder and Pfim.
    /// </summary>
    public static bool IsBiowareDds(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;
        uint magic = BitConverter.ToUInt32(data, 0);
        return magic != MicrosoftDdsMagic;
    }

    /// <summary>
    /// Decode a BioWare DDS to RGBA. Returns null if the data is not a valid/complete BioWare DDS
    /// (including standard Microsoft DDS, truncated data, or an unsupported channel count).
    /// </summary>
    public static ImageData? Read(byte[] data)
    {
        if (data == null || data.Length < HeaderSize)
            return null;
        if (!IsBiowareDds(data))
            return null;

        uint width = BitConverter.ToUInt32(data, 0);
        uint height = BitConverter.ToUInt32(data, 4);
        uint colors = BitConverter.ToUInt32(data, 8);

        if (colors != 3 && colors != 4)
            return null;
        if (width == 0 || height == 0)
            return null;
        // Reject absurd dimensions from a corrupt/hostile header before allocating, matching the
        // 4096 cap on the parallel ConvertBiowareDdsToStandard path. NWN textures are <= 2048.
        if (width > MaxDimension || height > MaxDimension)
            return null;

        int blockPitch = ((int)width + 3) >> 2;
        int blockRows = ((int)height + 3) >> 2;
        long numBlocks = (long)blockPitch * blockRows;
        int bytesPerBlock = colors == 4 ? 16 : 8;
        long expectedSize = HeaderSize + numBlocks * bytesPerBlock;
        if (data.Length < expectedSize)
            return null;

        var rgba = new byte[(long)width * height * 4];
        int off = HeaderSize;
        var block = new byte[16 * 4];
        var compressed = new byte[8];

        for (long i = 0; i < numBlocks; i++)
        {
            int refX = 4 * (int)(i % blockPitch);
            int refY = 4 * (int)(i / blockPitch);

            if (colors == 4)
            {
                Array.Copy(data, off, compressed, 0, 8);
                DecodeDxt45AlphaBlock(block, compressed);
                off += 8;
                Array.Copy(data, off, compressed, 0, 8);
                DecodeDxtColorBlock(block, compressed);
                off += 8;
            }
            else
            {
                Array.Copy(data, off, compressed, 0, 8);
                DecodeDxt1Block(block, compressed);
                off += 8;
            }

            int bw = refX + 4 > (int)width ? (int)width - refX : 4;
            int bh = refY + 4 > (int)height ? (int)height - refY : 4;
            for (int by = 0; by < bh; by++)
            {
                int dst = 4 * ((refY + by) * (int)width + refX);
                int src = 4 * (by * 4);
                Array.Copy(block, src, rgba, dst, 4 * bw);
            }
        }

        return new ImageData((int)width, (int)height, rgba);
    }

    // ---- Block decoders (ported from the public-domain stb DDS decoder; see rollnw Image.cpp) ----

    private static int ConvertBitRange(int c, int fromBits, int toBits)
    {
        int b = (1 << (fromBits - 1)) + c * ((1 << toBits) - 1);
        return (b + (b >> fromBits)) >> fromBits;
    }

    private static void Rgb888From565(int c, out int r, out int g, out int b)
    {
        r = ConvertBitRange((c >> 11) & 31, 5, 8);
        g = ConvertBitRange((c >> 5) & 63, 6, 8);
        b = ConvertBitRange(c & 31, 5, 8);
    }

    private static void DecodeDxt1Block(byte[] uncompressed, byte[] compressed)
    {
        int nextBit = 4 * 8;
        var dc = new byte[4 * 4];
        int c0 = compressed[0] + (compressed[1] << 8);
        int c1 = compressed[2] + (compressed[3] << 8);
        Rgb888From565(c0, out int r, out int g, out int b);
        dc[0] = (byte)r; dc[1] = (byte)g; dc[2] = (byte)b; dc[3] = 255;
        Rgb888From565(c1, out r, out g, out b);
        dc[4] = (byte)r; dc[5] = (byte)g; dc[6] = (byte)b; dc[7] = 255;
        if (c0 > c1)
        {
            dc[8] = (byte)((2 * dc[0] + dc[4]) / 3);
            dc[9] = (byte)((2 * dc[1] + dc[5]) / 3);
            dc[10] = (byte)((2 * dc[2] + dc[6]) / 3);
            dc[11] = 255;
            dc[12] = (byte)((dc[0] + 2 * dc[4]) / 3);
            dc[13] = (byte)((dc[1] + 2 * dc[5]) / 3);
            dc[14] = (byte)((dc[2] + 2 * dc[6]) / 3);
            dc[15] = 255;
        }
        else
        {
            dc[8] = (byte)((dc[0] + dc[4]) / 2);
            dc[9] = (byte)((dc[1] + dc[5]) / 2);
            dc[10] = (byte)((dc[2] + dc[6]) / 2);
            dc[11] = 255;
            dc[12] = 0; dc[13] = 0; dc[14] = 0; dc[15] = 0;
        }
        for (int i = 0; i < 16 * 4; i += 4)
        {
            int idx = ((compressed[nextBit >> 3] >> (nextBit & 7)) & 3) * 4;
            nextBit += 2;
            uncompressed[i + 0] = dc[idx + 0];
            uncompressed[i + 1] = dc[idx + 1];
            uncompressed[i + 2] = dc[idx + 2];
            uncompressed[i + 3] = dc[idx + 3];
        }
    }

    private static void DecodeDxt45AlphaBlock(byte[] uncompressed, byte[] compressed)
    {
        int nextBit = 8 * 2;
        var da = new byte[8];
        da[0] = compressed[0];
        da[1] = compressed[1];
        if (da[0] > da[1])
        {
            da[2] = (byte)((6 * da[0] + 1 * da[1]) / 7);
            da[3] = (byte)((5 * da[0] + 2 * da[1]) / 7);
            da[4] = (byte)((4 * da[0] + 3 * da[1]) / 7);
            da[5] = (byte)((3 * da[0] + 4 * da[1]) / 7);
            da[6] = (byte)((2 * da[0] + 5 * da[1]) / 7);
            da[7] = (byte)((1 * da[0] + 6 * da[1]) / 7);
        }
        else
        {
            da[2] = (byte)((4 * da[0] + 1 * da[1]) / 5);
            da[3] = (byte)((3 * da[0] + 2 * da[1]) / 5);
            da[4] = (byte)((2 * da[0] + 3 * da[1]) / 5);
            da[5] = (byte)((1 * da[0] + 4 * da[1]) / 5);
            da[6] = 0;
            da[7] = 255;
        }
        for (int i = 3; i < 16 * 4; i += 4)
        {
            int idx = 0;
            int bit;
            bit = (compressed[nextBit >> 3] >> (nextBit & 7)) & 1; idx += bit << 0; ++nextBit;
            bit = (compressed[nextBit >> 3] >> (nextBit & 7)) & 1; idx += bit << 1; ++nextBit;
            bit = (compressed[nextBit >> 3] >> (nextBit & 7)) & 1; idx += bit << 2; ++nextBit;
            uncompressed[i] = da[idx & 7];
        }
    }

    private static void DecodeDxtColorBlock(byte[] uncompressed, byte[] compressed)
    {
        int nextBit = 4 * 8;
        var dc = new byte[4 * 3];
        int c0 = compressed[0] + (compressed[1] << 8);
        int c1 = compressed[2] + (compressed[3] << 8);
        Rgb888From565(c0, out int r, out int g, out int b);
        dc[0] = (byte)r; dc[1] = (byte)g; dc[2] = (byte)b;
        Rgb888From565(c1, out r, out g, out b);
        dc[3] = (byte)r; dc[4] = (byte)g; dc[5] = (byte)b;
        dc[6] = (byte)((2 * dc[0] + dc[3]) / 3);
        dc[7] = (byte)((2 * dc[1] + dc[4]) / 3);
        dc[8] = (byte)((2 * dc[2] + dc[5]) / 3);
        dc[9] = (byte)((dc[0] + 2 * dc[3]) / 3);
        dc[10] = (byte)((dc[1] + 2 * dc[4]) / 3);
        dc[11] = (byte)((dc[2] + 2 * dc[5]) / 3);
        for (int i = 0; i < 16 * 4; i += 4)
        {
            int idx = ((compressed[nextBit >> 3] >> (nextBit & 7)) & 3) * 3;
            nextBit += 2;
            uncompressed[i + 0] = dc[idx + 0];
            uncompressed[i + 1] = dc[idx + 1];
            uncompressed[i + 2] = dc[idx + 2];
            // alpha (i+3) preserved from the alpha block for DXT5
        }
    }
}
