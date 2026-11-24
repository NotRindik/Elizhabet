#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public static class PixelOsClipboard
{
    const uint CfDib = 8;
    const uint CfHdrop = 15;
    const uint CfDibV5 = 17;

    public static bool Supported
    {
        get { return Application.platform == RuntimePlatform.WindowsEditor; }
    }

    public static bool TryGetImage(out Color32[] data, out int w, out int h)
    {
        data = null;
        w = 0;
        h = 0;
        if(!Supported || !OpenClipboard(IntPtr.Zero)) return false;
        byte[] png = null;
        byte[] dib = null;
        string file = null;
        try
        {
            var pngFormat = RegisterClipboardFormat("PNG");
            if(pngFormat != 0 && IsClipboardFormatAvailable(pngFormat)) png = Read(pngFormat);
            if(png == null && IsClipboardFormatAvailable(CfDibV5)) dib = Read(CfDibV5);
            if(png == null && dib == null && IsClipboardFormatAvailable(CfDib)) dib = Read(CfDib);
            if(png == null && dib == null && IsClipboardFormatAvailable(CfHdrop)) file = FirstImageFile();
        }
        finally
        {
            CloseClipboard();
        }
        if(png != null && FromEncoded(png, out data, out w, out h)) return true;
        if(dib != null && FromDib(dib, out data, out w, out h)) return true;
        if(file != null && File.Exists(file))
        {
            try
            {
                return FromEncoded(File.ReadAllBytes(file), out data, out w, out h);
            }
            catch(Exception)
            {
                return false;
            }
        }
        return false;
    }

    static byte[] Read(uint format)
    {
        var handle = GetClipboardData(format);
        if(handle == IntPtr.Zero) return null;
        var size = (long)GlobalSize(handle).ToUInt64();
        if(size <= 0 || size > int.MaxValue) return null;
        var ptr = GlobalLock(handle);
        if(ptr == IntPtr.Zero) return null;
        try
        {
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, (int)size);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    static string FirstImageFile()
    {
        var handle = GetClipboardData(CfHdrop);
        if(handle == IntPtr.Zero) return null;
        var count = DragQueryFile(handle, 0xFFFFFFFF, null, 0);
        for(uint i = 0; i < count; i++)
        {
            var len = DragQueryFile(handle, i, null, 0);
            var sb = new StringBuilder((int)len + 1);
            DragQueryFile(handle, i, sb, len + 1);
            var path = sb.ToString();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if(ext == ".png" || ext == ".jpg" || ext == ".jpeg") return path;
        }
        return null;
    }

    static bool FromEncoded(byte[] bytes, out Color32[] data, out int w, out int h)
    {
        data = null;
        w = 0;
        h = 0;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if(!tex.LoadImage(bytes)) return false;
            w = tex.width;
            h = tex.height;
            data = tex.GetPixels32();
            return w > 0 && h > 0;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    static bool FromDib(byte[] b, out Color32[] data, out int w, out int h)
    {
        data = null;
        w = 0;
        h = 0;
        if(b.Length < 40) return false;
        var headerSize = BitConverter.ToInt32(b, 0);
        var width = BitConverter.ToInt32(b, 4);
        var rawHeight = BitConverter.ToInt32(b, 8);
        var bits = BitConverter.ToInt16(b, 14);
        var compression = BitConverter.ToInt32(b, 16);
        var colorsUsed = BitConverter.ToInt32(b, 32);
        var topDown = rawHeight < 0;
        var height = Math.Abs(rawHeight);
        if(width <= 0 || height <= 0 || (bits != 24 && bits != 32)) return false;
        if(compression != 0 && compression != 3) return false;

        uint rm = 0x00FF0000, gm = 0x0000FF00, bm = 0x000000FF, am = 0;
        var offset = headerSize;
        if(compression == 3)
        {
            if(b.Length < 52) return false;
            rm = U(b, 40);
            gm = U(b, 44);
            bm = U(b, 48);
            if(headerSize >= 56) am = U(b, 52);
            if(headerSize == 40) offset += 12;
        }
        else if(bits == 32)
        {
            am = 0xFF000000;
        }
        offset += colorsUsed * 4;
        var stride = ((width * bits + 31) / 32) * 4;
        if(offset < 0 || offset + (long)stride * height > b.Length) return false;

        var result = new Color32[width * height];
        var anyAlpha = false;
        for(var y = 0; y < height; y++)
        {
            var row = offset + (topDown ? height - 1 - y : y) * stride;
            for(var x = 0; x < width; x++)
            {
                Color32 c;
                if(bits == 24)
                {
                    var p = row + x * 3;
                    c = new Color32(b[p + 2], b[p + 1], b[p], 255);
                }
                else
                {
                    var v = U(b, row + x * 4);
                    var a = am != 0 ? Channel(v, am) : (byte)0;
                    if(a != 0) anyAlpha = true;
                    c = new Color32(Channel(v, rm), Channel(v, gm), Channel(v, bm), a);
                }
                result[y * width + x] = c;
            }
        }
        if(bits == 32 && !anyAlpha)
            for(var i = 0; i < result.Length; i++) result[i].a = 255;

        data = result;
        w = width;
        h = height;
        return true;
    }

    static uint U(byte[] b, int i)
    {
        return BitConverter.ToUInt32(b, i);
    }

    static byte Channel(uint v, uint mask)
    {
        if(mask == 0) return 0;
        var shift = 0;
        while(((mask >> shift) & 1) == 0) shift++;
        var max = mask >> shift;
        var value = (v & mask) >> shift;
        if(max == 255) return (byte)value;
        return (byte)Mathf.RoundToInt(value * 255f / max);
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll")]
    static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterClipboardFormat(string name);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern UIntPtr GlobalSize(IntPtr handle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder file, uint size);
}
#endif
