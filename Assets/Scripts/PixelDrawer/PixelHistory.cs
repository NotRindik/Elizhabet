#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Undo/redo for pixel strokes. Strokes are stored as per-pixel diffs in absolute layer coordinates
// (so they survive canvas growth and Trim). A hidden proxy object registered in Unity's Undo system
// carries the history position, so Ctrl+Z / Ctrl+Y and the Edit menu work and interleave with normal scene undo.
[InitializeOnLoad]
public static class PixelHistory
{
    const int MaxStrokes = 200;

    class UndoProxy : ScriptableObject
    {
        public int position;
    }

    struct Change
    {
        public int x, y;
        public Color32 oldColor, newColor;
    }

    class Stroke
    {
        public PixelLayer layer;
        public readonly List<Change> changes = new List<Change>();
        public readonly Dictionary<long, int> index = new Dictionary<long, int>();
    }

    static readonly List<Stroke> strokes = new List<Stroke>();
    static int baseIndex;   // absolute index of strokes[0]
    static int applied;     // absolute number of strokes currently applied
    static UndoProxy proxy;
    static Stroke current;
    static bool applying;
    static readonly HashSet<PixelLayer> affected = new HashSet<PixelLayer>();

    static PixelHistory()
    {
        var old = Resources.FindObjectsOfTypeAll<UndoProxy>();
        for(var i = 0; i < old.Length; i++) Object.DestroyImmediate(old[i]);
        Undo.undoRedoPerformed += OnUndoRedo;
        PixelCanvasEditing.PixelChanged += OnPixelChanged;
    }

    public static void BeginStroke(PixelLayer layer)
    {
        if(current != null) EndStroke();
        current = new Stroke { layer = layer };
    }

    public static void EndStroke()
    {
        var stroke = current;
        current = null;
        if(stroke == null) return;

        // drop pixels that ended up unchanged
        for(var i = stroke.changes.Count - 1; i >= 0; i--)
        {
            var c = stroke.changes[i];
            if(Same(c.oldColor, c.newColor)) stroke.changes.RemoveAt(i);
        }
        if(stroke.changes.Count == 0) return;

        if(proxy == null)
        {
            proxy = ScriptableObject.CreateInstance<UndoProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            proxy.position = applied;
        }

        var keep = applied - baseIndex;
        if(keep < strokes.Count) strokes.RemoveRange(keep, strokes.Count - keep); // new stroke kills redo

        Undo.RegisterCompleteObjectUndo(proxy, "Pixel Paint");
        strokes.Add(stroke);
        applied++;
        proxy.position = applied;
        Undo.IncrementCurrentGroup();

        while(strokes.Count > MaxStrokes)
        {
            strokes.RemoveAt(0);
            baseIndex++;
        }

        // canvas follows the painted area both ways: Grow while painting, Shrink when the stroke is done
        PixelCanvasEditing.Shrink(stroke.layer);
    }

    static void OnPixelChanged(PixelLayer layer, int x, int y, Color32 oldColor, Color32 newColor)
    {
        if(applying || current == null) return;
        var key = ((long)y << 32) | (uint)x;
        if(current.index.TryGetValue(key, out var i))
        {
            var c = current.changes[i];
            c.newColor = newColor;
            current.changes[i] = c;
        }
        else
        {
            current.index[key] = current.changes.Count;
            current.changes.Add(new Change { x = x, y = y, oldColor = oldColor, newColor = newColor });
        }
    }

    static void OnUndoRedo()
    {
        if(proxy == null) return;
        var target = proxy.position;
        var changed = false;
        affected.Clear();

        while(applied > target && applied > baseIndex)
        {
            applied--;
            Apply(strokes[applied - baseIndex], false);
            changed = true;
        }
        while(applied < target && applied - baseIndex < strokes.Count)
        {
            Apply(strokes[applied - baseIndex], true);
            applied++;
            changed = true;
        }
        proxy.position = applied;

        if(changed)
        {
            PixelCanvasEditing.Apply();
            foreach(var layer in affected)
                if(layer != null) PixelCanvasEditing.Shrink(layer);
            affected.Clear();
            SceneView.RepaintAll();
        }
    }

    static void Apply(Stroke stroke, bool redo)
    {
        if(stroke.layer == null || stroke.layer.Root == null) return;
        affected.Add(stroke.layer);
        applying = true;
        for(var i = 0; i < stroke.changes.Count; i++)
        {
            var c = stroke.changes[i];
            PixelCanvasEditing.SetPixel(stroke.layer, c.x, c.y, redo ? c.newColor : c.oldColor);
        }
        applying = false;
    }

    static bool Same(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
#endif
