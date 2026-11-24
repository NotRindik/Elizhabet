using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public enum PixelCorrectionType
{
    [InspectorName("Brightness / Contrast")] BrightnessContrast,
    [InspectorName("Hue / Saturation / Value")] HueSaturation,
    Posterize,
    Invert,
    Levels,
    [InspectorName("Tone Curve")] Curves,
    [InspectorName("Color Balance")] ColorBalance,
    Binarize,
    [InspectorName("Gradient Map")] GradientMap
}

[System.Serializable]
public class PixelCorrection
{
    public PixelCorrectionType type = PixelCorrectionType.HueSaturation;

    [Range(-100, 100)] public float brightness;
    [Range(-100, 100)] public float contrast;

    [Range(-180, 180)] public float hue;
    [Range(-100, 100)] public float saturation;
    [Range(-100, 100)] public float value;

    [Range(2, 32)] public int posterizeLevels = 4;

    [Range(0, 255)] public float inBlack;
    [Range(0, 255)] public float inWhite = 255;
    [Range(0.1f, 9.99f)] public float gamma = 1;
    [Range(0, 255)] public float outBlack;
    [Range(0, 255)] public float outWhite = 255;

    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve curveRed = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve curveGreen = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve curveBlue = AnimationCurve.Linear(0, 0, 1, 1);

    public Vector3 shadows;
    public Vector3 midtones;
    public Vector3 highlights;
    public bool preserveLuminosity = true;

    [Range(0, 255)] public float threshold = 128;

    public Gradient gradient = DefaultGradient();

    public static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        return g;
    }

    public bool StepsColors
    {
        get { return type == PixelCorrectionType.Posterize || type == PixelCorrectionType.Binarize; }
    }

    public Color Evaluate(Color c)
    {
        var a = c.a;
        switch(type)
        {
            case PixelCorrectionType.BrightnessContrast:
            {
                var b = brightness / 100f;
                var k = contrast >= 0 ? 1f / Mathf.Max(0.0001f, 1f - contrast / 100f * 0.99f) : 1f + contrast / 100f;
                c.r = (c.r + b - 0.5f) * k + 0.5f;
                c.g = (c.g + b - 0.5f) * k + 0.5f;
                c.b = (c.b + b - 0.5f) * k + 0.5f;
                break;
            }
            case PixelCorrectionType.HueSaturation:
            {
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                h = Mathf.Repeat(h + hue / 360f, 1f);
                var ds = saturation / 100f;
                var dv = value / 100f;
                s = ds < 0 ? s * (1f + ds) : s + (1f - s) * ds;
                v = dv < 0 ? v * (1f + dv) : v + (1f - v) * dv;
                c = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
                break;
            }
            case PixelCorrectionType.Posterize:
            {
                var n = Mathf.Max(2, posterizeLevels) - 1;
                c.r = Mathf.Round(Mathf.Clamp01(c.r) * n) / n;
                c.g = Mathf.Round(Mathf.Clamp01(c.g) * n) / n;
                c.b = Mathf.Round(Mathf.Clamp01(c.b) * n) / n;
                break;
            }
            case PixelCorrectionType.Invert:
                c.r = 1f - c.r;
                c.g = 1f - c.g;
                c.b = 1f - c.b;
                break;
            case PixelCorrectionType.Levels:
                c.r = Level(c.r);
                c.g = Level(c.g);
                c.b = Level(c.b);
                break;
            case PixelCorrectionType.Curves:
                c.r = Curve(curveRed, Curve(curve, c.r));
                c.g = Curve(curveGreen, Curve(curve, c.g));
                c.b = Curve(curveBlue, Curve(curve, c.b));
                break;
            case PixelCorrectionType.ColorBalance:
                c = Balance(c);
                break;
            case PixelCorrectionType.Binarize:
            {
                var l = Luma(c);
                var on = l * 255f >= threshold ? 1f : 0f;
                c = new Color(on, on, on);
                break;
            }
            case PixelCorrectionType.GradientMap:
                c = gradient != null ? gradient.Evaluate(Mathf.Clamp01(Luma(c))) : c;
                break;
        }
        return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), a);
    }

    float Level(float x)
    {
        var lo = inBlack / 255f;
        var hi = Mathf.Max(lo + 0.004f, inWhite / 255f);
        var t = Mathf.Clamp01((x - lo) / (hi - lo));
        t = Mathf.Pow(t, 1f / Mathf.Max(0.01f, gamma));
        return Mathf.Lerp(outBlack / 255f, outWhite / 255f, t);
    }

    static float Curve(AnimationCurve c, float x)
    {
        return c != null && c.length > 0 ? c.Evaluate(Mathf.Clamp01(x)) : x;
    }

    static float Luma(Color c)
    {
        return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
    }

    Color Balance(Color c)
    {
        var l = Luma(c);
        var shadowW = Mathf.Clamp01(1f - l * 3f);
        var highW = Mathf.Clamp01(l * 3f - 2f);
        var midW = Mathf.Clamp01(1f - shadowW - highW);
        var shift = (shadows * shadowW + midtones * midW + highlights * highW) / 100f * 0.5f;
        var r = c.r + shift.x;
        var g = c.g + shift.y;
        var b = c.b + shift.z;
        if(preserveLuminosity)
        {
            var nl = r * 0.299f + g * 0.587f + b * 0.114f;
            var d = l - nl;
            r += d;
            g += d;
            b += d;
        }
        return new Color(r, g, b, c.a);
    }

    public int Hash()
    {
        unchecked
        {
            var h = (int)type * 397;
            h = h * 31 + brightness.GetHashCode();
            h = h * 31 + contrast.GetHashCode();
            h = h * 31 + hue.GetHashCode();
            h = h * 31 + saturation.GetHashCode();
            h = h * 31 + value.GetHashCode();
            h = h * 31 + posterizeLevels;
            h = h * 31 + inBlack.GetHashCode();
            h = h * 31 + inWhite.GetHashCode();
            h = h * 31 + gamma.GetHashCode();
            h = h * 31 + outBlack.GetHashCode();
            h = h * 31 + outWhite.GetHashCode();
            h = h * 31 + CurveHash(curve);
            h = h * 31 + CurveHash(curveRed);
            h = h * 31 + CurveHash(curveGreen);
            h = h * 31 + CurveHash(curveBlue);
            h = h * 31 + shadows.GetHashCode();
            h = h * 31 + midtones.GetHashCode();
            h = h * 31 + highlights.GetHashCode();
            h = h * 31 + (preserveLuminosity ? 1 : 0);
            h = h * 31 + threshold.GetHashCode();
            if(gradient != null)
            {
                var keys = gradient.colorKeys;
                for(var i = 0; i < keys.Length; i++) h = h * 31 + keys[i].color.GetHashCode() + keys[i].time.GetHashCode();
                h = h * 31 + (int)gradient.mode;
            }
            return h;
        }
    }

    static int CurveHash(AnimationCurve c)
    {
        if(c == null) return 0;
        unchecked
        {
            var h = c.length;
            var keys = c.keys;
            for(var i = 0; i < keys.Length; i++)
                h = h * 31 + keys[i].time.GetHashCode() + keys[i].value.GetHashCode() * 7 + keys[i].inTangent.GetHashCode() * 13 + keys[i].outTangent.GetHashCode() * 17;
            return h;
        }
    }
}

public class PixelCorrectionChain
{
    public readonly List<PixelLayer> layers = new List<PixelLayer>();
    readonly Dictionary<int, Color32> cache = new Dictionary<int, Color32>();

    public int Hash()
    {
        unchecked
        {
            var h = 17;
            for(var i = 0; i < layers.Count; i++)
            {
                h = h * 31 + layers[i].correction.Hash();
                h = h * 31 + layers[i].opacity.GetHashCode();
            }
            return h;
        }
    }

    public bool StepsColors
    {
        get
        {
            for(var i = 0; i < layers.Count; i++)
                if(layers[i].correction.StepsColors) return true;
            return false;
        }
    }

    public Color Evaluate(Color c)
    {
        for(var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            var corrected = l.correction.Evaluate(c);
            c = Color.Lerp(c, corrected, Mathf.Clamp01(l.opacity));
        }
        return c;
    }

    public Color32 Evaluate(Color32 c)
    {
        if(c.a == 0 || layers.Count == 0) return c;
        var key = (c.r << 16) | (c.g << 8) | c.b;
        Color32 result;
        if(!cache.TryGetValue(key, out result))
        {
            result = Evaluate(new Color32(c.r, c.g, c.b, 255));
            cache[key] = result;
        }
        result.a = c.a;
        return result;
    }
}

public static class PixelCorrections
{
    static readonly int LutId = Shader.PropertyToID("_CorrectionLut");
    static readonly int LutOnId = Shader.PropertyToID("_CorrectionOn");
    static readonly int LutSizeId = Shader.PropertyToID("_CorrectionLutSize");
    static readonly Dictionary<int, Texture3D> luts = new Dictionary<int, Texture3D>();
    static MaterialPropertyBlock block;

    public static List<PixelLayer> Sorted(PixelCanvas canvas)
    {
        var layers = new List<PixelLayer>(canvas.GetComponentsInChildren<PixelLayer>(true));
        layers.Sort((a, b) => a.sortIndex.CompareTo(b.sortIndex));
        return layers;
    }

    static int ClipBase(List<PixelLayer> sorted, int index)
    {
        for(var j = index - 1; j >= 0; j--)
        {
            var l = sorted[j];
            if(l.IsCorrection || l.clip) continue;
            return j;
        }
        for(var j = 0; j < index; j++)
            if(!sorted[j].IsCorrection) return j;
        return -1;
    }

    public static bool Affects(PixelLayer correction)
    {
        return correction != null && correction.IsCorrection && correction.visible && correction.opacity > 0 && correction.gameObject.activeInHierarchy;
    }

    public static PixelCorrectionChain ChainFor(List<PixelLayer> sorted, int index)
    {
        var chain = new PixelCorrectionChain();
        var target = sorted[index];
        if(target.IsCorrection) return chain;
        var targetBase = target.clip ? ClipBase(sorted, index) : index;
        for(var j = index + 1; j < sorted.Count; j++)
        {
            var l = sorted[j];
            if(!Affects(l)) continue;
            if(l.clip)
            {
                var b = ClipBase(sorted, j);
                if(b != index && b != targetBase) continue;
            }
            chain.layers.Add(l);
        }
        return chain;
    }

    public static void Refresh(PixelCanvas canvas)
    {
        if(canvas == null) return;
        var sorted = Sorted(canvas);
        for(var i = 0; i < sorted.Count; i++)
        {
            var layer = sorted[i];
            if(layer.IsCorrection) continue;
            var chain = ChainFor(sorted, i);
            layer.SetCorrectionLut(chain.layers.Count > 0 ? LutFor(chain) : null);
        }
    }

    public static Texture3D LutFor(PixelCorrectionChain chain)
    {
        var size = chain.StepsColors ? 64 : 32;
        var key = chain.Hash() * 31 + size;
        Texture3D lut;
        if(luts.TryGetValue(key, out lut) && lut != null) return lut;
        if(luts.Count > 64)
        {
            foreach(var pair in luts)
                if(pair.Value != null) Object.DestroyImmediate(pair.Value);
            luts.Clear();
        }
        lut = new Texture3D(size, size, size, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        lut.hideFlags = HideFlags.HideAndDontSave;
        lut.wrapMode = TextureWrapMode.Clamp;
        lut.filterMode = FilterMode.Bilinear;
        var colors = new Color32[size * size * size];
        var step = 1f / (size - 1);
        for(var b = 0; b < size; b++)
            for(var g = 0; g < size; g++)
                for(var r = 0; r < size; r++)
                    colors[r + g * size + b * size * size] = chain.Evaluate(new Color(r * step, g * step, b * step, 1f));
        lut.SetPixels32(colors);
        lut.Apply(false);
        luts[key] = lut;
        return lut;
    }

    public static void Bind(SpriteRenderer sr, Texture3D lut)
    {
        if(sr == null) return;
        if(block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        if(lut != null)
        {
            block.SetTexture(LutId, lut);
            block.SetFloat(LutSizeId, lut.width);
            block.SetFloat(LutOnId, 1f);
        }
        else
        {
            block.SetFloat(LutOnId, 0f);
        }
        sr.SetPropertyBlock(block);
    }
}
