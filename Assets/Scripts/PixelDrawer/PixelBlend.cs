using UnityEngine;

public enum PixelBlendMode
{
    Normal,
    Darken,
    Multiply,
    [InspectorName("Color Burn")] ColorBurn,
    [InspectorName("Linear Burn")] LinearBurn,
    Subtract,
    Lighten,
    Screen,
    [InspectorName("Color Dodge")] ColorDodge,
    [InspectorName("Glow Dodge")] GlowDodge,
    Add,
    [InspectorName("Add (Glow)")] AddGlow,
    Overlay,
    [InspectorName("Soft Light")] SoftLight,
    [InspectorName("Hard Light")] HardLight,
    Difference,
    [InspectorName("Vivid Light")] VividLight,
    [InspectorName("Linear Light")] LinearLight,
    [InspectorName("Pin Light")] PinLight,
    [InspectorName("Hard Mix")] HardMix,
    Exclusion,
    [InspectorName("Darker Color")] DarkerColor,
    [InspectorName("Lighter Color")] LighterColor,
    Divide,
    Hue,
    Saturation,
    Color,
    Brightness
}

public static class PixelBlend
{
    public static bool IsLive(PixelBlendMode mode)
    {
        switch(mode)
        {
            case PixelBlendMode.Normal:
            case PixelBlendMode.Darken:
            case PixelBlendMode.Multiply:
            case PixelBlendMode.Subtract:
            case PixelBlendMode.Lighten:
            case PixelBlendMode.Screen:
            case PixelBlendMode.Add:
            case PixelBlendMode.AddGlow:
                return true;
        }
        return false;
    }

    public static Vector3 Blend(PixelBlendMode mode, Vector3 b, Vector3 s)
    {
        switch(mode)
        {
            case PixelBlendMode.DarkerColor:
                return Lum(s) < Lum(b) ? s : b;
            case PixelBlendMode.LighterColor:
                return Lum(s) > Lum(b) ? s : b;
            case PixelBlendMode.Hue:
                return SetLum(SetSat(s, Sat(b)), Lum(b));
            case PixelBlendMode.Saturation:
                return SetLum(SetSat(b, Sat(s)), Lum(b));
            case PixelBlendMode.Color:
                return SetLum(s, Lum(b));
            case PixelBlendMode.Brightness:
                return SetLum(b, Lum(s));
        }
        return new Vector3(Channel(mode, b.x, s.x), Channel(mode, b.y, s.y), Channel(mode, b.z, s.z));
    }

    static float Channel(PixelBlendMode mode, float b, float s)
    {
        switch(mode)
        {
            case PixelBlendMode.Darken: return Mathf.Min(b, s);
            case PixelBlendMode.Multiply: return b * s;
            case PixelBlendMode.ColorBurn: return b >= 1f ? 1f : s <= 0f ? 0f : 1f - Mathf.Min(1f, (1f - b) / s);
            case PixelBlendMode.LinearBurn: return Mathf.Max(0f, b + s - 1f);
            case PixelBlendMode.Subtract: return Mathf.Max(0f, b - s);
            case PixelBlendMode.Lighten: return Mathf.Max(b, s);
            case PixelBlendMode.Screen: return b + s - b * s;
            case PixelBlendMode.ColorDodge:
            case PixelBlendMode.GlowDodge: return b <= 0f ? 0f : s >= 1f ? 1f : Mathf.Min(1f, b / (1f - s));
            case PixelBlendMode.Add:
            case PixelBlendMode.AddGlow: return Mathf.Min(1f, b + s);
            case PixelBlendMode.Overlay: return Hard(s, b);
            case PixelBlendMode.SoftLight: return Soft(b, s);
            case PixelBlendMode.HardLight: return Hard(b, s);
            case PixelBlendMode.Difference: return Mathf.Abs(b - s);
            case PixelBlendMode.VividLight: return s <= 0.5f ? Channel(PixelBlendMode.ColorBurn, b, 2f * s) : Channel(PixelBlendMode.ColorDodge, b, 2f * s - 1f);
            case PixelBlendMode.LinearLight: return Mathf.Clamp01(b + 2f * s - 1f);
            case PixelBlendMode.PinLight: return s <= 0.5f ? Mathf.Min(b, 2f * s) : Mathf.Max(b, 2f * s - 1f);
            case PixelBlendMode.HardMix: return b + s >= 1f ? 1f : 0f;
            case PixelBlendMode.Exclusion: return b + s - 2f * b * s;
            case PixelBlendMode.Divide: return s <= 0f ? 1f : Mathf.Min(1f, b / s);
        }
        return s;
    }

    static float Hard(float b, float s)
    {
        return s <= 0.5f ? b * 2f * s : Channel(PixelBlendMode.Screen, b, 2f * s - 1f);
    }

    static float Soft(float b, float s)
    {
        if(s <= 0.5f) return b - (1f - 2f * s) * b * (1f - b);
        var d = b <= 0.25f ? ((16f * b - 12f) * b + 4f) * b : Mathf.Sqrt(b);
        return b + (2f * s - 1f) * (d - b);
    }

    static float Lum(Vector3 c)
    {
        return 0.3f * c.x + 0.59f * c.y + 0.11f * c.z;
    }

    static Vector3 SetLum(Vector3 c, float l)
    {
        var d = l - Lum(c);
        c += new Vector3(d, d, d);
        var lum = Lum(c);
        var n = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
        var x = Mathf.Max(c.x, Mathf.Max(c.y, c.z));
        if(n < 0f) c = new Vector3(lum, lum, lum) + (c - new Vector3(lum, lum, lum)) * (lum / Mathf.Max(0.0001f, lum - n));
        if(x > 1f) c = new Vector3(lum, lum, lum) + (c - new Vector3(lum, lum, lum)) * ((1f - lum) / Mathf.Max(0.0001f, x - lum));
        return c;
    }

    static float Sat(Vector3 c)
    {
        return Mathf.Max(c.x, Mathf.Max(c.y, c.z)) - Mathf.Min(c.x, Mathf.Min(c.y, c.z));
    }

    static Vector3 SetSat(Vector3 c, float s)
    {
        var max = Mathf.Max(c.x, Mathf.Max(c.y, c.z));
        var min = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
        var range = max - min;
        if(range <= 0.0001f) return Vector3.zero;
        return new Vector3((c.x - min) / range * s, (c.y - min) / range * s, (c.z - min) / range * s);
    }
}
