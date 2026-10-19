using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Pixel Palette", menuName = "Pixel Canvas/Palette")]
public class PixelPalette : ScriptableObject
{
    public List<Color32> colors = new List<Color32>();

    public bool TryNearest(Color32 c, out Color32 result)
    {
        result = c;
        if(colors.Count == 0) return false;
        var best = int.MaxValue;
        for(var i = 0; i < colors.Count; i++)
        {
            var p = colors[i];
            var dr = p.r - c.r;
            var dg = p.g - c.g;
            var db = p.b - c.b;
            var d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
            if(d >= best) continue;
            best = d;
            result = new Color32(p.r, p.g, p.b, c.a);
        }
        return true;
    }

    public bool Contains(Color32 c)
    {
        for(var i = 0; i < colors.Count; i++)
        {
            var p = colors[i];
            if(p.r == c.r && p.g == c.g && p.b == c.b) return true;
        }
        return false;
    }
}
