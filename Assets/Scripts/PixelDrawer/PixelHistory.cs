#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelHistory
{
    const int MaxSteps = 200;

    class UndoProxy : ScriptableObject
    {
        public int position;
    }

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
    static UndoProxy proxy;
    static Step current;
    static int position;
    static bool applying;

    public static System.Action Interrupted;

    static PixelHistory()
    {
        PixelCanvasEditing.PixelChanged += OnPixelChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        AssemblyReloadEvents.beforeAssemblyReload += () => EndStroke();
    }

    public static bool IsOpen
    {
        get { return current != null; }
    }

    public static void BeginStroke(PixelLayer layer)
    {
        if(current != null) EndStroke();
        current = new Step { layer = layer };
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
        PixelCanvasEditing.Apply();
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

        if(proxy == null) proxy = ScriptableObject.CreateInstance<UndoProxy>();
        if(position < steps.Count) steps.RemoveRange(position, steps.Count - position);
        steps.Add(step);
        if(steps.Count > MaxSteps) steps.RemoveAt(0);
        position = steps.Count;

        Undo.RegisterCompleteObjectUndo(proxy, "Pixel Edit");
        proxy.position = position;
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
        var target = Mathf.Clamp(proxy.position, 0, steps.Count);
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
