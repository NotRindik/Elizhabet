using UnityEngine;

[CreateAssetMenu(fileName = "Pixel Brush", menuName = "Pixel Canvas/Brush")]
public class PixelBrush : ScriptableObject
{
    public enum BrushShape { Square, Circle }
    public enum BrushFill { Solid, Checker }

    public BrushShape shape = BrushShape.Square;
    [Tooltip("Solid paints every covered pixel. Checker paints every second pixel (simple dithering).")]
    public BrushFill fill = BrushFill.Solid;
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
        if(fill == BrushFill.Checker && ((x + y) & 1) != 0) return false;
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
                if(fill == BrushFill.Checker && ((px + py) & 1) != 0) continue;
                plot(px, py, color);
            }
        }
    }
}
