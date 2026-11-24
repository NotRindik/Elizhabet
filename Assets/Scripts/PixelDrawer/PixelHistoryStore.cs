#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelHistoryStore : ScriptableObject
{
    [Serializable]
    public class StoredMask
    {
        public bool present;
        public int x;
        public int y;
        public int w;
        public int h;
        public bool outside;
        public bool[] bits;
    }

    [Serializable]
    public class StoredStep
    {
        public PixelLayer layer;
        public int frame;
        public int[] xs;
        public int[] ys;
        public Color32[] oldColors;
        public Color32[] newColors;
        public byte[] oldGlows;
        public byte[] newGlows;
        public bool hasSelection;
        public PixelCanvas selectionCanvas;
        public StoredMask selectionBefore;
        public StoredMask selectionAfter;
        public Vector2Int shift;
        public bool hasTrim;
        public RectInt trimBefore;
    }

    public List<StoredStep> steps = new List<StoredStep>();
    public PixelUndoProxy proxy;
    public int position;
    public int dropped;
    public int strokeId;
}
#endif
