using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace IconFileFiller;

/// <summary>
/// Writes PNG images into the Windows ICO file format.
/// ICO format: 6-byte header, 16-byte directory entry per image, then raw PNG blobs.
/// </summary>
public static class IcoWriter
{
    private static readonly int[] StandardSizes = [16, 32, 48, 256];

    /// <summary>
    /// Creates an ICO file from a single source PNG, auto-generating 16, 32, 48, and 256 pixel entries.
    /// </summary>
    public static void CreateIco(string pngPath, string icoOutputPath)
    {
        using var sourceBitmap = new Bitmap(pngPath);

        var pngEntries = new List<(int Width, int Height, byte[] Data)>();
        foreach (int size in StandardSizes)
        {
            byte[] pngBytes = ResizeAndEncode(sourceBitmap, size, size);
            pngEntries.Add((size, size, pngBytes));
        }

        WriteIcoFile(icoOutputPath, pngEntries);
    }

    /// <summary>
    /// Creates a single ICO file from multiple source PNGs. Each PNG becomes an entry
    /// in the ICO at its original dimensions (resized if larger than 256px).
    /// </summary>
    public static void CreateIcoFromMultiple(IList<string> pngPaths, string icoOutputPath)
    {
        var pngEntries = new List<(int Width, int Height, byte[] Data)>();

        foreach (string pngPath in pngPaths)
        {
            using var sourceBitmap = new Bitmap(pngPath);

            int width = Math.Min(sourceBitmap.Width, 256);
            int height = Math.Min(sourceBitmap.Height, 256);

            byte[] pngBytes = ResizeAndEncode(sourceBitmap, width, height);
            pngEntries.Add((width, height, pngBytes));
        }

        WriteIcoFile(icoOutputPath, pngEntries);
    }

    private static byte[] ResizeAndEncode(Bitmap source, int width, int height)
    {
        using var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, 0, 0, width, height);
        }

        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static void WriteIcoFile(string outputPath, List<(int Width, int Height, byte[] Data)> entries)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        int imageCount = entries.Count;

        // === ICO Header (6 bytes) ===
        writer.Write((short)0);          // Reserved, must be 0
        writer.Write((short)1);          // Type: 1 = ICO
        writer.Write((short)imageCount); // Number of images

        // === Directory Entries (16 bytes each) ===
        int dataOffset = 6 + (16 * imageCount);

        foreach (var (width, height, data) in entries)
        {
            // Width/Height: 0 means 256 in the ICO spec
            writer.Write((byte)(width < 256 ? width : 0));
            writer.Write((byte)(height < 256 ? height : 0));
            writer.Write((byte)0);   // Color palette count (0 = no palette)
            writer.Write((byte)0);   // Reserved
            writer.Write((short)1);  // Color planes
            writer.Write((short)32); // Bits per pixel
            writer.Write(data.Length);     // Size of image data in bytes
            writer.Write(dataOffset);      // Offset from beginning of file

            dataOffset += data.Length;
        }

        // === Image Data (raw PNG blobs) ===
        foreach (var (_, _, data) in entries)
        {
            writer.Write(data);
        }
    }
}
