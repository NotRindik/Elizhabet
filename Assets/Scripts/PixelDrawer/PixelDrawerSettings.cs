using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class PixelHotkey
{
    public KeyCode key;
    public bool ctrl;
    public bool shift;
    public bool alt;

    public PixelHotkey(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        this.key = key;
        this.ctrl = ctrl;
        this.shift = shift;
        this.alt = alt;
    }

    public bool Matches(Event e)
    {
        if(key == KeyCode.None || e.type != EventType.KeyDown || e.keyCode != key) return false;
        return (e.control || e.command) == ctrl && e.shift == shift && e.alt == alt;
    }

    public override string ToString()
    {
        var s = "";
        if(ctrl) s += "Ctrl+";
        if(shift) s += "Shift+";
        if(alt) s += "Alt+";
        switch(key)
        {
            case KeyCode.LeftBracket: return s + "[";
            case KeyCode.RightBracket: return s + "]";
            case KeyCode.None: return "-";
            default: return s + key;
        }
    }
}

public enum PixelDuplicateMode { Ask, Linked, FullCopy }

public enum PixelStorageMode { Asset, Png }

public class PixelDrawerSettings : ScriptableObject
{
    [Header("Hotkeys (Scene view). Undo/Redo use Unity's own Ctrl+Z / Ctrl+Y.")]
    [Tooltip("Enters paint mode when a Pixel Layer is selected and paint mode is not active.")]
    public PixelHotkey enterPaintMode = new PixelHotkey(KeyCode.B);
    [Tooltip("Selects the brush while in paint mode.")]
    public PixelHotkey brushTool = new PixelHotkey(KeyCode.B);
    [Tooltip("Selects the eraser while in paint mode.")]
    public PixelHotkey eraserTool = new PixelHotkey(KeyCode.E);
    [Tooltip("Picks the color as it looks ON SCREEN under the cursor (same as Alt + click).")]
    [FormerlySerializedAs("eyedropper")]
    public PixelHotkey pickScreen = new PixelHotkey(KeyCode.I);
    [Tooltip("Picks the exact color from ALL layers of the canvas under the cursor (no scene effects). " +
             "Press it for an instant pick, or hold it together with Alt and click/drag.")]
    public PixelHotkey pickLayers = new PixelHotkey(KeyCode.X, true, false, true);
    public PixelHotkey sizeUp = new PixelHotkey(KeyCode.RightBracket);
    public PixelHotkey sizeDown = new PixelHotkey(KeyCode.LeftBracket);

    [Header("Tools")]
    public PixelHotkey fillTool = new PixelHotkey(KeyCode.G);
    public PixelHotkey lineTool = new PixelHotkey(KeyCode.L);
    public PixelHotkey rectTool = new PixelHotkey(KeyCode.U);
    public PixelHotkey ellipseTool = new PixelHotkey(KeyCode.O);
    public PixelHotkey selectRectTool = new PixelHotkey(KeyCode.M);
    public PixelHotkey lassoTool = new PixelHotkey(KeyCode.Q);
    public PixelHotkey wandTool = new PixelHotkey(KeyCode.R);
    public PixelHotkey moveTool = new PixelHotkey(KeyCode.V);

    [Header("Animation (canvas frames)")]
    public PixelHotkey prevFrame = new PixelHotkey(KeyCode.Comma);
    public PixelHotkey nextFrame = new PixelHotkey(KeyCode.Period);
    [Tooltip("Alternative key for the previous frame. Works with or without the paint tool.")]
    public PixelHotkey prevFrameArrow = new PixelHotkey(KeyCode.LeftArrow, true);
    [Tooltip("Alternative key for the next frame. Works with or without the paint tool.")]
    public PixelHotkey nextFrameArrow = new PixelHotkey(KeyCode.RightArrow, true);
    [Tooltip("Plays or pauses the canvas animation in the editor (Scene view, outside play mode).")]
    public PixelHotkey togglePlay = new PixelHotkey(KeyCode.Space);
    [Tooltip("Hold Ctrl (Cmd on Mac) and click a pixel to select the layer that owns it. Not used by the selection tools, where Ctrl subtracts.")]
    public bool ctrlClickSelectsLayer = true;

    [Header("View")]
    [Tooltip("Hold to pan the Scene view just by moving the mouse (no click needed) while the Pixel Paint tool is active. Release to go back to painting.")]
    public PixelHotkey panView = new PixelHotkey(KeyCode.Space, true);

    [Header("Creating (layers and frames)")]
    [Tooltip("Creates a new empty layer on the active canvas.")]
    public PixelHotkey newLayer = new PixelHotkey(KeyCode.N, true, true);
    [Tooltip("Adds a new blank frame to the active canvas timeline.")]
    public PixelHotkey newFrame = new PixelHotkey(KeyCode.N, true, false, true);

    [Header("Selection and clipboard (only intercepted while the Pixel Paint tool is active)")]
    public PixelHotkey copy = new PixelHotkey(KeyCode.C, true);
    public PixelHotkey cut = new PixelHotkey(KeyCode.X, true);
    public PixelHotkey paste = new PixelHotkey(KeyCode.V, true);
    public PixelHotkey pasteAsNewLayer = new PixelHotkey(KeyCode.V, true, true);
    public PixelHotkey copyMerged = new PixelHotkey(KeyCode.C, true, true);
    public PixelHotkey selectAll = new PixelHotkey(KeyCode.A, true);
    public PixelHotkey deselect = new PixelHotkey(KeyCode.D, true);
    public PixelHotkey invertSelection = new PixelHotkey(KeyCode.I, true, true);
    public PixelHotkey clearSelectionPixels = new PixelHotkey(KeyCode.Delete);
    public PixelHotkey transform = new PixelHotkey(KeyCode.T, true);

    [Header("Duplicating layers and canvases (Ctrl+D in the Hierarchy)")]
    [Tooltip("Linked: the copy points to the same PNG, painting changes every copy.\nFull Copy: the copy gets its own PNG files.\nAsk: choose every time.")]
    public PixelDuplicateMode duplicateMode = PixelDuplicateMode.Ask;

    [Header("Storage")]
    [Tooltip("What new layers are saved as.\nAsset: one Pixel Art asset per layer (frames and glow inside).\nPng: one PNG file per layer (no animation, no glow). Existing layers keep their storage until converted.")]
    public PixelStorageMode storage = PixelStorageMode.Asset;

    [Header("Saving")]
    [Tooltip("Writes all pending layer PNGs when the Pixel Paint tool is deactivated, in addition to saving with the scene.")]
    public bool saveOnToolExit = true;
}
