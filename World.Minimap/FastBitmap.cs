/*
    The MIT License (MIT)

    Copyright (c) 2014 Luiz Fernando Silva

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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

            // Declare an array to hold the bytes of the bitmap
            int bytes = Math.Abs(_bitmapData.Stride) * _bitmap.Height;
            int[] argbValues = new int[bytes / BytesPerPixel];

            // Copy the RGB values into the array
            Marshal.Copy(_bitmapData.Scan0, argbValues, 0, bytes / BytesPerPixel);

            if (unlockAfter) {
                Unlock();
            }

            return argbValues;
        }
    }

    public FastBitmap(Bitmap bitmap)
    {
        if (Image.GetPixelFormatSize(bitmap.PixelFormat) != 32) {
            throw new ArgumentException("The provided bitmap must have a 32bpp depth", "bitmap");
        }

        _bitmap = bitmap;

        _width = bitmap.Width;
        _height = bitmap.Height;
    }

    public void Dispose()
    {
        if (_locked) {
            Unlock();
        }
    }

    public FastBitmapLocker Lock()
    {
        if (_locked) {
            throw new InvalidOperationException("Unlock must be called before a Lock operation");
        }

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
        if (!_locked) {
            throw new InvalidOperationException("Lock must be called before an Unlock operation");
        }

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
        if (!_locked) {
            throw new InvalidOperationException("The FastBitmap must be locked before any pixel operations are made");
        }

        if (x < 0 || x >= _width) {
            throw new ArgumentOutOfRangeException("The X component must be >= 0 and < width");
        }
        if (y < 0 || y >= _height) {
            throw new ArgumentOutOfRangeException("The Y component must be >= 0 and < height");
        }

        *(uint*)(_scan0 + x + y * _strideWidth) = color;
    }

    public Color GetPixel(int x, int y)
    {
        return Color.FromArgb(GetPixelInt(x, y));
    }

    public int GetPixelInt(int x, int y)
    {
        if (!_locked) {
            throw new InvalidOperationException("The FastBitmap must be locked before any pixel operations are made");
        }

        if (x < 0 || x >= _width) {
            throw new ArgumentOutOfRangeException("The X component must be >= 0 and < width");
        }
        if (y < 0 || y >= _height) {
            throw new ArgumentOutOfRangeException("The Y component must be >= 0 and < height");
        }

        return *(_scan0 + x + y * _strideWidth);
    }

    public uint GetPixelUInt(int x, int y)
    {
        if (!_locked) {
            throw new InvalidOperationException("The FastBitmap must be locked before any pixel operations are made");
        }

        if (x < 0 || x >= _width) {
            throw new ArgumentOutOfRangeException("The X component must be >= 0 and < width");
        }
        if (y < 0 || y >= _height) {
            throw new ArgumentOutOfRangeException("The Y component must be >= 0 and < height");
        }

        return *((uint*)_scan0 + x + y * _strideWidth);
    }

    public void CopyFromArray(int[] colors, bool ignoreZeroes = false)
    {
        if (colors.Length != _width * _height) {
            throw new ArgumentException("The number of colors of the given array mismatch the pixel count of the bitmap", "colors");
        }

        // Simply copy the argb values array
        int* s0t = _scan0;

        fixed (int* source = colors) {
            int* s0s = source;
            int bpp = 1; // Bytes per pixel

            int count = _width * _height * bpp;

            if (!ignoreZeroes) {
                // Unfold the loop
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

                while (rem-- > 0) {
                    *(s0t++) = *(s0s++);
                }
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

        // Clear all the pixels
        int count = _width * _height;
        int* curScan = _scan0;

        // Defines the ammount of assignments that the main while() loop is performing per loop.
        // The value specified here must match the number of assignment statements inside that loop
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
        while (rem-- > 0) {
            *(curScan++) = color;
        }

        if (unlockAfter) {
            Unlock();
        }
    }

    public void CopyRegion(Bitmap source, Rectangle srcRect, Rectangle destRect)
    {
        // Throw exception when trying to copy same bitmap over
        if (source == _bitmap) {
            throw new ArgumentException("Copying regions across the same bitmap is not supported", "source");
        }

        Rectangle srcBitmapRect = new Rectangle(0, 0, source.Width, source.Height);
        Rectangle destBitmapRect = new Rectangle(0, 0, _width, _height);

        // Check if the rectangle configuration doesn't generate invalid states or does not affect the target image
        if (srcRect.Width <= 0 || srcRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0 ||
            !srcBitmapRect.IntersectsWith(srcRect) || !destRect.IntersectsWith(destBitmapRect))
            return;

        // Find the areas of the first and second bitmaps that are going to be affected
        srcBitmapRect = Rectangle.Intersect(srcRect, srcBitmapRect);

        // Clip the source rectangle on top of the destination rectangle in a way that clips out the regions of the original bitmap
        // that will not be drawn on the destination bitmap for being out of bounds
        srcBitmapRect = Rectangle.Intersect(srcBitmapRect, new Rectangle(srcRect.X, srcRect.Y, destRect.Width, destRect.Height));

        destBitmapRect = Rectangle.Intersect(destRect, destBitmapRect);

        // Clipt the source bitmap region yet again here
        srcBitmapRect = Rectangle.Intersect(srcBitmapRect, new Rectangle(-destRect.X + srcRect.X, -destRect.Y + srcRect.Y, _width, _height));

        // Calculate the rectangle containing the maximum possible area that is supposed to be affected by the copy region operation
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
    public static extern IntPtr memcpy(IntPtr dest, IntPtr src, ulong count);

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