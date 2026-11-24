#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelHistory
{
    const int MaxSteps = 200;


    struct Change
    {
        public int x;
        public int y;
        public Color32 oldColor;
        public Color32 newColor;
        public byte oldGlow;
        public byte newGlow;
    }

    class Step
    {
        public PixelLayer layer;
        public int frame = -1;
        public readonly List<Change> changes = new List<Change>();
        public readonly Dictionary<long, int> index = new Dictionary<long, int>();
        public bool hasSelection;
        public PixelCanvas selectionCanvas;
        public PixelMask selectionBefore;
        public PixelMask selectionAfter;
        public Vector2Int shift;
        public bool hasTrim;
        public RectInt trimBefore;

        public bool IsEmpty
        {
            get { return changes.Count == 0 && !hasSelection && !hasTrim && shift == Vector2Int.zero; }
        }
    }

    static readonly List<Step> steps = new List<Step>();
    static PixelUndoProxy proxy;
    static Step current;
    static int position;
    static int dropped;
    static bool applying;

    public static System.Action Interrupted;

    static PixelHistory()
    {
        PixelCanvasEditing.PixelChanged += OnPixelChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            EndStroke();
            Save();
        };
        Restore();
    }

    static void Save()
    {
        if(proxy == null || steps.Count == 0) return;
        var store = ScriptableObject.CreateInstance<PixelHistoryStore>();
        store.hideFlags = HideFlags.HideAndDontSave;
        store.proxy = proxy;
        store.position = position;
        store.dropped = dropped;
        store.strokeId = StrokeId;
        for(var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var count = step.changes.Count;
            var stored = new PixelHistoryStore.StoredStep
            {
                layer = step.layer,
                frame = step.frame,
                xs = new int[count],
                ys = new int[count],
                oldColors = new Color32[count],
                newColors = new Color32[count],
                oldGlows = new byte[count],
                newGlows = new byte[count],
                hasSelection = step.hasSelection,
                selectionCanvas = step.selectionCanvas,
                selectionBefore = StoreMask(step.selectionBefore),
                selectionAfter = StoreMask(step.selectionAfter),
                shift = step.shift,
                hasTrim = step.hasTrim,
                trimBefore = step.trimBefore
            };
            for(var c = 0; c < count; c++)
            {
                var change = step.changes[c];
                stored.xs[c] = change.x;
                stored.ys[c] = change.y;
                stored.oldColors[c] = change.oldColor;
                stored.newColors[c] = change.newColor;
                stored.oldGlows[c] = change.oldGlow;
                stored.newGlows[c] = change.newGlow;
            }
            store.steps.Add(stored);
        }
    }

    static void Restore()
    {
        var stores = Resources.FindObjectsOfTypeAll<PixelHistoryStore>();
        if(stores.Length == 0) return;
        var store = stores[0];
        if(store.proxy != null)
        {
            proxy = store.proxy;
            steps.Clear();
            for(var i = 0; i < store.steps.Count; i++)
            {
                var stored = store.steps[i];
                var step = new Step
                {
                    layer = stored.layer,
                    frame = stored.frame,
                    hasSelection = stored.hasSelection,
                    selectionCanvas = stored.selectionCanvas,
                    selectionBefore = LoadMask(stored.selectionBefore),
                    selectionAfter = LoadMask(stored.selectionAfter),
                    shift = stored.shift,
                    hasTrim = stored.hasTrim,
                    trimBefore = stored.trimBefore
                };
                var count = stored.xs != null ? stored.xs.Length : 0;
                for(var c = 0; c < count; c++)
                    step.changes.Add(new Change { x = stored.xs[c], y = stored.ys[c], oldColor = stored.oldColors[c], newColor = stored.newColors[c], oldGlow = stored.oldGlows[c], newGlow = stored.newGlows[c] });
                steps.Add(step);
            }
            position = Mathf.Clamp(store.position, 0, steps.Count);
            dropped = store.dropped;
            StrokeId = store.strokeId;
        }
        for(var i = 0; i < stores.Length; i++)
            Object.DestroyImmediate(stores[i]);
    }

    static PixelHistoryStore.StoredMask StoreMask(PixelMask mask)
    {
        if(mask == null) return new PixelHistoryStore.StoredMask();
        return new PixelHistoryStore.StoredMask { present = true, x = mask.x, y = mask.y, w = mask.w, h = mask.h, outside = mask.outside, bits = (bool[])mask.bits.Clone() };
    }

    static PixelMask LoadMask(PixelHistoryStore.StoredMask stored)
    {
        if(stored == null || !stored.present) return null;
        var mask = new PixelMask(stored.x, stored.y, stored.w, stored.h, stored.outside);
        if(stored.bits != null) System.Array.Copy(stored.bits, mask.bits, Mathf.Min(stored.bits.Length, mask.bits.Length));
        return mask;
    }

    public static bool IsOpen
    {
        get { return current != null; }
    }

    public static int StrokeId { get; private set; }

    public static void BeginStroke(PixelLayer layer)
    {
        if(current != null) EndStroke();
        current = new Step { layer = layer };
        StrokeId++;
    }

    public static void EndStroke()
    {
        var step = current;
        current = null;
        Push(step);
    }

    public static void Discard()
    {
        current = null;
    }

    public static void Clear()
    {
        current = null;
        steps.Clear();
        position = 0;
        dropped = 0;
        if(proxy != null) proxy.position = 0;
    }

    public static void Rollback()
    {
        if(current == null) return;
        var step = current;
        applying = true;
        for(var i = step.changes.Count - 1; i >= 0; i--)
        {
            var c = step.changes[i];
            PixelCanvasEditing.SetPixel(step.layer, c.x, c.y, c.oldColor, c.oldGlow);
        }
        applying = false;
        step.changes.Clear();
        step.index.Clear();
        if(step.hasSelection)
        {
            PixelSelection.Set(step.selectionCanvas, step.selectionBefore, false);
            step.hasSelection = false;
        }
        if(step.shift != Vector2Int.zero)
        {
            PixelCanvasEditing.ShiftOrigin(step.layer, -step.shift.x, -step.shift.y);
            step.shift = Vector2Int.zero;
        }
        PixelCanvasEditing.RequestApply();
    }

    public static void RecordSelection(PixelCanvas canvas, PixelMask before, PixelMask after)
    {
        if(before == null && after == null) return;
        if(current != null)
        {
            if(!current.hasSelection)
            {
                current.hasSelection = true;
                current.selectionCanvas = canvas;
                current.selectionBefore = before;
            }
            current.selectionAfter = after;
            return;
        }
        var step = new Step { hasSelection = true, selectionCanvas = canvas, selectionBefore = before, selectionAfter = after };
        Push(step);
    }

    public static void RecordShift(PixelLayer layer, int dx, int dy)
    {
        if(dx == 0 && dy == 0) return;
        if(current != null && current.layer == layer)
        {
            current.shift += new Vector2Int(dx, dy);
            return;
        }
        Push(new Step { layer = layer, shift = new Vector2Int(dx, dy) });
    }

    public static void RecordTrim(PixelLayer layer, RectInt before)
    {
        Push(new Step { layer = layer, hasTrim = true, trimBefore = before });
    }

    static void OnPixelChanged(PixelLayer layer, int x, int y, Color32 oldColor, Color32 newColor, byte oldGlow, byte newGlow)
    {
        if(applying || current == null || current.layer != layer) return;
        var key = ((long)x << 32) ^ (uint)y;
        int existing;
        if(current.index.TryGetValue(key, out existing))
        {
            var c = current.changes[existing];
            c.newColor = newColor;
            c.newGlow = newGlow;
            current.changes[existing] = c;
        }
        else
        {
            current.index[key] = current.changes.Count;
            current.changes.Add(new Change { x = x, y = y, oldColor = oldColor, newColor = newColor, oldGlow = oldGlow, newGlow = newGlow });
        }
    }

    static void Push(Step step)
    {
        if(step == null) return;
        for(var i = step.changes.Count - 1; i >= 0; i--)
        {
            var c = step.changes[i];
            if(c.oldColor.r == c.newColor.r && c.oldColor.g == c.newColor.g && c.oldColor.b == c.newColor.b && c.oldColor.a == c.newColor.a && c.oldGlow == c.newGlow)
                step.changes.RemoveAt(i);
        }
        if(step.IsEmpty) return;
        if(step.frame < 0 && step.layer != null) step.frame = step.layer.Root != null ? step.layer.Root.frame : step.layer.frame;

        if(proxy == null)
        {
            proxy = ScriptableObject.CreateInstance<PixelUndoProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
        }
        if(position < steps.Count) steps.RemoveRange(position, steps.Count - position);
        steps.Add(step);
        if(steps.Count > MaxSteps)
        {
            steps.RemoveAt(0);
            dropped++;
        }
        position = steps.Count;

        Undo.RegisterCompleteObjectUndo(proxy, "Pixel Edit");
        proxy.position = dropped + position;
        Undo.IncrementCurrentGroup();

        if(step.layer != null && step.changes.Count > 0) PixelCanvasEditing.Shrink(step.layer);
    }

    static void OnUndoRedo()
    {
        if(current != null)
        {
            Rollback();
            current = null;
            if(Interrupted != null) Interrupted();
        }
        if(proxy == null) return;
        var target = Mathf.Clamp(proxy.position - dropped, 0, steps.Count);
        var affected = new HashSet<PixelLayer>();
        applying = true;
        while(position > target)
        {
            position--;
            Apply(steps[position], false, affected);
        }
        while(position < target)
        {
            Apply(steps[position], true, affected);
            position++;
        }
        applying = false;
        PixelCanvasEditing.Apply();
        foreach(var layer in affected)
            if(layer != null) PixelCanvasEditing.Shrink(layer);
        SceneView.RepaintAll();
    }

    static void Apply(Step step, bool redo, HashSet<PixelLayer> affected)
    {
        var layer = step.layer;
        if(layer != null && layer.Root != null && step.frame >= 0 && step.frame != layer.Root.frame && step.frame < layer.Root.FrameCount)
            PixelArtOps.SetCanvasFrame(layer.Root, step.frame, false);
        if(layer != null && layer.Root != null)
        {
            if(step.hasTrim)
            {
                if(redo) PixelCanvasEditing.Trim(layer);
                else PixelCanvasEditing.Expand(layer, step.trimBefore);
            }
            if(step.shift != Vector2Int.zero)
            {
                var s = redo ? step.shift : -step.shift;
                PixelCanvasEditing.ShiftOrigin(layer, s.x, s.y);
            }
            if(redo)
                for(var i = 0; i < step.changes.Count; i++)
                    PixelCanvasEditing.SetPixel(layer, step.changes[i].x, step.changes[i].y, step.changes[i].newColor, step.changes[i].newGlow);
            else
                for(var i = step.changes.Count - 1; i >= 0; i--)
                    PixelCanvasEditing.SetPixel(layer, step.changes[i].x, step.changes[i].y, step.changes[i].oldColor, step.changes[i].oldGlow);
            if(step.changes.Count > 0) affected.Add(layer);
        }
        if(step.hasSelection && step.selectionCanvas != null)
            PixelSelection.Set(step.selectionCanvas, redo ? step.selectionAfter : step.selectionBefore, false);
    }
}
#endif
