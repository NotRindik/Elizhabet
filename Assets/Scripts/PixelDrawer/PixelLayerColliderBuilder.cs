#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PixelLayerColliderBuilder
{
    const int BoxWarnLimit = 2000;

    struct Edge
    {
        public int sx, sy, ex, ey, dir;

        public Edge(int sx, int sy, int ex, int ey, int dir)
        {
            this.sx = sx;
            this.sy = sy;
            this.ex = ex;
            this.ey = ey;
            this.dir = dir;
        }
    }

    static PixelLayerColliderBuilder()
    {
        EditorSceneManager.sceneSaving += (scene, path) => RebuildAuto(scene, false);
        EditorApplication.playModeStateChanged += state =>
        {
            if(state == PlayModeStateChange.ExitingEditMode) RebuildAuto(default(Scene), true);
        };
    }

    static void RebuildAuto(Scene scene, bool everywhere)
    {
        var found = Resources.FindObjectsOfTypeAll<PixelLayerCollider>();
        var any = false;
        for(var i = 0; i < found.Length; i++)
        {
            var c = found[i];
            if(c == null || EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid() || !c.autoRebuild) continue;
            if(!everywhere && c.gameObject.scene != scene) continue;
            if(!any) PixelFloating.CommitIfActive();
            any = true;
            Rebuild(c, false, false);
        }
        if(any) PixelCanvasEditing.SaveAll();
    }

    public static bool Rebuild(PixelLayerCollider target, bool force, bool undo)
    {
        var layer = target.GetComponent<PixelLayer>();
        Vector2Int origin;
        int w, h;
        var pixels = PixelCanvasEditing.ReadPixels(layer, out origin, out w, out h);
        var ppu = layer.Root != null ? layer.Root.pixelsPerUnit : 16f;
        var hash = ComputeHash(target, pixels, origin, w, h, ppu);
        var child = target.transform.Find(PixelLayerCollider.ChildName);
        if(!force && child != null && hash == target.builtHash) return false;

        if(undo) Undo.RecordObject(target, "Rebuild Pixel Collider");
        if(child == null) child = CreateChild(target, undo);
        var go = child.gameObject;
        Clear(go, target.composite, undo);

        if(target.composite && go.GetComponent<CompositeCollider2D>() == null)
        {
            Add<CompositeCollider2D>(go, undo);
            var rb = go.GetComponent<Rigidbody2D>();
            if(rb != null) rb.bodyType = RigidbodyType2D.Static;
        }
        var comp = go.GetComponent<CompositeCollider2D>();
        if(comp != null) comp.isTrigger = target.isTrigger;

        if(pixels != null && w > 0 && h > 0)
        {
            int gw, gh;
            var mask = BuildMask(pixels, w, h, target.alphaThreshold, target.cellSize, target.cellCoverage, out gw, out gh);
            if(target.minIslandCells > 1) RemoveIslands(mask, gw, gh, target.minIslandCells);
            if(target.mode == PixelLayerCollider.Mode.Polygon) MakePolygon(go, target, mask, gw, gh, origin, w, h, ppu, undo);
            else MakeBoxes(go, target, mask, gw, gh, origin, w, h, ppu, undo);
        }

        target.builtHash = hash;
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(go);
        if(undo) EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        return true;
    }

    public static void ClearAll(PixelLayerCollider target)
    {
        var child = target.transform.Find(PixelLayerCollider.ChildName);
        if(child == null) return;
        Undo.RecordObject(target, "Clear Pixel Collider");
        Clear(child.gameObject, false, true);
        Undo.DestroyObjectImmediate(child.gameObject);
        target.builtHash = 0;
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
    }

    static void MakePolygon(GameObject go, PixelLayerCollider t, bool[] mask, int gw, int gh, Vector2Int origin, int w, int h, float ppu, bool undo)
    {
        var loops = Trace(mask, gw, gh);
        var paths = new List<Vector2[]>();
        for(var i = 0; i < loops.Count; i++)
        {
            var corners = RemoveCollinear(loops[i]);
            if(corners.Count < 3) continue;
            var pts = new List<Vector2>(corners.Count);
            for(var k = 0; k < corners.Count; k++)
            {
                var px = Mathf.Min(corners[k].x * t.cellSize, w) + origin.x;
                var py = Mathf.Min(corners[k].y * t.cellSize, h) + origin.y;
                pts.Add(new Vector2(px, py));
            }
            pts = Simplify(pts, t.simplify);
            if(pts.Count < 3) continue;
            var path = new Vector2[pts.Count];
            for(var k = 0; k < path.Length; k++) path[k] = pts[k] / ppu;
            paths.Add(path);
        }
        if(paths.Count == 0) return;
        var poly = Add<PolygonCollider2D>(go, undo);
        Setup(poly, t);
        poly.pathCount = paths.Count;
        for(var i = 0; i < paths.Count; i++) poly.SetPath(i, paths[i]);
    }

    static void MakeBoxes(GameObject go, PixelLayerCollider t, bool[] mask, int gw, int gh, Vector2Int origin, int w, int h, float ppu, bool undo)
    {
        var rects = GreedyRects(mask, gw, gh);
        if(rects.Count > BoxWarnLimit) Debug.LogWarning("PixelLayerCollider: " + rects.Count + " boxes on " + t.name + ". Raise Cell Size or use Polygon mode.", t);
        for(var i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            var x0 = r.xMin * t.cellSize;
            var y0 = r.yMin * t.cellSize;
            var x1 = Mathf.Min(r.xMax * t.cellSize, w);
            var y1 = Mathf.Min(r.yMax * t.cellSize, h);
            var box = Add<BoxCollider2D>(go, undo);
            Setup(box, t);
            box.offset = new Vector2(origin.x + (x0 + x1) * 0.5f, origin.y + (y0 + y1) * 0.5f) / ppu;
            box.size = new Vector2(x1 - x0, y1 - y0) / ppu;
        }
    }

    static void Setup(Collider2D c, PixelLayerCollider t)
    {
        c.gameObject.layer = Mathf.RoundToInt(Mathf.Log(t.layerMask.value, 2));
        c.isTrigger = t.isTrigger;
        c.sharedMaterial = t.material;
        c.compositeOperation = t.composite ? Collider2D.CompositeOperation.Merge : Collider2D.CompositeOperation.None;
    }

    static Transform CreateChild(PixelLayerCollider t, bool undo)
    {
        var go = new GameObject(PixelLayerCollider.ChildName);
        if(undo) Undo.RegisterCreatedObjectUndo(go, "Rebuild Pixel Collider");
        go.transform.SetParent(t.transform, false);
        go.layer = t.gameObject.layer;
        return go.transform;
    }

    static void Clear(GameObject go, bool keepComposite, bool undo)
    {
        var cols = go.GetComponents<Collider2D>();
        for(var i = 0; i < cols.Length; i++)
        {
            if(keepComposite && cols[i] is CompositeCollider2D) continue;
            Destroy(cols[i], undo);
        }
        if(keepComposite) return;
        var rb = go.GetComponent<Rigidbody2D>();
        if(rb != null) Destroy(rb, undo);
    }

    static void Destroy(Object o, bool undo)
    {
        if(undo) Undo.DestroyObjectImmediate(o);
        else Object.DestroyImmediate(o);
    }

    static T Add<T>(GameObject go, bool undo) where T : Component
    {
        return undo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
    }

    static bool[] BuildMask(Color32[] pixels, int w, int h, int threshold, int cell, float coverage, out int gw, out int gh)
    {
        gw = (w + cell - 1) / cell;
        gh = (h + cell - 1) / cell;
        var mask = new bool[gw * gh];
        for(var gy = 0; gy < gh; gy++)
        {
            for(var gx = 0; gx < gw; gx++)
            {
                var x0 = gx * cell;
                var y0 = gy * cell;
                var x1 = Mathf.Min(x0 + cell, w);
                var y1 = Mathf.Min(y0 + cell, h);
                var solid = 0;
                for(var y = y0; y < y1; y++)
                    for(var x = x0; x < x1; x++)
                        if(pixels[y * w + x].a >= threshold) solid++;
                var area = (x1 - x0) * (y1 - y0);
                mask[gy * gw + gx] = solid > 0 && solid >= area * coverage;
            }
        }
        return mask;
    }

    static void RemoveIslands(bool[] mask, int gw, int gh, int minCells)
    {
        var seen = new bool[mask.Length];
        var stack = new Stack<int>();
        var comp = new List<int>();
        for(var i = 0; i < mask.Length; i++)
        {
            if(!mask[i] || seen[i]) continue;
            comp.Clear();
            seen[i] = true;
            stack.Push(i);
            while(stack.Count > 0)
            {
                var c = stack.Pop();
                comp.Add(c);
                var x = c % gw;
                var y = c / gw;
                if(x > 0) Visit(c - 1, mask, seen, stack);
                if(x < gw - 1) Visit(c + 1, mask, seen, stack);
                if(y > 0) Visit(c - gw, mask, seen, stack);
                if(y < gh - 1) Visit(c + gw, mask, seen, stack);
            }
            if(comp.Count >= minCells) continue;
            for(var k = 0; k < comp.Count; k++) mask[comp[k]] = false;
        }
    }

    static void Visit(int i, bool[] mask, bool[] seen, Stack<int> stack)
    {
        if(!mask[i] || seen[i]) return;
        seen[i] = true;
        stack.Push(i);
    }

    static List<RectInt> GreedyRects(bool[] mask, int gw, int gh)
    {
        var used = new bool[mask.Length];
        var rects = new List<RectInt>();
        for(var y = 0; y < gh; y++)
        {
            for(var x = 0; x < gw; x++)
            {
                var i = y * gw + x;
                if(!mask[i] || used[i]) continue;
                var rw = 1;
                while(x + rw < gw && mask[i + rw] && !used[i + rw]) rw++;
                var rh = 1;
                while(y + rh < gh && RowFree(mask, used, (y + rh) * gw + x, rw)) rh++;
                for(var yy = y; yy < y + rh; yy++)
                    for(var xx = x; xx < x + rw; xx++)
                        used[yy * gw + xx] = true;
                rects.Add(new RectInt(x, y, rw, rh));
            }
        }
        return rects;
    }

    static bool RowFree(bool[] mask, bool[] used, int start, int count)
    {
        for(var i = 0; i < count; i++)
            if(!mask[start + i] || used[start + i]) return false;
        return true;
    }

    static bool Solid(bool[] mask, int gw, int gh, int x, int y)
    {
        return x >= 0 && y >= 0 && x < gw && y < gh && mask[y * gw + x];
    }

    static long Key(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }

    static List<List<Vector2Int>> Trace(bool[] mask, int gw, int gh)
    {
        var edges = new List<Edge>();
        for(var y = 0; y < gh; y++)
        {
            for(var x = 0; x < gw; x++)
            {
                if(!mask[y * gw + x]) continue;
                if(!Solid(mask, gw, gh, x, y - 1)) edges.Add(new Edge(x, y, x + 1, y, 0));
                if(!Solid(mask, gw, gh, x + 1, y)) edges.Add(new Edge(x + 1, y, x + 1, y + 1, 1));
                if(!Solid(mask, gw, gh, x, y + 1)) edges.Add(new Edge(x + 1, y + 1, x, y + 1, 2));
                if(!Solid(mask, gw, gh, x - 1, y)) edges.Add(new Edge(x, y + 1, x, y, 3));
            }
        }

        var outA = new Dictionary<long, int>(edges.Count);
        var outB = new Dictionary<long, int>();
        for(var i = 0; i < edges.Count; i++)
        {
            var key = Key(edges[i].sx, edges[i].sy);
            if(!outA.ContainsKey(key)) outA[key] = i;
            else outB[key] = i;
        }

        var used = new bool[edges.Count];
        var loops = new List<List<Vector2Int>>();
        for(var s = 0; s < edges.Count; s++)
        {
            if(used[s]) continue;
            var loop = new List<Vector2Int>();
            var cur = s;
            while(true)
            {
                var e = edges[cur];
                used[cur] = true;
                loop.Add(new Vector2Int(e.sx, e.sy));
                var next = Next(edges, outA, outB, used, e, s);
                if(next < 0 || next == s) break;
                cur = next;
            }
            loops.Add(loop);
        }
        return loops;
    }

    static int Next(List<Edge> edges, Dictionary<long, int> outA, Dictionary<long, int> outB, bool[] used, Edge e, int start)
    {
        var key = Key(e.ex, e.ey);
        var best = -1;
        var bestScore = -1;
        int index;
        if(outA.TryGetValue(key, out index)) Consider(edges, index, e.dir, used, start, ref best, ref bestScore);
        if(outB.TryGetValue(key, out index)) Consider(edges, index, e.dir, used, start, ref best, ref bestScore);
        return best;
    }

    static void Consider(List<Edge> edges, int index, int dir, bool[] used, int start, ref int best, ref int bestScore)
    {
        if(used[index] && index != start) return;
        var turn = (edges[index].dir - dir + 4) % 4;
        var score = turn == 1 ? 3 : turn == 0 ? 2 : turn == 3 ? 1 : 0;
        if(score <= bestScore) return;
        best = index;
        bestScore = score;
    }

    static List<Vector2Int> RemoveCollinear(List<Vector2Int> loop)
    {
        var result = new List<Vector2Int>();
        var n = loop.Count;
        for(var i = 0; i < n; i++)
        {
            var prev = loop[(i + n - 1) % n];
            var cur = loop[i];
            var next = loop[(i + 1) % n];
            var cross = (cur.x - prev.x) * (next.y - cur.y) - (cur.y - prev.y) * (next.x - cur.x);
            if(cross != 0) result.Add(cur);
        }
        return result;
    }

    static List<Vector2> Simplify(List<Vector2> pts, float tolerance)
    {
        var n = pts.Count;
        if(tolerance <= 0 || n < 5) return pts;
        var far = 1;
        var farDist = 0f;
        for(var i = 1; i < n; i++)
        {
            var d = (pts[i] - pts[0]).sqrMagnitude;
            if(d <= farDist) continue;
            farDist = d;
            far = i;
        }
        var keep = new bool[n];
        keep[0] = true;
        keep[far] = true;
        Reduce(pts, 0, far, tolerance, keep);
        Reduce(pts, far, n, tolerance, keep);
        var result = new List<Vector2>();
        for(var i = 0; i < n; i++)
            if(keep[i]) result.Add(pts[i]);
        return result;
    }

    static void Reduce(List<Vector2> pts, int a, int b, float tolerance, bool[] keep)
    {
        var pa = pts[a];
        var pb = pts[b % pts.Count];
        var maxDist = 0f;
        var index = -1;
        for(var i = a + 1; i < b; i++)
        {
            var d = DistToSegment(pts[i], pa, pb);
            if(d <= maxDist) continue;
            maxDist = d;
            index = i;
        }
        if(index < 0 || maxDist <= tolerance) return;
        keep[index] = true;
        Reduce(pts, a, index, tolerance, keep);
        Reduce(pts, index, b, tolerance, keep);
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var len = ab.sqrMagnitude;
        if(len <= 0) return (p - a).magnitude;
        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
        return (p - (a + ab * t)).magnitude;
    }

    static uint Mix(uint hash, int value)
    {
        return (hash ^ (uint)value) * 16777619u;
    }

    static int ComputeHash(PixelLayerCollider t, Color32[] pixels, Vector2Int origin, int w, int h, float ppu)
    {
        var hash = 2166136261u;
        hash = Mix(hash, (int)t.mode);
        hash = Mix(hash, t.alphaThreshold);
        hash = Mix(hash, t.cellSize);
        hash = Mix(hash, Mathf.RoundToInt(t.cellCoverage * 1000));
        hash = Mix(hash, Mathf.RoundToInt(t.simplify * 1000));
        hash = Mix(hash, t.minIslandCells);
        hash = Mix(hash, t.isTrigger ? 1 : 0);
        hash = Mix(hash, t.composite ? 1 : 0);
        hash = Mix(hash, t.material != null ? t.material.GetInstanceID() : 0);
        hash = Mix(hash, Mathf.RoundToInt(ppu * 1000));
        hash = Mix(hash, origin.x);
        hash = Mix(hash, origin.y);
        hash = Mix(hash, w);
        hash = Mix(hash, h);
        if(pixels == null) return (int)hash;
        var bits = 0;
        var count = 0;
        for(var i = 0; i < pixels.Length; i++)
        {
            bits = (bits << 1) | (pixels[i].a >= t.alphaThreshold ? 1 : 0);
            if(++count < 32) continue;
            hash = Mix(hash, bits);
            bits = 0;
            count = 0;
        }
        hash = Mix(hash, bits);
        return (int)hash;
    }
}

[CustomEditor(typeof(PixelLayerCollider))]
public class PixelLayerColliderEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var t = (PixelLayerCollider)target;
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        var changed = EditorGUI.EndChangeCheck();
        if(changed && t.transform.Find(PixelLayerCollider.ChildName) != null) PixelLayerColliderBuilder.Rebuild(t, false, true);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Rebuild")) PixelLayerColliderBuilder.Rebuild(t, true, true);
        if(GUILayout.Button("Clear")) PixelLayerColliderBuilder.ClearAll(t);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(Info(t), EditorStyles.miniLabel);
    }

    static string Info(PixelLayerCollider t)
    {
        var child = t.transform.Find(PixelLayerCollider.ChildName);
        if(child == null) return "Not built";
        var boxes = child.GetComponents<BoxCollider2D>().Length;
        var poly = child.GetComponent<PolygonCollider2D>();
        if(poly == null) return boxes + " boxes";
        var points = 0;
        for(var i = 0; i < poly.pathCount; i++) points += poly.GetPath(i).Length;
        return poly.pathCount + " paths, " + points + " points";
    }
}
#endif
