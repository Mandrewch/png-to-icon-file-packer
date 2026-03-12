using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace IconFileFiller;

/// <summary>
/// Writes images into the Windows ICO file format.
/// Small entries (&lt;=48px) are stored as BMP/DIB for maximum compatibility.
/// The 256px entry is stored as a compressed PNG (standard Windows practice).
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

        var entries = new List<(int Width, int Height, byte[] Data)>();
        foreach (int size in StandardSizes)
        {
            bool usePng = size >= 256;
            byte[] data = ResizeAndEncode(sourceBitmap, size, size, usePng);
            entries.Add((size, size, data));
        }

        WriteIcoFile(icoOutputPath, entries);
    }

    /// <summary>
    /// Creates a single ICO file from multiple source PNGs. Each PNG becomes an entry
    /// in the ICO at its original dimensions (resized if larger than 256px).
    /// </summary>
    public static void CreateIcoFromMultiple(IList<string> pngPaths, string icoOutputPath)
    {
        var entries = new List<(int Width, int Height, byte[] Data)>();

        foreach (string pngPath in pngPaths)
        {
            using var sourceBitmap = new Bitmap(pngPath);

            int width = Math.Min(sourceBitmap.Width, 256);
            int height = Math.Min(sourceBitmap.Height, 256);

            bool usePng = width >= 256;
            byte[] data = ResizeAndEncode(sourceBitmap, width, height, usePng);
            entries.Add((width, height, data));
        }

        WriteIcoFile(icoOutputPath, entries);
    }

    private static byte[] ResizeAndEncode(Bitmap source, int width, int height, bool usePng)
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

        if (usePng)
        {
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        else
        {
            // BMP/DIB format for ICO: BITMAPINFOHEADER + raw 32bpp BGRA pixels (bottom-up).
            // No BITMAPFILEHEADER — ICO stores raw DIB data.
            // Height in header is doubled (ICO spec: height covers XOR + AND mask planes).
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];

            var bmpData = resized.LockBits(
                new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                // Copy rows bottom-up (BMP is stored upside-down)
                for (int row = 0; row < height; row++)
                {
                    IntPtr src = bmpData.Scan0 + (height - 1 - row) * bmpData.Stride;
                    System.Runtime.InteropServices.Marshal.Copy(src, pixels, row * stride, stride);
                }
            }
            finally
            {
                resized.UnlockBits(bmpData);
            }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // BITMAPINFOHEADER (40 bytes)
            bw.Write(40);           // biSize
            bw.Write(width);        // biWidth
            bw.Write(height * 2);   // biHeight (doubled per ICO spec)
            bw.Write((short)1);     // biPlanes
            bw.Write((short)32);    // biBitCount
            bw.Write(0);            // biCompression (BI_RGB)
            bw.Write(pixels.Length);// biSizeImage
            bw.Write(0);            // biXPelsPerMeter
            bw.Write(0);            // biYPelsPerMeter
            bw.Write(0);            // biClrUsed
            bw.Write(0);            // biClrImportant

            // Pixel data (BGRA, bottom-up already arranged above)
            bw.Write(pixels);

            // AND mask (1-bit, all zeros = fully opaque via alpha channel)
            int maskRowStride = ((width + 31) / 32) * 4;
            bw.Write(new byte[maskRowStride * height]);

            return ms.ToArray();
        }
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
