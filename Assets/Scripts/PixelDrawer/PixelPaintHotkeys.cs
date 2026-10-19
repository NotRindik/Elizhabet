#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

[InitializeOnLoad]
public static class PixelPaintHotkeys
{
    public static bool LayerPickHeld { get; set; }

    static PixelPaintHotkeys()
    {
        SceneView.beforeSceneGui += OnSceneGUI;
    }

    static PixelLayer ActiveLayer()
    {
        var go = Selection.activeGameObject;
        return go != null ? go.GetComponent<PixelLayer>() : null;
    }

    static void OnSceneGUI(SceneView view)
    {
        
        var e = Event.current;
        if(e.type == EventType.KeyUp && LayerPickHeld)
        {
            if(e.keyCode == KeyCode.LeftAlt || e.keyCode == KeyCode.RightAlt || e.keyCode == PixelDrawerAssets.Settings.pickLayers.key)
                LayerPickHeld = false;
            return;
        }

        var active = ToolManager.activeToolType == typeof(PixelPaintTool);
        if(active && (e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand))
        {
            HandleCommand(e, view);
            return;
        }

        if(e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
//        if(EditorGUIUtility.editingTextField) return;

        var layer = ActiveLayer();
        if(!active && layer == null) return;

        var settings = PixelDrawerAssets.Settings;

        if(!active)
        {
            if(settings.enterPaintMode.Matches(e))
            {
                PixelPaintState.Mode = PixelPaintMode.Brush;
                ToolManager.SetActiveTool<PixelPaintTool>();
                Consume(e, view);
            }
            return;
        }

        if(PixelFloating.Active)
        {
            if(e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                PixelFloating.Commit();
                Consume(e, view);
                return;
            }
            if(e.keyCode == KeyCode.Escape)
            {
                PixelFloating.Cancel();
                Consume(e, view);
                return;
            }
            int fdx, fdy;
            if(ArrowDelta(e, out fdx, out fdy))
            {
                PixelFloating.Nudge(fdx, fdy);
                Consume(e, view);
                return;
            }
        }
        else if(layer != null && PixelPaintState.Mode == PixelPaintMode.Move && !layer.locked)
        {
            int dx, dy;
            if(ArrowDelta(e, out dx, out dy) && PixelSelection.For(layer.Root) == null)
            {
                PixelCanvasEditing.ShiftOrigin(layer, dx, dy);
                PixelHistory.RecordShift(layer, dx, dy);
                Consume(e, view);
                return;
            }
        }

        if(settings.brushTool.Matches(e)) SetMode(PixelPaintMode.Brush, e, view);
        else if(settings.eraserTool.Matches(e)) SetMode(PixelPaintMode.Eraser, e, view);
        else if(settings.fillTool.Matches(e)) SetMode(PixelPaintMode.Fill, e, view);
        else if(settings.lineTool.Matches(e)) SetMode(PixelPaintMode.Line, e, view);
        else if(settings.rectTool.Matches(e)) SetMode(PixelPaintMode.Rectangle, e, view);
        else if(settings.ellipseTool.Matches(e)) SetMode(PixelPaintMode.Ellipse, e, view);
        else if(settings.selectRectTool.Matches(e)) SetMode(PixelPaintMode.SelectRect, e, view);
        else if(settings.lassoTool.Matches(e)) SetMode(PixelPaintMode.SelectLasso, e, view);
        else if(settings.wandTool.Matches(e)) SetMode(PixelPaintMode.SelectWand, e, view);
        else if(settings.moveTool.Matches(e)) SetMode(PixelPaintMode.Move, e, view);
        else if(settings.pickLayers.Matches(e))
        {
            LayerPickHeld = true;
            PickAtMouse(e, PixelPickSource.Layers);
            Consume(e, view);
        }
        else if(settings.pickScreen.Matches(e))
        {
            PickAtMouse(e, PixelPickSource.Screen);
            Consume(e, view);
        }
        else if(settings.sizeUp.Matches(e))
        {
            PixelPaintState.Size++;
            Consume(e, view);
        }
        else if(settings.sizeDown.Matches(e))
        {
            PixelPaintState.Size--;
            Consume(e, view);
        }
        else if(layer != null && RunEdit(layer, e, settings))
        {
            Consume(e, view);
        }
    }

    static bool RunEdit(PixelLayer layer, Event e, PixelDrawerSettings s)
    {
        if(s.copyMerged.Matches(e)) PixelClipboard.Copy(layer, false, true);
        else if(s.copy.Matches(e)) PixelClipboard.Copy(layer, false, false);
        else if(s.cut.Matches(e)) PixelClipboard.Copy(layer, true, false);
        else if(s.pasteAsNewLayer.Matches(e)) PixelClipboard.Paste(layer, true);
        else if(s.paste.Matches(e)) PixelClipboard.Paste(layer, false);
        else if(s.selectAll.Matches(e)) PixelSelection.SelectAll(layer.Root);
        else if(s.deselect.Matches(e)) PixelSelection.Clear(layer.Root);
        else if(s.invertSelection.Matches(e)) PixelSelection.Invert(layer.Root);
        else if(s.clearSelectionPixels.Matches(e)) ClearSelectionPixels(layer);
        else if(s.transform.Matches(e))
        {
            if(!layer.locked) PixelFloating.BeginFromLayer(layer);
        }
        else return false;
        return true;
    }

    static void HandleCommand(Event e, SceneView view)
    {
        var layer = ActiveLayer();
        if(layer == null || layer.Root == null) return;
        var name = e.commandName;
        System.Action action = null;
        switch(name)
        {
            case "Copy": action = () => PixelClipboard.Copy(layer, false, false); break;
            case "Cut": action = () => PixelClipboard.Copy(layer, true, false); break;
            case "Paste": action = () => PixelClipboard.Paste(layer, false); break;
            case "SelectAll": action = () => PixelSelection.SelectAll(layer.Root); break;
            case "Duplicate": action = () => PixelSelection.Clear(layer.Root); break;
            case "Delete":
            case "SoftDelete":
                if(!PixelSelection.Has(layer.Root)) return;
                action = () => ClearSelectionPixels(layer);
                break;
        }
        if(action == null) return;
        if(e.type == EventType.ExecuteCommand) action();
        Consume(e, view);
    }

    public static void ClearSelectionPixels(PixelLayer layer)
    {
        if(layer == null || layer.locked) return;
        PixelFloating.CommitIfActive();
        PixelHistory.BeginStroke(layer);
        PixelOps.ClearSelection(layer);
        PixelCanvasEditing.Apply();
        PixelHistory.EndStroke();
        SceneView.RepaintAll();
    }

    public static void CreatePatternBrush(PixelLayer layer, PixelPatternBrush.PatternKind kind)
    {
        Color32[] data;
        int w, h;
        if(!PixelClipboard.Capture(layer, false, out data, out w, out h))
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "Nothing to capture: the layer is empty.", "OK");
            return;
        }
        if(kind == PixelPatternBrush.PatternKind.Stamp) Crop(ref data, ref w, ref h);
        if(w <= 0 || h <= 0) return;
        var tex = PixelDrawerAssets.CreatePatternTexture(kind == PixelPatternBrush.PatternKind.Stamp ? "Stamp" : "Tile", data, w, h);
        var brush = PixelDrawerAssets.CreatePatternBrush(kind == PixelPatternBrush.PatternKind.Stamp ? "Stamp Brush" : "Tile Brush", kind, tex);
        PixelPaintState.Brush = brush;
        PixelPaintState.Mode = PixelPaintMode.Brush;
        EditorGUIUtility.PingObject(brush);
    }

    static void Crop(ref Color32[] data, ref int w, ref int h)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for(var y = 0; y < h; y++)
        {
            for(var x = 0; x < w; x++)
            {
                if(data[y * w + x].a == 0) continue;
                if(x < minX) minX = x;
                if(x > maxX) maxX = x;
                if(y < minY) minY = y;
                if(y > maxY) maxY = y;
            }
        }
        if(maxX < 0)
        {
            w = 0;
            h = 0;
            return;
        }
        var nw = maxX - minX + 1;
        var nh = maxY - minY + 1;
        var result = new Color32[nw * nh];
        for(var y = 0; y < nh; y++)
            System.Array.Copy(data, (minY + y) * w + minX, result, y * nw, nw);
        data = result;
        w = nw;
        h = nh;
    }

    static bool ArrowDelta(Event e, out int dx, out int dy)
    {
        dx = 0;
        dy = 0;
        var step = e.shift ? 8 : 1;
        switch(e.keyCode)
        {
            case KeyCode.LeftArrow: dx = -step; return true;
            case KeyCode.RightArrow: dx = step; return true;
            case KeyCode.UpArrow: dy = step; return true;
            case KeyCode.DownArrow: dy = -step; return true;
        }
        return false;
    }

    static void SetMode(PixelPaintMode mode, Event e, SceneView view)
    {
        PixelPaintState.Mode = mode;
        Consume(e, view);
    }

    static void PickAtMouse(Event e, PixelPickSource source)
    {
        var layer = ActiveLayer();
        if(layer != null) PixelPaintTool.PickColorAt(layer, e.mousePosition, source);
    }

    static void Consume(Event e, SceneView view)
    {
        e.Use();
        view.Repaint();
    }
}
#endif
