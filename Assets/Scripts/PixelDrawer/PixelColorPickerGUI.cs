#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Встроенный цветовой пикер в стиле Photoshop / Clip Studio:
/// квадрат Saturation/Value + вертикальная полоса Hue + полоса Alpha,
/// RGBA-слайдеры и HEX-поле. Рисуется прямо внутри окна (без попапа).
/// </summary>
public class PixelColorPickerGUI
{
    const int SvRes = 48;
    const int StripRes = 64;

    float h, s = 1f, v = 1f, a = 1f;
    Color last = new Color(-1f, -1f, -1f, -1f);

    Texture2D svTex, hueTex, alphaTex;
    float svTexHue = -1f;
    Color alphaTexRgb = new Color(-1f, -1f, -1f, 1f);

    GUIStyle channelStyleR, channelStyleG, channelStyleB, channelStyleA, hexStyle;

    // ------------------------------------------------------------------
    public void Dispose()
    {
        Kill(ref svTex);
        Kill(ref hueTex);
        Kill(ref alphaTex);
    }

    static void Kill(ref Texture2D t)
    {
        if(t != null) Object.DestroyImmediate(t);
        t = null;
    }

    static Texture2D NewTex(int w, int hgt)
    {
        var t = new Texture2D(w, hgt, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        return t;
    }

    // ------------------------------------------------------------------
    //  Основной вызов. Возвращает true, если цвет изменился.
    // ------------------------------------------------------------------
    public bool Draw(ref Color color, float availableWidth)
    {
        EnsureStyles();

        var changed = false;

        // Цвет изменили снаружи (палитра, пипетка...) — синхронизируем HSV
        if(!Same(color, last))
        {
            SetFromColor(color);
            last = color;
        }

        // ----- раскладка -----
        var size = Mathf.Clamp(availableWidth - 24f, 110f, 180f);
        var area = GUILayoutUtility.GetRect(0, size, GUILayout.ExpandWidth(true));
        var svRect = new Rect(area.x, area.y, Mathf.Max(20f, area.width - 24f), size);
        var hueRect = new Rect(area.xMax - 16f, area.y, 16f, size);
        var svId = GUIUtility.GetControlID(FocusType.Passive);
        var hueId = GUIUtility.GetControlID(FocusType.Passive);

        GUILayout.Space(4);
        var alphaRect = GUILayoutUtility.GetRect(0, 14f, GUILayout.ExpandWidth(true));
        var alphaId = GUIUtility.GetControlID(FocusType.Passive);

        // ----- ввод мышью -----
        Vector2 n;
        if(Drag(svRect, svId, out n))
        {
            s = n.x;
            v = 1f - n.y;
            changed = true;
        }
        if(Drag(hueRect, hueId, out n))
        {
            h = 1f - n.y;
            changed = true;
        }
        if(Drag(alphaRect, alphaId, out n))
        {
            a = n.x;
            changed = true;
        }

        if(changed)
        {
            color = Color.HSVToRGB(h, s, v);
            color.a = a;
            last = color;
        }

        // ----- отрисовка -----
        if(Event.current.type == EventType.Repaint)
        {
            EnsureTextures();

            GUI.DrawTexture(svRect, svTex, ScaleMode.StretchToFill, false);
            GUI.DrawTexture(hueRect, hueTex, ScaleMode.StretchToFill, false);

            DrawChecker(alphaRect);
            GUI.DrawTexture(alphaRect, alphaTex, ScaleMode.StretchToFill, true);

            var border = EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.6f) : new Color(0f, 0f, 0f, 0.35f);
            Frame(svRect, 1f, border);
            Frame(hueRect, 1f, border);
            Frame(alphaRect, 1f, border);

            // курсор в квадрате
            var cx = svRect.x + s * svRect.width;
            var cy = svRect.yMax - v * svRect.height;
            var ring = new Rect(cx - 6f, cy - 6f, 12f, 12f);
            Frame(ring, 1f, Color.black);
            Frame(Shrink(ring, 1f), 1f, Color.white);
            Frame(Shrink(ring, 2f), 1f, Color.black);

            // маркер hue
            var hy = hueRect.yMax - h * hueRect.height;
            EditorGUI.DrawRect(new Rect(hueRect.x - 2f, hy - 2f, hueRect.width + 4f, 4f), Color.black);
            EditorGUI.DrawRect(new Rect(hueRect.x - 1f, hy - 1f, hueRect.width + 2f, 2f), Color.white);

            // маркер alpha
            var ax = alphaRect.x + a * alphaRect.width;
            EditorGUI.DrawRect(new Rect(ax - 2f, alphaRect.y - 2f, 4f, alphaRect.height + 4f), Color.black);
            EditorGUI.DrawRect(new Rect(ax - 1f, alphaRect.y - 1f, 2f, alphaRect.height + 2f), Color.white);
        }

        GUILayout.Space(6);

        // ----- RGBA -----
        EditorGUI.BeginChangeCheck();
        var r8 = Channel("R", channelStyleR, Mathf.RoundToInt(color.r * 255f));
        var g8 = Channel("G", channelStyleG, Mathf.RoundToInt(color.g * 255f));
        var b8 = Channel("B", channelStyleB, Mathf.RoundToInt(color.b * 255f));
        var a8 = Channel("A", channelStyleA, Mathf.RoundToInt(color.a * 255f));
        if(EditorGUI.EndChangeCheck())
        {
            color = new Color(r8 / 255f, g8 / 255f, b8 / 255f, a8 / 255f);
            SetFromColor(color);
            last = color;
            changed = true;
        }

        // ----- HEX + превью -----
        GUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Hex", GUILayout.Width(28f));
        var hex = "#" + ColorUtility.ToHtmlStringRGBA(color);
        var typed = EditorGUILayout.DelayedTextField(hex);
        if(typed != hex)
        {
            if(!typed.StartsWith("#")) typed = "#" + typed;
            Color parsed;
            if(ColorUtility.TryParseHtmlString(typed, out parsed))
            {
                color = parsed;
                SetFromColor(color);
                last = color;
                changed = true;
            }
        }

        var sw = GUILayoutUtility.GetRect(44f, 18f, GUILayout.Width(44f));
        if(Event.current.type == EventType.Repaint)
        {
            DrawChecker(sw);
            var opaque = new Color(color.r, color.g, color.b, 1f);
            EditorGUI.DrawRect(new Rect(sw.x, sw.y, sw.width * 0.5f, sw.height), opaque);
            EditorGUI.DrawRect(new Rect(sw.x + sw.width * 0.5f, sw.y, sw.width * 0.5f, sw.height), color);
            Frame(sw, 1f, new Color(0f, 0f, 0f, 0.6f));
        }
        EditorGUILayout.EndHorizontal();

        return changed;
    }

    // ------------------------------------------------------------------
    //  Внутреннее
    // ------------------------------------------------------------------
    void EnsureStyles()
    {
        if(channelStyleR != null) return;
        channelStyleR = MakeChannelStyle(new Color(0.95f, 0.40f, 0.40f));
        channelStyleG = MakeChannelStyle(new Color(0.45f, 0.85f, 0.45f));
        channelStyleB = MakeChannelStyle(new Color(0.45f, 0.65f, 1.00f));
        channelStyleA = MakeChannelStyle(EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.25f, 0.25f, 0.25f));
    }

    static GUIStyle MakeChannelStyle(Color c)
    {
        var st = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        st.normal.textColor = c;
        return st;
    }

    static int Channel(string label, GUIStyle style, int value)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, style, GUILayout.Width(16f));
        value = EditorGUILayout.IntSlider(value, 0, 255);
        EditorGUILayout.EndHorizontal();
        return value;
    }

    void SetFromColor(Color c)
    {
        float nh, ns, nv;
        Color.RGBToHSV(c, out nh, out ns, out nv);
        // Для серых / чёрного hue неопределён — оставляем прежний, чтобы курсор не прыгал
        if(ns > 0.0001f && nv > 0.0001f) h = nh;
        s = ns;
        v = nv;
        a = c.a;
    }

    static bool Same(Color x, Color y)
    {
        const float eps = 1f / 512f;
        return Mathf.Abs(x.r - y.r) < eps && Mathf.Abs(x.g - y.g) < eps &&
               Mathf.Abs(x.b - y.b) < eps && Mathf.Abs(x.a - y.a) < eps;
    }

    void EnsureTextures()
    {
        if(svTex == null)
        {
            svTex = NewTex(SvRes, SvRes);
            svTexHue = -1f;
        }
        if(Mathf.Abs(svTexHue - h) > 0.0005f)
        {
            var px = new Color32[SvRes * SvRes];
            for(var y = 0; y < SvRes; y++)
            {
                for(var x = 0; x < SvRes; x++)
                {
                    px[y * SvRes + x] = Color.HSVToRGB(h, x / (SvRes - 1f), y / (SvRes - 1f));
                }
            }
            svTex.SetPixels32(px);
            svTex.Apply(false);
            svTexHue = h;
        }

        if(hueTex == null)
        {
            hueTex = NewTex(1, StripRes);
            var px = new Color32[StripRes];
            for(var y = 0; y < StripRes; y++) px[y] = Color.HSVToRGB(y / (StripRes - 1f), 1f, 1f);
            hueTex.SetPixels32(px);
            hueTex.Apply(false);
        }

        var rgb = Color.HSVToRGB(h, s, v);
        if(alphaTex == null || !Same(new Color(rgb.r, rgb.g, rgb.b, 1f), alphaTexRgb))
        {
            if(alphaTex == null) alphaTex = NewTex(StripRes, 1);
            var px = new Color32[StripRes];
            for(var x = 0; x < StripRes; x++) px[x] = new Color(rgb.r, rgb.g, rgb.b, x / (StripRes - 1f));
            alphaTex.SetPixels32(px);
            alphaTex.Apply(false);
            alphaTexRgb = new Color(rgb.r, rgb.g, rgb.b, 1f);
        }
    }

    static bool Drag(Rect r, int id, out Vector2 n)
    {
        n = Vector2.zero;
        var e = Event.current;
        var type = e.GetTypeForControl(id);

        if(type == EventType.MouseDown)
        {
            if(e.button != 0 || !r.Contains(e.mousePosition)) return false;
            GUIUtility.hotControl = id;
            e.Use();
        }
        else if(type == EventType.MouseDrag)
        {
            if(GUIUtility.hotControl != id) return false;
            e.Use();
        }
        else if(type == EventType.MouseUp)
        {
            if(GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
            return false;
        }
        else
        {
            return false;
        }

        n = new Vector2(
            Mathf.Clamp01((e.mousePosition.x - r.x) / r.width),
            Mathf.Clamp01((e.mousePosition.y - r.y) / r.height));
        return true;
    }

    static void DrawChecker(Rect r)
    {
        const float cell = 6f;
        EditorGUI.DrawRect(r, new Color(0.80f, 0.80f, 0.80f));
        var dark = new Color(0.55f, 0.55f, 0.55f);
        var cols = Mathf.CeilToInt(r.width / cell);
        var rows = Mathf.CeilToInt(r.height / cell);
        for(var cy = 0; cy < rows; cy++)
        {
            for(var cx = 0; cx < cols; cx++)
            {
                if(((cx + cy) & 1) == 0) continue;
                var x = r.x + cx * cell;
                var y = r.y + cy * cell;
                var w = Mathf.Min(cell, r.xMax - x);
                var hh = Mathf.Min(cell, r.yMax - y);
                EditorGUI.DrawRect(new Rect(x, y, w, hh), dark);
            }
        }
    }

    static Rect Shrink(Rect r, float p)
    {
        return new Rect(r.x + p, r.y + p, r.width - p * 2f, r.height - p * 2f);
    }

    static void Frame(Rect r, float t, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y + t, t, r.height - t * 2f), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y + t, t, r.height - t * 2f), c);
    }
}
#endif
