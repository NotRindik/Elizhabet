using UnityEngine;

// Brush definition. Create more via Assets > Create > Pixel Canvas > Brush (or the "+" button in the paint panel).
// Every PixelBrush asset in the project shows up in the brush list automatically.
// For custom behaviour (spray, dither, stamps...) inherit from this class and override Covers or Dab.
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

    // Is pixel (i, j) of the size x size footprint painted? i, j are in 0..size-1.
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

    // One dab of the brush centered on (x, y). Paints through the plot callback.
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
