#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelTempView
{
    class RefView
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Sprite sprite;
        public Texture2D texture;
        public float ppu;
    }

    static readonly Dictionary<PixelCanvas, RefView> references = new Dictionary<PixelCanvas, RefView>();
    static readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
    static PixelLayer tileOwner;
    static int tileCount;

    static PixelTempView()
    {
        EditorApplication.delayCall += DestroyAllMarkers;
        EditorApplication.playModeStateChanged += state =>
        {
            if(state == PlayModeStateChange.ExitingEditMode) DestroyAllMarkers();
            else if(state == PlayModeStateChange.EnteredEditMode) PixelCanvasSync.Request();
        };
    }

    public static SpriteRenderer CreateRenderer(Transform parent, string name, string kind, int order)
    {
        var go = new GameObject(name, typeof(SpriteRenderer), typeof(PixelTempMarker));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.GetComponent<PixelTempMarker>().kind = kind;
        go.transform.SetParent(parent, false);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }

    public static void DestroyAllMarkers()
    {
        ClearTiles();
        references.Clear();
        var markers = Resources.FindObjectsOfTypeAll<PixelTempMarker>();
        for(var i = 0; i < markers.Length; i++)
        {
            var m = markers[i];
            if(m == null || EditorUtility.IsPersistent(m)) continue;
            if(m.kind == "float" && PixelFloating.Active) continue;
            Object.DestroyImmediate(m.gameObject);
        }
        if(!EditorApplication.isPlayingOrWillChangePlaymode) PixelCanvasSync.Request();
    }

    public static void SyncReferences()
    {
        var playing = EditorApplication.isPlayingOrWillChangePlaymode;
        var canvases = PixelCanvasSync.Canvases();
        var live = new HashSet<PixelCanvas>();
        for(var i = 0; i < canvases.Count; i++)
        {
            var canvas = canvases[i];
            if(playing || canvas.reference == null || !canvas.referenceVisible) continue;
            live.Add(canvas);
            UpdateReference(canvas);
        }
        var stale = new List<PixelCanvas>();
        foreach(var pair in references)
            if(pair.Key == null || !live.Contains(pair.Key)) stale.Add(pair.Key);
        for(var i = 0; i < stale.Count; i++)
        {
            DestroyView(references[stale[i]]);
            references.Remove(stale[i]);
        }
    }

    static void UpdateReference(PixelCanvas canvas)
    {
        RefView view;
        if(!references.TryGetValue(canvas, out view) || view.go == null)
        {
            view = new RefView();
            view.sr = CreateRenderer(canvas.transform, "PixelReference", "reference", -10000);
            view.go = view.sr.gameObject;
            references[canvas] = view;
        }
        var tex = canvas.reference;
        var ppu = canvas.pixelsPerUnit / Mathf.Max(0.01f, canvas.referenceScale);
        if(view.sprite == null || view.texture != tex || !Mathf.Approximately(view.ppu, ppu))
        {
            if(view.sprite != null) Object.DestroyImmediate(view.sprite);
            view.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, ppu);
            view.sprite.hideFlags = HideFlags.HideAndDontSave;
            view.texture = tex;
            view.ppu = ppu;
        }
        view.sr.sprite = view.sprite;
        view.sr.color = new Color(1, 1, 1, canvas.referenceOpacity);
        view.go.transform.localPosition = new Vector3(canvas.referenceOffset.x, canvas.referenceOffset.y, 0) / canvas.pixelsPerUnit;
    }

    static void DestroyView(RefView view)
    {
        if(view.go != null) Object.DestroyImmediate(view.go);
        if(view.sprite != null) Object.DestroyImmediate(view.sprite);
    }

    public static void SyncTiles(PixelLayer layer, bool enable)
    {
        var canvas = layer.Root;
        var sr0 = layer.Surface(false);
        var sprite = sr0 != null ? sr0.sprite : null;
        if(!enable || canvas == null || canvas.tileMode == PixelCanvas.TileMode.Off || sprite == null)
        {
            ClearTiles();
            return;
        }
        if(tileOwner != layer) ClearTiles();
        tileOwner = layer;

        var offsets = new List<Vector2Int>();
        for(var dx = -1; dx <= 1; dx++)
        {
            for(var dy = -1; dy <= 1; dy++)
            {
                if(dx == 0 && dy == 0) continue;
                if(dx != 0 && !canvas.TileX) continue;
                if(dy != 0 && !canvas.TileY) continue;
                offsets.Add(new Vector2Int(dx, dy));
            }
        }
        if(tileCount != offsets.Count)
        {
            ClearTiles();
            tileOwner = layer;
            for(var i = 0; i < offsets.Count; i++) tiles.Add(CreateRenderer(layer.transform, "PixelTile", "tile", layer.sortIndex));
            tileCount = offsets.Count;
        }
        var ppu = canvas.pixelsPerUnit;
        for(var i = 0; i < tiles.Count; i++)
        {
            if(tiles[i] == null) continue;
            tiles[i].sprite = sprite;
            tiles[i].sortingOrder = layer.sortIndex;
            tiles[i].color = new Color(1, 1, 1, layer.opacity);
            tiles[i].transform.localPosition = sr0.transform.localPosition
                + new Vector3(offsets[i].x * canvas.tileSize.x, offsets[i].y * canvas.tileSize.y, 0) / ppu;
        }
    }

    public static void ClearTiles()
    {
        for(var i = 0; i < tiles.Count; i++)
            if(tiles[i] != null) Object.DestroyImmediate(tiles[i].gameObject);
        tiles.Clear();
        tileCount = 0;
        tileOwner = null;
    }
}
#endif
