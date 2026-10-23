using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GameHub.Core.Icons;

public static class IconExtractor
{
    public const int SharpIconMinWidth = 128;

    /// <summary>Pixel width of a PNG on disk without keeping the file locked; 0 if missing/unreadable.</summary>
    public static int GetImageWidth(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;

        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            return image.Width;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Replaces a missing or low-resolution cached icon with a sharper one extracted from the exe.
    /// Never overwrites with something smaller. Returns true only if the file on disk was improved.
    /// </summary>
    /// <param name="force">Regenerate even if the current icon is already large (used to replace icons from a buggy earlier build).</param>
    public static bool TryUpgradeIcon(string exePath, string iconPngPath, bool force = false)
    {
        var currentWidth = GetImageWidth(iconPngPath);
        if (!force && currentWidth >= SharpIconMinWidth) return false;
        if (!File.Exists(exePath)) return false;

        var temp = iconPngPath + ".new";
        try
        {
            if (!TryExtractIcon(exePath, temp)) return false;
            if (!force && GetImageWidth(temp) <= currentWidth) return false;

            File.Move(temp, iconPngPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    /// <summary>
    /// Extracts the exe's icon as a PNG, preferring the 256px "jumbo" icon Explorer shows
    /// and falling back to the small associated icon. Returns false (without throwing) on failure.
    /// </summary>
    public static bool TryExtractIcon(string exePath, string outputPngPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPngPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var bitmap = TryGetJumboIcon(exePath) ?? TryGetAssociatedIconBitmap(exePath);
            if (bitmap is null) return false;

            bitmap.Save(outputPngPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Bitmap? TryGetAssociatedIconBitmap(string exePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            return icon?.ToBitmap();
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryGetJumboIcon(string exePath)
    {
        var hbitmap = IntPtr.Zero;
        object? comFactory = null;
        try
        {
            var riid = typeof(IShellItemImageFactory).GUID;
            var hr = SHCreateItemFromParsingName(exePath, IntPtr.Zero, ref riid, out IShellItemImageFactory factory);
            comFactory = factory;
            if (hr != 0 || factory is null) return null;

            var size = new SIZE { cx = 256, cy = 256 };
            hr = factory.GetImage(size, SIIGBF.ICONONLY, out hbitmap);
            if (hr != 0 || hbitmap == IntPtr.Zero) return null;

            using var temp = Image.FromHbitmap(hbitmap);
            return CopyWithAlpha(hbitmap, temp);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap);
            if (comFactory is not null) Marshal.ReleaseComObject(comFactory);
        }
    }

    // Image.FromHbitmap drops the alpha channel, so read the 32-bit pixels ourselves.
    // GetDIBits with a negative height always yields top-down rows, whatever the source orientation.
    private static Bitmap CopyWithAlpha(IntPtr hbitmap, Image fallback)
    {
        if (GetObject(hbitmap, Marshal.SizeOf<BITMAP>(), out var bmp) == 0 || bmp.bmWidth <= 0 || bmp.bmHeight <= 0)
            return new Bitmap(fallback);

        var width = bmp.bmWidth;
        var height = bmp.bmHeight;
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32
        };

        var pixels = new byte[width * height * 4];
        var hdc = GetDC(IntPtr.Zero);
        int scanLines;
        try
        {
            scanLines = GetDIBits(hdc, hbitmap, 0, (uint)height, pixels, ref header, 0);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }

        if (scanLines == 0)
            return new Bitmap(fallback);

        // Some bitmaps carry no alpha at all (all zero); treat those as fully opaque.
        var hasAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0) { hasAlpha = true; break; }
        }

        if (!hasAlpha)
        {
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        }

        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
                Marshal.Copy(pixels, y * width * 4, data.Scan0 + y * data.Stride, width * 4);
        }
        finally
        {
            result.UnlockBits(data);
        }

        return result;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines, byte[]? bits,
        ref BITMAPINFOHEADER bmi, uint usage);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public short bmPlanes;
        public short bmBitsPixel;
        public IntPtr bmBits;
    }

    [Flags]
    private enum SIIGBF
    {
        RESIZETOFIT = 0x00,
        BIGGERSIZEOK = 0x01,
        MEMORYONLY = 0x02,
        ICONONLY = 0x04,
    }
}
