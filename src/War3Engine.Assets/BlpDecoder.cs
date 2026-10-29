using War3Net.Drawing.Blp;

namespace War3Engine.Assets;

public readonly record struct BlpImage(int Width, int Height, byte[] Rgba);

public readonly record struct BlpImageSize(int Width, int Height);

public static class BlpDecoder
{
    /// <summary>Decodes mip 0 of a BLP file to RGBA8888, top row first.</summary>
    public static BlpImage Decode(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var blp = new BlpFile(stream);
        var pixels = blp.GetPixels(0, out var width, out var height, bgra: false);
        if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
        {
            throw new InvalidDataException($"Invalid BLP dimensions {width}x{height}, {pixels.Length} bytes");
        }

        // JPEG-content BLPs (team colors and some UI art) come back from the shared
        // decoder with BGRA channel order (verified against the WC3 team-color palette),
        // unlike Direct-content BLPs which honor the requested RGBA order.
        if (IsJpegContent(data))
        {
            for (var i = 0; i < width * height * 4; i += 4)
            {
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            }
        }

        return new BlpImage(width, height, pixels);
    }

    /// <summary>Reads only the header (no pixel decoding); used for terrain atlas layout.</summary>
    public static BlpImageSize GetSize(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var blp = new BlpFile(stream);
        return new BlpImageSize(blp.Width, blp.Height);
    }

    private static bool IsJpegContent(byte[] data) =>
        data.Length > 8 &&
        data[0] == 'B' && data[1] == 'L' && data[2] == 'P' &&
        (data[3] == '1' || data[3] == '2') &&
        BitConverter.ToUInt32(data, 4) == 0;
}
