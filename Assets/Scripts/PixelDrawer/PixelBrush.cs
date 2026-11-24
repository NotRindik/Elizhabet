using UnityEngine;

[CreateAssetMenu(fileName = "Pixel Brush", menuName = "Pixel Canvas/Brush")]
public class PixelBrush : ScriptableObject
{
    public enum BrushShape { Square, Circle }
    public enum BrushFill
    {
        Solid,
        [InspectorName("Checker 50%")] Checker,
        [InspectorName("Dots 25%")] Dots25,
        [InspectorName("Dots 12%")] Dots12,
        [InspectorName("Dots 6%")] Dots6,
        [InspectorName("Dense 75%")] Dense75,
        [InspectorName("Dense 87%")] Dense87,
        [InspectorName("Lines Horizontal")] LinesHorizontal,
        [InspectorName("Lines Vertical")] LinesVertical,
        [InspectorName("Diagonal /")] Diagonal,
        [InspectorName("Diagonal \\")] AntiDiagonal,
        Grid,
        Crosshatch,
        Bricks
    }

    static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    public BrushShape shape = BrushShape.Square;
    [Tooltip("Solid paints every covered pixel. The other fills paint a dithering pattern locked to the canvas grid, so strokes join without seams.")]
    public BrushFill fill = BrushFill.Solid;
    [Tooltip("Paint the pixels the pattern skips instead. Handy for a second shade on top of the first one.")]
    public bool fillInvert;
    [Tooltip("Shifts the pattern in pixels. Use it to place two passes of the same pattern side by side.")]
    public Vector2Int fillOffset;
    [Min(1)] public int minSize = 1;
    [Min(1)] public int maxSize = 32;
    [Tooltip("Distance in pixels between dabs along a stroke. 1 = continuous line.")]
    [Min(1)] public int spacing = 1;

    public int MinSize { get { return Mathf.Max(1, minSize); } }
    public int MaxSize { get { return Mathf.Max(MinSize, maxSize); } }

    public int ClampSize(int size)
    {
        return Mathf.Clamp(size, MinSize, MaxSize);
    }
    public virtual bool Covers(int i, int j, int size)
    {
        if(shape == BrushShape.Circle && size > 2)
        {
            var c = (size - 1) * 0.5f;
            var dx = i - c;
            var dy = j - c;
            var r = size * 0.5f;
            return dx * dx + dy * dy <= r * r - 0.5f;
        }
        return true;
    }
    public virtual bool FillPixel(int x, int y, Color32 color, out Color32 result)
    {
        result = color;
        return PatternOn(x, y);
    }

    public bool PatternOn(int x, int y)
    {
        if(fill == BrushFill.Solid) return true;
        var on = FillPattern(fill, x - fillOffset.x, y - fillOffset.y);
        return fillInvert ? !on : on;
    }

    public static bool FillPattern(BrushFill fill, int x, int y)
    {
        switch(fill)
        {
            case BrushFill.Checker: return ((x + y) & 1) == 0;
            case BrushFill.Dots25: return Bayer4[(y & 3) * 4 + (x & 3)] < 4;
            case BrushFill.Dots12: return Bayer4[(y & 3) * 4 + (x & 3)] < 2;
            case BrushFill.Dots6: return Bayer4[(y & 3) * 4 + (x & 3)] < 1;
            case BrushFill.Dense75: return Bayer4[(y & 3) * 4 + (x & 3)] < 12;
            case BrushFill.Dense87: return Bayer4[(y & 3) * 4 + (x & 3)] < 14;
            case BrushFill.LinesHorizontal: return (y & 1) == 0;
            case BrushFill.LinesVertical: return (x & 1) == 0;
            case BrushFill.Diagonal: return ((x - y) & 3) == 0;
            case BrushFill.AntiDiagonal: return ((x + y) & 3) == 0;
            case BrushFill.Grid: return (x & 3) == 0 || (y & 3) == 0;
            case BrushFill.Crosshatch: return ((x - y) & 3) == 0 || ((x + y) & 3) == 0;
            case BrushFill.Bricks: return (y & 3) == 0 || ((x + ((y >> 2) & 1) * 4) & 7) == 0;
        }
        return true;
    }

    public virtual RectInt CursorRect(int x, int y, int size)
    {
        return new RectInt(x - size / 2, y - size / 2, size, size);
    }

    public virtual void Dab(int x, int y, int size, Color32 color, System.Action<int, int, Color32> plot)
    {
        var ox = x - size / 2;
        var oy = y - size / 2;
        for(var j = 0; j < size; j++)
        {
            for(var i = 0; i < size; i++)
            {
                if(!Covers(i, j, size)) continue;
                var px = ox + i;
                var py = oy + j;
                if(!PatternOn(px, py)) continue;
                plot(px, py, color);
            }
        }
    }
}
