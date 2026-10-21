#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

[InitializeOnLoad]
public static class PixelPaintHotkeys
{
    public static bool LayerPickHeld { get; set; }

    static bool viewToolHeld;
    static bool restorePaintTool;

    static Vector2 lastPanMousePosition;
    static bool hasPanMousePosition;

    static PixelPaintHotkeys()
    {
        SceneView.beforeSceneGui += OnSceneGUI;
    }

    static PixelCanvas ActiveCanvas()
    {
        var go = Selection.activeGameObject;
        if(go == null || !go.scene.IsValid()) return null;
        return go.GetComponentInParent<PixelCanvas>();
    }

    static PixelLayer ActiveLayer()
    {
        var go = Selection.activeGameObject;
        return go != null ? go.GetComponent<PixelLayer>() : null;
    }

    static bool IsViewShortcut(Event e)
    {
        return PixelDrawerAssets.Settings.panView.Matches(e);
    }

    // The pan ends when the hotkey itself or any of its modifiers is released
    static bool IsViewRelease(Event e)
    {
        if(e.type != EventType.KeyUp) return false;

        var h = PixelDrawerAssets.Settings.panView;

        if(e.keyCode == h.key) return true;

        if(h.ctrl &&
           (e.keyCode == KeyCode.LeftControl ||
            e.keyCode == KeyCode.RightControl ||
            e.keyCode == KeyCode.LeftCommand ||
            e.keyCode == KeyCode.RightCommand))
            return true;

        if(h.shift &&
           (e.keyCode == KeyCode.LeftShift ||
            e.keyCode == KeyCode.RightShift))
            return true;

        if(h.alt &&
           (e.keyCode == KeyCode.LeftAlt ||
            e.keyCode == KeyCode.RightAlt))
            return true;

        return false;
    }

    static bool HandleViewTool(Event e, SceneView view, bool fromWindow)
    {
        if(e.type == EventType.KeyDown &&
           IsViewShortcut(e) &&
           !viewToolHeld &&
           !EditorGUIUtility.editingTextField)
        {
            restorePaintTool =
                ToolManager.activeToolType == typeof(PixelPaintTool);

            if(restorePaintTool)
            {
                viewToolHeld = true;
                hasPanMousePosition = false;

                e.Use();
                Repaint(view);
                return true;
            }
        }

        if(viewToolHeld)
        {
            if(IsViewRelease(e))
            {
                RestorePaintTool();
                e.Use();
                Repaint(view);
                return true;
            }

            if(view != null &&
               (e.type == EventType.MouseMove ||
                e.type == EventType.MouseDrag))
            {
                if(!hasPanMousePosition)
                {
                    lastPanMousePosition = e.mousePosition;
                    hasPanMousePosition = true;
                    return true;
                }

                var delta = e.mousePosition - lastPanMousePosition;
                lastPanMousePosition = e.mousePosition;

                if(delta.sqrMagnitude > 0f)
                {
                    var camera = view.camera;

                    if(camera != null)
                    {
                        float worldUnitsPerPixel;

                        if(camera.orthographic)
                        {
                            worldUnitsPerPixel =
                                camera.orthographicSize * 2f /
                                Mathf.Max(1, camera.pixelHeight);
                        }
                        else
                        {
                            float distance =
                                Vector3.Distance(camera.transform.position, view.pivot);

                            float worldHeight =
                                2f * distance *
                                Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

                            worldUnitsPerPixel =
                                worldHeight / Mathf.Max(1, camera.pixelHeight);
                        }

                        view.pivot -=
                            camera.transform.right * delta.x * worldUnitsPerPixel;

                        view.pivot +=
                            camera.transform.up * delta.y * worldUnitsPerPixel;

                        view.Repaint();
                    }
                }

                return true;
            }
        }

        return false;
    }
    static bool IsHotkeyModifier(PixelHotkey h, KeyCode k)
    {
        if(h.ctrl &&
           (k == KeyCode.LeftControl || k == KeyCode.RightControl ||
            k == KeyCode.LeftCommand || k == KeyCode.RightCommand))
            return true;

        if(h.shift &&
           (k == KeyCode.LeftShift || k == KeyCode.RightShift))
            return true;

        if(h.alt &&
           (k == KeyCode.LeftAlt || k == KeyCode.RightAlt))
            return true;

        return false;
    }
    static void UpdateModifierPick(Event e, bool active)
    {
        var p = PixelDrawerAssets.Settings.pickLayers;

        if(p.key != KeyCode.None) return;
        if(!p.ctrl && !p.shift && !p.alt) return;

        switch(e.type)
        {
            case EventType.KeyDown:
            case EventType.KeyUp:
            case EventType.MouseDown:
            case EventType.MouseDrag:
            case EventType.MouseUp:
            case EventType.MouseMove:
                break;
            default:
                return;
        }

        if(e.type == EventType.KeyUp && IsHotkeyModifier(p, e.keyCode))
        {
            LayerPickHeld = false;
            return;
        }

        LayerPickHeld =
            active &&
            (e.control || e.command) == p.ctrl &&
            e.shift == p.shift &&
            e.alt == p.alt;
    }
    
    static void RestorePaintTool()
    {
        if(!viewToolHeld) return;

        viewToolHeld = false;
        hasPanMousePosition = false;

        if(restorePaintTool)
            ToolManager.SetActiveTool<PixelPaintTool>();

        restorePaintTool = false;
    }

    static void OnSceneGUI(SceneView view)
    {
        ProcessEvent(Event.current, view, false);
    }

    public static void HandleWindowEvent(Event e)
    {
        ProcessEvent(e, null, true);
    }

    static void ProcessEvent(Event e, SceneView view, bool fromWindow)
    {
        if(e == null) return;
        
        UpdateModifierPick(
            e,
            ToolManager.activeToolType == typeof(PixelPaintTool));

        if(HandleViewTool(e, view, fromWindow))
        {
            Repaint(view);
            return;
        }

        if(e.type == EventType.KeyUp && LayerPickHeld &&
           PixelDrawerAssets.Settings.pickLayers.key != KeyCode.None)
        {
            var pick = PixelDrawerAssets.Settings.pickLayers;

            var released =
                e.keyCode == pick.key ||
                (pick.alt &&
                 (e.keyCode == KeyCode.LeftAlt ||
                  e.keyCode == KeyCode.RightAlt)) ||
                (pick.ctrl &&
                 (e.keyCode == KeyCode.LeftControl ||
                  e.keyCode == KeyCode.RightControl ||
                  e.keyCode == KeyCode.LeftCommand ||
                  e.keyCode == KeyCode.RightCommand)) ||
                (pick.shift &&
                 (e.keyCode == KeyCode.LeftShift ||
                  e.keyCode == KeyCode.RightShift));

            if(released) LayerPickHeld = false;

            return;
        }

        if(fromWindow && EditorGUIUtility.editingTextField)
            return;

        var active =
            ToolManager.activeToolType == typeof(PixelPaintTool);

        if(!fromWindow &&
           active &&
           (e.type == EventType.ValidateCommand ||
            e.type == EventType.ExecuteCommand))
        {
            HandleCommand(e, view);
            return;
        }

        if(e.type != EventType.KeyDown ||
           e.keyCode == KeyCode.None)
            return;

        var layer = ActiveLayer();
        var playCanvas = layer != null ? layer.Root : ActiveCanvas();

        if(playCanvas != null &&
           !EditorApplication.isPlaying &&
           !EditorGUIUtility.editingTextField &&
           PixelDrawerAssets.Settings.togglePlay.Matches(e))
        {
            EditorApplication.delayCall +=
                () => PixelArtPreview.Toggle(playCanvas);

            Consume(e, view);
            return;
        }

        // New layer / new frame (works with the paint tool active or not)
        if(playCanvas != null &&
           !EditorApplication.isPlaying &&
           !EditorGUIUtility.editingTextField &&
           RunCreate(playCanvas, e, PixelDrawerAssets.Settings))
        {
            Consume(e, view);
            return;
        }

        // Ctrl + arrows: previous / next frame (before the arrow handlers of Move / floating)
        if(playCanvas != null &&
           !EditorGUIUtility.editingTextField &&
           RunFrameStep(playCanvas, e, PixelDrawerAssets.Settings))
        {
            Consume(e, view);
            return;
        }

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
            if(e.keyCode == KeyCode.Return ||
               e.keyCode == KeyCode.KeypadEnter)
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
        else if(layer != null &&
                PixelPaintState.Mode == PixelPaintMode.Move &&
                !layer.locked)
        {
            int dx, dy;

            if(ArrowDelta(e, out dx, out dy) &&
               PixelSelection.For(layer.Root) == null)
            {
                PixelCanvasEditing.ShiftOrigin(layer, dx, dy);
                PixelHistory.RecordShift(layer, dx, dy);
                Consume(e, view);
                return;
            }
        }

        if(settings.brushTool.Matches(e))
            SetMode(PixelPaintMode.Brush, e, view);
        else if(settings.eraserTool.Matches(e))
            SetMode(PixelPaintMode.Eraser, e, view);
        else if(settings.fillTool.Matches(e))
            SetMode(PixelPaintMode.Fill, e, view);
        else if(settings.lineTool.Matches(e))
            SetMode(PixelPaintMode.Line, e, view);
        else if(settings.rectTool.Matches(e))
            SetMode(PixelPaintMode.Rectangle, e, view);
        else if(settings.ellipseTool.Matches(e))
            SetMode(PixelPaintMode.Ellipse, e, view);
        else if(settings.selectRectTool.Matches(e))
            SetMode(PixelPaintMode.SelectRect, e, view);
        else if(settings.lassoTool.Matches(e))
            SetMode(PixelPaintMode.SelectLasso, e, view);
        else if(settings.wandTool.Matches(e))
            SetMode(PixelPaintMode.SelectWand, e, view);
        else if(settings.moveTool.Matches(e))
            SetMode(PixelPaintMode.Move, e, view);
        else if(settings.pickLayers.Matches(e))
        {
            if(fromWindow) return;

            LayerPickHeld = true;
            PickAtMouse(e, PixelPickSource.Layers);
            Consume(e, view);
        }
        else if(settings.pickScreen.Matches(e))
        {
            if(fromWindow) return;

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
        else if(settings.prevFrame.Matches(e) ||
                settings.nextFrame.Matches(e))
        {
            var step = settings.prevFrame.Matches(e) ? -1 : 1;
            var canvas = layer != null ? layer.Root : null;

            if(canvas != null)
                EditorApplication.delayCall +=
                    () => PixelArtOps.StepFrame(canvas, step);

            Consume(e, view);
        }
        else if(layer != null && RunEdit(layer, e, settings))
        {
            Consume(e, view);
        }
    }

    static bool RunFrameStep(PixelCanvas canvas, Event e, PixelDrawerSettings s)
    {
        var step = 0;
        if(s.prevFrameArrow.Matches(e)) step = -1;
        else if(s.nextFrameArrow.Matches(e)) step = 1;
        if(step == 0) return false;

        EditorApplication.delayCall += () => PixelArtOps.StepFrame(canvas, step);
        return true;
    }

    static bool RunCreate(PixelCanvas canvas, Event e, PixelDrawerSettings s)
    {
        if(s.newLayer.Matches(e))
        {
            EditorApplication.delayCall += () =>
            {
                PixelFloating.CommitIfActive();
                PixelLayerOps.CreateLayer(canvas, null, true);
            };
            return true;
        }

        if(s.newFrame.Matches(e))
        {
            EditorApplication.delayCall += () =>
            {
                PixelFloating.CommitIfActive();
                PixelArtOps.AddFrame(canvas, false);
            };
            return true;
        }

        return false;
    }

    static bool RunEdit(PixelLayer layer, Event e, PixelDrawerSettings s)
    {
        if(s.copyMerged.Matches(e))
            PixelClipboard.Copy(layer, false, true);
        else if(s.copy.Matches(e))
            PixelClipboard.Copy(layer, false, false);
        else if(s.cut.Matches(e))
            PixelClipboard.Copy(layer, true, false);
        else if(s.pasteAsNewLayer.Matches(e))
            PixelClipboard.Paste(layer, true);
        else if(s.paste.Matches(e))
            PixelClipboard.Paste(layer, false);
        else if(s.selectAll.Matches(e))
            PixelSelection.SelectAll(layer.Root);
        else if(s.deselect.Matches(e))
            PixelSelection.Clear(layer.Root);
        else if(s.invertSelection.Matches(e))
            PixelSelection.Invert(layer.Root);
        else if(s.clearSelectionPixels.Matches(e))
            ClearSelectionPixels(layer);
        else if(s.transform.Matches(e))
        {
            if(!layer.locked)
                PixelFloating.BeginFromLayer(layer);
        }
        else
            return false;

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
            case "Copy":
                action = () => PixelClipboard.Copy(layer, false, false);
                break;

            case "Cut":
                action = () => PixelClipboard.Copy(layer, true, false);
                break;

            case "Paste":
                action = () => PixelClipboard.Paste(layer, false);
                break;

            case "SelectAll":
                action = () => PixelSelection.SelectAll(layer.Root);
                break;

            case "Duplicate":
                action = () => PixelSelection.Clear(layer.Root);
                break;

            case "Delete":
            case "SoftDelete":
                if(!PixelSelection.Has(layer.Root)) return;
                action = () => ClearSelectionPixels(layer);
                break;
        }

        if(action == null) return;

        if(e.type == EventType.ExecuteCommand)
            action();

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

    public static void CreatePatternBrush(
        PixelLayer layer,
        PixelPatternBrush.PatternKind kind)
    {
        Color32[] data;
        int w, h;

        if(!PixelClipboard.Capture(layer, false, out data, out w, out h))
        {
            EditorUtility.DisplayDialog(
                "Pixel Canvas",
                "Nothing to capture: the layer is empty.",
                "OK");
            return;
        }

        if(kind == PixelPatternBrush.PatternKind.Stamp)
            Crop(ref data, ref w, ref h);

        if(w <= 0 || h <= 0) return;

        var stamp = kind == PixelPatternBrush.PatternKind.Stamp;

        var tex = PixelDrawerAssets.CreatePatternTexture(
            stamp ? "Stamp" : "Tile", data, w, h);

        var brush = PixelDrawerAssets.CreatePatternBrush(
            stamp ? "Stamp Brush" : "Tile Brush", kind, tex);

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
            System.Array.Copy(
                data, (minY + y) * w + minX,
                result, y * nw, nw);

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
            case KeyCode.LeftArrow:
                dx = -step;
                return true;

            case KeyCode.RightArrow:
                dx = step;
                return true;

            case KeyCode.UpArrow:
                dy = step;
                return true;

            case KeyCode.DownArrow:
                dy = -step;
                return true;
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

        if(layer != null)
            PixelPaintTool.PickColorAt(layer, e.mousePosition, source);
    }

    static void Consume(Event e, SceneView view)
    {
        e.Use();
        Repaint(view);
    }

    static void Repaint(SceneView view)
    {
        if(view != null)
            view.Repaint();

        SceneView.RepaintAll();
    }
}
#endif
