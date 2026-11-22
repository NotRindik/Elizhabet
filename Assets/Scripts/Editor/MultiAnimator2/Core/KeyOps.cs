using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiAnimator2
{
    public static class KeyOps
    {
        private const float Tolerance = 0.0005f;

        public static int FindKey(Keyframe[] keys, float time)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Mathf.Abs(keys[i].time - time) < Tolerance)
                    return i;
            return -1;
        }

        public static int FindKey(ObjectReferenceKeyframe[] keys, float time)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Mathf.Abs(keys[i].time - time) < Tolerance)
                    return i;
            return -1;
        }

        public static List<KeyRef> KeysAt(AnimationClip clip, PropertyGroup group, float time)
        {
            var result = new List<KeyRef>();
            if (clip == null || group == null)
                return result;

            foreach (var b in group.Bindings)
            {
                if (group.IsObject)
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                    if (keys == null)
                        continue;
                    int i = FindKey(keys, time);
                    if (i >= 0)
                        result.Add(new KeyRef(clip, b, keys[i].time, true));
                }
                else
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve == null)
                        continue;
                    var keys = curve.keys;
                    int i = FindKey(keys, time);
                    if (i >= 0)
                        result.Add(new KeyRef(clip, b, keys[i].time, false));
                }
            }
            return result;
        }

        public static bool Exists(KeyRef key)
        {
            if (key.Clip == null)
                return false;

            if (key.IsObject)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(key.Clip, key.Binding);
                return keys != null && FindKey(keys, key.Time) >= 0;
            }

            var curve = AnimationUtility.GetEditorCurve(key.Clip, key.Binding);
            return curve != null && FindKey(curve.keys, key.Time) >= 0;
        }

        public static CurveSnapshot Capture(CurveId id)
        {
            var snapshot = new CurveSnapshot();
            if (id.IsObject)
            {
                snapshot.ObjectKeys = AnimationUtility.GetObjectReferenceCurve(id.Clip, id.Binding)
                                      ?? Array.Empty<ObjectReferenceKeyframe>();
            }
            else
            {
                var curve = AnimationUtility.GetEditorCurve(id.Clip, id.Binding);
                if (curve != null)
                {
                    snapshot.Keys = curve.keys;
                    snapshot.PreWrap = curve.preWrapMode;
                    snapshot.PostWrap = curve.postWrapMode;
                }
            }
            return snapshot;
        }

        public static Dictionary<CurveId, CurveSnapshot> Snapshot(IEnumerable<KeyRef> keys)
        {
            var result = new Dictionary<CurveId, CurveSnapshot>();
            foreach (var k in keys)
            {
                var id = k.Curve;
                if (!result.ContainsKey(id))
                    result[id] = Capture(id);
            }
            return result;
        }

        private static Dictionary<CurveId, HashSet<int>> GroupByCurve(IEnumerable<KeyRef> keys)
        {
            var result = new Dictionary<CurveId, HashSet<int>>();
            foreach (var k in keys)
            {
                if (!result.TryGetValue(k.Curve, out var set))
                    result[k.Curve] = set = new HashSet<int>();
                set.Add(k.TimeKey);
            }
            return result;
        }

        public static List<KeyRef> ApplyMove(Dictionary<CurveId, CurveSnapshot> originals,
            IEnumerable<KeyRef> keys, float delta, string undoName)
        {
            var moved = new List<KeyRef>();

            foreach (var (id, times) in GroupByCurve(keys))
            {
                if (!originals.TryGetValue(id, out var snapshot))
                    continue;

                Undo.RecordObject(id.Clip, undoName);

                if (id.IsObject)
                {
                    var keep = new List<ObjectReferenceKeyframe>();
                    var move = new List<ObjectReferenceKeyframe>();
                    foreach (var k in snapshot.ObjectKeys)
                        (times.Contains(TimeKeys.Of(k.time)) ? move : keep).Add(k);

                    var targets = new HashSet<int>();
                    for (int i = 0; i < move.Count; i++)
                    {
                        var k = move[i];
                        k.time = Mathf.Max(0f, k.time + delta);
                        move[i] = k;
                        targets.Add(TimeKeys.Of(k.time));
                        moved.Add(new KeyRef(id.Clip, id.Binding, k.time, true));
                    }

                    keep.RemoveAll(k => targets.Contains(TimeKeys.Of(k.time)));
                    keep.AddRange(move);
                    keep.Sort((a, b) => a.time.CompareTo(b.time));
                    AnimationUtility.SetObjectReferenceCurve(id.Clip, id.Binding, keep.ToArray());
                }
                else
                {
                    var keep = new List<Keyframe>();
                    var move = new List<Keyframe>();
                    foreach (var k in snapshot.Keys)
                        (times.Contains(TimeKeys.Of(k.time)) ? move : keep).Add(k);

                    var targets = new HashSet<int>();
                    for (int i = 0; i < move.Count; i++)
                    {
                        var k = move[i];
                        k.time = Mathf.Max(0f, k.time + delta);
                        move[i] = k;
                        targets.Add(TimeKeys.Of(k.time));
                        moved.Add(new KeyRef(id.Clip, id.Binding, k.time, false));
                    }

                    keep.RemoveAll(k => targets.Contains(TimeKeys.Of(k.time)));
                    keep.AddRange(move);
                    keep.Sort((a, b) => a.time.CompareTo(b.time));
                    var curve = new AnimationCurve(keep.ToArray())
                    {
                        preWrapMode = snapshot.PreWrap,
                        postWrapMode = snapshot.PostWrap
                    };
                    AnimationUtility.SetEditorCurve(id.Clip, id.Binding, curve);
                }

                EditorUtility.SetDirty(id.Clip);
            }

            return moved;
        }

        public static void Delete(IEnumerable<KeyRef> keys)
        {
            foreach (var (id, times) in GroupByCurve(keys))
            {
                Undo.RecordObject(id.Clip, "Delete Keys");

                if (id.IsObject)
                {
                    var keysArr = AnimationUtility.GetObjectReferenceCurve(id.Clip, id.Binding);
                    if (keysArr == null)
                        continue;
                    var kept = new List<ObjectReferenceKeyframe>();
                    foreach (var k in keysArr)
                        if (!times.Contains(TimeKeys.Of(k.time)))
                            kept.Add(k);
                    AnimationUtility.SetObjectReferenceCurve(id.Clip, id.Binding, kept.ToArray());
                }
                else
                {
                    var curve = AnimationUtility.GetEditorCurve(id.Clip, id.Binding);
                    if (curve == null)
                        continue;
                    var kept = new List<Keyframe>();
                    foreach (var k in curve.keys)
                        if (!times.Contains(TimeKeys.Of(k.time)))
                            kept.Add(k);
                    curve.keys = kept.ToArray();
                    AnimationUtility.SetEditorCurve(id.Clip, id.Binding, curve);
                }

                EditorUtility.SetDirty(id.Clip);
            }
        }

        public static List<ClipboardEntry> Copy(IEnumerable<KeyRef> keys)
        {
            var result = new List<ClipboardEntry>();
            foreach (var k in keys)
            {
                if (k.IsObject)
                {
                    var arr = AnimationUtility.GetObjectReferenceCurve(k.Clip, k.Binding);
                    if (arr == null)
                        continue;
                    int i = FindKey(arr, k.Time);
                    if (i < 0)
                        continue;
                    result.Add(new ClipboardEntry
                        { Binding = k.Binding, IsObject = true, Value = arr[i].value, Time = arr[i].time });
                }
                else
                {
                    var curve = AnimationUtility.GetEditorCurve(k.Clip, k.Binding);
                    if (curve == null)
                        continue;
                    var arr = curve.keys;
                    int i = FindKey(arr, k.Time);
                    if (i < 0)
                        continue;
                    result.Add(new ClipboardEntry
                        { Binding = k.Binding, IsObject = false, Key = arr[i], Time = arr[i].time });
                }
            }
            result.Sort((a, b) => a.Time.CompareTo(b.Time));
            return result;
        }

        public static List<KeyRef> Paste(IReadOnlyList<ClipboardEntry> entries, AnimationClip target, float time)
        {
            var pasted = new List<KeyRef>();
            if (target == null || entries.Count == 0)
                return pasted;

            Undo.RecordObject(target, "Paste Keys");
            float origin = entries[0].Time;
            var floatCurves = new Dictionary<EditorCurveBinding, AnimationCurve>();
            var objectCurves = new Dictionary<EditorCurveBinding, List<ObjectReferenceKeyframe>>();

            foreach (var e in entries)
            {
                float t = Mathf.Max(0f, time + e.Time - origin);

                if (e.IsObject)
                {
                    if (!objectCurves.TryGetValue(e.Binding, out var list))
                    {
                        var existing = AnimationUtility.GetObjectReferenceCurve(target, e.Binding);
                        objectCurves[e.Binding] = list = existing != null
                            ? new List<ObjectReferenceKeyframe>(existing)
                            : new List<ObjectReferenceKeyframe>();
                    }
                    list.RemoveAll(k => TimeKeys.Of(k.time) == TimeKeys.Of(t));
                    list.Add(new ObjectReferenceKeyframe { time = t, value = e.Value });
                }
                else
                {
                    if (!floatCurves.TryGetValue(e.Binding, out var curve))
                        floatCurves[e.Binding] = curve =
                            AnimationUtility.GetEditorCurve(target, e.Binding) ?? new AnimationCurve();
                    int existingIndex = FindKey(curve.keys, t);
                    if (existingIndex >= 0)
                        curve.RemoveKey(existingIndex);
                    var key = e.Key;
                    key.time = t;
                    curve.AddKey(key);
                }

                pasted.Add(new KeyRef(target, e.Binding, t, e.IsObject));
            }

            foreach (var (binding, curve) in floatCurves)
                AnimationUtility.SetEditorCurve(target, binding, curve);

            foreach (var (binding, list) in objectCurves)
            {
                list.Sort((a, b) => a.time.CompareTo(b.time));
                AnimationUtility.SetObjectReferenceCurve(target, binding, list.ToArray());
            }

            EditorUtility.SetDirty(target);
            return pasted;
        }

        public static List<KeyRef> AddKeys(AnimationClip clip, IEnumerable<PropertyGroup> groups, float time)
        {
            var added = new List<KeyRef>();
            if (clip == null)
                return added;

            Undo.RecordObject(clip, "Add Keys");

            foreach (var g in groups)
            {
                foreach (var b in g.Bindings)
                {
                    if (g.IsObject)
                    {
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                        if (keys == null || keys.Length == 0)
                            continue;
                        if (FindKey(keys, time) < 0)
                        {
                            Object value = keys[0].value;
                            foreach (var k in keys)
                                if (k.time <= time)
                                    value = k.value;
                            var list = new List<ObjectReferenceKeyframe>(keys)
                                { new() { time = time, value = value } };
                            list.Sort((x, y) => x.time.CompareTo(y.time));
                            AnimationUtility.SetObjectReferenceCurve(clip, b, list.ToArray());
                        }
                        added.Add(new KeyRef(clip, b, time, true));
                    }
                    else
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, b);
                        if (curve == null)
                            continue;
                        if (FindKey(curve.keys, time) < 0)
                        {
                            curve.AddKey(new Keyframe(time, curve.Evaluate(time)));
                            AnimationUtility.SetEditorCurve(clip, b, curve);
                        }
                        added.Add(new KeyRef(clip, b, time, false));
                    }
                }
            }

            EditorUtility.SetDirty(clip);
            return added;
        }

        public static bool TryGetFloat(KeyRef key, out float value)
        {
            value = 0f;
            var curve = AnimationUtility.GetEditorCurve(key.Clip, key.Binding);
            if (curve == null)
                return false;
            var keys = curve.keys;
            int i = FindKey(keys, key.Time);
            if (i < 0)
                return false;
            value = keys[i].value;
            return true;
        }

        public static void SetFloat(KeyRef key, float value)
        {
            var curve = AnimationUtility.GetEditorCurve(key.Clip, key.Binding);
            if (curve == null)
                return;
            int i = FindKey(curve.keys, key.Time);
            if (i < 0)
                return;
            Undo.RecordObject(key.Clip, "Change Key Value");
            var k = curve.keys[i];
            k.value = value;
            curve.MoveKey(i, k);
            AnimationUtility.SetEditorCurve(key.Clip, key.Binding, curve);
            EditorUtility.SetDirty(key.Clip);
        }

        public static bool TryGetObject(KeyRef key, out Object value)
        {
            value = null;
            var keys = AnimationUtility.GetObjectReferenceCurve(key.Clip, key.Binding);
            if (keys == null)
                return false;
            int i = FindKey(keys, key.Time);
            if (i < 0)
                return false;
            value = keys[i].value;
            return true;
        }

        public static void SetObject(KeyRef key, Object value)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(key.Clip, key.Binding);
            if (keys == null)
                return;
            int i = FindKey(keys, key.Time);
            if (i < 0)
                return;
            Undo.RecordObject(key.Clip, "Change Key Value");
            keys[i].value = value;
            AnimationUtility.SetObjectReferenceCurve(key.Clip, key.Binding, keys);
            EditorUtility.SetDirty(key.Clip);
        }
    }
}
