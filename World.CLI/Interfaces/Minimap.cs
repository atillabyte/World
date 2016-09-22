using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

public static class Minimap
{
    public static Bitmap Create(World world)
    {
        var canvas = new Bitmap(world.Width, world.Height);
        var bitmap = new FastBitmap(canvas);

        bitmap.Lock();
        bitmap.Clear(world.BackgroundColor);

        foreach (var block in world.Blocks.Where(block => colors[block.Type] != 0u).OrderByDescending(x => x.Layer >= 1))
            foreach (var location in block.Locations)
                bitmap.SetPixel(location.X, location.Y, colors[block.Type]);

        bitmap.Unlock();
        return canvas;
    }

    static Dictionary<uint, uint> colors = new WebClient() { Proxy = null }.DownloadString("https://raw.githubusercontent.com/EEJesse/EEBlocks/master/Colors.txt").Split('\n')
            .Where(x => !string.IsNullOrEmpty(x)).ToDictionary(x => uint.Parse(x.Split(' ')[0]), x => uint.Parse(x.Split(' ')[1]));
}

/// <!-- FastBitmap | The MIT License (MIT) | (c) Luiz Fernando Silva -->
public unsafe class FastBitmap : IDisposable
{
    private const int BytesPerPixel = 4;
    private readonly Bitmap _bitmap;
    private BitmapData _bitmapData;
    private int _strideWidth;
    private int* _scan0;
    private bool _locked;
    private readonly int _width;
    private readonly int _height;
    public int Width { get { return _width; } }
    public int Height { get { return _height; } }
    public IntPtr Scan0 { get { return _bitmapData.Scan0; } }
    public int Stride { get { return _strideWidth; } }
    public bool Locked { get { return _locked; } }

    public int[] DataArray
    {
        get {
            bool unlockAfter = false;
            if (!_locked) {
                Lock();
                unlockAfter = true;
            }

            int bytes = Math.Abs(_bitmapData.Stride) * _bitmap.Height;
            int[] argbValues = new int[bytes / BytesPerPixel];

            Marshal.Copy(_bitmapData.Scan0, argbValues, 0, bytes / BytesPerPixel);

            if (unlockAfter)
                Unlock();

            return argbValues;
        }
    }

    public FastBitmap(Bitmap bitmap)
    {
        if (Image.GetPixelFormatSize(bitmap.PixelFormat) != 32)
            throw new ArgumentException("The provided bitmap must have a 32bpp depth", "bitmap");

        _bitmap = bitmap;

        _width = bitmap.Width;
        _height = bitmap.Height;
    }

    public void Dispose()
    {
        if (_locked)
            Unlock();
    }

    public FastBitmapLocker Lock()
    {
        if (_locked)
            throw new InvalidOperationException("Unlock must be called before a Lock operation");

        return Lock(ImageLockMode.ReadWrite);
    }

    private FastBitmapLocker Lock(ImageLockMode lockMode)
    {
        Rectangle rect = new Rectangle(0, 0, _bitmap.Width, _bitmap.Height);

        return Lock(lockMode, rect);
    }

    private FastBitmapLocker Lock(ImageLockMode lockMode, Rectangle rect)
    {
        // Lock the bitmap's bits
        _bitmapData = _bitmap.LockBits(rect, lockMode, _bitmap.PixelFormat);

        _scan0 = (int*)_bitmapData.Scan0;
        _strideWidth = _bitmapData.Stride / BytesPerPixel;

        _locked = true;

        return new FastBitmapLocker(this);
    }

    public void Unlock()
    {
        if (!_locked)
            throw new InvalidOperationException("Lock must be called before an Unlock operation");

        _bitmap.UnlockBits(_bitmapData);
        _locked = false;
    }

    public void SetPixel(int x, int y, Color color)
    {
        SetPixel(x, y, color.ToArgb());
    }

    public void SetPixel(int x, int y, int color)
    {
        SetPixel(x, y, (uint)color);
    }

    public void SetPixel(int x, int y, uint color)
    {
        if (!_locked)
            throw new InvalidOperationException("The FastBitmap must be locked before any pixel operations are made");

        if (x < 0 || x >= _width)
            throw new ArgumentOutOfRangeException("The X component must be >= 0 and < width");
        if (y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException("The Y component must be >= 0 and < height");

        *(uint*)(_scan0 + x + y * _strideWidth) = color;
    }

    public Color GetPixel(int x, int y)
    {
        return Color.FromArgb(GetPixelInt(x, y));
    }

    public int GetPixelInt(int x, int y)
    {
        if (!_locked)
            throw new InvalidOperationException("The FastBitmap must be locked before any pixel operations are made");

        if (x < 0 || x >= _width)
            throw new ArgumentOutOfRangeException("The X component must be >= 0 and < width");
        if (y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException("The Y component must be >= 0 and < height");

        return *(_scan0 + x + y * _strideWidth);
    }

    public uint GetPixelUInt(int x, int y)
    {
        if (!_locked)
            throw new InvalidOperationException("The FastBitmap must be locked before any pixel operations are made");

        if (x < 0 || x >= _width)
            throw new ArgumentOutOfRangeException("The X component must be >= 0 and < width");
        if (y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException("The Y component must be >= 0 and < height");

        return *((uint*)_scan0 + x + y * _strideWidth);
    }

    public void CopyFromArray(int[] colors, bool ignoreZeroes = false)
    {
        if (colors.Length != _width * _height)
            throw new ArgumentException("The number of colors of the given array mismatch the pixel count of the bitmap", "colors");

        int* s0t = _scan0;

        fixed (int* source = colors) {
            int* s0s = source;
            int bpp = 1;

            int count = _width * _height * bpp;

            if (!ignoreZeroes) {
                const int sizeBlock = 8;
                int rem = count % sizeBlock;

                count /= sizeBlock;

                while (count-- > 0) {
                    *(s0t++) = *(s0s++);
                    *(s0t++) = *(s0s++);
                    *(s0t++) = *(s0s++);
                    *(s0t++) = *(s0s++);

                    *(s0t++) = *(s0s++);
                    *(s0t++) = *(s0s++);
                    *(s0t++) = *(s0s++);
                    *(s0t++) = *(s0s++);
                }

                while (rem-- > 0)
                    *(s0t++) = *(s0s++);
            } else {
                while (count-- > 0) {
                    if (*(s0s) == 0) { s0t++; s0s++; continue; }
                    *(s0t++) = *(s0s++);
                }
            }
        }
    }

    public void Clear(Color color)
    {
        Clear(color.ToArgb());
    }

    public void Clear(int color)
    {
        bool unlockAfter = false;
        if (!_locked) {
            Lock();
            unlockAfter = true;
        }

        int count = _width * _height;
        int* curScan = _scan0;

        const int assignsPerLoop = 8;

        int rem = count % assignsPerLoop;
        count /= assignsPerLoop;

        while (count-- > 0) {
            *(curScan++) = color;
            *(curScan++) = color;
            *(curScan++) = color;
            *(curScan++) = color;

            *(curScan++) = color;
            *(curScan++) = color;
            *(curScan++) = color;
            *(curScan++) = color;
        }

        while (rem-- > 0)
            *(curScan++) = color;

        if (unlockAfter)
            Unlock();
    }

    public void CopyRegion(Bitmap source, Rectangle srcRect, Rectangle destRect)
    {
        if (source == _bitmap)
            throw new ArgumentException("Copying regions across the same bitmap is not supported", "source");

        Rectangle srcBitmapRect = new Rectangle(0, 0, source.Width, source.Height);
        Rectangle destBitmapRect = new Rectangle(0, 0, _width, _height);

        if (srcRect.Width <= 0 || srcRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0 ||
            !srcBitmapRect.IntersectsWith(srcRect) || !destRect.IntersectsWith(destBitmapRect))
            return;

        srcBitmapRect = Rectangle.Intersect(srcRect, srcBitmapRect);
        srcBitmapRect = Rectangle.Intersect(srcBitmapRect, new Rectangle(srcRect.X, srcRect.Y, destRect.Width, destRect.Height));
        destBitmapRect = Rectangle.Intersect(destRect, destBitmapRect);

        srcBitmapRect = Rectangle.Intersect(srcBitmapRect, new Rectangle(-destRect.X + srcRect.X, -destRect.Y + srcRect.Y, _width, _height));

        int copyWidth = Math.Min(srcBitmapRect.Width, destBitmapRect.Width);
        int copyHeight = Math.Min(srcBitmapRect.Height, destBitmapRect.Height);

        if (copyWidth == 0 || copyHeight == 0)
            return;

        int srcStartX = srcBitmapRect.Left;
        int srcStartY = srcBitmapRect.Top;

        int destStartX = destBitmapRect.Left;
        int destStartY = destBitmapRect.Top;

        using (FastBitmap fastSource = source.FastLock()) {
            ulong strideWidth = (ulong)copyWidth * BytesPerPixel;

            for (int y = 0; y < copyHeight; y++) {
                int destX = destStartX;
                int destY = destStartY + y;

                int srcX = srcStartX;
                int srcY = srcStartY + y;

                long offsetSrc = (srcX + srcY * fastSource._strideWidth);
                long offsetDest = (destX + destY * _strideWidth);

                memcpy(_scan0 + offsetDest, fastSource._scan0 + offsetSrc, strideWidth);
            }
        }
    }

    public static bool CopyPixels(Bitmap source, Bitmap target)
    {
        if (source.Width != target.Width || source.Height != target.Height || source.PixelFormat != target.PixelFormat)
            return false;

        using (FastBitmap fastSource = source.FastLock(),
                          fastTarget = target.FastLock()) {
            memcpy(fastTarget.Scan0, fastSource.Scan0, (ulong)(fastSource.Height * fastSource._strideWidth * BytesPerPixel));
        }

        return true;
    }

    public static void ClearBitmap(Bitmap bitmap, Color color)
    {
        ClearBitmap(bitmap, color.ToArgb());
    }

    public static void ClearBitmap(Bitmap bitmap, int color)
    {
        using (var fb = bitmap.FastLock()) {
            fb.Clear(color);
        }
    }

    public static void CopyRegion(Bitmap source, Bitmap target, Rectangle srcRect, Rectangle destRect)
    {
        FastBitmap fastTarget = new FastBitmap(target);

        using (fastTarget.Lock()) {
            fastTarget.CopyRegion(source, srcRect, destRect);
        }
    }

    // .NET wrapper to native call of 'memcpy'. Requires Microsoft Visual C++ Runtime installed
    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern IntPtr memcpy(IntPtr dest, IntPtr src, ulong count);

    // .NET wrapper to native call of 'memcpy'. Requires Microsoft Visual C++ Runtime installed
    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    public static extern IntPtr memcpy(void* dest, void* src, ulong count);

    public struct FastBitmapLocker : IDisposable
    {
        private readonly FastBitmap _fastBitmap;

        public FastBitmap FastBitmap
        {
            get { return _fastBitmap; }
        }

        public FastBitmapLocker(FastBitmap fastBitmap)
        {
            _fastBitmap = fastBitmap;
        }

        public void Dispose()
        {
            if (_fastBitmap._locked)
                _fastBitmap.Unlock();
        }
    }
}
public static class FastBitmapExtensions
{
    public static FastBitmap FastLock(this Bitmap bitmap)
    {
        FastBitmap fast = new FastBitmap(bitmap);
        fast.Lock();

        return fast;
    }
}