#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

// Global Scene view hotkeys. Bindings come from PixelDrawerSettings.
[InitializeOnLoad]
public static class PixelPaintHotkeys
{
    // True while the "pick from layers" key is held (Alt + X by default). Alt + click then picks from layers.
    public static bool LayerPickHeld { get; set; }

    static PixelPaintHotkeys()
    {
        SceneView.duringSceneGui += OnSceneGUI;
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
        if(e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
        if(EditorGUIUtility.editingTextField) return;

        var active = ToolManager.activeToolType == typeof(PixelPaintTool);
        if(!active && !HasLayerSelected()) return;

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

        if(settings.brushTool.Matches(e))
        {
            PixelPaintState.Mode = PixelPaintMode.Brush;
            Consume(e, view);
        }
        else if(settings.eraserTool.Matches(e))
        {
            PixelPaintState.Mode = PixelPaintMode.Eraser;
            Consume(e, view);
        }
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
    }

    static void PickAtMouse(Event e, PixelPickSource source)
    {
        var go = Selection.activeGameObject;
        if(go != null) PixelPaintTool.PickColorAt(go.GetComponent<PixelLayer>(), e.mousePosition, source);
    }

    static bool HasLayerSelected()
    {
        var go = Selection.activeGameObject;
        return go != null && go.GetComponent<PixelLayer>() != null;
    }

    static void Consume(Event e, SceneView view)
    {
        e.Use();
        view.Repaint();
    }
}
#endif
