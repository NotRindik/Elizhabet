using UnityEngine;
using UnityEngine.UIElements;

namespace MultiAnimator2
{
    public static class Palette
    {
        private static readonly Color[] Tracks =
        {
            new(0.98f, 0.45f, 0.45f), new(0.98f, 0.75f, 0.35f), new(0.55f, 0.90f, 0.45f),
            new(0.38f, 0.78f, 0.98f), new(0.72f, 0.56f, 0.98f), new(0.98f, 0.56f, 0.85f)
        };

        public static readonly Color Background = new(0.16f, 0.16f, 0.16f);
        public static readonly Color RowEven = new(0.19f, 0.19f, 0.19f);
        public static readonly Color RowOdd = new(0.175f, 0.175f, 0.175f);
        public static readonly Color PropertyRow = new(0.15f, 0.15f, 0.15f);
        public static readonly Color RowLine = new(0f, 0f, 0f, 0.4f);
        public static readonly Color FocusedRow = new(0.24f, 0.32f, 0.42f, 0.35f);
        public static readonly Color GridMajor = new(1f, 1f, 1f, 0.07f);
        public static readonly Color GridMinor = new(1f, 1f, 1f, 0.025f);
        public static readonly Color OutsideClip = new(0f, 0f, 0f, 0.25f);
        public static readonly Color Selected = new(1f, 0.82f, 0.2f);
        public static readonly Color Stacked = new(1f, 0.9f, 0.4f);
        public static readonly Color BoxFill = new(0.3f, 0.6f, 1f, 0.12f);
        public static readonly Color BoxStroke = new(0.4f, 0.7f, 1f, 0.8f);
        public static readonly Color Ruler = new(0.215f, 0.215f, 0.215f);
        public static readonly Color RulerTick = new(0.55f, 0.55f, 0.55f);
        public static readonly Color RulerText = new(0.7f, 0.7f, 0.7f);
        public static readonly Color Playhead = new(0.35f, 0.65f, 1f);
        public static readonly Color EndMarker = new(1f, 1f, 1f, 0.25f);

        public static Color ForPart(int index) => Tracks[Mathf.Abs(index) % Tracks.Length];

        public static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }

    public static class Paint
    {
        public static void Rect(Painter2D p, Rect r, Color color)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();
        }

        public static void Outline(Painter2D p, Rect r, Color color, float width = 1f)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Stroke();
        }

        public static void Diamond(Painter2D p, Vector2 c, float size, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x, c.y - size));
            p.LineTo(new Vector2(c.x + size, c.y));
            p.LineTo(new Vector2(c.x, c.y + size));
            p.LineTo(new Vector2(c.x - size, c.y));
            p.ClosePath();
            p.Fill();
        }

        public static void Square(Painter2D p, Vector2 c, float size, Color color) =>
            Rect(p, new Rect(c.x - size, c.y - size, size * 2f, size * 2f), color);

        public static void VLine(Painter2D p, float x, float y0, float y1, Color color, float width = 1f) =>
            Rect(p, new Rect(Mathf.Round(x) - width * 0.5f, y0, width, y1 - y0), color);

        public static void HLine(Painter2D p, float y, float x0, float x1, Color color) =>
            Rect(p, new Rect(x0, Mathf.Round(y) - 0.5f, x1 - x0, 1f), color);
    }
}
